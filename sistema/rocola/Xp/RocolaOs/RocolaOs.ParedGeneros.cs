// LA PARED CON EL CATALOGO (fase 11, grupo pared-generos; FASE11_pared-generos.md). Peticion del dueno del 14-11-2026 («que cada letra
// tuviera 500 paginas juntando mi musica local y la de Spotify [...] poniendo los artistas y discos mas populares») CON SU DECISION DEL
// 20-11-2026, que manda sobre el orden por genero de antes: «ya no se ordenara por genero, creo que seria malo cambiar la distribucion actual
// a la que la gente ya esta acostumbrada, mejor ahora cada disco tendra al lado la pastilla que diga de que genero es como un dato extra [...]
// se mostraran discos de Spotify siguiendo la coherencia que ya hay, y priorizando a los mejores artistas». Una parte de Form1.
//
// CON EL MODO SPOTIFY ENCENDIDO, LA LLAVE DE LA API Y EL CATALOGO PRESENTE (RocolaOs.ParedGenerosCatalogo.cs), cada letra (# y de la A a la Z)
// junta los discos de XP y los del catalogo EN EL ORDEN DE XP (Form1.Biblioteca.cs, BuildLibrary: por artista y luego por disco, con
// string.Compare(..., OrdinalIgnoreCase)), INTERCALADOS:
//   - tus discos entran todos y en el orden de XP (la lista de XP es la de siempre: solo se le meten discos en medio);
//   - del catalogo, hasta llenar 500 paginas (18 discos cada una, 9 000 por letra contando los tuyos), los primeros DEL ARCHIVO: el archivo va
//     del artista mas famoso al menos famoso, asi que si no caben todos entran primero los de los artistas mas famosos;
//   - un artista del catalogo con el mismo nombre que uno tuyo (sin acentos ni mayusculas: MotorBusqueda.Normaliza, la de XP) va con el tuyo
//     (se ordena con tu nombre), y un disco del catalogo que ya tienes (mismo nombre de disco, sin acentos ni mayusculas) no se repite;
//   - a igualdad de artista y disco, lo tuyo antes.
// La lista de una letra se arma en un hilo (nunca en la pantalla) y entra de una pieza; la eleccion se queda en el mismo disco.
// En la memoria, solo las listas de la letra que se mira y de 3 mas (las de al lado y la ultima).
//
// LA PASTILLA DEL GENERO EN CADA CASILLA (el dato extra): la de XP (la de HorneaGenero/ColorDeEtiqueta: relleno tenue del color del genero,
// borde fino, texto aclarado), horneada DENTRO del mapa de la casilla (como el palomito de XP: no cuesta nada por cuadro), abajo a la
// izquierda de la portada. En tus discos, el genero que XP ya sabe de ellos (GeneroDeCancion: la tabla de discos, la de artistas, lo que dice
// el nombre); si no lo sabe («Otros»), sin pastilla. En los del catalogo, el de tus discos de ese artista si lo tienes, si no el del catalogo.
// Ganchos g91 (la clave de la casilla), g92 (la casilla por procesador) y g94-g98 (la casilla de la tarjeta).
//
// SIN EL MODO O SIN CATALOGO, la pared de XP tal cual, sin pastillas (cada gancho devuelve lo de XP; ningun hilo ni archivo).
// La pared NO pregunta nada a la API de Spotify: solo al abrir un disco del catalogo (1 peticion con su id; sin id, 1 busqueda y el disco).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using SkiaSharp;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
        internal const string RocolaOsPgFuncion = "pared";
        internal const int RocolaOsPgPaginasMax = 500;
        private const int RocolaOsPgListasEnMemoria = 4;
        private const string RocolaOsPgCarpeta = RocolaOsSpotifyCarpeta + "catalogo:";

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraParedGeneros()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsPgFuncion,
                Que = "las letras con el catalogo (solo con el modo Spotify): los discos de Spotify intercalados en el orden de XP, hasta 500 paginas por letra, y la pastilla del genero en cada disco",
                DeFabrica = true,
                Arranca = f => f.RocolaOsPgArranca(),
                Latido = f => f.RocolaOsPgLatido(),
                Vivo = (f, sb) => f.RocolaOsPgVivo(sb),
            });
        }

        // UNA LETRA YA ARMADA (inmutable salvo al esconder un disco: entonces se cambia entera)
        private sealed class RocolaOsPgLetra
        {
            internal char Letra;
            internal List<Album> Lista;
            internal string[] Generos;                                  // la pastilla de cada casilla (null: sin pastilla)
            internal Dictionary<Album, string> Pastillas;               // lo mismo por disco (solo los que llevan): lo pregunta la casilla
            internal Dictionary<Album, RocolaOsPgDisco> Catalogo;       // las del catalogo
            internal int Locales, DelCatalogo;
            internal long Ms;
            internal RocolaOsPgCatalogo De;
            internal List<Album> AlbumsDeXp;
        }
        // lo que la pantalla le da al hilo que arma (una copia: el hilo no toca nada de XP)
        private sealed class RocolaOsPgEncargo
        {
            internal char Letra;
            internal RocolaOsPgCatalogo Cat;
            internal List<Album> Locales;
            internal string[] GenerosLocales;                           // el genero de XP de cada uno (null: XP no lo sabe)
            internal HashSet<string> Ocultos;
            internal List<Album> AlbumsDeXp;
            internal string Volcado;
            internal int Numero;
        }

        private RocolaOsPgCatalogo rocolaOsPgCat;
        private List<RocolaOsPgLetra> rocolaOsPgListas;                   // la mas reciente al final
        private Dictionary<char, int> rocolaOsPgPedidas;                  // letra -> el numero del pedido en camino (el ultimo vale)
        private int rocolaOsPgPedidoN;
        private HashSet<string> rocolaOsPgOcultos;
        private Dictionary<string, List<Album>> rocolaOsPgEsperan;        // mbid -> los discos que esperan su portada
        private HashSet<Album> rocolaOsPgAbriendo;
        private List<Album> rocolaOsPgAlbumsXp;                           // la biblioteca de XP con que se armaron (otra tras re-escanear)
        private char rocolaOsPgUltimaLetra;
        private string rocolaOsPgUltimaPagina;
        private bool rocolaOsPgArrancada, rocolaOsPgHayLlave, rocolaOsPgDescargando;
        private bool rocolaOsPgSinPastillas;                              // (pruebas, ROCOLA_PARED_SIN_PASTILLAS=1: la misma pared sin pastillas, para comparar)
        private bool rocolaOsPgSoloMide;                                  // (pruebas, ROCOLA_PARED_MIDE=1 con el modo apagado: solo se miden los cuadros)
        // LO QUE PREGUNTAN LAS CASILLAS (solo la pantalla): las letras armadas que valen ahora (null sin el modo, sin la llave o sin catalogo)
        private static RocolaOsPgLetra[] rocolaOsPgVista;
        // lo que se mide (para el estado y las pruebas)
        private long rocolaOsPgArmadas, rocolaOsPgArmadasMsMax, rocolaOsPgCambios, rocolaOsPgCambiosPagina, rocolaOsPgMemMax, rocolaOsPgGcMax;
        private long rocolaOsPgUltimoCuadro, rocolaOsPgUltimoCambio;
        private int[] rocolaOsPgCuadros, rocolaOsPgCuadrosHojeo;
        private long rocolaOsPgApiAlEmpezar = -1;
        private static long rocolaOsPgHorneadas;                          // casillas horneadas con pastilla (estado)
        private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> rocolaOsPgListos = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        // ---------- si va ----------
        // (con la llave de la API: decision del dueno del 15-11-2026, «encendido sin llaves la rocola debe quedar como XP en lo que se ve y
        // suena»; sin ella tampoco se podria abrir un disco del catalogo)
        private bool RocolaOsPgVa { get { return rocolaOsPgCat != null && rocolaOsPgHayLlave && RocolaOs.Activo && RocolaOs.Encendida(RocolaOsPgFuncion) && RocolaOsSpotifyCatalogoVa; } }
        private static bool RocolaOsPgLetraSuya(char c) { return c == '#' || (c >= 'A' && c <= 'Z'); }

        private void RocolaOsPgArranca()
        {
            // solo con el modo Spotify: sin el, ni hilos ni archivos
            if (!RocolaOs.Encendida("spotify"))
            {
                RocolaOs.Apunta("pared_generos", "el modo Spotify esta apagado: la pared es la de XP");
                string mide = null;
                try { mide = Environment.GetEnvironmentVariable("ROCOLA_PARED_MIDE"); } catch { }
                if (mide == "1") { rocolaOsPgSoloMide = true; rocolaOsPgCuadros = new int[1001]; rocolaOsPgCuadrosHojeo = new int[1001]; }
                return;
            }
            rocolaOsPgArrancada = true;
            rocolaOsPgListas = new List<RocolaOsPgLetra>();
            rocolaOsPgPedidas = new Dictionary<char, int>();
            rocolaOsPgOcultos = new HashSet<string>(StringComparer.Ordinal);
            rocolaOsPgEsperan = new Dictionary<string, List<Album>>(StringComparer.OrdinalIgnoreCase);
            rocolaOsPgAbriendo = new HashSet<Album>();
            rocolaOsPgCuadros = new int[1001]; rocolaOsPgCuadrosHojeo = new int[1001];
            try { rocolaOsPgSinPastillas = Environment.GetEnvironmentVariable("ROCOLA_PARED_SIN_PASTILLAS") == "1"; } catch { }
            RocolaOs.Apunta("pared_generos", "encendida: el catalogo en " + RocolaOsPgDescarga.Carpeta + " (de " + RocolaOsPgDescarga.Url + "), en cuanto haya la llave de la API");
        }

        // EL LATIDO (1 s, en la pantalla): el catalogo nuevo, lo armado que no pudo encargarse, las casillas y el estado
        private int rocolaOsPgLatidos;
        private void RocolaOsPgLatido()
        {
            if (!rocolaOsPgArrancada && !rocolaOsPgSoloMide) return;
            long mem = Environment.WorkingSet, gc = GC.GetTotalMemory(false);
            if (mem > rocolaOsPgMemMax) rocolaOsPgMemMax = mem;
            if (gc > rocolaOsPgGcMax) rocolaOsPgGcMax = gc;
            if (rocolaOsPgSoloMide) { if (++rocolaOsPgLatidos % 2 == 0) RocolaOsPgEscribeEstado(); return; }
            // LA LLAVE DE LA API (el archivo se lee como mucho cada 5 s): sin ella, ni se baja el catalogo ni hay pared con el catalogo
            bool llave = RocolaOsSpotifyCatalogoApi.HayLlave;
            if (llave && !rocolaOsPgDescargando)
            {
                rocolaOsPgDescargando = true;
                RocolaOsPgDescarga.Arranca();
                RocolaOsPgPortadas.Arranca(RocolaOsPgDescarga.Carpeta);
            }
            if (llave != rocolaOsPgHayLlave)
            {
                rocolaOsPgHayLlave = llave;
                if (rocolaOsPgCat != null && letterBar != null && !libBuilding)
                {
                    RecalculaLetras();
                    if (RocolaOsPgLetraSuya(currentLetter)) { if (llave) RocolaOsPgPideConVecinas(currentLetter); else if (panelSongs != null && !panelSongs.Visible) RefreshAlbums(); }
                }
            }
            Action a;
            while (rocolaOsPgListos.TryDequeue(out a)) a();
            RocolaOsPgCatalogo c = RocolaOsPgDescarga.Actual;
            if (c != null && !ReferenceEquals(c, rocolaOsPgCat)) RocolaOsPgLlegoCatalogo(c);
            RocolaOsPgPublica();   // («spotify apagar», la llave quitada...: las casillas sin pastilla al momento)
            if (++rocolaOsPgLatidos % 2 == 0) RocolaOsPgEscribeEstado();
        }

        private void RocolaOsPgLlegoCatalogo(RocolaOsPgCatalogo c)
        {
            bool primero = rocolaOsPgCat == null;
            rocolaOsPgCat = c;
            rocolaOsPgPedidas.Clear();   // (las de un catalogo de antes se siguen viendo hasta que llegue la nueva de cada una: de una pieza)
            if (rocolaOsPgApiAlEmpezar < 0) rocolaOsPgApiAlEmpezar = RocolaOsSpotifyCatalogoApi.Peticiones;
            if (!RocolaOsPgVa) return;
            if (letterBar != null && !libBuilding) RecalculaLetras();   // las letras que solo trae el catalogo
            if (RocolaOsPgLetraSuya(currentLetter)) RocolaOsPgPideConVecinas(currentLetter);
            if (!primero) RocolaOs.Apunta("pared_generos", "catalogo nuevo: las letras se arman otra vez");
        }

        // el genero de XP de un disco tuyo (lo que ya sabe: GeneroDeCancion), o null si no lo sabe; se toma cada vez que se arma su letra
        private string RocolaOsPgGeneroLocal(Album a)
        {
            string g = GeneroDeCancion(null, a);
            return string.IsNullOrEmpty(g) || g == "Otros" ? null : g;
        }

        // ================================================================= g90: la lista de la letra ===================================
        // g90 (Form1.Biblioteca.cs, CurrentList: «return list;»): LA LISTA DE UNA LETRA. Sin el modo o sin catalogo, la de XP. Con ellos, la
        // armada (si ya esta) o la de XP mientras se arma.
        private List<Album> RocolaOsParedGenerosLista(List<Album> xp)
        {
            if (!rocolaOsPgArrancada || !RocolaOsPgLetraSuya(currentLetter) || !RocolaOsPgVa) return xp;
            if (!ReferenceEquals(albums, rocolaOsPgAlbumsXp)) { rocolaOsPgListas.Clear(); rocolaOsPgPedidas.Clear(); rocolaOsPgAlbumsXp = albums; RocolaOsPgPublica(); }
            char l = currentLetter;
            if (l != rocolaOsPgUltimaLetra) { rocolaOsPgUltimaLetra = l; RocolaOsPgPideConVecinas(l); }
            RocolaOsPgLetra e = RocolaOsPgEnMemoria(l);
            if (e != null)
            {
                if (!ReferenceEquals(e.De, rocolaOsPgCat)) RocolaOsPgPide(l);   // (de un catalogo de antes: se ve hasta que llegue la nueva)
                return e.Lista;
            }
            RocolaOsPgPide(l);
            return xp;
        }
        private RocolaOsPgLetra RocolaOsPgEnMemoria(char l)
        {
            List<RocolaOsPgLetra> ls = rocolaOsPgListas;
            for (int i = ls.Count - 1; i >= 0; i--)
                if (ls[i].Letra == l)
                {
                    RocolaOsPgLetra e = ls[i];
                    if (i != ls.Count - 1) { ls.RemoveAt(i); ls.Add(e); }
                    return e;
                }
            return null;
        }
        private RocolaOsPgLetra RocolaOsPgMirada()
        {
            if (rocolaOsPgListas == null || !RocolaOsPgLetraSuya(currentLetter)) return null;
            for (int i = rocolaOsPgListas.Count - 1; i >= 0; i--) if (rocolaOsPgListas[i].Letra == currentLetter) return rocolaOsPgListas[i];
            return null;
        }
        // la letra y sus vecinas (las de REGRESAR+izq/der y las de pasar de la ultima pagina): asi casi nunca se ve la de XP mientras se arma
        private void RocolaOsPgPideConVecinas(char l)
        {
            RocolaOsPgPide(l);
            string orden = LetrasOrden;
            int i = orden.IndexOf(l);
            if (i < 0) return;
            for (int dir = 1; dir >= -1; dir -= 2)
                for (int k = 1; k < orden.Length; k++)
                {
                    char c = orden[((i + dir * k) % orden.Length + orden.Length) % orden.Length];
                    if (letterAvailable == null || !letterAvailable.Contains(c)) continue;
                    if (RocolaOsPgLetraSuya(c)) RocolaOsPgPide(c);
                    break;
                }
        }
        private void RocolaOsPgPide(char l)
        {
            if (rocolaOsPgCat == null || libBuilding || albums == null || rocolaOsPgPedidas.ContainsKey(l)) return;
            RocolaOsPgLetra ya = RocolaOsPgEnMemoria(l);
            if (ya != null && ReferenceEquals(ya.De, rocolaOsPgCat)) return;
            List<Album> loc;
            if (!byLetter.TryGetValue(l, out loc) || loc == null) loc = new List<Album>();
            loc = new List<Album>(loc);
            string[] gen = new string[loc.Count];
            for (int i = 0; i < loc.Count; i++) gen[i] = RocolaOsPgGeneroLocal(loc[i]);
            string vol = null;
            try { vol = Environment.GetEnvironmentVariable("ROCOLA_PARED_VOLCADO"); } catch { }
            RocolaOsPgEncargo e = new RocolaOsPgEncargo
            {
                Letra = l, Cat = rocolaOsPgCat, Locales = loc, GenerosLocales = gen,
                Ocultos = new HashSet<string>(rocolaOsPgOcultos, StringComparer.Ordinal), AlbumsDeXp = albums, Volcado = string.IsNullOrEmpty(vol) ? null : vol,
            };
            e.Numero = ++rocolaOsPgPedidoN;
            rocolaOsPgPedidas[l] = e.Numero;
            RocolaOsPgArmador.Encola(e);
        }

        // EL HILO QUE ARMA (uno; lo que se pide se arma en orden): nada de la pantalla ni de XP, solo la copia
        private static class RocolaOsPgArmador
        {
            private static readonly object cerrojo = new object();
            private static readonly Queue<RocolaOsPgEncargo> cola = new Queue<RocolaOsPgEncargo>();
            private static Thread hilo;
            internal static void Encola(RocolaOsPgEncargo e)
            {
                lock (cerrojo)
                {
                    cola.Enqueue(e);
                    if (hilo == null) hilo = RocolaOs.Hilo("pared_generos", Corre);
                    Monitor.PulseAll(cerrojo);
                }
            }
            private static void Corre()
            {
                while (true)
                {
                    RocolaOsPgEncargo e;
                    lock (cerrojo) { while (cola.Count == 0) Monitor.Wait(cerrojo); e = cola.Dequeue(); }
                    RocolaOsPgLetra r = null;
                    try { r = RocolaOsPgArma(e); }
                    catch (Exception ex) { RocolaOs.Falla("pared_generos", "al armar la letra " + e.Letra, ex); }
                    RocolaOsPgLetra listo = r; char letra = e.Letra; int numero = e.Numero;
                    Action a = delegate { Form1 f = RocolaOs.LaRocola; if (f != null) f.RocolaOsPgLlegoLetra(letra, numero, listo); };
                    if (!RocolaOs.Encarga("pared_generos:letra", a)) rocolaOsPgListos.Enqueue(a);   // (la ventana aun sin ensenarse: el latido)
                }
            }
        }

        // un disco del catalogo que entra: con que nombre de artista se ordena (el tuyo si es el mismo artista), su pastilla y su lugar en el archivo
        private sealed class RocolaOsPgEntrada
        {
            internal RocolaOsPgDisco D;
            internal string Artista, Pastilla;
            internal int N;
        }
        // EL ORDEN DE XP (Form1.Biblioteca.cs, BuildLibrary: «albums.Sort»): artista y luego disco, string.Compare con OrdinalIgnoreCase
        private static int RocolaOsPgOrdenXp(string artA, string disA, string artB, string disB)
        {
            int c = string.Compare(artA, artB, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(disA, disB, StringComparison.OrdinalIgnoreCase);
        }
        // ARMAR UNA LETRA (en el hilo que arma)
        private static RocolaOsPgLetra RocolaOsPgArma(RocolaOsPgEncargo e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            List<RocolaOsPgArtista> arts = RocolaOsPgLeeLetra(e.Cat, e.Letra);
            // tus artistas: su nombre (el primero en el orden de XP) y el genero que mas se repite entre tus discos de el (sin los que XP no sabe)
            Dictionary<string, string> nombreTuyo = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, Dictionary<string, int>> votos = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            HashSet<string> tuyos = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < e.Locales.Count; i++)
            {
                Album a = e.Locales[i];
                string k = MotorBusqueda.Normaliza(a.Artist ?? "");
                if (!nombreTuyo.ContainsKey(k)) nombreTuyo[k] = a.Artist ?? "";
                tuyos.Add(k + "\n" + MotorBusqueda.Normaliza(a.Name ?? ""));
                // (XP: una carpeta sin « - » es un disco sin nombre de ese artista; cuenta como el disco que se llama como el)
                if (string.IsNullOrEmpty(a.Name)) tuyos.Add(k + "\n" + k);
                string g = e.GenerosLocales[i];
                if (g == null) continue;
                Dictionary<string, int> v;
                if (!votos.TryGetValue(k, out v)) { v = new Dictionary<string, int>(StringComparer.Ordinal); votos[k] = v; }
                int n; v.TryGetValue(g, out n); v[g] = n + 1;
            }
            Dictionary<string, string> generoTuyo = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Dictionary<string, int>> kv in votos)
            {
                string mejor = null; int mejorN = 0;
                foreach (KeyValuePair<string, int> g in kv.Value) if (g.Value > mejorN || (g.Value == mejorN && string.CompareOrdinal(g.Key, mejor) < 0)) { mejor = g.Key; mejorN = g.Value; }
                generoTuyo[kv.Key] = mejor;
            }
            // LO QUE ENTRA DEL CATALOGO: los primeros del archivo (del artista mas famoso al menos) hasta llenar las 500 paginas con los tuyos
            int tope = RocolaOsPgPaginasMax * PageSize;
            int quedan = Math.Max(0, tope - e.Locales.Count);
            List<RocolaOsPgEntrada> entran = new List<RocolaOsPgEntrada>(Math.Min(quedan, e.Cat.Discos));
            foreach (RocolaOsPgArtista ar in arts)
            {
                if (quedan <= 0) break;
                string k = MotorBusqueda.Normaliza(ar.Nombre);
                if (k.Length == 0) continue;
                string suyo, gen;
                if (!nombreTuyo.TryGetValue(k, out suyo)) suyo = ar.Nombre;
                if (!generoTuyo.TryGetValue(k, out gen) || gen == null) gen = ar.Genero == "Otros" || string.IsNullOrEmpty(ar.Genero) ? null : ar.Genero;
                foreach (RocolaOsPgDisco d in ar.Discos)
                {
                    if (quedan <= 0) break;
                    if (e.Ocultos.Contains(d.Clave) || tuyos.Contains(k + "\n" + MotorBusqueda.Normaliza(d.Nombre))) continue;
                    entran.Add(new RocolaOsPgEntrada { D = d, Artista = suyo, Pastilla = gen, N = entran.Count });
                    quedan--;
                }
            }
            // en el orden de XP (a igualdad, en el del archivo)
            entran.Sort(delegate (RocolaOsPgEntrada x, RocolaOsPgEntrada y)
            {
                int c = RocolaOsPgOrdenXp(x.Artista, x.D.Nombre, y.Artista, y.D.Nombre);
                return c != 0 ? c : x.N.CompareTo(y.N);
            });
            // INTERCALADOS con los tuyos (que ya vienen en el orden de XP): a igualdad, lo tuyo antes
            RocolaOsPgLetra r = new RocolaOsPgLetra { Letra = e.Letra, De = e.Cat, AlbumsDeXp = e.AlbumsDeXp, Catalogo = new Dictionary<Album, RocolaOsPgDisco>(entran.Count) };
            List<Album> lista = new List<Album>(e.Locales.Count + entran.Count);
            List<string> generos = new List<string>(lista.Capacity);
            Dictionary<Album, string> pastillas = new Dictionary<Album, string>(lista.Capacity);
            int il = 0, ic = 0;
            while (il < e.Locales.Count || ic < entran.Count)
            {
                bool tuyo;
                if (ic >= entran.Count) tuyo = true;
                else if (il >= e.Locales.Count) tuyo = false;
                else
                {
                    Album t = e.Locales[il]; RocolaOsPgEntrada c = entran[ic];
                    tuyo = RocolaOsPgOrdenXp(t.Artist, t.Name, c.Artista, c.D.Nombre) <= 0;
                }
                Album a; string g;
                if (tuyo) { a = e.Locales[il]; g = e.GenerosLocales[il]; il++; }
                else
                {
                    RocolaOsPgEntrada c = entran[ic++];
                    a = new Album { Folder = RocolaOsPgCarpeta + c.D.Clave, Name = c.D.Nombre, Artist = c.D.Art.Nombre, Letter = e.Letra };
                    r.Catalogo[a] = c.D;
                    g = c.Pastilla;
                }
                lista.Add(a); generos.Add(g);
                if (g != null) pastillas[a] = g;
            }
            r.Lista = lista; r.Generos = generos.ToArray(); r.Pastillas = pastillas;
            r.Locales = e.Locales.Count; r.DelCatalogo = entran.Count;
            r.Ms = sw.ElapsedMilliseconds;
            if (e.Volcado != null) RocolaOsPgVuelca(e.Volcado, r, entran);
            return r;
        }
        // (pruebas) ROCOLA_PARED_VOLCADO=CARPETA: cada letra armada, en CARPETA/letra-X.tsv (casilla, pastilla o «-», de donde, artista, disco,
        // clave, el artista con que se ordena)
        private static void RocolaOsPgVuelca(string dir, RocolaOsPgLetra r, List<RocolaOsPgEntrada> entran)
        {
            try
            {
                Dictionary<RocolaOsPgDisco, string> como = new Dictionary<RocolaOsPgDisco, string>(entran.Count);
                foreach (RocolaOsPgEntrada x in entran) como[x.D] = x.Artista;
                Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder(r.Lista.Count * 56);
                for (int i = 0; i < r.Lista.Count; i++)
                {
                    Album a = r.Lista[i];
                    RocolaOsPgDisco d;
                    bool cat = r.Catalogo.TryGetValue(a, out d);
                    sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(r.Generos[i] ?? "-").Append('\t').Append(cat ? "catalogo" : "xp").Append('\t')
                      .Append(a.Artist).Append('\t').Append(a.Name).Append('\t').Append(cat ? d.Clave : a.Folder).Append('\t').Append(cat ? como[d] : a.Artist).Append('\n');
                }
                string nombre = r.Letra == '#' ? "letra-numeral.tsv" : "letra-" + r.Letra + ".tsv";
                File.WriteAllText(Path.Combine(dir, nombre), sb.ToString());
            }
            catch { }
        }

        // LLEGO UNA LETRA ARMADA (en la pantalla): entra de una pieza; si es la que se mira, la eleccion se queda en el mismo disco
        private void RocolaOsPgLlegoLetra(char l, int numero, RocolaOsPgLetra r)
        {
            int n;
            bool ultimo = rocolaOsPgPedidas.TryGetValue(l, out n) && n == numero;
            if (ultimo) rocolaOsPgPedidas.Remove(l);   // (si no, hay uno mas nuevo en camino: este no vale)
            if (r == null || !ultimo) return;
            if (!ReferenceEquals(r.De, rocolaOsPgCat) || !ReferenceEquals(r.AlbumsDeXp, albums) || libBuilding)
            {
                if (l == currentLetter && RocolaOsPgVa) RocolaOsPgPide(l);   // (de un catalogo o una biblioteca de antes: otra vez)
                return;
            }
            rocolaOsPgArmadas++;
            if (r.Ms > rocolaOsPgArmadasMsMax) rocolaOsPgArmadasMsMax = r.Ms;
            RocolaOs.Apunta("pared_generos", "letra " + l + ": " + r.Lista.Count.ToString(CultureInfo.InvariantCulture) + " discos (" + r.Locales.ToString(CultureInfo.InvariantCulture) + " tuyos y "
                + r.DelCatalogo.ToString(CultureInfo.InvariantCulture) + " del catalogo, " + ((r.Lista.Count + PageSize - 1) / PageSize).ToString(CultureInfo.InvariantCulture) + " paginas, "
                + r.Pastillas.Count.ToString(CultureInfo.InvariantCulture) + " con pastilla) armada en " + r.Ms.ToString(CultureInfo.InvariantCulture) + " ms");
            bool mirando = l == currentLetter && RocolaOsPgVa;
            // lo que se veia: la de antes de esta letra (de un catalogo de antes) o la de XP
            RocolaOsPgLetra vieja = null;
            for (int i = rocolaOsPgListas.Count - 1; i >= 0; i--) if (rocolaOsPgListas[i].Letra == l) { vieja = rocolaOsPgListas[i]; rocolaOsPgListas.RemoveAt(i); }
            List<Album> antes = vieja != null ? vieja.Lista : null;
            if (mirando && antes == null && !byLetter.TryGetValue(l, out antes)) antes = null;
            Album elegido = mirando && antes != null && selAlbumIndex > 0 && selAlbumIndex < antes.Count ? antes[selAlbumIndex] : null;
            rocolaOsPgListas.Add(r);
            while (rocolaOsPgListas.Count > RocolaOsPgListasEnMemoria)
            {
                int i = 0;
                while (i < rocolaOsPgListas.Count - 1 && rocolaOsPgListas[i].Letra == currentLetter) i++;
                rocolaOsPgListas.RemoveAt(i);
            }
            RocolaOsPgPublica();
            if (!mirando) return;
            rocolaOsPgCambios++;
            int nuevo = elegido != null ? r.Lista.IndexOf(elegido) : 0;
            if (nuevo < 0 && elegido != null && elegido.Folder != null)   // (uno del catalogo de antes: el mismo disco del nuevo, por su clave)
                for (int i = 0; i < r.Lista.Count; i++) if (string.Equals(r.Lista[i].Folder, elegido.Folder, StringComparison.Ordinal)) { nuevo = i; break; }
            if (nuevo < 0) nuevo = 0;
            selAlbumIndex = nuevo; page = nuevo / PageSize;
            if (panelSongs != null && panelSongs.Visible) return;   // (dentro de un disco: al volver, XP pone la pared en su sitio)
            RefreshAlbums();
        }

        // ================================================================= g47: la pagina que se mira ===================================
        // (desde g47, RefreshAlbums): las portadas de la pagina que se mira y lo que se mide al hojear
        private void RocolaOsPgRefresco()
        {
            if (rocolaOsPgSoloMide) { string s0 = currentLetter + "|" + page.ToString(CultureInfo.InvariantCulture); if (s0 != rocolaOsPgUltimaPagina) { rocolaOsPgUltimaPagina = s0; rocolaOsPgUltimoCambio = Stopwatch.GetTimestamp(); rocolaOsPgCambiosPagina++; } return; }
            if (!rocolaOsPgArrancada) return;
            RocolaOsPgLetra e = RocolaOsPgVa ? RocolaOsPgMirada() : null;
            string sig = currentLetter + "|" + page.ToString(CultureInfo.InvariantCulture);
            if (sig != rocolaOsPgUltimaPagina) { rocolaOsPgUltimaPagina = sig; rocolaOsPgUltimoCambio = Stopwatch.GetTimestamp(); if (e != null) rocolaOsPgCambiosPagina++; }
            if (e == null) return;
            RocolaOsPgPortadas.Mira(currentLetter, page);
            // las portadas: las que ya estan en el disco, de esta pagina y de las de al lado (XP las lee por adelantado); las que faltan, solo
            // de esta (al verse)
            for (int p = page - 1; p <= page + 1; p++)
            {
                int ini = p * PageSize;
                if (ini < 0) continue;
                for (int i = ini; i < ini + PageSize && i < e.Lista.Count; i++)
                {
                    Album a = e.Lista[i];
                    RocolaOsPgDisco d;
                    if (a.CoverPath != null || !e.Catalogo.TryGetValue(a, out d) || !d.Portada || d.Mbid.Length == 0) continue;
                    if (RocolaOsPgPortadas.EnDisco(d.Mbid))
                    {
                        RocolaOsPgPortadas.Usada(d.Mbid);
                        a.CoverPath = RocolaOsPgPortadas.Ruta(d.Mbid);
                        RequestCover(a, p != page, p == page);
                    }
                    else if (p == page)
                    {
                        List<Album> l;
                        if (!rocolaOsPgEsperan.TryGetValue(d.Mbid, out l)) { l = new List<Album>(); rocolaOsPgEsperan[d.Mbid] = l; }
                        if (!l.Contains(a)) l.Add(a);
                        RocolaOsPgPortadas.Pide(d.Mbid, currentLetter, page);
                    }
                }
            }
        }
        internal void RocolaOsPgLlegoPortada(string mbid)
        {
            List<Album> l;
            if (rocolaOsPgEsperan == null || !rocolaOsPgEsperan.TryGetValue(mbid, out l)) return;
            rocolaOsPgEsperan.Remove(mbid);
            string ruta = RocolaOsPgPortadas.Ruta(mbid);
            foreach (Album a in l) { a.CoverPath = ruta; RequestCover(a, false, true); }
            if (albumView != null && albumView.Visible) albumView.Invalidate();
        }

        // ================================================================= la pastilla de cada casilla ================================
        // LAS LETRAS QUE PREGUNTAN LAS CASILLAS: las armadas en la memoria si va (si no, ninguna: sin pastillas). Si cambian, las casillas
        // cambian de clave (g91) y la pared se repinta: las que la necesitan se hornean otra vez, con su presupuesto de cada cuadro (el de XP).
        private void RocolaOsPgPublica()
        {
            RocolaOsPgLetra[] nueva = rocolaOsPgArrancada && !rocolaOsPgSinPastillas && RocolaOsPgVa && rocolaOsPgListas.Count > 0 ? rocolaOsPgListas.ToArray() : null;
            RocolaOsPgLetra[] vieja = rocolaOsPgVista;
            bool igual = (nueva == null) == (vieja == null);
            if (igual && nueva != null)
            {
                if (nueva.Length != vieja.Length) igual = false;
                else
                    for (int i = 0; i < nueva.Length && igual; i++)
                    {
                        bool esta = false;
                        for (int j = 0; j < vieja.Length; j++) if (ReferenceEquals(nueva[i], vieja[j])) { esta = true; break; }
                        igual = esta;
                    }
            }
            if (igual) return;
            rocolaOsPgVista = nueva;
            rocolaOsPgClaves.Clear();
            if (albumView != null && albumView.Visible) albumView.Invalidate();
        }
        // la pastilla de un disco de la pared (null: ninguna)
        private static string RocolaOsPgPastillaDe(Album a)
        {
            RocolaOsPgLetra[] v = rocolaOsPgVista;
            if (v == null || a == null) return null;
            string g;
            for (int i = v.Length - 1; i >= 0; i--) if (v[i].Pastillas.TryGetValue(a, out g)) return g;
            return null;
        }
        // g91 (Form1.Pared.cs, AlbumView.ClaveCasilla: «string k = CellKey(a);»): LA CLAVE DE LA CASILLA lleva su pastilla (la pastilla va
        // horneada dentro, como el palomito: una casilla con pastilla es otro mapa). Sin pastilla, la de XP (la misma cadena). La variante se
        // arma UNA vez por disco (se pide para cada casilla en cada cuadro).
        private sealed class RocolaOsPgClave { internal string De, Pastilla, Clave; }
        private static readonly Dictionary<Album, RocolaOsPgClave> rocolaOsPgClaves = new Dictionary<Album, RocolaOsPgClave>();
        private static string RocolaOsParedGenerosClave(Album a, string k)
        {
            if (k == null || rocolaOsPgVista == null) return k;
            string p = RocolaOsPgPastillaDe(a);
            if (p == null) return k;
            RocolaOsPgClave c;
            if (!rocolaOsPgClaves.TryGetValue(a, out c)) { c = new RocolaOsPgClave(); rocolaOsPgClaves[a] = c; }
            if (!ReferenceEquals(c.De, k) || !ReferenceEquals(c.Pastilla, p)) { c.De = k; c.Pastilla = p; c.Clave = k + "\u0001g:" + p; }
            return c.Clave;
        }
        // LA PASTILLA HORNEADA PARA UNA CASILLA (null: ninguna). Su letra: la familia de la casilla, en negrita como la de la lista del disco de
        // XP (fCola), con el cuerpo segun el lado de la portada (8 pt con el lado de 1024x768).
        private static SKBitmap RocolaOsParedGenerosPastillaCasilla(Album a, int side, string familia)
        {
            string p = RocolaOsPgPastillaDe(a);
            if (p == null) return null;
            float pt = (float)Math.Round(Math.Max(6.5f, Math.Min(12f, side * 0.06f)) * 2f) / 2f;
            return RocolaOsPgHornea(p, familia ?? "", pt);
        }
        // DONDE VA (en las medidas de la casilla, las de BakeCell): abajo a la izquierda de la cara del disco (la que se ve de frente: Cube.Draw
        // con angulo 0, de ancho lado/raiz de 2, el mismo calculo que BakeSeparatedCell), encima de su borde de abajo; si no cabe a lo ancho, en
        // chico (a su medida)
        private static Rectangle RocolaOsPgSitio(SKBitmap p, int cw, int side)
        {
            int left = (int)Math.Round((cw - side) / 2 + side * .5 - side * .5 / Math.Sqrt(2));
            int right = left + (int)Math.Round(side / Math.Sqrt(2));
            int m = Math.Max(3, side / 30);
            int w = p.Width, h = p.Height, cabe = right - left - 2 * m;
            if (cabe < 8) return Rectangle.Empty;
            if (w > cabe) { h = Math.Max(1, (int)Math.Round(h * (double)cabe / w)); w = cabe; }
            return new Rectangle(left + m, side - 2 - m - h, w, h);
        }
        // g94/g95 (CaratulasSeparadasGpu.cs, BakeSeparatedCell): el trozo de la casilla que va encima de la portada (en XP, el del palomito) se
        // agranda para llevar tambien la pastilla. Sin pastilla, el de XP tal cual.
        private static Rectangle RocolaOsParedGenerosMarca(Rectangle mark, SKBitmap p, int cw, int side)
        {
            if (p == null) return mark;
            Rectangle r = RocolaOsPgSitio(p, cw, side);
            if (r.IsEmpty) return mark;
            r.Inflate(1, 1);
            if (r.X < 0) { r.Width += r.X; r.X = 0; }
            return mark.IsEmpty ? r : Rectangle.Union(mark, r);
        }
        // g92 y g98: PINTA LA PASTILLA en el lienzo de la casilla (en sus medidas; el que llama ya movio el lienzo a su sitio). Sin pastilla, nada
        private static void RocolaOsParedGenerosPinta(SKCanvas g, SKBitmap p, int cw, int side)
        {
            if (p == null || g == null) return;
            Rectangle r = RocolaOsPgSitio(p, cw, side);
            if (r.IsEmpty) return;
            Imagenes.Pinta(g, p, new SKRect(0, 0, p.Width, p.Height), new SKRect(r.Left, r.Top, r.Right, r.Bottom), r.Width == p.Width ? Filtro.DeFabrica : Filtro.Bilineal, false, false);
            Interlocked.Increment(ref rocolaOsPgHorneadas);
        }
        // LA PASTILLA DEL GENERO DE XP, horneada como la hornea XP (SongListView.HorneaGenero, Form1.Vistas.cs): el mismo relleno tenue del color
        // del genero, el mismo borde fino, el mismo texto aclarado y la misma medida (texto + 16, alto + 5, radio 7). Lo unico que se le pone
        // debajo es su misma forma en negro a medias: en XP va sobre el fondo oscuro de la lista; aqui va encima de una portada, y sin eso no se
        // leeria sobre una portada clara.
        private static readonly Dictionary<string, SKBitmap> rocolaOsPgHorno = new Dictionary<string, SKBitmap>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Rocola.Nativo.Letra> rocolaOsPgLetras = new Dictionary<string, Rocola.Nativo.Letra>(StringComparer.Ordinal);
        private static SKBitmap RocolaOsPgHornea(string etiqueta, string familia, float pt)
        {
            string clave = etiqueta + "\u0001" + familia + "\u0001" + pt.ToString(CultureInfo.InvariantCulture);
            lock (rocolaOsPgHorno)
            {
                SKBitmap b;
                if (rocolaOsPgHorno.TryGetValue(clave, out b)) return b;
                string cf = familia + "\u0001" + pt.ToString(CultureInfo.InvariantCulture);
                Rocola.Nativo.Letra f;
                if (!rocolaOsPgLetras.TryGetValue(cf, out f)) { f = Rocola.Nativo.Letra.EnPuntos(familia, pt, true); rocolaOsPgLetras[cf] = f; }
                Color c = ColorDeEtiqueta(etiqueta);
                SizeF sz = TextoGdiPlus.Mide(etiqueta, f, AjusteLetra.DelSistema, new SizeF(999, 999999f), FormatoTexto.TipograficoCompartido);
                int w = (int)Math.Ceiling(sz.Width) + 16, h = (int)Math.Ceiling(sz.Height) + 5;
                if (w < 8) w = 8; if (h < 8) h = 8;
                b = Mapas.Nuevo(w, h);
                using (SKCanvas g = new SKCanvas(b))
                {
                    bool suave = true;
                    AjusteLetra ajuste = AjusteLetra.Suave;
                    g.Clear(Color.Transparent.Sk());
                    using (SKPath gp = ContornoRedondo(new Rectangle(0, 0, w - 1, h - 1), Math.Min(7, (h - 1) / 2)))
                    {
                        using (SKPaint fondo = new SKPaint { Color = Color.FromArgb(170, 0, 0, 0).Sk() }) Pixel.Rellena(g, gp, fondo, suave);
                        using (SKPaint sb = new SKPaint { Color = Color.FromArgb(60, c).Sk() }) Pixel.Rellena(g, gp, sb, suave);
                        using (SKPaint pn = new SKPaint { Color = Color.FromArgb(120, c).Sk(), StrokeWidth = 1f }) Pixel.Traza(g, gp, pn, suave);
                    }
                    FormatoTexto sf = new FormatoTexto(); sf.Horizontal = Alinea.Centro; sf.Vertical = Alinea.Centro;
                    sf.Banderas = BanderasTexto.SinPartir;
                    using (SKPaint sb = new SKPaint { Color = Lighten(c, 0.45).Sk() })
                        TextoGdiPlus.Escribe(g, etiqueta, f, ajuste, sb, new RectangleF(0, 0, w, h), sf);
                }
                rocolaOsPgHorno[clave] = b;
                return b;
            }
        }

        // g93 (Form1.Video.cs, UiSig: «sb = Mix(sb, headerView.Section)»): se llama en cada cuadro con la pared delante y devuelve la misma
        // cadena de XP (la firma no cambia): aqui solo se mide lo que tarda cada cuadro (el estado: p50/p95/p99/max, aparte los de mientras se
        // hojea).
        private string RocolaOsParedGenerosFirma(string seccion)
        {
            if (!rocolaOsPgArrancada && !rocolaOsPgSoloMide) return seccion;
            long t = Stopwatch.GetTimestamp();
            if (rocolaOsPgUltimoCuadro != 0)
            {
                long ms = (t - rocolaOsPgUltimoCuadro) * 1000 / Stopwatch.Frequency;
                if (ms < 2000)
                {
                    int b = (int)Math.Min(1000, ms);
                    rocolaOsPgCuadros[b]++;
                    if ((t - rocolaOsPgUltimoCambio) * 1000 / Stopwatch.Frequency < 400 && (rocolaOsPgSoloMide || RocolaOsPgMirada() != null)) rocolaOsPgCuadrosHojeo[b]++;
                }
            }
            rocolaOsPgUltimoCuadro = t;
            return seccion;
        }

        // ================================================================= abrir un disco del catalogo ==================================
        private string RocolaOsPgGeneroDe(Album a)
        {
            if (rocolaOsPgListas == null) return null;
            for (int i = rocolaOsPgListas.Count - 1; i >= 0; i--)
            {
                int k = rocolaOsPgListas[i].Lista.IndexOf(a);
                if (k >= 0) return rocolaOsPgListas[i].Generos[k];
            }
            return null;
        }
        private RocolaOsPgDisco RocolaOsPgDiscoDe(Album a)
        {
            if (a == null || a.Folder == null || rocolaOsPgListas == null || !a.Folder.StartsWith(RocolaOsPgCarpeta, StringComparison.Ordinal)) return null;
            RocolaOsPgDisco d;
            for (int i = rocolaOsPgListas.Count - 1; i >= 0; i--) if (rocolaOsPgListas[i].Catalogo.TryGetValue(a, out d)) return d;
            return null;
        }
        private static bool RocolaOsPgEsDelCatalogo(Album a) { return a != null && a.Folder != null && a.Folder.StartsWith(RocolaOsPgCarpeta, StringComparison.Ordinal); }
        // (desde g49, EnterAlbum) ENTRAR A UN DISCO DEL CATALOGO: sus canciones se piden (con su id, 1 peticion; sin id, 1 busqueda por nombre y
        // artista y el disco) y entran a la lista de XP al llegar, como los de Spotify del catalogo. true = era del catalogo.
        private bool RocolaOsPgEntra(Album a)
        {
            if (!RocolaOsPgEsDelCatalogo(a)) return false;
            RocolaOsPgDisco d = RocolaOsPgDiscoDe(a);
            if (d == null || a.Tracks.Count > 0 || !rocolaOsPgAbriendo.Add(a)) return true;
            Action<object, string> llego = delegate (object r, string error)
            {
                RocolaOs.Encarga("pared_generos:disco", delegate { RocolaOsPgLlegoDisco(a, d, r as RocolaOsSpotifyCatalogoDisco, error); });
            };
            if (d.IdSpotify.Length > 0) RocolaOsSpotifyCatalogoApi.AbreDisco(d.IdSpotify, llego);
            else RocolaOsSpotifyCatalogoApi.BuscaDiscoDelCatalogo(d.Art.Nombre, d.Nombre, delegate (object r, string error)
            {
                RocolaOsSpotifyCatalogoDisco x = r as RocolaOsSpotifyCatalogoDisco;
                if (x == null) { llego(null, error); return; }
                RocolaOsSpotifyCatalogoApi.AbreDisco(x.Id, llego);   // (detras, en la cola del catalogo)
            });
            RocolaOs.Apunta("pared_generos", "se abre un disco del catalogo: «" + d.Art.Nombre + " - " + d.Nombre + "» (" + (d.IdSpotify.Length > 0 ? "con su id" : "sin id: se busca") + ")");
            return true;
        }
        private void RocolaOsPgLlegoDisco(Album a, RocolaOsPgDisco d, RocolaOsSpotifyCatalogoDisco x, string error)
        {
            rocolaOsPgAbriendo.Remove(a);
            bool dentro = panelSongs != null && panelSongs.Visible && currentAlbum == a;
            if (x == null || x.Canciones == null || x.Canciones.Count == 0)
            {
                bool noEsta = (x != null && x.Canciones != null) || error == RocolaOsSpotifyCatalogoApi.NoEsta
                    || (error != null && (error.StartsWith("la API contesto 404", StringComparison.Ordinal) || error.StartsWith("la API contesto 400", StringComparison.Ordinal)));
                RocolaOs.Apunta("pared_generos", "el disco del catalogo «" + d.Art.Nombre + " - " + d.Nombre + "» no llego (" + (noEsta ? "Spotify no lo tiene: se esconde la sesion" : error ?? "sin respuesta") + ")");
                if (dentro) RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");   // (el aviso de XP)
                if (noEsta) { RocolaOsPgEsconde(a, d); if (dentro) BackToAlbums(); }
                return;
            }
            rocolaOsSpDeDisco[a] = x;
            RocolaOsSpotifyCatalogoLlena(a, x);
            // sus canciones dicen el genero de la pastilla de su casilla (la pastilla de la lista del disco de XP, GeneroDeCancion: el de la
            // cancion manda); sin pastilla, lo que diga XP
            string gen = RocolaOsPgGeneroDe(a);
            if (gen != null) foreach (Track t in a.Tracks) if (string.IsNullOrEmpty(t.Genre)) t.Genre = gen;
            generoVer++; duracionesVer++;
            if (dentro)
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
        // ESCONDER UN DISCO (Spotify no lo tiene): se quita de las listas en memoria y no vuelve en la sesion; la eleccion no salta
        private void RocolaOsPgEsconde(Album a, RocolaOsPgDisco d)
        {
            rocolaOsPgOcultos.Add(d.Clave);
            for (int k = 0; k < rocolaOsPgListas.Count; k++)
            {
                RocolaOsPgLetra e = rocolaOsPgListas[k];
                int i = e.Lista.IndexOf(a);
                if (i < 0) continue;
                List<Album> l = new List<Album>(e.Lista); l.RemoveAt(i);
                List<string> g = new List<string>(e.Generos); g.RemoveAt(i);
                Dictionary<Album, RocolaOsPgDisco> c = new Dictionary<Album, RocolaOsPgDisco>(e.Catalogo); c.Remove(a);
                Dictionary<Album, string> ps = new Dictionary<Album, string>(e.Pastillas); ps.Remove(a);
                RocolaOsPgLetra n = new RocolaOsPgLetra { Letra = e.Letra, Lista = l, Generos = g.ToArray(), Pastillas = ps, Catalogo = c, Locales = e.Locales, DelCatalogo = e.DelCatalogo - 1, Ms = e.Ms, De = e.De, AlbumsDeXp = e.AlbumsDeXp };
                rocolaOsPgListas[k] = n;
                if (e.Letra == currentLetter && selAlbumIndex > i) selAlbumIndex--;
            }
            RocolaOsPgPublica();
            if (panelSongs != null && !panelSongs.Visible && RocolaOsPgLetraSuya(currentLetter)) RefreshAlbums();
        }

        // ================================================================= g45: las letras ============================================
        // (desde g45, LetrasVisibles) LAS LETRAS QUE TRAE EL CATALOGO, aunque no tengas nada en ellas, en su sitio del abecedario
        private string RocolaOsPgLetras(string deXp)
        {
            if (!rocolaOsPgArrancada || letterAvailable == null) return deXp;
            if (!RocolaOsPgVa)
            {
                // (sin la llave o sin el catalogo despues de haberlos tenido: fuera las letras que solo traia el catalogo)
                bool quitada = false;
                foreach (char c in LetrasOrden)
                {
                    List<Album> l;
                    if (RocolaOsPgLetraSuya(c) && letterAvailable.Contains(c) && (!byLetter.TryGetValue(c, out l) || l == null || l.Count == 0)) { letterAvailable.Remove(c); quitada = true; }
                }
                if (!quitada) return deXp;
                StringBuilder sq = new StringBuilder(LetrasOrden.Length);
                foreach (char c in LetrasOrden) if (letterAvailable.Contains(c)) sq.Append(c);
                return sq.Length > 0 ? sq.ToString() : "#";
            }
            bool nueva = false;
            foreach (char c in rocolaOsPgCat.Bloques.Keys) if (rocolaOsPgCat.Bloques[c][3] > 0 && letterAvailable.Add(c)) nueva = true;
            if (!nueva && deXp != "#") return deXp;
            StringBuilder sb = new StringBuilder(LetrasOrden.Length);
            foreach (char c in LetrasOrden) if (letterAvailable.Contains(c)) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "#";
        }

        // ================================================================= el estado ===================================================
        private void RocolaOsPgVivo(StringBuilder sb)
        {
            if (!rocolaOsPgArrancada) return;
            RocolaOsPgCatalogo c = rocolaOsPgCat;
            sb.Append("pared=").Append(c == null ? "sin catalogo" : c.Discos.ToString(CultureInfo.InvariantCulture) + " discos del catalogo").Append('\n');
        }
        private static void RocolaOsPgPercentiles(StringBuilder sb, string nombre, int[] h)
        {
            long n = 0; foreach (int v in h) n += v;
            sb.Append(nombre).Append("_cuadros=").Append(n.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (n == 0) return;
            double[] ps = { 0.50, 0.95, 0.99 };
            string[] nom = { "p50", "p95", "p99" };
            for (int k = 0; k < ps.Length; k++)
            {
                long meta = (long)Math.Ceiling(n * ps[k]), acc = 0;
                for (int i = 0; i < h.Length; i++) { acc += h[i]; if (acc >= meta) { sb.Append(nombre).Append('_').Append(nom[k]).Append("_ms=").Append(i.ToString(CultureInfo.InvariantCulture)).Append('\n'); break; } }
            }
            int max = 0; for (int i = h.Length - 1; i >= 0; i--) if (h[i] > 0) { max = i; break; }
            long lentos50 = 0, lentos100 = 0; for (int i = 51; i < h.Length; i++) { lentos50 += h[i]; if (i > 100) lentos100 += h[i]; }
            sb.Append(nombre).Append("_max_ms=").Append(max.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(nombre).Append("_de_mas_de_50ms=").Append(lentos50.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(nombre).Append("_de_mas_de_100ms=").Append(lentos100.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        private void RocolaOsPgEscribeEstado()
        {
            try
            {
                RocolaOsPgCatalogo c = rocolaOsPgCat;
                StringBuilder sb = new StringBuilder(1024);
                sb.Append("catalogo=").Append(RocolaOsPgDescarga.Estado).Append('\n');
                if (c != null) sb.Append("catalogo_fecha=").Append(c.Fecha).Append("\ncatalogo_discos=").Append(c.Discos.ToString(CultureInfo.InvariantCulture)).Append("\ncatalogo_kb=").Append((c.Datos.Length / 1024).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("catalogo_mirados=").Append(Interlocked.Read(ref RocolaOsPgDescarga.Mirados).ToString(CultureInfo.InvariantCulture)).Append("\ncatalogo_bajados=").Append(Interlocked.Read(ref RocolaOsPgDescarga.Bajados).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("va=").Append(RocolaOsPgVa ? "si" : "no").Append('\n');
                sb.Append("letra=").Append(currentLetter).Append("\npagina=").Append(page.ToString(CultureInfo.InvariantCulture)).Append("\nelegido=").Append(selAlbumIndex.ToString(CultureInfo.InvariantCulture)).Append('\n');
                RocolaOsPgLetra m = RocolaOsPgMirada();
                if (m != null)
                {
                    sb.Append("paginas=").Append(((m.Lista.Count + PageSize - 1) / PageSize).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    Album a = selAlbumIndex >= 0 && selAlbumIndex < m.Lista.Count ? m.Lista[selAlbumIndex] : null;
                    if (a != null) sb.Append("elegido_disco=").Append(a.Artist).Append(" - ").Append(a.Name).Append('\n');
                    sb.Append("pastilla=").Append(selAlbumIndex >= 0 && selAlbumIndex < m.Generos.Length ? m.Generos[selAlbumIndex] ?? "" : "").Append('\n');
                    int conP = 0, ini = page * PageSize;
                    for (int i = ini; i < ini + PageSize && i < m.Lista.Count; i++) if (m.Generos[i] != null) conP++;
                    sb.Append("pagina_con_pastilla=").Append(conP.ToString(CultureInfo.InvariantCulture)).Append("\nletra_con_pastilla=").Append(m.Pastillas.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
                else sb.Append("pastilla=\n");
                sb.Append("vista=").Append(rocolaOsPgVista == null ? 0 : rocolaOsPgVista.Length).Append("\ncasillas_con_pastilla=").Append(Interlocked.Read(ref rocolaOsPgHorneadas).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("en_memoria=");
                if (rocolaOsPgListas != null) foreach (RocolaOsPgLetra e in rocolaOsPgListas) sb.Append(e.Letra);
                sb.Append('\n');
                sb.Append("armadas=").Append(rocolaOsPgArmadas.ToString(CultureInfo.InvariantCulture)).Append("\narmada_ms_max=").Append(rocolaOsPgArmadasMsMax.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("cambiadas_mirando=").Append(rocolaOsPgCambios.ToString(CultureInfo.InvariantCulture)).Append("\npaginas_vistas=").Append(rocolaOsPgCambiosPagina.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("ocultos=").Append((rocolaOsPgOcultos != null ? rocolaOsPgOcultos.Count : 0).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("portadas_pedidas=").Append(Interlocked.Read(ref RocolaOsPgPortadas.Pedidas).ToString(CultureInfo.InvariantCulture))
                  .Append("\nportadas_bajadas=").Append(Interlocked.Read(ref RocolaOsPgPortadas.Bajadas).ToString(CultureInfo.InvariantCulture))
                  .Append("\nportadas_sin=").Append(Interlocked.Read(ref RocolaOsPgPortadas.NoTienen).ToString(CultureInfo.InvariantCulture))
                  .Append("\nportadas_fallidas=").Append(Interlocked.Read(ref RocolaOsPgPortadas.Fallidas).ToString(CultureInfo.InvariantCulture))
                  .Append("\nportadas_dejadas=").Append(Interlocked.Read(ref RocolaOsPgPortadas.Dejadas).ToString(CultureInfo.InvariantCulture))
                  .Append("\nportadas_borradas=").Append(Interlocked.Read(ref RocolaOsPgPortadas.Borradas).ToString(CultureInfo.InvariantCulture))
                  .Append("\nportadas_mb=").Append((RocolaOsPgPortadas.BytesEnDisco / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
                long api = RocolaOsSpotifyCatalogoApi.Peticiones;
                sb.Append("api_peticiones=").Append(api.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("memoria_mb=").Append((Environment.WorkingSet / 1048576).ToString(CultureInfo.InvariantCulture)).Append("\nmemoria_mb_max=").Append((rocolaOsPgMemMax / 1048576).ToString(CultureInfo.InvariantCulture))
                  .Append("\ngc_mb=").Append((GC.GetTotalMemory(false) / 1048576).ToString(CultureInfo.InvariantCulture)).Append("\ngc_mb_max=").Append((rocolaOsPgGcMax / 1048576).ToString(CultureInfo.InvariantCulture)).Append('\n');
                if (rocolaOsPgCuadros != null) { RocolaOsPgPercentiles(sb, "todos", rocolaOsPgCuadros); RocolaOsPgPercentiles(sb, "hojeo", rocolaOsPgCuadrosHojeo); }
                string dir = RocolaOs.Run("rocola-xp/pared");
                Directory.CreateDirectory(dir);
                string ruta = Path.Combine(dir, "estado"), t = ruta + ".t";
                File.WriteAllText(t, sb.ToString());
                File.Move(t, ruta, true);
            }
            catch (Exception ex) { RocolaOs.Falla("pared_generos", "al escribir el estado", ex); }
        }
    }
}

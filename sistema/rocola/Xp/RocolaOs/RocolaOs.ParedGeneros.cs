// LA PARED POR GENEROS (fase 11, grupo pared-generos; PLAN_PARED_GENEROS.md; FASE11_pared-generos.md). Peticion del dueno del 14-11-2026
// («que cada letra tuviera 500 paginas juntando mi musica local y la de Spotify, pero poniendo los artistas y discos mas populares de cada
// artista por genero y ordenado por el abecedario»), con sus decisiones del 14-11 (el genero, con la pastilla de XP en la cabecera) y del
// 15-11-2026 (sin salto de genero: la pared se mueve como en XP). Una parte de Form1.
//
// CON EL MODO SPOTIFY ENCENDIDO Y EL CATALOGO PRESENTE (RocolaOs.ParedGenerosCatalogo.cs), cada letra (# y de la A a la Z) junta los discos de
// XP y los del catalogo:
//   - por GENERO, en un orden FIJO para la sesion: primero los generos de los que el dueno tiene mas discos, luego los demas por cuantos
//     trae el catalogo, «Otros» al final. El genero de cada disco de XP es el de XP (GeneroDeCancion: la etiqueta de GenreLabel), tomado la
//     primera vez y fijo la sesion (XP aprende generos de fondo: la pared no baila);
//   - dentro, por ARTISTA (sin acentos ni mayusculas: MotorBusqueda.Normaliza, la de XP); un artista de XP y el del catalogo con el mismo
//     nombre van juntos, con el genero de los discos de XP (el que mas se repite) o, si no tiene, el del catalogo;
//   - de cada artista, primero sus discos de XP (en el orden de XP) y despues los del catalogo en el orden del archivo (del mas al menos
//     escuchado), sin los que el dueno ya tiene (mismo nombre de disco, sin acentos ni mayusculas);
//   - como mucho 500 paginas (18 discos cada una): lo que sobra del catalogo (lo de atras en el archivo) no entra.
// La lista de una letra se arma en un hilo (nunca en la pantalla) y entra de una pieza; la eleccion se queda en el mismo disco.
// En la memoria, solo las listas de la letra que se mira y de 3 mas (las de al lado y la ultima).
//
// LA CABECERA: junto a «LETRA A», la pastilla del genero de XP (la de HorneaGenero/ColorDeEtiqueta: el mismo dibujo) con el genero del disco
// elegido (el de su sitio en la pared). Ganchos g91-g94. La navegacion, la de XP.
//
// SIN EL MODO O SIN CATALOGO, la pared y la cabecera son las de XP tal cual (cada gancho devuelve lo de XP; ningun hilo ni archivo).
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
                Que = "las letras por genero con el catalogo (solo con el modo Spotify): hasta 500 paginas por letra",
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
            internal string[] Generos;                                  // el genero de cada casilla (el de su sitio): la pastilla
            internal Dictionary<Album, RocolaOsPgDisco> Catalogo;       // las del catalogo
            internal int Locales, DelCatalogo, Generos0;
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
            internal string[] GenerosLocales;
            internal Dictionary<string, int> Rango;
            internal HashSet<string> Ocultos;
            internal List<Album> AlbumsDeXp;
            internal string Volcado;
            internal int Numero;
        }

        private RocolaOsPgCatalogo rocolaOsPgCat;
        private Dictionary<string, int> rocolaOsPgRango;                  // genero -> su lugar (fijo la sesion)
        private string[] rocolaOsPgOrden;                                 // (para el estado)
        private Dictionary<string, string> rocolaOsPgGeneroDe;            // carpeta de un disco de XP -> su genero (fijo la sesion)
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
        private bool rocolaOsPgSoloMide;                                  // (pruebas, ROCOLA_PARED_MIDE=1 con el modo apagado: solo se miden los cuadros)
        private static string rocolaOsPgPastilla;                         // el genero de la pastilla de la cabecera (o null): solo la pantalla
        // lo que se mide (para el estado y las pruebas)
        private long rocolaOsPgArmadas, rocolaOsPgArmadasMsMax, rocolaOsPgCambios, rocolaOsPgCambiosPagina, rocolaOsPgMemMax, rocolaOsPgGcMax;
        private long rocolaOsPgUltimoCuadro, rocolaOsPgUltimoCambio;
        private int[] rocolaOsPgCuadros, rocolaOsPgCuadrosHojeo;
        private long rocolaOsPgApiAlEmpezar = -1;
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
            rocolaOsPgGeneroDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            rocolaOsPgListas = new List<RocolaOsPgLetra>();
            rocolaOsPgPedidas = new Dictionary<char, int>();
            rocolaOsPgOcultos = new HashSet<string>(StringComparer.Ordinal);
            rocolaOsPgEsperan = new Dictionary<string, List<Album>>(StringComparer.OrdinalIgnoreCase);
            rocolaOsPgAbriendo = new HashSet<Album>();
            rocolaOsPgCuadros = new int[1001]; rocolaOsPgCuadrosHojeo = new int[1001];
            RocolaOs.Apunta("pared_generos", "encendida: el catalogo en " + RocolaOsPgDescarga.Carpeta + " (de " + RocolaOsPgDescarga.Url + "), en cuanto haya la llave de la API");
        }

        // EL LATIDO (1 s, en la pantalla): el catalogo nuevo, lo armado que no pudo encargarse, el orden y el estado
        private int rocolaOsPgLatidos;
        private void RocolaOsPgLatido()
        {
            if (!rocolaOsPgArrancada && !rocolaOsPgSoloMide) return;
            long mem = Environment.WorkingSet, gc = GC.GetTotalMemory(false);
            if (mem > rocolaOsPgMemMax) rocolaOsPgMemMax = mem;
            if (gc > rocolaOsPgGcMax) rocolaOsPgGcMax = gc;
            if (rocolaOsPgSoloMide) { if (++rocolaOsPgLatidos % 2 == 0) RocolaOsPgEscribeEstado(); return; }
            // LA LLAVE DE LA API (el archivo se lee como mucho cada 5 s): sin ella, ni se baja el catalogo ni hay pared por generos
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
            if (rocolaOsPgCat != null && rocolaOsPgRango == null && !libBuilding) RocolaOsPgPreparaOrden();
            if (++rocolaOsPgLatidos % 2 == 0) RocolaOsPgEscribeEstado();
        }

        private void RocolaOsPgLlegoCatalogo(RocolaOsPgCatalogo c)
        {
            bool primero = rocolaOsPgCat == null;
            rocolaOsPgCat = c;
            rocolaOsPgPedidas.Clear();   // (las de un catalogo de antes se siguen viendo hasta que llegue la nueva de cada una: de una pieza)
            if (rocolaOsPgApiAlEmpezar < 0) rocolaOsPgApiAlEmpezar = RocolaOsSpotifyCatalogoApi.Peticiones;
            if (!RocolaOsPgVa) return;
            if (!libBuilding) RocolaOsPgPreparaOrden();
            if (letterBar != null && !libBuilding) RecalculaLetras();   // las letras que solo trae el catalogo
            if (RocolaOsPgLetraSuya(currentLetter)) RocolaOsPgPideConVecinas(currentLetter);
            if (!primero) RocolaOs.Apunta("pared_generos", "catalogo nuevo: las letras se arman otra vez");
        }

        // EL ORDEN DE LOS GENEROS, una vez la sesion: los del dueno (por cuantos discos tiene), los demas por los del catalogo, «Otros» al final
        private void RocolaOsPgPreparaOrden()
        {
            if (rocolaOsPgRango != null || rocolaOsPgCat == null || albums == null) return;
            Dictionary<string, int> mios = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Album a in albums)
            {
                if (a == null || !RocolaOsPgLetraSuya(a.Letter)) continue;
                string g = RocolaOsPgGeneroLocal(a);
                int n; mios.TryGetValue(g, out n); mios[g] = n + 1;
            }
            List<string> todos = new List<string>(mios.Keys);
            foreach (string g in rocolaOsPgCat.PorGenero.Keys) if (!mios.ContainsKey(g)) todos.Add(g);
            todos.Remove("Otros");
            todos.Sort(delegate (string x, string y)
            {
                int mx, my, cx, cy;
                mios.TryGetValue(x, out mx); mios.TryGetValue(y, out my);
                if (mx != my) return my.CompareTo(mx);
                rocolaOsPgCat.PorGenero.TryGetValue(x, out cx); rocolaOsPgCat.PorGenero.TryGetValue(y, out cy);
                if (cx != cy) return cy.CompareTo(cx);
                return string.CompareOrdinal(x, y);
            });
            todos.Add("Otros");
            Dictionary<string, int> r = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < todos.Count; i++) r[todos[i]] = i;
            rocolaOsPgRango = r;
            rocolaOsPgOrden = todos.ToArray();
            StringBuilder sb = new StringBuilder();
            foreach (string g in todos) { int m; mios.TryGetValue(g, out m); if (sb.Length > 0) sb.Append(", "); sb.Append(g).Append(' ').Append(m.ToString(CultureInfo.InvariantCulture)); }
            RocolaOs.Apunta("pared_generos", "el orden de los generos (fijo la sesion; tus discos de cada uno): " + sb);
        }
        // el genero de un disco de XP en la pared: el de XP la primera vez que se mira, y ese la sesion entera
        private string RocolaOsPgGeneroLocal(Album a)
        {
            string g;
            string k = a.Folder ?? "";
            if (rocolaOsPgGeneroDe.TryGetValue(k, out g)) return g;
            g = GeneroDeCancion(null, a);
            if (string.IsNullOrEmpty(g)) g = "Otros";
            rocolaOsPgGeneroDe[k] = g;
            return g;
        }

        // ================================================================= g90: la lista de la letra ===================================
        // g90 (Form1.Biblioteca.cs, CurrentList: «return list;»): LA LISTA DE UNA LETRA. Sin el modo o sin catalogo, la de XP. Con ellos, la
        // armada (si ya esta) o la de XP mientras se arma.
        private List<Album> RocolaOsParedGenerosLista(List<Album> xp)
        {
            if (!rocolaOsPgArrancada || !RocolaOsPgLetraSuya(currentLetter) || !RocolaOsPgVa) return xp;
            if (!ReferenceEquals(albums, rocolaOsPgAlbumsXp)) { rocolaOsPgListas.Clear(); rocolaOsPgPedidas.Clear(); rocolaOsPgAlbumsXp = albums; }
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
            if (rocolaOsPgRango == null) { RocolaOsPgPreparaOrden(); if (rocolaOsPgRango == null) return; }
            List<Album> loc;
            if (!byLetter.TryGetValue(l, out loc) || loc == null) loc = new List<Album>();
            loc = new List<Album>(loc);
            string[] gen = new string[loc.Count];
            for (int i = 0; i < loc.Count; i++) gen[i] = RocolaOsPgGeneroLocal(loc[i]);
            string vol = null;
            try { vol = Environment.GetEnvironmentVariable("ROCOLA_PARED_VOLCADO"); } catch { }
            RocolaOsPgEncargo e = new RocolaOsPgEncargo
            {
                Letra = l, Cat = rocolaOsPgCat, Locales = loc, GenerosLocales = gen, Rango = rocolaOsPgRango,
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

        private sealed class RocolaOsPgGrupo
        {
            internal string Clave, Nombre, GeneroCat;
            internal readonly List<int> Locales = new List<int>();
            internal readonly List<RocolaOsPgDisco> Catalogo = new List<RocolaOsPgDisco>();
            internal string Genero;
            internal int Rango;
        }
        // ARMAR UNA LETRA (en el hilo que arma)
        private static RocolaOsPgLetra RocolaOsPgArma(RocolaOsPgEncargo e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            List<RocolaOsPgArtista> arts = RocolaOsPgLeeLetra(e.Cat, e.Letra);
            Dictionary<string, RocolaOsPgGrupo> grupos = new Dictionary<string, RocolaOsPgGrupo>(StringComparer.Ordinal);
            List<RocolaOsPgGrupo> orden = new List<RocolaOsPgGrupo>();
            HashSet<string> tuyos = new HashSet<string>(StringComparer.Ordinal);
            Func<string, string, RocolaOsPgGrupo> grupo = delegate (string clave, string nombre)
            {
                RocolaOsPgGrupo g;
                if (!grupos.TryGetValue(clave, out g)) { g = new RocolaOsPgGrupo { Clave = clave, Nombre = nombre }; grupos[clave] = g; orden.Add(g); }
                return g;
            };
            for (int i = 0; i < e.Locales.Count; i++)
            {
                Album a = e.Locales[i];
                string k = MotorBusqueda.Normaliza(a.Artist ?? "");
                grupo(k, a.Artist ?? "").Locales.Add(i);
                tuyos.Add(k + "\n" + MotorBusqueda.Normaliza(a.Name ?? ""));
                // (XP: una carpeta sin « - » es un disco sin nombre de ese artista; cuenta como el disco que se llama como el)
                if (string.IsNullOrEmpty(a.Name)) tuyos.Add(k + "\n" + k);
            }
            int tope = RocolaOsPgPaginasMax * PageSize;
            int quedan = Math.Max(0, tope - e.Locales.Count);
            foreach (RocolaOsPgArtista ar in arts)
            {
                if (quedan <= 0) break;
                string k = MotorBusqueda.Normaliza(ar.Nombre);
                if (k.Length == 0) continue;
                RocolaOsPgGrupo g = null;
                foreach (RocolaOsPgDisco d in ar.Discos)
                {
                    if (quedan <= 0) break;
                    if (e.Ocultos.Contains(d.Clave) || tuyos.Contains(k + "\n" + MotorBusqueda.Normaliza(d.Nombre))) continue;
                    if (g == null) g = grupo(k, ar.Nombre);
                    g.Catalogo.Add(d);
                    quedan--;
                }
                if (g == null) grupos.TryGetValue(k, out g);
                if (g != null && g.GeneroCat == null) g.GeneroCat = ar.Genero;
            }
            // el genero de cada artista: el que mas se repite entre sus discos de XP (sin «Otros»), o el del catalogo, o «Otros»
            foreach (RocolaOsPgGrupo g in orden)
            {
                Dictionary<string, int> votos = new Dictionary<string, int>(StringComparer.Ordinal);
                string mejor = null; int mejorN = 0;
                foreach (int i in g.Locales)
                {
                    string x = e.GenerosLocales[i];
                    if (x == "Otros") continue;
                    int n; votos.TryGetValue(x, out n); votos[x] = ++n;
                    if (n > mejorN || (n == mejorN && RocolaOsPgRangoDe(e.Rango, x) < RocolaOsPgRangoDe(e.Rango, mejor))) { mejor = x; mejorN = n; }
                }
                g.Genero = mejor ?? g.GeneroCat ?? "Otros";
                g.Rango = RocolaOsPgRangoDe(e.Rango, g.Genero);
            }
            orden.Sort(delegate (RocolaOsPgGrupo x, RocolaOsPgGrupo y)
            {
                if (x.Rango != y.Rango) return x.Rango.CompareTo(y.Rango);
                int c = string.CompareOrdinal(x.Clave, y.Clave);
                return c != 0 ? c : string.CompareOrdinal(x.Nombre, y.Nombre);
            });
            RocolaOsPgLetra r = new RocolaOsPgLetra { Letra = e.Letra, De = e.Cat, AlbumsDeXp = e.AlbumsDeXp, Catalogo = new Dictionary<Album, RocolaOsPgDisco>() };
            List<Album> lista = new List<Album>(Math.Min(tope, e.Locales.Count + e.Cat.Discos));
            List<string> generos = new List<string>(lista.Capacity);
            HashSet<string> distintos = new HashSet<string>(StringComparer.Ordinal);
            foreach (RocolaOsPgGrupo g in orden)
            {
                if (g.Locales.Count + g.Catalogo.Count == 0) continue;
                distintos.Add(g.Genero);
                foreach (int i in g.Locales) { lista.Add(e.Locales[i]); generos.Add(g.Genero); }
                foreach (RocolaOsPgDisco d in g.Catalogo)
                {
                    Album a = new Album { Folder = RocolaOsPgCarpeta + d.Clave, Name = d.Nombre, Artist = d.Art.Nombre, Letter = e.Letra };
                    r.Catalogo[a] = d;
                    lista.Add(a); generos.Add(g.Genero);
                }
            }
            r.Lista = lista; r.Generos = generos.ToArray();
            r.Locales = e.Locales.Count; r.DelCatalogo = lista.Count - e.Locales.Count; r.Generos0 = distintos.Count;
            r.Ms = sw.ElapsedMilliseconds;
            if (e.Volcado != null) RocolaOsPgVuelca(e.Volcado, r);
            return r;
        }
        private static int RocolaOsPgRangoDe(Dictionary<string, int> rango, string g)
        {
            int v;
            if (g != null && rango.TryGetValue(g, out v)) return v;
            return g == null ? int.MaxValue : rango.Count;   // (un genero que no estaba al fijar el orden: antes de «Otros» no; al final)
        }
        // (pruebas) ROCOLA_PARED_VOLCADO=CARPETA: cada letra armada, en CARPETA/letra-X.tsv (casilla, genero, de donde, artista, disco)
        private static void RocolaOsPgVuelca(string dir, RocolaOsPgLetra r)
        {
            try
            {
                Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder(r.Lista.Count * 48);
                for (int i = 0; i < r.Lista.Count; i++)
                {
                    Album a = r.Lista[i];
                    RocolaOsPgDisco d;
                    bool cat = r.Catalogo.TryGetValue(a, out d);
                    sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(r.Generos[i]).Append('\t').Append(cat ? "catalogo" : "xp").Append('\t')
                      .Append(a.Artist).Append('\t').Append(a.Name).Append('\t').Append(cat ? d.Clave : a.Folder).Append('\n');
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
                + r.Generos0.ToString(CultureInfo.InvariantCulture) + " generos) armada en " + r.Ms.ToString(CultureInfo.InvariantCulture) + " ms");
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
        // (desde g47, RefreshAlbums): la pastilla, las portadas de la pagina que se mira y lo que se mide al hojear
        private void RocolaOsPgRefresco()
        {
            if (rocolaOsPgSoloMide) { string s0 = currentLetter + "|" + page.ToString(CultureInfo.InvariantCulture); if (s0 != rocolaOsPgUltimaPagina) { rocolaOsPgUltimaPagina = s0; rocolaOsPgUltimoCambio = Stopwatch.GetTimestamp(); rocolaOsPgCambiosPagina++; } return; }
            if (!rocolaOsPgArrancada) return;
            RocolaOsPgLetra e = RocolaOsPgVa ? RocolaOsPgMirada() : null;
            string sig = currentLetter + "|" + page.ToString(CultureInfo.InvariantCulture);
            if (sig != rocolaOsPgUltimaPagina) { rocolaOsPgUltimaPagina = sig; rocolaOsPgUltimoCambio = Stopwatch.GetTimestamp(); if (e != null) rocolaOsPgCambiosPagina++; }
            RocolaOsPgPonPastilla(e);
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

        // ================================================================= la pastilla (g91-g94) ======================================
        private void RocolaOsPgPonPastilla(RocolaOsPgLetra e)
        {
            string g = null;
            if (e != null && selAlbumIndex >= 0 && selAlbumIndex < e.Generos.Length) g = e.Generos[selAlbumIndex];
            if (g == "Otros") g = null;   // (XP no inventa: sin genero, sin pastilla)
            if (g == rocolaOsPgPastilla) return;
            rocolaOsPgPastilla = g;
            if (headerView != null) headerView.Invalidate();
        }
        // g94 (Form1.Biblioteca.cs, SetSelectedLocal): la eleccion se movio dentro de la pagina: la pastilla dice el genero del disco elegido
        private void RocolaOsParedGenerosMovio()
        {
            if (!rocolaOsPgArrancada) return;
            RocolaOsPgPonPastilla(RocolaOsPgVa ? RocolaOsPgMirada() : null);
        }
        // g93 (Form1.Video.cs, UiSig: «sb = Mix(sb, headerView.Section)»): la firma de la imagen de arriba cambia con la pastilla (si no, la
        // tarjeta no rehace la cabecera). Sin pastilla, la misma cadena de XP (el mismo numero). Se llama en cada cuadro con la pared delante:
        // aqui se mide lo que tarda cada cuadro (el estado: p50/p95/p99/max, aparte los de mientras se hojea).
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
            string p = rocolaOsPgPastilla;
            return p == null ? seccion : seccion + "\u0001" + p;
        }
        // g91 (Form1.Vistas.cs, HeaderView.RenderContent: el ancho del nombre): lo que ocupa la pastilla con su hueco (0 sin pastilla)
        private static float RocolaOsParedGenerosAncho(int h, Rocola.Nativo.Letra f)
        {
            string p = rocolaOsPgPastilla;
            if (p == null || f == null) return 0f;
            SKBitmap b = RocolaOsPgHornea(p, f);
            return b == null ? 0f : h * 0.32f + b.Width;
        }
        // g92 (Form1.Vistas.cs, HeaderView.RenderContent: «x += anchoSec;»): la pastilla, detras del nombre, centrada en el alto; devuelve lo
        // que ocupo (0 si no hay o no cabe entera)
        private static float RocolaOsParedGenerosPinta(SKCanvas g, float x, int h, int w, Rocola.Nativo.Letra f, bool medio, bool copia)
        {
            string p = rocolaOsPgPastilla;
            if (p == null || f == null) return 0f;
            SKBitmap b = RocolaOsPgHornea(p, f);
            if (b == null) return 0f;
            float x0 = (float)Math.Floor(x + h * 0.32f);
            if (x0 + b.Width > w) return 0f;
            float y0 = (float)Math.Floor((h - b.Height) * 0.5f);
            Imagenes.Pinta(g, b, new SKRect(0, 0, b.Width, b.Height), new SKRect(x0, y0, x0 + b.Width, y0 + b.Height), Rocola.Nativo.Filtro.DeFabrica, medio, copia);
            return h * 0.32f + b.Width;
        }
        // LA PASTILLA DEL GENERO DE XP, horneada como la hornea XP (SongListView.HorneaGenero, Form1.Vistas.cs): el mismo relleno tenue del color
        // del genero, el mismo borde fino, el mismo texto aclarado y la misma medida (texto + 16, alto + 5, radio 7), con la letra de la cabecera
        private static readonly Dictionary<string, SKBitmap> rocolaOsPgHorno = new Dictionary<string, SKBitmap>(StringComparer.Ordinal);
        private static float rocolaOsPgHornoCuerpo = -1f;
        private static SKBitmap RocolaOsPgHornea(string etiqueta, Rocola.Nativo.Letra f)
        {
            if (Math.Abs(rocolaOsPgHornoCuerpo - f.Puntos) > 0.01f)
            {
                foreach (SKBitmap v in rocolaOsPgHorno.Values) v.Dispose();
                rocolaOsPgHorno.Clear(); rocolaOsPgHornoCuerpo = f.Puntos;
            }
            SKBitmap b;
            if (rocolaOsPgHorno.TryGetValue(etiqueta, out b)) return b;
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
                    using (SKPaint sb = new SKPaint { Color = Color.FromArgb(60, c).Sk() }) Pixel.Rellena(g, gp, sb, suave);
                    using (SKPaint pn = new SKPaint { Color = Color.FromArgb(120, c).Sk(), StrokeWidth = 1f }) Pixel.Traza(g, gp, pn, suave);
                }
                FormatoTexto sf = new FormatoTexto(); sf.Horizontal = Alinea.Centro; sf.Vertical = Alinea.Centro;
                sf.Banderas = BanderasTexto.SinPartir;
                using (SKPaint sb = new SKPaint { Color = Lighten(c, 0.45).Sk() })
                    TextoGdiPlus.Escribe(g, etiqueta, f, ajuste, sb, new RectangleF(0, 0, w, h), sf);
            }
            rocolaOsPgHorno[etiqueta] = b;
            return b;
        }

        // ================================================================= abrir un disco del catalogo ==================================
        private string RocolaOsPgGeneroDe(Album a)
        {
            if (rocolaOsPgListas == null) return null;
            for (int i = rocolaOsPgListas.Count - 1; i >= 0; i--)
            {
                int k = rocolaOsPgListas[i].Lista.IndexOf(a);
                if (k >= 0) { string g = rocolaOsPgListas[i].Generos[k]; return g == "Otros" ? null : g; }
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
            // sus canciones dicen el genero de su sitio en la pared (la pastilla de la lista del disco de XP, GeneroDeCancion: el de la cancion
            // manda), el mismo que la de la cabecera; sin genero («Otros»), lo que diga XP
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
                RocolaOsPgLetra n = new RocolaOsPgLetra { Letra = e.Letra, Lista = l, Generos = g.ToArray(), Catalogo = c, Locales = e.Locales, DelCatalogo = e.DelCatalogo - 1, Generos0 = e.Generos0, Ms = e.Ms, De = e.De, AlbumsDeXp = e.AlbumsDeXp };
                rocolaOsPgListas[k] = n;
                if (e.Letra == currentLetter && selAlbumIndex > i) selAlbumIndex--;
            }
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
                if (rocolaOsPgOrden != null) sb.Append("orden=").Append(string.Join(",", rocolaOsPgOrden)).Append('\n');
                sb.Append("letra=").Append(currentLetter).Append("\npagina=").Append(page.ToString(CultureInfo.InvariantCulture)).Append("\nelegido=").Append(selAlbumIndex.ToString(CultureInfo.InvariantCulture)).Append('\n');
                RocolaOsPgLetra m = RocolaOsPgMirada();
                if (m != null)
                {
                    sb.Append("paginas=").Append(((m.Lista.Count + PageSize - 1) / PageSize).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    Album a = selAlbumIndex >= 0 && selAlbumIndex < m.Lista.Count ? m.Lista[selAlbumIndex] : null;
                    if (a != null) sb.Append("elegido_disco=").Append(a.Artist).Append(" - ").Append(a.Name).Append('\n');
                }
                sb.Append("pastilla=").Append(rocolaOsPgPastilla ?? "").Append('\n');
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

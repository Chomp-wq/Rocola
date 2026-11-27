// EL ASISTENTE ESTRELLA EXPLICA LO NUEVO DE SPOTIFY (fase 11, grupo asistente-spotify; FASE11_asistente-spotify.md). Una parte de Form1,
// encima de la ayuda de XP (Codigo/AsistenteRocola.cs y AsistenteGuiaRocola.cs) y del catalogo de Spotify (RocolaOs.SpotifyCatalogo*.cs).
// Peticion del dueno del 15-11-2026 («tambien tendras que ajustar el asistente con nuevas instrucciones, ya que estamos metiendo nuevas
// cosas»): con el modo Spotify y la llave de la API, la pregunta de la estrella («¿Que quieres hacer?») lleva una cuarta opcion,
// «Canciones de Spotify», que abre otra pregunta igual con tres guias:
//   1. «Pedir una cancion de Spotify»: el buscador, lo escrito, las canciones de Spotify entre los resultados (y lo que hace cada tarjeta:
//      un disco, un artista que solo esta en Spotify, uno tuyo con su pared unida, «Todos los discos de»), la moneda (sin creditos no se pide) y PONER.
//   2. «La letra ♫ de Spotify»: ir a la letra ♫ de la pared con ROJO + IZQ/DER, entrar a un disco o lista, la moneda y PONER.
//      (27-11-2026) Y la tarjeta «LAS MEJORES CANCIONES DE ARTISTA» (el atajo del buscador con ese nombre, RocolaOs.Mejores.cs): su paso, y el del
//      artista (guia 3) dice donde esta el atajo.
//   3. «Los discos de un artista»: el buscador, la tarjeta del artista (la tuya abre la pared unida, 23-11-2026; o «Todos los discos de»), su pared de discos
//      («Canciones de» en la primera casilla), entrar a un disco y volver con REGRESAR: a la pared, y otra vez, al buscador.
// Cada guia es como las de XP: mira lo que pasa DE VERDAD en la rocola cada latido y decide el paso que toca, la estrella senala con la
// mano y la flecha dorada la pieza de la pantalla (lo mismo que senala XP: el buscador, el teclado, la tarjeta elegida, la barra de letras,
// los creditos, el disco o la cancion elegidos, la cabecera) y el globo dibuja los botones. TODO CON SU MISMO DIBUJO: el globo de la guia es
// PintaGuiaAsistente de XP con otros textos, y el de la pregunta, el de PintaPanelAsistente con cuatro opciones o con las tres de Spotify
// (RocolaOs.AsistentePinta.cs). La estrella sigue sin cara (nada de esto la toca).
// SIN EL MODO SPOTIFY O SIN LA LLAVE DE LA API, el asistente es el de XP tal cual: cada gancho devuelve lo de XP (tres opciones, sus
// misiones, su dibujo), y la cuenta de si va se hace al abrir la ayuda (cambiar la llave vale desde la siguiente vez que se abre).
// Los ganchos (GANCHOS.tsv): g70-g75 (cuantas opciones tiene la pregunta), g76 (donde va cada opcion), g77 (elegir), g78 (ROJO en la
// pregunta), g79 (la pagina del globo), g80 (el dibujo de la pregunta), g81 (el paso de la guia), g82 (lo que senala), g83 (el dibujo del
// globo de la guia).
using System;
using System.Drawing;

namespace Rocola
{
    public partial class Form1
    {
        // las misiones de Spotify (las de XP son 1 a 3) y sus pasos (los de XP son 1 a 12): numeros que XP nunca tiene
        internal const int RocolaOsMisionPide = 4, RocolaOsMisionLetra = 5, RocolaOsMisionArtista = 6;
        internal const int RocolaOsPasoAbre = 40, RocolaOsPasoEscribe = 41, RocolaOsPasoBuscaSpotify = 42, RocolaOsPasoCancionSpotify = 43,
            RocolaOsPasoDiscoSpotify = 44, RocolaOsPasoArtistaSpotify = 45, RocolaOsPasoMasSpotify = 46, RocolaOsPasoTodos = 47,
            RocolaOsPasoMoneda = 48, RocolaOsPasoCancionDisco = 49, RocolaOsPasoLetra = 50, RocolaOsPasoEntraDisco = 51,
            RocolaOsPasoParedCanciones = 52, RocolaOsPasoParedDisco = 53, RocolaOsPasoVuelveDisco = 54, RocolaOsPasoVuelveBuscador = 55,
            RocolaOsPasoSinLetra = 56, RocolaOsPasoAbreArtista = 57, RocolaOsPasoEscribeArtista = 58, RocolaOsPasoBuscaTarjeta = 59,
            RocolaOsPasoMejores = 60;   // (27-11-2026) la tarjeta «LAS MEJORES CANCIONES DE ARTISTA» (RocolaOs.Mejores.cs)

        // LA PREGUNTA: estaticos porque los usan funciones estaticas de XP (AsistenteOpcionRect, AsistenteVerdeEn de la tarjeta). Solo los
        // escribe la pantalla (el gancho g79, al hornear el globo, y elegir o volver en la pregunta); la tarjeta dibuja en el mismo hilo.
        private static bool rocolaOsAsSpotify;    // esta vez que se abrio la ayuda, con el modo Spotify y la llave de la API
        private static int rocolaOsAsMenu;        // 0 = la pregunta de XP (con la cuarta opcion), 1 = la de Spotify
        private int rocolaOsAsAbierta;            // el asistenteInicioTick de la ultima vez que se miro (otro = se abrio de nuevo)
        private bool rocolaOsAsVioPared, rocolaOsAsVioDisco;   // (guia 3) ya estuvo en una pared del buscador, y en un disco de ella
        private int rocolaOsAsPasoApuntado;

        // g70-g75: CUANTAS OPCIONES tiene la pregunta. De XP, AsistenteOpciones (3); con Spotify, 4 en la primera y 3 en la de Spotify.
        internal static int RocolaOsAsistenteOpciones { get { return RocolaOsAsistenteCuatro ? 4 : AsistenteOpciones; } }
        internal static bool RocolaOsAsistenteCuatro { get { return rocolaOsAsSpotify && rocolaOsAsMenu == 0; } }

        // g76: DONDE VA CADA OPCION con cuatro: las mismas medidas de XP (el mismo margen, el mismo ancho) un poco mas juntas, para que la
        // cuarta quepa encima de la leyenda de abajo (XP: desde el 50 % del alto, cada 13,5 %, de 11,5 % de alto; aqui desde el 47 %, cada
        // 11,2 %, de 10 %: la cuarta acaba en el 90,6 %, y la leyenda empieza hacia el 93 %).
        internal static Rectangle RocolaOsAsistenteOpcion4(int x, int derecha, int w, int h, int i)
        {
            return new Rectangle(x, (int)(h * (.47f + i * .112f)), Math.Max(1, w - x - derecha), Math.Max(22, (int)(h * .10f)));
        }

        // ¿Va lo de Spotify en la ayuda? El modo Spotify encendido (la funcion «spotify») y la llave de la API (sin ella no hay resultados de
        // Spotify, ni paredes, ni letra ♫: no hay nada que explicar). Lo primero, lo que XP nunca tiene: sin ROCOLA_OS no se lee nada.
        private bool RocolaOsAsistenteHaySpotify()
        {
            if (!RocolaOs.Activo || !RocolaOs.Encendida("spotify")) return false;
            if (!RocolaOsSpotifyCatalogoApi.HayLlave) return false;
            return RocolaOsSpotifyCatalogoVa;
        }

        // g79 (EnsureAsistentePanel: «int pagina = adios ? PaginaAdios : guia ? 16 + asistenteMision : 0;»): la pagina del globo cuando
        // pregunta. De XP, 0. Con Spotify, 40 (la de XP con la cuarta opcion) o 41 (la de Spotify): otro numero, para que el globo se hornee
        // otra vez al cambiar (lo guarda por pagina) y la tarjeta reinicie el fundido de sus letras. Cada vez que la ayuda se abre de nuevo se
        // mira otra vez si va lo de Spotify, y la pregunta empieza por la de XP.
        private int RocolaOsAsistenteMenuPagina()
        {
            if (asistenteInicioTick != rocolaOsAsAbierta || rocolaOsAsAbierta == 0)
            {
                rocolaOsAsAbierta = asistenteInicioTick == 0 ? 1 : asistenteInicioTick;
                bool antes = rocolaOsAsSpotify;
                rocolaOsAsSpotify = RocolaOsAsistenteHaySpotify();
                rocolaOsAsMenu = 0;
                if (rocolaOsAsSpotify && !antes) RocolaOs.Apunta("asistente", "la ayuda con lo de Spotify (cuatro opciones)");
            }
            if (!rocolaOsAsSpotify) return 0;
            return 40 + rocolaOsAsMenu;
        }

        // g77 (EligeAsistente: «EmpiezaMision(asistenteSeleccion + 1);»): ELEGIR EN LA PREGUNTA. true = ya lo atendio. De XP (o en las tres
        // primeras opciones), false: la mision de XP. «Canciones de Spotify» pasa a la pregunta de Spotify; en ella, cada opcion es su guia.
        private bool RocolaOsAsistenteElige()
        {
            if (!rocolaOsAsSpotify) return false;
            if (rocolaOsAsMenu == 0)
            {
                if (asistenteSeleccion != AsistenteOpciones) return false;   // (las tres de XP)
                rocolaOsAsMenu = 1; asistenteSeleccion = 0;
                asistenteActividadTick = Environment.TickCount;
                AnimaAsistente(AsistenteGestoGpu.Explicar);
                RocolaOs.Apunta("asistente", "pregunta de Spotify");
                return true;
            }
            RocolaOsAsistenteEmpieza(RocolaOsMisionPide + Math.Max(0, Math.Min(2, asistenteSeleccion)));
            return true;
        }

        // g78 (AsistenteEntrada, en la pregunta: «if (IsBack(kc)) CierraAsistente();»): ROJO en la pregunta de Spotify vuelve a la de XP, con
        // «Canciones de Spotify» elegida. true = ya lo atendio; en la de XP (y sin Spotify), false: se cierra, como XP.
        private bool RocolaOsAsistenteAtras()
        {
            if (!rocolaOsAsSpotify || rocolaOsAsMenu != 1 || asistenteMision != 0) return false;
            rocolaOsAsMenu = 0; asistenteSeleccion = AsistenteOpciones;
            asistenteActividadTick = Environment.TickCount;
            AnimaAsistente(AsistenteGestoGpu.Senalar);
            return true;
        }

        // Empezar una guia de Spotify: lo mismo que EmpiezaMision de XP (que solo acepta las suyas)
        private void RocolaOsAsistenteEmpieza(int mision)
        {
            asistenteMision = mision; asistentePaso = 0; asistenteLogroTick = 0; asistenteBienTick = 0;
            asistenteAprende = 0; asistentePedidosBase = asistentePedidos;
            rocolaOsAsVioPared = rocolaOsAsVioDisco = false; rocolaOsAsPasoApuntado = 0;
            BasesGuia();
            AnimaAsistente(AsistenteGestoGpu.Explicar);
            RocolaOs.Apunta("asistente", "guia de Spotify " + mision + " (" + RocolaOsAsistenteNombreGuia(mision) + ")");
            EvaluaGuia();
        }
        internal static string RocolaOsAsistenteNombreGuia(int mision)
        {
            return mision == RocolaOsMisionPide ? "Pedir una canción de Spotify" : mision == RocolaOsMisionLetra ? "La letra ♫ de Spotify"
                : mision == RocolaOsMisionArtista ? "Los discos de un artista" : "";
        }

        // ================================================================ lo que esta pasando =========================================

        private bool RocolaOsAsEnPared { get { return currentLetter == RocolaOsSpotifyParedMarca && rocolaOsSpPared != null && !EnDisco && !searchOpen; } }
        private bool RocolaOsAsEnDiscoSpotify { get { return EnDisco && !searchOpen && RocolaOsEsSpotify(currentAlbum); } }
        private bool RocolaOsAsEnLetra { get { return currentLetter == RocolaOsSpotifyMarca && !EnDisco && !searchOpen; } }
        // el disco elegido en la pared que se mira (sin pedir nada: g52 pide, esto solo mira)
        private Album RocolaOsAsElegidoEnPared()
        {
            if (albumView == null || albumView.Page == null) return null;
            int i = albumView.Selected;
            return i >= 0 && i < albumView.Page.Count ? albumView.Page[i] : null;
        }
        // la tarjeta elegida en el buscador: 0 nada (o lo de XP), 1 cancion de Spotify, 2 disco o lista de Spotify, 3 artista que solo esta
        // en Spotify, 4 un artista tuyo con su pared unida (tus discos y los de Spotify; 23-11-2026, antes «mas en Spotify»), 5 «Todos los discos de»
        private int RocolaOsAsTarjeta()
        {
            if (!EnResultados) return 0;
            int s = searchOverlay.SelResult;
            if (s < 0 || s >= searchOverlay.Results.Count) return 0;
            SearchItem it = searchOverlay.Results[s];
            if (it != null && it.AlbumRef != null && RocolaOsMejoresAlbumes.Contains(it.AlbumRef)) return 6;   // («LAS MEJORES CANCIONES DE ARTISTA», 27-11-2026)
            if (RocolaOsSpotifyUnidaDe(it) != null) return 4;
            // (23-11-2026) un artista tuyo que Spotify no conoce: tambien una pared del buscador (solo tus discos); su paso dice lo mismo que la unida
            if (it != null && it.Kind == 0 && it.AlbumRef != null && !RocolaOsEsSpotify(it.AlbumRef) && RocolaOsSpotifyCatalogoVa && RocolaOsSpotifyTuyos().Contains(MotorBusqueda.Normaliza(it.Display ?? ""))) return 4;
            if (it == null || !RocolaOsEsSpotify(it.AlbumRef)) return 0;
            RocolaOsSpotifyParedQue q;
            if (rocolaOsSpParedDe != null && rocolaOsSpParedDe.TryGetValue(it.AlbumRef, out q)) return 5;
            if (it.Kind == 2 && it.TrackRef != null) return 1;
            if (it.Kind == 0 && rocolaOsSpDeArtista != null && rocolaOsSpDeArtista.ContainsKey(it.AlbumRef)) return 3;
            if (it.Kind == 1) return 2;
            return 0;
        }
        private static readonly int[] RocolaOsPasoDeTarjeta = { 0, RocolaOsPasoCancionSpotify, RocolaOsPasoDiscoSpotify, RocolaOsPasoArtistaSpotify, RocolaOsPasoMasSpotify, RocolaOsPasoTodos, RocolaOsPasoMejores };

        // EL PASO QUE TOCA en una guia de Spotify, mirando solo el estado real
        private int RocolaOsAsistentePaso()
        {
            int m = asistenteMision;
            bool artista = m == RocolaOsMisionArtista;
            if (searchOpen)
            {
                if (!EnResultados) return artista ? RocolaOsPasoEscribeArtista : RocolaOsPasoEscribe;
                int t = RocolaOsAsTarjeta();
                if (t == 1) return credits <= 0 ? RocolaOsPasoMoneda : RocolaOsPasoCancionSpotify;
                if (t == 0) return artista ? RocolaOsPasoBuscaTarjeta : RocolaOsPasoBuscaSpotify;
                return RocolaOsPasoDeTarjeta[t];
            }
            if (EnDisco)
            {
                if (RocolaOsAsEnDiscoSpotify)
                {
                    if (artista && rocolaOsAsVioPared) return RocolaOsPasoVuelveDisco;
                    return credits <= 0 ? RocolaOsPasoMoneda : RocolaOsPasoCancionDisco;
                }
                return PasoVolver;   // (un disco de la rocola: primero volver, como XP)
            }
            if (RocolaOsAsEnPared)
            {
                if (artista && rocolaOsAsVioDisco) return RocolaOsPasoVuelveBuscador;
                Album a = RocolaOsAsElegidoEnPared();
                return a != null && rocolaOsSpPared.Canciones != null && ReferenceEquals(a, rocolaOsSpPared.Canciones) ? RocolaOsPasoParedCanciones : RocolaOsPasoParedDisco;
            }
            if (m == RocolaOsMisionLetra)
            {
                if (!letterAvailable.Contains(RocolaOsSpotifyMarca)) return RocolaOsPasoSinLetra;
                return currentLetter == RocolaOsSpotifyMarca ? RocolaOsPasoEntraDisco : RocolaOsPasoLetra;
            }
            return artista ? RocolaOsPasoAbreArtista : RocolaOsPasoAbre;
        }

        // g81 (EvaluaGuia: «if (asistenteMision == MisionCancion || asistenteMision == MisionBuscar)»): EL LATIDO DE UNA GUIA DE SPOTIFY.
        // true = era de Spotify y ya lo atendio; en las de XP, false (y sigue lo de XP). El logro ya lo mira XP antes (se guarda sola).
        private bool RocolaOsAsistenteEvalua()
        {
            int m = asistenteMision;
            if (m < RocolaOsMisionPide || m > RocolaOsMisionArtista) return false;
            if (m == RocolaOsMisionArtista)
            {
                if (RocolaOsAsEnPared) rocolaOsAsVioPared = true;
                if (rocolaOsAsVioPared && RocolaOsAsEnDiscoSpotify) rocolaOsAsVioDisco = true;
                // lo logro: volvio al buscador con REGRESAR desde la pared (entrara o no a un disco de ella: lo que se aprende es la vuelta)
                if (rocolaOsAsVioPared && searchOpen) { RocolaOsAsistenteLogro("volvio al buscador"); return true; }
            }
            else if (asistentePedidos != asistentePedidosBase) { RocolaOsAsistenteLogro(RocolaOsAsPidioSpotify ? "pidio una de Spotify" : "pidio una de la rocola"); return true; }
            RocolaOsAsistentePonPaso(RocolaOsAsistentePaso());
            return true;
        }
        private bool RocolaOsAsPidioSpotify { get { return ultimoPedido != null && ultimoPedido.Song != null && RocolaOsEsSpotify(ultimoPedido.Song); } }
        private void RocolaOsAsistenteLogro(string que)
        {
            Logro();
            RocolaOs.Apunta("asistente", "guia de Spotify " + asistenteMision + ": lo logro (" + que + ")");
        }

        // PonPaso de XP con el orden de las guias de Spotify (el de XP solo conoce el de las suyas): avanzar es «¡muy bien!» y la estrella
        // asiente; si no, senala (o busca, si toca la moneda)
        private void RocolaOsAsistentePonPaso(int paso)
        {
            if (paso == asistentePaso) return;
            int antes = RocolaOsAsistenteOrden(asistenteMision, asistentePaso), ahora = RocolaOsAsistenteOrden(asistenteMision, paso);
            bool avanza = asistentePaso != 0 && antes >= 0 && ahora > antes;
            asistentePaso = paso;
            if (avanza)
            {
                asistenteBienTick = Environment.TickCount; if (asistenteBienTick == 0) asistenteBienTick = 1;
                if (asistenteMonedaTick == 0 || unchecked(Environment.TickCount - asistenteMonedaTick) > 1200) AnimaAsistente(AsistenteGestoGpu.Confirmar);
            }
            else { asistenteBienTick = 0; AnimaAsistente(paso == RocolaOsPasoMoneda ? AsistenteGestoGpu.Buscar : AsistenteGestoGpu.Senalar); }
            asistenteActividadTick = Environment.TickCount;
            if (paso != rocolaOsAsPasoApuntado)
            {
                rocolaOsAsPasoApuntado = paso;
                string titulo, texto, teclas; RocolaOsAsistenteTextos(paso, out titulo, out texto, out teclas);
                RocolaOs.Apunta("asistente", "guia de Spotify " + asistenteMision + ": paso " + paso + " «" + titulo + "»");
            }
        }

        // EN QUE ORDEN VAN (para «PASO 2 DE 4» y saber si se avanzo). -1 = no es un paso de esa guia (sale «SIGAMOS»). Como en XP, la moneda
        // va antes del ultimo paso y solo cuenta sin credito.
        internal static int RocolaOsAsistenteOrden(int mision, int paso)
        {
            if (mision == RocolaOsMisionPide)
            {
                if (paso == RocolaOsPasoAbre) return 0;
                if (paso == RocolaOsPasoEscribe) return 1;
                if (paso == RocolaOsPasoMoneda) return 2;
                if (paso == RocolaOsPasoBuscaSpotify || paso == RocolaOsPasoCancionSpotify || paso == RocolaOsPasoDiscoSpotify || paso == RocolaOsPasoArtistaSpotify
                    || paso == RocolaOsPasoMasSpotify || paso == RocolaOsPasoTodos || paso == RocolaOsPasoCancionDisco || paso == RocolaOsPasoParedCanciones
                    || paso == RocolaOsPasoParedDisco || paso == RocolaOsPasoMejores) return 3;
                return -1;
            }
            if (mision == RocolaOsMisionLetra)
            {
                if (paso == RocolaOsPasoLetra || paso == RocolaOsPasoSinLetra) return 0;
                if (paso == RocolaOsPasoEntraDisco) return 1;
                if (paso == RocolaOsPasoMoneda) return 2;
                if (paso == RocolaOsPasoCancionDisco || paso == RocolaOsPasoCancionSpotify) return 3;
                // (revision: sin la letra ♫ la guia manda al buscador, «y sigue como la guia 1»: sus pasos cuentan en el lugar de «elegir un
                // disco», en vez de salir «SIGAMOS» en cada uno)
                if (paso == RocolaOsPasoEscribe || paso == RocolaOsPasoBuscaSpotify || paso == RocolaOsPasoDiscoSpotify || paso == RocolaOsPasoArtistaSpotify
                    || paso == RocolaOsPasoMasSpotify || paso == RocolaOsPasoTodos || paso == RocolaOsPasoParedCanciones || paso == RocolaOsPasoParedDisco || paso == RocolaOsPasoMejores) return 1;
                return -1;
            }
            if (mision == RocolaOsMisionArtista)
            {
                if (paso == RocolaOsPasoAbreArtista) return 0;
                if (paso == RocolaOsPasoEscribeArtista) return 1;
                if (paso == RocolaOsPasoBuscaTarjeta || paso == RocolaOsPasoArtistaSpotify || paso == RocolaOsPasoMasSpotify || paso == RocolaOsPasoTodos || paso == RocolaOsPasoMejores) return 2;
                if (paso == RocolaOsPasoParedCanciones || paso == RocolaOsPasoParedDisco) return 3;
                if (paso == RocolaOsPasoVuelveDisco || paso == RocolaOsPasoVuelveBuscador) return 4;
                return -1;
            }
            return -1;
        }
        // «Paso 2 de 4»: la moneda solo cuenta si hace falta (NumeroPaso de XP). 0 = no es de la guia.
        internal static int RocolaOsAsistenteNumero(int mision, int paso, bool conCredito, out int total)
        {
            int orden = RocolaOsAsistenteOrden(mision, paso);
            if (mision == RocolaOsMisionArtista) { total = 5; return orden + 1; }
            total = 4 - (conCredito ? 1 : 0);
            if (orden < 0) return 0;
            if (conCredito && orden == 2) return 0;
            return orden + 1 - (conCredito && orden > 2 ? 1 : 0);
        }

        // g82 (ObjetivoGuia: «switch (paso)»): LO QUE SENALA cada paso de Spotify es lo mismo que senala un paso de XP (el buscador, el
        // teclado, la tarjeta elegida, la barra de letras, los creditos, el disco o la cancion elegidos, la cabecera): se le da ese paso. Los de
        // XP, tal cual.
        internal static int RocolaOsAsistenteObjetivo(int paso)
        {
            if (paso < RocolaOsPasoAbre) return paso;
            switch (paso)
            {
                case RocolaOsPasoAbre: case RocolaOsPasoAbreArtista: case RocolaOsPasoSinLetra: return PasoAbreBuscador;
                case RocolaOsPasoEscribe: case RocolaOsPasoEscribeArtista: return PasoEscribe;
                case RocolaOsPasoBuscaSpotify: case RocolaOsPasoCancionSpotify: case RocolaOsPasoDiscoSpotify: case RocolaOsPasoArtistaSpotify:
                case RocolaOsPasoMasSpotify: case RocolaOsPasoTodos: case RocolaOsPasoBuscaTarjeta: case RocolaOsPasoMejores: return PasoResultado;
                case RocolaOsPasoMoneda: return PasoMoneda;
                case RocolaOsPasoCancionDisco: return PasoCancion;
                case RocolaOsPasoVuelveDisco: return PasoVolver;
                case RocolaOsPasoLetra: return PasoLetra;
                case RocolaOsPasoEntraDisco: case RocolaOsPasoParedCanciones: case RocolaOsPasoParedDisco: return PasoDisco;
                case RocolaOsPasoVuelveBuscador: return PasoPagina;   // (la cabecera: dice el nombre del artista)
            }
            return 0;
        }

        // ================================================================ lo que dice el globo ========================================
        // Con el tono de XP (TextosGuia): titulo corto, una frase y los botones dibujados (B rojo, V verde, M moneda, U/D flechas rojas, L/R
        // amarillas, '+' a la vez, '/' o).
        internal static void RocolaOsAsistenteTextos(int paso, out string titulo, out string texto, out string teclas)
        {
            switch (paso)
            {
                case RocolaOsPasoAbre: titulo = "Abre el buscador"; texto = "Mantén el botón ROJO y pulsa la flecha de ARRIBA. Ahí salen también las canciones de Spotify."; teclas = "B+U"; break;
                case RocolaOsPasoAbreArtista: titulo = "Abre el buscador"; texto = "Mantén el botón ROJO y pulsa la flecha de ARRIBA para buscar al artista."; teclas = "B+U"; break;
                case RocolaOsPasoSinLetra: titulo = RocolaOsSpotifyLetraEnLaPared ? "Aún no hay letra ♫" : "Spotify, en el buscador"; texto = RocolaOsSpotifyLetraEnLaPared ? "Sale con tu cuenta o al abrir un disco de Spotify. Mientras, búscalas: ROJO + ARRIBA." : "Los discos y listas de Spotify se buscan: ROJO + ARRIBA."; teclas = "B+U"; break;
                case RocolaOsPasoEscribe: titulo = "Escribe el nombre"; texto = "Elige las letras con las flechas y VERDE. Luego ve con las flechas a las de Spotify."; teclas = "L/R U/D V"; break;
                case RocolaOsPasoEscribeArtista: titulo = "Escribe el artista"; texto = "Sale su tarjeta. Arriba, «LAS MEJORES CANCIONES DE…» trae sus más escuchadas."; teclas = "L/R U/D V"; break;
                case RocolaOsPasoBuscaSpotify: titulo = "Busca las de Spotify"; texto = "Esa es de la rocola. Muévete con las flechas hasta una canción de Spotify."; teclas = "L/R U/D"; break;
                case RocolaOsPasoBuscaTarjeta: titulo = "Busca su tarjeta"; texto = "Muévete hasta el artista o «Todos los discos de...»."; teclas = "L/R U/D"; break;
                case RocolaOsPasoCancionSpotify: titulo = "Pídela con VERDE"; texto = "Esta canción es de Spotify. Se pide como las tuyas y cuesta 1 crédito."; teclas = "V"; break;
                case RocolaOsPasoDiscoSpotify: titulo = "Un disco de Spotify"; texto = "Pulsa VERDE para abrirlo y elige tu canción con las flechas."; teclas = "V"; break;
                case RocolaOsPasoArtistaSpotify: titulo = "Un artista de Spotify"; texto = "No está en la rocola. Pulsa VERDE y verás sus discos en su propia pared."; teclas = "V"; break;
                case RocolaOsPasoMasSpotify: titulo = "Sus discos"; texto = "Pulsa VERDE y verás sus discos juntos en su propia pared."; teclas = "V"; break;
                case RocolaOsPasoMejores: titulo = "Sus mejores canciones"; texto = "Pulsa VERDE: salen sus más escuchadas, y las que tienes tú, primero."; teclas = "V"; break;
                case RocolaOsPasoTodos: titulo = "Todos los discos"; texto = "Pulsa VERDE y verás todos los discos que salen con lo que escribiste."; teclas = "V"; break;
                case RocolaOsPasoMoneda: titulo = "Mete una moneda"; texto = "Sin créditos no se pide: cada canción, también las de Spotify, cuesta 1 crédito."; teclas = "M"; break;
                case RocolaOsPasoCancionDisco: titulo = "Elige tu canción"; texto = "Sube o baja con las flechas y pulsa VERDE para pedirla. El ROJO te regresa."; teclas = "U/D V"; break;
                case RocolaOsPasoLetra: titulo = "Ve a la letra ♫"; texto = "Mantén ROJO y pulsa IZQUIERDA o DERECHA: en la ♫ están los discos y listas de Spotify."; teclas = "B+L/R"; break;
                case RocolaOsPasoEntraDisco: titulo = "Elige un disco o lista"; texto = "Muévete con las flechas y pulsa VERDE para ver sus canciones."; teclas = "L/R U/D V"; break;
                case RocolaOsPasoParedCanciones: titulo = "Sus canciones"; texto = "La primera casilla trae sus canciones. VERDE abre; el ROJO te regresa al buscador."; teclas = "V B"; break;
                case RocolaOsPasoParedDisco: titulo = "Su pared de discos"; texto = "Muévete con las flechas; ARRIBA o ABAJO cambian de página. VERDE abre un disco."; teclas = "L/R U/D V"; break;
                case RocolaOsPasoVuelveDisco: titulo = "Vuelve con ROJO"; texto = "El botón ROJO te regresa a su pared, en la misma casilla."; teclas = "B"; break;
                case RocolaOsPasoVuelveBuscador: titulo = "Vuelve al buscador"; texto = "Pulsa ROJO otra vez: regresas al buscador con lo que escribiste."; teclas = "B"; break;
                default: TextosGuia(paso, out titulo, out texto, out teclas); break;   // (los de XP: volver de un disco de la rocola)
            }
        }

        // ================================================================ «AYUDA» siempre en su casilla ===============================
        // Peticion del dueno del 15-11-2026 («La casilla del asistente debe decir ayuda»): con Rocola OS activo la casilla de arriba dice SIEMPRE
        // «AYUDA» junto a su signo, tambien en 1024x768, donde XP la dejaba compacta (solo «?») para darle sitio a la cabecera.
        // g84 (AcomodaAyuda: «int pideApretado = pide * 85 / 100;»): XP, antes de quitarle la palabra, deja que la cabecera ceda hasta un 15 %
        // de lo que pide; con Rocola OS cede todo lo que haga falta hasta quedarse con 90 px (lo minimo con que XP la deja: menos y XP esconde
        // la casilla entera). La cabecera se encoge como ya lo hace XP en pantallas angostas (su nombre se achica de medio en medio punto y el
        // numero de pagina pasa a su forma corta: HeaderView.RenderContent). Sin ROCOLA_OS, lo de XP.
        internal static int RocolaOsAyudaCede(int pide)
        {
            int xp = pide * 85 / 100;
            if (!RocolaOs.Activo) return xp;
            return Math.Min(xp, 90);
        }
    }
}

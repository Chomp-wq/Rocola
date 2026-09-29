// EL BUSCADOR CON PORTADAS (pantalla viva, tanda 6, 24-09-2026): la idea "Teclado compacto y resultados con portada"
// del lienzo de bocetos. Solo con tarjeta grafica y con su interruptor en Ctrl+F3 -> Pantalla (buscador_portadas=).
// Sin tarjeta grafica, o con el interruptor apagado, el buscador es exactamente el de siempre: el dibujo por procesador
// no se toca.
//
// Con la idea encendida:
//  - ARRIBA, la casilla de lo escrito, mas grande, con su lupa, y a la derecha cuantos resultados hay.
//  - DEBAJO, LOS ATAJOS. Solo los que tienen algo de verdad detras, y en este orden:
//      * "Lo mas pedido hoy": las canciones mas pedidas desde las 8 de la manana, de mas a menos (la misma cuenta que
//        usa el modo atraccion, AtraccionGpu.cs).
//      * "Videos": lleva a la seccion de videos de la pared, igual que su letra.
//      * "Karaoke": los discos cuyo genero es Karaoke (el mismo genero que pinta las pastillas).
//      * "Buscaste: ...": las dos ultimas busquedas con las que alguien eligio algo, de esta noche. Solo en memoria, y
//        nunca la clave de servicio.
//    Si no caben todos, se quedan fuera los ultimos.
//  - A LA IZQUIERDA, EL TECLADO COMPACTO: teclas de tamano fijo, sin estirarlas hasta llenar media pantalla, y siempre
//    a la vista (elegir un resultado ya no lo tapa con la portada grande).
//  - A LA DERECHA, LAS TARJETAS: dos columnas de tarjetas grandes con su portada (o las iniciales, mientras la portada
//    no llega), que es (artista, album o cancion), el nombre con lo tecleado resaltado, un renglon con lo que ayuda a
//    elegir y SONANDO o EN COLA si toca. Si hay mas de las que caben, se baja fila a fila y una barrita lo dice.
//  - LAS TARJETAS QUE LLEGAN ENTRAN UNA TRAS OTRA, subiendo un poco mientras aparecen. Lo hace la tarjeta grafica: la
//    imagen de las tarjetas se hace una vez y cada tarjeta se dibuja como su propio trozo, con su transparencia.
//    Solo entran las que cambiaron; las que siguen iguales se quedan quietas.
//
// COMO SE REPARTE EL TRABAJO (igual que el buscador de siempre: ver BuscadorLigeroGpu.cs y EnsureUiPieza):
//  - la imagen grande lleva solo lo quieto: los atajos, el teclado sin elegir y las ayudas. Ni teclear, ni posar la
//    consulta, ni moverse la tocan;
//  - la casilla de lo escrito, la tecla (o el atajo) elegida y la tarjeta elegida van en sus imagenes chicas;
//  - las tarjetas van en otra imagen, la pieza 4, que se rehace al posar la consulta, al bajar de fila, al cambiar la
//    cola o al llegar una portada.
//
// LAS PORTADAS se piden al disco solo para las tarjetas que se ven, y cuando la vista lleva un cuarto de segundo quieta:
// las consultas intermedias de quien teclea deprisa no piden nada. Van por la cola de siempre, con sus carriles contados y
// por debajo de la interfaz (Form1.Portadas.cs). Ya escaladas, se guardan las ultimas 64.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;

namespace Rocola
{
    public partial class Form1
    {
        // Existe en los dos perfiles porque la configuracion es la misma; sin tarjeta grafica no se usa.
        private bool buscadorPortadas = true;   // "buscador_portadas="

        // ---------------- LOS ENGANCHES (sin tarjeta grafica no hacen nada) ----------------
        // Se abre el buscador: el reparto y los atajos, al dia.
        private void AlAbrirBuscador()
        {
#if ROCOLA_GPU
            AlAbrirBuscadorGpu();
#endif
        }
        // Cuantas coincidencias tiene la consulta posada: el buscador ensena las 40 mejores, pero la casilla dice todas.
        private void CuentaResultados(string consulta, int total)
        {
#if ROCOLA_GPU
            if (searchOverlay != null) { searchOverlay.TotalResultados = total; searchOverlay.TotalDe = consulta; }
#endif
        }
        // Alguien eligio un resultado: la consulta que lo llevo ahi pasa a los atajos ("Buscaste: ...").
        private void ApuntaBusqueda(SearchItem it)
        {
#if ROCOLA_GPU
            ApuntaBusquedaGpu(it);
#endif
        }
        // Llego una portada del disco: si la esperaba una tarjeta del buscador, las tarjetas se rehacen.
        private void PortadaLlegoBuscador(string clave)
        {
#if ROCOLA_GPU
            if (searchOpen && searchOverlay != null && searchOverlay.Nuevo && searchOverlay.EsperaPortada(clave))
                searchOverlay.PortadasVer++;
#endif
        }
    }

#if ROCOLA_GPU
    // Una tarjeta de resultado tal como se la pasa el formulario a la tarjeta grafica: su trozo de la imagen de las
    // tarjetas (coordenadas de esa imagen), cuanto se ve y cuanto le falta por subir.
    internal struct CartaGpu { public float X0, Y0, X1, Y1, Alfa, Dy; }

    public partial class Form1
    {
        // Manda el buscador nuevo: el interruptor encendido y la tarjeta grafica dibujando.
        private Rectangle[] uiCartasCambios;
        private int uiCartasCambiosVer = -1;
        private Rectangle[] CambiosPiezaBuscador(int cual, int desde)
        {
            return cual == SearchOverlay.PiezaCartas && desde == uiCartasCambiosVer - 1
                && uiCartasCambiosVer == uiPiezaVer[cual] ? uiCartasCambios : null;
        }
        private bool BuscadorPortadasActivo() { return buscadorPortadas && gpuEnabled && gpuPanel != null; }

        // Al cambiar el interruptor, al encender o apagar la tarjeta y al abrir el buscador.
        private void AjustaBuscadorPortadas()
        {
            if (searchOverlay == null) return;
            searchOverlay.OnAtajo = UsaAtajo;
            searchOverlay.GetDuracion = DuracionResultado;
            bool era = searchOverlay.Nuevo;
            searchOverlay.PonNuevo(BuscadorPortadasActivo());
            if (era != searchOverlay.Nuevo && searchOpen) ArmaAtajos();
        }
        // Con el buscador abierto (el nuevo o el de siempre), sus piezas son parte de la ventana: van con ella al abrir y al
        // cerrar (GpuPanel.PiezasConLaVentana).
        private bool PiezasSubenConLaVentana() { return searchOpen && searchOverlay != null; }
        // Lo que se desliza de la vista previa del buscador (Titulos largos deslizantes, TextoMovilGpu.cs): en el de siempre,
        // el texto de debajo del cubo (la pieza 3); en el buscador con portadas no hay cubo, y es la tarjeta elegida (la 2).
        private int PiezaVistaPreviaBuscador() { return (searchOverlay != null && searchOverlay.Nuevo) ? 2 : 3; }
        private void AlAbrirBuscadorGpu()
        {
            AjustaBuscadorPortadas();
            if (searchOverlay == null) return;
            searchOverlay.ReiniciaNuevo();
            if (searchOverlay.Nuevo) ArmaAtajos();
        }
        private string DuracionResultado(SearchItem it)
        {
            return (it != null && it.TrackRef != null) ? DurText(it.TrackRef.Path) : "";
        }

        // ---------------- LOS ATAJOS ----------------
        internal const int AtajoPedidas = 0, AtajoVideos = 1, AtajoKaraoke = 2, AtajoBuscaste = 3;
        internal const int BuscadasMax = 2, AtajoResultadosMax = 40;
        private sealed class AtajoBusca { public int Tipo; public string Texto; public string Consulta; }
        private readonly List<AtajoBusca> atajosBusca = new List<AtajoBusca>();
        private readonly List<string> buscadas = new List<string>();   // la mas nueva primero
        private string buscadasNoche;

        private void ArmaAtajos()
        {
            atajosBusca.Clear();
            if (searchOverlay == null) return;
            DateTime ahora = DateTime.Now;
            if (!libBuilding)
            {
                CambiaDeNoche(ahora);
                if (nocheCuenta.Count > 0) AgregaAtajo(AtajoPedidas, "Lo m\u00e1s pedido hoy", null);
                if (videoAlbums != null && videoAlbums.Count > 0) AgregaAtajo(AtajoVideos, "Videos", null);
                if (DiscosKaraoke().Count > 0) AgregaAtajo(AtajoKaraoke, "Karaoke", null);
            }
            OlvidaBuscadasViejas(ahora);
            for (int i = 0; i < buscadas.Count; i++) AgregaAtajo(AtajoBuscaste, "Buscaste: " + TextoBuscada(buscadas[i]), buscadas[i]);
            List<string> textos = new List<string>(atajosBusca.Count);
            for (int i = 0; i < atajosBusca.Count; i++) textos.Add(atajosBusca[i].Texto);
            searchOverlay.PonAtajos(textos);
        }
        private void AgregaAtajo(int tipo, string texto, string consulta)
        {
            AtajoBusca a = new AtajoBusca(); a.Tipo = tipo; a.Texto = texto; a.Consulta = consulta;
            atajosBusca.Add(a);
        }
        // Las busquedas son de la noche (de 8 de la manana a 8 de la manana, como lo mas pedido): la de ayer no se ensena.
        private void OlvidaBuscadasViejas(DateTime ahora)
        {
            string noche = ClaveNoche(ahora);
            if (buscadasNoche != noche) { buscadas.Clear(); buscadasNoche = noche; }
        }
        // Como se lee en el atajo: con mayuscula al principio de cada palabra, y corta si es larga.
        internal static string TextoBuscada(string q)
        {
            string t = (q ?? "").Trim();
            if (t.Length == 0) return t;
            t = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(t.ToLowerInvariant());
            if (t.Length > 18) t = t.Substring(0, 17).TrimEnd() + "\u2026";
            return t;
        }

        private void ApuntaBusquedaGpu(SearchItem it)
        {
            if (searchOverlay == null || !searchOverlay.Nuevo || it == null || it.Kind < 0 || it.Kind > 2) return;
            if (searchOverlay.ModoAtajo != null) return;   // lo puso un atajo, no una busqueda
            string q = (searchOverlay.Query ?? "").Trim();
            if (q.Length < 2 || SecretCreditGate.Matches(q)) return;   // nunca la clave de servicio
            OlvidaBuscadasViejas(DateTime.Now);
            string n = Normalize(q);
            for (int i = buscadas.Count - 1; i >= 0; i--) if (Normalize(buscadas[i]) == n) buscadas.RemoveAt(i);
            buscadas.Insert(0, q);
            while (buscadas.Count > BuscadasMax) buscadas.RemoveAt(buscadas.Count - 1);
            if (searchOpen) ArmaAtajos();
        }

        // Pulsaron un atajo.
        private void UsaAtajo(int k)
        {
            if (searchOverlay == null || k < 0 || k >= atajosBusca.Count) return;
            MarkSearchActivity();
            AtajoBusca a = atajosBusca[k];
            if (a.Tipo == AtajoVideos)
            {
                if (videoAlbums == null || videoAlbums.Count == 0 || letterBar == null || letterBar.OnPick == null) return;
                CloseSearch();
                letterBar.OnPick(VideoMark);   // lo mismo que elegir la letra de los videos
                return;
            }
            if (a.Tipo == AtajoBuscaste) { searchOverlay.BuscaAtajo(a.Consulta); return; }
            searchOverlay.MuestraAtajo(a.Texto, a.Tipo == AtajoPedidas ? PedidasHoy() : DiscosKaraoke());
        }

        // Las mas pedidas de esta noche que siguen en la biblioteca, de mas a menos. Cada una es la cancion del indice del
        // buscador, asi que al elegirla se pide como cualquier resultado.
        private List<SearchItem> PedidasHoy()
        {
            List<SearchItem> res = new List<SearchItem>();
            if (libBuilding || searchIndex == null) return res;
            CambiaDeNoche(DateTime.Now);
            List<string> rutas = MasPedidasNoche(nocheCuenta, nocheOrden, AtajoResultadosMax);
            if (rutas.Count == 0) return res;
            Dictionary<string, int> quiero = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rutas.Count; i++) quiero[rutas[i]] = i;
            SearchItem[] hallada = new SearchItem[rutas.Count];
            int faltan = rutas.Count;
            for (int i = 0; i < searchIndex.Count && faltan > 0; i++)
            {
                SearchItem it = searchIndex[i]; int j;
                if (it == null || it.Kind != 2 || it.TrackRef == null || it.TrackRef.Path == null) continue;
                if (!quiero.TryGetValue(it.TrackRef.Path, out j) || hallada[j] != null) continue;
                hallada[j] = it; faltan--;
            }
            for (int j = 0; j < hallada.Length; j++) if (hallada[j] != null) res.Add(hallada[j]);
            return res;
        }

        // Los discos cuyo genero es Karaoke, por nombre. Se guarda la lista hasta que cambie el indice o se aprendan
        // generos nuevos.
        private List<SearchItem> karaokeLista, karaokeIndice; private int karaokeGen = -1, karaokeN = -1;
        private List<SearchItem> DiscosKaraoke()
        {
            if (libBuilding || searchIndex == null) return new List<SearchItem>();
            if (karaokeLista != null && karaokeGen == generoVer && ReferenceEquals(karaokeIndice, searchIndex) && karaokeN == searchIndex.Count)
                return karaokeLista;
            List<SearchItem> res = new List<SearchItem>();
            for (int i = 0; i < searchIndex.Count; i++)
            {
                SearchItem it = searchIndex[i];
                if (it == null || it.Kind != 1 || it.AlbumRef == null) continue;
                if (string.Equals(GeneroDeCancion(null, it.AlbumRef), "Karaoke", StringComparison.Ordinal)) res.Add(it);
            }
            res.Sort(delegate (SearchItem a, SearchItem b) { return string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase); });
            if (res.Count > AtajoResultadosMax) res.RemoveRange(AtajoResultadosMax, res.Count - AtajoResultadosMax);
            karaokeLista = res; karaokeGen = generoVer; karaokeIndice = searchIndex; karaokeN = searchIndex.Count;
            return res;
        }

        // ---------------- PARA LA TARJETA GRAFICA ----------------
        // Cada tarjeta de la pieza 4, con su entrada. Tambien dice cuanto se ve la tarjeta elegida, que va aparte y
        // entra con la suya.
        private int FillCartas(CartaGpu[] buf, out float elegida)
        {
            elegida = 1f;
            if (!searchOpen || searchOverlay == null || !searchOverlay.Nuevo) return 0;
            return searchOverlay.LlenaCartas(buf, GpuPanel.AhoraMs(), out elegida);
        }
        // De donde sale la portada al entrar a un disco desde el buscador nuevo: de la portada de su tarjeta.
        private RectangleF PortadaElegidaBuscador()
        {
            Rectangle r;
            if (searchOverlay == null || !searchOverlay.PortadaElegida(out r)) return RectangleF.Empty;
            return new RectangleF(searchOverlay.Left + r.X, searchOverlay.Top + r.Y, r.Width, r.Height);
        }

        private partial class SearchOverlay
        {
            private bool nuevo;
            public bool Nuevo { get { return nuevo; } }
            public bool EnChips;                 // la seleccion esta en la fila de atajos
            public int SelChip;
            public int TotalResultados;          // cuantas coincidencias tiene la consulta TotalDe (la casilla lo dice)
            public string TotalDe;
            public string ModoAtajo;             // null = busqueda normal; si no, el atajo que puso los resultados
            public int PortadasVer;              // sube al llegar una portada que esperaba una tarjeta
            public Action<int> OnAtajo;
            public Func<SearchItem, string> GetDuracion;
            public const int PiezaCartas = 4;    // la imagen de las tarjetas (ver NumPiezas)
            internal const int CartasMax = 40;
            internal const double EntradaMs = 240, EscalonMs = 60, EsperaPedirMs = 250, JuntaPortadasMs = 120;
            internal const int EscalonesMax = 8;
            private readonly List<string> atajos = new List<string>();
            private int atajosVer;
            private readonly List<Rectangle> atajoRects = new List<Rectangle>();
            private int atajosMedidosVer = -1, atajosMedidosW = -1; private float atajosMedidosS = -1f;
            private int resScroll;               // primera fila de tarjetas que se ve

            public void PonNuevo(bool v)
            {
                if (v == nuevo) return;
                nuevo = v;
                ReiniciaNuevo();
                if (!v) SueltaHorneados();   // apagado: lo horneado ya no sirve
                Invalidate();
            }
            public void ReiniciaNuevo()
            {
                EnChips = false; SelChip = 0; resScroll = 0; ModoAtajo = null; TotalResultados = 0; TotalDe = null;
            }
            public void PonAtajos(List<string> textos)
            {
                bool igual = textos.Count == atajos.Count;
                for (int i = 0; igual && i < textos.Count; i++) if (textos[i] != atajos[i]) igual = false;
                if (igual) return;
                atajos.Clear(); atajos.AddRange(textos); atajosVer++;
                if (atajos.Count == 0) EnChips = false;
                if (SelChip >= atajos.Count) SelChip = Math.Max(0, atajos.Count - 1);
                Invalidate();
            }
            internal int AtajosVisibles() { AseguraReparto(); return atajoRects.Count; }

            // Un atajo que pone resultados sin teclear (lo mas pedido, karaoke). La casilla dice cual es.
            public void MuestraAtajo(string etiqueta, List<SearchItem> lista)
            {
                Escrito = ""; Query = "";
                ModoAtajo = etiqueta;
                Results = new List<SearchItem>(lista);   // lista propia: asi las tarjetas saben que llego otra
                TotalResultados = Results.Count; TotalDe = null;
                resScroll = 0; EnChips = false;
                FocusResults = Results.Count > 0; SelResult = 0;
                Invalidate();
            }
            // "Buscaste: ...": se vuelve a buscar y la seleccion salta a los resultados.
            public void BuscaAtajo(string consulta)
            {
                ModoAtajo = null;
                Escrito = consulta ?? "";
                PosaConsulta();   // si ya estaba puesta no hace nada
                resScroll = 0; EnChips = false;
                if (Results != null && Results.Count > 0) { FocusResults = true; SelResult = 0; }
                Invalidate();
            }
            // Borrar con la casilla vacia en un atajo: se sale del atajo.
            private void SalDeAtajo()
            {
                ModoAtajo = null;
                Results = new List<SearchItem>();
                TotalResultados = 0; TotalDe = null;
                FocusResults = false; SelResult = 0; resScroll = 0;
                Invalidate();
            }
            // La consulta posada cambio (Changed): las tarjetas vuelven arriba y el atajo, si lo habia, se acabo.
            private void AlCambiarConsulta()
            {
                resScroll = 0; ModoAtajo = null; EnChips = false;
            }

            // ======================================================================================================
            //  EL REPARTO (sacado del boceto, que mide 1280x720, y escalado a lo que mida el buscador)
            // ======================================================================================================
            internal struct RepartoBusca
            {
                public float S;
                public int M, Tecla, Hueco, HuecoFila4, CartaW, CartaH, CartaHueco, Filas;
                public Rectangle Caja, Atajos, Teclado, Resultados, Barra, Ayudas;
            }
            internal static int Px(float v, float s) { return (int)Math.Round(v * s); }
            internal static RepartoBusca Reparto(int W, int H)
            {
                RepartoBusca r = new RepartoBusca();
                float s = Math.Min(W / 1280f, H / 720f);
                if (s < .6f) s = .6f; else if (s > 2f) s = 2f;
                r.S = s; r.M = Px(30, s);
                int ancho = Math.Max(40, W - 2 * r.M);
                r.Caja = new Rectangle(r.M, Px(26, s), ancho, Px(70, s));
                r.Atajos = new Rectangle(r.M, r.Caja.Bottom + Px(24, s), ancho, Px(34, s));
                r.Tecla = Px(50, s); r.Hueco = Math.Max(3, Px(8, s)); r.HuecoFila4 = Math.Max(4, Px(10, s));
                r.Teclado = new Rectangle(r.M, r.Atajos.Bottom + Px(36, s), 10 * r.Tecla + 9 * r.Hueco, 5 * r.Tecla + 4 * r.Hueco);
                r.Ayudas = new Rectangle(r.M, H - Px(48, s), ancho, Px(22, s));
                int x = r.Teclado.Right + Px(58, s), tope = r.Ayudas.Top - Px(18, s);
                r.CartaHueco = Px(12, s); r.CartaH = Px(106, s);
                r.CartaW = Math.Max(1, (Math.Max(2, W - r.M - x) - r.CartaHueco) / 2);
                r.Filas = Math.Max(1, (tope - r.Teclado.Top + r.CartaHueco) / (r.CartaH + r.CartaHueco));
                r.Resultados = new Rectangle(x, r.Teclado.Top, 2 * r.CartaW + r.CartaHueco, r.Filas * r.CartaH + (r.Filas - 1) * r.CartaHueco);
                r.Barra = new Rectangle(r.Resultados.Right + Px(8, s), r.Resultados.Top, Math.Max(2, Px(4, s)), r.Resultados.Height);
                return r;
            }
            private RepartoBusca rep; private int repW = -1, repH = -1;
            // El reparto de ahora, con las teclas y los atajos ya puestos en su sitio. Las teclas se colocan siempre: el
            // camino de siempre (LayoutKeys) las mueve a su reparto cuando pinta, y esto las devuelve al nuevo.
            private RepartoBusca AseguraReparto()
            {
                if (Width != repW || Height != repH) { rep = Reparto(Width, Height); repW = Width; repH = Height; }
                LayoutKeysNuevo(rep);
                MideAtajos(rep);
                return rep;
            }
            private void LayoutKeysNuevo(RepartoBusca r)
            {
                for (int f = 0; f < rows.Count && f < 5; f++)
                {
                    List<SearchKey> fila = rows[f];
                    int y = r.Teclado.Y + f * (r.Tecla + r.Hueco);
                    if (f < 4)
                    {
                        int n = fila.Count, w = n * r.Tecla + (n - 1) * r.Hueco;
                        int x0 = r.Teclado.X + (r.Teclado.Width - w) / 2;
                        for (int c = 0; c < n; c++) fila[c].R = new RectangleF(x0 + c * (r.Tecla + r.Hueco), y, r.Tecla, r.Tecla);
                    }
                    else
                    {
                        // ESPACIO y BORRAR, en la proporcion del boceto: 340 y 222 de 562
                        int total = r.Teclado.Width - r.HuecoFila4 * (fila.Count - 1), x = r.Teclado.X;
                        int primera = (fila.Count == 2) ? (int)Math.Round(total * 340f / 562f) : total / Math.Max(1, fila.Count);
                        for (int c = 0; c < fila.Count; c++)
                        {
                            int w = (fila.Count == 2 && c == 1) ? total - primera : primera;
                            fila[c].R = new RectangleF(x, y, w, r.Tecla);
                            x += w + r.HuecoFila4;
                        }
                    }
                }
            }
            private void MideAtajos(RepartoBusca r)
            {
                if (atajosMedidosVer == atajosVer && atajosMedidosS == r.S && atajosMedidosW == r.Atajos.Width) return;
                AseguraFuentes(r.S);
                atajoRects.Clear();
                int x = r.Atajos.X, pad = Px(14, r.S), hueco = Px(10, r.S);
                for (int i = 0; i < atajos.Count; i++)
                {
                    int w = (int)Math.Ceiling(Medidor().MeasureString(atajos[i], fnAtajo, 9999, sfMide).Width) + 2 * pad;
                    if (x + w > r.Atajos.Right) break;   // no cabe: los ultimos se quedan fuera
                    atajoRects.Add(new Rectangle(x, r.Atajos.Y, w, r.Atajos.Height));
                    x += w + hueco;
                }
                atajosMedidosVer = atajosVer; atajosMedidosS = r.S; atajosMedidosW = r.Atajos.Width;
                if (atajoRects.Count == 0) EnChips = false;
                else if (SelChip >= atajoRects.Count) SelChip = atajoRects.Count - 1;
            }
            // Donde va la tarjeta i (de los resultados), en coordenadas del buscador. Vacio si no se ve.
            internal Rectangle CartaRect(int i)
            {
                RepartoBusca r = rep;
                if (i < 0) return Rectangle.Empty;
                int fila = i / 2 - resScroll, col = i % 2;
                if (fila < 0 || fila >= r.Filas) return Rectangle.Empty;
                return new Rectangle(r.Resultados.X + col * (r.CartaW + r.CartaHueco), r.Resultados.Y + fila * (r.CartaH + r.CartaHueco), r.CartaW, r.CartaH);
            }
            internal int CartasTotales() { return Math.Min(Results == null ? 0 : Results.Count, CartasMax); }
            private int FilasTotales() { return (CartasTotales() + 1) / 2; }
            private void AjustaScroll()
            {
                RepartoBusca r = rep;
                int fila = SelResult / 2;
                if (fila < resScroll) resScroll = fila;
                else if (fila >= resScroll + r.Filas) resScroll = fila - r.Filas + 1;
                int tope = Math.Max(0, FilasTotales() - r.Filas);
                if (resScroll > tope) resScroll = tope;
                if (resScroll < 0) resScroll = 0;
            }
            // La portada de la tarjeta elegida (para que al entrar a un disco la portada salga de ahi).
            internal bool PortadaElegida(out Rectangle r)
            {
                r = Rectangle.Empty;
                AseguraReparto();
                int i = FocusResults ? SelResult : 0;
                if (i < 0 || i >= CartasTotales()) return false;
                Rectangle c = CartaRect(i);
                if (c.IsEmpty) return false;
                r = CajaPortadaCarta(c, rep.S);
                return true;
            }
            private static Rectangle CajaPortadaCarta(Rectangle c, float s)
            {
                int pad = Px(10, s), lado = Math.Max(4, c.Height - 2 * pad - Px(2, s));
                return new Rectangle(c.X + pad, c.Y + (c.Height - lado) / 2, lado, lado);
            }

            // ======================================================================================================
            //  COLORES Y LETRA (los del boceto; donde el boceto usaba su naranja, aqui va el color de la seccion)
            // ======================================================================================================
            // El mismo tono, con el canal mas alto a 240: el color de la seccion puede venir oscuro, y los bordes y la
            // tecla elegida tienen que verse igual con cualquiera.
            internal static Color Vivo(Color c)
            {
                int m = Math.Max(c.R, Math.Max(c.G, c.B));
                if (m < 8) return Color.FromArgb(240, 138, 42);
                float k = 240f / m;
                return Color.FromArgb(Math.Min(255, (int)(c.R * k + .5f)), Math.Min(255, (int)(c.G * k + .5f)), Math.Min(255, (int)(c.B * k + .5f)));
            }
            internal static Color FondoTecla(Color v)
            {
                return Color.FromArgb(184, Math.Min(255, (int)(v.R * .12f) + 14), Math.Min(255, (int)(v.G * .12f) + 14), Math.Min(255, (int)(v.B * .12f) + 17));
            }
            internal static readonly Color FondoCarta = Color.FromArgb(219, 20, 26, 54), FondoCartaElegida = Color.FromArgb(255, 30, 37, 72);
            internal static readonly Color FondoCaja = Color.FromArgb(209, 20, 30, 60);
            internal static readonly Color TextoAtajo = Color.FromArgb(207, 224, 255), BordeAtajo = Color.FromArgb(128, 150, 200, 255), FondoAtajo = Color.FromArgb(115, 30, 50, 100);
            internal static readonly Color TextoSub = Color.FromArgb(170, 184, 222), TextoCuentaColor = Color.FromArgb(199, 211, 245);
            internal static readonly Color TipoArtista = Color.FromArgb(156, 195, 255), TipoServicio = Color.FromArgb(170, 178, 200);
            internal static readonly Color Resalte = Color.FromArgb(89, 255, 198, 104), TextoResalte = Color.FromArgb(255, 226, 176);
            internal static readonly Color BorrarFondo = Color.FromArgb(184, 60, 20, 20), BorrarBorde = Color.FromArgb(153, 228, 72, 72), BorrarTinta = Color.FromArgb(255, 215, 215);
            internal static readonly Color AyudaTinta = Color.FromArgb(223, 230, 255), Invita = Color.FromArgb(143, 155, 194);

            private Font fnCaja, fnCuenta, fnAtajo, fnTecla, fnTeclaPal, fnTipo, fnTitulo, fnSub, fnMarca, fnAyuda, fnVacio, fnIniciales;
            private float fnS = -1f;
            private StringFormat sfMide, sfIzq, sfLinea, sfCentro;
            private Bitmap medidorBmp; private Graphics medidor;
            private Graphics Medidor()
            {
                if (medidor == null)
                {
                    medidorBmp = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
                    medidor = Graphics.FromImage(medidorBmp);
                    medidor.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                }
                return medidor;
            }
            private void AseguraFuentes(float s)
            {
                if (sfMide == null)
                {
                    // Tipografico (sin el relleno de GDI+) y midiendo los espacios del final: asi el resaltado de lo
                    // tecleado cae justo encima de sus letras.
                    sfMide = new StringFormat(StringFormat.GenericTypographic);
                    sfMide.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
                    sfIzq = new StringFormat(sfMide);
                    sfIzq.Trimming = StringTrimming.EllipsisCharacter;
                    sfIzq.LineAlignment = StringAlignment.Center;
                    // Sin recorte: el texto ya llega recortado (Recorta). El recorte con puntos suspensivos de GDI+ no
                    // hace lo mismo en todas partes (en la simulacion el texto se salia de la tarjeta).
                    sfLinea = new StringFormat(sfMide);
                    sfLinea.LineAlignment = StringAlignment.Center;
                    sfCentro = new StringFormat(StringFormat.GenericTypographic);
                    sfCentro.FormatFlags |= StringFormatFlags.NoWrap;
                    sfCentro.Alignment = StringAlignment.Center; sfCentro.LineAlignment = StringAlignment.Center;
                    sfCentro.Trimming = StringTrimming.EllipsisCharacter;
                }
                if (fnS == s && fnCaja != null) return;
                Font[] viejas = { fnCaja, fnCuenta, fnAtajo, fnTecla, fnTeclaPal, fnTipo, fnTitulo, fnSub, fnMarca, fnAyuda, fnVacio, fnIniciales };
                for (int i = 0; i < viejas.Length; i++) if (viejas[i] != null) viejas[i].Dispose();
                fnCaja = Letra(34f * s, 14f, FontStyle.Bold);
                fnCuenta = Letra(16f * s, 10f, FontStyle.Bold);
                fnAtajo = Letra(14f * s, 10f, FontStyle.Bold);
                fnTecla = Letra(24f * s, 12f, FontStyle.Bold);
                fnTeclaPal = Letra(20f * s, 10f, FontStyle.Bold);
                fnTipo = Letra(11f * s, 9f, FontStyle.Bold);
                fnTitulo = Letra(17f * s, 12f, FontStyle.Bold);
                fnSub = Letra(12f * s, 10f, FontStyle.Regular);
                fnMarca = Letra(11f * s, 9f, FontStyle.Bold);
                fnAyuda = Letra(15f * s, 11f, FontStyle.Bold);
                fnVacio = Letra(16f * s, 11f, FontStyle.Italic);
                fnIniciales = Letra(30f * s, 11f, FontStyle.Bold);
                fnS = s;
            }
            private static Font Letra(float px, float minimo, FontStyle estilo)
            {
                return new Font(FontFamily.GenericSansSerif, Math.Max(minimo, px), estilo, GraphicsUnit.Pixel);
            }
            private float Mide(Graphics g, string t, Font f)
            {
                if (string.IsNullOrEmpty(t)) return 0f;
                return g.MeasureString(t, f, 99999, sfMide).Width;
            }
            private static int Alto(Graphics g, Font f) { return (int)Math.Ceiling(f.GetHeight(g)); }

            // Un resplandor alrededor de una forma redondeada: capas cada vez mas anchas y mas tenues.
            private void Resplandor(Graphics g, Rectangle r, int radio, Color c, int ancho, float fuerza)
            {
                if (ancho < 2 || fuerza <= 0f) return;
                int pasos = Math.Max(3, Math.Min(8, ancho / 3));
                int a = Math.Max(1, Math.Min(255, (int)(255f * fuerza * .085f * 8f / pasos)));
                for (int i = pasos; i >= 1; i--)
                {
                    int crece = (int)Math.Round(ancho * (float)i / pasos);
                    Rectangle rr = Rectangle.Inflate(r, crece, crece);
                    using (GraphicsPath gp = RoundedPath(rr, radio + crece))
                    {
                        sbS.Color = Color.FromArgb(a, c); g.FillPath(sbS, gp);
                    }
                }
            }

            // ---------------- LO QUIETO, HORNEADO ----------------
            // El resplandor, el fondo y el borde de lo elegido (tecla, atajo o tarjeta), los de la casilla con su lupa y los
            // de cada tarjeta con la sombra de su portada solo dependen del tamano, del color y de la escala. Se dibujan una
            // vez en una imagen aparte y despues se pegan: teclear o moverse ya no rehace el resplandor capa a capa.
            // Pegarlos da los mismos pixeles que dibujarlos: van lo primero, sobre un sitio vacio, y en las imagenes chicas
            // se hornean en el mismo sitio que ocupan en ellas (el margen de la imagen horneada es el de la pieza).
            internal const int QuietoTecla = 0, QuietoAtajo = 1, QuietoCaja = 2, QuietoCarta = 3, QuietoCartaElegida = 4;
            internal const int HorneadosMax = 16;   // se usan unos once: tres teclas, hasta cinco atajos, la casilla y dos tarjetas
            private sealed class Horneado { public int Tipo, W, H, C1, C2, Margen; public float S; public Bitmap B; }
            private readonly List<Horneado> horneados = new List<Horneado>();
            internal int HorneadosHechos;        // cuantas se hornearon (la bateria mira que moverse no hornee)

            // Cuanto sobresale lo quieto de su forma. En las imagenes chicas es su propio margen (ver PiezaVivaNueva).
            internal static int MargenQuieto(int tipo, int w, int h, float s)
            {
                if (tipo == QuietoTecla) return Px(20, s) + 3;
                if (tipo == QuietoAtajo || tipo == QuietoCaja) return Px(16, s) + 3;
                if (tipo == QuietoCartaElegida) return Px(20, s) + 4;
                // Una tarjeta sin elegir: su borde y, en las pantallas chicas, la sombra de la portada, que puede asomar.
                Rectangle cov = CajaPortadaCarta(new Rectangle(0, 0, w, h), s);
                return Math.Max(0, cov.Bottom + Px(6, s) + 3 - h) + 3;
            }
            private void PegaQuieto(Graphics g, int tipo, Rectangle r, Color c1, Color c2)
            {
                float s = rep.S;
                int w = r.Width, h = r.Height, a1 = c1.ToArgb(), a2 = c2.ToArgb();
                Horneado q = null;
                for (int i = 0; i < horneados.Count; i++)
                {
                    Horneado x = horneados[i];
                    if (x.Tipo == tipo && x.W == w && x.H == h && x.C1 == a1 && x.C2 == a2 && x.S == s) { q = x; break; }
                }
                if (q == null)
                {
                    // Los de otra escala ya no sirven (cambio el tamano de la pantalla): se sueltan.
                    for (int i = horneados.Count - 1; i >= 0; i--)
                        if (horneados[i].S != s) { horneados[i].B.Dispose(); horneados.RemoveAt(i); }
                    int m = MargenQuieto(tipo, w, h, s);
                    q = new Horneado();
                    q.Tipo = tipo; q.W = w; q.H = h; q.C1 = a1; q.C2 = a2; q.S = s; q.Margen = m;
                    q.B = new Bitmap(w + 2 * m, h + 2 * m, PixelFormat.Format32bppPArgb);
                    using (Graphics gb = Graphics.FromImage(q.B))
                    {
                        gb.Clear(Color.Transparent);
                        gb.SmoothingMode = g.SmoothingMode; gb.PixelOffsetMode = g.PixelOffsetMode;
                        gb.CompositingQuality = g.CompositingQuality;
                        DibujaQuieto(gb, tipo, new Rectangle(m, m, w, h), c1, c2, s);
                    }
                    horneados.Add(q);
                    HorneadosHechos++;
                    while (horneados.Count > HorneadosMax) { horneados[0].B.Dispose(); horneados.RemoveAt(0); }
                }
                else if (horneados[horneados.Count - 1] != q) { horneados.Remove(q); horneados.Add(q); }   // el ultimo usado, al final
                g.DrawImage(q.B, new Rectangle(r.X - q.Margen, r.Y - q.Margen, q.B.Width, q.B.Height), 0, 0, q.B.Width, q.B.Height, GraphicsUnit.Pixel);
            }
            internal void SueltaHorneados()
            {
                for (int i = 0; i < horneados.Count; i++) horneados[i].B.Dispose();
                horneados.Clear();
            }
            internal int HorneadosGuardados() { return horneados.Count; }
            // Lo quieto de cada cosa, capa a capa (lo que antes se dibujaba cada vez).
            private void DibujaQuieto(Graphics g, int tipo, Rectangle r, Color c1, Color c2, float s)
            {
                if (tipo == QuietoTecla)
                {
                    // c1 = el fondo de la tecla elegida (el color de la seccion, o el rojo en BORRAR); c2 = su borde
                    int radio = Math.Max(3, Px(10, s));
                    Resplandor(g, r, radio, c1, Px(20, s), .9f);
                    using (GraphicsPath gp = RoundedPath(r, radio))
                    {
                        sbS.Color = c1; g.FillPath(sbS, gp);
                        pnS.Color = c2; pnS.Width = 2f; g.DrawPath(pnS, gp);
                    }
                }
                else if (tipo == QuietoAtajo)
                {
                    int radio = Math.Max(2, r.Height / 2);
                    Resplandor(g, r, radio, c1, Px(16, s), .8f);
                    using (GraphicsPath gp = RoundedPath(r, radio))
                    {
                        sbS.Color = c1; g.FillPath(sbS, gp);
                        pnS.Color = Lighten(c1, .4); pnS.Width = 2f; g.DrawPath(pnS, gp);
                    }
                }
                else if (tipo == QuietoCaja)
                {
                    int radio = Px(18, s);
                    Resplandor(g, r, radio, c1, Px(14, s), .35f);
                    using (GraphicsPath gp = RoundedPath(r, radio))
                    {
                        sbS.Color = FondoCaja; g.FillPath(sbS, gp);
                        pnS.Color = Color.FromArgb(204, c1); pnS.Width = Math.Max(1.5f, 2f * s); g.DrawPath(pnS, gp);
                    }
                    int pad = Px(26, s), lupa = Px(30, s);
                    LupaNueva(g, r.X + pad, r.Y + r.Height / 2, lupa, Lighten(c1, .62));
                }
                else
                {
                    bool sel = tipo == QuietoCartaElegida;
                    int radio = Px(14, s);
                    if (sel) Resplandor(g, r, radio, c1, Px(18, s), .6f);
                    using (GraphicsPath gp = RoundedPath(r, radio))
                    {
                        sbS.Color = sel ? FondoCartaElegida : FondoCarta; g.FillPath(sbS, gp);
                        if (!sel) { pnS.Color = Color.FromArgb(115, c1); pnS.Width = 1f; g.DrawPath(pnS, gp); }
                    }
                    if (sel)
                    {
                        // El aro blanco va por FUERA de la tarjeta, como en el boceto.
                        using (GraphicsPath gp = RoundedPath(Rectangle.Inflate(r, 1, 1), radio + 1)) { pnS.Color = Color.White; pnS.Width = 2f; g.DrawPath(pnS, gp); }
                    }
                    // La sombra de debajo de la portada, suave.
                    Rectangle cov = CajaPortadaCarta(r, s);
                    int rp = Math.Max(2, Px(8, s));
                    for (int k = 3; k >= 1; k--)
                    {
                        Rectangle sh = cov; sh.Offset(0, Px(2f * k, s)); sh.Inflate(k, k);
                        using (GraphicsPath gp = RoundedPath(sh, rp + k)) { sbS.Color = Color.FromArgb(34, 0, 0, 0); g.FillPath(sbS, gp); }
                    }
                }
            }

            // ======================================================================================================
            //  LA IMAGEN GRANDE: los atajos, el teclado y las ayudas (lo quieto)
            // ======================================================================================================
            private void RenderNuevo(Graphics g)
            {
                EnsurePaintRes();
                RepartoBusca r = AseguraReparto();
                AseguraFuentes(r.S);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < atajoRects.Count; i++) DibujaAtajo(g, i, false, 0, 0);
                for (int f = 0; f < rows.Count; f++)
                    for (int c = 0; c < rows[f].Count; c++) DibujaTeclaNueva(g, f, c, false, 0, 0);
                DibujaAyudasNuevas(g, r);
                if (ToastOn) DrawToast(g, Width, Height);
            }
            // Lo que cambia la imagen grande. Ni la consulta, ni los resultados, ni donde esta la seleccion.
            internal long FirmaFija()
            {
                long h = SigBase;
                h = Mix(h, Width); h = Mix(h, Height);
                Color ac = Accent; h = Mix(h, ac.ToArgb());
                h = Mix(h, atajosVer);
                return h;
            }
            // Lo que cambia el contenido del buscador nuevo (para el fundido de colores: ver GetPaletteContent).
            internal long FirmaContenido()
            {
                long h = SigBase;
                h = Mix(h, EnChips); h = Mix(h, SelChip); h = Mix(h, resScroll); h = Mix(h, ModoAtajo);
                h = Mix(h, PortadasVer); h = Mix(h, atajosVer);
                return h;
            }

            private void DibujaTeclaNueva(Graphics g, int f, int c, bool sel, int dx, int dy)
            {
                SearchKey k = rows[f][c];
                Rectangle kr = Rectangle.Round(k.R); kr.Offset(dx, dy);
                if (kr.Width < 2 || kr.Height < 2) return;
                float s = rep.S;
                Color v = Vivo(Accent);
                bool borrar = k.Kind == KKind.Back;
                int radio = Math.Max(3, Px(10, s));
                Color fondo, borde, tinta;
                if (sel) { fondo = borrar ? SRed : v; borde = Lighten(fondo, .35); tinta = Color.White; }
                else if (borrar) { fondo = BorrarFondo; borde = BorrarBorde; tinta = BorrarTinta; }
                else { fondo = FondoTecla(v); borde = Color.FromArgb(115, v); tinta = Lighten(v, .8); }
                if (sel) PegaQuieto(g, QuietoTecla, kr, fondo, borde);   // su resplandor, su fondo y su borde, horneados
                else
                    using (GraphicsPath gp = RoundedPath(kr, radio))
                    {
                        sbS.Color = fondo; g.FillPath(sbS, gp);
                        pnS.Color = borde; pnS.Width = 1f; g.DrawPath(pnS, gp);
                    }
                sbS.Color = tinta;
                g.DrawString(k.Label, (k.Kind == KKind.Char) ? fnTecla : fnTeclaPal, sbS, kr, sfCentro);
            }
            private void DibujaAtajo(Graphics g, int i, bool sel, int dx, int dy)
            {
                if (i < 0 || i >= atajoRects.Count || i >= atajos.Count) return;
                Rectangle cr = atajoRects[i]; cr.Offset(dx, dy);
                Color v = Vivo(Accent);
                if (sel) PegaQuieto(g, QuietoAtajo, cr, v, v);
                else
                    using (GraphicsPath gp = RoundedPath(cr, Math.Max(2, cr.Height / 2)))
                    {
                        sbS.Color = FondoAtajo; g.FillPath(sbS, gp);
                        pnS.Color = BordeAtajo; pnS.Width = 1f; g.DrawPath(pnS, gp);
                    }
                sbS.Color = sel ? Color.White : TextoAtajo;
                g.DrawString(atajos[i], fnAtajo, sbS, cr, sfCentro);
            }
            private void DibujaAyudasNuevas(Graphics g, RepartoBusca r)
            {
                float s = r.S;
                int cy = r.Ayudas.Y + r.Ayudas.Height / 2;
                int d = Math.Max(14, Px(22, s)), dd = Math.Max(12, Px(18, s)), fl = Math.Max(12, Px(20, s));
                float gap = Px(8, s), grp = Px(22, s), x = r.Ayudas.X;
                StoreHintGlow(SMover, x, cy, d);
                x = CtrlPad(g, x, cy, d);
                x = CtrlTxt(g, fnAyuda, "Mover", x + gap, cy, AyudaTinta) + grp;
                StoreHintGlow(SPoner, x, cy, dd);
                x = CtrlDot(g, x, cy, dd, SGreen);
                x = CtrlTxt(g, fnAyuda, "Poner / Elegir", x + gap, cy, AyudaTinta) + grp;
                StoreHintGlow(SCerrar, x, cy, dd + 3 + fl + Px(12, s));
                x = CtrlDot(g, x, cy, dd, SRed) + 3;
                x = CtrlTxt(g, fnAyuda, "+", x, cy, AyudaTinta) + 3;
                x = FlechaArriba(g, x, cy, fl, SRed);
                CtrlTxt(g, fnAyuda, "Cerrar", x + gap, cy, AyudaTinta);
            }

            // ======================================================================================================
            //  LAS IMAGENES CHICAS: la tecla o el atajo elegido (0), la casilla (1), la tarjeta elegida (2) y las
            //  tarjetas (4). La 3 (el texto bajo el cubo) no existe aqui: no hay cubo.
            // ======================================================================================================
            private bool PiezaVivaNueva(int cual, out Rectangle donde)
            {
                donde = Rectangle.Empty;
                RepartoBusca r = AseguraReparto();
                float s = r.S;
                if (cual == PiezaTecla)
                {
                    if (EnChips)
                    {
                        if (SelChip < 0 || SelChip >= atajoRects.Count) return false;
                        donde = Rectangle.Inflate(atajoRects[SelChip], Px(16, s) + 3, Px(16, s) + 3);
                        return true;
                    }
                    if (FocusResults || SelRow < 0 || SelRow >= rows.Count || SelCol < 0 || SelCol >= rows[SelRow].Count) return false;
                    Rectangle kr = Rectangle.Round(rows[SelRow][SelCol].R);
                    if (kr.Width < 2 || kr.Height < 2) return false;
                    donde = Rectangle.Inflate(kr, Px(20, s) + 3, Px(20, s) + 3);
                    return true;
                }
                if (cual == PiezaEscrito)
                {
                    donde = Rectangle.Inflate(r.Caja, Px(16, s) + 3, Px(16, s) + 3);
                    return true;
                }
                if (cual == 2)
                {
                    if (!FocusResults || SelResult < 0 || SelResult >= CartasTotales()) return false;
                    Rectangle c = CartaRect(SelResult);
                    if (c.IsEmpty) return false;
                    donde = Rectangle.Inflate(c, Px(20, s) + 4, Px(20, s) + 4);
                    return true;
                }
                if (cual == PiezaCartas)
                {
                    donde = Rectangle.Inflate(Rectangle.Union(r.Resultados, r.Barra), MargenTarj, MargenTarj);
                    return true;
                }
                return false;
            }
            private void DibujaPiezaVivaNueva(Graphics g, int cual, int dx, int dy)
            {
                EnsurePaintRes();
                RepartoBusca r = AseguraReparto();
                AseguraFuentes(r.S);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (cual == PiezaTecla)
                {
                    if (EnChips) DibujaAtajo(g, SelChip, true, dx, dy);
                    else if (!FocusResults && SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)
                        DibujaTeclaNueva(g, SelRow, SelCol, true, dx, dy);
                }
                else if (cual == PiezaEscrito) DibujaCajaNueva(g, dx, dy);
                else if (cual == 2)
                {
                    if (!FocusResults || SelResult < 0 || SelResult >= CartasTotales()) return;
                    Rectangle c = CartaRect(SelResult);
                    if (c.IsEmpty) return;
                    c.Offset(dx, dy);
                    DibujaCartaNueva(g, SelResult, c, true, Normalize(Query ?? ""));
                }
                else if (cual == PiezaCartas) HorneaCartas(g, dx, dy);
            }
            // Lo que cambia cada imagen chica (se mezcla con su sitio y su tamano en EnsureUiPieza).
            internal long FirmaPieza(int cual)
            {
                AseguraReparto();
                long h = SigBase;
                Color ac = Accent; h = Mix(h, ac.ToArgb()); h = Mix(h, (int)(rep.S * 1000f));
                if (cual == PiezaTecla)
                {
                    h = Mix(h, EnChips); h = Mix(h, FocusResults); h = Mix(h, SelRow); h = Mix(h, SelCol); h = Mix(h, SelChip);
                    if (EnChips && SelChip >= 0 && SelChip < atajos.Count) h = Mix(h, atajos[SelChip]);
                }
                else if (cual == PiezaEscrito) { h = Mix(h, Escrito); h = Mix(h, ModoAtajo); h = Mix(h, TextoCuenta()); }
                else if (cual == 2) { h = Mix(h, FocusResults); h = Mix(h, SelResult); h = Mix(h, VersionCartas()); }
                else if (cual == PiezaCartas) h = Mix(h, VersionCartas());
                return h;
            }

            // ---------------- LA CASILLA DE LO ESCRITO ----------------
            internal string TextoCuenta()
            {
                if (Results != null && Results.Count > 0 && Results[0] != null && Results[0].Kind == 3) return "";   // la de servicio no es un resultado
                int n;
                if (ModoAtajo != null) n = (Results != null) ? Results.Count : 0;
                else if (string.IsNullOrEmpty(Query) || TotalDe != Query) return "";
                else n = TotalResultados;
                if (n <= 0) return "Sin resultados";
                return (n == 1) ? "1 resultado" : n.ToString("#,0") + " resultados";
            }
            private void DibujaCajaNueva(Graphics g, int dx, int dy)
            {
                float s = rep.S;
                Rectangle qb = rep.Caja; qb.Offset(dx, dy);
                Color v = Vivo(Accent);
                PegaQuieto(g, QuietoCaja, qb, v, v);   // el resplandor, el fondo, el borde y la lupa, horneados
                int pad = Px(26, s), cy = qb.Y + qb.Height / 2, lupa = Px(30, s);
                float x = qb.X + pad + lupa + Px(14, s);
                string cuenta = TextoCuenta();
                float wc = Mide(g, cuenta, fnCuenta);
                if (wc > 0f)
                {
                    sbS.Color = TextoCuentaColor;
                    float xc = qb.Right - pad - wc;
                    g.DrawString(cuenta, fnCuenta, sbS, new RectangleF(xc, qb.Y, wc + 2, qb.Height), sfIzq);
                }
                float lim = qb.Right - pad - (wc > 0f ? wc + Px(18, s) : 0f);
                float disp = Math.Max(10f, lim - x);
                RectangleF linea = new RectangleF(x, qb.Y, disp, qb.Height);
                Color caret = Lighten(v, .35);
                if (Escrito.Length == 0 && ModoAtajo != null)
                {
                    Renglon(g, ModoAtajo, fnCaja, TextoAtajo, x, cy, disp);
                    return;
                }
                float wcar = Mide(g, "|", fnCaja);
                if (Escrito.Length == 0)
                {
                    sbS.Color = caret; g.DrawString("|", fnCaja, sbS, linea, sfIzq);
                    Renglon(g, "Escribe un artista, un disco o una canci\u00f3n", fnCuenta, Invita, x + wcar + Px(6, s), cy, Math.Max(10f, disp - wcar - Px(6, s)));
                    return;
                }
                // Si lo escrito no cabe se ve el FINAL, que es lo que se esta tecleando.
                string t = Escrito;
                if (Mide(g, t, fnCaja) + wcar > disp)
                {
                    int ini = 1;
                    while (ini < t.Length && Mide(g, "\u2026" + t.Substring(ini), fnCaja) + wcar > disp) ini++;
                    t = "\u2026" + t.Substring(Math.Min(ini, t.Length));
                }
                sbS.Color = Color.White;
                g.DrawString(t, fnCaja, sbS, linea, sfIzq);
                float wt = Mide(g, t, fnCaja);
                sbS.Color = caret;
                g.DrawString("|", fnCaja, sbS, new RectangleF(x + wt + 1, qb.Y, wcar + 4, qb.Height), sfIzq);
            }
            private static void LupaNueva(Graphics g, float x, int cy, int d, Color c)
            {
                float aro = d * .56f, ancho = Math.Max(2f, d * .1f);
                float ax = x + d * .08f, ay = cy - d * .42f;
                using (Pen pn = new Pen(c, ancho))
                {
                    pn.StartCap = LineCap.Round; pn.EndCap = LineCap.Round;
                    g.DrawEllipse(pn, ax, ay, aro, aro);
                    float ex = ax + aro * .85f, ey = ay + aro * .85f;
                    g.DrawLine(pn, ex, ey, x + d * .92f, cy + d * .42f);
                }
            }

            // ---------------- LAS TARJETAS ----------------
            // La version de las tarjetas: sube cuando cambia algo de lo que se ve en ellas. Al llegar portadas espera un
            // poco para rehacerlas de una vez; y cuando la vista lleva un rato quieta, pide las portadas que faltan.
            private int cartasVer;
            private object vcLista; private int vcN = -1, vcScroll = -1, vcCola = -1, vcDatos = -1, vcPortadas = -1, vcAcento, vcW = -1, vcH = -1, vcFilas = -1;
            private string vcQuery, vcModo; private double vcCuando = double.MinValue, vcCuandoVista = double.MinValue;
            private readonly List<Album> portadasFaltan = new List<Album>();
            private sealed class FirmaCarta
            {
                internal SearchItem Item; internal Image Cover;
                internal string Tipo, Titulo, Sub, Genero, Path;
                internal int ColorTipo, Cola; internal bool Suena;
                internal bool Igual(FirmaCarta other)
                {
                    return other != null && ReferenceEquals(Item, other.Item) && ReferenceEquals(Cover, other.Cover)
                        && Tipo == other.Tipo && Titulo == other.Titulo && Sub == other.Sub && Genero == other.Genero
                        && Path == other.Path && ColorTipo == other.ColorTipo && Cola == other.Cola && Suena == other.Suena;
                }
            }
            private FirmaCarta[] cartasPintadas;
            private object cartasPintadasLista;
            private int cartasPintadasScroll, cartasPintadasW, cartasPintadasH, cartasPintadasAccent;
            private string cartasPintadasQuery;
            private Rectangle cartasPintadasResultados, cartasPintadasBarra;
            private int cartasPintadasFilas;
            private bool cartasRecortadas;

            // Una portada o una marca de cola no cambian toda la pagina. Se compara lo que cada
            // tarjeta realmente muestra, y se repinta en el mismo lienzo solo donde cambio.
            // null = reconstruccion completa; arreglo vacio = nada visible cambio.
            internal Rectangle[] PintaCartasConCambios(Graphics g, int dx, int dy, bool entera)
            {
                RepartoBusca r = AseguraReparto(); AseguraFuentes(r.S); EnsurePaintRes();
                int first = resScroll * 2, n = Math.Max(0, Math.Min(CartasTotales() - first, r.Filas * 2));
                FirmaCarta[] next = new FirmaCarta[n];
                for (int j = 0; j < n; j++)
                {
                    SearchItem it = Results[first + j]; FirmaCarta f = new FirmaCarta(); next[j] = f; f.Item = it;
                    if (it == null) continue;
                    Color c; DatosCarta(it, out f.Tipo, out c, out f.Titulo, out f.Sub); f.ColorTipo = c.ToArgb();
                    f.Cola = GetQueuePos != null ? GetQueuePos(it) : 0; f.Suena = GetSonando != null && GetSonando(it);
                    Album album = AlbumDePortada(it); f.Path = album != null ? album.CoverPath : null;
                    Rectangle cover = CajaPortadaCarta(CartaRect(first + j), r.S);
                    f.Cover = Miniatura(album, cover.Width);
                    f.Genero = f.Cover == null && GetGenero != null ? GetGenero(it) : null;
                }
                bool partial = !entera && n > 0 && cartasPintadas != null && cartasPintadas.Length == n
                    && ReferenceEquals(cartasPintadasLista, Results) && cartasPintadasScroll == resScroll
                    && cartasPintadasW == Width && cartasPintadasH == Height && cartasPintadasQuery == Query
                    && cartasPintadasResultados == r.Resultados && cartasPintadasBarra == r.Barra && cartasPintadasFilas == r.Filas
                    && cartasPintadasAccent == Accent.ToArgb();
                Rectangle[] regions = null;
                if (partial)
                {
                    List<Rectangle> dirty = new List<Rectangle>();
                    for (int j = 0; j < n; j++)
                        if (!next[j].Igual(cartasPintadas[j]))
                        {
                            Rectangle cr = CartaRect(first + j);
                            // Incluye sombra, borde y filtrado. Se redibujan tambien vecinos que
                            // intersectan el recuadro: borrar una sombra nunca borra otra tarjeta.
                            int margin = MargenQuieto(QuietoCarta, cr.Width, cr.Height, r.S) + 2;
                            cr.Inflate(margin, margin); cr.Offset(dx, dy);
                            cr.Intersect(Rectangle.Ceiling(g.VisibleClipBounds));
                            if (!cr.IsEmpty) dirty.Add(cr);
                        }
                    // Unir recuadros solapados evita volver a mezclar bordes antialias.
                    for (int i = 0; i < dirty.Count; i++)
                        for (int j = i + 1; j < dirty.Count; j++)
                            if (dirty[i].IntersectsWith(dirty[j]))
                            { dirty[i] = Rectangle.Union(dirty[i], dirty[j]); dirty.RemoveAt(j); i = -1; break; }
                    regions = dirty.ToArray();
                    cartasRecortadas = true;
                    try
                    {
                        foreach (Rectangle area in regions)
                        {
                            GraphicsState state = g.Save();
                            try
                            {
                                g.SetClip(area, CombineMode.Intersect);
                                g.CompositingMode = CompositingMode.SourceCopy;
                                using (SolidBrush clear = new SolidBrush(Color.Transparent)) g.FillRectangle(clear, area);
                                g.CompositingMode = CompositingMode.SourceOver;
                                HorneaCartas(g, dx, dy);
                            }
                            finally { g.Restore(state); }
                        }
                    }
                    finally { cartasRecortadas = false; }
                }
                else { g.Clear(Color.Transparent); HorneaCartas(g, dx, dy); }
                // Las portadas pendientes fuera del ultimo recuadro tambien deben seguir encargadas.
                portadasFaltan.Clear();
                for (int j = 0; j < next.Length; j++)
                    if (next[j].Cover == null && next[j].Path != null)
                    {
                        Album album = AlbumDePortada(next[j].Item);
                        if (album != null && !portadasFaltan.Contains(album)) portadasFaltan.Add(album);
                    }
                cartasPintadas = next; cartasPintadasLista = Results; cartasPintadasScroll = resScroll;
                cartasPintadasW = Width; cartasPintadasH = Height; cartasPintadasQuery = Query;
                cartasPintadasAccent = Accent.ToArgb();
                cartasPintadasResultados = r.Resultados; cartasPintadasBarra = r.Barra; cartasPintadasFilas = r.Filas;
                return regions;
            }
            internal int VersionCartas()
            {
                RepartoBusca r = rep;
                int n = CartasTotales(), ac = Accent.ToArgb();
                double ahora = GpuPanel.AhoraMs();
                bool vista = ReferenceEquals(Results, vcLista) && n == vcN && resScroll == vcScroll && Width == vcW && Height == vcH
                             && r.Filas == vcFilas && vcQuery == Query && vcModo == ModoAtajo && ac == vcAcento;
                bool igual = vista && vcCola == ColaVer && vcDatos == DatosVer && vcPortadas == PortadasVer;
                if (!igual)
                {
                    // Solo llegaron portadas y se acaban de rehacer: se espera un poco a que lleguen las demas.
                    bool soloPortadas = vista && vcCola == ColaVer && vcDatos == DatosVer;
                    if (!soloPortadas || ahora - vcCuando >= JuntaPortadasMs)
                    {
                        if (!vista) vcCuandoVista = ahora;
                        vcLista = Results; vcN = n; vcScroll = resScroll; vcW = Width; vcH = Height; vcFilas = r.Filas;
                        vcQuery = Query; vcModo = ModoAtajo; vcAcento = ac; vcCola = ColaVer; vcDatos = DatosVer; vcPortadas = PortadasVer;
                        vcCuando = ahora;
                        cartasVer++;
                    }
                }
                PidePortadasSiQuieto(ahora);
                return cartasVer;
            }
            private void PidePortadasSiQuieto(double ahora)
            {
                if (portadasFaltan.Count == 0 || ahora - vcCuandoVista < EsperaPedirMs || GetCover == null) return;
                for (int i = 0; i < portadasFaltan.Count; i++)
                {
                    try { GetCover(portadasFaltan[i]); } catch { }   // la encarga (o ya estaba): al llegar, PortadaLlegoBuscador
                }
                portadasFaltan.Clear();
            }
            internal bool EsperaPortada(string clave)
            {
                if (string.IsNullOrEmpty(clave)) return false;
                for (int i = 0; i < hornoN && i < hornoPortada.Length; i++)
                    if (hornoPortada[i] != null && string.Equals(hornoPortada[i], clave, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            // Lo que quedo en la imagen de las tarjetas: que habia en cada casilla, su trozo y cuando empieza a entrar.
            private SearchItem[] hornoItem = new SearchItem[0];
            private Rectangle[] hornoRect = new Rectangle[0];
            private double[] hornoDesde = new double[0];
            private string[] hornoPortada = new string[0];   // la portada que le falta (null si la tiene o no tiene)
            private int hornoN, hornoIndice0;
            private object hornoLista; private int hornoScroll = -1;
            private Rectangle hornoBarra = Rectangle.Empty;

            private void HorneaCartas(Graphics g, int dx, int dy)
            {
                RepartoBusca r = rep;
                float s = r.S;
                int n = CartasTotales();
                double ahora = GpuPanel.AhoraMs();
                bool listaNueva = !ReferenceEquals(Results, hornoLista);
                bool bajo = !listaNueva && resScroll != hornoScroll;
                int primera = resScroll * 2, visibles = Math.Max(0, Math.Min(n - primera, r.Filas * 2));
                SearchItem[] items = new SearchItem[visibles];
                Rectangle[] rects = new Rectangle[visibles];
                double[] desde = new double[visibles];
                string[] faltan = new string[visibles];
                string qn = Normalize(Query ?? "");
                portadasFaltan.Clear();
                int escalon = 0;
                for (int j = 0; j < visibles; j++)
                {
                    int i = primera + j;
                    Rectangle c = CartaRect(i);
                    items[j] = Results[i];
                    if (c.IsEmpty) continue;
                    Rectangle enImagen = c; enImagen.Offset(dx, dy);
                    int margin = MargenQuieto(QuietoCarta, enImagen.Width, enImagen.Height, s) + 2;
                    Rectangle ink = Rectangle.Inflate(enImagen, margin, margin);
                    if (!cartasRecortadas || ink.IntersectsWith(Rectangle.Ceiling(g.VisibleClipBounds)))
                        faltan[j] = DibujaCartaNueva(g, i, enImagen, false, qn);
                    else faltan[j] = j < hornoPortada.Length ? hornoPortada[j] : null;
                    rects[j] = Rectangle.Inflate(enImagen, MargenTarj, MargenTarj);
                    // Como entra. Llego otra lista: las casillas que cambiaron, una tras otra, y las demas como iban.
                    // Bajando de fila: ya puestas. Si no (la cola, una portada, el color): como iban.
                    if (listaNueva)
                    {
                        bool cambio = j >= hornoN || primera != hornoIndice0 || !ReferenceEquals(hornoItem[j], items[j]);
                        desde[j] = cambio ? ahora + Math.Min(escalon++, EscalonesMax) * EscalonMs : hornoDesde[j];
                    }
                    else if (bajo || j >= hornoN) desde[j] = double.MinValue;
                    else desde[j] = hornoDesde[j];
                }
                hornoItem = items; hornoRect = rects; hornoDesde = desde; hornoPortada = faltan; hornoN = visibles; hornoIndice0 = primera;
                hornoLista = Results; hornoScroll = resScroll;
                // La barrita de la derecha, si hay mas filas de las que caben.
                hornoBarra = Rectangle.Empty;
                int filas = FilasTotales();
                if (filas > r.Filas && r.Barra.Height > 8)
                {
                    Rectangle pista = r.Barra; pista.Offset(dx, dy);
                    int alto = Math.Max(Px(24, s), pista.Height * r.Filas / filas);
                    int y = pista.Y + (pista.Height - alto) * resScroll / Math.Max(1, filas - r.Filas);
                    using (GraphicsPath gp = RoundedPath(pista, Math.Max(1, pista.Width / 2))) { sbS.Color = Color.FromArgb(40, 207, 224, 255); g.FillPath(sbS, gp); }
                    using (GraphicsPath gp = RoundedPath(new Rectangle(pista.X, y, pista.Width, alto), Math.Max(1, pista.Width / 2))) { sbS.Color = Color.FromArgb(170, Vivo(Accent)); g.FillPath(sbS, gp); }
                    hornoBarra = Rectangle.Inflate(pista, 1, 1);
                }
                if (visibles == 0) DibujaSinCartas(g, r, dx, dy);
            }
            // Sin tarjetas: que hacer, dicho en el sitio de las tarjetas. Esta imagen se dibuja entera en ese caso.
            private void DibujaSinCartas(Graphics g, RepartoBusca r, int dx, int dy)
            {
                string t;
                if (ModoAtajo != null) t = "Todav\u00eda no hay nada aqu\u00ed";
                else if (string.IsNullOrEmpty(Query)) t = (atajoRects.Count > 0) ? "Escribe para buscar, o elige un atajo de arriba" : "Escribe para buscar";
                else t = "Sin resultados para \u00ab" + Query + "\u00bb";
                Renglon(g, t, fnVacio, TextoSub, r.Resultados.X + dx, r.Resultados.Y + dy + Px(8, r.S) + Alto(g, fnVacio) / 2f, r.Resultados.Width);
            }

            // Cada tarjeta de la imagen, con su entrada, para la tarjeta grafica. Devuelve cuantas; 0 = la imagen se
            // dibuja entera (no hay tarjetas: el aviso de que hacer).
            internal int LlenaCartas(CartaGpu[] buf, double ahora, out float elegida)
            {
                elegida = 1f;
                if (buf == null || hornoN == 0) return 0;
                float sube = Px(12, rep.S);
                int m = 0;
                for (int j = 0; j < hornoN && m < buf.Length; j++)
                {
                    Rectangle rr = hornoRect[j];
                    if (rr.Width < 1 || rr.Height < 1) continue;
                    float e = Entrada(ahora - hornoDesde[j]);
                    buf[m].X0 = rr.Left; buf[m].Y0 = rr.Top; buf[m].X1 = rr.Right; buf[m].Y1 = rr.Bottom;
                    buf[m].Alfa = e; buf[m].Dy = (1f - e) * sube;
                    m++;
                    if (FocusResults && hornoIndice0 + j == SelResult) elegida = e;
                }
                if (!hornoBarra.IsEmpty && m < buf.Length)
                {
                    buf[m].X0 = hornoBarra.Left; buf[m].Y0 = hornoBarra.Top; buf[m].X1 = hornoBarra.Right; buf[m].Y1 = hornoBarra.Bottom;
                    buf[m].Alfa = 1f; buf[m].Dy = 0f;
                    m++;
                }
                return m;
            }
            // 0 antes de empezar, 1 al acabar; frena al final (como sale en el boceto).
            internal static float Entrada(double t)
            {
                if (t <= 0) return 0f;
                if (t >= EntradaMs) return 1f;
                double u = 1.0 - t / EntradaMs;
                return (float)(1.0 - u * u * u);
            }

            // Una tarjeta. Devuelve la portada que le falta (la clave, para pedirla) o null.
            private string DibujaCartaNueva(Graphics g, int i, Rectangle cr, bool sel, string qn)
            {
                if (Results == null || i < 0 || i >= Results.Count || Results[i] == null) return null;
                SearchItem it = Results[i];
                float s = rep.S;
                Color v = Vivo(Accent);
                // El fondo y el borde (en la elegida, el resplandor y el aro) y la sombra de la portada, horneados.
                PegaQuieto(g, sel ? QuietoCartaElegida : QuietoCarta, cr, v, v);
                Rectangle cov = CajaPortadaCarta(cr, s);
                string falta = DibujaPortadaCarta(g, it, cov, s);
                int tx = cov.Right + Px(12, s), tw = cr.Right - Px(10, s) - tx;
                if (tw < 8) return falta;

                string tipo, titulo, sub; Color ctipo;
                DatosCarta(it, out tipo, out ctipo, out titulo, out sub);
                int posCola = (GetQueuePos != null) ? GetQueuePos(it) : 0;
                bool suena = (GetSonando != null) && GetSonando(it);
                int gap = Math.Max(2, Px(4, s));
                int hTipo = Alto(g, fnTipo), hTit = Alto(g, fnTitulo), hSub = Alto(g, fnSub), hMarca = Alto(g, fnMarca) + 2;
                bool hayMarca = posCola > 0 || suena;
                int total = hTipo + gap + hTit + (sub.Length > 0 ? gap + hSub : 0) + (hayMarca ? gap + hMarca : 0);
                int y = cr.Y + (cr.Height - total) / 2;
                Renglon(g, tipo, fnTipo, ctipo, tx, y + hTipo / 2f, tw);
                y += hTipo + gap;
                // En la elegida, el nombre y el renglon que no caben se deslizan (Titulos largos deslizantes, TextoMovilGpu.cs):
                // van en su cinta y aqui no se dibujan. Sin cintas (apagado, o cabe), quietos y cortados, como siempre.
                if (!(sel && Cinta(g, titulo, fnTitulo, Color.White, tx, y + hTit / 2f, tw)))
                    LineaResaltada(g, titulo, fnTitulo, Color.White, tx, y + hTit / 2f, tw, qn);
                y += hTit + gap;
                if (sub.Length > 0)
                {
                    if (!(sel && Cinta(g, sub, fnSub, TextoSub, tx, y + hSub / 2f, tw)))
                        LineaResaltada(g, sub, fnSub, TextoSub, tx, y + hSub / 2f, tw, qn);
                    y += hSub + gap;
                }
                if (hayMarca)
                {
                    // Las MISMAS palabras y colores que dentro del disco y que las tarjetas de siempre.
                    float mx = tx;
                    if (suena) mx = Marca(g, "\u25ba SONANDO", SonAmbar, mx, y, hMarca, tx + tw);
                    if (posCola > 0) Marca(g, "EN COLA " + posCola, ColaVerde, mx, y, hMarca, tx + tw);
                }
                return falta;
            }
            private void DatosCarta(SearchItem it, out string tipo, out Color ctipo, out string titulo, out string sub)
            {
                sub = "";
                if (it.Kind == 0)
                {
                    tipo = "ARTISTA"; ctipo = TipoArtista; titulo = it.Display ?? "";
                    int discos = 0, canciones = 0;
                    if (it.Albums != null)
                        for (int k = 0; k < it.Albums.Count; k++)
                            if (it.Albums[k] != null) { discos++; canciones += it.Albums[k].Tracks.Count; }
                    if (discos == 0 && it.AlbumRef != null) { discos = 1; canciones = it.AlbumRef.Tracks.Count; }
                    if (discos > 0) sub = discos + (discos == 1 ? " disco" : " discos") + (canciones > 0 ? " \u00b7 " + canciones + (canciones == 1 ? " canci\u00f3n" : " canciones") : "");
                }
                else if (it.Kind == 1)
                {
                    tipo = "\u00c1LBUM"; ctipo = SonAmbar;
                    Album a = it.AlbumRef;
                    titulo = (a != null && !string.IsNullOrEmpty(a.Name)) ? a.Name : (it.Display ?? "");
                    if (a != null)
                    {
                        int nc = a.Tracks.Count, nv = (a.VideoFiles != null) ? a.VideoFiles.Count : 0;
                        string cuenta = nc > 0 ? nc + (nc == 1 ? " canci\u00f3n" : " canciones") : (nv > 0 ? nv + (nv == 1 ? " video" : " videos") : "");
                        sub = string.IsNullOrEmpty(a.Artist) ? cuenta : (cuenta.Length > 0 ? a.Artist + " \u00b7 " + cuenta : a.Artist);
                    }
                }
                else if (it.Kind == 2)
                {
                    tipo = "CANCI\u00d3N"; ctipo = ColaVerde;
                    Track t = it.TrackRef;
                    titulo = (t != null && !string.IsNullOrEmpty(t.Title)) ? TituloSinPista(t.Title) : (it.Display ?? "");   // sin "03 - " (AcabadoGpu.cs)
                    string artista = (t != null && !string.IsNullOrEmpty(t.Artist)) ? t.Artist : (it.AlbumRef != null ? (it.AlbumRef.Artist ?? "") : "");
                    string dur = (GetDuracion != null) ? (GetDuracion(it) ?? "") : "";
                    // La duracion si ya se sabe; si no, el disco: lo que ayude a reconocerla.
                    string extra = dur.Length > 0 ? dur : (it.AlbumRef != null ? (it.AlbumRef.Name ?? "") : "");
                    sub = artista.Length > 0 ? (extra.Length > 0 ? artista + " \u00b7 " + extra : artista) : extra;
                }
                else { tipo = "SERVICIO"; ctipo = TipoServicio; titulo = it.Display ?? ""; }
            }
            // Una pastilla de marca; devuelve donde empieza la siguiente. Si no cabe, no se dibuja.
            private float Marca(Graphics g, string texto, Color c, float x, int y, int alto, float lim)
            {
                float w = Mide(g, texto, fnMarca) + 2 * Px(9, rep.S);
                if (x + w > lim) return x;
                Rectangle r = new Rectangle((int)x, y, (int)Math.Ceiling(w), alto);
                using (GraphicsPath gp = RoundedPath(r, Math.Max(2, alto / 2)))
                {
                    sbS.Color = Color.FromArgb(71, 0, 0, 0); g.FillPath(sbS, gp);
                    pnS.Color = c; pnS.Width = 1f; g.DrawPath(pnS, gp);
                }
                sbS.Color = c;
                g.DrawString(texto, fnMarca, sbS, new RectangleF(r.X, r.Y - alto / 2f, r.Width, 2f * alto), sfCentro);
                return x + w + Px(6, rep.S);
            }
            // Un renglon que no cabe, como cinta que se desliza. Solo si alguien las recoge (la tarjeta elegida, al hornear su
            // imagen chica con el interruptor encendido: EnsureUiPieza); si no, false y se dibuja quieto.
            private bool Cinta(Graphics g, string t, Font f, Color c, float x, float cy, float ancho)
            {
                if (string.IsNullOrEmpty(t)) return false;
                float h = f.GetHeight(g);
                // La cinta decide sola si cabe: lo que no mide mas que su recuadro menos un pixel se queda quieto. Con el recuadro
                // un pixel mas ancho, cabe exactamente lo que cabe en Recorta (lo que no mide mas de 'ancho'), y se ve quieto,
                // con lo tecleado resaltado.
                return TextoConCinta(g, t, f, c, new RectangleF(x, cy - h, ancho + 1f, 2f * h), sfLinea);
            }
            // Un texto en un renglon, centrado en 'cy'. El recuadro es el doble de alto que la letra: con uno justo, GDI+
            // puede dar el renglon por "que no cabe" y no dibujarlo (se vio en la simulacion a 1024x768).
            private void Renglon(Graphics g, string t, Font f, Color c, float x, float cy, float ancho)
            {
                if (string.IsNullOrEmpty(t) || ancho < 2f) return;
                t = Recorta(g, t, f, ancho);
                if (t.Length == 0) return;
                float h = f.GetHeight(g);
                sbS.Color = c;
                g.DrawString(t, f, sbS, new RectangleF(x, cy - h, ancho + 2f, 2f * h), sfLinea);
            }
            // El texto tal cual si cabe; si no, lo que quepa con puntos suspensivos al final.
            private string Recorta(Graphics g, string t, Font f, float ancho)
            {
                if (Mide(g, t, f) <= ancho) return t;
                int lo = 0, hi = t.Length - 1;   // cuantas letras se quedan
                while (lo < hi)
                {
                    int m = (lo + hi + 1) / 2;
                    if (Mide(g, t.Substring(0, m).TrimEnd() + "\u2026", f) <= ancho) lo = m; else hi = m - 1;
                }
                if (lo == 0) return (Mide(g, "\u2026", f) <= ancho) ? "\u2026" : "";
                return t.Substring(0, lo).TrimEnd() + "\u2026";
            }
            // Un renglon con lo tecleado resaltado, si se ve entero; si no, sin resaltar y con puntos suspensivos.
            private void LineaResaltada(Graphics g, string t, Font f, Color c, float x, float cy, float ancho, string qn)
            {
                if (string.IsNullOrEmpty(t)) return;
                int ini, largo;
                if (qn != null && qn.Length > 0 && BuscaTramo(t, qn, out ini, out largo))
                {
                    string ante = t.Substring(0, ini), coincide = t.Substring(ini, largo), resto = t.Substring(ini + largo);
                    float wa = Mide(g, ante, f), wc = Mide(g, coincide, f);
                    if (wa + wc <= ancho)
                    {
                        float hTxt = f.GetHeight(g);
                        using (GraphicsPath gp = RoundedPath(new Rectangle((int)(x + wa) - 1, (int)(cy - hTxt / 2f), (int)Math.Ceiling(wc) + 2, (int)Math.Ceiling(hTxt)), 3))
                        { sbS.Color = Resalte; g.FillPath(sbS, gp); }
                        Renglon(g, ante, f, c, x, cy, wa + 2);
                        Renglon(g, coincide, f, TextoResalte, x + wa, cy, wc + 2);
                        if (resto.Length > 0) Renglon(g, resto, f, c, x + wa + wc, cy, Math.Max(4f, ancho - wa - wc));
                        return;
                    }
                }
                Renglon(g, t, f, c, x, cy, ancho);
            }

            // ---------------- LA PORTADA DE UNA TARJETA ----------------
            // La de su disco (la del primero de un artista que la tenga), ya escalada; si aun no esta en memoria, las
            // iniciales sobre un degradado del color de su genero. Devuelve la clave de la que falta, o null.
            private readonly Dictionary<string, Bitmap> miniaturas = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> miniaturasOrden = new List<string>();
            private int miniaturasLado = -1;
            internal const int MiniaturasMax = 64;
            private string DibujaPortadaCarta(Graphics g, SearchItem it, Rectangle cov, float s)
            {
                int radio = Math.Max(2, Px(8, s));   // (la sombra de debajo va horneada con la tarjeta: DibujaQuieto)
                Album a = AlbumDePortada(it);
                string falta = null;
                Bitmap mini = null;
                if (a != null && a.CoverPath != null)
                {
                    mini = Miniatura(a, cov.Width);
                    if (mini == null) { falta = a.CoverPath; if (!portadasFaltan.Contains(a)) portadasFaltan.Add(a); }
                }
                using (GraphicsPath gp = RoundedPath(cov, radio))
                {
                    if (mini != null)
                    {
                        using (TextureBrush tb = new TextureBrush(mini, WrapMode.Clamp))
                        {
                            tb.TranslateTransform(cov.X, cov.Y);
                            g.FillPath(tb, gp);
                        }
                        return null;
                    }
                    Color c1 = Color.FromArgb(91, 53, 201), c2 = Color.FromArgb(79, 179, 154);
                    string gen = (GetGenero != null) ? GetGenero(it) : "";
                    if (!string.IsNullOrEmpty(gen)) { Color cg = ColorDeEtiqueta(gen); c1 = Shade(cg, .35); c2 = Lighten(cg, .12); }
                    using (LinearGradientBrush lb = new LinearGradientBrush(cov, c1, c2, 45f)) g.FillPath(lb, gp);
                    GraphicsState gs = g.Save();
                    g.SetClip(gp, CombineMode.Intersect); // respetar el recuadro parcial: no volver a pintar vecinos fuera de el
                    int d = (int)(cov.Width * .78f);
                    sbS.Color = Color.FromArgb(36, 255, 255, 255);
                    g.FillEllipse(sbS, cov.Right - d + (int)(cov.Width * .28f), cov.Y - (int)(cov.Width * .28f), d, d);
                    g.Restore(gs);
                    sbS.Color = Color.FromArgb(235, 255, 255, 255);
                    g.DrawString(Iniciales(it), fnIniciales, sbS, cov, sfCentro);
                }
                return falta;
            }
            private static Album AlbumDePortada(SearchItem it)
            {
                if (it == null) return null;
                if (it.Kind == 0 && it.Albums != null)
                    for (int k = 0; k < it.Albums.Count; k++) if (it.Albums[k] != null && it.Albums[k].CoverPath != null) return it.Albums[k];
                return it.AlbumRef;
            }
            internal static string Iniciales(SearchItem it)
            {
                if (it == null) return "#";
                if (it.Kind == 3) return "+4";
                string t = (it.Kind == 0) ? it.Display
                         : (it.Kind == 2 && it.TrackRef != null && !string.IsNullOrEmpty(it.TrackRef.Artist)) ? it.TrackRef.Artist
                         : (it.AlbumRef != null && !string.IsNullOrEmpty(it.AlbumRef.Artist)) ? it.AlbumRef.Artist : it.Display;
                return Iniciales(t);
            }
            internal static string Iniciales(string t)
            {
                if (string.IsNullOrEmpty(t)) return "#";
                string[] p = t.Split(new char[] { ' ', '-', '_', '.', ',', '&', '/' }, StringSplitOptions.RemoveEmptyEntries);
                string r = "";
                for (int i = 0; i < p.Length && r.Length < 2; i++)
                {
                    char c = p[i][0];
                    if (char.IsLetterOrDigit(c)) r += char.ToUpperInvariant(c);
                }
                return r.Length > 0 ? r : "#";
            }
            // La portada escalada al lado de la tarjeta, recortada al cuadrado del centro. Solo con lo que ya esta en
            // memoria: pedirla al disco es cosa de PidePortadasSiQuieto.
            private Bitmap Miniatura(Album a, int lado)
            {
                if (a == null || a.CoverPath == null || lado < 4) return null;
                if (lado != miniaturasLado)
                {
                    foreach (Bitmap b in miniaturas.Values) b.Dispose();
                    miniaturas.Clear(); miniaturasOrden.Clear(); miniaturasLado = lado;
                }
                Bitmap m;
                if (miniaturas.TryGetValue(a.CoverPath, out m)) return m;
                Image img = (GetCoverYa != null) ? GetCoverYa(a) : null;
                if (img == null) return null;
                try { m = Escala(img, lado); }
                catch { return null; }   // la portada se solto mientras tanto: se vuelve a intentar al llegar
                miniaturas[a.CoverPath] = m; miniaturasOrden.Add(a.CoverPath);
                while (miniaturasOrden.Count > MiniaturasMax)
                {
                    string k0 = miniaturasOrden[0]; miniaturasOrden.RemoveAt(0);
                    Bitmap b0;
                    if (miniaturas.TryGetValue(k0, out b0)) { miniaturas.Remove(k0); if (b0 != null) b0.Dispose(); }
                }
                return m;
            }
            internal static Bitmap Escala(Image img, int lado)
            {
                int w = img.Width, h = img.Height, m = Math.Min(w, h);
                if (m < 1) throw new ArgumentException("portada vacia");
                Bitmap b = new Bitmap(lado, lado, PixelFormat.Format32bppPArgb);
                try
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(img, new Rectangle(0, 0, lado, lado), new Rectangle((w - m) / 2, (h - m) / 2, m, m), GraphicsUnit.Pixel);
                    }
                }
                catch { b.Dispose(); throw; }
                return b;
            }

            // ======================================================================================================
            //  EL DESTELLO DE LA TECLA ESCRITA (como en el boceto): al escribir una letra -con el teclado de la pantalla o con
            //  uno de verdad- su tecla se enciende un instante. Es un halo de la tarjeta grafica encima del teclado (lo pide
            //  FillUiGlows en cada cuadro): no rehace ninguna imagen.
            // ======================================================================================================
            internal const double DestelloMs = 320;
            private int destelloF = -1, destelloC = -1; private double destelloDesde = double.MinValue;
            private void MarcaTecla(char ch)
            {
                char u = char.ToUpperInvariant(ch);
                for (int f = 0; f < rows.Count; f++)
                    for (int c = 0; c < rows[f].Count; c++)
                    {
                        SearchKey k = rows[f][c];
                        if ((k.Kind == KKind.Char && char.ToUpperInvariant(k.Ch) == u) || (k.Kind == KKind.Space && ch == ' ') || (k.Kind == KKind.Back && ch == '\b'))
                        { destelloF = f; destelloC = c; destelloDesde = GpuPanel.AhoraMs(); return; }
                    }
            }
            // Donde va el destello y cuanto se ve ahora (se apaga en DestelloMs). false = no hay.
            internal bool GetDestelloTecla(double ahora, out float cx, out float cy, out float r, out int argb)
            {
                cx = cy = r = 0f; argb = 0;
                if (!nuevo || destelloF < 0 || destelloF >= rows.Count || destelloC < 0 || destelloC >= rows[destelloF].Count) return false;
                double t = ahora - destelloDesde;
                if (t < 0 || t >= DestelloMs) return false;
                AseguraReparto();
                SearchKey k = rows[destelloF][destelloC];
                float q = (float)(1.0 - t / DestelloMs);
                cx = k.R.X + k.R.Width / 2f; cy = k.R.Y + k.R.Height / 2f; r = Math.Max(k.R.Width, k.R.Height) * 0.95f;
                Color c = (k.Kind == KKind.Back) ? SRed : Vivo(Accent);
                argb = Color.FromArgb((int)(210f * q * q), c.R, c.G, c.B).ToArgb();
                return true;
            }

            // ======================================================================================================
            //  MOVERSE, PULSAR Y TOCAR
            // ======================================================================================================
            private void MoveSelNuevo(int dx, int dy)
            {
                AseguraReparto();
                int n = CartasTotales();
                if (EnChips)
                {
                    int na = atajoRects.Count;
                    if (na == 0) { EnChips = false; Invalidate(); return; }
                    if (SelChip < 0) SelChip = 0; else if (SelChip >= na) SelChip = na - 1;
                    if (dy > 0)
                    {
                        EnChips = false;
                        SelRow = 0; SelCol = TeclaMasCerca(0, atajoRects[SelChip].X + atajoRects[SelChip].Width / 2f);
                    }
                    else if (dx != 0) SelChip = Math.Max(0, Math.Min(na - 1, SelChip + dx));
                    Invalidate();
                    return;
                }
                if (FocusResults)
                {
                    if (n == 0) { FocusResults = false; Invalidate(); return; }
                    int sel = Math.Max(0, Math.Min(n - 1, SelResult));
                    int fila = sel / 2, col = sel % 2;
                    if (dx < 0 && col == 0)
                    {
                        // Vuelta al teclado: a la fila de teclas que tiene al lado, en su ultima tecla.
                        FocusResults = false;
                        Rectangle c = CartaRect(sel);
                        float cy = c.IsEmpty ? rep.Teclado.Y : c.Y + c.Height / 2f;
                        SelRow = FilaMasCerca(cy); SelCol = rows[SelRow].Count - 1;
                        Invalidate();
                        return;
                    }
                    if (dx != 0)
                    {
                        int nc = col + dx;
                        if (nc >= 0 && nc <= 1 && fila * 2 + nc < n) sel = fila * 2 + nc;
                    }
                    if (dy != 0)
                    {
                        int nf = fila + dy;
                        if (nf >= 0 && nf < FilasTotales()) sel = Math.Min(n - 1, nf * 2 + col);
                    }
                    SelResult = sel;
                    AjustaScroll();
                    Invalidate();
                    return;
                }
                if (rows.Count == 0) return;
                if (SelRow < 0) SelRow = 0; else if (SelRow >= rows.Count) SelRow = rows.Count - 1;
                if (SelCol < 0) SelCol = 0; else if (SelCol >= rows[SelRow].Count) SelCol = rows[SelRow].Count - 1;
                if (dy < 0 && SelRow == 0)
                {
                    if (atajoRects.Count > 0)
                    {
                        RectangleF k = rows[SelRow][SelCol].R;
                        EnChips = true; SelChip = AtajoMasCerca(k.X + k.Width / 2f);
                    }
                    Invalidate();
                    return;
                }
                if (dy != 0)
                {
                    int nr = Math.Max(0, Math.Min(rows.Count - 1, SelRow + dy));
                    if (nr != SelRow)
                    {
                        RectangleF k = rows[SelRow][SelCol].R;
                        SelRow = nr; SelCol = TeclaMasCerca(nr, k.X + k.Width / 2f);
                    }
                }
                if (dx != 0)
                {
                    if (dx > 0 && SelCol >= rows[SelRow].Count - 1 && n > 0)
                    {
                        // A las tarjetas: a la fila que se ve mas cerca de la tecla, en su primera columna.
                        RectangleF k = rows[SelRow][SelCol].R;
                        FocusResults = true; SelResult = CartaMasCerca(k.Y + k.Height / 2f, n);
                        AjustaScroll();
                        Invalidate();
                        return;
                    }
                    SelCol = Math.Max(0, Math.Min(rows[SelRow].Count - 1, SelCol + dx));
                }
                Invalidate();
            }
            private int TeclaMasCerca(int fila, float cx)
            {
                int mejor = 0; float d = float.MaxValue;
                for (int c = 0; c < rows[fila].Count; c++)
                {
                    RectangleF k = rows[fila][c].R;
                    float dd = Math.Abs(k.X + k.Width / 2f - cx);
                    if (dd < d) { d = dd; mejor = c; }
                }
                return mejor;
            }
            private int FilaMasCerca(float cy)
            {
                int mejor = 0; float d = float.MaxValue;
                for (int f = 0; f < rows.Count; f++)
                {
                    if (rows[f].Count == 0) continue;
                    RectangleF k = rows[f][0].R;
                    float dd = Math.Abs(k.Y + k.Height / 2f - cy);
                    if (dd < d) { d = dd; mejor = f; }
                }
                return mejor;
            }
            private int AtajoMasCerca(float cx)
            {
                int mejor = 0; float d = float.MaxValue;
                for (int i = 0; i < atajoRects.Count; i++)
                {
                    float dd = Math.Abs(atajoRects[i].X + atajoRects[i].Width / 2f - cx);
                    if (dd < d) { d = dd; mejor = i; }
                }
                return mejor;
            }
            private int CartaMasCerca(float cy, int n)
            {
                RepartoBusca r = rep;
                int mejor = resScroll * 2; float d = float.MaxValue;
                for (int f = 0; f < r.Filas; f++)
                {
                    int i = (resScroll + f) * 2;
                    if (i >= n) break;
                    float yc = r.Resultados.Y + f * (r.CartaH + r.CartaHueco) + r.CartaH / 2f;
                    float dd = Math.Abs(yc - cy);
                    if (dd < d) { d = dd; mejor = i; }
                }
                return Math.Min(mejor, n - 1);
            }
            private void PressNuevo()
            {
                if (EnChips)
                {
                    if (SelChip >= 0 && SelChip < atajoRects.Count && OnAtajo != null) OnAtajo(SelChip);
                    return;
                }
                if (FocusResults)
                {
                    if (Results != null && SelResult >= 0 && SelResult < CartasTotales() && OnChoose != null) OnChoose(Results[SelResult]);
                    return;
                }
                if (SelRow < 0 || SelRow >= rows.Count || SelCol < 0 || SelCol >= rows[SelRow].Count) return;
                Activate(rows[SelRow][SelCol]);
            }
            private void ClickNuevo(Point p)
            {
                AseguraReparto();
                for (int f = 0; f < rows.Count; f++)
                    for (int c = 0; c < rows[f].Count; c++)
                        if (rows[f][c].R.Contains(p.X, p.Y))
                        {
                            EnChips = false; FocusResults = false; SelRow = f; SelCol = c;
                            Activate(rows[f][c]);
                            Invalidate();
                            return;
                        }
                for (int i = 0; i < atajoRects.Count; i++)
                    if (atajoRects[i].Contains(p))
                    {
                        EnChips = true; SelChip = i; FocusResults = false;
                        Invalidate();
                        if (OnAtajo != null) OnAtajo(i);
                        return;
                    }
                int n = CartasTotales();
                for (int i = resScroll * 2; i < n; i++)
                {
                    Rectangle c = CartaRect(i);
                    if (c.IsEmpty) break;
                    if (c.Contains(p)) { if (OnChoose != null) OnChoose(Results[i]); return; }
                }
            }
        }
    }

    internal sealed partial class GpuPanel
    {
        public Func<int, int, Rectangle[]> GetPiezaCambios;
        private void SubePieza(int i, Bitmap bitmap)
        {
            if (piezaTex[i] != IntPtr.Zero && piezaW[i] == bitmap.Width && piezaH[i] == bitmap.Height && GetPiezaCambios != null)
            {
                Rectangle[] changes = GetPiezaCambios(i, piezaVer[i]);
                if (changes != null)
                {
                    bool updated = true;
                    foreach (Rectangle area in changes)
                        if (!new Rectangle(0, 0, bitmap.Width, bitmap.Height).Contains(area)
                            || !stars.UpdateOverlayRegion(piezaTex[i], bitmap, area)) { updated = false; break; }
                    if (updated) return;
                }
            }
            piezaTex[i] = Upload(piezaTex[i], ref piezaW[i], ref piezaH[i], bitmap, out piezaU[i], out piezaV[i]);
        }
        // EL BUSCADOR CON PORTADAS: cada tarjeta es su trozo de la pieza de las tarjetas, con su transparencia y lo que le
        // falta por subir, y cuanto se ve la tarjeta elegida (que entra con la suya). Lo pone el formulario (FillCartas).
        // Sin tarjetas, la pieza se dibuja entera.
        internal delegate int LlenaCartasDel(CartaGpu[] buf, out float elegida);
        public LlenaCartasDel GetCartas;
        public float CartaElegidaAlfa = 1f;   // la de este cuadro: la usa la tarjeta elegida (pieza 2)
        internal const int PiezaCartas = 4;
        private readonly CartaGpu[] cartasBuf = new CartaGpu[24];
        private readonly D3D9.Vtx[] cartasVtx = new D3D9.Vtx[24 * 6];

        // Va ANTES que las demas piezas: la tarjeta elegida y la tecla elegida se dibujan encima. 'dy' la corre hacia abajo
        // (con la ventana del buscador subiendo) y 'alfa' y 'opacidad' son los de lo que tiene debajo.
        private void DibujaCartasPrimero(float dy, float alfa, float opacidad)
        {
            IntPtr tex = piezaTex[PiezaCartas];   // MaxPiezas la incluye (D3D9Star.cs)
            if (tex == IntPtr.Zero || GetPiezaRect == null || PiezasN <= PiezaCartas) { CartaElegidaAlfa = 1f; return; }
            Rectangle pr = GetPiezaRect(PiezaCartas);
            if (pr.Width < 1 || pr.Height < 1) { CartaElegidaAlfa = 1f; return; }
            float elegida = 1f;
            int n = (GetCartas != null) ? GetCartas(cartasBuf, out elegida) : 0;
            CartaElegidaAlfa = (n > 0) ? Math.Max(0f, Math.Min(1f, elegida)) : 1f;
            if (n <= 0)
            {
                SetTexQuadA(piezaQuad, pr.X, pr.Y + dy, pr.X + pr.Width, pr.Y + pr.Height + dy, piezaU[PiezaCartas], piezaV[PiezaCartas],
                            FadeArgb(((uint)(alfa * 255) << 24) | 0xFFFFFFu, opacidad));
                stars.DrawQuad(tex, piezaQuad);
                return;
            }
            float bw = Math.Max(1, piezaW[PiezaCartas]), bh = Math.Max(1, piezaH[PiezaCartas]);
            float su = piezaU[PiezaCartas] / bw, sv = piezaV[PiezaCartas] / bh;
            int m = 0;
            for (int i = 0; i < n && i < cartasBuf.Length; i++)
            {
                CartaGpu c = cartasBuf[i];
                float a = Math.Max(0f, Math.Min(1f, c.Alfa)) * alfa;
                int ai = (int)(a * 255f + .5f);
                if (ai <= 0 || c.X1 <= c.X0 || c.Y1 <= c.Y0) continue;
                uint col = FadeArgb(((uint)ai << 24) | 0xFFFFFFu, opacidad);
                float x0 = pr.X + c.X0 - .5f, y0 = pr.Y + c.Y0 + c.Dy + dy - .5f, x1 = pr.X + c.X1 - .5f, y1 = pr.Y + c.Y1 + c.Dy + dy - .5f;
                float u0 = c.X0 * su, v0 = c.Y0 * sv, u1 = c.X1 * su, v1 = c.Y1 * sv;
                int o = m * 6;
                VC(ref cartasVtx[o + 0], x0, y0, col, u0, v0); VC(ref cartasVtx[o + 1], x1, y0, col, u1, v0); VC(ref cartasVtx[o + 2], x1, y1, col, u1, v1);
                VC(ref cartasVtx[o + 3], x0, y0, col, u0, v0); VC(ref cartasVtx[o + 4], x1, y1, col, u1, v1); VC(ref cartasVtx[o + 5], x0, y1, col, u0, v1);
                m++;
            }
            if (m > 0) stars.DrawTexturedBatch(tex, cartasVtx, m * 2);
        }
        private static void VC(ref D3D9.Vtx x, float px, float py, uint c, float u, float v)
        { x.X = px; x.Y = py; x.Z = 0; x.Rhw = 1; x.Color = c; x.U = u; x.V = v; }

        // ---- CON LA VENTANA DEL BUSCADOR (sube al abrir y baja al cerrar: BuildWinState en Form1.Sonido.cs) ----
        // La casilla, lo elegido y (en el buscador con portadas) las tarjetas van en sus piezas, no en la imagen grande: sin
        // esto la ventana subiria sin ellas y saldrian de golpe al llegar. Vale para los dos buscadores. Al ABRIR, encima de
        // la ventana van las piezas de ahora, corridas lo mismo que ella. Al CERRAR, la ventana se queda con las texturas ya
        // subidas de las piezas (sin copiar ni subir nada, como RobaUiParaVentana) y se sueltan al acabar. Con las piezas van
        // las cintas de su vista previa (lo que se desliza: CintasConLaVentana).
        public Func<bool> GetPiezasSuben;
        private int ventVistaPrevia = 3;   // cual de las que se llevo es la de la vista previa
        private readonly IntPtr[] ventPiezaTex = new IntPtr[MaxPiezas];
        private readonly Rectangle[] ventPiezaRect = new Rectangle[MaxPiezas];
        private readonly float[] ventPiezaU = new float[MaxPiezas], ventPiezaV = new float[MaxPiezas];
        internal int PiezasEnLaVentana()
        {
            int n = 0;
            for (int i = 0; i < MaxPiezas; i++) if (ventPiezaTex[i] != IntPtr.Zero) n++;
            return n;
        }
        internal void RobaPiezasParaVentana()
        {
            SueltaPiezasVentana();
            ventVistaPrevia = PiezaVistaPrevia();
            if (GetPiezaRect == null) return;
            for (int i = 0; i < PiezasN && i < MaxPiezas; i++)
            {
                if (piezaTex[i] == IntPtr.Zero) continue;
                Rectangle pr = GetPiezaRect(i);
                if (pr.Width < 1 || pr.Height < 1) continue;
                ventPiezaTex[i] = piezaTex[i]; ventPiezaRect[i] = pr; ventPiezaU[i] = piezaU[i]; ventPiezaV[i] = piezaV[i];
                piezaTex[i] = IntPtr.Zero; piezaW[i] = piezaH[i] = -1;
                piezaVer[i] = int.MinValue;   // la que toque despues se vuelve a pedir y a subir
            }
        }
        internal void SueltaPiezasVentana()
        {
            for (int i = 0; i < MaxPiezas; i++)
                if (ventPiezaTex[i] != IntPtr.Zero) { D3D9.Release(ventPiezaTex[i]); ventPiezaTex[i] = IntPtr.Zero; }
        }
        // Despues de dibujar la ventana (D3D9Star.cs). 'uiPropia' = la ventana es la propia imagen de la UI (abriendo).
        private void PiezasConLaVentana(GpuWin gw, bool uiPropia)
        {
            if (!gw.Has) { if (PiezasEnLaVentana() > 0) SueltaPiezasVentana(); return; }
            float a = Math.Max(0f, Math.Min(1f, gw.Alpha));
            if (gw.HideUi && uiPropia)
            {
                if (GetPiezaRect != null && GetPiezasSuben != null && GetPiezasSuben()) DibujaPiezasCorridas(gw.Dy, a);
                return;
            }
            // Cerrando: primero las tarjetas y encima lo demas, como siempre. Pieza a pieza y sin bucle: son cinco y fijas
            // (la bateria 'envios' vigila que no aparezcan envios nuevos dentro de bucles).
            uint ink = ((uint)(a * 255f + .5f) << 24) | 0xFFFFFFu;
            PiezaDeLaVentana(PiezaCartas, gw.Dy, ink);
            PiezaDeLaVentana(0, gw.Dy, ink); PiezaDeLaVentana(1, gw.Dy, ink); PiezaDeLaVentana(2, gw.Dy, ink); PiezaDeLaVentana(3, gw.Dy, ink);
            int vp = ventVistaPrevia;
            if (vp >= 0 && vp < MaxPiezas && ventPiezaTex[vp] != IntPtr.Zero) CintasConLaVentana(ventPiezaRect[vp], gw.Dy, a);
        }
        private void PiezaDeLaVentana(int i, float dy, uint ink)
        {
            if (ventPiezaTex[i] == IntPtr.Zero) return;
            Rectangle pr = ventPiezaRect[i];
            SetTexQuadA(piezaQuad, pr.X, pr.Y + dy, pr.X + pr.Width, pr.Y + pr.Height + dy, ventPiezaU[i], ventPiezaV[i], ink);
            stars.DrawQuad(ventPiezaTex[i], piezaQuad);
        }
        // Las piezas de ahora corridas 'dy', con la transparencia de la ventana: lo mismo que el bucle de las piezas de
        // D3D9Star.cs, sin el vidrio de la lista de canciones (con el buscador abierto no esta). Tambien pieza a pieza.
        private void DibujaPiezasCorridas(float dy, float a)
        {
            DibujaCartasPrimero(dy, a, 1f);
            PiezaCorrida(0, dy, a); PiezaCorrida(1, dy, a); PiezaCorrida(2, dy, a); PiezaCorrida(3, dy, a);
            int vp = PiezaVistaPrevia();
            if (vp < PiezasN && piezaTex[vp] != IntPtr.Zero)
            {
                Rectangle pr = GetPiezaRect(vp);
                if (pr.Width >= 1 && pr.Height >= 1) CintasConLaVentana(pr, dy, a * FocusAlpha(vp, pr) * (vp == 2 ? CartaElegidaAlfa : 1f));
            }
        }
        private void PiezaCorrida(int i, float dy, float a)
        {
            if (i >= PiezasN || i >= MaxPiezas || piezaTex[i] == IntPtr.Zero) return;
            Rectangle pr = GetPiezaRect(i);
            if (pr.Width < 1 || pr.Height < 1) return;
            uint ink = ((uint)(a * FocusAlpha(i, pr) * (i == 2 ? CartaElegidaAlfa : 1f) * 255) << 24) | 0xFFFFFFu;
            SetTexQuadA(piezaQuad, pr.X, pr.Y + dy, pr.X + pr.Width, pr.Y + pr.Height + dy, piezaU[i], piezaV[i], ink);
            stars.DrawQuad(piezaTex[i], piezaQuad);
        }

        // ---- LAS CINTAS DE LA VISTA PREVIA (Titulos largos deslizantes: TextoMovilGpu.cs) ----
        // El grupo 4 de cintas es lo que se desliza de la vista previa del buscador: en el de siempre, el texto de debajo del
        // cubo (pieza 3); en el buscador con portadas, que no tiene cubo, la tarjeta elegida (pieza 2). Lo dice el formulario.
        public Func<int> GetPiezaVistaPrevia;
        internal int PiezaVistaPrevia()
        {
            int p = (GetPiezaVistaPrevia != null) ? GetPiezaVistaPrevia() : 3;
            return (p >= 0 && p < MaxPiezas) ? p : 3;
        }
        private bool cintasVentana;           // dibujando las cintas de la vista previa con la ventana moviendose
        private Rectangle cintaOrigenVentana;
        private float cintaAlfaVentana;
        // Que grupos de cintas van encima de la lista (DibujaCintasTexto). Sin la ventana, lo mismo que antes: la 4 sin el
        // vidrio de las canciones y las demas con el; con la ventana moviendose, solo las de la vista previa.
        private bool CintaDeLista(int grupo)
        {
            if (GetPiezaRect == null) return false;
            if (cintasVentana) return grupo == 4;
            return (grupo == 4) ? !GlassPieces : GlassPieces;
        }
        private Rectangle OrigenVistaPrevia() { return cintasVentana ? cintaOrigenVentana : GetPiezaRect(PiezaVistaPrevia()); }
        // Lo que se ve de las cintas de la vista previa: lo mismo que su pieza (la tarjeta elegida, ademas, entra con la suya).
        private float AlfaVistaPrevia(Rectangle origen)
        {
            if (cintasVentana) return cintaAlfaVentana;
            int p = PiezaVistaPrevia();
            return FocusAlpha(p, origen) * PaletteAlpha() * ControlsOpacity * (p == 2 ? CartaElegidaAlfa : 1f);
        }
        // Las cintas de la vista previa encima de su pieza, corridas con la ventana (al pixel: la ventana dura 210 ms).
        private void CintasConLaVentana(Rectangle pieza, float dy, float alfa)
        {
            cintasVentana = true;
            cintaOrigenVentana = new Rectangle(pieza.X, pieza.Y + (int)Math.Round(dy), pieza.Width, pieza.Height);
            cintaAlfaVentana = Math.Max(0f, Math.Min(1f, alfa));
            try { DibujaCintasTexto(true); }
            finally { cintasVentana = false; }
        }
    }
#endif
}

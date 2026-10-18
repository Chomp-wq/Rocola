// LA PANTALLA VIVA, TANDA 2: DENTRO DEL DISCO. Las ideas del lienzo de bocetos para el interior de un disco (24-09-2026).
// Solo con tarjeta grafica.
//
// Cada idea tiene su interruptor en Ctrl+F3 -> Pantalla -> Dentro del disco y se guarda en rocola_config.txt. Sin
// tarjeta grafica no hacen nada, y tampoco cuando el disco lo pinta el procesador (un video que lo tapa, Ctrl+F11):
// ese dibujo esta congelado y sale exactamente como antes.
//
//  - LISTA GRANDE. Filas mas altas y letra mas grande, unas nueve canciones por pagina: se lee de lejos.
//  - ECUALIZADOR EN LA QUE SUENA. La pastilla de SONANDO lleva cuatro barritas que se mueven con la musica en vez
//    del triangulito. Las barras las pone la tarjeta encima de la lista: la pagina no se vuelve a hornear.
//  - EN COLA, CON LOS MINUTOS. "EN COLA 3 · en ~9 min", con la misma cuenta que el aviso de tu turno.
//  - VER ADONDE VA. Al pedir una cancion dentro del disco, su boleto sale de la fila y vuela hasta la capsula de la
//    cola (o hasta la tarjeta de lo que suena, si no sonaba nada); al llegar, la capsula da un saltito y su numero
//    sube rodando.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // Existen en los dos perfiles porque la configuracion es la misma; sin tarjeta grafica no se usan.
        private bool listaGrande = true;   // "lista_grande="
        private bool eqSonando = true;     // "eq_sonando="
        private bool colaMinutos = true;   // "cola_minutos="
        private bool vueloCola = true;     // "vuelo_cola="
    }

#if ROCOLA_GPU
    // Lo que el panel de la tarjeta necesita de cada idea, cuadro a cuadro.
    internal struct GpuEq { public bool Has; public float X, Y, W, H; public int Barra, Hueco; }
    internal struct GpuBoleto { public bool Has; public Bitmap Bmp; public int Ver; public RectangleF De, A; public double Desde; }

    public partial class Form1
    {
        // ---------------- LA LISTA, CUANDO LA DIBUJA LA TARJETA ----------------
        // La lista del disco hace cosas distintas SOLO mientras la compone la tarjeta grafica. Se decide aqui, en
        // cada sincronizacion del panel (SyncGpuFullScreen), y se deshace en cuanto el disco lo vuelve a pintar el
        // procesador. Por eso el dibujo por procesador sale siempre el de siempre (lo comprueba pruebas/discovivo.cs).
        internal const int FilaNormal = 30;
        private Font fuenteListaNormal, fuenteListaGrande;
        // Si el disco, abierto, lo compondria ahora la tarjeta: la misma cuenta que hace SyncGpuFullScreen para el
        // modo disco, sin mirar el buscador (con el buscador encima la lista no se ve, y asi no cambia de ida y vuelta).
        private bool ListaPorGpu()
        {
            if (!gpuEnabled || gpuPanel == null) return false;
            bool cfgOn = (lblConfig != null && lblConfig.Visible);
            bool videoBlocks = videoOn && !videoGpuOn;
            return gpuDiscTry && !videoBlocks && !videoFull && !cfgOn && !attractOn;
        }
        private void AjustaDiscoVivo(bool porGpu)
        {
            if (songList == null) return;
            if (fuenteListaNormal == null) fuenteListaNormal = songList.Font;   // la de siempre, la que puso el constructor
            int vivo = 0;
            if (porGpu)
            {
                if (listaGrande) vivo |= SongListView.VivoGrande;
                if (eqSonando) vivo |= SongListView.VivoEq;
                if (colaMinutos) vivo |= SongListView.VivoMinutos;
            }
            int fila = FilaNormal;
            Font f = fuenteListaNormal, vieja = null;
            if ((vivo & SongListView.VivoGrande) != 0)
            {
                fila = FilaGrande(songList.Height);
                float cuerpo = CuerpoGrande(fila);
                if (fuenteListaGrande == null || Math.Abs(fuenteListaGrande.SizeInPoints - cuerpo) > 0.01f)
                {
                    vieja = fuenteListaGrande;
                    fuenteListaGrande = new Font(fuenteListaNormal.FontFamily, cuerpo, FontStyle.Bold);
                }
                f = fuenteListaGrande;
            }
            if (songList.GetEspera == null) songList.GetEspera = EsperaDePuesto;
            if (songList.Vivo == vivo && songList.RowH == fila && ReferenceEquals(songList.Font, f)) return;
            bool otraFila = songList.RowH != fila;
            songList.Vivo = vivo; songList.RowH = fila;
            if (!ReferenceEquals(songList.Font, f)) songList.Font = f;
            if (vieja != null && !ReferenceEquals(vieja, f)) vieja.Dispose();   // ya no la usa nadie
            if ((vivo & SongListView.VivoMinutos) != 0) ActualizaEsperas(true);
            // Otro alto de fila es otro reparto de paginas. Se llega a la pagina de la elegida SIN hojear -no te has
            // movido, solo cambio cuantas caben- y no hay de donde hojear: otro alto de fila siempre lleva otra letra,
            // y la letra nueva ya tiro lo horneado (SongListView.OnFontChanged).
            if (otraFila && panelSongs != null && panelSongs.Visible && songItems.Count > 0) FillSongPage();
            RefrescaLista();
        }
        // El alto de fila de la lista grande: unas nueve por pagina, sin bajar de 44 px ni pasar de 80.
        internal static int FilaGrande(int altoLista)
        {
            int f = altoLista / 9;
            return (f < 44) ? 44 : (f > 80) ? 80 : f;
        }
        // Y su letra, en puntos: un tercio de la fila (en 1024x768, unos 16,7; la de siempre es de 13).
        internal static float CuerpoGrande(int fila) { return fila / 3f; }

        // ---------------- EN COLA, CON LOS MINUTOS ----------------
        // Cuanto falta para cada puesto de la cola, en minutos ya redondeados como en el aviso de tu turno. Se
        // recalcula cada medio segundo (solo si la lista lo pide) y, si algun numero cambio, la pagina se vuelve a
        // hornear: en la practica, una vez por minuto.
        private int[] esperaMin = new int[0];   // por puesto (0 = el primero); -1 = no se sabe
        private int esperaTick;
        private int EsperaDePuesto(int puesto)
        {
            int[] e = esperaMin;
            int i = puesto - 1;
            return (e != null && i >= 0 && i < e.Length) ? e[i] : -1;
        }
        // Los piden la lista del disco y, con "Lo que suena, en grande", el panel de reposo (SuenaVivoGpu.cs).
        private bool QuiereEsperas()
        {
            if (songList != null && (songList.Vivo & SongListView.VivoMinutos) != 0) return true;
            return suenaGrande && estadoExpandido && gpuEnabled;
        }
        private void ActualizaEsperas(bool ya)
        {
            if (!QuiereEsperas()) return;
            int ahora = Environment.TickCount;
            if (!ya && esperaTick != 0 && ahora - esperaTick < 500) return;
            esperaTick = ahora; if (esperaTick == 0) esperaTick = 1;
            int n = queue.Count;
            int[] nuevo = new int[n];
            double s = RestanDeLaQueSuena();
            for (int i = 0; i < n; i++)
            {
                nuevo[i] = MinutosEspera(s);
                if (s >= 0)
                {
                    double d = (queue[i] != null) ? SegundosQueSuenan(queue[i].PathKey()) : 0;   // sin los silencios que se saltan
                    s = (d > 0) ? s + d : -1;   // de una que no se sabe en adelante, ninguna se sabe
                }
            }
            int[] antes = esperaMin;
            bool igual = antes != null && nuevo.Length == antes.Length;
            for (int i = 0; igual && i < n; i++) if (nuevo[i] != antes[i]) igual = false;
            if (igual) return;
            esperaMin = nuevo;
            CambioElPanelDeReposo();   // el panel grande dice en cuanto suena cada una (SuenaVivoGpu.cs)
            if (songList == null) return;
            songList.EsperaVer++;
            RefrescaLista();
        }
        // Segundos -> lo que se escribe: -1 no se sabe, 0 menos de un minuto, y si no los minutos redondeados.
        internal static int MinutosEspera(double s)
        {
            if (s < 0 || double.IsNaN(s)) return -1;
            if (s < 60) return 0;
            return (int)((s + 30) / 60);
        }
        internal static string TextoCola(int puesto, int minutos)
        {
            string t = "EN COLA " + puesto;
            if (minutos < 0) return t;
            return t + ((minutos == 0) ? " · ya casi" : (" · en ~" + minutos + " min"));
        }

        // ---------------- EL ECUALIZADOR ----------------
        // Donde van las barritas: lo apunto el horneado de la pagina (SongListView.PastillaSonandoEq), relativo a su
        // fila. Solo con la pagina quieta y la que suena a la vista.
        private GpuEq BuildEqState()
        {
            GpuEq q = new GpuEq();
            if (songList == null || (songList.Vivo & SongListView.VivoEq) == 0 || gpuMode != 3) return q;
            if (panelSongs == null || !panelSongs.Visible || songList.Sliding || searchOpen) return q;
            int idx = songList.eqIdx;
            if (idx < songList.PageStart || idx >= songList.PageStart + songList.RowsPerPage || idx >= songItems.Count) return q;
            if (songList.GetSonando == null || !songList.GetSonando(idx)) return q;
            float yFila = panelSongs.Top + songList.Top + (idx - songList.PageStart) * songList.RowH;
            q.Has = true;
            q.X = panelSongs.Left + songList.Left + songList.eqX; q.W = songList.eqW; q.H = songList.eqH;
            q.Y = yFila + (songList.RowH - q.H) * .5f;
            q.Barra = songList.eqBarra; q.Hueco = songList.eqHueco;
            return q;
        }

        // ---------------- EL BOLETO QUE VUELA A LA COLA ----------------
        // No es el vuelo de la portada de la pared (VuelaALaCola, que reusa el viaje entre cubos): aqui lo que vuela
        // es la CANCION -su titulo y su portadita- desde la fila donde la pediste, en arco, hasta donde va a esperar.
        internal const int BoletoMs = 820;            // lo que dura, de la fila a la capsula
        internal const float BoletoAparece = .10f;    // el primer tramo: aparece encima de la fila
        internal const float BoletoLlega = .86f;      // cuando toca la capsula (despues se hunde en ella)
        private Bitmap boletoBmp; private int boletoVer; private GpuBoleto boleto;
        // Devuelve si salio. Solo con la tarjeta dibujando el disco, la fila elegida a la vista y la pagina quieta.
        private bool VuelaBoleto(PlayItem it)
        {
            if (!vueloCola || it == null || !gpuEnabled || gpuPanel == null || gpuMode != 3) return false;
            if (panelSongs == null || !panelSongs.Visible || songList == null || songList.Sliding) return false;
            int fila = songSel - songList.PageStart;
            if (fila < 0 || fila >= songList.RowsPerPage || songSel >= songItems.Count) return false;
            // Si no sonaba nada, la cancion no espera: va directa a sonar. Entonces vuela a la tarjeta de lo que suena.
            bool aCola = nowPlaying != null;
            RectangleF hasta;
            if (aCola)
            {
                if (pastCola == null || !pastCola.Visible || pastCola.Height < 8) return false;
                hasta = new RectangleF(pastCola.Left, pastCola.Top, pastCola.Width, pastCola.Height);
            }
            else
            {
                if (miniCube == null || miniCube.Height < 8) return false;
                hasta = new RectangleF(miniCube.Left, miniCube.Top, miniCube.Width, miniCube.Height);
            }
            int x0 = panelSongs.Left + songList.Left, y0 = panelSongs.Top + songList.Top + fila * songList.RowH;
            string titulo = TituloSinPista(songItems[songSel].ToString());
            Size tam = TamanoBoleto(songList.RowH, titulo, songList.Font, songList.Width);
            if (boletoBmp == null || boletoBmp.Width != tam.Width || boletoBmp.Height != tam.Height)
            {
                if (boletoBmp != null) boletoBmp.Dispose();
                boletoBmp = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppPArgb);
            }
            Image tapa = (cubeBox != null) ? cubeBox.Cover : null;   // la del disco abierto, ya en memoria
            PintaBoleto(boletoBmp, tapa, titulo, songList.Font);
            boleto.Has = true; boleto.Bmp = boletoBmp; boleto.Ver = ++boletoVer;
            // Sale CALCADO encima de su fila: su titulo, con la misma letra, justo encima del de la fila. Asi parece
            // que la fila se despega y se va volando.
            float xb = x0 + songList.XTexto() - TextoBoletoX(tam.Height);
            boleto.De = new RectangleF(xb, y0 + (songList.RowH - tam.Height) * .5f, tam.Width, tam.Height);
            boleto.A = hasta;
            boleto.Desde = GpuPanel.AhoraMs();
            // La capsula se entera de cuando llega: hasta entonces enseña el numero de antes.
            if (aCola) pastCola.Llega((int)(BoletoMs * BoletoLlega));
            return true;
        }
        private GpuBoleto BuildBoletoState()
        {
            if (boleto.Has && GpuPanel.AhoraMs() - boleto.Desde > BoletoMs + 50) boleto.Has = false;
            return boleto;
        }
        // El boleto mide casi una fila de alto y lo que pida su titulo (con la letra de la lista) de ancho, sin pasar
        // del 62 % de la lista.
        internal static Size TamanoBoleto(int fila, string titulo, Font f, int anchoLista)
        {
            int h = Math.Max(30, (int)(fila * .92f));
            int w = (int)Math.Ceiling(TextoBoletoX(h)) + AnchoTexto(titulo ?? "", f) + (int)(h * .16f * 1.4f) + 4;
            int max = Math.Max(h * 3, (int)(anchoLista * .62f));
            if (w > max) w = max;
            if (w < h * 3) w = h * 3;
            return new Size(w, h);
        }
        // Donde empieza el titulo dentro del boleto: despues de la portadita.
        internal static float TextoBoletoX(int h)
        {
            float pad = h * .16f, lado = h - 2 * pad;
            return pad + 1f + lado + pad * 1.3f;
        }
        internal static void PintaBoleto(Bitmap bmp, Image tapa, string titulo, Font f)
        {
            int w = bmp.Width, h = bmp.Height;
            using (Graphics g = Graphics.FromImage(bmp))
            using (StringFormat izq = new StringFormat())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                // Dos pixeles transparentes alrededor: vuela girado y con filtro, y asi su borde suavizado no toca el filo.
                RectangleF caja = new RectangleF(2f, 2f, w - 4.5f, h - 4.5f);
                using (GraphicsPath p = Redondo(caja, h * .28f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(242, 16, 60, 38))) g.FillPath(b, p);
                    using (Pen pe = new Pen(ColaVerde, 1.6f)) g.DrawPath(pe, p);
                }
                float pad = h * .16f, lado = h - 2 * pad;
                RectangleF rt = new RectangleF(pad + 1f, pad, lado, lado);
                using (GraphicsPath pt = Redondo(rt, lado * .18f))
                {
                    if (tapa != null)
                    {
                        GraphicsState st = g.Save();
                        g.SetClip(pt);
                        lock (tapa) g.DrawImage(tapa, rt);
                        g.Restore(st);
                    }
                    else using (SolidBrush b = new SolidBrush(Color.FromArgb(90, ColaVerde))) g.FillPath(b, pt);
                }
                // El mismo formato que el titulo de la fila, para salir calcado encima de el.
                izq.FormatFlags = StringFormatFlags.NoWrap; izq.Trimming = StringTrimming.EllipsisCharacter;
                izq.Alignment = StringAlignment.Near; izq.LineAlignment = StringAlignment.Center;
                float xt = TextoBoletoX(h);
                using (SolidBrush b = new SolidBrush(Color.White))
                    g.DrawString(titulo ?? "", f, b, new RectangleF(xt, 0f, Math.Max(10f, w - xt - pad * 1.4f), h), izq);
            }
        }

        // Una vez por latido del reloj de la pantalla: los minutos de la cola.
        private void DiscoVivoTic()
        {
            ActualizaEsperas(false);
        }
    }

    public partial class Form1
    {
        private partial class SongListView
        {
            // Lo que cambia cuando la lista la dibuja la tarjeta grafica. 0 = nada: la lista de siempre, pixel a
            // pixel. Lo pone Form1.AjustaDiscoVivo, y solo mientras el disco se compone por tarjeta.
            internal const int VivoGrande = 1, VivoEq = 2, VivoMinutos = 4;
            public int Vivo;
            public Func<int, int> GetEspera;   // puesto en la cola -> minutos que faltan (0 = menos de uno; -1 = no se sabe)
            public int EsperaVer;              // sube cuando cambia algun minuto: entra en la firma del horneado
            // Donde quedaron las barritas en el ultimo horneado: de que cancion y en que sitio de su fila.
            internal int eqIdx = -1; internal float eqX, eqW, eqH; internal int eqBarra, eqHueco;
            private float durCuerpo = -1f; private int durAncho;

            // El circulito del numero crece con la fila (con la de siempre se queda en 11 px de radio).
            internal static float RadioGrande(int fila) { return Math.Min(20f, fila * .29f); }
            // El hueco de la duracion con la letra grande: lo que mide "00:00" (con la de siempre bastan 58 px).
            private int AnchoDuracion(Graphics g)
            {
                float c = Font.SizeInPoints;
                if (Math.Abs(c - durCuerpo) > 0.01f) { durAncho = Math.Max(58, AnchoTexto(g, "00:00", Font) + 4); durCuerpo = c; }
                return durAncho;
            }
            // EL REPARTO DE UNA FILA, cuando la dibuja la tarjeta. Manda el TITULO: es lo que se lee de lejos.
            //  - Una pedida dice los minutos ("EN COLA 3 · en ~9 min") si su titulo sigue cabiendo entero o, si no, le
            //    queda al menos el 30 % de su sitio. Si ni asi, la de siempre, "EN COLA 3". En la lista de tamaño normal
            //    se calla su genero si con eso caben: de una cancion pedida importa mas cuando suena que de que es.
            //  - En la lista grande, el genero va en toda la pagina o en ninguna fila (PlanPaginaGpu).
            // Devuelve el texto de la pastilla de la cola (null si no esta pedida); 'gen' vuelve vacio si se calla.
            internal const float TituloMinimo = .30f;
            private bool generosFuera;   // lista grande: esta pagina va sin pastillas de genero
            private string PlanFilaGpu(Graphics g, int idx, int pos, bool enCola, bool suena, Rectangle r, int durW, float xTexto, int hCola, ref string gen)
            {
                bool grande = (Vivo & VivoGrande) != 0;
                if (grande && generosFuera) gen = null;
                if (!enCola) return null;
                string t = "EN COLA " + pos;
                int min = ((Vivo & VivoMinutos) != 0 && GetEspera != null) ? GetEspera(pos) : -1;
                if (min < 0) return t;
                string largo = Form1.TextoCola(pos, min);
                float sitio = r.Width - xTexto;
                float quitaSon = suena ? AnchoSonando(g, hCola) + 8f : 0f;
                int quitaGen = AnchoGenero(gen, r.Width, xTexto);
                float queda = TituloCabe(r, durW, xTexto, AnchoTexto(g, largo, fCola) + hCola + 8f + quitaSon);
                float titulo = AnchoTexto(g, (GetText != null) ? GetText(idx) : "", Font);
                if (titulo <= queda - quitaGen || queda - quitaGen >= sitio * TituloMinimo) return largo;
                if (!grande && quitaGen > 0 && (titulo <= queda || queda >= sitio * TituloMinimo)) { gen = null; return largo; }
                return t;
            }
            // LA LISTA GRANDE: las pastillas del genero salen en TODAS las filas de la pagina o en NINGUNA. Salen solo si
            // en todas el titulo cabe entero con su genero al lado; una fila sin genero entre otras que lo llevan haria
            // pensar que esa cancion es de otra cosa. Mira la pagina de verdad (PageStart), tambien cuando se hornea
            // solo la fila elegida: si no, la elegida podria decidir distinto que las demas.
            private void PlanPaginaGpu(Graphics g, int rpp, int n, int w, float xTexto, int hCola)
            {
                generosFuera = false;
                for (int row = 0; row < rpp; row++)
                {
                    int idx = PageStart + row;
                    if (idx >= n) break;
                    string gen = (GetGenero != null) ? GetGenero(idx) : null;
                    int quitaGen = AnchoGenero(gen, w, xTexto);
                    if (quitaGen == 0) continue;   // esta fila no llevaria pastilla de todos modos
                    int pos = (GetQueuePos != null) ? GetQueuePos(idx) : 0;
                    bool suena = (GetSonando != null) && GetSonando(idx);
                    string dur = (GetRight != null) ? GetRight(idx) : "";
                    int durW = string.IsNullOrEmpty(dur) ? 0 : AnchoDuracion(g);
                    Rectangle r = new Rectangle(0, row * RowH, w, RowH);
                    string t = PlanFilaGpu(g, idx, pos, pos > 0, suena, r, durW, xTexto, hCola, ref gen);
                    float marcas = (suena ? AnchoSonando(g, hCola) + 8f : 0f) + ((t != null) ? AnchoTexto(g, t, fCola) + hCola + 8f : 0f);
                    float titulo = AnchoTexto(g, (GetText != null) ? GetText(idx) : "", Font);
                    if (titulo > TituloCabe(r, durW, xTexto, marcas) - quitaGen) { generosFuera = true; return; }
                }
            }
            // Lo que ocupa la pastilla del genero con sus huecos, o 0 si no saldria (la misma regla que PintaPagina: solo
            // si cabe en la mitad de la fila).
            private int AnchoGenero(string gen, int anchoFila, float xTexto)
            {
                if (string.IsNullOrEmpty(gen)) return 0;
                Bitmap pg = HorneaGenero(gen);
                return (pg != null && pg.Width + 18 <= (anchoFila - (int)xTexto) / 2) ? pg.Width + 18 : 0;
            }
            // Lo que le queda al titulo con estas marcas a su derecha (la misma cuenta que PintaPagina).
            private static float TituloCabe(Rectangle r, int durW, float xTexto, float marcas)
            {
                return r.Right - 14 - durW - marcas - ((marcas > 0f) ? 4f : 0f) - 6f - xTexto;
            }
            // Lo que ocupa la pastilla de SONANDO que va a salir (sin su separacion).
            private float AnchoSonando(Graphics g, int hCola)
            {
                if ((Vivo & VivoEq) == 0) return AnchoTexto(g, "► SONANDO", fCola) + hCola;
                int barra, hueco, pad, sep; MedidasEq(hCola, out barra, out hueco, out pad, out sep);
                return pad + 4 * barra + 3 * hueco + sep + AnchoTexto(g, "SONANDO", fCola) + pad;
            }
            private static void MedidasEq(int hCola, out int barra, out int hueco, out int pad, out int sep)
            {
                barra = Math.Max(2, (int)Math.Round(hCola * .12f)); hueco = Math.Max(1, (int)Math.Round(hCola * .08f));
                pad = (int)(hCola * .5f); sep = (int)(hCola * .3f);
            }
            // Donde empieza el titulo de una fila (lo usa el boleto para salir calcado encima de el).
            internal float XTexto()
            {
                float rad = ((Vivo & VivoGrande) != 0) ? RadioGrande(RowH) : Math.Min(11f, RowH * 0.34f);
                return 16 + rad * 2 + 10;
            }
            // La pastilla de SONANDO con el hueco del ecualizador a la izquierda: las barritas las pone la tarjeta
            // encima, en su sitio (eqX/eqW/eqH, relativo a la fila). Devuelve lo que ocupa, como la de siempre.
            private int PastillaSonandoEq(Graphics g, int idx, ref float xMarca, float cy, int hCola)
            {
                const string t = "SONANDO";
                int barra, hueco, pad, sep; MedidasEq(hCola, out barra, out hueco, out pad, out sep);
                int anchoEq = 4 * barra + 3 * hueco;
                int wt = AnchoTexto(g, t, fCola);
                int wp = pad + anchoEq + sep + wt + pad;
                xMarca -= wp;
                RectangleF cr = new RectangleF(xMarca, cy - hCola * 0.5f, wp, hCola);
                g.DrawImageUnscaled(Pastilla(true, wp, hCola), (int)cr.X, (int)cr.Y);
                float x0 = (int)cr.X + pad;
                colaB.Color = SonAmbar;
                g.DrawString(t, fCola, colaB, new RectangleF(x0 + anchoEq + sep, cr.Y, wt, hCola), cenFmt);
                eqIdx = idx; eqX = x0; eqW = anchoEq; eqH = (float)Math.Round(hCola * .56f); eqBarra = barra; eqHueco = hueco;
                return wp + 8;
            }
        }

        private partial class PastillaView
        {
            // LA CANCION QUE LLEGA A LA COLA (solo con tarjeta grafica). Mientras su boleto vuela se sigue viendo el
            // numero de antes; al aterrizar, la capsula da un saltito, destella y el numero sube rodando.
            internal const int SaltoMs = 300, LlegaRodMs = 260, LlegaDestMs = 520;
            internal const float SaltoAlto = .14f;
            private int llegaTick; private string llegaDe = "";
            private bool dibujaGpu;
            // El boleto aterriza dentro de 'enMs'. Si el anterior aun no habia aterrizado, se sigue viendo el numero
            // de antes del primero: el rodillo ira de ahi al que haya al llegar.
            public void Llega(int enMs)
            {
                int t = Environment.TickCount;
                bool pendiente = llegaTick != 0 && t - llegaTick < 0;
                if (!pendiente) llegaDe = Valor ?? "";
                llegaTick = t + Math.Max(0, enMs); if (llegaTick == 0) llegaTick = 1;
            }
            // El dibujo para la textura de la tarjeta. El del procesador (OnPaint) NUNCA enseña la llegada.
            public void RenderContentGpu(Graphics g)
            {
                dibujaGpu = true;
                try { RenderContent(g); }
                finally { dibujaGpu = false; }
            }
            private const int LlegaTotalMs = LlegaDestMs;   // lo que mas dura de las tres cosas
            // Solo DESDE que aterriza: mientras vuela no cambia nada de la capsula y su imagen no tiene que rehacerse.
            private bool LlegaViva(int t) { return llegaTick != 0 && t - llegaTick >= 0 && t - llegaTick < LlegaTotalMs; }
            private float SaltoLlegada(int t)
            {
                if (!dibujaGpu || llegaTick == 0) return 1f;
                int d = t - llegaTick;
                if (d < 0 || d >= SaltoMs) return 1f;
                return 1f + SaltoAlto * (float)Math.Sin(Math.PI * d / SaltoMs);
            }
            private float DestelloLlegada(int t)
            {
                if (!dibujaGpu || llegaTick == 0) return 0f;
                int d = t - llegaTick;
                return (d < 0 || d >= LlegaDestMs) ? 0f : 1f - d / (float)LlegaDestMs;
            }
            // El rodillo de la llegada: -1 = no toca; 0 mientras vuela (el numero de antes, quieto); de 0 a 1 al llegar.
            private float RodadoLlegada(int t, ref string de)
            {
                if (!dibujaGpu || llegaTick == 0) return -1f;
                int d = t - llegaTick;
                if (d >= LlegaTotalMs) { llegaTick = 0; return -1f; }
                if (d >= LlegaRodMs) return -1f;
                de = llegaDe;
                return (d < 0) ? 0f : d / (float)LlegaRodMs;
            }
        }
    }

    internal sealed partial class GpuPanel
    {
        public Func<GpuEq> GetEq;
        public Func<GpuBoleto> GetBoleto;
        private IntPtr boletoTex; private int boletoVer = -1, boletoBw, boletoBh; private float boletoU = 1f, boletoV = 1f;
        private GpuBoleto boletoCuadro;
        private double discoUltMs, discoRelojMs;
        private readonly float[] eqAlto = new float[4];
        private readonly D3D9.Vtx[] boletoQuad = new D3D9.Vtx[6], eqV = new D3D9.Vtx[4 * 6];

        // El momento del cuadro que se esta dibujando (el de cuando se VERA, si se sabe), sin ir nunca hacia atras.
        private double MomentoDisco()
        {
            double t = (vistoMs >= 0) ? vistoMs : AhoraMs();
            if (t < discoRelojMs) t = discoRelojMs;
            discoRelojMs = t;
            return t;
        }
        // Textura del boleto: ANTES de Begin, como las demas. Solo se sube cuando cambia su version.
        private void PreparaDiscoVivo()
        {
            boletoCuadro = (GetBoleto != null) ? GetBoleto() : new GpuBoleto();
            if (boletoCuadro.Has && boletoCuadro.Bmp != null && (boletoCuadro.Ver != boletoVer || boletoTex == IntPtr.Zero))
            { boletoTex = Upload(boletoTex, ref boletoBw, ref boletoBh, boletoCuadro.Bmp, out boletoU, out boletoV); boletoVer = boletoCuadro.Ver; }
            double ahora = MomentoDisco();
            float dt = (discoUltMs > 0) ? (float)Math.Max(0, Math.Min(.1, (ahora - discoUltMs) / 1000.0)) : 0f;
            discoUltMs = ahora;
            PasoEq(ahora, dt);
        }
        // Un cuadro con textura GIRADO 'ang' alrededor de su centro, con su tramo de textura (u0,v0)-(u1,v1).
        internal static void SetTexQuadGiro(D3D9.Vtx[] v, float cx, float cy, float hw, float hh, float ang,
                                            float u0, float v0, float u1, float v1, uint argb)
        {
            float c = (float)Math.Cos(ang), s = (float)Math.Sin(ang);
            float ax = cx - hw * c + hh * s, ay = cy - hw * s - hh * c;   // arriba a la izquierda
            float bx = cx + hw * c + hh * s, by = cy + hw * s - hh * c;   // arriba a la derecha
            float dx = cx + hw * c - hh * s, dy = cy + hw * s + hh * c;   // abajo a la derecha
            float ex = cx - hw * c - hh * s, ey = cy - hw * s + hh * c;   // abajo a la izquierda
            SetV(ref v[0], ax, ay, u0, v0); SetV(ref v[1], bx, by, u1, v0); SetV(ref v[2], dx, dy, u1, v1);
            SetV(ref v[3], ax, ay, u0, v0); SetV(ref v[4], dx, dy, u1, v1); SetV(ref v[5], ex, ey, u0, v1);
            for (int i = 0; i < 6; i++) v[i].Color = argb;
        }

        // LAS BARRITAS. Cada una con su ritmo (como en el boceto: 0,9 / 0,7 / 1,1 / 0,8 s) y empujada por la musica:
        // la primera por los graves, las del medio por los medios y la ultima por los agudos. Asi se mueven siempre
        // que suena algo y saltan con los golpes.
        private static readonly double[] EqPeriodo = { .9, .7, 1.1, .8 };
        internal static float AlturaBarra(int k, double seg, float nivel)
        {
            if (k < 0 || k >= EqPeriodo.Length) return 0f;
            if (float.IsNaN(nivel) || nivel < 0f) nivel = 0f; else if (nivel > 1f) nivel = 1f;
            double osc = .5 + .5 * Math.Sin(2.0 * Math.PI * (seg / EqPeriodo[k] + k * .23));
            double v = .45 * osc + .75 * nivel;
            if (v > 1.0) v = 1.0;
            return (float)(.22 + .78 * v);
        }
        private void PasoEq(double ahora, float dt)
        {
            float mid = lunaMid, hi = lunaHigh;
            float sube = 1f - (float)Math.Exp(-dt * 30.0), baja = 1f - (float)Math.Exp(-dt * 10.0);
            for (int k = 0; k < 4; k++)
            {
                float nivel = (k == 0) ? lunaBeat : (k == 1) ? mid : (k == 2) ? (mid + hi) * .5f : hi;
                float meta = AlturaBarra(k, ahora / 1000.0, nivel);
                // Sube casi al instante y baja algo mas despacio: marca el golpe sin temblar.
                float kk = (meta > eqAlto[k]) ? sube : baja;
                eqAlto[k] += (meta - eqAlto[k]) * kk;
            }
        }
        // Encima de la lista (y de la fila elegida, que es una pieza aparte): cuatro rectangulos en una llamada.
        private void DibujaEq()
        {
            GpuEq q = (GetEq != null) ? GetEq() : new GpuEq();
            if (!q.Has || q.W < 4f || q.H < 3f || q.Barra < 1) return;
            int a = (int)(SceneAlpha() * ControlsOpacity * 255f + .5f); if (a > 255) a = 255;
            if (a <= 0) return;
            uint col = ((uint)a << 24) | ((uint)Form1.SonAmbar.R << 16) | ((uint)Form1.SonAmbar.G << 8) | Form1.SonAmbar.B;
            for (int k = 0; k < 4; k++)
            {
                float alto = Math.Max(2f, q.H * Math.Max(0f, Math.Min(1f, eqAlto[k])));
                float x0 = q.X + k * (q.Barra + q.Hueco), x1 = x0 + q.Barra, y1 = q.Y + q.H, y0 = y1 - alto;
                SetV(ref eqV[k * 6 + 0], x0, y0, 0, 0); SetV(ref eqV[k * 6 + 1], x1, y0, 0, 0); SetV(ref eqV[k * 6 + 2], x1, y1, 0, 0);
                SetV(ref eqV[k * 6 + 3], x0, y0, 0, 0); SetV(ref eqV[k * 6 + 4], x1, y1, 0, 0); SetV(ref eqV[k * 6 + 5], x0, y1, 0, 0);
                for (int i = 0; i < 6; i++) eqV[k * 6 + i].Color = col;
            }
            stars.DrawMesh(eqV, 8);
        }

        // EL VUELO DEL BOLETO: aparece encima de la fila, sube un poco en arco y cae en la capsula encogiendose; al
        // tocarla se hunde en ella y se apaga, y la capsula destella (su saltito y su numero van en su propia imagen).
        internal static void PasoBoleto(float u, RectangleF de, RectangleF a, out float x, out float y, out float esc, out float alfa, out float ang)
        {
            float x0 = de.X + de.Width * .5f, y0 = de.Y + de.Height * .5f;
            float x2 = a.X + a.Width * .5f, y2 = a.Y + a.Height * .5f;
            // Llega del alto de la capsula y sin pasar de la mitad de su ancho: cae DENTRO de ella.
            float sFin = Math.Min(a.Height * .85f / Math.Max(1f, de.Height), a.Width * .6f / Math.Max(1f, de.Width));
            sFin = Math.Max(.15f, Math.Min(1f, sFin));
            // El punto de control del arco: un poco por encima de los dos extremos, mas cerca del de salida.
            float x1 = x0 + (x2 - x0) * .35f, y1 = Math.Min(y0, y2) - Math.Max(24f, Math.Abs(y2 - y0) * .35f);
            if (!(u > 0f)) u = 0f; else if (u > 1f) u = 1f;
            if (u < Form1.BoletoAparece)
            {
                // Aparece QUIETO y a su tamaño, encima de la fila: la fila se pone verde y se despega.
                x = x0; y = y0; esc = 1f; alfa = u / Form1.BoletoAparece; ang = 0f;
                return;
            }
            if (u < Form1.BoletoLlega)
            {
                float v = (u - Form1.BoletoAparece) / (Form1.BoletoLlega - Form1.BoletoAparece);
                // Mitad "arranca y frena suave", mitad "arranca ya": se despega de la fila sin esperar y llega frenando.
                float q3 = (1f - v) * (1f - v) * (1f - v);
                float e = .5f * ((v < .5f) ? 4f * v * v * v : 1f - 4f * q3) + .5f * (1f - q3);
                float q = 1f - e;
                x = q * q * x0 + 2f * q * e * x1 + e * e * x2;
                y = q * q * y0 + 2f * q * e * y1 + e * e * y2;
                esc = 1f + (sFin - 1f) * e; alfa = 1f;
                ang = -.10f * (float)Math.Sin(Math.PI * v);   // se inclina un poco en el aire y llega derecho
                return;
            }
            float h = (u - Form1.BoletoLlega) / (1f - Form1.BoletoLlega);
            x = x2; y = y2; esc = sFin * (1f - .25f * h); alfa = 1f - h; ang = 0f;
        }
        private void DibujaBoleto()
        {
            if (!boletoCuadro.Has || boletoTex == IntPtr.Zero || boletoCuadro.Bmp == null) return;
            float u = (float)((MomentoDisco() - boletoCuadro.Desde) / Form1.BoletoMs);
            if (!(u >= 0f) || u >= 1f) return;
            float x, y, esc, alfa, ang;
            PasoBoleto(u, boletoCuadro.De, boletoCuadro.A, out x, out y, out esc, out alfa, out ang);
            float bw = boletoCuadro.Bmp.Width, bh = boletoCuadro.Bmp.Height;
            // Al tocar la capsula, un destello detras (el mismo halo de los viajes de portada).
            if (u >= Form1.BoletoLlega)
            {
                float k = (u - Form1.BoletoLlega) / (1f - Form1.BoletoLlega);
                int ga = (int)(120f * (1f - k) + .5f);
                if (ga > 0) stars.DrawGlow(x, y, Math.Max(boletoCuadro.A.Height, 24f) * 1.1f, ((uint)ga << 24) | 0x0078E6A5u);
            }
            int ai = (int)(alfa * 255f + .5f); if (ai > 255) ai = 255;
            if (ai <= 0) return;
            SetTexQuadGiro(boletoQuad, x, y, (bw - 2f) * .5f * esc, (bh - 2f) * .5f * esc, ang,
                           boletoU / bw, boletoV / bh, boletoU * (bw - 1f) / bw, boletoV * (bh - 1f) / bh, ((uint)ai << 24) | 0x00FFFFFFu);
            stars.DrawQuad(boletoTex, boletoQuad);
        }
        private void SueltaDiscoVivo()
        {
            if (boletoTex != IntPtr.Zero) { D3D9.Release(boletoTex); boletoTex = IntPtr.Zero; }
            boletoVer = -1; boletoBw = boletoBh = 0;
        }
    }
#endif
}

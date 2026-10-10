// EL BUSCADOR: la pantalla, el indice y lo que se tecleado
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
        private sealed class QueryBag { internal string Key; internal BolsaLetra Data; }
        private List<QueryBag> recentQueries;
        private int recentQueryItems;
        private List<char> letterQueryOrder;
        private bool lastMatchesApproximate;
        private void ClearRecentQueries()
        {
            if (recentQueries != null) recentQueries.Clear();
            recentQueryItems = 0;
            if (bolsaLetra != null) bolsaLetra.Clear();
            if (letterQueryOrder != null) letterQueryOrder.Clear();
        }
        private BolsaLetra FindRecentQuery(string key)
        {
            if (recentQueries == null || key.Length > 128) return null;
            for (int i = 0; i < recentQueries.Count; i++)
            {
                QueryBag entry = recentQueries[i];
                if (entry.Key != key) continue;
                recentQueries.RemoveAt(i); recentQueries.Add(entry);
                return entry.Data;
            }
            return null;
        }
        private void RememberQuery(string key, BolsaLetra data)
        {
            if (recentQueries == null) recentQueries = new List<QueryBag>();
            while (recentQueries.Count > 0 && (recentQueries.Count >= 8 || recentQueryItems + data.Lista.Count > 65536))
            {
                recentQueryItems -= recentQueries[0].Data.Lista.Count;
                recentQueries.RemoveAt(0);
            }
            QueryBag entry = new QueryBag(); entry.Key = key; entry.Data = data;
            recentQueries.Add(entry); recentQueryItems += data.Lista.Count;
        }

        private BolsaLetra FindLetterQuery(char key)
        {
            BolsaLetra data;
            if (bolsaLetra == null || !bolsaLetra.TryGetValue(key, out data)) return null;
            if (letterQueryOrder == null) letterQueryOrder = new List<char>();
            letterQueryOrder.Remove(key); letterQueryOrder.Add(key);
            return data;
        }
        private void RememberLetterQuery(char key, BolsaLetra data)
        {
            // Maximo 16 letras y 262144 referencias, tambien con bibliotecas grandes
            // o alfabetos distintos. No es un limite de resultados ni del indice.
            if (data.Lista.Count > 131072 || bolsaLetra == null) return;
            if (letterQueryOrder == null) letterQueryOrder = new List<char>();
            int retained = 0;
            foreach (BolsaLetra old in bolsaLetra.Values) retained += old.Lista.Count;
            while (bolsaLetra.Count > 0 && (bolsaLetra.Count >= 16 || retained + data.Lista.Count > 262144))
            {
                char oldest;
                if (letterQueryOrder.Count > 0)
                { oldest = letterQueryOrder[0]; letterQueryOrder.RemoveAt(0); }
                else
                {
                    // Tambien tolerar que una prueba o el reescaneo vacie el diccionario solo.
                    oldest = key;
                    foreach (char candidate in bolsaLetra.Keys) { oldest = candidate; break; }
                }
                BolsaLetra old;
                if (bolsaLetra.TryGetValue(oldest, out old)) { retained -= old.Lista.Count; bolsaLetra.Remove(oldest); }
            }
            bolsaLetra[key] = data;
            letterQueryOrder.Remove(key); letterQueryOrder.Add(key);
        }

        private SearchOverlay searchOverlay;
        // Lo que casa con cada letra SUELTA, ya puntuado. Ver RecomputeResults: la primera letra es la unica
        // que obliga a puntuar el indice entero, y para una letra el resultado no cambia mientras no cambie
        // la musica. Se vacia al rearmar la biblioteca.
        private sealed class BolsaLetra
        {
            public List<SearchItem> Lista;
            public int[] Puntos;
            public List<SearchItem> Mejores;
            public bool Approximate;
        }
        private BuscaView buscaView;        // casilla de BUSCAR (arriba en medio, con los dos botones dibujados)
        private static bool BuscaTramo(string texto, string qNorm, out int ini, out int largo)
        {
            ini = 0; largo = 0;
            if (string.IsNullOrEmpty(texto) || string.IsNullOrEmpty(qNorm)) return false;
            TextoTramo dato = PreparaTramo(texto);
            string limpio = dato.Limpio; int[] mapa = dato.Mapa; int j = limpio.Length;
            int k = limpio.IndexOf(qNorm, StringComparison.Ordinal);
            if (k < 0 || k >= j) return false;
            ini = mapa[k];
            int fin = (k + qNorm.Length < j) ? mapa[k + qNorm.Length] : texto.Length;
            largo = fin - ini;
            if (largo <= 0 || ini + largo > texto.Length) return false;
            return true;
        }

        private void BuildSearchPostings()
        {
            searchPostings = null;
            int n = searchIndex.Count;
            if (n < 16000) return;
            int[] counts = new int[28];
            for (int i = 0; i < n; i++)
                for (int bit = 0; bit < counts.Length; bit++)
                    if ((searchIndex[i].Mask & (1 << bit)) != 0) counts[bit]++;
            List<SearchItem>[] postings = new List<SearchItem>[28];
            int limit = Math.Min(n / 8, 8192);
            int selected = 0;
            for (int bit = 0; bit < counts.Length; bit++)
                if (counts[bit] <= limit)
                {
                    postings[bit] = new List<SearchItem>(counts[bit]);
                    selected |= 1 << bit;
                }
            for (int i = 0; i < n; i++)
            {
                SearchItem item = searchIndex[i];
                int mask = item.Mask & selected;
                if (mask == 0) continue;
                for (int bit = 0; bit < postings.Length; bit++)
                    if ((mask & (1 << bit)) != 0) postings[bit].Add(item);
            }
            searchPostings = postings;
        }

        private List<SearchItem> SearchCandidates(List<SearchItem> source, int mask)
        {
            if (!object.ReferenceEquals(source, searchIndex) || searchPostings == null) return source;
            List<SearchItem> best = source;
            for (int bit = 0; bit < searchPostings.Length; bit++)
            {
                List<SearchItem> list = searchPostings[bit];
                if ((mask & (1 << bit)) != 0 && list != null && list.Count < best.Count) best = list;
            }
            return best;
        }

        // ==========  PASTILLA (creditos, cola)  ==========
        // Un dato suelto dentro de una capsula: la etiqueta en gris y el numero en el color que le toca. Antes
        // eran dos textos sueltos apilados en una esquina; en una capsula se leen como una UNIDAD y de lejos se
        // distingue el numero, que es lo unico que el cliente necesita ver.
        // ==========  LA CASILLA DE BUSCAR  ==========
        //
        // Antes, para buscar por nombre habia que apretar ROJO y VERDE A LA VEZ. Nadie lo descubria: no
        // estaba escrito en ninguna parte que fuera un combo, y el cliente que llega por primera vez -de
        // noche, con prisa y con los amigos mirando- no se pone a probar parejas de botones. O sea que la
        // forma MAS RAPIDA de poner una cancion -saber el nombre y escribirlo- era la unica que nadie usaba.
        //
        // Ahora hay una casilla grande en medio de la parte de arriba, con una lupa, que PARPADEA en azul
        // para que se vea desde lejos, y AL LADO estan dibujados los dos botones que hay que apretar: el
        // rojo, un mas, y la flecha de arriba. No hay nada que leer ni que adivinar: se ve que son dos y
        // se ve cuales son.
        private class BuscaView : Control
        {
            public bool Abierto = false;      // con el buscador abierto la casilla se queda encendida fija
            public double Phase = 0;          // para el parpadeo
            private Font fTxt; private float fAlto = -1f;

            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref fTxt); } }
                finally { base.Dispose(disposing); }
            }

            public BuscaView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
                this.TabStop = false;
            }

            // Lo que mide de ancho con ese alto. El acomodo necesita saberlo ANTES de dibujar nada.
            public static int Ancho(int alto)
            {
                int d = Sim(alto);
                // lupa + hueco + "BUSCAR" + hueco + rojo + mas + flecha, con aire a los lados
                return (int)(d * 1.15f) + 8 + (int)(alto * 2.05f) + 12 + d + 10 + d + 8 + (int)(d * 0.9f) + 22;
            }
            private static int Sim(int alto) { int d = (int)(alto * 0.52f); if (d < 12) d = 12; if (d > 30) d = 30; return d; }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                RenderContent(e.Graphics);
            }

            public void RenderContent(Graphics g) { RenderContent(g, false); }
            // forGpu=true: se dibuja a brillo FIJO. El parpadeo lo pone la tarjeta grafica como un halo
            // aparte (FillUiGlows); si parpadeara aqui, habria que rehacer y volver a subir la textura de la
            // interfaz entera veinticuatro veces por segundo, que es justo lo que no se puede hacer.
            public void RenderContent(Graphics g, bool forGpu)
            {
                int w = Width, h = Height;
                if (w < 30 || h < 12) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (fTxt == null || fAlto != h)
                { if (fTxt != null) fTxt.Dispose(); fTxt = new Font(FontFamily.GenericSansSerif, Math.Max(8f, h * 0.30f), FontStyle.Bold); fAlto = h; }

                // EL PARPADEO. Con el buscador cerrado late para llamar la atencion; con el buscador abierto
                // se queda fija y encendida, porque ya cumplio su trabajo y parpadeando distraeria.
                double lat = (Abierto || forGpu) ? 1.0 : (0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(Phase * 2.4)));
                Color azul = Color.FromArgb(90, 190, 255);
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (GraphicsPath gp = RoundedPath(r, Math.Min(14, h / 2)))
                {
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb((int)(28 + 58 * lat), azul))) g.FillPath(sb, gp);
                    using (Pen pn = new Pen(Color.FromArgb((int)(110 + 145 * lat), azul), 2f)) g.DrawPath(pn, gp);
                }

                int cy = h / 2, d = Sim(h);
                float x = 11;
                Color tinta = Color.FromArgb((int)(170 + 85 * lat), 235, 245, 255);
                x = Lupa(g, x, cy, (int)(d * 1.15f), tinta);
                x = CtrlTxt(g, fTxt, "BUSCAR", x + 8, cy, tinta) + 12;
                // Y los dos botones que hay que apretar, dibujados tal cual son.
                x = CtrlDot(g, x, cy, d, Color.FromArgb(228, 72, 72)) + 4;
                x = CtrlTxt(g, fTxt, "+", x, cy, tinta) + 6;
                Flecha(g, x, cy, (int)(d * 0.9f), Color.FromArgb(228, 72, 72));
            }

            // Donde va el halo del parpadeo cuando lo dibuja la tarjeta grafica (coordenadas dentro del control).
            public bool GetLatido(out float cx, out float cy, out float r)
            {
                cx = Width / 2f; cy = Height / 2f; r = Width * 0.55f;
                return !Abierto && Width > 30 && Height > 12;
            }

            // La lupa: un aro y su mango.
            private static float Lupa(Graphics g, float x, int cy, int d, Color c)
            {
                float aro = d * 0.72f;
                using (Pen pn = new Pen(c, Math.Max(2f, d * 0.13f)))
                {
                    g.DrawEllipse(pn, x, cy - aro / 2f, aro, aro);
                    float m = aro * 0.72f;
                    g.DrawLine(pn, x + aro * 0.85f, cy + aro * 0.35f, x + aro * 0.85f + m * 0.5f, cy + aro * 0.35f + m * 0.5f);
                }
                return x + d;
            }

            // La flecha de ARRIBA, en una tecla redondeada: se lee como un boton, no como un adorno.
            private static void Flecha(Graphics g, float x, int cy, int d, Color c)
            {
                Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
                using (GraphicsPath gp = RoundedPath(k, Math.Max(3, d / 4)))
                {
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, c))) g.FillPath(sb, gp);
                    using (Pen pn = new Pen(Color.FromArgb(215, c), 1.6f)) g.DrawPath(pn, gp);
                }
                float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
                CtrlTri(g, c, new PointF(cx, cy - t), new PointF(cx - an, cy + t * 0.62f), new PointF(cx + an, cy + t * 0.62f));
            }
        }

        // ==========  BUSCADOR EN PANTALLA  ==========
        private enum KKind { Char, Space, Back, Choose }

        private class SearchKey
        {
            public KKind Kind;
            public char Ch;
            public string Label;
            public RectangleF R;
        }

        private partial class SearchOverlay : Control
        {
            public bool SeparateGpu = false;
            public string Query = "";
            public List<SearchItem> Results = new List<SearchItem>();
            // De que disco es y de que genero cada resultado. Los pone Form1 (el genero lo aprende el paseo de
            // fondo). 'DatosVer' sube cuando eso cambia y entra en la firma del cache de las tarjetas: sin el,
            // los colores que llegan tarde no se verian hasta teclear otra cosa.
            public Func<SearchItem, string> GetDisco;
            public Func<SearchItem, string> GetGenero;
            public int DatosVer;
            // QUE CANCION YA ESTA PEDIDA, Y CUAL SUENA AHORA. Lo mismo que ya dice la lista de dentro del
            // disco, con las mismas palabras y los mismos colores: el cliente busca "Depeche", ve cuarenta
            // canciones y no tiene forma de acordarse de cuales pidio hace diez minutos.
            //
            // 'ColaVer' sube cada vez que la cola cambia y entra en la firma del cache de las tarjetas: sin
            // el, pedir una cancion no cambiaria nada de lo que se ve hasta teclear otra letra.
            public Func<SearchItem, int> GetQueuePos;    // 0 = no esta pedida; si no, su puesto en la cola
            public Func<SearchItem, bool> GetSonando;
            public int ColaVer;
            public int SelRow = 1;
            public int SelCol = 0;
            public bool FocusResults = false;
            public int SelResult = 0;
            private int resCols = 1, resRowsPerCol = 8;   // dimensiones de la cuadricula de resultados (se fijan al pintar)
            public double CubeAngle = 0;
            public double Phase = 0;
            public Color Accent = DefaultAccent;
            public Func<Album, Image> GetCover;
            public Action<string> OnQueryChanged;
            public Action<SearchItem> OnChoose;
            public Action OnClose;
            public bool ToastOn = false;
            public string ToastMsg = "";
            public Color ToastBack = Color.FromArgb(238, 40, 165, 90);
            private Rectangle cubeRect = Rectangle.Empty;
            public double PreviewAngle { get { return CubeAngle; } }   // angulo del cubo de vista previa (lo dibuja la GPU)
            // Calcula EN VIVO el cubo de vista previa (celda + par de portadas a fundir) para que la GPU lo
            // dibuje como geometria. Devuelve false si no hay cubo de vista previa visible ahora.
            public bool GetPreviewCube(out Rectangle area, out Image ca, out Image cb, out float f)
            {
                area = Rectangle.Empty; ca = null; cb = null; f = 0f;
#if ROCOLA_GPU
                if (Nuevo) return false;   // el buscador con portadas no tiene cubo: la portada va en cada tarjeta
#endif
                int W = Width, H = Height;
                if (W < 60 || H < 60) return false;
                if (!(FocusResults && Results != null && Results.Count > 0 && GetCover != null)) return false;
                int M = 30, contentY = M + 70 + 28, contentH = H - contentY - M;
                if (contentH < 60) return false;
                int kbW = (int)((W - 2 * M) * 0.52);
                return ComputePreview(new Rectangle(M, contentY, kbW, contentH), out ca, out cb, out f, out area);
            }
            private readonly List<Image> artistCovers = new List<Image>();
            private Bitmap blendBmp;
            // COMPARTIDO con el fondo del form, igual que la rejilla de discos y el interior del disco: este
            // control pinta su VENTANA del mismo campo. Antes el buscador era el UNICO que se armaba su propio
            // campo de estrellas, y eso costaba doble: por un lado otro juego de bitmaps de pantalla completa
            // en memoria, por otro que su cielo era OTRO -las estrellas saltaban al abrir el buscador-.
            public Starfield Sky;
            public float PulseLo = 0f, PulseMid = 0f, PulseHi = 0f;   // pulso 0..1 por banda (graves/medios/agudos)
            public float BgDim = 1f;        // atenuacion del starfield mientras se navega con musica (el form la anima)
            // Brillo de la barra de instrucciones del buscador (Mover / Poner-Elegir / Cerrar) al pulsar.
            public const int SMover = 0, SPoner = 1, SCerrar = 2;
            private static readonly Color SRed = Color.FromArgb(228, 72, 72), SGreen = Color.FromArgb(72, 212, 112), SYellow = Color.FromArgb(242, 206, 72);
            private readonly InstrFlash hintFlash = new InstrFlash();
            public void FlashMove(bool vertical) { hintFlash.Fire(SMover, vertical ? SRed : SYellow); }   // arriba/abajo ROJO, izq/der AMARILLO
            public void FlashOK() { hintFlash.Fire(SPoner, SGreen); }
            public void FlashClose() { hintFlash.Fire(SCerrar, Color.White); }
            // Centro/radio de los 3 halos del hint bar (Mover/Poner/Cerrar), guardados al maquetar. En GPU el halo NO
            // se hornea en la textura: lo dibuja la GPU como quad (GetHintGlow) para que el desvanecido no re-suba la textura.
            private readonly float[] hgCx = new float[3], hgCy = new float[3], hgR = new float[3];
            private void StoreHintGlow(int k, float leftX, float cy, float symW) { hgCx[k] = leftX + symW / 2f; hgCy[k] = cy; hgR[k] = symW * 0.9f + 6f; }
            // La flecha de ARRIBA en una tecla redondeada, igual que en la casilla de BUSCAR de la fila de
            // arriba: el cliente ve el MISMO dibujo antes de entrar y dentro, asi que no tiene que aprender
            // dos cosas.
            private static float FlechaArriba(Graphics g, float x, int cy, int d, Color c)
            {
                Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
                using (GraphicsPath gp = RoundedPath(k, Math.Max(3, d / 4)))
                {
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, c))) g.FillPath(sb, gp);
                    using (Pen pn = new Pen(Color.FromArgb(215, c), 1.6f)) g.DrawPath(pn, gp);
                }
                float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
                CtrlTri(g, c, new PointF(cx, cy - t), new PointF(cx - an, cy + t * 0.62f), new PointF(cx + an, cy + t * 0.62f));
                return x + d;
            }
            public bool GetHintGlow(int k, out float cx, out float cy, out float r, out int argb)
            {
                cx = cy = r = 0f; argb = 0;
                if (k < 0 || k > 2) return false;
                float lvl = hintFlash.Level(k);   // k == SMover/SPoner/SCerrar (0/1/2)
                if (lvl <= 0.02f) return false;
                cx = hgCx[k]; cy = hgCy[k]; r = hgR[k];
                Color c = hintFlash.Col(k);
                argb = Color.FromArgb((int)(lvl * 215f), c.R, c.G, c.B).ToArgb();   // alfa = brillo del destello; rgb = su color
                return true;
            }
            // Recursos de pintado CACHEADOS: el buscador creaba ~90 objetos GDI por cuadro (fuentes,
            // pinceles y plumas, incluso por cada tecla y resultado) a 24fps. Ahora todo es reusable.
            private Font fQuery, fHint, fToast, fHead, fEmpty, fRes, fResSub, fPrevT, fPrevS, fPrevH;
            private Font fChar, fSp; private float fKh = -1f;   // fuentes del teclado (dependen de su altura)
            private SolidBrush sbS; private Pen pnS;            // pincel/pluma reusables (se les muta color/grosor)
            // CACHE del teclado SIN seleccion: las ~40 teclas (con degradado, contorno y texto) son carisimas
            // de redibujar cada vez (RoundedPath + LinearGradientBrush por tecla). Como el teclado es estatico,
            // se dibuja UNA vez a un bitmap y luego solo se copia + se pinta EN VIVO la tecla seleccionada.
            private Bitmap kbCache; private int kbCacheW = -1, kbCacheH = -1, kbCacheAccent; private float kbCacheKh = -1f;
            private Point kbCacheOrigin;
            // CACHE de la lista de resultados (mismo motivo que el teclado): ~40 tarjetas con degradado, contorno
            // y 2 textos cada una. Los resultados dependen SOLO de la consulta, asi que se cachean por Query y
            // solo se pinta EN VIVO la tarjeta seleccionada. Moverse por teclas/resultados deja de dar tirones.
            private Bitmap resCache; private string resCacheQuery = "\0"; private int resCacheW = -1, resCacheH = -1, resCacheAccent, resCacheDatos = -1, resCacheCola = -1;
            // COMO ESTABA MARCADA CADA TARJETA cuando se horneo el cache. Sirve para no rehornear las
            // cincuenta al pedir UNA cancion: ver el retoque de mas abajo.
            private int[] resCacheMarca = new int[0];
            private SolidBrush marcaB;   // el resaltado de lo tecleado (reusable)
            private Point resCacheOrigin;
            // HORNOS DE LA TARJETA DE RESULTADO. Todas las tarjetas miden lo mismo, asi que su fondo (rectangulo
            // redondeado relleno + borde, con antialias) se dibuja UNA vez a un bitmap y luego cada tarjeta lo
            // copia. Igual la pastilla del genero, de la que solo hay una docena distintas en toda la rocola.
            // Antes se dibujaban a mano en las CUARENTA tarjetas cada vez que el cliente tecleaba una letra.
            private Bitmap bmpTarj, bmpTarjSel;
            private int hornoTarjW = -1, hornoTarjH = -1, hornoTarjAc;
            private readonly Dictionary<string, Bitmap> hornoGen = new Dictionary<string, Bitmap>();
            private void EnsureHornoTarjeta(int w, int h)
            {
                int ac = Accent.ToArgb();
                if (bmpTarj != null && hornoTarjW == w && hornoTarjH == h && hornoTarjAc == ac) return;
                if (bmpTarj != null) bmpTarj.Dispose();
                if (bmpTarjSel != null) bmpTarjSel.Dispose();
                bmpTarj = HorneaTarjeta(w, h, false);
                bmpTarjSel = HorneaTarjeta(w, h, true);
                hornoTarjW = w; hornoTarjH = h; hornoTarjAc = ac;
            }
            // El borde se traza CENTRADO en el contorno de la tarjeta: la mitad de fuera cae sobre el fondo.
            // Por eso el bitmap lleva un margen de MargenTarj a cada lado y se pega desplazado: asi lo que
            // sale en pantalla es identico al pixel a dibujarla a mano, que es lo que se hacia antes.
            private const int MargenTarj = 3;
            private Bitmap HorneaTarjeta(int w, int h, bool sel)
            {
                if (w < 2) w = 2; if (h < 2) h = 2;
                Bitmap b = new Bitmap(w + 2 * MargenTarj, h + 2 * MargenTarj, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath gp = RoundedPath(new Rectangle(MargenTarj, MargenTarj, w, h), 8))
                    {
                        using (SolidBrush sb = new SolidBrush(sel ? Shade(Accent, 0.26) : Shade(Accent, 0.50))) g.FillPath(sb, gp);
                        using (Pen pn = new Pen(Color.FromArgb(sel ? 215 : 100, 160, 200, 245), sel ? 2f : 1.1f)) g.DrawPath(pn, gp);
                    }
                }
                return b;
            }
            // LA PASTILLA DE UNA MARCA ("EN COLA 3", "SUENA AHORA"), horneada igual que la del genero y
            // guardada por su texto: en una pantalla de cuarenta resultados se repiten mucho, y hornear un
            // fondo redondeado cuarenta veces por tecla se nota.
            private readonly Dictionary<string, Bitmap> hornoMarca = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
            private Bitmap HorneaMarcaRes(Graphics medidor, string texto, Color c)
            {
                Bitmap b;
                if (hornoMarca.TryGetValue(texto, out b)) return b;
                Size gs = TextRenderer.MeasureText(medidor, texto, fResSub, new Size(9999, 99), TextFormatFlags.NoPadding);
                int w = gs.Width + 14, h = 15;
                if (w < 8) w = 8;
                b = new Bitmap(w + 2 * MargenTarj, h + 2 * MargenTarj, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath gp = RoundedPath(new Rectangle(MargenTarj, MargenTarj, w, h), 7))
                    {
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, c))) g.FillPath(sb, gp);
                        using (Pen pn = new Pen(Color.FromArgb(170, c), 1f)) g.DrawPath(pn, gp);
                    }
                }
                hornoMarca[texto] = b;
                return b;
            }

            private Bitmap HorneaGeneroRes(Graphics medidor, string etiqueta)
            {
                Bitmap b;   // el color sale del GENERO, no del color de la seccion: no caduca nunca
                if (hornoGen.TryGetValue(etiqueta, out b)) return b;
                Color c = ColorDeEtiqueta(etiqueta);
                Size gs = TextRenderer.MeasureText(medidor, etiqueta, fResSub, new Size(9999, 99), TextFormatFlags.NoPadding);
                int w = gs.Width + 14, h = 15;
                if (w < 8) w = 8;
                b = new Bitmap(w + 2 * MargenTarj, h + 2 * MargenTarj, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath gp = RoundedPath(new Rectangle(MargenTarj, MargenTarj, w, h), 7))
                    {
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(60, c))) g.FillPath(sb, gp);
                        using (Pen pn = new Pen(Color.FromArgb(120, c), 1f)) g.DrawPath(pn, gp);
                    }
                }
                hornoGen[etiqueta] = b;
                return b;
            }
            // Los tres rotulos posibles (ARTISTA/ALBUM/CANCION) miden siempre lo mismo: se mide una vez.
            private readonly int[] anchoRotulo = { -1, -1, -1 };
            private int altoLetra = -1;   // alto de una letra en la fuente del nombre: es siempre el mismo
            private int AnchoRotulo(Graphics g, int kind, string tag)
            {
                if (kind < 0 || kind > 2) return TextRenderer.MeasureText(g, tag, fResSub, new Size(9999, 99), TextFormatFlags.NoPadding).Width;
                if (anchoRotulo[kind] < 0)
                    anchoRotulo[kind] = TextRenderer.MeasureText(g, tag, fResSub, new Size(9999, 99), TextFormatFlags.NoPadding).Width;
                return anchoRotulo[kind];
            }

            private void EnsurePaintRes()
            {
                if (fQuery != null) return;
                fQuery = new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Bold);
                fHint = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold);
                fToast = new Font(FontFamily.GenericSansSerif, 15.5f, FontStyle.Bold);
                fHead = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold);
                fEmpty = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Italic);
                fRes = new Font(FontFamily.GenericSansSerif, 10.5f, FontStyle.Bold);
                fResSub = new Font(FontFamily.GenericSansSerif, 7.5f, FontStyle.Bold);
                fPrevT = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
                fPrevS = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Regular);
                fPrevH = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
                sbS = new SolidBrush(Color.Black);
                pnS = new Pen(Color.Black, 1f);
            }

            private readonly List<List<SearchKey>> rows = new List<List<SearchKey>>();
            private readonly List<KeyValuePair<RectangleF, int>> resultRects = new List<KeyValuePair<RectangleF, int>>();

            public SearchOverlay()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
                this.TabStop = true;
                this.BackColor = Color.FromArgb(8, 12, 36);
                BuildKeys();
            }

            private void BuildKeys()
            {
                AddRow("1234567890");
                AddRow("QWERTYUIOP");
                AddRow("ASDFGHJKL\u00D1");
                AddRow("ZXCVBNM");
                List<SearchKey> sp = new List<SearchKey>();
                sp.Add(new SearchKey { Kind = KKind.Space, Label = "ESPACIO" });
                sp.Add(new SearchKey { Kind = KKind.Back, Label = "BORRAR" });
                rows.Add(sp);
            }

            private void AddRow(string s)
            {
                List<SearchKey> row = new List<SearchKey>();
                for (int i = 0; i < s.Length; i++)
                    row.Add(new SearchKey { Kind = KKind.Char, Ch = s[i], Label = s[i].ToString() });
                rows.Add(row);
            }

            // REPINTADO PARCIAL. El buscador se repintaba ENTERO veinticuatro veces por segundo: a pantalla
            // completa eso es rehacer el fondo estrellado -que late con la musica- mas dos copias grandes (el
            // teclado y las tarjetas) en CADA cuadro. Casi nada de eso cambia de un cuadro al siguiente: lo unico
            // que se mueve solo es el cubo que gira y el destello del boton recien pulsado. Esto repinta SOLO eso;
            // el cuadro completo se pide una de cada tres veces. Al pulsar una tecla se sigue repintando entero,
            // asi que la respuesta al cliente es la misma de siempre.
            public void InvalidarVivo()
            {
                if (ToastOn) { Invalidate(); return; }   // el aviso flotante entra y sale: ahi si toca todo
                int W = Width, H = Height;
                if (W < 60 || H < 60) return;
                int M = 30, kbW = (int)((W - 2 * M) * 0.52);
                int der = Math.Min(W, M + kbW + 10);   // sin pasar a la mitad derecha: alli las tarjetas estan quietas
                // Se marcan varios trozos SUELTOS, no uno grande que los englobe: Windows suma zonas, asi que
                // aunque la tecla elegida este abajo del todo y las ayudas arriba, lo que se repinta de verdad
                // son esos dos trocitos y no la franja entera que va de uno a otro.
                // 1) la franja de las ayudas, con margen de sobra: el halo del destello se sale de la linea del texto
                Invalidate(new Rectangle(0, Math.Max(0, M + 46), der, 112));
                // 2) el cubo que gira, con su texto debajo
                if (cubeRect.Width > 1 && cubeRect.Height > 1) Invalidate(Rectangle.Inflate(cubeRect, 26, 84));
                // 3) con el teclado a la vista, la tecla ELEGIDA respira (su borde late al ritmo de la fase)
                else if (SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)
                {
                    Rectangle kr = Rectangle.Round(rows[SelRow][SelCol].R);
                    kr.Inflate(10, 10);
                    Invalidate(kr);
                }
            }

            // LO TECLEADO Y LO QUE MANDA EN LOS RESULTADOS SON DOS COSAS.
            //
            // Antes eran la misma, y por eso cada letra rehacia y subia la textura de pantalla completa:
            // 2.80 ms por pulsacion a 1920 -7.3 MB- mas 3.07 ms de rehornear las 40 tarjetas. Quien teclea
            // suelta cinco o diez letras por segundo, asi que eso pasaba diez veces en un segundo, en la
            // escena que el medidor señala como la peor.
            //
            // Mientras las letras estan entrando NADIE esta leyendo los resultados; lo que si tiene que
            // verse al instante es lo que se escribe. Asi que 'Escrito' se apunta letra a letra y va en su
            // propia imagen chica (ver PiezaEscrito), y 'Query' -la que manda en los resultados y en la
            // firma de la pantalla completa- se POSA cuando la persona para de teclear. Las consultas
            // intermedias, que nadie llega a leer, ni se buscan ni se hornean.
            public string Escrito = "";
            public void SetQuery(string q) { Query = q; Escrito = q; FocusResults = false; SelResult = 0; Invalidate(); }
            public void TypeChar(char c)
            {
#if ROCOLA_GPU
                if (Nuevo) MarcaTecla(c);   // su tecla se enciende un instante (BuscadorPortadasGpu.cs)
#endif
                Escrito += c; Tecleo();
            }
            public void Backspace()
            {
#if ROCOLA_GPU
                if (Nuevo) MarcaTecla('\b');
                if (Nuevo && ModoAtajo != null && Escrito.Length == 0) { SalDeAtajo(); return; }   // borrar sin nada escrito: se sale del atajo
#endif
                if (Escrito.Length > 0) Escrito = Escrito.Substring(0, Escrito.Length - 1);
                Tecleo();
            }

            // Una letra entra: se ve al momento, y nada mas. La caja de lo escrito es una imagen chica
            // aparte, asi que repintarla no arrastra ni el cielo ni el teclado.
            public Action OnTecleo;
            private void Tecleo()
            {
                if (OnTecleo != null) OnTecleo();
                Rectangle arriba, derecha;
                if (ToastOn || !ZonasAlEscribir(out arriba, out derecha)) Invalidate(); else Invalidate(arriba);
            }

            // Y aqui es donde los resultados se enteran. Lo llama el formulario cuando la persona para.
            public void PosaConsulta()
            {
                if (Query == Escrito) return;   // ya estaban enseñando esto: no hay nada que rehacer
                Query = Escrito;
                Changed();
            }
            // AL TECLEAR NO CAMBIA MEDIA PANTALLA. Escribir una letra toca la caja de lo escrito y las
            // tarjetas de la derecha, y nada mas: el teclado se queda exactamente igual, tecla por tecla.
            // Marcando solo esas dos zonas, lo que hay que volver a copiar debajo -el cielo- se reduce a la
            // mitad, y el teclado horneado no se copia en absoluto. A 1920x1080 eso es lo que se notaba al
            // escribir rapido.
            //
            // La UNICA vez que el lado del teclado si cambia es cuando se venia de estar en los resultados:
            // alli habia un cubo girando en su sitio y ahora vuelven las teclas. Ese caso pide todo.
            // Los resultados pasan a otra consulta. Cuesta: rehornear las 40 tarjetas son 3.07 ms. Por eso
            // esto ya NO se llama en cada letra, sino cuando la consulta se posa.
            private void Changed()
            {
#if ROCOLA_GPU
                if (Nuevo) AlCambiarConsulta();
#endif
                bool veniaDeResultados = FocusResults;
                FocusResults = false; SelResult = 0;
                if (OnQueryChanged != null) OnQueryChanged(Query);
                Rectangle arriba, derecha;
                if (veniaDeResultados || ToastOn || !ZonasAlEscribir(out arriba, out derecha)) { Invalidate(); return; }
                // Dos trozos SUELTOS, no uno que los englobe: Windows suma zonas y repinta solo estas dos.
                Invalidate(arriba); Invalidate(derecha);
            }

            // LAS DOS ZONAS QUE CAMBIAN AL ESCRIBIR UNA LETRA: la caja de lo escrito con su barra de ayudas,
            // y la columna de resultados. En medio queda el teclado, que no cambia.
            // Va en un metodo aparte para que la prueba pueda pedir ESTAS zonas y no una copia de la cuenta:
            // si fueran dos copias, cambiar una y olvidar la otra dejaria medio buscador congelado sin que la
            // prueba se enterara, que es exactamente el fallo que esta vigilando.
            public bool ZonasAlEscribir(out Rectangle arriba, out Rectangle derecha)
            {
                arriba = Rectangle.Empty; derecha = Rectangle.Empty;
                int W = Width, H = Height;
                if (W < 60 || H < 60) return false;
                int M = 30, kbW = (int)((W - 2 * M) * 0.52);
                arriba = new Rectangle(0, 0, W, M + 70 + 28);
                int rx = Math.Max(0, M + kbW + 10);
                derecha = new Rectangle(rx, 0, Math.Max(1, W - rx), H);
                return true;
            }

            public void MoveSel(int dx, int dy)
            {
#if ROCOLA_GPU
                if (Nuevo) { MoveSelNuevo(dx, dy); return; }   // con atajos y tarjetas en dos columnas (BuscadorPortadasGpu.cs)
#endif
                if (FocusResults)
                {
                    int cap = Math.Max(1, resCols * resRowsPerCol);
                    int n = Math.Min((Results != null) ? Results.Count : 0, cap);
                    if (n == 0) { if (dx < 0) FocusResults = false; Invalidate(); return; }
                    if (SelResult < 0) SelResult = 0;
                    if (SelResult >= n) SelResult = n - 1;
                    int rpc = (resRowsPerCol > 0) ? resRowsPerCol : 1;
                    int col = SelResult / rpc, row = SelResult % rpc;
                    if (dx < 0 && col == 0) { FocusResults = false; Invalidate(); return; }   // izquierda en 1a columna -> teclado
                    if (dy != 0)
                    {
                        int nr = row + dy; if (nr < 0) nr = 0; if (nr >= rpc) nr = rpc - 1;
                        int ni = col * rpc + nr; if (ni < n) SelResult = ni;
                    }
                    if (dx != 0)
                    {
                        int nc = col + dx; if (nc < 0) nc = 0;
                        int ni = nc * rpc + row; if (ni >= n) ni = n - 1;
                        if (ni >= 0) SelResult = ni;
                    }
                    Invalidate();
                    return;
                }
                if (rows.Count == 0) return;
                if (dy != 0)
                {
                    SelRow += dy;
                    if (SelRow < 0) SelRow = 0;
                    if (SelRow >= rows.Count) SelRow = rows.Count - 1;
                    if (SelCol >= rows[SelRow].Count) SelCol = rows[SelRow].Count - 1;
                }
                if (dx != 0)
                {
                    if (dx > 0 && SelCol >= rows[SelRow].Count - 1 && Results != null && Results.Count > 0)
                    {
                        FocusResults = true; SelResult = 0; Invalidate(); return;
                    }
                    SelCol += dx;
                    if (SelCol < 0) SelCol = 0;
                    if (SelCol >= rows[SelRow].Count) SelCol = rows[SelRow].Count - 1;
                }
                Invalidate();
            }

            public void PressSelected()
            {
#if ROCOLA_GPU
                if (Nuevo) { PressNuevo(); return; }
#endif
                if (FocusResults)
                {
                    if (Results != null && SelResult >= 0 && SelResult < Results.Count && OnChoose != null) OnChoose(Results[SelResult]);
                    return;
                }
                if (SelRow < 0 || SelRow >= rows.Count) return;
                if (SelCol < 0 || SelCol >= rows[SelRow].Count) return;
                Activate(rows[SelRow][SelCol]);
            }

            private void Activate(SearchKey k)
            {
                if (k.Kind == KKind.Char) TypeChar(k.Ch);
                else if (k.Kind == KKind.Space) TypeChar(' ');
                else if (k.Kind == KKind.Back) Backspace();
                else if (k.Kind == KKind.Choose) { if (Results != null && Results.Count > 0 && OnChoose != null) OnChoose(Results[0]); }
            }

            public void ClickAt(Point p)
            {
                OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0));
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
#if ROCOLA_GPU
                if (Nuevo) { ClickNuevo(e.Location); base.OnMouseClick(e); return; }
#endif
                if (!FocusResults)
                {
                    for (int r = 0; r < rows.Count; r++)
                        for (int c = 0; c < rows[r].Count; c++)
                            if (rows[r][c].R.Contains(e.X, e.Y)) { SelRow = r; SelCol = c; Activate(rows[r][c]); Invalidate(); base.OnMouseClick(e); return; }
                }
                else if (cubeRect != Rectangle.Empty && cubeRect.Contains(e.X, e.Y))
                {
                    FocusResults = false; Invalidate(); base.OnMouseClick(e); return;
                }
                for (int i = 0; i < resultRects.Count; i++)
                    if (resultRects[i].Key.Contains(e.X, e.Y))
                    {
                        int idx = resultRects[i].Value;
                        if (Results != null && idx >= 0 && idx < Results.Count && OnChoose != null) OnChoose(Results[idx]);
                        base.OnMouseClick(e); return;
                    }
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics, e.ClipRectangle, false); }

            // Dibuja el buscador en cualquier Graphics. forGpu=true: NO pinta el starfield ni el cubo de vista
            // previa (los pone la GPU: starfield de fondo + cubo como geometria); aqui solo la UI (transparente).
            // LA PIEZA QUE CAMBIA AL MOVERSE: la tecla elegida.
            //
            // Se dibuja EN VIVO encima del teclado cacheado, y eso ya estaba bien. El problema era que iba
            // DENTRO de la imagen de pantalla completa, y la tecla elegida entraba en la firma de esa imagen:
            // pulsar una flecha obligaba a REHACERLA Y RE-SUBIRLA entera a la tarjeta grafica. Medido: 5.91 ms
            // por pulsacion a 1080p, el 35% de un cuadro; y como las flechas no frenaban la auto-repeticion,
            // mantener una pulsada eran ~30 de esas por segundo. ESO era lo que hundia los cuadros por segundo
            // al teclear, hasta en una maquina potente: el gasto no es pintar -de eso la tarjeta va sobrada-
            // sino rehacer la imagen en el procesador y empujar siete megas por el bus.
            //
            // Ahora sale APARTE, en su propia imagen chica, igual que la franja de estado de abajo (ver
            // EnsureUiBotOverlay). Moverse por el teclado ya no toca la imagen grande.
            //
            // SOLO LA TECLA. La tarjeta elegida de los resultados se probo tambien y se quito: moverse por
            // los resultados cambia ademas el cubo de la portada y el texto de debajo, que van dentro de la
            // imagen grande, asi que esa mitad hay que rehacerla igual.
            //
            // Que sale IGUAL esta comprobado pixel a pixel, y no es suerte: la pieza se rellena OPACA, asi que
            // tapa entero su hueco y no hay borde suavizado que se mezcle dos veces (ver pruebas/mover.cs).
            public const int PiezaTecla = 0;
            // Y la caja de lo escrito, que es la que cambia en CADA letra. Antes iba dentro de la imagen de
            // pantalla completa y por eso teclear costaba 2.80 ms por pulsacion a 1920 (7.3 MB); aqui es una
            // tira de unos cientos de KB.
            public const int PiezaEscrito = 1;
            private bool paraGpu;   // dibujando para la imagen de pantalla completa: la tecla elegida va aparte

            // Donde va la pieza, en coords del buscador. false = ahora no hay ninguna que dibujar.
            //
            // Con los RESULTADOS enfocados no hay: ahi el teclado ni se dibuja -en su hueco va el cubo de la
            // portada- y sus teclas ni siquiera tienen sitio asignado. Devolver una igual dejaba una tecla
            // suelta flotando encima del cubo, que es justo lo que se veia en el bar.
            public bool PiezaViva(int cual, out Rectangle donde)
            {
                donde = Rectangle.Empty;
#if ROCOLA_GPU
                if (Nuevo) return PiezaVivaNueva(cual, out donde);   // el buscador con portadas (BuscadorPortadasGpu.cs)
                if (cual >= 2) return ExtraGpuRect(cual, out donde);
#endif
                if (cual == PiezaEscrito)
                {
                    // La caja de lo escrito se ve SIEMPRE que el buscador esta abierto, tambien con el foco
                    // en los resultados: es donde se lee lo que se busco.
                    Rectangle cj = CajaEscrito();
                    if (cj.Width < 8 || cj.Height < 8) return false;
                    donde = Rectangle.Inflate(cj, 2, 2);
                    return true;
                }
                if (cual != PiezaTecla || FocusResults) return false;
                if (SelRow < 0 || SelRow >= rows.Count || SelCol < 0 || SelCol >= rows[SelRow].Count) return false;
                Rectangle kr = Rectangle.Round(rows[SelRow][SelCol].R);
                if (kr.Width < 2 || kr.Height < 2) return false;   // el teclado aun no se ha repartido
                donde = Rectangle.Inflate(kr, 2, 2);
                return true;
            }

            // Y la dibuja. El desplazamiento va POR PARAMETRO y no moviendo el lienzo: el texto lo pinta el
            // sistema (TextRenderer) en pixeles de verdad y no hace caso a las trasformaciones, asi que
            // moviendo el lienzo la letra se iba fuera de la imagen chica (ver EnsureUiPieza).
            public void DibujaPiezaViva(Graphics g, int cual, int dx, int dy)
            {
#if ROCOLA_GPU
                if (Nuevo) { DibujaPiezaVivaNueva(g, cual, dx, dy); return; }
                if (cual >= 2) { DrawExtraGpu(g, cual, dx, dy); return; }
#endif
                if (cual == PiezaEscrito)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    DibujaCajaEscrito(g, dx, dy, true);
                    return;
                }
                if (cual != PiezaTecla || FocusResults) return;
                if (SelRow < 0 || SelRow >= rows.Count || SelCol < 0 || SelCol >= rows[SelRow].Count) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawOneKey(g, SelRow, SelCol, true, dx, dy);
            }

            // LA CAJA DONDE SE ESCRIBE. Su sitio lo saben los dos caminos: el dibujado normal y la imagen
            // chica que la lleva cuando hay tarjeta grafica.
            public Rectangle CajaEscrito() { int M = 30; return new Rectangle(M, M, Width - 2 * M, 70); }
            // DONDE VA EL TECLADO -y, cuando hay resultados, la vista previa que ocupa su sitio-. Esta
            // cuenta la necesitan dos: el pintado de aqui y el trozo que dibuja la tarjeta grafica
            // aparte, que vive en AcabadoGpu.cs. Alli estaba copiada a mano, con el 128 escrito tal cual
            // en vez de M+70+28. Ahora hay una sola y los dos la piden, que es lo que impide que se
            // separen sin que nadie se entere.
            internal Rectangle RegionTeclado()
            {
                int M = 30, contentY = M + 70 + 28, contentH = Height - contentY - M;
                return new Rectangle(M, contentY, (int)((Width - 2 * M) * 0.52), contentH);
            }
            // Y donde va la portada grande dentro de esa region. Lo mismo: lo usan ComputePreview y el
            // trozo de la tarjeta.
            internal static Rectangle CajaPortada(Rectangle region)
            {
                int side = (int)(Math.Min(region.Width, region.Height) * 0.62);
                if (side < 40) side = 40;
                int cx = region.X + region.Width / 2;
                int top = region.Y + (region.Height - side) / 2 - 24;
                if (top < region.Y + 6) top = region.Y + 6;
                return new Rectangle(cx - side / 2, top, side, side);
            }

            // Y como se dibuja. El desplazamiento va POR PARAMETRO y no moviendo el lienzo, por lo mismo que
            // en DibujaPiezaViva: el texto lo pinta el sistema en pixeles de verdad y no hace caso a las
            // trasformaciones.
            private void DibujaCajaEscrito(Graphics g, int dx, int dy, bool sinParpadeo)
            {
                Rectangle qb = CajaEscrito(); qb.Offset(dx, dy);
                EnsurePaintRes();
                using (GraphicsPath gp = RoundedPath(qb, 16))
                {
                    sbS.Color = Shade(Accent, 0.42); g.FillPath(sbS, gp);
                    pnS.Color = Lighten(Accent, 0.35); pnS.Width = 2f; g.DrawPath(pnS, gp);
                }
                // Con tarjeta grafica el cursor NO parpadea (fijo): parpadear obligaria a rehacer esta imagen
                // una vez por segundo aunque nadie estuviera escribiendo.
                bool caretOn = sinParpadeo || Math.Sin(Phase * 3.0) > 0;
                string shown = "Buscar:  " + Escrito + (caretOn ? "|" : "");
                TextRenderer.DrawText(g, shown, fQuery, new Rectangle(qb.X + 18, qb.Y, qb.Width - 30, qb.Height), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            public void RenderContent(Graphics g, Rectangle clip, bool forGpu)
            {
                int W = Width, H = Height;
                if (W < 60 || H < 60) return;
                paraGpu = forGpu;
#if ROCOLA_GPU
                if (forGpu && Nuevo) { RenderNuevo(g); return; }   // el buscador con portadas (BuscadorPortadasGpu.cs)
#endif
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (forGpu) clip = new Rectangle(0, 0, W, H);

                if (!forGpu)
                {
                    // EL CIELO, POR EL CAMINO RAPIDO. El buscador era el unico sitio que lo redibujaba entero
                    // -las seiscientas estrellas, una a una, con su halo- en cada repintado. A 1920x1080 eso
                    // costaba 8.2 ms POR CUADRO, y como al teclear se repinta todo, cada letra costaba 20 ms:
                    // de ahi el bajon de imagenes por segundo al escribir, peor cuanto mas grande la pantalla.
                    // Ahora hace lo mismo que la rejilla de discos: copia el cielo ya horneado y las estrellas
                    // ya horneadas (dos copias), y encima le pone EN VIVO solo lo que de verdad se mueve, que
                    // es el latido de las medianas/grandes y las fugaces. Se ve igual y cuesta una decima parte.
                    if (Sky != null)
                    {
                        Sky.SkyOffsetX = this.Left; Sky.SkyOffsetY = this.Top;
                        Sky.SkyFullW = (this.Parent != null) ? this.Parent.ClientSize.Width : W;
                        Sky.SkyFullH = (this.Parent != null) ? this.Parent.ClientSize.Height : H;
                        Sky.SkyMid = SkyColorMid(Accent); Sky.Acento = Accent;
                        Sky.Dim = BgDim;   // atenuado mientras se navega con musica
                        Sky.DrawReactive(g, W, H, SkyColor(Accent), SkyColor2(Accent), PulseLo, PulseMid, PulseHi);
                    }
                }

                int M = 30;   // el mismo margen que usa CajaEscrito: de aqui cuelga todo el reparto de abajo
                Rectangle qb = CajaEscrito();
                Rectangle hintRect = new Rectangle(qb.X, qb.Bottom + 2, qb.Width, 26);
                if (qb.IntersectsWith(clip) || hintRect.IntersectsWith(clip))
                {
                    EnsurePaintRes();
                    // CON TARJETA GRAFICA la caja de lo escrito NO se dibuja aqui: va en su propia imagen
                    // chica (PiezaEscrito), que es lo que hace que teclear no rehaga los siete megas de la
                    // pantalla completa en cada letra. Sin tarjeta se dibuja aqui, como siempre.
                    if (!forGpu) DibujaCajaEscrito(g, 0, 0, false);
                    {
                        Font hf = fHint;
                        int hcy = hintRect.Y + hintRect.Height / 2;
                        Color htx = Color.FromArgb(214, 222, 242);
                        int hd = 20; float hgap = 7, hgrp = 20;   // MISMO tamano que la barra principal
                        float hx = hintRect.X;
                        // Guarda el centro/radio del halo (para dibujarlo por GPU). En GPU NO se hornea aqui (forGpu);
                        // en CPU si se pinta como siempre. Asi el destello se anima por GPU sin re-subir la textura.
                        StoreHintGlow(SMover, hx, hcy, hd + 2);
                        if (!forGpu) GlowAround(g, hx, hcy, hd + 2, hintFlash.Level(SMover), hintFlash.Col(SMover));   // MOVER = cruceta (color segun direccion)
                        hx = CtrlPad(g, hx, hcy, hd + 2);
                        hx = CtrlTxt(g, hf, "Mover", hx + hgap, hcy, htx) + hgrp;
                        StoreHintGlow(SPoner, hx, hcy, hd);
                        if (!forGpu) GlowAround(g, hx, hcy, hd, hintFlash.Level(SPoner), hintFlash.Col(SPoner));        // PONER/ELEGIR = punto verde
                        hx = CtrlDot(g, hx, hcy, hd, SGreen);
                        hx = CtrlTxt(g, hf, "Poner / Elegir", hx + hgap, hcy, htx) + hgrp;
                        // CERRAR = los MISMOS dos botones con los que se abrio (rojo + arriba). Antes era
                        // rojo + verde, que ademas chocaba con el verde de "poner": el cliente que queria
                        // salir apretaba el verde y ponia una cancion sin querer.
                        StoreHintGlow(SCerrar, hx, hcy, hd * 2 + 3);
                        if (!forGpu) GlowAround(g, hx, hcy, hd * 2 + 3, hintFlash.Level(SCerrar), hintFlash.Col(SCerrar));   // CERRAR = rojo + arriba
                        hx = CtrlDot(g, hx, hcy, hd, SRed) + 3;
                        hx = CtrlTxt(g, hf, "+", hx, hcy, htx) + 3;
                        hx = FlechaArriba(g, hx, hcy, hd, SRed);
                        hx = CtrlTxt(g, hf, "Cerrar", hx + hgap, hcy, htx);
                    }
                }

                Rectangle kbRegion = RegionTeclado();   // teclado mas angosto -> resultados mas anchos (caben 2 columnas)
                int contentY = kbRegion.Y, contentH = kbRegion.Height, kbW = kbRegion.Width;
                if (contentH < 60) return;
                Rectangle resRegion = new Rectangle(M + kbW + 20, contentY, (W - 2 * M) - kbW - 20, contentH);

                bool showCube = FocusResults && Results != null && Results.Count > 0 && GetCover != null;
                if (showCube)
                {
                    if (forGpu)
                    {
                        Image ca, cb; float f; Rectangle area;
                        if (!SeparateGpu && ComputePreview(kbRegion, out ca, out cb, out f, out area)) DrawPreviewInfo(g, kbRegion, area);
                    }
                    else if (kbRegion.IntersectsWith(clip)) DrawPreviewCube(g, kbRegion);
                }
                else
                {
                    cubeRect = Rectangle.Empty;
                    LayoutKeys(kbRegion);
                    if (kbRegion.IntersectsWith(clip)) DrawKeys(g, kbRegion);
                }
                if (resRegion.IntersectsWith(clip)) DrawResults(g, resRegion);

                if (ToastOn) DrawToast(g, W, H);
            }

            // Lo que ocupa el aviso flotante de DrawToast, con su contorno y su suavizado: lo unico de la pantalla que cambia
            // cuando el aviso entra o sale. Es la misma cuenta que DrawToast; la bateria 'buscaligero' compara la pantalla
            // entera antes y despues del aviso, asi que si las dos cuentas se separan, se entera.
            internal Rectangle ZonaAviso()
            {
                int tw = 400, th = 56;
                Rectangle r = new Rectangle((Width - tw) / 2, 104, tw, th);
                r.Inflate(4, 4);
                return r;
            }

            private void DrawToast(Graphics g, int W, int H)
            {
                int tw = 400, th = 56;
                Rectangle r = new Rectangle((W - tw) / 2, 104, tw, th);
                EnsurePaintRes();
                using (GraphicsPath gp = RoundedPath(r, 16))
                {
                    sbS.Color = ToastBack; g.FillPath(sbS, gp);
                    pnS.Color = Lighten(ToastBack, 0.45); pnS.Width = 2.4f; g.DrawPath(pnS, gp);
                }
                TextRenderer.DrawText(g, ToastMsg, fToast, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            private void LayoutKeys(Rectangle region)
            {
                int rowsN = rows.Count;
                if (rowsN == 0) return;
                float gap = 6;
                float rowH = (region.Height - gap * (rowsN - 1)) / rowsN;
                int cols = 10;
                float keyW = (region.Width - gap * (cols - 1)) / cols;
                for (int r = 0; r < rows.Count; r++)
                {
                    float y = region.Y + r * (rowH + gap);
                    if (r < 4)
                    {
                        int c = rows[r].Count;
                        float totalW = c * keyW + gap * (c - 1);
                        float x0 = region.X + (region.Width - totalW) / 2f;
                        for (int c2 = 0; c2 < c; c2++)
                            rows[r][c2].R = new RectangleF(x0 + c2 * (keyW + gap), y, keyW, rowH);
                    }
                    else
                    {
                        int n = rows[r].Count;
                        float[] frac;
                        if (n == 2) frac = new float[] { 0.60f, 0.40f };          // ESPACIO mas ancho, BORRAR
                        else if (n == 3) frac = new float[] { 0.40f, 0.28f, 0.32f };
                        else { frac = new float[n]; for (int q = 0; q < n; q++) frac[q] = 1f / n; }
                        float avail = region.Width - gap * (n - 1);
                        float x = region.X;
                        for (int c2 = 0; c2 < n; c2++)
                        {
                            float fw = avail * frac[c2];
                            rows[r][c2].R = new RectangleF(x, y, fw, rowH);
                            x += fw + gap;
                        }
                    }
                }
            }

            private void DrawKeys(Graphics g, Rectangle region)
            {
                if (rows.Count == 0 || rows[0].Count == 0) return;
                float kh = rows[0][0].R.Height;
                if (kh < 8) kh = 8;
                EnsurePaintRes();
                if (fChar == null || fKh != kh)   // fuentes del teclado: solo se recrean si cambia su altura
                {
                    if (fChar != null) fChar.Dispose();
                    if (fSp != null) fSp.Dispose();
                    fChar = new Font(FontFamily.GenericSansSerif, Math.Max(12f, kh * 0.42f), FontStyle.Bold);
                    fSp = new Font(FontFamily.GenericSansSerif, Math.Max(9f, kh * 0.24f), FontStyle.Bold);
                    fKh = kh;
                }
                if (region.Width < 2 || region.Height < 2) return;
                // CACHE de las teclas SIN seleccion: se rehace solo si cambia tamano/altura/color (casi nunca).
                // Dibujar 40 teclas (degradado + contorno + texto) en cada cuadro era lo que hundia el FPS del
                // buscador; ahora se copia el cache + SOLO la tecla seleccionada en vivo (barato).
                int accentArgb = Accent.ToArgb();
                if (kbCache == null || kbCacheW != region.Width || kbCacheH != region.Height || kbCacheAccent != accentArgb || kbCacheKh != kh)
                {
                    if (kbCache == null || kbCache.Width != region.Width || kbCache.Height != region.Height)   // REUSA si el tamano no cambio (p.ej. solo cambio el color)
                    {
                        if (kbCache != null) kbCache.Dispose();
                        kbCache = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppPArgb);
                    }
                    using (Graphics cg = Graphics.FromImage(kbCache))
                    {
                        cg.Clear(Color.Transparent);
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        for (int r = 0; r < rows.Count; r++)
                            for (int c = 0; c < rows[r].Count; c++)
                                DrawOneKey(cg, r, c, false, -region.X, -region.Y);   // TODAS sin seleccion, en coords del bitmap
                    }
                    kbCacheW = region.Width; kbCacheH = region.Height; kbCacheAccent = accentArgb; kbCacheKh = kh; kbCacheOrigin = region.Location;
                }
                g.DrawImageUnscaled(kbCache, kbCacheOrigin.X, kbCacheOrigin.Y);
                // Tecla seleccionada EN VIVO encima: su relleno es opaco y tapa la version sin seleccion del cache.
                // Con tarjeta grafica NO se dibuja aqui: sale como PIEZA VIVA aparte (ver PiezaViva).
                if (!paraGpu && SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)
                    DrawOneKey(g, SelRow, SelCol, true, 0, 0);
            }

            // Dibuja UNA tecla. dx/dy desplaza el rect (para el cache: coords relativas al bitmap). Se usa
            // Offset directo -en vez de TranslateTransform del Graphics- porque TextRenderer (GDI) IGNORA la
            // transformacion y el texto caeria fuera del cache.
            private void DrawOneKey(Graphics g, int r, int c, bool sel, int dx, int dy)
            {
                SearchKey k = rows[r][c];
                Rectangle kr = Rectangle.Round(k.R); kr.Offset(dx, dy);
                if (kr.Width < 2 || kr.Height < 2) return;
                double kp = 0.5 + 0.5 * Math.Sin(Phase * 2.2 - (r * 0.55 + c * 0.32));
                using (GraphicsPath gp = RoundedPath(kr, 10))
                {
                    Color top = sel ? Lighten(Accent, 0.32) : Lighten(Shade(Accent, 0.34), 0.10 + 0.14 * kp);
                    Color bot = sel ? Accent : Shade(Accent, 0.55);
                    using (LinearGradientBrush b = new LinearGradientBrush(kr, top, bot, LinearGradientMode.Vertical)) g.FillPath(b, gp);
                    if (sel)
                    {
                        int a = 130 + (int)((0.5 + 0.5 * Math.Sin(Phase * 2.6)) * 120);
                        pnS.Color = Color.FromArgb(a, 170, 215, 255); pnS.Width = 2.8f; g.DrawPath(pnS, gp);
                    }
                    else
                    {
                        int a = 85 + (int)(kp * 130);
                        pnS.Color = Color.FromArgb(a, 150, 200, 255); pnS.Width = 1.7f; g.DrawPath(pnS, gp);
                    }
                }
                Font use = (k.Kind == KKind.Char) ? fChar : fSp;
                // EN LA TECLA DE BORRAR, EL BOTON ROJO AL LADO. El rojo borra una letra sin tener que ir
                // hasta esta tecla, pero eso no lo sabe nadie: es un atajo invisible. Dibujando el boton
                // DENTRO de la tecla que hace lo mismo, el cliente lo entiende sin que nadie se lo diga, y
                // de paso deja de pasearse por el teclado para borrar una letra mal puesta.
                // El boton va DEBAJO de la palabra, no al lado: "BORRAR" ya llena la tecla de lado a lado, y
                // la tecla es alta de sobra. Asi el boton sale grande -que es lo que hay que ver desde la
                // silla- sin apretujar nada.
                if (k.Kind == KKind.Back && kr.Width > 70 && kr.Height > 56)
                {
                    int d = kr.Height / 4; if (d > 34) d = 34;
                    TextRenderer.DrawText(g, k.Label, use, new Rectangle(kr.X, kr.Y, kr.Width, kr.Height - d - 6),
                                          Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    CtrlDot(g, kr.X + (kr.Width - d) / 2f, kr.Bottom - 8 - d / 2, d, Color.FromArgb(228, 72, 72));
                    return;
                }
                TextRenderer.DrawText(g, k.Label, use, kr, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            private void DrawResults(Graphics g, Rectangle region)
            {
                resultRects.Clear();
                EnsurePaintRes();
                int top = region.Y + 30;
                int areaH = region.Bottom - top;
                int n = 0, cardH = 38;
                if (areaH < 28) { resCols = 1; resRowsPerCol = 1; }
                else
                {
                    int gapY = 5, gapX = 8, colMinW = 185;
                    int cols = (region.Width + gapX) / (colMinW + gapX);
                    if (cols < 1) cols = 1; if (cols > 2) cols = 2;            // hasta 2 columnas en este panel
                    int colW = (region.Width - (cols - 1) * gapX) / cols;
                    int rowsPerCol = (areaH + gapY) / (cardH + gapY);
                    if (rowsPerCol < 1) rowsPerCol = 1;
                    resCols = cols; resRowsPerCol = rowsPerCol;
                    int capacity = cols * rowsPerCol;
                    n = (Results != null) ? Math.Min(Results.Count, capacity) : 0;
                    if (FocusResults && n > 0) { if (SelResult < 0) SelResult = 0; if (SelResult >= n) SelResult = n - 1; }
                    for (int i = 0; i < n; i++)   // solo geometria (barato): rects absolutos para el raton y para pintar
                    {
                        int col = i / rowsPerCol, row = i % rowsPerCol;   // llena hacia ABAJO y luego a la siguiente columna
                        int rx = region.X + col * (colW + gapX), ry = top + row * (cardH + gapY);
                        resultRects.Add(new KeyValuePair<RectangleF, int>(new Rectangle(rx, ry, colW, cardH), i));
                    }
                }
                // CACHE: cabecera + tarjetas SIN seleccion. Se rehace solo al cambiar la consulta/region/color;
                // moverse por los resultados NO lo toca (solo copia el cache + la tarjeta seleccionada en vivo).
                int accentArgb = Accent.ToArgb();
                string qNorm = Normalize(Query ?? "");   // UNA vez por dibujado, no una por tarjeta
                // AL PEDIR UNA CANCION NO SE REHORNEAN LAS CINCUENTA TARJETAS.
                //
                // Las marcas van DENTRO del mapa de bits de cada tarjeta, asi que al cambiar la cola hay que
                // volver a dibujar... pero solo la que cambio. Rehacer el cache entero cuesta 10.8 ms medidos
                // con cincuenta resultados en pantalla, y en la maquina del bar eso son cuatro o cinco veces
                // mas: mas de un cuadro, o sea un tiron que se ve al pulsar el boton (y con musica sonando,
                // se oye). Retocando solo lo que cambio es una tarjeta en vez de cincuenta.
                //
                // Se retocan tambien las dos vecinas de arriba y abajo: cada tarjeta se dibuja con tres
                // pixeles de sombra que se salen de su sitio, y borrando en limpio se llevaria por delante un
                // trozo de la de al lado.
                bool soloCola = resCache != null && resCacheW == region.Width && resCacheH == region.Height
                                && resCacheAccent == accentArgb && resCacheQuery == Query && resCacheDatos == DatosVer
                                && resCacheOrigin == region.Location && resCacheCola != ColaVer
                                && resCacheMarca.Length == n;
                if (soloCola)
                {
                    int[] ahora = new int[n];
                    for (int i = 0; i < n; i++) ahora[i] = MarcaDe(i);   // UNA vez por tarjeta, no dos
                    using (Graphics cg = Graphics.FromImage(resCache))
                    {
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        for (int i = 0; i < n; i++)
                        {
                            if (ahora[i] == resCacheMarca[i]) continue;
                            for (int k = i - 1; k <= i + 1; k++)
                            {
                                if (k < 0 || k >= n) continue;
                                Rectangle rk = Rectangle.Round(resultRects[k].Key); rk.Offset(-region.X, -region.Y);
                                rk.Inflate(MargenTarj, MargenTarj);
                                cg.CompositingMode = CompositingMode.SourceCopy;
                                cg.FillRectangle(Brushes.Transparent, rk);
                                cg.CompositingMode = CompositingMode.SourceOver;
                                Rectangle rr2 = Rectangle.Round(resultRects[k].Key); rr2.Offset(-region.X, -region.Y);
                                DrawOneResultCard(cg, k, rr2, false, qNorm);
                            }
                        }
                    }
                    resCacheMarca = ahora;
                    resCacheCola = ColaVer;
                }
                else if (resCache == null || resCacheW != region.Width || resCacheH != region.Height || resCacheAccent != accentArgb || resCacheQuery != Query || resCacheDatos != DatosVer || resCacheCola != ColaVer)
                {
                    int rw = Math.Max(1, region.Width), rh = Math.Max(1, region.Height);
                    if (resCache == null || resCache.Width != rw || resCache.Height != rh)   // REUSA el bitmap si el tamano no cambio
                    {
                        if (resCache != null) resCache.Dispose();   // solo se reasigna al cambiar de tamano, no en cada tecla (menos basura/tirones)
                        resCache = new Bitmap(rw, rh, PixelFormat.Format32bppPArgb);
                    }
                    using (Graphics cg = Graphics.FromImage(resCache))
                    {
                        cg.Clear(Color.Transparent);
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        TextRenderer.DrawText(cg, "Resultados", fHead, new Rectangle(0, 0, region.Width, 24), Color.White, TextFormatFlags.Left);
                        if (n == 0)
                            TextRenderer.DrawText(cg, (Query.Length == 0) ? "Escribe para buscar..." : "Sin resultados", fEmpty, new Rectangle(0, 30, region.Width, 28), Color.FromArgb(190, 200, 225), TextFormatFlags.Left);
                        else
                            for (int i = 0; i < n; i++)
                            {
                                Rectangle rr = Rectangle.Round(resultRects[i].Key); rr.Offset(-region.X, -region.Y);
                                DrawOneResultCard(cg, i, rr, false, qNorm);
                            }
                    }
                    resCacheW = region.Width; resCacheH = region.Height; resCacheAccent = accentArgb; resCacheQuery = Query; resCacheOrigin = region.Location; resCacheDatos = DatosVer; resCacheCola = ColaVer;
                    if (resCacheMarca.Length != n) resCacheMarca = new int[n];
                    for (int i = 0; i < n; i++) resCacheMarca[i] = MarcaDe(i);
                }
                g.DrawImageUnscaled(resCache, resCacheOrigin.X, resCacheOrigin.Y);
                // Tarjeta seleccionada EN VIVO encima: su relleno es opaco y tapa la version sin seleccion del cache.
                if (n > 0)
                {
                    int selIdx = FocusResults ? SelResult : 0;
                    if ((!paraGpu || !SeparateGpu) && selIdx >= 0 && selIdx < n) DrawOneResultCard(g, selIdx, Rectangle.Round(resultRects[selIdx].Key), true, qNorm);
                }
            }

            // Dibuja UNA tarjeta de resultado en rr (ya en las coords donde toca: absolutas en vivo, relativas en el cache).
            // 'qn' es la consulta ya limpia de tildes. Antes se limpiaba DENTRO, o sea CUARENTA veces por cada
            // tecla que pulsaba el cliente, para obtener siempre lo mismo. Ahora se limpia una vez y se pasa.
            private void DrawOneResultCard(Graphics g, int i, Rectangle rr, bool sel, string qn)
            {
                if (Results == null || i < 0 || i >= Results.Count) return;
                EnsureHornoTarjeta(rr.Width, rr.Height);
                g.DrawImageUnscaled(sel ? bmpTarjSel : bmpTarj, rr.X - MargenTarj, rr.Y - MargenTarj);
                SearchItem it = Results[i];
                string display = it.Display;
#if ROCOLA_GPU
                if (SeparateGpu && it.Kind == 2 && it.TrackRef != null)
                    display = it.TrackRef.Artist + " - " + TituloSinPista(it.TrackRef.Title);
#endif
                string tag = (it.Kind == 0) ? "ARTISTA" : (it.Kind == 1) ? "ALBUM" : "CANCION";
#if ROCOLA_GPU
                if (SeparateGpu && it.Kind == 3) tag = "SERVICIO";
#endif
                Color tagc = (it.Kind == 0) ? Color.FromArgb(150, 215, 255) : (it.Kind == 1) ? Color.FromArgb(255, 210, 120) : Color.FromArgb(170, 245, 170);

                // ---- RENGLON DE ARRIBA: que es, de que disco, y de que genero ----
                // La pastilla del genero va pegada a la derecha; lo que quede es para el tipo y el disco.
                int xIz = rr.X + 9, derLim = rr.Right - 9;
                string gen = (GetGenero != null) ? GetGenero(it) : "";
                if (gen != null && gen.Length > 0)
                {
                    Bitmap pg = HorneaGeneroRes(g, gen);
                    int gw = pg.Width - 2 * MargenTarj, gh = pg.Height - 2 * MargenTarj;
                    int gx = derLim - gw;
                    if (gx > xIz + 40)
                    {
                        g.DrawImageUnscaled(pg, gx - MargenTarj, rr.Y + 3 - MargenTarj);   // el fondo, ya hecho
                        TextRenderer.DrawText(g, gen, fResSub, new Rectangle(gx, rr.Y + 3, gw, gh), Lighten(ColorDeEtiqueta(gen), 0.45),
                                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        derLim = gx - 8;
                    }
                }
                // ---- LAS MARCAS: "ya la pediste" y "suena ahora" ----
                // Van en el renglon de arriba, a la izquierda de la pastilla del genero, y con las MISMAS
                // palabras y los mismos colores que dentro del disco. Las dos pueden salir a la vez: una
                // cancion puede estar sonando Y estar pedida otra vez, y el cliente necesita las dos cosas.
                //
                // Si no cabe, no se dibuja: en una pantalla estrecha manda el nombre de la cancion.
                int posCola = (GetQueuePos != null) ? GetQueuePos(it) : 0;
                bool suena = (GetSonando != null) && GetSonando(it);
                // Las MISMAS palabras que dentro del disco, incluido el triangulito: U+25BA y no U+25B6,
                // que son casi identicos pero el otro no esta en el juego de simbolos de Windows XP y alli
                // saldria un rectangulo vacio.
                //
                // Y EL VERDE VA PRIMERO, o sea el mas a la derecha. Si en una tarjeta estrecha solo cabe uno,
                // tiene que ser ese: el cliente esta eligiendo cancion, y lo que le hace falta saber es que
                // ESA ya la pidio. Que suene ahora ya se lo dice la franja de abajo.
                if (posCola > 0) derLim = MarcaRes(g, "EN COLA " + posCola, ColaVerde, rr, xIz, derLim);
                if (suena) derLim = MarcaRes(g, "\u25BA SONANDO", SonAmbar, rr, xIz, derLim);

                // El rotulo se MIDE una vez (los tres posibles miden siempre lo mismo) pero se DIBUJA aqui:
                // el texto no se puede hornear aparte, porque el sistema lo mezcla con el color que tiene
                // debajo y una letra dibujada sobre transparente y luego pegada sale mas fina.
                int tw = AnchoRotulo(g, it.Kind, tag);
                TextRenderer.DrawText(g, tag, fResSub, new Rectangle(xIz, rr.Y + 4, Math.Max(10, derLim - xIz), 13), tagc,
                                      TextFormatFlags.Left | TextFormatFlags.NoPadding);
                string disco = (GetDisco != null) ? GetDisco(it) : "";
                if (disco != null && disco.Length > 0)
                {
                    int dx = xIz + tw + 8;
                    if (derLim - dx > 24)
                        TextRenderer.DrawText(g, "\u00b7  " + disco, fResSub, new Rectangle(dx, rr.Y + 4, derLim - dx, 13),
                                              Color.FromArgb(138, 150, 182),
                                              TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }

                // ---- RENGLON PRINCIPAL: el nombre, con LO TECLEADO encendido ----
                Rectangle tr = new Rectangle(rr.X + 9, rr.Y + 16, rr.Width - 16, rr.Height - 18);
                int mIni, mLar;
                if (qn != null && qn.Length > 0 && BuscaTramo(display, qn, out mIni, out mLar))
                {
                    // Se mide con NoPadding y se dibuja con NoPadding: si no, medir y pintar no coinciden y el
                    // resaltado sale corrido.
                    const TextFormatFlags FL = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping;
                    string ante = display.Substring(0, mIni), coincide = display.Substring(mIni, mLar);
                    int wa = TextRenderer.MeasureText(g, ante, fRes, new Size(9999, 99), TextFormatFlags.NoPadding).Width;
                    int wc = TextRenderer.MeasureText(g, coincide, fRes, new Size(9999, 99), TextFormatFlags.NoPadding).Width;
                    if (wa + wc <= tr.Width)   // si el tramo no se ve entero, mejor no resaltar nada
                    {
                        if (marcaB == null) marcaB = new SolidBrush(Color.Black);
                        marcaB.Color = Color.FromArgb(70, 255, 205, 90);
                        // El resaltado se ajusta al ALTO DE LA LETRA, no al de la caja: la caja es mas alta que
                        // el texto y la marca quedaba colgando por debajo.
                        if (altoLetra < 0) altoLetra = TextRenderer.MeasureText(g, "Xg", fRes, new Size(9999, 99), TextFormatFlags.NoPadding).Height;
                        int hTxt = altoLetra;
                        using (GraphicsPath gp = RoundedPath(new Rectangle(tr.X + wa - 2, tr.Y - 1, wc + 4, hTxt + 2), 4))
                            g.FillPath(marcaB, gp);
                        TextRenderer.DrawText(g, ante, fRes, new Rectangle(tr.X, tr.Y, tr.Width, tr.Height), Color.White, FL);
                        TextRenderer.DrawText(g, coincide, fRes, new Rectangle(tr.X + wa, tr.Y, tr.Width - wa, tr.Height), Color.FromArgb(255, 214, 120), FL);
                        TextRenderer.DrawText(g, display.Substring(mIni + mLar), fRes,
                                              new Rectangle(tr.X + wa + wc, tr.Y, Math.Max(4, tr.Width - wa - wc), tr.Height),
                                              Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                        return;
                    }
                }
                TextRenderer.DrawText(g, display, fRes, tr, Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            // COMO ESTA MARCADA UNA TARJETA, en un solo numero: sirve para saber cuales cambiaron sin volver
            // a dibujarlas. Cero es "ni pedida ni sonando".
            private int MarcaDe(int i)
            {
                if (Results == null || i < 0 || i >= Results.Count) return 0;
                SearchItem it = Results[i];
                int pos = (GetQueuePos != null) ? GetQueuePos(it) : 0;
                bool suena = (GetSonando != null) && GetSonando(it);
                return pos * 2 + (suena ? 1 : 0);
            }

            // Dibuja una marca pegada a 'derLim' y devuelve el nuevo limite derecho para lo que venga detras.
            // Si no cabe deja el limite como estaba y no dibuja nada.
            private int MarcaRes(Graphics g, string texto, Color c, Rectangle rr, int xIz, int derLim)
            {
                Bitmap pm = HorneaMarcaRes(g, texto, c);
                int mw = pm.Width - 2 * MargenTarj, mh = pm.Height - 2 * MargenTarj;
                int mx = derLim - mw;
                if (mx <= xIz + 40) return derLim;   // no cabe: manda el nombre de la cancion
                g.DrawImageUnscaled(pm, mx - MargenTarj, rr.Y + 3 - MargenTarj);
                TextRenderer.DrawText(g, texto, fResSub, new Rectangle(mx, rr.Y + 3, mw, mh), c,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return mx - 6;
            }

            // Cachea las caratulas (ya redimensionadas) de todos los discos del artista
            // Reconstruye SIEMPRE: pide las portadas vigentes a la cache (nunca guarda una
            // referencia que pudiera liberarse despues -> evita dibujar imagenes muertas).
            private List<Image> EnsureArtistCovers(SearchItem it)
            {
                artistCovers.Clear();
                if (it.Albums != null)
                    for (int i = 0; i < it.Albums.Count; i++)
                    {
                        Image im = Portada(it.Albums[i]);
                        if (im != null) artistCovers.Add(im);
                    }
                return artistCovers;
            }

            // Mezcla a->b con factor f (0..1) en un bitmap reutilizable
            private Image BlendInto(Image a, Image b, float f)
            {
                if (blendBmp == null) blendBmp = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) g.DrawImage(a, dst);
                    if (b != null && f > 0f)
                    {
                        ColorMatrix cm = new ColorMatrix();
                        cm.Matrix33 = f;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                }
                return blendBmp;
            }

            // Calcula la(s) portada(s) y la celda del cubo de vista previa (sin dibujarlo). ca/cb/f = par a
            // fundir (discos TOP/artista con varias portadas). Devuelve false si no hay nada que mostrar.
            // BARRER RESULTADOS NO ENCARGA CARATULAS. Antes, cada resultado por el que el cliente pasaba de
            // largo con la flecha encargaba SU caratula al disco: recorrer cuarenta resultados dejaba cuarenta
            // imagenes grandes por descifrar, todas ellas de resultados que ya no estaba mirando. En el
            // ordenador del bar, que tiene un solo nucleo, ese trabajo de fondo es justo lo que se siente como
            // tirones AL MOVERSE. Mientras se mueve solo se usa lo que ya esta en memoria; en cuanto se para
            // en un resultado (dos decimas), se encarga la suya.
            private int selMomento = Environment.TickCount;
            private int selAnterior = -1;
            private const int EsperaPortadaMs = 200;
            public Func<Album, Image> GetCoverYa;   // SOLO lo que ya esta en memoria; no encarga nada al disco
            private Image Portada(Album a)
            {
                if (a == null) return null;
                bool parado = (Environment.TickCount - selMomento) >= EsperaPortadaMs;
                if (parado || GetCoverYa == null) return (GetCover != null) ? GetCover(a) : null;
                return GetCoverYa(a);
            }

            private bool ComputePreview(Rectangle region, out Image ca, out Image cb, out float f, out Rectangle area)
            {
                ca = null; cb = null; f = 0f; area = Rectangle.Empty;
                int n = (Results != null) ? Results.Count : 0;
                if (SelResult != selAnterior) { selAnterior = SelResult; selMomento = Environment.TickCount; }
                if (n == 0 || SelResult < 0 || SelResult >= n) { cubeRect = Rectangle.Empty; return false; }
                SearchItem it = Results[SelResult];
#if ROCOLA_GPU
                if (SeparateGpu && it.Kind == 3) { cubeRect = Rectangle.Empty; return false; }
#endif
                if (it.Kind == 0)
                {
                    List<Image> cov = EnsureArtistCovers(it);
                    if (cov.Count == 0) ca = Portada(it.AlbumRef);
                    else if (cov.Count == 1) ca = cov[0];
                    else
                    {
                        double cyc = 3.8, hold = 2.6;   // ~3.2s por caratula, funde ~1s
                        int m = cov.Count;
                        double total = cyc * m;
                        double pos = Phase - Math.Floor(Phase / total) * total;
                        int idx = (int)(pos / cyc) % m;
                        double within = pos - idx * cyc;
                        float ff = 0f;
                        if (within > hold) ff = (float)((within - hold) / (cyc - hold));
                        if (ff < 0f) ff = 0f; else if (ff > 1f) ff = 1f;
                        ca = cov[idx]; cb = cov[(idx + 1) % m]; f = ff;
                    }
                }
                else ca = Portada(it.AlbumRef);

                area = CajaPortada(region);
                cubeRect = area;
                return true;
            }

            // CPU: dibuja el cubo de vista previa (girando) + su texto.
            private void DrawPreviewCube(Graphics g, Rectangle region)
            {
                Image ca, cb; float f; Rectangle area;
                if (!ComputePreview(region, out ca, out cb, out f, out area)) return;
                Image cover = (cb == null || f <= 0.002f) ? ca : BlendInto(ca, cb, f);
                g.InterpolationMode = InterpolationMode.Bilinear;
                Cube.Draw(g, area, cover, CubeAngle);
                DrawPreviewInfo(g, region, area);
            }

            // Texto de la vista previa (titulo, subtitulo y ayuda) debajo del cubo. Comun a CPU y GPU.
            private void DrawPreviewInfo(Graphics g, Rectangle region, Rectangle area)
            {
                int n = (Results != null) ? Results.Count : 0;
                if (SelResult < 0 || SelResult >= n) return;
                SearchItem it = Results[SelResult];
                string title, sub;
                if (it.Kind == 1) { title = (it.AlbumRef != null) ? it.AlbumRef.Name : it.Display; sub = (it.AlbumRef != null) ? it.AlbumRef.Artist : ""; }
                else if (it.Kind == 2 && it.TrackRef != null) { title = it.TrackRef.Title; sub = it.TrackRef.Artist + ((it.AlbumRef != null) ? "   -   " + it.AlbumRef.Name : ""); }
                else { title = it.Display; int dc = (it.Albums != null) ? it.Albums.Count : 0; sub = dc + (dc == 1 ? " disco" : " discos"); }

#if ROCOLA_GPU
                if (SeparateGpu && it.Kind == 2) title = TituloSinPista(title);
#endif
                int ty = area.Bottom + 12;
                EnsurePaintRes();
#if ROCOLA_GPU
                if (!CintaVistaPrevia(g, title, fPrevT, Color.White, new Rectangle(region.X, ty, region.Width, 28)))
#endif
                TextRenderer.DrawText(g, title, fPrevT, new Rectangle(region.X, ty, region.Width, 28), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
#if ROCOLA_GPU
                if (!CintaVistaPrevia(g, sub, fPrevS, Color.FromArgb(215, 225, 245), new Rectangle(region.X, ty + 30, region.Width, 24)))
#endif
                TextRenderer.DrawText(g, sub, fPrevS, new Rectangle(region.X, ty + 30, region.Width, 24), Color.FromArgb(215, 225, 245), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                // Ayuda con ICONOS (mismo estilo que la barra de arriba), centrada.
                {
                    Font hf = fPrevH;
                    SmoothingMode oldsm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                    System.Drawing.Text.TextRenderingHint oldth = g.TextRenderingHint;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    Color htx = Color.FromArgb(205, 214, 238);
                    Color yellow = Color.FromArgb(242, 206, 72), green = Color.FromArgb(72, 212, 112);
                    int hcy = region.Bottom - 14;
                    int dd = 15; float gap = 7, grp = 26;
                    string t1 = "volver al teclado", t2 = "elegir";
                    SizeF s1 = g.MeasureString(t1, hf), s2 = g.MeasureString(t2, hf);
                    float total = dd + gap + s1.Width + grp + dd + gap + s2.Width;
                    float hx = region.X + (region.Width - total) / 2f;
                    hx = CtrlDirLeft(g, hx, hcy, dd, yellow) + gap;   // boton IZQUIERDA
                    hx = CtrlTxt(g, hf, t1, hx, hcy, htx) + grp;
                    hx = CtrlDot(g, hx, hcy, dd, green) + gap;         // boton PONER (verde)
                    hx = CtrlTxt(g, hf, t2, hx, hcy, htx);
                    g.SmoothingMode = oldsm; g.TextRenderingHint = oldth;
                }
            }
        }

        private class SearchItem
        {
            public int Kind;     // 0=artista, 1=album, 2=cancion
            public string Display;
            public string Key;
            public int TitleAt, TitleLength; // titulo/artista principal dentro de Key, sin otra copia
            public int Mask;    // que letras contiene Key (ver MarcaLetras)
            public int Score;
            public Album AlbumRef;
            public Track TrackRef;
            public List<Album> Albums;   // todos los discos del artista (Kind==0)
        }

        private class Album
        {
            public string Folder;
            public string Name;
            public string Artist;
            public char Letter;
            public string CoverPath;
            public List<Track> Tracks = new List<Track>();
            public List<string> VideoFiles = new List<string>();
            public bool IsTop = false;             // disco virtual de "lo mas sonado" por genero
            public bool TopIsVideo = false;        // (TOP) este disco virtual contiene VIDEOS, no canciones
            public string KeyCache, KeyFrom;       // clave de la casilla ya armada (ver CellKey): se pide en cada cuadro
            public string KeyQ, KeyQDe;            // y su variante con el palomito (ver ClaveCasilla), tambien armada una vez
            public List<string> CycleCovers;       // (TOP) caratulas distintas para ciclar en el cubo seleccionado
            public List<string> TrackCovers;       // (TOP) caratula de origen por cada cancion (alineado con Tracks)
            public List<string> TrackInfo;         // (TOP) "Artista - Album" de origen por cada cancion
            public List<Album> TrackAlbums;        // (TOP) disco de origen por cada cancion (para "sonando")
        }
    }
}

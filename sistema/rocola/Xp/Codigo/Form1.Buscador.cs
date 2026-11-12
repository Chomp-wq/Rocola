// EL BUSCADOR: la pantalla, el indice y lo que se tecleado
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la casilla de BUSCAR y el buscador de siempre dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; lo ya traducido de otros archivos se llama directo con el lienzo y su estado (ContornoRedondo y el cielo de Form1.Fondo.cs; el cubo, CtrlTxt, CtrlDot, CtrlPad, CtrlTri, CtrlDirLeft y GlowAround de Form1.Pared.cs), y tambien llaman directo a estas DrawCtrlOverlay, EnsureUiOverlay, EnsureUiPieza (Form1.Video.cs) y el precalentado; solo lo que aun es de la capa (las portadas de la cache, Image, y el Graphics de WinForms de OnPaint) pasa por el puente (Plataforma/Puente.cs y Puente.buscador.cs)
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
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
        private partial class BuscaView : Control   // EN LINUX: partial: la firma de XP de RenderContent con un Graphics (la de OnPaint) va en el puente (Plataforma/Puente.buscador.cs)
        {
            public bool Abierto = false;      // con el buscador abierto la casilla se queda encendida fija
            public double Phase = 0;          // para el parpadeo
            private Rocola.Nativo.Letra fTxt; private float fAlto = -1f;   // EN LINUX: la letra de la casilla, una Letra (Nativo/Letras.cs; Form1 tiene un metodo Letra)

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
#if ROCOLA_GPU
            // LO QUE DIBUJA DE VERDAD, medido con su letra (una vez por alto), con 12 px de aire al final como los 11 del
            // principio. Ancho() reserva el texto a ojo y deja ~45 px vacios al final; con tarjeta grafica la fila de
            // arriba se reparte con este (AcomodaAyuda). Sin tarjeta (dibujo por CPU, congelado) sigue Ancho().
            private static int justoAlto = -1, justoTexto, justoMas;
            public static int AnchoJusto(int alto)
            {
                int d = Sim(alto);
                if (justoAlto != alto)
                {
                    // EN LINUX: using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b)): el mapa de 1x1 solo daba un Graphics con que medir, y medir no pide lienzo (TextoGdiPlus.Mide)
                    using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, Math.Max(8f, alto * 0.30f), true))   // EN LINUX: new Font(FontFamily.GenericSansSerif, pt, Bold): la generica sin remates de XP (Microsoft Sans Serif, Letras.SinRemates), en puntos y en negrita (Nativo/Letras.cs)
                    {
                        AjusteLetra ajuste = AjusteLetra.Suave;   // EN LINUX: g.TextRenderingHint = AntiAlias, en una variable
                        justoTexto = (int)Math.Ceiling(TextoGdiPlus.Mide("BUSCAR", f, ajuste, new SizeF(0, 0), null).Width);   // EN LINUX: g.MeasureString("BUSCAR", f): sin caja (0, 0) y sin formato (null: el de fabrica) (Nativo/TextoGdiPlus.cs)
                        justoMas = (int)Math.Ceiling(TextoGdiPlus.Mide("+", f, ajuste, new SizeF(0, 0), null).Width);   // EN LINUX: g.MeasureString("+", f)
                    }
                    justoAlto = alto;
                }
                // Lo mismo que recorre RenderContent: lupa (d), 8 + BUSCAR + 12, rojo + 4, "+" + 6, flecha.
                return Math.Min(Ancho(alto), 11 + d + 8 + justoTexto + 12 + d + 4 + justoMas + 6 + (int)(d * 0.9f) + 12);
            }
#endif

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.TextRenderingHint = Rocola.Dibujo2D.TextRenderingHint.ClearTypeGridFit;   // EN LINUX: el Graphics de WinForms (OnPaint, aun sin traducir): el modo de letra con que pinta, escrito entero; RenderContent(Graphics) va por el puente (Plataforma/Puente.buscador.cs)
                RenderContent(e.Graphics);
            }

            // EN LINUX: public void RenderContent(Graphics g) { RenderContent(g, false); }: la firma de XP para OnPaint va en el puente (Plataforma/Puente.buscador.cs); la de abajo (con forGpu) la llama directo DrawCtrlOverlay (Form1.Video.cs)
            // forGpu=true: se dibuja a brillo FIJO. El parpadeo lo pone la tarjeta grafica como un halo
            // aparte (FillUiGlows); si parpadeara aqui, habria que rehacer y volver a subir la textura de la
            // interfaz entera veinticuatro veces por segundo, que es justo lo que no se puede hacer.
            public void RenderContent(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, bool forGpu)   // EN LINUX: el lienzo de Skia (con su matriz y su recorte) y el estado del que llama que aqui no se pone (el modo de letra, la copia y el medio pixel); DrawCtrlOverlay (Form1.Video.cs) la llama directo con su lienzo y su estado; OnPaint, que aun pasa un Graphics, llega por el puente (Plataforma/Puente.buscador.cs), que le deja al Graphics el suavizado que le deja XP
            {
                int w = Width, h = Height;
                if (w < 30 || h < 12) return;
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias: el estado del dibujo en variables
                if (fTxt == null || fAlto != h)
                { if (fTxt != null) fTxt.Dispose(); fTxt = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, Math.Max(8f, h * 0.30f), true); fAlto = h; }   // EN LINUX: new Font(FontFamily.GenericSansSerif, pt, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos y en negrita

                // EL PARPADEO. Con el buscador cerrado late para llamar la atencion; con el buscador abierto
                // se queda fija y encendida, porque ya cumplio su trabajo y parpadeando distraeria.
                double lat = (Abierto || forGpu) ? 1.0 : (0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(Phase * 2.4)));
                Color azul = Color.FromArgb(90, 190, 255);
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (SKPath gp = ContornoRedondo(r, Math.Min(14, h / 2)))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                {
                    using (SKPaint sb = new SKPaint { Color = Color.FromArgb((int)(28 + 58 * lat), azul).Sk() }) Pixel.Rellena(g, gp, sb, suave, medio, copia);   // EN LINUX: SolidBrush: el pincel de un color en un SKPaint; g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pn = new SKPaint { Color = Color.FromArgb((int)(110 + 145 * lat), azul).Sk(), StrokeWidth = 2f }) Pixel.Traza(g, gp, pn, suave, medio, copia);   // EN LINUX: Pen(color, 2): la pluma en un SKPaint (Pixel le pone el minimo de un pixel y las esquinas de GDI+); g.DrawPath
                }

                int cy = h / 2, d = Sim(h);
                float x = 11;
                Color tinta = Color.FromArgb((int)(170 + 85 * lat), 235, 245, 255);
                x = Lupa(g, suave, copia, medio, x, cy, (int)(d * 1.15f), tinta);   // EN LINUX: el lienzo y su estado
                x = CtrlTxt(g, suave, ajuste, copia, medio, fTxt, "BUSCAR", x + 8, cy, tinta) + 12;   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
                // Y los dos botones que hay que apretar, dibujados tal cual son.
                x = CtrlDot(g, suave, ajuste, copia, medio, x, cy, d, Color.FromArgb(228, 72, 72)) + 4;   // EN LINUX: CtrlDot (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                x = CtrlTxt(g, suave, ajuste, copia, medio, fTxt, "+", x, cy, tinta) + 6;   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
#if ROCOLA_GPU
                Tecla = new Rectangle((int)x, cy - (int)(d * 0.9f) / 2, (int)(d * 0.9f), (int)(d * 0.9f));   // la guia del rojo la resalta
#endif
                Flecha(g, suave, ajuste, copia, medio, x, cy, (int)(d * 0.9f), Color.FromArgb(228, 72, 72));   // EN LINUX: el lienzo y su estado
            }
#if ROCOLA_GPU
            // Donde quedo la tecla de la flecha la ultima vez que se dibujo (coordenadas del control; vacio si aun no).
            // Con el ROJO mantenido, la guia la resalta a ella en vez de pintar otra flecha fuera (GuiaRojoGpu.cs).
            public Rectangle Tecla;
#endif

            // Donde va el halo del parpadeo cuando lo dibuja la tarjeta grafica (coordenadas dentro del control).
            public bool GetLatido(out float cx, out float cy, out float r)
            {
                cx = Width / 2f; cy = Height / 2f; r = Width * 0.55f;
                return !Abierto && Width > 30 && Height > 12;
            }

            // La lupa: un aro y su mango.
            private static float Lupa(SKCanvas g, bool suave, bool copia, bool medio, float x, int cy, int d, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama que pesa aqui (suavizado, copia y medio pixel)
            {
                float aro = d * 0.72f;
                using (SKPaint pn = new SKPaint { Color = c.Sk(), StrokeWidth = Math.Max(2f, d * 0.13f) })   // EN LINUX: Pen(color, ancho): la pluma en un SKPaint
                {
                    Pixel.TrazaOvalo(g, new RectangleF(x, cy - aro / 2f, aro, aro), pn, suave, medio, copia);   // EN LINUX: g.DrawEllipse (Nativo/Pixel.cs)
                    float m = aro * 0.72f;
                    Pixel.Linea(g, x + aro * 0.85f, cy + aro * 0.35f, x + aro * 0.85f + m * 0.5f, cy + aro * 0.35f + m * 0.5f, pn, suave, medio, copia);   // EN LINUX: g.DrawLine (Nativo/Pixel.cs)
                }
                return x + d;
            }

            // La flecha de ARRIBA, en una tecla redondeada: se lee como un boton, no como un adorno.
            private static void Flecha(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama (suavizado, modo de letra, copia y medio pixel)
            {
                Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
                using (SKPath gp = ContornoRedondo(k, Math.Max(3, d / 4)))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                {
                    using (SKPaint sb = new SKPaint { Color = Color.FromArgb(70, c).Sk() }) Pixel.Rellena(g, gp, sb, suave, medio, copia);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pn = new SKPaint { Color = Color.FromArgb(215, c).Sk(), StrokeWidth = 1.6f }) Pixel.Traza(g, gp, pn, suave, medio, copia);   // EN LINUX: Pen(color, 1,6) y g.DrawPath
                }
                float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
                CtrlTri(g, suave, ajuste, copia, medio, c, new PointF(cx, cy - t), new PointF(cx - an, cy + t * 0.62f), new PointF(cx + an, cy + t * 0.62f));   // EN LINUX: CtrlTri (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
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
            public Func<Album, Rocola.Dibujo2D.Image> GetCover;   // EN LINUX: las portadas son de la cache de Form1.Portadas.cs, que aun las publica como Image de la capa (Dibujo2D.cs): escrito entero; para pintarlas, sus puntos por el puente (PuenteDibujo.Mapa)
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
            public bool GetPreviewCube(out Rectangle area, out Rocola.Dibujo2D.Image ca, out Rocola.Dibujo2D.Image cb, out float f)   // EN LINUX: las portadas del cubo (Image de la cache, de la capa) para la tarjeta (Form1.Video.cs y Form1.Sonido.cs las pasan a D3D9Star.cs, que aun las sube como Bitmap de la capa): escrito entero
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
            private readonly List<Rocola.Dibujo2D.Image> artistCovers = new List<Rocola.Dibujo2D.Image>();   // EN LINUX: las portadas de la cache (Image de la capa), las mismas (RetienePortada de Form1.Biblioteca.cs las compara por identidad): escrito entero
            private SKBitmap blendBmp;   // EN LINUX: un SKBitmap (Nativo/Mapas.cs)
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
            private static float FlechaArriba(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama (suavizado, modo de letra, copia y medio pixel); la llama tambien la barra de ayudas de BuscadorPortadasGpu.cs, directo
            {
                Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
                using (SKPath gp = ContornoRedondo(k, Math.Max(3, d / 4)))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                {
                    using (SKPaint sb = new SKPaint { Color = Color.FromArgb(70, c).Sk() }) Pixel.Rellena(g, gp, sb, suave, medio, copia);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pn = new SKPaint { Color = Color.FromArgb(215, c).Sk(), StrokeWidth = 1.6f }) Pixel.Traza(g, gp, pn, suave, medio, copia);   // EN LINUX: Pen(color, 1,6) y g.DrawPath
                }
                float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
                CtrlTri(g, suave, ajuste, copia, medio, c, new PointF(cx, cy - t), new PointF(cx - an, cy + t * 0.62f), new PointF(cx + an, cy + t * 0.62f));   // EN LINUX: CtrlTri (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
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
            private Rocola.Nativo.Letra fQuery, fHint, fToast, fHead, fEmpty, fRes, fResSub, fPrevT, fPrevS, fPrevH;   // EN LINUX: las letras, Letra (Nativo/Letras.cs; Form1 tiene un metodo Letra)
            private Rocola.Nativo.Letra fChar, fSp; private float fKh = -1f;   // fuentes del teclado (dependen de su altura)   // EN LINUX: Letra
            private SKPaint sbS; private SKPaint pnS;            // pincel/pluma reusables (se les muta color/grosor)   // EN LINUX: el SolidBrush y la Pen, dos SKPaint (Pixel les pone lo demas de GDI+ al pintar y se lo quita)
            // CACHE del teclado SIN seleccion: las ~40 teclas (con degradado, contorno y texto) son carisimas
            // de redibujar cada vez (RoundedPath + LinearGradientBrush por tecla). Como el teclado es estatico,
            // se dibuja UNA vez a un bitmap y luego solo se copia + se pinta EN VIVO la tecla seleccionada.
            private SKBitmap kbCache; private int kbCacheW = -1, kbCacheH = -1, kbCacheAccent; private float kbCacheKh = -1f;   // EN LINUX: un SKBitmap
            private Point kbCacheOrigin;
            // CACHE de la lista de resultados (mismo motivo que el teclado): ~40 tarjetas con degradado, contorno
            // y 2 textos cada una. Los resultados dependen SOLO de la consulta, asi que se cachean por Query y
            // solo se pinta EN VIVO la tarjeta seleccionada. Moverse por teclas/resultados deja de dar tirones.
            private SKBitmap resCache; private string resCacheQuery = "\0"; private int resCacheW = -1, resCacheH = -1, resCacheAccent, resCacheDatos = -1, resCacheCola = -1;   // EN LINUX: un SKBitmap
            // COMO ESTABA MARCADA CADA TARJETA cuando se horneo el cache. Sirve para no rehornear las
            // cincuenta al pedir UNA cancion: ver el retoque de mas abajo.
            private int[] resCacheMarca = new int[0];
            private SKPaint marcaB;   // el resaltado de lo tecleado (reusable)   // EN LINUX: un SKPaint
            private Point resCacheOrigin;
            // HORNOS DE LA TARJETA DE RESULTADO. Todas las tarjetas miden lo mismo, asi que su fondo (rectangulo
            // redondeado relleno + borde, con antialias) se dibuja UNA vez a un bitmap y luego cada tarjeta lo
            // copia. Igual la pastilla del genero, de la que solo hay una docena distintas en toda la rocola.
            // Antes se dibujaban a mano en las CUARENTA tarjetas cada vez que el cliente tecleaba una letra.
            private SKBitmap bmpTarj, bmpTarjSel;   // EN LINUX: SKBitmap
            private int hornoTarjW = -1, hornoTarjH = -1, hornoTarjAc;
            private readonly Dictionary<string, SKBitmap> hornoGen = new Dictionary<string, SKBitmap>();   // EN LINUX: SKBitmap
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
            private SKBitmap HorneaTarjeta(int w, int h, bool sel)   // EN LINUX: un SKBitmap
            {
                if (w < 2) w = 2; if (h < 2) h = 2;
                SKBitmap b = Mapas.Nuevo(w + 2 * MargenTarj, h + 2 * MargenTarj);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage (un lienzo nuevo: sin copia ni medio pixel, los de fabrica)
                {
                    bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable
                    g.Clear(Color.Transparent.Sk());   // EN LINUX: g.Clear(Color.Transparent)
                    using (SKPath gp = ContornoRedondo(new Rectangle(MargenTarj, MargenTarj, w, h), 8))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                    {
                        using (SKPaint sb = new SKPaint { Color = (sel ? Shade(Accent, 0.26) : Shade(Accent, 0.50)).Sk() }) Pixel.Rellena(g, gp, sb, suave, false, false);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs), sin medio pixel ni copia (un Graphics nuevo)
                        using (SKPaint pn = new SKPaint { Color = Color.FromArgb(sel ? 215 : 100, 160, 200, 245).Sk(), StrokeWidth = sel ? 2f : 1.1f }) Pixel.Traza(g, gp, pn, suave, false, false);   // EN LINUX: Pen(color, ancho) y g.DrawPath
                    }
                }
                return b;
            }
            // LA PASTILLA DE UNA MARCA ("EN COLA 3", "SUENA AHORA"), horneada igual que la del genero y
            // guardada por su texto: en una pantalla de cuarenta resultados se repiten mucho, y hornear un
            // fondo redondeado cuarenta veces por tecla se nota.
            private readonly Dictionary<string, SKBitmap> hornoMarca = new Dictionary<string, SKBitmap>(StringComparer.Ordinal);   // EN LINUX: SKBitmap
            private SKBitmap HorneaMarcaRes(SKCanvas medidor, string texto, Color c)   // EN LINUX: un SKBitmap; el medidor, el lienzo del que llama (el texto de GDI mide sin el: TextoGdi.Mide)
            {
                SKBitmap b;   // EN LINUX: SKBitmap
                if (hornoMarca.TryGetValue(texto, out b)) return b;
                Size gs = TextoGdi.Mide(texto, fResSub, new Size(9999, 99), BanderasGdi.SinMargen);   // EN LINUX: TextRenderer.MeasureText(medidor, texto, letra, caja, NoPadding): la medida de GDI (Nativo/TextoGdi.cs), que no mira el Graphics
                int w = gs.Width + 14, h = 15;
                if (w < 8) w = 8;
                b = Mapas.Nuevo(w + 2 * MargenTarj, h + 2 * MargenTarj);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage (un lienzo nuevo: sin copia ni medio pixel)
                {
                    bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias
                    g.Clear(Color.Transparent.Sk());   // EN LINUX: g.Clear(Color.Transparent)
                    using (SKPath gp = ContornoRedondo(new Rectangle(MargenTarj, MargenTarj, w, h), 7))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                    {
                        using (SKPaint sb = new SKPaint { Color = Color.FromArgb(70, c).Sk() }) Pixel.Rellena(g, gp, sb, suave, false, false);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                        using (SKPaint pn = new SKPaint { Color = Color.FromArgb(170, c).Sk(), StrokeWidth = 1f }) Pixel.Traza(g, gp, pn, suave, false, false);   // EN LINUX: Pen(color, 1) y g.DrawPath
                    }
                }
                hornoMarca[texto] = b;
                return b;
            }

            private SKBitmap HorneaGeneroRes(SKCanvas medidor, string etiqueta)   // EN LINUX: un SKBitmap; el medidor, el lienzo del que llama (el texto de GDI mide sin el)
            {
                SKBitmap b;   // el color sale del GENERO, no del color de la seccion: no caduca nunca   // EN LINUX: SKBitmap
                if (hornoGen.TryGetValue(etiqueta, out b)) return b;
                Color c = ColorDeEtiqueta(etiqueta);
                Size gs = TextoGdi.Mide(etiqueta, fResSub, new Size(9999, 99), BanderasGdi.SinMargen);   // EN LINUX: TextRenderer.MeasureText(medidor, ..., NoPadding) (Nativo/TextoGdi.cs)
                int w = gs.Width + 14, h = 15;
                if (w < 8) w = 8;
                b = Mapas.Nuevo(w + 2 * MargenTarj, h + 2 * MargenTarj);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage (un lienzo nuevo: sin copia ni medio pixel)
                {
                    bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias
                    g.Clear(Color.Transparent.Sk());   // EN LINUX: g.Clear(Color.Transparent)
                    using (SKPath gp = ContornoRedondo(new Rectangle(MargenTarj, MargenTarj, w, h), 7))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                    {
                        using (SKPaint sb = new SKPaint { Color = Color.FromArgb(60, c).Sk() }) Pixel.Rellena(g, gp, sb, suave, false, false);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                        using (SKPaint pn = new SKPaint { Color = Color.FromArgb(120, c).Sk(), StrokeWidth = 1f }) Pixel.Traza(g, gp, pn, suave, false, false);   // EN LINUX: Pen(color, 1) y g.DrawPath
                    }
                }
                hornoGen[etiqueta] = b;
                return b;
            }
            // Los tres rotulos posibles (ARTISTA/ALBUM/CANCION) miden siempre lo mismo: se mide una vez.
            private readonly int[] anchoRotulo = { -1, -1, -1 };
            private int altoLetra = -1;   // alto de una letra en la fuente del nombre: es siempre el mismo
            private int AnchoRotulo(SKCanvas g, int kind, string tag)   // EN LINUX: el lienzo del que llama (el texto de GDI mide sin el: TextoGdi.Mide)
            {
                if (kind < 0 || kind > 2) return TextoGdi.Mide(tag, fResSub, new Size(9999, 99), BanderasGdi.SinMargen).Width;   // EN LINUX: TextRenderer.MeasureText(g, ..., NoPadding) (Nativo/TextoGdi.cs)
                if (anchoRotulo[kind] < 0)
                    anchoRotulo[kind] = TextoGdi.Mide(tag, fResSub, new Size(9999, 99), BanderasGdi.SinMargen).Width;   // EN LINUX: TextRenderer.MeasureText(g, ..., NoPadding)
                return anchoRotulo[kind];
            }

            private void EnsurePaintRes()
            {
                if (fQuery != null) return;
                fQuery = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 20f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 20f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fHint = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 9.5f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 9.5f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fToast = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 15.5f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 15.5f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fHead = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 13f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 13f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fEmpty = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 11f, false, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 11f, Italic): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fRes = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 10.5f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 10.5f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fResSub = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 7.5f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 7.5f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fPrevT = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 15f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 15f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fPrevS = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 11f);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 11f, Regular): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                fPrevH = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 11f, true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, 11f, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos (Nativo/Letras.cs)
                sbS = new SKPaint { Color = Color.Black.Sk() };   // EN LINUX: new SolidBrush(Color.Black): un SKPaint
                pnS = new SKPaint { Color = Color.Black.Sk(), StrokeWidth = 1f };   // EN LINUX: new Pen(Color.Black, 1): un SKPaint (la union, las puntas y los guiones de fabrica: los de la Pen)
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

            private bool PuedeElegirConsulta()
            {
                if (Query == Escrito) return true;
                // Publicar los resultados nuevos, pero no heredar el indice de una tarjeta antigua.
                // El siguiente toque confirma lo que ahora se ve. Solo GPU: CPU permanece congelado.
#if ROCOLA_GPU
                if (Nuevo) { PosaConsulta(); return false; }
#endif
                return true;
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
            public void DibujaPiezaViva(SKCanvas g, ref bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc, int cual, int dx, int dy)   // EN LINUX: el lienzo de Skia y el estado del Graphics del que llama (el suavizado vuelve con lo que XP le deja puesto); EnsureUiPieza (Form1.Video.cs) la llama directo con su lienzo y su estado
            {
#if ROCOLA_GPU
                if (Nuevo) { suave = true; DibujaPiezaVivaNueva(g, ajuste, copia, filtro, medio, cual, dx, dy); return; }   // EN LINUX: la traducida (BuscadorPortadasGpu.cs), directo, con el lienzo y su estado; XP le pone el suavizado al empezar y se lo deja puesto (ref)
                if (cual >= 2) { DrawExtraGpu(g, suave, ajuste, copia, filtro, medio, hdc, cual, dx, dy); return; }   // EN LINUX: el lienzo y su estado (AcabadoGpu.cs, ya traducido)
#endif
                if (cual == PiezaEscrito)
                {
                    suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias (se le queda al que llama: ref)
                    DibujaCajaEscrito(g, suave, ajuste, copia, medio, hdc, dx, dy, true);   // EN LINUX: el lienzo y su estado
                    return;
                }
                if (cual != PiezaTecla || FocusResults) return;
                if (SelRow < 0 || SelRow >= rows.Count || SelCol < 0 || SelCol >= rows[SelRow].Count) return;
                suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias (se le queda al que llama: ref)
                DrawOneKey(g, suave, ajuste, copia, medio, hdc, SelRow, SelCol, true, dx, dy);   // EN LINUX: el lienzo y su estado
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
            private void DibujaCajaEscrito(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, SKMatrix hdc, int dx, int dy, bool sinParpadeo)   // EN LINUX: el lienzo de Skia y el estado del que llama (y la matriz del HDC, la del texto de GDI)
            {
                Rectangle qb = CajaEscrito(); qb.Offset(dx, dy);
                EnsurePaintRes();
                using (SKPath gp = ContornoRedondo(qb, 16))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                {
                    sbS.Color = Shade(Accent, 0.42).Sk(); Pixel.Rellena(g, gp, sbS, suave, medio, copia);   // EN LINUX: sbS.Color y g.FillPath (Nativo/Pixel.cs)
                    pnS.Color = Lighten(Accent, 0.35).Sk(); pnS.StrokeWidth = 2f; Pixel.Traza(g, gp, pnS, suave, medio, copia);   // EN LINUX: pnS.Color, pnS.Width y g.DrawPath (Nativo/Pixel.cs)
                }
                // Con tarjeta grafica el cursor NO parpadea (fijo): parpadear obligaria a rehacer esta imagen
                // una vez por segundo aunque nadie estuviera escribiendo.
                bool caretOn = sinParpadeo || Math.Sin(Phase * 3.0) > 0;
                string shown = "Buscar:  " + Escrito + (caretOn ? "|" : "");
                TextoGdi.Escribe(g, hdc, shown, fQuery, new Rectangle(qb.X + 18, qb.Y, qb.Width - 30, qb.Height), Color.White, BanderasGdi.Izquierda | BanderasGdi.EnMedio | BanderasGdi.PuntosAlFinal, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs): GDI pinta con la matriz del HDC del que llama; sin suavizar con los modos SingleBitPerPixel del que llama
            }

            public void RenderContent(SKCanvas g, AjusteLetra ajuste, bool copia, ref Filtro filtro, bool medio, SKMatrix hdc, Rectangle clip, bool forGpu)   // EN LINUX: el lienzo de Skia (con su matriz y su recorte) y el estado del que llama (modo de letra, copia, filtro, medio pixel y la matriz del HDC, la del texto de GDI); el filtro vuelve con lo que XP le deja puesto (DrawPreviewCube); EnsureUiOverlay (Form1.Video.cs) y los precalentados (Form1.Portadas.cs y PrecargaPaginaGpu.cs) la llaman directo; OnPaint, que aun pasa un Graphics, llega por el puente (Plataforma/Puente.buscador.cs), que le deja al Graphics el suavizado y el filtro como XP
            {
                int W = Width, H = Height;
                if (W < 60 || H < 60) return;
                paraGpu = forGpu;
#if ROCOLA_GPU
                if (forGpu && Nuevo) { RenderNuevo(g, ajuste, copia, filtro, medio, hdc); return; }   // el buscador con portadas (BuscadorPortadasGpu.cs)   // EN LINUX: la traducida, directo, con el lienzo y su estado (le pone el suavizado, como XP)
#endif
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable
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
                        Sky.DrawReactive(g, suave, filtro, medio, copia, W, H, SkyColor(Accent), SkyColor2(Accent), PulseLo, PulseMid, PulseHi);   // EN LINUX: Sky.DrawReactive(g, W, H, ...): la traducida (Form1.Fondo.cs), directo, con el lienzo (mira su recorte) y lo que pesa del estado (suavizado, filtro, medio pixel y copia: la copia la pone y la devuelve ella, como XP)
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
                    if (!forGpu) DibujaCajaEscrito(g, suave, ajuste, copia, medio, hdc, 0, 0, false);   // EN LINUX: el lienzo y su estado
                    {
                        Rocola.Nativo.Letra hf = fHint;   // EN LINUX: Letra
                        int hcy = hintRect.Y + hintRect.Height / 2;
                        Color htx = Color.FromArgb(214, 222, 242);
                        int hd = 20; float hgap = 7, hgrp = 20;   // MISMO tamano que la barra principal
                        float hx = hintRect.X;
                        // Guarda el centro/radio del halo (para dibujarlo por GPU). En GPU NO se hornea aqui (forGpu);
                        // en CPU si se pinta como siempre. Asi el destello se anima por GPU sin re-subir la textura.
                        StoreHintGlow(SMover, hx, hcy, hd + 2);
                        if (!forGpu) GlowAround(g, suave, ajuste, copia, medio, hx, hcy, hd + 2, hintFlash.Level(SMover), hintFlash.Col(SMover));   // MOVER = cruceta (color segun direccion)   // EN LINUX: GlowAround (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                        hx = CtrlPad(g, suave, ajuste, copia, medio, hx, hcy, hd + 2);   // EN LINUX: CtrlPad (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                        hx = CtrlTxt(g, suave, ajuste, copia, medio, hf, "Mover", hx + hgap, hcy, htx) + hgrp;   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
                        StoreHintGlow(SPoner, hx, hcy, hd);
                        if (!forGpu) GlowAround(g, suave, ajuste, copia, medio, hx, hcy, hd, hintFlash.Level(SPoner), hintFlash.Col(SPoner));        // PONER/ELEGIR = punto verde   // EN LINUX: GlowAround (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                        hx = CtrlDot(g, suave, ajuste, copia, medio, hx, hcy, hd, SGreen);   // EN LINUX: CtrlDot (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                        hx = CtrlTxt(g, suave, ajuste, copia, medio, hf, "Poner / Elegir", hx + hgap, hcy, htx) + hgrp;   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
                        // CERRAR = los MISMOS dos botones con los que se abrio (rojo + arriba). Antes era
                        // rojo + verde, que ademas chocaba con el verde de "poner": el cliente que queria
                        // salir apretaba el verde y ponia una cancion sin querer.
                        StoreHintGlow(SCerrar, hx, hcy, hd * 2 + 3);
                        if (!forGpu) GlowAround(g, suave, ajuste, copia, medio, hx, hcy, hd * 2 + 3, hintFlash.Level(SCerrar), hintFlash.Col(SCerrar));   // CERRAR = rojo + arriba   // EN LINUX: GlowAround (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                        hx = CtrlDot(g, suave, ajuste, copia, medio, hx, hcy, hd, SRed) + 3;   // EN LINUX: CtrlDot (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                        hx = CtrlTxt(g, suave, ajuste, copia, medio, hf, "+", hx, hcy, htx) + 3;   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
                        hx = FlechaArriba(g, suave, ajuste, copia, medio, hx, hcy, hd, SRed);   // EN LINUX: el lienzo y su estado
                        hx = CtrlTxt(g, suave, ajuste, copia, medio, hf, "Cerrar", hx + hgap, hcy, htx);   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
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
                        Rocola.Dibujo2D.Image ca, cb; float f; Rectangle area;   // EN LINUX: las portadas de la cache (Image, escrito entero)
                        if (!SeparateGpu && ComputePreview(kbRegion, out ca, out cb, out f, out area)) DrawPreviewInfo(g, suave, ajuste, copia, medio, hdc, kbRegion, area);   // EN LINUX: el lienzo y su estado
                    }
                    else if (kbRegion.IntersectsWith(clip)) DrawPreviewCube(g, suave, ajuste, copia, ref filtro, medio, hdc, kbRegion);   // EN LINUX: el lienzo y su estado (el filtro que deja puesto, por ref)
                }
                else
                {
                    cubeRect = Rectangle.Empty;
                    LayoutKeys(kbRegion);
                    if (kbRegion.IntersectsWith(clip)) DrawKeys(g, suave, ajuste, copia, filtro, medio, hdc, kbRegion);   // EN LINUX: el lienzo y su estado
                }
                if (resRegion.IntersectsWith(clip)) DrawResults(g, suave, ajuste, copia, filtro, medio, hdc, resRegion);   // EN LINUX: el lienzo y su estado

                if (ToastOn) DrawToast(g, suave, ajuste, copia, medio, hdc, W, H);   // EN LINUX: el lienzo y su estado
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

            private void DrawToast(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, SKMatrix hdc, int W, int H)   // EN LINUX: el lienzo de Skia y el estado del que llama (y la matriz del HDC, la del texto de GDI); lo llama tambien RenderNuevo (BuscadorPortadasGpu.cs), directo
            {
                int tw = 400, th = 56;
                Rectangle r = new Rectangle((W - tw) / 2, 104, tw, th);
                EnsurePaintRes();
                using (SKPath gp = ContornoRedondo(r, 16))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                {
                    sbS.Color = ToastBack.Sk(); Pixel.Rellena(g, gp, sbS, suave, medio, copia);   // EN LINUX: sbS.Color y g.FillPath (Nativo/Pixel.cs)
                    pnS.Color = Lighten(ToastBack, 0.45).Sk(); pnS.StrokeWidth = 2.4f; Pixel.Traza(g, gp, pnS, suave, medio, copia);   // EN LINUX: pnS.Color, pnS.Width y g.DrawPath
                }
                TextoGdi.Escribe(g, hdc, ToastMsg, fToast, r, Color.White, BanderasGdi.Centrado | BanderasGdi.EnMedio, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), HorizontalCenter | VerticalCenter, con la matriz del HDC del que llama; sin suavizar con los modos SingleBitPerPixel del que llama
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

            private void DrawKeys(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc, Rectangle region)   // EN LINUX: el lienzo de Skia y el estado del que llama
            {
                if (rows.Count == 0 || rows[0].Count == 0) return;
                float kh = rows[0][0].R.Height;
                if (kh < 8) kh = 8;
                EnsurePaintRes();
                if (fChar == null || fKh != kh)   // fuentes del teclado: solo se recrean si cambia su altura
                {
                    if (fChar != null) fChar.Dispose();
                    if (fSp != null) fSp.Dispose();
                    fChar = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, Math.Max(12f, kh * 0.42f), true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, pt, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos
                    fSp = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, Math.Max(9f, kh * 0.24f), true);   // EN LINUX: new Font(FontFamily.GenericSansSerif, pt, Bold)
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
                        kbCache = Mapas.Nuevo(region.Width, region.Height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb) (Nativo/Mapas.cs)
                    }
                    using (SKCanvas cg = new SKCanvas(kbCache))   // EN LINUX: Graphics.FromImage (un lienzo nuevo: el modo de letra del sistema, sin copia ni medio pixel, la matriz del HDC la identidad)
                    {
                        cg.Clear(Color.Transparent.Sk());   // EN LINUX: cg.Clear(Color.Transparent)
                        bool suaveC = true;   // EN LINUX: cg.SmoothingMode = AntiAlias, en una variable
                        for (int r = 0; r < rows.Count; r++)
                            for (int c = 0; c < rows[r].Count; c++)
                                DrawOneKey(cg, suaveC, AjusteLetra.DelSistema, false, false, SKMatrix.Identity, r, c, false, -region.X, -region.Y);   // TODAS sin seleccion, en coords del bitmap   // EN LINUX: el lienzo del mapa y su estado (el de un Graphics nuevo: SystemDefault, sin copia ni medio pixel, la matriz del HDC la identidad)
                    }
                    kbCacheW = region.Width; kbCacheH = region.Height; kbCacheAccent = accentArgb; kbCacheKh = kh; kbCacheOrigin = region.Location;
                }
                Imagenes.Pinta(g, kbCache, new SKRect(0, 0, kbCache.Width, kbCache.Height), new SKRect(kbCacheOrigin.X, kbCacheOrigin.Y, kbCacheOrigin.X + kbCache.Width, kbCacheOrigin.Y + kbCache.Height), filtro, medio, copia);   // EN LINUX: g.DrawImageUnscaled(kbCache, x, y): entera en ese punto, con el filtro, el medio pixel y la copia del lienzo (Nativo/Imagenes.cs)
                // Tecla seleccionada EN VIVO encima: su relleno es opaco y tapa la version sin seleccion del cache.
                // Con tarjeta grafica NO se dibuja aqui: sale como PIEZA VIVA aparte (ver PiezaViva).
                if (!paraGpu && SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)
                    DrawOneKey(g, suave, ajuste, copia, medio, hdc, SelRow, SelCol, true, 0, 0);   // EN LINUX: el lienzo y su estado
            }

            // Dibuja UNA tecla. dx/dy desplaza el rect (para el cache: coords relativas al bitmap). Se usa
            // Offset directo -en vez de TranslateTransform del Graphics- porque TextRenderer (GDI) IGNORA la
            // transformacion y el texto caeria fuera del cache.
            private void DrawOneKey(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, SKMatrix hdc, int r, int c, bool sel, int dx, int dy)   // EN LINUX: el lienzo de Skia y el estado del que llama (y la matriz del HDC, la del texto de GDI)
            {
                SearchKey k = rows[r][c];
                Rectangle kr = Rectangle.Round(k.R); kr.Offset(dx, dy);
                if (kr.Width < 2 || kr.Height < 2) return;
                double kp = 0.5 + 0.5 * Math.Sin(Phase * 2.2 - (r * 0.55 + c * 0.32));
                using (SKPath gp = ContornoRedondo(kr, 10))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                {
                    Color top = sel ? Lighten(Accent, 0.32) : Lighten(Shade(Accent, 0.34), 0.10 + 0.14 * kp);
                    Color bot = sel ? Accent : Shade(Accent, 0.55);
                    using (SKShader sh = Degradado.Lineal(kr, top, bot, Degradado.Vertical, true)) using (SKPaint b = new SKPaint { Shader = sh }) Pixel.Rellena(g, gp, b, suave, medio, copia);   // EN LINUX: LinearGradientBrush(caja, a, b, Vertical): su shader (Nativo/Degradado.cs; los modos son angulos escalables, y repetido fuera como el WrapMode.Tile de fabrica) en un SKPaint; g.FillPath (Nativo/Pixel.cs)
                    if (sel)
                    {
                        int a = 130 + (int)((0.5 + 0.5 * Math.Sin(Phase * 2.6)) * 120);
                        pnS.Color = Color.FromArgb(a, 170, 215, 255).Sk(); pnS.StrokeWidth = 2.8f; Pixel.Traza(g, gp, pnS, suave, medio, copia);   // EN LINUX: pnS.Color, pnS.Width y g.DrawPath (Nativo/Pixel.cs)
                    }
                    else
                    {
                        int a = 85 + (int)(kp * 130);
                        pnS.Color = Color.FromArgb(a, 150, 200, 255).Sk(); pnS.StrokeWidth = 1.7f; Pixel.Traza(g, gp, pnS, suave, medio, copia);   // EN LINUX: pnS.Color, pnS.Width y g.DrawPath
                    }
                }
                Rocola.Nativo.Letra use = (k.Kind == KKind.Char) ? fChar : fSp;   // EN LINUX: Letra
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
                    TextoGdi.Escribe(g, hdc, k.Label, use, new Rectangle(kr.X, kr.Y, kr.Width, kr.Height - d - 6),   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), con la matriz del HDC del que llama
                                          Color.White, BanderasGdi.Centrado | BanderasGdi.EnMedio, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: HorizontalCenter | VerticalCenter; sin suavizar con los modos SingleBitPerPixel del que llama
                    CtrlDot(g, suave, ajuste, copia, medio, kr.X + (kr.Width - d) / 2f, kr.Bottom - 8 - d / 2, d, Color.FromArgb(228, 72, 72));   // EN LINUX: CtrlDot (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                    return;
                }
                TextoGdi.Escribe(g, hdc, k.Label, use, kr, Color.White, BanderasGdi.Centrado | BanderasGdi.EnMedio, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), HorizontalCenter | VerticalCenter; sin suavizar con los modos SingleBitPerPixel del que llama
            }

            private void DrawResults(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc, Rectangle region)   // EN LINUX: el lienzo de Skia y el estado del que llama
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
                    using (SKCanvas cg = new SKCanvas(resCache))   // EN LINUX: Graphics.FromImage (un lienzo nuevo: el modo de letra del sistema, el filtro Bilinear, sin medio pixel, la matriz del HDC la identidad)
                    {
                        bool suaveC = true, copiaC = false;   // EN LINUX: cg.SmoothingMode = AntiAlias; y la copia del lienzo nuevo (SourceOver), en variables
                        for (int i = 0; i < n; i++)
                        {
                            if (ahora[i] == resCacheMarca[i]) continue;
                            for (int k = i - 1; k <= i + 1; k++)
                            {
                                if (k < 0 || k >= n) continue;
                                Rectangle rk = Rectangle.Round(resultRects[k].Key); rk.Offset(-region.X, -region.Y);
                                rk.Inflate(MargenTarj, MargenTarj);
                                copiaC = true;   // EN LINUX: cg.CompositingMode = SourceCopy
                                using (SKPaint tr = new SKPaint { Color = Color.Transparent.Sk() }) Pixel.Rellena(cg, rk, tr, suaveC, false, copiaC);   // EN LINUX: cg.FillRectangle(Brushes.Transparent, rk): un pincel nuevo del color Transparent, con la copia puesta (Nativo/Pixel.cs)
                                copiaC = false;   // EN LINUX: cg.CompositingMode = SourceOver
                                Rectangle rr2 = Rectangle.Round(resultRects[k].Key); rr2.Offset(-region.X, -region.Y);
                                DrawOneResultCard(cg, suaveC, AjusteLetra.DelSistema, copiaC, Filtro.Bilineal, false, SKMatrix.Identity, k, rr2, false, qNorm);   // EN LINUX: el lienzo del mapa y su estado (el de un Graphics nuevo: SystemDefault, Bilinear, sin medio pixel, la matriz del HDC la identidad; la copia, la de la variable)
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
                        resCache = Mapas.Nuevo(rw, rh);   // EN LINUX: new Bitmap(rw, rh, Format32bppPArgb) (Nativo/Mapas.cs)
                    }
                    using (SKCanvas cg = new SKCanvas(resCache))   // EN LINUX: Graphics.FromImage (un lienzo nuevo)
                    {
                        cg.Clear(Color.Transparent.Sk());   // EN LINUX: cg.Clear(Color.Transparent)
                        bool suaveC = true;   // EN LINUX: cg.SmoothingMode = AntiAlias, en una variable
                        TextoGdi.Escribe(cg, SKMatrix.Identity, "Resultados", fHead, new Rectangle(0, 0, region.Width, 24), Color.White, BanderasGdi.Izquierda, false);   // EN LINUX: TextRenderer.DrawText en el mapa (Nativo/TextoGdi.cs): la matriz del HDC de un mapa (la identidad) y el modo de letra de un Graphics nuevo (SystemDefault: suavizado)
                        if (n == 0)
                            TextoGdi.Escribe(cg, SKMatrix.Identity, (Query.Length == 0) ? "Escribe para buscar..." : "Sin resultados", fEmpty, new Rectangle(0, 30, region.Width, 28), Color.FromArgb(190, 200, 225), BanderasGdi.Izquierda, false);   // EN LINUX: TextRenderer.DrawText en el mapa (Nativo/TextoGdi.cs)
                        else
                            for (int i = 0; i < n; i++)
                            {
                                Rectangle rr = Rectangle.Round(resultRects[i].Key); rr.Offset(-region.X, -region.Y);
                                DrawOneResultCard(cg, suaveC, AjusteLetra.DelSistema, false, Filtro.Bilineal, false, SKMatrix.Identity, i, rr, false, qNorm);   // EN LINUX: el lienzo del mapa y su estado (el de un Graphics nuevo)
                            }
                    }
                    resCacheW = region.Width; resCacheH = region.Height; resCacheAccent = accentArgb; resCacheQuery = Query; resCacheOrigin = region.Location; resCacheDatos = DatosVer; resCacheCola = ColaVer;
                    if (resCacheMarca.Length != n) resCacheMarca = new int[n];
                    for (int i = 0; i < n; i++) resCacheMarca[i] = MarcaDe(i);
                }
                Imagenes.Pinta(g, resCache, new SKRect(0, 0, resCache.Width, resCache.Height), new SKRect(resCacheOrigin.X, resCacheOrigin.Y, resCacheOrigin.X + resCache.Width, resCacheOrigin.Y + resCache.Height), filtro, medio, copia);   // EN LINUX: g.DrawImageUnscaled(resCache, x, y): entera en ese punto, con el filtro, el medio pixel y la copia del lienzo (Nativo/Imagenes.cs)
                // Tarjeta seleccionada EN VIVO encima: su relleno es opaco y tapa la version sin seleccion del cache.
                if (n > 0)
                {
                    int selIdx = FocusResults ? SelResult : 0;
                    if ((!paraGpu || !SeparateGpu) && selIdx >= 0 && selIdx < n) DrawOneResultCard(g, suave, ajuste, copia, filtro, medio, hdc, selIdx, Rectangle.Round(resultRects[selIdx].Key), true, qNorm);   // EN LINUX: el lienzo y su estado
                }
            }

            // Dibuja UNA tarjeta de resultado en rr (ya en las coords donde toca: absolutas en vivo, relativas en el cache).
            // 'qn' es la consulta ya limpia de tildes. Antes se limpiaba DENTRO, o sea CUARENTA veces por cada
            // tecla que pulsaba el cliente, para obtener siempre lo mismo. Ahora se limpia una vez y se pasa.
            private void DrawOneResultCard(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc, int i, Rectangle rr, bool sel, string qn)   // EN LINUX: el lienzo de Skia y el estado del que llama (y la matriz del HDC, la del texto de GDI); la llama tambien DrawExtraGpu (AcabadoGpu.cs), directo
            {
                if (Results == null || i < 0 || i >= Results.Count) return;
                EnsureHornoTarjeta(rr.Width, rr.Height);
                SKBitmap hb = sel ? bmpTarjSel : bmpTarj; Imagenes.Pinta(g, hb, new SKRect(0, 0, hb.Width, hb.Height), new SKRect(rr.X - MargenTarj, rr.Y - MargenTarj, rr.X - MargenTarj + hb.Width, rr.Y - MargenTarj + hb.Height), filtro, medio, copia);   // EN LINUX: g.DrawImageUnscaled(fondo, x, y): entera en ese punto (Nativo/Imagenes.cs)
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
                    SKBitmap pg = HorneaGeneroRes(g, gen);   // EN LINUX: SKBitmap
                    int gw = pg.Width - 2 * MargenTarj, gh = pg.Height - 2 * MargenTarj;
                    int gx = derLim - gw;
                    if (gx > xIz + 40)
                    {
                        Imagenes.Pinta(g, pg, new SKRect(0, 0, pg.Width, pg.Height), new SKRect(gx - MargenTarj, rr.Y + 3 - MargenTarj, gx - MargenTarj + pg.Width, rr.Y + 3 - MargenTarj + pg.Height), filtro, medio, copia);   // el fondo, ya hecho   // EN LINUX: g.DrawImageUnscaled (Nativo/Imagenes.cs)
                        TextoGdi.Escribe(g, hdc, gen, fResSub, new Rectangle(gx, rr.Y + 3, gw, gh), Lighten(ColorDeEtiqueta(gen), 0.45),   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), con la matriz del HDC del que llama
                                              BanderasGdi.Centrado | BanderasGdi.EnMedio | BanderasGdi.SinMargen, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: HorizontalCenter | VerticalCenter | NoPadding; sin suavizar con los modos SingleBitPerPixel del que llama
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
                if (posCola > 0) derLim = MarcaRes(g, ajuste, copia, filtro, medio, hdc, "EN COLA " + posCola, ColaVerde, rr, xIz, derLim);   // EN LINUX: el lienzo y su estado
                if (suena) derLim = MarcaRes(g, ajuste, copia, filtro, medio, hdc, "\u25BA SONANDO", SonAmbar, rr, xIz, derLim);   // EN LINUX: el lienzo y su estado

                // El rotulo se MIDE una vez (los tres posibles miden siempre lo mismo) pero se DIBUJA aqui:
                // el texto no se puede hornear aparte, porque el sistema lo mezcla con el color que tiene
                // debajo y una letra dibujada sobre transparente y luego pegada sale mas fina.
                int tw = AnchoRotulo(g, it.Kind, tag);
                TextoGdi.Escribe(g, hdc, tag, fResSub, new Rectangle(xIz, rr.Y + 4, Math.Max(10, derLim - xIz), 13), tagc,   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), con la matriz del HDC del que llama
                                      BanderasGdi.Izquierda | BanderasGdi.SinMargen, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: Left | NoPadding; sin suavizar con los modos SingleBitPerPixel del que llama
                string disco = (GetDisco != null) ? GetDisco(it) : "";
                if (disco != null && disco.Length > 0)
                {
                    int dx = xIz + tw + 8;
                    if (derLim - dx > 24)
                        TextoGdi.Escribe(g, hdc, "\u00b7  " + disco, fResSub, new Rectangle(dx, rr.Y + 4, derLim - dx, 13),   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), con la matriz del HDC del que llama
                                              Color.FromArgb(138, 150, 182),
                                              BanderasGdi.Izquierda | BanderasGdi.PuntosAlFinal | BanderasGdi.SinMargen, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: Left | EndEllipsis | NoPadding; sin suavizar con los modos SingleBitPerPixel del que llama
                }

                // ---- RENGLON PRINCIPAL: el nombre, con LO TECLEADO encendido ----
                Rectangle tr = new Rectangle(rr.X + 9, rr.Y + 16, rr.Width - 16, rr.Height - 18);
                int mIni, mLar;
                if (qn != null && qn.Length > 0 && BuscaTramo(display, qn, out mIni, out mLar))
                {
                    // Se mide con NoPadding y se dibuja con NoPadding: si no, medir y pintar no coinciden y el
                    // resaltado sale corrido.
                    const BanderasGdi FL = BanderasGdi.Izquierda | BanderasGdi.SinMargen | BanderasGdi.SinRecorte;   // EN LINUX: TextFormatFlags Left | NoPadding | NoClipping (Nativo/TextoGdi.cs)
                    string ante = display.Substring(0, mIni), coincide = display.Substring(mIni, mLar);
                    int wa = TextoGdi.Mide(ante, fRes, new Size(9999, 99), BanderasGdi.SinMargen).Width;   // EN LINUX: TextRenderer.MeasureText(g, ..., NoPadding) (Nativo/TextoGdi.cs)
                    int wc = TextoGdi.Mide(coincide, fRes, new Size(9999, 99), BanderasGdi.SinMargen).Width;   // EN LINUX: TextRenderer.MeasureText(g, ..., NoPadding)
                    if (wa + wc <= tr.Width)   // si el tramo no se ve entero, mejor no resaltar nada
                    {
                        if (marcaB == null) marcaB = new SKPaint { Color = Color.Black.Sk() };   // EN LINUX: new SolidBrush(Color.Black): un SKPaint
                        marcaB.Color = Color.FromArgb(70, 255, 205, 90).Sk();   // EN LINUX: marcaB.Color
                        // El resaltado se ajusta al ALTO DE LA LETRA, no al de la caja: la caja es mas alta que
                        // el texto y la marca quedaba colgando por debajo.
                        if (altoLetra < 0) altoLetra = TextoGdi.Mide("Xg", fRes, new Size(9999, 99), BanderasGdi.SinMargen).Height;   // EN LINUX: TextRenderer.MeasureText(g, ..., NoPadding)
                        int hTxt = altoLetra;
                        using (SKPath gp = ContornoRedondo(new Rectangle(tr.X + wa - 2, tr.Y - 1, wc + 4, hTxt + 2), 4))   // EN LINUX: RoundedPath (Form1.Fondo.cs): la traducida, ContornoRedondo, directo: el mismo trazo en un SKPath (par-impar, como el Alternate de fabrica de GraphicsPath, y la figura cerrada); se suelta al salir del using, como el GraphicsPath de XP
                            Pixel.Rellena(g, gp, marcaB, suave, medio, copia);   // EN LINUX: g.FillPath (Nativo/Pixel.cs)
                        TextoGdi.Escribe(g, hdc, ante, fRes, new Rectangle(tr.X, tr.Y, tr.Width, tr.Height), Color.White, FL, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs); sin suavizar con los modos SingleBitPerPixel del que llama
                        TextoGdi.Escribe(g, hdc, coincide, fRes, new Rectangle(tr.X + wa, tr.Y, tr.Width - wa, tr.Height), Color.FromArgb(255, 214, 120), FL, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs)
                        TextoGdi.Escribe(g, hdc, display.Substring(mIni + mLar), fRes,   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), con la matriz del HDC del que llama
                                              new Rectangle(tr.X + wa + wc, tr.Y, Math.Max(4, tr.Width - wa - wc), tr.Height),
                                              Color.White, BanderasGdi.Izquierda | BanderasGdi.PuntosAlFinal | BanderasGdi.SinMargen, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: Left | EndEllipsis | NoPadding; sin suavizar con los modos SingleBitPerPixel del que llama
                        return;
                    }
                }
                TextoGdi.Escribe(g, hdc, display, fRes, tr, Color.White, BanderasGdi.Izquierda | BanderasGdi.PuntosAlFinal, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), Left | EndEllipsis; sin suavizar con los modos SingleBitPerPixel del que llama
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
            private int MarcaRes(SKCanvas g, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc, string texto, Color c, Rectangle rr, int xIz, int derLim)   // EN LINUX: el lienzo de Skia y el estado del que llama que pesa aqui (y la matriz del HDC, la del texto de GDI)
            {
                SKBitmap pm = HorneaMarcaRes(g, texto, c);   // EN LINUX: SKBitmap
                int mw = pm.Width - 2 * MargenTarj, mh = pm.Height - 2 * MargenTarj;
                int mx = derLim - mw;
                if (mx <= xIz + 40) return derLim;   // no cabe: manda el nombre de la cancion
                Imagenes.Pinta(g, pm, new SKRect(0, 0, pm.Width, pm.Height), new SKRect(mx - MargenTarj, rr.Y + 3 - MargenTarj, mx - MargenTarj + pm.Width, rr.Y + 3 - MargenTarj + pm.Height), filtro, medio, copia);   // EN LINUX: g.DrawImageUnscaled (Nativo/Imagenes.cs)
                TextoGdi.Escribe(g, hdc, texto, fResSub, new Rectangle(mx, rr.Y + 3, mw, mh), c,   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), con la matriz del HDC del que llama
                                      BanderasGdi.Centrado | BanderasGdi.EnMedio | BanderasGdi.SinMargen, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: HorizontalCenter | VerticalCenter | NoPadding; sin suavizar con los modos SingleBitPerPixel del que llama
                return mx - 6;
            }

            // Cachea las caratulas (ya redimensionadas) de todos los discos del artista
            // Reconstruye SIEMPRE: pide las portadas vigentes a la cache (nunca guarda una
            // referencia que pudiera liberarse despues -> evita dibujar imagenes muertas).
            private List<Rocola.Dibujo2D.Image> EnsureArtistCovers(SearchItem it)   // EN LINUX: las portadas de la cache (Image, escrito entero)
            {
                artistCovers.Clear();
                if (it.Albums != null)
                    for (int i = 0; i < it.Albums.Count; i++)
                    {
                        Rocola.Dibujo2D.Image im = Portada(it.Albums[i]);   // EN LINUX: Image de la cache
                        if (im != null) artistCovers.Add(im);
                    }
                return artistCovers;
            }

            // Mezcla a->b con factor f (0..1) en un bitmap reutilizable
            private SKBitmap BlendInto(Rocola.Dibujo2D.Image a, Rocola.Dibujo2D.Image b, float f)   // EN LINUX: dos portadas de la cache (Image de la capa: sus puntos por el puente) mezcladas en un SKBitmap
            {
                if (blendBmp == null) blendBmp = Mapas.Nuevo(320, 320);   // EN LINUX: new Bitmap(320, 320, Format32bppPArgb) (Nativo/Mapas.cs)
                using (SKCanvas g = new SKCanvas(blendBmp))   // EN LINUX: Graphics.FromImage (un lienzo nuevo: sin copia ni medio pixel)
                {
                    g.Clear(Color.Black.Sk());   // EN LINUX: g.Clear(Color.Black)
                    Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear, en una variable
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) { SKBitmap ma = PuenteDibujo.Mapa(a); Imagenes.Pinta(g, ma, new SKRect(0, 0, ma.Width, ma.Height), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), filtro, false, false); }   // EN LINUX: g.DrawImage(a, dst): la imagen entera en la caja (Nativo/Imagenes.cs), sin medio pixel ni copia; sus puntos, los mismos, por el puente
                    if (b != null && f > 0f)
                    {
                        float[][] cm = MatrizColor.Identidad();   // EN LINUX: new ColorMatrix(): la identidad (Nativo/Imagenes.cs)
                        cm[3][3] = f;   // EN LINUX: cm.Matrix33 = f
                        // EN LINUX: using (ImageAttributes ia = new ImageAttributes()): lo que lleva (la matriz de color y la envoltura de fabrica) va directo a Imagenes.Pinta
                        {
                            // EN LINUX: ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap): la matriz, abajo
                            SKBitmap mb = PuenteDibujo.Mapa(b); Imagenes.Pinta(g, mb, new SKRect(0, 0, mb.Width, mb.Height), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), filtro, false, false, Envoltura.Transparente, cm);   // EN LINUX: g.DrawImage(b, dst, 0, 0, b.Width, b.Height, Pixel, ia): con la matriz de color y la envoltura de fabrica de ImageAttributes (Clamp: transparente fuera) (Nativo/Imagenes.cs)
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
            public Func<Album, Rocola.Dibujo2D.Image> GetCoverYa;   // SOLO lo que ya esta en memoria; no encarga nada al disco   // EN LINUX: Image de la cache (de la capa), escrito entero
            private Rocola.Dibujo2D.Image Portada(Album a)   // EN LINUX: Image de la cache
            {
                if (a == null) return null;
                bool parado = (Environment.TickCount - selMomento) >= EsperaPortadaMs;
                if (parado || GetCoverYa == null) return (GetCover != null) ? GetCover(a) : null;
                return GetCoverYa(a);
            }

            private bool ComputePreview(Rectangle region, out Rocola.Dibujo2D.Image ca, out Rocola.Dibujo2D.Image cb, out float f, out Rectangle area)   // EN LINUX: las portadas de la cache (Image, escrito entero)
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
                    List<Rocola.Dibujo2D.Image> cov = EnsureArtistCovers(it);   // EN LINUX: Image de la cache
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
            private void DrawPreviewCube(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, ref Filtro filtro, bool medio, SKMatrix hdc, Rectangle region)   // EN LINUX: el lienzo de Skia y el estado del que llama; el filtro que pone se le queda al que llama (ref)
            {
                Rocola.Dibujo2D.Image ca, cb; float f; Rectangle area;   // EN LINUX: las portadas de la cache (Image, escrito entero)
                if (!ComputePreview(region, out ca, out cb, out f, out area)) return;
                SKBitmap cover = (cb == null || f <= 0.002f) ? PuenteDibujo.Mapa(ca) : BlendInto(ca, cb, f);   // EN LINUX: Image cover: el cubo (Cube.Draw, Form1.Pared.cs, ya traducido) pinta los puntos: los de la portada de la cache (Image de la capa: los mismos, sin copia, por el puente) o la mezcla (blendBmp, el mismo SKBitmap cada vez, como en XP)
                filtro = Filtro.Bilineal;   // EN LINUX: g.InterpolationMode = Bilinear (se le queda al que llama: ref)
                Cube.Draw(g, suave, copia, filtro, medio, area, cover, CubeAngle);   // EN LINUX: Cube.Draw(g, area, cover, CubeAngle): la traducida (Form1.Pared.cs), directo, con el lienzo y lo que pesa del estado (suavizado, copia, filtro y medio pixel); no le cambia nada al estado, como en XP
                DrawPreviewInfo(g, suave, ajuste, copia, medio, hdc, region, area);   // EN LINUX: el lienzo y su estado
            }

            // Texto de la vista previa (titulo, subtitulo y ayuda) debajo del cubo. Comun a CPU y GPU.
            private void DrawPreviewInfo(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, SKMatrix hdc, Rectangle region, Rectangle area)   // EN LINUX: el lienzo de Skia y el estado del que llama (y la matriz del HDC, la del texto de GDI); la llama tambien DrawExtraGpu (AcabadoGpu.cs), directo
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
                if (!CintaVistaPrevia(g, ajuste, title, fPrevT, Color.White, new Rectangle(region.X, ty, region.Width, 28)))   // EN LINUX: la traducida (TextoMovilGpu.cs), directo, con el lienzo y su modo de letra
#endif
                TextoGdi.Escribe(g, hdc, title, fPrevT, new Rectangle(region.X, ty, region.Width, 28), Color.White, BanderasGdi.Centrado | BanderasGdi.PuntosAlFinal, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs), HorizontalCenter | EndEllipsis, con la matriz del HDC del que llama; sin suavizar con los modos SingleBitPerPixel del que llama
#if ROCOLA_GPU
                if (!CintaVistaPrevia(g, ajuste, sub, fPrevS, Color.FromArgb(215, 225, 245), new Rectangle(region.X, ty + 30, region.Width, 24)))   // EN LINUX: la traducida (TextoMovilGpu.cs), directo
#endif
                TextoGdi.Escribe(g, hdc, sub, fPrevS, new Rectangle(region.X, ty + 30, region.Width, 24), Color.FromArgb(215, 225, 245), BanderasGdi.Centrado | BanderasGdi.PuntosAlFinal, ajuste == AjusteLetra.SinSuavizar || ajuste == AjusteLetra.SinSuavizarAjustada);   // EN LINUX: TextRenderer.DrawText (Nativo/TextoGdi.cs); sin suavizar con los modos SingleBitPerPixel del que llama
                // Ayuda con ICONOS (mismo estilo que la barra de arriba), centrada.
                {
                    Rocola.Nativo.Letra hf = fPrevH;   // EN LINUX: Letra
                    bool oldsm = suave; suave = true;   // EN LINUX: SmoothingMode oldsm = g.SmoothingMode; g.SmoothingMode = AntiAlias: el estado en variables
                    AjusteLetra oldth = ajuste;   // EN LINUX: TextRenderingHint oldth = g.TextRenderingHint
                    ajuste = AjusteLetra.ClearTypeAjustada;   // EN LINUX: g.TextRenderingHint = ClearTypeGridFit
                    Color htx = Color.FromArgb(205, 214, 238);
                    Color yellow = Color.FromArgb(242, 206, 72), green = Color.FromArgb(72, 212, 112);
                    int hcy = region.Bottom - 14;
                    int dd = 15; float gap = 7, grp = 26;
                    string t1 = "volver al teclado", t2 = "elegir";
                    SizeF s1 = TextoGdiPlus.Mide(t1, hf, ajuste, new SizeF(0, 0), null), s2 = TextoGdiPlus.Mide(t2, hf, ajuste, new SizeF(0, 0), null);   // EN LINUX: g.MeasureString(t, hf): sin caja y sin formato (Nativo/TextoGdiPlus.cs), con el modo de letra de ahora
                    float total = dd + gap + s1.Width + grp + dd + gap + s2.Width;
                    float hx = region.X + (region.Width - total) / 2f;
                    hx = CtrlDirLeft(g, suave, ajuste, copia, medio, hx, hcy, dd, yellow) + gap;   // boton IZQUIERDA   // EN LINUX: CtrlDirLeft (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                    hx = CtrlTxt(g, suave, ajuste, copia, medio, hf, t1, hx, hcy, htx) + grp;   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
                    hx = CtrlDot(g, suave, ajuste, copia, medio, hx, hcy, dd, green) + gap;         // boton PONER (verde)   // EN LINUX: CtrlDot (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel); no le cambia nada al estado, como en XP
                    hx = CtrlTxt(g, suave, ajuste, copia, medio, hf, t2, hx, hcy, htx);   // EN LINUX: CtrlTxt (Form1.Pared.cs): la traducida, directo, con el lienzo y el estado del que llama (suavizado, modo de letra, copia y medio pixel) y la Letra (la misma); no le cambia nada al estado, como en XP
                    suave = oldsm; ajuste = oldth;   // EN LINUX: g.SmoothingMode = oldsm; g.TextRenderingHint = oldth (las variables vuelven: al que llama no le cambia nada, como en XP)
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

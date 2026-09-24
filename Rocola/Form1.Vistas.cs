// LOS CONTROLES DIBUJADOS A MANO: lista, letras, encabezado, pastillas y progreso
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

        // BARRA DE AVANCE plana (la de Windows en XP es verde con rayas y no pega con nada).
        private sealed class BarraAvance : Control
        {
            public float Valor;   // 0..1
            public Color Acento = Color.FromArgb(255, 170, 90);
            public BarraAvance()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent; Height = 8;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(4, Height));
                using (GraphicsPath pa = RoundedPath(r, Height / 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 46, 82)))
                    g.FillPath(b, pa);
                float f = Valor; if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
                int w = (int)(r.Width * f);
                if (w > 2)
                    using (GraphicsPath pa = RoundedPath(new Rectangle(0, 0, w, r.Height), Height / 2))
                    using (SolidBrush b = new SolidBrush(Acento))
                        g.FillPath(b, pa);
            }
        }

        // DONDE CAE lo tecleado dentro de un texto, ignorando mayusculas y acentos, pero devolviendo la
        // posicion en el texto ORIGINAL. Normalizar el texto entero y buscar ahi NO vale: al quitar los acentos
        // las posiciones se corren y el resaltado caeria una letra mas alla de donde debe.
        private sealed class TextoTramo
        {
            public string Limpio;
            public int[] Mapa;
        }
        private sealed class CacheTramos
        {
            public readonly Dictionary<string, TextoTramo> Textos = new Dictionary<string, TextoTramo>(StringComparer.Ordinal);
            public readonly Queue<string> Orden = new Queue<string>();
            public int Bytes;
        }

        // ==========  TIRA DE LETRAS (con brillo animado)  ==========
        // Barra de paginas de la lista de canciones: "Pagina X de Y" al centro, y a los lados las
        // instrucciones VARIABLES con flechas DIBUJADAS (la fuente de XP no trae flechas unicode):
        //   [<] pagina 1        Pagina 2 de 3        pagina 3 [>]
        private class SongPagerBar : Control
        {
            private int pagina = 0; // pagina actual (desde 0)
            public int Pages = 1;   // total de paginas
            private Font pgFont;    // fuente cacheada (esta barra se redibuja al paginar / con el brillo)

            // "Pagina 2 de 5" cambiaba de golpe mientras la lista de debajo se deslizaba, y las dos cosas
            // contaban lo mismo yendo cada una por su lado. Ahora el letrero acompana a la lista: el numero
            // viejo se va hacia el mismo lado y el nuevo entra detras, con la MISMA curva y la misma duracion.
            private int animTick = -1; private int pagAnt = 0; private float animDir;
            public int Page
            {
                get { return pagina; }
                set
                {
                    if (value == pagina) return;
                    animDir = (value > pagina) ? 1f : -1f;
                    pagAnt = pagina; pagina = value; animTick = Environment.TickCount;
                }
            }
            public bool Animando { get { return animTick >= 0 && Environment.TickCount - animTick < SongListView.SlideMs; } }
            public float Paso
            {
                get
                {
                    if (animTick < 0) return 1f;
                    int dt = Environment.TickCount - animTick;
                    return (dt >= SongListView.SlideMs) ? 1f : (dt / (float)SongListView.SlideMs);
                }
            }
            // Cuanto falta por recorrer, con la curva de la lista. Cero = ya llego.
            private float FaltaAhora()
            {
                if (animTick < 0) return 0f;
                int dt = Environment.TickCount - animTick;
                if (dt < 0 || dt >= SongListView.SlideMs) { animTick = -1; return 0f; }
                return SongListView.CurvaLista(dt);
            }
            // Al abrir un disco el letrero se pone donde toque, sin viaje.
            public void PonSinAnimar(int p) { pagina = p; animTick = -1; }

            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref pgFont); } }
                finally { base.Dispose(disposing); }
            }

            public SongPagerBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            // Dibuja la barra de paginas en cualquier Graphics (el control, o la textura de UI del modo GPU).
            public void RenderContent(Graphics g)
            {
                int w = Width, h = Height;
                g.FillRectangle(CB(BackColor), 0, 0, w, h);   // pincel reusable (mutado)
                if (Pages <= 1 || w < 60 || h < 10) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int cy = h / 2;
                Color txt = Color.FromArgb(228, 236, 252);
                Color dim = Color.FromArgb(175, 188, 215);
                Color yellow = Color.FromArgb(242, 206, 72);   // mismo amarillo que la cruceta izq/der
                if (pgFont == null) pgFont = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold);
                Font f = pgFont;
                {
                    // Centro: Pagina X de Y. Mientras se hojea, el numero viejo sale y el nuevo entra; el
                    // viaje es corto (un tercio del ancho) porque lo que tiene que leerse es el numero, no
                    // el movimiento. Las flechas de los lados NO viajan: son botones y deben quedarse quietas.
                    float falta = FaltaAhora();
                    string c = "Pagina " + (pagina + 1) + " de " + Pages;
                    SizeF cs = g.MeasureString(c, f);
                    float viaje = w / 3f;
                    if (falta > 0.001f)
                    {
                        string cAnt = "Pagina " + (pagAnt + 1) + " de " + Pages;
                        SizeF as_ = g.MeasureString(cAnt, f);
                        int aAnt = (int)(255f * falta); if (aAnt > 255) aAnt = 255; else if (aAnt < 0) aAnt = 0;
                        g.DrawString(cAnt, f, CB(Color.FromArgb(aAnt, txt)),
                                     (w - as_.Width) / 2f - animDir * viaje * (1f - falta), cy - as_.Height / 2f);
                    }
                    int aNue = (int)(255f * (1f - falta)); if (aNue > 255) aNue = 255; else if (aNue < 0) aNue = 0;
                    g.DrawString(c, f, CB(Color.FromArgb(aNue, txt)),
                                 (w - cs.Width) / 2f + animDir * viaje * falta, cy - cs.Height / 2f);

                    // Izquierda: [<] pagina anterior (solo si existe)
                    if (Page > 0)
                    {
                        float x = 4;
                        x = CtrlDirLeft(g, x, cy, 11, yellow) + 5;
                        CtrlTxt(g, f, "pagina " + Page, x, cy, dim);
                    }
                    // Derecha: pagina siguiente [>] (solo si existe)
                    if (Page < Pages - 1)
                    {
                        string r = "pagina " + (Page + 2);
                        SizeF rs = g.MeasureString(r, f);
                        float xr = w - 4 - 11 - 5 - rs.Width;
                        xr = CtrlTxt(g, f, r, xr, cy, dim) + 5;
                        CtrlDirRight(g, xr, cy, 11, yellow);
                    }
                }
            }
        }

        private class LetterBar : Control
        {
            private SolidBrush pulseBrush; private Pen pulsePen;                       // resaltado de letra (reusables)
            private GraphicsPath pulsePath; private RectangleF pulseBox = RectangleF.Empty;  // trayectoria cacheada
            private GraphicsPath goldPath; private RectangleF goldBox = RectangleF.Empty;    // marcador TOP dorado
            private SolidBrush goldBrush; private Pen goldPen; private SolidBrush goldTextBrush;
            private Bitmap stripCache; private int scW = -1, scH = -1; private char scCur = '\0';
            private string scLetters; private int scAvailN = -2; private int scDatos = 0;     // firma de la tira cacheada
            private Font fMain, fVideo, fNum;
            private float fontSlot = -1f, fontAlto = -1f;   // cuerpos elegidos, y para que hueco de ancho y alto
            private string lblVideo = "VIDEO";                          // palabra, o simbolo si no cabe
            private StringFormat sfCen; private SolidBrush whiteB;
            public string Letters = "\u2605#ABCDEFGHIJKLMNOPQRSTUVWXYZ\u25BA";
            // AL CAMBIAR DE LETRA la capsula se DESLIZA de la casilla que dejas a la que eliges, en vez de
            // aparecer de golpe al otro lado de la barra. Dura poco y frena al llegar: eso es lo que hace que
            // se lea como un movimiento y no como un parpadeo.
            private const int AnimMs = 190;
            private char cur = '#', animDesde = '\0';
            private readonly System.Diagnostics.Stopwatch animReloj = new System.Diagnostics.Stopwatch();
            public char Current
            {
                get { return cur; }
                set
                {
                    if (value == cur) return;
                    // Solo se desliza si las dos casillas existen; si no, cambio seco (que es lo que habia).
                    if (Letters != null && Letters.IndexOf(cur) >= 0 && Letters.IndexOf(value) >= 0)
                    { animDesde = cur; animReloj.Reset(); animReloj.Start(); }
                    cur = value;
                }
            }
            // 0 = acaba de salir, 1 = ya llego. Fuera de la animacion vale 1.
            public float AnimPaso
            {
                get
                {
                    if (!animReloj.IsRunning) return 1f;
                    double ms = animReloj.Elapsed.TotalMilliseconds;
                    if (ms >= AnimMs) { animReloj.Stop(); return 1f; }
                    float t = (float)(ms / AnimMs);
                    float q = 1f - t;
                    return 1f - q * q * q;   // frena al llegar
                }
            }
            // Ojo: NO vale con preguntarle al cronometro si anda, porque quien lo para es la lectura de
            // AnimPaso. Si nadie la ha leido todavia, el cronometro sigue andando aunque el tiempo ya haya
            // pasado, y entonces "hay animacion" mentiria (y en modo GPU obligaria a rehacer la textura para
            // siempre). Se mira el tiempo, que es la verdad.
            public bool AnimViva
            {
                get { return animReloj.IsRunning && animReloj.Elapsed.TotalMilliseconds < AnimMs; }
            }
            public double Phase = 0;
            public HashSet<char> Available;
            // Cuantas canciones hay detras de cada letra, y de que color es el genero que MANDA en ella. Form1
            // los REEMPLAZA enteros cuando el rastreo de fondo aprende mas generos (nunca los toca por dentro),
            // asi que aqui se pueden leer sin candado: o se ve el diccionario viejo o se ve el nuevo, entero.
            public Dictionary<char, int> Counts;
            public Dictionary<char, Color> Tints;
            public Action<char> OnPick;

            public LetterBar()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int n = Letters.Length;
                if (n == 0 || Width <= 0) { base.OnMouseClick(e); return; }
                // CON DECIMALES, igual que se DIBUJA. Con division entera la casilla en la que crees pinchar y
                // la que la barra dibuja se iban separando hacia la derecha -en 1024 casi un tercio de casilla
                // al final, y con un video al lado MAS DE UNA CASILLA ENTERA-: pinchabas una letra y te llevaba
                // a la de al lado.
                float slot = (float)Width / n;
                int i = (int)(e.X / slot);
                if (i < 0) i = 0; else if (i >= n) i = n - 1;
                if (OnPick != null) OnPick(Letters[i]);
                base.OnMouseClick(e);
            }

            private void EnsureRes()
            {
                if (whiteB == null) whiteB = new SolidBrush(Color.White);
                if (sfCen == null)
                {
                    sfCen = new StringFormat();
                    sfCen.Alignment = StringAlignment.Center; sfCen.LineAlignment = StringAlignment.Center;
                    sfCen.FormatFlags = StringFormatFlags.NoWrap;   // una etiqueta JAMAS se parte en renglones
                }
            }

            // ---------- REPARTO DE ALTO ----------
            // Debajo de cada letra va su numero de canciones, pero solo si hay sitio (con un video al lado la
            // barra se queda a la mitad de ancho y ahi no se leeria) Y solo si de verdad hay algun numero que
            // ensenar: reservarle el renglon a un numero que nunca llega deja las letras subidas, pegadas al
            // techo de la barra y con un hueco vacio debajo.
            private bool HayCuenta(float slot, int h)
            {
                if (Counts == null || slot < 21f || h < 34 || Letters == null) return false;
                return HayNumeros();
            }
            private float AltoCuenta(float slot, int h) { return HayCuenta(slot, h) ? h * 0.30f : 0f; }

            // EL CUERPO DE LETRA SE ELIGE SEGUN EL HUECO QUE HAY. "VIDEO" mide 36 px y la casilla 34 con la
            // pantalla entera; y con un video puesto al lado la barra se encoge a la mitad, asi que no cabe.
            // Antes se dibujaba con un cuerpo fijo dentro de su casilla y GDI+ lo partia en varios renglones,
            // que ademas no cabian de alto: se veia "VI / D / EO" recortado. Ahora se busca el cuerpo mas
            // grande con el que QUEPA, y como el formato ya no deja partir renglon, no puede volver a pasar.
            private void AjustaFuentes(Graphics g, float slot, int h)
            {
                float altoL = h - AltoCuenta(slot, h);
                if (fMain != null && Math.Abs(fontSlot - slot) < 0.5f && Math.Abs(fontAlto - altoL) < 0.5f) return;
                if (fMain != null) fMain.Dispose();
                if (fVideo != null) fVideo.Dispose();
                if (fNum != null) { fNum.Dispose(); fNum = null; }
                float hueco = slot - 2f;
                // EL CUERPO SALE DEL ALTO DE LA BARRA, no de un numero fijo. Con 40 px de barra da los 10.5
                // puntos de siempre, asi que en 1024x768 no cambia nada; pero en una pantalla ancha, donde la
                // barra crece, las letras crecen con ella en vez de quedarse chiquitas y perdidas en medio de
                // una casilla de 100 pixeles.
                float cuerpo = altoL * 0.375f;
                fMain = CuerpoQueQuepa(g, "W", hueco, cuerpo);
                // La estrella no lleva cuerpo de letra: se dibuja, y su tamano sale de la casilla. Sale del
                // ALTO -como el de las letras- y se recorta si la casilla es tan estrecha que no cabria.
                radioEstrella = altoL * 0.25f;
                if (radioEstrella > hueco * 0.50f) radioEstrella = hueco * 0.50f;
                if (radioEstrella < 2f) radioEstrella = 2f;
                Font fPal = CuerpoQueQuepa(g, "VIDEO", hueco, cuerpo * 0.72f);
                if (g.MeasureString("VIDEO", fPal).Width <= hueco)
                {
                    lblVideo = "VIDEO"; fVideo = fPal;
                }
                else
                {
                    // Ni bajando el cuerpo cabe la palabra: ahi se pone el SIMBOLO, que ocupa lo mismo que una
                    // letra, se lee igual de lejos y dice lo mismo.
                    fPal.Dispose();
                    lblVideo = "\u25BA";
                    fVideo = CuerpoQueQuepa(g, "\u25BA", hueco, cuerpo);
                }
                if (AltoCuenta(slot, h) > 0f) fNum = CuerpoQueQuepa(g, "8888", hueco, cuerpo * 0.62f);
                fontSlot = slot; fontAlto = altoL;
            }
            // El cuerpo pedido, encogido de medio en medio lo justo para que 'muestra' quepa a lo ancho de su
            // casilla. Nunca baja de 5 puntos: por debajo no se leeria de todos modos.
            private Font CuerpoQueQuepa(Graphics g, string muestra, float hueco, float cuerpo)
            {
                if (!(cuerpo > 5f)) cuerpo = 5f;
                for (; ; )
                {
                    Font f = new Font(this.Font.FontFamily, cuerpo, FontStyle.Bold);
                    if (cuerpo <= 5f || g.MeasureString(muestra, f).Width <= hueco) return f;
                    f.Dispose();
                    cuerpo -= 0.5f;
                }
            }

            // ---------- COLOR DE CADA LETRA ----------
            // El del genero que manda dentro de ella, pero SUBIDO hacia el blanco: el color puro de un genero
            // oscuro (el metal es rojo sangre, el gotico morado profundo) no se leeria en una franja de 40
            // pixeles vista de pie a dos metros. Asi conserva la familia y se sigue leyendo de lejos.
            private Color TonoLetra(char ch, bool hay)
            {
                if (!hay) return Color.FromArgb(104, 114, 144);          // sin canciones: apagada, como siempre
                Color g;
                if (Tints == null || !Tints.TryGetValue(ch, out g)) return Color.FromArgb(232, 238, 250);
                return Color.FromArgb(255,
                    g.R + (int)((255 - g.R) * 0.45f),
                    g.G + (int)((255 - g.G) * 0.45f),
                    g.B + (int)((255 - g.B) * 0.45f));
            }

            // Firma de los datos que se pintan en la tira: si cambia, hay que rehacerla. El rastreo de fondo
            // va aprendiendo generos y contando canciones, y la barra tiene que enterarse.
            public int DatosSig() { return FirmaDatos(); }

            private Dictionary<char, int> fCnt; private Dictionary<char, Color> fTin;
            private int fVal, fNCnt = -1, fNTin = -1; private bool fOk;
            private int FirmaDatos()
            {
                // Se recorrian 29 letras con dos busquedas de diccionario cada una, DOS veces por cuadro: al
                // dibujar y al firmar la textura de la GPU. Y el resultado casi nunca cambia -solo cuando el
                // paseo de fondo aprende algo-. Como Form1 REEMPLAZA los diccionarios enteros (nunca los toca
                // por dentro), basta con mirar si siguen siendo los mismos objetos y del mismo tamano.
                int nC = (Counts == null) ? -1 : Counts.Count, nT = (Tints == null) ? -1 : Tints.Count;
                if (fOk && ReferenceEquals(fCnt, Counts) && ReferenceEquals(fTin, Tints) && fNCnt == nC && fNTin == nT)
                    return fVal;
                fVal = CalculaFirma();
                fCnt = Counts; fTin = Tints; fNCnt = nC; fNTin = nT; fOk = true;
                return fVal;
            }
            private int CalculaFirma()
            {
                int f = 17;
                for (int i = 0; i < Letters.Length; i++)
                {
                    char c = Letters[i];
                    int n = 0; if (Counts != null) Counts.TryGetValue(c, out n);
                    int t = 0; Color col;
                    if (Tints != null && Tints.TryGetValue(c, out col)) t = col.ToArgb();
                    f = f * 31 + n; f = f * 31 + t;
                }
                return f;
            }

            // Lo mismo para "hay algun numero que ensenar": tambien recorria las 29 letras, y se pregunta
            // varias veces por cuadro (al repartir el alto, al elegir los cuerpos y al situar los halos).
            private bool hcVal; private bool hcOk;
            private Dictionary<char, int> hcCnt; private int hcN = -1;
            private bool HayNumeros()
            {
                int nC = (Counts == null) ? -1 : Counts.Count;
                if (hcOk && ReferenceEquals(hcCnt, Counts) && hcN == nC) return hcVal;
                bool r = false;
                if (Counts != null && Letters != null)
                    for (int i = 0; i < Letters.Length; i++)
                    {
                        int n;
                        if (Counts.TryGetValue(Letters[i], out n) && n > 0) { r = true; break; }
                    }
                hcVal = r; hcCnt = Counts; hcN = nC; hcOk = true;
                return r;
            }

            // ---------- EL RIEL DE VIDRIO ----------
            // La fila se apoya en un riel oscuro con un brillo arriba: deja de flotar sobre el fondo y se lee
            // como una pieza de la maquina, no como texto suelto encima del cielo.
            private void Riel(Graphics g, int w, int h)
            {
                RectangleF r = new RectangleF(0, 1, w - 1, h - 2);
                using (GraphicsPath p = RoundRectF(r, 12))
                {
                    using (LinearGradientBrush lb = new LinearGradientBrush(
                               new RectangleF(r.X, r.Y, r.Width, r.Height + 1),
                               Color.FromArgb(96, 34, 46, 88), Color.FromArgb(64, 14, 20, 46), 90f))
                        g.FillPath(lb, p);
                    using (Pen bo = new Pen(Color.FromArgb(58, 140, 180, 255), 1f)) g.DrawPath(bo, p);
                }
                float hb = h * 0.32f;
                using (GraphicsPath p2 = RoundRectF(new RectangleF(3, 2.5f, w - 7, hb), 7))
                using (LinearGradientBrush hl = new LinearGradientBrush(new RectangleF(3, 2.5f, w - 7, hb + 1),
                           Color.FromArgb(34, 190, 215, 255), Color.FromArgb(0, 190, 215, 255), 90f))
                    g.FillPath(hl, p2);
            }

            // LA ESTRELLA DE "LO MAS SONADO" SE DIBUJA, NO SE ESCRIBE.
            //
            // Antes se escribia, como se escribe una A: se le pedia a la letra de Windows el simbolo "estrella
            // negra". Y en Windows XP eso no funciona. La rocola pide Segoe UI, que en XP no existe, Windows le
            // da Tahoma, y Tahoma de XP trae el juego de simbolos WGL4, donde la estrella NO esta. Cuando a
            // Windows le piden un simbolo que no tiene, pinta un rectangulo vacio: eso era lo que se veia en la
            // rocola del bar, un rectangulo en el sitio de la estrella. En la otra maquina si se veia bien, que
            // es por lo que no se noto.
            //
            // Dibujada con lineas se ve igual en cualquier maquina, no depende de las letras que tenga Windows
            // instaladas, y ademas cuesta menos que escribir texto.
            private static readonly PointF[] estrellaUnidad = FormaEstrella();
            private readonly PointF[] estrellaPts = new PointF[10];   // se rellena y se reusa: cero basura por cuadro
            private float radioEstrella = 6f;
            private static PointF[] FormaEstrella()
            {
                // Cinco puntas: se va alternando el radio de fuera y el de dentro cada 36 grados. La proporcion
                // 0.382 es la de siempre, la misma que usan las estrellas de las letras de Windows.
                PointF[] p = new PointF[10];
                const double dentro = 0.382;
                for (int i = 0; i < 10; i++)
                {
                    double a = (-90 + i * 36) * Math.PI / 180.0;
                    double r = ((i & 1) == 0) ? 1.0 : dentro;
                    // El 0.09549 baja la estrella para que quede centrada POR SU DIBUJO: la punta de arriba
                    // llega mas lejos que las dos patas de abajo, asi que sin esto se veria alta.
                    p[i] = new PointF((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r + 0.09549));
                }
                return p;
            }
            // Rellena la estrella centrada en 'r' y corrida (dx,dy). Es el reemplazo exacto del texto de antes.
            private void Estrella(Graphics g, Brush b, RectangleF r, float dx, float dy)
            {
                float cx = r.X + r.Width * 0.5f + dx, cy = r.Y + r.Height * 0.5f + dy, ra = radioEstrella;
                for (int i = 0; i < 10; i++)
                    estrellaPts[i] = new PointF(cx + estrellaUnidad[i].X * ra, cy + estrellaUnidad[i].Y * ra);
                g.FillPolygon(b, estrellaPts);
            }
            // La estrella con el mismo RELIEVE que las letras: sombra debajo y filo claro arriba.
            private void EstrellaGrabada(Graphics g, SolidBrush b, RectangleF r, Color tono, bool hay)
            {
                b.Color = Color.FromArgb(hay ? 150 : 70, 0, 0, 0);
                Estrella(g, b, r, 0f, 1.4f);
                b.Color = hay ? Color.FromArgb(66, 190, 215, 255) : Color.FromArgb(26, 150, 170, 210);
                Estrella(g, b, r, 0f, -1.1f);
                b.Color = tono;
                Estrella(g, b, r, 0f, 0f);
            }

            // Una letra con RELIEVE: sombra debajo y filo claro arriba, como grabada en cristal.
            private void Grabada(Graphics g, SolidBrush b, string txt, Font f, RectangleF r, Color tono, bool hay)
            {
                b.Color = Color.FromArgb(hay ? 150 : 70, 0, 0, 0);
                g.DrawString(txt, f, b, new RectangleF(r.X, r.Y + 1.4f, r.Width, r.Height), sfCen);
                b.Color = hay ? Color.FromArgb(66, 190, 215, 255) : Color.FromArgb(26, 150, 170, 210);
                g.DrawString(txt, f, b, new RectangleF(r.X, r.Y - 1.1f, r.Width, r.Height), sfCen);
                b.Color = tono;
                g.DrawString(txt, f, b, r, sfCen);
            }

            // Dibuja el riel y TODAS las letras (menos la actual y la estrella, que van en vivo) en una tira
            // reusable. Solo se rehace al cambiar de letra, de tamano, de disponibles o de datos.
            private void RebuildStrip(int w, int h, int n, float slot)
            {
                if (stripCache == null || stripCache.Width != w || stripCache.Height != h)
                {
                    if (stripCache != null) stripCache.Dispose();
                    stripCache = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                }
                float bajo = AltoCuenta(slot, h);
                float altoL = h - bajo;
                using (Graphics g = Graphics.FromImage(stripCache))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Riel(g, w, h);
                    // Una raya finisima entre casilla y casilla: ORDENA la fila sin llamar la atencion.
                    using (Pen sep = new Pen(Color.FromArgb(24, 150, 175, 225)))
                        for (int i = 1; i < n; i++)
                            g.DrawLine(sep, i * slot, 10, i * slot, h - 10);
                    for (int i = 0; i < n; i++)
                    {
                        char ch = Letters[i];
                        bool isTop = (ch == TopMark);
                        bool avail = (Available == null) || Available.Contains(ch);
                        RectangleF rL = new RectangleF(i * slot, 0, slot, altoL);
                        if (ch != Current && !(isTop && avail))   // la actual y la estrella encendida van en vivo
                        {
                            bool isVideo = (ch == VideoMark);
                            if (isTop) EstrellaGrabada(g, tb, rL, Color.FromArgb(122, 116, 96), avail);
                            else Grabada(g, tb, isVideo ? lblVideo : ch.ToString(), isVideo ? fVideo : fMain,
                                         rL, TonoLetra(ch, avail), avail);
                        }
                        if (bajo > 0f && fNum != null) Cuenta(g, tb, ch, i, slot, h, bajo, avail);
                    }
                }
            }

            // El numero de canciones que hay detras de una letra, debajo de ella y en su mismo color pero mas
            // apagado: el cliente ve de un vistazo donde esta lo gordo y deja de entrar a letras casi vacias.
            private void Cuenta(Graphics g, SolidBrush b, char ch, int i, float slot, int h, float bajo, bool avail)
            {
                int cuantas = 0;
                if (Counts == null || !Counts.TryGetValue(ch, out cuantas) || cuantas <= 0) return;
                Color tono = TonoLetra(ch, avail);
                b.Color = Color.FromArgb(avail ? 185 : 105,
                                         (tono.R * 3 + 90) / 4, (tono.G * 3 + 105) / 4, (tono.B * 3 + 150) / 4);
                g.DrawString(TextoCuenta(cuantas), fNum, b, new RectangleF(i * slot, h - bajo - 1f, slot, bajo), sfCen);
            }
            // El hueco esta medido para CUATRO cifras. Una biblioteca con mas de 9999 canciones en una sola
            // letra escribiria un numero mas largo del que cabe, asi que a partir de ahi se dice en miles.
            private static string TextoCuenta(int n)
            {
                if (n < 10000) return n.ToString();
                int miles = n / 1000;
                return (miles > 999 ? 999 : miles) + "k";
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics, false); }
            // Casilla de la letra ACTUAL (para el pulso por GPU): centro/radio en coords del control.
            // false si no aplica (la actual es TOP, que lleva su propio halo dorado, o no esta en la tira).
            public bool GetCurrentGlow(out float cx, out float cy, out float r)
            {
                cx = cy = r = 0f;
                int n = Letters.Length; if (n == 0 || Width < 4 || Height < 4) return false;
                if (Current == TopMark) return false;
                int i = Letters.IndexOf(Current); if (i < 0) return false;
                float slot = (float)Width / n;
                float altoL = Height - AltoCuenta(slot, Height);
                cx = XCapsula(i, slot) + slot / 2f; cy = altoL / 2f;   // mientras se desliza, el halo va con ella
                r = Math.Max(slot, altoL) * 0.75f;
                return true;
            }
            // Casilla del marcador TOP (para su dorado pulsante por GPU); false si TOP no esta disponible.
            public bool GetTopGlow(out float cx, out float cy, out float r)
            {
                cx = cy = r = 0f;
                int n = Letters.Length; if (n == 0 || Width < 4 || Height < 4) return false;
                if (Available == null || !Available.Contains(TopMark)) return false;
                int i = Letters.IndexOf(TopMark); if (i < 0) return false;
                float slot = (float)Width / n;
                float altoL = Height - AltoCuenta(slot, Height);
                cx = i * slot + slot / 2f; cy = altoL / 2f;
                r = Math.Max(slot, altoL) * 0.75f;
                return true;
            }
            // QUE TROZOS DE LA BARRA CAMBIAN DE UN CUADRO A OTRO.
            //
            // Solo dos laten: la casilla elegida y la estrellita de LO MAS SONADO. Las otras veintiocho letras
            // viven en una tira ya horneada que no se mueve. Repintar la barra ENTERA por esos dos latidos
            // costaba el triple de lo necesario -0.35 ms contra 0.11 en una barra de 1024 px- y eso son
            // veinticuatro veces por segundo, todo el rato que la rocola esta encendida.
            //
            // Devuelve false cuando hay que repintarla entera: mientras la capsula viaja de una letra a otra
            // cambia media barra, y ahi no hay atajo que valga.
            public bool ZonasLatido(out Rectangle uno, out Rectangle dos)
            {
                uno = Rectangle.Empty; dos = Rectangle.Empty;
                if (Width < 8 || Height < 8) return false;
                if (AnimViva) return false;
                float cx, cy, r;
                if (GetCurrentGlow(out cx, out cy, out r)) uno = CajaLatido(cx, r);
                if (GetTopGlow(out cx, out cy, out r)) dos = CajaLatido(cx, r);
                return uno.Width > 0 || dos.Width > 0;
            }
            private Rectangle CajaLatido(float cx, float r)
            {
                int x = (int)(cx - r) - 2, an = (int)(r * 2) + 5;   // con aire: el halo se sale de la casilla
                if (x < 0) { an += x; x = 0; }
                if (x + an > Width) an = Width - x;
                return (an < 1) ? Rectangle.Empty : new Rectangle(x, 0, an, Height);
            }

            // La X de la casilla elegida AHORA MISMO: mientras dura el deslizamiento va entre la casilla que
            // se deja y la que se elige; el resto del tiempo es la de destino y punto.
            private float XCapsula(int iDestino, float slot)
            {
                float paso = AnimPaso;
                if (paso >= 1f) return iDestino * slot;
                int iDe = (Letters != null) ? Letters.IndexOf(animDesde) : -1;
                if (iDe < 0) return iDestino * slot;
                return (iDe + (iDestino - iDe) * paso) * slot;
            }

            public void RenderContent(Graphics g) { RenderContent(g, false); }
            // Dibuja la barra de letras en cualquier Graphics (el control, o la textura de UI del modo GPU).
            // forGpu=true: brillos FIJOS (mitad del pulso) en la textura; el pulso vivo lo ponen los halos por
            // quad (GetCurrentGlow/GetTopGlow) -> la textura de UI ya no se rehace ~13 veces/seg por esta barra.
            public void RenderContent(Graphics g, bool forGpu)
            {
                int w = Width, h = Height, n = (Letters == null) ? 0 : Letters.Length;
                if (w < 4 || h < 4 || n == 0) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float slot = (float)w / n;
                double pulse = forGpu ? 0.45 : 0.5 + 0.5 * Math.Sin(Phase * 2.2);
                EnsureRes();
                AjustaFuentes(g, slot, h);
                float bajo = AltoCuenta(slot, h);
                float altoL = h - bajo;
#if ROCOLA_GPU
                if (forGpu && LupaGpu) { RenderLupa(g, w, h, n, slot, altoL, bajo); return; }   // Ctrl+F3 -> Pantalla (abajo)
#endif

                // TIRA CACHEADA: el riel y las ~28 letras solo se redibujan al cambiar letra/tamano/datos.
                // Por cuadro: copiar la tira + la letra ACTUAL con su capsula + la estrella (antes: ~28 textos).
                int availN = (Available == null) ? -1 : Available.Count;
                int datos = FirmaDatos();
                if (stripCache == null || scW != w || scH != h || scCur != Current || scLetters != Letters
                    || scAvailN != availN || scDatos != datos)
                {
                    RebuildStrip(w, h, n, slot);
                    scW = w; scH = h; scCur = Current; scLetters = Letters; scAvailN = availN; scDatos = datos;
                }
                g.DrawImageUnscaled(stripCache, 0, 0);

                // LA CASILLA ELEGIDA: una capsula de NEON, del mismo material que la aurora del fondo -halo por
                // fuera, contorno encendido y el centro casi vacio- en vez de una caja azul plana.
                for (int i = 0; i < n; i++)
                {
                    char ch = Letters[i];
                    if (ch != Current || ch == TopMark) continue;   // TOP lo dibuja el bloque dorado
                    bool isVideo = (ch == VideoMark);
                    string lbl = isVideo ? lblVideo : ch.ToString();
                    Font useF = isVideo ? fVideo : fMain;
                    float paso = AnimPaso, xCap = XCapsula(i, slot);
                    RectangleF slotRect = new RectangleF(i * slot, 0, slot, altoL);
                    // Mientras la capsula viaja, la letra de destino no puede quedarse en blanco: la tira
                    // cacheada la deja fuera (es la elegida), asi que aqui se dibuja con su color normal y se
                    // va poniendo blanca conforme la capsula llega.
                    if (paso < 1f)
                    {
                        // Se reusa la brocha de siempre: Grabada le pone el color que necesita, y mas abajo se
                        // le vuelve a poner el suyo antes de usarla. Una brocha nueva por cuadro seria basura
                        // para el recolector justo mientras algo se esta moviendo.
                        bool hayD = (Available == null) || Available.Contains(ch);
                        Grabada(g, whiteB, lbl, useF, slotRect, TonoLetra(ch, hayD), hayD);
                    }
                    RectangleF box = new RectangleF(xCap + 3.5f, 3.5f, slot - 7f, altoL - 7f);
                    if (pulsePath == null || box != pulseBox)   // misma casilla -> misma trayectoria
                    {
                        if (pulsePath != null) pulsePath.Dispose();
                        pulsePath = RoundRectF(box, 9);
                        pulseBox = box;
                    }
                    if (pulseBrush == null) pulseBrush = new SolidBrush(Color.Black);
                    if (pulsePen == null) pulsePen = new Pen(Color.Black, 2.6f);
                    pulsePen.LineJoin = LineJoin.Round;
                    for (int k = 5; k >= 1; k--)   // el halo: pasadas cada vez mas anchas y mas tenues
                    {
                        pulsePen.Color = Color.FromArgb(9 + (int)(pulse * 7 * (6 - k)), 120, 190, 255);
                        pulsePen.Width = k * 2.4f;
                        g.DrawPath(pulsePen, pulsePath);
                    }
                    pulseBrush.Color = Color.FromArgb(26 + (int)(pulse * 30), 70, 150, 240);
                    g.FillPath(pulseBrush, pulsePath);
                    pulsePen.Color = Color.FromArgb(120 + (int)(pulse * 70), 130, 200, 255); pulsePen.Width = 2.6f;
                    g.DrawPath(pulsePen, pulsePath);
                    pulsePen.Color = Color.FromArgb(215 + (int)(pulse * 40), 225, 242, 255); pulsePen.Width = 1.1f;
                    g.DrawPath(pulsePen, pulsePath);
                    whiteB.Color = (paso >= 1f) ? Color.White : Color.FromArgb((int)(255 * paso), 255, 255, 255);
                    g.DrawString(lbl, useF, whiteB, slotRect, sfCen);
                    break;
                }

                // LA ESTRELLITA DORADA de "lo mas sonado": siempre pulsando para llamar la atencion del
                // cliente (solo si hay algo dentro). Se dibuja en vivo este o no seleccionada.
                int topI = Letters.IndexOf(TopMark);
                if (topI >= 0 && Available != null && Available.Contains(TopMark))
                {
                    RectangleF gsl = new RectangleF(topI * slot, 0, slot, altoL);
                    double gpu = forGpu ? 0.5 : 0.5 + 0.5 * Math.Sin(Phase * 2.6);   // GPU: brillo FIJO en la textura (el pulso dorado lo pone el halo por quad, GetTopGlow)
                    bool topSel = (Current == TopMark);
                    if (goldTextBrush == null) goldTextBrush = new SolidBrush(Color.Black);
                    if (topSel)
                    {
                        // SELECCIONADA: capsula dorada, del mismo corte que la de neon de las letras.
                        RectangleF gbx = new RectangleF(XCapsula(topI, slot) + 3.5f, 3.5f, gsl.Width - 7f, altoL - 7f);
                        int ga = 80 + (int)(gpu * 150);
                        if (goldPath == null || gbx != goldBox) { if (goldPath != null) goldPath.Dispose(); goldPath = RoundRectF(gbx, 9); goldBox = gbx; }
                        if (goldBrush == null) goldBrush = new SolidBrush(Color.Black);
                        if (goldPen == null) goldPen = new Pen(Color.Black, 2.2f);
                        goldPen.LineJoin = LineJoin.Round;
                        goldBrush.Color = Color.FromArgb(ga, 255, 200, 40);
                        goldPen.Color = Color.FromArgb(235, 255, 228, 130); goldPen.Width = 2.2f;
                        g.FillPath(goldBrush, goldPath);
                        g.DrawPath(goldPen, goldPath);
                        goldTextBrush.Color = Color.White;
                        Estrella(g, goldTextBrush, gsl, 0f, 0f);
                    }
                    else
                    {
                        // NO seleccionada: brilla SOLO la estrella (sin caja, para no confundir con seleccion).
                        // halo dorado tenue (4 pasadas desplazadas) + la estrella dorada pulsante encima.
                        int hAlpha = 35 + (int)(gpu * 55);
                        goldTextBrush.Color = Color.FromArgb(hAlpha, 255, 205, 90);
                        Estrella(g, goldTextBrush, gsl, -1f, 0f);
                        Estrella(g, goldTextBrush, gsl, 1f, 0f);
                        Estrella(g, goldTextBrush, gsl, 0f, -1f);
                        Estrella(g, goldTextBrush, gsl, 0f, 1f);
                        int gb = 60 + (int)(gpu * 70); if (gb > 255) gb = 255;
                        goldTextBrush.Color = Color.FromArgb(255, 255, 210, gb);   // dorado pulsante brillante
                        Estrella(g, goldTextBrush, gsl, 0f, 0f);
                    }
                }
            }

            private static GraphicsPath RoundRectF(RectangleF r, int radius)
            {
                GraphicsPath p = new GraphicsPath();
                float d = radius * 2;
                if (d > r.Height) d = r.Height;
                if (d > r.Width) d = r.Width;
                if (d < 2) d = 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }
#if ROCOLA_GPU
            // ---------- LA LUPA (solo con tarjeta grafica: Ctrl+F3 -> Pantalla, PantallaVivaGpu.cs) ----------
            // La letra elegida y sus vecinas se dibujan mas grandes, como bajo una lupa que viaja con la capsula, y una
            // rayita debajo de cada letra dice cuanta musica tiene comparada con la que mas tiene. Va en su PROPIA
            // tira (lupaCache): la de siempre, la del dibujo por procesador, no se toca ni se rehace por esto. Y como
            // la barra por tarjeta se hornea en la textura de la interfaz, esto solo cuesta al cambiar de letra (y en
            // los cuadros que dura el deslizamiento de la capsula), nunca en un cuadro quieto.
            public bool LupaGpu;
            internal const float LupaMax = .45f;     // cuanto crece la del centro (un 45 %: mas, y la letra roza la capsula)
            internal const float LupaAncho = 1.6f;   // y a cuantas casillas llega: a dos ya casi no crece, a tres nada
            private Bitmap lupaCache; private int luW = -1, luH = -1, luAvailN = -2, luDatos, luCentro = int.MinValue;
            private char luCur = '\0'; private string luLetters;
            private readonly Dictionary<char, float> anchoLetra = new Dictionary<char, float>(); private Font anchoDe;

            // Cuanto crece una letra que esta a 'd' casillas del centro de la lupa (1 = nada).
            internal static float EscalaLupa(float d) { return 1f + LupaMax * (float)Math.Exp(-(d / LupaAncho) * (d / LupaAncho)); }
            // ...pero sin salirse de su casilla ni de la barra: 'ancho' es lo que mide la letra sin crecer.
            internal static float TopeLupa(float s, float ancho, float slot, float alto, float altoL)
            {
                if (ancho > 0f) s = Math.Min(s, Math.Max(1f, (slot - 3f) / ancho));
                if (alto > 0f) s = Math.Min(s, Math.Max(1f, (altoL - 3f) / alto));
                return s;
            }
            // Lo que mide de verdad el dibujo de una letra (sin el aire que le pone GDI+ a los lados). Se mide una vez
            // por letra y cuerpo.
            private float AnchoDe(Graphics g, string lbl, Font f)
            {
                if (!ReferenceEquals(anchoDe, fMain)) { anchoLetra.Clear(); anchoDe = fMain; }
                char k = (lbl.Length == 1) ? lbl[0] : '\u0001';   // "VIDEO" va por su cuenta
                float a;
                if (anchoLetra.TryGetValue(k, out a)) return a;
                a = g.MeasureString(lbl, f, PointF.Empty, StringFormat.GenericTypographic).Width;
                anchoLetra[k] = a;
                return a;
            }
            // Lo que crece la letra de la casilla 'i' con la lupa centrada en 'centro' (en casillas, 0.5 = el medio
            // de la primera).
            private float EscalaDe(Graphics g, int i, float centro, float slot, float altoL)
            {
                char ch = Letters[i];
                float s = EscalaLupa(i + .5f - centro);
                if (ch == TopMark) return TopeLupa(s, radioEstrella * 2f, slot, radioEstrella * 2f, altoL);
                bool isVideo = (ch == VideoMark);
                Font f = isVideo ? fVideo : fMain;
                string lbl = isVideo ? lblVideo : ch.ToString();
                float alto = f.SizeInPoints * g.DpiY / 72f * .78f;   // lo que sube una mayuscula, mas o menos
                return TopeLupa(s, AnchoDe(g, lbl, f), slot, alto, altoL);
            }
            // Dibuja 'accion' con el lienzo agrandado 's' veces alrededor del centro de 'r'.
            private delegate void Dibujo();
            private static void Agrandado(Graphics g, RectangleF r, float s, Dibujo accion)
            {
                if (s <= 1.001f) { accion(); return; }
                GraphicsState st = g.Save();
                float cx = r.X + r.Width * .5f, cy = r.Y + r.Height * .5f;
                g.TranslateTransform(cx, cy); g.ScaleTransform(s, s); g.TranslateTransform(-cx, -cy);
                accion();
                g.Restore(st);
            }

            private void RenderLupa(Graphics g, int w, int h, int n, float slot, float altoL, float bajo)
            {
                const double pulse = 0.45;   // brillo FIJO, como en el camino de la tarjeta de siempre (el pulso va por halo)
                int iCur = Letters.IndexOf(Current);
                float centro = (iCur >= 0) ? XCapsula(iCur, slot) / slot + .5f : -100f;   // la lupa va con la capsula
                int availN = (Available == null) ? -1 : Available.Count;
                int datos = FirmaDatos();
                int cq = (int)Math.Round(centro * 64f);
                if (lupaCache == null || luW != w || luH != h || luCur != Current || luLetters != Letters
                    || luAvailN != availN || luDatos != datos || luCentro != cq)
                {
                    RebuildLupa(g, w, h, n, slot, altoL, bajo, centro);
                    luW = w; luH = h; luCur = Current; luLetters = Letters; luAvailN = availN; luDatos = datos; luCentro = cq;
                }
                g.DrawImageUnscaled(lupaCache, 0, 0);

                // LA CASILLA ELEGIDA: la misma capsula de neon, y la letra agrandada.
                for (int i = 0; i < n; i++)
                {
                    char ch = Letters[i];
                    if (ch != Current || ch == TopMark) continue;
                    bool isVideo = (ch == VideoMark);
                    string lbl = isVideo ? lblVideo : ch.ToString();
                    Font useF = isVideo ? fVideo : fMain;
                    float paso = AnimPaso, xCap = XCapsula(i, slot);
                    RectangleF slotRect = new RectangleF(i * slot, 0, slot, altoL);
                    float s = EscalaDe(g, i, centro, slot, altoL);
                    if (paso < 1f)
                    {
                        bool hayD = (Available == null) || Available.Contains(ch);
                        Color tono = TonoLetra(ch, hayD);
                        Agrandado(g, slotRect, s, delegate { Grabada(g, whiteB, lbl, useF, slotRect, tono, hayD); });
                    }
                    RectangleF box = new RectangleF(xCap + 3.5f, 3.5f, slot - 7f, altoL - 7f);
                    if (pulsePath == null || box != pulseBox)
                    {
                        if (pulsePath != null) pulsePath.Dispose();
                        pulsePath = RoundRectF(box, 9);
                        pulseBox = box;
                    }
                    if (pulseBrush == null) pulseBrush = new SolidBrush(Color.Black);
                    if (pulsePen == null) pulsePen = new Pen(Color.Black, 2.6f);
                    pulsePen.LineJoin = LineJoin.Round;
                    for (int k = 5; k >= 1; k--)
                    {
                        pulsePen.Color = Color.FromArgb(9 + (int)(pulse * 7 * (6 - k)), 120, 190, 255);
                        pulsePen.Width = k * 2.4f;
                        g.DrawPath(pulsePen, pulsePath);
                    }
                    pulseBrush.Color = Color.FromArgb(26 + (int)(pulse * 30), 70, 150, 240);
                    g.FillPath(pulseBrush, pulsePath);
                    pulsePen.Color = Color.FromArgb(120 + (int)(pulse * 70), 130, 200, 255); pulsePen.Width = 2.6f;
                    g.DrawPath(pulsePen, pulsePath);
                    pulsePen.Color = Color.FromArgb(215 + (int)(pulse * 40), 225, 242, 255); pulsePen.Width = 1.1f;
                    g.DrawPath(pulsePen, pulsePath);
                    whiteB.Color = (paso >= 1f) ? Color.White : Color.FromArgb((int)(255 * paso), 255, 255, 255);
                    Agrandado(g, slotRect, s, delegate { g.DrawString(lbl, useF, whiteB, slotRect, sfCen); });
                    break;
                }

                // LA ESTRELLITA DORADA de "lo mas sonado", tambien bajo la lupa si esta cerca.
                int topI = Letters.IndexOf(TopMark);
                if (topI >= 0 && Available != null && Available.Contains(TopMark))
                {
                    RectangleF gsl = new RectangleF(topI * slot, 0, slot, altoL);
                    const double gpu = 0.5;
                    bool topSel = (Current == TopMark);
                    if (goldTextBrush == null) goldTextBrush = new SolidBrush(Color.Black);
                    float r0 = radioEstrella;
                    radioEstrella = r0 * EscalaDe(g, topI, centro, slot, altoL);
                    if (topSel)
                    {
                        RectangleF gbx = new RectangleF(XCapsula(topI, slot) + 3.5f, 3.5f, gsl.Width - 7f, altoL - 7f);
                        int ga = 80 + (int)(gpu * 150);
                        if (goldPath == null || gbx != goldBox) { if (goldPath != null) goldPath.Dispose(); goldPath = RoundRectF(gbx, 9); goldBox = gbx; }
                        if (goldBrush == null) goldBrush = new SolidBrush(Color.Black);
                        if (goldPen == null) goldPen = new Pen(Color.Black, 2.2f);
                        goldPen.LineJoin = LineJoin.Round;
                        goldBrush.Color = Color.FromArgb(ga, 255, 200, 40);
                        goldPen.Color = Color.FromArgb(235, 255, 228, 130); goldPen.Width = 2.2f;
                        g.FillPath(goldBrush, goldPath);
                        g.DrawPath(goldPen, goldPath);
                        goldTextBrush.Color = Color.White;
                        Estrella(g, goldTextBrush, gsl, 0f, 0f);
                    }
                    else
                    {
                        int hAlpha = 35 + (int)(gpu * 55);
                        goldTextBrush.Color = Color.FromArgb(hAlpha, 255, 205, 90);
                        Estrella(g, goldTextBrush, gsl, -1f, 0f);
                        Estrella(g, goldTextBrush, gsl, 1f, 0f);
                        Estrella(g, goldTextBrush, gsl, 0f, -1f);
                        Estrella(g, goldTextBrush, gsl, 0f, 1f);
                        int gb = 60 + (int)(gpu * 70); if (gb > 255) gb = 255;
                        goldTextBrush.Color = Color.FromArgb(255, 255, 210, gb);
                        Estrella(g, goldTextBrush, gsl, 0f, 0f);
                    }
                    radioEstrella = r0;
                }
            }

            // La tira de la lupa: el mismo riel y las mismas letras que RebuildStrip, pero cada letra crecida segun lo
            // cerca que este del centro, y la rayita de cuanta musica tiene cada una.
            private void RebuildLupa(Graphics gMide, int w, int h, int n, float slot, float altoL, float bajo, float centro)
            {
                if (lupaCache == null || lupaCache.Width != w || lupaCache.Height != h)
                {
                    if (lupaCache != null) lupaCache.Dispose();
                    lupaCache = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                }
                // Lo que mas tiene una letra: la rayita de cada una se mide contra eso.
                int mas = 0;
                if (Counts != null)
                    for (int i = 0; i < n; i++) { int c; if (Counts.TryGetValue(Letters[i], out c) && c > mas) mas = c; }
                using (Graphics g = Graphics.FromImage(lupaCache))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Riel(g, w, h);
                    using (Pen sep = new Pen(Color.FromArgb(24, 150, 175, 225)))
                        for (int i = 1; i < n; i++)
                            g.DrawLine(sep, i * slot, 10, i * slot, h - 10);
                    float grueso = Math.Max(1.5f, h * .035f);
                    float yRaya = (bajo > 0f) ? altoL - grueso * .5f : h - grueso - 2f;
                    for (int i = 0; i < n; i++)
                    {
                        char ch = Letters[i];
                        bool isTop = (ch == TopMark);
                        bool avail = (Available == null) || Available.Contains(ch);
                        RectangleF rL = new RectangleF(i * slot, 0, slot, altoL);
                        if (ch != Current && !(isTop && avail))
                        {
                            bool isVideo = (ch == VideoMark);
                            float s = EscalaDe(gMide, i, centro, slot, altoL);
                            if (isTop)
                            {
                                float r0 = radioEstrella; radioEstrella = r0 * s;
                                EstrellaGrabada(g, tb, rL, Color.FromArgb(122, 116, 96), avail);
                                radioEstrella = r0;
                            }
                            else
                            {
                                string lbl = isVideo ? lblVideo : ch.ToString();
                                Font f = isVideo ? fVideo : fMain;
                                Color tono = TonoLetra(ch, avail);
                                Agrandado(g, rL, s, delegate { Grabada(g, tb, lbl, f, rL, tono, avail); });
                            }
                        }
                        if (bajo > 0f && fNum != null) Cuenta(g, tb, ch, i, slot, h, bajo, avail);
                        // LA RAYITA: cuanta musica hay detras de esta letra, contra la que mas tiene.
                        int cuantas = 0;
                        if (mas > 0 && Counts != null && Counts.TryGetValue(ch, out cuantas) && cuantas > 0)
                        {
                            float largo = RayaLupa(cuantas, mas, slot);
                            Color tono = TonoLetra(ch, avail);
                            tb.Color = Color.FromArgb(avail ? 170 : 80, tono);
                            g.FillRectangle(tb, i * slot + (slot - largo) * .5f, yRaya - grueso * .5f, largo, grueso);
                        }
                    }
                }
            }
            // Lo larga que es la rayita de una letra: proporcional a lo que tiene, y al menos 3 px para que se vea
            // que hay algo.
            internal static float RayaLupa(int cuantas, int mas, float slot)
            {
                if (cuantas <= 0 || mas <= 0) return 0f;
                float max = Math.Max(3f, slot - 10f);
                return Math.Max(3f, max * Math.Min(1f, cuantas / (float)mas));
            }
#endif
        }

        // ==========  ENCABEZADO DE LA SECCION  ==========
        // Antes era una etiqueta de texto plano: "LO MAS SONADO    -    pagina 1 de 3". Ahora el nombre de la
        // seccion va en SU color -el mismo que la letra luce en la barra de arriba- y las paginas se ven de un
        // vistazo como puntitos: cuantas hay y en cual estas se sabe sin leer, que es lo que le sirve a alguien
        // que esta de pie delante de la maquina.
        private class HeaderView : Control
        {
            public string Section = "";
            public int Page = 0, Total = 1;
            public Color Accent = DefaultAccent;

            private Font fSec, fPag; private float fontAlto = -1f;
            private SolidBrush brocha; private StringFormat sfIzq;

            protected override void OnFontChanged(EventArgs e)
            {
                SueltaDibujo(ref fSec); SueltaDibujo(ref fPag);
                fontAlto = -1f;
                base.OnFontChanged(e); Invalidate();
            }
            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref fSec); SueltaDibujo(ref fPag); SueltaDibujo(ref brocha); SueltaDibujo(ref sfIzq); } }
                finally { base.Dispose(disposing); }
            }

            public HeaderView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
                this.TabStop = false;   // aqui no se pincha nada: antes era una etiqueta, que nunca tomaba el foco
            }

            private void EnsureRes(int h)
            {
                if (brocha == null) brocha = new SolidBrush(Color.White);
                if (sfIzq == null) sfIzq = CreaSfDibuja(StringAlignment.Near);
                if (fSec != null && Math.Abs(fontAlto - h) < 0.5f) return;
                if (fSec != null) fSec.Dispose();
                if (fPag != null) fPag.Dispose();
                // Los cuerpos salen del alto del propio encabezado: con 28 px dan los 12.5 puntos de siempre.
                fSec = new Font(this.Font.FontFamily, h * 0.446f, FontStyle.Bold);
                fPag = new Font(this.Font.FontFamily, h * 0.303f, FontStyle.Bold);
                fontAlto = h;
            }

            // El color del acento, subido hacia el blanco lo justo para que un genero oscuro se siga leyendo.
            private static Color Claro(Color c)
            {
                return Color.FromArgb(255, c.R + (int)((255 - c.R) * 0.40f),
                                           c.G + (int)((255 - c.G) * 0.40f),
                                           c.B + (int)((255 - c.B) * 0.40f));
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            public void RenderContent(Graphics g)
            {
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                EnsureRes(h);
                Color ac = Claro(Accent);

                // SE MIDE TODO ANTES DE PINTAR NADA. Este encabezado comparte fila con las instrucciones, asi
                // que el sitio que le toca cambia con la pantalla. Y lo que quepa se pega a la DERECHA, para
                // que no quede colgando en medio de un hueco.
                float pw0 = Math.Max(3f, h * 0.13f);
                float anchoNombre = pw0 + h * 0.32f + AnchoTexto(g, Section, fSec) + 4f;
                int pag0 = Page; if (pag0 < 0) pag0 = 0; else if (Total > 0 && pag0 >= Total) pag0 = Total - 1;

                // LA RAYITA SEPARADORA se dibuja SIEMPRE que detras del nombre venga algo, lleve puntitos o no.
                // Su ancho estaba metido DENTRO de la cuenta de los puntitos, asi que en una letra con mas de
                // doce paginas -que no lleva puntitos- el reparto se quedaba corto justo por ella y el
                // "pagina X de Y" se caia entero... teniendo 150 px de sitio libre al lado. Eso era lo de
                // "LETRA B" sin numero de paginas, y por eso las letras con pocas paginas si se veian bien.
                // Ahora va por su cuenta y se cuenta siempre.
                float anchoSep = h * 0.5f;

                float r0 = Math.Max(2.2f, h * 0.115f), paso0 = r0 * 3.4f;
                // El punto de la pagina en la que estas lleva un halo alrededor, bastante mas ancho que el
                // punto. Se reservaba el punto pelado, asi que estando en la ultima pagina el halo se salia por
                // el borde y se veia cortado. Se reserva el halo.
                float halo0 = r0 * 1.35f * 2.1f;
                float anchoPuntos0 = (Total > 1 && Total <= 12) ? (halo0 + (Total - 1) * paso0 + halo0) : 0f;

                // EL NUMERO DE PAGINA SIEMPRE SE DICE. Antes era todo o nada: si "pagina 3 de 18" no cabia
                // entero no se dibujaba nada, y el cliente se quedaba sin saber cuantas paginas hay -que es
                // justo el dato que necesita para decidir si sigue pasando-. Ahora, si no cabe la frase larga,
                // se prueba una mas corta, y luego la mas corta de todas.
                string[] formas = new string[] { "pagina " + (pag0 + 1) + " de " + Total,
                                                 (pag0 + 1) + " de " + Total,
                                                 (pag0 + 1) + "/" + Total };
                float[] anchoForma = new float[formas.Length];
                for (int i = 0; i < formas.Length; i++) anchoForma[i] = h * 0.45f + AnchoTexto(g, formas[i], fPag) + 8f;

                // Se prueba con los puntitos puestos y, si aun asi no entra ninguna forma, se quitan: los
                // puntitos dicen en que pagina estas de un vistazo, pero el numero es el UNICO que dice cuantas
                // hay en total, asi que si hay que elegir gana el numero.
                bool conPuntos0 = anchoPuntos0 > 0f;
                int forma = -1;
                if (Total > 1)
                    for (int intento = 0; intento < 2 && forma < 0; intento++)
                    {
                        if (intento == 1) { if (!conPuntos0) break; conPuntos0 = false; }
                        float libre = w - anchoNombre - anchoSep - (conPuntos0 ? anchoPuntos0 : 0f);
                        for (int i = 0; i < formas.Length; i++)
                            if (anchoForma[i] <= libre) { forma = i; break; }
                    }
                if (conPuntos0 && anchoNombre + anchoSep + anchoPuntos0 > w) conPuntos0 = false;
                string txt0 = (forma >= 0) ? formas[forma] : "";
                bool algoDetras = conPuntos0 || forma >= 0;
                float total0 = anchoNombre + (algoDetras ? anchoSep : 0f)
                             + (conPuntos0 ? anchoPuntos0 : 0f) + ((forma >= 0) ? anchoForma[forma] : 0f);
                // Pegado a la derecha, con 2 px de aire: medir un texto y dibujarlo no dan exactamente lo mismo
                // -el dibujo se pasa como un pixel- y sin ese aire la ultima letra se salia de la caja.
                float x = (total0 + 2f < w) ? (w - total0 - 2f) : 0f;

                // Una pastilla del color de la seccion delante del nombre: ata el encabezado con la letra que
                // esta encendida arriba, para que se vea que hablan de lo mismo.
                float pw = Math.Max(3f, h * 0.13f), ph = h * 0.58f;
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(230, ac)))
                using (GraphicsPath pp = Redondo(new RectangleF(x, (h - ph) / 2f, pw, ph), pw / 2f))
                    g.FillPath(pb, pp);
                x += pw + h * 0.32f;

                // EL NOMBRE MANDA, pero no puede comerse la fila entera: si no cabe con el cuerpo elegido, se
                // encoge hasta caber. Y lo que venga detras solo se dibuja SI CABE ENTERO; nada se dibuja "a
                // medias" ni se sale por la derecha.
                string sec = Section ?? "";
                Font fUsar = fSec; Font fApreta = null;
                float tope = Math.Max(10f, w - x);
                float anchoSec = AnchoTexto(g, sec, fUsar);
                if (anchoSec > tope)
                {
                    float cuerpo = fSec.SizeInPoints;
                    while (cuerpo > 6f)
                    {
                        cuerpo -= 0.5f;
                        if (fApreta != null) fApreta.Dispose();
                        fApreta = new Font(this.Font.FontFamily, cuerpo, FontStyle.Bold);
                        anchoSec = AnchoTexto(g, sec, fApreta);
                        if (anchoSec <= tope) break;
                    }
                    if (fApreta != null) fUsar = fApreta;
                }
                brocha.Color = Color.FromArgb(160, 0, 0, 0);
                g.DrawString(sec, fUsar, brocha, new RectangleF(x, 1.4f, tope, h), sfIzq);
                brocha.Color = ac;
                g.DrawString(sec, fUsar, brocha, new RectangleF(x, 0, tope, h), sfIzq);
                if (fApreta != null) fApreta.Dispose();
                x += anchoSec;

                if (!algoDetras) return;   // una sola pagina, o no cabe nada mas
                int pag = pag0;
                float r = r0, paso = paso0;
                bool conPuntos = conPuntos0;

                using (Pen sep = new Pen(Color.FromArgb(70, 150, 175, 225)))
                    g.DrawLine(sep, x, h * 0.26f, x, h * 0.74f);
                x += anchoSep;

                // LOS PUNTITOS. Con muchas paginas no caben ni se contarian de un vistazo, asi que a partir de
                // doce se dice con numeros y ya.
                if (conPuntos)
                {
                    float cy = h / 2f;
                    for (int i = 0; i < Total; i++)
                    {
                        float cx = x + halo0 + i * paso;
                        bool esta = (i == pag);
                        float rr = esta ? r * 1.35f : r;
                        if (esta)
                        {
                            using (SolidBrush halo = new SolidBrush(Color.FromArgb(60, ac)))
                                g.FillEllipse(halo, cx - rr * 2.1f, cy - rr * 2.1f, rr * 4.2f, rr * 4.2f);
                            using (SolidBrush b2 = new SolidBrush(ac))
                                g.FillEllipse(b2, cx - rr, cy - rr, rr * 2f, rr * 2f);
                        }
                        else
                            using (Pen p2 = new Pen(Color.FromArgb(120, 150, 168, 205), Math.Max(1f, r * 0.5f)))
                                g.DrawEllipse(p2, cx - rr * 0.75f, cy - rr * 0.75f, rr * 1.5f, rr * 1.5f);
                    }
                    x += halo0 + (Total - 1) * paso + halo0;
                }

                if (forma < 0) return;
                x += h * 0.45f;
                // Ultima comprobacion con el ancho que de verdad ocupo el nombre (si hubo que encogerlo sobra
                // sitio, y si el reparto se quedo corto por un pelo se acorta la frase antes que callarse). Solo
                // se calla si no cabe ni "3/18": entonces si, mejor nada que un numero a medias -"pagina 3 de 1"
                // con la ultima cifra comida ademas dice algo FALSO-.
                while (AnchoTexto(g, txt0, fPag) > w - x && forma < formas.Length - 1) txt0 = formas[++forma];
                if (AnchoTexto(g, txt0, fPag) > w - x) return;
                brocha.Color = Color.FromArgb(150, 160, 190);
                g.DrawString(txt0, fPag, brocha, new RectangleF(x, 0, Math.Max(10f, w - x), h), sfIzq);
            }

            private static GraphicsPath Redondo(RectangleF r, float radio)
            {
                GraphicsPath p = new GraphicsPath();
                float d = radio * 2;
                if (d > r.Height) d = r.Height;
                if (d > r.Width) d = r.Width;
                if (d < 2) d = 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }
        }

        private partial class PastillaView : Control
        {
            public string Etiqueta = "", Valor = "";
            public Color Acento = DefaultAccent;

            // ---------- LAS PORTADITAS DE LA COLA ----------
            // "En cola  3" es un dato del local: dice CUANTAS hay. Tres portaditas dicen lo mismo y
            // ademas CUALES son, que es lo que el cliente quiere saber para no volver a pedir la que
            // ya pidio. Se ven las tres primeras; si hay mas, la tercera lleva un "+2" en la esquina.
            public bool ConPortadas;      // esta capsula lleva hueco para portaditas (la de la cola si, la de la moneda no)
            public Image[] Portadas;      // las tres primeras de la cola (huecos vacios donde no haya)
            public int Mas;               // cuantas quedan detras de las tres
            private const int Huecos = 3;

            private Font fEtq, fVal, fMas; private float fontAlto = -1f;
            private SolidBrush brocha; private StringFormat sfCen;

            // ---------- LA CEREMONIA DE LA MONEDA ----------
            // El numero de creditos cambiaba de 0 a 1 y se acabo. Es informacion correcta y, a dos metros,
            // de noche y con los amigos hablando, invisible. Aqui la moneda CAE, el numero RUEDA y la
            // capsula DESTELLA: es el instante en que el dinero se vuelve musica y tiene que notarse
            // desde la barra. Es lo mismo dicho de una forma que no hay que leer.
            private int monTick, rodTick, aviTick;   // 0 = esa parte no esta corriendo
            private string rodDe = "";               // el numero de ANTES, el que se va rodando hacia arriba
            private const int MonMs = 420, RodMs = 240, AviMs = 520, DestMs = 520;

            public void Moneda()
            {
                int t = Environment.TickCount; if (t == 0) t = 1;
                monTick = t;
                rodTick = t + MonMs;      // el numero rueda cuando la moneda ATERRIZA, no antes
                rodDe = Valor ?? "";      // se guarda AHORA: quien llama todavia no ha puesto el nuevo
            }
            // Cuando no hay creditos: la capsula se sacude. Hoy solo salia un letrero, y a las dos de la
            // manana nadie lee letreros -el cliente sigue picandole al boton creyendo que esta descompuesta-.
            public void Aviso()
            {
                int t = Environment.TickCount; if (t == 0) t = 1;
                aviTick = t;
            }
            // Si hay algo moviendose. Lo preguntan el reloj de la pantalla, para repintar, y la firma de la
            // textura de la interfaz, para rehacerse: sin eso, CON TARJETA GRAFICA la moneda no se veria
            // caer -la textura no cambiaria y la animacion se quedaria congelada dentro de ella-.
            public bool Animando
            {
                get
                {
                    int t = Environment.TickCount;
#if ROCOLA_GPU
                    if (LlegaViva(t)) return true;   // la cancion que llega a la cola (DiscoVivoGpu.cs)
#endif
                    return (monTick != 0 && t - monTick < MonMs + DestMs)
                        || (aviTick != 0 && t - aviTick < AviMs);
                }
            }
            // Un numero que cambia mientras dura la animacion, y SOLO entonces: quieta, la firma no se
            // mueve y la textura no se rehace ni una vez de mas.
            public int Paso { get { return Animando ? (Environment.TickCount / 16) : 0; } }

            protected override void OnFontChanged(EventArgs e)
            {
                SueltaDibujo(ref fEtq); SueltaDibujo(ref fVal); SueltaDibujo(ref fMas);
                fontAlto = -1f;
                base.OnFontChanged(e); Invalidate();
            }
            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref fEtq); SueltaDibujo(ref fVal); SueltaDibujo(ref fMas); SueltaDibujo(ref brocha); SueltaDibujo(ref sfCen); } }
                finally { base.Dispose(disposing); }
            }

            public PastillaView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
                this.TabStop = false;
            }

            // Lo que mide de ancho una pastilla con ese contenido y ese alto. Lo usa el acomodo, que necesita
            // saberlo ANTES de dibujar nada (y por eso mide con TextRenderer, sin Graphics).
            //
            // 'conPortadas' dice si ESTA capsula lleva portaditas, y no CUANTAS lleva ahora mismo, a
            // proposito. El ancho de la capsula de la cola manda hasta donde puede llegar el TITULO de la
            // cancion (derPast, en el acomodo); si dependiera de la cola, el titulo se repartiria de otra
            // manera en dos lineas cada vez que alguien pide algo, y el nombre de la que suena bailaria
            // solo, sin que nadie lo hubiera tocado. Se mide siempre el hueco de las tres, llenas o vacias.
            public static int Ancho(string etiq, string valor, int alto, FontFamily fam, bool conPortadas)
            {
                // Se mide con GDI+, que es con lo que RenderContent la PINTA. Antes se media con TextRenderer,
                // que es GDI: otro motor, otros anchos. En la pantalla del bar la diferencia eran dos pixeles y
                // pasaba desapercibida; en una pantalla grande crecia con el cuerpo de la letra y se comia el
                // final de "Inserta una moneda".
                using (Font fe = new Font(fam, alto * 0.34f, FontStyle.Bold))
                using (Font fv = new Font(fam, alto * 0.52f, FontStyle.Bold))
                {
                    int w = AnchoTexto(etiq, fe);
                    if (!string.IsNullOrEmpty(valor)) w += AnchoTexto(valor, fv) + (int)(alto * 0.22f);
                    if (conPortadas) w += AnchoPortaditas(alto);
                    return w + (int)(alto * 0.9f);
                }
            }
            // El hueco de las tres portaditas: el suyo propio, lo que las separa y lo que las separa del numero.
            private static int LadoPortadita(int alto) { return (int)(alto * 0.52f); }
            private static int AnchoPortaditas(int alto)
            {
                return Huecos * LadoPortadita(alto) + (Huecos - 1) * (int)(alto * 0.09f) + (int)(alto * 0.20f);
            }

            private void EnsureRes(int h)
            {
                if (brocha == null) brocha = new SolidBrush(Color.White);
                if (sfCen == null) sfCen = CreaSfDibuja(StringAlignment.Center);
                if (fEtq != null && Math.Abs(fontAlto - h) < 0.5f) return;
                if (fEtq != null) fEtq.Dispose();
                if (fVal != null) fVal.Dispose();
                if (fMas != null) fMas.Dispose();
                fEtq = new Font(this.Font.FontFamily, h * 0.34f, FontStyle.Bold);
                fVal = new Font(this.Font.FontFamily, h * 0.52f, FontStyle.Bold);
                fMas = new Font(this.Font.FontFamily, LadoPortadita(h) * 0.34f, FontStyle.Bold);
                fontAlto = h;
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            public void RenderContent(Graphics g)
            {
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                EnsureRes(h);
                int t = Environment.TickCount;
                // LA SACUDIDA del aviso de sin creditos: dos idas y venidas que se apagan solas.
                System.Drawing.Drawing2D.GraphicsState stSac = null;
                if (aviTick != 0)
                {
                    int da = t - aviTick;
                    if (da >= 0 && da < AviMs)
                    {
                        float ua = da / (float)AviMs;
                        float dx = (float)(Math.Sin(ua * 18.0) * h * 0.13 * (1.0 - ua));
                        stSac = g.Save(); g.TranslateTransform(dx, 0f);
                    }
                    else if (da >= AviMs) aviTick = 0;
                }
#if ROCOLA_GPU
                // EL SALTITO de la cancion que llega a la cola: solo en la imagen de la tarjeta (DiscoVivoGpu.cs).
                float salto = SaltoLlegada(t);
                if (stSac == null && salto != 1f)
                {
                    stSac = g.Save();
                    g.TranslateTransform(w * 0.5f, h * 0.5f); g.ScaleTransform(salto, salto); g.TranslateTransform(-w * 0.5f, -h * 0.5f);
                }
#endif
                using (GraphicsPath p = RoundRectF(new RectangleF(0.5f, 0.5f, w - 1, h - 1), h / 2f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(52, Acento))) g.FillPath(b, p);
                    using (Pen pe = new Pen(Color.FromArgb(95, Acento), 1f)) g.DrawPath(pe, p);
                    // EL DESTELLO: el borde se enciende y se apaga cuando la moneda aterriza. Es lo que se
                    // ve desde lejos, cuando el numero ya es demasiado chico para leerse.
                    float br = 0f;
                    if (monTick != 0) { int dd = t - (monTick + MonMs); if (dd >= 0 && dd < DestMs) br = 1f - dd / (float)DestMs; }
                    if (aviTick != 0) { int da2 = t - aviTick; if (da2 >= 0 && da2 < AviMs) br = Math.Max(br, 1f - da2 / (float)AviMs); }
#if ROCOLA_GPU
                    br = Math.Max(br, DestelloLlegada(t));
#endif
                    if (br > 0.01f)
                    {
                        using (SolidBrush b2 = new SolidBrush(Color.FromArgb((int)(70 * br), Acento))) g.FillPath(b2, p);
                        using (Pen pe2 = new Pen(Color.FromArgb((int)(210 * br), Acento), 2f)) g.DrawPath(pe2, p);
                    }
                }
                // El contenido va CENTRADO: al acomodo se le puede dar un pelo mas de ancho del justo y sigue
                // viendose bien, en vez de quedar pegado a un lado.
                string et = Etiqueta ?? "", va = Valor ?? "";
                float we = g.MeasureString(et, fEtq).Width;
                float wv = (va.Length > 0) ? g.MeasureString(va, fVal).Width : 0f;
                float hueco = (va.Length > 0) ? h * 0.18f : 0f;
                float wPort = ConPortadas ? AnchoPortaditas(h) : 0f;
                float x = (w - (we + wv + hueco + wPort)) / 2f;
                if (x < h * 0.25f) x = h * 0.25f;
                brocha.Color = Color.FromArgb(215, 205, 214, 238);
                g.DrawString(et, fEtq, brocha, new RectangleF(x, 0, we + 6, h), sfCen);
                float xv = x + we + hueco;
                // EL NUMERO RUEDA COMO UN ODOMETRO: el de antes sube y se va, el nuevo entra por abajo. Se
                // recorta a su hueco para que ninguno de los dos asome fuera de la capsula.
                float rp = -1f;
                string de = rodDe;
                if (monTick != 0) { int dr = t - rodTick; if (dr < 0) rp = 0f; else if (dr < RodMs) rp = dr / (float)RodMs; }
#if ROCOLA_GPU
                if (rp < 0f) rp = RodadoLlegada(t, ref de);   // el numero de la cola sube cuando llega el boleto
#endif
                if (rp >= 0f)
                {
                    float sube = (float)(1.0 - Math.Pow(1.0 - rp, 3));   // frena al llegar, como un rodillo
                    System.Drawing.Drawing2D.GraphicsState st = g.Save();
                    g.SetClip(new RectangleF(xv - 3f, 1f, Math.Max(wv, 10f) + 12f, h - 2f), CombineMode.Intersect);
                    brocha.Color = Acento;
                    if (de.Length > 0)
                        g.DrawString(de, fVal, brocha, new RectangleF(xv, -h * sube, wv + 6, h), sfCen);
                    if (va.Length > 0)
                        g.DrawString(va, fVal, brocha, new RectangleF(xv, h * (1f - sube), wv + 6, h), sfCen);
                    g.Restore(st);
                }
                else if (va.Length > 0)
                {
                    brocha.Color = Acento;
                    g.DrawString(va, fVal, brocha, new RectangleF(xv, 0, wv + 6, h), sfCen);
                }

                // LAS PORTADITAS DE LA COLA, detras del numero. Los huecos vacios se insinuan apenas: dicen
                // "aqui van las que pidas" sin pelearle la vista a nada.
                if (ConPortadas)
                {
                    int lado = LadoPortadita(h);
                    int sep = (int)(h * 0.09f);
                    float py = (h - lado) / 2f;
                    float px0 = xv + wv + h * 0.20f;
                    InterpolationMode antes = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;   // achicar una portada a 20 px sin que salga a granos
                    for (int i = 0; i < Huecos; i++)
                    {
                        RectangleF rq = new RectangleF(px0 + i * (lado + sep), py, lado, lado);
                        Image im = (Portadas != null && i < Portadas.Length) ? Portadas[i] : null;
                        using (GraphicsPath ph = RoundRectF(rq, lado * 0.22f))
                        {
                            if (im != null)
                            {
                                System.Drawing.Drawing2D.GraphicsState sp = g.Save();
                                g.SetClip(ph, CombineMode.Intersect);
                                g.DrawImage(im, rq);
                                g.Restore(sp);
                                using (Pen pe = new Pen(Color.FromArgb(120, 235, 240, 255), 1f)) g.DrawPath(pe, ph);
                            }
                            else using (Pen pe = new Pen(Color.FromArgb(52, Acento), 1f)) g.DrawPath(pe, ph);
                        }
                    }
                    g.InterpolationMode = antes;
                    // Y SI HAY MAS DE TRES, cuantas quedan detras. Sin esto, una cola de nueve se veria
                    // igual que una de tres y el numero de al lado parecerian dos cosas que no cuadran.
                    if (Mas > 0)
                    {
                        RectangleF ru = new RectangleF(px0 + (Huecos - 1) * (lado + sep), py, lado, lado);
                        float hb = lado * 0.46f;
                        using (GraphicsPath ph = RoundRectF(ru, lado * 0.22f))
                        {
                            System.Drawing.Drawing2D.GraphicsState sp = g.Save();
                            g.SetClip(ph, CombineMode.Intersect);
                            using (SolidBrush b3 = new SolidBrush(Color.FromArgb(205, 8, 11, 28)))
                                g.FillRectangle(b3, ru.X, ru.Bottom - hb, ru.Width, hb);
                            brocha.Color = Color.FromArgb(238, 232, 240, 255);
                            g.DrawString("+" + Mas, fMas, brocha, new RectangleF(ru.X, ru.Bottom - hb, ru.Width, hb), sfCen);
                            g.Restore(sp);
                        }
                    }
                }

                // LA MONEDA. Entra por arriba, gira -se aplasta a lo ancho, que es como se ve girar una
                // moneda- y aterriza sobre el numero con un botecito.
                if (monTick != 0)
                {
                    int dt = t - monTick;
                    if (dt >= 0 && dt < MonMs)
                    {
                        float u = dt / (float)MonMs;
                        float d = h * 0.44f;
                        float cx = (va.Length > 0 || rodDe.Length > 0) ? xv + Math.Max(wv, 10f) * 0.5f : w * 0.5f;
                        float cy = -d + (h * 0.5f + d) * (u * u);
                        if (u > 0.74f) cy -= (float)Math.Abs(Math.Sin((u - 0.74f) * 12.0)) * h * 0.16f * ((1f - u) / 0.26f);
                        float aw = Math.Max(d * 0.16f, d * (float)Math.Abs(Math.Cos(u * 11.5)));
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 246, 206, 96)))
                            g.FillEllipse(b, cx - aw * 0.5f, cy - d * 0.5f, aw, d);
                        using (Pen pe = new Pen(Color.FromArgb(190, 255, 242, 196), 1.2f))
                            g.DrawEllipse(pe, cx - aw * 0.5f, cy - d * 0.5f, aw, d);
                    }
                    if (dt >= MonMs + DestMs) monTick = 0;
                }
                if (stSac != null) g.Restore(stSac);
            }

            private static GraphicsPath RoundRectF(RectangleF r, float radio)
            {
                GraphicsPath p = new GraphicsPath();
                float d = radio * 2;
                if (d > r.Height) d = r.Height;
                if (d > r.Width) d = r.Width;
                if (d < 2) d = 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }
        }

        // ==========  PASTILLA DE GENERO  ==========
        // La misma que sale en la lista de un disco y en los resultados del buscador -relleno muy tenue del
        // color del genero, borde fino y el texto en ese color aclarado-, pero como control suelto, para
        // ponerla en la franja de abajo: asi, al sonar una cancion, tambien se ve DE QUE ES.
        //
        // Se mide sola: le da su ancho y su alto al acomodo, que es quien la coloca detras del artista.
        private class GeneroView : Control
        {
            private string texto = "";
            public string Texto
            {
                get { return texto; }
                set { string v = value ?? ""; if (v != texto) { texto = v; medido = false; Invalidate(); } }
            }
            public Color Tinte = DefaultAccent;

            private bool medido; private int anchoPide, altoPide;
            private SolidBrush brocha; private StringFormat sfCen;

            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref brocha); SueltaDibujo(ref sfCen); } }
                finally { base.Dispose(disposing); }
            }

            public GeneroView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
                this.TabStop = false;
            }
            protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); medido = false; }

            private void Mide()
            {
                if (medido) return;
                anchoPide = (texto.Length > 0) ? AnchoTexto(texto, this.Font) + 20 : 0;
                altoPide = this.Font.Height + 6;
                medido = true;
            }
            public int AnchoQuePide() { Mide(); return anchoPide; }
            public int AltoQuePide() { Mide(); return altoPide; }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            public void RenderContent(Graphics g)
            {
                int w = Width, h = Height;
                if (w < 6 || h < 6 || texto.Length == 0) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (brocha == null) brocha = new SolidBrush(Color.White);
                if (sfCen == null) sfCen = CreaSfDibuja(StringAlignment.Center);
                using (GraphicsPath p = RoundedPath(new Rectangle(0, 0, w - 1, h - 1), Math.Min(7, (h - 1) / 2)))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(60, Tinte))) g.FillPath(b, p);
                    using (Pen pe = new Pen(Color.FromArgb(120, Tinte), 1f)) g.DrawPath(pe, p);
                }
                brocha.Color = Lighten(Tinte, 0.45);
                g.DrawString(texto, this.Font, brocha, new RectangleF(0, 0, w, h), sfCen);
            }
        }

        // ==========  BARRA DE PROGRESO  ==========
        private class ProgressView : Control
        {
            private bool _neon;
            public bool Neon { get { return _neon; } set { if (_neon != value) { _neon = value; Invalidate(); } } }
            private double _value;
            public double Value { get { return _value; } set { double v = !(value > 0) ? 0 : (value > 1 ? 1 : value); if (v != _value) { _value = v; Invalidate(); } } } // NaN tampoco es progreso valido
            private Color _accent = DefaultAccent;   // ESTETICA: la barra toma el color del genero que suena
            public Color Accent { get { return _accent; } set { if (value.ToArgb() != _accent.ToArgb()) { _accent = value; Invalidate(); } } }

            private SolidBrush trackB; private Pen trackP;   // riel: colores fijos -> se cachean (se dibuja en cada reconstruccion de UI)

            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref trackB); SueltaDibujo(ref trackP); } }
                finally { base.Dispose(disposing); }
            }

            public ProgressView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            // Dibuja la barra de progreso en cualquier Graphics (el control, o la textura de UI del modo GPU).
            public void RenderContent(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Width, h = Height;
                if (w < 4 || h < 4) return;
                if (_neon)
                {
                    // El brillo cabe dentro de la altura existente, también al hornear la UI GPU.
                    // Trazos simples: sin bitmap intermedio, desenfoque ni reloj de animación.
                    float pad = Math.Min((w - 1) * 0.25f, Math.Min(4f, (h - 1) * 0.5f));
                    float y = (h - 1) * 0.5f;
                    float end = w - 1 - pad;
                    using (Pen rail = new Pen(Color.FromArgb(135, 8, 15, 35), Math.Max(1f, h - 2)))
                    { rail.StartCap = rail.EndCap = LineCap.Round; g.DrawLine(rail, pad, y, end, y); }
                    float length = (float)(_value * (end - pad));
                    if (length > 0)
                    {
                        Color tint = Lighten(_accent, 0.35);
                        using (Pen halo = new Pen(Color.FromArgb(45, tint), Math.Max(1f, h - 1)))
                        using (Pen tube = new Pen(Color.FromArgb(160, tint), Math.Max(1f, h * 0.48f)))
                        using (Pen core = new Pen(Lighten(_accent, 0.88), Math.Max(1f, h * 0.16f)))
                        {
                            halo.StartCap = halo.EndCap = tube.StartCap = tube.EndCap = core.StartCap = core.EndCap = LineCap.Round;
                            g.DrawLine(halo, pad, y, pad + length, y);
                            g.DrawLine(tube, pad, y, pad + length, y);
                            g.DrawLine(core, pad, y, pad + length, y);
                        }
                    }
                    return;
                }

                // Riel HUNDIDO: oscuro translucido + borde interior tenue (parece rebajado, no plano).
                Rectangle track = new Rectangle(0, 0, w - 1, h - 1);
                if (trackB == null) { trackB = new SolidBrush(Color.FromArgb(130, 6, 10, 26)); trackP = new Pen(Color.FromArgb(40, 255, 255, 255), 1f); }
                using (GraphicsPath tp = RoundedPath(track, h / 2))
                {
                    g.FillPath(trackB, tp);
                    g.DrawPath(trackP, tp);
                }

                int fw = (int)(_value * (w - 2));
                if (fw >= 3)
                {
                    Rectangle fill = new Rectangle(1, 1, fw, h - 3);
                    int rad = Math.Max(1, (h - 3) / 2);
                    using (GraphicsPath fp = RoundedPath(fill, rad))
                    {
                        // halo "neon" alrededor del relleno (acorde al cielo estrellado)
                        using (Pen glow = new Pen(Color.FromArgb(70, Lighten(_accent, 0.5)), 3f)) { glow.LineJoin = LineJoin.Round; g.DrawPath(glow, fp); }
                        // cuerpo: degradado vertical brillante arriba -> acento abajo
                        using (LinearGradientBrush fb = new LinearGradientBrush(new Rectangle(fill.X, fill.Y, fill.Width, fill.Height + 1), Lighten(_accent, 0.6), Shade(_accent, 0.1), LinearGradientMode.Vertical))
                            g.FillPath(fb, fp);
                        // gloss: franja clara en la mitad superior (aspecto vidrioso)
                        Rectangle gloss = new Rectangle(fill.X + 1, fill.Y + 1, Math.Max(1, fill.Width - 2), Math.Max(1, (fill.Height - 1) / 2));
                        using (GraphicsPath gpath = RoundedPath(gloss, Math.Max(1, gloss.Height / 2)))
                        using (LinearGradientBrush gb = new LinearGradientBrush(new Rectangle(gloss.X, gloss.Y, gloss.Width, gloss.Height + 1), Color.FromArgb(150, 255, 255, 255), Color.FromArgb(15, 255, 255, 255), LinearGradientMode.Vertical))
                            g.FillPath(gb, gpath);
                        // borde nitido brillante
                        using (Pen edge = new Pen(Color.FromArgb(180, Lighten(_accent, 0.65)), 1f)) g.DrawPath(edge, fp);
                    }
                }
            }
        }

        // ==========  LISTA DE CANCIONES (dibujada a mano, no ListBox nativo) ==========
        // Reemplaza al ListBox: asi el interior del disco se puede componer por GPU (un control nativo no se
        // puede poner sobre D3D). Es una VISTA de UNA pagina: la seleccion global la maneja el form (songSel).
        private partial class SongListView : Control
        {
            public Func<int> GetCount;          // total de items (songItems.Count)
            public Func<int, string> GetText;   // texto del item i
            public Func<int, string> GetRight;   // texto CORTO a la derecha del item i (la duracion); "" = nada
            public Func<int, int> GetQueuePos;   // 0 = no esta pedida; n = va en el puesto n de la cola
            // SEPARADO de la cola a proposito: una cancion puede estar SONANDO y ADEMAS estar pedida otra
            // vez, y el cliente necesita las dos cosas -que la esta oyendo, y que ya la va a volver a oir-.
            // Con un solo dato una marca tapaba a la otra.
            public Func<int, bool> GetSonando;
            public Func<int, string> GetGenero;  // etiqueta de genero del item i ("" = aun no se sabe)
            public int PageStart = 0;           // primer item de la pagina visible
            public int Sel = 0;                 // seleccion GLOBAL (entre todas las paginas)
            public int Datos = 0;               // sube cuando cambia el CONTENIDO (cola, generos, duraciones): obliga a rehornear
            public int RowH = 30;
            public Color Accent = DefaultAccent;   // color del genero (para el resaltado)
            public Action<int> OnActivate;      // doble-click sobre un item (encolar)
            public Action<int> OnPick;          // click simple: fija la seleccion

            private SolidBrush hlB; private Pen hlP;
            private SolidBrush txtB; private StringFormat txtFmt; private SolidBrush bgB;
            private SolidBrush durB; private StringFormat durFmt;   // duracion: gris suave, pegada a la derecha
            // El circulito del numero de pista y la marca de "ya pedida". Todo cacheado: la lista se redibuja
            // entera cada vez que cambia algo, y crear una brocha por fila seria basura por gusto.
            private SolidBrush colaB;
            // Sin tarjeta grafica no hay lista transparente y esto no compila nada: es un metodo parcial
            // sin cuerpo, que el compilador borra junto con su llamada.
            partial void SueltaAcabadoGpu();
            private Font fNum, fCola; private float fontFuente = -1f;
            private StringFormat cenFmt;
            // EL CIRCULITO Y LA PASTILLA, HORNEADOS. Dibujar dos elipses suavizadas por fila costaba 80 us por
            // fila -mas que todo el resto de la fila junta- y la pastilla otros 55. Como son SIEMPRE la misma
            // figura, se pintan una vez a un mapa de bits y despues cada fila solo las copia. Lo unico que se
            // sigue dibujando por fila es el numero, que si cambia.
            private Bitmap bmpCirc, bmpCircCola, bmpCircSon;
            // Las pastillas se hornean POR ANCHO: cada texto mide lo suyo ("SONANDO" no mide lo mismo que
            // "EN COLA 12"), y con un ancho fijo el texto se salia -de ahi que se leyera "SONAND"-.
            private readonly Dictionary<int, Bitmap> hornoPastV = new Dictionary<int, Bitmap>();
            private readonly Dictionary<int, Bitmap> hornoPastA = new Dictionary<int, Bitmap>();
            private int hornoPastAlto = -1;
            private Bitmap Pastilla(bool ambar, int w, int h)
            {
                if (h != hornoPastAlto) { TiraPastillas(); hornoPastAlto = h; }
                Dictionary<int, Bitmap> horno = ambar ? hornoPastA : hornoPastV;
                Bitmap b;
                if (horno.TryGetValue(w, out b) && b != null) return b;
                b = HorneaPastilla(w, h, ambar ? SonAmbar : ColaVerde);
                horno[w] = b;
                return b;
            }
            private void TiraPastillas()
            {
                foreach (Bitmap b in hornoPastV.Values) if (b != null) b.Dispose();
                foreach (Bitmap b in hornoPastA.Values) if (b != null) b.Dispose();
                hornoPastV.Clear(); hornoPastA.Clear();
            }
            private Bitmap[] bmpDig, bmpDigCola, bmpDigSon;   // las diez cifras, tambien horneadas
            private int hornoLado = -1, digW, digH;
            private float hornoCuerpo = -1f;
            private static Bitmap HorneaCirculo(int lado, Color c, bool fuerte)
            {
                if (lado < 3) lado = 3;
                Bitmap b = new Bitmap(lado, lado, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    RectangleF r = new RectangleF(0.6f, 0.6f, lado - 1.7f, lado - 1.7f);
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(fuerte ? 70 : 34, c))) g.FillEllipse(sb, r);
                    using (Pen p = new Pen(Color.FromArgb(fuerte ? 150 : 60, c), 1f)) g.DrawEllipse(p, r);
                }
                return b;
            }
            private static Bitmap HorneaPastilla(int w, int h, Color c)
            {
                if (w < 4) w = 4;
                if (h < 4) h = 4;
                Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath gp = RoundedPath(new Rectangle(0, 0, w - 1, h - 1), h / 2))
                    {
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(52, c))) g.FillPath(sb, gp);
                        using (Pen p = new Pen(Color.FromArgb(110, c), 1f)) g.DrawPath(p, gp);
                    }
                }
                return b;
            }
            // Las diez cifras, de un ancho comun para que los numeros queden alineados entre filas.
            private void HorneaCifras(Font f, Color c, out Bitmap[] destino, out int w, out int h)
            {
                w = 6; h = 8;
                using (Bitmap med = new Bitmap(4, 4))
                using (Graphics g = Graphics.FromImage(med))
                    for (int d = 0; d < 10; d++)
                    {
                        SizeF sz = g.MeasureString(((char)('0' + d)).ToString(), f, 999, StringFormat.GenericTypographic);
                        if (sz.Width > w) w = (int)Math.Ceiling(sz.Width);
                        if (sz.Height > h) h = (int)Math.Ceiling(sz.Height);
                    }
                w += 2; h += 2;
                destino = new Bitmap[10];
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    using (SolidBrush sb = new SolidBrush(c))
                        for (int d = 0; d < 10; d++)
                        {
                            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                            using (Graphics g = Graphics.FromImage(b))
                            {
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                                g.Clear(Color.Transparent);
                                g.DrawString(((char)('0' + d)).ToString(), f, sb, new RectangleF(0, 0, w, h), sf);
                            }
                            destino[d] = b;
                        }
                }
            }
            // Pinta un numero copiando sus cifras ya horneadas, centrado en 'cx'.
            // marca: 0 = normal, 1 = pedida (verde), 2 = sonando ahora (ambar)
            private void PintaNumero(Graphics g, int v, float cx, float cy, int marca)
            {
                Bitmap[] set = (marca == 2) ? bmpDigSon : (marca == 1) ? bmpDigCola : bmpDig;
                if (set == null) return;
                if (v < 0) v = 0;
                int cifras = (v < 10) ? 1 : (v < 100) ? 2 : (v < 1000) ? 3 : 4;
                int x = (int)(cx - cifras * digW * 0.5f), y = (int)(cy - digH * 0.5f);
                int div = 1;
                for (int k = 1; k < cifras; k++) div *= 10;
                for (int k = 0; k < cifras; k++)
                {
                    int d = (v / div) % 10; div /= 10;
                    if (d >= 0 && d <= 9) g.DrawImageUnscaled(set[d], x + k * digW, y);
                }
            }

            // PASTILLA DE GENERO ya dibujada. Se hornea UNA por etiqueta (son una docena en toda la rocola)
            // y luego cada fila solo la copia. Dibujar por fila un rectangulo redondeado con borde mas su
            // texto era justo lo que encarecia la lista, y la lista se repinta entera al moverse por ella.
            private readonly Dictionary<string, Bitmap> hornoGen = new Dictionary<string, Bitmap>();
            private float hornoGenCuerpo = -1f;
            private Bitmap HorneaGenero(string etiqueta)
            {
                if (string.IsNullOrEmpty(etiqueta) || fCola == null) return null;
                if (Math.Abs(hornoGenCuerpo - fCola.SizeInPoints) > 0.01f) { TiraHornoGen(); hornoGenCuerpo = fCola.SizeInPoints; }
                Bitmap b;
                if (hornoGen.TryGetValue(etiqueta, out b)) return b;
                Color c = ColorDeEtiqueta(etiqueta);
                int w, h;
                using (Bitmap med = new Bitmap(4, 4))
                using (Graphics mg = Graphics.FromImage(med))
                {
                    SizeF sz = mg.MeasureString(etiqueta, fCola, 999, StringFormat.GenericTypographic);
                    w = (int)Math.Ceiling(sz.Width) + 16;
                    h = (int)Math.Ceiling(sz.Height) + 5;
                }
                if (w < 8) w = 8; if (h < 8) h = 8;
                b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.Clear(Color.Transparent);
                    // El MISMO aspecto que la pastilla de genero de los resultados del buscador: relleno muy
                    // tenue del color del genero, borde fino y el texto en ese color aclarado.
                    using (GraphicsPath gp = RoundedPath(new Rectangle(0, 0, w - 1, h - 1), Math.Min(7, (h - 1) / 2)))
                    {
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(60, c))) g.FillPath(sb, gp);
                        using (Pen pn = new Pen(Color.FromArgb(120, c), 1f)) g.DrawPath(pn, gp);
                    }
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                        sf.FormatFlags = StringFormatFlags.NoWrap;
                        using (SolidBrush sb = new SolidBrush(Lighten(c, 0.45)))
                            g.DrawString(etiqueta, fCola, sb, new RectangleF(0, 0, w, h), sf);
                    }
                }
                hornoGen[etiqueta] = b;
                return b;
            }
            private void TiraHornoGen()
            {
                foreach (Bitmap b in hornoGen.Values) if (b != null) b.Dispose();
                hornoGen.Clear();
            }

            private void EnsureHorno(int lado, int ph)
            {
                if (bmpDig == null || Math.Abs(hornoCuerpo - fNum.SizeInPoints) > 0.01f)
                {
                    if (bmpDig != null) for (int i = 0; i < bmpDig.Length; i++) if (bmpDig[i] != null) bmpDig[i].Dispose();
                    if (bmpDigCola != null) for (int i = 0; i < bmpDigCola.Length; i++) if (bmpDigCola[i] != null) bmpDigCola[i].Dispose();
                    if (bmpDigSon != null) for (int i = 0; i < bmpDigSon.Length; i++) if (bmpDigSon[i] != null) bmpDigSon[i].Dispose();
                    int w1, h1, w2, h2, w3, h3;
                    HorneaCifras(fNum, Color.FromArgb(178, 190, 218), out bmpDig, out w1, out h1);
                    HorneaCifras(fNum, ColaVerde, out bmpDigCola, out w2, out h2);
                    HorneaCifras(fNum, SonAmbar, out bmpDigSon, out w3, out h3);
                    digW = Math.Max(w1, Math.Max(w2, w3)); digH = Math.Max(h1, Math.Max(h2, h3));
                    hornoCuerpo = fNum.SizeInPoints;
                }
                if (bmpCirc == null || hornoLado != lado)
                {
                    if (bmpCirc != null) bmpCirc.Dispose();
                    if (bmpCircCola != null) bmpCircCola.Dispose();
                    if (bmpCircSon != null) bmpCircSon.Dispose();
                    bmpCirc = HorneaCirculo(lado, NumGris, false);
                    bmpCircCola = HorneaCirculo(lado, ColaVerde, true);
                    bmpCircSon = HorneaCirculo(lado, SonAmbar, true);
                    hornoLado = lado;
                }
                if (hornoPastAlto != ph) { TiraPastillas(); hornoPastAlto = ph; }   // otro alto de fila: se rehornean
            }
            private static readonly Color NumGris = Color.FromArgb(150, 170, 220);

            public SongListView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Shade(DefaultAccent, 0.76);
            }

            private void ClearFontDrawing()
            {
                SueltaDibujo(ref fNum); SueltaDibujo(ref fCola);
                SueltaBitmaps(ref bmpDig); SueltaBitmaps(ref bmpDigCola); SueltaBitmaps(ref bmpDigSon);
                if (hornoGen != null) TiraHornoGen();
                SueltaDibujo(ref hornoPag); SueltaDibujo(ref hornoAnt);
                fontFuente = hornoCuerpo = hornoGenCuerpo = -1f;
                slideTick = -1;
            }
            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                // Familia y estilo tambien importan: la firma anterior solo veia el cuerpo.
                ClearFontDrawing();
                Invalidate();
            }
            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing)
                    {
                        ClearFontDrawing();
                        if (hornoPastV != null && hornoPastA != null) TiraPastillas();
                        SueltaDibujo(ref bmpCirc); SueltaDibujo(ref bmpCircCola); SueltaDibujo(ref bmpCircSon);
                        SueltaDibujo(ref hlB); SueltaDibujo(ref hlP); SueltaDibujo(ref txtB); SueltaDibujo(ref txtFmt);
                        SueltaDibujo(ref bgB); SueltaDibujo(ref durB); SueltaDibujo(ref durFmt);
                        SueltaDibujo(ref colaB); SueltaDibujo(ref cenFmt);
                        SueltaAcabadoGpu();   // el velo y la clave de color de la lista transparente (solo con tarjeta)
                    }
                }
                finally { base.Dispose(disposing); }
            }

            // ================= HOJEAR DENTRO DE UN DISCO =================
            // Un disco con mas de una pagina cambiaba de pagina DE GOLPE: catorce canciones se convertian en
            // otras catorce sin que nada dijera hacia donde te habias movido, y era facil perderse. Ahora la
            // pagina que dejas SALE por un lado y la que eliges ENTRA por el otro, con el mismo frenado suave
            // y la misma duracion que ya usa la pared de discos: la rocola se mueve toda igual.
            //
            // Y se puede hacer sin que cueste nada porque las dos paginas van HORNEADAS: dibujar una cuesta
            // 1,8 ms y durante la animacion hacen falta las dos en cada cuadro (3,7 ms, que en el equipo de
            // XP serian bastantes mas y romperian el movimiento). Horneadas, cada cuadro son dos copias.
            public const int SlideMs = 230;
            private int slideTick = -1; private float slideDesde;
            private Bitmap hornoPag, hornoAnt;          // la pagina de ahora y la que se esta yendo
            private long hornoSig = long.MinValue;      // que hay horneado (pagina, seleccion, tamano, datos...)
            private int hornoAncho = -1, hornoAlto = -1;

            // dir: +1 la nueva entra por la DERECHA (vas hacia delante), -1 por la izquierda.
            public void StartSlide(int dir)
            {
                int w = ClientSize.Width;
                if (w < 8 || hornoPag == null) { slideTick = -1; return; }   // sin nada horneado no hay de donde salir
                // Los dos mapas de bits se INTERCAMBIAN, no se tiran: uno pasa a ser la pagina que se va y en
                // el otro se dibuja encima la que entra. Tirarlos obligaria a reservar dos megas en cada
                // cambio de pagina, y hojeando deprisa eso son recogidas de basura -o sea, tirones- justo
                // mientras la animacion esta corriendo.
                Bitmap t = hornoAnt; hornoAnt = hornoPag; hornoPag = t;
                hornoSig = long.MinValue;
                slideDesde = dir * (float)w; slideTick = Environment.TickCount;
            }
            public bool Sliding { get { return slideTick >= 0 && Environment.TickCount - slideTick < SlideMs; } }
            // Olvida lo horneado y corta cualquier hojeo en curso. Se llama al ABRIR un disco: si no, venir de
            // la pagina 3 de otro disco haria "hojear" hasta la 1 del nuevo, que no significa nada.
            public void OlvidaPagina()
            {
                slideTick = -1;
                if (hornoAnt != null) { hornoAnt.Dispose(); hornoAnt = null; }
                if (hornoPag != null) { hornoPag.Dispose(); hornoPag = null; }
                hornoSig = long.MinValue; hornoAncho = hornoAlto = -1;
            }
            // Cuanto lleva hecha la animacion (0..1). Entra en la firma de la textura de UI del modo GPU: sin
            // eso la textura no se rehace y la animacion no se veria mover.
            public float SlidePaso
            {
                get
                {
                    if (slideTick < 0) return 1f;
                    int dt = Environment.TickCount - slideTick;
                    return (dt >= SlideMs) ? 1f : (dt / (float)SlideMs);
                }
            }
            // CURVA del hojeo (la misma que la pared de discos, para que las dos se muevan igual): devuelve
            // cuanto FALTA por recorrer, de 1 (recien empieza) a 0 (ya llego).
            internal static float CurvaLista(int dt)
            {
                if (dt < 0 || dt >= SlideMs) return 0f;
                float t = dt / (float)SlideMs;
                return (1f - t) * (1f - t) * (1f - t);   // frenado suave al llegar (cubico)
            }
            private float DesplazaAhora()
            {
                if (slideTick < 0) return 0f;
                int dt = Environment.TickCount - slideTick;
                if (dt < 0 || dt >= SlideMs) { slideTick = -1; return 0f; }   // el mapa de la pagina vieja se GUARDA: es el otro lado del intercambio
                return slideDesde * CurvaLista(dt);
            }

            // Lo que identifica al mapa de bits horneado. Si algo de esto cambia, hay que volver a dibujarlo.
            private long FirmaPagina(int w, int h)
            {
                long k = 17;
                k = k * 31 + PageStart; k = k * 31 + Sel; k = k * 31 + Datos;
                k = k * 31 + w; k = k * 31 + h; k = k * 31 + RowH;
                k = k * 31 + Accent.ToArgb(); k = k * 31 + BackColor.ToArgb();
                k = k * 31 + (int)(Font.SizeInPoints * 64f);
                k = k * 31 + ((GetCount != null) ? GetCount() : 0);
#if ROCOLA_GPU
                if (Vivo != 0) { k = k * 31 + Vivo; k = k * 31 + EsperaVer; }   // lo que cambia con la tarjeta (DiscoVivoGpu.cs)
#endif
                return k;
            }

            // Asegura que 'hornoPag' es la pagina de ahora, dibujada tal cual. El fondo del horneado es OPACO
            // -el mismo relleno que pinta la lista- asi que copiarlo despues da EXACTAMENTE los mismos pixeles
            // que dibujarlo directo. (Hornear sobre transparente NO valdria: el texto suavizado sale mas fino.)
            private void AseguraHorneada(int w, int h)
            {
                long sig = FirmaPagina(w, h);
                if (hornoPag != null && hornoSig == sig && hornoAncho == w && hornoAlto == h) return;
                if (hornoPag == null || hornoAncho != w || hornoAlto != h)
                {
                    if (hornoPag != null) hornoPag.Dispose();
                    if (hornoAnt != null && (hornoAncho != w || hornoAlto != h)) { hornoAnt.Dispose(); hornoAnt = null; slideTick = -1; }
                    hornoPag = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    hornoAncho = w; hornoAlto = h;
                }
#if ROCOLA_GPU
                eqIdx = -1;   // donde van las barritas del ecualizador: lo vuelve a apuntar este horneado (DiscoVivoGpu.cs)
#endif
                using (Graphics gb = Graphics.FromImage(hornoPag)) PintaPagina(gb, PageStart);
                hornoSig = sig;
            }

            // Lo que ve el cliente: la pagina horneada, y mientras se hojea, las DOS deslizandose.
            public void RenderContent(Graphics g)
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                AseguraHorneada(w, h);
                float dx = DesplazaAhora();
                if (dx == 0f || hornoAnt == null) { g.DrawImageUnscaled(hornoPag, 0, 0); return; }
                // La que SE VA sale por el lado contrario al que entra la nueva.
                g.DrawImageUnscaled(hornoAnt, (int)(dx - slideDesde), 0);
                g.DrawImageUnscaled(hornoPag, (int)dx, 0);
            }

            public int RowsPerPage { get { int ih = Math.Max(1, RowH); return Math.Max(1, ClientSize.Height / ih); } }

            protected override void OnPaintBackground(PaintEventArgs e) { /* todo en OnPaint */ }
            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }

            // Dibuja la pagina visible en cualquier Graphics (el control, o la textura de UI del modo GPU).
            // El fondo es OPACO (como el ListBox): la lista tapa el starfield donde esta (igual que en CPU).
            // Dibuja UNA pagina entera (la que empiece en 'arranque'). Antes esto era RenderContent tal cual;
            // se separo para poder HORNEARLA a un mapa de bits y despues solo copiarla: dibujar una pagina
            // cuesta 1,8 ms -son catorce filas con su texto, su numero, su genero y su duracion- y al hojear
            // hacen falta DOS a la vez, en cada cuadro de la animacion. Horneada, cada cuadro es una copia.
            private void PintaPagina(Graphics g, int arranque)
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                int n = (GetCount != null) ? GetCount() : 0;
                int rpp = RowsPerPage;
                if (hlB == null) { hlB = new SolidBrush(Color.Black); hlP = new Pen(Color.Black, 1.4f); }
                if (txtB == null)
                {   // GDI+ (DrawString), NO TextRenderer: TextRenderer usa GDI e IGNORA la traslacion del Graphics,
                    txtB = new SolidBrush(Color.White);   // asi que sobre la textura de UI (trasladada) el texto caia en 0,0
                    txtFmt = new StringFormat();
                    txtFmt.FormatFlags = StringFormatFlags.NoWrap;
                    txtFmt.Trimming = StringTrimming.EllipsisCharacter;
                    txtFmt.Alignment = StringAlignment.Near;
                    txtFmt.LineAlignment = StringAlignment.Center;
                }
                if (bgB == null) bgB = new SolidBrush(BackColor); else bgB.Color = BackColor;
                if (colaB == null)
                {
                    colaB = new SolidBrush(Color.Black);
                    cenFmt = CreaSfDibuja(StringAlignment.Center);
                }
                float cuerpo = Font.SizeInPoints;
                if (fNum == null || Math.Abs(fontFuente - cuerpo) > 0.01f)
                {
                    if (fNum != null) fNum.Dispose();
                    if (fCola != null) fCola.Dispose();
                    fNum = new Font(Font.FontFamily, cuerpo * 0.78f, FontStyle.Bold);
                    fCola = new Font(Font.FontFamily, cuerpo * 0.72f, FontStyle.Bold);
                    fontFuente = cuerpo;
                }
                g.FillRectangle(bgB, 0, 0, w, h);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float rad = Math.Min(11f, RowH * 0.34f);        // el circulito del numero
#if ROCOLA_GPU
                if ((Vivo & VivoGrande) != 0) rad = RadioGrande(RowH);   // lista grande (DiscoVivoGpu.cs): crece con la fila
#endif
                float xTexto = 16 + rad * 2 + 10;
                // ALTO de las marcas. Sale del circulito del numero, pero ese va TOPADO (rad no pasa de 11)
                // para que en una fila alta no quede un circulo gigante... y la letra si crece con la fila. En
                // una pantalla grande eso dejaba el texto MAS ALTO que su propia pastilla, asomando por arriba
                // y por abajo. Asi que la pastilla crece tambien si hace falta para envolver a su letra.
                int hCola = Math.Max((int)(rad * 1.64f), fCola.Height + 4);
                EnsureHorno((int)(rad * 2), hCola);
#if ROCOLA_GPU
                if ((Vivo & VivoGrande) != 0) PlanPaginaGpu(g, rpp, n, w, xTexto, hCola);   // el genero: en toda la pagina o en ninguna fila
#endif
                for (int row = 0; row < rpp; row++)
                {
                    int idx = arranque + row;
                    if (idx >= n) break;
                    Rectangle r = new Rectangle(0, row * RowH, w, RowH);
                    if (idx == Sel)
                    {
                        Rectangle hl = new Rectangle(r.X + 4, r.Y + 3, r.Width - 8, r.Height - 6);
                        hlB.Color = Color.FromArgb(150, Lighten(Accent, 0.15));
                        hlP.Color = Color.FromArgb(180, Lighten(Accent, 0.45));
                        using (GraphicsPath gp = RoundedPath(hl, 9)) { g.FillPath(hlB, gp); g.DrawPath(hlP, gp); }
                    }
                    // EL NUMERO DE PISTA en un circulito, y en VERDE si esa cancion ya esta pedida. Antes no
                    // habia forma de saber que ya la habias puesto: la volvias a pedir y gastabas otro credito.
                    // (y en AMBAR si es la que esta sonando en este momento)
                    int pos = (GetQueuePos != null) ? GetQueuePos(idx) : 0;
                    bool suena = (GetSonando != null) && GetSonando(idx);
                    bool enCola = pos > 0;
                    int marca = suena ? 2 : (enCola ? 1 : 0);   // el circulito manda lo mas urgente: que SUENA
                    float cy = r.Y + RowH * 0.5f;
                    g.DrawImageUnscaled((marca == 2) ? bmpCircSon : (marca == 1) ? bmpCircCola : bmpCirc, 16, (int)(cy - rad));
                    PintaNumero(g, idx + 1, 16 + rad, cy, marca);
                    // DURACION a la derecha (m:ss). Se dibuja primero para saber cuanto ancho quitarle al
                    // titulo: asi un nombre largo se corta con "..." ANTES de encimarse con la duracion.
                    string dur = (GetRight != null) ? GetRight(idx) : "";
                    int durW = 0;
                    if (!string.IsNullOrEmpty(dur))
                    {
                        if (durB == null)
                        {
                            durB = new SolidBrush(Color.White);
                            durFmt = new StringFormat();
                            durFmt.FormatFlags = StringFormatFlags.NoWrap;
                            durFmt.Alignment = StringAlignment.Far;
                            durFmt.LineAlignment = StringAlignment.Center;
                        }
                        durW = 58;
#if ROCOLA_GPU
                        if ((Vivo & VivoGrande) != 0) durW = AnchoDuracion(g);   // con la letra grande no caben 58 px
#endif
                        durB.Color = (idx == Sel) ? Color.FromArgb(235, 245, 255) : Color.FromArgb(175, 190, 215);
                        g.DrawString(dur, Font, durB, new Rectangle(r.Right - durW - 14, r.Y, durW, r.Height), durFmt);
                    }
                    // EL GENERO, en una pastillita, justo al lado de la duracion. Es el mismo genero y el
                    // mismo color que ya salen en los resultados del buscador: la rocola habla siempre igual.
                    int quitaGen = 0;
                    string gen = (GetGenero != null) ? GetGenero(idx) : null;
#if ROCOLA_GPU
                    // Con la tarjeta grafica (DiscoVivoGpu.cs) manda el titulo: una pedida dice sus minutos si caben, y
                    // con la lista grande el genero sale solo si el titulo cabe entero a su lado.
                    string tColaGpu = null;
                    if ((Vivo & (VivoGrande | VivoMinutos)) != 0) tColaGpu = PlanFilaGpu(g, idx, pos, enCola, suena, r, durW, xTexto, hCola, ref gen);
#endif
                    if (!string.IsNullOrEmpty(gen))
                    {
                        Bitmap pg = HorneaGenero(gen);
                        // Solo si sobra sitio: en una pantalla estrecha manda el nombre de la cancion.
                        if (pg != null && pg.Width + 18 <= (r.Width - (int)xTexto) / 2)
                        {
                            // Los 18 son los huecos de los dos lados: asi la pastilla nunca llega a rozar ni la
                            // duracion (a su derecha) ni la marca de "ya pedida" ni el nombre (a su izquierda).
                            quitaGen = pg.Width + 18;
                            g.DrawImageUnscaled(pg, r.Right - 20 - durW - pg.Width, (int)(cy - pg.Height * 0.5f));
                        }
                    }
                    // LAS DOS MARCAS CONVIVEN. Una cancion puede estar sonando Y estar pedida otra vez, y el
                    // cliente necesita saber las dos cosas: que la esta oyendo, y que ya la va a volver a oir.
                    // Antes la de la cola tapaba a la de sonando y parecia que no estaba sonando.
                    // Cada pastilla se mide POR SU TEXTO: con un ancho fijo, "SONANDO" no cabia y salia
                    // "SONAND". El triangulito de play delante deja claro cual es incluso sin leer la palabra.
                    int quitaCola = 0;
                    float xMarca = r.Right - 14 - durW - quitaGen;
                    if (enCola)
                    {
                        string t = "EN COLA " + pos;
#if ROCOLA_GPU
                        if (tColaGpu != null) t = tColaGpu;   // "EN COLA 3 · en ~9 min"
#endif
                        int wp = AnchoTexto(g, t, fCola) + hCola;
                        xMarca -= wp;
                        RectangleF cr = new RectangleF(xMarca, cy - hCola * 0.5f, wp, hCola);
                        g.DrawImageUnscaled(Pastilla(false, wp, hCola), (int)cr.X, (int)cr.Y);
                        colaB.Color = ColaVerde;
                        g.DrawString(t, fCola, colaB, cr, cenFmt);
                        xMarca -= 8; quitaCola += wp + 8;
                    }
#if ROCOLA_GPU
                    // Con el ecualizador (DiscoVivoGpu.cs) la pastilla de SONANDO lleva el hueco de las barritas en vez
                    // del triangulito, y ya no hace falta la de siempre.
                    if (suena && (Vivo & VivoEq) != 0) { quitaCola += PastillaSonandoEq(g, idx, ref xMarca, cy, hCola); suena = false; }
#endif
                    if (suena)
                    {
                        // U+25BA y no U+25B6: son dos triangulitos casi identicos, pero el otro no esta en el
                        // juego de simbolos que trae Windows XP y alli saldria un rectangulo vacio.
                        const string t = "\u25BA SONANDO";
                        int wp = AnchoTexto(g, t, fCola) + hCola;
                        xMarca -= wp;
                        RectangleF cr = new RectangleF(xMarca, cy - hCola * 0.5f, wp, hCola);
                        g.DrawImageUnscaled(Pastilla(true, wp, hCola), (int)cr.X, (int)cr.Y);
                        colaB.Color = SonAmbar;
                        g.DrawString(t, fCola, colaB, cr, cenFmt);
                        quitaCola += wp + 8;
                    }
                    if (quitaCola > 0) quitaCola += 4;   // un pelo de aire antes del nombre
                    // El nombre acaba SEIS PIXELES antes de lo primero que haya a su derecha. Antes se restaba
                    // un margen distinto del que usa la marca de "ya pedida", y un nombre largo llegaba a
                    // tocarla; ahora se calcula desde el mismo borde para los dos y siempre queda separacion.
                    int derContenido = r.Right - 14 - durW - quitaGen - quitaCola;
                    Rectangle tr = new Rectangle(r.X + (int)xTexto, r.Y, Math.Max(10, derContenido - 6 - r.X - (int)xTexto), r.Height);
                    string text = (GetText != null) ? GetText(idx) : "";
                    g.DrawString(text, Font, txtB, tr, txtFmt);   // GDI+: respeta la traslacion del Graphics (en el control es identidad; en la textura de UI lleva la lista a su sitio)
                }
            }

            private int RowIndexAt(int y) { int row = y / Math.Max(1, RowH); return PageStart + row; }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int idx = RowIndexAt(e.Y);
                if (GetCount != null && idx >= 0 && idx < GetCount() && OnPick != null) OnPick(idx);
                base.OnMouseClick(e);
            }
            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                int idx = RowIndexAt(e.Y);
                if (GetCount != null && idx >= 0 && idx < GetCount() && OnActivate != null) OnActivate(idx);
                base.OnMouseDoubleClick(e);
            }
        }

        // ==========  ETIQUETAS (ID3)  ==========
        private class Track
        {
            public string Path;
            public string Artist;
            public string Title;
            public string Album;
            public string Genre;

            // En la lista del disco se muestra EXACTAMENTE el nombre del archivo (sin extension), tal
            // cual esta en la carpeta: cero adivinanzas. Title/Artist se siguen usando por dentro para
            // "Sonando:", el color por genero y el buscador.
            public override string ToString()
            {
                try { return System.IO.Path.GetFileNameWithoutExtension(Path); }
                catch { return Title; }
            }

            private static readonly string[] Id3v1Genres = {
                "Blues","Classic Rock","Country","Dance","Disco","Funk","Grunge","Hip-Hop","Jazz","Metal",
                "New Age","Oldies","Other","Pop","R&B","Rap","Reggae","Rock","Techno","Industrial",
                "Alternative","Ska","Death Metal","Pranks","Soundtrack","Euro-Techno","Ambient","Trip-Hop","Vocal","Jazz+Funk",
                "Fusion","Trance","Classical","Instrumental","Acid","House","Game","Sound Clip","Gospel","Noise",
                "AlternRock","Bass","Soul","Punk","Space","Meditative","Instrumental Pop","Instrumental Rock","Ethnic","Gothic",
                "Darkwave","Techno-Industrial","Electronic","Pop-Folk","Eurodance","Dream","Southern Rock","Comedy","Cult","Gangsta",
                "Top 40","Christian Rap","Pop/Funk","Jungle","Native American","Cabaret","New Wave","Psychadelic","Rave","Showtunes",
                "Trailer","Lo-Fi","Tribal","Acid Punk","Acid Jazz","Polka","Retro","Musical","Rock & Roll","Hard Rock"
            };

            private static string NormalizeGenre(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return "";
                raw = raw.Trim();
                if (raw.Length > 0 && raw[0] == '(')
                {
                    int close = raw.IndexOf(')');
                    if (close > 1)
                    {
                        string num = raw.Substring(1, close - 1);
                        int n;
                        if (int.TryParse(num, out n) && n >= 0 && n < Id3v1Genres.Length)
                        {
                            string rest = raw.Substring(close + 1).Trim();
                            if (rest.Length > 0) return rest;
                            return Id3v1Genres[n];
                        }
                    }
                }
                int num2;
                if (int.TryParse(raw, out num2) && num2 >= 0 && num2 < Id3v1Genres.Length) return Id3v1Genres[num2];
                return raw;
            }

            // RAPIDO: no abre el archivo. Titulo/artista salen del nombre y la carpeta.
            // (Con +100k canciones, abrir cada MP3 para leer ID3 era lo que tardaba.)
            public static Track FromFile(string path)
            {
                Track t = new Track();
                t.Path = Pool(path); t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                string fa, ft;
                bool split = SplitArtist(name, out fa, out ft);
                if (split && AllDigits(fa))
                {
                    // "01 - X - Y": quitar el numero de pista y reanalizar -> artista X, titulo Y.
                    string fa2, ft2;
                    if (SplitArtist(ft, out fa2, out ft2) && !AllDigits(fa2)) { t.Artist = fa2; t.Title = ft2; }
                    else t.Title = name;   // "01 - Titulo": igual que siempre
                }
                else if (split) { t.Artist = fa; t.Title = ft; }
                else { t.Title = name; }
                if (string.IsNullOrEmpty(t.Artist))
                {
                    string d = System.IO.Path.GetDirectoryName(path);
                    string leaf = (d != null) ? LeafName(d) : "Desconocido";
                    string da, dn;
                    if (SplitArtist(leaf, out da, out dn)) t.Artist = da; else t.Artist = leaf;
                }
                return t;
            }

            // EL GENERO A SOLAS, LEYENDO LO MENOS POSIBLE.
            //
            // La etiqueta de texto de un MP3 puede llevar la CARATULA dentro y medir megas. Leerla entera para
            // sacar UNA palabra es tirar el disco a la basura, y el paseo de fondo hacia eso con TODAS las
            // canciones de la biblioteca: con veinte mil archivos son gigas de lectura inutil.
            //
            // La etiqueta va por MARCOS, y cada marco dice de antemano cuanto mide. Aqui se leen solo las diez
            // letras de cabecera de cada marco y se SALTAN los que no interesan -la caratula, sobre todo- sin
            // llegar a leerlos. De un archivo con caratula de 300 KB se leen unos cientos de bytes. Si la
            // etiqueta larga no trae genero, se prueba la corta del final (128 bytes); y si tampoco, se queda
            // sin genero, que es decoracion, en vez de leerse el archivo entero.
            private static int SyncSafe4(byte[] b, int p)
            {
                if (b == null || p < 0 || p + 4 > b.Length) return -1;
                if ((b[p] | b[p + 1] | b[p + 2] | b[p + 3]) >= 128) return -1;
                return (b[p] << 21) | (b[p + 1] << 14) | (b[p + 2] << 7) | b[p + 3];
            }
            private static int BigEndian4(byte[] b, int p)
            {
                if (b == null || p < 0 || p + 4 > b.Length) return -1;
                uint v = ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3];
                return v > int.MaxValue ? -1 : (int)v;
            }
            private static byte[] DeUnsync(byte[] b, int start, int size)
            {
                if (b == null || start < 0 || size < 0 || start + size > b.Length) return new byte[0];
                byte[] d = new byte[size]; int n = 0, end = start + size;
                for (int i = start; i < end; i++)
                {
                    byte v = b[i]; d[n++] = v;
                    if (v == 0xff && i + 1 < end && b[i + 1] == 0x00) i++;
                }
                if (n == d.Length) return d;
                byte[] exact = new byte[n]; Array.Copy(d, 0, exact, 0, n); return exact;
            }

            // EL ARTISTA, SOLO SI LO DICE LA ETIQUETA. Lo usa el indice de origen de la IA (IaOrigenDe): agrupar
            // canciones por artista solo vale si el dato es fiable, asi que aqui NO se adivina del nombre del
            // archivo ni de la carpeta (eso lo hace FromFile para la pantalla). Se leen solo los marcos de texto;
            // la caratula se salta sin leerla. Es estatico y no toca la biblioteca: se puede llamar desde el hilo
            // de fondo. Sin etiqueta, o si no es un MP3: cadena vacia, que es "no se sabe".
            public static string SoloArtista(string path)
            {
                Track t = new Track();
                t.Path = path; t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                try
                {
                    if (System.IO.Path.GetExtension(path).ToLowerInvariant() != ".mp3") return "";
                    ReadId3(path, t);
                }
                catch { }
                return (t.Artist ?? "").Trim();
            }

            public static string SoloGenero(string path)
            {
                Track t = new Track();
                t.Path = path; t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                try
                {
                    if (System.IO.Path.GetExtension(path).ToLowerInvariant() != ".mp3") return "";
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                        ReadGenreStream(fs, t);
                }
                catch { }
                return t.Genre ?? "";
            }

            private static void ReadGenreStream(Stream fs, Track t)
            {
                // Si v2 esta truncada/corrupta, intentar v1 sin abrir otro archivo.
                try
                {
                    fs.Position = 0;
                    byte[] cab = new byte[10];
                    if (ReadFully(fs, cab, 0, 10) == 10 && cab[0] == (byte)'I' && cab[1] == (byte)'D' && cab[2] == (byte)'3'
                        && cab[3] >= 3 && cab[3] <= 4)   // 2.2 y versiones futuras tienen otra forma
                    {
                        int major = cab[3];
                        // En 2.3 la des-sincronizacion cubre el cuerpo entero y cambia sus posiciones.
                        // Es un caso raro: se usa el lector completo, que la revierte antes de separar marcos.
                        if (major <= 3 && (cab[5] & 0x80) != 0)
                        {
                            ReadId3Stream(fs, t);
                            return;
                        }
                        long size = SyncSafe4(cab, 6);
                        long fin = 10 + size;
                        if (size <= 0 || fin > fs.Length) throw new InvalidDataException("ID3 truncado");
                        if ((cab[5] & 0x40) != 0)
                        {
                            byte[] eh = new byte[4]; if (ReadFully(fs, eh, 0, 4) != 4) throw new EndOfStreamException();
                            int es = (major >= 4) ? SyncSafe4(eh, 0) : BigEndian4(eh, 0);
                            long resta = (major >= 4) ? es - 4L : es;
                            if (es < (major >= 4 ? 6 : 0) || resta < 0 || fs.Position + resta > fin) throw new InvalidDataException("ID3 extendido invalido");
                            fs.Position += resta;
                        }
                        byte[] fh = new byte[10];
                        while (fs.Position + 10 <= fin)
                        {
                            if (ReadFully(fs, fh, 0, 10) != 10) break;
                            if (fh[0] == 0) break;   // relleno: se acabaron los marcos
                            int fsz;
                            if (major >= 4) fsz = SyncSafe4(fh, 4);
                            else fsz = BigEndian4(fh, 4);
                            if (fsz <= 0 || fs.Position + fsz > fin) break;
                            bool esGenero = (fh[0] == (byte)'T' && fh[1] == (byte)'C' && fh[2] == (byte)'O' && fh[3] == (byte)'N');
                            if (esGenero && fsz <= 4096)
                            {
                                // Se le pasa el marco entero (cabecera incluida) al mismo lector de siempre,
                                // asi el genero se interpreta exactamente igual que cuando se lee todo.
                                byte[] uno = new byte[10 + fsz];
                                Array.Copy(fh, 0, uno, 0, 10);
                                int leidos = 0, r;
                                while (leidos < fsz && (r = fs.Read(uno, 10 + leidos, fsz - leidos)) > 0) leidos += r;
                                ParseId3v2(uno, 10 + leidos, major, cab[5] & 0x80, t);
                                if (!string.IsNullOrEmpty(t.Genre)) break;
                            }
                            else fs.Position += fsz;   // la caratula y lo demas: ni se tocan
                        }
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(t.Genre)) { try { ReadId3v1Stream(fs, t); } catch { } }
            }

            // Lee el ID3 bajo demanda (una sola vez), sobre todo para el GENERO (color de fondo).
            private bool tagsLoaded = false;
            public void EnsureTags()
            {
                if (tagsLoaded) return;
                tagsLoaded = true;
                try { if (System.IO.Path.GetExtension(Path).ToLowerInvariant() == ".mp3") ReadId3(Path, this); }
                catch { }
            }

            private static void ReadId3(string path, Track t)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    ReadId3Stream(fs, t);
            }

            // Cabecera y etiqueta v1 usan el mismo handle; no reabrir el MP3 al cambiar cancion.
            private static void ReadId3Stream(Stream fs, Track t)
            {
                fs.Position = 0;
                byte[] header = new byte[10];
                if (ReadFully(fs, header, 0, 10) == 10 && header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3'
                    && header[3] >= 3 && header[3] <= 4)
                {
                    int major = header[3];
                    int size = SyncSafe4(header, 6);
                    if (size > 0 && size <= 33554432)   // tope 32MB: un encabezado CORRUPTO puede declarar hasta 256MB y ese new byte[] tumbaria la RAM de la rocola; se cae al ID3v1
                    {
                        int flags = header[5];
                        if (major <= 3 && (flags & 0x80) != 0)
                        {
                            // En 2.3 el escape global cambia posiciones: conservar el lector completo.
                            byte[] tag = new byte[size];
                            int read = ReadFully(fs, tag, 0, size);
                            tag = DeUnsync(tag, 0, read); read = tag.Length; flags &= ~0x80;
                            ParseId3v2(tag, read, major, flags, t);
                        }
                        else ReadId3Frames(fs, size, major, flags, t);
                    }
                }
                if (string.IsNullOrEmpty(t.Title) || string.IsNullOrEmpty(t.Artist) || string.IsNullOrEmpty(t.Album) || string.IsNullOrEmpty(t.Genre))
                    ReadId3v1Stream(fs, t);
            }

            // Lee solo los marcos de texto. Saltar APIC evita leer megabytes de portada al pulsar.
            // El parser existente interpreta los textos/flags; esta rutina solo recorre el archivo.
            private static void ReadId3Frames(Stream fs, int size, int major, int flags, Track t)
            {
                long end = Math.Min(fs.Length, fs.Position + size);
                if ((flags & 0x40) != 0)
                {
                    byte[] ext = new byte[4];
                    if (end - fs.Position < 4 || ReadFully(fs, ext, 0, 4) != 4) return;
                    int es = major >= 4 ? SyncSafe4(ext, 0) : BigEndian4(ext, 0);
                    long remaining = major >= 4 ? es - 4L : es;
                    if (es < (major >= 4 ? 6 : 0) || remaining < 0 || remaining > end - fs.Position) return;
                    fs.Seek(remaining, SeekOrigin.Current);
                }
                byte[] header = new byte[10];
                while (end - fs.Position >= 10)
                {
                    if (ReadFully(fs, header, 0, 10) != 10) return;
                    bool valid = true;
                    for (int i = 0; i < 4; i++)
                        if (!((header[i] >= 'A' && header[i] <= 'Z') || (header[i] >= '0' && header[i] <= '9'))) { valid = false; break; }
                    if (!valid) return;
                    int frameSize = major >= 4 ? SyncSafe4(header, 4) : BigEndian4(header, 4);
                    if (frameSize <= 0 || frameSize > end - fs.Position) return;
                    string id = Encoding.ASCII.GetString(header, 0, 4);
                    if (id == "TIT2" || id == "TPE1" || id == "TALB" || id == "TCON")
                    {
                        byte[] frame = new byte[10 + frameSize];
                        Array.Copy(header, frame, 10);
                        if (ReadFully(fs, frame, 10, frameSize) != frameSize) return;
                        ParseId3v2(frame, frame.Length, major, flags & 0x80, t);
                    }
                    else fs.Seek(frameSize, SeekOrigin.Current);
                }
            }

            private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
            {
                int done = 0, read;
                while (done < count && (read = stream.Read(buffer, offset + done, count - done)) > 0) done += read;
                return done;
            }

            private static void ParseId3v2(byte[] tag, int len, int major, Track t)
            { ParseId3v2(tag, len, major, 0, t); }

            private static void ParseId3v2(byte[] tag, int len, int major, int tagFlags, Track t)
            {
                int pos = 0;
                if ((tagFlags & 0x40) != 0)
                {
                    if (len < 4) return;
                    int es = (major >= 4) ? SyncSafe4(tag, 0) : BigEndian4(tag, 0);
                    int skip = (major >= 4) ? es : (es < 0 ? -1 : es + 4);
                    if (es < (major >= 4 ? 6 : 0) || skip < 4 || skip > len) return;
                    pos = skip;
                }
                while (pos + 10 <= len)
                {
                    string id = Encoding.ASCII.GetString(tag, pos, 4);
                    if (id[0] == '\0') break;
                    bool valid = true;
                    for (int i = 0; i < 4; i++) { char c = id[i]; if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) { valid = false; break; } }
                    if (!valid) break;
                    int frameSize;
                    if (major >= 4) frameSize = SyncSafe4(tag, pos + 4);
                    else frameSize = BigEndian4(tag, pos + 4);
                    int startPos = pos + 10;
                    if (frameSize <= 0 || startPos + frameSize > len) break;
                    if (id == "TIT2" || id == "TPE1" || id == "TALB" || id == "TCON")
                    {
                        int formatFlags = tag[pos + 9];
                        bool compressed = major >= 4 ? (formatFlags & 0x08) != 0 : (formatFlags & 0x80) != 0;
                        bool encrypted = major >= 4 ? (formatFlags & 0x04) != 0 : (formatFlags & 0x40) != 0;
                        byte[] body = tag; int bs = startPos, bn = frameSize;
                        if (major >= 4 && (((tagFlags & 0x80) != 0) || (formatFlags & 0x02) != 0))
                        { body = DeUnsync(tag, startPos, frameSize); bs = 0; bn = body.Length; }
                        if (major >= 4)
                        {
                            if ((formatFlags & 0x40) != 0) { bs++; bn--; }       // identidad de grupo
                            if ((formatFlags & 0x01) != 0) { bs += 4; bn -= 4; } // indicador de longitud
                        }
                        else if ((formatFlags & 0x20) != 0) { bs++; bn--; }
                        string val = (!compressed && !encrypted && bn > 0 && bs >= 0 && bs + bn <= body.Length)
                            ? DecodeText(body, bs, bn) : "";
                        if (val != null) val = val.Trim();
                        bool has = !string.IsNullOrEmpty(val);
                        // El ID3 MANDA sobre titulo/artista: el nombre de archivo es solo una adivinanza
                        // (en discos de varios artistas suele venir al reves, o sin titulo siquiera).
                        if (id == "TIT2" && has) t.Title = val;
                        else if (id == "TPE1" && has) t.Artist = val;
                        else if (id == "TALB" && has && string.IsNullOrEmpty(t.Album)) t.Album = val;
                        else if (id == "TCON" && has && string.IsNullOrEmpty(t.Genre)) t.Genre = NormalizeGenre(val);
                    }
                    pos = startPos + frameSize;
                }
            }

            private static string DecodeText(byte[] b, int start, int size)
            {
                if (size < 1) return "";
                byte enc = b[start];
                int s = start + 1, n = size - 1;
                string result;
                switch (enc)
                {
                    case 1:
                        if (n >= 2 && b[s] == 0xFF && b[s + 1] == 0xFE) result = Encoding.Unicode.GetString(b, s + 2, n - 2);
                        else if (n >= 2 && b[s] == 0xFE && b[s + 1] == 0xFF) result = Encoding.BigEndianUnicode.GetString(b, s + 2, n - 2);
                        else result = Encoding.Unicode.GetString(b, s, n);
                        break;
                    case 2: result = Encoding.BigEndianUnicode.GetString(b, s, n); break;
                    case 3: result = Encoding.UTF8.GetString(b, s, n); break;
                    default: result = Encoding.GetEncoding(28591).GetString(b, s, n); break;
                }
                int z = result.IndexOf('\0');
                if (z >= 0) result = result.Substring(0, z);
                return result.Trim();
            }

            private static void ReadId3v1Stream(Stream fs, Track t)
            {
                if (fs.Length < 128) return;
                fs.Seek(-128, SeekOrigin.End);
                byte[] b = new byte[128];
                if (ReadFully(fs, b, 0, 128) != 128) return;
                if (!(b[0] == (byte)'T' && b[1] == (byte)'A' && b[2] == (byte)'G')) return;
                Encoding latin = Encoding.GetEncoding(28591);
                char[] tc = new char[] { '\0', ' ' };
                string title = latin.GetString(b, 3, 30).Trim(tc);
                string artist = latin.GetString(b, 33, 30).Trim(tc);
                string album = latin.GetString(b, 63, 30).Trim(tc);
                if (string.IsNullOrEmpty(t.Title) && title.Length > 0) t.Title = title;
                if (string.IsNullOrEmpty(t.Artist) && artist.Length > 0) t.Artist = artist;
                if (string.IsNullOrEmpty(t.Album) && album.Length > 0) t.Album = album;
                int gi = b[127];
                if (string.IsNullOrEmpty(t.Genre) && gi >= 0 && gi < Id3v1Genres.Length) t.Genre = Id3v1Genres[gi];
            }
        }
    }
}

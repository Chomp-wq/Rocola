#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la casilla de AYUDA, su signo y su horno dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; CtrlDot, CtrlTxt y CtrlTri (Form1.Pared.cs) y DrawCtrlOverlay (Form1.Video.cs) ya estan traducidos y la llaman directo, y su Pinta (lo que era su OnPaint) tambien, sin puente (Plataforma/Puente.asistente.cs ya no tiene nada de esta casilla); RoundedPath (Form1.Fondo.cs, ya traducido) da su SKPath directo (ContornoRedondo)

namespace Rocola
{
    // LA CASILLA DE AYUDA (peticion del 11-10-2026). Va al lado de la de BUSCAR y se lee igual que ella:
    // un simbolo, la palabra y los DOS BOTONES que hay que apretar, dibujados tal cual son en la maquina:
    // el ROJO, un mas, y la flecha de ABAJO (roja, como en la cruceta). En vez de la lupa lleva un signo de
    // interrogacion DORADO que se mueve: lo anima la tarjeta grafica encima de la interfaz (bota, se balancea
    // y de vez en cuando da una vuelta de moneda), asi que la textura de la interfaz no se rehace por eso.
    // Tocarla con el dedo o el raton tambien abre la ayuda. Solo con tarjeta grafica: la ayuda solo existe ahi.
    public partial class Form1
    {
        private AyudaView ayudaView;

        private class AyudaView : IVista, IDisposable   // EN LINUX: : Control -> sin herencia (port nativo, vista-ayuda: W3d): una vista de la pantalla nativa (Nativo/IVista.cs) con su CajaVista; en la ventana de la capa la pone su hueco (Plataforma/Anfitrion.cs), que la pinta con Pinta y le da el raton como WinForms al control; ya no es partial: sin la firma de XP de RenderContent con un Graphics (su OnPaint) en el puente, que ya no hace falta
        {
            public bool Compacta;           // sin la palabra AYUDA cuando la fila va justa (1024x768)
            public bool Abierta;            // con la ayuda en pantalla la casilla queda encendida fija
            private Rocola.Nativo.Letra fTxt; private float fAlto = -1f;   // EN LINUX: la letra de la casilla, una Letra (Nativo/Letras.cs; Form1 tiene un metodo Letra)
            private readonly CajaVista caja; public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } } public int Left { get { return caja.Left; } set { caja.Left = value; } } public int Top { get { return caja.Top; } set { caja.Top = value; } } public int Width { get { return caja.Width; } set { caja.Width = value; } } public int Height { get { return caja.Height; } set { caja.Height = value; } } public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); } public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } } public void Invalidate() { caja.Invalidate(); }   // EN LINUX: lo que heredaba de Control y usa la rocola (lo de IVista), de su CajaVista (Nativo/IVista.cs): misma semantica (SetBounds y Visible con el mismo valor no avisan; Visible es la suya y la de su dueno, GetVisibleCore)
            public AyudaView()
            {
                caja = new CajaVista(this);   // EN LINUX: SetStyle(AllPaintingInWmPaint | UserPaint | OptimizedDoubleBuffer | SupportsTransparentBackColor, true): sin ventana propia no hay WM_ERASEBKGND, doble bufer ni comprobacion del fondo transparente: la pinta su Pinta entera (la pantalla compone con transparencia); no toca Selectable ni StandardClick (los de fabrica de Control, que el hueco conserva)
                // EN LINUX: BackColor = Color.Transparent: el OnPaintBackground de fabrica con un color transparente no pinta nada (en XP pintaba lo del padre debajo; aqui la pantalla compone con transparencia): Pinta no pinta fondo
                // EN LINUX: TabStop = false: la vista no es un control con foco de tabulador: en la pantalla nativa las teclas van a la ventana (Form1.ProcessCmdKey) y el tabulador no recorre vistas (Nativo/PantallaRocola.Vistas.cs)
            }
            void IDisposable.Dispose() { Dispose(true); } private void Dispose(bool disposing)   // EN LINUX: protected override void Dispose(bool disposing) de Control: la llama su hueco al desecharse con la ventana (Control.Dispose desecha a los hijos) con disposing = true, como WinForms; sin finalizador no hay Dispose(false), que aqui no hacia nada
            {
                try { if (disposing && fTxt != null) { fTxt.Dispose(); fTxt = null; } }
                finally { }   // EN LINUX: base.Dispose(disposing) (sacarla de la ventana, su handle, el aviso Disposed): lo hace su hueco despues de llamar aqui, en el mismo orden
            }
            private static int Sim(int alto) { int d = (int)(alto * 0.52f); if (d < 12) d = 12; if (d > 30) d = 30; return d; }

            // Lo que mide de ancho con ese alto: interrogacion + (AYUDA) + rojo + mas + flecha, y 12 px de aire al final
            // (los mismos 11 del principio). Se MIDE con la letra de verdad, una vez por alto: la cuenta a ojo de antes
            // le dejaba ~45 px vacios al final, justo lo que le faltaba al encabezado en 1024x768 (aviso del 11-10-2026).
            private static int medidoAlto = -1, medidoPalabra, medidoMas;
            public static int Ancho(int alto, bool compacta)
            {
                int d = Sim(alto);
                if (medidoAlto != alto)
                {
                    // EN LINUX: using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b)): el mapa de 1x1 solo daba un Graphics con que medir, y medir no pide lienzo (TextoGdiPlus.Mide)
                    using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, Math.Max(8f, alto * 0.30f), true))   // EN LINUX: new Font(FontFamily.GenericSansSerif, pt, Bold): la generica sin remates de XP (Microsoft Sans Serif, Letras.SinRemates), en puntos y en negrita (Nativo/Letras.cs)
                    {
                        AjusteLetra ajuste = AjusteLetra.Suave;   // EN LINUX: g.TextRenderingHint = AntiAlias, en una variable
                        medidoPalabra = (int)Math.Ceiling(TextoGdiPlus.Mide("AYUDA", f, ajuste, new SizeF(0, 0), null).Width);   // EN LINUX: g.MeasureString("AYUDA", f): sin caja (0, 0) y sin formato (null: el de fabrica) (Nativo/TextoGdiPlus.cs)
                        medidoMas = (int)Math.Ceiling(TextoGdiPlus.Mide("+", f, ajuste, new SizeF(0, 0), null).Width);   // EN LINUX: g.MeasureString("+", f)
                    }
                    medidoAlto = alto;
                }
                // Lo mismo que recorre RenderContent: icono, (8 + AYUDA + 12 | 6), rojo + 4, "+" + 6, flecha.
                return 11 + (int)(d * 1.15f) + (compacta ? 6 : 8 + medidoPalabra + 12) + d + 4 + medidoMas + 6 + (int)(d * 0.9f) + 12;
            }

            // Donde va el signo de interrogacion (coordenadas del control). Lo dibuja la tarjeta, animado.
            public RectangleF Icono()
            {
                int d = Sim(Height); float lado = d * 1.15f;
                return new RectangleF(11, (Height - lado) / 2f, lado, lado);
            }

            public void Pinta(SKCanvas g) { RenderContent(g, false, false, false); }   // EN LINUX: protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics, false); }: la pinta la pantalla (IVista.Pinta) en su lienzo, con el estado del Graphics de un WM_PAINT de la capa (Graphics.FromImage: sin copia ni medio pixel, Escritorio.PintaSolo); el suavizado y el modo de letra que la firma de XP le dejaba al Graphics se iban con el

            // forGpu: el signo NO va en la textura (lo pone la tarjeta, moviendose); sin tarjeta, fijo.
            public void RenderContent(SKCanvas g, bool copia, bool medio, bool forGpu)   // EN LINUX: el lienzo de Skia (con su matriz y su recorte) y el estado del que llama que aqui no se pone (la copia y el medio pixel); la llaman directo su Pinta (arriba) y DrawCtrlOverlay (Form1.Video.cs)
            {
                int w = Width, h = Height;
                if (w < 30 || h < 12) return;
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias: el estado del dibujo en variables
                AjusteLetra ajuste = AjusteLetra.Suave;   // EN LINUX: g.TextRenderingHint = AntiAlias
                if (fTxt == null || fAlto != h)
                { if (fTxt != null) fTxt.Dispose(); fTxt = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, Math.Max(8f, h * 0.30f), true); fAlto = h; }   // EN LINUX: new Font(FontFamily.GenericSansSerif, pt, Bold): Microsoft Sans Serif (Letras.SinRemates), en puntos y en negrita
                Color oro = Color.FromArgb(255, 205, 90);
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (SKPath gp = ContornoRedondo(r, Math.Min(14, h / 2)))   // EN LINUX: RoundedPath devuelve un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo) y se suelta como en XP
                {
                    using (SKPaint sb = new SKPaint { Color = Color.FromArgb(Abierta ? 92 : 64, oro).Sk() }) Pixel.Rellena(g, gp, sb, suave, medio, copia);   // EN LINUX: SolidBrush: el pincel de un color en un SKPaint; g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pn = new SKPaint { Color = Color.FromArgb(Abierta ? 255 : 220, oro).Sk(), StrokeWidth = 2f }) Pixel.Traza(g, gp, pn, suave, medio, copia);   // EN LINUX: Pen(color, 2): la pluma en un SKPaint (Pixel le pone el minimo de un pixel y las esquinas de GDI+); g.DrawPath
                }
                int cy = h / 2, d = Sim(h);
                RectangleF ic = Icono();
                if (!forGpu) PintaInterrogacion(g, suave, medio, copia, ic, 1f);   // EN LINUX: el lienzo y su estado
                float x = ic.Right;
                Color tinta = Color.FromArgb(255, 255, 240, 205);
                if (!Compacta) x = CtrlTxt(g, suave, ajuste, copia, medio, fTxt, "AYUDA", x + 8, cy, tinta);   // EN LINUX: CtrlTxt (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado y la Letra
                x += Compacta ? 6 : 12;
                x = CtrlDot(g, suave, ajuste, copia, medio, x, cy, d, Color.FromArgb(228, 72, 72)) + 4;   // EN LINUX: CtrlDot (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado
                x = CtrlTxt(g, suave, ajuste, copia, medio, fTxt, "+", x, cy, tinta) + 6;   // EN LINUX: CtrlTxt (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado y la Letra
                Tecla = new Rectangle((int)x, cy - (int)(d * 0.9f) / 2, (int)(d * 0.9f), (int)(d * 0.9f));   // la guia del rojo la resalta
                FlechaAbajo(g, suave, ajuste, copia, medio, x, cy, (int)(d * 0.9f), Color.FromArgb(228, 72, 72));   // EN LINUX: el lienzo y su estado
            }
            // Donde quedo la tecla de la flecha la ultima vez que se dibujo (coordenadas del control; vacio si aun no).
            // Con el ROJO mantenido, la guia la resalta a ella en vez de pintar otra flecha fuera (GuiaRojoGpu.cs).
            public Rectangle Tecla;

            // La flecha de ABAJO, en una tecla redondeada (la gemela de la de BUSCAR, que apunta arriba).
            internal static void FlechaAbajo(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)   // EN LINUX: el lienzo de Skia y el estado del que llama (suavizado, modo de letra, copia y medio pixel)
            {
                Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
                using (SKPath gp = ContornoRedondo(k, Math.Max(3, d / 4)))   // EN LINUX: RoundedPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo)
                {
                    using (SKPaint sb = new SKPaint { Color = Color.FromArgb(70, c).Sk() }) Pixel.Rellena(g, gp, sb, suave, medio, copia);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pn = new SKPaint { Color = Color.FromArgb(215, c).Sk(), StrokeWidth = 1.6f }) Pixel.Traza(g, gp, pn, suave, medio, copia);   // EN LINUX: Pen(color, 1,6) y g.DrawPath
                }
                float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
                CtrlTri(g, suave, ajuste, copia, medio, c, new PointF(cx, cy + t), new PointF(cx - an, cy - t * 0.62f), new PointF(cx + an, cy - t * 0.62f));   // EN LINUX: CtrlTri (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado
            }
        }

        // El signo dorado: degradado de oro, filo oscuro y un brillo arriba. Lo usan la casilla sin tarjeta y
        // la textura que anima la tarjeta (asi los dos se ven iguales).
        internal static void PintaInterrogacion(SKCanvas g, bool suave, bool medio, bool copia, RectangleF r, float alfa)   // EN LINUX: el lienzo de Skia y el estado del dibujo que pesa aqui (suavizado, medio pixel y copia)
        {
            if (r.Width < 4 || r.Height < 4) return;
            int a = (int)(255 * Math.Max(0f, Math.Min(1f, alfa)));
            using (SKPath p = new SKPath { FillType = SKPathFillType.EvenOdd })   // EN LINUX: GraphicsPath: un SKPath (su relleno de fabrica, Alternate, es par-impar)
            using (Rocola.Nativo.Letra fam = Rocola.Nativo.Letra.EnPixeles(Letras.ConRemates, r.Height * 1.02f, true))   // EN LINUX: FontFamily(Serif) es Times New Roman, y AddString hace con ella la letra en pixeles y en negrita (FontStyle.Bold); Letra con su espacio entero (Form1 tiene un metodo Letra)
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto sf = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                sf.Horizontal = Alinea.Centro; sf.Vertical = Alinea.Centro;   // EN LINUX: Alignment y LineAlignment = Center
                TextoGdiPlus.Trazo(p, "?", fam, new RectangleF(r.X - r.Width * .3f, r.Y + r.Height * .04f, r.Width * 1.6f, r.Height), sf);   // EN LINUX: p.AddString (Nativo/TextoGdiPlus.cs)
                using (SKShader degradado = Degradado.Lineal(new RectangleF(r.X, r.Y, r.Width, r.Height + 1),   // EN LINUX: LinearGradientBrush(caja, a, b, 90): su shader (Nativo/Degradado.cs) en un SKPaint
                    Color.FromArgb(a, 255, 238, 150), Color.FromArgb(a, 214, 140, 24), 90f)) using (SKPaint oro = new SKPaint { Shader = degradado })   // EN LINUX: el pincel del degradado: un SKPaint con su shader (se sueltan con sus using)
                using (SKPaint filo = new SKPaint { Color = Color.FromArgb(a * 3 / 4, 70, 40, 6).Sk(), StrokeWidth = Math.Max(1.2f, r.Height * .06f) })   // EN LINUX: Pen(color, ancho)
                {
                    filo.StrokeJoin = SKStrokeJoin.Round;   // EN LINUX: LineJoin.Round
                    Pixel.Traza(g, p, filo, suave, medio, copia);   // EN LINUX: g.DrawPath (Nativo/Pixel.cs)
                    Pixel.Rellena(g, p, oro, suave, medio, copia);   // EN LINUX: g.FillPath
                }
            }
        }

        // LA FILA DE ARRIBA CON TARJETA GRAFICA: instrucciones | BUSCAR + AYUDA | encabezado. Devuelve donde acaba el
        // par (el encabezado va detras). 'minX' es donde acaban las instrucciones; BUSCAR ya viene colocada a la manera
        // de siempre (centrada en la pantalla), que es como se queda sin tarjeta.
        //
        // EN PANTALLAS BAJAS SE REPARTE (aviso del 11-10-2026, captura a 1024x768): BUSCAR iba centrada en la pantalla
        // y dejaba 77 px vacios detras de las instrucciones, las dos casillas median ~45 px de mas por dentro, y el
        // encabezado se quedaba en 208 px: "LO MAS SONADO" justo y sin numero de pagina, y la AYUDA sin su palabra.
        // Ahora las casillas miden lo que dibujan, BUSCAR sigue en el centro si hay sitio, y si no, el par se corre a la
        // izquierda lo justo para que el encabezado tenga lo que pide (HeaderView.AnchoPedido). Con sitio de sobra
        // (1280 o mas) BUSCAR queda donde estaba.
        private int AcomodaAyuda(int minX, int W, int filaH, int barraDer, bool buscaVisible)
        {
            int busX = buscaView != null ? buscaView.Left : minX, busW = buscaView != null ? buscaView.Width : 0;
            if (ayudaView == null) return busX + busW;
            if (!buscaVisible || !gpuEnabled || buscaView == null) { ayudaView.Visible = false; return busX + busW; }
            int libre = barraDer - minX;   // para BUSCAR, AYUDA, sus dos huecos y el encabezado
            busW = Math.Min(BuscaView.AnchoJusto(filaH), Math.Max(0, libre));
            int pide = headerView != null ? headerView.AnchoPedido(filaH) : 0;   // si no cabe, se queda con lo que haya
            // Con la palabra si al encabezado le queda lo que pide; si no, sin ella; y si ni asi le quedan 90 px, sin AYUDA.
            bool compacta = busW + 10 + AyudaView.Ancho(filaH, false) + 12 + pide > libre;
            // LA PALABRA AYUDA EN PANTALLAS BAJAS (peticion del 12-10-2026, captura a 1024x768 con la casilla sin su
            // palabra). Antes de quitarsela, el encabezado cede hasta un 15 % de lo que pide: su nombre se achica solo un
            // poco (HeaderView.RenderContent lo encoge de medio en medio punto hasta caber) y el numero de pagina pasa a
            // su forma corta. "LO MAS SONADO" sigue grande; la casilla de AYUDA dice lo que es.
            int pideApretado = pide * 85 / 100;
            if (compacta && busW + 10 + AyudaView.Ancho(filaH, false) + 12 + pideApretado <= libre)
            {
                compacta = false;
                pide = libre - (busW + 10 + AyudaView.Ancho(filaH, false) + 12);   // todo lo que queda, que es al menos el 85 %
            }
            int ancho = AyudaView.Ancho(filaH, compacta);
            bool cabe = busW + 10 + ancho + 12 + 90 <= libre;
            int grupo = busW + (cabe ? 10 + ancho : 0);
            busX = ColocaParBusca(W, busW, grupo, minX, barraDer, pide);
            buscaView.SetBounds(busX, 8, busW, filaH);
            ayudaView.Compacta = compacta;
            ayudaView.SetBounds(busX + busW + 10, 8, ancho, filaH);
            ayudaView.Visible = cabe;
            return busX + grupo;
        }

        // CON VIDEO SONANDO, BUSCAR Y AYUDA VAN ENCIMA DEL VIDEO (revision de raiz del 12-10-2026: "despues de un rato ya no
        // se puede usar el asistente"). Con video, letras, encabezado y discos se encogen a la mitad izquierda, y en esa fila
        // ya no cabian: las dos casillas se escondian mientras durara el video, que en el bar es casi siempre. Encima del
        // cuadro del video queda libre una franja del alto de la fila (el video empieza a la altura de las letras): ahora
        // van ahi, centradas sobre el. Solo con tarjeta grafica; sin ella (dibujo por CPU, congelado) todo sigue igual.
        private void AcomodaSobreVideo(int x0, int x1, int filaH)
        {
            int libre = Math.Max(0, x1 - x0);
            int busW = Math.Min(BuscaView.AnchoJusto(filaH), libre);
            bool compacta = ayudaView != null && busW + 10 + AyudaView.Ancho(filaH, false) > libre;
            int ancho = ayudaView != null ? AyudaView.Ancho(filaH, compacta) : 0;
            bool cabe = ayudaView != null && busW >= 120 && busW + 10 + ancho <= libre;
            int busX = ParSobreVideo(x0, x1, busW, cabe ? ancho : 0);
            buscaView.SetBounds(busX, 8, busW, filaH);
            buscaView.Visible = busW >= 120;
            if (ayudaView == null) return;
            ayudaView.Compacta = compacta;
            ayudaView.SetBounds(busX + busW + 10, 8, Math.Max(1, ancho), filaH);
            ayudaView.Visible = cabe;
        }

        // Donde empieza BUSCAR para que el par (BUSCAR, 10 px, AYUDA) quede centrado entre x0 y x1. Pura.
        internal static int ParSobreVideo(int x0, int x1, int busW, int ayudaW)
        {
            int grupo = busW + (ayudaW > 0 ? 10 + ayudaW : 0);
            return x0 + Math.Max(0, (x1 - x0 - grupo) / 2);
        }

        // Donde empieza BUSCAR (con AYUDA a su derecha, 'grupo' de ancho entre las dos). Pura: se prueba sin ventana.
        // EL PAR, CENTRADO (peticion del 14-10-2026: «haz que la casilla de buscar y ayuda esten bien centradas»). Antes
        // se centraba BUSCAR sola y AYUDA quedaba pegada a su derecha: el par se veia corrido a la derecha. Ahora el centro
        // de la pantalla cae en el medio del par (BUSCAR, 10 px, AYUDA). Sin AYUDA (grupo = BUSCAR), BUSCAR sola en el centro.
        internal static int ColocaParBusca(int W, int busW, int grupo, int minX, int barraDer, int pideEncabezado)
        {
            int x = (W - grupo) / 2;                                  // el par, con su centro en el de la pantalla
            if (x + grupo > barraDer) x = barraDer - grupo;
            int tope = barraDer - 12 - pideEncabezado - grupo;        // mas a la derecha apretaria al encabezado
            if (x > tope) x = tope;
            if (x < minX) x = minX;                                   // nunca encima de las instrucciones
            return x;
        }

        // LA FILA DE BUSCAR Y AYUDA SE VE: la pared de discos esta a la vista. Dentro de un disco la textura de la interfaz
        // solo pinta las instrucciones, la etiqueta y la lista; las dos casillas siguen marcadas como visibles (la pared
        // las recupera al volver), y lo que la tarjeta les pone encima -el latido azul de BUSCAR y la interrogacion que
        // bota- se quedaba flotando sobre el disco (aviso del usuario del 12-10-2026, con foto). Lo mismo con el buscador
        // abierto, la atraccion, el video a pantalla completa y la configuracion de botones.
        private bool FilaBuscaAyudaVisible()
        {
            return !(panelSongs != null && panelSongs.Visible) && !searchOpen && !attractOn && !videoFull && !configMode;
        }

        // El signo, en coordenadas de la pantalla, cuando se ve (si no, vacio).
        private RectangleF AyudaIconoRect()
        {
            if (ayudaView == null || !ayudaView.Visible || !FilaBuscaAyudaVisible()) return RectangleF.Empty;
            RectangleF r = ayudaView.Icono();
            r.Offset(ayudaView.Left, ayudaView.Top);
            return r;
        }

        // Tocar la casilla abre la ayuda (pantalla tactil o raton).
        private bool AyudaTocada(Point p)
        {
            if (ayudaView == null || !ayudaView.Visible || !ayudaView.Bounds.Contains(p)) return false;
            if (!FilaBuscaAyudaVisible()) return false;   // dentro de un disco (o con el buscador) no esta: el toque es para lo de abajo
            // Con la ayuda abierta (preguntando o guiando), tocar la casilla la vuelve a guardar: es la gemela
            // tactil de ROJO + ABAJO, que tambien abre y cierra. El mismo toque que la abrio no la cierra.
            if (asistenteVisible)
            {
                if (unchecked(Environment.TickCount - asistenteInicioTick) >= 450) CierraAsistente();
                return true;
            }
            string motivo = MotivoSinAyuda();
            if (motivo != null) { AvisaSinAyuda(motivo); return true; }
            if (!topeAyuda.Pide(Environment.TickCount)) { AvisaTopeCombo(); return true; }   // el mismo tope que ROJO + ABAJO
            AbreAsistente();   // tambien mientras regresa a su sitio: da media vuelta desde donde iba
            return true;
        }
    }

    internal sealed partial class GpuPanel
    {
        // El signo de interrogacion dorado de la casilla de AYUDA, animado aqui encima de la interfaz.
        public Func<RectangleF> GetAyudaIcono;
        private IntPtr ayudaTex; private int ayudaTW, ayudaTH; private float ayudaU = 1f, ayudaV = 1f;
        private int ayudaLado = -1;
        private RectangleF ayudaRect;
        private readonly Tarjeta.Vtx[] ayudaQuad = new Tarjeta.Vtx[6];

        // Antes de Begin: se hornea una vez por tamano (nunca por cuadro).
        private void PreparaAyudaIcono()
        {
            ayudaRect = GetAyudaIcono != null ? GetAyudaIcono() : RectangleF.Empty;
            if (ayudaRect.Width < 4) return;
            int lado = Math.Max(16, Math.Min(256, (int)(ayudaRect.Height * 2f)));
            if (ayudaTex != IntPtr.Zero && lado == ayudaLado) return;
            using (SKBitmap b = BakeInterrogacion(lado))   // EN LINUX: el SKBitmap del horno, tal cual a Upload (D3D9Star.cs ya recibe SKBitmap)
                ayudaTex = Upload(ayudaTex, ref ayudaTW, ref ayudaTH, b, out ayudaU, out ayudaV);
            ayudaLado = ayudaTex != IntPtr.Zero ? lado : -1;
        }
        internal static SKBitmap BakeInterrogacion(int lado)   // EN LINUX: un SKBitmap (PreparaAyudaIcono lo sube tal cual)
        {
            if (lado < 8 || lado > 1024) throw new ArgumentOutOfRangeException("lado");
            SKBitmap b = Mapas.Nuevo(lado, lado);   // EN LINUX: new Bitmap(lado, lado, Format32bppArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage
            {
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable (el estado del dibujo va aparte; medio pixel y copia, los de fabrica)
                Form1.PintaInterrogacion(g, suave, false, false, new RectangleF(lado * .08f, lado * .06f, lado * .84f, lado * .84f), 1f);   // EN LINUX: el lienzo y su estado
            }
            return b;
        }

        // Bota, se balancea y cada cinco segundos da media vuelta de moneda. Con un brillo dorado que respira.
        internal static void AnimaInterrogacion(double t, out float bota, out float giro, out float anchoX, out float escala, out float brillo)
        {
            if (double.IsNaN(t) || double.IsInfinity(t)) t = 0;
            bota = (float)(-Math.Abs(Math.Sin(t * 2.6)) * .10);
            giro = (float)(Math.Sin(t * 1.7) * .16);
            double vuelta = t % 5.0;
            anchoX = vuelta < .7 ? (float)Math.Cos(vuelta / .7 * Math.PI * 2) : 1f;
            if (Math.Abs(anchoX) < .08f) anchoX = anchoX < 0 ? -.08f : .08f;
            escala = 1f + .05f * (float)Math.Sin(t * 5.2);
            brillo = .5f + .5f * (float)Math.Sin(t * 2.6 + 1.2);
        }

        private void DibujaInterrogacion(float alfa)
        {
            if (ayudaTex == IntPtr.Zero || ayudaRect.Width < 4 || !(alfa > .004f)) return;
            float bota, giro, anchoX, escala, brillo;
            AnimaInterrogacion(AhoraMs() / 1000.0, out bota, out giro, out anchoX, out escala, out brillo);
            float cx = ayudaRect.X + ayudaRect.Width * .5f, cy = ayudaRect.Y + ayudaRect.Height * .5f + bota * ayudaRect.Height;
            float r = ayudaRect.Height * .62f * escala;
            stars.GlowBatchBegin(1);
            stars.GlowBatchAdd(cx, cy, r * 1.7f, ((uint)((30 + 60 * brillo) * alfa) << 24) | 0xFFCC55u);
            stars.GlowBatchEnd();
            float rx = r * Math.Abs(anchoX);
            SetTexQuadA(ayudaQuad, cx - rx, cy - r, cx + rx, cy + r, ayudaU, ayudaV, ((uint)(255 * Math.Min(1f, alfa)) << 24) | 0xFFFFFFu);
            if (anchoX < 0)   // de espaldas: la textura se ve espejada, como una moneda que gira
                for (int i = 0; i < 6; i++) ayudaQuad[i].U = ayudaU - ayudaQuad[i].U;
            RotateStarQuad(ayudaQuad, cx, cy, giro);
            stars.DrawQuad(ayudaTex, ayudaQuad);
        }

        private void SueltaAyudaIcono()
        {
            if (ayudaTex != IntPtr.Zero) { Tarjeta.Suelta(ayudaTex); ayudaTex = IntPtr.Zero; }
            ayudaLado = -1;
        }
    }
}
#endif

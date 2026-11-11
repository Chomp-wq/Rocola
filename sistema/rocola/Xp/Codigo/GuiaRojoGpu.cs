using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la guia del rojo dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; RoundedPath (Form1.Fondo.cs, ya traducido) da su SKPath directo (ContornoRedondo)

namespace Rocola
{
    public partial class Form1
    {
        private bool guiaRojo = true;
#if ROCOLA_GPU
        // El modificador conserva sus acciones. Solo se añade una pista visual;
        // los destellos son quads GPU y no invalidan la textura de la interfaz.
        private bool GuiaRojoVisible()
        {
            return GuiaRojoActiva(guiaRojo, gpuEnabled, RocolaConTeclado(), RojoMantenido(),
                maintMode || libBuilding || videoFull || searchOpen || attractOn || configMode || AsistenteCapturaEntrada
                || (panelSongs != null && panelSongs.Visible)
                || letterBar == null || !letterBar.Visible);
        }

        internal static bool GuiaRojoActiva(bool opcion, bool gpu, bool foco, bool rojo, bool bloqueada)
        { return opcion && gpu && foco && rojo && !bloqueada; }

        // POR QUE NO SALE (aviso del 11-10-2026: "al dejar presionado el rojo ya no salian las pistas ni podia usar
        // el asistente"). Las mismas condiciones que GuiaRojoVisible, dichas con palabras. null = sale.
        private string MotivoSinGuiaRojo()
        {
            if (!guiaRojo) return "pistas del rojo apagadas en Ctrl+F3";
            if (!gpuEnabled) return "sin tarjeta grafica";
            if (!RocolaConTeclado()) return "la rocola no tiene el foco del teclado";
            if (maintMode) return "mantenimiento (F10)";
            if (libBuilding) return "leyendo la biblioteca";
            if (videoFull) return "video a pantalla completa";
            if (searchOpen) return "buscador abierto";
            if (attractOn) return "modo atraccion";
            if (configMode) return "configurando botones";
            if (AsistenteCapturaEntrada) return asistenteVisible ? "la ayuda esta preguntando" : "la ayuda esta regresando a su sitio";
            if (panelSongs != null && panelSongs.Visible) return "dentro de un disco";
            if (letterBar == null || !letterBar.Visible) return "sin barra de letras";
            return null;
        }
        // Si el rojo lleva un rato mantenido en la pared y la guia no sale por algo que no es de diseno, se apunta UNA
        // vez por pulsacion en rocola_error.txt (siempre activo), con el motivo. Asi un fallo del bar se puede
        // diagnosticar sin adivinar. El buscador, el disco, la atraccion y el video completo son de diseno: no se apuntan.
        private int rojoMantenidoDesde, rojoApuntes; private bool rojoApuntado;
        private void VigilaRojoMantenido()
        {
            if (!RojoMantenido()) { rojoMantenidoDesde = 0; rojoApuntado = false; return; }
            int ahora = Environment.TickCount;
            if (rojoMantenidoDesde == 0) { rojoMantenidoDesde = ahora == 0 ? 1 : ahora; return; }
            if (rojoApuntado || rojoApuntes >= 20 || unchecked(ahora - rojoMantenidoDesde) < 700) return;
            rojoApuntado = true;
            string motivo = MotivoSinGuiaRojo();
            if (motivo == null || searchOpen || attractOn || videoFull || (panelSongs != null && panelSongs.Visible)) return;
            rojoApuntes++;
            LogError("Rojo mantenido sin pistas: " + motivo, null);
        }

        internal static Rectangle GuiaRojoFlecha(Rectangle barra, bool derecha)
        {
            int ancho = Math.Max(4, Math.Min(24, barra.Left - 2));
            int alto = Math.Max(16, Math.Min(30, barra.Height * 2 / 3));
            return new Rectangle(derecha ? barra.Right + 2 : barra.Left - ancho - 2,
                barra.Top + (barra.Height - alto) / 2, ancho, alto);
        }

        internal static void PintaGuiaRojo(SKCanvas g, bool suave, bool copia, bool medio, Rectangle barra, Rectangle busca)   // EN LINUX: el lienzo de Skia y lo que pesa aqui del estado del Graphics (suavizado, copia y medio pixel), como parametros
        { PintaGuiaRojoAyuda(g, suave, copia, medio, barra, busca, Rectangle.Empty, Rectangle.Empty, Rectangle.Empty); }   // EN LINUX: con el lienzo y su estado

        // LAS FLECHAS DE BUSCAR Y AYUDA, LAS DE SU CASILLA (peticion del 12-10-2026: "en vez de que aparezcan las
        // flechitas feas al presionar el boton rojo, que mejor se resalten las que ya tienen en su contenedor"). Cada
        // casilla ya dibuja su tecla de flecha (arriba en BUSCAR, abajo en AYUDA). Antes, con el rojo mantenido, salia
        // ademas un triangulito suelto en el hueco de al lado; ahora no sale nada fuera: la tecla de dentro se enciende
        // (un filo claro y un velo encima, aqui) y la tarjeta le pone el brillo que late (FillGuiaRojoGlows), mas
        // fuerte al apretar esa direccion. 'teclaBusca'/'teclaAyuda' van en las mismas coordenadas que las casillas.
        internal static void PintaGuiaRojoAyuda(SKCanvas g, bool suave, bool copia, bool medio, Rectangle barra, Rectangle busca, Rectangle ayuda, Rectangle teclaBusca, Rectangle teclaAyuda)   // EN LINUX: el lienzo de Skia y el estado del Graphics que pesa aqui (suavizado, copia y medio pixel)
        {
            if (ayuda.Width > 0 && ayuda.Height > 0)
            {
                using (SKPaint borde = new SKPaint { Color = Color.FromArgb(230, 255, 214, 94).Sk(), StrokeWidth = 2f })   // EN LINUX: Pen(color, 2): la pluma es un SKPaint con su color y su ancho (Pixel.Traza le pone lo de GDI+)
                using (SKPath contorno = ContornoRedondo(new Rectangle(ayuda.X, ayuda.Y, ayuda.Width - 1, ayuda.Height - 1), Math.Min(14, ayuda.Height / 2)))   // EN LINUX: RoundedPath devuelve un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo)
                    Pixel.Traza(g, contorno, borde, suave, medio, copia);   // EN LINUX: g.DrawPath(borde, contorno) (Nativo/Pixel.cs)
                ResaltaTecla(g, suave, copia, medio, teclaAyuda, false);   // EN LINUX: con el lienzo y su estado
            }
            using (SKPaint borde = new SKPaint { Color = Color.FromArgb(230, 255, 214, 94).Sk(), StrokeWidth = 2f })   // EN LINUX: Pen(color, 2)
            using (SKPaint oro = new SKPaint { Color = Color.FromArgb(255, 255, 222, 102).Sk() })   // EN LINUX: SolidBrush: un SKPaint de ese color
            {
                using (SKPath contorno = ContornoRedondo(new Rectangle(barra.X, barra.Y, Math.Max(1, barra.Width - 1), Math.Max(1, barra.Height - 1)), Math.Min(12, barra.Height / 2)))   // EN LINUX: RoundedPath da un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs)
                    Pixel.Traza(g, contorno, borde, suave, medio, copia);   // EN LINUX: g.DrawPath(borde, contorno)
                for (int i = 0; i < 2; i++)
                {
                    Rectangle r = GuiaRojoFlecha(barra, i != 0);
                    Pixel.RellenaPoligono(g, new PointF[] {   // EN LINUX: g.FillPolygon(oro, puntos): relleno Alternate, el de fabrica (Nativo/Pixel.cs)
                        new PointF(i == 0 ? r.Left : r.Right, r.Top + r.Height * .5f),
                        new PointF(i == 0 ? r.Right : r.Left, r.Top),
                        new PointF(i == 0 ? r.Right : r.Left, r.Bottom) }, oro, suave, true, medio, copia);   // EN LINUX: el pincel y el estado del dibujo
                }
                if (busca.Width > 0 && busca.Height > 0)
                {
                    using (SKPath contorno = ContornoRedondo(new Rectangle(busca.X, busca.Y, busca.Width - 1, busca.Height - 1), Math.Min(14, busca.Height / 2)))   // EN LINUX: RoundedPath da un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs)
                        Pixel.Traza(g, contorno, borde, suave, medio, copia);   // EN LINUX: g.DrawPath(borde, contorno)
                    ResaltaTecla(g, suave, copia, medio, teclaBusca, true);   // EN LINUX: con el lienzo y su estado
                }
            }
        }

        // La tecla encendida, como una tecla de verdad cuando se ilumina: llena de rojo vivo, la flecha en blanco
        // encima (en el mismo sitio y con la misma forma que la de la casilla) y un filo claro un poco por fuera, sin
        // salirse de la casilla. Un velo claro encima la dejaba deslavada (se vio en la vista previa).
        internal static void ResaltaTecla(SKCanvas g, bool suave, bool copia, bool medio, Rectangle k, bool arriba)   // EN LINUX: el lienzo de Skia y el estado del Graphics que pesa aqui (suavizado, copia y medio pixel)
        {
            if (k.Width < 4 || k.Height < 4) return;
            int radio = Math.Max(3, k.Width / 4);
            using (SKPath dentro = ContornoRedondo(k, radio))   // EN LINUX: RoundedPath devuelve un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo)
            using (SKPaint rojo = new SKPaint { Color = Color.FromArgb(250, 232, 70, 66).Sk() })   // EN LINUX: SolidBrush
                Pixel.Rellena(g, dentro, rojo, suave, medio, copia);   // EN LINUX: g.FillPath(rojo, dentro) (Nativo/Pixel.cs)
            Rectangle f = Rectangle.Inflate(k, 2, 2);
            using (SKPath filo = ContornoRedondo(f, radio + 2))   // EN LINUX: RoundedPath da un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs)
            using (SKPaint pn = new SKPaint { Color = Color.FromArgb(240, 255, 214, 200).Sk(), StrokeWidth = 2f })   // EN LINUX: Pen(color, 2)
                Pixel.Traza(g, filo, pn, suave, medio, copia);   // EN LINUX: g.DrawPath(pn, filo)
            // Las mismas cuentas que Flecha (BUSCAR) y FlechaAbajo (AYUDA).
            int d = k.Width, cy = k.Y + d / 2;
            float cx = k.X + d / 2f, t = d * 0.30f, an = d * 0.24f, s = arriba ? 1f : -1f;
            using (SKPaint blanco = new SKPaint { Color = Color.FromArgb(255, 255, 250, 246).Sk() })   // EN LINUX: SolidBrush
                Pixel.RellenaPoligono(g, new PointF[] { new PointF(cx, cy - s * t), new PointF(cx - an, cy + s * t * 0.62f), new PointF(cx + an, cy + s * t * 0.62f) }, blanco, suave, true, medio, copia);   // EN LINUX: g.FillPolygon(blanco, puntos): relleno Alternate, el de fabrica
        }

        // La tecla de la flecha de una casilla, en coordenadas de la pantalla (vacia si la casilla no se ve o aun no se
        // dibujo). 'tecla' viene en coordenadas de la casilla.
        internal static Rectangle GuiaRojoTecla(Rectangle casilla, Rectangle tecla)
        {
            if (casilla.Width <= 0 || casilla.Height <= 0 || tecla.Width <= 0 || tecla.Height <= 0) return Rectangle.Empty;
            Rectangle r = new Rectangle(casilla.X + tecla.X, casilla.Y + tecla.Y, tecla.Width, tecla.Height);
            return Rectangle.Intersect(r, casilla) == r ? r : Rectangle.Empty;   // una medida vieja que ya no cabe: nada
        }
        private Rectangle TeclaBusca()
        { return buscaView != null && buscaView.Visible ? GuiaRojoTecla(buscaView.Bounds, buscaView.Tecla) : Rectangle.Empty; }
        private Rectangle TeclaAyuda()
        { return ayudaView != null && ayudaView.Visible ? GuiaRojoTecla(ayudaView.Bounds, ayudaView.Tecla) : Rectangle.Empty; }

        private void DrawGuiaRojo(SKCanvas g, bool suave, bool copia, bool medio)   // EN LINUX: el lienzo de Skia y el estado del Graphics de la imagen de la interfaz; EnsureUiOverlay (Form1.Video.cs, aun sin traducir) la llama con su Graphics por el puente (Plataforma/Puente.acabado-estilo.cs)
        {
            if (!GuiaRojoVisible()) return;
            PintaGuiaRojoAyuda(g, suave, copia, medio, letterBar.Bounds,   // EN LINUX: con el lienzo y su estado
                buscaView != null && buscaView.Visible ? buscaView.Bounds : Rectangle.Empty,
                ayudaView != null && ayudaView.Visible ? ayudaView.Bounds : Rectangle.Empty,
                TeclaBusca(), TeclaAyuda());
        }

        private bool GuiaDireccionHeld(Tecla configurada, Tecla flecha)   // EN LINUX: Keys -> Tecla, los mismos numeros
        { return KeyHeld(configurada) || (KeyHeld(flecha) && !IsConfiguredKey(flecha)); }

        private int FillGuiaRojoGlows(GpuPanel.UiGlow[] buf, int n)
        {
            if (!GuiaRojoVisible()) return n;
            for (int i = 0; i < 4 && n < buf.Length; i++)
            {
                Rectangle r;
                bool pulsa;
                if (i < 2)
                {
                    r = GuiaRojoFlecha(letterBar.Bounds, i == 1);
                    pulsa = GuiaDireccionHeld(i == 0 ? keyLeft : keyRight, i == 0 ? Tecla.Left : Tecla.Right);   // EN LINUX: Keys -> Tecla, los mismos numeros
                }
                else
                {
                    // El brillo va sobre la tecla de la flecha de la casilla (la roja de dentro), no en el hueco de fuera.
                    r = i == 2 ? TeclaBusca() : TeclaAyuda();
                    if (r.Width <= 0) continue;
                    pulsa = i == 2 ? GuiaDireccionHeld(keyUp, Tecla.Up) : GuiaDireccionHeld(keyDown, Tecla.Down);   // EN LINUX: Keys -> Tecla, los mismos numeros
                }
                buf[n].Cx = r.Left + r.Width * .5f;
                buf[n].Cy = r.Top + r.Height * .5f;
                buf[n].R = Math.Max(18f, r.Height * .9f);
                buf[n].Argb = i >= 2 ? (uint)Color.FromArgb(pulsa ? 245 : 80, 255, 96, 84).ToArgb()
                                     : (uint)Color.FromArgb(pulsa ? 245 : 65, 255, 214, 80).ToArgb();
                n++;
            }
            return n;
        }
#endif
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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
            return GuiaRojoActiva(guiaRojo, gpuEnabled, ContainsFocus, BackHeld(),
                maintMode || libBuilding || videoFull || searchOpen || attractOn || configMode || AsistenteCapturaEntrada
                || (panelSongs != null && panelSongs.Visible)
                || letterBar == null || !letterBar.Visible);
        }

        internal static bool GuiaRojoActiva(bool opcion, bool gpu, bool foco, bool rojo, bool bloqueada)
        { return opcion && gpu && foco && rojo && !bloqueada; }

        internal static Rectangle GuiaRojoFlecha(Rectangle barra, bool derecha)
        {
            int ancho = Math.Max(4, Math.Min(24, barra.Left - 2));
            int alto = Math.Max(16, Math.Min(30, barra.Height * 2 / 3));
            return new Rectangle(derecha ? barra.Right + 2 : barra.Left - ancho - 2,
                barra.Top + (barra.Height - alto) / 2, ancho, alto);
        }

        internal static void PintaGuiaRojo(Graphics g, Rectangle barra, Rectangle busca)
        { PintaGuiaRojoAyuda(g, barra, busca, Rectangle.Empty); }

        // Con la casilla de AYUDA: su contorno y una flecha ROJA hacia abajo (rojo + abajo abre la ayuda).
        internal static void PintaGuiaRojoAyuda(Graphics g, Rectangle barra, Rectangle busca, Rectangle ayuda)
        {
            if (ayuda.Width > 0 && ayuda.Height > 0)
            {
                using (Pen borde = new Pen(Color.FromArgb(230, 255, 214, 94), 2f))
                using (GraphicsPath contorno = RoundedPath(new Rectangle(ayuda.X, ayuda.Y, ayuda.Width - 1, ayuda.Height - 1), Math.Min(14, ayuda.Height / 2)))
                    g.DrawPath(borde, contorno);
                using (SolidBrush rojo = new SolidBrush(Color.FromArgb(255, 236, 76, 72)))
                {
                    Rectangle f = GuiaRojoFlechaAyuda(ayuda);
                    g.FillPolygon(rojo, new PointF[] { new PointF(f.Left, f.Top), new PointF(f.Right, f.Top), new PointF(f.Left + f.Width * .5f, f.Bottom) });
                }
            }
            using (Pen borde = new Pen(Color.FromArgb(230, 255, 214, 94), 2f))
            using (SolidBrush oro = new SolidBrush(Color.FromArgb(255, 255, 222, 102)))
            {
                using (GraphicsPath contorno = RoundedPath(new Rectangle(barra.X, barra.Y, Math.Max(1, barra.Width - 1), Math.Max(1, barra.Height - 1)), Math.Min(12, barra.Height / 2)))
                    g.DrawPath(borde, contorno);
                for (int i = 0; i < 2; i++)
                {
                    Rectangle r = GuiaRojoFlecha(barra, i != 0);
                    g.FillPolygon(oro, new PointF[] {
                        new PointF(i == 0 ? r.Left : r.Right, r.Top + r.Height * .5f),
                        new PointF(i == 0 ? r.Right : r.Left, r.Top),
                        new PointF(i == 0 ? r.Right : r.Left, r.Bottom) });
                }
                if (busca.Width > 0 && busca.Height > 0)
                {
                    using (GraphicsPath contorno = RoundedPath(new Rectangle(busca.X, busca.Y, busca.Width - 1, busca.Height - 1), Math.Min(14, busca.Height / 2)))
                        g.DrawPath(borde, contorno);
                    // En el hueco entre controles: no tapa la lupa ni el texto.
                    float x = busca.Left - 7, y = busca.Top + busca.Height * .5f;
                    g.FillPolygon(oro, new PointF[] { new PointF(x, y - 7), new PointF(x - 4, y + 7), new PointF(x + 4, y + 7) });
                }
            }
        }

        // La flecha roja de la ayuda va en el hueco a la izquierda de su casilla, como la de BUSCAR.
        internal static Rectangle GuiaRojoFlechaAyuda(Rectangle ayuda)
        { return new Rectangle(ayuda.Left - 9, ayuda.Top + ayuda.Height / 2 - 7, 8, 14); }

        private void DrawGuiaRojo(Graphics g)
        {
            if (!GuiaRojoVisible()) return;
            PintaGuiaRojoAyuda(g, letterBar.Bounds,
                buscaView != null && buscaView.Visible ? buscaView.Bounds : Rectangle.Empty,
                ayudaView != null && ayudaView.Visible ? ayudaView.Bounds : Rectangle.Empty);
        }

        private bool GuiaDireccionHeld(Keys configurada, Keys flecha)
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
                    pulsa = GuiaDireccionHeld(i == 0 ? keyLeft : keyRight, i == 0 ? Keys.Left : Keys.Right);
                }
                else if (i == 2)
                {
                    if (buscaView == null || !buscaView.Visible) continue;
                    r = buscaView.Bounds;
                    r = new Rectangle(r.Left - 11, r.Top + r.Height / 2 - 7, 8, 14);
                    pulsa = GuiaDireccionHeld(keyUp, Keys.Up);
                }
                else
                {
                    if (ayudaView == null || !ayudaView.Visible) continue;
                    r = GuiaRojoFlechaAyuda(ayudaView.Bounds);
                    pulsa = GuiaDireccionHeld(keyDown, Keys.Down);
                }
                buf[n].Cx = r.Left + r.Width * .5f;
                buf[n].Cy = r.Top + r.Height * .5f;
                buf[n].R = Math.Max(18f, r.Height * .9f);
                buf[n].Argb = i == 3 ? (uint)Color.FromArgb(pulsa ? 245 : 70, 255, 90, 80).ToArgb()
                                     : (uint)Color.FromArgb(pulsa ? 245 : 65, 255, 214, 80).ToArgb();
                n++;
            }
            return n;
        }
#endif
    }
}

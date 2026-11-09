// LA HOJA: EL PINTADO (port nativo de las ventanas de XP, ventanas2, paso H; ver Hoja.cs). La hoja se pinta entera en su superficie cuando
// algo cambio, como la capa de la referencia pintaba cada ventana (Escritorio.ArmaCapas, PintaArbol y PintaHijo): primero el marco de XP,
// despues lo de dentro (el fondo y lo que pinte la hoja, recortado a su cliente) y encima las piezas, de la ultima a la primera (Controls[0]
// arriba), cada una en su sitio y recortada a su medida y por las hermanas que tiene encima: en WinForms cada control es una ventana con
// WS_CLIPSIBLINGS, y donde la tapa una hermana de encima no se ve, aunque la de encima sea transparente (una transparente ensena lo que
// pinta su padre, PaintTransparentBackground, nunca a sus hermanas de debajo). Lo que lance una pieza al pintarse va a ThreadException y
// la pieza queda con la «X» roja (CajaPieza.PintaFondoYContenido).
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    public partial class Hoja
    {
        private bool excepcionPintando;   // (la «X» roja de lo de dentro de la hoja)
        private void PintaTodo(SKCanvas g)
        {
            PinturaXp.Marco(g, Width, Height, Marco, AltoBarra, Activa == this, text, CajaCerrar, EstadoCerrar);
            Point oc = OrigenCliente; Size cl = ClientSize;
            int n = g.Save();
            g.Translate(oc.X, oc.Y);
            g.ClipRect(new SKRect(0, 0, cl.Width, cl.Height), SKClipOperation.Intersect, false);
            int m = g.Save();
            try { PintaCliente(g, new Rectangle(Point.Empty, cl)); }
            catch (Exception ex) { PantallaRocola.Falla(ex); }
            finally { g.RestoreToCount(m); }
            g.RestoreToCount(n);
            n = g.Save();
            g.Translate(oc.X, oc.Y);
            List<IPieza> l = Controls.Lista;
            for (int i = l.Count - 1; i >= 0; i--) PintaHija(g, l, i);
            g.RestoreToCount(n);
        }
        // LO DE DENTRO DE LA HOJA (el WM_PAINT de la ventana: OnPaintBackground con su color y OnPaint), con la «X» roja si lanza
        private void PintaCliente(SKCanvas g, Rectangle cr)
        {
            if (excepcionPintando) { PinturaXp.ExcepcionAlPintar(g, cr); return; }
            bool lanzo = true;
            try
            {
                Color c = BackColor;
                if (c.A != 0) PinturaXp.Rellena(g, cr, c);
                PintaContenido(g);
                lanzo = false;
            }
            finally { if (lanzo) { excepcionPintando = true; Invalidate(); } }
        }
        // lo que pinta la hoja encima de su fondo (OnPaint): de fabrica nada; el aviso, su icono
        internal virtual void PintaContenido(SKCanvas g) { }
        // UNA PIEZA, RECORTADA POR LAS HERMANAS QUE TIENE ENCIMA (WS_CLIPSIBLINGS: las de antes en la lista)
        private static void PintaHija(SKCanvas g, List<IPieza> hijas, int i)
        {
            CajaPieza c = hijas[i].Caja;
            Rectangle rh = c.Bounds;
            if (!c.VisiblePropio || rh.Width <= 0 || rh.Height <= 0) return;
            int n = -1;
            for (int j = i - 1; j >= 0; j--)
            {
                Rectangle s = hijas[j].Caja.Bounds;
                if (!hijas[j].Caja.VisiblePropio || s.Width <= 0 || s.Height <= 0 || !s.IntersectsWith(rh)) continue;
                if (n < 0) n = g.Save();
                g.ClipRect(new SKRect(s.Left, s.Top, s.Right, s.Bottom), SKClipOperation.Difference, false);
            }
            PintaArbol(g, hijas[i]);
            if (n >= 0) g.RestoreToCount(n);
        }
        // una pieza y sus hijas, en su sitio (g esta en el origen del cliente de su padre)
        private static void PintaArbol(SKCanvas g, IPieza p)
        {
            CajaPieza c = p.Caja;
            Rectangle r = c.Bounds;
            if (!c.VisiblePropio || r.Width <= 0 || r.Height <= 0) return;
            int n = g.Save();
            try
            {
                g.Translate(r.Left, r.Top);
                g.ClipRect(new SKRect(0, 0, r.Width, r.Height), SKClipOperation.Intersect, false);
                int m = g.Save();
                try { c.PintaFondoYContenido(g); }
                catch (Exception ex) { PantallaRocola.Falla(ex); }
                finally { g.RestoreToCount(m); }
                Piezas hs = c.Hijas;
                if (hs != null) for (int k = hs.Lista.Count - 1; k >= 0; k--) PintaHija(g, hs.Lista, k);
            }
            finally { g.RestoreToCount(n); }
        }
    }
}

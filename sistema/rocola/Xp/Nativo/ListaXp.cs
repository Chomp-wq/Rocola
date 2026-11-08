// LA LISTA (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP era el ListBox de WinForms de Ctrl+F2 (las carpetas de la
// musica). Solo lo que XP usa de el (el inventario: SetBounds, BackColor, ForeColor, BorderStyle, IntegralHeight, HorizontalScrollbar,
// Items.Clear, Items.Add, SelectedIndex) y sus reglas, portadas de la capa (Plataforma/Ventanas.Control.cs: ListBox), revisada contra la
// fuente (ListBox.cs): renglones del alto de la letra, el elegido en el azul de seleccion de XP; se elige con el raton o con las flechas,
// Inicio, Fin y AvPag/RePag (las quiere: no mueven el foco, IsInputKey); barras de desplazamiento de 17 puntos cuando hace falta (la
// horizontal solo con HorizontalScrollbar, para las rutas largas), con el pulgar que se arrastra; la rueda la mueve de 3 en 3 y no sube al
// padre. El borde: Fixed3D, el del tema (azul grisaceo); FixedSingle, una linea negra (WS_BORDER).
using System;
using System.Collections.Generic;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // LOS RENGLONES DE LA LISTA (ListBox.ObjectCollection): XP solo los borra y los anade
    public sealed class RenglonesLista
    {
        private readonly ListaXp dueno; internal readonly List<object> L = new List<object>();
        internal RenglonesLista(ListaXp d) { dueno = d; }
        public void Clear() { L.Clear(); dueno.QuitaEleccion(); dueno.Invalida(); }
        public int Add(object item) { L.Add(item); dueno.Invalida(); return L.Count - 1; }
        internal int Count { get { return L.Count; } }
        internal object this[int i] { get { return L[i]; } }
    }

    public sealed class ListaXp : IPieza, IDisposable
    {
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        private int sel = -1, top, desplazaX;
        private const int Barra = PinturaXp.Barra;   // SM_CXVSCROLL / SM_CYHSCROLL de XP
        // ListBox: 120x96 de fabrica, fondo blanco (SystemColors.Window), borde Fixed3D, IntegralHeight
        public ListaXp()
        {
            caja = new CajaPieza(this, 120, 96);
            Items = new RenglonesLista(this);
            caja.BackColor = Color.White;
        }
        // ---------- lo que XP usa ----------
        public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
        internal Rectangle Bounds { get { return caja.Bounds; } }
        public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
        public Color ForeColor { get { return caja.ForeColor; } set { caja.ForeColor = value; } }
        public BordeXp BorderStyle { get; set; } = BordeXp.Fixed3D;
        public bool IntegralHeight { get; set; } = true;
        public bool HorizontalScrollbar { get; set; }
        public RenglonesLista Items { get; }
        public int SelectedIndex
        {
            get { return sel; }
            set { if (value < -1 || value >= Items.Count) value = -1; if (sel == value) return; sel = value; AVista(); caja.Invalidate(); }
        }
        public void Dispose() { caja.Desecha(null); }
        internal void QuitaEleccion() { sel = -1; }
        internal void Invalida() { caja.Invalidate(); }
        internal int TopIndex { get { return top; } set { top = Math.Max(0, Math.Min(value, Math.Max(0, Items.Count - Visibles))); caja.Invalidate(); } }
        // el alto del renglon: el de la letra (ItemHeight de un ListBox sin OwnerDraw: Font.Height)
        private int ItemHeight { get { return Math.Max(1, caja.Font.AltoEntero); } }
        private int Marco { get { return BorderStyle == BordeXp.None ? 0 : 1; } }
        private int AnchoMayor()
        {
            int w = 0;
            foreach (object o in Items.L) w = Math.Max(w, TextoGdi.Mide(Convert.ToString(o), caja.Font, new Size(int.MaxValue, int.MaxValue), BanderasGdi.SinMargen).Width + 3);
            return w;
        }
        // lo que queda para los renglones y si van las barras (la vertical si no caben; la horizontal si un renglon es mas ancho)
        private void Zonas(out Rectangle util, out bool vert, out bool horiz)
        {
            int m = Marco; Rectangle b = caja.Bounds;
            Rectangle r = new Rectangle(m, m, Math.Max(0, b.Width - 2 * m), Math.Max(0, b.Height - 2 * m));
            vert = false; horiz = false;
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                int w = r.Width - (vert ? Barra : 0), h = r.Height - (horiz ? Barra : 0);
                vert = Items.Count * ItemHeight > h;
                horiz = HorizontalScrollbar && AnchoMayor() > w;
            }
            util = new Rectangle(r.X, r.Y, Math.Max(0, r.Width - (vert ? Barra : 0)), Math.Max(0, r.Height - (horiz ? Barra : 0)));
        }
        private int Visibles { get { Rectangle u; bool v, h; Zonas(out u, out v, out h); return Math.Max(1, u.Height / Math.Max(1, ItemHeight)); } }
        private void AVista() { if (sel < 0) return; int n = Visibles; if (sel < top) top = sel; else if (sel >= top + n) top = sel - n + 1; }

        // ---------- el teclado (ListBox.IsInputKey y el ListBox de Windows con WM_KEYDOWN) ----------
        bool IPieza.QuiereTecla(Tecla keyData)
        {
            Tecla k = keyData & Tecla.KeyCode;
            return k == Tecla.Up || k == Tecla.Down || k == Tecla.Left || k == Tecla.Right || k == Tecla.PageUp || k == Tecla.PageDown || k == Tecla.Home || k == Tecla.End;
        }
        void IPieza.TeclaAbajo(PulsoTecla e)
        {
            if (e.Handled || Items.Count == 0) return;
            int n = Visibles, i;
            switch (e.KeyCode)
            {
                case Tecla.Down: i = Math.Min(Items.Count - 1, sel + 1); break;
                case Tecla.Up: i = Math.Max(0, sel - 1); break;
                case Tecla.PageDown: i = Math.Min(Items.Count - 1, Math.Max(0, sel) + n - 1); break;
                case Tecla.PageUp: i = Math.Max(0, sel - n + 1); break;
                case Tecla.Home: i = 0; break;
                case Tecla.End: i = Items.Count - 1; break;
                case Tecla.Left: desplazaX = Math.Max(0, desplazaX - 8); caja.Invalidate(); e.Handled = true; return;
                case Tecla.Right: desplazaX += 8; caja.Invalidate(); e.Handled = true; return;
                default: return;
            }
            SelectedIndex = i; e.Handled = true;
        }
        // ---------- el raton ----------
        void IPieza.RatonAbajo(Raton e)
        {
            caja.Focus();
            Rectangle u; bool v, h; Zonas(out u, out v, out h);
            if (v && e.X >= u.Right && e.Y < u.Bottom)
            {   // la barra vertical: las flechas de una en una, el canal de pagina en pagina, y el pulgar se arrastra (SB_THUMBTRACK)
                int n = Visibles; Rectangle pv = PulgarV(u);
                if (e.Y < u.Top + Barra) TopIndex = top - 1; else if (e.Y >= u.Bottom - Barra) TopIndex = top + 1;
                else if (pv.Contains(e.X, e.Y)) agarreV = e.Y - pv.Top;
                else TopIndex = e.Y < pv.Top ? top - n : top + n;
            }
            else if (h && e.Y >= u.Bottom && e.X < u.Right)
            {
                int max = Math.Max(0, AnchoMayor() - u.Width); Rectangle ph = PulgarH(u);
                if (e.X < u.Left + Barra) desplazaX -= 8; else if (e.X >= u.Right - Barra) desplazaX += 8;
                else if (ph.Contains(e.X, e.Y)) agarreH = e.X - ph.Left;
                else desplazaX += e.X < ph.Left ? -u.Width : u.Width;
                desplazaX = Math.Max(0, Math.Min(max, desplazaX)); caja.Invalidate();
            }
            else if (u.Contains(e.X, e.Y))
            {
                int i = top + (e.Y - u.Top) / Math.Max(1, ItemHeight);
                if (i < Items.Count) SelectedIndex = i;
            }
        }
        // ARRASTRAR EL PULGAR: la lista sigue al pulgar mientras se mueve (la posicion por la proporcion del canal, como la barra de Windows),
        // y al soltar queda donde se dejo
        private int agarreV = -1, agarreH = -1;
        void IPieza.RatonMueve(Raton e)
        {
            if (agarreV < 0 && agarreH < 0) return;
            Rectangle u; bool v, h; Zonas(out u, out v, out h);
            if (agarreV >= 0 && v)
            {
                Rectangle pv = PulgarV(u); int canal = u.Height - 2 * Barra - pv.Height, sobra = Math.Max(0, Items.Count - Visibles);
                if (canal > 0) TopIndex = (int)Math.Round((double)(e.Y - agarreV - (u.Top + Barra)) * sobra / canal);
            }
            if (agarreH >= 0 && h)
            {
                Rectangle ph = PulgarH(u); int canal = u.Width - 2 * Barra - ph.Width, sobra = Math.Max(0, AnchoMayor() - u.Width);
                if (canal > 0) { desplazaX = Math.Max(0, Math.Min(sobra, (int)Math.Round((double)(e.X - agarreH - (u.Left + Barra)) * sobra / canal))); caja.Invalidate(); }
            }
        }
        void IPieza.RatonArriba(Raton e) { agarreV = agarreH = -1; }
        // la rueda: 3 renglones por muesca, y la lista de Windows se la queda (no sube al padre)
        void IPieza.Rueda(Raton e) { TopIndex = top - e.Delta / 120 * 3; e.Handled = true; }
        void IPieza.AlRecibirFoco() { caja.Invalidate(); }
        void IPieza.AlPerderFoco() { caja.Invalidate(); }
        private Rectangle PulgarV(Rectangle u)
        {
            int canal = u.Height - 2 * Barra, n = Visibles, total = Math.Max(n, Items.Count);
            int largo = Math.Max(8, canal * n / Math.Max(1, total)), y = u.Top + Barra + (total > n ? (canal - largo) * top / (total - n) : 0);
            return new Rectangle(u.Right, y, Barra, largo);
        }
        private Rectangle PulgarH(Rectangle u)
        {
            int canal = u.Width - 2 * Barra, total = Math.Max(u.Width, AnchoMayor());
            int largo = Math.Max(8, canal * u.Width / Math.Max(1, total)), x = u.Left + Barra + (total > u.Width ? (canal - largo) * desplazaX / (total - u.Width) : 0);
            return new Rectangle(x, u.Bottom, largo, Barra);
        }
        // ---------- lo que pinta (la capa: ListBox.OnPaint) ----------
        void IPieza.PintaContenido(SKCanvas g)
        {
            SKMatrix hdc = g.TotalMatrix;
            Rectangle u; bool v, h; Zonas(out u, out v, out h);
            int alto = Math.Max(1, ItemHeight), mayor = AnchoMayor();
            desplazaX = h ? Math.Max(0, Math.Min(desplazaX, mayor - u.Width)) : 0;
            top = Math.Max(0, Math.Min(top, Math.Max(0, Items.Count - Math.Max(1, u.Height / alto))));
            int n = g.Save();
            g.ClipRect(new SKRect(u.Left, u.Top, u.Right, u.Bottom), SKClipOperation.Intersect, false);
            for (int i = top, y = u.Top; i < Items.Count && y < u.Bottom; i++, y += alto)
            {
                bool elegido = i == sel;
                if (elegido) PinturaXp.Rellena(g, new Rectangle(u.Left, y, u.Width, alto), Color.FromArgb(49, 106, 197));
                Rectangle rt = new Rectangle(u.Left + 1 - desplazaX, y, Math.Max(u.Width, mayor) + 8, alto);
                PinturaXp.Texto(g, hdc, Convert.ToString(Items[i]), caja.Font, rt, elegido ? Color.White : caja.ForeColor, BanderasGdi.SinMargen | BanderasGdi.UnRenglon | BanderasGdi.SinPrefijo);
            }
            g.RestoreToCount(n);
            if (v) PinturaXp.BarraDesplazamiento(g, new Rectangle(u.Right, u.Top, Barra, u.Height), true, PulgarV(u));
            if (h) PinturaXp.BarraDesplazamiento(g, new Rectangle(u.Left, u.Bottom, u.Width, Barra), false, PulgarH(u));
            if (v && h) PinturaXp.Rellena(g, new RectangleF(u.Right, u.Bottom, Barra, Barra), Color.FromArgb(236, 233, 216));
            if (BorderStyle != BordeXp.None)
                PinturaXp.TrazaRect(g, new RectangleF(0, 0, caja.Bounds.Width - 1, caja.Bounds.Height - 1), BorderStyle == BordeXp.FixedSingle ? Color.Black : Color.FromArgb(127, 157, 185), 1f, false);
        }
    }
}

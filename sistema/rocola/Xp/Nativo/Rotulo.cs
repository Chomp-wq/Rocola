// EL ROTULO Y EL GRUPO (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP eran Label y Panel de WinForms en sus hojas.
// Solo lo que XP usa de ellos (el inventario) y sus reglas, portadas de la capa (Plataforma/Ventanas.Control.cs: Label y Panel), revisada
// contra la fuente (Label.cs, Panel.cs, ControlPaint.cs).
using System;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // EL ROTULO (Label): un texto con su letra, su color y su alineacion, pintado con el texto de GDI (TextRenderer: Program.cs de XP pone
    // SetCompatibleTextRenderingDefault(false)). No toma el foco ni entra en el tabulador (Label: Selectable apagado, TabStop false); acepta
    // el fondo transparente y se repinta al cambiar de medida y de texto (Label.cs: SupportsTransparentBackColor, ResizeRedraw,
    // OnTextChanged). Mide 100x23 de fabrica (Label.DefaultSize).
    public sealed class Rotulo : IPieza, IDisposable
    {
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        private AlineaTexto alin = AlineaTexto.TopLeft;
        public Rotulo()
        {
            caja = new CajaPieza(this, 100, 23);
            caja.Seleccionable = false; caja.Transparente = true; caja.RepintaConMedida = true; caja.RepintaConTexto = true; caja.TabStop = false;
        }
        // ---------- lo que XP usa ----------
        public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
        public Rectangle Bounds { get { return caja.Bounds; } }
        public string Text { get { return caja.Text; } set { caja.Text = value; } }
        public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
        public Color ForeColor { get { return caja.ForeColor; } set { caja.ForeColor = value; } }
        public Letra Font { get { return caja.Font; } set { caja.Font = value; } }
        public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } }
        public bool Enabled { get { return caja.Enabled; } set { caja.Enabled = value; } }
        public AlineaTexto TextAlign { get { return alin; } set { alin = value; caja.Invalidate(); } }
        public bool AutoEllipsis { get; set; }
        public bool UseMnemonic { get; set; } = true;
        public void Dispose() { caja.Desecha(null); }
        // ---------- lo que pinta (Label.OnPaint): ControlPaint.CreateTextFormatFlags con WordBreak y TextBoxControl (DT_EDITCONTROL: el ultimo
        // renglon que no cabe entero no se pinta), EndEllipsis con AutoEllipsis, NoPrefix sin UseMnemonic; en gris, TextRenderer.DisabledTextColor
        // (ControlDark, o el fondo oscurecido si el fondo es mas oscuro). El «&» se quita como lo hacia la capa (la referencia). ----------
        void IPieza.PintaContenido(SKCanvas g)
        {
            SKMatrix hdc = g.TotalMatrix;
            BanderasGdi f = PinturaXp.DeAlineacion(alin) | BanderasGdi.Partir | BanderasGdi.CajaTexto;
            if (AutoEllipsis) f |= BanderasGdi.PuntosAlFinal;
            if (!UseMnemonic) f |= BanderasGdi.SinPrefijo;
            Color c = caja.Enabled ? caja.ForeColor : PinturaXp.ColorEnGris(caja.BackColor);
            PinturaXp.Texto(g, hdc, UseMnemonic ? PinturaXp.SinPrefijo(caja.Text) : caja.Text, caja.Font, caja.ClientRectangle, c, f);
        }
    }

    // EL GRUPO (Panel): una caja con piezas dentro y su fondo; no toma el foco ni entra en el tabulador (sus piezas si: Panel.cs, Selectable
    // apagado y TabStop false); acepta el fondo transparente; con borde, la linea gris de XP. Mide 200x100 de fabrica (Panel.DefaultSize).
    public sealed class Grupo : IPieza, IContenedor, IDisposable
    {
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        private BordeXp borde;
        public Grupo()
        {
            caja = new CajaPieza(this, 200, 100);
            caja.Seleccionable = false; caja.Transparente = true; caja.TabStop = false;
            Controls = new Piezas(this);
            caja.Hijas = Controls;
        }
        // ---------- lo que XP usa ----------
        public Piezas Controls { get; }
        public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
        public Rectangle Bounds { get { return caja.Bounds; } }
        public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
        public Color ForeColor { get { return caja.ForeColor; } set { caja.ForeColor = value; } }
        public Letra Font { get { return caja.Font; } set { caja.Font = value; } }
        public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } }
        public bool Enabled { get { return caja.Enabled; } set { caja.Enabled = value; } }
        public BordeXp BorderStyle { get { return borde; } set { borde = value; caja.Invalidate(); } }
        public void BringToFront() { caja.BringToFront(); }
        public void Invalidate() { caja.Invalidate(); }
        public bool IsDisposed { get { return caja.IsDisposed; } }
        public event EventHandler VisibleChanged { add { caja.VisibleChanged += value; } remove { caja.VisibleChanged -= value; } }
        public event EventHandler Disposed { add { caja.Disposed += value; } remove { caja.Disposed -= value; } }
        public void Dispose() { caja.Desecha(null); }
        // ---------- lo que es como contenedor de sus piezas ----------
        Hoja IContenedor.HojaDe { get { return caja.Hoja; } }
        IContenedor IContenedor.PadreDe { get { return caja.Padre; } }
        bool IContenedor.VisibleDe { get { return caja.Visible; } }
        bool IContenedor.EnabledDe { get { return caja.Enabled; } }
        Color IContenedor.FondoDe { get { return caja.BackColor; } }
        Color IContenedor.TintaDe { get { return caja.ForeColor; } }
        Letra IContenedor.LetraDe { get { return caja.Font; } }
        void IContenedor.Ensucia() { caja.Invalidate(); }
        // ---------- lo que pinta (Panel.OnPaint de la capa: el borde, si lo tiene) ----------
        void IPieza.PintaContenido(SKCanvas g)
        {
            if (borde != BordeXp.None) PinturaXp.TrazaRect(g, new RectangleF(0, 0, caja.Bounds.Width - 1, caja.Bounds.Height - 1), Color.FromArgb(172, 168, 153), 1f, false);
        }
    }
}

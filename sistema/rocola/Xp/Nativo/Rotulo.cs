// EL ROTULO Y EL GRUPO (port nativo de las ventanas de XP, ventanas2, pasos H y C): lo que en XP eran Label y Panel de WinForms en sus
// hojas (Ctrl+F2 y, desde el paso C, Ctrl+F3 y el historial de caratulas). Solo lo que XP usa de ellos (el inventario y el compilador: lo que
// las lineas de XP leen o ponen) y sus reglas, portadas de la capa (en la referencia, Plataforma/Ventanas.Control.cs: Label y Panel), revisada contra la fuente
// (Label.cs, Panel.cs, ControlPaint.cs, Control.cs).
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
        // (Control.Bounds: ponerlo es SetBounds con las cuatro medidas; Ctrl+F3 copia la caja de un rotulo a otro, Rendimiento.cs)
        public Rectangle Bounds { get { return caja.Bounds; } set { caja.SetBounds(value.X, value.Y, value.Width, value.Height); } }
        public string Name { get { return caja.Name; } set { caja.Name = value; } }
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
    // No es sellado porque en XP la tarjeta de Ctrl+F3 (Tarjeta, Form1.Ajustes.cs) hereda de Panel: hereda de el y pinta lo suyo (Pinta,
    // el OnPaint de Panel) y suelta lo suyo (Dispose(bool), el patron de Control.Dispose). Nada mas lo hereda.
    public class Grupo : IPieza, IContenedor, IDisposable
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
        // (Control.Bounds, Location, Size, Width y Height: ponerlos es SetBounds con lo que no cambia tal cual, Control.cs)
        public Rectangle Bounds { get { return caja.Bounds; } set { caja.SetBounds(value.X, value.Y, value.Width, value.Height); } }
        public Point Location { get { return caja.Bounds.Location; } set { Rectangle b = caja.Bounds; caja.SetBounds(value.X, value.Y, b.Width, b.Height); } }
        public Size Size { get { return caja.Bounds.Size; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, value.Width, value.Height); } }
        public int Width { get { return caja.Bounds.Width; } }
        public int Height { get { return caja.Bounds.Height; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, b.Width, value); } }
        public string Name { get { return caja.Name; } set { caja.Name = value; } }
        public int TabIndex { get { return caja.TabIndex; } set { caja.TabIndex = value; } }
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
        // DESECHARLO (Control.Dispose: Dispose(true); el de la tarjeta suelta su letra y llama a este)
        public void Dispose() { Dispose(true); }
        protected virtual void Dispose(bool disposing) { if (disposing) caja.Desecha(null); }
        // ---------- lo que es como contenedor de sus piezas ----------
        Hoja IContenedor.HojaDe { get { return caja.Hoja; } }
        IContenedor IContenedor.PadreDe { get { return caja.Padre; } }
        bool IContenedor.VisibleDe { get { return caja.Visible; } }
        bool IContenedor.EnabledDe { get { return caja.Enabled; } }
        Color IContenedor.FondoDe { get { return caja.BackColor; } }
        Color IContenedor.TintaDe { get { return caja.ForeColor; } }
        Letra IContenedor.LetraDe { get { return caja.Font; } }
        void IContenedor.Ensucia() { caja.Invalidate(); }
        // ---------- lo que pinta (Panel.OnPaint de la capa: el borde, si lo tiene; la tarjeta pinta lo suyo en su lugar) ----------
        void IPieza.PintaContenido(SKCanvas g) { Pinta(g); }
        protected virtual void Pinta(SKCanvas g)
        {
            if (borde != BordeXp.None) PinturaXp.TrazaRect(g, new RectangleF(0, 0, caja.Bounds.Width - 1, caja.Bounds.Height - 1), Color.FromArgb(172, 168, 153), 1f, false);
        }
    }
}

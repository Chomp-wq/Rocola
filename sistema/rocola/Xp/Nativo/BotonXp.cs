// EL BOTON (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP era el Button de WinForms en sus hojas (Ctrl+F2, los avisos).
// Solo lo que XP usa de el (el inventario: SetBounds, Text, FlatStyle, FlatAppearance.BorderColor, BackColor, ForeColor, Enabled, Click) y
// sus reglas, portadas de la capa (Plataforma/Ventanas.Control.cs: Button), revisada contra la fuente (Button.cs, ButtonBase.cs,
// ButtonFlatAdapter, ControlPaint.cs): se aprieta con el raton (y toma el foco), con Espacio (al soltarlo) y con Intro si es el boton por
// defecto (el que tiene el foco: Form.ProcessDialogKey de la hoja); se pinta plano (FlatStyle.Flat, con los colores que calcula ControlPaint)
// o con el tema Luna de XP (el de los avisos).
using System;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // COMO SE PINTA (FlatStyle): los mismos numeros y nombres, solo los que XP usa
    public enum EstiloBoton { Flat = 0, Standard = 2 }
    // EL ASPECTO DEL BOTON PLANO (FlatButtonAppearance): XP solo le pone el color del borde
    public sealed class AspectoPlano { public Color BorderColor { get; set; } }

    public sealed class BotonXp : IPieza, IDisposable
    {
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        private bool porDefecto, encima, apretado, espacio;
        // Button: acepta el fondo transparente, repinta al cambiar de medida y de texto (ButtonBase.cs), y su clic no es el de Control
        // (StandardClick y StandardDoubleClick apagados, Button.cs: el clic es el suyo, al soltar, y un doble clic son dos clics). 75x23.
        public BotonXp()
        {
            caja = new CajaPieza(this, 75, 23);
            caja.Transparente = true; caja.RepintaConMedida = true; caja.RepintaConTexto = true; caja.ClicNormal = false; caja.DobleClicNormal = false;
        }
        // ---------- lo que XP usa ----------
        public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
        internal Rectangle Bounds { get { return caja.Bounds; } }
        public string Text { get { return caja.Text; } set { caja.Text = value; } }
        public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
        public Color ForeColor { get { return caja.ForeColor; } set { caja.ForeColor = value; } }
        public bool Enabled { get { return caja.Enabled; } set { caja.Enabled = value; } }
        public EstiloBoton FlatStyle { get; set; } = EstiloBoton.Standard;
        public AspectoPlano FlatAppearance { get; } = new AspectoPlano();
        public event EventHandler Click;
        public void Dispose() { caja.Desecha(null); }
        // el resultado que le pone a su hoja al pulsarlo (Button.DialogResult: lo usa el aviso)
        internal Resultado Resultado { get; set; }
        // EL BOTON POR DEFECTO (IButtonControl.NotifyDefault): se pinta con el borde de mas (plano) o el azul (Luna)
        internal void NotifyDefault(bool value) { if (porDefecto != value) { porDefecto = value; caja.Invalidate(); } }
        // Button.PerformClick (Button.cs:324-337): si se puede elegir, su clic
        internal void PerformClick() { if (caja.CanSelect) OnClick(); }
        // Button.OnClick (Button.cs:243-257): la hoja toma su resultado (tambien None) y despues el aviso Click
        private void OnClick()
        {
            Hoja f = caja.Hoja;
            if (f != null) f.resultado = Resultado;
            EventHandler h = Click; if (h != null) h(this, EventArgs.Empty);
        }
        // ---------- el raton (ButtonBase.OnMouseEnter, OnMouseLeave, OnMouseDown, OnMouseMove; Button.OnMouseUp) ----------
        void IPieza.RatonEntra() { encima = true; caja.Invalidate(); }
        void IPieza.RatonSale() { encima = false; caja.Invalidate(); }
        void IPieza.RatonAbajo(Raton e) { if (e.Button == BotonRaton.Left) { caja.Focus(); apretado = true; caja.Invalidate(); } }
        void IPieza.RatonMueve(Raton e)
        {
            bool dentro = caja.ClientRectangle.Contains(e.X, e.Y);
            if (apretado && encima != dentro) { encima = dentro; caja.Invalidate(); }
        }
        // el clic es del boton izquierdo, apretado aqui y soltado encima del mismo boton (WindowFromPoint: Button.OnMouseUp, Button.cs:268-293)
        void IPieza.RatonArriba(Raton e)
        {
            bool estaba = apretado;
            if (apretado) { apretado = false; caja.Invalidate(); }
            if (e.Button == BotonRaton.Left && estaba && Hoja.PiezaEn(PantallaRocola.Flecha) == (object)this) OnClick();
        }
        // ---------- el teclado: Espacio lo aprieta y al soltarlo lo pulsa (ButtonBase.OnKeyDown y OnKeyUp, ButtonBase.cs:1100-1123 y 1128-1154) ----------
        void IPieza.TeclaAbajo(PulsoTecla e)
        {
            if (e.KeyData == Tecla.Space) { if (!espacio) { espacio = true; caja.Invalidate(); } e.Handled = true; }
        }
        void IPieza.TeclaArriba(PulsoTecla e)
        {
            if (espacio)
            {
                espacio = false; caja.Invalidate();
                if (e.KeyCode == Tecla.Enter || e.KeyCode == Tecla.Space) OnClick();
                e.Handled = true;
            }
        }
        void IPieza.AlPerderFoco() { espacio = false; caja.Invalidate(); }
        void IPieza.AlRecibirFoco() { caja.Invalidate(); }
        private bool Abajo { get { return espacio || (apretado && encima); } }
        // ---------- lo que pinta (la capa: Button.OnPaint) ----------
        void IPieza.PintaContenido(SKCanvas g)
        {
            SKMatrix hdc = g.TotalMatrix;
            string t = PinturaXp.SinPrefijo(caja.Text);
            BanderasGdi fl = PinturaXp.DeAlineacion(AlineaTexto.MiddleCenter) | BanderasGdi.Partir;
            Rectangle r = caja.ClientRectangle;
            int ancho = r.Width, alto = r.Height;
            bool activo = caja.Enabled;
            Color fondo0 = caja.BackColor;
            if (FlatStyle == EstiloBoton.Flat)
            {
                // ButtonFlatAdapter (PaintUp, PaintOver y PaintDown) con los colores de ButtonBaseAdapter.ColorOptions.Calculate
                Color fondo = fondo0, marco = FlatAppearance.BorderColor.IsEmpty ? caja.ForeColor : FlatAppearance.BorderColor;
                if (activo && Abajo)
                {
                    Color luz = PinturaXp.LightLight(fondo0);
                    fondo = PinturaXp.Ajusta(luz, luz.GetBrightness() < .5f ? 1.2f : 0.9f);
                }
                else if (activo && encima) fondo = PinturaXp.Ajusta(fondo0, fondo0.GetBrightness() < .5f ? 1.2f : 0.9f);
                PinturaXp.Rellena(g, r, fondo);
                Rectangle rt = r; rt.Inflate(-2, -2); if (activo && Abajo) rt.Offset(1, 1);
                PinturaXp.Texto(g, hdc, t, caja.Font, rt, activo ? caja.ForeColor : PinturaXp.Dark(fondo0), fl);
                Rectangle rm = r;
                if (porDefecto) { PinturaXp.TrazaRect(g, new RectangleF(rm.X, rm.Y, rm.Width - 1, rm.Height - 1), marco, 1f, false); rm.Inflate(-1, -1); }
                PinturaXp.TrazaRect(g, new RectangleF(rm.X, rm.Y, rm.Width - 1, rm.Height - 1), marco, 1f, false);
            }
            else
            {
                // el boton de Windows XP con el tema Luna: marco azul oscuro redondeado, cara blanca que se oscurece hacia abajo, anaranjado
                // con el raton encima, azul si es el boton por defecto, y gris si no se puede usar
                RectangleF rf = new RectangleF(0.5f, 0.5f, ancho - 1f, alto - 1f);
                using (PinturaXp.Trazo gp = PinturaXp.Redondo(rf, 3f))
                {
                    Color c1 = !activo ? Color.FromArgb(245, 244, 234) : Abajo ? Color.FromArgb(229, 228, 221) : Color.FromArgb(255, 255, 255);
                    Color c2 = !activo ? Color.FromArgb(245, 244, 234) : Abajo ? Color.FromArgb(242, 241, 238) : Color.FromArgb(236, 235, 230);
                    SKPaint b = PinturaXp.Degradado(r, c1, c2, true);
                    try { Pixel.Rellena(g, gp.P, b, true); } finally { PinturaXp.Suelta(b); }
                    if (activo && (encima || porDefecto) && !Abajo)
                    {
                        Color e1 = encima ? Color.FromArgb(255, 240, 207) : Color.FromArgb(206, 231, 255), e2 = encima ? Color.FromArgb(229, 151, 0) : Color.FromArgb(105, 130, 238);
                        PinturaXp.TrazaRect(g, new RectangleF(2f, 2f, ancho - 4f, alto - 4f), e2, 2f, true);
                        PinturaXp.Linea(g, 2f, 1.5f, ancho - 2f, 1.5f, e1, 1f, true);
                    }
                    SKPaint p = PinturaXp.Pluma(activo ? Color.FromArgb(0, 60, 116) : Color.FromArgb(201, 199, 186), 1f);
                    try { Pixel.Traza(g, gp.P, p, true); } finally { PinturaXp.Suelta(p); }
                }
                Rectangle rt = r; rt.Inflate(-3, -3);
                PinturaXp.Texto(g, hdc, t, caja.Font, rt, activo ? caja.ForeColor : Color.FromArgb(161, 161, 146), fl);
            }
        }
    }
}

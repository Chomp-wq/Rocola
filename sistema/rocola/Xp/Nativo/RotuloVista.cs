// EL ROTULO DE LA PANTALLA (port nativo, tanda 4, oleada 7: grupo rotulos-y-letras, W6 con «letras-de-los-controles»). Lo que en XP eran los
// once Label de la ventana de la rocola (lblStatus, lblNow, lblNowTop, lblNext, lblMejorada, lblTime, lblAlbum -dentro del panel del disco-,
// lblToast, lblAttract, lblConfig y lblGpu), sin imitar a Label ni a Control: una vista de la pantalla nativa (Nativo/IVista.cs) con su
// CajaVista y SOLO lo que el codigo de XP les lee o les pone (el inventario: Text, Visible, Font, ForeColor, BackColor, SetBounds, Bounds,
// Left, Top, Width, Height, Right, Bottom, Size, Location, TextAlign, AutoEllipsis, UseMnemonic, AutoSize, Region e Invalidate; el
// BringToFront de XP es PantallaRocola.VistaAlFrente, como el de las demas vistas). En una hoja (Ctrl+F3, Ctrl+F2, los avisos) el Label es
// otra cosa, una pieza de la hoja (Nativo/Rotulo.cs: su caja es la de las piezas, con el foco y el tabulador de la hoja); este es el de la
// ventana de la rocola, que la pantalla compone entre las demas vistas, el panel de la tarjeta y el video.
//
// Las reglas son las de WinForms (dotnet/winforms release/3.0, portada de .NET Framework 4.8: Label.cs y Control.cs), como las pintaba la
// capa (en la referencia, Plataforma/Ventanas.Control.cs, Label) que esto sustituye:
//   - Label: no toma el foco ni entra en el tabulador (SetStyle(Selectable, false), TabStop = false), acepta el fondo transparente
//     (SupportsTransparentBackColor), se repinta al cambiar de medida (ResizeRedraw) y de texto (OnTextChanged); mide 100x23 de fabrica
//     (DefaultSize). El raton SI es suyo dentro de su caja (Label.WndProc, WM_NCHITTEST: HTCLIENT), aunque no haga nada con el: lo que
//     tapa no lo recibe.
//   - Los cambios repintan solo si cambian (Control.Text, ForeColor, BackColor, Font; Label.TextAlign, UseMnemonic, AutoEllipsis).
//   - La letra: el Control.Font de WinForms con Font.Equals (Letras.Iguales): poner una igual a la guardada NO cambia la guardada (DiscoVivoGpu
//     y las vistas comparan la letra por identidad), y se repinta si la que se ve cambia. Sin poner, la de fabrica de Windows XP
//     (CajaPieza.LetraDeFabrica: Microsoft Sans Serif de 8,25 puntos, la Control.DefaultFont, que es la de la ventana de la rocola: Form1 no
//     pone la suya). XP pone la letra, el color y el fondo a los once (TransLabel, Form1.Fondo.cs, y las lineas de cada uno): los valores
//     heredados del dueno (los ambientales de WinForms) no hacen falta; sin poner, el color es el negro de fabrica (ControlText) y el fondo
//     no se pinta.
//   - La pintura (Label.OnPaint con UseCompatibleTextRendering apagado, que pone Program.cs de XP): el fondo si no es transparente
//     (Control.OnPaintBackground: un relleno del BackColor; con alfa, encima de lo de debajo) y el texto con el TextRenderer de GDI
//     (Nativo/TextoGdi.cs por PinturaXp.Texto) con ControlPaint.CreateTextFormatFlags: la alineacion, WordBreak y TextBoxControl, EndEllipsis
//     con AutoEllipsis y NoPrefix sin UseMnemonic; con UseMnemonic el «&» se quita como lo quitaba la capa (la referencia); con la matriz
//     del HDC, la que trae el lienzo. Sin Image, sin borde (XP no los pone), siempre Enabled (XP no lo toca).
//   - Region: la forma de la ventana (SetWindowRgn). XP solo se la pone al aviso sin tarjeta (lblToast, la pastilla con esquinas redondas);
//     aqui es Forma, un SKPath que el rotulo guarda (Control.Region: la de antes se suelta al poner otra) y que la pantalla usa para recortar
//     la pintura, a los de debajo y el raton (fuera de la forma, el raton es del de debajo).
//   - AutoEllipsis en WinForms ademas ensena un ToolTip con el texto entero al pasar el raton por un rotulo cortado: la capa no lo hacia (la
//     referencia) y aqui tampoco (riesgo apuntado en PORT_rotulos-y-letras.md).
using System;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public sealed class RotuloVista : IVista, IDisposable
    {
        private readonly CajaVista caja;
        private string text = "";
        private Letra font;
        private Color foreColor = Color.Empty, backColor = Color.Empty;
        private AlineaTexto alin = AlineaTexto.TopLeft;
        private bool autoEllipsis, useMnemonic = true;
        private SKPath forma;

        public RotuloVista()
        {
            caja = new CajaVista(this); caja.SetBounds(0, 0, 100, 23);   // (Label.DefaultSize; ResizeRedraw: RepintaConMedida, el de fabrica de la caja)
        }

        // ---------- el sitio y si se ve (Control: Bounds, SetBounds, Location, Size, Right, Bottom, Visible, Invalidate) ----------
        public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } }
        public int Left { get { return caja.Left; } set { caja.Left = value; } }
        public int Top { get { return caja.Top; } set { caja.Top = value; } }
        public int Width { get { return caja.Width; } set { caja.Width = value; } }
        public int Height { get { return caja.Height; } set { caja.Height = value; } }
        public int Right { get { Rectangle b = caja.Bounds; return b.Right; } }
        public int Bottom { get { Rectangle b = caja.Bounds; return b.Bottom; } }
        public Point Location { get { return caja.Bounds.Location; } set { Rectangle b = caja.Bounds; caja.SetBounds(value.X, value.Y, b.Width, b.Height); } }
        public Size Size { get { return caja.Bounds.Size; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, value.Width, value.Height); } }
        public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
        public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } }
        public void Invalidate() { caja.Invalidate(); }
        // un Label no toma el foco ni se para en el tabulador (Label: Selectable apagado, TabStop false)
        public bool Seleccionable { get { return false; } }
        public bool ParaElTabulador { get { return false; } }

        // ---------- lo que ensena ----------
        // Control.Text: null es ""; si cambia, Label.OnTextChanged lo repinta
        public string Text
        {
            get { return text; }
            set { if (value == null) value = ""; if (text == value) return; text = value; caja.Invalidate(); }
        }
        // Control.Font (ver la cabecera): la guardada solo cambia si la nueva no es igual; se repinta si la que se ve cambia
        public Letra Font
        {
            get { return font ?? CajaPieza.LetraDeFabrica; }
            set
            {
                Letra antes = Font;
                if (value == null ? font != null : (font == null || !Letras.Iguales(value, font))) font = value;
                if (value == null || !Letras.Iguales(antes, value)) caja.Invalidate();
            }
        }
        // Control.ForeColor y BackColor: se repinta si el que se ve cambia (OnForeColorChanged, OnBackColorChanged)
        public Color ForeColor
        {
            get { return foreColor.IsEmpty ? Color.Black : foreColor; }
            set { Color antes = ForeColor; foreColor = value; if (!antes.Equals(ForeColor)) caja.Invalidate(); }
        }
        public Color BackColor
        {
            get { return backColor; }
            set { Color antes = BackColor; backColor = value; if (!antes.Equals(BackColor)) caja.Invalidate(); }
        }
        // Label.TextAlign, AutoEllipsis y UseMnemonic: si cambian, se repinta
        public AlineaTexto TextAlign { get { return alin; } set { if (alin == value) return; alin = value; caja.Invalidate(); } }
        public bool AutoEllipsis { get { return autoEllipsis; } set { if (autoEllipsis == value) return; autoEllipsis = value; caja.Invalidate(); } }
        public bool UseMnemonic { get { return useMnemonic; } set { if (useMnemonic == value) return; useMnemonic = value; caja.Invalidate(); } }
        // Label.AutoSize: XP solo lo pone en false, que es el de fabrica de un Label creado en el codigo (no hace nada). Encenderlo seria medirse
        // con el texto (Label.AdjustSize), que XP no usa: no esta.
        public bool AutoSize
        {
            get { return false; }
            set { if (value) throw new NotSupportedException("AutoSize: la rocola de XP no lo enciende en sus rotulos"); }
        }
        // Control.Region (la forma de la ventana, SetWindowRgn): el rotulo se queda con el trazo; la forma de antes se suelta (el setter de
        // Control.Region hace Dispose de la vieja) y la pantalla vuelve a componer (SetWindowRgn repinta si se ve)
        public SKPath Forma
        {
            get { return forma; }
            set
            {
                SKPath antes = forma;
                if (antes == value) return;
                forma = value;
                if (antes != null) antes.Dispose();
                caja.Invalidate();
            }
        }

        // ---------- la pintura (Control.OnPaintBackground y Label.OnPaint; ver la cabecera) ----------
        public void Pinta(SKCanvas g)
        {
            SKMatrix hdc = g.TotalMatrix;   // (el origen del HDC del rotulo: el lienzo ya viene en su esquina)
            Rectangle cr = new Rectangle(0, 0, caja.Width, caja.Height);
            Color bc = BackColor;
            if (bc.A != 0) PinturaXp.Rellena(g, cr, bc);   // (Control.OnPaintBackground: FillRectangle con un SolidBrush, como la capa)
            BanderasGdi f = PinturaXp.DeAlineacion(alin) | BanderasGdi.Partir | BanderasGdi.CajaTexto;
            if (autoEllipsis) f |= BanderasGdi.PuntosAlFinal;
            if (!useMnemonic) f |= BanderasGdi.SinPrefijo;
            PinturaXp.Texto(g, hdc, useMnemonic ? PinturaXp.SinPrefijo(text) : text, Font, cr, ForeColor, f);
        }

        // Control.Dispose: la forma se va con el (la Region de un control desechado tambien)
        public void Dispose() { SKPath f = forma; forma = null; if (f != null) f.Dispose(); }
    }
}

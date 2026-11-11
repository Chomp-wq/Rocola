// LAS PIEZAS PROPIAS DE CTRL+F3, SU MITAD NATIVA (port nativo de las ventanas de XP, ventanas2, paso C). En XP, el deslizador, el
// interruptor y el boton plano de Ctrl+F3 (Form1.Ajustes.cs) y la barra de avance (Form1.Vistas.cs) heredaban de Control: de el sacaban su
// sitio, sus colores, su letra, su texto, el foco, el repintado y el aviso Click. Aqui no heredan de nada: cada una es una pieza de la hoja
// (IPieza, Nativo/Pieza.cs) con su CAJA (CajaPieza, por composicion), y esta mitad le da, de la caja, SOLO lo que su codigo de XP usa de
// Control (lo pidio el compilador: ni un miembro mas) con los nombres con que XP lo usa. Su comportamiento (raton, teclas, foco, pintado)
// esta en su codigo de XP, cada linea con su «EN LINUX»; las reglas de WinForms que la hoja aplica a cada pieza (quien recibe la tecla, el
// clic al soltar encima, el foco, el pintado recortado por las hermanas) estan en Nativo/Hoja*.cs. La tarjeta (Tarjeta : Panel en XP)
// hereda del grupo nativo (Grupo, Nativo/Rotulo.cs) y no necesita nada de aqui.
using System;
using System.Drawing;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
        // EL DESLIZADOR (Control en XP)
        private sealed partial class Deslizador
        {
            private readonly CajaPieza caja;
            CajaPieza IPieza.Caja { get { return caja; } }
            public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
            public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
            public bool TabStop { get { return caja.TabStop; } set { caja.TabStop = value; } }
            public int Width { get { return caja.Bounds.Width; } }
            // (Control.Height: ponerlo es SetBounds con lo demas tal cual, Control.cs)
            public int Height { get { return caja.Bounds.Height; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, b.Width, value); } }
            public bool Enabled { get { return caja.Enabled; } set { caja.Enabled = value; } }
            public string Name { get { return caja.Name; } set { caja.Name = value; } }
            public bool Focused { get { return caja.Focused; } }
            public bool Focus() { return caja.Focus(); }
            public void Invalidate() { caja.Invalidate(); }
        }

        // EL INTERRUPTOR (Control en XP)
        private sealed partial class Interruptor
        {
            private readonly CajaPieza caja;
            CajaPieza IPieza.Caja { get { return caja; } }
            public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
            public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
            public Color ForeColor { get { return caja.ForeColor; } set { caja.ForeColor = value; } }
            public Rocola.Nativo.Letra Font { get { return caja.Font; } set { caja.Font = value; } }
            public string Text { get { return caja.Text; } set { caja.Text = value; } }
            public bool TabStop { get { return caja.TabStop; } set { caja.TabStop = value; } }
            public int Height { get { return caja.Bounds.Height; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, b.Width, value); } }
            public bool Enabled { get { return caja.Enabled; } set { caja.Enabled = value; } }
            public string Name { get { return caja.Name; } set { caja.Name = value; } }
            public bool Focused { get { return caja.Focused; } }
            public bool Focus() { return caja.Focus(); }
            public void Invalidate() { caja.Invalidate(); }
        }

        // EL BOTON PLANO (Control en XP)
        private sealed partial class BotonPlano
        {
            private readonly CajaPieza caja;
            CajaPieza IPieza.Caja { get { return caja; } }
            public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
            public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
            public Color ForeColor { get { return caja.ForeColor; } set { caja.ForeColor = value; } }
            public Rocola.Nativo.Letra Font { get { return caja.Font; } set { caja.Font = value; } }
            public string Text { get { return caja.Text; } set { caja.Text = value; } }
            public bool TabStop { get { return caja.TabStop; } set { caja.TabStop = value; } }
            public int TabIndex { get { return caja.TabIndex; } set { caja.TabIndex = value; } }
            public int Width { get { return caja.Bounds.Width; } }
            public int Height { get { return caja.Bounds.Height; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, b.Width, value); } }
            public bool Enabled { get { return caja.Enabled; } set { caja.Enabled = value; } }
            public string Name { get { return caja.Name; } set { caja.Name = value; } }
            public bool Focused { get { return caja.Focused; } }
            public bool Focus() { return caja.Focus(); }
            public void Invalidate() { caja.Invalidate(); }
            public void BringToFront() { caja.BringToFront(); }
            // EL AVISO CLICK (Control.Click y Control.OnClick, que lo lanza: Control.cs). El de raton: soltar encima del mismo control, con
            // cualquier boton, si no es el segundo de un doble clic (Control.WmMouseUp con StandardClick: OnClick y OnMouseClick; el segundo es
            // DoubleClick y no Click); la hoja lo decide y llama a Clic (Nativo/Hoja.Entrada.cs). El de teclado lo lanza su KeyDown (Espacio o
            // Intro, en su codigo de XP).
            public event EventHandler Click;
            private void OnClick(EventArgs e) { EventHandler h = Click; if (h != null) h(this, e); }
            void IPieza.Clic(Raton e) { OnClick(EventArgs.Empty); }
        }

        // LA BARRA DE AVANCE (Control en XP; Form1.Vistas.cs, solo la usa Ctrl+F3)
        private sealed partial class BarraAvance
        {
            private readonly CajaPieza caja;
            CajaPieza IPieza.Caja { get { return caja; } }
            public void SetBounds(int x, int y, int width, int height) { caja.SetBounds(x, y, width, height); }
            public Color BackColor { get { return caja.BackColor; } set { caja.BackColor = value; } }
            public int Width { get { return caja.Bounds.Width; } }
            public int Height { get { return caja.Bounds.Height; } set { Rectangle b = caja.Bounds; caja.SetBounds(b.X, b.Y, b.Width, value); } }
            public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } }
            public void Invalidate() { caja.Invalidate(); }
        }
    }
}

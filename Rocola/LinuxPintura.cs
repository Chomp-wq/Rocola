// ====================================================================================================
//  ETIQUETAS QUE SE VEN: EL FONDO TRANSPARENTE EN LINUX
// ----------------------------------------------------------------------------------------------------
//  En Windows, a una etiqueta se le pone el fondo "transparente" y el sistema se encarga de que se vea lo
//  que hay detras -en la rocola, el cielo estrellado-. En Linux eso NO pasa: el fondo transparente se
//  pinta de NEGRO, y encima el texto sale recortado. En pantalla se veian rectangulos negros donde va lo
//  que suena, lo que sigue, el tiempo y el nombre del disco.
//
//  La solucion no es un apano: es hacer aqui lo que en Windows hace el sistema. Antes de pintar su texto,
//  la etiqueta le pide a su ventana que pinte EL TROZO DE FONDO que le toca, corriendo el dibujo para que
//  las estrellas caigan donde deben. El resultado es el mismo cielo continuo, sin costuras.
// ====================================================================================================
using System;
using System.Windows.Forms;

namespace Rocola
{
    // La ventana que sabe pintar su propio fondo se ofrece a pintarselo a sus hijos.
    internal interface IFondoPintable
    {
        void PintaFondoEn(PaintEventArgs e);
    }

    internal class EtiquetaClara : Label
    {
        public EtiquetaClara()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = System.Drawing.Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Se busca hacia arriba la primera ventana que sepa pintar su fondo. Si no hay ninguna -no
            // deberia pasar-, se deja el comportamiento de siempre en vez de dibujar cualquier cosa.
            Control c = Parent;
            int x = Left, y = Top;
            while (c != null && !(c is IFondoPintable)) { x += c.Left; y += c.Top; c = c.Parent; }
            IFondoPintable f = c as IFondoPintable;
            if (f == null) { base.OnPaintBackground(e); return; }
            System.Drawing.Drawing2D.GraphicsState st = e.Graphics.Save();
            try { e.Graphics.TranslateTransform(-x, -y); f.PintaFondoEn(e); }
            catch { }
            finally { e.Graphics.Restore(st); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);   // con UserPaint, el fondo no se pinta solo: se pinta aqui
            base.OnPaint(e);
        }
    }
}

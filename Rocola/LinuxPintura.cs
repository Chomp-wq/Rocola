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
    // La ventana que sabe pintar su cielo se ofrece a pintarle a un hijo EL TROZO que le toca. Se le dice
    // en que punto del cielo empieza ese trozo y de que tamano es; el campo de estrellas ya sabe recortar
    // asi -es lo mismo que hacen la pared de discos y la barra de letras, que tambien son ventanas al
    // MISMO cielo de pantalla completa-, y por eso las estrellas caen donde deben y no hay costura.
    internal interface IFondoPintable
    {
        void PintaCieloEn(System.Drawing.Graphics g, int offX, int offY, int an, int al);
    }

    // EL AYUDANTE: pinta detras de un control el trozo de cielo que le toca. Lo usan tanto las etiquetas
    // como los controles propios que se apoyaban en la transparencia (la barra de ayuda, el letrero de la
    // seccion...). No cuesta caro: el cielo de la ventana ya esta horneado en un mapa de bits, asi que esto
    // es copiar pixeles y no volver a calcularlo.
    internal static class Fondo
    {
        public static void PintaDetras(Control c, PaintEventArgs e)
        {
            if (c == null || e == null || c.Width < 1 || c.Height < 1) return;
            Control p = c.Parent;
            int x = c.Left, y = c.Top;
            while (p != null && !(p is IFondoPintable)) { x += p.Left; y += p.Top; p = p.Parent; }
            IFondoPintable f = p as IFondoPintable;
            if (f == null) return;
            try { f.PintaCieloEn(e.Graphics, x, y, c.Width, c.Height); } catch { }
        }
    }

    internal class EtiquetaClara : Label
    {
        public EtiquetaClara()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = System.Drawing.Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { Fondo.PintaDetras(this, e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);   // con UserPaint, el fondo no se pinta solo: se pinta aqui
            base.OnPaint(e);
        }
    }
}

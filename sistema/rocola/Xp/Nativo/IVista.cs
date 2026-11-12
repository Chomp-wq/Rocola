// LAS VISTAS DE LA ROCOLA EN LA PANTALLA NATIVA (port nativo, tanda 4, grupo pantalla-nativa: W2). Lo que en XP eran los controles de
// WinForms que la rocola pone en su ventana (BuscaView, SongListView, AttractView, el panel del disco...), sin imitar a Control: solo lo que
// el codigo de XP usa de ellos fuera de su clase (el inventario de las ventanas, /root/rocola-trabajo/port/inventario-t4.json, W2 y W3-0:
// Bounds, Visible, Left, Top, Width, Height, SetBounds e Invalidate) mas lo que la pantalla les pide: que se pinten (OnPaint) y el raton
// (OnMouseDown, OnMouseClick...).
//
//   IVista       la vista: lo que XP usa de ella y lo que la pantalla le pide. Mientras dure la transicion, una vista de XP puede seguir
//                heredando de Control de la capa (la oleada 3 les pone «, IVista» en la cabecera sin quitar la herencia): Bounds, Visible,
//                Left, Top, Width, Height, SetBounds e Invalidate los da Control con su misma firma, y Pinta y Raton son de fabrica (Pinta
//                la pinta la capa: PantallaRocola.PintaVista; el raton se lo sigue dando la capa a sus On*). Una vista que ya no hereda
//                (oleadas 5 y 6) los implementa con su CajaVista y sobrescribe Pinta y Raton.
//   AvisoRaton   que le paso al raton (WM_LBUTTONDOWN, WM_MOUSEMOVE...): los mismos nombres y numeros que escriben las trazas de la capa
//                (TrazaVentanas.EventoRaton: baja, clic, mueve, entra, sale, doble, rueda, arriba).
//   (panel-del-disco) Una vista puede CONTENER a otras (el panel del disco: BgPanel con la lista, el cubo y la barra de paginas): lo que
//                XP hacia con Panel.Controls.Add es PantallaRocola.AgregaVista(panel, hija), con las reglas de un Panel de WinForms: la hija
//                esta en las coordenadas de su dueno, se recorta a el, se pinta despues de el y las hijas entre si de abajo arriba
//                (la primera que se agrega, la de mas arriba del orden z), y el raton va a la hija de mas arriba bajo la flecha.
//   CajaVista    lo comun de una vista que ya no hereda (no es una clase base: cada vista tiene la suya, como CajaPieza en las hojas): su
//                sitio y si se ve; avisa a la pantalla de cada cambio (PantallaRocola.VistaCambio).
//
// Las reglas que mueven las vistas (el orden de composicion, el recorte por los hermanos de encima, el reparto del raton) estan en
// PantallaRocola.Vistas.cs, cada una con su sitio en la fuente de WinForms (dotnet/winforms release/3.0, portada de .NET Framework 4.8).
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SkiaSharp;

// (pruebas) la prueba de la vista falsa junto a un control de la capa (Pruebas/Ventanas/PantallaVistas) usa la pantalla por dentro
[assembly: InternalsVisibleTo("PruebasVistas")]

namespace Rocola
{
    // QUE LE PASO AL RATON EN UNA VISTA (los avisos On* de Control que la rocola de XP escucha en sus vistas)
    public enum AvisoRaton { Abajo = 0, Clic = 1, Mueve = 2, Entra = 3, Sale = 4, DobleClic = 5, Rueda = 6, Arriba = 7 }

    public interface IVista
    {
        // EL SITIO, EN EL CLIENTE DE SU DUENO (Control.Bounds: Control.cs, Bounds/SetBounds; cambiarlo vuelve a componer)
        Rectangle Bounds { get; set; }
        int Left { get; set; }
        int Top { get; set; }
        int Width { get; set; }
        int Height { get; set; }
        void SetBounds(int x, int y, int width, int height);
        // SI SE VE: la suya y la de su dueno (Control.Visible: GetVisibleCore, «GetState(STATE_VISIBLE) && (ParentInternal == null ||
        // ParentInternal.GetVisibleCore())»)
        bool Visible { get; set; }
        // HAY QUE PINTARLA OTRA VEZ (Control.Invalidate: el WM_PAINT llega despues, cuando no hay encargos ni entrada)
        void Invalidate();
        // PINTARSE (OnPaintBackground y OnPaint) en g, con el origen en su esquina y recortada a lo suyo que se ve. De fabrica (una vista
        // que aun es Control de la capa): la pinta la capa.
        void Pinta(SKCanvas g) { PantallaRocola.PintaVistaDeLaCapa(this, g); }
        // EL RATON (los On* del raton de Control, en el orden de WinForms: WmMouseDown, WmMouseUp -Click, MouseClick o DoubleClick,
        // MouseDoubleClick, y despues MouseUp: Control.cs:12673-12743-, WmMouseMove, WmMouseEnter/Leave y WmMouseWheel). e en las
        // coordenadas de la vista; en la rueda, e.Handled = true para que no siga al dueno. De fabrica, nada (Control.OnMouse*: solo el aviso).
        void Raton(AvisoRaton que, Raton e) { }
        // SI TOMA EL FOCO DEL TECLADO AL HACERLE CLIC (ControlStyles.Selectable: Control lo trae encendido, Panel y Label lo apagan). Solo lo
        // mira el hueco de la capa de la transicion (Plataforma/Anfitrion.cs); la pantalla nativa no da el foco del teclado a las vistas.
        bool Seleccionable { get { return true; } }
    }

    // LO COMUN DE UNA VISTA QUE YA NO HEREDA DE CONTROL: su sitio, si se ve y si hay que pintarla. Cada cambio avisa a la pantalla, que
    // vuelve a componer (sitio y visible) o a pintar (Invalidate); como en WinForms, SetBounds con el mismo sitio no avisa (Control.SetBounds:
    // «if (x != this.x || ...) SetBoundsCore»), y Visible con el mismo valor tampoco (SetVisibleCore compara con GetVisibleCore).
    public sealed class CajaVista
    {
        private readonly IVista duena;
        private Rectangle bounds;
        private bool visible = true;
        // Control.ResizeRedraw (SetBoundsCore: «if (GetStyle(ControlStyles.ResizeRedraw)) Invalidate()»): una vista cuya clase de XP no lo pone
        // (el panel del disco: BgPanel) no se pinta entera al cambiar de medida, y una que lo pone si (las demas, de fabrica aqui)
        public bool RepintaConMedida = true;
        public CajaVista(IVista duena) { this.duena = duena; }
        public Rectangle Bounds { get { return bounds; } set { SetBounds(value.X, value.Y, value.Width, value.Height); } }
        public int Left { get { return bounds.X; } set { SetBounds(value, bounds.Y, bounds.Width, bounds.Height); } }
        public int Top { get { return bounds.Y; } set { SetBounds(bounds.X, value, bounds.Width, bounds.Height); } }
        public int Width { get { return bounds.Width; } set { SetBounds(bounds.X, bounds.Y, value, bounds.Height); } }
        public int Height { get { return bounds.Height; } set { SetBounds(bounds.X, bounds.Y, bounds.Width, value); } }
        public void SetBounds(int x, int y, int width, int height)
        {
            Rectangle r = new Rectangle(x, y, Math.Max(0, width), Math.Max(0, height));
            if (r == bounds) return;
            bool otraMedida = r.Size != bounds.Size;
            bounds = r;
            PantallaRocola.VistaCambio(duena, otraMedida && RepintaConMedida);
        }
        // la suya y la de su dueno (GetVisibleCore)
        public bool Visible { get { return visible && PantallaRocola.DuenoVisible(duena); } set { if (visible == value) return; visible = value; PantallaRocola.VistaCambio(duena, false); } }
        internal bool VisiblePropio { get { return visible; } }
        public void Invalidate() { PantallaRocola.VistaCambio(duena, true); }
    }
}

// LA HOJA: LAS TECLAS Y EL RATON (port nativo de las ventanas de XP, ventanas2, paso H; ver Hoja.cs). Lo que WinForms hace con cada tecla y
// cada aviso del raton en una ventana de la rocola, en el orden de la fuente (Control.PreProcessMessage, ProcessKeyMessage, WmMouseDown,
// WmMouseUp, WmMouseWheel; Form.ProcessDialogKey; ContainerControl.ProcessDialogKey), portado de la capa (Plataforma/Ventanas.Escritorio.cs:
// Tecla, RatonEn, RatonBoton, RatonRueda), revisada contra ella. Cada paso deja su linea en la traza de las ventanas (TrazaVentanas, las
// mismas que la capa), para compararla con la referencia.
//   (transicion) Las teclas y el raton los lee la capa (Escritorio, desde TeclasLinux) y, si la ventana activa es una hoja, o el raton esta
//   sobre una hoja, o la hoja lo tiene capturado, se los pasa aqui: la capa lleva aun los modificadores (Mayus, Ctrl, Alt) y lo ya atendido
//   (PantallaRocola.AnotaTecla, GetKeyState). Se van con la capa: entonces la pantalla se las dara directo.
using System;
using System.Drawing;

namespace Rocola
{
    public partial class Hoja
    {
        // ---------- las teclas ----------
        // UNA TECLA PARA LA HOJA ACTIVA: va a la pieza con el foco (o a la hoja). Abajo: ProcessCmdKey (de una hoja sin menus ni
        // aceleradores, false: Control.cs:9440-9454, ContainerControl.cs:1190-1210, Form.cs:4862-4900), ProcessDialogKey si la pieza no la
        // quiere (IsInputKey: Control.PreProcessMessage, Control.cs:9275-9321), KeyDown primero en la hoja con KeyPreview y, si no la atendio
        // (Handled), en la pieza (ProcessKeyMessage y ProcessKeyPreview, Control.cs:9705-9714 y 9734-9738, Form.cs:4985-4993); el caracter (WM_CHAR) igual,
        // salvo con SuppressKeyPress (KeyEventArgs.cs); Alt+F4 sin atender cierra la hoja (DefWindowProc de WM_SYSKEYDOWN: SC_CLOSE, por la
        // cola). Arriba: KeyUp, primero en la hoja con KeyPreview.
        internal static void Tecla(Tecla tecla, bool abajo, Tecla modificadores, bool repetida)
        {
            object destino = foco != null && !Desechada(foco) ? foco : Activa;
            if (destino == null) return;
            Hoja f = HojaDe(destino);
            if (f == null) return;
            Tecla datos = tecla | modificadores;
            bool previa = f.vistaPrevia && f != destino;
            if (abajo)
            {
                bool alt = (modificadores & Rocola.Tecla.Alt) != 0;
                PulsoTecla m = new PulsoTecla(datos, repetida);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("tecla " + TrazaVentanas.Datos((int)datos) + (repetida ? " rep" : "") + " a " + TrazaVentanas.Pieza(destino));
                bool pck = f.TeclaDeMando(ref m, datos);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("pck " + (pck ? "1" : "0"));
                if (pck) return;
                if (!QuiereTecla(destino, datos) && f.ProcesaTeclaDeDialogo(datos)) return;
                bool atendida = false, suprime = false;
                if (previa)
                {
                    PulsoTecla ef = new PulsoTecla(datos);
                    Action<PulsoTecla> h = f.teclaAntes; if (h != null) h(ef);
                    atendida = ef.Handled; suprime = ef.SuppressKeyPress;
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("keydown " + TrazaVentanas.Datos((int)datos) + " a " + TrazaVentanas.Pieza(f) + (atendida ? " h" : "") + (suprime ? " s" : ""));
                }
                if (!atendida && !Desechada(destino))
                {
                    PulsoTecla e = new PulsoTecla(datos);
                    TeclaAbajoDe(destino, e); atendida = e.Handled; suprime |= e.SuppressKeyPress;
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("keydown " + TrazaVentanas.Datos((int)datos) + " a " + TrazaVentanas.Pieza(destino) + (e.Handled ? " h" : "") + (e.SuppressKeyPress ? " s" : ""));
                }
                if (!atendida && alt && tecla == Rocola.Tecla.F4 && (modificadores & Rocola.Tecla.Control) == 0)
                {
                    Hoja raiz = f;
                    if (!raiz.disposed) PantallaRocola.EncolaDesde(raiz, delegate { if (!raiz.disposed && raiz.vida != 0) raiz.CierraPorSistema(); }, "Alt+F4", false);
                }
                if (!suprime)
                {
                    char ch = PantallaRocola.CaracterDe(tecla, modificadores);
                    if (ch != '\0' && !Desechada(destino))
                    {
                        PulsoCaracter kp = new PulsoCaracter(ch);
                        if (previa)
                        {
                            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("caracter " + ((int)ch).ToString(System.Globalization.CultureInfo.InvariantCulture) + " a " + TrazaVentanas.Pieza(f));
                            if (kp.Handled) return;   // (KeyPress de la hoja: ninguna hoja de XP lo escucha)
                        }
                        if (TrazaVentanas.Activa) TrazaVentanas.Apunta("caracter " + ((int)ch).ToString(System.Globalization.CultureInfo.InvariantCulture) + " a " + TrazaVentanas.Pieza(destino));
                        IPieza pd = destino as IPieza; if (pd != null) pd.Caracter(kp);
                    }
                }
            }
            else
            {
                PulsoTecla e = new PulsoTecla(datos);
                if (previa)
                {
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("keyup " + TrazaVentanas.Datos((int)datos) + " a " + TrazaVentanas.Pieza(f));
                    Action<PulsoTecla> h = f.teclaArribaAntes; if (h != null) h(e);
                    if (e.Handled) return;
                }
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("keyup " + TrazaVentanas.Datos((int)datos) + " a " + TrazaVentanas.Pieza(destino));
                TeclaArribaDe(destino, e);
            }
        }
        // ProcessCmdKey de la hoja: una hoja de XP no tiene menus ni aceleradores (Form.cs:4862-4900); la de prueba F12 tendra el suyo
        private bool TeclaDeMando(ref PulsoTecla m, Tecla datos) { return false; }
        private static bool QuiereTecla(object destino, Tecla k) { IPieza p = destino as IPieza; return p != null && p.QuiereTecla(k); }
        // el KeyDown y el KeyUp de la pieza; el de la hoja misma (si el foco lo tiene ella) es su TeclaAntes y su TeclaArribaAntes
        private static void TeclaAbajoDe(object destino, PulsoTecla e)
        {
            IPieza p = destino as IPieza; if (p != null) { p.TeclaAbajo(e); return; }
            Hoja h = destino as Hoja; if (h != null) { Action<PulsoTecla> a = h.teclaAntes; if (a != null) a(e); }
        }
        private static void TeclaArribaDe(object destino, PulsoTecla e)
        {
            IPieza p = destino as IPieza; if (p != null) { p.TeclaArriba(e); return; }
            Hoja h = destino as Hoja; if (h != null) { Action<PulsoTecla> a = h.teclaArribaAntes; if (a != null) a(e); }
        }
        // LAS TECLAS DE DIALOGO (ProcessDialogKey: de la pieza sube a su padre hasta la hoja, Control.cs:9591-9595): Intro pulsa el boton por
        // defecto y Escape el de cancelar (Form.ProcessDialogKey, Form.cs:4908-4948); Tab y las flechas mueven el foco
        // (ContainerControl.ProcessDialogKey, ContainerControl.cs:1156-1188)
        private bool ProcesaTeclaDeDialogo(Tecla keyData)
        {
            if ((keyData & (Rocola.Tecla.Alt | Rocola.Tecla.Control)) == Rocola.Tecla.None)
            {
                Tecla k = keyData & Rocola.Tecla.KeyCode;
                if (k == Rocola.Tecla.Enter && porDefecto != null) { porDefecto.PerformClick(); return true; }
                if (k == Rocola.Tecla.Escape && BotonCancelar != null) { BotonCancelar.PerformClick(); return true; }
                if (k == Rocola.Tecla.Tab) { if (ProcessTabKey((keyData & Rocola.Tecla.Shift) == Rocola.Tecla.None)) return true; }
                else if (k == Rocola.Tecla.Left || k == Rocola.Tecla.Right || k == Rocola.Tecla.Up || k == Rocola.Tecla.Down)
                { if (ProcessArrowKey(k == Rocola.Tecla.Right || k == Rocola.Tecla.Down)) return true; }
            }
            return false;
        }

        // ---------- el raton (lo que en XP manda Windows: WM_MOUSEMOVE, WM_LBUTTONDOWN, WM_MOUSEWHEEL...) ----------
        // Como Windows y WinForms: la pieza de encima de todo bajo la flecha recibe el movimiento (y MouseEnter / MouseLeave al cambiar); la que
        // recibe el boton bajado se queda con el raton hasta soltarlo (la captura de Windows); al soltar encima, Click y MouseClick (o DoubleClick
        // y MouseDoubleClick si es el segundo en 500 ms y a menos de 4 puntos: el doble clic de XP, GetDoubleClickTime y SM_CXDOUBLECLK), y
        // despues MouseUp. La rueda va a la pieza con el foco, de 120 en 120.
        private static object bajoRaton, captura;
        private static double ultimoClicMs = -1e9; private static Point ultimoClicEn; private static BotonRaton ultimoClicBoton; private static object ultimoClicControl;
        private static bool dobleEnCurso;
        internal const double DobleClicMs = 500; internal const int DobleClicPuntos = 4;
        private static Hoja ncHoja; private static int ncCaptura; private static Point ncInicio, ncOrigen;
        private static Hoja cerrarEncima;

        // (transicion) el raton es de las hojas: una lo tiene capturado, hay una modal (las de la capa, debajo, no lo reciben) o esta encima
        internal static bool TomaElRaton(Point p)
        {
            if (captura != null && !Desechada(captura)) return true;
            if (ncHoja != null && ncCaptura != 0) return true;
            if (HayModal) return true;
            for (int i = abiertas.Count - 1; i >= 0; i--) { Hoja f = abiertas[i]; if (!f.disposed && f.visible && f.bounds.Contains(p)) return true; }
            return false;
        }
        // (transicion) el raton se fue a una ventana de la capa
        internal static void RatonFuera() { MarcaEncima(null); Encima(null); }
        // LA PIEZA DE ENCIMA DE TODO EN UN PUNTO DE LA PANTALLA (WindowFromPoint; ChildWindowFromPoint dentro): las hojas de arriba abajo; dentro,
        // las piezas de la primera a la ultima. Con una hoja modal abierta, las de debajo estan deshabilitadas: ahi no hay nadie.
        internal static object PiezaEn(Point p)
        {
            Hoja modal = ModalArriba;
            for (int i = abiertas.Count - 1; i >= 0; i--)
            {
                Hoja f = abiertas[i];
                if (f.disposed || !f.visible || !f.bounds.Contains(p)) { if (f == modal) return null; continue; }
                return HijaEn(f, p);
            }
            return null;
        }
        private static object HijaEn(object c, Point p)
        {
            Piezas hs = HijasDe(c);
            if (hs == null) return c;
            for (int i = 0; i < hs.Lista.Count; i++)
            {
                IPieza h = hs.Lista[i];
                if (!h.Caja.VisiblePropio || h.Caja.IsDisposed) continue;
                Point o = OrigenEnPantalla(h);
                if (!new Rectangle(o, h.Caja.Bounds.Size).Contains(p)) continue;
                return h.Caja.Enabled ? HijaEn(h, p) : c;   // (una apagada no recibe el raton: lo recibe la de debajo, su padre)
            }
            return c;
        }
        // donde empieza en la pantalla lo de dentro de una hoja o de una pieza (PointToScreen)
        private static Point OrigenEnPantalla(object n)
        {
            Hoja h = n as Hoja;
            if (h != null) return new Point(h.bounds.X + Marco, h.bounds.Y + Marco + AltoBarra);
            IPieza p = (IPieza)n;
            Point o = OrigenEnPantalla(p.Caja.Padre);
            return new Point(o.X + p.Caja.Bounds.X, o.Y + p.Caja.Bounds.Y);
        }
        // EL MARCO DE LA HOJA (lo que en Windows es el area de fuera del cliente, WM_NCHITTEST): 0 dentro del cliente, 1 la barra de titulo,
        // 2 la «X» de cerrar, 3 el borde. Ahi no llegan los avisos del raton de las piezas (WM_NCLBUTTONDOWN...).
        internal static int ZonaMarco(Hoja f, Point p)
        {
            Point o = f.Location;
            if (new Rectangle(new Point(o.X + Marco, o.Y + Marco + AltoBarra), f.ClientSize).Contains(p)) return 0;
            Rectangle x = f.CajaCerrar; x.Offset(o);
            if (x.Contains(p)) return 2;
            if (p.Y < o.Y + AltoBarra + Marco) return 1;
            return 3;
        }
        private static Raton Args(object c, BotonRaton b, int clics, int delta)
        {
            Point o = OrigenEnPantalla(c), p = PantallaRocola.Flecha;
            return new Raton(b, clics, p.X - o.X, p.Y - o.Y, delta);
        }
        // un aviso del raton a una pieza (lo que lance va a ThreadException, como desde la ventana en WinForms)
        private static void Llama(object c, int tipo, Raton e)
        {
            if (c == null || Desechada(c)) return;
            if (TrazaVentanas.Activa)
                TrazaVentanas.Apunta("raton " + TrazaVentanas.EventoRaton[tipo] + " " + TrazaVentanas.Pieza(c) + (e == null ? "" : " " + e.X + "," + e.Y + " " + TrazaVentanas.Datos((int)e.Button) + " " + e.Clicks + " " + e.Delta));
            IPieza p = c as IPieza;
            if (p == null) return;   // (la hoja misma: ninguna hoja de XP escucha su raton)
            try
            {
                switch (tipo)
                {
                    case 0: p.RatonAbajo(e); break;
                    case 1: p.Clic(e); p.RatonArriba(e); break;                                   // soltar encima (Click antes que MouseUp)
                    case 2: p.RatonMueve(e); break;
                    case 3: p.RatonEntra(); break;
                    case 4: p.RatonSale(); break;
                    case 5: p.DobleClic(new Raton(e.Button, 2, e.X, e.Y, 0)); p.RatonArriba(e); break;   // el segundo clic (los avisos del doble, con 2)
                    case 6: p.Rueda(e); break;
                    case 7: p.RatonArriba(e); break;                                              // soltar fuera: sin clic
                }
            }
            catch (Exception ex) { PantallaRocola.Falla(ex); }
        }
        private static void Encima(object c)
        {
            if (c == bajoRaton) return;
            object antes = bajoRaton; bajoRaton = c;
            if (antes != null && !Desechada(antes)) Llama(antes, 4, null);
            if (c != null) Llama(c, 3, null);
        }
        private static void MarcaEncima(Hoja f)
        {
            if (cerrarEncima == f) return;
            if (cerrarEncima != null && !cerrarEncima.disposed) cerrarEncima.PonEstadoCerrar(0);
            cerrarEncima = f;
            if (f != null) f.PonEstadoCerrar(1);
        }
        // el raton se movio a 'p' (la capa ya lo puso en PantallaRocola.Flecha); 'botones' son los que estan abajo
        internal static void RatonEn(Point p, BotonRaton botones)
        {
            if (ncHoja != null && ncCaptura != 0)
            {   // arrastrando una hoja por su barra de titulo (con su contenido, como XP), o con la «X» apretada
                if (ncCaptura == 1) ncHoja.Mueve(new Point(ncOrigen.X + p.X - ncInicio.X, ncOrigen.Y + p.Y - ncInicio.Y));
                else ncHoja.PonEstadoCerrar(ZonaMarco(ncHoja, p) == 2 ? 2 : 0);
                return;
            }
            if (captura != null && !Desechada(captura)) { Llama(captura, 2, Args(captura, botones, 0, 0)); return; }
            object c = PiezaEn(p);
            Hoja fm = c as Hoja;
            int zona = fm != null ? ZonaMarco(fm, p) : 0;
            MarcaEncima(zona == 2 ? fm : null);
            if (zona != 0) { Encima(null); return; }   // en el marco no hay avisos de cliente
            Encima(c);
            if (bajoRaton != null) Llama(bajoRaton, 2, Args(bajoRaton, botones, 0, 0));
        }
        internal static void RatonBoton(BotonRaton b, bool abajo, Point p)
        {
            if (abajo)
            {
                object c = PiezaEn(p);
                Hoja f = c == null ? null : HojaDe(c);
                if (f != null && f != Activa) { AlFrente(f); ActivaHoja(f, true); }
                Hoja fm = c as Hoja;
                int zona = fm != null ? ZonaMarco(fm, p) : 0;
                if (zona != 0)
                {   // en el marco: la «X» se aprieta (cierra al soltar encima) y la barra de titulo arrastra la hoja
                    Encima(null);
                    if (b != BotonRaton.Left) return;
                    ncHoja = fm; ncInicio = p; ncOrigen = fm.Location;
                    ncCaptura = zona == 2 ? 2 : zona == 1 ? 1 : 0;
                    if (ncCaptura == 2) fm.PonEstadoCerrar(2);
                    return;
                }
                Encima(c);
                if (c == null) return;
                captura = c;
                double t = PantallaRocola.AhoraMs;
                dobleEnCurso = b == ultimoClicBoton && c == ultimoClicControl && t - ultimoClicMs <= DobleClicMs
                    && Math.Abs(p.X - ultimoClicEn.X) <= DobleClicPuntos / 2 && Math.Abs(p.Y - ultimoClicEn.Y) <= DobleClicPuntos / 2;
                if (dobleEnCurso) ultimoClicMs = -1e9;   // un tercer clic empieza otra cuenta
                else { ultimoClicMs = t; ultimoClicEn = p; ultimoClicBoton = b; ultimoClicControl = c; }
                Llama(c, 0, Args(c, b, dobleEnCurso ? 2 : 1, 0));
            }
            else
            {
                if (ncHoja != null && ncCaptura != 0)
                {
                    Hoja fm = ncHoja; int que = ncCaptura;
                    ncHoja = null; ncCaptura = 0;
                    if (que == 2)
                    {
                        bool encima = ZonaMarco(fm, p) == 2;
                        fm.PonEstadoCerrar(encima ? 1 : 0);
                        if (encima && !fm.disposed) fm.CierraPorSistema();   // (WM_SYSCOMMAND SC_CLOSE: cerrar con la «X»)
                    }
                    return;
                }
                object c = captura != null && !Desechada(captura) ? captura : PiezaEn(p);
                captura = null;
                if (c == null) return;
                bool doble = dobleEnCurso; dobleEnCurso = false;
                // WmMouseUp (Control.cs:12673-12743): el clic (StandardClick) solo si se solto encima de la misma pieza (WindowFromPoint: no
                // sobre una hija ni sobre otra de encima); el segundo de un doble clic es DoubleClick si la pieza tiene StandardDoubleClick, y si
                // no, otro Click. Despues MouseUp, de un clic.
                bool clic = ClicNormal(c) && PiezaEn(p) == c;
                Llama(c, !clic ? 7 : doble && DobleClicNormal(c) ? 5 : 1, Args(c, b, 1, 0));
                Encima(PiezaEn(p));
            }
        }
        private static bool ClicNormal(object c) { IPieza p = c as IPieza; return p == null || p.Caja.ClicNormal; }
        private static bool DobleClicNormal(object c) { IPieza p = c as IPieza; return p == null || p.Caja.DobleClicNormal; }
        // WM_MOUSEWHEEL va a la pieza con el foco (o a la hoja activa); si no la atiende (Handled), DefWindowProc la pasa a su padre, y asi
        // hacia arriba (Control.WmMouseWheel, Control.cs:12748-12764)
        internal static void RatonRueda(int muescas)
        {
            for (object c = foco != null && !Desechada(foco) ? foco : Activa; c != null && !Desechada(c); c = PadreDe(c))
            {
                Raton e = Args(c, BotonRaton.None, 0, muescas * 120);
                Llama(c, 6, e);
                if (e.Handled) break;
            }
        }
        // LA MANO: la flecha de la pieza de debajo (o de la capturada), o la de su padre (Control.Cursor, que se hereda)
        internal static bool ManoBajoRaton
        {
            get
            {
                for (object c = captura ?? bajoRaton; c != null; c = PadreDe(c)) { IPieza p = c as IPieza; if (p != null && p.Caja.Mano) return true; }
                return false;
            }
        }
        // al esconderse la hoja: lo que tenia del raton se olvida
        private void OlvidaRaton()
        {
            if (ncHoja == this) { ncHoja = null; ncCaptura = 0; }
            if (cerrarEncima == this) cerrarEncima = null;
        }
    }
}

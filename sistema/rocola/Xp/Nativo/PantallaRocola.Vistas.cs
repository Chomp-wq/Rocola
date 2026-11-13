// LA PANTALLA DE LA ROCOLA: LAS VISTAS, LA COMPOSICION Y LA ENTRADA (port nativo, tanda 4, grupo pantalla-nativa: W2). Lo que en XP hacian
// Windows y WinForms con los controles de la ventana de la rocola: el orden en que se ven (Controls, BringToFront, SendToBack), el recorte
// de cada uno por los de encima (WS_CLIPSIBLINGS), a quien le toca el raton (la ventana de encima bajo la flecha, la captura del boton, el
// clic y el doble clic) y a quien la tecla. Solo lo que la rocola de XP usa, con las reglas de WinForms (dotnet/winforms release/3.0,
// portada de .NET Framework 4.8: Control.cs y Control.ControlCollection.cs; /tmp/claude-0/agente-winforms/ref) y de Win32.
//
// LA LISTA DE LA VENTANA: lo que la rocola pone en su ventana, el de mas arriba primero (Controls[0], como WinForms): vistas (IVista,
// pintadas por procesador, o pintadas por la tarjeta: IVista.DeLaTarjeta, el GpuPanel de XP con su dispositivo de Direct3D 9) y capas de
// textura (CapaDeTextura: el video; en XP, el control del WMP). Es tambien LA LISTA DE LO QUE VA ENCIMA DEL PANEL de la tarjeta: el panel
// (gpupanel-sin-control) es una vista de la lista, y el video y los rotulos (lblAttract, lblConfig, lblGpu, el aviso lblToast...
// Nativo/RotuloVista.cs, rotulos-y-letras) son de la lista como cualquier vista; su orden frente al panel lo deciden los Controls.Add y
// BringToFront de XP (AgregaVista, VistaAlFrente), como alli. Una vista puede tener FORMA (IVista.Forma: la Region de WinForms,
// SetWindowRgn; el aviso sin tarjeta): se pinta solo dentro de ella, recorta a las de debajo solo con ella y el raton fuera de ella es del de
// debajo.
//
// TRANSICION (mientras queden las ventanas de la capa: Plataforma/Ventanas.*.cs). La rocola aun es una ventana de la capa y sus vistas
// aun son controles de la capa: la capa decide su orden, su pintura y su raton (lo que fijan las 41 pruebas), y la pantalla se lo pide por
// LaCapa (lo pone Plataforma/Anfitrion.cs). Una vista que ya no hereda de Control pero cuyo dueno es una ventana de la capa entra en la
// lista de controles de esa ventana con un hueco, asi su orden, su recorte y su raton son los de WinForms entre los controles que quedan; la
// capa la pinta llamando a su Pinta y le da el raton por su Raton. Lo mismo una capa de textura (el video: rotulos-y-letras; antes iba en
// HuecoDelVideo de Plataforma/VideoEnVentanas.cs, ya borrado): en una ventana de la capa, su hueco de textura. Lo que no es de
// ninguna ventana de la capa va a la lista nativa de la pantalla. Las teclas y el raton de evdev (TeclasLinux.cs) llegan aqui y, con la
// capa, se los da a ella (que ya da a las hojas nativas lo suyo); sin la capa, la pantalla los reparte con las reglas de abajo.
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    // UNA CAPA DE TEXTURA EN LA LISTA DE LA VENTANA (el video): su sitio en la ventana, si se ve y la textura (el fondo de la tarjeta es de la
    // superficie de su vista: PantallaRocola.SuperficieDe)
    internal sealed class CapaDeTextura
    {
        internal Rectangle Sitio;
        internal bool Visible = true;
        internal uint Textura; internal int AnchoGl, AltoGl; internal bool Rgba;
        internal string Nombre = "CapaDeTextura";   // (pruebas: ROCOLA_TRAZA_CAPAS)
    }

    internal static partial class PantallaRocola
    {
        // ---------- (transicion) lo que la pantalla le pide a las ventanas de la capa ----------
        internal interface ILaCapa
        {
            bool EsSuya(object ventana);                             // una ventana (o control) de la capa
            bool EsSuyaLaVista(IVista v);                            // la vista aun es un control de la capa, o esta en un hueco de una
            void Agrega(object ventana, IVista v);                   // Controls.Add: al fondo de la lista de esa ventana (un hueco si ya no hereda)
            void AlFrente(IVista v);                                 // BringToFront
            void AlFondo(IVista v);                                  // SendToBack
            void Quita(IVista v);                                    // Controls.Remove
            void Cambio(IVista v, bool repintar);                    // una vista en un hueco cambio de sitio o de visible, o hay que repintarla
            bool ClienteDelDueno(IVista v, out Size cliente);        // Parent.ClientSize de una vista que es control de la capa o esta en un hueco (false: sin dueno)
            bool DuenoVisible(IVista v);                             // si se ve la ventana (o el control) en que esta
            void Pinta(IVista v, SKCanvas g);                        // pintar una vista que aun es control de la capa (OnPaintBackground y OnPaint)
            bool Enfoca(IVista v);                                   // Focus de una vista que es control de la capa o esta en un hueco (Control.Focus)
            bool OyeComoControl(IVista v, AvisoRaton que, Action<Raton> a);   // una vista que aun es control: su evento (MouseDown, MouseClick); false si no lo es
            bool EsSuyaLaCapa(CapaDeTextura t);                      // (rotulos-y-letras) la capa de textura esta en un hueco de textura de una ventana de la capa
            void AgregaCapa(object ventana, CapaDeTextura t);        // Controls.Add de una capa de textura (el video) a una ventana de la capa: su hueco de textura, al fondo
            void CapaAlFrente(CapaDeTextura t);                      // BringToFront de su hueco
            void QuitaCapa(CapaDeTextura t);                         // Controls.Remove de su hueco
            void CambioCapa(CapaDeTextura t);                        // su sitio, si se ve o su textura cambiaron: su hueco los toma
            bool Presenta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera);   // Present de la tarjeta en una hoja (la ventana de prueba F12; false: no es de nadie, glFlush)
            void OlvidaTarjeta(IntPtr ventana);
            bool PresentaEnVista(IVista v, uint textura, int ancho, int alto, bool sinEspera);   // (gpupanel-sin-control) Present en la superficie de una vista de la tarjeta que esta en un hueco de la capa
            void OlvidaEnVista(IVista v);                            // (gpupanel-sin-control) su dispositivo se suelta: su hueco ya no tiene fondo que componer
            bool VentanaMinimizada(IVista v);                        // (gpupanel-sin-control) FindForm() de la vista (su hueco) y su WindowState == Minimized
            void HayQueComponer();                                   // algo cambio: volver a componer
            void Tecla(Tecla tecla, bool abajo);                     // una tecla (Escritorio.Tecla: el foco y el orden de WinForms)
            void RatonEn(int x, int y);
            void RatonBoton(BotonRaton b, bool abajo);
            void RatonRueda(int muescas);
            void FinDeSesion();                                      // el apagado (WM_QUERYENDSESSION y WM_ENDSESSION a sus ventanas)
        }
        internal static ILaCapa LaCapa;

        // ---------- la pantalla ----------
        // Las veces por segundo que refresca (GetDeviceCaps(VREFRESH) en XP): la del modo de KMS, o 60 fuera de pantalla. La pone quien
        // abre la pantalla.
        internal static int RefrescoHz = 60;

        // ---------- la lista de la ventana ----------
        private static readonly List<object> lista = new List<object>();   // IVista o CapaDeTextura; [0] el de mas arriba
        private static int cambiosVentana, pintadoVentana = -1;
        // Controls.Add: al FINAL de la coleccion, que es el FONDO del orden (Control.ControlCollection.Add: «base.InnerList.Add(value)» y el
        // orden z es el de la coleccion, el [0] arriba: ControlCollection.SetChildIndex, UpdateZOrder). En una ventana de la capa, la capa.
        internal static void AgregaVista(object ventana, IVista v)
        {
            ventana = VentanaDe(ventana);   // (form1-sin-form) la rocola: su ventana (de la capa mientras dure; PantallaRocola.Teclado.cs)
            ILaCapa c = LaCapa;
            IVista dueno = ventana as IVista;
            if (dueno != null && (c == null || !c.EsSuya(ventana)))
            {
                // EL DUENO ES UNA VISTA (el panel del disco: Panel.Controls.Add): si ya esta en una ventana de la capa, su hueco la contiene; si no
                // (en la pantalla nativa, o aun sin poner en ninguna ventana: XP agrega el cubo, la lista y la barra de paginas al panel ANTES de
                // agregar el panel a la ventana), la pantalla lleva la cuenta de sus hijas
                if (c != null && c.EsSuyaLaVista(dueno)) { c.Agrega(ventana, v); TraspasaHijas(v); return; }
                AgregaHija(dueno, v); return;
            }
            if (c != null && (c.EsSuya(ventana) || c.EsSuyaLaVista(v))) { c.Agrega(ventana, v); TraspasaHijas(v); return; }
            // (gpupanel-sin-control) Add de una que ya esta en la ventana: la manda al fondo (ControlCollection.Add: «if (value._parent == Owner) {
            // value.SendToBack(); return; }», Control.ControlCollection.cs:67-71; la capa hace igual, Ventanas.Control.cs)
            if (lista.Contains(v)) { VistaAlFondo(v); return; }
            IVista antes = PadreDe(v);   // (gpupanel-sin-control) la que estaba dentro de otra vista sale de alli antes (ControlCollection.Add: «if (value._parent != null) value._parent.Controls.Remove(value)», Control.ControlCollection.cs:73-77)
            if (antes != null) { bool sev = EnLaLista(antes); SueltaDeLaRama(v); hijas[antes].Remove(v); padre.Remove(v); if (sev) CambioVentana(); }
            lista.Add(v); AnotaTab(v, lista); CambioVentana();   // (vistas-buscador) su TabIndex, como Controls.Add (PantallaRocola.Foco.cs)
            if (v.DeLaTarjeta) TarjetaCambio(v, false);   // (gpupanel-sin-control) la ventana nueva que se ve recibe su WM_PAINT
        }
        // LAS VISTAS DENTRO DE UNA VISTA (panel-del-disco). Control.ControlCollection.Add («base.InnerList.Add(value)» al final: el fondo del orden
        // z, [0] arriba) en el Controls de un Panel; Add de una que ya tenia otro dueno la saca antes del viejo (ControlCollection.Add:
        // «value.parent.Controls.Remove(value)»). Los hijos de cada dueno, [0] el de mas arriba (IVista: el rotulo del disco, lblAlbum, tambien)
        private static readonly Dictionary<IVista, List<object>> hijas = new Dictionary<IVista, List<object>>();
        private static readonly Dictionary<IVista, IVista> padre = new Dictionary<IVista, IVista>();
        private static IVista PadreDe(IVista v) { IVista p; return v != null && padre.TryGetValue(v, out p) ? p : null; }
        private static void AgregaHija(IVista dueno, IVista v)
        {
            if (v == null || v == dueno) return;
            IVista antes = PadreDe(v);
            if (antes == dueno) { VistaAlFondo(v); return; }   // (gpupanel-sin-control: Add de una que ya esta en el: al fondo, ControlCollection.Add)
            if (antes != null) { hijas[antes].Remove(v); padre.Remove(v); }
            lista.Remove(v);
            List<object> l; if (!hijas.TryGetValue(dueno, out l)) { l = new List<object>(); hijas[dueno] = l; }
            l.Add(v); padre[v] = dueno; AnotaTab(v, l); if (EnLaLista(dueno)) CambioVentana();
        }
        // una vista con hijas anotadas entra en una ventana de la capa: sus hijas pasan a su hueco, en el mismo orden (Controls.Add una por una)
        private static void TraspasaHijas(IVista v)
        {
            ILaCapa c = LaCapa; List<object> l;
            if (c == null || v == null || !hijas.TryGetValue(v, out l) || !c.EsSuyaLaVista(v)) return;
            hijas.Remove(v);
            foreach (object o in l)
            {
                IVista h = o as IVista;
                if (h != null) { padre.Remove(h); AgregaVista(v, h); }
            }
        }
        // si la vista, o la de mas arriba de su cadena de duenos, esta en la lista nativa de la ventana
        private static bool EnLaLista(IVista v) { for (IVista a = v; a != null; a = PadreDe(a)) if (lista.Contains(a)) return true; return false; }
        // si 'o' es 'raiz' o esta dentro de ella
        private static bool EnLaRama(object o, object raiz) { for (IVista a = o as IVista; a != null; a = PadreDe(a)) if (a == raiz) return true; return o == raiz; }
        // donde esta la vista en la ventana (su esquina) sumando las de sus duenos
        internal static Point OrigenDe(IVista v)
        {
            int x = 0, y = 0;
            for (IVista a = v; a != null; a = PadreDe(a)) { Rectangle r = a.Bounds; x += r.X; y += r.Y; }
            return new Point(x, y);
        }
        // el orden de una dentro de su dueno (0 arriba), o -1 si no es hija de ninguno de la pantalla nativa
        internal static int IndiceEnElDueno(IVista v) { IVista p = PadreDe(v); return p == null ? -1 : hijas[p].IndexOf(v); }
        // Parent.ClientSize (el tamano del cliente de su dueno, que en el panel y la ventana de XP es su medida: sin borde ni barras): el
        // de la vista dueno, el de la ventana (la pantalla) si esta en la lista nativa, o el que da la capa; false si no tiene dueno
        internal static bool ClienteDelDueno(IVista v, out Size cliente)
        {
            IVista p = PadreDe(v);
            if (p != null) { Rectangle r = p.Bounds; cliente = r.Size; return true; }
            if (lista.Contains(v)) { cliente = new Size(Ancho, Alto); return true; }
            ILaCapa c = LaCapa;
            if (c != null) return c.ClienteDelDueno(v, out cliente);
            cliente = Size.Empty; return false;
        }
        internal static void AgregaCapa(CapaDeTextura t) { if (!lista.Contains(t)) { lista.Add(t); AnotaTab(t, lista); CambioVentana(); } }   // (gpupanel-sin-control: con su TabIndex, como el control del WMP)
        // (rotulos-y-letras) Controls.Add de una capa de textura (el video) a una ventana: en una ventana de la capa, su hueco de textura; si no,
        // la lista nativa. Lo que cambia de ella (sitio, si se ve, la textura) lo avisa quien la mueve (CambioCapa): en la lista nativa, volver a
        // componer si cambio el sitio o si se ve (la textura la toma el compositor en cada cuadro); en un hueco, que la capa lo tome.
        internal static void AgregaCapa(object ventana, CapaDeTextura t)
        {
            ventana = VentanaDe(ventana);   // (form1-sin-form) la rocola: su ventana
            ILaCapa c = LaCapa;
            if (c != null && c.EsSuya(ventana)) { c.AgregaCapa(ventana, t); return; }
            AgregaCapa(t);
        }
        internal static void CambioCapa(CapaDeTextura t, bool recompone)
        {
            ILaCapa c = LaCapa;
            if (c != null && c.EsSuyaLaCapa(t)) { c.CambioCapa(t); return; }
            if (recompone && lista.Contains(t)) CambioVentana();
        }
        // BringToFront: el [0] (Control.BringToFront: «ParentInternal.Controls.SetChildIndex(this, 0)», Control.cs)
        internal static void VistaAlFrente(object v)
        {
            IVista iv = v as IVista; ILaCapa c = LaCapa;
            IVista dueno = PadreDe(iv);
            if (dueno != null) { List<object> h = hijas[dueno]; if (h.Remove(iv)) { h.Insert(0, iv); if (EnLaLista(dueno)) CambioVentana(); } return; }   // (dentro de una vista: el [0] de las suyas)
            if (iv != null && c != null && c.EsSuyaLaVista(iv)) { c.AlFrente(iv); return; }
            CapaDeTextura ct = v as CapaDeTextura; if (ct != null && c != null && c.EsSuyaLaCapa(ct)) { c.CapaAlFrente(ct); return; }   // (rotulos-y-letras) la del video, en su hueco
            if (lista.Remove(v)) { lista.Insert(0, v); CambioVentana(); }
        }
        // SendToBack: el ultimo (Control.SendToBack: «ParentInternal.Controls.SetChildIndex(this, -1)»)
        internal static void VistaAlFondo(object v)
        {
            IVista iv = v as IVista; ILaCapa c = LaCapa;
            IVista dueno = PadreDe(iv);
            if (dueno != null) { List<object> h = hijas[dueno]; if (h.Remove(iv)) { h.Add(iv); if (EnLaLista(dueno)) CambioVentana(); } return; }   // (dentro de una vista: el ultimo de las suyas)
            if (iv != null && c != null && c.EsSuyaLaVista(iv)) { c.AlFondo(iv); return; }
            if (lista.Remove(v)) { lista.Add(v); CambioVentana(); }
        }
        // Controls.Remove (Control.ControlCollection.Remove): fuera; si tenia el raton o la captura, ya no (el que esta debajo lo recibe al
        // moverse la flecha)
        internal static void QuitaVista(object v)
        {
            IVista iv = v as IVista; ILaCapa c = LaCapa;
            if (iv != null) { tarjetasQueSeVen.Remove(iv); tarjetasPorPintar.Remove(iv); }   // (gpupanel-sin-control, revision: una de la tarjeta que vuelve a entrar se ensena otra vez y recibe su WM_PAINT)
            IVista dueno = PadreDe(iv);
            if (dueno != null)   // (dentro de una vista: fuera de las suyas, con lo que tenga dentro)
            {
                bool vista = EnLaLista(dueno);
                SueltaDeLaRama(iv);
                hijas[dueno].Remove(iv); padre.Remove(iv);
                if (vista) CambioVentana();
                return;
            }
            if (iv != null && c != null && c.EsSuyaLaVista(iv)) { c.Quita(iv); return; }
            CapaDeTextura ct = v as CapaDeTextura; if (ct != null && c != null && c.EsSuyaLaCapa(ct)) { c.QuitaCapa(ct); return; }   // (rotulos-y-letras) la del video, en su hueco
            if (!lista.Remove(v)) return;
            SueltaDeLaRama(v);
            CambioVentana();
        }
        // si tenia el raton, la captura o el foco (ella o algo de lo que lleva dentro), ya no
        private static void SueltaDeLaRama(object v)
        {
            if (EnLaRama(bajoRaton, v)) bajoRaton = null;
            if (EnLaRama(captura, v)) captura = null;
            if (EnLaRama(foco, v)) foco = null;
        }
        // el orden de una (0 arriba), o -1 si no esta en la lista nativa
        internal static int IndiceDe(object v) { return lista.IndexOf(v); }
        internal static object[] ListaDeLaVentana { get { return lista.ToArray(); } }
        // lo que avisa una vista que ya no hereda (CajaVista): en la lista nativa, volver a componer; en un hueco de la capa, la capa
        internal static void VistaCambio(IVista v, bool repintar)
        {
            if (lista.Contains(v) || (padre.ContainsKey(v) && EnLaLista(v))) { CambioVentana(); if (v.DeLaTarjeta) TarjetaCambio(v, repintar); return; }
            if (padre.ContainsKey(v)) return;   // (hija de una vista que aun no esta en ninguna ventana: nada que componer)
            ILaCapa c = LaCapa; if (c != null) c.Cambio(v, repintar);
        }
        internal static void CambioVentana() { cambiosVentana++; HayQueComponer(); }
        // (gpupanel-sin-control) una vista de la tarjeta de la lista nativa cambio: si hay que pintarla (Invalidate) o se acaba de ensenar
        // (ShowWindow da WM_PAINT a la ventana que aparece; la capa invalida al control al cambiar Visible: Ventanas.Control.SetVisibleCore),
        // su WM_PAINT queda pendiente (PintadoSinLaCapa)
        private static readonly HashSet<IVista> tarjetasQueSeVen = new HashSet<IVista>();
        private static void TarjetaCambio(IVista v, bool repintar)
        {
            bool ve = Ve(v), antes = tarjetasQueSeVen.Contains(v);
            if (ve) tarjetasQueSeVen.Add(v); else tarjetasQueSeVen.Remove(v);
            if (ve && (repintar || !antes)) PorPintar(v);
        }
        // si se ve el dueno de una vista (GetVisibleCore del padre): la ventana de la pantalla siempre se ve; en la capa, lo dice la capa
        internal static bool DuenoVisible(IVista v)
        {
            IVista dueno = PadreDe(v);
            if (dueno != null) return dueno.Visible;   // (GetVisibleCore: la suya y la de su dueno)
            if (lista.Contains(v)) return true;
            ILaCapa c = LaCapa; return c == null || c.DuenoVisible(v);
        }
        // Pinta de fabrica de una vista (la que aun es control de la capa)
        internal static void PintaVistaDeLaCapa(IVista v, SKCanvas g) { ILaCapa c = LaCapa; if (c != null) c.Pinta(v, g); }
        // EL FONDO DE LA VENTANA (OnPaintBackground y OnPaint de la rocola, por procesador): lo pone la rocola cuando su ventana sea la
        // pantalla (oleada 8); sin el, transparente (lo de debajo es negro: la pantalla se borra a negro en cada cuadro)
        internal static Action<SKCanvas> FondoDeLaVentana;
        // hay que volver a componer: con la capa, ella lleva la cuenta de los cambios (Escritorio.Mueve); sin ella, la pantalla
        private static int cambios, compuesto = -1;
        internal static void HayQueComponer()
        {
            ILaCapa c = LaCapa;
            if (c != null) { c.HayQueComponer(); return; }
            cambios++; Despierta();
        }

        // ---------- el foco de una vista y quien escucha su raton desde fuera (armado-vistas, oleada 3: W3-0) ----------
        // FOCUS (Control.Focus: «return FocusInternal()»; FocusInternal: «if (CanFocus) SetFocus(new HandleRef(this, Handle)); ... return
        // Focused», con CanFocus = IsHandleCreated && IsWindowVisible && IsWindowEnabled: Control.cs, Focus, FocusInternal y CanFocus). La
        // rocola de XP le da el foco al buscador de siempre al abrirlo sin tarjeta (searchOverlay.Focus(): Form1.Biblioteca.cs y
        // Form1.Video.cs). En una vista que aun es control de la capa (o en su hueco), la capa (Control.Focus: el foco de WinForms, con su traza
        // y LostFocus); en la lista nativa, la vista que se ve pasa a ser la del foco (la rueda va a ella: RatonRueda; las teclas siguen yendo
        // a la ventana, que tiene KeyPreview -Tab y las flechas las mueve la pantalla entre las vistas: PantallaRocola.Foco.cs-). Una vista no tiene Enabled: siempre lo esta.
        internal static bool EnfocaVista(IVista v)
        {
            ILaCapa c = LaCapa;
            if (c != null && c.EsSuyaLaVista(v)) return c.Enfoca(v);
            if (v != null && EnLaLista(v) && Ve(v) && foco != v)
            {
                foco = v;
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("foco " + v.GetType().Name);
            }
            return v != null && foco == v;
        }
        // LOS QUE ESCUCHAN EL RATON DE UNA VISTA DESDE FUERA (en XP, los eventos de Control: «ayudaView.MouseClick += ...» y
        // «attractView.MouseDown += ...», Form1.cs). En WinForms el evento lo lanza el On* de fabrica (Control.OnMouseDown: «MouseEventHandler
        // handler = (MouseEventHandler)Events[EventMouseDown]; if (handler != null) handler(this, e)»; igual OnMouseClick), asi que llega despues
        // de lo que la vista hace en su On* antes de llamar a base: aqui, despues de su Raton. Solo los que usa la rocola (Abajo = MouseDown,
        // Clic = MouseClick; y, del panel de la tarjeta -gpupanel-sin-control, Form1.Video.cs-, Mueve = MouseMove y Rueda = MouseWheel:
        // Control.OnMouseMove y OnMouseWheel los lanzan igual). En una vista que aun es control de la capa, su evento (la capa lo lanza en el
        // mismo sitio); si no, esta lista, que avisan la pantalla (Avisa) y el hueco de la capa (Plataforma/Anfitrion.cs) despues de Raton.
        private static readonly Dictionary<IVista, List<KeyValuePair<AvisoRaton, Action<Raton>>>> oyentes = new Dictionary<IVista, List<KeyValuePair<AvisoRaton, Action<Raton>>>>();
        internal static void OyeRaton(IVista v, AvisoRaton que, Action<Raton> a)
        {
            if (que != AvisoRaton.Abajo && que != AvisoRaton.Clic && que != AvisoRaton.Mueve && que != AvisoRaton.Rueda) throw new ArgumentException("solo MouseDown (Abajo), MouseClick (Clic), MouseMove (Mueve) y MouseWheel (Rueda): lo que usa la rocola", "que");
            if (v == null || a == null) return;   // (Control.MouseDown += null: no hace nada)
            ILaCapa c = LaCapa;
            if (c != null && c.OyeComoControl(v, que, a)) return;
            List<KeyValuePair<AvisoRaton, Action<Raton>>> l;
            if (!oyentes.TryGetValue(v, out l)) { l = new List<KeyValuePair<AvisoRaton, Action<Raton>>>(); oyentes[v] = l; }
            l.Add(new KeyValuePair<AvisoRaton, Action<Raton>>(que, a));   // (en el orden en que se suscribieron, como un evento multicast)
        }
        internal static void AvisaOyentes(IVista v, AvisoRaton que, Raton e)
        {
            List<KeyValuePair<AvisoRaton, Action<Raton>>> l;
            if (v == null || !oyentes.TryGetValue(v, out l)) return;
            foreach (KeyValuePair<AvisoRaton, Action<Raton>> o in l.ToArray()) if (o.Key == que) o.Value(e);
        }

        // ---------- componer la lista de la ventana ----------
        // LOS TRAMOS: de abajo arriba, las vistas seguidas van pintadas por procesador en una misma imagen del tamano de la pantalla (el
        // primer tramo, con el fondo de la ventana) y cada capa de textura parte la pintura (lo que este encima de ella va en el tramo
        // siguiente). CADA VISTA, RECORTADA POR LAS QUE TIENE ENCIMA: en WinForms cada control es una ventana hija con WS_CLIPSIBLINGS
        // (Control.CreateParams, Control.cs:1691: «cp.Style |= NativeMethods.WS_CHILD | NativeMethods.WS_CLIPSIBLINGS»): donde la tapa un
        // hermano de encima (antes en la lista, se vea por donde se vea, aunque sea transparente) no se pinta; el transparente ensena lo que
        // pinta su padre, nunca a sus hermanos de debajo. Una vista sin medida o que no se ve, ni se pinta ni recorta.
        private sealed class Tramo { internal SKBitmap Mapa; internal int Version; }
        private static readonly List<Tramo> tramos = new List<Tramo>();
        private static int versionTramo;
        internal static void CapasDeLaVentana(List<CapaPantalla> salida)
        {
            if (lista.Count == 0 && FondoDeLaVentana == null) { SueltaTramos(0); return; }
            object[] l = lista.ToArray();
            bool repinta = pintadoVentana != cambiosVentana;   // (cada cambio de una vista -sitio, visible, Invalidate- sube cambiosVentana)
            // los cortes: las capas de textura que se ven y las vistas de la tarjeta que se ven (gpupanel-sin-control), de abajo arriba
            List<int> cortes = new List<int>();
            for (int i = l.Length - 1; i >= 0; i--) { CapaDeTextura t = l[i] as CapaDeTextura; IVista vt = l[i] as IVista; if ((t != null && t.Visible) || (vt != null && vt.DeLaTarjeta && Ve(vt))) cortes.Add(i); }
            int n = cortes.Count + 1;
            while (tramos.Count < n) tramos.Add(new Tramo());
            SueltaTramos(n);
            int hasta = l.Length - 1;   // el tramo t va de 'hasta' (abajo) al corte siguiente (exclusivo)
            for (int t = 0; t < n; t++)
            {
                int corte = t < cortes.Count ? cortes[t] : -1;
                Tramo tr = tramos[t];
                if (repinta || tr.Mapa == null || tr.Mapa.Width != Ancho || tr.Mapa.Height != Alto)
                {
                    if (tr.Mapa == null || tr.Mapa.Width != Ancho || tr.Mapa.Height != Alto) { if (tr.Mapa != null) tr.Mapa.Dispose(); tr.Mapa = new SKBitmap(new SKImageInfo(Math.Max(1, Ancho), Math.Max(1, Alto), SKColorType.Bgra8888, SKAlphaType.Premul)); }
                    using (SKCanvas g = new SKCanvas(tr.Mapa))
                    {
                        g.Clear(SKColors.Transparent);
                        Action<SKCanvas> fondo = FondoDeLaVentana;
                        if (t == 0 && fondo != null) { int s = g.Save(); try { fondo(g); } catch (Exception ex) { Falla(ex); } g.RestoreToCount(s); }
                        for (int i = hasta; i > corte; i--) { IVista v = l[i] as IVista; if (v != null && !v.DeLaTarjeta) PintaRecortada(l, i, v, g); }   // (una de la tarjeta que no se ve no es corte: no se pinta)
                        g.Flush();
                    }
                    tr.Version = ++versionTramo;
                }
                CapaPantalla c = new CapaPantalla();
                c.Clave = tr; c.Mapa = tr.Mapa; c.Version = tr.Version; c.Rect = new Rectangle(0, 0, Ancho, Alto); c.Tipo = "vista";
                salida.Add(c);
                if (corte >= 0)
                {
                    CapaPantalla ct = new CapaPantalla();
                    CapaDeTextura x = l[corte] as CapaDeTextura;
                    if (x != null) { ct.Clave = x; ct.DeTextura = true; ct.Textura = x.Textura; ct.AnchoGl = x.AnchoGl; ct.AltoGl = x.AltoGl; ct.Rgba = x.Rgba; ct.Rect = x.Sitio; ct.Nombre = x.Nombre; ct.Tipo = "tarjeta"; }
                    else
                    {
                        // (gpupanel-sin-control) LA VISTA DE LA TARJETA: lo que la tarjeta presento en su superficie, en el sitio de la vista (el
                        // cliente de su ventana, donde Direct3D 9 ponia el fondo); sin presentar aun, sin textura (no se ve nada: la de debajo
                        // tampoco, que la recorta)
                        IVista vt = (IVista)l[corte]; Superficie su = SuperficieDeLaVista(vt);
                        ct.Clave = (object)su ?? vt; ct.DeTextura = true; ct.Textura = su != null ? su.Textura : 0; ct.AnchoGl = su != null ? su.AnchoGl : 0; ct.AltoGl = su != null ? su.AltoGl : 0;
                        ct.Rect = vt.Bounds; ct.Nombre = vt.GetType().Name; ct.Tipo = "tarjeta";
                    }
                    salida.Add(ct);
                    hasta = corte - 1;
                }
            }
            pintadoVentana = cambiosVentana;
        }
        private static void SueltaTramos(int quedan) { for (int i = quedan; i < tramos.Count; i++) if (tramos[i].Mapa != null) { tramos[i].Mapa.Dispose(); tramos[i].Mapa = null; } }
        private static void PintaRecortada(object[] l, int i, IVista v, SKCanvas g)
        {
            Rectangle r = v.Bounds;
            if (!Ve(v) || r.Width <= 0 || r.Height <= 0) return;
            int s = g.Save();
            try
            {
                g.ClipRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom));
                RecortaForma(g, v, r, SKClipOperation.Intersect);   // (rotulos-y-letras) su forma, si la tiene (la Region: solo se ve dentro)
                for (int j = i - 1; j >= 0; j--)
                {
                    Rectangle rs; bool ve;
                    IVista o = l[j] as IVista; CapaDeTextura t = l[j] as CapaDeTextura;
                    if (o != null) { rs = o.Bounds; ve = Ve(o); } else { rs = t.Sitio; ve = t.Visible; }
                    if (!ve || rs.Width <= 0 || rs.Height <= 0 || !rs.IntersectsWith(r)) continue;
                    if (o != null && o.Forma != null) RecortaForma(g, o, rs, SKClipOperation.Difference);   // (la de encima con forma tapa solo con ella)
                    else g.ClipRect(new SKRect(rs.Left, rs.Top, rs.Right, rs.Bottom), SKClipOperation.Difference);
                }
                g.Translate(r.Left, r.Top);
                v.Pinta(g);
                PintaHijas(v, g);
            }
            catch (Exception ex) { Falla(ex); }   // (lo que lanza un OnPaint va a ThreadException: NativeWindow.Callback)
            finally { g.RestoreToCount(s); }
        }
        // LAS HIJAS DE UNA VISTA, despues de ella y de abajo arriba (Ventanas.Escritorio.PintaArbol: el control y despues sus hijos), cada una
        // recortada a su dueno (un hijo es una ventana hija: WS_CHILD, no se sale del cliente de su padre) y por las hermanas que tiene encima
        // (WS_CLIPSIBLINGS, como las de la ventana); 'g' esta en la esquina del dueno
        private static void PintaHijas(IVista dueno, SKCanvas g)
        {
            List<object> l; if (!hijas.TryGetValue(dueno, out l)) return;
            object[] h = l.ToArray();
            for (int i = h.Length - 1; i >= 0; i--)
            {
                IVista v = h[i] as IVista; if (v == null || v.DeLaTarjeta) continue;   // (gpupanel-sin-control: una de la tarjeta no se pinta por procesador; dentro de otra vista no se compone: XP no tiene ninguna)
                Rectangle r = v.Bounds;
                if (!Ve(v) || r.Width <= 0 || r.Height <= 0) continue;
                int s = g.Save();
                try
                {
                    g.ClipRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom));
                    RecortaForma(g, v, r, SKClipOperation.Intersect);
                    for (int j = i - 1; j >= 0; j--)
                    {
                        IVista o = h[j] as IVista; if (o == null) continue;
                        Rectangle rs = o.Bounds;
                        if (!Ve(o) || rs.Width <= 0 || rs.Height <= 0 || !rs.IntersectsWith(r)) continue;
                        if (o.Forma != null) RecortaForma(g, o, rs, SKClipOperation.Difference);
                        else g.ClipRect(new SKRect(rs.Left, rs.Top, rs.Right, rs.Bottom), SKClipOperation.Difference);
                    }
                    g.Translate(r.Left, r.Top);
                    v.Pinta(g);
                    PintaHijas(v, g);
                }
                catch (Exception ex) { Falla(ex); }
                finally { g.RestoreToCount(s); }
            }
        }
        // si se ve la vista misma (su Visible incluye al dueno, que en la lista nativa es la pantalla)
        private static bool Ve(IVista v) { return v.Visible; }
        // (rotulos-y-letras) LA FORMA DE UNA VISTA (Control.Region: SetWindowRgn), en sus coordenadas: recorta lo que se pinta (Intersect: la vista
        // solo dentro de ella) o a las de debajo (Difference: la de encima tapa solo con ella), sin suavizar, como el recorte por region de la
        // capa (Ventanas.Escritorio.PintaArbol y PintaHijo: SetClip(Region) en el sitio del control)
        private static void RecortaForma(SKCanvas g, IVista v, Rectangle sitio, SKClipOperation op)
        {
            SKPath f = v.Forma; if (f == null) return;
            using (SKPath t = new SKPath(f)) { t.Transform(SKMatrix.CreateTranslation(sitio.Left, sitio.Top)); g.ClipPath(t, op, false); }
        }
        // si el punto (en las coordenadas de la vista) esta dentro de su forma; sin forma, si (con Region, la ventana es solo su forma: fuera de
        // ella el raton es del de debajo; el centro del pixel, como la capa: Ventanas.Escritorio, HijoEn)
        private static bool EnLaForma(IVista v, Point q) { SKPath f = v.Forma; return f == null || f.Contains(q.X + 0.5f, q.Y + 0.5f); }

        // ---------- el raton ----------
        // LA DE ENCIMA BAJO LA FLECHA (WindowFromPoint y ChildWindowFromPointEx: la ventana hija de mas arriba que contiene el punto y se
        // ve; Control.cs:5806). Una capa de textura tambien es una ventana (el control del WMP): se queda con el raton aunque no haga nada con
        // el. (Una vista de la tarjeta -el GpuPanel- es una vista: recibe su raton.)
        internal static object VistaEn(Point p)
        {
            foreach (object o in lista)
            {
                IVista v = o as IVista;
                if (v != null) { if (Ve(v) && v.Bounds.Contains(p) && EnLaForma(v, new Point(p.X - v.Bounds.X, p.Y - v.Bounds.Y))) return Dentro(v, new Point(p.X - v.Bounds.X, p.Y - v.Bounds.Y)); continue; }
                CapaDeTextura t = (CapaDeTextura)o;
                if (t.Visible && t.Sitio.Contains(p)) return t;
            }
            return null;
        }
        // la ventana hija de mas adentro bajo el punto (en las coordenadas de la vista): ChildWindowFromPointEx baja por las hijas de mas arriba
        // que se ven y contienen el punto
        private static IVista Dentro(IVista v, Point q)
        {
            List<object> l; if (!hijas.TryGetValue(v, out l)) return v;
            foreach (object o in l)
            {
                IVista h = o as IVista;
                if (h != null && Ve(h) && h.Bounds.Contains(q) && EnLaForma(h, new Point(q.X - h.Bounds.X, q.Y - h.Bounds.Y))) return Dentro(h, new Point(q.X - h.Bounds.X, q.Y - h.Bounds.Y));
            }
            return v;
        }
        private static object bajoRaton, captura, ultimoClicEn;
        private static double ultimoClicMs = -1e9; private static Point ultimoClicPunto; private static BotonRaton ultimoClicBoton;
        private static bool dobleEnCurso;
        internal static BotonRaton BotonesRaton;
        // El doble clic de Windows XP de fabrica (GetDoubleClickTime 500 ms, SM_CXDOUBLECLK y SM_CYDOUBLECLK 4 puntos), como la capa
        internal const double DobleClicMs = 500; internal const int DobleClicPuntos = 4;
        private static void Avisa(object a, AvisoRaton que, BotonRaton b, int clics, int delta, Raton e = null)
        {
            IVista v = a as IVista;
            if (v == null) return;   // (una capa de textura: se queda con el raton y no hace nada)
            Point r = OrigenDe(v);
            if (e == null) e = new Raton(b, clics, Flecha.X - r.X, Flecha.Y - r.Y, delta);
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("raton " + TrazaVentanas.EventoRaton[(int)que] + " " + v.GetType().Name + (que == AvisoRaton.Entra || que == AvisoRaton.Sale ? "" : " " + e.X + "," + e.Y + " " + TrazaVentanas.Datos((int)e.Button) + " " + e.Clicks + " " + e.Delta));
            try { v.Raton(que, e); AvisaOyentes(v, que, e); } catch (Exception ex) { Falla(ex); }   // (los de fuera despues: el evento lo lanza el On* de fabrica)
        }
        private static void BajoLaFlecha(object v)
        {
            if (v == bajoRaton) return;
            object antes = bajoRaton; bajoRaton = v;
            if (antes != null) Avisa(antes, AvisoRaton.Sale, BotonRaton.None, 0, 0);   // (WM_MOUSELEAVE: OnMouseLeave)
            if (v != null) Avisa(v, AvisoRaton.Entra, BotonRaton.None, 0, 0);          // (el primer WM_MOUSEMOVE: OnMouseEnter)
        }
        // el raton se movio (dx, dy) o esta en (x, y); queda dentro de la pantalla
        internal static void RatonMueve(int dx, int dy) { RatonEn(Flecha.X + dx, Flecha.Y + dy); }
        internal static void RatonEn(int x, int y)
        {
            ILaCapa c = LaCapa; if (c != null) { c.RatonEn(x, y); return; }
            x = Math.Max(0, Math.Min(Ancho - 1, x)); y = Math.Max(0, Math.Min(Alto - 1, y));
            if (x == Flecha.X && y == Flecha.Y) return;
            Flecha = new Point(x, y);
            // las hojas nativas, encima de todo: lo tienen capturado, hay una modal o la flecha esta encima de una (Hoja.TomaElRaton)
            if (captura == null && Hoja.TomaElRaton(Flecha)) { BajoLaFlecha(null); Hoja.RatonEn(Flecha, BotonesRaton); return; }
            Hoja.RatonFuera();
            // con un boton apretado, el raton es del que lo recibio (SetCapture en WmMouseDown, Control.cs:12567-12571: CaptureInternal)
            if (captura != null) { Avisa(captura, AvisoRaton.Mueve, BotonesRaton, 0, 0); return; }
            BajoLaFlecha(VistaEn(Flecha));
            if (bajoRaton != null) Avisa(bajoRaton, AvisoRaton.Mueve, BotonesRaton, 0, 0);
        }
        internal static void RatonBoton(BotonRaton b, bool abajo)
        {
            ILaCapa cp = LaCapa; if (cp != null) { cp.RatonBoton(b, abajo); return; }
            if (abajo)
            {
                BotonesRaton |= b;
                if (Hoja.TomaElRaton(Flecha)) { BajoLaFlecha(null); Hoja.RatonBoton(b, true, Flecha); return; }
                object v = VistaEn(Flecha);
                BajoLaFlecha(v);
                if (v == null) return;
                captura = v;   // (WmMouseDown: CaptureInternal = true)
                // EL DOBLE CLIC (WM_LBUTTONDBLCLK de Windows: el segundo en el mismo sitio, con el mismo boton y en el plazo; un tercero empieza
                // otra cuenta), los Clicks de MouseDown dicen 2
                double t = AhoraMs;
                dobleEnCurso = b == ultimoClicBoton && v == ultimoClicEn && t - ultimoClicMs <= DobleClicMs
                    && Math.Abs(Flecha.X - ultimoClicPunto.X) <= DobleClicPuntos / 2 && Math.Abs(Flecha.Y - ultimoClicPunto.Y) <= DobleClicPuntos / 2;
                if (dobleEnCurso) ultimoClicMs = -1e9;
                else { ultimoClicMs = t; ultimoClicPunto = Flecha; ultimoClicBoton = b; ultimoClicEn = v; }
                Avisa(v, AvisoRaton.Abajo, b, dobleEnCurso ? 2 : 1, 0);
            }
            else
            {
                BotonesRaton &= ~b;
                if (captura == null && Hoja.TomaElRaton(Flecha)) { Hoja.RatonBoton(b, false, Flecha); return; }
                object v = captura ?? VistaEn(Flecha);
                captura = null;
                if (v == null) return;
                bool doble = dobleEnCurso; dobleEnCurso = false;
                // WmMouseUp (Control.cs:12673-12743): el clic solo si se solto encima de la misma (WindowFromPoint(pt) == Handle) y con
                // StandardClick (de fabrica en Control: Control.cs:449-454, tambien StandardDoubleClick); el segundo de un doble clic es
                // DoubleClick y MouseDoubleClick (con 2), si no Click y MouseClick; y despues MouseUp.
                if (VistaEn(Flecha) == v) Avisa(v, doble ? AvisoRaton.DobleClic : AvisoRaton.Clic, b, doble ? 2 : 1, 0);
                Avisa(v, AvisoRaton.Arriba, b, 1, 0);
                BajoLaFlecha(VistaEn(Flecha));
            }
        }
        // LA RUEDA (WM_MOUSEWHEEL) va a la que tiene el foco; si no la atiende (Handled), DefWindowProc la pasa a su dueno (WmMouseWheel,
        // Control.cs:12748-12764): la ventana (RuedaDeLaVentana). Con una hoja nativa activa, a la pieza con el foco de la hoja.
        internal static Action<Raton> RuedaDeLaVentana;
        private static object foco;
        internal static object FocoDeLaVentana { get { return foco; } set { foco = value; } }
        internal static void RatonRueda(int muescas)
        {
            ILaCapa c = LaCapa; if (c != null) { c.RatonRueda(muescas); return; }
            if (Hoja.Activa != null) { Hoja.RatonRueda(muescas); return; }
            // (sin atender, DefWindowProc la pasa al padre: la vista que la contiene, y despues la ventana)
            for (IVista v = foco as IVista; v != null; v = PadreDe(v))
            {
                Point r = OrigenDe(v);
                Raton e = new Raton(BotonRaton.None, 0, Flecha.X - r.X, Flecha.Y - r.Y, muescas * 120);
                Avisa(v, AvisoRaton.Rueda, BotonRaton.None, 0, muescas * 120, e);
                if (e.Handled) return;
            }
            Action<Raton> w = RuedaDeLaVentana;
            if (w != null) { Raton e = new Raton(BotonRaton.None, 0, Flecha.X, Flecha.Y, muescas * 120); Protege(delegate { w(e); }); }
        }

        // ---------- las teclas ----------
        // LO QUE LA VENTANA DE LA ROCOLA HACE CON LAS TECLAS (Form1: ProcessCmdKey, KeyPress con Form_KeyPress y OnKeyUp; Alt+F4 la cierra).
        // Lo pone la rocola cuando su ventana sea la pantalla (oleada 8); hoy las teclas las reparte la capa.
        internal delegate bool Mando(ref PulsoTecla m, Tecla datos);
        internal sealed class TeclasDeLaVentana
        {
            internal Mando ProcesaMando;                 // ProcessCmdKey (la primera: PreProcessMessage, Control.cs:9275-9300)
            internal Func<Tecla, bool> TeclaDeDialogo;   // ProcessDialogKey (de una ventana sin AcceptButton ni CancelButton: false); el Tab y las flechas de ContainerControl los pone la pantalla despues (PantallaRocola.Foco.cs)
            internal Action<PulsoTecla> TeclaAbajo;      // KeyDown
            internal Action<PulsoCaracter> Caracter;     // KeyPress
            internal Action<PulsoTecla> TeclaArriba;     // KeyUp (OnKeyUp)
            internal Action CierraPorSistema;            // Alt+F4 sin atender (SC_CLOSE, CloseReason.UserClosing)
        }
        internal static TeclasDeLaVentana Teclas;
        internal static Tecla Modificadores;
        // UNA TECLA (lo que manda evdev o las pruebas, en el hilo de la pantalla). Con la capa, la reparte ella. Sin ella: los
        // modificadores se juntan (Shift, Control y Alt de cualquier lado: WM_KEYDOWN da VK_SHIFT, VK_CONTROL y VK_MENU), se anota como
        // atendida (GetKeyState) y va a la hoja activa o a la ventana: ProcessCmdKey, ProcessDialogKey, KeyDown, el caracter (WM_CHAR,
        // o WM_SYSCHAR con Alt) a KeyPress salvo SuppressKeyPress, y Alt+F4 sin atender por la cola (DefWindowProc de WM_SYSKEYDOWN); al
        // soltar, KeyUp. (vistas-buscador) Tab y las flechas, sin atender por ProcessCmdKey ni por la ventana, mueven el foco entre las vistas
        // (PantallaRocola.Foco.cs); las teclas siguen yendo a la ventana, que en la rocola tiene KeyPreview y ve cada tecla antes que nadie.
        internal static void LlegaTecla(Tecla tecla, bool abajo)
        {
            ILaCapa c = LaCapa; if (c != null) { c.Tecla(tecla, abajo); return; }
            Tecla mod = tecla == Rocola.Tecla.ShiftKey || tecla == Rocola.Tecla.LShiftKey || tecla == Rocola.Tecla.RShiftKey ? Rocola.Tecla.Shift
                     : tecla == Rocola.Tecla.ControlKey || tecla == Rocola.Tecla.LControlKey || tecla == Rocola.Tecla.RControlKey ? Rocola.Tecla.Control
                     : tecla == Rocola.Tecla.Menu || tecla == Rocola.Tecla.LMenu || tecla == Rocola.Tecla.RMenu ? Rocola.Tecla.Alt : Rocola.Tecla.None;
            if (mod != Rocola.Tecla.None) tecla = mod == Rocola.Tecla.Shift ? Rocola.Tecla.ShiftKey : mod == Rocola.Tecla.Control ? Rocola.Tecla.ControlKey : Rocola.Tecla.Menu;
            if (abajo) Modificadores |= mod;
            int vk = (int)tecla & 0xFF; bool antes = AnotaTecla(vk, abajo), repetida = abajo && antes;
            Tecla datos = tecla | Modificadores;
            if (Hoja.Activa != null) { if (!abajo) Modificadores &= ~mod; Hoja.Tecla(tecla, abajo, Modificadores, repetida); return; }
            TeclasDeLaVentana v = Teclas;
            if (v == null) { if (!abajo) Modificadores &= ~mod; return; }
            if (abajo)
            {
                bool alt = (Modificadores & Rocola.Tecla.Alt) != 0;
                PulsoTecla m = new PulsoTecla(datos, repetida);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("tecla " + TrazaVentanas.Datos((int)datos) + (repetida ? " rep" : "") + " a ventana");
                bool pck = v.ProcesaMando != null && v.ProcesaMando(ref m, datos);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("pck " + (pck ? "1" : "0"));
                if (pck) return;
                if (v.TeclaDeDialogo != null && v.TeclaDeDialogo(datos)) return;
                if (TeclaDeDialogoDeLaVentana(datos)) return;   // (vistas-buscador) Tab y flechas entre las vistas: ContainerControl.ProcessDialogKey (PantallaRocola.Foco.cs)
                PulsoTecla e = new PulsoTecla(datos);
                if (v.TeclaAbajo != null) v.TeclaAbajo(e);
                bool atendida = e.Handled, suprime = e.SuppressKeyPress;
                if (!atendida && alt && tecla == Rocola.Tecla.F4 && (Modificadores & Rocola.Tecla.Control) == 0 && v.CierraPorSistema != null)
                {
                    Action cierra = v.CierraPorSistema;
                    EncolaDesde(null, cierra, "Alt+F4", false);
                }
                if (!suprime)
                {
                    char ch = CaracterDe(tecla, Modificadores);
                    if (ch != '\0' && v.Caracter != null)
                    {
                        if (TrazaVentanas.Activa) TrazaVentanas.Apunta("caracter " + ((int)ch).ToString(System.Globalization.CultureInfo.InvariantCulture) + " a ventana");
                        v.Caracter(new PulsoCaracter(ch));
                    }
                }
            }
            else
            {
                Modificadores &= ~mod;
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("keyup " + TrazaVentanas.Datos((int)(tecla | Modificadores)) + " a ventana");
                if (v.TeclaArriba != null) v.TeclaArriba(new PulsoTecla(tecla | Modificadores));
            }
        }

        // ---------- la tarjeta ----------
        // LA SUPERFICIE DE UNA VISTA DE LA TARJETA (gpupanel-sin-control: W4). En XP el GpuPanel era una ventana con un dispositivo de Direct3D 9
        // creado sobre ella (CreateDevice con su Handle) que presentaba en ella (Present). Aqui la vista no tiene ventana: el dispositivo recibe
        // el identificador de su superficie (SuperficieDe: lo que en XP era el Handle; no choca con los de la capa, que empiezan en 0x1000, ni
        // con los de las hojas, en 0x40000000) y Present le da a la superficie su textura: con la vista en un hueco de la capa, el hueco (la
        // capa la compone como componia el control «Nativa»); en la lista nativa, la pantalla la compone en el sitio y el lugar del orden de la
        // vista (CapasDeLaVentana). Soltarla (SueltaSuperficie, el base.Dispose de XP) es como destruir la ventana: Present ya no la encuentra.
        internal sealed class Superficie
        {
            internal readonly IVista Vista; internal readonly IntPtr Id;
            internal uint Textura; internal int AnchoGl, AltoGl;
            internal bool Ensenada;   // (si se veia la ultima vez que cambio: al ensenarse recibe su WM_PAINT)
            internal Superficie(IVista v, IntPtr id) { Vista = v; Id = id; }
        }
        private static readonly Dictionary<IVista, Superficie> superficies = new Dictionary<IVista, Superficie>();
        private static long ultimaSuperficie = 0x60000000;
        internal static IntPtr SuperficieDe(IVista v)
        {
            Superficie s;
            if (!superficies.TryGetValue(v, out s)) { s = new Superficie(v, new IntPtr(++ultimaSuperficie)); superficies[v] = s; }
            return s.Id;
        }
        // (gpupanel-sin-control, revision) y sale de su dueno: Control.Dispose con disposing (el unico que llama el Dispose de XP: la vista no
        // tiene finalizador) hace «_parent.Controls.Remove(this)» despues de DestroyHandle (Control.cs:5345-5350); en un hueco de la capa lo
        // hace el hueco al desecharse (Plataforma/Anfitrion.cs), aqui en la lista nativa
        internal static void SueltaSuperficie(IVista v)
        {
            superficies.Remove(v);
            ILaCapa c = LaCapa;
            if (c != null && c.EsSuyaLaVista(v)) return;
            if (lista.Contains(v) || padre.ContainsKey(v)) QuitaVista(v);
            tarjetasQueSeVen.Remove(v); tarjetasPorPintar.Remove(v);
        }
        private static Superficie SuperficieConId(IntPtr id)
        {
            if (id == IntPtr.Zero) return null;
            foreach (Superficie s in superficies.Values) if (s.Id == id) return s;
            return null;
        }
        private static Superficie SuperficieDeLaVista(IVista v) { Superficie s; return v != null && superficies.TryGetValue(v, out s) ? s : null; }
        // FindForm() de la vista y su WindowState == Minimized (GpuPanel.PuedeDibujar): con la vista en un hueco de la capa, la ventana de su
        // hueco; en la pantalla nativa la ventana es la pantalla, que no se minimiza
        internal static bool VentanaMinimizada(IVista v)
        {
            ILaCapa c = LaCapa;
            return c != null && c.EsSuyaLaVista(v) && c.VentanaMinimizada(v);
        }

        // Present de Direct3D 9 en la ventana 'ventana' (PantallaTarjeta.cs): en la superficie de una vista de la tarjeta (en un hueco de la
        // capa, la capa; en la lista nativa, su capa de textura y se compone); en una hoja (la ventana de prueba F12), la capa; sin ninguna, glFlush.
        internal static bool SinEspera;   // el medidor de Ctrl+F3 pide presentar sin esperar al barrido
        internal static double UltimaPresentacionTarjeta = -1e9;
        internal static Action Componer;  // compone y presenta (lo pone quien abre la pantalla)
        // (true si se compuso la pantalla: OpenGL ya no es lo del dispositivo)
        internal static bool PresentaTarjeta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera)
        {
            ILaCapa c = LaCapa;
            Superficie t = SuperficieConId(ventana);
            if (t != null && c != null && c.EsSuyaLaVista(t.Vista)) return c.PresentaEnVista(t.Vista, textura, ancho, alto, sinEspera);
            if (t == null || !EnLaLista(t.Vista))
            {
                if (t == null && c != null) return c.Presenta(ventana, textura, ancho, alto, sinEspera);
                GlMas.Flush(); return false;   // (una vista que no esta en ninguna ventana: Present sin ventana)
            }
            t.Textura = textura; t.AnchoGl = ancho; t.AltoGl = alto;
            SinEspera = sinEspera;
            UltimaPresentacionTarjeta = AhoraMs;
            compuesto = cambios;
            Action a = Componer; if (a != null) a();
            return true;
        }
        internal static void OlvidaTarjeta(IntPtr ventana)
        {
            Superficie t = SuperficieConId(ventana);
            ILaCapa c = LaCapa;
            if (t != null) { if (c != null && c.EsSuyaLaVista(t.Vista)) c.OlvidaEnVista(t.Vista); t.Textura = 0; return; }
            if (c != null) c.OlvidaTarjeta(ventana);
        }
        // EL WM_PAINT DE UNA VISTA DE LA TARJETA en la lista nativa (lo que la capa hace con un control «Nativa»: Escritorio.Ensucia y PintaYa):
        // hay que pintarla cuando se invalida o se ensena (ShowWindow da WM_PAINT); lo da PintadoSinLaCapa antes de componer, uno por vuelta
        private static readonly List<IVista> tarjetasPorPintar = new List<IVista>();
        private static void PorPintar(IVista v) { if (!tarjetasPorPintar.Contains(v)) tarjetasPorPintar.Add(v); Despierta(); }
        private static SKBitmap mapaSuelto; private static SKCanvas lienzoSuelto;   // (el lienzo que recibe su Pinta: la tarjeta no lo usa)
        private static bool PintaUnaTarjeta()
        {
            if (tarjetasPorPintar.Count == 0) return false;
            IVista v = tarjetasPorPintar[0]; tarjetasPorPintar.RemoveAt(0);
            if (!EnLaLista(v) || !Ve(v)) return true;
            if (lienzoSuelto == null) { mapaSuelto = new SKBitmap(1, 1); lienzoSuelto = new SKCanvas(mapaSuelto); }
            int g = lienzoSuelto.Save();
            try { v.Pinta(lienzoSuelto); } catch (Exception ex) { Falla(ex); }   // (lo que lanza un WM_PAINT va a ThreadException)
            finally { lienzoSuelto.RestoreToCount(g); }
            return true;
        }

        // ---------- el pintado sin la capa (WM_PAINT de la ventana de la pantalla) ----------
        // Lo que hoy da la capa (Escritorio.PintaUno, HaySucio y ProximoPintado): el WM_PAINT de una vista de la tarjeta que hay que pintar
        // (gpupanel-sin-control) y, si no, componer si algo cambio y la tarjeta no esta presentando (o hace mas de 100 ms que no: si presenta,
        // compone ella en cada cuadro). Lo pone quien abre la pantalla cuando no hay capa.
        internal static void PintadoSinLaCapa()
        {
            PintaUno = delegate { if (PintaUnaTarjeta()) return true; if (!ComposicionPendiente()) return false; compuesto = cambios; try { Action a = Componer; if (a != null) a(); } catch (Exception ex) { Falla(ex); } return true; };
            HaySucio = delegate { return tarjetasPorPintar.Count > 0 || ComposicionPendiente(); };   // (gpupanel-sin-control: el WM_PAINT de las vistas de la tarjeta primero, como Escritorio.PintaUno)
            ProximoPintado = delegate { return Componer != null && cambios != compuesto ? UltimaPresentacionTarjeta + 100.5 : double.MaxValue; };
        }
        private static bool ComposicionPendiente() { return Componer != null && cambios != compuesto && AhoraMs - UltimaPresentacionTarjeta > 100; }
    }
}

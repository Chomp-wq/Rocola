// EL ANFITRION DE ROCOLA OS (traduccion de XP, fase 1): lo que en XP hace Windows con la pantalla. Abre la pantalla de verdad (KMS, con
// OpenGL ES 2 en el hilo de la rocola) o una fuera de pantalla (pruebas y capturas: ROCOLA_PANTALLA=fuera:1024x768, o si no hay tarjeta),
// y en cada cuadro le da al compositor nativo (Nativo/CompositorPantalla.cs) las capas, de abajo arriba: las de las ventanas de la capa (lo
// pintado con Dibujo2D, y lo que pinta la tarjeta o el video en sus controles «Nativa»), la lista nativa de la ventana
// (Nativo/PantallaRocola.Vistas.cs) y las hojas nativas (Nativo/Hoja.cs). (Las capturas, ROCOLA_CAPTURA..., las hace el compositor.)
// TRANSICION (port nativo, tanda 4, grupo pantalla-nativa: W2): aqui queda solo lo que une la pantalla nativa con las ventanas de la capa
// (Plataforma/Ventanas.*.cs): VentanasDeLaCapa (lo que la pantalla le pide a la capa: PantallaRocola.ILaCapa) y el hueco de una vista que
// ya no hereda de Control dentro de una ventana de la capa (HuecoDeVista). Se pasa a Nativo, sin la capa, al final (N7).
using System;
using System.Collections.Generic;
using System.Globalization;
using Rocola.Pantalla;
using SkiaSharp;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;
using Rocola.Dibujo2D;   // GDI+ de la transicion (Dibujo2D.cs); ya no es global (port nativo, D1)

namespace Rocola
{
    internal sealed class Anfitrion : IAnfitrion
    {
        internal static Anfitrion Actual;
        private readonly CompositorPantalla compositor;
        internal int Composiciones { get { return compositor.Composiciones; } }

        // Al empezar (antes del Main de XP, en el mismo hilo): la pantalla y el contexto de OpenGL.
        internal static void Arranca()
        {
            if (Actual != null) return;
            string p = Environment.GetEnvironmentVariable("ROCOLA_PANTALLA") ?? "";
            ISuperficie s = null;
            if (!p.StartsWith("fuera"))
            {
                try { s = new Kms(); }
                catch (Exception ex) { Console.Error.WriteLine("Pantalla: sin KMS (" + ex.Message + "): fuera de pantalla"); }
            }
            if (s == null)
            {
                int w = 1024, h = 768;
                int i = p.IndexOf(':');
                if (i > 0) { string[] t = p.Substring(i + 1).Split('x'); if (t.Length == 2) { int.TryParse(t[0], out w); int.TryParse(t[1], out h); } }
                s = new FueraDePantalla(w, h);
            }
            Actual = new Anfitrion(s);
            Escritorio.Anfitrion = Actual;
            PantallaRocola.LaCapa = new VentanasDeLaCapa();   // (transicion) la pantalla nativa le pide a la capa lo de sus ventanas
            PantallaRocola.Componer = Actual.Compone;
            ReproductorVideo.Pantalla = new VideoEnPantalla();   // la capa del video (Nativo/ReproductorVideo.cs): en una ventana de la capa, su hueco (VideoEnVentanas.cs)
            // las teclas: con la pantalla de verdad, los teclados y botones de /dev/input (TeclasLinux.cs); y las de prueba
            if (s is Kms || Environment.GetEnvironmentVariable("ROCOLA_TECLAS") == "1") TeclasLinux.Arranca("/dev/input");
            TeclasLinux.Programadas(Environment.GetEnvironmentVariable("ROCOLA_PULSA"));
            // (banco, port nativo ventanas2 C) ROCOLA_MIDE_HOJA=1: cuanto tarda cada pintura entera de una hoja nativa (Nativo/Hoja.cs), en la salida de errores
            if (Environment.GetEnvironmentVariable("ROCOLA_MIDE_HOJA") == "1")
                Hoja.MidePintura = delegate (Hoja h, double ms) { Console.Error.WriteLine("hoja-pinta " + h.Width + "x" + h.Height + " " + ms.ToString("0.00", CultureInfo.InvariantCulture) + " ms"); };
        }

        private Anfitrion(ISuperficie s)
        {
            compositor = new CompositorPantalla(s);
            compositor.ConMano = delegate { return Escritorio.CursorActual == Cursors.Hand; };   // (la mano de lo que esta bajo el raton en la capa)
            PantallaRocola.Ancho = s.Ancho; PantallaRocola.Alto = s.Alto;   // (Escritorio.Pantalla: es la misma)
            int hz = 60;
            int a = s.Descripcion.IndexOf(" a "), b = s.Descripcion.IndexOf(" Hz");
            if (a > 0 && b > a) int.TryParse(s.Descripcion.Substring(a + 3, b - a - 3), out hz);
            PantallaRocola.RefrescoHz = Escritorio.RefrescoHz = hz >= 24 && hz <= 400 ? hz : 60;
            Console.WriteLine("Pantalla: " + s.Descripcion);
        }

        // ---- un cuadro: el compositor nativo con las capas de abajo arriba ----
        public void Compone() { compositor.Compone(Capas); }
        internal void GuardaPng(string ruta) { compositor.GuardaPng(ruta); }
        internal uint PuntoEn(int x, int y) { return compositor.PuntoEn(x, y); }   // (pruebas)
        // LAS CAPAS: las de las ventanas de la capa (Escritorio.Capas: cada ventana en tramos de Dibujo2D partidos por sus controles de la
        // tarjeta y del video, en el orden de las ventanas), la lista nativa de la ventana (lo que no es de ninguna ventana de la capa: hoy
        // nada) y las hojas nativas, encima de todo, en su orden. Es el mismo orden que daba Escritorio.Capas (las hojas al final) y las
        // mismas capas: se compone lo mismo. (Revision: las hojas se toman tal cual las dio Escritorio.Capas -su superficie y la version que
        // se pinto, Hoja.Superficie- y no se le pide otra vez la superficie a cada hoja: con una pieza que lanza al pintarse -la «X» roja- la
        // segunda peticion la volvia a pintar en el mismo cuadro y se subia otra pintura que la de la capa. Cuando la capa se vaya (N7), la
        // pantalla las pedira a Hoja.Abiertas una sola vez por cuadro.)
        private List<CapaPantalla> Capas()
        {
            List<CapaPantalla> r = new List<CapaPantalla>();
            List<CapaPantalla> hojas = null, bajo = null;
            foreach (Capa c in Escritorio.Capas())
            {
                CapaPantalla p = new CapaPantalla();
                p.Clave = c; p.Rect = c.Rect; p.Opacidad = c.Opacidad;
                if (c.Nativa != null)
                {
                    Control n = c.Nativa;
                    p.DeTextura = true; p.Textura = n.TexturaGl; p.AnchoGl = n.AnchoGl; p.AltoGl = n.AltoGl; p.Rgba = n.TexturaGlRgba; p.Nombre = (VistaDeHueco(n) ?? n).GetType().Name; p.Tipo = "tarjeta";   // (gpupanel-sin-control: el hueco de la vista de la tarjeta se nombra por ella, «GpuPanel», como cuando era el control)
                }
                else if (c.Imagen != null) { p.Mapa = c.Imagen.Sk; p.Version = c.Version; p.Tipo = "dibujo2d"; }
                else if (c.TarjetaDeHoja != null)   // (port nativo, f12-como-hoja) la tarjeta en el cliente de una hoja: va con las hojas, justo despues de la suya
                {
                    Hoja th = c.TarjetaDeHoja;
                    p.DeTextura = true; p.Textura = th.TexturaGl; p.AnchoGl = th.AnchoGl; p.AltoGl = th.AltoGl; p.Nombre = th.GetType().Name; p.Tipo = "tarjeta";
                    if (c.Detras) { (bajo ?? (bajo = new List<CapaPantalla>())).Add(p); continue; }
                    if (hojas == null) hojas = new List<CapaPantalla>();
                    hojas.Add(p); continue;
                }
                else if (c.Mapa != null)   // una hoja nativa (Nativo/Hoja.cs): va encima de la lista nativa
                {
                    p.Mapa = c.Mapa; p.Version = c.Version; p.Tipo = "hoja";
                    if (c.Detras) { (bajo ?? (bajo = new List<CapaPantalla>())).Add(p); continue; }   // (port nativo, f12-como-hoja) detras de las ventanas de la capa
                    if (hojas == null) hojas = new List<CapaPantalla>();
                    hojas.Add(p); continue;
                }
                else continue;
                r.Add(p);
            }
            PantallaRocola.CapasDeLaVentana(r);
            if (hojas != null) r.AddRange(hojas);
            if (bajo != null) r.InsertRange(0, bajo);
            return r;
        }

        // ---------- (transicion) LO QUE LA PANTALLA NATIVA LE PIDE A LAS VENTANAS DE LA CAPA ----------
        // Cada cosa es la llamada de la capa que hacia lo mismo antes de la pantalla nativa (Escritorio y Control), asi la rocola se ve, se
        // mueve y recibe la entrada igual. Se va con la capa (N7).
        // (trazas) la vista de un hueco, o null si no es un hueco: TrazaVentanas.Pieza lo nombra por ella
        internal static object VistaDeHueco(object o) { HuecoDeVista h = o as HuecoDeVista; return h != null ? h.Vista : null; }
        private sealed class VentanasDeLaCapa : PantallaRocola.ILaCapa
        {
            private readonly Dictionary<IVista, HuecoDeVista> huecos = new Dictionary<IVista, HuecoDeVista>();
            private bool soloLaSuya;   // (Cambio: el Visible propio de la vista, sin el de su dueno)
            public bool EsSuya(object ventana) { return ventana is Control; }
            public bool EsSuyaLaVista(IVista v) { return v is Control || huecos.ContainsKey(v); }
            private Control ControlDe(IVista v) { Control c = v as Control; if (c != null) return c; HuecoDeVista h; return huecos.TryGetValue(v, out h) ? h : null; }
            // Controls.Add de la ventana: la vista misma si aun es un control; si no, su hueco (al fondo de la lista, como Controls.Add)
            public void Agrega(object ventana, IVista v)
            {
                Control dueno = ventana as Control;
                // (panel-del-disco) el dueno puede ser una vista que ya no hereda de Control (BgPanel): su hueco es el Panel de WinForms que
                // contiene a las hijas (Controls.Add del hueco: su orden, su recorte al cliente y su raton, los de la capa)
                if (dueno == null) { IVista dv = ventana as IVista; if (dv != null) dueno = ControlDe(dv); }
                Control c = v as Control;
                if (c == null)
                {
                    HuecoDeVista h;
                    if (!huecos.TryGetValue(v, out h)) { h = new HuecoDeVista(v); huecos[v] = h; Sigue(v, h); }
                    c = h;
                }
                if (dueno != null) dueno.Controls.Add(c);
            }
            // (panel-del-disco) Controls.Add de un rotulo (Label) a una vista que ya esta en una ventana de la capa: al hueco de la vista
            public void AgregaRotulo(IVista dueno, object rotulo)
            {
                Control d = ControlDe(dueno), r = rotulo as Control;
                if (d != null && r != null) d.Controls.Add(r);
            }
            // Parent.ClientSize de una vista que es control de la capa o esta en un hueco: el cliente de su padre; false si no esta en ninguno
            public bool ClienteDelDueno(IVista v, out Size cliente)
            {
                Control c = ControlDe(v); Control p = c != null ? c.Parent : null;
                if (p == null) { cliente = Size.Empty; return false; }
                cliente = p.ClientSize; return true;
            }
            public void AlFrente(IVista v) { Control c = ControlDe(v); if (c != null) c.BringToFront(); }
            public void AlFondo(IVista v) { Control c = ControlDe(v); if (c != null) c.SendToBack(); }
            public void Quita(IVista v)
            {
                Control c = ControlDe(v); if (c == null) return;
                Control p = c.Parent; if (p != null) p.Controls.Remove(c);
                HuecoDeVista h = c as HuecoDeVista;
                if (h != null) { huecos.Remove(v); h.Quitado = true; h.Dispose(); }   // (Controls.Remove no desecha la vista: solo se va su hueco)
            }
            public void Cambio(IVista v, bool repintar)
            {
                HuecoDeVista h; if (!huecos.TryGetValue(v, out h)) return;
                Sigue(v, h);
                if (repintar) h.Invalidate();
            }
            // el hueco toma el sitio y el visible propio de su vista (SetBounds y Visible del control: lo mismo que haria la vista si heredara)
            private void Sigue(IVista v, HuecoDeVista h)
            {
                Rectangle r = v.Bounds;
                if (h.Bounds != r) h.SetBounds(r.X, r.Y, r.Width, r.Height);
                soloLaSuya = true; bool ve; try { ve = v.Visible; } finally { soloLaSuya = false; }
                if (h.VisiblePropio != ve) h.Visible = ve;
            }
            public bool DuenoVisible(IVista v)
            {
                if (soloLaSuya) return true;
                Control c = ControlDe(v); Control p = c != null ? c.Parent : null;
                return p == null || p.Visible;
            }
            // la pintura de un control (WM_PAINT: PintaFondoYContenido, con su «X» roja si lanza) en el lienzo de quien la pide, con la
            // matriz que ese lienzo ya lleva como la del HDC
            public void Pinta(IVista v, SKCanvas g)
            {
                Control c = v as Control; if (c == null) return;
                using (Graphics gr = PuenteDibujo.Graphics(g, g.TotalMatrix)) c.PintaFondoYContenido(gr, c.ClientRectangle);
            }
            // Focus de la vista (armado-vistas): el de la capa, en el control mismo o en su hueco
            public bool Enfoca(IVista v) { Control c = ControlDe(v); return c != null && c.Focus(); }
            // quien escucha el raton de una vista que aun es control: su evento de la capa (MouseDown, MouseClick), que la capa lanza en el
            // On* de fabrica como WinForms; el MouseEventArgs pasa como Raton con los mismos numeros (como el hueco: R)
            public bool OyeComoControl(IVista v, AvisoRaton que, Action<Raton> a)
            {
                Control c = v as Control; if (c == null) return false;
                MouseEventHandler h = delegate (object s, MouseEventArgs e) { a(HuecoDeVista.R(e)); };
                if (que == AvisoRaton.Abajo) c.MouseDown += h; else if (que == AvisoRaton.Mueve) c.MouseMove += h; else if (que == AvisoRaton.Rueda) c.MouseWheel += h; else c.MouseClick += h;   // (gpupanel-sin-control: tambien MouseMove y MouseWheel)
                return true;
            }
            public ILugarDelVideo LugarDelVideo(object ventana) { return new VideoEnVentanas().Lugar(ventana); }
            // Present de la tarjeta en una hoja (la ventana de prueba F12): su cliente guarda el fondo y el escritorio compone con el. (gpupanel-
            // sin-control) Ya no hay ningun control de la capa con un dispositivo: el GpuPanel es una vista (PresentaEnVista); sin hoja, glFlush.
            public bool Presenta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera)
            {
                Hoja hoja = Hoja.DeLaVentana(ventana);   // (port nativo, f12-como-hoja) la ventana de prueba F12 es una hoja: su cliente
                if (hoja != null) { hoja.PresentaTarjeta(textura, ancho, alto); Escritorio.SinEspera = sinEspera; Escritorio.PresentaNativa(null); return true; }
                GlMas.Flush(); return false;
            }
            public void OlvidaTarjeta(IntPtr ventana) { Hoja hoja = Hoja.DeLaVentana(ventana); if (hoja != null) hoja.OlvidaTarjeta(); }
            // (gpupanel-sin-control) Present en la superficie de una vista de la tarjeta que esta en un hueco: lo que hacia antes con el control
            // (Control.FromHandle del Handle del GpuPanel): el hueco («Nativa») guarda el fondo y el escritorio compone con el
            public bool PresentaEnVista(IVista v, uint textura, int ancho, int alto, bool sinEspera)
            {
                Control control = ControlDe(v);
                if (control == null) { GlMas.Flush(); return false; }
                control.TexturaGl = textura; control.AnchoGl = ancho; control.AltoGl = alto;
                Escritorio.SinEspera = sinEspera;
                Escritorio.PresentaNativa(control);
                return true;
            }
            public void OlvidaEnVista(IVista v) { Control control = ControlDe(v); if (control != null) control.TexturaGl = 0; }
            // (gpupanel-sin-control) Control.FindForm del hueco (la Form de arriba de la cadena de padres) y si esta minimizada
            public bool VentanaMinimizada(IVista v)
            {
                Control control = ControlDe(v); Form ventana = control != null ? control.FindForm() : null;
                return ventana != null && ventana.WindowState == FormWindowState.Minimized;
            }
            public void HayQueComponer() { Escritorio.Mueve(); }
            public void Tecla(Tecla tecla, bool abajo) { Escritorio.Tecla(tecla, abajo); }
            public void RatonEn(int x, int y) { Escritorio.RatonEn(x, y); }
            public void RatonBoton(BotonRaton b, bool abajo) { Escritorio.RatonBoton((MouseButtons)(int)b, abajo); }
            public void RatonRueda(int muescas) { Escritorio.RatonRueda(muescas); }
            public void FinDeSesion() { Escritorio.FinDeSesion(); }
        }

        // EL HUECO DE UNA VISTA QUE YA NO HEREDA DE CONTROL, en la lista de controles de una ventana de la capa (como HuecoDelVideo,
        // VideoEnVentanas.cs): ocupa su lugar en el orden, la capa lo pinta y le da el raton con las reglas de WinForms que ya cumple, y el
        // hueco se lo pasa a la vista. El fondo lo pinta la vista (su Pinta es OnPaintBackground y OnPaint). El codigo de XP no lo ve.
        // (armado-vistas) Los que escuchan el raton de la vista desde fuera (PantallaRocola.OyeRaton: los eventos MouseDown y MouseClick de XP)
        // van despues de su Raton, donde WinForms lanza el evento (el On* de fabrica). Al desecharse con su ventana (Control.Dispose desecha a
        // los hijos), desecha a su vista si lo es (IDisposable: el Dispose(bool) de XP de esa vista) antes de lo suyo, en el orden de la
        // vista que heredaba (lo suyo y despues base.Dispose); quitado de la lista (Controls.Remove) no, que WinForms no desecha al quitar.
        // Las trazas lo nombran por su vista (Vista: TrazaVentanas.Pieza), como cuando la vista era el control.
        private sealed class HuecoDeVista : Control
        {
            private readonly IVista vista;
            internal bool Quitado;
            internal IVista Vista { get { return vista; } }
            // (panel-del-disco) el hueco de una vista que no toma el foco del teclado es un Panel: ni Selectable ni TabStop; (vista-ayuda,
            // revision) si no, el TabStop que XP le pone a la vista, como si heredara
            // (gpupanel-sin-control) el de una vista de la tarjeta es «Nativa» como lo era el GpuPanel: el escritorio deja su hueco y compone ahi
            // lo que la tarjeta presento (TexturaGl, PresentaEnVista), y su WM_PAINT llama a OnPaint (Escritorio.PintaYa): Pinta de la vista
            internal HuecoDeVista(IVista v) { vista = v; if (!v.Seleccionable) SetStyle(ControlStyles.Selectable, false); TabStop = v.Seleccionable && v.ParaElTabulador; Nativa = v.DeLaTarjeta; }
            protected override void OnPaintBackground(PaintEventArgs e) { }
            protected override void OnPaint(PaintEventArgs e) { vista.Pinta(PuenteDibujo.Lienzo(e.Graphics)); base.OnPaint(e); }
            internal static Raton R(MouseEventArgs e) { return new Raton((BotonRaton)(int)e.Button, e.Clicks, e.X, e.Y, e.Delta); }
            protected override void OnMouseDown(MouseEventArgs e) { Raton r = R(e); vista.Raton(AvisoRaton.Abajo, r); PantallaRocola.AvisaOyentes(vista, AvisoRaton.Abajo, r); base.OnMouseDown(e); }
            protected override void OnMouseClick(MouseEventArgs e) { Raton r = R(e); vista.Raton(AvisoRaton.Clic, r); PantallaRocola.AvisaOyentes(vista, AvisoRaton.Clic, r); base.OnMouseClick(e); }
            protected override void Dispose(bool disposing)
            {
                if (!disposing || Quitado || IsDisposed || Disposing) { base.Dispose(disposing); return; }
                IDisposable d = vista as IDisposable;
                try { if (d != null) d.Dispose(); }
                finally { base.Dispose(disposing); }
            }
            protected override void OnMouseDoubleClick(MouseEventArgs e) { vista.Raton(AvisoRaton.DobleClic, R(e)); base.OnMouseDoubleClick(e); }
            protected override void OnMouseUp(MouseEventArgs e) { vista.Raton(AvisoRaton.Arriba, R(e)); base.OnMouseUp(e); }
            protected override void OnMouseMove(MouseEventArgs e) { Raton r = R(e); vista.Raton(AvisoRaton.Mueve, r); PantallaRocola.AvisaOyentes(vista, AvisoRaton.Mueve, r); base.OnMouseMove(e); }   // (gpupanel-sin-control: los que oyen su MouseMove)
            protected override void OnMouseEnter(EventArgs e) { vista.Raton(AvisoRaton.Entra, new Raton(BotonRaton.None, 0, 0, 0, 0)); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { vista.Raton(AvisoRaton.Sale, new Raton(BotonRaton.None, 0, 0, 0, 0)); base.OnMouseLeave(e); }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                Raton r = R(e);
                vista.Raton(AvisoRaton.Rueda, r);
                PantallaRocola.AvisaOyentes(vista, AvisoRaton.Rueda, r);   // (gpupanel-sin-control: los que oyen su MouseWheel, antes de mirar Handled como WmMouseWheel)
                HandledMouseEventArgs h = e as HandledMouseEventArgs; if (h != null && r.Handled) h.Handled = true;
                base.OnMouseWheel(e);
            }
        }
    }
}

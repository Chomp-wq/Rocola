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
        private readonly Dictionary<Hoja, object> clavesHoja = new Dictionary<Hoja, object>();
        // LAS CAPAS: las de las ventanas de la capa (Escritorio.Capas: cada ventana en tramos de Dibujo2D partidos por sus controles de la
        // tarjeta y del video, en el orden de las ventanas; sin las hojas nativas, que van aparte), la lista nativa de la ventana (lo que no
        // es de ninguna ventana de la capa: hoy nada) y las hojas nativas, encima de todo, en su orden. Es el mismo orden que daba
        // Escritorio.Capas (las hojas al final) y las mismas capas: se compone lo mismo.
        private List<CapaPantalla> Capas()
        {
            List<CapaPantalla> r = new List<CapaPantalla>();
            foreach (Capa c in Escritorio.Capas())
            {
                if (c.Nativa == null && c.Imagen == null) continue;   // (una hoja nativa: va abajo, de la pantalla)
                CapaPantalla p = new CapaPantalla();
                p.Clave = c; p.Rect = c.Rect; p.Opacidad = c.Opacidad;
                if (c.Nativa != null)
                {
                    Control n = c.Nativa;
                    p.DeTextura = true; p.Textura = n.TexturaGl; p.AnchoGl = n.AnchoGl; p.AltoGl = n.AltoGl; p.Rgba = n.TexturaGlRgba; p.Nombre = n.GetType().Name; p.Tipo = "tarjeta";
                }
                else { p.Mapa = c.Imagen.Sk; p.Version = c.Version; p.Tipo = "dibujo2d"; }
                r.Add(p);
            }
            PantallaRocola.CapasDeLaVentana(r);
            // LAS HOJAS NATIVAS, encima de todas (Nativo/Hoja.cs): cada una en su superficie, que se repinta entera solo si algo cambio (su
            // Version: la que se pinto, Hoja.Superficie), en el orden de las hojas
            foreach (Hoja h in Hoja.Abiertas)
            {
                if (h.IsDisposed || !h.Visible) continue;
                object k; if (!clavesHoja.TryGetValue(h, out k)) clavesHoja[h] = k = new object();
                CapaPantalla p = new CapaPantalla();
                int v; p.Mapa = h.Superficie(out v); p.Version = v; p.Rect = h.Bounds; p.Clave = k; p.Tipo = "hoja";
                r.Add(p);
            }
            foreach (Hoja h in new List<Hoja>(clavesHoja.Keys)) if (h.IsDisposed || !h.Visible) clavesHoja.Remove(h);
            return r;
        }

        // ---------- (transicion) LO QUE LA PANTALLA NATIVA LE PIDE A LAS VENTANAS DE LA CAPA ----------
        // Cada cosa es la llamada de la capa que hacia lo mismo antes de la pantalla nativa (Escritorio y Control), asi la rocola se ve, se
        // mueve y recibe la entrada igual. Se va con la capa (N7).
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
                Control c = v as Control;
                if (c == null)
                {
                    HuecoDeVista h;
                    if (!huecos.TryGetValue(v, out h)) { h = new HuecoDeVista(v); huecos[v] = h; Sigue(v, h); }
                    c = h;
                }
                if (dueno != null) dueno.Controls.Add(c);
            }
            public void AlFrente(IVista v) { Control c = ControlDe(v); if (c != null) c.BringToFront(); }
            public void AlFondo(IVista v) { Control c = ControlDe(v); if (c != null) c.SendToBack(); }
            public void Quita(IVista v)
            {
                Control c = ControlDe(v); if (c == null) return;
                Control p = c.Parent; if (p != null) p.Controls.Remove(c);
                HuecoDeVista h = c as HuecoDeVista;
                if (h != null) { huecos.Remove(v); h.Dispose(); }
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
            public ILugarDelVideo LugarDelVideo(object ventana) { return new VideoEnVentanas().Lugar(ventana); }
            // Present de la tarjeta: lo que hacia PantallaTarjeta antes de la pantalla nativa (el control de la ventana guarda el fondo y el
            // escritorio compone con el)
            public bool Presenta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera)
            {
                Control control = Control.FromHandle(ventana);
                if (control == null) { GlMas.Flush(); return false; }
                control.TexturaGl = textura; control.AnchoGl = ancho; control.AltoGl = alto;
                Escritorio.SinEspera = sinEspera;
                Escritorio.PresentaNativa(control);
                return true;
            }
            public void OlvidaTarjeta(IntPtr ventana) { Control control = Control.FromHandle(ventana); if (control != null) control.TexturaGl = 0; }
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
        private sealed class HuecoDeVista : Control
        {
            private readonly IVista vista;
            internal HuecoDeVista(IVista v) { vista = v; }
            protected override void OnPaintBackground(PaintEventArgs e) { }
            protected override void OnPaint(PaintEventArgs e) { vista.Pinta(PuenteDibujo.Lienzo(e.Graphics)); base.OnPaint(e); }
            private static Raton R(MouseEventArgs e) { return new Raton((BotonRaton)(int)e.Button, e.Clicks, e.X, e.Y, e.Delta); }
            protected override void OnMouseDown(MouseEventArgs e) { vista.Raton(AvisoRaton.Abajo, R(e)); base.OnMouseDown(e); }
            protected override void OnMouseClick(MouseEventArgs e) { vista.Raton(AvisoRaton.Clic, R(e)); base.OnMouseClick(e); }
            protected override void OnMouseDoubleClick(MouseEventArgs e) { vista.Raton(AvisoRaton.DobleClic, R(e)); base.OnMouseDoubleClick(e); }
            protected override void OnMouseUp(MouseEventArgs e) { vista.Raton(AvisoRaton.Arriba, R(e)); base.OnMouseUp(e); }
            protected override void OnMouseMove(MouseEventArgs e) { vista.Raton(AvisoRaton.Mueve, R(e)); base.OnMouseMove(e); }
            protected override void OnMouseEnter(EventArgs e) { vista.Raton(AvisoRaton.Entra, new Raton(BotonRaton.None, 0, 0, 0, 0)); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { vista.Raton(AvisoRaton.Sale, new Raton(BotonRaton.None, 0, 0, 0, 0)); base.OnMouseLeave(e); }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                Raton r = R(e);
                vista.Raton(AvisoRaton.Rueda, r);
                HandledMouseEventArgs h = e as HandledMouseEventArgs; if (h != null && r.Handled) h.Handled = true;
                base.OnMouseWheel(e);
            }
        }
    }
}

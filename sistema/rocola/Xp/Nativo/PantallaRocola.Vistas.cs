// LA PANTALLA DE LA ROCOLA: LAS VISTAS, LA COMPOSICION Y LA ENTRADA (port nativo, tanda 4, grupo pantalla-nativa: W2). Lo que en XP hacian
// Windows y WinForms con los controles de la ventana de la rocola: el orden en que se ven (Controls, BringToFront, SendToBack), el recorte
// de cada uno por los de encima (WS_CLIPSIBLINGS), a quien le toca el raton (la ventana de encima bajo la flecha, la captura del boton, el
// clic y el doble clic) y a quien la tecla. Solo lo que la rocola de XP usa, con las reglas de WinForms (dotnet/winforms release/3.0,
// portada de .NET Framework 4.8: Control.cs y Control.ControlCollection.cs; /tmp/claude-0/agente-winforms/ref) y de Win32.
//
// LA LISTA DE LA VENTANA: lo que la rocola pone en su ventana, el de mas arriba primero (Controls[0], como WinForms): vistas (IVista,
// pintadas por procesador) y capas de textura (CapaDeTextura: el fondo de la tarjeta, el video; en XP, el GpuPanel de Direct3D y el control
// del WMP). Ahi van tambien los rotulos de encima del panel (lblAttract, lblConfig, lblGpu: oleada 7) cuando dejen de ser Label.
//
// TRANSICION (mientras queden las ventanas de la capa: Plataforma/Ventanas.*.cs). La rocola aun es una ventana de la capa y sus vistas
// aun son controles de la capa: la capa decide su orden, su pintura y su raton (lo que fijan las 41 pruebas), y la pantalla se lo pide por
// LaCapa (lo pone Plataforma/Anfitrion.cs). Una vista que ya no hereda de Control pero cuyo dueno es una ventana de la capa entra en la
// lista de controles de esa ventana con un hueco (como el video hoy: HuecoDelVideo, VideoEnVentanas.cs), asi su orden, su recorte y su raton
// son los de WinForms entre los controles que quedan; la capa la pinta llamando a su Pinta y le da el raton por su Raton. Lo que no es de
// ninguna ventana de la capa va a la lista nativa de la pantalla. Las teclas y el raton de evdev (TeclasLinux.cs) llegan aqui y, con la
// capa, se los da a ella (que ya da a las hojas nativas lo suyo); sin la capa, la pantalla los reparte con las reglas de abajo.
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    // UNA CAPA DE TEXTURA EN LA LISTA DE LA VENTANA (el fondo de la tarjeta; el video): su sitio en la ventana, si se ve y la textura
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
            bool DuenoVisible(IVista v);                             // si se ve la ventana (o el control) en que esta
            void Pinta(IVista v, SKCanvas g);                        // pintar una vista que aun es control de la capa (OnPaintBackground y OnPaint)
            bool Enfoca(IVista v);                                   // Focus de una vista que es control de la capa o esta en un hueco (Control.Focus)
            bool OyeComoControl(IVista v, AvisoRaton que, Action<Raton> a);   // una vista que aun es control: su evento (MouseDown, MouseClick); false si no lo es
            ILugarDelVideo LugarDelVideo(object ventana);            // la capa del video en una ventana de la capa (VideoEnVentanas.cs)
            bool Presenta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera);   // Present de la tarjeta en un control de la capa (false: no es de nadie, glFlush)
            void OlvidaTarjeta(IntPtr ventana);
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
            ILaCapa c = LaCapa;
            if (c != null && (c.EsSuya(ventana) || c.EsSuyaLaVista(v))) { c.Agrega(ventana, v); return; }
            if (lista.Contains(v)) return;   // (Add de uno que ya esta: no hace nada mas que dejarlo donde estaba, ControlCollection.Add)
            lista.Add(v); CambioVentana();
        }
        internal static void AgregaCapa(CapaDeTextura t) { if (!lista.Contains(t)) { lista.Add(t); CambioVentana(); } }
        // BringToFront: el [0] (Control.BringToFront: «ParentInternal.Controls.SetChildIndex(this, 0)», Control.cs)
        internal static void VistaAlFrente(object v)
        {
            IVista iv = v as IVista; ILaCapa c = LaCapa;
            if (iv != null && c != null && c.EsSuyaLaVista(iv)) { c.AlFrente(iv); return; }
            if (lista.Remove(v)) { lista.Insert(0, v); CambioVentana(); }
        }
        // SendToBack: el ultimo (Control.SendToBack: «ParentInternal.Controls.SetChildIndex(this, -1)»)
        internal static void VistaAlFondo(object v)
        {
            IVista iv = v as IVista; ILaCapa c = LaCapa;
            if (iv != null && c != null && c.EsSuyaLaVista(iv)) { c.AlFondo(iv); return; }
            if (lista.Remove(v)) { lista.Add(v); CambioVentana(); }
        }
        // Controls.Remove (Control.ControlCollection.Remove): fuera; si tenia el raton o la captura, ya no (el que esta debajo lo recibe al
        // moverse la flecha)
        internal static void QuitaVista(object v)
        {
            IVista iv = v as IVista; ILaCapa c = LaCapa;
            if (iv != null && c != null && c.EsSuyaLaVista(iv)) { c.Quita(iv); return; }
            if (!lista.Remove(v)) return;
            if (bajoRaton == v) bajoRaton = null;
            if (captura == v) captura = null;
            if (foco == v) foco = null;
            CambioVentana();
        }
        // el orden de una (0 arriba), o -1 si no esta en la lista nativa
        internal static int IndiceDe(object v) { return lista.IndexOf(v); }
        internal static object[] ListaDeLaVentana { get { return lista.ToArray(); } }
        // lo que avisa una vista que ya no hereda (CajaVista): en la lista nativa, volver a componer; en un hueco de la capa, la capa
        internal static void VistaCambio(IVista v, bool repintar)
        {
            if (lista.Contains(v)) { CambioVentana(); return; }
            ILaCapa c = LaCapa; if (c != null) c.Cambio(v, repintar);
        }
        internal static void CambioVentana() { cambiosVentana++; HayQueComponer(); }
        // si se ve el dueno de una vista (GetVisibleCore del padre): la ventana de la pantalla siempre se ve; en la capa, lo dice la capa
        internal static bool DuenoVisible(IVista v)
        {
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
        // a la ventana, que tiene KeyPreview -el foco del teclado de una vista es de la oleada 6c-). Una vista no tiene Enabled: siempre lo esta.
        internal static bool EnfocaVista(IVista v)
        {
            ILaCapa c = LaCapa;
            if (c != null && c.EsSuyaLaVista(v)) return c.Enfoca(v);
            if (v != null && lista.Contains(v) && Ve(v) && foco != v)
            {
                foco = v;
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("foco " + v.GetType().Name);
            }
            return v != null && foco == v;
        }
        // LOS QUE ESCUCHAN EL RATON DE UNA VISTA DESDE FUERA (en XP, los eventos de Control: «ayudaView.MouseClick += ...» y
        // «attractView.MouseDown += ...», Form1.cs). En WinForms el evento lo lanza el On* de fabrica (Control.OnMouseDown: «MouseEventHandler
        // handler = (MouseEventHandler)Events[EventMouseDown]; if (handler != null) handler(this, e)»; igual OnMouseClick), asi que llega despues
        // de lo que la vista hace en su On* antes de llamar a base: aqui, despues de su Raton. Solo los dos que usa la rocola (Abajo = MouseDown,
        // Clic = MouseClick). En una vista que aun es control de la capa, su evento (la capa lo lanza en el mismo sitio); si no, esta lista,
        // que avisan la pantalla (Avisa) y el hueco de la capa (Plataforma/Anfitrion.cs) despues de Raton.
        private static readonly Dictionary<IVista, List<KeyValuePair<AvisoRaton, Action<Raton>>>> oyentes = new Dictionary<IVista, List<KeyValuePair<AvisoRaton, Action<Raton>>>>();
        internal static void OyeRaton(IVista v, AvisoRaton que, Action<Raton> a)
        {
            if (que != AvisoRaton.Abajo && que != AvisoRaton.Clic) throw new ArgumentException("solo MouseDown (Abajo) y MouseClick (Clic): lo que usa la rocola", "que");
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
            // los cortes: las capas de textura que se ven, de abajo arriba
            List<int> cortes = new List<int>();
            for (int i = l.Length - 1; i >= 0; i--) { CapaDeTextura t = l[i] as CapaDeTextura; if (t != null && t.Visible) cortes.Add(i); }
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
                        for (int i = hasta; i > corte; i--) { IVista v = l[i] as IVista; if (v != null) PintaRecortada(l, i, v, g); }
                        g.Flush();
                    }
                    tr.Version = ++versionTramo;
                }
                CapaPantalla c = new CapaPantalla();
                c.Clave = tr; c.Mapa = tr.Mapa; c.Version = tr.Version; c.Rect = new Rectangle(0, 0, Ancho, Alto); c.Tipo = "vista";
                salida.Add(c);
                if (corte >= 0)
                {
                    CapaDeTextura x = (CapaDeTextura)l[corte];
                    CapaPantalla ct = new CapaPantalla();
                    ct.Clave = x; ct.DeTextura = true; ct.Textura = x.Textura; ct.AnchoGl = x.AnchoGl; ct.AltoGl = x.AltoGl; ct.Rgba = x.Rgba; ct.Rect = x.Sitio; ct.Nombre = x.Nombre; ct.Tipo = "tarjeta";
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
                for (int j = i - 1; j >= 0; j--)
                {
                    Rectangle rs; bool ve;
                    IVista o = l[j] as IVista; CapaDeTextura t = l[j] as CapaDeTextura;
                    if (o != null) { rs = o.Bounds; ve = Ve(o); } else { rs = t.Sitio; ve = t.Visible; }
                    if (!ve || rs.Width <= 0 || rs.Height <= 0 || !rs.IntersectsWith(r)) continue;
                    g.ClipRect(new SKRect(rs.Left, rs.Top, rs.Right, rs.Bottom), SKClipOperation.Difference);
                }
                g.Translate(r.Left, r.Top);
                v.Pinta(g);
            }
            catch (Exception ex) { Falla(ex); }   // (lo que lanza un OnPaint va a ThreadException: NativeWindow.Callback)
            finally { g.RestoreToCount(s); }
        }
        // si se ve la vista misma (su Visible incluye al dueno, que en la lista nativa es la pantalla)
        private static bool Ve(IVista v) { return v.Visible; }

        // ---------- el raton ----------
        // LA DE ENCIMA BAJO LA FLECHA (WindowFromPoint y ChildWindowFromPointEx: la ventana hija de mas arriba que contiene el punto y se
        // ve; Control.cs:5806). Una capa de textura tambien es una ventana (el GpuPanel, el control del WMP): se queda con el raton aunque no
        // haga nada con el.
        internal static object VistaEn(Point p)
        {
            foreach (object o in lista)
            {
                IVista v = o as IVista;
                if (v != null) { if (Ve(v) && v.Bounds.Contains(p)) return v; continue; }
                CapaDeTextura t = (CapaDeTextura)o;
                if (t.Visible && t.Sitio.Contains(p)) return t;
            }
            return null;
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
            Rectangle r = v.Bounds;
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
            IVista v = foco as IVista;
            if (v != null)
            {
                Rectangle r = v.Bounds;
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
            internal Func<Tecla, bool> TeclaDeDialogo;   // ProcessDialogKey (de una ventana sin AcceptButton ni CancelButton: false)
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
        // soltar, KeyUp. (Las vistas de la ventana no toman el foco del teclado: en la rocola la ventana tiene KeyPreview y ve cada tecla
        // antes que nadie; el foco de una vista -el buscador- llega en la oleada 6.)
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
        // Present de Direct3D 9 en la ventana 'ventana' (PantallaTarjeta.cs): en un control de la capa, la capa; en la pantalla nativa, la
        // capa de textura de la tarjeta (la pone el panel de la tarjeta cuando deje de ser control: oleada 6); sin ninguna, glFlush.
        internal static Func<IntPtr, CapaDeTextura> TarjetaDe;
        internal static bool SinEspera;   // el medidor de Ctrl+F3 pide presentar sin esperar al barrido
        internal static double UltimaPresentacionTarjeta = -1e9;
        internal static Action Componer;  // compone y presenta (lo pone quien abre la pantalla)
        // (true si se compuso la pantalla: OpenGL ya no es lo del dispositivo)
        internal static bool PresentaTarjeta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera)
        {
            ILaCapa c = LaCapa;
            Func<IntPtr, CapaDeTextura> f = TarjetaDe;
            CapaDeTextura t = f != null ? f(ventana) : null;
            if (t == null)
            {
                if (c != null) return c.Presenta(ventana, textura, ancho, alto, sinEspera);
                GlMas.Flush(); return false;
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
            Func<IntPtr, CapaDeTextura> f = TarjetaDe;
            CapaDeTextura t = f != null ? f(ventana) : null;
            if (t != null) { t.Textura = 0; return; }
            ILaCapa c = LaCapa; if (c != null) c.OlvidaTarjeta(ventana);
        }

        // ---------- el pintado sin la capa (WM_PAINT de la ventana de la pantalla) ----------
        // Lo que hoy da la capa (Escritorio.PintaUno, HaySucio y ProximoPintado): componer si algo cambio y la tarjeta no esta presentando
        // (o hace mas de 100 ms que no: si presenta, compone ella en cada cuadro). Lo pone quien abre la pantalla cuando no hay capa.
        internal static void PintadoSinLaCapa()
        {
            PintaUno = delegate { if (!ComposicionPendiente()) return false; compuesto = cambios; try { Action a = Componer; if (a != null) a(); } catch (Exception ex) { Falla(ex); } return true; };
            HaySucio = ComposicionPendiente;
            ProximoPintado = delegate { return Componer != null && cambios != compuesto ? UltimaPresentacionTarjeta + 100.5 : double.MaxValue; };
        }
        private static bool ComposicionPendiente() { return Componer != null && cambios != compuesto && AhoraMs - UltimaPresentacionTarjeta > 100; }
    }
}

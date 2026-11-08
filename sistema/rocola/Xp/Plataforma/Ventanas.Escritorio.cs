// EL ESCRITORIO DE ROCOLA OS: lo que en XP hace Windows con las ventanas de la rocola (traduccion de XP, fase 1). Un hilo de pantalla
// con su cola (lo que otros hilos piden con BeginInvoke, las teclas, los temporizadores), las ventanas una encima de otra y el foco.
// La pantalla la pone el anfitrion (KMS y OpenGL ES 2); el escritorio le da las capas: lo que se pinta con Dibujo2D, en imagenes que
// solo se repintan si algo cambio, y los huecos que pinta la tarjeta (los controles «Nativa»: el GpuPanel de XP).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace Rocola.Ventanas
{
    public interface IAnfitrion
    {
        // Pinta las capas en la pantalla y la presenta (lo llama el escritorio cuando cambia algo y la tarjeta no esta presentando).
        void Compone();
    }

    public sealed class Capa
    {
        public Rectangle Rect;      // en la pantalla
        public Bitmap Imagen;       // Dibujo2D (null en un hueco de la tarjeta)
        public int Version;         // cambia cuando la imagen se repinto
        public Control Nativa;      // el control que pinta la tarjeta
        public float Opacidad = 1f;
    }

    public static class Escritorio
    {
        public static IAnfitrion Anfitrion;
        public static Size Pantalla = new Size(1024, 768);
        public static int RefrescoHz = 60;   // lo pone el anfitrion (KMS: el del modo de la pantalla)
        public static Thread HiloUi;
        private static readonly Stopwatch reloj = Stopwatch.StartNew();
        public static double AhoraMs { get { return reloj.Elapsed.TotalMilliseconds; } }
        internal static Control Foco;
        internal static Form Activa;
        public static Keys Modificadores;
        public static MouseButtons BotonesRaton;
        public static readonly bool[] TeclasAbajo = new bool[256];   // lo que en XP pregunta GetKeyState/GetAsyncKeyState

        // ---------- la cola ----------
        public sealed class Pendiente : IAsyncResult
        {
            public Delegate Metodo; public object[] Args; public object Resultado; public Exception Error; public bool Espera;
            private readonly ManualResetEvent hecho = new ManualResetEvent(false);
            public volatile bool Completo;
            public void Termina() { Completo = true; hecho.Set(); }
            public object AsyncState { get { return null; } }
            public WaitHandle AsyncWaitHandle { get { return hecho; } }
            public bool CompletedSynchronously { get { return false; } }
            public bool IsCompleted { get { return Completo; } }
        }
        private static readonly object cerrojo = new object();
        private static readonly Queue<Pendiente> cola = new Queue<Pendiente>();
        private static readonly Queue<Action> entrada = new Queue<Action>();
        private static readonly AutoResetEvent senal = new AutoResetEvent(false);
        public static Pendiente Encola(Delegate d, object[] args, bool espera)
        {
            Pendiente p = new Pendiente(); p.Metodo = d; p.Args = args; p.Espera = espera;
            lock (cerrojo) cola.Enqueue(p);
            senal.Set();
            return p;
        }
        // La entrada (teclas y raton) la manda el anfitrion desde su hilo.
        public static void Entrada(Action a) { lock (cerrojo) entrada.Enqueue(a); senal.Set(); }
        public static void Despierta() { senal.Set(); }
        internal static object Llama(Delegate d, object[] args)
        {
            Action a = d as Action; if (a != null) { a(); return null; }
            MethodInvoker mi = d as MethodInvoker; if (mi != null) { mi(); return null; }
            EventHandler eh = d as EventHandler; if (eh != null) { eh(args != null && args.Length > 0 ? args[0] : null, args != null && args.Length > 1 ? (EventArgs)args[1] : EventArgs.Empty); return null; }
            return d.DynamicInvoke(args);
        }

        // ---------- los temporizadores ----------
        private static readonly List<Timer> timers = new List<Timer>();
        internal static void AgregaTimer(Timer t) { lock (cerrojo) if (!timers.Contains(t)) timers.Add(t); senal.Set(); }
        internal static void QuitaTimer(Timer t) { lock (cerrojo) timers.Remove(t); }
        private static double ProximoTimer()
        {
            double m = double.MaxValue;
            lock (cerrojo) foreach (Timer t in timers) if (t.Proximo < m) m = t.Proximo;
            return m;
        }

        // ---------- lo que hay pendiente (PeekMessage de XP) ----------
        private static readonly List<Control> nativasPorPintar = new List<Control>();
        public static bool HayMensajes()
        {
            lock (cerrojo) { if (cola.Count > 0 || entrada.Count > 0 || nativasPorPintar.Count > 0) return true; }
            return ProximoTimer() <= AhoraMs;
        }
        // Espera hasta ms o hasta que llegue algo (MsgWaitForMultipleObjects de XP). true si llego algo.
        public static bool EsperaMensajes(int ms)
        {
            if (HayMensajes()) return true;
            double t = ProximoTimer() - AhoraMs;
            if (t < ms) ms = Math.Max(0, (int)Math.Ceiling(t));
            senal.WaitOne(ms);
            return HayMensajes();
        }

        // Atiende todo lo pendiente: la entrada, lo pedido por otros hilos, los temporizadores vencidos y la pintura de los controles de
        // la tarjeta.
        public static void Bombea()
        {
            while (true)
            {
                Action a = null;
                lock (cerrojo) if (entrada.Count > 0) a = entrada.Dequeue();
                if (a == null) break;
                Protege(a);
            }
            int n; lock (cerrojo) n = cola.Count;
            for (int i = 0; i < n; i++)
            {
                Pendiente p; lock (cerrojo) { if (cola.Count == 0) break; p = cola.Dequeue(); }
                try { p.Resultado = Llama(p.Metodo, p.Args); }
                catch (Exception ex) { if (p.Espera) p.Error = ex; else Application.Avisa(ex); }
                p.Termina();
            }
            double ahora = AhoraMs; Timer[] vencidos = null;
            lock (cerrojo)
            {
                List<Timer> l = null;
                foreach (Timer t in timers) if (t.Proximo <= ahora) (l ?? (l = new List<Timer>())).Add(t);
                if (l != null) vencidos = l.ToArray();
            }
            if (vencidos != null)
                foreach (Timer t in vencidos)
                {
                    if (!t.Enabled) continue;
                    t.Proximo += t.Interval; if (t.Proximo <= ahora) t.Proximo = ahora + t.Interval;
                    Protege(t.Dispara);
                }
            Control[] np = null;
            lock (cerrojo) if (nativasPorPintar.Count > 0) { np = nativasPorPintar.ToArray(); nativasPorPintar.Clear(); }
            if (np != null) foreach (Control c in np) PintaYa(c);
        }
        private static void Protege(Action a) { try { a(); } catch (Exception ex) { Application.Avisa(ex); } }

        // ---------- las ventanas ----------
        private static readonly List<Form> formas = new List<Form>();   // la ultima, la de mas arriba
        internal static void Agrega(Form f) { lock (formas) { if (!formas.Contains(f)) formas.Add(f); } Ensucia(f); }
        // Al esconderse una ventana (al cerrar una hoja), se activa la de debajo y el foco vuelve a su control activo, como en Windows.
        internal static void Oculta(Form f)
        {
            lock (formas) formas.Remove(f);
            bool teniaFoco = Foco != null && (Foco == f || f.Contains(Foco));
            if (teniaFoco) { Control antes = Foco; Foco = null; if (!antes.IsDisposed) Protege(antes.LlamaLostFocus); }
            if (Activa == f)
            {
                Form otra = null; lock (formas) for (int i = formas.Count - 1; i >= 0 && otra == null; i--) if (!formas[i].IsDisposed && formas[i].Visible) otra = formas[i];
                Activa = null;
                f.LlamaDesactivada();
                if (otra != null) ActivaForma(otra, true);
            }
            if (ncForma == f) { ncForma = null; ncCaptura = 0; }
            Ensucia(null);
        }
        internal static void Quita(Form f) { Oculta(f); lock (versiones) versiones.Remove(f); lock (modales) modales.Remove(f); }
        // Delante de todas; y las suyas (Owner: la hoja de Ctrl+F3 es de la rocola) siempre encima de su duena, como en Windows.
        internal static void AlFrente(Form f)
        {
            if (f == null) return;
            lock (formas) { if (formas.Remove(f)) { formas.Add(f); SubeLasSuyas(f, 0); } }
            Ensucia(null);
        }
        private static void SubeLasSuyas(Form f, int nivel)
        {
            if (nivel > 8) return;
            foreach (Form o in formas.ToArray()) if (o != f && o.Owner == f && formas.Remove(o)) { formas.Add(o); SubeLasSuyas(o, nivel + 1); }
        }
        public static Form[] Formas { get { lock (formas) return formas.ToArray(); } }
        // LA VENTANA ACTIVA (WM_ACTIVATE): la de antes se desactiva y la nueva, si 'enfoca', pone el foco donde lo tenia (su control activo,
        // si se ve), o en el primero del tabulador si no tenia, o en ella misma (Form.Active de WinForms).
        internal static void ActivaForma(Form f, bool enfoca)
        {
            if (f == null || f.IsDisposed) return;
            if (Activa != f)
            {
                Form antes = Activa; Activa = f;
                if (antes != null && !antes.IsDisposed) antes.LlamaDesactivada();
                Ensucia(antes); Ensucia(f);   // (la barra de titulo cambia de color)
                if (enfoca) f.EnfocaAlActivar();
                f.LlamaActivada();
            }
            else if (enfoca && (Foco == null || !(Foco == f || f.Contains(Foco)))) f.EnfocaAlActivar();
        }
        // EL FOCO, como Windows y WinForms. porTeclado (Select: Tab, flechas, ActiveControl): primero Leave/Enter y despues LostFocus/
        // GotFocus; con el raton o Focus(): LostFocus, Leave, Enter y GotFocus. Leave y Enter van por el camino de contenedores dentro de un
        // mismo formulario (cada uno recuerda su control activo; al volver a el, no se repiten).
        internal static void PonFoco(Control c) { PonFoco(c, false); }
        internal static void PonFoco(Control c, bool porTeclado)
        {
            if (c == null || c.IsDisposed) return;
            Form f = c as Form ?? c.FindForm();
            Control antes = Foco;
            Control activoNuevo = c == f ? null : c;
            Control activoAntes = f != null ? f.activo : null;
            if (antes == c && activoAntes == activoNuevo) return;
            if (f != null && f.Visible && Activa != f) ActivaForma(f, false);
            if (porTeclado) CambiaActivo(f, activoAntes, activoNuevo);
            Foco = c;
            if (antes != null && antes != c && !antes.IsDisposed) Protege(antes.LlamaLostFocus);
            if (!porTeclado) CambiaActivo(f, activoAntes, activoNuevo);
            if (antes != c) Protege(c.LlamaGotFocus);
            if (f != null) f.ActualizaBotonPorDefecto();
        }
        private static void CambiaActivo(Form f, Control de, Control a)
        {
            if (f == null) return;
            f.activo = a;
            if (de == a) return;
            List<Control> sale = new List<Control>(), entra = new List<Control>();
            for (Control x = de; x != null && x != f; x = x.Parent) { if (a != null && (x == a || x.Contains(a))) break; sale.Add(x); }
            for (Control x = a; x != null && x != f; x = x.Parent) { if (de != null && (x == de || x.Contains(de))) break; entra.Insert(0, x); }
            foreach (Control x in sale) if (!x.IsDisposed) Protege(x.LlamaLeave);
            foreach (Control x in entra) if (!x.IsDisposed) Protege(x.LlamaEnter);
        }
        internal static void QuitaFoco(Control c) { Control antes = Foco; Foco = null; if (antes != null && !antes.IsDisposed) Protege(antes.LlamaLostFocus); }

        // LAS VENTANAS MODALES (ShowDialog): mientras hay una abierta, Windows deshabilita las de debajo (no reciben el raton).
        private static readonly List<Form> modales = new List<Form>();
        private static Form ModalArriba { get { lock (modales) { for (int i = modales.Count - 1; i >= 0; i--) if (!modales[i].IsDisposed && modales[i].Visible) return modales[i]; } return null; } }
        // Una ventana que queda debajo de la modal de arriba esta deshabilitada (ShowDialog de WinForms): no toma el foco ni se activa.
        internal static bool Deshabilitada(Form f)
        {
            Form m = ModalArriba;
            if (m == null || f == null || f == m) return false;
            lock (formas) { int im = formas.IndexOf(m), i = formas.IndexOf(f); return im >= 0 && i >= 0 && i < im; }
        }

        // ---------- el raton (lo que en XP manda Windows: WM_MOUSEMOVE, WM_LBUTTONDOWN, WM_MOUSEWHEEL...) ----------
        // Como Windows y WinForms: el control de encima de todo bajo la flecha recibe el movimiento (y MouseEnter / MouseLeave al cambiar);
        // el que recibe el boton bajado se queda con el raton hasta soltarlo (la captura de Windows); al soltar dentro, Click y MouseClick
        // (o DoubleClick y MouseDoubleClick si es el segundo en 500 ms y a menos de 4 puntos, como XP), y despues MouseUp. La rueda va al
        // control con el foco (asi la manda XP), de 120 en 120. La flecha se ve si ShowCursor deja la cuenta en 0 o mas (Cursor.Show/Hide).
        public static Point PosicionRaton = new Point(1023, 767);
        internal static int VisibilidadCursor;
        public static bool CursorVisible { get { return VisibilidadCursor >= 0; } }
        private static Control bajoRaton, captura;
        private static double ultimoClicMs = -1e9; private static Point ultimoClicEn; private static MouseButtons ultimoClicBoton; private static Control ultimoClicControl;
        private static bool dobleEnCurso;
        internal const double DobleClicMs = 500; internal const int DobleClicPuntos = 4;
        // el control de encima de todo en un punto de la pantalla (las ventanas de arriba abajo; dentro, los hijos del primero al ultimo).
        // Con una ventana modal abierta, las de debajo estan deshabilitadas (como en Windows): ahi no hay nadie.
        public static Control ControlEn(Point p)
        {
            Form[] fs = Formas;
            Form modal = ModalArriba;
            for (int i = fs.Length - 1; i >= 0; i--)
            {
                Form f = fs[i];
                if (f.IsDisposed || !f.Visible || !f.Bounds.Contains(p)) { if (f == modal) return null; continue; }
                return HijoEn(f, p);
            }
            return null;
        }
        // EL MARCO DE LAS HOJAS (lo que en Windows es el area de fuera del cliente): 0 dentro del cliente, 1 la barra de titulo, 2 la «X» de
        // cerrar, 3 el borde. Ahi no llegan los eventos del raton de los controles (WM_NCLBUTTONDOWN...).
        internal static int ZonaMarco(Form f, Point p)
        {
            if (f == null || !f.ConBorde) return 0;
            Point o = f.Location;
            if (new Rectangle(f.OrigenEnPantalla(), f.ClientSize).Contains(p)) return 0;
            Rectangle x = f.CajaCerrar; x.Offset(o);
            if (f.ControlBox && x.Contains(p)) return 2;
            if (p.Y < o.Y + Form.Titulo + Form.Borde) return 1;
            return 3;
        }
        private static Form ncForma; private static int ncCaptura; private static Point ncInicio, ncOrigen;
        private static Control HijoEn(Control c, Point p)
        {
            List<Control> hijos = c.Controls.Lista;
            for (int i = 0; i < hijos.Count; i++)
            {
                Control h = hijos[i];
                if (!h.VisiblePropio || h.IsDisposed) continue;
                if (new Rectangle(c.PointToScreen(h.Location), h.Size).Contains(p))
                    return h.Enabled ? HijoEn(h, p) : c;   // (uno deshabilitado no recibe el raton: lo recibe el de debajo, su padre)
            }
            return c;
        }
        private static MouseEventArgs Args(Control c, MouseButtons b, int clics, int delta)
        {
            Point q = c.PointToClient(PosicionRaton);
            return new MouseEventArgs(b, clics, q.X, q.Y, delta);
        }
        private static void Llama(Control c, int tipo, MouseEventArgs e)
        {
            if (c == null || c.IsDisposed) return;
            try { c.LlamaMouse(tipo, e); } catch (Exception ex) { Application.Avisa(ex); }
        }
        // el raton se movio (dx, dy) o esta en (x, y); queda dentro de la pantalla
        public static void RatonMueve(int dx, int dy) { RatonEn(PosicionRaton.X + dx, PosicionRaton.Y + dy); }
        public static void RatonEn(int x, int y)
        {
            x = Math.Max(0, Math.Min(Pantalla.Width - 1, x)); y = Math.Max(0, Math.Min(Pantalla.Height - 1, y));
            if (x == PosicionRaton.X && y == PosicionRaton.Y) return;
            PosicionRaton = new Point(x, y);
            if (ncForma != null && ncCaptura != 0)
            {   // arrastrando una hoja por su barra de titulo (con su contenido, como XP), o con la «X» apretada
                if (ncCaptura == 1) ncForma.Location = new Point(ncOrigen.X + x - ncInicio.X, ncOrigen.Y + y - ncInicio.Y);
                else ncForma.PonEstadoCerrar(ZonaMarco(ncForma, PosicionRaton) == 2 ? 2 : 0);
                return;
            }
            if (captura != null && !captura.IsDisposed) { Llama(captura, 2, Args(captura, BotonesRaton, 0, 0)); return; }
            Control c = ControlEn(PosicionRaton);
            Form fm = c as Form;
            int zona = fm != null ? ZonaMarco(fm, PosicionRaton) : 0;
            MarcaEncima(zona == 2 ? fm : null);
            if (zona != 0) { Encima(null); return; }   // en el marco no hay eventos de cliente
            Encima(c);
            if (bajoRaton != null) Llama(bajoRaton, 2, Args(bajoRaton, BotonesRaton, 0, 0));
        }
        private static Form cerrarEncima;
        private static void MarcaEncima(Form f)
        {
            if (cerrarEncima == f) return;
            if (cerrarEncima != null && !cerrarEncima.IsDisposed) cerrarEncima.PonEstadoCerrar(0);
            cerrarEncima = f;
            if (f != null) f.PonEstadoCerrar(1);
        }
        private static void Encima(Control c)
        {
            if (c == bajoRaton) return;
            Control antes = bajoRaton; bajoRaton = c;
            if (antes != null && !antes.IsDisposed) Llama(antes, 4, null);
            if (c != null) Llama(c, 3, null);
        }
        public static void RatonBoton(MouseButtons b, bool abajo)
        {
            if (abajo)
            {
                BotonesRaton |= b;
                Control c = ControlEn(PosicionRaton);
                Form f = c == null ? null : c as Form ?? c.FindForm();
                if (f != null && f != Activa) { AlFrente(f); ActivaForma(f, true); }
                Form fm = c as Form;
                int zona = fm != null ? ZonaMarco(fm, PosicionRaton) : 0;
                if (zona != 0)
                {   // en el marco: la «X» se aprieta (cierra al soltar encima) y la barra de titulo arrastra la hoja
                    Encima(null);
                    if (b != MouseButtons.Left) return;
                    ncForma = fm; ncInicio = PosicionRaton; ncOrigen = fm.Location;
                    ncCaptura = zona == 2 ? 2 : zona == 1 ? 1 : 0;
                    if (ncCaptura == 2) fm.PonEstadoCerrar(2);
                    return;
                }
                Encima(c);
                if (c == null) return;
                captura = c;
                double t = AhoraMs;
                dobleEnCurso = b == ultimoClicBoton && c == ultimoClicControl && t - ultimoClicMs <= DobleClicMs
                    && Math.Abs(PosicionRaton.X - ultimoClicEn.X) <= DobleClicPuntos / 2 && Math.Abs(PosicionRaton.Y - ultimoClicEn.Y) <= DobleClicPuntos / 2;
                if (dobleEnCurso) ultimoClicMs = -1e9;   // un tercer clic empieza otra cuenta
                else { ultimoClicMs = t; ultimoClicEn = PosicionRaton; ultimoClicBoton = b; ultimoClicControl = c; }
                Llama(c, 0, Args(c, b, dobleEnCurso ? 2 : 1, 0));
            }
            else
            {
                BotonesRaton &= ~b;
                if (ncForma != null && ncCaptura != 0)
                {
                    Form fm = ncForma; int que = ncCaptura;
                    ncForma = null; ncCaptura = 0;
                    if (que == 2)
                    {
                        bool encima = ZonaMarco(fm, PosicionRaton) == 2;
                        fm.PonEstadoCerrar(encima ? 1 : 0);
                        if (encima && !fm.IsDisposed) fm.Close();   // (WM_SYSCOMMAND SC_CLOSE: como cerrar con la «X» en XP)
                    }
                    return;
                }
                Control c = captura ?? ControlEn(PosicionRaton);
                captura = null;
                if (c == null) return;
                MouseEventArgs e = Args(c, b, dobleEnCurso ? 2 : 1, 0);
                bool dentro = c.ClientRectangle.Contains(e.X, e.Y);
                Llama(c, !dentro ? 7 : dobleEnCurso ? 5 : 1, e);
                dobleEnCurso = false;
                Encima(ControlEn(PosicionRaton));
            }
        }
        public static void RatonRueda(int muescas)
        {
            Control c = Foco != null && !Foco.IsDisposed ? Foco : Activa;
            if (c != null) Llama(c, 6, Args(c, MouseButtons.None, 0, muescas * 120));
        }
        // la flecha que toca (la del control de debajo, o la de su padre)
        public static Cursor CursorActual
        {
            get
            {
                for (Control c = captura ?? bajoRaton; c != null; c = c.Parent) if (c.Cursor != null) return c.Cursor;
                return Cursors.Default;
            }
        }

        // ---------- las teclas (el anfitrion las manda con Entrada(() => Escritorio.Tecla(...))) ----------
        // Como en WinForms: primero ProcessCmdKey del control con el foco (y de sus padres: la rocola de XP atiende sus botones en el de
        // Form1), luego ProcessDialogKey, KeyDown (antes en el formulario si tiene KeyPreview) y KeyPress.
        public static void Tecla(Keys tecla, bool abajo)
        {
            Keys mod = tecla == Keys.ShiftKey || tecla == Keys.LShiftKey || tecla == Keys.RShiftKey ? Keys.Shift
                     : tecla == Keys.ControlKey || tecla == Keys.LControlKey || tecla == Keys.RControlKey ? Keys.Control
                     : tecla == Keys.Menu || tecla == Keys.LMenu || tecla == Keys.RMenu ? Keys.Alt : Keys.None;
            if (mod != Keys.None) tecla = mod == Keys.Shift ? Keys.ShiftKey : mod == Keys.Control ? Keys.ControlKey : Keys.Menu;
            if (abajo) Modificadores |= mod;
            int vk = (int)tecla & 0xFF; bool repetida = abajo && TeclasAbajo[vk]; TeclasAbajo[vk] = abajo;
            Keys datos = tecla | Modificadores;
            // la tecla va al que tiene el foco (en Windows, tambien si se escondio: SelectNextIfFocused no siempre encuentra otro), o a la
            // ventana activa; KeyPreview es el del formulario de ese control
            Control destino = Foco != null && !Foco.IsDisposed ? Foco : Activa;
            if (destino == null) { if (!abajo) Modificadores &= ~mod; return; }
            Form f = destino as Form ?? destino.FindForm();
            if (abajo)
            {
                // el lParam de WM_KEYDOWN como en Windows: una vez (bits 0-15) y, si la tecla ya estaba apretada (la repeticion al dejarla
                // apretada), el bit 30. XP lo mira para no contar otra moneda, ni saltar otra vez, ni repetir sus atajos (Form1.cs,
                // Form1.Ajustes.cs, AsistenteRocola.cs): sin el, la moneda mantenida 1,5 s daba 128 creditos en vez de 4.
                Message m = Message.Create(destino.Handle, 0x100, (IntPtr)(int)tecla, (IntPtr)(repetida ? 0x40000001 : 1));
                if (destino.LlamaProcessCmdKey(ref m, datos)) return;
                if (!destino.LlamaIsInputKey(datos) && destino.LlamaProcessDialogKey(datos)) return;
                KeyEventArgs e = new KeyEventArgs(datos);
                if (f != null && f.KeyPreview && f != destino) { f.LlamaKeyDown(e); if (e.Handled) return; }
                destino.LlamaKeyDown(e);
                if (!e.SuppressKeyPress)
                {
                    char ch = Caracter(tecla, Modificadores);
                    if (ch != '\0')
                    {
                        KeyPressEventArgs kp = new KeyPressEventArgs(ch);
                        if (f != null && f.KeyPreview && f != destino) { f.LlamaKeyPress(kp); if (kp.Handled) return; }
                        destino.LlamaKeyPress(kp);
                    }
                }
            }
            else
            {
                Modificadores &= ~mod;
                KeyEventArgs e = new KeyEventArgs(tecla | Modificadores);
                if (f != null && f.KeyPreview && f != destino) { f.LlamaKeyUp(e); if (e.Handled) return; }
                destino.LlamaKeyUp(e);
            }
        }
        private static char Caracter(Keys k, Keys mod)
        {
            if ((mod & (Keys.Control | Keys.Alt)) != 0) return '\0';
            bool may = (mod & Keys.Shift) != 0;
            if (k >= Keys.A && k <= Keys.Z) return (char)((may ? 'A' : 'a') + (k - Keys.A));
            if (k >= Keys.D0 && k <= Keys.D9 && !may) return (char)('0' + (k - Keys.D0));
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return (char)('0' + (k - Keys.NumPad0));
            if (k == Keys.Space) return ' ';
            if (k == Keys.Enter) return '\r';
            if (k == Keys.Back) return '\b';
            if (k == Keys.Escape) return (char)27;
            if (k == Keys.OemMinus) return may ? '_' : '-';
            if (k == Keys.OemPeriod) return may ? ':' : '.';
            if (k == Keys.Oemcomma) return may ? ';' : ',';
            return '\0';
        }

        // ---------- la pintura ----------
        private static readonly Dictionary<Form, int> versiones = new Dictionary<Form, int>();
        private static int cambios;
        public static int Cambios { get { return Volatile.Read(ref cambios); } }
        internal static void Ensucia(Control c)
        {
            if (c != null && c.Nativa) { lock (cerrojo) if (!nativasPorPintar.Contains(c)) nativasPorPintar.Add(c); senal.Set(); return; }
            Form f = c == null ? null : c.TopLevelControl as Form;
            if (f != null) lock (versiones) { int v; versiones.TryGetValue(f, out v); versiones[f] = v + 1; }
            Interlocked.Increment(ref cambios);
            senal.Set();
        }
        // una ventana cambio de sitio: hay que volver a componer, no a pintar (ArmaCapas mueve sus capas)
        internal static void Mueve() { Interlocked.Increment(ref cambios); senal.Set(); }
        internal static void PintaYa(Control c)
        {
            if (c == null || c.IsDisposed || !c.Visible) return;
            if (c.Nativa) { using (Graphics g = Graphics.FromImage(LienzoSuelto)) using (PaintEventArgs e = new PaintEventArgs(g, c.ClientRectangle)) c.LlamaPaint(e); c.sucio = false; }
        }
        private static Bitmap lienzoSuelto;
        internal static Bitmap LienzoSuelto { get { return lienzoSuelto ?? (lienzoSuelto = new Bitmap(1, 1, PixelFormat.Format32bppArgb)); } }
        public static Rectangle RectEnPantalla(Control c) { return c == null ? Rectangle.Empty : new Rectangle(c.OrigenEnPantalla(), c.ClientSize); }

        private sealed class CapasDeForma { public List<Capa> Lista = new List<Capa>(); public int Version = -1; public Size Tam; }
        private static readonly Dictionary<Form, CapasDeForma> cache = new Dictionary<Form, CapasDeForma>();
        private static int versionCapa;
        // Las capas de la pantalla, de abajo arriba. Solo en el hilo de la pantalla.
        public static List<Capa> Capas()
        {
            List<Capa> r = new List<Capa>();
            foreach (Form f in Formas)
            {
                if (f.IsDisposed || !f.Visible) continue;
                CapasDeForma cf;
                if (!cache.TryGetValue(f, out cf)) cache[f] = cf = new CapasDeForma();
                int v; lock (versiones) versiones.TryGetValue(f, out v);
                ArmaCapas(f, cf, v);
                r.AddRange(cf.Lista);
            }
            foreach (Form f in new List<Form>(cache.Keys)) if (f.IsDisposed) { foreach (Capa c in cache[f].Lista) if (c.Imagen != null) c.Imagen.Dispose(); cache.Remove(f); }
            return r;
        }
        private static bool TapaTodo(Control h, Size cliente) { return h.Nativa && h.VisiblePropio && h.Left <= 0 && h.Top <= 0 && h.Right >= cliente.Width && h.Bottom >= cliente.Height; }
        private static void ArmaCapas(Form f, CapasDeForma cf, int version)
        {
            Point o = f.Location, oc = f.OrigenCliente;
            Size cliente = f.ClientSize;
            List<Control> hijos = f.Controls.Lista;
            // de atras adelante; los de la tarjeta parten la pintura en tramos. Lo que queda debajo de un hueco de la tarjeta que tapa la
            // ventana entera no se pinta.
            int desde = hijos.Count - 1; bool tapaTodo = false;
            for (int i = 0; i < hijos.Count; i++) if (TapaTodo(hijos[i], cliente)) { desde = i; tapaTodo = true; break; }
            List<List<int>> tramos = new List<List<int>>(); List<Control> nativas = new List<Control>();
            List<int> actual = new List<int>();
            // LA TARJETA PINTANDO EN LA VENTANA MISMA (en XP, un dispositivo de Direct3D creado sobre el Handle del formulario: la ventana
            // de prueba F12): su dibujo ocupa el cliente, encima del fondo de la ventana y debajo de sus controles
            if (f.TexturaGl != 0 && !tapaTodo) { tramos.Add(actual); nativas.Add(f); actual = new List<int>(); }
            for (int i = desde; i >= 0; i--)
            {
                Control h = hijos[i];
                if (!h.VisiblePropio) continue;
                if (h.Nativa) { tramos.Add(actual); nativas.Add(h); actual = new List<int>(); }
                else actual.Add(i);
            }
            tramos.Add(actual);
            int necesarias = tramos.Count + nativas.Count;
            bool rehacer = cf.Version != version || cf.Tam != f.Size || cf.Lista.Count != necesarias;
            if (!rehacer)
            {
                int k = 0;   // solo se mueven las capas (la ventana arrastrada) y los huecos
                for (int t = 0; t < tramos.Count; t++)
                {
                    cf.Lista[k].Rect = new Rectangle(o, f.Size); k++;
                    if (t < nativas.Count) { Control nn = nativas[t]; cf.Lista[k].Rect = RectEnPantalla(nn); cf.Lista[k].Nativa = nn; k++; }
                }
                return;
            }
            List<Capa> viejas = cf.Lista; cf.Lista = new List<Capa>();
            int vi = 0;
            for (int t = 0; t < tramos.Count; t++)
            {
                Capa c = null;
                while (vi < viejas.Count && viejas[vi].Imagen == null) vi++;
                if (vi < viejas.Count) c = viejas[vi++];
                if (c == null || c.Imagen.Width != f.Width || c.Imagen.Height != f.Height)
                {
                    if (c != null) c.Imagen.Dispose();
                    c = new Capa(); c.Imagen = new Bitmap(Math.Max(1, f.Width), Math.Max(1, f.Height), PixelFormat.Format32bppPArgb);
                }
                c.Rect = new Rectangle(o, f.Size); c.Version = ++versionCapa; c.Opacidad = (float)f.Opacity;
                using (Graphics g = Graphics.FromImage(c.Imagen))
                {
                    g.Clear(Color.Transparent);
                    if (t == 0 && !tapaTodo)
                    {
                        PintaMarco(f, g);
                        g.TranslateTransform(oc.X, oc.Y);
                        g.SetClip(new Rectangle(Point.Empty, cliente));
                        PintaSolo(f, g);
                        g.ResetTransform(); g.ResetClip();
                    }
                    g.TranslateTransform(oc.X, oc.Y);
                    foreach (int i in tramos[t]) PintaHijo(hijos, i, g);
                }
                cf.Lista.Add(c);
                if (t < nativas.Count) { Control nn = nativas[t]; Capa cn = new Capa(); cn.Rect = RectEnPantalla(nn); cn.Nativa = nn; cf.Lista.Add(cn); }
            }
            for (; vi < viejas.Count; vi++) if (viejas[vi].Imagen != null) viejas[vi].Imagen.Dispose();
            cf.Version = version; cf.Tam = f.Size;
        }
        private static void PintaSolo(Control c, Graphics g)
        {
            GraphicsState st = g.Save();
            g.SellaRecorte();   // (como la region de su ventana en Windows: su SetClip no lo saca de su sitio)
            try { c.PintaFondoYContenido(g, c.ClientRectangle); }
            catch (Exception ex) { Application.Avisa(ex); }
            finally { g.Restore(st); }
        }
        // Un control y sus hijos, en su sitio (g ya esta en el origen del cliente del padre).
        internal static void PintaArbol(Control c, Graphics g, bool enSuSitio)
        {
            if (!c.VisiblePropio || c.Width <= 0 || c.Height <= 0) return;
            GraphicsState st = g.Save();
            try
            {
                if (enSuSitio) g.TranslateTransform(c.Left, c.Top);
                g.IntersectClip(new Rectangle(0, 0, c.Width, c.Height));
                if (c.Region != null) g.SetClip(c.Region, CombineMode.Intersect);   // (la forma del control: la pastilla del aviso)
                if (c.Nativa) return;
                Point oc = c.OrigenCliente;
                if (oc.X != 0 || oc.Y != 0) g.TranslateTransform(oc.X, oc.Y);
                PintaSolo(c, g);
                List<Control> hijos = c.Controls.Lista;
                for (int i = hijos.Count - 1; i >= 0; i--) PintaHijo(hijos, i, g);
            }
            finally { g.Restore(st); }
        }
        // UN HIJO, RECORTADO POR LOS HERMANOS QUE TIENE ENCIMA. En WinForms cada control es una ventana con WS_CLIPSIBLINGS: donde lo tapa un
        // hermano de encima (antes en la lista) no se ve, aunque el de encima sea transparente: el transparente ensena lo que pinta su
        // padre (PaintTransparentBackground), nunca a sus hermanos de debajo. Asi se ve en XP, por ejemplo, un interruptor de dos renglones
        // que tapa el primer renglon de la nota de debajo, o el boton Listo encima de una tarjeta (sus esquinas son del fondo de la hoja).
        private static void PintaHijo(List<Control> hijos, int i, Graphics g)
        {
            Control h = hijos[i];
            if (!h.VisiblePropio || h.Width <= 0 || h.Height <= 0) return;
            Rectangle rh = h.Bounds;
            GraphicsState st = null;
            for (int j = i - 1; j >= 0; j--)
            {
                Control s = hijos[j];
                if (!s.VisiblePropio || s.Width <= 0 || s.Height <= 0 || !s.Bounds.IntersectsWith(rh)) continue;
                if (st == null) st = g.Save();
                if (s.Region != null) { g.TranslateTransform(s.Left, s.Top); g.SetClip(s.Region, CombineMode.Exclude); g.TranslateTransform(-s.Left, -s.Top); }
                else g.ExcludeClip(s.Bounds);
            }
            PintaArbol(h, g, true);
            if (st != null) g.Restore(st);
        }
        // LA BARRA DE TITULO DE LAS HOJAS (la de Windows XP, Luna azul): las esquinas de arriba redondas, el titulo con su sombra y la «X» de
        // cerrar (normal, con el raton encima o apretada); mas clara si la hoja no es la activa (con un aviso encima).
        private static void PintaMarco(Form f, Graphics g)
        {
            if (!f.ConBorde) return;
            bool activa = Activa == f;
            int alto = Form.Titulo + Form.Borde;
            Color c1 = activa ? Color.FromArgb(0, 88, 238) : Color.FromArgb(150, 178, 238), c2 = activa ? Color.FromArgb(0, 64, 208) : Color.FromArgb(122, 150, 223);
            Color borde = activa ? Color.FromArgb(0, 72, 224) : Color.FromArgb(126, 154, 228);
            SmoothingMode sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = new GraphicsPath())
            {
                const int r = 7;
                gp.AddArc(0, 0, 2 * r, 2 * r, 180, 90);
                gp.AddArc(f.Width - 2 * r, 0, 2 * r, 2 * r, 270, 90);
                gp.AddLine(f.Width, r, f.Width, alto);
                gp.AddLine(f.Width, alto, 0, alto);
                gp.CloseFigure();
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, f.Width, alto), c1, c2, LinearGradientMode.Vertical))
                    g.FillPath(b, gp);
            }
            g.SmoothingMode = sm;
            using (SolidBrush b = new SolidBrush(borde))
            {
                g.FillRectangle(b, 0, alto, Form.Borde, f.Height - alto);
                g.FillRectangle(b, f.Width - Form.Borde, alto, Form.Borde, f.Height - alto);
                g.FillRectangle(b, 0, f.Height - Form.Borde, f.Width, Form.Borde);
            }
            Rectangle x = f.CajaCerrar;
            int textoHasta = f.ControlBox ? x.Left - 4 : f.Width - 8;
            using (Font letra = new Font("Trebuchet MS", 10f, FontStyle.Bold))
            {
                Rectangle rt = new Rectangle(8, Form.Borde, Math.Max(0, textoHasta - 8), Form.Titulo);
                TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                if (activa) { Rectangle rs = rt; rs.Offset(1, 1); TextRenderer.DrawText(g, f.Text, letra, rs, Color.FromArgb(10, 24, 131), fl); }
                TextRenderer.DrawText(g, f.Text, letra, rt, activa ? Color.White : Color.FromArgb(216, 228, 248), fl);
            }
            if (!f.ControlBox) return;
            int e = f.EstadoCerrar;
            Color a1 = e == 2 ? Color.FromArgb(178, 52, 28) : e == 1 ? Color.FromArgb(240, 132, 104) : Color.FromArgb(226, 98, 70);
            Color a2 = e == 2 ? Color.FromArgb(206, 84, 56) : e == 1 ? Color.FromArgb(214, 80, 50) : Color.FromArgb(196, 58, 30);
            if (!activa) { a1 = Color.FromArgb(198, 150, 160); a2 = Color.FromArgb(178, 128, 140); }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = new GraphicsPath())
            {
                const int r = 3;
                gp.AddArc(x.Left, x.Top, 2 * r, 2 * r, 180, 90);
                gp.AddArc(x.Right - 1 - 2 * r, x.Top, 2 * r, 2 * r, 270, 90);
                gp.AddArc(x.Right - 1 - 2 * r, x.Bottom - 1 - 2 * r, 2 * r, 2 * r, 0, 90);
                gp.AddArc(x.Left, x.Bottom - 1 - 2 * r, 2 * r, 2 * r, 90, 90);
                gp.CloseFigure();
                using (LinearGradientBrush b = new LinearGradientBrush(x, a1, a2, LinearGradientMode.Vertical)) g.FillPath(b, gp);
                using (Pen p = new Pen(Color.FromArgb(activa ? 235 : 180, 255, 255, 255))) g.DrawPath(p, gp);
            }
            using (Pen p = new Pen(Color.White, 2f))
            {
                g.DrawLine(p, x.Left + 6.5f, x.Top + 6.5f, x.Right - 7.5f, x.Bottom - 7.5f);
                g.DrawLine(p, x.Right - 7.5f, x.Top + 6.5f, x.Left + 6.5f, x.Bottom - 7.5f);
            }
            g.SmoothingMode = sm;
        }

        // ---------- los bucles ----------
        public static double UltimaPresentacionNativa;   // la pone el anfitrion al presentar un cuadro de la tarjeta
        private static int compuesto = -1;
        // La tarjeta presento un cuadro en un control «Nativa» (Direct3DaGL, Present: en XP, Direct3D 9 lo ponia en la ventana): el anfitrion
        // compone las capas con el y lo pone en la pantalla. SinEspera: el medidor de Ctrl+F3 pide presentar sin esperar al barrido.
        public static bool SinEspera;
        internal static void PresentaNativa(Control c)
        {
            UltimaPresentacionNativa = AhoraMs;
            compuesto = Cambios;
            if (Anfitrion != null) Anfitrion.Compone();
        }
        internal static void Vuelta(bool conIdle)
        {
            Bombea();
            if (conIdle) Application.LlamaIdle();
            Bombea();
            // Si la tarjeta no esta presentando (o hace rato que no), el escritorio presenta lo de Dibujo2D cuando cambia.
            double ahora = AhoraMs;
            int c = Cambios;
            if (Anfitrion != null && c != compuesto && ahora - UltimaPresentacionNativa > 100)
            {
                compuesto = c;
                try { Anfitrion.Compone(); } catch (Exception ex) { Application.Avisa(ex); }
            }
        }
        internal static void Bucle(Func<bool> sigue)
        {
            if (HiloUi == null) HiloUi = Thread.CurrentThread;
            while (sigue())
            {
                Vuelta(true);
                if (!sigue()) break;
                EsperaMensajes(16);
            }
        }
        // el bucle de una hoja modal (lo esconde ShowDialog al acabar)
        internal static void BucleModal(Form f)
        {
            lock (modales) modales.Add(f);
            try { Bucle(delegate { return f.SigueModal && !Application.Saliendo; }); }
            finally { lock (modales) modales.Remove(f); }
        }
    }
}

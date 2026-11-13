// LA HOJA (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP eran las ventanas que la rocola abre encima de la suya
// (Ctrl+F2, los avisos y, en el paso C, Ctrl+F3 y el historial de caratulas; y la de prueba F12, que no es modal: Muestra). No imita a
// Form: es una hoja de la pantalla de la rocola, pintada con SkiaSharp en su superficie, con SOLO lo que XP usa de una ventana de WinForms
// (el inventario: Text, ClientSize, BackColor, Font, Controls.Add, Close, CancelButton, KeyPreview con KeyDown y KeyUp, ShowDialog con su
// duena; y lo que XP espera de ella sin escribirlo: el marco de XP Luna, el sitio, el modal, la activacion, el foco y el tabulador, Intro
// y Escape, el raton, el pintado y la «X» roja). Cada regla cita su linea de la fuente con que se reviso la capa de la transicion
// (dotnet/winforms release/3.0, portada de .NET Framework 4.8: /tmp/claude-0/agente-winforms/ref; Form.cs, ContainerControl.cs, Control.cs)
// o la documentacion de Win32. Se PORTARON de la capa (Plataforma/Ventanas.Form.cs y Ventanas.Escritorio.cs, revisadas contra esa fuente),
// sin su API: sin Handle, sin mensajes de Windows, sin Invoke, sin IWin32Window, sin Load, Shown, Activated ni FormClosing (XP no los
// usa en sus hojas).
//
// Partes: Hoja.cs (la vida: crearla, ensenarla modal, cerrarla, el apagado; las hojas abiertas, su orden y la activa; la superficie),
// Hoja.Foco.cs (el foco y el tabulador), Hoja.Entrada.cs (las teclas y el raton) y Hoja.Pinta.cs (el pintado).
//
// LA VENTANA DE DEBAJO es la de la rocola, que es la pantalla (Nativo/PantallaRocola.Ventana.cs): las hojas van siempre encima de ella (en
// XP la hoja nueva es la de arriba, y mientras una hoja modal esta abierta la rocola no recibe teclas ni raton, asi que no se pone delante),
// y lo que una hoja necesita de ella (si esta activa, que ceda o recupere la activacion y el foco, su sitio como duena, volver a componer
// la pantalla) lo pone esa ventana al abrirse en los ganchos «la ventana de debajo» de abajo (son ganchos y no llamadas porque las
// pruebas de la hoja compilan estos archivos sin la ventana de la rocola: Pruebas/Ventanas/Hoja).
using System;
using System.Collections.Generic;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // EL MARCO DE UNA HOJA (el FormBorderStyle y el StartPosition con que XP abre cada ventana): Dialogo = FixedDialog sin maximizar ni
    // minimizar, sin barra de tareas ni icono, centrada en su duena (CenterParent): Ctrl+F2, Ctrl+F3, el historial de caratulas y los
    // avisos. Sencillo = FixedSingle sin maximizar, centrada en la pantalla (CenterScreen), sin duena: la ventana de prueba F12. Mide igual
    // que el dialogo (la barra de 26 y el borde de 3: WS_CAPTION lleva WS_DLGFRAME y Form.Marco de la capa da 3 para FixedSingle con icono,
    // Form.CreateParams, Form.cs:886-985); se diferencia en donde se abre (CenterToScreen, Form.cs:4113-4141).
    public enum MarcoXp { Dialogo = 0, Sencillo = 1 }

    public partial class Hoja : IContenedor, IDuenoDeEncargos, IDisposable
    {
        // EL MARCO DE XP (tema Luna, 96 puntos por pulgada), como lo cuenta AdjustWindowRectEx con los estilos de Form.CreateParams
        // (Form.cs:886-985): la barra de titulo mide SM_CYCAPTION = 26 (WS_CAPTION: hay ControlBox) y FixedDialog lleva WS_EX_DLGMODALFRAME
        // (2) y WS_BORDER (1) = 3 a cada lado. Un dialogo de 960x732 de cliente mide 966x764 (Ctrl+F3); uno de 640x372, 646x404 (Ctrl+F2).
        internal const int AltoBarra = 26, Marco = 3;
        private readonly MarcoXp clase;
        private Rectangle bounds;            // en la pantalla, con el marco
        private string text = "";
        private Color backColor = Color.Empty;
        private Letra font;
        private bool visible, modal, disposed, llamadoClosing, cargada;
        private long vida;                   // la ventana de ahora (0: sin ventana): con ella viajan los encargos (IsHandleCreated)
        private static long ultimaVida = 0x40000000;
        private object duena;                // la duena de Windows del dialogo (ShowDialog sin duena: la ventana activa)
        private RazonCierre razonCierre = RazonCierre.None;
        internal Resultado resultado = Resultado.None;
        internal IPieza activo;              // la pieza activa del contenedor (ContainerControl.ActiveControl)
        private BotonXp porDefecto;          // el boton por defecto de ahora (Form.UpdateDefaultButton)

        // UNA HOJA NUEVA: escondida, de 300x300 con su marco (Form.DefaultSize, Form.cs:1030-1036)
        public Hoja(MarcoXp marco)
        {
            clase = marco;
            bounds = new Rectangle(0, 0, 300, 300);
            Controls = new Piezas(this);
            if (PantallaRocola.Hilo == null || PantallaRocola.EnSuHilo) PantallaRocola.PonContexto();   // (InstallIfNeeded al crear un control)
        }

        // ---------- lo que XP le pone ----------
        // El titulo (Text): se repinta la barra (Form.OnTextChanged)
        public string Text { get { return text; } set { if (value == null) value = ""; if (text == value) return; text = value; Invalidate(); } }
        // La medida de dentro (ClientSize: Form.SetClientSizeCore, Form.cs:5546-5575, con el marco por fuera)
        public Size ClientSize
        {
            get { return new Size(Math.Max(0, bounds.Width - 2 * Marco), Math.Max(0, bounds.Height - 2 * Marco - AltoBarra)); }
            set { bounds.Size = new Size(value.Width + 2 * Marco, value.Height + 2 * Marco + AltoBarra); Invalidate(); }
        }
        // El fondo y la letra de las piezas que no tienen los suyos (Control.BackColor y Font; sin poner, el gris de las ventanas de XP y la
        // letra de fabrica); el color del texto, el de fabrica (negro)
        public Color BackColor { get { return backColor.IsEmpty ? CajaPieza.ColorDeFondo : backColor; } set { backColor = value; Invalidate(); } }
        public Letra Font { get { return font ?? CajaPieza.LetraDeFabrica; } set { font = value; Invalidate(); } }
        public Piezas Controls { get; }
        // El boton de cancelar (Form.CancelButton): Escape lo pulsa (Form.ProcessDialogKey, Form.cs:4908-4948)
        public BotonXp BotonCancelar { get; set; }
        // El boton de aceptar (Form.AcceptButton): Intro lo pulsa si el foco no lo tiene otro boton (Form.UpdateDefaultButton, Form.cs:5963-5990; AcceptButton, :262-284). XP no se lo pone a
        // sus hojas; lo usa el selector de carpetas (el «Aceptar» de SHBrowseForFolder es el boton por defecto del dialogo).
        private BotonXp aceptar;
        internal BotonXp BotonAceptar { get { return aceptar; } set { aceptar = value; ActualizaBotonPorDefecto(); } }
        // EL KEYDOWN Y EL KEYUP DE LA VENTANA CON KEYPREVIEW (Form.KeyPreview, Form.cs:1346-1363; ProcessKeyPreview, :4985-4993): con alguien
        // escuchando, la hoja ve cada tecla antes que la pieza del foco (y es su KeyDown y su KeyUp si el foco lo tiene ella misma)
        private Action<PulsoTecla> teclaAntes, teclaArribaAntes;
        private bool vistaPrevia;
        public event Action<PulsoTecla> TeclaAntes { add { vistaPrevia = true; teclaAntes += value; } remove { teclaAntes -= value; } }
        public event Action<PulsoTecla> TeclaArribaAntes { add { vistaPrevia = true; teclaArribaAntes += value; } remove { teclaArribaAntes -= value; } }

        // ---------- lo que la hoja es (para la pantalla, la composicion y las pruebas: el codigo de XP no lo usa) ----------
        Hoja IContenedor.HojaDe { get { return this; } }
        IContenedor IContenedor.PadreDe { get { return null; } }
        bool IContenedor.VisibleDe { get { return visible; } }
        bool IContenedor.EnabledDe { get { return true; } }   // (WinForms no le cambia Enabled a la de debajo de un modal: lo hace Windows)
        Color IContenedor.FondoDe { get { return BackColor; } }
        Color IContenedor.TintaDe { get { return Color.Black; } }
        Letra IContenedor.LetraDe { get { return Font; } }
        void IContenedor.Ensucia() { Invalidate(); }
        internal bool Visible { get { return visible; } }
        internal bool IsDisposed { get { return disposed; } }
        // con su ventana creada (IsHandleCreated): desde que se ensena hasta que se acaba su modal
        internal bool IsHandleCreated { get { return vida != 0; } }
        long IDuenoDeEncargos.Vida { get { return vida; } }
        internal Rectangle Bounds { get { return bounds; } }
        internal Point Location { get { return bounds.Location; } }
        internal int Width { get { return bounds.Width; } }
        internal int Height { get { return bounds.Height; } }
        internal Point OrigenCliente { get { return new Point(Marco, Marco + AltoBarra); } }
        // la «X» de cerrar de la barra de titulo (en coordenadas de la hoja) y como esta: 0 normal, 1 con el raton encima, 2 apretada
        internal Rectangle CajaCerrar { get { return new Rectangle(Width - 5 - 21, (AltoBarra + Marco - 21) / 2 + 1, 21, 21); } }
        internal int EstadoCerrar { get; private set; }
        internal void PonEstadoCerrar(int e) { if (EstadoCerrar == e) return; EstadoCerrar = e; Invalidate(); }
        // arrastrada por su barra de titulo: cambia de sitio sin repintarse (la composicion la pone en el sitio nuevo)
        internal void Mueve(Point p) { if (bounds.Location == p) return; bounds.Location = p; Compone(); }
        // Lo que la hoja hace al pedirle que se cierre (lo que en XP es su FormClosing): true = no se cierra. De fabrica nada (las hojas de
        // XP no lo usan); el aviso si (AvisoXp: la «X» de un aviso es su boton de cancelar).
        internal virtual bool AlCerrarse(RazonCierre razon) { return false; }

        // ---------- las hojas abiertas, su orden y la activa (lo que en XP lleva Windows) ----------
        private static readonly List<Hoja> abiertas = new List<Hoja>();   // la ultima, la de mas arriba
        private static readonly List<Hoja> modales = new List<Hoja>();
        // LA HOJA ACTIVA (WM_ACTIVATE); null: la activa es la ventana de la rocola, o ninguna
        internal static Hoja Activa { get; private set; }
        internal static Hoja[] Abiertas { get { return abiertas.ToArray(); } }
        private static Hoja ModalArriba { get { for (int i = modales.Count - 1; i >= 0; i--) if (!modales[i].disposed && modales[i].visible) return modales[i]; return null; } }
        internal static bool HayModal { get { return ModalArriba != null; } }
        // Una hoja que queda debajo de la modal de arriba esta deshabilitada (ShowDialog deshabilita las demas ventanas del hilo mientras
        // dura: ThreadContext.DisableWindowsForModalLoop, Application.cs:2608-2620): no toma el foco ni se activa ni recibe el raton
        internal static bool Deshabilitada(Hoja h)
        {
            Hoja m = ModalArriba;
            if (m == null || h == null || h == m) return false;
            int im = abiertas.IndexOf(m), i = abiertas.IndexOf(h);
            return im >= 0 && i >= 0 && i < im;
        }

        // ---------- la ventana de debajo (la de la rocola): lo pone PantallaRocola.Ventana.cs (AbreNativa) ----------
        internal static Func<object> VentanaActiva;                 // la ventana de debajo, si es la activa (o null)
        internal static Action CedeLaVentana;                       // la de debajo deja de ser la activa (la hoja se activa)
        internal static Action QuitaFocoALaVentana;                 // la vista de la ventana de debajo con el foco lo pierde, sin apuntarlo
        internal static Action<object> ActivaLaVentana;             // activa la ventana de debajo, con su foco (al acabar un modal)
        internal static Func<object, bool> VentanaViva;             // la ventana de debajo sigue y se ve
        internal static Func<object, Rectangle> SitioDeLaVentana;   // donde esta la ventana de debajo (la duena de un dialogo)
        internal static Action HayQueComponer;                      // algo de una hoja cambio: hay que volver a componer la pantalla
        internal static Action ActivaLaVentanaDeDebajo;             // se fue la hoja activa y no queda otra: se activa la de debajo
        private static void Compone() { Action a = HayQueComponer; if (a != null) a(); }
        // la ventana activa, de las dos procedencias
        internal static object LaActiva { get { if (Activa != null) return Activa; Func<object> f = VentanaActiva; return f != null ? f() : null; } }
        private static bool VivaYVisible(object v)
        {
            Hoja h = v as Hoja;
            if (h != null) return !h.disposed && h.visible;
            Func<object, bool> f = VentanaViva; return v != null && f != null && f(v);
        }

        // ---------- crear, ensenar y esconder ----------
        // la ventana (CreateHandle): una vida nueva
        private void CreaVentana() { if (vida == 0) vida = ++ultimaVida; }
        // ENSENARSE (Form.SetVisibleCore, Form.cs:2507-2631, y Form.OnLoad, :4457-4531): la primera vez desde que tiene ventana el dialogo se
        // centra en su duena (CenterParent: CenterToParent) y Shown queda en la cola (OnLoad lo deja con BeginInvoke); despues se ve, avisa
        // VisibleChanged a sus piezas (Control.OnVisibleChanged, Control.cs:7875-7918), se pone delante y se activa, con el foco.
        private void Ensena()
        {
            CreaVentana();
            if (!cargada)
            {
                cargada = true;
                if (modal) CentraEnDuena(); else if (clase == MarcoXp.Sencillo) CentraEnPantalla();   // (OnLoad: CenterParent al ShowDialog, CenterScreen al Show)
                Hoja yo = this;
                PantallaRocola.EncolaDesde(this, delegate { if (TrazaVentanas.Activa) TrazaVentanas.Apunta("shown " + TrazaVentanas.Pieza(yo)); yo.AlEnsenarse(); }, "Shown", false);
            }
            visible = true;
            foreach (IPieza p in Controls.Lista.ToArray()) if (p.Caja.Visible) p.Caja.AvisaVisible();
            Invalidate();
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("ventana-ve " + TrazaVentanas.Pieza(this) + (modal ? " modal" : ""));
            if (!abiertas.Contains(this)) abiertas.Add(this);
            Activate();
        }
        // CenterToParent (Form.CenterToParent, Form.cs:4061-4105): en medio de su duena ((izq + der - ancho) / 2), sin salirse del area de
        // trabajo (en Rocola OS no hay barra de tareas: la pantalla entera); sin duena, CenterToScreen
        private void CentraEnDuena()
        {
            Rectangle o;
            if (!SitioDeDuena(out o)) { CentraEnPantalla(); return; }
            Rectangle wa = AreaDeTrabajo;
            int x = (o.Left + o.Right - Width) / 2, y = (o.Top + o.Bottom - Height) / 2;
            if (x < wa.X) x = wa.X; else if (x + Width > wa.Right) x = wa.Right - Width;
            if (y < wa.Y) y = wa.Y; else if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            bounds.Location = new Point(x, y);
        }
        // CenterToScreen (Form.cs:4113-4141): en medio del area de trabajo, nunca por encima ni a la izquierda
        private void CentraEnPantalla()
        {
            Rectangle wa = AreaDeTrabajo;
            bounds.Location = new Point(Math.Max(wa.X, wa.X + (wa.Width - Width) / 2), Math.Max(wa.Y, wa.Y + (wa.Height - Height) / 2));
        }
        private static Rectangle AreaDeTrabajo { get { return new Rectangle(0, 0, PantallaRocola.Ancho, PantallaRocola.Alto); } }
        private bool SitioDeDuena(out Rectangle r)
        {
            r = Rectangle.Empty;
            Hoja h = duena as Hoja;
            if (h != null) { if (h.disposed) return false; r = h.bounds; return true; }
            if (duena == null) return false;
            Func<object, Rectangle> sitio = SitioDeLaVentana;
            if (sitio == null) return false;
            r = sitio(duena);
            return true;
        }
        // ESCONDERSE (lo que hace Windows al esconder una ventana: la capa, Escritorio.Oculta): sale de las abiertas; si tenia el foco, nadie
        // lo tiene (WM_KILLFOCUS); si era la activa, se activa la de arriba (o la ventana de la rocola). 'apunta': se escondio ella (Visible = false, al
        // acabar el modal), no al desecharla (como la capa: SetVisibleCore lo apunta, Dispose no)
        private void Esconde(bool apunta)
        {
            if (!visible) return;
            visible = false;
            if (apunta && TrazaVentanas.Activa) TrazaVentanas.Apunta("ventana-esconde " + TrazaVentanas.Pieza(this) + " " + resultado);
            abiertas.Remove(this);
            if (ContieneFoco(null)) QuitaFoco();
            if (Activa == this)
            {
                Hoja otra = null;
                for (int i = abiertas.Count - 1; i >= 0 && otra == null; i--) if (!abiertas[i].disposed && abiertas[i].visible) otra = abiertas[i];
                Activa = null;
                if (otra != null) ActivaHoja(otra, true);
                else { Action a = ActivaLaVentanaDeDebajo; if (a != null) a(); }
            }
            OlvidaRaton();
            Compone();
        }
        // DESTRUIR LA VENTANA (DestroyHandle, al acabar el modal): el foco, si estaba dentro, se va; la vida acaba (lo pendiente en la cola
        // para ella ya no corre: el encargo se pierde: InvokeMarshaledCallbacks, Control.cs:6775-6839)
        private void DestruyeVentana()
        {
            if (vida == 0) return;
            if (ContieneFoco(null)) QuitaFoco();
            vida = 0;
        }
        // Form.Activate (Form.cs:2869-2882): delante y activa, salvo debajo de una modal
        internal void Activate() { if (!visible || Deshabilitada(this)) return; AlFrente(this); ActivaHoja(this, true); }
        // Delante de todas (y las suyas encima de su duena: las ventanas con duena van siempre encima de ella, Windows)
        internal static void AlFrente(Hoja h)
        {
            if (h == null || !abiertas.Remove(h)) return;
            abiertas.Add(h);
            h.DetrasDeLaVentana = false;
            SubeLasSuyas(h, 0);
            Compone();
        }
        private static void SubeLasSuyas(Hoja h, int nivel)
        {
            if (nivel > 8) return;
            foreach (Hoja o in abiertas.ToArray()) if (o != h && o.duena == h && abiertas.Remove(o)) { abiertas.Add(o); SubeLasSuyas(o, nivel + 1); }
        }
        // LA HOJA ACTIVA (WM_ACTIVATE; Escritorio.ActivaForma de la capa): la de antes (una hoja, o la ventana de la rocola) se desactiva y la
        // nueva, si 'enfoca', pone el foco donde lo tenia (su pieza activa, si se ve), o en la primera del tabulador si no tenia, o en ella
        // misma (Form.Active, Form.cs:289-341)
        internal static void ActivaHoja(Hoja h, bool enfoca)
        {
            if (h == null || h.disposed) return;
            if (Activa != h)
            {
                Hoja antes = Activa; Activa = h;
                if (antes != null) antes.Invalidate();
                else { Action c = CedeLaVentana; if (c != null) c(); }
                h.Invalidate();   // (la barra de titulo cambia de color)
                if (enfoca) h.EnfocaAlActivar();
            }
            else if (enfoca && (foco == null || !h.ContieneFoco(null))) h.EnfocaAlActivar();
        }
        // la ventana de la rocola se activa: la hoja activa deja de serlo. Y, como en Windows, activar una ventana es subirla: se pone
        // delante de las ventanas que no son de nadie (las hojas sin duena y sin modal, la de prueba F12), que quedan detras de ella
        // (DetrasDeLaVentana); las que tienen duena o son modales siguen encima (una ventana con duena va siempre encima de su duena). La hoja vuelve
        // delante cuando se pone otra vez al frente (AlFrente, Activate).
        internal static void DesactivaPorLaVentana()
        {
            bool cambio = false;
            foreach (Hoja o in abiertas) if (!o.modal && o.duena == null && !o.DetrasDeLaVentana) { o.DetrasDeLaVentana = true; cambio = true; }
            if (cambio) Compone();
            Hoja a = Activa; if (a == null) return; Activa = null; if (!a.disposed) a.Invalidate();
        }
        // detras de la ventana de la rocola: no se ve, ni recibe el raton (la rocola ocupa la pantalla entera)
        internal bool DetrasDeLaVentana;
        // activar otra vez una ventana (la que estaba activa al abrir un dialogo, al cerrarlo), de las dos procedencias
        private static void ActivaVentana(object v)
        {
            Hoja h = v as Hoja;
            if (h != null) { ActivaHoja(h, true); return; }
            Action<object> a = ActivaLaVentana; if (a != null) a(v);
        }

        // ---------- ensenarla sin modal (Form.Show) ----------
        // SHOW (Control.Visible = true: Form.SetVisibleCore, Form.cs:2507-2631): sin duena y sin bucle propio; vuelve en cuanto se ve y se
        // activa (la ventana que estaba activa deja de serlo). Shown queda en la cola (AlEnsenarse). Una hoja desechada no se ensena
        // (Control.SetVisibleCore: ObjectDisposedException); una que ya se ve, no hace nada.
        public void Muestra()
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            if (visible) return;
            razonCierre = RazonCierre.None; resultado = Resultado.None; llamadoClosing = false;
            Ensena();
        }
        // LO QUE LA HOJA AVISA A QUIEN LA HEREDA (los OnShown y OnFormClosed de Form; de fabrica nada): Shown, ya ensenada y en la cola (BeginInvoke
        // de OnLoad), y FormClosed, al cerrarse por Close o por el apagado (despues del aviso de «closed», antes de desecharse)
        internal virtual void AlEnsenarse() { }
        internal virtual void AlCerrada(RazonCierre razon) { }

        // ---------- modal (ShowDialog) ----------
        // MODAL (Form.ShowDialog, Form.cs:5669-5823): la duena es la que se da (la ventana de la rocola, u otra hoja) o, sin ella, la activa;
        // mientras tanto sigue todo lo demas (los relojes de la rocola, los encargos, la tarjeta: el bucle de la hoja es el mismo bucle de la
        // pantalla, anidado: PantallaRocola.CorreModal) y las ventanas de debajo no reciben teclas ni raton, sin tocar su Enabled. Al acabar se
        // activa otra vez la que estaba activa (o la duena), se esconde, se destruye su ventana y se devuelve el resultado.
        public Resultado MuestraModal(object duenaDada)
        {
            if (duenaDada == this) throw new ArgumentException("Forms cannot own themselves or their owners.", "owner");
            if (visible) throw new InvalidOperationException("Form that is already visible cannot be displayed as a modal dialog box.");
            if (modal) throw new InvalidOperationException("Form that is already displayed modally cannot be displayed as a modal dialog box.");
            cargada = false; razonCierre = RazonCierre.None;
            object activa = LaActiva;
            duena = duenaDada ?? activa;
            try
            {
                modal = true; resultado = Resultado.None; llamadoClosing = false;
                CreaVentana();
                try
                {
                    Ensena();
                    modales.Add(this);
                    try { PantallaRocola.CorreModal(delegate { return !CheckCloseDialog(false) && !PantallaRocola.Saliendo; }); }
                    finally { modales.Remove(this); }
                }
                finally
                {
                    object vuelve = activa != null && activa != this && VivaYVisible(activa) ? activa
                                  : duena != null && duena != this && VivaYVisible(duena) ? duena : null;
                    if (vuelve != null) ActivaVentana(vuelve);
                    if (!disposed) { Esconde(true); DestruyeVentana(); }
                    modal = false;
                }
            }
            finally { duena = null; }
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("dialogo-fin " + TrazaVentanas.Pieza(this) + " " + resultado);
            return resultado;
        }
        internal object Duena { get { return duena; } }

        // ---------- cerrar (Form.Close y WmClose, Form.cs:3323-3341 y 6638-6783) ----------
        // Con la ventana creada, WM_CLOSE con CloseReason.UserClosing; si no, se desecha. Un dialogo pone Cancel si no tenia resultado y
        // solo avisa el cierre (que puede impedirlo, AlCerrarse); el cierre de verdad lo hace su bucle al acabar (CheckCloseDialog).
        public void Close()
        {
            if (vida != 0) { razonCierre = RazonCierre.UserClosing; WmClose(); }
            else Dispose();
        }
        // WM_SYSCOMMAND SC_CLOSE: la «X» de la barra de titulo y Alt+F4
        internal void CierraPorSistema() { razonCierre = RazonCierre.UserClosing; WmClose(); }
        private void WmClose()
        {
            if (modal)
            {
                if (resultado == Resultado.None) resultado = Resultado.Cancel;
                llamadoClosing = false;
                CheckCloseDialog(true);
                return;
            }
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + TrazaVentanas.Pieza(this) + " " + razonCierre);
            if (AlCerrarse(razonCierre)) return;
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + TrazaVentanas.Pieza(this) + " " + razonCierre);
            AlCerrada(razonCierre);
            Dispose();
        }
        // Form.CheckCloseDialog (Form.cs:3268-3318): si el dialogo ya tiene resultado (o se escondio), el aviso de que se cierra (una vez; si
        // se impide, el resultado vuelve a None) y, si no es solo eso, el de que se cerro. true = el dialogo se acaba.
        internal bool CheckCloseDialog(bool soloClosing)
        {
            if (disposed) return true;
            if (resultado == Resultado.None && visible) return false;
            try
            {
                if (!llamadoClosing)
                {
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + TrazaVentanas.Pieza(this) + " " + razonCierre);
                    if (AlCerrarse(razonCierre)) resultado = Resultado.None; else llamadoClosing = true;
                }
                if (!soloClosing && resultado != Resultado.None)
                {
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + TrazaVentanas.Pieza(this) + " " + razonCierre);
                    llamadoClosing = false;
                    AlCerrada(razonCierre);
                }
            }
            catch (Exception ex) { resultado = Resultado.None; PantallaRocola.Falla(ex); }
            return resultado != Resultado.None || !visible;
        }

        // ---------- el apagado del sistema (SIGTERM: lo que en XP es el apagado de Windows) ----------
        // WM_QUERYENDSESSION y WM_ENDSESSION (Form.WndProc, Form.cs:7208-7210, y WmClose, 6638-6783), como Form.FinDeSesion de la capa: con la consulta, un
        // dialogo pone Cancel si no tenia resultado y avisa que se cierra por CheckCloseDialog; con el fin, que se cerro y se desecha. Quien
        // pregunta a todas las ventanas de arriba abajo es la pantalla (PantallaRocola.FinDeSesion), las hojas primero (estan encima).
        internal bool FinDeSesion(bool consulta, bool seAcaba)
        {
            if (disposed || vida == 0) return true;
            razonCierre = RazonCierre.WindowsShutDown;
            if (consulta)
            {
                if (modal)
                {
                    if (resultado == Resultado.None) resultado = Resultado.Cancel;
                    llamadoClosing = false;
                    return CheckCloseDialog(true);
                }
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + TrazaVentanas.Pieza(this) + " " + razonCierre);
                return !AlCerrarse(razonCierre);
            }
            if (!seAcaba) return true;
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + TrazaVentanas.Pieza(this) + " " + razonCierre);
            AlCerrada(razonCierre);
            Dispose();
            return true;
        }
        // se cierra la ventana de la rocola, que es la duena de hojas: las suyas, de la ultima a la primera, reciben el aviso con
        // FormOwnerClosing (Form.WmClose, Form.cs:6696-6708 y 6763-6774: OwnedForms); con 'cerrada', el de que se cerraron
        internal static bool LaDuenaSeCierra(object d, bool cerrada)
        {
            bool impide = false;
            for (int i = abiertas.Count - 1; i >= 0; i--)
            {
                Hoja h = abiertas[i];
                if (h.disposed || h.duena != d || d == null) continue;
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta((cerrada ? "closed " : "closing ") + TrazaVentanas.Pieza(h) + " " + RazonCierre.FormOwnerClosing);
                if (!cerrada && h.AlCerrarse(RazonCierre.FormOwnerClosing)) { impide = true; break; }
            }
            return !impide;
        }

        // ---------- desecharla (Form.Dispose y Control.Dispose, Control.cs:5296-5395): se esconde, se destruye su ventana, se desechan sus
        // piezas (cada una con su Disposed) ----------
        public void Dispose()
        {
            if (disposed) return;
            Esconde(false);
            DestruyeVentana();
            modales.Remove(this);
            if (Activa == this) Activa = null;
            IPieza[] l = Controls.Lista.ToArray(); Controls.Lista.Clear();
            foreach (IPieza x in l) { x.Caja.PonPadre(null); IDisposable d = x as IDisposable; if (d != null) d.Dispose(); else x.Caja.Desecha(null); }
            disposed = true;
            TexturaGl = 0;
            if (superficie != null) { superficie.Dispose(); superficie = null; }
        }

        // ---------- LA TARJETA EN EL CLIENTE DE LA HOJA (la ventana de prueba F12) ----------
        // En XP, un dispositivo de Direct3D 9 creado sobre la ventana (CreateDevice con su Handle) presenta su fondo en el cliente. Aqui el
        // dispositivo recibe el identificador de la ventana (IdVentana: la vida de la hoja, que no choca con ningun otro: los de las hojas empiezan
        // en 0x40000000) y, al presentar, la pantalla se lo pasa a la hoja (DeLaVentana y PresentaTarjeta): la composicion pone su textura sobre el
        // cliente, encima de la superficie de la hoja y debajo de la hoja que venga despues. Las capas las da el anfitrion (Nativo/Anfitrion.cs,
        // Capas).
        internal uint TexturaGl; internal int AnchoGl, AltoGl;
        internal IntPtr IdVentana { get { return new IntPtr(vida); } }   // 0: sin ventana
        internal static Hoja DeLaVentana(IntPtr id)
        {
            long v = id.ToInt64();
            if (v == 0) return null;
            for (int i = abiertas.Count - 1; i >= 0; i--) { Hoja h = abiertas[i]; if (!h.disposed && h.vida == v) return h; }
            return null;
        }
        // Present: el fondo del dispositivo pasa a ser lo de dentro de la hoja (la pantalla compone despues)
        internal void PresentaTarjeta(uint textura, int ancho, int alto) { TexturaGl = textura; AnchoGl = ancho; AltoGl = alto; }
        // el dispositivo se suelta: la ventana ya no tiene fondo que componer
        internal void OlvidaTarjeta() { if (TexturaGl == 0) return; TexturaGl = 0; Compone(); }
        // el cliente en la pantalla (donde Direct3D pone el fondo)
        internal Rectangle ClienteEnPantalla { get { return new Rectangle(bounds.X + Marco, bounds.Y + Marco + AltoBarra, ClientSize.Width, ClientSize.Height); } }

        // ---------- la superficie de la hoja: se pinta entera cuando algo cambio (como la capa de la referencia) y la composicion la sube a
        // la tarjeta solo si cambio (Version) ----------
        private SKBitmap superficie;
        private bool sucia = true;
        internal int Version { get; private set; }
        internal void Invalidate() { sucia = true; Version++; Compone(); }
        // la superficie y la version que quedo pintada en ella (la de ANTES de pintar: si al pintarse algo la vuelve a ensuciar -la «X»
        // roja de una pieza que lanza, PintaFondoYContenido-, la siguiente vez se pinta otra vez con otra version y quien compone la sube;
        // leyendo la version despues de pintar, esa segunda pintura no se subia: revision del paso H)
        internal SKBitmap Superficie(out int version) { version = Version; return Superficie(); }
        // (banco, paso C) quien quiera medir cuanto tarda cada pintura entera de una hoja: la hoja y sus milisegundos (lo pone
        // Plataforma/Anfitrion.cs con ROCOLA_MIDE_HOJA=1; sin el, nada)
        internal static Action<Hoja, double> MidePintura;
        internal SKBitmap Superficie()
        {
            if (!sucia && superficie != null) return superficie;
            sucia = false;
            if (superficie == null || superficie.Width != Math.Max(1, Width) || superficie.Height != Math.Max(1, Height))
            {
                if (superficie != null) superficie.Dispose();
                superficie = Mapas.Nuevo(Width, Height);
            }
            long t0 = MidePintura != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            using (SKCanvas c = new SKCanvas(superficie))
            {
                c.Clear(Color.Transparent.Sk());
                PintaTodo(c);
                c.Flush();
            }
            if (MidePintura != null) MidePintura(this, (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            return superficie;
        }
    }
}

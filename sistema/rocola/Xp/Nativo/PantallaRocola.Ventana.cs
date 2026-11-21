// LA VENTANA DE LA ROCOLA EN LA PANTALLA NATIVA, SIN LA CAPA (port nativo, oleada 8, grupo form1-sin-form: W7). En XP la rocola era una
// Form de WinForms sin borde y maximizada: la unica ventana del programa aparte de las que ella abre (las hojas: Ctrl+F2, Ctrl+F3, los
// avisos, la de prueba F12). Aqui su ventana es la pantalla misma: sus vistas son la lista de la ventana (PantallaRocola.Vistas.cs), su
// fondo el primer tramo de la composicion y sus teclas PantallaRocola.Teclas. Este archivo lleva lo que Windows y WinForms hacian con esa
// ventana y la rocola de XP supone, con las reglas de la fuente de WinForms (dotnet/winforms release/3.0, portada de .NET Framework 4.8:
// Form.cs, Control.cs, ContainerControl.cs) tal como las fijaron las 41 pruebas en la capa de la transicion (en la referencia, Plataforma/Ventanas.Form.cs y
// Ventanas.Escritorio.cs, de donde sale cada regla):
//   - la medida: 300x300 al nacer con el marco de una ventana normal de XP (Sizable: 4 de marco y 26 de barra); Size la cambia y avisa
//     (OnResize); sin borde, el cliente es la ventana; maximizada, al crearse toma la pantalla entera (otro OnResize);
//   - ensenarse (Form.SetVisibleCore, Form.cs:2507-2631): crea la ventana (una vida para sus encargos), maximiza, Load deja Shown en la
//     cola, se ve, pasa a ser la activa y pone el foco en la primera vista del tabulador (o en ella misma);
//   - la activa y el foco (WM_ACTIVATE y WM_SETFOCUS; Form.Active, ContainerControl.ActiveControl): frente a las hojas, que van siempre
//     encima; al volver de una hoja el foco regresa a la vista que lo tenia; Focus() de la ventana se lo da a su vista activa si se ve;
//   - cerrarse (Form.Close y WmClose, Form.cs:6638-6790): FormClosing, FormClosed y Dispose, con su razon (UserClosing con Close y Alt+F4,
//     WindowsShutDown con el apagado del sistema, antes sus hojas con FormOwnerClosing); un kill -9 no avisa de nada;
//   - desecharse (Form.Dispose y Control.Dispose, Control.cs:5296-5395): deja de ser la activa y de tener el foco, se destruye (lo
//     encargado a ella que no se atendio se pierde) y desecha a sus vistas, y cada vista a las suyas.
// Las trazas (TrazaVentanas) la nombran como la nombraba la capa: «Form1'Rocola'» (la clase de la rocola y su titulo).
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    internal static partial class PantallaRocola
    {
        private static VentanaNativa ventana;   // la de la rocola (o ninguna: las pruebas de la lista nativa)
        private static object activo;           // la vista activa de la ventana (ContainerControl.ActiveControl: la ultima que tuvo el foco)
        internal static string NombreDeLaVentana;   // (trazas) null: «ventana»
        private static string NombreVentana { get { return NombreDeLaVentana ?? "ventana"; } }
        private static string NombreDe(object c) { return c == LaVentana ? NombreVentana : c.GetType().Name; }
        // (pruebas: VolcadoEstado) quien tiene el foco y cual es la activa, con el nombre de las trazas; null si no hay ventana nativa
        internal static bool HayVentanaNativa { get { return ventana != null; } }
        internal static string NombreDelFoco { get { return foco == null ? "nadie" : foco == LaVentana ? NombreVentana : TrazaVentanas.Pieza(foco); } }
        internal static string NombreDeLaActiva { get { VentanaNativa w = ventana; return w != null && w.Activa && !w.Desechada ? NombreVentana : "nadie"; } }

        // EL FOCO (Escritorio.PonFoco de la capa: SetFocus de Windows y ContainerControl.ActiveControl): 'c' es una vista o la ventana
        // (LaVentana). La ventana que recibe el foco sin ser la activa pasa a serlo; la vista activa de la ventana es la del foco (ninguna,
        // si el foco es la ventana); el que lo tenia en una hoja lo pierde. (Los avisos GotFocus, LostFocus, Enter y Leave no los escucha
        // nadie en la ventana de la rocola.)
        internal static void PonFocoVentana(object c, bool porTeclado)
        {
            if (c == null) return;
            object antes = foco; object activoNuevo = c == LaVentana ? null : c;
            if (antes == c && activo == activoNuevo) return;
            VentanaNativa w = ventana;
            if (w != null && w.Visible && !w.Activa) ActivaVentana(false);
            activo = activoNuevo;
            foco = c;
            if (TrazaVentanas.Activa && antes != c) TrazaVentanas.Apunta("foco " + NombreDe(c));
            if (antes == null && w != null) Hoja.SueltaFoco();   // (el foco lo tenia una pieza de una hoja)
        }
        // LA VENTANA ACTIVA (Escritorio.ActivaForma: WM_ACTIVATE): la hoja que lo era deja de serlo y la ventana, si 'enfoca', pone el foco
        // donde lo tenia (Form.Active)
        private static void ActivaVentana(bool enfoca)
        {
            VentanaNativa w = ventana;
            if (w == null || w.Desechada) return;
            Hoja.DesactivaPorLaVentana();
            if (!w.Activa)
            {
                w.Activa = true;
                CambioVentana();
                if (enfoca) EnfocaAlActivar();
            }
            else if (enfoca && foco == null) EnfocaAlActivar();
        }
        // Form.EnfocaAlActivar de la capa: a su vista activa si se ve (si no, a la ventana); sin vista activa, a la primera del tabulador
        private static void EnfocaAlActivar()
        {
            VentanaNativa w = ventana;
            if (w == null || w.Desechada || !w.Visible) return;
            IVista a = activo as IVista; if (a != null && !EnLaLista(a)) a = null;
            if (a == null) { if (!SiguienteVista(LaVentana, null, true, true, true, false)) PonFocoVentana(LaVentana, false); return; }
            if (a.Visible) PonFocoVentana(a, false); else PonFocoVentana(LaVentana, false);
        }
        // una ventana debajo de una hoja modal esta deshabilitada: no toma el foco ni se activa (Escritorio.Deshabilitada)
        private static bool VentanaDeshabilitada { get { return Hoja.HayModal; } }

        // EL APAGADO DEL SISTEMA SIN LA CAPA (Escritorio.FinDeSesion): WM_QUERYENDSESSION a cada ventana de arriba abajo (las hojas, que van
        // encima; la rocola; las hojas que quedaron detras de ella) y, si ninguna lo impidio, WM_ENDSESSION con la sesion acabandose en el
        // mismo orden; si una lo impidio, las que ya contestaron reciben WM_ENDSESSION sin acabarse.
        internal static void FinDeSesion()
        {
            List<object> vs = new List<object>();
            Hoja[] hs = Hoja.Abiertas; for (int i = hs.Length - 1; i >= 0; i--) if (!hs[i].DetrasDeLaVentana) vs.Add(hs[i]);
            if (ventana != null && ventana.Visible && !ventana.Desechada) vs.Add(ventana);
            for (int i = hs.Length - 1; i >= 0; i--) if (hs[i].DetrasDeLaVentana) vs.Add(hs[i]);
            bool seAcaba = true; int hasta = -1;
            for (int i = 0; i < vs.Count; i++) { hasta = i; if (!FinDeSesionDe(vs[i], true, true)) { seAcaba = false; break; } }
            for (int i = 0; i <= hasta && i < vs.Count; i++) FinDeSesionDe(vs[i], false, seAcaba);
        }
        // EL CIERRE QUE PIDE ROCOLA OS (SIGUSR1; PantallaRocola.cs): un WM_CLOSE de fuera a la ventana de la rocola, este o no debajo de una
        // hoja modal (un mensaje puesto en la cola le llega tambien a una ventana deshabilitada). Sin ventana no hay a quien mandarselo.
        // Cerrada la rocola, el programa se acaba (Application.Run(form): al destruirse la ventana principal, ExitThread, y con el WM_QUIT
        // acaba tambien cualquier bucle modal que siga abierto: el de un aviso sobre una ventana sin duena, la de prueba F12).
        internal static void CierreDeFuera()
        {
            VentanaNativa w = ventana; if (w == null) return;
            w.CierraDeFuera();
            if (w.Desechada) Sal();
        }
        private static bool FinDeSesionDe(object v, bool consulta, bool seAcaba)
        {
            Hoja h = v as Hoja; if (h != null) return h.FinDeSesion(consulta, seAcaba);
            return ((VentanaNativa)v).FinDeSesion(consulta, seAcaba);
        }

        // LA VENTANA NATIVA DE LA ROCOLA: la abre la rocola al construirse (Nativo/Form1.Ventana.cs)
        internal static IVentanaDeLaRocola AbreNativa(AvisosDeLaVentana avisos)
        {
            VentanaNativa w = new VentanaNativa(avisos);
            ventana = w;
            PonContexto();   // (el constructor de Control: WindowsFormsSynchronizationContext.InstallIfNeeded)
            // las teclas de la ventana (Form1: ProcessCmdKey, KeyPress y OnKeyUp) y su cierre con Alt+F4
            TeclasDeLaVentana t = new TeclasDeLaVentana();
            t.ProcesaMando = avisos.ProcesaMando; t.Caracter = avisos.Caracter; t.TeclaArriba = avisos.TeclaArriba;
            t.CierraPorSistema = delegate { if (!w.Desechada && w.Creada) w.CierraPorSistema(); };
            Teclas = t;
            DuenoDeLasTeclas = w;
            FondoDeLaVentana = w.PintaFondo;
            PonElPintado();
            // la ventana de delante y el foco que la rocola pide (ForceFocus, Form1.cs): PantallaRocola.Teclado.cs
            LaDeDelante = delegate { if (Hoja.Activa != null) return Hoja.Activa; return w.Activa && !w.Desechada ? avisos.Rocola : null; };
            PonDelante = delegate (object v) { if (v == avisos.Rocola && !w.Desechada) w.Activate(); };
            PonEncima = delegate (object v) { if (v == avisos.Rocola) HayQueComponer(); };
            PonFoco = delegate (object v) { if (v == avisos.Rocola && !w.Desechada) w.Enfoca(); };
            // lo que las hojas le piden a la ventana de debajo (Nativo/Hoja.cs: la activa, el foco, la duena)
            Hoja.VentanaActiva = delegate { return w.Activa && !w.Desechada ? avisos.Rocola : null; };
            Hoja.CedeLaVentana = delegate { if (!w.Activa) return; w.Activa = false; CambioVentana(); };
            Hoja.QuitaFocoALaVentana = delegate { foco = null; };
            Hoja.ActivaLaVentana = delegate (object v) { if (v == avisos.Rocola) ActivaVentana(true); };
            Hoja.VentanaViva = delegate (object v) { return v == avisos.Rocola && !w.Desechada && w.Visible; };
            Hoja.SitioDeLaVentana = delegate (object v) { return v == avisos.Rocola ? new Rectangle(Point.Empty, w.Medida) : Rectangle.Empty; };
            Hoja.HayQueComponer = HayQueComponer;
            Hoja.ActivaLaVentanaDeDebajo = delegate { if (w.Visible && !w.Desechada) ActivaVentana(true); };
            return w;
        }

        internal sealed class VentanaNativa : IVentanaDeLaRocola, IDuenoDeEncargos
        {
            private readonly AvisosDeLaVentana avisos;
            private string titulo = "";
            private Size medida = new Size(300, 300);   // (Form.DefaultSize)
            private bool conBorde = true, maximizada, creada, visible, cargada, hechoVisible, desechada, desechandose;
            private long vida;
            private static long ultimaVida = 0x20000000;   // (no choca con las de la capa, desde 0x1000, ni con las de las hojas, desde 0x40000000)
            private Color fondo = Color.FromArgb(236, 233, 216);   // (SystemColors.Control de XP Luna: el BackColor de fabrica)
            private RazonCierre razon = RazonCierre.None;
            internal bool Activa;
            internal VentanaNativa(AvisosDeLaVentana a) { avisos = a; }
            private string Nombre { get { return avisos.Rocola.GetType().Name + (string.IsNullOrEmpty(titulo) ? "" : "'" + TrazaVentanas.Limpio(titulo) + "'"); } }

            public string Titulo { get { return titulo; } set { titulo = value ?? ""; NombreDeLaVentana = Nombre; } }
            // el marco de una ventana normal de XP (Form.cs de la capa: Sizable con barra, 4 de marco y 26 de barra); sin borde, nada
            public Size Cliente
            {
                get { return conBorde ? new Size(Math.Max(0, medida.Width - 8), Math.Max(0, medida.Height - 8 - 26)) : medida; }
                set { Medida = conBorde ? new Size(value.Width + 8, value.Height + 8 + 26) : value; }
            }
            public Size Medida { get { return medida; } set { PonMedida(value); } }
            public Size MedidaMinima { set { } }   // (la rocola va siempre maximizada: nadie la encoge)
            // Control.SetBounds y SetBoundsCore: con otra medida, OnResize (y se pinta de nuevo)
            private void PonMedida(Size s)
            {
                if (s.Width < 0) s.Width = 0; if (s.Height < 0) s.Height = 0;
                if (s == medida) return;
                medida = s;
                avisos.CambioDeMedida();
                CambioVentana();
            }
            public Color Fondo { get { return fondo; } set { if (fondo == value) return; fondo = value; CambioVentana(); } }
            public void SinBorde() { conBorde = false; if (creada && visible && maximizada) PonMedida(new Size(Ancho, Alto)); CambioVentana(); }
            public void Maximiza() { bool antes = maximizada; maximizada = true; if (!creada || !visible || antes) return; PonMedida(new Size(Ancho, Alto)); }
            public bool Minimizada { get { return false; } }   // (nadie la minimiza: en Rocola OS no hay barra de tareas)
            public bool Creada { get { return creada; } }
            public bool Desechada { get { return desechada; } }
            public bool Desechandose { get { return desechandose; } }
            public long Vida { get { return vida; } }
            public bool Habilitada { get { return true; } }   // (XP nunca la deshabilita)
            public bool ContieneElFoco { get { return foco != null; } }
            internal bool Visible { get { return visible; } }
            // Control.Focus de un contenedor (ContainerControl.WmSetFocus): a su vista activa si se ve; si no, a la ventana
            public bool Enfoca()
            {
                if (!visible || VentanaDeshabilitada) return false;
                IVista a = activo as IVista;
                if (a != null && EnLaLista(a) && a.Visible) PonFocoVentana(a, false);
                else PonFocoVentana(LaVentana, false);
                return foco != null;
            }
            // Control.Invalidate(bool): su fondo se pinta de nuevo; con las hijas, tambien las que pinta la tarjeta (su WM_PAINT) y las que
            // tienen hijas (la capa: Ventanas.Control.cs, Invalidate(bool))
            public void Invalida(bool conHijas)
            {
                CambioVentana();
                if (!conHijas) return;
                foreach (object o in ListaDeLaVentana)
                {
                    IVista v = o as IVista;
                    if (v != null ? v.DeLaTarjeta : o is CapaDeTextura) PorPintarDeLaVentana(o);
                }
            }
            public void PintaFondoDeFabrica(SKCanvas g) { if (fondo.A != 0) PinturaXp.Rellena(g, new Rectangle(Point.Empty, Cliente), fondo); }
            // el fondo de la ventana en el primer tramo de la composicion (OnPaintBackground; OnPaint de fabrica no pinta nada), recortado
            // a su cliente
            internal void PintaFondo(SKCanvas g)
            {
                Size c = Cliente;
                g.ClipRect(new SKRect(0, 0, c.Width, c.Height));
                avisos.PintaFondo(g);
            }
            // ENSENARSE (Form.SetVisibleCore)
            public void Ensena()
            {
                if (desechada) throw new ObjectDisposedException(avisos.Rocola.GetType().Name);
                if (visible) return;
                hechoVisible = true;
                if (!creada)
                {
                    creada = true; vida = ++ultimaVida;                           // (CreateHandle)
                    if (maximizada) PonMedida(new Size(Ancho, Alto));             // (WS_MAXIMIZE: la pantalla entera, sin marco)
                }
                if (hechoVisible && !cargada)                                     // (OnCreateControl: Load, que deja Shown en la cola)
                {
                    cargada = true;
                    EncolaDesde(this, delegate { if (TrazaVentanas.Activa) TrazaVentanas.Apunta("shown " + Nombre); avisos.Ensenada(); }, "Shown", false);
                }
                visible = true;
                VentanaSeVe();
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("ventana-ve " + Nombre);
                Activate();
            }
            // Form.Activate (Form.cs:2869-2882): delante y activa, salvo debajo de una modal
            internal void Activate() { if (!visible || VentanaDeshabilitada) return; HayQueComponer(); ActivaVentana(true); }
            // CERRAR (Form.Close y WmClose)
            public void Cierra() { if (creada) { razon = RazonCierre.UserClosing; WmClose(); } else Desecha(); }
            internal void CierraPorSistema() { razon = RazonCierre.UserClosing; WmClose(); }
            private void WmClose()
            {
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + Nombre + " " + razon);
                avisos.Cerrandose(razon);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + Nombre + " " + razon);
                avisos.Cerrada(razon);
                Desecha();
            }
            // EL WM_CLOSE QUE LLEGA DE FUERA (Form.WndProc, Form.cs:7200-7206: sin razon puesta, CloseReason.TaskManagerClosing; y WmClose de
            // una ventana que no es modal, 6638-6783): primero el aviso de que se cierra a las hojas de las que es duena, de la ultima a la
            // primera, con FormOwnerClosing (una puede impedirlo; las de XP no lo hacen), y a ella SIEMPRE (OnFormClosing se llama aunque
            // una hoja lo impida; la rocola no mira e.Cancel); si nadie lo impidio, el de que se cerraron a sus hojas y a ella, y Dispose
            // (que desecha tambien sus hojas: DesechaLoSuyo). Con una razon que no es WindowsShutDown ni UserClosing la rocola guarda todo
            // y conserva el estado vivo (Form1.Video.cs, OnFormClosing).
            internal void CierraDeFuera()
            {
                if (desechada || !creada) return;
                if (razon == RazonCierre.None) razon = RazonCierre.TaskManagerClosing;
                bool impide = !Hoja.LaDuenaSeCierra(avisos.Rocola, false);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + Nombre + " " + razon);
                avisos.Cerrandose(razon);
                if (impide) return;
                Hoja.LaDuenaSeCierra(avisos.Rocola, true);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + Nombre + " " + razon);
                avisos.Cerrada(razon);
                Desecha();
            }
            // EL APAGADO (Form.FinDeSesion de la capa: Form.cs:7208-7211 y WmClose): con la consulta, antes sus hojas (FormOwnerClosing) y
            // despues ella; con el fin, sus hojas, ella y Dispose
            internal bool FinDeSesion(bool consulta, bool seAcaba)
            {
                if (desechada || !creada) return true;
                razon = RazonCierre.WindowsShutDown;
                if (consulta)
                {
                    bool impide = !Hoja.LaDuenaSeCierra(avisos.Rocola, false);
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + Nombre + " " + razon);
                    avisos.Cerrandose(razon);
                    return !impide;
                }
                if (!seAcaba) return true;
                Hoja.LaDuenaSeCierra(avisos.Rocola, true);
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + Nombre + " " + razon);
                avisos.Cerrada(razon);
                Desecha();
                return true;
            }
            // la rocola es su duena, o la duena de su duena... (la cadena de OwnedForms)
            private bool EsDeLaRocola(Hoja h)
            {
                for (int n = 0; h != null && n < 64; n++)
                {
                    object d = h.Duena;
                    if (d == avisos.Rocola) return d != null;
                    h = d as Hoja;
                }
                return false;
            }
            // DESECHAR (Form.Dispose(): Dispose(true), el de la rocola, que acaba en DesechaLoSuyo)
            public void Desecha() { avisos.Desecha(true); }
            public void DesechaLoSuyo(bool disposing)
            {
                if (!disposing) return;
                // (Form.Dispose, Form.cs:3632-3642: antes de lo de Control, las ventanas de las que es duena se desechan, de la ultima a la
                // primera. Solo las hay abiertas si la rocola se cierra debajo de un dialogo: el cierre de fuera, CierraDeFuera; con Alt+F4 el
                // dialogo se cierra el, y en el apagado cada hoja ya se desecho con su WM_ENDSESSION)
                if (!desechada && !desechandose)
                {
                    Hoja[] hs = Hoja.Abiertas;
                    // (y las de sus hojas: cada Form.Dispose desecha las suyas, y Windows destruye con su duena el aviso de user32 y el
                    // selector de carpetas. El selector o un aviso abiertos sobre Ctrl+F2 tienen de duena a esa hoja, no a la rocola: sin
                    // desecharlos, su bucle modal seguiria con la rocola ya cerrada y el programa no saldria)
                    for (int i = hs.Length - 1; i >= 0; i--) if (!hs[i].IsDisposed && EsDeLaRocola(hs[i])) hs[i].Dispose();
                }
                if (!desechada)
                {
                    // (Form.Dispose: Escritorio.Quita) ya no tiene el foco ni es la activa
                    if (foco != null) { foco = null; if (TrazaVentanas.Activa) TrazaVentanas.Apunta("foco nadie"); }
                    Activa = false;
                    visible = false;
                    HayQueComponer();
                }
                if (desechandose || desechada) return;
                desechandose = true;
                try
                {
                    vida = 0; creada = false;   // (DestroyHandle: lo encargado a ella que no se atendio se pierde)
                    DesechaVistas();
                }
                finally { desechandose = false; desechada = true; }
            }
        }

        // Control.Dispose: «los hijos se desechan» (Control.cs:5351-5363), en el orden de la coleccion; cada vista, lo suyo (su Dispose(bool)
        // de XP, si lo tiene) y despues sus hijas (base.Dispose). La lista se vacia antes, como ControlCollection.Clear.
        private static void DesechaVistas()
        {
            object[] l = lista.ToArray();
            lista.Clear(); activo = null; bajoRaton = null; captura = null;
            CambioVentana();
            foreach (object o in l) DesechaVista(o as IVista);
        }
        private static void DesechaVista(IVista v)
        {
            if (v == null) return;
            List<object> hs; hijas.TryGetValue(v, out hs);
            object[] suyas = hs != null ? hs.ToArray() : null;
            try { IDisposable d = v as IDisposable; if (d != null) d.Dispose(); }
            finally
            {
                if (suyas != null)
                {
                    hijas.Remove(v);
                    foreach (object o in suyas) { IVista h = o as IVista; if (h != null) { padre.Remove(h); DesechaVista(h); } }
                }
            }
        }
    }
}

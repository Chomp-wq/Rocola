// EL FOCO DEL TECLADO ENTRE LAS VISTAS DE LA VENTANA (port nativo, oleada 6c, grupo vistas-buscador: W3c). Lo que en XP hacia WinForms con el
// foco de los controles de la ventana de la rocola cuando no hay capa (la lista nativa de PantallaRocola.Vistas.cs): el orden del tabulador
// (TabIndex, que pone Controls.Add), Tab y Mayus+Tab y las flechas (la parte de ContainerControl.ProcessDialogKey), y que el foco salga de una
// vista que se esconde (SelectNextIfFocused). Con la capa de la transicion no se usa: el hueco de cada vista (Plataforma/Anfitrion.cs) es un
// Control con el Selectable y el TabStop de la vista (IVista.Seleccionable y ParaElTabulador) y la capa hace todo eso con sus reglas, que
// fijan las 41 pruebas. Es lo mismo que Nativo/Hoja.Foco.cs hace con las piezas de una hoja, aqui con las vistas.
//
// Las reglas, de la fuente de WinForms (dotnet/winforms release/3.0, portada de .NET Framework 4.8; /tmp/claude-0/agente-winforms/ref):
//   - TabIndex: Control.ControlCollection.Add le pone al que no lo tiene el mayor de sus hermanos mas uno (Control.ControlCollection.cs:
//     82-95: «if (value._tabIndex == -1) { int nextTabIndex = 0; for (int c = 0; c < (Count - 1); c++) { int t = this[c].TabIndex; if
//     (nextTabIndex <= t) nextTabIndex = t + 1; } value._tabIndex = nextTabIndex; }», con TabIndex = 0 en el que no lo tiene: Control.cs:3614-3616); no cambia al
//     traerlo al frente, al quitarlo y volverlo a poner ni al pasarlo a otro dueno. (gpupanel-sin-control) Antes era un contador comun: da
//     el mismo orden entre las hermanas de una misma coleccion mientras nadie cambie de dueno, pero no el de WinForms cuando una que ya
//     tenia TabIndex entra en otra coleccion (en WinForms empata con las de alli y decide su lugar en la lista); ahora es por coleccion,
//     contando tambien lo que no se elige (capas de textura y rotulos: en XP, controles con su TabIndex).
//   - Control.Focus (CanFocus: visible y habilitada; no mira Selectable ni TabStop): PantallaRocola.EnfocaVista (PantallaRocola.Vistas.cs).
//   - CanSelect (Control.CanSelectCore: ControlStyles.Selectable, y habilitada y visible ella y sus duenos): Seleccionable y Visible (una
//     vista no tiene Enabled: siempre lo esta). TabStop: ParaElTabulador.
//   - SelectNextControl y GetNextControl (Control.cs:10709-10721 y 6177-6351): igual que en Hoja.Foco.cs; la ventana es el unico
//     ContainerControl (el panel del disco es un Panel: el tabulador entra en sus hijas).
//   - Form.ProcessTabKey (Form.cs:4995-5015): SelectNextControl(ActiveControl, forward, true, true, true); ContainerControl.ProcessArrowKey
//     (ContainerControl.cs:1119-1128): dentro del padre del activo, SelectNextControl(activo, forward, false, false, true); los dos solo sin
//     Alt ni Control (ContainerControl.ProcessDialogKey, ContainerControl.cs:1156-1185).
//   - Control.SetVisibleCore (Control.cs:11221-11228): al esconder una que tiene el foco (ContainsFocus: ella o algo de dentro),
//     SelectNextIfFocused (Control.cs:10770-10784): su contenedor (la ventana) elige la siguiente, SelectNextControl(this, true, true, true,
//     true). Si no hay ninguna, el foco no se mueve (Windows no se lo quita a una ventana hija que se esconde).
// ActiveControl de la ventana es la vista del foco (FocoDeLaVentana) si esta en ella. Ninguna clase imita a Control: solo son reglas sobre
// las vistas que la rocola ya tiene.
using System;
using System.Collections.Generic;

namespace Rocola
{
    internal static partial class PantallaRocola
    {
        // la ventana como contenedor del tabulador (las vistas de la lista nativa son sus hijas)
        private static readonly object LaVentana = new object();
        // (junta-rotulos, revision) el TabIndex de cada una, guardado con ella como el campo _tabIndex de Control: una tabla debil (lo que se
        // quita y nadie guarda -cada video crea su capa: VideoEnPantalla.Lugar- no se queda para siempre aqui; mientras vive, lo conserva)
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> tabIndex = new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>();
        // Controls.Add en 'coleccion' (la lista de la ventana o las hijas de una vista, ya con 'v' dentro): si aun no tiene TabIndex, el mayor
        // de los demas de la coleccion mas uno (Control.ControlCollection.Add, arriba)
        internal static void AnotaTab(object v, List<object> coleccion)
        {
            object ya; if (v == null || tabIndex.TryGetValue(v, out ya)) return;
            int siguiente = 0;
            foreach (object o in coleccion) { if (o == v) continue; int t = TabDe(o); if (siguiente <= t) siguiente = t + 1; }
            tabIndex.Add(v, siguiente);
        }
        // (Control.TabIndex: 0 si nunca se puso)
        private static int TabDe(object v) { object t; return v != null && tabIndex.TryGetValue(v, out t) ? (int)t : 0; }

        // las hijas de un contenedor que son vistas (las capas de textura no se eligen ni tienen hijas: saltarlas da lo mismo; los rotulos -RotuloVista,
        // rotulos-y-letras- son vistas que no se eligen ni se paran en el tabulador: Seleccionable y ParaElTabulador false, como Label)
        private static List<IVista> HijasTab(object n)
        {
            List<IVista> r = new List<IVista>();
            List<object> l;
            if (n == LaVentana) { foreach (object o in lista) { IVista v = o as IVista; if (v != null) r.Add(v); } }
            else if (n is IVista && hijas.TryGetValue((IVista)n, out l)) { foreach (object o in l) { IVista v = o as IVista; if (v != null) r.Add(v); } }
            return r;
        }
        private static object PadreTab(object v) { IVista p = PadreDe(v as IVista); return p != null ? (object)p : (v is IVista && lista.Contains(v) ? LaVentana : null); }
        // Control.Contains: ctl esta dentro de n (no es n)
        private static bool ContieneTab(object n, object ctl)
        {
            if (ctl == null || ctl == n) return false;
            for (object a = PadreTab(ctl); a != null; a = a == LaVentana ? null : PadreTab(a)) if (a == n) return true;
            return false;
        }
        private static bool PuedeElegirVista(object o) { IVista v = o as IVista; return v != null && v.Seleccionable && v.Visible; }

        // la vista activa de la ventana (ContainerControl.ActiveControl): la del foco, si esta en la ventana
        // (form1-sin-form: es la ultima que tuvo el foco, aunque ahora lo tenga una hoja: PantallaRocola.Ventana.cs)
        private static IVista ActivaDeLaVentana { get { IVista v = activo as IVista; return v != null && EnLaLista(v) ? v : null; } }

        // SelectNextControl: la siguiente que se pueda elegir y el foco a ella (Select: con la traza «foco», como EnfocaVista)
        private static bool SiguienteVista(object n, object ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
        {
            if (!ContieneTab(n, ctl) || (!nested && PadreTab(ctl) != n)) ctl = null;
            bool yaDioVuelta = false;
            object inicio = ctl, hallada = null;
            do
            {
                ctl = SiguienteEnOrden(n, ctl, forward);
                if (ctl == null)
                {
                    if (!wrap) break;
                    if (yaDioVuelta) break;
                    yaDioVuelta = true;
                }
                else if (PuedeElegirVista(ctl) && (!tabStopOnly || ((IVista)ctl).ParaElTabulador) && (nested || PadreTab(ctl) == n)) { hallada = ctl; break; }
            }
            while (ctl != inicio);
            if (hallada == null) return false;
            PonFocoVentana(hallada, true);   // (form1-sin-form) Select: el foco de la ventana, con su traza (PantallaRocola.Ventana.cs)
            return true;
        }
        // GetNextControl: hacia delante se entra en las hijas y despues la hermana con el TabIndex siguiente (a igualdad, la que va despues
        // en la lista); hacia atras, la hermana anterior y su ultima hija; null al pasar de la ultima
        private static object SiguienteEnOrden(object n, object ctl, bool forward)
        {
            if (!ContieneTab(n, ctl)) ctl = n;
            if (forward)
            {
                if (HijasTab(ctl).Count > 0)
                {
                    object primero = PrimeraEnOrden(ctl, true);
                    if (primero != null) return primero;
                }
                while (ctl != n)
                {
                    int objetivo = TabDe(ctl);
                    bool vistoCtl = false;
                    object hallado = null;
                    List<IVista> hs = HijasTab(PadreTab(ctl));
                    for (int c = 0; c < hs.Count; c++)
                    {
                        if (hs[c] != ctl)
                        {
                            int t = TabDe(hs[c]);
                            if (t >= objetivo && (hallado == null || TabDe(hallado) > t) && (t != objetivo || vistoCtl)) hallado = hs[c];
                        }
                        else vistoCtl = true;
                    }
                    if (hallado != null) return hallado;
                    ctl = PadreTab(ctl);
                }
            }
            else
            {
                if (ctl != n)
                {
                    int objetivo = TabDe(ctl);
                    bool vistoCtl = false;
                    object hallado = null;
                    object p = PadreTab(ctl);
                    List<IVista> hs = HijasTab(p);
                    for (int c = hs.Count - 1; c >= 0; c--)
                    {
                        if (hs[c] != ctl)
                        {
                            int t = TabDe(hs[c]);
                            if (t <= objetivo && (hallado == null || TabDe(hallado) < t) && (t != objetivo || vistoCtl)) hallado = hs[c];
                        }
                        else vistoCtl = true;
                    }
                    if (hallado != null) ctl = hallado;
                    else return p == n ? null : p;
                }
                while (HijasTab(ctl).Count > 0)
                {
                    object ultima = PrimeraEnOrden(ctl, false);
                    if (ultima == null) break;
                    ctl = ultima;
                }
            }
            return ctl == n ? null : ctl;
        }
        private static object PrimeraEnOrden(object n, bool forward)
        {
            List<IVista> hs = HijasTab(n);
            IVista hallada = null;
            if (forward) { for (int c = 0; c < hs.Count; c++) if (hallada == null || TabDe(hallada) > TabDe(hs[c])) hallada = hs[c]; }
            else { for (int c = hs.Count - 1; c >= 0; c--) if (hallada == null || TabDe(hallada) < TabDe(hs[c])) hallada = hs[c]; }
            return hallada;
        }

        // LO QUE ContainerControl.ProcessDialogKey HACE EN LA VENTANA (despues de lo suyo de la ventana, TeclasDeLaVentana.TeclaDeDialogo):
        // Tab y Mayus+Tab (Form.ProcessTabKey) y las flechas (ProcessArrowKey), sin Alt ni Control
        private static bool TeclaDeDialogoDeLaVentana(Tecla datos)
        {
            if ((datos & (Tecla.Alt | Tecla.Control)) != Tecla.None) return false;
            Tecla k = datos & Tecla.KeyCode;
            IVista activa = ActivaDeLaVentana;
            if (k == Tecla.Tab) return SiguienteVista(LaVentana, activa, (datos & Tecla.Shift) == Tecla.None, true, true, true);
            if (k == Tecla.Left || k == Tecla.Right || k == Tecla.Up || k == Tecla.Down)
            {
                object grupo = activa != null ? PadreTab(activa) : LaVentana;
                return SiguienteVista(grupo, activa, k == Tecla.Right || k == Tecla.Down, false, false, true);
            }
            return false;
        }

        // UNA VISTA QUE SE VA A ESCONDER (CajaVista.Visible, antes de cambiar; solo si se veia): si el foco es ella o algo de dentro y esta en
        // la lista nativa, la ventana elige la siguiente (SelectNextIfFocused); en un hueco de la capa lo hace la capa
        internal static void SeEsconde(IVista v)
        {
            if (v == null || !EnLaLista(v) || !EnLaRama(foco, v)) return;
            SiguienteVista(LaVentana, v, true, true, true, true);
        }
    }
}

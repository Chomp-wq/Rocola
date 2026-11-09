// LA HOJA: EL FOCO Y EL TABULADOR (port nativo de las ventanas de XP, ventanas2, paso H; ver Hoja.cs). Como Windows y WinForms: una sola
// pieza tiene el foco del teclado (o la hoja misma, si no tiene ninguna que lo tome); la hoja recuerda su pieza activa
// (ContainerControl.ActiveControl) y se la devuelve al activarse; Tab y Mayus+Tab recorren las piezas en el orden del tabulador entrando en
// los grupos; las flechas pasan a la siguiente dentro del mismo padre, con vuelta; si la del foco se esconde o se apaga, el foco pasa a la
// siguiente. Se porto de la capa (Plataforma/Ventanas.Control.cs y Ventanas.Escritorio.cs: PonFoco, GetNextControl, SelectNextControl),
// revisada contra la fuente (Control.cs, ContainerControl.cs, Form.cs).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Hoja
    {
        // LA PIEZA CON EL FOCO (o la hoja misma), de todas las hojas: Windows tiene un foco por hilo (GetFocus)
        private static object foco;
        internal static object Foco { get { return foco; } }

        // ---------- el arbol: las hojas y los grupos tienen piezas; cada pieza su padre ----------
        private static Piezas HijasDe(object n) { Hoja h = n as Hoja; if (h != null) return h.Controls; IPieza p = n as IPieza; return p != null ? p.Caja.Hijas : null; }
        private static object PadreDe(object n) { IPieza p = n as IPieza; return p != null ? p.Caja.Padre : null; }
        private static int TabDe(object n) { IPieza p = n as IPieza; return p != null ? p.Caja.TabIndexPuesto : -1; }
        private static bool Desechada(object n) { Hoja h = n as Hoja; if (h != null) return h.disposed; IPieza p = n as IPieza; return p == null || p.Caja.IsDisposed; }
        internal static Hoja HojaDe(object n) { Hoja h = n as Hoja; if (h != null) return h; IPieza p = n as IPieza; return p != null ? p.Caja.Hoja : null; }
        // Control.Contains (Control.cs:4995-5010): 'ctl' esta dentro de 'n' (en cualquier nivel)
        private static bool Contiene(object n, object ctl) { for (object c = ctl; c != null; c = PadreDe(c)) if (PadreDe(c) == n) return true; return false; }
        // ContainsFocus: el foco es ella o esta dentro (x = null: la hoja)
        internal bool ContieneFoco(object x) { object f = foco; if (f == null) return false; object n = x ?? this; return f == n || Contiene(n, f); }
        // la pieza activa, si sigue siendo suya (ContainerControl.ActiveControl, ContainerControl.cs:284-288)
        internal IPieza ActiveControl { get { IPieza a = activo; return a != null && !a.Caja.IsDisposed && Contiene(this, a) ? a : null; } }
        // CanSelect (CanSelectCore, Control.cs:4920-4936) y CanFocus (Control.cs:1262-1274; debajo de una hoja modal, no)
        private static bool PuedeElegir(object n) { IPieza p = n as IPieza; return p != null && p.Caja.CanSelect; }
        private static bool TabStopDe(object n) { IPieza p = n as IPieza; return p != null && p.Caja.TabStop; }

        // ---------- poner el foco (Escritorio.PonFoco de la capa) ----------
        // porTeclado (Select: Tab, flechas, la activacion) o con el raton y Focus(): cambia la pieza activa de la hoja, la de antes pierde el
        // foco (LostFocus) y la nueva lo recibe (GotFocus); despues, el boton por defecto (Form.UpdateDefaultButton, Form.cs:5963-5990). Lo
        // que lancen los avisos va a ThreadException y el resto sigue (Application.OnThreadException desde WndProc).
        internal static void PonFoco(object c, bool porTeclado)
        {
            if (c == null || Desechada(c)) return;
            Hoja f = HojaDe(c);
            if (f == null) return;
            object antes = foco;
            IPieza activoNuevo = c as IPieza, activoAntes = f.activo;
            if (antes == c && activoAntes == activoNuevo) return;
            if (f.visible && Activa != f) ActivaHoja(f, false);
            if (porTeclado) f.activo = activoNuevo;
            foco = c;
            if (TrazaVentanas.Activa && antes != c) TrazaVentanas.Apunta("foco " + TrazaVentanas.Pieza(c));
            if (antes == null) { Action q = QuitaFocoALaCapa; if (q != null) q(); }   // (transicion) el foco lo tenia una pieza de la capa
            else if (antes != c && !Desechada(antes)) { IPieza pa = antes as IPieza; if (pa != null) PantallaRocola.Protege(pa.AlPerderFoco); }
            if (!porTeclado) f.activo = activoNuevo;
            if (antes != c) { IPieza pc = c as IPieza; if (pc != null) PantallaRocola.Protege(pc.AlRecibirFoco); }
            f.ActualizaBotonPorDefecto();
        }
        // nadie tiene el foco (WM_KILLFOCUS sin otro que lo tome: la hoja se esconde o se destruye)
        private static void QuitaFoco()
        {
            object antes = foco; foco = null;
            if (TrazaVentanas.Activa && antes != null) TrazaVentanas.Apunta("foco nadie");
            IPieza pa = antes as IPieza; if (pa != null && !pa.Caja.IsDisposed) PantallaRocola.Protege(pa.AlPerderFoco);
        }
        // (transicion) el foco se lo lleva una pieza de la capa (la capa ya apunta a quien): la de la hoja lo pierde
        internal static void SueltaFoco()
        {
            object antes = foco; if (antes == null) return; foco = null;
            IPieza pa = antes as IPieza; if (pa != null && !pa.Caja.IsDisposed) PantallaRocola.Protege(pa.AlPerderFoco);
        }
        // Control.Focus (Control.cs:5675-5681): si puede tomarlo (se ve, esta activa y su hoja no esta debajo de un modal)
        internal bool Enfoca(IPieza p)
        {
            if (!p.Caja.Visible || !p.Caja.Enabled || Deshabilitada(this)) return false;
            PonFoco(p, false);
            return ContieneFoco(p);
        }
        // AL ACTIVARSE (Form.Active, Form.cs:289-341, como la capa en Form.EnfocaAlActivar): el foco vuelve a su pieza activa si se ve, o a
        // la hoja; sin pieza activa, a la primera del tabulador (asi al abrir Ctrl+F2 el foco esta en la lista)
        internal void EnfocaAlActivar()
        {
            if (disposed || !visible) return;
            IPieza a = ActiveControl;
            if (a == null) { if (!SelectNextControl(this, null, true, true, true, false)) PonFoco(this, false); return; }
            if (a.Caja.Visible) PonFoco(a, false); else PonFoco(this, false);
        }
        // EL BOTON POR DEFECTO (Form.UpdateDefaultButton, Form.cs:5963-5990): el boton que tiene el foco o, si no, el de aceptar; Intro lo pulsa
        internal void ActualizaBotonPorDefecto()
        {
            BotonXp nuevo = ActiveControl as BotonXp ?? aceptar;
            if (nuevo == porDefecto) return;
            if (porDefecto != null) porDefecto.NotifyDefault(false);
            porDefecto = nuevo;
            if (nuevo != null) nuevo.NotifyDefault(true);
        }

        // ---------- el tabulador (Control.SelectNextControl y GetNextControl, Control.cs:10709-10721 y 6177-6351) ----------
        // la siguiente que se pueda elegir desde 'ctl' dentro de 'n' (con vuelta si 'wrap'); Select le da el foco por teclado
        internal static bool SelectNextControl(object n, object ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
        {
            object sig = SiguienteElegible(n, ctl, forward, tabStopOnly, nested, wrap);
            if (sig == null) return false;
            if (PuedeElegir(sig)) PonFoco(sig, true);   // (Control.Select(directed, forward): Selectable y CanSelect)
            return true;
        }
        private static object SiguienteElegible(object n, object ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
        {
            if (!Contiene(n, ctl) || (!nested && PadreDe(ctl) != n)) ctl = null;
            bool yaDioVuelta = false;
            object inicio = ctl;
            do
            {
                ctl = GetNextControl(n, ctl, forward);
                if (ctl == null)
                {
                    if (!wrap) break;
                    if (yaDioVuelta) return null;
                    yaDioVuelta = true;
                }
                else if (PuedeElegir(ctl) && (!tabStopOnly || TabStopDe(ctl)) && (nested || PadreDe(ctl) == n)) return ctl;
            }
            while (ctl != inicio);
            return null;
        }
        // un grupo no lleva su propio foco (no es ContainerControl): el tabulador entra en el
        private static bool ManejaFoco(object c) { return c is Hoja; }
        // EL SIGUIENTE EN EL ORDEN DEL TABULADOR (Control.GetNextControl, paso por paso): hacia delante se entra en las hijas y despues se busca
        // la hermana con el TabIndex siguiente (a igualdad, la que va despues en la lista); hacia atras, la hermana anterior y su ultima hija.
        // null al pasar de la ultima.
        private static object GetNextControl(object n, object ctl, bool forward)
        {
            if (!Contiene(n, ctl)) ctl = n;
            if (forward)
            {
                Piezas hs0 = HijasDe(ctl);
                if (hs0 != null && hs0.Count > 0 && (ctl == n || !ManejaFoco(ctl)))
                {
                    object primero = PrimeroEnOrden(ctl, true);
                    if (primero != null) return primero;
                }
                while (ctl != n)
                {
                    int objetivo = TabDe(ctl);
                    bool vistoCtl = false;
                    object hallado = null;
                    List<IPieza> hs = HijasDe(PadreDe(ctl)).Lista;
                    for (int c = 0; c < hs.Count; c++)
                    {
                        if (hs[c] != ctl)
                        {
                            int t = hs[c].Caja.TabIndexPuesto;
                            if (t >= objetivo && (hallado == null || TabDe(hallado) > t) && (t != objetivo || vistoCtl)) hallado = hs[c];
                        }
                        else vistoCtl = true;
                    }
                    if (hallado != null) return hallado;
                    ctl = PadreDe(ctl);
                }
            }
            else
            {
                if (ctl != n)
                {
                    int objetivo = TabDe(ctl);
                    bool vistoCtl = false;
                    object hallado = null;
                    object p = PadreDe(ctl);
                    List<IPieza> hs = HijasDe(p).Lista;
                    for (int c = hs.Count - 1; c >= 0; c--)
                    {
                        if (hs[c] != ctl)
                        {
                            int t = hs[c].Caja.TabIndexPuesto;
                            if (t <= objetivo && (hallado == null || TabDe(hallado) < t) && (t != objetivo || vistoCtl)) hallado = hs[c];
                        }
                        else vistoCtl = true;
                    }
                    if (hallado != null) ctl = hallado;
                    else return p == n ? null : p;
                }
                for (Piezas hs = HijasDe(ctl); hs != null && hs.Count > 0 && (ctl == n || !ManejaFoco(ctl)); hs = HijasDe(ctl))
                {
                    object ultimo = PrimeroEnOrden(ctl, false);
                    if (ultimo == null) break;
                    ctl = ultimo;
                }
            }
            return ctl == n ? null : ctl;
        }
        private static object PrimeroEnOrden(object n, bool forward)
        {
            List<IPieza> hs = HijasDe(n).Lista;
            IPieza hallado = null;
            if (forward) { for (int c = 0; c < hs.Count; c++) if (hallado == null || hallado.Caja.TabIndexPuesto > hs[c].Caja.TabIndexPuesto) hallado = hs[c]; }
            else { for (int c = hs.Count - 1; c >= 0; c--) if (hallado == null || hallado.Caja.TabIndexPuesto < hs[c].Caja.TabIndexPuesto) hallado = hs[c]; }
            return hallado;
        }
        // Tab (Form.ProcessTabKey, Form.cs:4995-5015: con vuelta) y las flechas (ContainerControl.ProcessArrowKey, ContainerControl.cs:
        // 1119-1128: la siguiente dentro del mismo padre, con vuelta, sin entrar en las hijas y sin mirar TabStop)
        private bool ProcessTabKey(bool forward) { return SelectNextControl(this, ActiveControl, forward, true, true, true); }
        private bool ProcessArrowKey(bool forward)
        {
            IPieza a = ActiveControl;
            object grupo = a != null ? a.Caja.Padre : (object)this;
            return SelectNextControl(grupo, a, forward, false, false, true);
        }
        // SI SE ESCONDE O SE APAGA LA DEL FOCO (o la que lo contiene), el foco pasa antes a la siguiente, con vuelta (Control.SelectNextIfFocused,
        // Control.cs:10770-10784: se mira antes de esconderla, asi que hacia delante la siguiente puede ser su primera hija)
        internal void SiguienteSiTieneElFoco(IPieza p)
        {
            if (!ContieneFoco(p) || p.Caja.Padre == null) return;
            SelectNextControl(this, p, true, true, true, true);
        }
        // QUITAR UNA PIEZA CON EL FOCO (ContainerControl.AfterControlRemoved, ContainerControl.cs:449-515): al primero que se pueda, o a nadie
        internal void FocoSeVaCon(IPieza p)
        {
            object f = foco;
            if (f == null || (f != p && !Contiene(p, f))) return;
            if (!SelectNextControl(this, p, true, true, true, true)) QuitaFoco();
        }
    }
}

// EL SELECTOR DE CARPETAS (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP era el FolderBrowserDialog de «Agregar
// carpeta...» de Ctrl+F2 (Form1.Datos.cs), el «Buscar carpeta» de Windows XP (SHBrowseForFolder): una hoja modal con la descripcion arriba,
// el arbol de las carpetas y los botones Aceptar y Cancelar. Decision del dueno del 8-11-2026 (diseno del port nativo): un selector NATIVO
// con el aspecto del de XP; el arbol empieza en las carpetas de Rocola OS (/rocola) y en los discos montados (/proc/mounts), y se usa con el
// teclado (flechas, Inicio, Fin, AvPag/RePag; derecha abre y baja, izquierda cierra y sube; Intro acepta y Escape cancela) y con el raton
// (un clic elige, el cuadrito o el doble clic abren o cierran, la rueda mueve). Solo lo que XP usa del FolderBrowserDialog: Description,
// ShowNewFolderButton (XP lo pone en false: el boton de crear carpeta no se hace), ShowDialog(duena) y SelectedPath. La referencia
// (la capa de la transicion) contestaba Cancelar sin ensenar nada: es una diferencia aceptada (PORT_VENTANAS2.md).
//   ROCOLA_CARPETAS_RAIZ=ruta[:ruta...]   (pruebas) las raices del arbol en lugar de /rocola y los discos montados
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public sealed class SelectorCarpetas : IDisposable
    {
        public string Description { get; set; } = "";
        public bool ShowNewFolderButton { get; set; } = true;
        public string SelectedPath { get; set; } = "";
        public void Dispose() { }
        // EL «BUSCAR CARPETA» DE XP: titulo, descripcion arriba, el arbol (con la primera raiz elegida) y Aceptar y Cancelar abajo a la derecha
        // (Aceptar, el boton por defecto, solo con una carpeta elegida); la letra de los dialogos de XP (Tahoma de 8)
        public Resultado MuestraModal(object duena)
        {
            using (Hoja h = new Hoja(MarcoXp.Dialogo))
            {
                h.Text = "Buscar carpeta";
                h.Font = Letra.EnPuntos(Letras.Resuelve("Tahoma"), 8f);
                h.ClientSize = new Size(314, 322);
                Rotulo d = new Rotulo();
                d.SetBounds(12, 9, 290, 40); d.UseMnemonic = false; d.Text = Description ?? ""; d.BackColor = Color.Transparent;
                ArbolCarpetas a = new ArbolCarpetas(Raices());
                a.SetBounds(12, 52, 290, 224);
                BotonXp ok = new BotonXp(), no = new BotonXp();
                ok.Text = "Aceptar"; ok.SetBounds(146, 288, 75, 23); ok.Resultado = Resultado.OK; ok.Enabled = a.Elegida != null;
                no.Text = "Cancelar"; no.SetBounds(227, 288, 75, 23); no.Resultado = Resultado.Cancel;
                a.CambiaEleccion += delegate { ok.Enabled = a.Elegida != null; };
                h.Controls.Add(d); h.Controls.Add(a); h.Controls.Add(ok); h.Controls.Add(no);
                h.BotonCancelar = no; h.BotonAceptar = ok;
                Resultado r = h.MuestraModal(duena);
                if (r == Resultado.OK && a.Elegida != null) SelectedPath = a.Elegida;
                return r;
            }
        }
        // las raices del arbol: /rocola (los datos y el disco de la rocola) y cada disco montado (lo que /proc/mounts monta de un /dev/...),
        // sin las del sistema; sin ninguna, la raiz
        internal static List<string> Raices()
        {
            List<string> l = new List<string>();
            string e = Environment.GetEnvironmentVariable("ROCOLA_CARPETAS_RAIZ");
            if (!string.IsNullOrEmpty(e)) { foreach (string r in e.Split(':')) if (r.Length > 0 && Directory.Exists(r)) l.Add(r); return l; }
            if (Directory.Exists("/rocola")) l.Add("/rocola");
            try
            {
                foreach (string linea in File.ReadAllLines("/proc/mounts"))
                {
                    string[] c = linea.Split(' ');
                    if (c.Length < 2 || !c[0].StartsWith("/dev/")) continue;
                    string m = c[1].Replace("\\040", " ");
                    if (m == "/" || m.StartsWith("/proc") || m.StartsWith("/sys") || m.StartsWith("/dev") || m.StartsWith("/run") || m.StartsWith("/boot")) continue;
                    bool ya = false; foreach (string x in l) if (m == x || m.StartsWith(x.TrimEnd('/') + "/")) ya = true;
                    if (!ya && Directory.Exists(m)) l.Add(m);
                }
            }
            catch { }
            if (l.Count == 0) l.Add("/");
            return l;
        }
    }

    // EL ARBOL DE LAS CARPETAS (el TreeView de SHBrowseForFolder): renglones de 16 puntos (los iconos de 16), cada nivel 19 mas adentro, el
    // cuadrito de abrir y cerrar, la carpeta amarilla y el nombre; la elegida en el azul de XP (gris si el arbol no tiene el foco); la barra
    // de desplazamiento de XP si no caben.
    internal sealed class ArbolCarpetas : IPieza, IDisposable
    {
        private sealed class Nodo
        {
            internal string Ruta, Nombre; internal Nodo Padre; internal int Nivel; internal bool Abierto, Mirado;
            internal List<Nodo> Hijos = new List<Nodo>();
            internal int ConHijos = -1;   // (si tiene carpetas que ensenar, mirado una vez: -1 aun no; como SFGAO_HASSUBFOLDER del arbol de Windows)
        }
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        private readonly List<Nodo> raices = new List<Nodo>();
        private Nodo elegido;
        private int top;
        private const int Alto = 16, Sangria = 19, Barra = PinturaXp.Barra;
        internal event Action CambiaEleccion;
        internal ArbolCarpetas(List<string> rutas)
        {
            caja = new CajaPieza(this, 120, 96);
            caja.BackColor = Color.White;
            foreach (string r in rutas) raices.Add(new Nodo { Ruta = r, Nombre = r, Nivel = 0 });
            if (raices.Count > 0) elegido = raices[0];   // (como SHBrowseForFolder sin carpeta elegida: la primera raiz)
        }
        internal void SetBounds(int x, int y, int w, int h) { caja.SetBounds(x, y, w, h); }
        internal string Elegida { get { return elegido != null ? elegido.Ruta : null; } }
        internal int TopIndex { get { return top; } }   // (las pruebas: el primer renglon que se ve)
        public void Dispose() { caja.Desecha(null); }

        // las carpetas de una carpeta, una vez (sin las escondidas ni las de la papelera y el sistema de Windows), en orden
        private static void Mira(Nodo n)
        {
            if (n.Mirado) return;
            n.Mirado = true;
            List<string> l = new List<string>();
            try
            {
                foreach (string d in Directory.GetDirectories(n.Ruta))
                {
                    if (NoSeEnsena(Path.GetFileName(d))) continue;
                    l.Add(d);
                }
            }
            catch { }
            l.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string d in l) n.Hijos.Add(new Nodo { Ruta = d, Nombre = Path.GetFileName(d), Padre = n, Nivel = n.Nivel + 1 });
        }
        private static bool NoSeEnsena(string nom)
        {
            return nom.StartsWith(".") || nom.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) || nom.Equals("RECYCLER", StringComparison.OrdinalIgnoreCase)
                || nom.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase);
        }
        // si lleva el cuadrito de abrir: con las MISMAS carpetas que ensena al abrirse (revision del paso H: antes solo quitaba las escondidas, y
        // la raiz de un disco NTFS con solo la papelera o System Volume Information salia con su «+» y al abrirla no tenia nada), mirado una vez
        // por carpeta y no en cada pintura
        private static bool TieneHijos(Nodo n)
        {
            if (n.Mirado) return n.Hijos.Count > 0;
            if (n.ConHijos < 0)
            {
                n.ConHijos = 0;
                try { foreach (string d in Directory.EnumerateDirectories(n.Ruta)) if (!NoSeEnsena(Path.GetFileName(d))) { n.ConHijos = 1; break; } } catch { }
            }
            return n.ConHijos == 1;
        }
        // los renglones que se ven (los abiertos, en orden)
        private List<Nodo> Renglones()
        {
            List<Nodo> l = new List<Nodo>();
            foreach (Nodo r in raices) Agrega(r, l);
            return l;
        }
        private static void Agrega(Nodo n, List<Nodo> l) { l.Add(n); if (n.Abierto) foreach (Nodo h in n.Hijos) Agrega(h, l); }
        private Rectangle Util { get { Rectangle b = caja.Bounds; return new Rectangle(1, 1, Math.Max(0, b.Width - 2), Math.Max(0, b.Height - 2)); } }
        private int Caben { get { return Math.Max(1, Util.Height / Alto); } }
        private bool ConBarra(int renglones) { return renglones > Caben; }
        private void Elige(Nodo n)
        {
            if (n == null || n == elegido) return;
            elegido = n;
            List<Nodo> l = Renglones(); int i = l.IndexOf(n), c = Caben;
            if (i >= 0) { if (i < top) top = i; else if (i >= top + c) top = i - c + 1; }
            caja.Invalidate();
            Action a = CambiaEleccion; if (a != null) a();
        }
        private void Abre(Nodo n, bool abrir)
        {
            if (n == null) return;
            if (abrir) { Mira(n); if (n.Hijos.Count == 0) return; }
            if (n.Abierto == abrir) return;
            n.Abierto = abrir;
            if (!abrir && elegido != null) for (Nodo p = elegido.Padre; p != null; p = p.Padre) if (p == n) { Elige(n); break; }
            Ajusta(); caja.Invalidate();
        }
        private void Ajusta() { int n = Renglones().Count; top = Math.Max(0, Math.Min(top, n - Caben)); }

        // ---------- el teclado (el TreeView de Windows: las flechas, Inicio, Fin, AvPag y RePag son suyas) ----------
        bool IPieza.QuiereTecla(Tecla keyData)
        {
            Tecla k = keyData & Tecla.KeyCode;
            return k == Tecla.Up || k == Tecla.Down || k == Tecla.Left || k == Tecla.Right || k == Tecla.PageUp || k == Tecla.PageDown || k == Tecla.Home || k == Tecla.End;
        }
        void IPieza.TeclaAbajo(PulsoTecla e)
        {
            List<Nodo> l = Renglones();
            if (l.Count == 0) return;
            int i = elegido != null ? l.IndexOf(elegido) : -1, c = Caben;
            switch (e.KeyCode)
            {
                case Tecla.Down: Elige(l[Math.Min(l.Count - 1, i + 1)]); break;
                case Tecla.Up: Elige(l[Math.Max(0, i - 1)]); break;
                case Tecla.PageDown: Elige(l[Math.Min(l.Count - 1, Math.Max(0, i) + c - 1)]); break;
                case Tecla.PageUp: Elige(l[Math.Max(0, i - c + 1)]); break;
                case Tecla.Home: Elige(l[0]); break;
                case Tecla.End: Elige(l[l.Count - 1]); break;
                case Tecla.Right:
                    if (elegido == null) { Elige(l[0]); break; }
                    if (!elegido.Abierto) Abre(elegido, true); else if (elegido.Hijos.Count > 0) Elige(elegido.Hijos[0]);
                    break;
                case Tecla.Left:
                    if (elegido == null) break;
                    if (elegido.Abierto) Abre(elegido, false); else if (elegido.Padre != null) Elige(elegido.Padre);
                    break;
                default: return;
            }
            e.Handled = true;
        }
        // ---------- el raton ----------
        void IPieza.RatonAbajo(Raton e)
        {
            caja.Focus();
            if (e.Button != BotonRaton.Left) return;
            List<Nodo> l = Renglones(); Rectangle u = Util;
            bool barra = ConBarra(l.Count);
            if (barra && e.X >= u.Right - Barra)
            {   // la barra: las flechas de uno en uno, el canal de pagina en pagina hacia el lado del pulgar donde se toco, y el pulgar se arrastra
                // (como la de la lista, ListaXp; revision del paso H: el canal miraba la mitad de la barra y no el pulgar, y el pulgar no se
                // podia arrastrar)
                int c = Caben; Rectangle pv = Pulgar(u, l.Count);
                if (e.Y < u.Top + Barra) top--; else if (e.Y >= u.Bottom - Barra) top++;
                else if (pv.Contains(e.X, e.Y)) { agarre = e.Y - pv.Top; return; }
                else top += e.Y < pv.Top ? -c : c;
                Ajusta(); caja.Invalidate(); return;
            }
            int i = top + (e.Y - u.Top) / Alto;
            if (i < 0 || i >= l.Count) return;
            Nodo n = l[i];
            int x0 = u.Left + n.Nivel * Sangria;
            if (e.X >= x0 && e.X < x0 + 16 && TieneHijos(n)) { Abre(n, !n.Abierto); return; }   // (el cuadrito)
            Elige(n);
            if (e.Clicks == 2) Abre(n, !n.Abierto);
        }
        // ARRASTRAR EL PULGAR: el arbol sigue al pulgar (la posicion por la proporcion del canal, como la barra de Windows y ListaXp) y al
        // soltar queda donde se dejo
        private int agarre = -1;
        void IPieza.RatonMueve(Raton e)
        {
            if (agarre < 0) return;
            List<Nodo> l = Renglones(); Rectangle u = Util;
            if (!ConBarra(l.Count)) return;
            Rectangle pv = Pulgar(u, l.Count); int canal = u.Height - 2 * Barra - pv.Height, sobra = Math.Max(0, l.Count - Caben);
            if (canal <= 0) return;
            int nuevo = (int)Math.Round((double)(e.Y - agarre - (u.Top + Barra)) * sobra / canal);
            nuevo = Math.Max(0, Math.Min(sobra, nuevo));
            if (nuevo != top) { top = nuevo; caja.Invalidate(); }
        }
        void IPieza.RatonArriba(Raton e) { agarre = -1; }
        // el pulgar de la barra (el mismo que se pinta)
        private Rectangle Pulgar(Rectangle u, int renglones)
        {
            int c = Caben, total = Math.Max(c, renglones), canal = u.Height - 2 * Barra;
            int largo = Math.Max(8, canal * c / Math.Max(1, total)), py = u.Top + Barra + (total > c ? (canal - largo) * top / (total - c) : 0);
            return new Rectangle(u.Right - Barra, py, Barra, largo);
        }
        void IPieza.Rueda(Raton e) { top -= e.Delta / 120 * 3; Ajusta(); caja.Invalidate(); e.Handled = true; }
        void IPieza.AlRecibirFoco() { caja.Invalidate(); }
        void IPieza.AlPerderFoco() { caja.Invalidate(); }
        // ---------- lo que pinta ----------
        void IPieza.PintaContenido(SKCanvas g)
        {
            SKMatrix hdc = g.TotalMatrix;
            List<Nodo> l = Renglones(); Rectangle u = Util;
            bool barra = ConBarra(l.Count);
            Rectangle zona = new Rectangle(u.X, u.Y, u.Width - (barra ? Barra : 0), u.Height);
            bool conFoco = Hoja.Foco == (object)this;
            int n = g.Save();
            g.ClipRect(new SKRect(zona.Left, zona.Top, zona.Right, zona.Bottom), SKClipOperation.Intersect, false);
            for (int i = top, y = zona.Top; i < l.Count && y < zona.Bottom; i++, y += Alto)
            {
                Nodo nd = l[i];
                int x = zona.Left + nd.Nivel * Sangria;
                if (TieneHijos(nd))
                {   // el cuadrito de abrir y cerrar (9x9, el borde gris azulado de XP y su + o -)
                    Rectangle q = new Rectangle(x + 3, y + 3, 9, 9);
                    PinturaXp.Rellena(g, q, Color.White);
                    PinturaXp.TrazaRect(g, new RectangleF(q.X, q.Y, q.Width - 1, q.Height - 1), Color.FromArgb(148, 166, 194), 1f, false);
                    PinturaXp.Rellena(g, new Rectangle(q.X + 2, q.Y + 4, 5, 1), Color.Black);
                    if (!nd.Abierto) PinturaXp.Rellena(g, new Rectangle(q.X + 4, q.Y + 2, 1, 5), Color.Black);
                }
                Carpeta(g, x + 16, y, nd.Abierto);
                Rectangle rt = new Rectangle(x + 35, y, Math.Max(0, TextoGdi.Mide(nd.Nombre, caja.Font, new Size(int.MaxValue, int.MaxValue), BanderasGdi.SinMargen).Width + 4), Alto);
                bool es = nd == elegido;
                if (es) PinturaXp.Rellena(g, rt, conFoco ? Color.FromArgb(49, 106, 197) : Color.FromArgb(236, 233, 216));
                PinturaXp.Texto(g, hdc, nd.Nombre, caja.Font, new Rectangle(rt.X + 2, rt.Y, rt.Width, rt.Height), es && conFoco ? Color.White : Color.Black, BanderasGdi.SinMargen | BanderasGdi.UnRenglon | BanderasGdi.SinPrefijo | BanderasGdi.EnMedio);
            }
            g.RestoreToCount(n);
            if (barra) PinturaXp.BarraDesplazamiento(g, new Rectangle(u.Right - Barra, u.Top, Barra, u.Height), true, Pulgar(u, l.Count));
            PinturaXp.TrazaRect(g, new RectangleF(0, 0, caja.Bounds.Width - 1, caja.Bounds.Height - 1), Color.FromArgb(127, 157, 185), 1f, false);
        }
        // la carpeta amarilla de XP (16x16), abierta o cerrada
        private static void Carpeta(SKCanvas g, int x, int y, bool abierta)
        {
            PinturaXp.Rellena(g, new Rectangle(x + 1, y + 3, 6, 2), Color.FromArgb(230, 190, 90));
            PinturaXp.Rellena(g, new Rectangle(x + 1, y + 4, 14, 10), Color.FromArgb(250, 216, 120));
            PinturaXp.TrazaRect(g, new RectangleF(x + 1, y + 4, 13, 9), Color.FromArgb(200, 150, 50), 1f, false);
            if (abierta) PinturaXp.Rellena(g, new Rectangle(x + 3, y + 7, 12, 6), Color.FromArgb(255, 236, 170));
        }
    }
}

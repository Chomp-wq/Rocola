// LAS REGLAS DE LA HOJA NATIVA Y SUS PIEZAS (port nativo de las ventanas de XP, ventanas2, paso H), una por una, con los mismos archivos del
// programa (se compilan aqui tal cual; TrazaVentanas y VolcadoEstado van de mentira, abajo). Son las pruebas de las 41 (Pruebas41) que
// miran ventanas de dialogo y sus controles, escritas contra Hoja y las piezas con las MISMAS cadenas esperadas de WinForms, mas las de
// Ctrl+F2 (la lista, los botones, Intro y Escape), los avisos, el selector de carpetas, el tabulador por grupos, las flechas, el foco que
// se va con la pieza escondida o apagada y KeyPreview; y (paso C) lo que Ctrl+F3 le pide: tarjetas dentro de paginas, la pagina que se
// esconde con el foco dentro, lo transparente encima de una hermana, la moneda con el foco en un deslizador, Escape por KeyPreview y la
// vida de la pagina. Cada prueba dice lo obtenido y, si falla, lo que daria WinForms en XP.
// De las cadenas de WinForms con la vida de una ventana se quitan los avisos que la hoja no tiene porque XP no los usa en sus hojas
// (HandleCreated, Load, Activated, Deactivate, HandleDestroyed); lo que marcan se mira aparte (IsHandleCreated, Hoja.Activa).
//   dotnet run        (o: dotnet bin/.../PruebasHoja.dll; guiones/pruebas_hoja.sh lo compila fuera del repositorio, con AOT=1 tambien NativeAOT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // (de mentira: en el programa son los ganchos de prueba de Plataforma) la traza de las ventanas, con quien la mire
    internal static class TrazaVentanas
    {
        internal static bool Activa = true;
        internal static Action<string> Observa;
        internal static void Apunta(string que) { Action<string> o = Observa; if (o != null) o(que); }
        internal static string Datos(int k) { return k.ToString("X"); }
        internal static string Pieza(object o)
        {
            if (o == null) return "nadie";
            string n = o.GetType().Name, t = null;
            IPieza p = o as IPieza; Hoja h = o as Hoja;
            if (p != null) t = p.Caja.Text; else if (h != null) t = h.Text;
            return string.IsNullOrEmpty(t) ? n : n + "'" + t + "'";
        }
        internal static string Sitio(string archivo, int linea) { return Path.GetFileName(archivo ?? "?") + ":" + linea; }
        internal static readonly string[] EventoRaton = { "baja", "clic", "mueve", "entra", "sale", "doble", "rueda", "arriba" };
    }
    internal static class VolcadoEstado { internal static void Mira(object principal) { } }

    // UNA PIEZA DE PRUEBA (la C de las 41): apunta lo que le llega; puede tener hijas
    internal sealed class PiezaPrueba : IPieza, IContenedor
    {
        internal static readonly List<string> Log = new List<string>();
        private readonly CajaPieza caja; private readonly string n;
        public CajaPieza Caja { get { return caja; } }
        public bool ManejaTecla, SuprimeTecla, ManejaRueda, LanzaAlPintar, Foco, Entra, QuiereFlechas; public int Pintado; public Action AlClic;
        public PiezaPrueba(string n) { this.n = n; caja = new CajaPieza(this, 0, 0); Controls = new Piezas(this); caja.Hijas = Controls; }
        public Piezas Controls { get; }
        void IPieza.PintaContenido(SKCanvas g) { Pintado++; if (LanzaAlPintar) throw new InvalidOperationException("pinta mal"); }
        bool IPieza.QuiereTecla(Tecla k) { return QuiereFlechas && (k == Tecla.Left || k == Tecla.Right || k == Tecla.Up || k == Tecla.Down); }
        void IPieza.TeclaAbajo(PulsoTecla e) { Log.Add(n + ".KeyDown(" + e.KeyCode + ")"); e.Handled = ManejaTecla; if (SuprimeTecla) e.SuppressKeyPress = true; }
        void IPieza.TeclaArriba(PulsoTecla e) { Log.Add(n + ".KeyUp(" + e.KeyCode + ")"); }
        void IPieza.Caracter(PulsoCaracter e) { Log.Add(n + ".KeyPress(" + (int)e.KeyChar + ")"); }
        void IPieza.RatonAbajo(Raton e) { Log.Add(n + ".MouseDown(" + e.Clicks + ")"); }
        void IPieza.RatonArriba(Raton e) { Log.Add(n + ".MouseUp(" + e.Clicks + ")"); }
        void IPieza.Clic(Raton e) { Log.Add(n + ".Click"); if (AlClic != null) AlClic(); }
        void IPieza.RatonEntra() { if (Entra) Log.Add(n + ".MouseEnter"); }
        void IPieza.RatonSale() { if (Entra) Log.Add(n + ".MouseLeave"); }
        void IPieza.DobleClic(Raton e) { Log.Add(n + ".DoubleClick"); }
        void IPieza.Rueda(Raton e) { Log.Add(n + ".MouseWheel"); if (ManejaRueda) e.Handled = true; }
        void IPieza.AlRecibirFoco() { if (Foco) Log.Add(n + ".GotFocus"); }
        void IPieza.AlPerderFoco() { if (Foco) Log.Add(n + ".LostFocus"); }
        Hoja IContenedor.HojaDe { get { return caja.Hoja; } }
        IContenedor IContenedor.PadreDe { get { return caja.Padre; } }
        bool IContenedor.VisibleDe { get { return caja.Visible; } }
        bool IContenedor.EnabledDe { get { return caja.Enabled; } }
        Color IContenedor.FondoDe { get { return caja.BackColor; } }
        Color IContenedor.TintaDe { get { return caja.ForeColor; } }
        Letra IContenedor.LetraDe { get { return caja.Font; } }
        void IContenedor.Ensucia() { caja.Invalidate(); }
    }
    // una hoja que no se deja cerrar la primera vez (FormClosing con Cancel)
    internal sealed class HojaQueSeResiste : Hoja
    {
        private int veces;
        internal HojaQueSeResiste() : base(MarcoXp.Dialogo) { }
        internal override bool AlCerrarse(RazonCierre razon) { return veces++ == 0; }
    }

    internal static class P
    {
        static int pasa, falla, excepciones;
        static void Mira(string nombre, string obtenido, string esperado)
        {
            bool ok = obtenido == esperado;
            if (ok) pasa++; else falla++;
            Console.WriteLine((ok ? "PASA  " : "FALLA ") + nombre + ": " + obtenido + (ok ? "" : "\n          WinForms: " + esperado));
        }
        static string Log(string prefijo)
        {
            List<string> l = new List<string>();
            foreach (string s in PiezaPrueba.Log) if (s.StartsWith(prefijo)) l.Add(s.Substring(prefijo.Length));
            return string.Join(",", l);
        }
        // otro hilo que, pasado 'ms', le da algo a la pantalla como una entrada (lo que en las 41 es Escritorio.Entrada)
        static void Luego(int ms, Action a) { new Thread(delegate () { Thread.Sleep(ms); PantallaRocola.Entrada(a); }) { IsBackground = true }.Start(); }
        static void Teclea(Tecla k, Tecla mods = Tecla.None) { Hoja.Tecla(k, true, mods, false); Hoja.Tecla(k, false, mods, false); }
        static void Raton(int x, int y) { PantallaRocola.Flecha = new Point(x, y); Hoja.RatonEn(PantallaRocola.Flecha, BotonRaton.None); }
        static void Boton(BotonRaton b, bool abajo) { Hoja.RatonBoton(b, abajo, PantallaRocola.Flecha); }
        static void Clic(BotonRaton b) { Boton(b, true); Boton(b, false); }
        // la vida de la hoja que se mira, con los nombres de WinForms (de la traza: ventana-ve, shown, closing, closed, ventana-esconde)
        static Hoja observada; static readonly List<string> vida = new List<string>();
        static void Observa(string q)
        {
            Hoja h = observada; if (h == null) return;
            string n = TrazaVentanas.Pieza(h);
            if (q == "ventana-ve " + n + " modal") vida.Add("Visible(True)");
            else if (q == "shown " + n) vida.Add("Shown");
            else if (q.StartsWith("closing " + n + " ")) vida.Add("FormClosing(" + q.Substring(("closing " + n + " ").Length) + "," + h.resultado + ")");
            else if (q.StartsWith("closed " + n + " ")) vida.Add("FormClosed(" + q.Substring(("closed " + n + " ").Length) + ")");
            else if (q.StartsWith("ventana-esconde " + n + " ")) vida.Add("Visible(False)");
        }
        // de la cadena de WinForms, sin los avisos que la hoja no tiene (XP no los usa en sus hojas)
        static string SinLoQueXpNoUsa(string s)
        {
            int i = s.IndexOf(" => "); string cola = i >= 0 ? s.Substring(i) : "", ev = i >= 0 ? s.Substring(0, i) : s;
            List<string> l = new List<string>();
            foreach (string t in ev.Split(',')) if (t != "HandleCreated" && t != "Load" && t != "Activated" && t != "Deactivate" && t != "HandleDestroyed") l.Add(t);
            return string.Join(",", l) + cola;
        }
        static Point Cliente(Hoja h, int x, int y) { return new Point(h.Location.X + Hoja.Marco + x, h.Location.Y + Hoja.Marco + Hoja.AltoBarra + y); }
        static string Foco() { return TrazaVentanas.Pieza(Hoja.Foco); }

        static int Main()
        {
            PantallaRocola.Hilo = Thread.CurrentThread;
            PantallaRocola.AlFallar = delegate (Exception ex) { excepciones++; if (Environment.GetEnvironmentVariable("PRUEBAS_TRAZA") == "1") Console.WriteLine("Falla: " + ex); };
            TrazaVentanas.Observa = Observa;
            bool hecho = false;
            PantallaRocola.EncolaDesde(null, delegate { try { Pruebas(); } catch (Exception ex) { Console.WriteLine("LA PRUEBA TRONO: " + ex); falla++; } hecho = true; }, "pruebas", false);
            PantallaRocola.Corre(delegate { return !hecho; });
            Console.WriteLine("pasan " + pasa + ", fallan " + falla);
            return falla == 0 ? 0 : 1;
        }

        static void Pruebas()
        {
            // ---- medidas (DefaultSize y el marco de Luna) ----
            Hoja d1 = new Hoja(MarcoXp.Dialogo); d1.Text = "x"; d1.ClientSize = new Size(960, 732);
            Mira("FixedDialog con ClientSize 960x732 (Ctrl+F3): Size", new Size(d1.Width, d1.Height).ToString(), "{Width=966, Height=764}");
            Hoja d2 = new Hoja(MarcoXp.Dialogo); d2.ClientSize = new Size(640, 372);
            Mira("FixedDialog con ClientSize 640x372 (Ctrl+F2): Size y ClientSize", new Size(d2.Width, d2.Height) + " " + d2.ClientSize, "{Width=646, Height=404} {Width=640, Height=372}");
            Mira("Label, Button, Panel, ListBox de fabrica", new Rotulo().Bounds.Size + " " + new BotonXp().Bounds.Size + " " + new Grupo().Bounds.Size + " " + new ListaXp().Bounds.Size,
                 "{Width=100, Height=23} {Width=75, Height=23} {Width=200, Height=100} {Width=120, Height=96}");

            // ---- dialogos (las cadenas de las 41) ----
            Hoja d = new Hoja(MarcoXp.Dialogo); d.Text = "D."; BotonXp b = new BotonXp(); b.Resultado = Resultado.OK; d.Controls.Add(b);
            observada = d; vida.Clear();
            Luego(150, delegate { b.PerformClick(); });
            Resultado r = d.MuestraModal(null);
            Mira("ShowDialog cerrado por un boton con DialogResult", string.Join(",", vida) + " => " + r + " handle=" + d.IsHandleCreated + " disposed=" + d.IsDisposed,
                 SinLoQueXpNoUsa("HandleCreated,Load,Visible(True),Activated,Shown,FormClosing(None,OK),FormClosed(None),Deactivate,Visible(False),HandleDestroyed => OK handle=False disposed=False"));
            vida.Clear();
            Luego(150, delegate { d.Close(); });
            r = d.MuestraModal(null);
            Mira("Otra vez ShowDialog del mismo, cerrado con Close", string.Join(",", vida) + " => " + r,
                 SinLoQueXpNoUsa("HandleCreated,Load,Visible(True),Activated,Shown,FormClosing(UserClosing,Cancel),FormClosed(UserClosing),Deactivate,Visible(False),HandleDestroyed => Cancel"));
            HojaQueSeResiste dr = new HojaQueSeResiste(); dr.Text = "R."; observada = dr; vida.Clear();
            Luego(100, delegate { dr.Close(); });
            Luego(300, delegate { vida.Add("sigue=" + dr.Visible); dr.Close(); });
            r = dr.MuestraModal(null);
            Mira("FormClosing que cancela la primera vez", string.Join(",", vida) + " => " + r,
                 SinLoQueXpNoUsa("HandleCreated,Load,Visible(True),Activated,Shown,FormClosing(UserClosing,Cancel),sigue=True,FormClosing(UserClosing,Cancel),FormClosed(UserClosing),Deactivate,Visible(False),HandleDestroyed => Cancel"));
            observada = null;
            Mira("Mientras esta abierta es la activa; al cerrarla, ninguna (no habia otra)", (Hoja.Activa == null) + "", "True");

            // ---- encargos con la hoja de duena ----
            string err;
            try { PantallaRocola.Encola(new Hoja(MarcoXp.Dialogo), delegate { }); err = "no lanzo"; } catch (InvalidOperationException) { err = "InvalidOperationException"; }
            Mira("BeginInvoke sin ventana en ningun lado", err, "InvalidOperationException");
            Hoja e1 = new Hoja(MarcoXp.Dialogo); e1.Text = "e"; bool corrio = false;
            Luego(100, delegate { PantallaRocola.Encola(e1, delegate { corrio = true; }); e1.Close(); });
            e1.MuestraModal(null);
            PantallaRocola.Bombea();
            Mira("BeginInvoke pendiente de una ventana destruida: no corre", corrio.ToString(), "False");

            // ---- lo que se hereda: letra y fondo ----
            Grupo gp = new Grupo(); Rotulo rh = new Rotulo(); gp.Controls.Add(rh);
            Letra t8 = Letra.EnPuntos(Letras.Resuelve("Tahoma"), 8f); gp.Font = t8;
            Mira("Letra del padre: la hija sin la suya la hereda", rh.Font == t8 ? "la del padre" : "otra", "la del padre");
            try { new ListaXp().BackColor = Color.Transparent; err = "no lanzo"; } catch (ArgumentException) { err = "ArgumentException"; }
            Mira("Fondo transparente en un control que no lo acepta", err, "ArgumentException");
            Grupo pt = new Grupo(); pt.BackColor = Color.Transparent; PiezaPrueba sin = new PiezaPrueba("sin"); pt.Controls.Add(sin); Rotulo et = new Rotulo(); pt.Controls.Add(et);
            Mira("Hijo de un padre transparente: el que no acepta transparencia queda gris", sin.Caja.BackColor.ToArgb().ToString("X8") + " " + et.BackColor.A, "FFECE9D8 0");

            // ---- teclado, raton y foco en una hoja abierta ----
            Hoja h = new Hoja(MarcoXp.Dialogo); h.Text = "teclas"; h.ClientSize = new Size(800, 560);
            PiezaPrueba k = new PiezaPrueba("k"); k.Caja.SetBounds(0, 0, 50, 50); h.Controls.Add(k);
            Luego(80, delegate { try { DentroDeLaHoja(h, k); } catch (Exception ex) { Console.WriteLine("LA PRUEBA TRONO: " + ex); falla++; } h.Close(); });
            h.MuestraModal(null);

            // ---- Alt+F4 y la «X» de una hoja ----
            Hoja ha = new Hoja(MarcoXp.Dialogo); ha.Text = "G."; observada = ha; vida.Clear();
            Luego(100, delegate { Hoja.Tecla(Tecla.Menu, true, Tecla.Alt, false); Teclea(Tecla.F4, Tecla.Alt); Hoja.Tecla(Tecla.Menu, false, Tecla.None, false); });
            r = ha.MuestraModal(null);
            Mira("Alt+F4 cierra la hoja (UserClosing)", string.Join(",", vida) + " => " + r, "Visible(True),Shown,FormClosing(UserClosing,Cancel),FormClosed(UserClosing),Visible(False) => Cancel");
            Hoja hx = new Hoja(MarcoXp.Dialogo); hx.Text = "X."; observada = hx; vida.Clear();
            Luego(100, delegate { Rectangle x = hx.CajaCerrar; Raton(hx.Location.X + x.X + 10, hx.Location.Y + x.Y + 10); Clic(BotonRaton.Left); });
            r = hx.MuestraModal(null);
            Mira("La «X» cierra la hoja (SC_CLOSE: UserClosing)", string.Join(",", vida) + " => " + r, "Visible(True),Shown,FormClosing(UserClosing,Cancel),FormClosed(UserClosing),Visible(False) => Cancel");
            observada = null;

            // ---- los avisos (MessageBox de XP: solo Aceptar) ----
            Luego(150, delegate { Hoja.Tecla(Tecla.Menu, true, Tecla.Alt, false); Teclea(Tecla.F4, Tecla.Alt); Hoja.Tecla(Tecla.Menu, false, Tecla.None, false); });
            bool primeroCerrado = false;
            // (por si Alt+F4 no lo cierra; si ya se cerro, nada: esa tecla le llegaria a la prueba que este corriendo a los 1,5 s -revision del
            // paso H: con las pruebas nuevas le llegaba al selector de la barra y lo cancelaba-)
            Luego(1500, delegate { if (!primeroCerrado) Teclea(Tecla.Escape); });
            Stopwatch mb = Stopwatch.StartNew();
            r = AvisoXp.Muestra("solo aceptar", "prueba");
            primeroCerrado = true;
            Mira("MessageBox OK y Alt+F4", r + (mb.ElapsedMilliseconds < 1000 ? " con Alt+F4" : " con Escape"), "OK con Alt+F4");
            Luego(150, delegate { Teclea(Tecla.Escape); });
            Mira("Aviso: Escape es Aceptar (el boton de cancelar de un aviso de solo Aceptar)", AvisoXp.Muestra("escape", "prueba").ToString(), "OK");
            Luego(150, delegate { Teclea(Tecla.Enter); });
            Mira("Aviso: Intro pulsa el boton por defecto", AvisoXp.Muestra("intro", "prueba").ToString(), "OK");
            Luego(150, delegate { Hoja a = Hoja.Activa; Rectangle x = a.CajaCerrar; Raton(a.Location.X + x.X + 10, a.Location.Y + x.Y + 10); Clic(BotonRaton.Left); });
            Mira("Aviso: la «X» contesta Aceptar", AvisoXp.Muestra("equis", "prueba").ToString(), "OK");
            Hoja duena = new Hoja(MarcoXp.Dialogo); duena.Text = "duena"; duena.ClientSize = new Size(640, 372);
            BotonXp bq = new BotonXp(); bq.SetBounds(10, 10, 75, 23); duena.Controls.Add(bq);
            string focoDespues = "";
            Luego(100, delegate
            {
                Teclea(Tecla.Tab);   // (al boton)
                Luego(100, delegate { Teclea(Tecla.Enter); });
                AvisoXp.Muestra(duena, "con duena", "Rocola");
                focoDespues = Foco() + " activa=" + TrazaVentanas.Pieza(Hoja.Activa);
                duena.Close();
            });
            duena.MuestraModal(null);
            Mira("Aviso con duena: al cerrarlo, la duena vuelve a estar activa con su foco", focoDespues, "BotonXp activa=Hoja'duena'");

            // ---- (revision del paso H) el raton despues de un modal abierto por un clic: al volver, MouseEnter a la pieza que queda debajo
            // de la flecha AHORA, no a la del punto donde se solto (la capa: Encima(ControlEn(PosicionRaton)); Windows: WM_MOUSEMOVE a la de
            // debajo). Como «Quitar seleccionada» con su aviso: el raton se mueve mientras el aviso esta abierto y se cierra con Intro.
            Hoja hm = new Hoja(MarcoXp.Dialogo); hm.Text = "hm"; hm.ClientSize = new Size(800, 560);
            PiezaPrueba q = new PiezaPrueba("q"), w = new PiezaPrueba("w");
            q.Caja.SetBounds(10, 10, 50, 50); w.Caja.SetBounds(700, 480, 50, 50); q.Entra = w.Entra = true;
            hm.Controls.Add(q); hm.Controls.Add(w);
            q.AlClic = delegate
            {
                Luego(100, delegate { Point pw = Cliente(hm, 725, 505); Raton(pw.X, pw.Y); Teclea(Tecla.Enter); });
                AvisoXp.Muestra(hm, "modal desde un clic", "prueba");
            };
            string trasModal = "";
            Luego(100, delegate
            {
                PiezaPrueba.Log.Clear();
                Point pq = Cliente(hm, 35, 35); Raton(pq.X, pq.Y); Clic(BotonRaton.Left);
                trasModal = Log("q.") + "|" + Log("w.");
                hm.Close();
            });
            hm.MuestraModal(null);
            Mira("Un clic que abre un modal y el raton que se mueve mientras: al volver, MouseEnter a la de debajo de la flecha", trasModal,
                 "MouseEnter,MouseDown(1),Click,MouseLeave,MouseUp(1)|MouseEnter");

            // ---- el selector de carpetas (la decision del 8-11-2026: nativo, como SHBrowseForFolder) ----
            string raiz = Path.Combine(Path.GetTempPath(), "pruebas-hoja-" + Environment.ProcessId);
            Directory.CreateDirectory(Path.Combine(raiz, "Discos", "Uno")); Directory.CreateDirectory(Path.Combine(raiz, "Discos", "Dos")); Directory.CreateDirectory(Path.Combine(raiz, "Otra"));
            Environment.SetEnvironmentVariable("ROCOLA_CARPETAS_RAIZ", raiz);
            SelectorCarpetas sc = new SelectorCarpetas();
            Luego(150, delegate { Teclea(Tecla.Right); Teclea(Tecla.Down); Teclea(Tecla.Right); Teclea(Tecla.Down); Teclea(Tecla.Down); Teclea(Tecla.Enter); });
            r = sc.MuestraModal(null);
            Mira("Selector: derecha abre, abajo baja, Intro acepta (Discos, Dos y Uno en orden)", r + " " + sc.SelectedPath.Substring(raiz.Length), "OK /Discos/Uno");
            SelectorCarpetas sc2 = new SelectorCarpetas();
            Luego(150, delegate { Teclea(Tecla.Down); Teclea(Tecla.Escape); });
            Mira("Selector: Escape cancela y no cambia SelectedPath", sc2.MuestraModal(null) + " '" + sc2.SelectedPath + "'", "Cancel ''");
            Directory.Delete(raiz, true);

            // (revision del paso H) el selector: el cuadrito de abrir solo con carpetas que se ensenan al abrirla (no la papelera ni System
            // Volume Information: la raiz de un disco NTFS sin nada mas no lo lleva), y la barra del arbol: el canal pasa de pagina hacia el
            // lado del pulgar donde se toca y el pulgar se arrastra (como la barra de la lista)
            string raiz2 = Path.Combine(Path.GetTempPath(), "pruebas-hoja2-" + Environment.ProcessId);
            Directory.CreateDirectory(Path.Combine(raiz2, "Disco", "System Volume Information")); Directory.CreateDirectory(Path.Combine(raiz2, "Disco", "$RECYCLE.BIN"));
            Directory.CreateDirectory(Path.Combine(raiz2, "Otra", "Musica"));
            for (int i = 0; i < 40; i++) Directory.CreateDirectory(Path.Combine(raiz2, "Muchas", "c" + i.ToString("00")));
            Environment.SetEnvironmentVariable("ROCOLA_CARPETAS_RAIZ", Path.Combine(raiz2, "Disco") + ":" + Path.Combine(raiz2, "Otra"));
            string cuadritos = "";
            Luego(150, delegate
            {
                int v; SKBitmap sup = Hoja.Activa.Superficie(out v);   // (el arbol en 12,52 del cliente; renglones de 16; el cuadrito en 4,4 del renglon)
                cuadritos = Cuadrito(sup, 15 + 4, 81 + 4) + " " + Cuadrito(sup, 15 + 4, 81 + 16 + 4);
                Teclea(Tecla.Escape);
            });
            new SelectorCarpetas().MuestraModal(null);
            Mira("Selector: el cuadrito de abrir solo con carpetas que se ensenan (Disco: la papelera y System Volume Information; Otra: Musica)", cuadritos, "sin con");
            Environment.SetEnvironmentVariable("ROCOLA_CARPETAS_RAIZ", Path.Combine(raiz2, "Muchas"));
            string barraArbol = "";
            Luego(150, delegate
            {
                Hoja s = Hoja.Activa; ArbolCarpetas ar = (ArbolCarpetas)s.Controls[1];
                Teclea(Tecla.Right);   // (abre «Muchas»: 41 renglones, caben 13; el pulgar arriba, de 18 a 77 en el arbol)
                Point o = new Point(s.Location.X + Hoja.Marco + 12, s.Location.Y + Hoja.Marco + Hoja.AltoBarra + 52);
                Raton(o.X + 280, o.Y + 90); Clic(BotonRaton.Left);   // el canal debajo del pulgar, en la mitad de arriba de la barra
                int trasCanal = ar.TopIndex;
                Hoja.RatonRueda(5);   // (otra vez arriba)
                Raton(o.X + 280, o.Y + 40); Boton(BotonRaton.Left, true); Raton(o.X + 280, o.Y + 90); Boton(BotonRaton.Left, false);
                barraArbol = "canal " + trasCanal + ", arrastre " + ar.TopIndex;
                Teclea(Tecla.Escape);
            });
            new SelectorCarpetas().MuestraModal(null);
            Mira("Selector: el canal debajo del pulgar baja una pagina; el pulgar arrastrado 50 puntos baja 11 renglones", barraArbol, "canal 13, arrastre 11");
            Environment.SetEnvironmentVariable("ROCOLA_CARPETAS_RAIZ", null);
            Directory.Delete(raiz2, true);

            // ---- (paso C) lo que Ctrl+F3 pide a la hoja: tarjetas dentro de paginas, la pagina que se esconde con el foco dentro, lo
            // transparente encima de una hermana, la moneda con el foco en un deslizador, Escape por KeyPreview, VisibleChanged y Disposed de la
            // pagina (Rendimiento.cs) al abrir y al cerrar la hoja ----
            Hoja hc = new Hoja(MarcoXp.Dialogo); hc.Text = "f3"; hc.ClientSize = new Size(960, 732);
            Grupo pagina = new Grupo(); pagina.SetBounds(0, 96, 960, 636); pagina.BackColor = Color.FromArgb(11, 14, 32);
            int paginaVista = 0; bool paginaFuera = false;
            pagina.VisibleChanged += delegate { if (pagina.Visible) paginaVista++; };
            pagina.Disposed += delegate { paginaFuera = true; };
            hc.Controls.Add(pagina);
            string resultadoF3 = "";
            Luego(80, delegate { try { CtrlF3(hc, pagina); } catch (Exception ex) { Console.WriteLine("LA PRUEBA TRONO: " + ex); falla++; } });
            Resultado rc = hc.MuestraModal(null);
            resultadoF3 = rc + " " + hc.IsDisposed;
            int vistaAntes = paginaVista;
            hc.Dispose();
            Mira("Pagina: VisibleChanged al abrir la hoja (y al ensenarla otra vez); Disposed al desechar la hoja", vistaAntes + " " + paginaFuera, "2 True");
            Mira("Escape por KeyPreview (TeclaAntes cierra la hoja, sin boton de cancelar): Cancel", resultadoF3, "Cancel False");
        }

        // (paso C) dentro de la hoja de Ctrl+F3 de prueba: dos tarjetas (grupos) dentro de la pagina, con piezas en orden del tabulador
        static void CtrlF3(Hoja h, Grupo pagina)
        {
            Grupo t1 = new Grupo(), t2 = new Grupo(); t1.SetBounds(16, 4, 456, 400); t2.SetBounds(488, 4, 456, 400);
            PiezaPrueba a1 = new PiezaPrueba("a1"), a2 = new PiezaPrueba("a2"), b1 = new PiezaPrueba("b1"), b2 = new PiezaPrueba("b2"), fuera = new PiezaPrueba("fuera");
            a1.Caja.Text = "a1"; a2.Caja.Text = "a2"; b1.Caja.Text = "b1"; b2.Caja.Text = "b2"; fuera.Caja.Text = "fuera";
            a1.Caja.SetBounds(16, 40, 100, 22); a2.Caja.SetBounds(16, 80, 100, 22); b1.Caja.SetBounds(16, 40, 100, 22); b2.Caja.SetBounds(16, 80, 100, 22);
            fuera.Caja.SetBounds(804, 680 - 96, 140, 38);
            t1.Controls.Add(a1); t1.Controls.Add(a2); t2.Controls.Add(b1); t2.Controls.Add(b2);
            pagina.Controls.Add(t1); pagina.Controls.Add(t2); h.Controls.Add(fuera);
            a1.Caja.Focus();
            List<string> l = new List<string>();
            for (int i = 0; i < 5; i++) { Teclea(Tecla.Tab); l.Add(Foco()); }
            Mira("Tab por tarjetas anidadas (pagina > tarjeta > pieza) y fuera de ellas, con vuelta", string.Join(",", l),
                 "PiezaPrueba'a2',PiezaPrueba'b1',PiezaPrueba'b2',PiezaPrueba'fuera',PiezaPrueba'a1'");
            l.Clear();
            for (int i = 0; i < 5; i++) { Teclea(Tecla.Tab, Tecla.Shift); l.Add(Foco()); }
            Mira("Mayus+Tab por tarjetas anidadas, al reves", string.Join(",", l),
                 "PiezaPrueba'fuera',PiezaPrueba'b2',PiezaPrueba'b1',PiezaPrueba'a2',PiezaPrueba'a1'");
            l.Clear(); a2.Caja.Focus();
            Teclea(Tecla.Down); l.Add(Foco()); Teclea(Tecla.Down); l.Add(Foco()); Teclea(Tecla.Up); l.Add(Foco());
            Mira("Flechas dentro de la misma tarjeta, sin salir a la otra (con vuelta)", string.Join(",", l), "PiezaPrueba'a1',PiezaPrueba'a2',PiezaPrueba'a1'");
            // la pagina se esconde con el foco dentro (pintaPag de Ctrl+F3): SelectNextIfFocused se hace ANTES de esconderla (SetVisibleCore,
            // Control.cs:11221-11228) y GetNextControl desde la pagina entra primero en sus hijas (Control.cs:6177...): el foco va a la primera
            // de la pagina que se esconde, y se queda ahi (la pagina escondida con el foco dentro, como en XP)
            b2.Caja.Focus();
            pagina.Visible = false;
            Mira("La pagina se esconde con el foco dentro: el foco pasa a su primera pieza (se mira antes de esconderla)", Foco(), "PiezaPrueba'a1'");
            pagina.Visible = true;
            // lo transparente encima de una hermana ensena al padre (WS_CLIPSIBLINGS y PaintTransparentBackground), no a la hermana
            PiezaPrueba debajo = new PiezaPrueba("debajo"); debajo.Caja.BackColor = Color.FromArgb(0, 0, 255); debajo.Caja.SetBounds(200, 200, 100, 100);
            Rotulo encima = new Rotulo(); encima.BackColor = Color.Transparent; encima.SetBounds(250, 250, 100, 100);
            Grupo fondo = new Grupo(); fondo.BackColor = Color.FromArgb(255, 0, 0); fondo.SetBounds(0, 0, 960, 400);
            fondo.Controls.Add(encima); fondo.Controls.Add(debajo); pagina.Controls.Add(fondo); fondo.BringToFront();
            SKBitmap sup = h.Superficie();
            Point o = new Point(Hoja.Marco, Hoja.Marco + Hoja.AltoBarra + 96);
            SKColor solapa = sup.GetPixel(o.X + 270, o.Y + 270), soloDebajo = sup.GetPixel(o.X + 210, o.Y + 210), soloEncima = sup.GetPixel(o.X + 330, o.Y + 330);
            Mira("Lo transparente encima de una hermana: ensena al padre (rojo), no a la hermana (azul)", solapa + " " + soloDebajo + " " + soloEncima,
                 "#ffff0000 #ff0000ff #ffff0000");
            fondo.Dispose();
            // la moneda con el foco en un deslizador (la pieza quiere las flechas; la hoja ve las teclas antes: MonedaTambienAqui)
            PiezaPrueba des = new PiezaPrueba("des"); des.Caja.Text = "des"; des.Caja.SetBounds(16, 120, 424, 22); t1.Controls.Add(des);
            des.QuiereFlechas = true; des.Caja.Focus();
            int monedas = 0;
            h.TeclaAntes += delegate (PulsoTecla e) { if (e.KeyCode == Tecla.D5) { monedas++; e.Handled = true; e.SuppressKeyPress = true; } };
            PiezaPrueba.Log.Clear(); Teclea(Tecla.D5); Teclea(Tecla.Right);
            Mira("La moneda con el foco en un deslizador: la cuenta la hoja y el deslizador no la ve (si la flecha)", monedas + " " + Log("des.") + " foco=" + Foco(),
                 "1 KeyUp(D5),KeyDown(Right),KeyUp(Right) foco=PiezaPrueba'des'");
            // Escape por KeyPreview: la hoja lo ve antes que la pieza del foco y se cierra (sin boton de cancelar, como Ctrl+F3)
            h.TeclaAntes += delegate (PulsoTecla e) { if (e.KeyCode == Tecla.Escape) h.Close(); };
            Teclea(Tecla.Escape);
        }
        // el cuadrito de abrir de un renglon del arbol: su borde gris azulado (148, 166, 194) en el cuadro de 9x9 que empieza en x,y
        static string Cuadrito(SKBitmap b, int x, int y)
        {
            int n = 0;
            for (int j = y; j < y + 9; j++) for (int i = x; i < x + 9; i++) if (b.GetPixel(i, j) == new SKColor(148, 166, 194)) n++;
            return n > 0 ? "con" : "sin";
        }

        static void DentroDeLaHoja(Hoja h, PiezaPrueba k)
        {
            Mira("Al abrir, el foco en la primera del tabulador", Foco(), "PiezaPrueba");
            // ---- teclado (las cadenas de las 41) ----
            PiezaPrueba.Log.Clear(); k.ManejaTecla = true; Teclea(Tecla.A);
            Mira("KeyDown.Handled no quita KeyPress", Log("k.").Replace(",KeyUp(A)", ""), "KeyDown(A),KeyPress(97)");
            PiezaPrueba.Log.Clear(); k.SuprimeTecla = true; Teclea(Tecla.A);
            Mira("SuppressKeyPress si", Log("k.").Replace(",KeyUp(A)", ""), "KeyDown(A)");
            k.ManejaTecla = k.SuprimeTecla = false;
            PiezaPrueba.Log.Clear(); Hoja.Tecla(Tecla.ControlKey, true, Tecla.Control, false); Teclea(Tecla.H, Tecla.Control); Hoja.Tecla(Tecla.ControlKey, false, Tecla.None, false);
            Mira("Ctrl+H da el retroceso (8)", Log("k.").Replace("KeyDown(ControlKey),", "").Replace(",KeyUp(H)", "").Replace(",KeyUp(ControlKey)", ""), "KeyDown(H),KeyPress(8)");
            // KeyPreview (TeclaAntes y TeclaArribaAntes: KeyDown y KeyUp de la ventana con KeyPreview)
            h.TeclaAntes += delegate (PulsoTecla e) { PiezaPrueba.Log.Add("h.KeyDown(" + e.KeyCode + ")"); if (e.KeyCode == Tecla.F9) e.Handled = true; };
            h.TeclaArribaAntes += delegate (PulsoTecla e) { PiezaPrueba.Log.Add("h.KeyUp(" + e.KeyCode + ")"); };
            PiezaPrueba.Log.Clear(); Teclea(Tecla.B);
            Mira("KeyPreview: la hoja ve la tecla antes que la pieza", Log(""), "h.KeyDown(B),k.KeyDown(B),k.KeyPress(98),h.KeyUp(B),k.KeyUp(B)");
            PiezaPrueba.Log.Clear(); Teclea(Tecla.F9);
            Mira("KeyPreview con Handled: la pieza no ve el KeyDown (el KeyUp si)", Log(""), "h.KeyDown(F9),h.KeyUp(F9),k.KeyUp(F9)");

            // ---- botones: Escape al de cancelar, Intro al del foco, Espacio al soltarlo ----
            BotonXp bc = new BotonXp(); bc.Text = "Cancelar"; bc.SetBounds(700, 500, 75, 23); h.Controls.Add(bc); h.BotonCancelar = bc;
            BotonXp bd = new BotonXp(); bd.Text = "Uno"; bd.SetBounds(600, 500, 75, 23); h.Controls.Add(bd);
            bc.Click += delegate { PiezaPrueba.Log.Add("bc.Click"); }; bd.Click += delegate { PiezaPrueba.Log.Add("bd.Click"); };
            PiezaPrueba.Log.Clear(); Teclea(Tecla.Escape);
            Mira("Escape pulsa el boton de cancelar (CancelButton)", Log("bc."), "Click");
            ((IPieza)bd).Caja.Focus();
            PiezaPrueba.Log.Clear(); Teclea(Tecla.Enter);
            Mira("Intro pulsa el boton con el foco (el boton por defecto)", Log("bd."), "Click");
            PiezaPrueba.Log.Clear(); Hoja.Tecla(Tecla.Space, true, Tecla.None, false);
            string alBajar = Log("bd.") + "-"; Hoja.Tecla(Tecla.Space, false, Tecla.None, false);
            Mira("Espacio: el boton se pulsa al soltarlo", alBajar + "|" + Log("bd."), "-|Click");
            h.Controls.Remove(bc); h.Controls.Remove(bd); h.BotonCancelar = null;

            // ---- el tabulador por grupos y las flechas dentro del mismo padre ----
            PiezaPrueba a = new PiezaPrueba("a"), g1 = new PiezaPrueba("g1"), g2 = new PiezaPrueba("g2"), bb = new PiezaPrueba("b");
            a.Caja.Text = "a"; g1.Caja.Text = "g1"; g2.Caja.Text = "g2"; bb.Caja.Text = "b";
            Grupo G = new Grupo(); G.SetBounds(100, 0, 200, 100); g1.Caja.SetBounds(0, 0, 20, 20); g2.Caja.SetBounds(30, 0, 20, 20); G.Controls.Add(g1); G.Controls.Add(g2);
            h.Controls.Remove(k); a.Caja.SetBounds(0, 0, 20, 20); bb.Caja.SetBounds(400, 0, 20, 20);
            h.Controls.Add(a); h.Controls.Add(G); h.Controls.Add(bb);
            a.Caja.Focus();
            List<string> l = new List<string>();
            for (int i = 0; i < 4; i++) { Teclea(Tecla.Tab); l.Add(Foco()); }
            Mira("Tab entra en el grupo y da la vuelta", string.Join(",", l), "PiezaPrueba'g1',PiezaPrueba'g2',PiezaPrueba'b',PiezaPrueba'a'");
            l.Clear();
            for (int i = 0; i < 4; i++) { Teclea(Tecla.Tab, Tecla.Shift); l.Add(Foco()); }
            Mira("Mayus+Tab al reves (el grupo por su ultima)", string.Join(",", l), "PiezaPrueba'b',PiezaPrueba'g2',PiezaPrueba'g1',PiezaPrueba'a'");
            g1.Caja.Focus(); l.Clear();
            Teclea(Tecla.Right); l.Add(Foco()); Teclea(Tecla.Right); l.Add(Foco()); Teclea(Tecla.Left); l.Add(Foco());
            Mira("Flechas: la siguiente dentro del mismo padre, con vuelta", string.Join(",", l), "PiezaPrueba'g2',PiezaPrueba'g1',PiezaPrueba'g2'");
            g1.Caja.Focus(); g1.Foco = g2.Foco = true; PiezaPrueba.Log.Clear(); Teclea(Tecla.Tab);
            Mira("Tab: LostFocus de la de antes y GotFocus de la nueva", Log("g").Replace(",2.KeyUp(Tab)", ""), "1.LostFocus,2.GotFocus");
            g1.Foco = g2.Foco = false;
            g2.Caja.Visible = false;
            Mira("Esconder la del foco: el foco pasa a la siguiente", Foco(), "PiezaPrueba'b'");
            bb.Caja.Enabled = false;
            Mira("Apagar la del foco: el foco pasa a la siguiente (con vuelta)", Foco(), "PiezaPrueba'a'");
            int vistos = 0; G.VisibleChanged += delegate { vistos++; };
            G.Visible = false; G.Visible = true;
            bool desechado = false; G.Disposed += delegate { desechado = true; };
            G.Dispose();
            Mira("Grupo: VisibleChanged al esconderlo y ensenarlo; Disposed al desecharlo", vistos + " " + desechado + " " + h.Controls.Contains(G), "2 True False");
            h.Controls.Remove(a); h.Controls.Remove(bb); h.Controls.Add(k);

            // ---- el raton (las cadenas de las 41) ----
            PiezaPrueba pp = new PiezaPrueba("pp"); pp.Caja.SetBounds(100, 100, 200, 100); PiezaPrueba hh = new PiezaPrueba("hh"); hh.Caja.SetBounds(100, 0, 100, 100);
            pp.Controls.Add(hh); h.Controls.Add(pp);
            PiezaPrueba.Log.Clear();
            Point p1 = Cliente(h, 120, 150), p2 = Cliente(h, 250, 150);
            Raton(p1.X, p1.Y); Boton(BotonRaton.Left, true); Raton(p2.X, p2.Y); Boton(BotonRaton.Left, false);
            Mira("Soltar encima de un hijo: sin Click", Log("pp."), "MouseDown(1),MouseUp(1)");
            PiezaPrueba.Log.Clear(); Thread.Sleep(600);
            Raton(p1.X, p1.Y); Clic(BotonRaton.Left); Clic(BotonRaton.Left);
            Mira("Doble clic con StandardDoubleClick", Log("pp."), "MouseDown(1),Click,MouseUp(1),MouseDown(2),DoubleClick,MouseUp(1)");
            pp.Caja.DobleClicNormal = false; PiezaPrueba.Log.Clear(); Thread.Sleep(600);
            Clic(BotonRaton.Left); Clic(BotonRaton.Left);
            Mira("Doble clic sin StandardDoubleClick: dos Click", Log("pp."), "MouseDown(1),Click,MouseUp(1),MouseDown(2),Click,MouseUp(1)");
            int clics = 0; BotonXp bb2 = new BotonXp(); bb2.SetBounds(400, 100, 75, 23); bb2.Click += delegate { clics++; }; h.Controls.Add(bb2); Thread.Sleep(600);
            Point pb = Cliente(h, 420, 110); Raton(pb.X, pb.Y);
            Clic(BotonRaton.Left); Clic(BotonRaton.Left);
            int doble = clics; Thread.Sleep(600);
            Clic(BotonRaton.Right);
            Mira("Button: doble clic = dos clics; el derecho no pulsa", "doble=" + doble + " derecho=" + (clics - doble), "doble=2 derecho=0");
            PiezaPrueba.Log.Clear(); hh.Caja.Focus(); Hoja.RatonRueda(1);
            Mira("Rueda que el hijo no atiende: sube al padre", Log("pp.") + "|" + Log("hh."), "MouseWheel|MouseWheel");
            PiezaPrueba.Log.Clear(); hh.ManejaRueda = true; Hoja.RatonRueda(1);
            Mira("Rueda atendida: no sube", Log("pp.") + "|" + Log("hh."), "|MouseWheel");
            h.Controls.Remove(pp); h.Controls.Remove(bb2);

            // ---- arrastrar la hoja por su barra de titulo, y el pulgar de la barra de la lista ----
            Point o = h.Location;
            Raton(o.X + 150, o.Y + 10); Boton(BotonRaton.Left, true); Raton(o.X + 200, o.Y + 60); Raton(o.X + 250, o.Y + 110); Boton(BotonRaton.Left, false);
            Mira("Arrastrar la hoja por la barra de titulo", (h.Location.X - o.X) + "," + (h.Location.Y - o.Y), "100,100");
            ListaXp lb = new ListaXp(); lb.SetBounds(500, 300, 120, 96); for (int i = 0; i < 30; i++) lb.Items.Add("renglon " + i); h.Controls.Add(lb);
            Thread.Sleep(600);
            Point pl = Cliente(h, 500 + 120 - 9, 300 + 1 + 17 + 3);
            Raton(pl.X, pl.Y); Boton(BotonRaton.Left, true);
            int topAgarrado = lb.TopIndex;
            Raton(pl.X, pl.Y + 30); Boton(BotonRaton.Left, false);
            Mira("Agarrar el pulgar no pasa de pagina; arrastrarlo baja la lista", "al agarrar " + topAgarrado + ", al soltar " + (lb.TopIndex > 0 ? "mas abajo" : lb.TopIndex.ToString()),
                 "al agarrar 0, al soltar mas abajo");
            // la lista de Ctrl+F2 con el teclado (el ListBox de Windows: las flechas, Inicio, Fin, AvPag son suyas y no mueven el foco)
            ((IPieza)lb).Caja.Focus(); lb.SelectedIndex = -1; l.Clear();
            foreach (Tecla t in new[] { Tecla.Down, Tecla.Down, Tecla.End, Tecla.Home, Tecla.Up, Tecla.PageDown }) { Teclea(t); l.Add(lb.SelectedIndex.ToString()); }
            Mira("Lista: Abajo, Abajo, Fin, Inicio, Arriba, AvPag (baja lo que cabe menos uno)", string.Join(",", l) + " foco=" + Foco(), "0,1,29,0,0," + (VisiblesDe(lb) - 1) + " foco=ListaXp");
            int top0 = lb.TopIndex; Hoja.RatonRueda(-1);
            Mira("Lista: la rueda baja 3 renglones y no sube al padre", (lb.TopIndex - top0) + "", "3");
            h.Controls.Remove(lb);

            // ---- la «X» roja: una pieza que truena al pintar ya no vuelve a pintarse ----
            PiezaPrueba x = new PiezaPrueba("x"); x.LanzaAlPintar = true; x.Caja.SetBounds(600, 300, 50, 50); h.Controls.Add(x);
            int ex0 = excepciones; h.Superficie(); x.Caja.Invalidate(); h.Superficie(); x.Caja.Invalidate(); h.Superficie();
            Mira("OnPaint que lanza: una vez, y despues la X roja", "OnPaint=" + x.Pintado + " excepciones=" + (excepciones - ex0), "OnPaint=1 excepciones=1");
            h.Controls.Remove(x);
            // (revision del paso H) la version que quedo pintada (Hoja.Superficie(out version), la que la composicion compara para subir la
            // superficie): la pintura de la «X» roja, que se hace porque la pieza ensucio la hoja al lanzar, lleva otra version (se sube);
            // despues, sin cambios, la misma (no se sube)
            PiezaPrueba x2 = new PiezaPrueba("x2"); x2.LanzaAlPintar = true; x2.Caja.SetBounds(600, 300, 50, 50); h.Controls.Add(x2);
            int va, vb, vc; h.Superficie(out va); h.Superficie(out vb); h.Superficie(out vc);
            Mira("La X roja se vuelve a subir: otra version tras la pintura que lanzo, y la misma despues", (vb != va) + " " + (vc == vb) + " OnPaint=" + x2.Pintado,
                 "True True OnPaint=1");
            h.Controls.Remove(x2);
        }
        // los renglones que caben en la lista (lo que AvPag baja menos uno)
        static int VisiblesDe(ListaXp l) { int alto = Math.Max(1, CajaPieza.LetraDeFabrica.AltoEntero); return Math.Max(1, (96 - 2) / alto); }
    }
}

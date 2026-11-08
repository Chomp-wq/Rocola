// PRUEBAS PEQUENAS DE LA CAPA DE VENTANAS (Ventanas.*.cs de la traduccion de XP) contra lo que hace WinForms de .NET 2.0-4.8 en XP
// (la fuente de referencia: dotnet/winforms release/3.0, portada de .NET Framework 4.8). Cada prueba dice lo obtenido y, si falla, lo
// que daria WinForms. Se corre fuera de pantalla: ROCOLA_PANTALLA=fuera:1024x768 dotnet pruebas.dll
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Rocola.Ventanas;
using Rocola.Dibujo2D;
using Timer = Rocola.Ventanas.Timer;

static class P
{
    static int pasa, falla;
    static readonly List<string> log = new List<string>();
    static void Mira(string nombre, string obtenido, string esperado)
    {
        bool ok = obtenido == esperado;
        if (ok) pasa++; else falla++;
        Console.WriteLine((ok ? "PASA  " : "FALLA ") + nombre + ": " + obtenido + (ok ? "" : "\n          WinForms: " + esperado));
    }
    static string Log(string prefijo) { List<string> l = new List<string>(); foreach (string s in log) if (s.StartsWith(prefijo)) l.Add(s.Substring(prefijo.Length)); return string.Join(",", l); }
    static void Bombea(int ms) { Stopwatch t = Stopwatch.StartNew(); do { Application.DoEvents(); Thread.Sleep(1); } while (t.ElapsedMilliseconds < ms); }
    static void Luego(int ms, Action a) { new Thread(delegate () { Thread.Sleep(ms); Escritorio.Entrada(a); }) { IsBackground = true }.Start(); }
    static void Tecla(Keys k) { Escritorio.Tecla(k, true); Escritorio.Tecla(k, false); }
    static void Traza(string s) { if (Environment.GetEnvironmentVariable("PRUEBAS_TRAZA") == "1") Console.WriteLine("  .. " + s); }

    class F : Form
    {
        readonly string n; public F(string n) { this.n = n; Text = n; }
        protected override void OnHandleCreated(EventArgs e) { log.Add(n + ".HandleCreated"); base.OnHandleCreated(e); }
        protected override void OnHandleDestroyed(EventArgs e) { log.Add(n + ".HandleDestroyed"); base.OnHandleDestroyed(e); }
        protected override void OnLoad(EventArgs e) { log.Add(n + ".Load"); base.OnLoad(e); }
        protected override void OnVisibleChanged(EventArgs e) { log.Add(n + ".Visible(" + Visible + ")"); base.OnVisibleChanged(e); }
        protected override void OnActivated(EventArgs e) { log.Add(n + ".Activated"); base.OnActivated(e); }
        protected override void OnDeactivate(EventArgs e) { log.Add(n + ".Deactivate"); base.OnDeactivate(e); }
        protected override void OnShown(EventArgs e) { log.Add(n + ".Shown"); base.OnShown(e); }
        protected override void OnResize(EventArgs e) { log.Add(n + ".Resize(" + ClientSize.Width + "x" + ClientSize.Height + ")"); base.OnResize(e); }
        protected override void OnFormClosing(FormClosingEventArgs e) { log.Add(n + ".FormClosing(" + e.CloseReason + "," + DialogResult + ")"); base.OnFormClosing(e); }
        protected override void OnFormClosed(FormClosedEventArgs e) { log.Add(n + ".FormClosed(" + e.CloseReason + ")"); base.OnFormClosed(e); }
    }
    class C : Control
    {
        readonly string n; public bool ManejaTecla, SuprimeTecla, ManejaRueda, LanzaAlPintar, SinDobleClic; public int Pintado;
        public C(string n) { this.n = n; SetStyle(ControlStyles.Selectable, true); }
        public void Doble(bool si) { SetStyle(ControlStyles.StandardDoubleClick, si); }
        public void Transparente() { SetStyle(ControlStyles.SupportsTransparentBackColor, true); }
        protected override void OnFontChanged(EventArgs e) { log.Add(n + ".FontChanged"); base.OnFontChanged(e); }
        protected override void OnBackColorChanged(EventArgs e) { log.Add(n + ".BackColorChanged"); base.OnBackColorChanged(e); }
        protected override void OnKeyDown(KeyEventArgs e) { log.Add(n + ".KeyDown(" + e.KeyCode + ")"); e.Handled = ManejaTecla; if (SuprimeTecla) e.SuppressKeyPress = true; base.OnKeyDown(e); }
        protected override void OnKeyPress(KeyPressEventArgs e) { log.Add(n + ".KeyPress(" + (int)e.KeyChar + ")"); base.OnKeyPress(e); }
        protected override void OnMouseDown(MouseEventArgs e) { log.Add(n + ".MouseDown(" + e.Clicks + ")"); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { log.Add(n + ".MouseUp(" + e.Clicks + ")"); base.OnMouseUp(e); }
        protected override void OnClick(EventArgs e) { log.Add(n + ".Click"); base.OnClick(e); }
        protected override void OnDoubleClick(EventArgs e) { log.Add(n + ".DoubleClick"); base.OnDoubleClick(e); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            log.Add(n + ".MouseWheel");
            System.Reflection.PropertyInfo h = e.GetType().GetProperty("Handled");   // (HandledMouseEventArgs; la version de antes no lo tiene)
            if (h != null && ManejaRueda) h.SetValue(e, true);
            base.OnMouseWheel(e);
        }
        protected override void OnPaint(PaintEventArgs e) { Pintado++; if (LanzaAlPintar) throw new InvalidOperationException("pinta mal"); base.OnPaint(e); }
    }

    static Form m;
    static int excepciones;
    [STAThread]
    static void Main()
    {
        Console.WriteLine("empieza");
        Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { excepciones++; if (Environment.GetEnvironmentVariable("PRUEBAS_TRAZA") == "1") Console.WriteLine("ThreadException: " + e.Exception); };
        Console.WriteLine("ventana");
        m = new Form(); m.Text = "principal"; m.FormBorderStyle = FormBorderStyle.None; m.WindowState = FormWindowState.Maximized;
        m.Shown += delegate { Console.WriteLine("shown"); try { Pruebas(); } catch (Exception ex) { Console.WriteLine("LA PRUEBA TRONO: " + ex); falla++; } m.Close(); };
        Application.Run(m);
        Console.WriteLine("pasan " + pasa + ", fallan " + falla);
    }

    static void Pruebas()
    {
        // ---- medidas (DefaultSize, el marco de Luna, ClientSize frente a Size) ----
        Mira("Form de fabrica: Size y ClientSize (Sizable)", new Form().Size + " " + new Form().ClientSize, "{Width=300, Height=300} {Width=292, Height=266}");
        Mira("Label, Button, Panel, ListBox de fabrica", new Label().Size + " " + new Button().Size + " " + new Panel().Size + " " + new ListBox().Size,
             "{Width=100, Height=23} {Width=75, Height=23} {Width=200, Height=100} {Width=120, Height=96}");
        Form d1 = new Form(); d1.Text = "x"; d1.FormBorderStyle = FormBorderStyle.FixedDialog; d1.ClientSize = new Size(960, 732);
        Mira("FixedDialog con ClientSize 960x732 (Ctrl+F3): Size", d1.Size.ToString(), "{Width=966, Height=764}");
        Form d2 = new Form(); d2.Text = "x"; d2.ClientSize = new Size(920, 548); d2.FormBorderStyle = FormBorderStyle.FixedDialog;
        Mira("ClientSize y despues FixedDialog: conserva el cliente", d2.ClientSize + " " + d2.Size, "{Width=920, Height=548} {Width=926, Height=580}");
        Form d3 = new Form(); d3.Text = "x"; d3.Size = new Size(400, 300); d3.FormBorderStyle = FormBorderStyle.FixedDialog;
        Mira("Size y despues FixedDialog: conserva la medida", d3.Size + " " + d3.ClientSize, "{Width=400, Height=300} {Width=394, Height=268}");

        // ---- el orden de los avisos al ensenar y cerrar ----
        log.Clear();
        F a = new F("A."); a.ClientSize = new Size(1024, 700); a.FormBorderStyle = FormBorderStyle.FixedSingle; a.StartPosition = FormStartPosition.CenterScreen;
        log.Clear();
        a.Show(); Bombea(80);
        Mira("Show: el orden de los avisos", Log("A.."), "HandleCreated,Load,Visible(True),Activated,Shown");
        Mira("FixedSingle 1024x700 centrado (F12): sitio", a.Location.ToString(), "{X=0,Y=18}");
        log.Clear(); a.Close(); Bombea(30);
        Mira("Close de uno que no es dialogo", Log("A..") + " disposed=" + a.IsDisposed, "FormClosing(UserClosing,None),FormClosed(UserClosing),Deactivate,HandleDestroyed disposed=True");

        // ---- maximizada: se aplica al crear la ventana ----
        log.Clear();
        F c = new F("C."); c.Size = new Size(1100, 780); c.FormBorderStyle = FormBorderStyle.None; c.StartPosition = FormStartPosition.CenterScreen;
        c.WindowState = FormWindowState.Maximized;
        string antes = c.Size.ToString();
        log.Clear(); c.Show(); Bombea(50);
        Mira("WindowState antes de ensenarse: la medida no cambia", antes, "{Width=1100, Height=780}");
        Mira("Show maximizada: Resize antes de Load", Log("C..") + " " + c.Size, "HandleCreated,Resize(1024x768),Load,Visible(True),Activated,Shown {Width=1024, Height=768}");
        c.WindowState = FormWindowState.Normal;
        Mira("Normal otra vez: la medida de antes", c.Size.ToString(), "{Width=1100, Height=780}");
        c.Close(); Bombea(20);

        // ---- dialogos ----
        log.Clear();
        F d = new F("D."); Button b = new Button(); b.DialogResult = DialogResult.OK; d.Controls.Add(b);
        Luego(150, delegate { b.PerformClick(); });
        DialogResult r = d.ShowDialog(m);
        Mira("ShowDialog cerrado por un boton con DialogResult", Log("D..") + " => " + r + " handle=" + d.IsHandleCreated + " disposed=" + d.IsDisposed,
             "HandleCreated,Load,Visible(True),Activated,Shown,FormClosing(None,OK),FormClosed(None),Deactivate,Visible(False),HandleDestroyed => OK handle=False disposed=False");
        log.Clear();
        Luego(150, delegate { d.Close(); });
        r = d.ShowDialog(m);
        Mira("Otra vez ShowDialog del mismo, cerrado con Close", Log("D..") + " => " + r,
             "HandleCreated,Load,Visible(True),Activated,Shown,FormClosing(UserClosing,Cancel),FormClosed(UserClosing),Deactivate,Visible(False),HandleDestroyed => Cancel");
        log.Clear();
        int veces = 0;
        d.FormClosing += delegate (object s, FormClosingEventArgs e) { if (veces++ == 0) e.Cancel = true; };
        Luego(100, delegate { d.Close(); });
        Luego(300, delegate { log.Add("D..sigue=" + d.Visible); d.Close(); });
        r = d.ShowDialog(m);
        Mira("FormClosing que cancela la primera vez", Log("D..") + " => " + r,
             "HandleCreated,Load,Visible(True),Activated,Shown,FormClosing(UserClosing,Cancel),sigue=True,FormClosing(UserClosing,Cancel),FormClosed(UserClosing),Deactivate,Visible(False),HandleDestroyed => Cancel");
        log.Clear();
        F h = new F("H.");
        Luego(150, delegate { h.Visible = false; });
        r = h.ShowDialog(m);
        Mira("Esconder un dialogo lo acaba (sin FormClosed)", Log("H..") + " => " + r,
             "HandleCreated,Load,Visible(True),Activated,Shown,Visible(False),Deactivate,FormClosing(None,None),HandleDestroyed => None");
        d.Dispose(); h.Dispose();

        // ---- Idle: una vez cada vez que la cola se vacia ----
        int idles = 0; EventHandler ih = delegate { idles++; };
        Form q = new Form(); q.Text = "q";
        Application.Idle += ih;
        Luego(600, delegate { q.Close(); });
        q.ShowDialog(m);
        Application.Idle -= ih;
        Mira("Idle durante 600 ms sin mensajes (como mucho unos pocos)", idles <= 6 ? "pocos" : idles + " veces", "pocos");
        q.Dispose();

        // ---- BeginInvoke e InvokeRequired ----
        Traza("begininvoke");
        string err = "";
        try { new Control().BeginInvoke(new Action(delegate { })); err = "no lanzo"; } catch (InvalidOperationException) { err = "InvalidOperationException"; }
        Mira("BeginInvoke sin ventana en ningun lado", err, "InvalidOperationException"); Traza("b1");
        Panel pi = new Panel(); pi.Visible = false; m.Controls.Add(pi); Control hijo = new Control(); pi.Controls.Add(hijo); Traza("b2");
        bool corrio = false; hijo.BeginInvoke(new Action(delegate { corrio = true; })); Bombea(20);
        Mira("BeginInvoke de un hijo sin ventana: por la del padre", "handle=" + hijo.IsHandleCreated + " corrio=" + corrio, "handle=False corrio=True");
        Form e1 = new Form(); e1.Text = "e"; Traza("b3"); e1.Show(); Traza("b4"); Bombea(20); Traza("b5");
        bool corrio2 = false; e1.BeginInvoke(new Action(delegate { corrio2 = true; })); e1.Dispose(); Bombea(20);
        Mira("BeginInvoke pendiente de una ventana destruida: no corre", corrio2.ToString(), "False");
        bool ir1 = true, ir2 = false; Thread th = new Thread(delegate () { ir1 = new Control().InvokeRequired; ir2 = m.InvokeRequired; }); th.Start(); th.Join();
        Mira("InvokeRequired desde otro hilo (sin ventana / con ventana)", ir1 + "/" + ir2, "False/True");

        // ---- lo que se hereda: letra y fondo ----
        log.Clear();
        Panel p = new Panel(); m.Controls.Add(p);
        C c1 = new C("c1"), c2 = new C("c2"); c2.Font = new Font("Arial", 9f); p.Controls.Add(c1); p.Controls.Add(c2);
        log.Clear(); p.Font = new Font("Tahoma", 8f);
        Mira("Letra del padre: avisa a los hijos que la heredan", Log("c"), "1.FontChanged");
        log.Clear(); p.Font = new Font("Tahoma", 8f);
        Mira("La misma letra otra vez: sin aviso", Log("c") + "-", "-");
        log.Clear(); C c3 = new C("c3"); p.Controls.Add(c3);
        Mira("Hijo nuevo en un padre con otra letra: FontChanged", Log("c3."), "FontChanged");
        try { new Control().BackColor = Color.Transparent; err = "no lanzo"; } catch (ArgumentException) { err = "ArgumentException"; }
        Mira("Fondo transparente en un control que no lo acepta", err, "ArgumentException");
        Panel pt = new Panel(); pt.BackColor = Color.Transparent; Control sin = new Control(); pt.Controls.Add(sin); Label et = new Label(); pt.Controls.Add(et);
        Mira("Hijo de un padre transparente: el que no acepta transparencia queda gris", sin.BackColor.ToArgb().ToString("X8") + " " + et.BackColor.A,
             "FFECE9D8 0");
        m.Controls.Remove(p);

        // ---- temporizador: el plazo no se corre, y lo que se junta sale una vez ----
        List<double> ticks = new List<double>(); Stopwatch rt = Stopwatch.StartNew();
        Timer t = new Timer(); t.Interval = 42; t.Tick += delegate { ticks.Add(rt.Elapsed.TotalMilliseconds); if (ticks.Count == 1) Thread.Sleep(100); if (ticks.Count == 3) t.Stop(); };
        rt.Restart(); t.Start(); Bombea(400);
        Mira("Timer de 42 ms con un Tick de 100 ms: el tercero", ticks.Count >= 3 ? (Math.Abs(ticks[2] - 168) < 8 ? "a los 168 ms" : "a los " + ticks[2].ToString("0") + " ms") : "faltan", "a los 168 ms");

        // ---- teclado ----
        C k = new C("k"); k.SetBounds(0, 0, 50, 50); m.Controls.Add(k); k.Focus(); Bombea(10);
        log.Clear(); k.ManejaTecla = true; Tecla(Keys.A);
        Mira("KeyDown.Handled no quita KeyPress", Log("k."), "KeyDown(A),KeyPress(97)");
        log.Clear(); k.SuprimeTecla = true; Tecla(Keys.A);
        Mira("SuppressKeyPress si", Log("k."), "KeyDown(A)");
        k.ManejaTecla = k.SuprimeTecla = false;
        log.Clear(); Escritorio.Tecla(Keys.ControlKey, true); Tecla(Keys.H); Escritorio.Tecla(Keys.ControlKey, false);
        Mira("Ctrl+H da el retroceso (8)", Log("k.").Replace("KeyDown(ControlKey),", ""), "KeyDown(H),KeyPress(8)");
        F g = new F("G."); g.Text = "g"; g.Show(); Bombea(20);
        log.Clear(); Escritorio.Tecla(Keys.Menu, true); Tecla(Keys.F4); Escritorio.Tecla(Keys.Menu, false); Bombea(30);
        Mira("Alt+F4 cierra la ventana activa", Log("G..").Replace("Deactivate,", "").Replace(",HandleDestroyed", "") + " disposed=" + g.IsDisposed, "FormClosing(UserClosing,None),FormClosed(UserClosing) disposed=True");

        // ---- raton ----
        m.Activate(); Bombea(10);
        C pp = new C("pp"); pp.SetBounds(100, 100, 200, 100); C hh = new C("hh"); hh.SetBounds(100, 0, 100, 100); pp.Controls.Add(hh); m.Controls.Add(pp); Bombea(20);
        log.Clear();
        Escritorio.RatonEn(120, 150); Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonEn(250, 150); Escritorio.RatonBoton(MouseButtons.Left, false);
        Mira("Soltar encima de un hijo: sin Click", Log("pp."), "MouseDown(1),MouseUp(1)");
        log.Clear(); Thread.Sleep(600);
        Escritorio.RatonEn(120, 150);
        Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonBoton(MouseButtons.Left, false);
        Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonBoton(MouseButtons.Left, false);
        Mira("Doble clic con StandardDoubleClick", Log("pp."), "MouseDown(1),Click,MouseUp(1),MouseDown(2),DoubleClick,MouseUp(1)");
        pp.Doble(false); log.Clear(); Thread.Sleep(600);
        Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonBoton(MouseButtons.Left, false);
        Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonBoton(MouseButtons.Left, false);
        Mira("Doble clic sin StandardDoubleClick: dos Click", Log("pp."), "MouseDown(1),Click,MouseUp(1),MouseDown(2),Click,MouseUp(1)");
        int clics = 0; Button bb = new Button(); bb.SetBounds(400, 100, 75, 23); bb.Click += delegate { clics++; }; m.Controls.Add(bb); Bombea(10); Thread.Sleep(600);
        Escritorio.RatonEn(420, 110);
        Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonBoton(MouseButtons.Left, false);
        Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonBoton(MouseButtons.Left, false);
        int doble = clics; Thread.Sleep(600);
        Escritorio.RatonBoton(MouseButtons.Right, true); Escritorio.RatonBoton(MouseButtons.Right, false);
        Mira("Button: doble clic = dos clics; el derecho no pulsa", "doble=" + doble + " derecho=" + (clics - doble), "doble=2 derecho=0");
        log.Clear(); hh.Focus(); Bombea(5); Escritorio.RatonRueda(1);
        Mira("Rueda que el hijo no atiende: sube al padre", Log("pp.") + "|" + Log("hh."), "MouseWheel|MouseWheel");
        log.Clear(); hh.ManejaRueda = true; Escritorio.RatonRueda(1);
        Mira("Rueda atendida: no sube", Log("pp.") + "|" + Log("hh."), "|MouseWheel");

        // ---- arrastrar una hoja por su barra de titulo, y el pulgar de la barra de una lista ----
        Form s = new Form(); s.Text = "s"; s.FormBorderStyle = FormBorderStyle.FixedDialog; s.ClientSize = new Size(300, 200);
        s.StartPosition = FormStartPosition.Manual; s.Location = new Point(100, 100); s.Show(); Bombea(20);
        Escritorio.RatonEn(150, 110); Escritorio.RatonBoton(MouseButtons.Left, true); Escritorio.RatonEn(200, 160); Escritorio.RatonEn(250, 210);
        Escritorio.RatonBoton(MouseButtons.Left, false); Bombea(10);
        Mira("Arrastrar la hoja por la barra de titulo", s.Location.ToString(), "{X=200,Y=200}");
        s.Close(); Bombea(10); m.Activate(); Bombea(10);
        ListBox lb = new ListBox(); lb.SetBounds(500, 300, 120, 96); for (int i = 0; i < 30; i++) lb.Items.Add("renglon " + i); m.Controls.Add(lb); Bombea(20);
        Thread.Sleep(600);
        Escritorio.RatonEn(500 + 120 - 9, 300 + 1 + 17 + 3); Escritorio.RatonBoton(MouseButtons.Left, true);
        int topAgarrado = lb.TopIndex;
        Escritorio.RatonEn(500 + 120 - 9, 300 + 1 + 17 + 3 + 30); Escritorio.RatonBoton(MouseButtons.Left, false); Bombea(10);
        Mira("Agarrar el pulgar no pasa de pagina; arrastrarlo baja la lista", "al agarrar " + topAgarrado + ", al soltar " + (lb.TopIndex > 0 ? "mas abajo" : lb.TopIndex.ToString()),
             "al agarrar 0, al soltar mas abajo");
        m.Controls.Remove(lb);

        // ---- la X roja de .NET: un control que truena al pintar ya no vuelve a llamar a OnPaint ----
        C x = new C("x"); x.LanzaAlPintar = true; x.SetBounds(600, 300, 50, 50); m.Controls.Add(x);
        int ex0 = excepciones; Bombea(150); x.Invalidate(); Bombea(150); x.Invalidate(); Bombea(150);
        Mira("OnPaint que lanza: una vez, y despues la X roja", "OnPaint=" + x.Pintado + " excepciones=" + (excepciones - ex0), "OnPaint=1 excepciones=1");

        // ---- MessageBox: Alt+F4 en un aviso con solo Aceptar devuelve OK; en uno Si/No no hace nada ----
        Luego(150, delegate { Escritorio.Tecla(Keys.Menu, true); Tecla(Keys.F4); Escritorio.Tecla(Keys.Menu, false); });
        Luego(1500, delegate { Tecla(Keys.Escape); });   // (por si Alt+F4 no lo cierra)
        Stopwatch mb = Stopwatch.StartNew();
        r = MessageBox.Show("solo aceptar", "prueba");
        Mira("MessageBox OK y Alt+F4", r + (mb.ElapsedMilliseconds < 1000 ? " con Alt+F4" : " con Escape"), "OK con Alt+F4");
        Luego(150, delegate { Escritorio.Tecla(Keys.Menu, true); Tecla(Keys.F4); Escritorio.Tecla(Keys.Menu, false); });
        Luego(400, delegate { Tecla(Keys.Enter); });
        r = MessageBox.Show("si o no", "prueba", MessageBoxButtons.YesNo);
        Mira("MessageBox Si/No: Alt+F4 no cierra, Intro da Si", r.ToString(), "Yes");
    }
}

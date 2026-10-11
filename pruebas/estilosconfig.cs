using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;
using Label = System.Windows.Forms.Label;

// Configuración y UI reales, pero sin reproducción, escaneo ni datos de producción.
class EstilosConfig
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type form, gpu; static int checks;
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw new Exception("Invocación: " + n, e.InnerException); } }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static Control Find(Control p, string accessible)
    {
        foreach (Control c in p.Controls) {
            if (c.AccessibleName == accessible) return c;
            Control found = Find(c, accessible); if (found != null) return found;
        }
        return null;
    }
    static Control Named(Control p, string name)
    {
        foreach (Control c in p.Controls) {
            if (c.Name == name) return c;
            Control found = Named(c, name); if (found != null) return found;
        }
        return null;
    }
    static Control WithText(Control p, string text)
    {
        foreach (Control c in p.Controls) {
            if (c.Text == text) return c;
            Control found = WithText(c, text); if (found != null) return found;
        }
        return null;
    }
    static void Click(Control c) { Check(c != null, "Existe el control"); Call(typeof(Control), c, "OnClick", EventArgs.Empty); }
    static object Owner() { return Codigo.FormSinEncender(form); }
    static object UiOwner()
    {
        object owner = Owner();
        // La base Form necesita sus propios campos para Invalidate/ClientSize al
        // restablecer. No se ejecuta el constructor de Form1, que arranca la Rocola.
        DynamicMethod initialize = new DynamicMethod("BaseFormAislada", typeof(void), new Type[] { typeof(object) }, typeof(EstilosConfig), true);
        ILGenerator code = initialize.GetILGenerator(); code.Emit(OpCodes.Ldarg_0);
        code.Emit(OpCodes.Castclass, typeof(Form)); code.Emit(OpCodes.Call, typeof(Form).GetConstructor(Type.EmptyTypes));
        code.Emit(OpCodes.Ret); initialize.Invoke(null, new object[] { owner });
        return owner;
    }
    static void CheckCard(Control card)
    {
        using (Bitmap bmp = new Bitmap(card.Width, card.Height))
        using (Graphics g = Graphics.FromImage(bmp)) {
            foreach (Control c in card.Controls) {
                Check(c.Left >= 0 && c.Top >= 32 && c.Right <= card.Width && c.Bottom <= card.Height,
                    "Control dentro de su tarjeta: " + c.Text);
                foreach (Control d in card.Controls)
                    if (c != d) Check(!c.Bounds.IntersectsWith(d.Bounds), "Sin solapamiento: " + c.Text + " / " + d.Text);
                if (c is Label) Check(g.MeasureString(c.Text, c.Font, c.Width).Height <= c.Height, "Texto legible: " + c.Text);
                else if (c.GetType().Name == "BotonPlano") {
                    SizeF s = g.MeasureString(c.Text, c.Font);
                    Check(s.Width <= c.Width - 4 && s.Height <= c.Height - 2, "Botón legible: " + c.Text);
                }
            }
        }
    }
    static void VisibleLayout(Control page, Control style, Control[] cards, int sheet)
    {
        Check(style.Visible, "El selector de estilo permanece visible");
        CheckCard(style);
        for (int i = 0; i < cards.Length; i++) {
            Check(cards[i] != null && cards[i].Visible == (i / 2 == sheet), "Solo se muestra el grupo seleccionado");
            CheckCard(cards[i]);
        }
        foreach (Control c in page.Controls) if (c.Visible) {
            foreach (Control other in page.Controls) if (other != c && other.Visible)
                Check(!c.Bounds.IntersectsWith(other.Bounds), "Capas visibles no ocultan otros mandos: " + c.Name + "/" + other.Name);
            Point center = new Point(c.Left + c.Width / 2, c.Top + c.Height / 2);
            Check(page.GetChildAtPoint(center, GetChildAtPointSkip.Invisible) == c,
                "Hit-test real alcanza el control: " + c.Name + "/" + c.Text);
        }
        foreach (string name in new string[] { "Legacy", "Espacio natural" }) {
            Control button = Find(style, "Estilo del fondo: " + name);
            Check(style.GetChildAtPoint(new Point(button.Left + button.Width / 2, button.Top + button.Height / 2)) == button,
                "Selector alcanzable, no solo Visible: " + name);
        }
    }
    static void Frame()
    {
        foreach (int height in new int[] { 732, 1008 })
        using (Form host = new Form())
        using (Button ready = new Button())
        using (Label version = new Label()) {
            host.FormBorderStyle = FormBorderStyle.FixedDialog;
            host.ClientSize = new Size(960, height);
            ready.SetBounds(804, height - 52, 140, 38); host.Controls.Add(ready); host.Controls.Add(version);
            int top = ready.Top; Size original = host.ClientSize;
            for (int repeat = 0; repeat < 4; repeat++) {
                Call(form, null, "AjustaMarcoFondo", host, ready, version, true, original, top);
                Check(host.ClientSize == new Size(960, 694) && ready.Top == 652 && ready.Bottom <= host.ClientSize.Height,
                    "Fondo y Listo caben en el marco de 1024x768");
                Check(!version.Visible, "La fecha no se superpone al pie");
                Call(form, null, "AjustaMarcoFondo", host, ready, version, false, original, top);
                Check(host.ClientSize == original && ready.Top == top, "Salir de Fondo restaura marco original");
            }
        }
    }
    static void Config(string root)
    {
        foreach (int n in new int[] { Int32.MinValue, -1, 0, 2, 255, Int32.MaxValue })
            Check((int)Call(form, null, "NormalizaEstiloFondo", n) == 0, "Número inválido vuelve a Legacy");
        Check((int)Call(form, null, "NormalizaEstiloFondo", 1) == 1, "Natural válido");
        foreach (string text in new string[] { "natural", " NATURAL ", "Natural", "\tNaTuRaL\r\n" })
            Check((int)Call(form, null, "LeeEstiloFondo", text) == 1, "Natural tolera mayúsculas y espacios");
        foreach (string text in new string[] { null, "", " ", "legacy", " LEGACY ", "1", "2", "new", "naturales", "natural extra" })
            Check((int)Call(form, null, "LeeEstiloFondo", text) == 0, "Texto inválido / Legacy usa 0");
        object owner = Owner(); Set(owner, "estiloFondo", 1); Call(form, owner, "LoadConfigPrefs");
        Check((int)Get(owner, "estiloFondo") == 0, "Sin configuración se restaura Legacy");
        string path = Path.Combine(root, "rocola_config.txt");
        foreach (bool guide in new bool[] { false, true }) {
            Set(owner, "guiaRojo", guide); Call(form, owner, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, owner);
            Check(File.ReadAllText(path).Contains("guia_rojo=" + (guide ? "1" : "0")), "Guía roja se guarda");
            object readGuide = Owner(); Call(form, readGuide, "LoadConfigPrefs");
            Check((bool)Get(readGuide, "guiaRojo") == guide, "Guía roja restaura su opción");
        }
        foreach (bool animated in new bool[] { false, true }) {
            Set(owner, "asistenteAnimado", animated); Call(form, owner, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, owner);
            Check(File.ReadAllText(path).Contains("asistente_animado=" + (animated ? "1" : "0")), "Animación del asistente se guarda");
            object readAssistant = Owner(); Call(form, readAssistant, "LoadConfigPrefs");
            Check((bool)Get(readAssistant, "asistenteAnimado") == animated, "Animación del asistente restaura su opción");
        }
        foreach (string key in new string[] { "estilo_fondo=", "ESTILO_FONDO=", "Estilo_Fondo=" })
            foreach (string value in new string[] { "legacy", "LEGACY ", "natural", " Natural ", "raro" }) {
                File.WriteAllText(path, "  " + key + value + "\r\n");
                Call(form, owner, "LoadConfigPrefs");
                Check((int)Get(owner, "estiloFondo") == (int)Call(form, null, "LeeEstiloFondo", value),
                    "Lectura robusta de clave y valor");
            }
        Set(owner, "estiloFondo", 1);
        File.WriteAllText(path, "gpu=1\r\ntope_fps=200\r\nanim_ondas_fps=24\r\nanim_cubos_fps=50\r\n");
        Call(form, owner, "LoadConfigPrefs");
        Check((int)Get(owner, "estiloFondo") == 0, "Configuración antigua conserva Legacy");
        Check((int)Get(owner, "fpsTope") == 200 && (int)Get(owner, "animOndasFps") == 24 && (int)Get(owner, "animCubosFps") == 50,
            "Migrar no cambia preferencias existentes");
        foreach (int invalid in new int[] { -1, 0, 1, 2 }) {
            Set(owner, "estiloFondo", invalid); Call(form, owner, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, owner);
            string expected = invalid == 1 ? "natural" : "legacy";
            Check(File.ReadAllText(path).Contains("estilo_fondo=" + expected), "Guardado canónico y normalizado");
            object read = Owner(); Call(form, read, "LoadConfigPrefs");
            Check((int)Get(read, "estiloFondo") == (invalid == 1 ? 1 : 0), "Vuelta de guardado");
        }
    }
    static void Ui(string root, bool preview)
    {
        object owner = UiOwner(); Control gpuPanel = gpu == null ? null : (Control)Activator.CreateInstance(gpu, true);
        Label toast = new Label(); Timer toastTimer = new Timer();
        Set(owner, "lblToast", toast); Set(owner, "toastTimer", toastTimer);
        if (gpuPanel != null) Set(owner, "gpuPanel", gpuPanel);
        try {
            Set(owner, "fpsTope", 200); Set(owner, "animOndasFps", 24); Set(owner, "animCubosFps", 50);
            Set(owner, "nebNaturalFuerza", .37f);
            string[] flags = { "nebulosasCine", "cieloProfundo", "auroraBandas", "ondasSuaves", "estrellasFinas",
                "vinetaSuave", "haloSeleccion", "estadoTransparente", "progresoNeon", "neonIntenso", "progresoFluido",
                "lunaRealista", "haloLunar", "polvoLunar", "lunaProtagonista", "estelaEstelar", "estrella3d",
                "coloresFluidos", "paletaCompleta", "cancionesCristal", "colaDoce" };
            for (int i = 0; i < flags.Length; i++) Set(owner, flags[i], preview || i % 2 == 0);
            using (Font normal = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font small = new Font("Segoe UI", 8.5f))
            using (Panel page = (Panel)Call(form, owner, "PaginaFondo", normal, normal, small, normal, Color.White, Color.Gray, Color.DimGray))
            {
                if (preview) {
                    using (Form window = new Form())
                    using (Label heading = new Label())
                    using (Control ready = (Control)Activator.CreateInstance(form.GetNestedType("BotonPlano", B), true)) {
                        window.Text = "Rocola - Fondo (prueba aislada)";
                        window.BackColor = page.BackColor; window.ClientSize = new Size(960, 694);
                        window.FormBorderStyle = FormBorderStyle.FixedDialog;
                        window.MaximizeBox = false; window.StartPosition = FormStartPosition.CenterScreen;
                        heading.SetBounds(22, 12, 850, 28); heading.Font = normal;
                        heading.ForeColor = Color.White; heading.Text = "El fondo y sus adornos - prueba aislada (no guarda preferencias)";
                        ready.SetBounds(804, 652, 140, 38); ready.Font = normal; ready.Text = "Listo";
                        Set(ready, "Relleno", Color.FromArgb(30, 96, 70));
                        Set(ready, "Borde", Color.FromArgb(72, 182, 132));
                        ready.Click += delegate { window.Close(); };
                        window.Controls.Add(heading); window.Controls.Add(page); window.Controls.Add(ready); ready.BringToFront();
                        window.KeyPreview = true; window.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) window.Close(); };
                        window.ShowDialog(); window.Controls.Remove(page);
                    }
                    return;
                }
                Control adornments = Named(page, "FondoAjustesAdornos"), details = Named(page, "FondoAjustesDetalle"), style = Named(page, "FondoEstilo");
                Control[] cards = { adornments, details, Named(page, "FondoInterfazBarra"), Named(page, "FondoInterfazInterior"),
                    Named(page, "FondoEstrellaAspecto"), Named(page, "FondoEstrellaReposo") };
                Check(adornments != null && details != null && style != null, "Selector y tarjetas disponibles");
                Check(page.Size == new Size(960, 636) && !page.AutoScroll, "Fondo conserva tamaño y no necesita scroll");
                Check(page.Top == 58, "Fondo reutiliza el espacio superior sin reducir mandos");
                Check(adornments.Visible && details.Visible && style.Visible, "Fondo abre el cielo con el estilo siempre visible");
                foreach (Control c in page.Controls) Check(c.Left >= 0 && c.Top >= 0 && c.Right <= page.Width && c.Bottom <= page.Height,
                    "Todo cabe dentro de Fondo");
                Control tabA = Find(page, "Fondo: Cielo y aurora");
                string[] sheets = { "Cielo y aurora", "Interfaz", "Estrella" };
                VisibleLayout(page, style, cards, 0);
                for (int i = 0; i < sheets.Length; i++) {
                    Control tab = Find(page, "Fondo: " + sheets[i]);
                    Check(tab != null && tab.TabIndex == i + 1 && tab.Bottom < cards[0].Top && tab.Top > style.Bottom,
                        "Hoja con fila propia y orden de teclado");
                    Click(tab); VisibleLayout(page, style, cards, i);
                    using (Bitmap shot = new Bitmap(page.Width, page.Height)) {
                        page.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size));
                        shot.Save(Path.Combine(root, "fondo-hoja-" + i + ".png"));
                    }
                }
                for (int iteration = 0; iteration < 12; iteration++) {
                    int selected = iteration % 2;
                    Set(owner, "f3CambioGraf", false);
                    Click(Find(style, "Estilo del fondo: " + (selected == 1 ? "Espacio natural" : "Legacy")));
                    Check((int)Get(owner, "estiloFondo") == selected && (bool)Get(owner, "f3CambioGraf"), "El estilo se aplica y queda pendiente de guardar");
                    if (gpuPanel != null) Check((bool)Get(gpuPanel, "FondoNatural") == (selected == 1), "Se aplica inmediatamente al panel GPU");
                    for (int i = 0; i < flags.Length; i++) Check((bool)Get(owner, flags[i]) == (i % 2 == 0), "Alternar conserva " + flags[i]);
                    Check((float)Get(owner, "nebNaturalFuerza") == .37f && (int)Get(owner, "fpsTope") == 200
                        && (int)Get(owner, "animOndasFps") == 24 && (int)Get(owner, "animCubosFps") == 50,
                        "Estilo no cambia fuerza, límite ni cadencias");
                    Check(Find(style, "Estilo del fondo: " + (selected == 1 ? "Espacio natural" : "Legacy")).AccessibleDescription == "Seleccionado",
                        "Selección visible y accesible");
                    Call(form, owner, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, owner);
                    object read = Owner(); Call(form, read, "LoadConfigPrefs");
                    Check((int)Get(read, "estiloFondo") == selected, "Elección persistente");
                }
                Control legacy = Find(style, "Estilo del fondo: Legacy"), natural = Find(style, "Estilo del fondo: Espacio natural");
                KeyEventArgs enter = new KeyEventArgs(Keys.Enter); Call(natural.GetType(), natural, "OnKeyDown", enter);
                Check((int)Get(owner, "estiloFondo") == 1 && enter.SuppressKeyPress, "Enter aplica Natural sin campana");
                KeyEventArgs space = new KeyEventArgs(Keys.Space); Call(legacy.GetType(), legacy, "OnKeyDown", space);
                Check((int)Get(owner, "estiloFondo") == 0 && space.SuppressKeyPress, "Espacio aplica Legacy sin campana");
                natural.Enabled = false; Call(natural.GetType(), natural, "OnKeyDown", new KeyEventArgs(Keys.Enter));
                Check((int)Get(owner, "estiloFondo") == 0, "Deshabilitado no se activa por teclado");
                natural.Enabled = true; Click(natural);
                // Las capturas son del mismo control real, sin abrir ni modificar la Rocola de producción.
                using (Bitmap image = new Bitmap(page.Width, page.Height)) {
                    page.Font = normal; page.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save(Path.Combine(root, "fondo-estilo.png"));
                }
                Click(WithText(page, "Fondo como de fabrica"));
                Check((int)Get(owner, "estiloFondo") == 0 && legacy.AccessibleDescription == "Seleccionado"
                    && natural.AccessibleDescription != "Seleccionado", "Fábrica restaura y repinta Legacy");
                if (gpuPanel != null) Check(!(bool)Get(gpuPanel, "FondoNatural"), "Fábrica restaura Legacy en GPU");
                for (int i = 0; i < flags.Length; i++) Check((bool)Get(owner, flags[i]), "Fábrica restaura " + flags[i]);
                Check((int)Get(owner, "animOndasFps") == 24 && (int)Get(owner, "animCubosFps") == 50, "Reset Fondo no cambia cadencias de Imagen");
                Click(tabA); Check(adornments.Visible && details.Visible && style.Visible, "Cielo y selector quedan disponibles al volver");
                using (Bitmap image = new Bitmap(page.Width, page.Height)) {
                    page.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save(Path.Combine(root, "fondo-ajustes.png"));
                }
            }
        } finally { toastTimer.Stop(); toastTimer.Dispose(); toast.Dispose(); if (gpuPanel != null) gpuPanel.Dispose(); }
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            bool preview = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) == "fondo-preview";
            string executable = args.Length > 0 ? args[0] : Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Rocola.exe");
            Assembly assembly = Assembly.LoadFrom(executable); form = assembly.GetType("Rocola.Form1", true);
            gpu = assembly.GetType("Rocola.GpuPanel", false);
            string root = Path.Combine(Path.GetTempPath(), "rocola-estilosconfig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); form.GetField("carpetaDatos", B).SetValue(null, root);
            if (preview) { Ui(root, true); Console.WriteLine("Vista aislada cerrada; no se modificaron preferencias de produccion."); return 0; }
            Config(root); Ui(root, false); Frame();
            Console.WriteLine("PASS " + checks + " comprobaciones; configuración/UI aisladas, " + (gpu == null ? "CPU" : "GPU") + ". Capturas: " + root);
            return 0;
        } catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
    }
}

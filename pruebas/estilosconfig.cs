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
    static void Ui(string root)
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
            for (int i = 0; i < flags.Length; i++) Set(owner, flags[i], i % 2 == 0);
            using (Font normal = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font small = new Font("Segoe UI", 8.5f))
            using (Panel page = (Panel)Call(form, owner, "PaginaFondo", normal, normal, small, normal, Color.White, Color.Gray, Color.DimGray))
            {
                Control adornments = Named(page, "FondoAjustesAdornos"), details = Named(page, "FondoAjustesDetalle"), style = Named(page, "FondoEstilo");
                Check(adornments != null && details != null && style != null, "Las tres tarjetas están disponibles");
                Check(page.Size == new Size(960, 636) && !page.AutoScroll, "Fondo conserva tamaño y no necesita scroll");
                Check(adornments.Visible && details.Visible && !style.Visible, "Fondo abre sus controles originales");
                foreach (Control c in page.Controls) Check(c.Left >= 0 && c.Top >= 0 && c.Right <= page.Width && c.Bottom <= page.Height,
                    "Todo cabe dentro de Fondo");
                CheckCard(adornments); CheckCard(details); CheckCard(style);
                Control tabA = Find(page, "Fondo: Ajustes"), tabS = Find(page, "Fondo: Estilo");
                Check(tabA != null && tabS != null && tabA.Bottom < 40 && tabS.Bottom < 40, "Hojas caben en el antiguo encabezado");
                Check(!tabA.Bounds.IntersectsWith(tabS.Bounds) && tabA.TabIndex == 0 && tabS.TabIndex == 1, "Tabs sin cruces y con teclado ordenado");
                Click(tabS); Check(!adornments.Visible && !details.Visible && style.Visible, "Estilo no deja ajustes debajo");
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
                Click(tabA); Check(adornments.Visible && details.Visible && !style.Visible, "Ajustes queda disponible al volver");
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
            Assembly assembly = Assembly.LoadFrom(args[0]); form = assembly.GetType("Rocola.Form1", true);
            gpu = assembly.GetType("Rocola.GpuPanel", false);
            string root = Path.Combine(Path.GetTempPath(), "rocola-estilosconfig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); form.GetField("carpetaDatos", B).SetValue(null, root);
            Config(root); Ui(root);
            Console.WriteLine("PASS " + checks + " comprobaciones; configuración/UI aisladas, " + (gpu == null ? "CPU" : "GPU") + ". Capturas: " + root);
            return 0;
        } catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
    }
}

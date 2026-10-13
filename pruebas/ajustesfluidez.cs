using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// UI real sin iniciar música, escaneo ni ventanas de producción; datos aislados.
class AjustesFluidez
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type form; static int checks;
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
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
    static void Click(Control c) { Check(c != null, "El control es accesible"); Call(typeof(Control), c, "OnClick", EventArgs.Empty); }
    static object Owner() { return Codigo.FormSinEncender(form); }
    static void Bounds(Control card)
    {
        foreach (Control c in card.Controls) {
            Check(c.Left >= 0 && c.Top >= 0 && c.Right <= card.ClientSize.Width && c.Bottom <= card.ClientSize.Height,
                "Control recortado: " + c.Text);
            foreach (Control d in card.Controls)
                if (c != d) Check(!c.Bounds.IntersectsWith(d.Bounds), "Solapamiento: " + c.Text + " / " + d.Text);
        }
        using (Bitmap bmp = new Bitmap(card.Width, card.Height))
        using (Graphics g = Graphics.FromImage(bmp)) {
            foreach (Control c in card.Controls)
                if (c is Label) Check(g.MeasureString(c.Text, c.Font, c.Width).Height <= c.Height, "Explicación recortada: " + c.Text);
                else if (c.GetType().Name == "BotonPlano") {
                    SizeF size = g.MeasureString(c.Text, c.Font);
                    Check(size.Width <= c.Width - 4 && size.Height <= c.Height - 2, "Botón recortado: " + c.Text);
                }
        }
    }
    static void Settings(string root)
    {
        foreach (int fps in new int[] { 24, 50, 60 }) {
            Check((int)Call(form, null, "NormalizaFluidezAnimacion", fps) == fps, "Cadencia válida " + fps);
            Check((int)Call(form, null, "LeeFluidezAnimacion", "  " + fps + " ") == fps, "Lectura válida " + fps);
        }
        foreach (string value in new string[] { null, "", "0", "-24", "30", "49", "61", "240", "50.0", "9999999999999999", "auto" })
            Check((int)Call(form, null, "LeeFluidezAnimacion", value) == 60, "Texto inválido vuelve a 60: " + value);
        foreach (int fps in new int[] { Int32.MinValue, -1, 0, 25, 59, 61, Int32.MaxValue })
            Check((int)Call(form, null, "NormalizaFluidezAnimacion", fps) == 60, "Número inválido vuelve a 60");
        object owner = Owner();
        Call(form, owner, "LoadConfigPrefs");
        Check((int)Get(owner, "animOndasFps") == 60 && (int)Get(owner, "animCubosFps") == 60, "Sin config se usan 60 / 60");
        File.WriteAllText(Path.Combine(root, "rocola_config.txt"), "gpu=1\r\ntope_fps=200\r\n");
        Set(owner, "animOndasFps", 24); Set(owner, "animCubosFps", 50);
        Call(form, owner, "LoadConfigPrefs");
        Check((int)Get(owner, "animOndasFps") == 60 && (int)Get(owner, "animCubosFps") == 60 && (int)Get(owner, "fpsTope") == 200,
            "Config antigua conserva el límite general y usa nuevas cadencias 60 / 60");
        File.WriteAllText(Path.Combine(root, "rocola_config.txt"), "anim_ondas_fps=30\r\nanim_cubos_fps=50\r\n");
        Call(form, owner, "LoadConfigPrefs");
        Check((int)Get(owner, "animOndasFps") == 60 && (int)Get(owner, "animCubosFps") == 50, "Clave inválida no invalida la otra");
        using (Font normal = new Font("Segoe UI", 10f, FontStyle.Bold))
        using (Font small = new Font("Segoe UI", 8.5f))
        using (Panel page = (Panel)Call(form, owner, "PaginaImagen", normal, normal, small, normal, Color.White, Color.Gray, Color.DimGray))
        {
            Control effects = Named(page, "Efectos"), fluency = Named(page, "Fluidez");
            Check(effects != null && fluency != null && effects.Visible && !fluency.Visible, "Imagen empieza en su hoja original");
            Check(effects.Parent.Bottom <= page.Height && effects.Parent.Right <= page.Width && !page.AutoScroll,
                "Dos hojas caben sin desplazar ni ampliar Imagen");
            Bounds(effects); Bounds(fluency);
            Click(Find(effects, "Imagen: Fluidez"));
            Check(!effects.Visible && fluency.Visible, "Se puede acceder a la hoja de fluidez");
            Check(Find(fluency, "Imagen: Efectos").TabIndex == 0 && Find(fluency, "Imagen: Fluidez").TabIndex == 1,
                "Orden de teclado empieza por las hojas visibles");
            foreach (int waves in new int[] { 24, 50, 60 })
            foreach (int cubes in new int[] { 24, 50, 60 }) {
                Set(owner, "f3CambioGraf", false);
                Click(Find(fluency, "Ondas de sonido: " + waves + " FPS"));
                Click(Find(fluency, "Todos los cubos: " + cubes + " FPS"));
                Check((int)Get(owner, "animOndasFps") == waves && (int)Get(owner, "animCubosFps") == cubes,
                    "Ajustes independientes " + waves + " / " + cubes);
                Check((bool)Get(owner, "f3CambioGraf"), "Cambio marcado para guardar al cerrar");
                Check((int)Get(owner, "fpsTope") == 200, "No cambia el límite general de FPS");
                Check(Find(fluency, "Ondas de sonido: " + waves + " FPS").AccessibleDescription == "Seleccionado"
                    && Find(fluency, "Todos los cubos: " + cubes + " FPS").AccessibleDescription == "Seleccionado",
                    "Selección visible y accesible coincide con ambos valores");
                Call(form, owner, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, owner);
                object read = Owner(); Call(form, read, "LoadConfigPrefs");
                Check((int)Get(read, "animOndasFps") == waves && (int)Get(read, "animCubosFps") == cubes,
                    "Ambos ajustes persisten " + waves + " / " + cubes);
                string config = File.ReadAllText(Path.Combine(root, "rocola_config.txt"));
                Check(config.Contains("anim_ondas_fps=" + waves) && config.Contains("anim_cubos_fps=" + cubes),
                    "Las dos claves independientes se escriben");
            }
            Control reset = null; foreach (Control c in page.Controls) if (c.Text == "Graficos como de fabrica") reset = c;
            Click(reset);
            Check((int)Get(owner, "animOndasFps") == 60 && (int)Get(owner, "animCubosFps") == 60, "Fábrica restaura 60 / 60");
            foreach (string group in new string[] { "Ondas de sonido", "Todos los cubos" })
                Check(Find(fluency, group + ": 60 FPS").AccessibleDescription == "Seleccionado"
                    && Find(fluency, group + ": 24 FPS").AccessibleDescription != "Seleccionado", "Fábrica repinta la selección");
            using (Bitmap image = new Bitmap(fluency.Width, fluency.Height)) {
                Point position = fluency.Location; Control parent = fluency.Parent;
                parent.Controls.Remove(fluency); fluency.Location = Point.Empty;
                try { fluency.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); }
                finally { parent.Controls.Add(fluency); fluency.Location = position; }
                image.Save(Path.Combine(root, "fluidez.png"));
            }
            using (Bitmap image = new Bitmap(page.Width, page.Height)) {
                page.Font = normal; page.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(root, "imagen-fluidez.png"));
            }
            Control option = Find(fluency, "Todos los cubos: 24 FPS");
            KeyEventArgs enabled = new KeyEventArgs(Keys.Enter);
            Call(option.GetType(), option, "OnKeyDown", enabled);
            Check((int)Get(owner, "animCubosFps") == 24 && enabled.SuppressKeyPress, "Enter selecciona sin sonido de tecla");
            option.Enabled = false; Set(owner, "animCubosFps", 50);
            Call(option.GetType(), option, "OnKeyDown", new KeyEventArgs(Keys.Space));
            Check((int)Get(owner, "animCubosFps") == 50, "Un botón deshabilitado no aplica ajustes por teclado");
            Click(Find(fluency, "Imagen: Efectos"));
            Check(effects.Visible && !fluency.Visible, "Efectos sigue disponible al volver");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
            string root = Path.Combine(Path.GetTempPath(), "rocola-fluidez-ajustes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); form.GetField("carpetaDatos", B).SetValue(null, root);
            Settings(root);
            Console.WriteLine("PASS " + checks + " comprobaciones; UI real, configuración aislada. Captura: " + Path.Combine(root, "fluidez.png"));
            return 0;
        } catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
    }
}

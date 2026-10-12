using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;
using Label = System.Windows.Forms.Label;

// CTRL+F3 -> FONDO, CON UN SOLO FONDO (12-10-2026): Espacio natural retirado, sus claves ignoradas y las tres hojas en el
// marco de siempre. Configuración y UI reales, pero sin reproducción, escaneo ni datos de producción.
class HojasFondo
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
        DynamicMethod initialize = new DynamicMethod("BaseFormAislada", typeof(void), new Type[] { typeof(object) }, typeof(HojasFondo), true);
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
    static void VisibleLayout(Control page, Control[] cards, int sheet)
    {
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
    }
    // UN SOLO FONDO (decision del usuario del 12-10-2026): Espacio natural se retiro entero.
    static void SinNatural()
    {
        foreach (string n in new string[] { "estiloFondo", "auroraReal", "lunaFase", "cieloHora" })
            Check(form.GetField(n, B) == null, "Ya no existe la preferencia " + n);
        foreach (string n in new string[] { "NormalizaEstiloFondo", "LeeEstiloFondo", "TextoEstiloFondo", "MontaEstiloFondo", "AjustaMarcoFondo", "BakeSongGlassStyled" })
            Check(form.GetMethod(n, B) == null, "Ya no existe " + n);
        Check(form.Assembly.GetType("Rocola.PreparacionFondoNatural", false) == null, "Sin preparacion de mapas de Natural");
        if (gpu == null) return;
        foreach (string n in new string[] { "FondoNatural", "AuroraReal", "LunaReal", "CieloHora" })
            Check(gpu.GetField(n, B) == null && gpu.GetProperty(n, B) == null, "El panel GPU ya no tiene " + n);
        Type campo = form.Assembly.GetType("Rocola.GpuStarfield", true);
        Check(campo.GetField("FondoNatural", B) == null && campo.GetMethod("BakeNaturalStarTexture", B) == null, "El campo de estrellas solo tiene el atlas Legacy");
        foreach (string n in new string[] { "BakeNebulosaNatural", "BakeViaNatural", "BakeVinetaNatural", "BakeStarNatural", "BakeLunaReal", "AurMallaNatural", "AurBrilloNatural", "CieloEstilo", "TinteNebulosaEstilo" })
            Check(gpu.GetMethod(n, B) == null, "Ya no existe " + n);
        // Lo de Legacy que se llama "natural" desde septiembre (las nebulosas con detalle natural) sigue.
        Check(gpu.GetMethod("BakeNatural", B) != null && form.GetField("nebNaturalFuerza", B) != null, "Las nebulosas con detalle natural de Legacy siguen");
    }
    static void Config(string root)
    {
        object owner = Owner();
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
        // Una configuracion de quien tenia Natural: sus claves se ignoran, lo demas se respeta y no se vuelven a escribir.
        File.WriteAllText(path, "gpu=1\r\nestilo_fondo=natural\r\naurora_real=0\r\nluna_fase=0\r\ncielo_hora=0\r\n"
            + "tope_fps=200\r\nanim_ondas_fps=24\r\nanim_cubos_fps=50\r\nestela_estelar=0\r\nestrella_3d=0\r\n");
        object old = Owner(); Call(form, old, "LoadConfigPrefs");
        Check((int)Get(old, "fpsTope") == 200 && (int)Get(old, "animOndasFps") == 24 && (int)Get(old, "animCubosFps") == 50
            && !(bool)Get(old, "estelaEstelar") && !(bool)Get(old, "estrella3d"), "Las preferencias de siempre se conservan");
        Call(form, old, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, old);
        string saved = File.ReadAllText(path);
        foreach (string key in new string[] { "estilo_fondo=", "aurora_real=", "luna_fase=", "cielo_hora=" })
            Check(!saved.Contains(key), "La clave retirada no se vuelve a escribir: " + key);
        Check(saved.Contains("tope_fps=200") && saved.Contains("estrella_3d=0"), "Guardar conserva lo demas");
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
                Control adornments = Named(page, "FondoAjustesAdornos"), details = Named(page, "FondoAjustesDetalle");
                Control[] cards = { adornments, details, Named(page, "FondoInterfazBarra"), Named(page, "FondoInterfazInterior"),
                    Named(page, "FondoEstrellaAspecto"), Named(page, "FondoEstrellaReposo") };
                Check(adornments != null && details != null, "Tarjetas disponibles");
                Check(Named(page, "FondoEstilo") == null && Find(page, "Estilo del fondo") == null
                    && Find(page, "Estilo del fondo: Espacio natural") == null, "Sin selector de estilo");
                Check(WithText(page, "Aurora con colores reales (Natural)") == null && WithText(page, "Luna con su fase real (Natural)") == null
                    && WithText(page, "Cielo segun la hora (Natural)") == null, "Sin los interruptores de Natural");
                Check(page.Size == new Size(960, 636) && page.Top == 96 && !page.AutoScroll, "Fondo usa el marco de las demas paginas, sin scroll");
                Check(adornments.Visible && details.Visible, "Fondo abre en Cielo y aurora");
                foreach (Control c in page.Controls) Check(c.Left >= 0 && c.Top >= 0 && c.Right <= page.Width && c.Bottom <= page.Height,
                    "Todo cabe dentro de Fondo");
                Control reset = WithText(page, "Fondo como de fabrica");
                Check(reset != null && cards[0].Bottom < reset.Top, "Las tarjetas no llegan al boton de fabrica");
                Control tabA = Find(page, "Fondo: Cielo y aurora");
                string[] sheets = { "Cielo y aurora", "Interfaz", "Estrella" };
                VisibleLayout(page, cards, 0);
                for (int i = 0; i < sheets.Length; i++) {
                    Control tab = Find(page, "Fondo: " + sheets[i]);
                    Check(tab != null && tab.TabIndex == i && tab.Top == 4 && tab.Bottom < cards[0].Top,
                        "Hoja con fila propia, arriba, y orden de teclado");
                    Click(tab); VisibleLayout(page, cards, i);
                    using (Bitmap shot = new Bitmap(page.Width, page.Height)) {
                        page.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size));
                        shot.Save(Path.Combine(root, "fondo-hoja-" + i + ".png"));
                    }
                }
                Click(reset);
                for (int i = 0; i < flags.Length; i++) Check((bool)Get(owner, flags[i]), "Fábrica restaura " + flags[i]);
                Check((int)Get(owner, "animOndasFps") == 24 && (int)Get(owner, "animCubosFps") == 50, "Reset Fondo no cambia cadencias de Imagen");
                Click(tabA); Check(adornments.Visible && details.Visible, "Cielo disponible al volver");
            }
        } finally { toastTimer.Stop(); toastTimer.Dispose(); toast.Dispose(); if (gpuPanel != null) gpuPanel.Dispose(); }
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            string executable = args.Length > 0 ? args[0] : Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Rocola.exe");
            Assembly assembly = Assembly.LoadFrom(executable); form = assembly.GetType("Rocola.Form1", true);
            gpu = assembly.GetType("Rocola.GpuPanel", false);
            string root = Path.Combine(Path.GetTempPath(), "rocola-hojasfondo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); form.GetField("carpetaDatos", B).SetValue(null, root);
            SinNatural(); Config(root); Ui(root);
            Console.WriteLine("PASS " + checks + " comprobaciones; configuración/UI aisladas, " + (gpu == null ? "CPU" : "GPU") + ". Capturas: " + root);
            return 0;
        } catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
    }
}

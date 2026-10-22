using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;
using Label = System.Windows.Forms.Label;

// CTRL+F3 DE VERDAD, PAGINA POR PAGINA Y HOJA POR HOJA (barrido del 12-10-2026).
// Abre la ventana real de ajustes (formulario aislado, datos aparte) y en cada pagina y cada hoja mira:
//   - que nada visible quede fuera de la ventana (la fecha de la version estaba en la y 734 de una ventana de 732);
//   - que ningun texto ni mando quede debajo del boton Listo (dos notas llegaban hasta el borde);
//   - que los textos quepan en su caja: con la letra de Mono y con una mas ancha (DejaVu Sans), parecida a la de XP.
// Las letras de Windows no estan aqui: es una aproximacion, no una foto de la maquina del bar.
class VentanaF3
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Form dlg; static Control listo; static int ok, mal; static List<string> vistos = new List<string>();
    static void Mal(string s) { if (vistos.Contains(s)) return; vistos.Add(s); mal++; Console.WriteLine("  FALLO " + s); }
    static string Ruta(Control c) { string s = ""; for (Control x = c; x != null && x != dlg; x = x.Parent) s = (string.IsNullOrEmpty(x.Name) ? x.GetType().Name : x.Name) + "/" + s; return s; }
    static string Corto(string s) { s = (s ?? "").Replace("\r", "").Replace("\n", " | "); return s.Length > 60 ? s.Substring(0, 60) + "..." : s; }
    static Control Boton(Control p, string texto)
    {
        foreach (Control c in p.Controls) { if (c.GetType().Name == "BotonPlano" && c.Text == texto && c.Visible) return c; Control r = Boton(c, texto); if (r != null) return r; }
        return null;
    }
    static void Click(Control c) { typeof(Control).GetMethod("OnClick", T).Invoke(c, new object[] { EventArgs.Empty }); }
    static void Revisa(Control p, Graphics g, string donde)
    {
        Rectangle lr = listo.RectangleToScreen(listo.ClientRectangle), ventana = dlg.RectangleToScreen(dlg.ClientRectangle);
        foreach (Control c in p.Controls)
        {
            if (!c.Visible) continue;
            Rectangle r = c.RectangleToScreen(c.ClientRectangle);
            if (c.Width > 0 && c.Height > 0 && !ventana.Contains(r)) Mal(donde + ": fuera de la ventana: " + Ruta(c) + " «" + Corto(c.Text) + "»"); else ok++;
            if (c.Controls.Count > 0) { Revisa(c, g, donde); continue; }
            // Un letrero que queda ENCIMA de un mando (antes en el orden de dibujo) y lo pisa: le borra un trozo con el fondo
            // (el boton "El local y sus bocinas..." perdia su borde de arriba bajo la nota del DSEE).
            string tipo0 = c.GetType().Name;
            if ((tipo0 == "BotonPlano" || tipo0 == "Interruptor" || tipo0 == "Deslizador") && p != null)
            {
                int yo = p.Controls.GetChildIndex(c);
                foreach (Control s in p.Controls)
                    if (s != c && s.Visible && s is Label && p.Controls.GetChildIndex(s) < yo && s.Bounds.IntersectsWith(c.Bounds))
                        Mal(donde + ": un letrero pisa un mando: " + Ruta(s) + " «" + Corto(s.Text) + "» sobre " + Ruta(c) + " «" + Corto(c.Text) + "»");
                ok++;
            }
            if (c != listo && r.IntersectsWith(lr)) Mal(donde + ": debajo de Listo: " + Ruta(c) + " «" + Corto(c.Text) + "»"); else ok++;
            if (string.IsNullOrEmpty(c.Text) || c.Font == null || c.Width <= 0) continue;
            string tipo = c.GetType().Name;
            if (c is Label && !((Label)c).AutoSize)
            {
                SizeF s = g.MeasureString(c.Text, c.Font, c.Width);
                if (s.Height > c.Height + 1) Mal(donde + ": texto cortado (" + Math.Ceiling(s.Height) + " de " + c.Height + " px): " + Ruta(c) + " «" + Corto(c.Text) + "»"); else ok++;
            }
            else if (tipo == "Interruptor" || tipo == "BotonPlano")
            {
                SizeF s = g.MeasureString(c.Text, c.Font);
                int sobra = tipo == "Interruptor" ? 50 : 6;
                if (s.Width + sobra > c.Width + 1) Mal(donde + ": texto que no cabe (" + Math.Ceiling(s.Width + sobra) + " de " + c.Width + " px): " + Ruta(c) + " «" + Corto(c.Text) + "»"); else ok++;
            }
        }
    }
    static void Mira(string donde)
    {
        dlg.Refresh(); Application.DoEvents();
        using (Graphics g = dlg.CreateGraphics()) Revisa(dlg, g, donde);
    }
    [STAThread] static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rocola_ventanaf3_" + Guid.NewGuid().ToString("N")));
        Assembly asm = Assembly.LoadFrom(args[0]); Type form = asm.GetType("Rocola.Form1", true);
        bool gpu = asm.GetType("Rocola.GpuPanel", false) != null;
        int vueltas = 0;
        foreach (string letra in new string[] { null, "DejaVu Sans" })
        {
            object f = Codigo.FormSinEncender(form);
            DynamicMethod dm = new DynamicMethod("BaseF3", typeof(void), new Type[] { typeof(object) }, typeof(VentanaF3), true);
            ILGenerator il = dm.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, typeof(Form));
            il.Emit(OpCodes.Call, typeof(Form).GetConstructor(Type.EmptyTypes)); il.Emit(OpCodes.Ret);
            dm.Invoke(null, new object[] { f });
            form.GetMethod("BuildUi", T).Invoke(f, null);
            try { form.GetMethod("LoadConfigPrefs", T).Invoke(f, null); } catch { }
            if (gpu) form.GetField("gpuEnabled", T).SetValue(f, true);
            if (letra != null) { try { form.GetField("familiaUi", T).SetValue(null, new FontFamily(letra)); } catch { Console.WriteLine("  (sin " + letra + ": se mira solo con la de Mono)"); continue; } }
            dlg = null; listo = null;
            string nombre = letra == null ? "letra de Mono" : letra;
            Timer t = new Timer(); t.Interval = 400; int n = 0;
            t.Tick += delegate
            {
                n++;
                foreach (Form o in Application.OpenForms) if (!ReferenceEquals(o, f)) { dlg = o; break; }
                if (dlg == null) { if (n > 30) { t.Stop(); Mal("la ventana de ajustes no se abrio"); } return; }
                t.Stop(); dlg.Location = new Point(0, 0);
                listo = Boton(dlg, "Listo");
                if (listo == null) { Mal("no hay boton Listo"); dlg.Close(); return; }
                foreach (string pag in new string[] { "Sonido", "Imagen", "Detalles", "Colores", "Fondo", "Pantalla" })
                {
                    Control b = Boton(dlg, pag); if (b == null) { Mal("falta la pagina " + pag); continue; }
                    Click(b); Mira(nombre + ", " + pag);
                    if (pag == "Sonido")
                    {   // La hoja del local y sus bocinas (12-10-2026): se abre con su boton y se vuelve con el suyo.
                        Control abre = Boton(dlg, "El local y sus bocinas...");
                        Check(abre != null, "Sonido tiene el boton de la hoja del local");
                        if (abre != null)
                        {
                            Click(abre); Mira(nombre + ", Sonido > El local y sus bocinas");
                            // La segunda hoja del grupo (22-10-2026): graves y agudos del local y la presencia en sala ruidosa.
                            Control abreT = Boton(dlg, "Tono y presencia del local...");
                            Check(abreT != null, "la hoja del local tiene el boton de la hoja de tono y presencia");
                            if (abreT != null)
                            {
                                Click(abreT); Mira(nombre + ", Sonido > El local y sus bocinas > Tono y presencia");
                                int swT = 0;
                                foreach (string nomSw in new string[] { "SonidoTonoGraves", "SonidoTonoAgudos" })
                                { Control[] c = dlg.Controls.Find(nomSw, true); if (c.Length == 1 && c[0].Visible) swT++; }
                                Check(swT == 2, "la hoja de tono tiene sus dos deslizadores a la vista (" + swT + ")");
                                Check(Boton(dlg, "Solo izquierda") == null, "y la hoja del local queda escondida mientras tanto");
                                Control vt = Boton(dlg, "Volver al local y sus bocinas");
                                Check(vt != null, "la hoja de tono y presencia tiene su boton de volver");
                                if (vt != null) { Click(vt); Check(Boton(dlg, "Solo izquierda") != null && Boton(dlg, "Tono y presencia del local...") != null, "al volver se ve otra vez la hoja del local"); }
                            }
                            Control vuelve = Boton(dlg, "Volver a las canciones");
                            Check(vuelve != null && Boton(dlg, "Solo izquierda") != null && Boton(dlg, "Polaridad") != null, "la hoja del local tiene la prueba de bocinas y su boton de volver");
                            if (vuelve != null) { Click(vuelve); Check(Boton(dlg, "El local y sus bocinas...") != null, "al volver se ven otra vez las canciones"); }
                        }
                        // La hoja de las mejoras con IA (13-10-2026: I1, I2 e I3), igual: su boton, sus tres interruptores y volver.
                        Control abreIa = Boton(dlg, "Mejoras con IA...");
                        Check(abreIa != null, "Sonido tiene el boton de la hoja de las mejoras con IA");
                        if (abreIa != null)
                        {
                            Click(abreIa); Mira(nombre + ", Sonido > Mejoras con IA");
                            int sw = 0;
                            foreach (string nomSw in new string[] { "SonidoIaMedida", "SonidoIaSiseo", "SonidoIaGenero" })
                            { Control[] c = dlg.Controls.Find(nomSw, true); if (c.Length == 1 && c[0].Visible) sw++; }
                            Check(sw == 3, "la hoja de las mejoras con IA tiene sus tres interruptores a la vista (" + sw + ")");
                            Control vuelveIa = Boton(dlg, "Volver a las canciones");
                            Check(vuelveIa != null, "la hoja de las mejoras con IA tiene su boton de volver");
                            if (vuelveIa != null) { Click(vuelveIa); Check(Boton(dlg, "Mejoras con IA...") != null, "al volver de la IA se ven otra vez las canciones"); }
                        }
                    }
                    if (pag == "Sonido")
                    {   // Lo que suena ahora (13-10-2026: S1 a S5): su hoja, sus tres interruptores, el boton sin mejoras y volver.
                        Control abreA = Boton(dlg, "Lo que suena ahora...");
                        Check(abreA != null, "Sonido tiene el boton de lo que suena ahora");
                        if (abreA != null)
                        {
                            Click(abreA); Mira(nombre + ", Sonido > Lo que suena ahora");
                            int swA = 0;
                            foreach (string nomSw in new string[] { "SonidoCanalMudo", "SonidoContinua", "SonidoEstereoAuto" })
                            { Control[] c = dlg.Controls.Find(nomSw, true); if (c.Length == 1 && c[0].Visible) swA++; }
                            Check(swA == 3, "la hoja de lo que suena tiene sus tres interruptores a la vista (" + swA + ")");
                            Control sinM = Boton(dlg, "Escuchar sin mejoras (15 s)");
                            Check(sinM != null, "y el boton de escuchar sin mejoras");
                            if (sinM != null)
                            {
                                Click(sinM); Mira(nombre + ", Sonido > Lo que suena ahora (sin mejoras)");
                                Control vol = null; foreach (Control c in dlg.Controls.Find("SonidoSinMejoras", true)) vol = c;
                                Check(vol != null && vol.Text.StartsWith("Volver a las mejoras"), "al pulsarlo dice como volver y cuanto queda");
                                if (vol != null) Click(vol);
                            }
                            Control vuelveA = Boton(dlg, "Volver a las canciones");
                            if (vuelveA != null) { Click(vuelveA); Check(Boton(dlg, "El local y sus bocinas...") != null, "al volver de lo que suena se ven otra vez las canciones"); }
                        }
                    }
                    if (pag == "Imagen")
                    {   // Las tres hojas de la tarjeta de efectos (Efectos, Fluidez y Performance). Hasta el 19-10-2026 nadie miraba Fluidez ni
                        // Performance (su pestana no es hija directa de la pagina), y esta ultima se salia de la ventana.
                        foreach (string hoja in new string[] { "Fluidez", "Performance" })
                        {
                            Control bt = Boton(dlg, hoja);
                            Check(bt != null, "Imagen tiene su hoja " + hoja);
                            if (bt == null) continue;
                            Click(bt); Mira(nombre + ", " + pag + " > " + hoja);
                            if (hoja != "Performance") continue;
                            // Performance (19-10-2026): sus ahorros van en hojitas por tema; se mira cada una.
                            foreach (string sub in new string[] { "Aurora", "Estrella", "Estado", "Grano", "Cielo" })
                            {
                                Control st = Boton(dlg, sub);
                                Check(st != null, "Performance tiene su hojita " + sub);
                                if (st != null) { Click(st); Mira(nombre + ", " + pag + " > Performance > " + sub); }
                            }
                        }
                        Control efe = Boton(dlg, "Efectos"); if (efe != null) Click(efe);
                    }
                    foreach (Control pg in dlg.Controls) if (pg is Panel && pg.Visible && pg.Width >= 900)
                    {
                        List<Control> hojas = new List<Control>();
                        foreach (Control c in pg.Controls) if (c.GetType().Name == "BotonPlano" && c.Top <= 40 && c.Height <= 36) hojas.Add(c);
                        hojas.Sort(delegate (Control x, Control y) { return x.Left.CompareTo(y.Left); });
                        for (int i = 1; i < hojas.Count; i++) { Click(hojas[i]); Mira(nombre + ", " + pag + " > " + hojas[i].Text); }
                        if (hojas.Count > 0) Click(hojas[0]);
                    }
                }
                vueltas++;
                dlg.Close();
            };
            t.Start();
            form.GetMethod("VentanaAjustes", T).Invoke(f, null);
            t.Stop(); t.Dispose();
        }
        Check(vueltas >= 1, "la ventana se abrio y se recorrio");
        Console.WriteLine(mal == 0 ? "  OK ventanaf3: " + ok + " comprobaciones en " + vueltas + " vueltas (6 paginas y sus hojas; letras de Linux, no las de XP)"
                                   : "  FALLO ventanaf3: " + mal + " problemas");
        return mal;
    }
    static void Check(bool c, string s) { if (c) ok++; else Mal(s); }
}

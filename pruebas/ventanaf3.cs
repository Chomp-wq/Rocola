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
                    {   // LA PAGINA DE SONIDO, SOLO LO ESENCIAL (27-10-2026): el modo billar con su boton de ajustes al lado y la prueba de bocinas
                        // a la vista; lo demas en la hoja de ajustes del sonido, y de ahi a sus hojas, que vuelven a ella.
                        Check(dlg.Controls.Find("SonidoLocalAuto", true).Length == 1 && dlg.Controls.Find("SonidoLocalAuto", true)[0].Visible, "Sonido tiene el interruptor del modo billar a la vista");
                        Check(Boton(dlg, "Solo izquierda") != null && Boton(dlg, "Polaridad") != null, "y la prueba de bocinas");
                        Check(Boton(dlg, "El local y sus bocinas...") == null, "las hojas no se ven sin abrir los ajustes del sonido");
                        Control aju = Boton(dlg, "Ajustes del sonido...");
                        Check(aju != null, "Sonido tiene el boton de los ajustes del sonido junto al modo billar");
                        if (aju != null)
                        {
                            Click(aju); Mira(nombre + ", Sonido > Ajustes del sonido");
                            Check(Boton(dlg, "Solo izquierda") == null, "la pagina queda escondida mientras tanto");
                            Control abre = Boton(dlg, "El local y sus bocinas...");
                            Check(abre != null, "los ajustes tienen el boton de la hoja del local");
                            if (abre != null)
                            {
                                Click(abre); Mira(nombre + ", Sonido > El local y sus bocinas");
                                Control abreT = Boton(dlg, "Tono y presencia del local...");
                                Check(abreT != null, "la hoja del local tiene el boton de la hoja de tono y presencia");
                                if (abreT != null)
                                {
                                    Click(abreT); Mira(nombre + ", Sonido > El local y sus bocinas > Tono y presencia");
                                    int swT = 0;
                                    foreach (string nomSw in new string[] { "SonidoTonoGraves", "SonidoTonoAgudos", "SonidoPresencia", "SonidoEses" })
                                    { Control[] c = dlg.Controls.Find(nomSw, true); if (c.Length == 1 && c[0].Visible) swT++; }
                                    Check(swT == 4, "la hoja de tono y presencia tiene sus dos deslizadores y sus dos interruptores a la vista (" + swT + ")");
                                    Check(Boton(dlg, "Como la cancion") == null, "y la hoja del local queda escondida mientras tanto");
                                    Control vt = Boton(dlg, "Volver al local y sus bocinas");
                                    Check(vt != null, "la hoja de tono y presencia tiene su boton de volver");
                                    if (vt != null) { Click(vt); Check(Boton(dlg, "Como la cancion") != null && Boton(dlg, "Tono y presencia del local...") != null, "al volver se ve otra vez la hoja del local"); }
                                }
                                Control vuelve = Boton(dlg, "Volver a los ajustes del sonido");
                                Check(vuelve != null && Boton(dlg, "Como la cancion") != null, "la hoja del local tiene la frecuencia de la tarjeta y su boton de volver");
                                if (vuelve != null) { Click(vuelve); Check(Boton(dlg, "El local y sus bocinas...") != null, "al volver se ven otra vez los ajustes del sonido"); }
                            }
                            Control abreV = Boton(dlg, "Voz, golpe y retumbe...");
                            Check(abreV != null, "los ajustes tienen el boton de la hoja de voz, golpe y retumbe");
                            if (abreV != null)
                            {
                                Click(abreV); Mira(nombre + ", Sonido > Voz, golpe y retumbe");
                                int swV = 0;
                                foreach (string nomSw in new string[] { "SonidoRetumbe", "SonidoAtaque", "SonidoBombo" })
                                { Control[] c = dlg.Controls.Find(nomSw, true); if (c.Length == 1 && c[0].Visible) swV++; }
                                Check(swV == 3, "la hoja de voz, golpe y retumbe tiene sus tres interruptores a la vista (" + swV + ")");
                                Control vv = Boton(dlg, "Volver a los ajustes del sonido");
                                if (vv != null) { Click(vv); Check(Boton(dlg, "Voz, golpe y retumbe...") != null, "al volver de la voz se ven otra vez los ajustes"); }
                            }
                            // EL ECUALIZADOR DEL DUENO (17-11-2026): su boton, su hoja con diez deslizadores, lo que hacen y como se vuelve.
                            Control abreEq = Boton(dlg, "Ecualizador...");
                            Check(abreEq != null, "los ajustes tienen el boton de la hoja del ecualizador");
                            if (abreEq != null)
                            {
                                Click(abreEq); Mira(nombre + ", Sonido > Ecualizador");
                                int nBar = 0; Control[] barras = new Control[10];
                                for (int i = 0; i < 10; i++) { Control[] c = dlg.Controls.Find("SonidoEqBanda" + i, true); if (c.Length == 1 && c[0].Visible) { barras[i] = c[0]; nBar++; } }
                                Check(nBar == 10, "la hoja del ecualizador tiene sus diez deslizadores a la vista (" + nBar + ")");
                                Check(dlg.Controls.Find("SonidoEq", true).Length == 1 && dlg.Controls.Find("SonidoEqCurva", true).Length == 1 && dlg.Controls.Find("SonidoEqCurva", true)[0].Visible, "y el dibujo de la curva");
                                Check(Boton(dlg, "Ecualizador...") == null && Boton(dlg, "Voz, golpe y retumbe...") == null, "la hoja de los ajustes queda escondida mientras tanto");
                                // cada deslizador y cada tarjeta de la hoja caben en su tarjeta (el barrido de arriba mira fuera de la ventana, aqui dentro de la tarjeta)
                                bool dentro = true;
                                foreach (Control tj in dlg.Controls.Find("SonidoEq", true)[0].Controls)
                                    foreach (Control hijo in tj.Controls) if (hijo.Left < 0 || hijo.Top < 0 || hijo.Right > tj.Width || hijo.Bottom > tj.Height) { dentro = false; Mal("fuera de su tarjeta: " + Ruta(hijo) + " «" + Corto(hijo.Text) + "»"); }
                                Check(dentro, "nada de la hoja del ecualizador se sale de su tarjeta");
                                FieldInfo fLaV = form.GetField("localAuto", T); int[] eqN = (int[])form.GetField("eqDuenoNormal", T).GetValue(null), eqB = (int[])form.GetField("eqDuenoBillar", T).GetValue(null);
                                bool billarAntes = (bool)fLaV.GetValue(null); fLaV.SetValue(null, false);
                                Array.Clear(eqN, 0, 10); Array.Clear(eqB, 0, 10);
                                Click(Boton(dlg, "Todo en 0"));
                                if (barras[3] != null)
                                {   // una flecha mueve 0,5 dB (como los demas deslizadores): sube la banda de 250 Hz en la memoria NORMAL
                                    barras[3].Focus();
                                    typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[3], new object[] { new KeyEventArgs(Keys.Right) });
                                    Check(eqN[3] == 1 && eqB[3] == 0, "una flecha sube la banda 0,5 dB en la memoria normal y no toca la del billar (" + eqN[3] + ", " + eqB[3] + ")");
                                    typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[3], new object[] { new KeyEventArgs(Keys.Right) });
                                    typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[3], new object[] { new KeyEventArgs(Keys.Right) });
                                    Control[] vals = dlg.Controls.Find("SonidoEqMargen", true);
                                    Check(eqN[3] == 3 && vals.Length == 1 && vals[0].Text.StartsWith("Todo baja ") && vals[0].Text.Contains(","), "tres flechas son 1,5 dB y el margen dice cuanto baja el todo (" + (vals.Length == 1 ? Corto(vals[0].Text) : "?") + ")");
                                    Mira(nombre + ", Sonido > Ecualizador (con una banda subida)");
                                    string capEq = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_EQ");   // opcional: una foto de la hoja (solo con la primera letra)
                                    if (!string.IsNullOrEmpty(capEq) && letra == null)
                                    {
                                        Control sub = dlg.Controls.Find("SonidoEqBanda5", true)[0];
                                        for (int k = 0; k < 6; k++) typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[1], new object[] { new KeyEventArgs(Keys.Right) });
                                        for (int k = 0; k < 8; k++) typeof(Control).GetMethod("OnKeyDown", T).Invoke(sub, new object[] { new KeyEventArgs(Keys.Left) });
                                        dlg.Refresh(); Application.DoEvents();
                                        using (Bitmap bm = new Bitmap(dlg.ClientSize.Width, dlg.ClientSize.Height)) { using (Graphics gc = Graphics.FromImage(bm)) gc.CopyFromScreen(dlg.PointToScreen(Point.Empty), Point.Empty, bm.Size); bm.Save(capEq); }
                                        for (int k = 0; k < 6; k++) typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[1], new object[] { new KeyEventArgs(Keys.Left) });
                                        for (int k = 0; k < 8; k++) typeof(Control).GetMethod("OnKeyDown", T).Invoke(sub, new object[] { new KeyEventArgs(Keys.Right) });
                                    }
                                    Control todo0 = null; foreach (Control c in dlg.Controls.Find("SonidoEqCero", true)) todo0 = c;
                                    if (todo0 != null) { Click(todo0); Check(eqN[3] == 0 && vals[0].Text.StartsWith("Margen: nada"), "«Todo en 0» deja la memoria en uso plana"); }
                                    // E1: ESCUCHAR SIN EL ECUALIZADOR (17-11-2026): un toque lo quita sin tocar las memorias, otro lo pone, y todo lo demas lo pone solo
                                    Control sinEq = null; foreach (Control c in dlg.Controls.Find("SonidoEqSin", true)) sinEq = c;
                                    Check(sinEq != null && sinEq.Visible && todo0 != null && sinEq.Text == "Sin ecualizador" && sinEq.AccessibleName == "Escuchar sin el ecualizador"
                                          && sinEq.Bounds.Width == 204 && sinEq.Bounds.Height == 34 && todo0.Bounds.Width == 204 && sinEq.Left >= todo0.Right, "la hoja tiene «Todo en 0» y «Sin ecualizador» lado a lado");
                                    if (sinEq != null)
                                    {
                                        FieldInfo fSin = form.GetField("eqDuenoSinHasta", T); MethodInfo puestoM = form.GetMethod("EqDuenoPuesto", T);
                                        Func<bool> ceros = delegate { foreach (int v in (int[])puestoM.Invoke(f, null)) if (v != 0) return false; return true; };
                                        eqN[3] = 6;
                                        Click(sinEq);
                                        Check((int)fSin.GetValue(f) != 0 && ceros() && eqN[3] == 6 && sinEq.Text.StartsWith("Ponerlo (") && sinEq.Text.EndsWith(" s)"), "un toque lo quita: pide ceros a la cadena, no toca las memorias y el boton dice «" + sinEq.Text + "»");
                                        Mira(nombre + ", Sonido > Ecualizador (quitado)");
                                        Control usaS = dlg.Controls.Find("SonidoEqUsando", true)[0];
                                        Console.WriteLine("    «" + usaS.Text + "»");
                                        Click(sinEq);
                                        Check((int)fSin.GetValue(f) == 0 && !ceros() && ((int[])puestoM.Invoke(f, null))[3] == 6 && sinEq.Text == "Sin ecualizador", "otro toque lo vuelve a poner");
                                        Click(sinEq); typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[3], new object[] { new KeyEventArgs(Keys.Right) });
                                        Check((int)fSin.GetValue(f) == 0 && eqN[3] == 7 && sinEq.Text == "Sin ecualizador", "mover un deslizador lo vuelve a poner");
                                        Click(sinEq); Click(Boton(dlg, "Todo en 0"));
                                        Check((int)fSin.GetValue(f) == 0 && eqN[3] == 0, "«Todo en 0» lo vuelve a poner");
                                        Click(sinEq); fSin.SetValue(f, unchecked(Environment.TickCount - 5) | 1);   // se le acabo el tiempo
                                        Check(!(bool)form.GetProperty("EqDuenoSinPuesto", T).GetValue(f, null) && (int)form.GetProperty("EqDuenoSinSegundos", T).GetValue(f, null) == 0, "a los 15 s ya no cuenta como quitado");
                                        eqN[3] = 4; fSin.SetValue(f, 0); Click(sinEq);
                                        Control vSin = Boton(dlg, "Volver a los ajustes del sonido"); if (vSin != null) Click(vSin);
                                        Check((int)fSin.GetValue(f) == 0 && eqN[3] == 4 && ceros() == false, "al volver de la hoja lo vuelve a poner");
                                        Click(Boton(dlg, "Ecualizador...")); eqN[3] = 0;
                                    }
                                    // con el modo billar guardado, la hoja lee y cambia la memoria del billar
                                    fLaV.SetValue(null, true); Control vEq = Boton(dlg, "Volver a los ajustes del sonido"); if (vEq != null) Click(vEq);
                                    Click(Boton(dlg, "Ecualizador..."));
                                    Control[] usa = dlg.Controls.Find("SonidoEqUsando", true);
                                    Check(usa.Length == 1 && usa[0].Text.Contains("billar") && !usa[0].Text.Contains("apagado"), "con el modo billar puesto, al volver a abrirla dice que usa la del billar");
                                    typeof(Control).GetMethod("OnKeyDown", T).Invoke(barras[3], new object[] { new KeyEventArgs(Keys.Left) });
                                    Check(eqB[3] == -1 && eqN[3] == 0, "y mover una banda cambia la del billar y no la normal");
                                }
                                // «Dejar como de fabrica» pone las dos en 0
                                eqN[1] = 5; eqB[8] = -7;
                                Control fab = Boton(dlg, "Dejar como de fabrica"); if (fab == null) foreach (Control c in dlg.Controls) if (c.GetType().Name == "BotonPlano" && c.Text == "Dejar como de fabrica") fab = c;
                                Check(fab != null, "la ventana tiene «Dejar como de fabrica»");
                                if (fab != null) { Click(fab); bool z = true; for (int i = 0; i < 10; i++) if (eqN[i] != 0 || eqB[i] != 0) z = false; Check(z, "«Dejar como de fabrica» pone las dos memorias del ecualizador en 0"); }
                                fLaV.SetValue(null, billarAntes);
                                Control vEq2 = Boton(dlg, "Volver a los ajustes del sonido");
                                Check(vEq2 != null, "la hoja del ecualizador tiene su boton de volver");
                                if (vEq2 != null) { Click(vEq2); Check(Boton(dlg, "Ecualizador...") != null && Boton(dlg, "Voz, golpe y retumbe...") != null, "al volver del ecualizador se ven otra vez los ajustes"); }
                            }
                            Control abreIa = Boton(dlg, "Mejoras con IA...");
                            Check(abreIa != null, "los ajustes tienen el boton de la hoja de las mejoras con IA");
                            if (abreIa != null)
                            {
                                Click(abreIa); Mira(nombre + ", Sonido > Mejoras con IA");
                                int sw = 0;
                                foreach (string nomSw in new string[] { "SonidoIaMedida", "SonidoIaSiseo", "SonidoIaGenero" })
                                { Control[] c = dlg.Controls.Find(nomSw, true); if (c.Length == 1 && c[0].Visible) sw++; }
                                Check(sw == 3, "la hoja de las mejoras con IA tiene sus tres interruptores a la vista (" + sw + ")");
                                Control vuelveIa = Boton(dlg, "Volver a los ajustes del sonido");
                                Check(vuelveIa != null, "la hoja de las mejoras con IA tiene su boton de volver");
                                if (vuelveIa != null) { Click(vuelveIa); Check(Boton(dlg, "Mejoras con IA...") != null, "al volver de la IA se ven otra vez los ajustes"); }
                            }
                            // Lo que suena ahora (13-10-2026: S1 a S5): su hoja, sus interruptores, el boton sin mejoras y volver.
                            Control abreA = Boton(dlg, "Lo que suena ahora...");
                            Check(abreA != null, "los ajustes tienen el boton de lo que suena ahora");
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
                                Control vuelveA = Boton(dlg, "Volver a los ajustes del sonido");
                                if (vuelveA != null) { Click(vuelveA); Check(Boton(dlg, "El local y sus bocinas...") != null, "al volver de lo que suena se ven otra vez los ajustes"); }
                            }
                            Control vAju = Boton(dlg, "Volver a las canciones");
                            Check(vAju != null, "los ajustes del sonido tienen su boton de volver");
                            if (vAju != null) { Click(vAju); Check(Boton(dlg, "Ajustes del sonido...") != null && Boton(dlg, "Solo izquierda") != null, "al volver se ve otra vez la pagina de Sonido"); }
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

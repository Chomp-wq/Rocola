using System;
using System.Drawing;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;

// LA FILA DE ARRIBA EN PANTALLAS BAJAS (aviso del 11-10-2026, con captura a 1024x768): "el icono de ayuda al lado del de
// busqueda tarda mucho en aparecer" y "optimiza los espacios para resoluciones bajas".
// Arma la interfaz de verdad (BuildUi + LayoutControls, sin encender la rocola ni la tarjeta) en varias resoluciones y
// comprueba: que la casilla de AYUDA se acomoda en cuanto se enciende la tarjeta (no al primer cambio de cancion); que
// instrucciones, BUSCAR, AYUDA y el encabezado van en orden, sin pisarse y dentro de la fila; que en 1024x768 la AYUDA
// lleva su palabra y el encabezado tiene lo que pide; que con sitio de sobra BUSCAR sigue en el centro; y que sin
// tarjeta (y en el perfil por CPU, congelado) la fila queda exactamente como antes. Las letras de Mono no son las de
// Windows: se miran el orden y lo que cabe, no pixeles exactos. No mide FPS ni dibuja con Direct3D.
class FilaBaja
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form; static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static object G(object o, string n) { FieldInfo f = form.GetField(n, T); return f != null ? f.GetValue(o) : null; }
    static void S(object o, string n, object v) { form.GetField(n, T).SetValue(o, v); }
    static object C(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in form.GetMethods(T))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + n);
    }
    static object Static(Type t, string n, params object[] a)
    {
        foreach (MethodInfo m in t.GetMethods(T))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + t.Name + "." + n);
    }
    // Visible "pedido" (el formulario no se muestra: Control.Visible diria false por el padre).
    static readonly FieldInfo esVisible = typeof(Control).GetField("is_visible", T);
    static bool Pedido(Control c) { return c != null && (bool)esVisible.GetValue(c); }

    static Form Arma()
    {
        object f = Codigo.FormSinEncender(form);
        DynamicMethod dm = new DynamicMethod("BaseFila", typeof(void), new Type[] { typeof(object) }, typeof(FilaBaja), true);
        ILGenerator il = dm.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, typeof(Form));
        il.Emit(OpCodes.Call, typeof(Form).GetConstructor(Type.EmptyTypes)); il.Emit(OpCodes.Ret);
        dm.Invoke(null, new object[] { f });
        C(f, "BuildUi");
        esVisible.SetValue(f, true);   // como si se viera, sin crear la ventana
        ((Form)f).MinimumSize = new Size(100, 100);   // Mono no baja de ~820 px de ancho si no se le dice
        return (Form)f;
    }

    static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rocola_filabaja_" + Guid.NewGuid().ToString("N")));
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        bool gpu = asm.GetType("Rocola.GpuPanel", false) != null;
        Type tBusca = form.GetNestedType("BuscaView", T), tAyuda = form.GetNestedType("AyudaView", T);
        Check(esVisible != null, "Mono deja leer la visibilidad pedida (is_visible)");

        if (gpu)
        {
            // La casilla tardaba en salir: el arranque acomodaba la pantalla ANTES de encender la tarjeta y nadie volvia a
            // acomodarla hasta el primer cambio de cancion o de creditos.
            Check(Codigo.Llama(form.GetMethod("ApplyAlbumGpu", T), "LayoutControls"), "Encender o apagar la tarjeta acomoda la fila en el acto");
            // Colocacion del par BUSCAR + AYUDA, pura.
            Check((int)Static(form, "ColocaParBusca", 1920, 258, 516, 328, 1906, 300) == (1920 - 516) / 2, "Con sitio de sobra, el par BUSCAR + AYUDA queda centrado en la pantalla (14-10-2026)");
            Check((int)Static(form, "ColocaParBusca", 1920, 258, 258, 328, 1906, 300) == (1920 - 258) / 2, "Sin AYUDA, BUSCAR sola en el centro");
            int x = (int)Static(form, "ColocaParBusca", 1024, 203, 407, 328, 1010, 255);
            Check(x >= 328 && x + 407 + 12 + 255 <= 1010, "En 1024, el par se corre lo justo para dejarle al encabezado lo que pide");
            Check((int)Static(form, "ColocaParBusca", 800, 203, 330, 328, 786, 255) == 328, "Si no hay sitio, el par se pega a las instrucciones (nunca encima)");
        }

        int[][] res = { new[] { 800, 600 }, new[] { 1024, 600 }, new[] { 1024, 768 }, new[] { 1280, 720 }, new[] { 1280, 1024 }, new[] { 1366, 768 }, new[] { 1920, 1080 } };
        using (Form f = Arma())
        {
            Control help = (Control)G(f, "helpBar"), busca = (Control)G(f, "buscaView"), ayuda = (Control)G(f, "ayudaView"), cab = (Control)G(f, "headerView");
            foreach (bool conTarjeta in gpu ? new bool[] { false, true } : new bool[] { false })
            {
                if (gpu) S(f, "gpuEnabled", conTarjeta);   // en el perfil por CPU ni existe
                foreach (int[] r in res)
                {
                    string n = r[0] + "x" + r[1] + (conTarjeta ? " con tarjeta" : " sin tarjeta") + ": ";
                    f.ClientSize = new Size(r[0], r[1]);
                    C(f, "LayoutControls");
                    int W = f.ClientSize.Width, pad = 14, barraDer = W - pad, filaH = busca.Height;
                    Check(W == r[0] && f.ClientSize.Height == r[1], n + "la ventana mide lo pedido");
                    bool justa = (bool)help.GetType().GetProperty("Justa", T).GetValue(help, null);
                    // Desde el 12-10-2026 hasta 1180 de ancho (antes < 1000): en 1024x768 son los 22 px que le faltaban a AYUDA.
                    Check(justa == (conTarjeta && W < 1180), n + (justa ? "instrucciones con menos aire" : "instrucciones como siempre"));
                    Check(help.Width == Math.Min((int)Static(help.GetType(), justa ? "AnchoJusta" : "Ancho", filaH), barraDer - pad), n + "las instrucciones miden lo que dibujan");
                    int minX = help.Right + 12;
                    Check(Pedido(busca) && busca.Left >= minX && busca.Right <= barraDer, n + "BUSCAR, detras de las instrucciones y dentro de la fila");
                    if (!conTarjeta)
                    {
                        // Exactamente como antes: ancho de siempre, centrada, y sin AYUDA (solo existe con tarjeta).
                        int w0 = Math.Min((int)Static(tBusca, "Ancho", filaH), Math.Max(0, barraDer - minX));
                        Check(busca.Width == w0 && busca.Left == (int)Static(form, "SitioBusca", W, w0, minX, barraDer), n + "BUSCAR como siempre");
                        Check(ayuda == null || !Pedido(ayuda), n + "sin casilla de AYUDA");
                        Check(cab.Left == busca.Right + 12 && cab.Right == barraDer, n + "el encabezado, detras de BUSCAR");
                        continue;
                    }
                    Check(busca.Width == (int)Static(tBusca, "AnchoJusto", filaH) && busca.Width <= (int)Static(tBusca, "Ancho", filaH), n + "BUSCAR mide lo que dibuja");
                    Check(Pedido(ayuda), n + "la casilla de AYUDA se ve");
                    bool compacta = (bool)ayuda.GetType().GetField("Compacta", T).GetValue(ayuda);
                    Check(ayuda.Width == (int)Static(tAyuda, "Ancho", filaH, compacta), n + "AYUDA mide lo que dibuja");
                    Check(ayuda.Left == busca.Right + 10 && ayuda.Top == busca.Top && ayuda.Height == busca.Height, n + "AYUDA, pegada a BUSCAR y a su altura");
                    Check(cab.Left == ayuda.Right + 12 && cab.Right == barraDer, n + "el encabezado, detras de AYUDA y hasta el borde");
                    int pide = (int)cab.GetType().GetMethod("AnchoPedido", T).Invoke(cab, new object[] { cab.Height });
                    if (W >= 1024)
                    {
                        Check(!compacta, n + "AYUDA con su palabra");
                        Check(cab.Width >= pide, n + "el encabezado tiene lo que pide (" + cab.Width + " de " + pide + " px)");
                    }
                    else Check(cab.Width >= 90, n + "al encabezado le quedan al menos 90 px");
                    // El par BUSCAR + AYUDA, centrado en la pantalla (14-10-2026: antes se centraba BUSCAR sola).
                    if (W >= 1920) Check(Math.Abs((busca.Left + ayuda.Right) / 2 - W / 2) <= 1, n + "BUSCAR y AYUDA juntas en el centro de la pantalla");
                    if (W == 1024 && r[1] == 768)
                        Console.WriteLine("     1024x768: instrucciones " + help.Left + ".." + help.Right + ", BUSCAR " + busca.Left + ".." + busca.Right
                            + ", AYUDA " + ayuda.Left + ".." + ayuda.Right + ", encabezado " + cab.Left + ".." + cab.Right + " (pide " + pide + ")");
                }
            }
            if (gpu)
            {
                // CON VIDEO SONANDO (revision del 12-10-2026): antes BUSCAR y AYUDA se escondian mientras durara el video, que
                // en el bar es casi siempre, y la ayuda "dejaba de estar" al rato. Ahora van encima del cuadro del video.
                Check((int)Static(form, "ParSobreVideo", 512, 1010, 203, 194) == 512 + (498 - 407) / 2, "El par, centrado sobre el video");
                Check((int)Static(form, "ParSobreVideo", 400, 786, 203, 0) == 400 + (386 - 203) / 2, "Solo BUSCAR, centrada sobre el video");
                Check((int)Static(form, "ParSobreVideo", 400, 500, 203, 194) == 400, "Si no cabe, empieza en el borde del video (nunca a su izquierda)");
                S(f, "videoOn", true);
                Control letras = (Control)G(f, "letterBar");   // el video empieza a la altura de las letras
                foreach (int[] r in res)
                {
                    string n = r[0] + "x" + r[1] + " con video: ";
                    f.ClientSize = new Size(r[0], r[1]); C(f, "LayoutControls");
                    int W = f.ClientSize.Width, splitX = (int)(W * 0.50), contentRight = splitX - 12;
                    Check(Pedido(busca) && Pedido(ayuda), n + "BUSCAR y AYUDA se ven");
                    Check(busca.Left >= splitX && ayuda.Left == busca.Right + 10 && ayuda.Right <= W - 14, n + "encima del video, dentro de la pantalla");
                    Check(Math.Abs((busca.Left + ayuda.Right) / 2 - (splitX + W - 14) / 2) <= 1, n + "centradas sobre el video");
                    Check(busca.Top == 8 && ayuda.Top == 8 && busca.Height == help.Height, n + "en la fila de arriba, a su altura");
                    Check(busca.Bottom <= letras.Top && ayuda.Bottom <= letras.Top, n + "sin pisar el cuadro del video (empieza con las letras)");
                    Check(cab.Left == help.Right + 12 && cab.Right == contentRight, n + "el encabezado, en la mitad izquierda tras las instrucciones");
                    if (W >= 1024) Check(!(bool)ayuda.GetType().GetField("Compacta", T).GetValue(ayuda), n + "AYUDA con su palabra");
                }
                f.ClientSize = new Size(1024, 768);
                S(f, "gpuEnabled", false); C(f, "LayoutControls");
                Check(!Pedido(busca) && !Pedido(ayuda) && cab.Left == help.Right + 12, "Sin tarjeta, con video, como siempre: sin BUSCAR ni AYUDA en la fila");
                S(f, "gpuEnabled", true);
                S(f, "videoOn", false);
                // Apagar la tarjeta en marcha devuelve la fila de siempre.
                S(f, "gpuEnabled", false); C(f, "LayoutControls");
                Check(!Pedido(ayuda) && busca.Width == Math.Min((int)Static(tBusca, "Ancho", busca.Height), 1010 - (help.Right + 12)), "Sin tarjeta otra vez: BUSCAR vuelve a su ancho de siempre");
            }
        }
        Console.WriteLine(mal == 0 ? "  OK filabaja: " + ok + " comprobaciones (" + (gpu ? "GPU" : "CPU") + "; letras de Mono, sin Direct3D ni FPS)"
                                   : "  FALLO filabaja: " + mal + " de " + (ok + mal));
        return mal;
    }
}

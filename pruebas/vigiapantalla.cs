using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// EL VIGIA DE LA PANTALLA (MedidasBitacora.cs: VigiaPantalla; Form1: ArrancaVigia, ApuntaParada).
//
// Cuanto tarda la pantalla en atender, medido siempre y dentro de la propia rocola. Las paradas de mas de 150 ms van a
// rocola_diagnostico.txt (con "diagnostico=1") diciendo que estaba haciendo la pantalla; las de mas de 500 ms, siempre a
// rocola_error.txt. Aqui:
//   1) con una ventana de verdad parada a proposito, el vigia dice cuanto duro y que la paro; lo corto no lo dice;
//   2) si la paro algo sin marca, dice que fue DESPUES de lo ultimo marcado; y si lleva diez segundos, lo dice sin esperar;
//   3) la rocola lo apunta donde toca: 150 ms a la bitacora, 500 ms a los errores (sin inundarlos);
//   4) el vigia se arranca al mostrarse, se para al cerrar, y las entradas grandes marcan lo que hacen;
//   5) cuanto cuesta marcar (medida, no criterio).
class VigiaPantallaPrueba
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TV;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static string Lee(string p) { try { return File.Exists(p) ? File.ReadAllText(p) : ""; } catch { return ""; } }
    static int Cuenta(string t, string que) { int n = 0, i = 0; while ((i = t.IndexOf(que, i, StringComparison.Ordinal)) >= 0) { n++; i += que.Length; } return n; }

    // Lo primero que hace (entre las primeras llamadas: la marca de una tecla arma antes su texto) es marcar.
    static bool EmpiezaMarcando(List<string> n) { for (int i = 0; i < n.Count && i < 4; i++) if (n[i].EndsWith("VigiaPantalla.Hace")) return true; return false; }
    static void Marca(string que) { TV.GetMethod("Hace", T).Invoke(null, new object[] { que }); }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TV = asm.GetType("Rocola.VigiaPantalla", false);
        Check(TV != null, "la rocola trae su vigia de la pantalla");
        if (TV == null) { Console.WriteLine("\n  FALLOS: " + fallos); return 1; }

        Console.WriteLine("\n  1) CON LA PANTALLA PARADA A PROPOSITO\n");
        // Una ventana de verdad, con su propio hilo y su bucle de mensajes, como la de la rocola.
        Form ventana = null; ManualResetEvent lista = new ManualResetEvent(false);
        Thread ui = new Thread(delegate ()
        {
            ventana = new Form(); ventana.ShowInTaskbar = false; ventana.Size = new System.Drawing.Size(200, 100);
            ventana.Shown += delegate { lista.Set(); };
            Application.Run(ventana);
        });
        ui.SetApartmentState(ApartmentState.STA); ui.IsBackground = true; ui.Start();
        Check(lista.WaitOne(10000, false), "la ventana de prueba esta en pantalla");
        List<KeyValuePair<int, string>> avisos = new List<KeyValuePair<int, string>>();
        object cer = new object();
        Action<int, string> avisa = delegate (int ms, string que) { lock (cer) avisos.Add(new KeyValuePair<int, string>(ms, que)); };
        object vigia = Activator.CreateInstance(TV, T, null, new object[] { ventana, 150, 20, avisa }, null);
        TV.GetMethod("Arranca", T).Invoke(vigia, null);
        Thread.Sleep(300);
        lock (cer) Check(avisos.Count == 0, "con la pantalla libre no hay nada que decir (" + avisos.Count + ")");

        ventana.BeginInvoke((MethodInvoker)delegate { Marca("prueba: una parada corta"); Thread.Sleep(80); });
        Thread.Sleep(400);
        lock (cer) Check(avisos.Count == 0, "una parada de 80 ms no se dice (el umbral es 150)");

        ventana.BeginInvoke((MethodInvoker)delegate { Marca("prueba: la parada larga"); Thread.Sleep(420); });
        Thread.Sleep(900);
        KeyValuePair<int, string> a1 = new KeyValuePair<int, string>(-1, "");
        lock (cer) { if (avisos.Count > 0) a1 = avisos[0]; }
        Console.WriteLine("        dijo: " + a1.Key + " ms, " + a1.Value);
        Check(avisos.Count == 1 && a1.Key >= 380 && a1.Key < 900, "una parada de 420 ms se dice una vez, con lo que duro");
        Check(a1.Value.StartsWith("en: prueba: la parada larga"), "y con lo que la paro");

        Console.WriteLine("\n  2) LO QUE NO ESTA MARCADO, Y LO QUE NO VUELVE\n");
        ventana.BeginInvoke((MethodInvoker)delegate { Marca("prueba: algo ya terminado"); });
        Thread.Sleep(600);
        ventana.BeginInvoke((MethodInvoker)delegate { Thread.Sleep(300); });   // sin marca
        Thread.Sleep(800);
        KeyValuePair<int, string> a2 = new KeyValuePair<int, string>(-1, "");
        lock (cer) { if (avisos.Count > 1) a2 = avisos[1]; }
        Console.WriteLine("        dijo: " + a2.Key + " ms, " + a2.Value);
        Check(a2.Value.StartsWith("despues de: prueba: algo ya terminado"), "una parada sin marca se dice como DESPUES de lo ultimo marcado");
        Check((int)TV.GetField("SigueParadaMs", T).GetValue(null) == 10000
              && Codigo.Textos(TV.GetMethod("Bucle", T)).Contains(" (y sigue parada)"), "si la pantalla no vuelve en diez segundos, se dice sin esperar a que vuelva");
        TV.GetMethod("Para", T).Invoke(vigia, null);
        Thread.Sleep(100);
        int antes; lock (cer) antes = avisos.Count;
        ventana.BeginInvoke((MethodInvoker)delegate { Thread.Sleep(300); });
        Thread.Sleep(600);
        lock (cer) Check(avisos.Count == antes, "parado el vigia, ya no dice nada");
        try { ventana.BeginInvoke((MethodInvoker)delegate { ventana.Close(); }); } catch { }

        Console.WriteLine("\n  3) LA ROCOLA LO APUNTA DONDE TOCA\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_vigiapantalla_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            string diag = Path.Combine(dir, "rocola_diagnostico.txt"), err = Path.Combine(dir, "rocola_error.txt");
            object f = Codigo.FormSinEncender(TF);
            MethodInfo apunta = TF.GetMethod("ApuntaParada", T);
            Set(f, "diagOn", true);
            apunta.Invoke(f, new object[] { 120, "en: nada grave" });
            apunta.Invoke(f, new object[] { 210, "en: una tecla (Right)" });
            Codigo.EsperaBitacora();
            Check(!Lee(diag).Contains("nada grave") && Lee(diag).Contains("pantalla parada 210 ms, en: una tecla (Right)") && !File.Exists(err),
                  "150 ms o mas: a la bitacora del diagnostico (y lo de menos, a ningun sitio)");
            apunta.Invoke(f, new object[] { 740, "en: publicar la biblioteca" });
            Codigo.EsperaBitacora();
            Check(Lee(err).Contains("[Pantalla parada 740 ms, en: publicar la biblioteca]") && Lee(diag).Contains("pantalla parada 740 ms"),
                  "500 ms o mas: tambien a los errores");
            apunta.Invoke(f, new object[] { 600, "en: otra" });
            apunta.Invoke(f, new object[] { 650, "en: y otra" });
            Codigo.EsperaBitacora();
            Check(Cuenta(Lee(err), "[Pantalla parada") == 1, "sin inundar los errores: como mucho una cada diez segundos");
            Set(f, "paradaErrorTick", unchecked(Environment.TickCount - 11000));
            apunta.Invoke(f, new object[] { 900, "en: la siguiente" });
            Codigo.EsperaBitacora();
            Check(Lee(err).Contains("[Pantalla parada 900 ms, en: la siguiente (y 2 mas de 500 ms desde el apunte anterior)]"),
                  "y la siguiente dice cuantas se callaron");
            object g = Codigo.FormSinEncender(TF);
            Set(g, "diagOn", false);
            File.Delete(diag); File.Delete(err);
            TF.GetMethod("ApuntaParada", T).Invoke(g, new object[] { 800, "en: sin diagnostico" });
            Codigo.EsperaBitacora();
            Check(!File.Exists(diag) && Lee(err).Contains("sin diagnostico"), "sin diagnostico=1, lo de 500 ms va igual a los errores");
            Check((int)TF.GetField("ParadaDiagMs", T).GetValue(null) == 150 && (int)TF.GetField("ParadaErrorMs", T).GetValue(null) == 500,
                  "los umbrales: 150 ms y 500 ms");
        }
        finally { carpeta.SetValue(null, previa); try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine("\n  4) SIEMPRE EN MARCHA, Y LAS ENTRADAS MARCAN LO QUE HACEN\n");
        Check(Codigo.Llama(TF.GetMethod("OnShown", T), "ArrancaVigia") && Codigo.Llama(TF.GetMethod("ArrancaVigia", T), "Arranca"),
              "se arranca al mostrarse la ventana, sin interruptor");
        Check(Codigo.Llama(TF.GetMethod("OnFormClosing", T), "Para"), "y se para al cerrar (cerrar tarda lo que tenga que tardar)");
        string[] entradas = { "AnimTimer_Tick", "ProcessCmdKey", "ProgressTimer_Tick", "PlayNext", "StartOpenedTrack", "Scanner_RunWorkerCompleted", "SigueArranque", "OnShown" };
        List<string> sinMarca = new List<string>();
        foreach (string e in entradas)
        {
            MethodInfo m = TF.GetMethod(e, T);
            List<string> n = m == null ? new List<string>() : Codigo.Llamadas(m);
            if (!EmpiezaMarcando(n)) sinMarca.Add(e);
        }
        Type gp = asm.GetType("Rocola.GpuPanel", false);
        if (gp != null) { MethodInfo r = gp.GetMethod("RenderFrame", T); List<string> n = r == null ? new List<string>() : Codigo.Llamadas(r); if (!EmpiezaMarcando(n)) sinMarca.Add("GpuPanel.RenderFrame"); }
        else Console.WriteLine("        (sin tarjeta en este perfil: no hay cuadro que marcar)");
        Check(sinMarca.Count == 0, "las entradas grandes marcan lo que hacen, lo primero de todo" + (sinMarca.Count > 0 ? " (faltan: " + string.Join(", ", sinMarca.ToArray()) + ")" : ""));

        Console.WriteLine("\n  5) LO QUE CUESTA MARCAR (medida, no criterio)\n");
        MethodInfo hace = TV.GetMethod("Hace", T);
        Action<string> directo = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), hace);
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000000; i++) directo("un cuadro de la tarjeta");
        sw.Stop();
        Console.WriteLine("        un millon de marcas: " + sw.ElapsedMilliseconds + " ms (" + (sw.Elapsed.TotalMilliseconds * 1e6 / 1000000).ToString("0") + " ns cada una)");
        Check(sw.ElapsedMilliseconds < 2000, "marcar es casi gratis");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

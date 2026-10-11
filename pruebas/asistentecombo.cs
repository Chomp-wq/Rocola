using System;
using System.Drawing;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;

// EL CONTROL DE LA AYUDA Y LA GUIA PASO A PASO (11-10-2026).
// La ayuda se abre con ROJO + flecha ABAJO (la gemela de BUSCAR, que es ROJO + ARRIBA) o tocando su casilla, y
// lleva de la mano: mira el estado real de la Rocola y decide el paso que toca. Esta bateria comprueba el atajo,
// que ya no queda el acorde rojo+verde de tres segundos (ni su ventana de 120 ms que retrasaba los botones), la
// tabla de pasos, el "paso X de N", que una mision avanza sola con lo que hace el cliente, que celebra al
// lograrlo y que se vuelve a guardar. No abre la Rocola ni simula teclas de Windows; no mide FPS.
class AsistenteCombo
{
    const BindingFlags Todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static Type form;

    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    static object Run(string name, params object[] args)
    {
        foreach (MethodInfo m in form.GetMethods(Todos))
            if (m.Name == name && m.GetParameters().Length == args.Length)
                try { return m.Invoke(null, args); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("Falta " + name);
    }
    static int K(string name) { return (int)form.GetField(name, Todos).GetValue(null); }
    static object Get(object o, string n) { return form.GetField(n, Todos).GetValue(o); }
    static void Set(object o, string n, object v) { form.GetField(n, Todos).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in form.GetMethods(Todos))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("Falta " + n);
    }
    static object Owner(Size size)
    {
        object owner = Codigo.FormSinEncender(form);
        DynamicMethod m = new DynamicMethod("BaseAisladaCombo", typeof(void), new Type[] { typeof(object) }, typeof(AsistenteCombo), true);
        ILGenerator code = m.GetILGenerator(); code.Emit(OpCodes.Ldarg_0); code.Emit(OpCodes.Castclass, typeof(Form));
        code.Emit(OpCodes.Call, typeof(Form).GetConstructor(Type.EmptyTypes)); code.Emit(OpCodes.Ret);
        m.Invoke(null, new object[] { owner }); ((Form)owner).ClientSize = size; return owner;
    }

    static void Atajo()
    {
        foreach (bool rojo in new bool[] { false, true })
            foreach (bool abajo in new bool[] { false, true })
                foreach (bool repite in new bool[] { false, true })
                    Check((bool)Run("EsAtajoAyuda", rojo, abajo, repite) == (rojo && abajo && !repite),
                        "ROJO + ABAJO abre/guarda la ayuda, una vez por pulsacion (" + rojo + "," + abajo + "," + repite + ")");
        Check(form.Assembly.GetType("Rocola.AsistenteCombinacion") == null, "Ya no queda el acorde rojo+verde de tres segundos");
        Check(form.GetMethod("ReenviaEntradaAsistente", Todos) == null && form.GetField("asistentePendiente", Todos) == null,
            "Rojo y verde responden al instante: sin ventana de reconocimiento de acorde");
        MethodInfo entrada = form.GetMethod("AsistenteEntrada", Todos);
        Check(Codigo.Llama(entrada, "EsAtajoAyuda") && Codigo.Llama(entrada, "AbreAsistente") && Codigo.Llama(entrada, "CierraAsistente"),
            "La entrada abre y guarda la ayuda con el atajo");
        Check(Codigo.Llama(form.GetMethod("ProcessCmdKey", Todos), "AsistenteEntrada"), "ProcessCmdKey pasa por la ayuda");
        Check(Codigo.Llama(form.GetMethod("AnimTimer_Tick", Todos), "TickAsistenteGpu"), "El latido sigue a la guia");
        Check(Codigo.Llama(form.GetMethod("TickAsistenteGpu", Todos), "EvaluaGuia"), "La guia se reevalua en cada latido");
        Check(Codigo.Llama(form.GetMethod("AyudaTocada", Todos), "AbreAsistente"), "Tocar la casilla de AYUDA abre la ayuda");
        Check(Codigo.Llama(form.GetMethod("AyudaTocada", Todos), "CierraAsistente"), "Y con la ayuda abierta, tocarla la vuelve a guardar");
        Check(Codigo.Llama(form.GetMethod("AsistenteClick", Todos), "CierraAsistente"), "Tocar fuera del globo de la pregunta la guarda");
        Check(Codigo.Llama(form.Assembly.GetType("Rocola.GpuPanel", true).GetMethod("AvanzaGlobosGuia", Todos), "AsistenteGuiaRectSigue"),
            "El globo de la guia recuerda su esquina (no cruza la pantalla a cada paso)");
        Check(!Codigo.Llama(form.GetMethod("EvaluaGuia", Todos), "AddItem") && !Codigo.Llama(form.GetMethod("EmpiezaMision", Todos), "AddItem"),
            "La guia no pone canciones ni gasta creditos: lo hace el cliente");
        // Todo pedido pasa por AddItem: desde un disco, un video o el buscador (ChooseSearchResult llama a AddItem
        // directamente). Si el aviso estuviera solo en AddSong, la mision "Buscar" no llegaria nunca a su logro.
        Check(Codigo.Llama(form.GetMethod("AddItem", Todos), "NotaPedidoAsistente"),
            "La guia se entera de cuando la cancion quedo en la cola, venga de donde venga");
        Check(Codigo.Llama(form.GetMethod("ChooseSearchResult", Todos), "AddItem"),
            "El buscador encola por AddItem (y por tanto avisa a la guia)");
    }

    static void Pasos()
    {
        int cancion = K("MisionCancion"), buscar = K("MisionBuscar"), aprender = K("MisionAprender");
        int disco = K("PasoDisco"), moneda = K("PasoMoneda"), pc = K("PasoCancion"), abre = K("PasoAbreBuscador"),
            escribe = K("PasoEscribe"), resultado = K("PasoResultado");
        // La tabla: del estado real al paso que toca.
        Check((int)Run("PasoParaCancion", cancion, false, false, false, 3) == disco, "En la pared: elegir un disco");
        Check((int)Run("PasoParaCancion", buscar, false, false, false, 3) == abre, "Buscando, en la pared: abrir el buscador");
        Check((int)Run("PasoParaCancion", cancion, false, false, true, 2) == pc, "Dentro del disco con credito: elegir la cancion");
        Check((int)Run("PasoParaCancion", cancion, false, false, true, 0) == moneda, "Dentro del disco sin credito: la moneda");
        Check((int)Run("PasoParaCancion", buscar, true, false, false, 0) == escribe, "Buscador abierto: escribir");
        Check((int)Run("PasoParaCancion", buscar, true, true, false, 1) == resultado, "En los resultados con credito: elegir");
        Check((int)Run("PasoParaCancion", buscar, true, true, false, 0) == moneda, "En los resultados sin credito: la moneda");
        Check((int)Run("PasoParaCancion", cancion, true, false, false, 1) == escribe, "Si abre el buscador poniendo un disco, la guia le sigue");
        // Paso X de N: la moneda solo cuenta si hace falta.
        object[] a = { cancion, pc, true, 0 }; int n = (int)Run("NumeroPaso", a);
        Check(n == 2 && (int)a[3] == 2, "Con credito, elegir la cancion es el paso 2 de 2");
        a = new object[] { cancion, pc, false, 0 }; n = (int)Run("NumeroPaso", a);
        Check(n == 3 && (int)a[3] == 3, "Sin credito, el paso 3 de 3");
        a = new object[] { buscar, resultado, true, 0 }; n = (int)Run("NumeroPaso", a);
        Check(n == 3 && (int)a[3] == 3, "Buscar con credito: elegir es el 3 de 3");
        a = new object[] { aprender, K("PasoLetra"), true, 0 }; n = (int)Run("NumeroPaso", a);
        Check(n == 3 && (int)a[3] == 5, "Aprender: saltar de letra es el 3 de 5");
        // Cada paso dice algo y dibuja sus botones.
        foreach (string p in new string[] { "PasoDisco", "PasoMoneda", "PasoCancion", "PasoAbreBuscador", "PasoEscribe", "PasoResultado",
                                            "PasoMover", "PasoPagina", "PasoLetra", "PasoEntrar", "PasoVolver", "PasoCierraBuscador" })
        {
            object[] t = { K(p), null, null, null }; Run("TextosGuia", t);
            Check(((string)t[1]).Length > 4 && ((string)t[2]).Length > 10 && ((string)t[3]).Length > 0, "Texto y botones para " + p);
        }
        object[] u = { K("PasoAbreBuscador"), null, null, null }; Run("TextosGuia", u);
        Check(((string)u[3]).Contains("B+U"), "Abrir el buscador dibuja ROJO + ARRIBA");
    }

    // Una mision completa sobre un formulario aislado: avanza sola con lo que hace el cliente.
    static void Mision()
    {
        using (Form owner = (Form)Owner(new Size(1024, 768)))
        {
            Set(owner, "asistenteVisible", true);
            Set(owner, "credits", 0);
            Call(owner, "EmpiezaMision", K("MisionAprender"));
            Check((int)Get(owner, "asistentePaso") == K("PasoMover"), "Aprender empieza por moverse");
            int ev = (int)Get(owner, "asistenteEvento");
            Set(owner, "selAlbumIndex", (int)Get(owner, "selAlbumIndex") + 1); Call(owner, "EvaluaGuia");
            Check((int)Get(owner, "asistentePaso") == K("PasoPagina") && (int)Get(owner, "asistenteEvento") != ev, "Al moverse, la estrella lo celebra y pasa a la pagina");
            Check((int)Get(owner, "asistenteBienTick") != 0, "Dice ¡muy bien!");
            Call(owner, "EvaluaGuia");
            Check((int)Get(owner, "asistentePaso") == K("PasoPagina"), "Sin hacer nada, no avanza");
            Set(owner, "page", (int)Get(owner, "page") + 1); Call(owner, "EvaluaGuia");
            Check((int)Get(owner, "asistentePaso") == K("PasoLetra"), "Al cambiar de pagina, toca la letra");
            char letra = (char)Get(owner, "currentLetter");
            Set(owner, "currentLetter", letra == 'B' ? 'C' : 'B'); Call(owner, "EvaluaGuia");
            Check((int)Get(owner, "asistentePaso") == K("PasoEntrar"), "Al saltar de letra, toca entrar a un disco");
            Check((int)Get(owner, "asistenteLogroTick") == 0, "Aun no se logro");
            // Pedir una cancion: la guia se entera sola y celebra.
            Set(owner, "asistenteMision", 0);
            Call(owner, "EmpiezaMision", K("MisionCancion"));
            Check((int)Get(owner, "asistentePaso") == K("PasoDisco"), "Poner una cancion empieza por el disco");
            Call(owner, "NotaPedidoAsistente"); Call(owner, "EvaluaGuia");
            Check((int)Get(owner, "asistenteLogroTick") != 0, "Al quedar la cancion en la cola, lo logro");
            Call(owner, "BuildAsistenteState");
            Bitmap globo = (Bitmap)Get(owner, "asistentePanel");
            Type gpu = form.Assembly.GetType("Rocola.GpuPanel", true);
            Size tam = (Size)gpu.GetMethod("AsistenteGuiaTam", Todos).Invoke(null, new object[] { 1024, 768 });
            Check(globo != null && globo.Size == tam, "Guiando, la estrella habla en el globo pequeno");
            Set(owner, "asistenteLogroTick", unchecked(Environment.TickCount - 5000) == 0 ? 1 : unchecked(Environment.TickCount - 5000));
            Call(owner, "EvaluaGuia");
            Check(!(bool)Get(owner, "asistenteVisible") && (int)Get(owner, "asistenteMision") == 0, "Tras celebrarlo, la ayuda se vuelve a guardar sola");
            Call(owner, "SueltaAsistente");
        }
    }

    static void Cableado()
    {
        MethodInfo entry = form.GetMethod("AsistenteEntrada", Todos);
        MethodInfo tick = form.GetMethod("TickAsistenteGpu", Todos);
        // La ayuda es solo GPU, respetando el renderer CPU congelado.
        if (entry == null && tick == null)
        {
            Check(form.GetMethod("EnsureAsistentePanel", Todos) == null, "Panel nuevo presente en CPU");
            Check(form.GetMethod("BuildAsistenteState", Todos) == null, "Estado visual nuevo presente en CPU");
            Check(form.GetNestedType("AyudaView", Todos) == null, "La casilla de AYUDA solo existe con tarjeta grafica");
            return;
        }
        Atajo(); Pasos(); Mision();
    }

    static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Uso: asistentecombo.exe Rocola.exe");
        Assembly assembly = Assembly.LoadFrom(args[0]);
        form = assembly.GetType("Rocola.Form1", true);
        Cableado();
        Console.WriteLine("OK asistente: " + checks + " comprobaciones del atajo ROJO + ABAJO, la tabla de pasos y una mision guiada. Sin abrir ventanas ni medir FPS reales.");
        return 0;
    }
}

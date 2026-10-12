using System;
using System.Drawing;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;

// EL CONTROL DE LA AYUDA Y LA GUIA PASO A PASO (11-10-2026).
// La ayuda se abre con ROJO + flecha ABAJO, en cualquier orden (la gemela de BUSCAR, que es ROJO + ARRIBA) o tocando su casilla, y
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
        bool[] dos = { false, true };
        foreach (bool esAbajo in dos) foreach (bool rojo in dos) foreach (bool esRojo in dos) foreach (bool abajo in dos) foreach (bool repite in dos)
            Check((bool)Run("EsAtajoAyuda", esAbajo, rojo, esRojo, abajo, repite) == (!repite && ((esAbajo && rojo) || (esRojo && abajo))),
                "ROJO + ABAJO abre/guarda la ayuda, una vez por pulsacion (" + esAbajo + "," + rojo + "," + esRojo + "," + abajo + "," + repite + ")");
        // El orden da igual (revision del 12-10-2026): al apretar los dos a la vez cualquiera llega primero.
        Check((bool)Run("EsAtajoAyuda", true, true, false, false, false), "ABAJO con el rojo apretado");
        Check((bool)Run("EsAtajoAyuda", false, false, true, true, false), "ROJO con el abajo apretado (antes no hacia nada)");
        Check(!(bool)Run("EsAtajoAyuda", true, false, false, false, false) && !(bool)Run("EsAtajoAyuda", false, false, true, false, false),
            "Un boton solo no es el atajo");
        // El otro boton, apretado segun la cola de mensajes O segun el teclado fisico.
        foreach (string m in new string[] { "RojoMantenido", "AbajoMantenido" })
            Check(Codigo.Llama(form.GetMethod(m, Todos), "SegunCola") && Codigo.Llama(form.GetMethod(m, Todos), "Fisica"), m + ": cola de mensajes y teclado fisico");
        Check(Codigo.Llama(form.GetMethod("SegunCola", Todos), "KeyHeld") && Codigo.Llama(form.GetMethod("Fisica", Todos), "GetAsyncKeyState"), "GetKeyState y GetAsyncKeyState");
        MethodInfo entrada0 = form.GetMethod("AsistenteEntrada", Todos);
        Check(Codigo.Llama(entrada0, "RojoMantenido") && Codigo.Llama(entrada0, "AbajoMantenido") && !Codigo.Llama(entrada0, "BackHeld"),
            "La entrada mira los dos botones mantenidos con la misma regla");
        Check(Codigo.Llama(form.GetMethod("GuiaRojoVisible", Todos), "RojoMantenido") && Codigo.Llama(form.GetMethod("VigilaRojoMantenido", Todos), "RojoMantenido"),
            "Las pistas del rojo miran el rojo con la misma regla que el atajo");
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
        // El regreso de la estrella ya no se le pregunta a la tarjeta (revision del 12-10-2026): lo cuenta el formulario.
        Type gpu = form.Assembly.GetType("Rocola.GpuPanel", true);
        Check(gpu.GetProperty("AsistenteEnTransicion", Todos) == null && gpu.GetMethod("RegresoSinCuadros", Todos) == null,
            "El formulario no depende de los cuadros de la tarjeta para soltar los botones");
        Check(Codigo.Llama(form.GetMethod("TickAsistenteGpu", Todos), "VigilaRojoMantenido") && Codigo.Llama(form.GetMethod("VigilaRojoMantenido", Todos), "MotivoSinGuiaRojo")
            && Codigo.Llama(form.GetMethod("VigilaRojoMantenido", Todos), "LogError"), "Rojo mantenido sin pistas: se apunta el motivo");
        // Nunca mas un atajo que no hace nada sin decir por que: se dice EN LA PANTALLA y se apunta.
        MethodInfo avisa = form.GetMethod("AvisaSinAyuda", Todos);
        Check(form.GetMethod("ApuntaAyudaNoPermitida", Todos) == null, "Ya no queda el apunte mudo");
        Check(Codigo.Llama(form.GetMethod("AsistenteEntrada", Todos), "AvisaSinAyuda") && Codigo.Llama(form.GetMethod("AyudaTocada", Todos), "AvisaSinAyuda"),
            "ROJO + ABAJO y tocar AYUDA, si no se puede, avisan");
        Check(Codigo.Llama(avisa, "ShowToast") && Codigo.Llama(avisa, "LogError"), "El aviso sale en la pantalla y queda en rocola_error.txt");
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

    // "Despues de usar el asistente una vez, los controles se bloquean para siempre" (aviso del 11-10-2026). El regreso
    // de la estrella lo decidia la tarjeta con su ULTIMO cuadro: si los cuadros se paraban con la ayuda aun abierta, la
    // tarjeta seguia creyendola abierta y el regreso no acababa nunca. Y "despues de un rato ya no se puede usar"
    // (12-10-2026): ahora el regreso lo cuenta solo el formulario, solo la niegan los bloqueos de verdad y, si no se
    // puede, lo dice en la pantalla.
    static void Desbloqueo()
    {
        int tope = K("RegresoTopeMs");
        Check(tope >= 720 && tope <= 1000, "El regreso retiene los botones lo que dura el salto y nada mas (" + tope + " ms)");
        Check((bool)Run("RegresoVigente", 1000, 1000) && (bool)Run("RegresoVigente", 1000, 1000 + tope - 1), "Dentro del tope, el regreso sigue");
        Check(!(bool)Run("RegresoVigente", 1000, 1000 + tope) && !(bool)Run("RegresoVigente", 1000, 999), "Pasado el tope (o con el reloj hacia atras), ya no retiene");
        Check((bool)Run("RegresoVigente", int.MaxValue - 100, unchecked(int.MaxValue + 200)), "Aguanta la vuelta del contador de Windows");
        MethodInfo regresando = form.GetMethod("get_AsistenteRegresando", Todos);
        int leeP, escP; Codigo.UsosDeCampo(regresando, "gpuPanel", out leeP, out escP);
        Check(Codigo.Llama(form.GetMethod("get_AsistenteVisible", Todos), "get_AsistenteRegresando") && Codigo.Llama(regresando, "RegresoVigente") && leeP == 0,
            "El regreso lo cuenta solo el formulario, con su reloj");
        // Volver a abrirla mientras regresa (antes ese rato no hacia nada y parecia bloqueada).
        Check(!Codigo.Llama(form.GetMethod("AyudaTocada", Todos), "get_AsistenteVisible"), "Tocar AYUDA la abre aunque la estrella aun regrese");
        // Solo la niegan los bloqueos de verdad (revision del 12-10-2026). El foco (si la tecla llego, la rocola la tiene),
        // leer la biblioteca (la ayuda no la toca) y verde = rojo (era del acorde viejo) la negaban sin decir nada.
        MethodInfo motivo = form.GetMethod("MotivoSinAyuda", Todos);
        Check(Codigo.Llama(form.GetMethod("AsistentePermitido", Todos), "MotivoSinAyuda"), "Permitir y avisar usan la misma lista");
        int leeL, escL, leeK, escK; Codigo.UsosDeCampo(motivo, "libBuilding", out leeL, out escL); Codigo.UsosDeCampo(motivo, "keyOK", out leeK, out escK);
        Check(!Codigo.Llama(motivo, "RocolaConTeclado") && !Codigo.Llama(motivo, "get_ContainsFocus") && leeL == 0 && leeK == 0,
            "Ni el foco, ni leer la biblioteca, ni verde = rojo niegan la ayuda");
        Check(Codigo.Llama(form.GetMethod("GuiaRojoVisible", Todos), "RocolaConTeclado") && Codigo.Llama(form.GetMethod("MotivoSinGuiaRojo", Todos), "RocolaConTeclado"),
            "Las pistas del rojo miran lo mismo");
        Check(Codigo.Llama(form.GetMethod("FocusTimer_Tick", Todos), "get_ContainsFocus") && Codigo.Llama(form.GetMethod("FocusTimer_Tick", Todos), "Focus"),
            "El vigilante del foco devuelve el foco a la ventana aunque ya este delante");

        // El caso que bloqueaba, en un formulario aislado: la tarjeta dejo de dibujar con la ayuda abierta.
        Type gpu = form.Assembly.GetType("Rocola.GpuPanel", true);
        using (Form owner = (Form)Owner(new Size(1024, 768)))
        using (Control panel = (Control)Activator.CreateInstance(gpu, true))
        {
            Check(!(bool)Call(owner, "RocolaConTeclado"), "Sin ventana de Windows (Linux): sin teclado, y sin reventar");
            Set(owner, "gpuEnabled", true);
            Set(owner, "asistenteVisible", true);
            // Lo ultimo que vio la tarjeta: la ayuda abierta, dibujandose, y un cuadro reciente.
            object estado = Activator.CreateInstance(form.Assembly.GetType("Rocola.AsistenteEstadoGpu", true));
            estado.GetType().GetField("Visible", Todos).SetValue(estado, true);
            gpu.GetField("asistenteEstado", Todos).SetValue(panel, estado);
            gpu.GetField("asistenteDibujando", Todos).SetValue(panel, true);
            gpu.GetField("asistenteUltimoAvance", Todos).SetValue(panel, (double)gpu.GetMethod("AhoraMs", Todos).Invoke(null, null) / 1000);
            int antes = Environment.TickCount;
            Call(owner, "CierraAsistente");   // sin panel puesto todavia: no hay pantalla que sincronizar
            int cierre = (int)Get(owner, "asistenteCierreTick");
            Check(!(bool)Get(owner, "asistenteVisible") && unchecked(cierre - antes) >= 0 && unchecked(Environment.TickCount - cierre) >= 0,
                "Al guardarla, el formulario apunta cuando");
            int lee, escribe;
            Codigo.UsosDeCampo(form.GetMethod("CierraAsistente", Todos), "asistenteRegresando", out lee, out escribe);
            Check(escribe > 0, "Guardarla empieza el regreso");
            Codigo.UsosDeCampo(form.GetMethod("AbreAsistente", Todos), "asistenteRegresando", out lee, out escribe);
            Check(escribe > 0, "Abrirla lo termina");
            // Como lo deja CierraAsistente con la tarjeta puesta (aqui sin ella no hay regreso que esperar).
            Set(owner, "gpuPanel", panel); Set(owner, "asistenteRegresando", true); Set(owner, "asistenteCierreTick", Environment.TickCount);
            Check((bool)Get(owner, "asistenteRegresando") && (bool)Call(owner, "get_AsistenteCapturaEntrada"), "Recien guardada: el regreso retiene los botones");
            Set(owner, "asistenteCierreTick", unchecked(Environment.TickCount - tope - 50));
            Check(!(bool)Call(owner, "get_AsistenteVisible") && !(bool)Call(owner, "get_AsistenteCapturaEntrada"),
                "Pasado el tope los botones vuelven, aunque la tarjeta siga dibujando el regreso");
            Check(!(bool)Get(owner, "asistenteRegresando"), "Y se queda suelto (no vuelve a bloquear)");
            // Lo que de verdad la niega, y lo que ya no (sin foco, como aqui en Linux, y leyendo la biblioteca).
            Set(owner, "libBuilding", true);
            Check(Call(owner, "MotivoSinAyuda") == null, "Sin foco del teclado y leyendo la biblioteca, la ayuda se puede abrir");
            Set(owner, "libBuilding", false);
            Set(owner, "configMode", true); Check((string)Call(owner, "MotivoSinAyuda") == "configurando botones", "Configurando botones: no"); Set(owner, "configMode", false);
            Set(owner, "maintMode", true); Check(((string)Call(owner, "MotivoSinAyuda")).Contains("F10"), "En mantenimiento: no"); Set(owner, "maintMode", false);
            Set(owner, "arranqueEnEspera", true); Check((string)Call(owner, "MotivoSinAyuda") == "arrancando", "Arrancando: no"); Set(owner, "arranqueEnEspera", false);
            Set(owner, "gpuPanel", null); Check((string)Call(owner, "MotivoSinAyuda") == "sin tarjeta grafica", "Sin tarjeta: no"); Set(owner, "gpuPanel", panel);
            // Y cuando no se puede, lo dice: en la pantalla (con tope de ritmo) y en rocola_error.txt.
            Call(owner, "AvisaSinAyuda", "configurando botones");
            int avisoTick = (int)Get(owner, "ayudaNoPermitidaTick");
            Check(avisoTick != 0, "El aviso sale en la pantalla");
            Call(owner, "AvisaSinAyuda", "configurando botones");
            Check((int)Get(owner, "ayudaNoPermitidaTick") == avisoTick, "Mantenerlo no amontona avisos (uno cada segundo y medio)");
            Codigo.EsperaBitacora();
            string bit = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
            string log = System.IO.Path.Combine(bit, "rocola_error.txt");
            Check(System.IO.File.Exists(log) && System.IO.File.ReadAllText(log).Contains("ROJO + ABAJO sin ayuda: configurando botones"), "Y queda apuntado en rocola_error.txt");
            // Reabrir mientras regresa: la tarjeta da media vuelta.
            Set(owner, "asistenteRegresando", true); Set(owner, "asistenteCierreTick", Environment.TickCount);
            Check((bool)Call(owner, "get_AsistenteVisible"), "Otra vez regresando");
            Set(owner, "gpuPanel", null);
            Call(owner, "AbreAsistente");
            Check((bool)Get(owner, "asistenteVisible") && !(bool)Get(owner, "asistenteRegresando"), "ROJO + ABAJO durante el regreso la vuelve a abrir");
            Set(owner, "asistenteVisible", false);
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
        Atajo(); Pasos(); Mision(); Desbloqueo();
    }

    static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Uso: asistentecombo.exe Rocola.exe");
        string datos = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rocola_asistentecombo_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(datos);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", datos);   // lo que se apunte en la bitacora, aparte
        Assembly assembly = Assembly.LoadFrom(args[0]);
        form = assembly.GetType("Rocola.Form1", true);
        Cableado();
        Console.WriteLine("OK asistente: " + checks + " comprobaciones del atajo ROJO + ABAJO, la tabla de pasos y una mision guiada. Sin abrir ventanas ni medir FPS reales.");
        return 0;
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

// LO QUE PUEDE PARAR LA PANTALLA, LEIDO DEL PROGRAMA COMPILADO.
//
// La pantalla de la rocola es un solo hilo: si se queda esperando al disco, a otro hilo, a abrir una cancion o a medirla,
// no pinta ni atiende una tecla mientras tanto. Esta bateria lee el programa compilado (su codigo intermedio) y parte de
// TODO lo que corre en la pantalla: los temporizadores, las teclas, el dibujo, los eventos y lo que se le manda con
// BeginInvoke. Desde ahi sigue las llamadas -sin cruzar a lo que se encarga a un hilo de fondo- y apunta cada sitio que
// llega a algo que puede esperar:
//   disco           abrir, leer, escribir, mirar si existe o borrar un archivo o una carpeta
//   bajar al disco  FlushFileBuffers (esperar a que el disco confirme)
//   espera          Thread.Sleep/Join, WaitOne, Monitor.Wait, esperar a un programa
//   proceso         lanzar un programa
//   imagen          abrir una imagen del disco
//   abrir audio     abrir una cancion (los lectores de NAudio)
//   medir cancion   MeasureFile (recorrer la cancion entera)
//
// Cada sitio que se acepta esta en la lista de abajo CON SU MOTIVO, revisado a mano. Si aparece uno nuevo, la bateria
// falla y ensena el camino desde la pantalla hasta la llamada: o se lleva a un hilo de fondo, o se revisa y se anade a la
// lista explicando por que puede quedarse. Y si un sitio de la lista ya no ocurre, tambien falla: la lista tiene que decir
// la verdad, no acumular excusas viejas.
//
// Lo que NO ve: una espera escondida detras de una llamada a otra biblioteca (NAudio, Windows) que no sea de las de arriba,
// un bucle largo sin llamadas, ni lo que hace un delegado guardado en un campo y llamado mas tarde (por ejemplo la firma
// -tamano y fecha- de la siguiente cancion, que MiraPorDelante mira en la pantalla al pedirla y al tomarla). Para eso estan el vigia de la pantalla (vigiapantalla.cs) y la ventana real (ventanareal.cs).
class Bloqueos
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static Assembly asm;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    // ================================ LA LISTA REVISADA ================================
    // { categoria, sitio, rama ("" las dos, "hd", "modest"), motivo }. El sitio es "Tipo.Metodo"; lo de dentro de un
    // delegado sale como "Tipo.Metodo (delegado)".
    static readonly string[,] Revisados = {
        { "abrir audio", "Form1.BeginAudioOpen", "", "solo en la medida de rendimiento (audioOpeningForMeasurement), que abre a proposito en la pantalla para cronometrarlo; la musica abre en segundo plano (AperturaEnFondo)" },
        { "bajar al disco", "Form1.IaCountPairs", "", "al arrancar, una vez: recorta un final a medias del archivo de ejemplos de la IA antes de contarlo" },
        { "disco", "Form1.IaCountPairs", "", "al arrancar, una vez: cuantos ejemplos de la IA hay (cabecera y tamano)" },
        { "disco", "Form1.AllMusicRoots", "", "solo esperando un disco de musica que faltaba al arrancar: cada 5 s, sin musica ni cola" },
        { "disco", "Form1.AnimTimer_Tick", "", "lo mismo: mira si volvio el disco que faltaba, cada 5 s, sin musica ni cola" },
        { "disco", "Form1.BorraEstado", "", "borrar el estado: al cerrar a proposito, al empezar el dia (una vez, en reposo) o con un estado de otro dia" },
        { "disco", "Form1.DeCuandoEs", "", "la fecha del programa: al arrancar (bitacora) y en Ctrl+F3" },
        { "disco", "Form1.EstadoPorRestaurar", "", "una vez por sesion: una moneda antes de restaurar lee lo guardado de antes (despues queda en memoria)" },
        { "disco", "Form1.LeeIntegArchivo", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.LeeNocheSiFalta", "", "lo mas pedido esta noche: una vez por sesion al entrar la atraccion, si aun no se habia leido" },
        { "disco", "Form1.LeeTintes", "", "al armar la biblioteca: el color de cada letra (un archivo pequeno), una vez por biblioteca" },
        { "disco", "Form1.LoadConfigPrefs", "", "arranque, una vez: la configuracion" },
        { "disco", "Form1.LoadStats", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.PorQueNoSePuedeAgregar", "", "ventana de carpetas de musica (Ctrl+F2), a pedido del operador" },
        { "disco", "Form1.ProcessCmdKey", "", "F5 del teclado de servicio: borrar el indice de la biblioteca para recorrer el disco de nuevo" },
        { "disco", "Form1.RaizConfiguradaAusente", "", "al restaurar creditos y cola (una vez): distinguir un disco desconectado de una cancion borrada" },
        { "disco", "Form1.ReadDeepDone", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.ReadDurFast", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.ReadEqCache", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.ReadIaDone", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.ReadIncompatible", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.ReadLoudCache", "", "arranque: solo si el lector de tablas no pudo (camino de respaldo)" },
        { "disco", "Form1.RestoreState", "", "restaurar creditos y cola tras un apagon: una vez (lee lo encargado en memoria si aun no llego al disco)" },
        { "disco", "Form1.RutaLectura", "", "arranque, una vez: donde estan los modelos de IA" },
        { "disco", "Form1.OrdenaIas", "", "arranque, una vez, antes de cargar los modelos: pasar los sueltos a la carpeta IAS (cambiar de nombre en el mismo disco, sin copiar; si no hay sueltos, solo mira la carpeta)" },
        { "disco", "Form1.LibreAnterior", "", "lo mismo: solo si ya habia en IAS un modelo con el nombre del suelto, busca el .anterior-N libre" },
        { "disco", "Form1.EscribeConfigEnTurno", "", "guardar la configuracion al cambiarla en las ventanas de servicio (SaveConfigPrefs; desde Ctrl+F3 va detras de la pantalla)" },
        { "disco", "Form1.SaveKeys", "", "asistente de botones: guardar al terminar la configuracion" },
        { "disco", "Form1.SavedStateTime", "", "al restaurar: la fecha del estado guardado si no la trae dentro (formato viejo)" },
        { "disco", "Form1.ShowMusicDirManager", "", "ventana de carpetas de musica (Ctrl+F2)" },
        { "disco", "Form1.ShowMusicDirManager (delegado)", "", "ventana de carpetas de musica (Ctrl+F2)" },
        { "disco", "Form1.VigilaRaicesAusentes", "", "al buscar la musica sin encontrarla (arranque o F5): mira si hay estado que esperar" },
        { "disco", "Form1.get_CarpetaDatos", "", "la carpeta de datos se decide una vez por sesion (se prueba si se puede escribir) y se recuerda" },
        { "disco", "Form1.get_MusicFolder", "", "al armar la biblioteca: donde esta la carpeta musica (hasta cuatro preguntas)" },
        { "disco", "Program.LogCrash", "", "solo tras un fallo que puede terminar el proceso: se escribe ya, a proposito (Bitacora.Vacia)" },
        { "disco", "RedChica.TryLoad", "", "arranque, una vez: cargar los modelos de IA" },
        { "disco", "Track.ReadId3", "", "la etiqueta ID3 de la cancion que empieza, SOLO si no llego leida por adelantado: la primera, una elegida con la cola vacia o un archivo que cambio (lo normal la deja lista PrecalientaSiguiente: 'etiquetadelante')" },
        { "disco", "Form1.AudioFileSignature", "", "al empezar la cancion: que la etiqueta leida por adelantado sigue valiendo (tamano y fechas del archivo, sin abrirlo ni leerlo)" },
        { "disco", "Form1.CapturaFuente", "", "al aplicar/publicar medidas: solo metadatos del original, como AudioFileSignature; impide usar volumen, EQ o recortes de una cancion sustituida. No abre ni recorre el audio" },
        { "disco", "Form1.CapturaRespaldo", "", "al seguir el arranque o cerrar el dia: como maximo 15 handles y sus longitudes, o retener la generacion previa de una mutacion en curso. Solo toma un candado corto de metadatos, nunca el candado del formato/Write/Flush ni espera a un escritor (datosdeltas lo comprueba bajo contencion). NO lee/copia contenido ni calcula hashes: esas operaciones ceden en fondo. Abrir un handle en Windows sigue pudiendo tardar, especialmente en rutas remotas" },
        { "disco", "Form1.LoadFuentesMedidas", "", "arranque: solo si fallo el lector de tablas en fondo, igual que LoadLoudCache; la ruta normal carga la identidad con las otras tablas" },
        { "espera", "GpuPanel.RenderFrame", "", "solo con la tarjeta perdida: 5 ms antes de volver a intentarlo" },
        { "espera", "GpuPanel.EsperaMensajes", "modest", "plan B de 1 ms si Windows no tiene MsgWaitForMultipleObjectsEx" },
        { "espera", "Sistema.EsperaMensajes", "hd", "plan B de 1 ms si Windows no tiene MsgWaitForMultipleObjectsEx" },
        { "disco", "Form1.BorraVideosMudos", "hd", "limpieza de videos mudos: ventana de servicio, a pedido del operador" },
        { "disco", "Form1.QuitaDeLaBiblioteca", "hd", "limpieza de videos mudos: ventana de servicio, a pedido del operador" },
        { "disco", "Form1.IaBuscaListo", "hd", "IA en vivo (si esta encendida): ya esta la version mejorada? Hasta cuatro consultas a la carpeta de la cache, como mucho una vez cada 10 s por cancion (la busqueda se recuerda: 'apollo')" },
        { "disco", "Form1.IaHashFor", "hd", "IA en vivo: identidad de la cancion (tamano y fecha) en cada pregunta; es lo que impide que una cancion reemplazada herede lo recordado" },
        { "disco", "Form1.ReadIaPipelineFingerprint", "hd", "IA en vivo: la huella del modelo, como mucho cada 10 s (se recuerda: 'apollo')" },
        { "disco", "Form1.IaTouch", "hd", "IA en vivo: marcar como recien usada la version que va a sonar (la limpieza del motor borra lo menos usado); una vez por recuerdo de la busqueda" },
        { "disco", "Form1.IaFindEngine", "hd", "buscar el motor de IA: al arrancar y como mucho cada 10 s; queda recordado" },
        { "disco", "Form1.IaMotorAlive", "hd", "IA en vivo: sigue vivo el motor? cada 10 s (dos consultas de metadatos)" },
        { "disco", "Form1.IaGpuList", "hd", "ventana de ajustes (Ctrl+F3), a pedido del operador" },
        { "disco", "Form1.IaRestartMotor", "hd", "boton de reiniciar el motor de IA en Ctrl+F3" },
        { "disco", "Form1.PlayTrack (delegado)", "hd", "IA en vivo: borrar una version mejorada que no se pudo abrir (rara; despues suena la original)" },
        { "espera", "Form1.IaStopMotor", "hd", "reiniciar el motor de IA desde Ctrl+F3: esperar a que se cierre el anterior" },
        { "proceso", "Form1.IaEnsureMotor", "hd", "lanzar el motor de IA: al arrancar y, si se cae, como mucho cada 10 s" },
        { "proceso", "Form1.IaStopMotor", "hd", "reiniciar el motor de IA desde Ctrl+F3" },
    };

    // ================================ LAS LLAMADAS DE UN METODO ================================
    // Se leen con el lector comun de las baterias (leercodigo.cs, Codigo.Pasos), como todas: 'leer' vigila que ninguna
    // lleve su propia copia del recorrido. De cada instruccion interesan las llamadas (call, callvirt, newobj) y los
    // delegados que se fabrican (ldftn, ldvirtftn), con a quien se le dan: la primera llamada de despues que no fabrica
    // otro delegado.
    struct Llamada { public MethodBase M; public bool Delegado; public string Siguiente; }
    static readonly Dictionary<MethodBase, List<Llamada>> memo = new Dictionary<MethodBase, List<Llamada>>();
    static List<Llamada> Llamadas(MethodBase m)
    {
        List<Llamada> r;
        if (memo.TryGetValue(m, out r)) return r;
        r = new List<Llamada>(); memo[m] = r;
        Module mod = m.Module;
        List<int> pend = new List<int>();   // delegados a la espera de ver a quien se le dan
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op == 0xFE)
            {
                if (p.Op2 == 0x06 || p.Op2 == 0x07)   // ldftn / ldvirtftn: se fabrica un delegado
                {
                    try { MethodBase t = mod.ResolveMethod(p.Numero); if (t != null) { Llamada l = new Llamada(); l.M = t; l.Delegado = true; r.Add(l); pend.Add(r.Count - 1); } } catch { }
                }
                continue;
            }
            if (p.Op == 0x28 || p.Op == 0x6F || p.Op == 0x73)   // call / callvirt / newobj
            {
                try
                {
                    MethodBase t = mod.ResolveMethod(p.Numero);
                    if (t != null)
                    {
                        Llamada l = new Llamada(); l.M = t; r.Add(l);
                        bool fabricaDelegado = p.Op == 0x73 && t.DeclaringType != null && t.DeclaringType.BaseType == typeof(MulticastDelegate);
                        if (!fabricaDelegado)   // a quien se le da el delegado: la primera llamada que no fabrica otro
                        {
                            foreach (int k in pend) { Llamada d = r[k]; d.Siguiente = N(t); r[k] = d; }
                            pend.Clear();
                        }
                    }
                }
                catch { }
            }
        }
        return r;
    }
    static string N(MethodBase m) { return (m.DeclaringType != null ? m.DeclaringType.Name : "?") + "." + m.Name; }

    // El sitio con un nombre estable: lo que el compilador fabrica para un delegado tiene nombres distintos segun el
    // compilador (mcs: "<Metodo>c__AnonStorey3.<>m__0", "<Metodo>m__5"; csc: "<Metodo>b__0" dentro de "<>c__DisplayClass").
    static readonly Dictionary<MethodBase, MethodBase> creador = new Dictionary<MethodBase, MethodBase>();
    static string Sitio(MethodBase m) { return Sitio(m, 0); }
    static string Sitio(MethodBase m, int hondo)
    {
        Type t = m.DeclaringType; string nombre = m.Name; bool delegado = false;
        string de = Entre(nombre);
        if (de != null) { nombre = de; delegado = true; }
        while (t != null && t.Name.IndexOf('<') >= 0)
        {
            string dt = Entre(t.Name);
            if (!delegado || nombre.StartsWith("<")) { if (dt != null && dt.Length > 0) nombre = dt; }
            delegado = true; t = t.DeclaringType;
        }
        if (nombre.Length == 0 || nombre.StartsWith("<"))
        {
            MethodBase c;
            if (hondo < 6 && creador.TryGetValue(m, out c) && c != m) { string sc = Sitio(c, hondo + 1); return sc.EndsWith(" (delegado)") ? sc : sc + " (delegado)"; }
            nombre = "?";
        }
        return (t != null ? t.Name : "?") + "." + nombre + (delegado ? " (delegado)" : "");
    }
    static string Entre(string s)
    {
        int a = s.IndexOf('<'); if (a < 0) return null;
        int b = s.IndexOf('>', a + 1); if (b < 0) return null;
        return s.Substring(a + 1, b - a - 1);
    }

    // A quien se le da un delegado que se va a otro hilo: lo de dentro no corre en la pantalla.
    static readonly string[] AlFondo = { "ThreadPool.QueueUserWorkItem", "ThreadPool.UnsafeQueueUserWorkItem", "Thread..ctor", "BackgroundWorker.add_DoWork",
        "Timer..ctor", "Form1.GuardaEnFondo", "Form1.GuardaUltimo", "Form1.GuardaDetras", "Form1.QueueDataWrite", "Bitacora.Mete",
        "AperturaEnFondo`1.Start", "MiraPorDelante`1.Pide", "BombaSonido.CuandoEsteLista", "Task.Run", "TaskFactory.StartNew" };
    static bool VaAlFondo(string siguiente)
    {
        if (siguiente == null) return false;
        foreach (string s in AlFondo) if (siguiente == s) return true;
        return false;
    }
    // Y a quien se le da uno que la pantalla va a correr: BeginInvoke, Invoke y los eventos.
    static bool VaALaPantalla(string siguiente)
    {
        if (siguiente == null) return false;
        if (siguiente.EndsWith(".BeginInvoke") || siguiente.EndsWith(".Invoke")) return !siguiente.StartsWith("Action") && !siguiente.StartsWith("Func") && !siguiente.StartsWith("MethodInvoker") && !siguiente.StartsWith("EventHandler");
        return siguiente.Contains(".add_") && !siguiente.EndsWith(".add_DoWork");
    }

    static string Categoria(MethodBase m)
    {
        string t = m.DeclaringType != null ? m.DeclaringType.FullName ?? "" : "", n = m.Name;
        if (t == "System.IO.Directory" && n == "GetParent") return null;   // solo cuentas con la ruta: no toca el disco
        if (t == "System.IO.File" || t == "System.IO.Directory" || t == "System.IO.FileInfo" || t == "System.IO.DirectoryInfo" || t == "System.IO.DriveInfo") return "disco";
        // Los ayudantes de la rocola que van al disco o abren una cancion cuentan como la llamada misma: asi el sitio que
        // sale es QUIEN los usa (guardar el estado, la configuracion...), y no el ayudante, que lo usan todos.
        if (t == "Rocola.Form1" && (n == "EscribeSeguro" || n == "EscribeSeguroYa" || n == "AnexaSeguro" || n == "EscribeFlujoSeguro" || n == "PublicaGuardadoSeguro"
            || n == "BorraSeguro" || n == "LeeLineasSeguro" || n == "AbreArchivoRespaldo")) return "disco";
        if (t == "Rocola.Bitacora" && n == "Vacia") return "disco";
        if (t == "Rocola.Form1" && n == "OpenAudio") return "abrir audio";
        if (t == "System.IO.FileStream" && n == ".ctor") return "disco";
        if ((t == "System.IO.StreamReader" || t == "System.IO.StreamWriter") && n == ".ctor")
        { ParameterInfo[] p = m.GetParameters(); if (p.Length > 0 && p[0].ParameterType == typeof(string)) return "disco"; }
        if ((t == "System.Drawing.Image" || t == "System.Drawing.Bitmap") && (n == "FromFile" || n == "FromStream")) return "imagen";
        if (t == "System.Drawing.Bitmap" && n == ".ctor") { ParameterInfo[] p = m.GetParameters(); if (p.Length > 0 && p[0].ParameterType == typeof(string)) return "imagen"; }
        if (t == "System.Threading.Thread" && (n == "Sleep" || n == "Join")) return "espera";
        if ((t == "System.Threading.WaitHandle" || t == "System.Threading.EventWaitHandle" || t == "System.Threading.ManualResetEvent" || t == "System.Threading.AutoResetEvent")
            && (n.StartsWith("WaitOne") || n == "WaitAll" || n == "WaitAny")) return "espera";
        if (t == "System.Threading.Monitor" && n == "Wait") return "espera";
        if (t == "System.Diagnostics.Process" && n == "WaitForExit") return "espera";
        if (t == "System.Diagnostics.Process" && n == "Start") return "proceso";
        if (n == ".ctor" && (t.EndsWith("Mp3FileReader") || t.EndsWith("WaveFileReader") || t.EndsWith("MediaFoundationReader") || t.EndsWith("AudioFileReader") || t.EndsWith("Mp3FileReaderBase"))) return "abrir audio";
        if (t == "Rocola.Form1" && (n == "MeasureFile" || n == "MeasureFileResponsive")) return "medir cancion";
        if (t == "Rocola.Form1" && n == "FlushFileBuffers") return "bajar al disco";
        return null;
    }

    static bool EsEntrada(MethodBase m)
    {
        string n = m.Name;
        string[] fin = { "_Tick", "_Click", "_KeyDown", "_KeyUp", "_KeyPress", "_MouseDown", "_MouseUp", "_MouseMove", "_MouseClick", "_MouseWheel",
                         "_Paint", "_Resize", "_PlaybackStopped", "_RunWorkerCompleted", "_ProgressChanged" };
        foreach (string s in fin) if (n.EndsWith(s)) return true;
        string[] exactos = { "OnPaint", "OnPaintBackground", "OnKeyDown", "OnKeyUp", "OnKeyPress", "ProcessCmdKey", "ProcessDialogKey", "OnMouseDown", "OnMouseUp",
                             "OnMouseMove", "OnMouseWheel", "WndProc", "OnResize", "OnSizeChanged", "OnShown", "OnIdle", "OnVisibleChanged", "OnGotFocus", "OnActivated" };
        foreach (string s in exactos) if (n == s) return true;
        return false;
    }

    sealed class Hallazgo { public string Cat, Sitio; public SortedDictionary<string, bool> Que = new SortedDictionary<string, bool>(); public string Camino; }

    static int Main(string[] args)
    {
        asm = System.Reflection.Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        // NLayer existe en ambas ramas; la salida exclusiva sigue siendo propia de HD.
        string rama = asm.GetType("Rocola.SalidaExacta", false) != null ? "hd" : "modest";
        Type[] tipos;
        try { tipos = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { tipos = Array.FindAll(e.Types, delegate (Type x) { return x != null; }); }
        List<MethodBase> todos = new List<MethodBase>();
        foreach (Type t in tipos) { foreach (MethodInfo m in t.GetMethods(T)) todos.Add(m); foreach (ConstructorInfo c in t.GetConstructors(T)) todos.Add(c); }

        // Las entradas: por nombre (eventos, dibujo, teclas) y los delegados que se le dan a la pantalla.
        List<MethodBase> entradas = new List<MethodBase>(); Dictionary<MethodBase, bool> esEntrada = new Dictionary<MethodBase, bool>();
        foreach (MethodBase m in todos)
            if (m.DeclaringType != null && m.DeclaringType.Namespace == "Rocola" && EsEntrada(m) && !esEntrada.ContainsKey(m)) { esEntrada[m] = true; entradas.Add(m); }
        foreach (MethodBase m in todos)
            foreach (Llamada l in Llamadas(m))
                if (l.Delegado && !creador.ContainsKey(l.M)) creador[l.M] = m;
        foreach (MethodBase m in todos)
            foreach (Llamada l in Llamadas(m))
                if (l.Delegado && VaALaPantalla(l.Siguiente) && l.M.DeclaringType != null && l.M.DeclaringType.Assembly == asm && !esEntrada.ContainsKey(l.M))
                { esEntrada[l.M] = true; entradas.Add(l.M); }

        // Todo lo que se alcanza desde ellas sin cruzar a un hilo de fondo (un solo recorrido, con el camino mas corto).
        Dictionary<MethodBase, MethodBase> padre = new Dictionary<MethodBase, MethodBase>();
        Queue<MethodBase> cola = new Queue<MethodBase>();
        foreach (MethodBase e in entradas) { padre[e] = null; cola.Enqueue(e); }
        Dictionary<string, Hallazgo> hallazgos = new Dictionary<string, Hallazgo>();
        while (cola.Count > 0)
        {
            MethodBase m = cola.Dequeue();
            foreach (Llamada l in Llamadas(m))
            {
                MethodBase c = l.M;
                if (l.Delegado) { if (VaAlFondo(l.Siguiente) || VaALaPantalla(l.Siguiente)) continue; }   // otro hilo, u otra entrada que ya se recorre
                string cat = Categoria(c);
                if (cat != null)
                {
                    string sitio = Sitio(m), k = cat + " | " + sitio;
                    Hallazgo h;
                    if (!hallazgos.TryGetValue(k, out h))
                    {
                        h = new Hallazgo(); h.Cat = cat; h.Sitio = sitio;
                        StringBuilder sb = new StringBuilder(N(c));
                        for (MethodBase x = m; x != null; x = padre[x]) sb.Insert(0, Sitio(x) + " > ");
                        h.Camino = sb.ToString(); hallazgos[k] = h;
                    }
                    h.Que[N(c)] = true;
                    continue;
                }
                if (c.DeclaringType == null || c.DeclaringType.Assembly != asm || padre.ContainsKey(c)) continue;
                padre[c] = m; cola.Enqueue(c);
            }
        }

        Console.WriteLine("\n  LO QUE PUEDE PARAR LA PANTALLA (" + (rama == "hd" ? "rocola HD" : "rocola Modest") + ")\n");
        Console.WriteLine("        " + entradas.Count + " entradas de la pantalla, " + padre.Count + " metodos alcanzados, " + hallazgos.Count + " sitios que pueden esperar");
        Dictionary<string, string> motivos = new Dictionary<string, string>();
        List<string> sobran = new List<string>();
        for (int i = 0; i < Revisados.GetLength(0); i++)
        {
            if (Revisados[i, 0].Length == 0) continue;
            string r = Revisados[i, 2];
            if (r.Length > 0 && r != rama) continue;
            string k = Revisados[i, 0] + " | " + Revisados[i, 1];
            motivos[k] = Revisados[i, 3];
            if (!hallazgos.ContainsKey(k)) sobran.Add(k);
        }
        List<string> ks = new List<string>(hallazgos.Keys); ks.Sort(StringComparer.Ordinal);
        int nuevos = 0;
        foreach (string k in ks)
        {
            if (motivos.ContainsKey(k)) continue;
            nuevos++;
            Hallazgo h = hallazgos[k];
            Console.WriteLine("  NUEVO  " + k + "   [" + string.Join(", ", new List<string>(h.Que.Keys).ToArray()) + "]");
            Console.WriteLine("           " + h.Camino);
        }
        Check(nuevos == 0, "ningun sitio nuevo que pueda parar la pantalla sin revisar (" + nuevos + " nuevos; " + (hallazgos.Count - nuevos) + " revisados, cada uno con su motivo)");
        foreach (string s in sobran) Console.WriteLine("  SOBRA  " + s + "   (ya no ocurre: quitarlo de la lista)");
        Check(sobran.Count == 0, "y la lista revisada no tiene sitios que ya no ocurren (" + sobran.Count + ")");
        Check(entradas.Count > 100 && padre.Count > 500, "el recorrido ve el programa entero, no un trozo (" + entradas.Count + " entradas, " + padre.Count + " metodos)");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

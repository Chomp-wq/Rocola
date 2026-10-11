using System;
using System.Reflection;

// No abre la Rocola, no simula teclas en Windows ni toca datos de produccion.
// El reloj y el estado de los dos botones se pasan a la maquina pura: el gesto
// debe tardar tres segundos de reloj, no tres segundos sumados por fotogramas.
class AsistenteCombo
{
    const BindingFlags Todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type detector;
    static MethodInfo actualiza, reinicia;
    static PropertyInfo capturada, progreso;
    static int checks;

    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    static object Nuevo()
    {
        object d = Activator.CreateInstance(detector, true);
        Estado(d, false, 0f, "recien creado");
        return d;
    }

    static void Estado(object d, bool captura, float avance, string label)
    {
        Check((bool)capturada.GetValue(d, null) == captura, "Capturada incorrecta: " + label);
        float p = (float)progreso.GetValue(d, null);
        Check(!Single.IsNaN(p) && !Single.IsInfinity(p) && p >= 0f && p <= 1f,
            "Progreso invalido o fuera de [0,1]: " + label);
        Check(Math.Abs(p - avance) <= .001f, "Progreso incorrecto: " + label + " (" + p + ", esperado " + avance + ")");
    }

    static bool Tick(object d, int now, bool red, bool green, bool allowed)
    {
        return (bool)actualiza.Invoke(d, new object[] { now, red, green, allowed });
    }

    static void Paso(object d, int now, bool red, bool green, bool allowed,
        bool fires, bool captured, float progress, string label)
    {
        Check(Tick(d, now, red, green, allowed) == fires, "Activacion incorrecta: " + label);
        Estado(d, captured, progress, label);
    }

    static int Suma(int start, int delta) { return unchecked(start + delta); }

    static void LimitesYRepeticion()
    {
        object d = Nuevo();
        Paso(d, 1000, false, false, true, false, false, 0f, "reposo");
        Paso(d, 1000, true, true, true, false, true, 0f, "captura inmediata");
        Paso(d, 1000, true, true, true, false, true, 0f, "autorepeticion sin reloj");
        Paso(d, 1001, true, true, true, false, true, 1f / 3000f, "primer milisegundo");
        Paso(d, 2500, true, true, true, false, true, .5f, "mitad");
        Paso(d, 3999, true, true, true, false, true, 2999f / 3000f, "2999 ms no bastan");
        Paso(d, 4000, true, true, true, true, true, 1f, "3000 ms exactos");
        for (int i = 0; i < 100; i++)
            Paso(d, 4000 + i * 170, true, true, true, false, true, 1f, "no reactivar sostenido " + i);
        Paso(d, 24000, false, false, true, false, false, 0f, "ambos libres limpian");
        Paso(d, 24000, true, true, true, false, true, 0f, "segunda combinacion nueva");
        Paso(d, 27000, true, true, true, true, true, 1f, "segunda activacion deliberada");
    }

    static void AmbosOrdenes()
    {
        for (int order = 0; order < 2; order++)
        {
            object d = Nuevo();
            bool redFirst = order == 0;
            Paso(d, 800, redFirst, !redFirst, true, false, false, 0f, "primer boton solo " + order);
            Paso(d, 12800, redFirst, !redFirst, true, false, false, 0f, "un solo boton no suma " + order);
            Paso(d, 14800, true, true, true, false, true, 0f, "entra segundo boton " + order);
            Paso(d, 17799, true, true, true, false, true, 2999f / 3000f, "espera completa desde segundo " + order);
            Paso(d, 17800, true, true, true, true, true, 1f, "activa ambos ordenes " + order);
        }
    }

    static void RemanentesYPermiso()
    {
        // Soltar un solo boton nunca convierte la cola del gesto en una nueva
        // pulsacion ni permite reanudar el contador al volver a apretarlo.
        foreach (bool releasedAfterFire in new bool[] { false, true })
        foreach (bool releaseRed in new bool[] { false, true })
        {
            string label = "parcial " + releasedAfterFire + "/" + releaseRed;
            object d = Nuevo();
            Paso(d, 100, true, true, true, false, true, 0f, label + " inicio");
            int releaseAt = releasedAfterFire ? 4000 : 2100;
            if (releasedAfterFire)
                Paso(d, 3100, true, true, true, true, true, 1f, label + " activa");
            Paso(d, releaseAt, !releaseRed, releaseRed, true, false, true, 0f, label + " cancelacion");
            Paso(d, releaseAt + 9000, true, true, true, false, true, 0f, label + " rebote no rearma");
            Paso(d, releaseAt + 18000, releaseRed, !releaseRed, true, false, true, 0f, label + " otro remanente");
            Paso(d, releaseAt + 22000, false, false, true, false, false, 0f, label + " soltados ambos");
            Paso(d, releaseAt + 24000, true, true, true, false, true, 0f, label + " gesto limpio");
            Paso(d, releaseAt + 27000, true, true, true, true, true, 1f, label + " activa limpio");
        }

        foreach (string context in new string[] { "sin foco", "mantenimiento", "renderer CPU", "ventana no permitida" })
        {
            object d = Nuevo();
            Paso(d, 100, true, false, false, false, false, 0f, context + ": rojo aislado no capturado");
            Paso(d, 4000, false, true, false, false, false, 0f, context + ": verde aislado no capturado");
            Paso(d, 5000, false, false, false, false, false, 0f, context + ": reposo prohibido");
            Paso(d, 6000, true, true, true, false, true, 0f, context + ": inicia permitido");
            Paso(d, 8900, true, true, false, false, true, 0f, context + ": pierde permiso");
            Paso(d, 16000, true, true, true, false, true, 0f, context + ": permiso no rearma botones retenidos");
            Paso(d, 16010, true, false, true, false, true, 0f, context + ": cola del gesto bloqueada");
            Paso(d, 17000, false, false, false, false, false, 0f, context + ": liberacion aun sin permiso");
            Paso(d, 18000, true, true, false, false, true, 0f, context + ": gesto prohibido bloquea remanentes");
            Paso(d, 25000, true, true, true, false, true, 0f, context + ": no inicia al ganar foco con ambos retenidos");
            Paso(d, 26000, false, false, true, false, false, 0f, context + ": rearme deliberado");
            Paso(d, 27000, true, true, true, false, true, 0f, context + ": inicia limpio");
            Paso(d, 30000, true, true, true, true, true, 1f, context + ": activa limpio");
        }
    }

    static void RelojYReinicio()
    {
        foreach (int start in new int[] { 0, -1, -1000, Int32.MaxValue - 1500, Int32.MinValue, Int32.MinValue + 10 })
        {
            object d = Nuevo();
            Paso(d, start, true, true, true, false, true, 0f, "origen reloj " + start);
            Paso(d, Suma(start, 2999), true, true, true, false, true, 2999f / 3000f, "antes del limite / wrap " + start);
            Paso(d, Suma(start, 3000), true, true, true, true, true, 1f, "limite / wrap " + start);
            Paso(d, Suma(start, 3001), true, true, true, false, true, 1f, "wrap no reactiva " + start);
        }
        object gap = Nuevo();
        Paso(gap, 1000, true, true, true, false, true, 0f, "salto de FPS inicio");
        Paso(gap, 5500, true, true, true, true, true, 1f, "salto adelante 4500 ms cuenta reloj");
        Paso(gap, 15000, true, true, true, false, true, 1f, "salto posterior no reactiva");
        object backwards = Nuevo();
        Paso(backwards, 9000, true, true, true, false, true, 0f, "reloj hacia atras inicio");
        Paso(backwards, 8999, true, true, true, false, true, 0f, "reloj hacia atras cancela seguro");
        Paso(backwards, 30000, true, true, true, false, true, 0f, "reloj posterior no rearma cancelacion");
        Paso(backwards, 31000, false, false, true, false, false, 0f, "liberacion tras retroceso");

        object dLong = Nuevo();
        Paso(dLong, 1234, true, true, true, false, true, 0f, "pausa larga inicio");
        Paso(dLong, Suma(1234, 24 * 60 * 60 * 1000), true, true, true, true, true, 1f, "reloj positivo largo no depende de frames");
        Paso(dLong, Suma(1234, Int32.MinValue), true, true, true, false, true, 1f, "activacion larga sigue unica");
        object ambiguous = Nuevo();
        Paso(ambiguous, 1234, true, true, true, false, true, 0f, "salto ambiguo inicio");
        Paso(ambiguous, Suma(1234, Int32.MinValue), true, true, true, false, true, 0f, "diferencia unsigned enorme no activa");
        Paso(ambiguous, 25000, true, true, true, false, true, 0f, "salto ambiguo exige soltar");

        foreach (bool activated in new bool[] { false, true })
        {
            object d = Nuevo();
            Paso(d, 1000, true, true, true, false, true, 0f, "reinicio inicio " + activated);
            if (activated) Paso(d, 4000, true, true, true, true, true, 1f, "reinicio ya activo");
            else Paso(d, 2200, true, false, true, false, true, 0f, "reinicio bloqueado");
            reinicia.Invoke(d, null);
            Estado(d, false, 0f, "reinicia limpia " + activated);
            Paso(d, 20000, true, true, true, false, true, 0f, "nuevo reloj tras reinicio " + activated);
            Paso(d, 23000, true, true, true, true, true, 1f, "nuevo gesto tras reinicio " + activated);
        }
    }

    static void Cadencias()
    {
        foreach (int fps in new int[] { 1, 7, 24, 50, 60, 120, 200 })
        foreach (int start in new int[] { 1000, Int32.MaxValue - 1000 })
        {
            object d = Nuevo();
            int count = 0;
            for (int frame = 0; frame <= fps * 8; frame++)
            {
                int elapsed = frame * 1000 / fps;
                bool fires = Tick(d, Suma(start, elapsed), true, true, true);
                if (fires) count++;
                Check(!fires || elapsed >= 3000, "Activacion prematura a " + fps + " FPS");
                Check(count <= 1, "Autorrepeticion reactiva a " + fps + " FPS");
                Estado(d, true, elapsed >= 3000 ? 1f : elapsed / 3000f, "cadencia " + fps + " / " + elapsed);
            }
            Check(count == 1, "No se activa una sola vez a " + fps + " FPS");
        }
        Random rng = new Random(2103);
        for (int run = 0; run < 100; run++)
        {
            object d = Nuevo();
            int start = run % 2 == 0 ? 1000 : Int32.MaxValue - 1000;
            int elapsed = 0, count = 0;
            while (elapsed < 12000)
            {
                bool fires = Tick(d, Suma(start, elapsed), true, true, true);
                if (fires) count++;
                Check(!fires || elapsed >= 3000, "Pausa irregular no adelanta el gesto " + run);
                Check(count <= 1, "Pausa irregular no repite activacion " + run);
                Estado(d, true, elapsed >= 3000 ? 1f : elapsed / 3000f, "irregular " + run + "/" + elapsed);
                elapsed += rng.Next(1, 600);
            }
            Check(count == 1, "Pausa irregular pierde el gesto " + run);
        }
    }

    static void Cableado(Assembly assembly)
    {
        Type form = assembly.GetType("Rocola.Form1", true);
        MethodInfo entry = form.GetMethod("AsistenteEntrada", Todos);
        MethodInfo tick = form.GetMethod("TickAsistenteGpu", Todos);
        // El detector permanece comun; la entrada, el panel y la animacion
        // nuevos son solo GPU, respetando el renderer CPU congelado.
        if (entry == null && tick == null)
        {
            Check(form.GetMethod("EnsureAsistentePanel", Todos) == null, "Panel nuevo presente en CPU");
            Check(form.GetMethod("BuildAsistenteState", Todos) == null, "Estado visual nuevo presente en CPU");
            return;
        }
        Check(entry != null && tick != null, "Falta entrada o latido del asistente GPU");
        Check(Codigo.Llama(form.GetMethod("ProcessCmdKey", Todos), "AsistenteEntrada"), "ProcessCmdKey no captura el gesto");
        Check(Codigo.Llama(form.GetMethod("AnimTimer_Tick", Todos), "TickAsistenteGpu"), "El reloj de animacion no revisa la pulsacion mantenida");
        Check(form.GetMethod("EnsureAsistentePanel", Todos) != null, "Falta panel del asistente GPU");
        Check(form.GetMethod("BuildAsistenteState", Todos) != null, "Falta estado visual del asistente GPU");
        Check(!Codigo.Llama(actualiza, "get_TickCount") && !Codigo.Llama(actualiza, "Sleep"), "Detector no puro o con espera bloqueante");
    }

    static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Uso: asistentecombo.exe Rocola.exe");
        Assembly assembly = Assembly.LoadFrom(args[0]);
        detector = assembly.GetType("Rocola.AsistenteCombinacion", true);
        Check(detector.IsSealed && !detector.IsPublic, "El detector debe ser internal sealed");
        actualiza = detector.GetMethod("Actualiza", Todos, null, new Type[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) }, null);
        reinicia = detector.GetMethod("Reinicia", Todos, null, Type.EmptyTypes, null);
        capturada = detector.GetProperty("Capturada", Todos);
        progreso = detector.GetProperty("Progreso", Todos);
        Check(actualiza != null && actualiza.ReturnType == typeof(bool), "Falta Actualiza(ahora, rojo, verde, permitido)");
        Check(reinicia != null && reinicia.ReturnType == typeof(void), "Falta Reinicia()");
        Check(capturada != null && capturada.PropertyType == typeof(bool), "Falta Capturada bool");
        Check(progreso != null && progreso.PropertyType == typeof(float), "Falta Progreso float");
        LimitesYRepeticion(); AmbosOrdenes(); RemanentesYPermiso(); RelojYReinicio(); Cadencias(); Cableado(assembly);
        Console.WriteLine("OK asistente: " + checks + " comprobaciones de gesto, remanentes, permiso, reloj wrap y cadencia. Sin abrir ventanas ni medir FPS reales.");
        return 0;
    }
}

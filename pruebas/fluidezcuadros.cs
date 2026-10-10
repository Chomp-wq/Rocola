using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
class FluidezCuadros
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks++; }
    static object Field(object x, string name) { return x.GetType().GetField(name, B).GetValue(x); }
    static object Call(object x, string name, params object[] args) { return x.GetType().GetMethod(name, B).Invoke(x, args); }
    static object Work(Type type, Bitmap bmp) { return Activator.CreateInstance(type, B, null, new object[] { bmp, 16 }, null); }
    static void Same(object expected, object actual)
    {
        Check(actual != null, "resultado terminado");
        int[] a = (int[])Field(expected, "Rect"), b = (int[])Field(actual, "Rect");
        Check(a.Length == b.Length, "misma cantidad de rectangulos");
        for (int i = 0; i < a.Length; i++) Check(a[i] == b[i], "mismo recorte y orden");
        Check((double)Field(expected, "Fraccion") == (double)Field(actual, "Fraccion"), "misma area exacta");
        Array vertices = (Array)Field(actual, "Vtx");
        Check(vertices.Length == (int)Field(actual, "N") * 6, "vertices completos");
        foreach (object v in vertices) Check((float)Field(v, "Rhw") == 1f && (float)Field(v, "Z") == 0f, "pose inicial de vertices exacta");
    }
    static void Tiles(Assembly asm)
    {
        Type old = asm.GetType("Rocola.TeselasVisibles", true), work = asm.GetType("Rocola.TrabajoTeselas", true);
        int[] sizes = { 1, 15, 16, 17, 31, 64, 143 };
        foreach (int w in sizes) foreach (int h in sizes)
        using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(1, 40, 70, 90))) g.FillRectangle(brush, 0, 0, 1, 1);
                using (Brush brush = new SolidBrush(Color.FromArgb(255, 40, 70, 90))) g.FillRectangle(brush, w / 3, h / 2, Math.Max(1,w / 4), Math.Max(1,h / 4));
            }
            object expected = old.GetMethod("De", B).Invoke(null, new object[] { bmp, 16, false });
            object task = Work(work, bmp);
            Check(Call(task, "Avanza", 0L) == null, "sin margen no consume trabajo");
            object actual = null; int steps = 0;
            while (actual == null && steps++ < 10000)
            {
                actual = Call(task, "Avanza", 1L);
                // El bitmap no queda bloqueado entre llamadas, ni siquiera si aun falta trabajo.
                BitmapData bits = bmp.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                bmp.UnlockBits(bits);
            }
            Same(expected, actual);
        }
        foreach (PixelFormat format in new PixelFormat[] { PixelFormat.Format24bppRgb, PixelFormat.Format32bppPArgb })
        using (Bitmap bmp = new Bitmap(65, 33, format))
        {
            using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(90, 30, 90, 120));
            object expected = old.GetMethod("De", B).Invoke(null, new object[] { bmp, 16, false });
            object task = Work(work, bmp), actual = null;
            while (actual == null) actual = Call(task, "Avanza", 1L);
            Same(expected, actual);
        }
        using (Bitmap bmp = new Bitmap(3840, 2160, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); g.FillRectangle(Brushes.White, 20, 20, 700, 130); g.FillRectangle(Brushes.White, 2500, 1900, 1200, 190); }
            Stopwatch timer = Stopwatch.StartNew();
            object expected = old.GetMethod("De", B).Invoke(null, new object[] { bmp, 16, false });
            double full = timer.Elapsed.TotalMilliseconds;
            object task = Work(work, bmp), actual = null; double max = 0, sum = 0; int steps = 0;
            Call(task, "Avanza", 1L); // JIT/reflexion fuera de la medicion de tramos
            while (actual == null)
            {
                timer.Reset(); timer.Start(); actual = Call(task, "Avanza", (long)(Stopwatch.Frequency * .00035));
                double ms = timer.Elapsed.TotalMilliseconds; sum += ms; max = Math.Max(max, ms); steps++;
            }
            Same(expected, actual);
            Console.WriteLine("SINTETICO 3840x2160: analisis completo=" + full.ToString("0.000") +
                " ms; repartido=" + sum.ToString("0.000") + " ms / " + steps + " tandas; max tanda=" + max.ToString("0.000") +
                " ms. Incluye reflexion/JIT; no mide FPS ni impone un techo al sistema operativo.");
        }
    }
    static void Diagnostic(Assembly asm)
    {
        object d = Activator.CreateInstance(asm.GetType("Rocola.FluidezGpu", true), true);
        Call(d, "Toma", 0d, 1d, 2d, 3d);
        for (int i = 1; i <= 100; i++) Call(d, "Toma", (double)(i * (i + 1) / 2), 1d, 2d, 3d);
        Check((bool)Call(d, "Resume", 10000d), "resume primera ventana");
        Check((double)Field(d, "P95") == 95d && (double)Field(d, "P99") == 99d && (double)Field(d, "Maximo") == 100d, "percentiles por rango");
        Check(!(bool)Call(d, "Resume", 10999d), "no ordena por cuadro");
        for (int i = 1; i <= 600; i++) Call(d, "Toma", 5050d + i * 5d, -1d, 4d, 0d);
        Check((bool)Call(d, "Resume", 12000d) && (double)Field(d, "Maximo") == 5d, "anillo acotado elimina muestras antiguas");
        Check((double)Field(d, "PrepararMax") == 0d && (double)Field(d, "DibujarMax") == 4d, "etapas independientes y sin duraciones negativas");
        Call(d, "Reinicia");
        Check(!(bool)Call(d, "Resume", 13000d), "restaurar/minimizar no cuenta la pausa como un cuadro");
    }
    static void Frame(object d, double start, double end, double prep, double draw, double present, double period, double agenda, int gc0, int gc1, int gc2)
    { Call(d, "TomaCuadro", start, end, prep, draw, present, period, agenda, gc0, gc1, gc2); }
    static int Pending(object d) { return (int)d.GetType().GetProperty("EpisodiosPendientes", B).GetValue(d, null); }
    static void Periods(Assembly asm)
    {
        Type panel = asm.GetType("Rocola.GpuPanel", true), measure = asm.GetType("Rocola.FluidezGpu", true);
        MethodInfo period = panel.GetMethod("PeriodoDiagnostico", B);
        Check(period != null && period.IsStatic && period.ReturnType == typeof(double), "Periodo productivo puro y accesible sin HAL");
        Check(panel.GetMethod("TopeMs", B).ReturnType == typeof(int), "El limitador productivo conserva milisegundos enteros");
        foreach (int fps in new int[] { 0, 24, 50, 60, 200 })
        foreach (double refresh in new double[] { 0, 1000d / 60, 1000d / 75, 1000d / 144, 1000d / 200 })
        foreach (int throttle in new int[] { 0, 33, 50, 1000 })
        {
            // 0 es automatico: este caso representa una pantalla de 60 Hz (66 FPS -> 15 ms).
            // No preguntar por la pantalla real del equipo ni encender un panel para comprobar la cuenta.
            int cap = fps == 0 ? 1000 / 66 : 1000 / fps;
            double expected = refresh; if (cap > expected) expected = cap; if (throttle > expected) expected = throttle;
            double actual = (double)period.Invoke(null, new object[] { refresh, cap, throttle });
            Check(actual == expected, "Periodo respeta rejilla, tope " + fps + " y throttle " + throttle);
            object d = Activator.CreateInstance(measure, true);
            Frame(d, 0, 1, .2, .3, .5, actual, 0, 10, 5, 2);
            Frame(d, actual, actual + 1, .2, .3, .5, actual, 0, 10, 5, 2);
            Check(Pending(d) == 0, "Un intervalo normal del tope/refresh/throttle nunca es un episodio");
            Call(d, "Reinicia");
            double threshold = actual * 2; if (threshold < 20) threshold = 20;
            Frame(d, 0, 1, .2, .3, .5, actual, 0, 10, 5, 2);
            double below = threshold - .0001;
            Frame(d, below, below + 1, .2, .3, .5, actual, 0, 10, 5, 2);
            Check(Pending(d) == 0, "Por debajo del umbral no hay falso tiron");
            double after = below + threshold + .0001;
            Frame(d, after, after + 1, .2, .3, .5, actual, 0, 11, 6, 3);
            Check(Pending(d) == 1, "El intervalo sobre el umbral genera un episodio aun con throttle");
            object episode = ((Array)Field(d, "episodios")).GetValue(0);
            Check((double)Field(episode, "Periodo") == actual && (int)Field(episode, "Gc0") == 1,
                "Episodio conserva el periodo y GC del mismo intervalo");
        }
        Check((double)period.Invoke(null, new object[] { 0d, 5, -10 }) == 5d, "Throttle no positivo no cambia un tope valido");
        Check((double)period.Invoke(null, new object[] { 0d, 0, 0 }) == 0d, "Helper no inventa nuevas politicas para un periodo invalido");
        object changes = Activator.CreateInstance(measure, true);
        Frame(changes, 0, 1, 1, 0, 0, 40, 0, 0, 0, 0);
        Frame(changes, 31, 32, 1, 0, 0, 5, 0, 0, 0, 0);
        Check(Pending(changes) == 0, "Cambio a un tope rapido no reclasifica el intervalo lento anterior");
        Frame(changes, 51, 52, 1, 0, 0, 40, 0, 0, 0, 0);
        Check(Pending(changes) == 1, "Cambio a un tope lento no oculta un tiron del periodo rapido anterior");
        Check((double)Field(((Array)Field(changes, "episodios")).GetValue(0), "Periodo") == 5,
            "Periodo de episodio pertenece al cuadro anterior");
        MethodInfo render = panel.GetMethod("RenderFrame", B), idle = panel.GetMethod("OnIdle", B);
        Check(Codigo.Llama(render, "PeriodoDiagnostico") && Codigo.Llama(render, "TopeMs") && Codigo.Llama(render, "TomaCuadro"),
            "RenderFrame usa el periodo productivo al registrar cuadros");
        Check(Codigo.LeeCampo(new System.Collections.Generic.List<MethodBase>(new MethodBase[] { render }), "periodoVistaMs") &&
            Codigo.LeeCampo(new System.Collections.Generic.List<MethodBase>(new MethodBase[] { render }), "ThrottleMs"),
            "El periodo productivo recibe rejilla y throttle del panel");
        Check(Codigo.Llama(render, "CollectionCount") && Codigo.Llama(render, "Reinicia") &&
            Codigo.Llama(render, "Resume") && Codigo.Llama(render, "DrenaEpisodios"), "GC/reset y salidas de diagnostico integrados");
        Check(Codigo.Llama(idle, "AgendaCuadro") && Codigo.LeeCampo(new System.Collections.Generic.List<MethodBase>(new MethodBase[] { idle }), "ShowFps"),
            "Agenda solo se cronometra con el contador visible");
        Check(Codigo.Llama(panel.GetMethod("ReiniciaMedida", B), "Reinicia") &&
            Codigo.Llama(panel.GetMethod("PuedeDibujar", B), "Reinicia") && Codigo.Llama(panel.GetMethod("StopGpu", B), "Reinicia"),
            "Medicion, restauracion y dispositivo cortan continuidad del diagnostico");
    }
    static void FailedPresentation(Assembly asm)
    {
        Type type = asm.GetType("Rocola.FluidezGpu", true), panel = asm.GetType("Rocola.GpuPanel", true);
        object d = Activator.CreateInstance(type, true);
        Frame(d, 0, 3, 1, 1, 1, 5, 0, 0, 0, 0);
        Frame(d, 40, 43, 1, 1, 1, 5, 0, 1, 1, 1);
        Check((bool)Call(d, "Resume", 100d) && (double)Field(d, "Maximo") == 40d && Pending(d) == 1,
            "Historial y episodio previos al fallo disponibles");
        int version = (int)Field(d, "Version");
        string[] names = { "intervalos", "ordenados", "preparar", "dibujar", "presentar", "fueraRender", "agenda", "gc0", "gc1", "gc2", "episodios" };
        object[] buffers = new object[names.Length];
        for (int i = 0; i < names.Length; i++) buffers[i] = Field(d, names[i]);
        Call(d, "DescartaContinuidad");
        Check((int)Field(d, "n") == 1 && Pending(d) == 1 && (double)Field(d, "Maximo") == 40d &&
            (int)Field(d, "Version") == version, "Fallo conserva historial, episodios y resumen ya calculados");
        Check((double)Field(d, "anterior") == -1 && (int)Field(d, "gc0Anterior") == -1 &&
            (int)Field(d, "gc1Anterior") == -1 && (int)Field(d, "gc2Anterior") == -1,
            "Fallo corta unicamente el origen de correlacion y GC");
        Frame(d, 5000, 5003, 1, 1, 1, 5, 0, 20, 10, 5);
        Check((int)Field(d, "n") == 1 && Pending(d) == 1,
            "Primer cuadro tras intento fallido no atribuye una pausa de 4960 ms al anterior");
        Frame(d, 5010, 5013, 1, 1, 1, 5, 0, 20, 10, 5);
        Check((int)Field(d, "n") == 2 && Pending(d) == 1 && (double)((Array)Field(d, "intervalos")).GetValue(1) == 10d,
            "Siguiente cuadro retoma intervalos validos sin inventar episodios");
        for (int i = 0; i < names.Length; i++) Check(object.ReferenceEquals(buffers[i], Field(d, names[i])),
            "Descartar continuidad no reemplaza buffers fijos");
        MethodInfo failed = panel.GetMethod("NoPresentado", B);
        Check(Codigo.Llama(failed, "DescartaContinuidad") &&
            Codigo.EscribeCampo(new System.Collections.Generic.List<MethodBase>(new MethodBase[] { failed }), "fluidezAgendaMs"),
            "Intento no presentado corta correlacion y descarta agenda acumulada");
        Check(Codigo.Llama(failed, "get_FallosMedidos") && Codigo.Llama(failed, "set_FallosMedidos"),
            "Intento fallido sigue contado independientemente de cuadros exitosos");
        // Solo el helper de contadores: sin constructor de Control, ventana, dispositivo ni dibujo.
        object uninitialized = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(panel);
        panel.GetField("fluidez", B).SetValue(uninitialized, d);
        panel.GetField("fluidezAgendaMs", B).SetValue(uninitialized, 9d);
        Check(!(bool)failed.Invoke(uninitialized, null) &&
            (int)panel.GetProperty("FallosMedidos", B).GetValue(uninitialized, null) == 1 &&
            (double)Field(uninitialized, "fluidezAgendaMs") == 0d && (double)Field(d, "anterior") == -1,
            "Camino productivo fallido devuelve false, suma un fallo y corta agenda/correlacion");
    }
    static void NoFrameAllocations(Type type)
    {
        System.Reflection.Emit.OpCode[] codes = new System.Reflection.Emit.OpCode[512];
        foreach (FieldInfo field in typeof(System.Reflection.Emit.OpCodes).GetFields(B))
        {
            if (field.FieldType != typeof(System.Reflection.Emit.OpCode)) continue;
            System.Reflection.Emit.OpCode code = (System.Reflection.Emit.OpCode)field.GetValue(null);
            int value = (ushort)code.Value;
            codes[value > 255 ? 256 + (value & 255) : value] = code;
        }
        foreach (string name in new string[] { "Toma", "TomaCuadro", "TomaInterna", "CortaIntervalo", "DescartaContinuidad", "DiferenciaGc", "Finito" })
        {
            byte[] il = type.GetMethod(name, B).GetMethodBody().GetILAsByteArray();
            for (int at = 0; at < il.Length;)
            {
                int value = il[at++]; if (value == 254) value = 256 + il[at++];
                System.Reflection.Emit.OpCode code = codes[value];
                Check(code != System.Reflection.Emit.OpCodes.Newarr && code != System.Reflection.Emit.OpCodes.Newobj &&
                    code != System.Reflection.Emit.OpCodes.Box, name + " no asigna arrays/objetos/boxing por cuadro");
                switch (code.OperandType)
                {
                    case System.Reflection.Emit.OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(il, at); at += 4 + count * 4; break;
                    case System.Reflection.Emit.OperandType.InlineI8:
                    case System.Reflection.Emit.OperandType.InlineR: at += 8; break;
                    case System.Reflection.Emit.OperandType.InlineVar: at += 2; break;
                    case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
                    case System.Reflection.Emit.OperandType.ShortInlineI:
                    case System.Reflection.Emit.OperandType.ShortInlineVar: at++; break;
                    case System.Reflection.Emit.OperandType.InlineNone: break;
                    default: at += 4; break;
                }
            }
        }
    }
    static void Correlation(Assembly asm)
    {
        Type type = asm.GetType("Rocola.FluidezGpu", true);
        object d = Activator.CreateInstance(type, true);
        Frame(d, 100, 109, 3, 4, 2, 10, 0, 10, 2, 1);
        Frame(d, 145, 235, 20, 30, 40, 10, 5, 11, 3, 1);
        Check((bool)Call(d, "Resume", 300d), "primera correlacion disponible");
        Check((double)Field(d, "Maximo") == 45d && (double)Field(d, "PrepararMax") == 3d &&
            (double)Field(d, "DibujarMax") == 4d && (double)Field(d, "PresentarMax") == 2d, "intervalo tiene fases anteriores, no actuales");
        Check((double)Field(d, "FueraRenderMax") == 36d && (double)Field(d, "AgendaMax") == 5d, "hueco End previo a Start actual, Agenda dentro del hueco");
        Check((int)Field(d, "Gc0Max") == 1 && (int)Field(d, "Gc1Max") == 1 && (int)Field(d, "Gc2Max") == 0, "GC difiere entre inicios del intervalo completo");
        Frame(d, 260, 266, 1, 2, 3, 10, 99, 12, 3, 1);
        System.Globalization.CultureInfo oldCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        string report;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
            report = (string)Call(d, "DrenaEpisodios", 400d);
        }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = oldCulture; }
        Check(report.Contains("episodios GPU: cantidad=2 descartados=0"), "drain agrupado de episodios");
        Check(report.Contains("inicio=100.0 intervalo=45.0 preparar=3.0 dibujar=4.0 presentar=2.0 fueraRender=36.0 agenda=5.0 periodo=10.0 gc=1/1/0"),
            "formato invariante y correlacion completa del primer episodio");
        Check(report.Contains("inicio=145.0 intervalo=115.0 preparar=20.0 dibujar=30.0 presentar=40.0 fueraRender=25.0 agenda=25.0"), "Agenda acotada al hueco real");
        Check(Pending(d) == 0 && (int)Field(d, "EpisodiosDescartados") == 0, "drain consume solo los episodios pendientes");
        Frame(d, 300, 306, 1, 2, 3, 10, -1, -1, -1, -1);
        Frame(d, 340, 346, 1, 2, 3, 10, 0, 3, 3, 3);
        Frame(d, 380, 386, 1, 2, 3, 10, 0, 2, 2, 2);
        Check((string)Call(d, "DrenaEpisodios", 1399d) == string.Empty && Pending(d) == 3, "no drena mas de una vez por segundo ni pierde episodios");
        Check((string)Call(d, "DrenaEpisodios", double.NaN) == string.Empty &&
            (string)Call(d, "DrenaEpisodios", double.PositiveInfinity) == string.Empty &&
            (string)Call(d, "DrenaEpisodios", -1d) == string.Empty && Pending(d) == 3, "reloj invalido no consume episodios");
        report = (string)Call(d, "DrenaEpisodios", 1400d);
        Check(report.Contains("cantidad=3") && !report.Contains("gc=1/") && report.Contains("agenda=0.0"), "GC ausente, negativo o reiniciado no genera resta falsa");
        Check(!(bool)Call(d, "Resume", double.NaN) && !(bool)Call(d, "Resume", -1d), "reloj de resumen invalido no altera ventana");
        Call(d, "Reinicia");
        Check(Pending(d) == 0 && (int)Field(d, "EpisodiosDescartados") == 0 && (double)Field(d, "FueraRenderMax") == 0d &&
            (int)Field(d, "Gc0Max") == 0 && !(bool)Call(d, "Resume", 2000d), "reset borra correlacion, estadisticas y episodios");

        Frame(d, 0, 1, 1, 0, 0, 25, 0, 0, 0, 0);
        Frame(d, 49, 50, 1, 0, 0, 25, 0, 0, 0, 0);
        Check(Pending(d) == 0, "umbral respeta dos periodos de 25 ms");
        Frame(d, 99, 100, 1, 0, 0, 5, 0, 0, 0, 0);
        Check(Pending(d) == 1, "igual al umbral de dos periodos si se registra");
        Frame(d, 118, 119, 1, 0, 0, 5, 0, 0, 0, 0);
        Check(Pending(d) == 1, "minimo de 20 ms no marca 19 ms");
        Frame(d, 138, 139, 1, 0, 0, 5, 0, 0, 0, 0);
        Check(Pending(d) == 2, "umbral minimo incluye 20 ms");

        foreach (double bad in new double[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1 })
        {
            int oldN = (int)Field(d, "n");
            Frame(d, bad, 200, 1, 0, 0, 5, 0, 0, 0, 0);
            Check((int)Field(d, "n") == oldN, "inicio invalido no crea muestra");
            Frame(d, 200, 201, 1, 0, 0, 5, 0, 0, 0, 0);
            Check((int)Field(d, "n") == oldN, "primera muestra tras invalido no cruza la pausa");
        }
        int kept = (int)Field(d, "n");
        Frame(d, 210, 209, 1, 0, 0, 5, 0, 0, 0, 0);
        Frame(d, 220, 221, 1, 0, 0, 5, 0, 0, 0, 0);
        Check((int)Field(d, "n") == kept, "End anterior al Start corta continuidad");
        Frame(d, 230, 231, double.NaN, 0, 0, 5, 0, 0, 0, 0);
        Frame(d, 240, 241, 1, 0, 0, 5, 0, 0, 0, 0);
        Check((int)Field(d, "n") == kept, "fase invalida corta continuidad");
        Frame(d, 250, 251, 1, 0, 0, 0, 0, 0, 0, 0);
        Frame(d, 260, 261, 1, 0, 0, 5, 0, 0, 0, 0);
        Check((int)Field(d, "n") == kept, "periodo invalido corta continuidad");
        Frame(d, 270, 271, 1, 0, 0, 5, double.NaN, 0, 0, 0);
        Frame(d, 280, 285, 1, 2, 2, 5, 0, 0, 0, 0);
        Check((int)Field(d, "n") == kept, "Agenda invalida corta continuidad");
        Frame(d, 279, 280, 1, 0, 0, 5, 0, 0, 0, 0);
        Check((int)Field(d, "n") == kept, "reloj que retrocede no registra intervalo");
        Frame(d, 279.5, 280.5, 1, 0, 0, 5, 0, 0, 0, 0);
        Check((int)Field(d, "n") == kept, "Start superpuesto al End medido no registra intervalo");

        Call(d, "Reinicia");
        object[] buffers = new object[11];
        string[] names = { "intervalos", "ordenados", "preparar", "dibujar", "presentar", "fueraRender", "agenda", "gc0", "gc1", "gc2", "episodios" };
        for (int i = 0; i < names.Length; i++) buffers[i] = Field(d, names[i]);
        Frame(d, 0, 3, 1, 1, 1, 10, 0, 0, 0, 0);
        for (int i = 1; i <= 100; i++) Frame(d, i * 40, i * 40 + 3, 1, 1, 1, 10, 0, i, i, i);
        Check(Pending(d) == 32 && (int)Field(d, "EpisodiosDescartados") == 68, "anillo de episodios acotado con descartes contados");
        report = (string)Call(d, "DrenaEpisodios", 10000d);
        Check(report.Split('\n').Length == 33 && report.Contains("cantidad=32 descartados=68"), "salida acotada incluye resumen de perdidas");
        Check(report.Contains("inicio=2720.0 intervalo=40.0") && !report.Contains("inicio=2680.0 intervalo=40.0") &&
            report.EndsWith("gc=1/1/1"), "episodios conservan los ultimos en orden cronologico");
        for (int i = 101; i <= 700; i++) Frame(d, i * 40, i * 40 + 3, 1, 1, 1, 10, 0, i, i, i);
        for (int i = 0; i < names.Length; i++) Check(object.ReferenceEquals(buffers[i], Field(d, names[i])) &&
            ((Array)buffers[i]).Length == (i == 10 ? 32 : 512), "buffers fijos no crecen ni se reemplazan");
        Check((int)Field(d, "n") == 512, "estadisticas correlacionadas conservan 512 intervalos");
        NoFrameAllocations(type);
    }
    [STAThread] static int Main(string[] args)
    {
        try { Assembly asm = Assembly.LoadFrom(args[0]); Tiles(asm); Diagnostic(asm); Periods(asm); FailedPresentation(asm); Correlation(asm); Console.WriteLine("PASS " + checks + " comprobaciones: teselas exactas/progresivas y diagnostico correlacionado/acotado."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

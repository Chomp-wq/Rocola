using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Prueba aislada del preparador GPU: no abre Form1, no usa Direct3D ni la biblioteca.
class PreparacionNaturalTest
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type serviceType, keyType, mapsType, gpuType, formType;
    static int checks, calls, active, maximum, mode;
    static bool cancellationSeen, backgroundSeen, prioritySeen;
    static readonly object fakeLock = new object();
    static readonly List<object> produced = new List<object>(), received = new List<object>();
    static readonly ManualResetEvent entered = new ManualResetEvent(false), release = new ManualResetEvent(false);
    static object Get(object instance, string name) { return instance.GetType().GetField(name, B).GetValue(instance); }
    static void Set(object instance, string name, object value) { instance.GetType().GetField(name, B).SetValue(instance, value); }
    static object Call(Type type, object instance, string name, params object[] args)
    {
        try { return type.InvokeMember(name, B | BindingFlags.InvokeMethod, null, instance, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object New(Type type, params object[] args)
    { return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static object Key(int width, int height, bool detail, float force) { return New(keyType, width, height, detail, force); }
    static object Maps(object key) { return New(mapsType, key, new Bitmap(2, 2), new Bitmap(2, 2)); }
    static bool EqualKey(object a, object b) { return (bool)Call(keyType, a, "Igual", b); }
    static void Wait(Func<bool> condition, string message)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.Elapsed.TotalSeconds > 4) throw new Exception(message); Thread.Sleep(3); }
    }
    static void WorkerIdle(object service)
    {
        object gate = Get(service, "puerta");
        Wait(delegate { lock (gate) return Get(service, "trabajador") == null; }, "Trabajador no termino");
    }
    static object Collect(object service, object key)
    {
        object result = null;
        Wait(delegate { result = Call(serviceType, service, "Recoge", key); return result != null; }, "No publico la ultima peticion");
        return result;
    }
    static object Service(int fakeMode)
    {
        lock (fakeLock) { calls = active = maximum = 0; mode = fakeMode; cancellationSeen = backgroundSeen = prioritySeen = false; produced.Clear(); received.Clear(); }
        entered.Reset(); release.Reset();
        object service = New(serviceType);
        FieldInfo seam = serviceType.GetField("PreparadorPrueba", B);
        ParameterExpression key = Expression.Parameter(keyType, "key"), cancel = Expression.Parameter(typeof(Func<bool>), "cancel");
        MethodInfo fake = typeof(PreparacionNaturalTest).GetMethod("Prepare", B);
        Expression body = Expression.Convert(Expression.Call(fake, Expression.Convert(key, typeof(object)), cancel), mapsType);
        seam.SetValue(service, Expression.Lambda(seam.FieldType, body, key, cancel).Compile());
        return service;
    }
    static object Prepare(object key, Func<bool> cancel)
    {
        int index;
        lock (fakeLock) { index = ++calls; received.Add(key); active++; maximum = Math.Max(maximum, active); backgroundSeen = Thread.CurrentThread.IsBackground; prioritySeen = Thread.CurrentThread.Priority == ThreadPriority.Lowest; }
        try
        {
            if (index == 1 && (mode == 1 || mode == 2)) { entered.Set(); if (!release.WaitOne(3500, false)) throw new Exception("Gate de prueba agotado"); }
            if (mode == 3 && index == 1) throw new InvalidOperationException("Fallo simulado de preparacion");
            if (mode == 4 && index == 1) return null;
            bool seen = cancel(); lock (fakeLock) cancellationSeen |= seen;
            object result = Maps(key); lock (fakeLock) produced.Add(result); return result;
        }
        finally { lock (fakeLock) active--; }
    }
    static int Count() { lock (fakeLock) return calls; }
    static void Keys()
    {
        float fallback = (float)formType.GetField("NebNaturalDef", B).GetRawConstantValue();
        foreach (float force in new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check((float)Get(Key(12, 8, true, force), "Fuerza") == fallback, "Clave no normaliza fuerza no finita");
        Check((float)Get(Key(12, 8, true, -1), "Fuerza") == 0 && (float)Get(Key(12, 8, true, 2), "Fuerza") == 1, "Fuerza fuera de rango");
        object key = Key(12, 8, true, .21f);
        foreach (string field in new string[] { "Ancho", "Alto", "Detalle", "Fuerza" }) Check(keyType.GetField(field, B).IsInitOnly, "Clave mutable: " + field);
        Check(EqualKey(key, Key(12, 8, true, .21f)), "Claves equivalentes difieren");
        foreach (object other in new object[] { Key(13, 8, true, .21f), Key(12, 9, true, .21f), Key(12, 8, false, .21f), Key(12, 8, true, .22f), null })
            Check(!EqualKey(key, other), "Clave ignora un parametro");
    }
    static void Coalescing()
    {
        object service = Service(1), first = Key(80, 50, true, .2f), last = null;
        object adopted = null;
        try
        {
            Call(serviceType, service, "Solicita", first); Check(entered.WaitOne(1500, false), "Trabajador no empieza");
            Stopwatch clock = Stopwatch.StartNew();
            for (int i = 0; i < 80; i++) { last = Key(81 + i, 51 + i, i % 2 == 0, i / 100f); Call(serviceType, service, "Solicita", last); }
            clock.Stop(); Check(clock.Elapsed.TotalMilliseconds < 250, "Solicita bloquea esperando trabajo anterior");
            Check(Count() == 1, "Se iniciaron trabajadores o trabajos pendientes en paralelo");
            release.Set(); adopted = Collect(service, last); WorkerIdle(service);
            Check(Count() == 2 && maximum == 1, "No agrupa solicitudes a un unico trabajador y ultimo pedido");
            Check(backgroundSeen && prioritySeen, "Trabajador no tiene prioridad minima/background");
            Check(cancellationSeen, "Pedido anterior no observa cancelacion");
            Check(EqualKey(Get(adopted, "Clave"), last), "Publico pedido obsoleto");
            Check(Call(serviceType, service, "Recoge", first) == null, "Recoge publica clave antigua");
            lock (fakeLock) Check(Get(produced[0], "Nebulosa") == null && Get(produced[0], "Via") == null, "Resultado obsoleto no fue liberado");
            Call(serviceType, service, "Solicita", last); WorkerIdle(service);
            Check(Count() == 2 && Call(serviceType, service, "Recoge", last) == null, "Clave ya entregada vuelve a generar mapas");
            Call(serviceType, service, "Dispose");
            Check(Get(adopted, "Nebulosa") != null && Get(adopted, "Via") != null, "Servicio libera propiedad transferida al consumidor");
        }
        finally { release.Set(); Call(serviceType, service, "Dispose"); WorkerIdle(service); if (adopted != null) Call(mapsType, adopted, "Dispose"); }
    }
    static void Closing()
    {
        object service = Service(2), key = Key(160, 90, true, .55f);
        try
        {
            Call(serviceType, service, "Solicita", key); Check(entered.WaitOne(1500, false), "No comenzo trabajo al cerrar");
            Stopwatch clock = Stopwatch.StartNew(); Call(serviceType, service, "Dispose"); clock.Stop();
            Check(clock.Elapsed.TotalMilliseconds < 250, "Dispose espera al trabajador y bloquea ventana");
            Call(serviceType, service, "Solicita", Key(170, 100, false, .1f)); release.Set(); WorkerIdle(service);
            Check(Count() == 1 && cancellationSeen, "Cerrar permite trabajo nuevo/no cancela el activo");
            Check(Call(serviceType, service, "Recoge", key) == null, "Publica mapas tras cerrar");
            lock (fakeLock) Check(Get(produced[0], "Nebulosa") == null && Get(produced[0], "Via") == null, "Resultado al cerrar queda vivo");
        }
        finally { release.Set(); Call(serviceType, service, "Dispose"); WorkerIdle(service); }
        service = Service(0); key = Key(25, 15, true, .7f);
        try
        {
            Call(serviceType, service, "Solicita", key); WorkerIdle(service);
            Check(Call(serviceType, service, "Recoge", Key(26, 15, true, .7f)) == null, "Recoge clave incorrecta");
            Check(Get(service, "listos") != null, "Consulta incorrecta elimina resultado valido");
            Call(serviceType, service, "Dispose");
            lock (fakeLock) Check(Get(produced[0], "Nebulosa") == null && Get(produced[0], "Via") == null, "Dispose no libera resultado no recogido");
        }
        finally { Call(serviceType, service, "Dispose"); WorkerIdle(service); }
    }
    static void Retry(int fakeMode)
    {
        object service = Service(fakeMode), key = Key(80, 40, true, .4f), result = null;
        try
        {
            Call(serviceType, service, "Solicita", key); WorkerIdle(service);
            Check(Count() == 1 && Call(serviceType, service, "Recoge", key) == null, "Fallo entrega resultado");
            for (int i = 0; i < 100; i++) Call(serviceType, service, "Solicita", key);
            WorkerIdle(service); Check(Count() == 1, "Fallo reintenta por cuadro sin backoff");
            Set(service, "falloMs", unchecked(Environment.TickCount - 1001));
            Call(serviceType, service, "Solicita", key); result = Collect(service, key); WorkerIdle(service);
            Check(Count() == 2 && EqualKey(Get(result, "Clave"), key), "Fallo queda sin reintento despues del backoff");
        }
        finally { Call(serviceType, service, "Dispose"); WorkerIdle(service); if (result != null) Call(mapsType, result, "Dispose"); }
    }
    static ulong Hash(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int[] row = new int[bitmap.Width]; ulong hash = 14695981039346656037UL;
            for (int y = 0; y < bitmap.Height; y++) { Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + y * (long)data.Stride), row, 0, row.Length); foreach (int p in row) { hash ^= unchecked((uint)p); hash *= 1099511628211UL; } }
            return hash;
        }
        finally { bitmap.UnlockBits(data); }
    }
    static void Builders()
    {
        FieldInfo force = formType.GetField("nebNaturalFuerza", B); object saved = force.GetValue(null);
        try
        {
            foreach (bool detail in new bool[] { false, true }) foreach (float captured in new float[] { 0, .37f, 1 })
            {
                force.SetValue(null, captured);
                using (Bitmap expected = (Bitmap)Call(gpuType, null, "BakeNebulosaNatural", 72, 48, detail))
                {
                    force.SetValue(null, captured == 1 ? 0f : 1f);
                    Func<bool> change = delegate { force.SetValue(null, .92f); return false; };
                    foreach (bool slow in new bool[] { false, true })
                        using (Bitmap result = (Bitmap)Call(gpuType, null, "BakeNebulosaPreparada", 72, 48, detail, captured, change, slow))
                            Check(Hash(expected) == Hash(result), "Preparacion ignora snapshot fuerza o pausas alteran pixeles");
                }
            }
            using (Bitmap expected = (Bitmap)Call(gpuType, null, "BakeViaNatural", 72, 48))
                foreach (bool slow in new bool[] { false, true })
                    using (Bitmap result = (Bitmap)Call(gpuType, null, "BakeViaPreparada", 72, 48, null, slow))
                        Check(Hash(expected) == Hash(result), "Via preparada altera pixeles");
            foreach (bool slow in new bool[] { false, true })
            {
                int callsToCancel = 0; Func<bool> cancel = delegate { return ++callsToCancel >= 3; };
                Check(Call(gpuType, null, "BakeNebulosaPreparada", 860, 360, true, .55f, cancel, slow) == null && callsToCancel == 3,
                    "Nebulosa no abandona calculo cancelado por filas");
                callsToCancel = 0;
                Check(Call(gpuType, null, "BakeViaPreparada", 860, 360, cancel, slow) == null && callsToCancel == 3,
                    "Via no abandona calculo cancelado por filas");
            }
            object realService = New(serviceType), key = Key(128, 80, true, .33f), maps = null;
            try
            {
                force.SetValue(null, .95f); Call(serviceType, realService, "Solicita", key); maps = Collect(realService, key); WorkerIdle(realService);
                using (Bitmap expected = (Bitmap)Call(gpuType, null, "BakeNebulosaPreparada", 128, 80, true, .33f, null, false))
                    Check(Hash(expected) == Hash((Bitmap)Get(maps, "Nebulosa")), "Worker real no usa fuerza capturada");
                Check(((Bitmap)Get(maps, "Via")).Size == new Size(128, 80), "Worker real publica dimensiones incorrectas");
            }
            finally { Call(serviceType, realService, "Dispose"); WorkerIdle(realService); if (maps != null) Call(mapsType, maps, "Dispose"); }
        }
        finally { force.SetValue(null, saved); }
    }
    static void NebOnly()
    {
        object service = New(serviceType), key = New(keyType, 128, 80, true, .33f, false), maps = null;
        try
        {
            Call(serviceType, service, "Solicita", key); maps = Collect(service, key); WorkerIdle(service);
            Check(Get(maps, "Nebulosa") != null && Get(maps, "Via") == null, "Nebonly rehornea via innecesariamente");
            Call(mapsType, maps, "Dispose"); maps = null;
            object full = New(keyType, 128, 80, true, .33f, true);
            Check(!EqualKey(key, full), "Clave ignora necesidad de via y suprime preparacion completa");
            Call(serviceType, service, "Solicita", full); maps = Collect(service, full); WorkerIdle(service);
            Check(Get(maps, "Nebulosa") != null && Get(maps, "Via") != null, "Pedido completo tras nebonly queda bloqueado");
        }
        finally { Call(serviceType, service, "Dispose"); WorkerIdle(service); if (maps != null) Call(mapsType, maps, "Dispose"); }
    }
    static void PreparePanel(Control panel)
    {
        Wait(delegate { Call(gpuType, panel, "PreparaFondoNatural"); return (bool)Call(gpuType, panel, "MapasNaturalesListos"); }, "Panel no adopta mapas de la ultima clave");
    }
    static void PanelPreparation()
    {
        FieldInfo force = formType.GetField("nebNaturalFuerza", B); object saved = force.GetValue(null);
        try
        {
            using (Control panel = (Control)New(gpuType))
            {
                panel.Size = new Size(1024, 768); Set(panel, "FondoNatural", true); Set(panel, "Nieblas", 2); Set(panel, "CieloProfundo", true);
                force.SetValue(null, .33f); PreparePanel(panel);
                Bitmap neb = ((Bitmap[])Get(panel, "mapasNebEstilo"))[1], via = ((Bitmap[])Get(panel, "mapasViaEstilo"))[1];
                force.SetValue(null, .66f); PreparePanel(panel);
                Check(!ReferenceEquals(neb, ((Bitmap[])Get(panel, "mapasNebEstilo"))[1]), "Fuerza nueva no renueva nebulosa asincrona");
                Check(ReferenceEquals(via, ((Bitmap[])Get(panel, "mapasViaEstilo"))[1]), "Fuerza nueva vuelve a construir via");
                neb = ((Bitmap[])Get(panel, "mapasNebEstilo"))[1]; Set(panel, "CieloProfundo", false); PreparePanel(panel);
                Check(!ReferenceEquals(neb, ((Bitmap[])Get(panel, "mapasNebEstilo"))[1]), "Detalle nuevo no cambia nebulosa asincrona");
                Check(ReferenceEquals(via, ((Bitmap[])Get(panel, "mapasViaEstilo"))[1]), "Detalle nuevo vuelve a construir via");
                panel.Size = new Size(960, 640); PreparePanel(panel);
                Check(!ReferenceEquals(via, ((Bitmap[])Get(panel, "mapasViaEstilo"))[1])
                    && ((Bitmap[])Get(panel, "mapasViaEstilo"))[1].Size == new Size(240, 160), "Resize conserva via de tamano incorrecto");
                object oldService = Get(panel, "preparacionNatural"); Call(serviceType, oldService, "Dispose"); WorkerIdle(oldService);
                object fake = Service(2); Set(panel, "preparacionNatural", fake);
                force.SetValue(null, .91f); Call(gpuType, panel, "PreparaFondoNatural");
                Check(entered.WaitOne(1500, false), "Panel no comienza preparacion pendiente");
                Stopwatch clock = Stopwatch.StartNew(); panel.Dispose(); clock.Stop();
                Check(clock.Elapsed.TotalMilliseconds < 250, "Cerrar panel espera al trabajador GPU");
                release.Set(); WorkerIdle(fake);
                foreach (Bitmap map in (Bitmap[])Get(panel, "mapasNebEstilo")) Check(map == null, "Cerrar panel retiene cache nebulosa");
                foreach (Bitmap map in (Bitmap[])Get(panel, "mapasViaEstilo")) Check(map == null, "Cerrar panel retiene cache via");
                lock (fakeLock) Check(Get(produced[0], "Nebulosa") == null && Get(produced[0], "Via") == null, "Panel cerrado conserva resultado trabajador");
            }
        }
        finally { release.Set(); force.SetValue(null, saved); }
    }
    [STAThread] static int Main(string[] args)
    {
        IntPtr originalAffinity = IntPtr.Zero;
        try
        {
            if (Array.IndexOf(args, "--dos-hilos") >= 0)
            {
                Process process = Process.GetCurrentProcess();
                originalAffinity = process.ProcessorAffinity;
                long allowed = originalAffinity.ToInt64(), chosen = 0; int count = 0;
                for (int bit = 0; bit < IntPtr.Size * 8 && count < 2; bit++)
                {
                    long mask = 1L << bit;
                    if ((allowed & mask) != 0) { chosen |= mask; count++; }
                }
                if (count < 1) throw new Exception("Sin afinidad disponible para prueba de dos hilos");
                process.ProcessorAffinity = new IntPtr(chosen);
            }
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            serviceType = assembly.GetType("Rocola.PreparacionFondoNatural", false);
            if (serviceType == null) { Console.WriteLine("OK CPU congelado: no existe preparador de mapas GPU."); return 0; }
            keyType = serviceType.GetNestedType("Pedido", B); mapsType = serviceType.GetNestedType("Mapas", B);
            gpuType = assembly.GetType("Rocola.GpuPanel", true); formType = assembly.GetType("Rocola.Form1", true);
            if (originalAffinity != IntPtr.Zero)
            {
                Type workload = assembly.GetType("Rocola.CargaTrabajo", true);
                int processors = (int)workload.GetField("Procesadores", B).GetValue(null);
                Check(processors >= 1 && processors <= 2, "Preparador no detecta afinidad de dos hilos");
                Console.WriteLine("Afinidad aislada del proceso de pruebas: " + processors + " hilos; no simula un procesador antiguo.");
            }
            Keys(); Coalescing(); Closing(); Retry(3); Retry(4); Builders(); NebOnly(); PanelPreparation();
            Console.WriteLine("PASS " + checks + " comprobaciones: snapshot, unico worker, coalescing, propiedad, cancelacion, cierre, backoff y builders identicos."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e); return 1; }
        finally {
            release.Set(); entered.Close(); release.Close();
            if (originalAffinity != IntPtr.Zero) Process.GetCurrentProcess().ProcessorAffinity = originalAffinity;
        }
    }
}

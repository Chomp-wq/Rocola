using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// LA TANDA DE RENDIMIENTO DEL 24-10-2026, con el diagnostico completo del A4 (Windows XP, dos nucleos):
//   1) mientras alguien elige (un boton hace menos de 10 s, o una escena del diagnostico) y fuera de la atraccion, el recolector va
//      en baja latencia (sin recolecciones de generacion 2); al dejar de elegir vuelve al modo de antes; con tope de 5 minutos y de
//      128 MB de crecimiento, y entonces no vuelve hasta que se deje de elegir. Se pide desde el latido.
//   2) al cambiar de medida la imagen de la interfaz (navegar <-> disco o buscador) se toma la de la otra medida en vez de reservar
//      otra: ir y volver no reserva imagenes nuevas, y cada vez se repinta entera.
//   3) publicar la biblioteca apunta lo que tarda cada paso si entre todos pasan de medio segundo;
//   4) el diagnostico completo dice, de cada tiron, lo ultimo que marco la pantalla en el hueco (sin contar la marca del propio cuadro).
// Mono no aplica la baja latencia del recolector (la deja en Interactive): aqui se mira la logica y que se pida, no su efecto. Eso
// solo se ve en el A4, con el diagnostico completo.
class Recolector
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static Type TF;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Toma(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); } throw new MissingFieldException(n); }
    static void Pon(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(f.IsStatic ? null : o, v); return; } } throw new MissingFieldException(n); }
    static object Llama(object o, string n, params object[] a) { MethodInfo m = TF.GetMethod(n, T); try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static int Estatico(string n) { return (int)TF.GetField(n, T).GetValue(null); }
    static void TamanoForm(object f, int w, int h)
    {
        BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo cs = typeof(Control).GetField("client_size", F), cr = typeof(Control).GetField("client_rect", F), bo = typeof(Control).GetField("bounds", F);
        if (cs != null) cs.SetValue(f, new Size(w, h));
        if (cr != null) cr.SetValue(f, new Rectangle(0, 0, w, h));
        if (bo != null) bo.SetValue(f, new Rectangle(0, 0, w, h));
    }

    static void ElRecolector()
    {
        Console.WriteLine("\n  1) MIENTRAS ALGUIEN ELIGE, SIN RECOLECCIONES GRANDES\n");
        object f = Codigo.FormSinEncender(TF);
        Pon(f, "attractOn", false); Pon(f, "diagEnMarcha", false);
        int p0 = Estatico("GcBajasPuestas"), q0 = Estatico("GcBajasQuitadas");
        Pon(f, "lastInputTick", 0);
        Llama(f, "RevisaRecolector");
        Check(!(bool)Toma(f, "gcBaja") && Estatico("GcBajasPuestas") == p0, "sin ningun boton tocado, nada");
        Pon(f, "lastInputTick", Environment.TickCount);
        Llama(f, "RevisaRecolector");
        Check((bool)Toma(f, "gcBaja") && Estatico("GcBajasPuestas") == p0 + 1, "un boton recien tocado: se pide la baja latencia");
        Llama(f, "RevisaRecolector");
        Check(Estatico("GcBajasPuestas") == p0 + 1, "y no se vuelve a pedir en cada vuelta del latido");
        Pon(f, "lastInputTick", Environment.TickCount - 20000);
        Llama(f, "RevisaRecolector");
        Check(!(bool)Toma(f, "gcBaja") && Estatico("GcBajasQuitadas") == q0 + 1, "a los 10 s sin tocar nada se vuelve al modo de antes");
        Pon(f, "lastInputTick", Environment.TickCount); Pon(f, "attractOn", true);
        Llama(f, "RevisaRecolector");
        Check(!(bool)Toma(f, "gcBaja"), "en la atraccion, no (nadie elige aunque se acabe de tocar)");
        Pon(f, "attractOn", false); Pon(f, "lastInputTick", 0); Pon(f, "diagEnMarcha", true);
        Llama(f, "RevisaRecolector");
        Check((bool)Toma(f, "gcBaja"), "una escena del diagnostico completo cuenta como alguien eligiendo (asi el diagnostico lo mide)");
        // El tope de tiempo.
        int maxMs = Estatico("GcBajaMaxMs");
        Pon(f, "gcBajaDesde", Environment.TickCount - maxMs - 1000);
        Llama(f, "RevisaRecolector");
        Check(!(bool)Toma(f, "gcBaja") && (bool)Toma(f, "gcAgotada"), "eligiendo mas de " + (maxMs / 60000) + " minutos seguidos se vuelve al modo de antes");
        Llama(f, "RevisaRecolector");
        Check(!(bool)Toma(f, "gcBaja"), "y no se vuelve a pedir mientras se siga eligiendo");
        Pon(f, "diagEnMarcha", false);
        Llama(f, "RevisaRecolector");
        Check(!(bool)Toma(f, "gcAgotada"), "al dejar de elegir el tope se olvida");
        // El tope de memoria.
        Pon(f, "diagEnMarcha", true);
        Llama(f, "RevisaRecolector");
        long crecer = (long)TF.GetField("GcBajaCrecerBytes", T).GetValue(null);
        Pon(f, "gcBajaMemoria", GC.GetTotalMemory(false) - crecer - (1L << 20));
        Llama(f, "RevisaRecolector");
        Check((bool)Toma(f, "gcAgotada") && !(bool)Toma(f, "gcBaja"), "si el monton crece mas de " + (crecer >> 20) + " MB en baja latencia, se vuelve al modo de antes");
        Pon(f, "diagEnMarcha", false); Llama(f, "RevisaRecolector");
        // Enchufado.
        Check(Codigo.Llama(TF.GetMethod("AnimTimer_Tick", T), "RevisaRecolector"), "lo revisa el latido de la pantalla");
        Check(Codigo.Llama(TF.GetMethod("PonLatenciaGcDirecto", T), "set_LatencyMode") && Codigo.Llama(TF.GetMethod("LeeLatenciaGcDirecto", T), "get_LatencyMode"),
              "y lo pide de verdad a .NET (GCSettings.LatencyMode), en un metodo aparte: en un .NET sin ese modo la llamada se atrapa fuera");
        Check(Codigo.Llama(TF.GetMethod("PonLatenciaGc", T), "PonLatenciaGcDirecto") && Codigo.Llama(TF.GetMethod("LeeLatenciaGc", T), "LeeLatenciaGcDirecto"), "(el de fuera llama al de dentro)");
        Console.WriteLine("        (Mono no aplica la baja latencia: queda en " + System.Runtime.GCSettings.LatencyMode + ". El efecto solo se ve en el A4.)");
    }

    static void LaImagenDeLaInterfaz()
    {
        Console.WriteLine("\n  2) LA IMAGEN DE LA INTERFAZ AL CAMBIAR DE MEDIDA\n");
        if (TF.GetMethod("EnsureUiOverlay", T) == null) { Check(false, "falta EnsureUiOverlay: esta bateria va con el perfil de tarjeta grafica"); return; }
        object f = Codigo.FormSinEncender(TF);
        Pon(f, "attractOn", false);
        TamanoForm(f, 1024, 768); Llama(f, "EnsureUiOverlay");
        Bitmap a = (Bitmap)Toma(f, "uiOverlay");
        TamanoForm(f, 1000, 768); int v1 = (int)Llama(f, "EnsureUiOverlay");
        Bitmap b = (Bitmap)Toma(f, "uiOverlay");
        Check(a != null && b != null && !object.ReferenceEquals(a, b) && b.Width == 1000, "otra medida: otra imagen (" + (a == null ? "?" : a.Width + "x" + a.Height) + " y " + (b == null ? "?" : b.Width + "x" + b.Height) + ")");
        bool misma = true, versiones = true; int v = v1;
        for (int k = 0; k < 6; k++)
        {
            TamanoForm(f, k % 2 == 0 ? 1024 : 1000, 768);
            int nv = (int)Llama(f, "EnsureUiOverlay");
            Bitmap c = (Bitmap)Toma(f, "uiOverlay");
            if (!object.ReferenceEquals(c, k % 2 == 0 ? a : b)) misma = false;
            if (nv == v) versiones = false;
            v = nv;
        }
        Check(misma, "ir y volver seis veces: siempre una de las dos imagenes, ninguna nueva (antes, una por vuelta)");
        Check(versiones, "y cada vuelta es una version nueva: la tarjeta la vuelve a subir entera");
        Check(Codigo.Llama(TF.GetMethod("EnsureUiOverlay", T), "Clear"), "(la imagen se sigue limpiando y componiendo entera)");
    }

    static void LaBiblioteca()
    {
        Console.WriteLine("\n  3) PUBLICAR LA BIBLIOTECA: LO QUE TARDA CADA PASO\n");
        Type tp = TF.GetNestedType("PasosPublicar", T);
        object p = Activator.CreateInstance(tp, true);
        MethodInfo paso = tp.GetMethod("Paso", T), fin = tp.GetMethod("Fin", T);
        paso.Invoke(p, new object[] { "uno" }); Thread.Sleep(40);
        paso.Invoke(p, new object[] { "dos" }); Thread.Sleep(25);
        string corto = (string)fin.Invoke(p, new object[] { 100000 });
        string d = (string)fin.Invoke(p, new object[] { 10 });
        Check(corto == null, "si entre todos no pasan del umbral, nada que apuntar");
        Check(d != null && d.Contains("uno ") && d.Contains("dos ") && d.Contains(" ms: "), "si pasan, lo de cada paso: " + d);
        MethodInfo pub = TF.GetMethod("Scanner_RunWorkerCompleted", T);
        List<string> ll = Codigo.Llamadas(pub);
        int pasos = 0; foreach (string s in ll) if (s.EndsWith("Paso")) pasos++;
        Check(pasos >= 10 && Codigo.Llama(pub, "Fin") && Codigo.Llama(pub, "LogError"), "publicar la biblioteca marca " + pasos + " pasos y, si tarda, lo apunta en rocola_error.txt");
        Check(Codigo.Llama(tp.GetMethod("Paso", T), "Hace"), "y cada paso marca al vigia de la pantalla con su nombre");
    }

    static void LaMarcaDelHueco(Assembly asm)
    {
        Console.WriteLine("\n  4) LO QUE MARCO LA PANTALLA EN EL HUECO DE UN TIRON\n");
        Type tv = asm.GetType("Rocola.VigiaPantalla", true);
        MethodInfo hace = tv.GetMethod("Hace", T), entre = tv.GetMethod("MarcaEntre", T);
        string cuadro = (string)tv.GetField("MarcaCuadro", T).GetValue(null);
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        hace.Invoke(null, new object[] { "una tecla (prueba)" }); Thread.Sleep(2);
        hace.Invoke(null, new object[] { "el latido (prueba)" }); Thread.Sleep(2);
        long antesDelCuadro = System.Diagnostics.Stopwatch.GetTimestamp();
        hace.Invoke(null, new object[] { cuadro });
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        object[] a = { t0, t1, cuadro, 0L };
        string m = (string)entre.Invoke(null, a);
        Check(m == "el latido (prueba)" && (long)a[3] <= antesDelCuadro, "la ultima marca del hueco, sin contar la del propio cuadro: «" + m + "»");
        object[] b = { antesDelCuadro, t1, cuadro, 0L };
        Check(entre.Invoke(null, b) == null, "y si en el hueco no se marco nada, nada");
        Check(Codigo.Llama(TF.Assembly.GetType("Rocola.DiagCaptura", false) != null ? TF.Assembly.GetType("Rocola.DiagCaptura").GetMethod("Cuadro", T) : null, "MarcaEntre"),
              "el diagnostico completo lo pregunta en cada tiron");
    }

    // 5) WM_PAINT NO DEJA LA X ROJA (24-10-2026). En .NET 2.0 una excepcion que se escapa de OnPaint marca el control y desde entonces
    // solo se pinta la X roja. El OnPaint del panel de la tarjeta la atrapa, como el bucle de cuadros.
    static void ElPintado(Assembly asm)
    {
        Console.WriteLine("\n  5) UN CUADRO QUE FALLA EN WM_PAINT NO SE ESCAPA\n");
        Type tp = asm.GetType("Rocola.GpuPanel", false);
        if (tp == null) { Check(false, "falta Rocola.GpuPanel"); return; }
        MethodInfo onPaint = tp.GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(System.Windows.Forms.PaintEventArgs) }, null);
        bool atrapa = false;
        if (onPaint != null && onPaint.GetMethodBody() != null)
            foreach (ExceptionHandlingClause c in onPaint.GetMethodBody().ExceptionHandlingClauses)
                if (c.Flags == ExceptionHandlingClauseOptions.Clause && c.CatchType == typeof(Exception)) atrapa = true;
        Check(atrapa, "OnPaint del panel envuelve el cuadro en un try/catch");
        // Y de verdad: un panel a medio armar (sin dispositivo, sin nada) hace tronar el cuadro; no debe salir de OnPaint.
        object p = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp);
        MethodInfo render = tp.GetMethod("RenderFrame", T);
        bool truena = false;
        try { render.Invoke(p, null); } catch (TargetInvocationException) { truena = true; }
        bool sale = false;
        try { onPaint.Invoke(p, new object[] { null }); } catch (TargetInvocationException) { sale = true; }
        Check(truena && !sale, "un cuadro que truena (" + (truena ? "truena" : "no truena") + ") no sale de OnPaint" + (sale ? ": SALIO" : ""));
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        ElRecolector();
        LaImagenDeLaInterfaz();
        LaBiblioteca();
        LaMarcaDelHueco(asm);
        ElPintado(asm);
        Console.WriteLine(fallos == 0 ? "\n  OK recolector: " + oks + " comprobaciones (logica; el efecto en el A4 lo dice el diagnostico completo)" : "\n  FALLO recolector: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}

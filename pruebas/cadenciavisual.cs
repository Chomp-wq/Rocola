using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

class CadenciaVisualTest
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly app;
    static int checks;
    static Type T(string name) { return app.GetType("Rocola." + name, true); }
    static object New(Type t) { return Activator.CreateInstance(t, true); }
    static object Get(object obj, string name) { return obj.GetType().GetField(name, F).GetValue(obj); }
    static void Set(object obj, string name, object value) { obj.GetType().GetField(name, F).SetValue(obj, value); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, F).Invoke(obj, args); }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static bool Near(double a, double b) { return Math.Abs(a - b) < 0.00001; }
    static object RefCall(object obj, string method, object state, params object[] tail)
    {
        object[] args = new object[tail.Length + 1]; args[0] = state;
        Array.Copy(tail, 0, args, 1, tail.Length); Call(obj, method, args); return args[0];
    }
    static void Relojes()
    {
        Type clock = T("CadenciaVisual");
        foreach (int fps in new int[] { 24, 50, 60 })
        foreach (int screen in new int[] { 30, 60, 75, 144, 240 })
        {
            object c = New(clock); int updates = 0; double previous = -1;
            for (int i = 0; i <= screen * 8; i++)
            {
                double now = i * 1000.0 / screen;
                bool update = (bool)Call(c, "Step", now, fps, false);
                double sample = (double)Get(c, "SampleMs");
                Check(sample + .00001 >= previous && sample <= now + .00001, "Reloj retrocede o predice el futuro");
                if (update) updates++;
                previous = sample;
            }
            Check(updates == Math.Min(screen, fps) * 8 + 1, "Cadencia incorrecta: " + fps + "/" + screen + " -> " + updates);
            Check(!(bool)Call(c, "Step", 8000.0, fps, false), "Repite una muestra en el mismo instante");
            Check((bool)Call(c, "Step", 8000.0, fps, true), "No acepta invalidacion inmediata");
            Check((bool)Call(c, "Step", 8250.0, fps, false), "No salta cuadros perdidos");
            Check(!(bool)Call(c, "Step", 8250.0, fps, false), "Reproduce atrasos en rafaga");
            Check((bool)Call(c, "Step", 8251.0, fps == 24 ? 60 : 24, false), "Cambio de ajuste tarda");
            Check((bool)Call(c, "Step", 10000.0, fps, false), "Reanudacion no reinicia reloj");
            Check((bool)Call(c, "Step", 10.0, fps, false), "Reloj atrasado no reinicia");
            Check(!(bool)Call(c, "Step", double.NaN, fps, false), "NaN aceptado");
            Check(!(bool)Call(c, "Step", double.PositiveInfinity, fps, false), "Infinito aceptado");
            Check(Near((double)Get(c, "SampleMs"), 10), "Entrada invalida corrompe reloj");
        }
    }
    static object Cube(Type type, object key, Bitmap cover, double angle, float x)
    {
        object c = New(type); Set(c, "Has", true); Set(c, "Key", key); Set(c, "CoverA", cover);
        Set(c, "Angle", angle); Set(c, "Tilt", (float)(angle / 10)); Set(c, "X", x); Set(c, "Fade", x / 100);
        return c;
    }
    static void Cubos()
    {
        Type panelType = T("GpuPanel"), cubeType = T("GpuCubeInfo"), flightType = T("GpuTrans");
        using (Control panel = (Control)New(panelType))
        using (Bitmap cover = new Bitmap(8, 8))
        using (Bitmap replacement = new Bitmap(8, 8))
        {
            for (int slot = 0; slot < 3; slot++)
            {
                object key = new object(); Set(panel, "cubeStep", false);
                object c = RefCall(panel, "SampleCubePose", Cube(cubeType, key, cover, 1, 10), slot);
                Check(Near((double)Get(c, "Angle"), 1), "Primer cubo no aparece");
                c = RefCall(panel, "SampleCubePose", Cube(cubeType, key, replacement, 2, 20), slot);
                Check(Near((double)Get(c, "Angle"), 1) && Near((float)Get(c, "Tilt"), .1f), "Pose entre muestras no se conserva");
                Check(Near((float)Get(c, "X"), 20) && Near((float)Get(c, "Fade"), .2f) && ReferenceEquals(Get(c, "CoverA"), replacement), "Cadencia demora posicion/portada");
                Set(panel, "cubeStep", true);
                c = RefCall(panel, "SampleCubePose", Cube(cubeType, key, replacement, 3, 20), slot);
                Check(Near((double)Get(c, "Angle"), 3), "Muestra del cubo no avanza");
                Set(panel, "cubeStep", false);
                c = RefCall(panel, "SampleCubePose", Cube(cubeType, new object(), replacement, 0, 20), slot);
                Check(Near((double)Get(c, "Angle"), 0), "Seleccion nueva hereda angulo viejo");
                object empty = RefCall(panel, "SampleCubePose", New(cubeType), slot);
                Check(!(bool)Get(empty, "Has"), "Cubo oculto sigue dibujado");
                c = RefCall(panel, "SampleCubePose", Cube(cubeType, key, cover, 4, 10), slot);
                Check(Near((double)Get(c, "Angle"), 4), "Cubo reanudado hereda muestra vieja");
            }
            object f = New(flightType); Set(f, "Has", true); Set(f, "Key", 1); Set(f, "Bmp", cover);
            Set(f, "X", 1f); Set(f, "Angle", 1.0); Set(f, "Alpha", 1f);
            RefCall(panel, "SampleCubeFlight", f);
            Set(f, "X", 2f); Set(f, "Angle", 2.0); Set(f, "Alpha", .5f);
            object hold = RefCall(panel, "SampleCubeFlight", f);
            Check(Near((float)Get(hold, "X"), 1) && Near((double)Get(hold, "Angle"), 1) && Near((float)Get(hold, "Alpha"), 1), "Vuelo entre muestras no se conserva");
            Set(f, "Key", 2);
            hold = RefCall(panel, "SampleCubeFlight", f);
            Check(Near((float)Get(hold, "X"), 2), "Vuelo interrumpido con igual portada usa pose vieja");
            Set(f, "Bmp", replacement); Set(f, "X", 3f);
            hold = RefCall(panel, "SampleCubeFlight", f);
            Check(Near((float)Get(hold, "X"), 3), "Vuelo con portada nueva tarda");
            RefCall(panel, "SampleCubeFlight", New(flightType)); Set(f, "X", 4f);
            hold = RefCall(panel, "SampleCubeFlight", f);
            Check(Near((float)Get(hold, "X"), 4), "Vuelo nuevo usa muestra vieja");
        }
    }
    static object Wave(Type type, float[] bands, float baseY)
    {
        object w = New(type); Set(w, "Has", true); Set(w, "B", bands); Set(w, "BaseY", baseY); Set(w, "Alto", 200f);
        return w;
    }
    static void Ondas()
    {
        foreach (int fps in new int[] { 24, 50, 60 })
        using (Control panel = (Control)New(T("GpuPanel")))
        {
            panel.Size = new Size(1024, 768); Set(panel, "OndasFps", fps);
            Type type = T("GpuAurora");
            float[] source = { .1f, .2f, float.NaN, float.PositiveInfinity, -1, 2, .6f, .7f };
            object first = Wave(type, source, 500);
            object[] args = { first, 0.0 };
            bool changed = (bool)Call(panel, "SampleWaveAt", args); first = args[0];
            float[] own = (float[])Get(first, "B");
            Check(changed && !ReferenceEquals(source, own), "Bandas no tienen copia propia");
            Check(Near(own[0], .1f) && own[2] == 0 && own[3] == 0 && own[4] == 0 && own[5] == 1, "Bandas invalidas no acotadas");
            source[0] = 1;
            args = new object[] { Wave(type, source, 450), 1000.0 / fps - .01 };
            changed = (bool)Call(panel, "SampleWaveAt", args); object hold = args[0];
            Check(!changed && Near(((float[])Get(hold, "B"))[0], .1f), "Ondas cambian antes de cadencia");
            Check(Near((float)Get(hold, "BaseY"), 450), "Ondas se separan de la barra entre muestras");
            args = new object[] { Wave(type, source, 440), 1000.0 / fps };
            changed = (bool)Call(panel, "SampleWaveAt", args);
            Check(changed && Near(((float[])Get(args[0], "B"))[0], 1), "Ondas no avanzan al plazo");
            panel.Width++;
            args = new object[] { Wave(type, source, 430), 1000.0 / fps + .1 };
            Check((bool)Call(panel, "SampleWaveAt", args), "Cambio de resolucion no invalida ondas");
            Set(panel, "OndasSuaves", !(bool)Get(panel, "OndasSuaves"));
            args = new object[] { Wave(type, source, 420), 1000.0 / fps + .2 };
            Check((bool)Call(panel, "SampleWaveAt", args), "Cambio de estilo tarda");
            args = new object[] { New(type), 1000.0 / fps + .3 };
            Check((bool)Call(panel, "SampleWaveAt", args) && !(bool)Get(args[0], "Has"), "Onda desactivada no desaparece");
        }
    }
    static void Velocidad()
    {
        Type form = T("Form1"), album = form.GetNestedType("AlbumView", F), cube = form.GetNestedType("CubeBox", F);
        foreach (int fps in new int[] { 24, 50, 60 })
        using (Control view = (Control)New(album))
        using (Control box = (Control)New(cube))
        {
            Call(view, "AvanceGiroGpu", 0.0);
            for (int i = 0; i < fps; i++) {
                Call(view, "AvanceGiroGpu", 1000.0 / fps / 42);
                Call(box, "AvanzaGpu", .06 * 1000.0 / fps / 42, .045 * (i + 1) * 1000.0 / fps / 42, (i + 1) * 1000.0 / fps);
            }
            Check(Near((double)Get(view, "SelAngle"), .06 * 1000 / 42), "FPS altera velocidad de giro de pared");
            Check(Near((double)Get(box, "Angle"), .06 * 1000 / 42), "FPS altera velocidad de cubo grande/mini");
            Check(Near((double)Get(box, "Phase"), .045 * 1000 / 42), "FPS altera bamboleo");
            double angle = (double)Get(view, "SelAngle");
            Call(view, "AvanceGiroGpu", double.NaN); Call(view, "AvanceGiroGpu", double.PositiveInfinity);
            Check(Near((double)Get(view, "SelAngle"), angle), "Tiempo invalido corrompe giro");
            Set(view, "Selected", 1); Set(view, "SelAngle", 0.0); Call(view, "AvanceGiroGpu", 1.0);
            Check(Near((double)Get(view, "SelAngle"), 0), "Seleccion nueva no empieza plana");
        }
        Check(Codigo.Llama(T("GpuPanel").GetMethod("RenderFrame", F), "BeginCubeFrame"), "Render no inicia reloj de cubos");
        Check(Codigo.Llama(T("GpuPanel").GetMethod("RenderFrame", F), "SampleCubePose"), "Render no muestrea cubos");
        Check(Codigo.Llama(T("GpuPanel").GetMethod("RenderFrame", F), "SampleCubeFlight"), "Render no muestrea vuelos");
        Check(Codigo.Llama(T("GpuPanel").GetMethod("DrawAurora", F), "SampleWave"), "Render no muestrea ondas");
        Check(Codigo.Llama(form.GetMethod("CubosDelCuadro", F), "AvanceGiroGpu"), "Pared no usa cadencia GPU");
        Check(Codigo.Llama(form.GetMethod("CubosDelCuadro", F), "AvanzaGpu"), "Cubos grande/mini no usan cadencia GPU");
        Check(Codigo.Llama(form.GetMethod("BuildTransState", F), "CubeFlightElapsed"), "Vuelo no usa reloj fino");
        using (Control box = (Control)New(cube))
        using (Bitmap first = new Bitmap(8, 8))
        using (Bitmap next = new Bitmap(8, 8))
        {
            Set(box, "Cover", first); Set(box, "relNueva", next);
            Set(box, "relTick", 42); Set(box, "gpuRelayTick", 42); Set(box, "gpuRelayStart", 0.0);
            Call(box, "AvanzaGpu", 0.0, 0.0, 200.0);
            double before = (double)Get(box, "Angle");
            Check(ReferenceEquals(Get(box, "Cover"), first), "Relevo cambia portada antes de media vuelta");
            Call(box, "AvanzaGpu", 0.0, 0.0, 200.0);
            Check(Near((double)Get(box, "Angle"), before) && ReferenceEquals(Get(box, "Cover"), first), "Relevo cambia entre muestras");
            Call(box, "AvanzaGpu", 0.0, 0.0, 220.0);
            Check((double)Get(box, "Angle") > Math.PI / 2 && ReferenceEquals(Get(box, "Cover"), next), "Portada y pose no cambian juntas");
            Call(box, "AvanzaGpu", 0.0, 0.0, 420.0);
            Check((int)Get(box, "relTick") == 0 && Near((double)Get(box, "Angle"), Math.PI), "Relevo no termina sin saltos");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            app = Assembly.LoadFrom(System.IO.Path.GetFullPath(args[0]));
            Relojes(); Cubos(); Ondas(); Velocidad();
            Console.WriteLine("  OK    " + checks + " comprobaciones: 24/50/60, entrada inmediata, todos los cubos, ondas unidas y reloj sin atrasos.");
            return 0;
        } catch (Exception e) { Console.WriteLine("FALLO " + e); return 1; }
    }
}

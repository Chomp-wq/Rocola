using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Regresion del reloj compartido por Legacy/Natural. No usa biblioteca ni datos del usuario.
class PausasEstrella
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type reloj, gpu;
    static int checks, failures;
    static void Check(bool ok, string text)
    { checks++; if (!ok) failures++; Console.WriteLine((ok ? "OK " : "FALLO ") + text); }
    static void Set(object o, string n, object v) { gpu.GetField(n, All).SetValue(o, v); }
    static object Get(object o, string n) { return gpu.GetField(n, All).GetValue(o); }

    static void ConsultaLenta()
    {
        FieldInfo prueba = reloj.GetField("Prueba", All);
        object original = prueba.GetValue(null);
        try
        {
            long frequency = Stopwatch.Frequency;
            MethodInfo clock = gpu.GetMethod("AhoraMs", All);
            clock.Invoke(null, null); // calentar el reloj/JIT antes de comparar los dominios
            foreach (int delay in new int[] { 0, 8, 20, 35 })
            foreach (int before in new int[] { 0, 15 })
            {
                double now = (double)clock.Invoke(null, null);
                long refresh = Stopwatch.GetTimestamp();
                prueba.SetValue(null, (Func<long[]>)delegate
                {
                    if (delay > 0) Thread.Sleep(delay);
                    return new long[] { frequency / 60, refresh };
                });
                if (before > 0) Thread.Sleep(before); // tambien una pausa del llamador antes de consultar
                object[] args = { now, 0d, 0d };
                bool ok = (bool)reloj.GetMethod("Rejilla", All).Invoke(null, args);
                double error = (double)args[1] - now;
                Check(ok && Math.Abs(error) < 2,
                    "consulta de " + delay + " ms / llamador de " + before + " ms no desplaza el reloj (error " + error.ToString("0.000") + " ms)");
            }
        }
        finally { prueba.SetValue(null, original); }
    }

    static double Vista(MethodInfo m, double now, double predicted, double period)
    { return (double)m.Invoke(null, new object[] { now, predicted, period }); }
    static void RejillaConocida(MethodInfo moment)
    {
        foreach (int hz in new int[] { 24, 50, 60, 75, 144, 180, 240 })
        {
            double period = 1000.0 / hz, predicted = 1000 + period;
            Check(Vista(moment, 1000, predicted, period) == predicted, hz + " Hz: conserva un objetivo que todavia no vence");
            bool grid = true;
            foreach (double delay in new double[] { 0, .001, period * .3, period, 2 * period + .2 })
            {
                double now = predicted + delay, result = Vista(moment, now, predicted, period);
                double steps = (result - predicted) / period;
                grid &= result > now && result - now <= period + 1e-6 && Math.Abs(steps - Math.Round(steps)) < 1e-6;
            }
            Check(grid, hz + " Hz: preparacion tardia mantiene la misma rejilla y usa el siguiente refresco");
        }
        Check(Vista(moment, 1000, -1, 0) == 1000, "sin compositor conserva el reloj normal (destino XP)");
        Check(Vista(moment, 1000, 1012, 0) == 1012, "sin periodo no inventa otro objetivo");
    }
    static void CuadroTardio()
    {
        MethodInfo moment = reloj.GetMethod("MomentoVista", All);
        Check(moment != null, "hay correccion del objetivo vencido antes de mover la estrella");
        if (moment != null) RejillaConocida(moment);
        foreach (bool natural in new bool[] { false, true })
        foreach (bool threeD in new bool[] { false, true })
        using (Control panel = (Control)Activator.CreateInstance(gpu, true))
        {
            Set(panel, "FondoNatural", natural); Set(panel, "Estrella3D", threeD);
            Set(panel, "LunaActiva", true); Set(panel, "idleBlend", 1f);
            double now = (double)gpu.GetMethod("AhoraMs", All).Invoke(null, null);
            double predicted = now - 5, period = 1000.0 / 60;
            Set(panel, "vistoMs", predicted);
            FieldInfo periodField = gpu.GetField("periodoVistaMs", All);
            if (periodField != null) periodField.SetValue(panel, period); // permite demostrar el fallo en el binario anterior
            Set(panel, "cuadroMs", predicted - period); Set(panel, "lunaClock", predicted - period);
            gpu.GetMethod("UpdateMoonMusic", All).Invoke(panel, new object[] { 0f });
            double frame = (double)Get(panel, "cuadroMs");
            double after = (double)gpu.GetMethod("AhoraMs", All).Invoke(null, null);
            double steps = (frame - predicted) / period;
            // El SO puede pausar este proceso tambien: comprobar la rejilla, no exigir terminar en 11 ms.
            Check(frame > now && frame <= after + period + 1e-6 && Math.Abs(steps - Math.Round(steps)) < 1e-6
                && (double)Get(panel, "starTravelX") > 0,
                (natural ? "Natural" : "Legacy") + (threeD ? " 3D" : " 2D") + ": la barra tardia no deja la pose en un refresco pasado");
            // Una peticion manual no hereda la rejilla del ultimo cuadro del bucle.
            Set(panel, "vistoMs", -1d);
            gpu.GetMethod("UpdateMoonMusic", All).Invoke(panel, new object[] { 0f });
            Check((double)Get(panel, "cuadroMs") >= frame, "repintado manual no hace retroceder la animacion");
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        reloj = asm.GetType("Rocola.RelojPantalla"); gpu = asm.GetType("Rocola.GpuPanel");
        ConsultaLenta(); CuadroTardio();
        Console.WriteLine("RESULTADO: " + checks + " comprobaciones, " + failures + " fallos. Reloj simulado, no FPS con biblioteca real.");
        return failures == 0 ? 0 : 1;
    }
}

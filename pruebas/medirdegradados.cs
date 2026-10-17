using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;

// Microcomparacion de preparar y liberar el pincel: no dibuja un cuadro ni mide FPS.
// Ejecutar cada version por separado, sin otras pruebas/compilaciones a la vez.
class MedirDegradados
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static int Counter(Type t, string name)
    { FieldInfo f = t.GetField(name, B); return f == null ? -1 : (int)f.GetValue(null); }
    static string N(double n) { return n.ToString("F3", CultureInfo.InvariantCulture); }
    static int Main(string[] args)
    {
        Type f = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1");
        Func<Rectangle, Color, Color, Brush> create = (Func<Rectangle, Color, Color, Brush>)
            Delegate.CreateDelegate(typeof(Func<Rectangle, Color, Color, Brush>), f.GetMethod("DegradadoV", B));
        Console.WriteLine("Linux/Mono; preparar y liberar pincel, sin pintar ni GPU. Cada muestra promedia 200 operaciones; no son latencias de cuadro.");
        foreach (int h in new int[] { 245, 327, 346 })
        {
            Rectangle area = new Rectangle(0, 0, 1920, h);
            Color a = Color.FromArgb(232, 12, 19, 42), b = Color.FromArgb(248, 7, 12, 29);
            for (int i = 0; i < 20; i++) using (Brush brush = create(area, a, b)) { }
            double[] batches = new double[25];
            int made = Counter(f, "DegradadosGuardados"), reused = Counter(f, "DegradadosReusados");
            for (int batch = 0; batch < batches.Length; batch++)
            {
                long before = Stopwatch.GetTimestamp();
                for (int i = 0; i < 200; i++) using (Brush brush = create(area, a, b)) { }
                batches[batch] = (Stopwatch.GetTimestamp() - before) * 1000000.0 / Stopwatch.Frequency / 200;
            }
            Array.Sort(batches);
            Console.WriteLine("alto=" + h + " operaciones=5000 mediana_us=" + N(batches[12]) + " p95_lote_us=" + N(batches[23])
                + " max_lote_us=" + N(batches[24]) + " horneados=" + (made < 0 ? -1 : Counter(f, "DegradadosGuardados") - made)
                + " reusos=" + (reused < 0 ? -1 : Counter(f, "DegradadosReusados") - reused));
        }
        return 0;
    }
}

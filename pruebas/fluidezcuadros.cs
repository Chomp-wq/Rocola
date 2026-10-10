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
    [STAThread] static int Main(string[] args)
    {
        try { Assembly asm = Assembly.LoadFrom(args[0]); Tiles(asm); Diagnostic(asm); Console.WriteLine("PASS " + checks + " comprobaciones: teselas exactas/progresivas y diagnostico acotado."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

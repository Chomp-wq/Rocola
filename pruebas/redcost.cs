using System;
using System.Diagnostics;
using System.Reflection;

// ¿Cuanto cuesta REALMENTE la red neuronal que ya corre en la rocola?
class RedCost
{
    static int Main(string[] a)
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        Type NE = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "NeuralEnv") NE = t;
        if (NE == null) { Console.WriteLine("no encontre NeuralEnv"); return 1; }
        NE.GetMethod("TryLoad", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
          .Invoke(null, new object[] { a.Length > 0 ? a[0] : "../e2e/rocola_ia.bin" });
        bool cargada = (bool)NE.GetField("Loaded", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
        Console.WriteLine("modelo cargado: " + cargada);
        if (!cargada) return 1;
        MethodInfo pred = NE.GetMethod("Predict", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        float[] x = new float[28], y = new float[12];
        Random r = new Random(3);
        for (int i = 0; i < 28; i++) x[i] = (float)(r.NextDouble() * 2 - 1);
        object[] args = { x, y };
        for (int i = 0; i < 20000; i++) pred.Invoke(null, args);   // calentar
        int N = 200000;
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) pred.Invoke(null, args);
        sw.Stop();
        // el invoke por reflexion pesa: medir su costo aparte y restarlo
        MethodInfo nada = typeof(RedCost).GetMethod("Nada", BindingFlags.Static | BindingFlags.NonPublic);
        Stopwatch sw2 = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) nada.Invoke(null, args);
        sw2.Stop();
        double us = (sw.Elapsed.TotalMilliseconds - sw2.Elapsed.TotalMilliseconds) * 1000.0 / N;
        Console.WriteLine(string.Format("una pasada de la red: {0:0.00} microsegundos", us));
        Console.WriteLine(string.Format("  DSEE tal como corre hoy  (86 ventanas/s x 2 canales): {0:0.000} ms de CPU por segundo de musica", us * 86 * 2 / 1000.0));
        Console.WriteLine(string.Format("  una red igual para el bajo (20 veces/s, mono)        : {0:0.000} ms de CPU por segundo de musica", us * 20 / 1000.0));
        return 0;
    }
    static bool Nada(float[] x, float[] y) { return true; }
}

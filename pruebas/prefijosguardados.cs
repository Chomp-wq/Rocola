using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

// Solo colas sinteticas en RAM. No lee ni modifica archivos de produccion.
class PrefijosGuardados
{
    const BindingFlags B = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
    static MethodInfo retire;
    static int checks;
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); checks++; Console.WriteLine("OK " + message); }
    static void Old(List<string> pending, List<string> saved)
    { for (int i = 0; i < saved.Count && pending.Count > 0; i++) pending.Remove(saved[i]); }
    static bool Equal(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
        return true;
    }
    static void Compare(List<string> pending, List<string> saved)
    {
        List<string> reference = new List<string>(pending);
        Old(reference, saved); retire.Invoke(null, new object[] { pending, saved });
        if (!Equal(reference, pending)) throw new Exception("orden/duplicados distintos de referencia");
    }
    static int Main(string[] args)
    {
        try
        {
            Type f = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
            retire = f.GetMethod("RetiraPrefijoGuardado", B);
            Check(retire != null, "la confirmacion tiene un camino lineal para prefijos");
            Compare(new List<string>(), new List<string>());
            Compare(new List<string>(), new List<string>(new string[] { "a" }));
            Compare(new List<string>(new string[] { null, "a", "a", "A", "b" }),
                    new List<string>(new string[] { null, "a", "a" }));
            Compare(new List<string>(new string[] { "a", "b", "a" }), new List<string>(new string[] { "b", "a" }));
            Check(true, "vacias, nulos, repetidas y diferencias de mayusculas conservan semantica");
            Random random = new Random(461);
            for (int trial = 0; trial < 3000; trial++)
            {
                List<string> pending = new List<string>();
                int count = random.Next(150);
                for (int i = 0; i < count; i++) pending.Add(random.Next(12).ToString());
                List<string> saved = pending.GetRange(0, random.Next(count + 1));
                // Simular llegada durante Write/Flush: el escritor debe confirmar solo su captura.
                for (int i = 0; i < 30; i++) pending.Add(random.Next(12).ToString());
                if ((trial & 1) != 0) saved.Add("ajena");
                Compare(pending, saved);
            }
            Check(true, "3000 colas con llegadas durante escritura y respaldo de prefijo ajeno");
            foreach (string method in new string[] { "SaveIaDone", "SaveDeepDone", "SaveDurFast" })
                Check(Codigo.Llama(Codigo.ConAyudantes(f, method), "RetiraPrefijoGuardado"), method + " usa confirmacion acotada bajo su cerrojo");
            List<string> all = new List<string>();
            for (int i = 0; i < 30000; i++) all.Add("registro-" + i);
            List<string> savedAll = new List<string>(all), expected = new List<string>(all);
            List<string> large = new List<string>(all); large.AddRange(all); expected.AddRange(all);
            Stopwatch watch = Stopwatch.StartNew(); Old(expected, savedAll); watch.Stop(); double oldMs = watch.Elapsed.TotalMilliseconds;
            watch.Reset(); watch.Start(); retire.Invoke(null, new object[] { large, savedAll }); watch.Stop();
            Check(Equal(expected, large) && large.Count == all.Count, "30000 confirmadas conservan las otras 30000, incluido duplicado exacto");
            Console.WriteLine("REFERENCIA " + oldMs.ToString("F3") + " ms; PREFIJO " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms. Microprueba RAM, no FPS ni disco real.");
            Console.WriteLine("PASS " + checks + " comprobaciones de prefijos"); return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }
}

using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// LO QUE COSTARIA UN MODELO MAS GRANDE PARA EL RECONSTRUCTOR DE AGUDOS.
//
// La red se consulta UNA VEZ POR VENTANA Y CANAL: 86 veces por segundo en estereo, y solo en las canciones
// que de verdad se estan reconstruyendo (una completa ni la toca). Aqui se mide de dos maneras:
//
//   1) la red sola, por tamaño;
//   2) el reconstructor ENTERO con ese modelo puesto de verdad, que es lo que se paga en la rocola.
//
// El tiempo de una red depende solo de su FORMA, no de sus numeros, asi que se usan pesos al azar.
class FuenteM : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public FuenteM(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    { for (int i = 0; i < c; i++) { b[o + i] = d[pos]; pos++; if (pos >= d.Length) pos = 0; } return c; }
}

class Modelo
{
    const int FS = 44100, CH = 2;
    static Type TF, TSbr, TRed;

    static object CreaTap(ISampleProvider fu)
    {
        foreach (ConstructorInfo ci in TSbr.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length < 1 || !ps[0].ParameterType.IsInstanceOfType(fu)) continue;
            object[] full = new object[ps.Length]; full[0] = fu;
            bool ok = true;
            for (int i = 1; i < ps.Length; i++) { if (ps[i].ParameterType != typeof(int)) { ok = false; break; } full[i] = 2048; }
            if (ok) { try { return ci.Invoke(full); } catch { } }
        }
        return null;
    }

    // Musica con un ACANTILADO a 16 kHz: es la unica que hace trabajar a la red (una completa no se toca).
    static float[] Musica(int segundos)
    {
        int n = FS * segundos * CH;
        float[] d = new float[n];
        Random r = new Random(1234);
        for (int i = 0; i < n; i += CH)
        {
            double t = (double)(i / CH) / FS, v = 0;
            for (int k = 0; k < 40; k++)
            {
                double hz = 55.0 * Math.Pow(1.18, k);
                if (hz > 16000) break;
                v += Math.Sin(2 * Math.PI * hz * t) * (0.9 / (1.0 + hz / 300.0));
            }
            v += (r.NextDouble() - 0.5) * 0.02;
            float s = (float)(v * 0.35);
            if (s > 1f) s = 1f; else if (s < -1f) s = -1f;
            for (int c = 0; c < CH; c++) d[i + c] = s;
        }
        return d;
    }

    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TSbr = TF.GetNestedType("SpectralTap", BindingFlags.NonPublic | BindingFlags.Public);
        TRed = TF.GetNestedType("RedChica", BindingFlags.NonPublic | BindingFlags.Public);
        if (TSbr == null || TRed == null) { Console.WriteLine("no encuentro el reconstructor o la red"); return 1; }

        string[] nom = { "(sin modelo)", "lineal", "chico", "actual", "doble", "cuadru", "grande", "enorme", "tope" };
        string[] forma = { "curva fija de -9 dB/oct", "28->12", "28->16->12", "28->48->48->12", "28->96->96->12",
                           "28->192->192->12", "28->256->256->256->12", "28->384->384->384->12", "28->512->512->512->12" };
        int[] pesos = { 0, 348, 668, 4332, 13260, 44940, 142092, 311436, 546316 };

        const double porSegundo = 86.14;   // 43 ventanas por segundo x 2 canales

        // ---------- 1) la red sola ----------
        Console.WriteLine("\n  1) LA RED SOLA (se consulta 86 veces por segundo de musica)\n");
        Console.WriteLine("  {0,-24} {1,9} {2,7} {3,13} {4,11}", "modelo", "pesos", "KB", "una consulta", "por segundo");
        Console.WriteLine("  " + new string('-', 70));
        float[] ent = new float[28], sal = new float[12];
        Random rr = new Random(5);
        for (int i = 0; i < 28; i++) ent[i] = (float)(rr.NextDouble() * 2 - 1);
        for (int k = 1; k < nom.Length; k++)
        {
            object red = Activator.CreateInstance(TRed, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                  null, new object[] { 28, 12 }, null);
            TRed.GetMethod("TryLoad").Invoke(red, new object[] { "modelo_" + nom[k] + ".bin" });
            if (!(bool)TRed.GetField("Loaded").GetValue(red)) { Console.WriteLine("  {0,-24}   (la rocola RECHAZA este modelo)", forma[k]); continue; }
            Func<float[], float[], bool> pred = (Func<float[], float[], bool>)
                Delegate.CreateDelegate(typeof(Func<float[], float[], bool>), red, TRed.GetMethod("Predict"));
            double ms = Mide(3000, delegate { pred(ent, sal); });
            Console.WriteLine("  {0,-24} {1,9} {2,7:0} {3,11:0.0000} ms {4,8:0.00} ms",
                              forma[k], pesos[k], pesos[k] * 4 / 1024.0, ms, ms * porSegundo);
        }

        // ---------- 2) el reconstructor entero, con el modelo puesto DE VERDAD ----------
        // NeuralEnv es el campo estatico que la rocola carga al arrancar desde rocola_ia.bin. Aqui se le
        // cambia el modelo por reflexion, que es exactamente lo que pasaria al copiar otro archivo al lado
        // del ejecutable.
        Console.WriteLine("\n  2) EL RECONSTRUCTOR ENTERO, CON ESE MODELO PUESTO");
        Console.WriteLine("     (musica cortada a 16 kHz: la que de verdad hace trabajar a la red)\n");
        FieldInfo fNeural = TF.GetField("NeuralEnv", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (fNeural == null) { Console.WriteLine("  no encuentro el campo del modelo"); return 1; }
        MethodInfo mTryLoad = TRed.GetMethod("TryLoad");
        FieldInfo fLoaded = TRed.GetField("Loaded");

        float[] datos = Musica(30);
        int bloque = FS * CH / 20;                 // 50 ms, el bloque real de la rocola
        float[] buf = new float[bloque];

        Console.WriteLine("  {0,-24} {1,14} {2,14} {3,12}", "modelo", "por segundo", "vs sin modelo", "en el bar");
        Console.WriteLine("  " + new string('-', 70));
        double baseMs = 0;
        for (int k = 0; k < nom.Length; k++)
        {
            object red = fNeural.GetValue(null);
            if (k == 0) fLoaded.SetValue(red, false);          // sin modelo: la curva clasica
            else
            {
                mTryLoad.Invoke(red, new object[] { "modelo_" + nom[k] + ".bin" });
                if (!(bool)fLoaded.GetValue(red)) { Console.WriteLine("  {0,-24}   (la rocola RECHAZA este modelo)", forma[k]); continue; }
            }
            ISampleProvider fu = new FuenteM(datos, FS, CH);
            object tap = CreaTap(fu);
            TSbr.GetField("Active").SetValue(tap, true);
            ISampleProvider p = (ISampleProvider)tap;
            double ms = Mide(50, delegate { p.Read(buf, 0, bloque); }) * 20.0;   // 20 bloques = 1 segundo
            if (k == 0) baseMs = ms;
            Console.WriteLine("  {0,-24} {1,11:0.00} ms {2,12:0.00} ms {3,9:0.0} ms",
                              forma[k], ms, ms - baseMs, ms * 4.0);
        }
        Console.WriteLine("\n     (\"en el bar\" = la estimacion de siempre: unas cuatro veces mas lento que esta maquina.");
        Console.WriteLine("      Todo esto corre en el hilo que dibuja; un cuadro a 24 por segundo dura 41.7 ms.)\n");
        return 0;
    }
}

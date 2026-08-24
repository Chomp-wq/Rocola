using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// DONDE SE VA EL TIEMPO DEL RECONSTRUCTOR DE AGUDOS (el DSEE).
//
// Es lo mas caro de la cadena de sonido y corre en el HILO DE LA PANTALLA, asi que cada milisegundo que
// gaste es un milisegundo que la interfaz no dibuja. Aqui se mide por partes: la FFT, el detector de
// acantilado, la regeneracion y las cuentas de la IA, para saber que vale la pena apretar.
class Fuente2 : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public Fuente2(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    { for (int i = 0; i < c; i++) { b[o + i] = d[pos]; pos++; if (pos >= d.Length) pos = 0; } return c; }
}

class DseePerf
{
    const int FS = 44100, CH = 2;
    static Type TF, TSbr;
    // El tamano de ventana con el que se arma el reconstructor. La rocola usa 2048 o 4096 y hay que poder
    // medir los dos, que es justo la pregunta: 4096 afina mas, pero cuesta mas, y ese coste se paga en el
    // hilo que dibuja. Se pasa como segundo argumento:  mono dseeperf.exe rocola.exe 4096
    static int ventana = 2048;

    static object Crea(Type t, params object[] args)
    {
        // Las dos ramas no tienen la misma firma: la de tarjeta grafica lleva ademas el tamano de ventana
        // (usa 4096 en su nivel alto). Se rellenan los enteros que sobren con 2048, que es el de siempre.
        foreach (ConstructorInfo ci in t.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length < args.Length) continue;
            bool encaja = true;
            for (int i = 0; i < args.Length; i++)
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { encaja = false; break; }
            if (!encaja) continue;
            object[] full = new object[ps.Length];
            Array.Copy(args, full, args.Length);
            for (int i = args.Length; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != typeof(int)) { encaja = false; break; }
                full[i] = ventana;
            }
            if (!encaja) continue;
            try { return ci.Invoke(full); }
            catch (Exception ex) { Console.WriteLine("     (fallo el constructor de " + t.Name + ": " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
        }
        return null;
    }

    // Musica de mentira pero con la forma que importa: mucha energia abajo, poca arriba y un ACANTILADO
    // a 16 kHz (que es lo que deja un MP3 de 128k). Sin acantilado el DSEE no reconstruye y no se mide nada.
    static float[] Musica(int segundos)
    {
        int n = FS * segundos * CH;
        float[] d = new float[n];
        Random r = new Random(1234);
        float[] fase = new float[40];
        for (int i = 0; i < n; i += CH)
        {
            double t = (double)(i / CH) / FS;
            double v = 0;
            for (int k = 0; k < 40; k++)
            {
                double hz = 55.0 * Math.Pow(1.18, k);
                if (hz > 16000) break;                       // el acantilado: arriba de 16k no hay nada
                v += Math.Sin(2 * Math.PI * hz * t + fase[k]) * (0.9 / (1.0 + hz / 300.0));
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
        for (int t = 0; t < 7; t++)                       // 7 tandas: la maquina esta compartida
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
        if (argv.Length > 1) int.TryParse(argv[1], out ventana);
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TSbr = TF.GetNestedType("SpectralTap", BindingFlags.NonPublic | BindingFlags.Public);
        if (TSbr == null) { Console.WriteLine("no encuentro SpectralTap"); return 1; }

        float[] datos = Musica(4);
        Console.WriteLine("\n=== EL RECONSTRUCTOR DE AGUDOS, POR PARTES  (ventana pedida: " + ventana + ") ===");
        Console.WriteLine("    (estereo a 44100; \"por segundo de musica\" = lo que cuesta procesar 1 s de audio)\n");

        // ---------- 1) la cadena completa del tap, encendido y apagado ----------
        int bloque = FS * CH / 20;                        // 50 ms, que es el bloque real de la rocola
        float[] buf = new float[bloque];

        ISampleProvider fu1 = new Fuente2(datos, FS, CH);
        object sbrOn = Crea(TSbr, fu1);
        ISampleProvider pOn = (ISampleProvider)sbrOn;
        FieldInfo fAct = TSbr.GetField("Active");
        fAct.SetValue(sbrOn, true);

        ISampleProvider fu2 = new Fuente2(datos, FS, CH);
        object sbrOff = Crea(TSbr, fu2);
        ISampleProvider pOff = (ISampleProvider)sbrOff;
        fAct.SetValue(sbrOff, false);

        double msOn = Mide(40, delegate { pOn.Read(buf, 0, bloque); });
        double msOff = Mide(40, delegate { pOff.Read(buf, 0, bloque); });
        Console.WriteLine("  bloque de 50 ms:  encendido " + msOn.ToString("0.000") + " ms   apagado " + msOff.ToString("0.000") + " ms");
        Console.WriteLine("  por segundo de musica:  encendido " + (msOn * 20).ToString("0.00")
                          + " ms  (" + (msOn * 20 / 10).ToString("0.0") + "% de un nucleo)   apagado "
                          + (msOff * 20).ToString("0.00") + " ms");
        FieldInfo fCut = TSbr.GetField("cutHz", BindingFlags.Instance | BindingFlags.NonPublic);
        Console.WriteLine("  acantilado detectado: " + ((float)fCut.GetValue(sbrOn)).ToString("0") + " Hz  (la musica de prueba corta a 16000)");

        // ---------- 2) por partes ----------
        Console.WriteLine("\n  --- por dentro de UNA ventana (hay 43 por segundo y canal = 86 en estereo) ---\n");
        object an = Crea(TSbr, FS, 1);
        MethodInfo mAnalyze = TSbr.GetMethod("AnalyzeFrame");
        MethodInfo mFft = TSbr.GetMethod("Fft", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mDet = TSbr.GetMethod("DetectCliff", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mReg = TSbr.GetMethod("Regenerate", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mBif = TSbr.GetMethod("BuildIaFeatures", BindingFlags.Instance | BindingFlags.NonPublic);

        // Delegados: llamar por Invoke dentro del bucle mediria el Invoke, no el codigo.
        Action<bool> fft = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), an, mFft);
        Action det = (Action)Delegate.CreateDelegate(typeof(Action), an, mDet);
        Action reg = (Action)Delegate.CreateDelegate(typeof(Action), an, mReg);
        Func<int, bool> bif = (Func<int, bool>)Delegate.CreateDelegate(typeof(Func<int, bool>), an, mBif);

        int L = (int)TSbr.GetProperty("FrameLen").GetValue(an, null);
        float[] mono = new float[L * 4];
        for (int i = 0; i < mono.Length; i++) mono[i] = datos[(i * CH) % datos.Length];
        mAnalyze.Invoke(an, new object[] { mono, 0 });     // deja re/im llenos y el acantilado medido
        float cut = (float)fCut.GetValue(an);
        int cutBin = (int)(cut / ((float)FS / L));
        Console.WriteLine("     ventana de " + L + " puntos, acantilado en " + cut.ToString("0") + " Hz (bin " + cutBin + ")");

        double msFft = Mide(2000, delegate { fft(false); });
        double msDet = Mide(2000, delegate { det(); });
        double msReg = Mide(2000, delegate { reg(); });
        double msBif = Mide(2000, delegate { bif(cutBin); });

        Console.WriteLine("     FFT (una)                " + msFft.ToString("0.0000") + " ms");
        Console.WriteLine("     detector de acantilado   " + msDet.ToString("0.0000") + " ms");
        Console.WriteLine("     regeneracion             " + msReg.ToString("0.0000") + " ms");
        Console.WriteLine("     cuentas para la IA       " + msBif.ToString("0.0000") + " ms");

        // Por segundo de musica en estereo: 2 canales x 43 ventanas, y por ventana 2 FFT (ida y vuelta).
        double vent = 43.07 * CH;
        Console.WriteLine();
        Console.WriteLine("     -> por segundo de musica (estereo):");
        Console.WriteLine("        FFT ida y vuelta     " + (msFft * 2 * vent).ToString("0.00") + " ms");
        Console.WriteLine("        detector              " + (msDet * 43.07).ToString("0.00") + " ms   (solo canal 0)");
        Console.WriteLine("        regeneracion          " + (msReg * vent).ToString("0.00") + " ms");
        Console.WriteLine("        cuentas de la IA      " + (msBif * vent).ToString("0.00") + " ms   (solo si hay modelo cargado)");

        return 0;
    }
}

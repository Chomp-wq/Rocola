using System;
using System.Reflection;
using NAudio.Wave;

// Fuente de prueba: entrega el arreglo que le den
class Fuente : ISampleProvider
{
    private readonly float[] d; private int p;
    private readonly WaveFormat wf;
    public Fuente(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    { int n = Math.Min(c, d.Length - p); for (int i = 0; i < n; i++) b[o + i] = d[p + i]; p += n; return n; }
}

class Bajo
{
    const int FS = 44100;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // energia a una frecuencia concreta (Goertzel), en dB
    static double NivelDb(float[] x, double hz, int ch, int canal)
    {
        int n = x.Length / ch;
        double w = 2 * Math.PI * hz / FS, cw = Math.Cos(w), coeff = 2 * cw;
        double s0 = 0, s1 = 0, s2 = 0;
        for (int i = 0; i < n; i++) { s0 = x[i * ch + canal] + coeff * s1 - s2; s2 = s1; s1 = s0; }
        double mag = Math.Sqrt(s1 * s1 + s2 * s2 - coeff * s1 * s2) / (n / 2.0);
        return 20 * Math.Log10(mag + 1e-12);
    }

    static float[] Procesar(float[] entrada, float amount, int ch, Type T)
    {
        object bp = Activator.CreateInstance(T,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
            null, new object[] { new Fuente(entrada, FS, ch), amount }, null);
        MethodInfo read = T.GetMethod("Read");
        float[] buf = new float[entrada.Length];
        int total = 0;
        while (total < buf.Length)
        {
            object[] a = new object[] { buf, total, Math.Min(4096, buf.Length - total) };
            int n = (int)read.Invoke(bp, a);
            if (n <= 0) break;
            total += n;
        }
        return buf;
    }

    static float[] Tono(double hz, double amp, double seg, int ch)
    {
        int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++) { float v = (float)(amp * Math.Sin(2 * Math.PI * hz * i / FS)); for (int c = 0; c < ch; c++) x[i * ch + c] = v; }
        return x;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        Type T = TF.GetNestedType("DeepBassProvider", BindingFlags.NonPublic);
        if (T == null) { Console.WriteLine("no encontre DeepBassProvider"); return 1; }

        // ---- 1) LO ESENCIAL: una nota grave de 45 Hz debe generar sus armonicos 90 y 135 Hz
        Console.WriteLine("\n  Nota grave de 45 Hz (la que la bocina NO puede dar):");
        float[] e45 = Tono(45, 0.5, 2.0, 2);
        float[] s45 = Procesar(e45, 0.30f, 2, T);
        double a90 = NivelDb(s45, 90, 2, 0), a135 = NivelDb(s45, 135, 2, 0);
        double b90 = NivelDb(e45, 90, 2, 0), b135 = NivelDb(e45, 135, 2, 0);
        double f45 = NivelDb(s45, 45, 2, 0) - NivelDb(e45, 45, 2, 0);
        Console.WriteLine(string.Format("     2o armonico (90 Hz):   {0,6:+0.0;-0.0} dB", a90 - b90));
        Console.WriteLine(string.Format("     3er armonico (135 Hz): {0,6:+0.0;-0.0} dB", a135 - b135));
        Console.WriteLine(string.Format("     la nota original 45 Hz: {0,6:+0.0;-0.0} dB (no debe tocarse)", f45));
        Check(a90 - b90 > 20, "genera el 2o armonico (el oido lo necesita)");
        Check(a135 - b135 > 20, "genera el 3er armonico (dos SEGUIDOS = se reconstruye la nota)");
        Check(Math.Abs(f45) < 1.0, "no toca la nota original (no gasta amplificador en lo que no sale)");

        // ---- 2) NO debe ensuciar la voz ni los agudos
        Console.WriteLine("\n  Contenido medio/agudo (voz 1 kHz, platillo 8 kHz):");
        foreach (double hz in new double[] { 1000, 8000 })
        {
            float[] e = Tono(hz, 0.5, 1.0, 2);
            float[] sa = Procesar(e, 0.30f, 2, T);
            double d = NivelDb(sa, hz, 2, 0) - NivelDb(e, hz, 2, 0);
            Console.WriteLine(string.Format("     {0,5:0} Hz: {1,6:+0.00;-0.00} dB", hz, d));
            Check(Math.Abs(d) < 0.5, "  no altera " + hz + " Hz");
        }

        // ---- 3) apagado = pase directo EXACTO
        float[] mix = Tono(60, 0.4, 0.5, 2);
        float[] off = Procesar(mix, 0f, 2, T);
        bool igual = true;
        for (int i = 0; i < mix.Length; i++) if (mix[i] != off[i]) { igual = false; break; }
        Console.WriteLine();
        Check(igual, "en 0% el audio sale IDENTICO bit a bit (apagarlo no cuesta nada)");

        // ---- 4) en silencio no inventa ruido
        float[] sil = new float[FS * 2];
        float[] ssil = Procesar(sil, 1.0f, 2, T);
        double pico = 0; foreach (float v in ssil) pico = Math.Max(pico, Math.Abs(v));
        Check(pico < 1e-6, "en silencio no genera nada (pico " + pico.ToString("0.0e+0") + ")");

        // ---- 5) el grave sale CENTRADO (mismo en los dos canales)
        float[] est = new float[FS * 2];
        for (int i = 0; i < FS; i++)
        { est[i * 2] = (float)(0.5 * Math.Sin(2 * Math.PI * 50 * i / FS)); est[i * 2 + 1] = (float)(0.45 * Math.Sin(2 * Math.PI * 50 * i / FS)); }
        float[] sest = Procesar(est, 0.5f, 2, T);
        double dif = Math.Abs(NivelDb(sest, 100, 2, 0) - NivelDb(sest, 100, 2, 1));
        Check(dif < 1.5, "los armonicos salen centrados en los dos canales (dif " + dif.ToString("0.00") + " dB)");

        // ---- 6) mono no truena
        float[] m1 = Tono(50, 0.5, 0.5, 1);
        float[] sm = Procesar(m1, 0.4f, 1, T);
        bool san = true; foreach (float v in sm) if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 4f) { san = false; break; }
        Check(san, "en mono (1 canal) funciona y no se desborda");

        // ---- 7) la perilla manda
        Console.WriteLine("\n  Cuanto sube el 2o armonico segun la perilla:");
        double prev = -999;
        foreach (int pct in new int[] { 10, 30, 60, 100 })
        {
            float[] sp = Procesar(e45, pct / 100f, 2, T);
            double lvl = NivelDb(sp, 90, 2, 0);
            Console.WriteLine(string.Format("     {0,3}% -> {1,6:0.0} dB", pct, lvl));
            Check(lvl > prev, "  al " + pct + "% suena mas que antes");
            prev = lvl;
        }
        // ---------- La tangente aproximada del realce de agudos ----------
        // Se cambio Math.Tanh (que se llamaba 88,200 veces por segundo) por una aproximacion. Aqui se comprueba
        // que dibuja la MISMA curva: si se apartara, el realce sonaria distinto.
        {
            MethodInfo TANH = TF.GetMethod("TanhRapida", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            Check(TANH != null, "existe la tangente aproximada del realce");
            if (TANH != null)
            {
                double peor = 0; float xPeor = 0;
                bool sube = true, dentro = true; float ant = -2f;
                for (float x = -6f; x <= 6f; x += 0.002f)
                {
                    float y = (float)TANH.Invoke(null, new object[]{ x });
                    double e = Math.Abs(Math.Tanh(x) - y);
                    if (x >= -3.2f && x <= 3.2f && e > peor) { peor = e; xPeor = x; }
                    if (y < ant - 1e-6f) sube = false;
                    if (y < -1.0001f || y > 1.0001f) dentro = false;
                    ant = y;
                }
                Console.WriteLine(string.Format("\n  la tangente aproximada se aparta como mucho {0:0.0000} de la de verdad (en x={1:0.0})", peor, xPeor));
                Console.WriteLine(string.Format("  el realce entra al 7% como mucho: en la salida eso son {0:0.0} dB por debajo", 20*Math.Log10(peor*0.07)));
                Check(peor < 0.03, "dibuja la misma curva que la de verdad (menos de 0.03 de diferencia)");
                Check(20*Math.Log10(peor*0.07) < -50, "la diferencia queda mas de 50 dB por debajo de la senal: inaudible");
                Check(sube, "y sigue siendo creciente: nunca da un salto ni se dobla hacia atras");
                Check(dentro, "y nunca se sale de -1..1, que es lo que la hace un saturador");
                Check(Math.Abs((float)TANH.Invoke(null, new object[]{ 0f })) < 1e-9f, "en silencio da silencio (no mete continua)");
            }
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

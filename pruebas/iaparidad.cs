using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NAudio.Wave;

// LO QUE APRENDE LA RED Y LO QUE RELLENA LA ROCOLA TIENEN QUE SER LAS MISMAS BANDAS.
//
// La red del DSEE se entrena en Python y se usa aqui. Lo que la une son 28 numeros de entrada (la forma del
// espectro que sobrevivio al codec) y 12 de salida (cuanto suena cada sub-banda por encima del corte). Si el
// entrenador mide esas bandas de una forma y la rocola de otra, la red aprende una cosa y aqui se aplica a
// otra, y NADA avisa: no se rompe nada, simplemente suena peor de lo que deberia.
//
// Eso es justo lo que pasaba. El extractor de Python ponia el techo de los objetivos en 20 kHz y la rocola en
// 20,5; Python no redondeaba el corte a un bin y la rocola si; Python dejaba subir el objetivo hasta +0,5 y la
// rocola hasta +0,1. Las pruebas de siempre pasaban la MISMA entrada por la red en los dos lados (eso valida la
// inferencia) pero nunca comparaban como se calcula esa entrada.
//
// Aqui se compara el calculo mismo, y de tres maneras:
//   1) contra ia/paridad_extractor.txt, que genera ia/contrato_dsee.py (espectros y senales reproducibles
//      bit a bit en los dos lenguajes);
//   2) TODOS los cortes posibles, a varias frecuencias de muestreo: la rocola escribe sus 40 numeros y el
//      contrato de Python los contrasta uno por uno;
//   3) las bandas que RELLENA RegenNeural (no solo las que mide MakePair), mirando que bins toca cada una.
class FuenteMuda : ISampleProvider
{
    private readonly WaveFormat wf;
    public FuenteMuda(int fs) { wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, 1); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { return 0; }
}

class IaParidad
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TSt;
    static FieldInfo fRe, fIm;
    static MethodInfo mPar, mAnaliza, mRegen;

    // El MISMO generador que ia/contrato_dsee.py: congruente de 32 bits y escalas potencia de dos.
    sealed class Lcg
    {
        uint s;
        static readonly float[] Esc = { 1f, 0.25f, 0.0625f, 0.015625f, 1f / 256f, 1f / 1024f, 1f / 4096f, 1f / 16384f };
        public Lcg(uint semilla) { s = semilla; }
        public uint Siguiente() { unchecked { s = s * 1664525u + 1013904223u; } return s; }
        public float Valor()
        {
            float u = (float)(Siguiente() >> 8) * (1f / 16777216f);
            float e = Esc[Siguiente() >> 29];
            return (u - 0.5f) * e;
        }
    }

    static void Espectro(int frame, uint semilla, float[] re, float[] im)
    {
        Lcg g = new Lcg(semilla);
        for (int k = 0; k < frame; k++) { re[k] = g.Valor(); im[k] = g.Valor(); }
    }

    static float[] Senal(int frame, int rate, uint semilla)
    {
        Lcg g = new Lcg(semilla);
        double[,] tonos = { { 220.0, 0.30 }, { 1375.0, 0.12 }, { 4410.0, 0.05 }, { 9100.0, 0.02 }, { 15300.0, 0.008 } };
        float[] x = new float[frame];
        for (int n = 0; n < frame; n++)
        {
            double v = 0;
            for (int t = 0; t < 5; t++) v += tonos[t, 1] * Math.Sin(2.0 * Math.PI * tonos[t, 0] * n / rate);
            x[n] = (float)v + g.Valor() * 0.125f;
        }
        return x;
    }

    // Un analizador del tamano pedido: el de 2048 es el de la cosecha; el de 4096 solo existe en la rama que
    // reproduce con ese cuadro (en la de XP el cuadro es una constante de 2048).
    static object Tap(int rate, int frame)
    {
        if (frame == 2048)
        {
            ConstructorInfo c = TSt.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                   null, new Type[] { typeof(int), typeof(int) }, null);
            return c == null ? null : c.Invoke(new object[] { rate, 1 });
        }
        foreach (ConstructorInfo c in TSt.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            ParameterInfo[] ps = c.GetParameters();
            if (ps.Length == 2 && ps[1].ParameterType == typeof(int) && ps[0].ParameterType.IsAssignableFrom(typeof(FuenteMuda)))
            {
                object t = c.Invoke(new object[] { new FuenteMuda(rate), frame });
                int len = (int)TSt.GetProperty("FrameLen").GetValue(t, null);
                return len == frame ? t : null;
            }
        }
        return null;
    }

    static void Pon(object tap, float[] re, float[] im)
    {
        float[] r = (float[])fRe.GetValue(tap), i = (float[])fIm.GetValue(tap);
        Array.Copy(re, r, r.Length); Array.Copy(im, i, i.Length);
    }

    static float BinHz(int rate, int frame) { return (float)rate / frame; }

    static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }

    static string Python() { return Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON") ?? "python3"; }

    // Corre el contrato de Python sobre un volcado. Devuelve null si no hay Python en esta maquina.
    static Dictionary<string, string> Contrasta(string volcado, out string todo)
    {
        todo = "";
        string script = Path.GetFullPath(Path.Combine("..", Path.Combine("ia", "contrato_dsee.py")));
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Python(), "\"" + script + "\" --comparar \"" + volcado + "\"");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            psi.WorkingDirectory = Path.GetDirectoryName(script);
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd(), e = p.StandardError.ReadToEnd();
                p.WaitForExit();
                todo = o + e;
            }
        }
        catch { return null; }
        Dictionary<string, string> d = new Dictionary<string, string>();
        foreach (string l in todo.Split('\n'))
        {
            string[] t = l.Trim().Split(' ');
            if (t.Length == 2 && !l.StartsWith(" ")) d[t[0]] = t[1];
        }
        return d;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        TSt = TF.GetNestedType("SpectralTap", BindingFlags.NonPublic | BindingFlags.Public);
        BindingFlags bi = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Check(TSt != null, "el reconstructor de agudos esta donde tiene que estar");
        if (TSt == null) return 1;
        fRe = TSt.GetField("re", bi); fIm = TSt.GetField("im", bi);
        mPar = TSt.GetMethod("MakePair", bi); mAnaliza = TSt.GetMethod("AnalyzeFrame", bi);
        mRegen = TSt.GetMethod("RegenNeural", bi);
        Check(fRe != null && fIm != null && mPar != null && mAnaliza != null && mRegen != null,
              "estan las piezas que se comparan: el espectro, MakePair, AnalyzeFrame y RegenNeural");
        if (fallos > 0) return fallos;
        bool hay4096 = Tap(44100, 4096) != null;
        float[] feat = new float[28], tgt = new float[12];

        // ============================================================================================
        Console.WriteLine("\n  1) LOS VECTORES DE PARIDAD DEL EXTRACTOR\n");
        string rutaPar = Path.Combine("..", Path.Combine("ia", "paridad_extractor.txt"));
        Check(File.Exists(rutaPar), "estan los vectores de paridad que genera ia/contrato_dsee.py");
        if (File.Exists(rutaPar))
        {
            int n = 0, saltados = 0, validos = 0, invalidos = 0, validezMal = 0;
            double peorS = 0, peorT = 0; double tolS = 2e-5, tolT = 2e-3;
            foreach (string linea in File.ReadAllLines(rutaPar))
            {
                if (linea.Length == 0 || linea[0] == '#') continue;
                string[] partes = linea.Split(':');
                string[] c = partes[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string tipo = c[0];
                int rate = int.Parse(c[1]), frame = int.Parse(c[2]);
                float corte = float.Parse(c[3], CultureInfo.InvariantCulture);
                uint semilla = uint.Parse(c[4]);
                bool valido = c[5] == "1";
                object tap = Tap(rate, frame);
                if (tap == null) { saltados++; continue; }
                if (tipo == "S")
                {
                    float[] re = new float[frame], im = new float[frame];
                    Espectro(frame, semilla, re, im);
                    Pon(tap, re, im);
                }
                else mAnaliza.Invoke(tap, new object[] { Senal(frame, rate, semilla), 0 });
                bool dio = (bool)mPar.Invoke(tap, new object[] { corte, feat, tgt });
                n++;
                if (dio != valido) { validezMal++; continue; }
                if (!dio) { invalidos++; continue; }
                validos++;
                string[] xs = partes[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string[] ys = partes[2].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                double peor = 0;
                for (int i = 0; i < 28; i++) peor = Math.Max(peor, Math.Abs(feat[i] - double.Parse(xs[i], CultureInfo.InvariantCulture)));
                for (int j = 0; j < 12; j++) peor = Math.Max(peor, Math.Abs(tgt[j] - double.Parse(ys[j], CultureInfo.InvariantCulture)));
                if (tipo == "S") peorS = Math.Max(peorS, peor); else peorT = Math.Max(peorT, peor);
            }
            Console.WriteLine("     " + n + " casos comparados (" + validos + " pares validos, " + invalidos + " que la rocola rechaza igual que el contrato)"
                              + (saltados > 0 ? "; " + saltados + " de cuadro 4096 no aplican en esta rama (su cuadro es fijo de 2048)" : ""));
            Console.WriteLine("     mayor diferencia, espectro inyectado: " + peorS.ToString("0.000e0", CultureInfo.InvariantCulture)
                              + "   |   senal por ventana y FFT float32: " + peorT.ToString("0.000e0", CultureInfo.InvariantCulture));
            Check(n >= 100 && validos >= 50 && invalidos >= 10, "hay casos de sobra, validos e invalidos");
            Check(validezMal == 0, "la rocola acepta y rechaza EXACTAMENTE los mismos pares que el contrato (" + validezMal + " distintos)");
            Check(peorS <= tolS, "las 28 entradas y los 12 objetivos coinciden con el contrato sobre el mismo espectro (tolerancia " + tolS + " en log10)");
            Check(peorT <= tolT, "y tambien partiendo de la senal: misma ventana, mismos bins, misma frecuencia (tolerancia " + tolT + ")");
        }

        // ============================================================================================
        Console.WriteLine("\n  2) TODOS LOS CORTES, Y LAS BANDAS QUE RELLENA RegenNeural\n");
        // Modelo de prueba: una sola capa con pesos cero, asi que la salida es el sesgo, distinto por banda.
        // Con la fuente plana, cada banda multiplica por su propio factor y se ve que bins toca cada una.
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_iaparidad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            float[] sesgo = new float[12];
            for (int j = 0; j < 12; j++) sesgo[j] = -3.9f + 0.3f * j;
            string modelo = Path.Combine(tmp, "prueba_ia.bin");
            using (BinaryWriter w = new BinaryWriter(File.Create(modelo)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIA1")); w.Write(1); w.Write(12); w.Write(28);
                for (int i = 0; i < 12 * 28; i++) w.Write(0f);
                for (int j = 0; j < 12; j++) w.Write(sesgo[j]);
            }
            FieldInfo fNeural = TF.GetField("NeuralEnv", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object red = fNeural != null ? fNeural.GetValue(null) : null;
            if (red != null) red.GetType().GetMethod("TryLoad").Invoke(red, new object[] { modelo });
            bool cargo = red != null && (bool)red.GetType().GetField("Loaded").GetValue(red);
            Check(cargo, "el modelo de prueba entra en la red de la rocola");

            string volcado = Path.Combine(tmp, "volcado.txt");
            int lineasP = 0, lineasR = 0, noCuadra = 0;
            Stopwatch reloj = Stopwatch.StartNew();
            using (StreamWriter sw = new StreamWriter(volcado, false, new UTF8Encoding(false)))
            {
                sw.NewLine = "\n";
                int[] tasas = { 22050, 32000, 44100, 48000, 88200, 96000 };
                int[] cuadros = hay4096 ? new int[] { 2048, 4096 } : new int[] { 2048 };
                uint semilla = 90001;
                foreach (int rate in tasas)
                    foreach (int frame in cuadros)
                    {
                        object tap = Tap(rate, frame);
                        int ny = frame / 2;
                        float bhz = BinHz(rate, frame);
                        float[] re = new float[frame], im = new float[frame];
                        Espectro(frame, semilla, re, im);
                        for (int cut = 60; cut < ny; cut++)
                        {
                            Pon(tap, re, im);
                            float corte = (cut + 0.5f) * bhz;
                            bool dio = (bool)mPar.Invoke(tap, new object[] { corte, feat, tgt });
                            StringBuilder sb = new StringBuilder("P ");
                            sb.Append(rate).Append(' ').Append(frame).Append(' ').Append(F(corte)).Append(' ').Append(semilla).Append(dio ? " 1" : " 0");
                            if (dio)
                            {
                                for (int i = 0; i < 28; i++) sb.Append(' ').Append(F(feat[i]));
                                for (int j = 0; j < 12; j++) sb.Append(' ').Append(F(tgt[j]));
                            }
                            sw.WriteLine(sb.ToString()); lineasP++;
                        }
                        // Bandas de RegenNeural: fuente plana bajo el corte, nada encima; asi re[t] = factor de su banda.
                        if (!cargo) continue;
                        float ciel = Math.Min(20500f, ny * bhz * 0.95f);
                        for (int cut = 64; cut < ny; cut++)
                        {
                            int topBin = Math.Min((int)(ciel / bhz), Math.Min(2 * cut - 2, ny - 4));
                            if (topBin <= cut + 4) continue;
                            float[] r2 = new float[frame], i2 = new float[frame];
                            for (int k = 0; k < cut; k++) { r2[k] = 1f; i2[k] = 0.5f; }
                            Pon(tap, r2, i2);
                            if (!(bool)mRegen.Invoke(tap, new object[] { cut, topBin })) continue;   // fc < 1200: tampoco en MakePair
                            float[] rr = (float[])fRe.GetValue(tap);
                            int[] ini = new int[12], fin = new int[12];
                            for (int j = 0; j < 12; j++) { ini[j] = -1; fin[j] = -1; }
                            for (int t = cut; t <= topBin; t++)
                            {
                                if (rr[t] == 0f) continue;
                                // A que banda pertenece: la de factor mas parecido (0,9 * 10^(y/2), sin tope en este rango)
                                double lg = Math.Log10(rr[t] / 0.9) * 2.0;
                                int j = (int)Math.Round((lg + 3.9) / 0.3);
                                if (j < 0 || j > 11 || Math.Abs(lg - sesgo[j]) > 0.01) { noCuadra++; continue; }
                                if (ini[j] < 0) ini[j] = t;
                                else if (fin[j] != t) noCuadra++;   // una banda con huecos no es una banda
                                fin[j] = t + 1;
                            }
                            StringBuilder sb = new StringBuilder("R ");
                            sb.Append(rate).Append(' ').Append(frame).Append(' ').Append(cut).Append(' ').Append(topBin);
                            for (int j = 0; j < 12; j++) sb.Append(' ').Append(ini[j]).Append(' ').Append(fin[j]);
                            sw.WriteLine(sb.ToString()); lineasR++;
                        }
                        semilla++;
                    }
            }
            Console.WriteLine("     la rocola calculo " + lineasP.ToString("#,0") + " pares (todos los cortes, "
                              + (hay4096 ? "cuadros 2048 y 4096" : "cuadro 2048") + ", seis frecuencias) y "
                              + lineasR.ToString("#,0") + " reconstrucciones, en " + reloj.ElapsedMilliseconds + " ms");
            Check(noCuadra == 0, "cada bin que rellena RegenNeural pertenece a una sola banda, sin huecos (" + noCuadra + " raros)");
            Check(lineasR > 1000, "se miraron las bandas de RegenNeural en mas de mil cortes");

            string salida;
            Dictionary<string, string> r = Contrasta(volcado, out salida);
            if (r == null)
                Console.WriteLine("  OK     (sin Python en esta maquina: el barrido completo NO se contrasto aqui)");
            else
            {
                string v;
                int lineas = r.TryGetValue("LINEAS", out v) ? int.Parse(v) : -1;
                int malas = r.TryGetValue("DISCREPANCIAS", out v) ? int.Parse(v) : -1;
                Console.WriteLine("     el contrato de Python repaso " + lineas.ToString("#,0") + " lineas; peor diferencia "
                                  + (r.TryGetValue("PEOR", out v) ? v : "?"));
                if (malas != 0) Console.Write(salida);
                Check(lineas == lineasP + lineasR, "Python leyo todo lo que escribio la rocola");
                Check(malas == 0, "y NINGUNA discrepancia: mismos pares validos, mismos 40 numeros, mismo topBin y mismas bandas en RegenNeural");
            }
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }

        // ============================================================================================
        Console.WriteLine("\n  3) EL LIMITE DE 20,5 kHz ES EL DE LA ROCOLA, Y NO EL DE 20 kHz DEL EXTRACTOR VIEJO\n");
        // Un caso que el extractor anterior de Python media distinto: corte de 11 kHz a 44,1 kHz, espectro plano
        // hasta 20 kHz y NADA por encima. Con el techo de la rocola (20,5 kHz) la ultima sub-banda llega a mirar
        // bins vacios y queda por debajo de la costura; con el techo viejo de 20 kHz saldria plana (0).
        {
            object tap = Tap(44100, 2048);
            float bhz = BinHz(44100, 2048);
            float[] re = new float[2048], im = new float[2048];
            for (int k = 0; k < 2048; k++) if (k * bhz < 20000f) { re[k] = 1f; im[k] = 0.5f; }
            Pon(tap, re, im);
            bool dio = (bool)mPar.Invoke(tap, new object[] { 11000f, feat, tgt });
            Console.WriteLine("     ultima sub-banda con nada por encima de 20 kHz: " + tgt[11].ToString("0.000", CultureInfo.InvariantCulture)
                              + " (con el techo viejo de 20 kHz seria 0,000)");
            Check(dio && tgt[11] < -0.2f && Math.Abs(tgt[0]) < 0.01f,
                  "la rocola mide hasta 20,5 kHz: lo que el contrato fija y lo que el extractor viejo no hacia");
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

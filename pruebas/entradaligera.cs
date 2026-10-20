using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// EL APRENDIZAJE EN VIVO DE UNA CANCION NUEVA, POR TROZOS (20-10-2026; AudioControles.cs, MideBloqueOriginal).
//
// Mientras suena una cancion que el repaso aun no ha medido, la cadena le aprende el volumen (la sonoridad K: dos biquads por canal y la suma de los
// cuadrados de lo que sale del segundo) y la huella tonal (8 pasa-bandas por canal y la suma de la potencia de cada banda). Antes lo hacia muestra a muestra,
// con la memoria de los filtros y los coeficientes en arreglos, y la huella cuadro a cuadro (AccumulateEqFrame). Ahora los cuadros enteros van de golpe:
// la huella por la misma HuellaDelTrozo del repaso, y la sonoridad K de estereo con los coeficientes y la memoria en variables (KCuadrosEstereo); el cuadro
// partido de los extremos y el mono / los canales de mas, muestra a muestra como siempre (KMuestraAMuestra).
//
// Aqui se compara el codigo de verdad con una copia del de antes (esta misma bateria lo lleva escrito), con el mismo audio troceado de todas las maneras, y
// se exige que TODO lo que queda despues de cada lectura sea igual, bit a bit:
//   - la sonoridad (curSumSq, curSamples), el canal que toca (kwCanal), el contador de limpieza (kwLimpia) y los 8 estados de cada canal (kwSt);
//   - la huella (eqMeasSum de las 8 bandas, eqMeasN), su contador de limpieza (eqLimpia), los estados (eqMeasSt) y el cuadro partido que espera (eqPend, eqPendN).
// Lecturas de 1, 2, 3, 5, 7, 63, 64, 65, 127, 128, 129, 1000, 4096, 4410 y 8191 muestras (casi todas parten un cuadro), a 8, 22,05, 44,1, 48 y 96 kHz, mono, estereo,
// tres y seis canales, con musica, un silencio de ceros exactos (para los restos diminutos), picos y medidas de una sola de las dos cosas.
class EntradaLigera
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static int Bits(float x) { return BitConverter.ToInt32(BitConverter.GetBytes(x), 0); }
    const int Cada = 64;
    static float Limp(float v) { return (v < 1e-15f && v > -1e-15f) ? 0f : v; }
    static void Limpia(float[] a) { for (int i = 0; i < a.Length; i++) { float v = a[i]; if (v < 1e-15f && v > -1e-15f) a[i] = 0f; } }

    static Type TF; static MethodInfo mShelf, mHp, mBpf; static float[] centros;
    static object Estatico(string n) { FieldInfo f = TF.GetField(n, T); return f.GetValue(null); }

    // ---------------- la copia del codigo de antes (git: c29b73e) ----------------
    sealed class Antes
    {
        public double curSumSq; public long curSamples; public int kwRate, kwCanales, kwCanal, kwLimpia, eqLimpia;
        public float[] kwCoef1, kwCoef2, kwSt;
        public double[] eqMeasSum = new double[8]; public long eqMeasN; public float[][] eqMeasCoef; public float[] eqMeasSt; public int eqMeasRate;
        public float[] eqPend; public int eqPendN;
        public bool midek = true, mideq = true;

        static void Acumula(float[] buffer, int offset, int channels, float[][] coefficients, float[] state, double[] sums)
        {
            double weight = 1.0 / channels;
            for (int c = 0; c < channels; c++)
            {
                float input = buffer[offset + c];
                for (int b = 0; b < 8; b++)
                {
                    float[] cf = coefficients[b]; if (cf == null) continue;
                    int q = c * 32 + b * 4;
                    float output = cf[0]*input + cf[1]*state[q] + cf[2]*state[q+1] - cf[3]*state[q+2] - cf[4]*state[q+3];
                    state[q+1]=state[q]; state[q]=input; state[q+3]=state[q+2]; state[q+2]=output;
                    sums[b] += (output * output) * weight;
                }
            }
        }
        public void Mide(float[] buffer, int offset, int count, int rate, int ch)
        {
            if (midek)
            {
                int ch3 = ch < 1 ? 1 : ch;
                if (kwRate != rate || kwCoef1 == null || kwCanales != ch3 || kwSt == null)
                {
                    kwRate = rate; kwCanales = ch3; kwCanal = 0;
                    kwCoef1 = (float[])mShelf.Invoke(null, new object[] { 1681.974f, rate, 3.99984f, 0.70717f });
                    kwCoef2 = (float[])mHp.Invoke(null, new object[] { 38.1355f, rate, 0.50033f });
                    kwSt = new float[ch3 * 8];
                }
                float[] st = kwSt, c1 = kwCoef1, c2 = kwCoef2;
                double sumK = 0; long nK = 0;
                int c = kwCanal; if (c >= ch3) c = 0;
                for (int i = offset; i < offset + count; i++)
                {
                    int b = c * 8; float x = buffer[i];
                    float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                    st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                    float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                    st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                    sumK += y2 * y2; nK++;
                    if (++c >= ch3) { c = 0; if (++kwLimpia == Cada) { kwLimpia = 0; Limpia(st); } }
                }
                kwCanal = c;
                curSumSq += sumK; curSamples += nK;
            }
            if (mideq)
            {
                int ch2 = ch < 1 ? 1 : ch;
                if (eqMeasRate != rate || eqMeasCoef == null || eqMeasSt == null || eqMeasSt.Length != ch2 * 32)
                {
                    eqMeasRate = rate; eqMeasCoef = new float[8][]; eqMeasSt = new float[ch2 * 32];
                    for (int b = 0; b < 8; b++) eqMeasCoef[b] = (float[])mBpf.Invoke(null, new object[] { centros[b], rate });
                    eqPendN = 0;
                }
                if (eqPend == null || eqPend.Length != ch2) { eqPend = new float[ch2]; eqPendN = 0; }
                float[] st = eqMeasSt; double[] sums = eqMeasSum;
                int i = offset, fin = offset + count;
                if (eqPendN > 0)
                {
                    while (eqPendN < ch2 && i < fin) eqPend[eqPendN++] = buffer[i++];
                    if (eqPendN == ch2) { Acumula(eqPend, 0, ch2, eqMeasCoef, st, sums); eqMeasN++; eqPendN = 0; }
                }
                if (eqPendN == 0)
                {
                    for (; i + ch2 <= fin; i += ch2)
                    {
                        Acumula(buffer, i, ch2, eqMeasCoef, st, sums);
                        eqMeasN++;
                        if (++eqLimpia == Cada) { eqLimpia = 0; Limpia(st); }
                    }
                    while (i < fin) eqPend[eqPendN++] = buffer[i++];
                }
            }
        }
    }

    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }

    // Lo que queda despues de una lectura, de las dos. Devuelve la descripcion de la primera diferencia, o null.
    static string Difiere(object f, Antes a, bool k, bool q)
    {
        if (k)
        {
            if (BitConverter.DoubleToInt64Bits((double)Get(f, "curSumSq")) != BitConverter.DoubleToInt64Bits(a.curSumSq)) return "curSumSq";
            if ((long)Get(f, "curSamples") != a.curSamples) return "curSamples";
            if ((int)Get(f, "kwCanal") != a.kwCanal) return "kwCanal";
            if ((int)Get(f, "kwLimpia") != a.kwLimpia) return "kwLimpia";
            float[] s = (float[])Get(f, "kwSt");
            if ((s == null) != (a.kwSt == null)) return "kwSt nulo";
            if (s != null) { if (s.Length != a.kwSt.Length) return "kwSt largo"; for (int i = 0; i < s.Length; i++) if (Bits(s[i]) != Bits(a.kwSt[i])) return "kwSt[" + i + "]"; }
        }
        if (q)
        {
            double[] u = (double[])Get(f, "eqMeasSum");
            for (int i = 0; i < 8; i++) if (BitConverter.DoubleToInt64Bits(u[i]) != BitConverter.DoubleToInt64Bits(a.eqMeasSum[i])) return "eqMeasSum[" + i + "]";
            if ((long)Get(f, "eqMeasN") != a.eqMeasN) return "eqMeasN";
            if ((int)Get(f, "eqLimpia") != a.eqLimpia) return "eqLimpia";
            if ((int)Get(f, "eqPendN") != a.eqPendN) return "eqPendN";
            float[] p = (float[])Get(f, "eqPend");
            for (int i = 0; i < a.eqPendN; i++) if (Bits(p[i]) != Bits(a.eqPend[i])) return "eqPend[" + i + "]";
            float[] s = (float[])Get(f, "eqMeasSt");
            if ((s == null) != (a.eqMeasSt == null)) return "eqMeasSt nulo";
            if (s != null) { if (s.Length != a.eqMeasSt.Length) return "eqMeasSt largo"; for (int i = 0; i < s.Length; i++) if (Bits(s[i]) != Bits(a.eqMeasSt[i])) return "eqMeasSt[" + i + "]"; }
        }
        return null;
    }

    static float[] Senal(int frames, int ch, int semilla)
    {
        Random r = new Random(semilla); float[] x = new float[frames * ch]; double ph = 0, ph2 = 0;
        for (int i = 0; i < frames; i++)
        {
            double v = 0.6 * Math.Sin(ph) + 0.2 * Math.Sin(ph2) + 0.15 * (r.NextDouble() - 0.5); ph += 0.02 + 0.00002 * (i % 777); ph2 += 0.37;
            bool mudo = (i > frames / 4 && i < frames / 4 + 9000) || (i > frames * 3 / 4 && i < frames * 3 / 4 + 3000);   // ceros exactos
            if (mudo) v = 0; else if (i % 5003 < 4) v = 0.99;
            for (int c = 0; c < ch; c++) x[i * ch + c] = mudo ? 0f : (float)Math.Max(-1.0, Math.Min(1.0, v * (1.0 - 0.07 * c)));
        }
        return x;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        mShelf = TF.GetMethod("EqShelfHi", T); mHp = TF.GetMethod("EqHighPass", T); mBpf = TF.GetMethod("EqBpf", T);
        centros = (float[])Estatico("EqCentros");
        MethodInfo mide = TF.GetMethod("MideBloqueOriginal", T);
        // el enganche, en el programa ya compilado: MideBloqueOriginal llama a los trozos y ya no a la huella cuadro a cuadro
        Check(Codigo.Llama(mide, "KCuadrosEstereo") && Codigo.Llama(mide, "KMuestraAMuestra"), "MideBloqueOriginal llama a la sonoridad K por trozos (KCuadrosEstereo y KMuestraAMuestra)");
        Check(Codigo.Llama(mide, "HuellaDelTrozoDesde"), "MideBloqueOriginal llama a la huella banda por banda (HuellaDelTrozoDesde)");
        Check(Codigo.Llamadas(mide).FindAll(delegate(string s) { return s.EndsWith("AccumulateEqFrame"); }).Count <= 1, "MideBloqueOriginal solo conserva AccumulateEqFrame para el cuadro que completa el partido");
        string dir = Path.Combine(Path.GetTempPath(), "entradaligera_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        int[] tamanos = { 1, 2, 3, 5, 7, 63, 64, 65, 127, 128, 129, 1000, 4096, 4410, 8191 };
        int[][] formatos = { new[] { 44100, 2 }, new[] { 44100, 1 }, new[] { 48000, 2 }, new[] { 22050, 1 }, new[] { 8000, 2 }, new[] { 96000, 2 }, new[] { 44100, 3 }, new[] { 48000, 6 } };
        int comparaciones = 0, partidos = 0;
        double msAntes = 0, msAhora = 0; long muestrasTiempo = 0;
        try
        {
            int semilla = 1;
            foreach (int[] fm in formatos)
            foreach (int modo in new int[] { 3, 1, 2 })   // 3 = las dos cosas, 1 = solo la sonoridad, 2 = solo la huella
            {
                int rate = fm[0], ch = fm[1]; bool k = (modo & 1) != 0, q = (modo & 2) != 0;
                if (modo != 3 && !(ch == 2 && rate == 44100) && !(ch == 1 && rate == 22050)) continue;
                int frames = rate / 2 + 5000 + 31 * semilla;
                float[] x = Senal(frames, ch, semilla++);
                object f = Codigo.FormSinEncender(TF);
                if (k) Set(f, "curMeasurePath", "k");
                if (q) { Set(f, "eqMeasPath", "q"); Set(f, "eqMeasSum", new double[8]); Set(f, "eqMeasN", 0L); }
                Antes a = new Antes(); a.midek = k; a.mideq = q;
                Random az = new Random(1000 + semilla);
                int pos = 0; string primera = null; int vueltas = 0;
                while (pos < x.Length)
                {
                    int n = tamanos[az.Next(tamanos.Length)]; if (pos + n > x.Length) n = x.Length - pos;
                    // el trozo va en medio de un arreglo mas grande (offset distinto de cero), y lo de alrededor es basura que no debe tocarse
                    int off = az.Next(0, 9);
                    float[] bufA = new float[n + off + 5], bufB = new float[n + off + 5];
                    for (int i = 0; i < bufA.Length; i++) { bufA[i] = bufB[i] = 777f + i; }
                    Array.Copy(x, pos, bufA, off, n); Array.Copy(x, pos, bufB, off, n);
                    mide.Invoke(f, new object[] { bufA, off, n, rate, ch });
                    a.Mide(bufB, off, n, rate, ch);
                    pos += n; vueltas++;
                    if (n % ch != 0) partidos++;
                    string d = Difiere(f, a, k, q);
                    bool tocado = false; for (int i = 0; i < bufA.Length; i++) if (i < off || i >= off + n) { if (bufA[i] != 777f + i) tocado = true; } else if (Bits(bufA[i]) != Bits(bufB[i])) tocado = true;
                    if (tocado && primera == null) primera = "el buffer cambio";
                    if (d != null && primera == null) primera = d + " (lectura " + vueltas + ", " + n + " muestras, desplazamiento " + off + ")";
                    comparaciones++;
                    if (primera != null) break;
                }
                Check(primera == null, rate + " Hz " + ch + " canal(es), " + (k && q ? "sonoridad y huella" : (k ? "solo sonoridad" : "solo huella")) + ": " + vueltas + " lecturas comparadas, todo igual bit a bit" + (primera != null ? " -- PRIMERA DIFERENCIA: " + primera : ""));
            }
            Check(comparaciones > 400 && partidos > 150, "muchas lecturas (" + comparaciones + "), " + partidos + " de ellas partiendo un cuadro");

            // la comparacion se da cuenta: un solo bit cambiado en lo de antes
            {
                object f = Codigo.FormSinEncender(TF); Set(f, "curMeasurePath", "k"); Set(f, "eqMeasPath", "q"); Set(f, "eqMeasSum", new double[8]); Set(f, "eqMeasN", 0L);
                Antes a = new Antes(); float[] x = Senal(9000, 2, 99);
                mide.Invoke(f, new object[] { x, 0, 6000, 44100, 2 }); a.Mide((float[])x.Clone(), 0, 6000, 44100, 2);
                Check(Difiere(f, a, true, true) == null, "control: dos lecturas iguales no difieren");
                a.kwSt[3] = BitConverter.ToSingle(BitConverter.GetBytes(Bits(a.kwSt[3]) ^ 1), 0);
                Check(Difiere(f, a, true, true) == "kwSt[3]", "control: un bit cambiado en la memoria de los filtros K se descubre");
                a.kwSt[3] = BitConverter.ToSingle(BitConverter.GetBytes(Bits(a.kwSt[3]) ^ 1), 0);
                a.eqMeasSum[5] = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(a.eqMeasSum[5]) ^ 1L); Check(Difiere(f, a, true, true) == "eqMeasSum[5]", "control: un bit cambiado en una suma de la huella se descubre");
            }

            // cuanto cuesta: 20 s de estereo a 44,1 kHz, lecturas de 4410 muestras como las de la cadena
            {
                int rate = 44100, ch = 2; float[] x = Senal(rate * 20, ch, 5); int bloque = 4410;
                for (int rep = 0; rep < 4; rep++)
                {
                    object f = Codigo.FormSinEncender(TF); Set(f, "curMeasurePath", "k"); Set(f, "eqMeasPath", "q"); Set(f, "eqMeasSum", new double[8]); Set(f, "eqMeasN", 0L);
                    Antes a = new Antes();
                    System.Diagnostics.Stopwatch w = System.Diagnostics.Stopwatch.StartNew();
                    for (int p = 0; p + bloque <= x.Length; p += bloque) mide.Invoke(f, new object[] { x, p, bloque, rate, ch });
                    double t1 = w.Elapsed.TotalMilliseconds; w.Restart();
                    for (int p = 0; p + bloque <= x.Length; p += bloque) a.Mide(x, p, bloque, rate, ch);
                    double t0 = w.Elapsed.TotalMilliseconds;
                    if (rep == 0) continue;   // el primero calienta
                    if (msAhora == 0 || t1 < msAhora) msAhora = t1;
                    if (msAntes == 0 || t0 < msAntes) msAntes = t0;
                    muestrasTiempo = x.Length;
                }
                Console.WriteLine("  (medida, 20 s de estereo: de antes " + (msAntes / 20).ToString("0.00") + " ms por segundo, ahora " + (msAhora / 20).ToString("0.00") + " ms por segundo; mejor de tres; incluye el costo de llamar por reflexion y el de los metodos de prueba)");
                Check(msAhora <= msAntes * 1.1, "el aprendizaje de ahora no es mas lento que el de antes (con margen de medida)");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK entradaligera: " + ok + " comprobaciones" : "  FALLO entradaligera: " + mal + " fallos de " + (ok + mal));
        return mal;
    }
}

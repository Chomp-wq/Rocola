using System;
using System.Reflection;

// EL REPASO A FONDO, MAS LIGERO CON LOS MISMOS NUMEROS (13-10-2026; AudioControles.cs).
// La huella del EQ y la sonoridad K del repaso se hacian dentro del bucle de cada cuadro de MeasureFile. Ahora cada trozo
// se pasa de una vez: la huella banda por banda (HuellaDelTrozo) y la sonoridad cuadro a cuadro con los coeficientes en
// variables (SonoridadDelTrozo). Aqui se comparan con la cuenta de antes (AccumulateEqFrame cuadro a cuadro y la sonoridad
// escrita como estaba en el bucle), con la limpieza de restos diminutos en los mismos cuadros: tienen que dejar los
// MISMOS estados y las MISMAS sumas, bit a bit, en mono, estereo y 6 canales, con trozos de tamanos raros, empezando a
// mitad de la cuenta de 64 y con silencios largos (donde actua la limpieza).
class RepasoLigero
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static bool Igual(float[] a, float[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(a[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0)) return false; return true; }
    static bool Igual(double[] a, double[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (BitConverter.DoubleToInt64Bits(a[i]) != BitConverter.DoubleToInt64Bits(b[i])) return false; return true; }

    static float[] Senal(int cuadros, int ch, int semilla)
    {
        float[] x = new float[cuadros * ch]; Random r = new Random(semilla);
        for (int f = 0; f < cuadros; f++)
        {
            bool mudo = (f / 20000) % 3 == 2;   // cada tanto, un silencio digital largo: ahi actua la limpieza
            for (int c = 0; c < ch; c++)
                x[f * ch + c] = mudo ? 0f : (float)(0.4 * Math.Sin(f * (0.011 + 0.003 * c)) + 0.3 * (r.NextDouble() - 0.5) + (f % 997 == 0 ? 0.99 : 0));
        }
        return x;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true), td = asm.GetType("Rocola.Desnormales", true);
        MethodInfo acc = tf.GetMethod("AccumulateEqFrame", B), huella = tf.GetMethod("HuellaDelTrozo", B), son = tf.GetMethod("SonoridadDelTrozo", B);
        MethodInfo limpia = td.GetMethod("Limpia", B, null, new Type[] { typeof(float[]) }, null);
        Check(huella != null && son != null, "la rocola tiene la huella y la sonoridad por trozos");
        if (huella == null || son == null) { Console.WriteLine("  repasoligero: " + mal + " FALLOS de " + (ok + mal)); return 1; }
        int cada = (int)td.GetField("Cada", B).GetValue(null);
        float[] centros = (float[])tf.GetField("EqCentros", B).GetValue(null);
        MethodInfo bpf = tf.GetMethod("EqBpf", B), shelf = tf.GetMethod("EqShelfHi", B), hp = tf.GetMethod("EqHighPass", B);
        foreach (int fs in new int[] { 44100, 22050 })
        foreach (int ch in new int[] { 1, 2, 6 })
        {
            float[][] coef = new float[8][];
            for (int b = 0; b < 8; b++) coef[b] = (float[])bpf.Invoke(null, new object[] { centros[b], fs });
            float[] k1 = (float[])shelf.Invoke(null, new object[] { 1681.974f, fs, 3.99984f, 0.70717f }), k2 = (float[])hp.Invoke(null, new object[] { 38.1355f, fs, 0.50033f });
            float[] x = Senal(fs * 4, ch, fs + ch);
            int total = x.Length / ch;
            // La de antes: cuadro a cuadro, con la limpieza de la huella DESPUES del cuadro y la de la sonoridad ANTES.
            float[] stA = new float[ch * 32]; double[] sumA = new double[8];
            float[] kA = new float[ch * 8]; double sqA = 0; long nA = 0, clipA = 0; int[] runA = new int[ch];
            long contados = 37;   // empieza a mitad de la cuenta de 64
            for (int f = 0; f < total; f++)
            {
                acc.Invoke(null, new object[] { x, f * ch, ch, coef, stA, sumA });
                long n = contados + f + 1;
                if ((n & (cada - 1)) == 0) { limpia.Invoke(null, new object[] { stA }); limpia.Invoke(null, new object[] { kA }); }
                for (int c = 0; c < ch; c++)
                {
                    int b = c * 8; float v = x[f * ch + c];
                    float y1 = k1[0]*v + k1[1]*kA[b] + k1[2]*kA[b+1] - k1[3]*kA[b+2] - k1[4]*kA[b+3];
                    kA[b+1]=kA[b]; kA[b]=v; kA[b+3]=kA[b+2]; kA[b+2]=y1;
                    float y2 = k2[0]*y1 + k2[1]*kA[b+4] + k2[2]*kA[b+5] - k2[3]*kA[b+6] - k2[4]*kA[b+7];
                    kA[b+5]=kA[b+4]; kA[b+4]=y1; kA[b+7]=kA[b+6]; kA[b+6]=y2;
                    sqA += y2 * y2; nA++;
                    float ax = v < 0f ? -v : v;
                    if (ax >= 0.985f) { runA[c]++; if (runA[c] == 3) clipA += 3; else if (runA[c] > 3) clipA++; }
                    else runA[c] = 0;
                }
            }
            // La de ahora: por trozos de tamanos raros.
            float[] stB = new float[ch * 32]; double[] sumB = new double[8];
            float[] kB = new float[ch * 8]; int[] runB = new int[ch];
            int hecho = 0, t = 0; int[] tam = { 4096, 1, 333, 63, 64, 65, 2000, 4410 };
            long cont = contados;
            double sqB = 0; long nB = 0, clipB = 0;
            while (hecho < total)
            {
                int k = Math.Min(tam[t++ % tam.Length], total - hecho);
                float[] trozo = new float[k * ch]; Array.Copy(x, hecho * ch, trozo, 0, k * ch);
                huella.Invoke(null, new object[] { trozo, k, ch, coef, stB, sumB, cont });
                object[] a = { trozo, k, ch, k1, k2, kB, sqB, nB, clipB, runB, cont };
                son.Invoke(null, a);
                sqB = (double)a[6]; nB = (long)a[7]; clipB = (long)a[8];
                hecho += k; cont += k;
            }
            string q = fs + " Hz, " + ch + " canales";
            Check(Igual(sumA, sumB) && Igual(stA, stB), q + ": la huella del EQ deja las mismas sumas y la misma memoria, bit a bit");
            Check(BitConverter.DoubleToInt64Bits(sqA) == BitConverter.DoubleToInt64Bits(sqB) && nA == nB && clipA == clipB && Igual(kA, kB),
                  q + ": la sonoridad K y los recortes, los mismos (" + clipA + " / " + clipB + " recortes)");
            bool limpio = true; foreach (float v in stB) if (v != 0f && Math.Abs(v) < 1e-15f) limpio = false;
            Check(limpio, q + ": tras los silencios, sin restos diminutos en la huella");
        }
        Console.WriteLine(mal == 0 ? "  OK repasoligero: " + ok + " comprobaciones (mismos numeros, bit a bit)" : "  repasoligero: " + mal + " FALLOS de " + (ok + mal));
        return mal;
    }
}

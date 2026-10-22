using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// LA TRANSFORMADA DEL QUITASISEO (I2), CON LAS ETAPAS DE DOS EN DOS (22-10-2026; Siseo.cs, QuitaSiseo.Transforma).
//
// El quitasiseo calcula una transformada de Fourier de 1.024 puntos (2.048 por encima de 48 kHz) cada 512 muestras, y otra de vuelta en los
// trozos donde quita algo. Era lo que mas pesaba de la etapa (tres cuartas partes). Ahora los arreglos y las tablas van en variables locales
// (antes se leian del objeto en cada acceso), el seno ya trae su signo (negar es exacto) y las etapas de la mariposa van de dos en dos, con los
// cuatro valores en variables y no escritos y vueltos a leer.
//
// Aqui se compara el codigo de verdad con una copia del de antes (esta misma bateria lo lleva escrito, con las mismas tablas del objeto), con entradas
// de todo tipo: ruido, impulsos, continua, signos alternos, ceros con signo (-0), numeros diminutos (desnormales), enormes (que desbordan a infinito
// y NaN), vectores dispersos como los que ve la transformada de vuelta y los de la musica de verdad. Se exige que re e im sean iguales BIT A BIT,
// en las dos direcciones, a 32, 44,1, 48 y 96 kHz (1.024 y 2.048 puntos: con 2.048 queda una etapa suelta).
//
// Y a escala de la etapa entera: el mismo audio por la QuitaSiseo de verdad en lecturas de todos los tamanos da lo mismo troceado de cualquier manera
// (la transformada nueva no guarda estado), con un siseo de fondo que de verdad dispara la rebaja (se cuentan los trozos con rebaja).
class SiseoRapido
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }

    // ---------------- la copia del codigo de antes (git: 8b12902) ----------------
    static void Antes(double[] re, double[] im, int n, double[] cosT, double[] senT, int[] inversa, bool inversaT)
    {
        for (int i = 0; i < n; i++)
        {
            int j = inversa[i];
            if (j > i) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
        }
        for (int largo = 2; largo <= n; largo <<= 1)
        {
            int mitad = largo >> 1, paso = n / largo;
            for (int i = 0; i < n; i += largo)
                for (int k = 0; k < mitad; k++)
                {
                    double wr = cosT[k * paso], wi = inversaT ? senT[k * paso] : -senT[k * paso];
                    int a = i + k, b = a + mitad;
                    double xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi;
                    re[a] += xr; im[a] += xi;
                }
        }
    }

    class Fuente : ISampleProvider
    {
        readonly WaveFormat f; readonly float[] datos; int pos; readonly int trozo;
        public Fuente(int tasa, float[] d, int trozo) { f = WaveFormat.CreateIeeeFloatWaveFormat(tasa, 2); datos = d; this.trozo = trozo; }
        public WaveFormat WaveFormat { get { return f; } }
        public int Read(float[] b, int o, int c)
        {
            int n = Math.Min(Math.Min(c, trozo), datos.Length - pos);
            if (n <= 0) return 0;
            Array.Copy(datos, pos, b, o, n); pos += n; return n;
        }
    }

    static long Bits(double d) { return BitConverter.DoubleToInt64Bits(d); }
    static int Bits(float d) { return BitConverter.ToInt32(BitConverter.GetBytes(d), 0); }

    static Type tq; static MethodInfo mTransforma;
    static FieldInfo fRe, fIm, fCos, fSen, fInv, fN;

    static void Entrada(int caso, Random r, double[] re, double[] im, int n)
    {
        for (int i = 0; i < n; i++) { re[i] = 0.0; im[i] = 0.0; }
        switch (caso)
        {
            case 0: for (int i = 0; i < n; i++) { re[i] = r.NextDouble() * 2 - 1; im[i] = r.NextDouble() * 2 - 1; } break;
            case 1: re[r.Next(n)] = 1.0; break;
            case 2: for (int i = 0; i < n; i++) { re[i] = 0.25; im[i] = -0.125; } break;
            case 3: for (int i = 0; i < n; i++) { re[i] = (i & 1) == 0 ? 1.0 : -1.0; im[i] = (i & 2) == 0 ? 0.5 : -0.5; } break;
            case 4: for (int i = 0; i < n; i++) { re[i] = -0.0; im[i] = (i % 3 == 0) ? -0.0 : 0.0; } re[5] = 1.0; break;           // ceros con signo
            case 5: for (int i = 0; i < n; i++) { re[i] = (r.NextDouble() * 2 - 1) * 1e-310; im[i] = (r.NextDouble() * 2 - 1) * 1e-312; } break;   // desnormales
            case 6: for (int i = 0; i < n; i++) { re[i] = (r.NextDouble() * 2 - 1) * 1e300; im[i] = (r.NextDouble() * 2 - 1) * 1e300; } break;     // desborda
            case 7:   // dispersos como los de la vuelta: solo unas bandas (y su espejo), el resto cero exacto
                for (int b = 0; b < 6; b++)
                {
                    int ini = 70 + r.Next(300), fin = ini + 2 + r.Next(60);
                    for (int k = ini; k < fin && k < n / 2 - 1; k++)
                    {
                        double rr = (r.NextDouble() * 2 - 1) * 0.01, ri = (r.NextDouble() * 2 - 1) * 0.01;
                        re[k] += rr; im[k] += ri; re[n - k] += rr; im[n - k] -= ri;
                    }
                }
                break;
            case 8: for (int i = 0; i < n; i++) { re[i] = Math.Sin(i * 0.37) * 0.5 + (r.NextDouble() - 0.5) * 1e-4; im[i] = Math.Cos(i * 0.011) * 0.25; } break;
            case 9: for (int i = 0; i < n; i++) { re[i] = double.PositiveInfinity; im[i] = (i & 1) == 0 ? double.NaN : -1.0; } break;
            default: for (int i = 0; i < n; i++) { re[i] = r.Next(-3, 4); im[i] = r.Next(-3, 4); } break;   // enteros pequenos: muchas sumas exactas y ceros
        }
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        tq = asm.GetType("Rocola.QuitaSiseo", false);
        Check(tq != null, "la rocola trae QuitaSiseo");
        if (tq == null) { Console.WriteLine("  FALLO siseorapido: esta rocola no tiene QuitaSiseo"); return 1; }
        mTransforma = tq.GetMethod("Transforma", T);
        fRe = tq.GetField("re", T); fIm = tq.GetField("im", T); fCos = tq.GetField("cosT", T); fSen = tq.GetField("senT", T); fInv = tq.GetField("inversa", T); fN = tq.GetField("n", T);
        Check(mTransforma != null && fRe != null && fIm != null && fCos != null && fSen != null && fInv != null && fN != null, "QuitaSiseo trae Transforma y sus arreglos");
        if (mal > 0) return 1;

        // 1) la transformada sola, contra la copia de antes, a cuatro frecuencias y en las dos direcciones
        foreach (int tasa in new int[] { 32000, 44100, 48000, 96000 })
        {
            object q = Activator.CreateInstance(tq, new object[] { new Fuente(tasa, new float[8], 8) });
            int n = (int)fN.GetValue(q);
            Check(n == (tasa > 48000 ? 2048 : 1024), tasa + " Hz: la transformada es de " + (tasa > 48000 ? 2048 : 1024) + " puntos (" + n + ")");
            double[] re = (double[])fRe.GetValue(q), im = (double[])fIm.GetValue(q);
            double[] cosT = (double[])fCos.GetValue(q), senT = (double[])fSen.GetValue(q);
            int[] inv = (int[])fInv.GetValue(q);
            Random r = new Random(100 + tasa);
            int iguales = 0, distintos = 0;
            for (int rep = 0; rep < 40; rep++)
                for (int caso = 0; caso <= 10; caso++)
                    foreach (bool vuelta in new bool[] { false, true })
                    {
                        double[] a = new double[n], b = new double[n];
                        Entrada(caso, r, a, b, n);
                        double[] ar = (double[])a.Clone(), ai = (double[])b.Clone();
                        Antes(ar, ai, n, cosT, senT, inv, vuelta);
                        Array.Copy(a, re, n); Array.Copy(b, im, n);
                        mTransforma.Invoke(q, new object[] { vuelta });
                        bool igual = true;
                        for (int i = 0; i < n && igual; i++) if (Bits(re[i]) != Bits(ar[i]) || Bits(im[i]) != Bits(ai[i])) igual = false;
                        if (igual) iguales++; else { distintos++; if (distintos <= 3) Console.WriteLine("  FALLO " + tasa + " Hz, caso " + caso + (vuelta ? " (vuelta)" : " (ida)") + ": difiere"); }
                    }
            Check(distintos == 0, tasa + " Hz: " + iguales + " transformadas iguales bit a bit a las de antes (" + distintos + " distintas)");
        }

        // 2) la etapa entera: el mismo audio troceado de todas las maneras sale igual (la transformada nueva no guarda estado)
        {
            int tasa = 44100, segundos = 14, cuadros = tasa * segundos;
            float[] audio = new float[cuadros * 2];
            Random r = new Random(7);
            // un siseo parejo y bajito, con un tono que suena a ratos: hay trozos pegados al piso (rebaja) y trozos con musica
            for (int f = 0; f < cuadros; f++)
            {
                double t = f / (double)tasa;
                double musica = ((int)(t * 2) % 3 == 0) ? 0.2 * Math.Sin(2 * Math.PI * 440 * t) + 0.1 * Math.Sin(2 * Math.PI * 5200 * t) : 0.0;
                audio[2 * f] = (float)(musica + (r.NextDouble() * 2 - 1) * 0.0008);
                audio[2 * f + 1] = (float)(musica * 0.8 + (r.NextDouble() * 2 - 1) * 0.0008);
            }
            string primero = null;
            foreach (int troceo in new int[] { 4410, 1, 1023, 50000 })
            {
                object q = Activator.CreateInstance(tq, new object[] { new Fuente(tasa, audio, troceo > 2 ? troceo : 2) });
                ISampleProvider sp = (ISampleProvider)q;
                float[] buf = new float[4410];
                ulong h = 14695981039346656037UL; long total = 0;
                while (true)
                {
                    int n = sp.Read(buf, 0, buf.Length); if (n <= 0) break;
                    for (int i = 0; i < n; i++) { h ^= (uint)Bits(buf[i]); h *= 1099511628211UL; }
                    total += n;
                }
                long trozos = (long)tq.GetField("TrozosConQuita", T).GetValue(q);
                string firma = h.ToString("X16") + "/" + total;
                Console.WriteLine("  troceo " + troceo + ": " + firma + ", " + trozos + " trozos con rebaja");
                if (primero == null) { primero = firma; Check(trozos > 100, "el siseo de fondo dispara la rebaja (" + trozos + " trozos): se prueba la transformada de vuelta"); }
                else Check(firma == primero, "troceo " + troceo + ": la misma salida, bit a bit, que con 4.410");
            }
        }

        Console.WriteLine();
        Console.WriteLine(mal == 0 ? "OK siseorapido: " + ok + " comprobaciones" : "FALLAN " + mal + " de " + (ok + mal));
        return mal == 0 ? 0 : 1;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// SONORIDAD INTEGRADA (ITU-R BS.1770) Y ESTIMACION RAPIDA: QUE CADA UNA SEA LO QUE DICE.
//
// La rocola tenia una sola medida de volumen, la "estimacion": tres tramos de 20 s con un filtro parecido
// al de la norma y sin puertas. Ahora mide ademas la cancion ENTERA como manda la norma y, cuando esa
// medida existe, nivela con ella. Esta bateria comprueba:
//
//   1) la ponderacion K: los coeficientes de la norma a 48 kHz y la misma respuesta a otras frecuencias;
//   2) los casos de conformidad 1 a 6 de EBU Tech 3341 (tonos de 1 kHz), a seis frecuencias de muestreo
//      y con lecturas de cualquier tamano, incluidas las que parten un cuadro entre dos canales;
//   3) los canales: mono, estereo, 5.1, 7.1, LFE fuera, y que sin mascara no se adivina la disposicion;
//   4) las puertas, el silencio y los archivos que no llegan a un bloque;
//   5) contra una referencia INDEPENDIENTE en Python (pruebas/ref_bs1770.py), con senales de programa;
//   6) la tabla versionada rocola_sonoridad.txt: no se pierde nada ni se pisa lo que no se entiende;
//   7) que solo se publica lo que se midio ENTERO (ni cancelado, ni cortado, ni de canales desconocidos),
//      y que la estimacion rapida sigue siendo EXACTAMENTE la de antes;
//   8) el nivelado: usa la integrada si esta, con los mismos topes y sin tocar la cancion que suena;
//   9) los minimos por DURACION (20 s de huella, 9 s de estimacion) a varias frecuencias;
//  10) las lecturas que parten un cuadro: ni se pierden muestras ni se cruzan canales;
//  11) que la tanda de la cancion entera respeta la musica, la pantalla y las caratulas;
//  12) lo que cuesta.
//
// Lo que NO prueba: los casos 7 y 8 de EBU Tech 3341 son grabaciones reales que se distribuyen como
// archivos y no se pueden descargar desde aqui; ni si el nivelado SUENA mejor (eso es escucha A/B).
class Sonoridad
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TM, TD, TS;
    static int fallos, oks;
    static void Check(bool ok, string q)
    {
        Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q);
        if (ok) oks++; else fallos++;
    }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }

    // ---------------------------------------------------------------------------------------------
    //  Acceso al medidor (internal) por reflexion, con delegados para no pagar Invoke por muestra.
    // ---------------------------------------------------------------------------------------------
    delegate void Procesa(float[] d, int o, int n);
    sealed class Med
    {
        public object M; public Procesa P;
        public Med(int fs, double[] pesos)
        {
            M = Activator.CreateInstance(TM, T, null, new object[] { fs, pesos }, null);
            P = (Procesa)Delegate.CreateDelegate(typeof(Procesa), M, TM.GetMethod("Procesa", T));
        }
        public bool Integrada(out double lkfs, out int usados)
        {
            object[] a = { 0.0, 0 };
            bool r = (bool)TM.GetMethod("Integrada", T).Invoke(M, a);
            lkfs = (double)a[0]; usados = (int)a[1];
            return r;
        }
        public double Lk()
        {
            double l; int u;
            if (!Integrada(out l, out u)) return double.NaN;
            return l;
        }
        public int Bloques { get { return (int)TM.GetProperty("Bloques", T).GetValue(M, null); } }
    }
    static double Mide(int fs, double[] pesos, float[] x, int trozo)
    {
        Med m = new Med(fs, pesos);
        if (trozo <= 0) m.P(x, 0, x.Length);
        else for (int i = 0; i < x.Length; i += trozo) m.P(x, i, Math.Min(trozo, x.Length - i));
        return m.Lk();
    }

    // ---------------------------------------------------------------------------------------------
    //  Senales
    // ---------------------------------------------------------------------------------------------
    // Tono de 1 kHz, todos los canales en fase, cada uno a su nivel de pico (dBFS). Tiempo continuo.
    static void Tono(List<float> dst, int fs, double[] dbCanal, double seg, ref long n0)
    {
        int ch = dbCanal.Length;
        long n = (long)Math.Round(seg * fs);
        double[] a = new double[ch];
        for (int c = 0; c < ch; c++) a[c] = Math.Pow(10.0, dbCanal[c] / 20.0);
        for (long i = 0; i < n; i++)
        {
            double s = Math.Sin(2.0 * Math.PI * 1000.0 * (n0 + i) / fs);
            for (int c = 0; c < ch; c++) dst.Add((float)(a[c] * s));
        }
        n0 += n;
    }
    static double[] Est(double db) { return new double[] { db, db }; }
    // Casos 1 a 6 de EBU Tech 3341 (sonoridad integrada, tonos de 1 kHz). Devuelve la senal y lo esperado.
    static float[] Caso(int k, int fs, out double[] pesos, out double esperado)
    {
        List<float> s = new List<float>(); long n0 = 0;
        pesos = new double[] { 1, 1 }; esperado = -23.0;
        switch (k)
        {
            case 1: Tono(s, fs, Est(-23), 20, ref n0); break;
            case 2: Tono(s, fs, Est(-33), 20, ref n0); esperado = -33.0; break;
            case 3: Tono(s, fs, Est(-36), 10, ref n0); Tono(s, fs, Est(-23), 60, ref n0); Tono(s, fs, Est(-36), 10, ref n0); break;
            case 4:
                Tono(s, fs, Est(-72), 10, ref n0); Tono(s, fs, Est(-36), 10, ref n0); Tono(s, fs, Est(-23), 60, ref n0);
                Tono(s, fs, Est(-36), 10, ref n0); Tono(s, fs, Est(-72), 10, ref n0); break;
            case 5: Tono(s, fs, Est(-26), 20, ref n0); Tono(s, fs, Est(-20), 20.1, ref n0); Tono(s, fs, Est(-26), 20, ref n0); break;
            case 6:
                // 5.0: L, R, C, Ls, Rs (sin LFE), con los envolventes pesando 1,41.
                Tono(s, fs, new double[] { -28, -28, -24, -30, -30 }, 20, ref n0);
                pesos = new double[] { 1, 1, 1, 1.41, 1.41 }; break;
        }
        return s.ToArray();
    }
    // Ruido determinista (generador congruente) con un nivel RMS por tramo; canales independientes.
    sealed class Lcg
    {
        uint s;
        public Lcg(uint semilla) { s = semilla; }
        public double Uniforme() { s = s * 1664525u + 1013904223u; return (s >> 8) / 16777216.0; }
        public double Gauss() { double u = Math.Max(1e-12, Uniforme()), v = Uniforme(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }
    }
    static float[] Programa(int fs, int ch, double[][] tramos, uint semilla)
    {
        Lcg g = new Lcg(semilla);
        List<float> s = new List<float>();
        foreach (double[] t in tramos)   // { dB RMS (o -999 = cero digital), segundos }
        {
            long n = (long)Math.Round(t[1] * fs);
            double a = t[0] <= -900 ? 0 : Math.Pow(10.0, t[0] / 20.0);
            for (long i = 0; i < n; i++)
                for (int c = 0; c < ch; c++) s.Add((float)(a * g.Gauss()));
        }
        return s.ToArray();
    }

    // ---------------------------------------------------------------------------------------------
    //  1) PONDERACION K
    // ---------------------------------------------------------------------------------------------
    static readonly double[] Estante48 = { 1.53512485958697, -2.69169618940638, 1.19839281085285, -1.69065929318241, 0.73248077421585 };
    static readonly double[] Rlb48 = { 1.0, -2.0, 1.0, -1.99004745483398, 0.99007225036621 };
    static double Modulo(double[] c, double f, double fs)
    {
        double w = 2 * Math.PI * f / fs;
        double zr = Math.Cos(w), zi = -Math.Sin(w);           // z^-1
        double z2r = zr * zr - zi * zi, z2i = 2 * zr * zi;    // z^-2
        double nr = c[0] + c[1] * zr + c[2] * z2r, ni = c[1] * zi + c[2] * z2i;
        double dr = 1 + c[3] * zr + c[4] * z2r, di = c[3] * zi + c[4] * z2i;
        return Math.Sqrt((nr * nr + ni * ni) / (dr * dr + di * di));
    }
    static void Ponderacion()
    {
        Console.WriteLine("\n  1) LA PONDERACION K\n");
        double[] e = (double[])TM.GetMethod("Estante", T).Invoke(null, new object[] { 48000.0 });
        double[] r = (double[])TM.GetMethod("PasoAlto", T).Invoke(null, new object[] { 48000.0 });
        double dif = 0;
        for (int i = 0; i < 5; i++) { dif = Math.Max(dif, Math.Abs(e[i] - Estante48[i])); dif = Math.Max(dif, Math.Abs(r[i] - Rlb48[i])); }
        Check(dif < 1e-12, "a 48 kHz, los coeficientes de la rocola son los de la tabla de la norma (diferencia maxima " + dif.ToString("0.0e0") + ")");
        // A otra frecuencia: la misma respuesta que la tabla de 48 kHz, frecuencia por frecuencia (hasta 0,45 fs).
        int[] tasas = { 8000, 11025, 16000, 22050, 32000, 44100, 88200, 96000, 192000 };
        bool bien = true;
        string linea = "";
        foreach (int fs in tasas)
        {
            double[] e2 = (double[])TM.GetMethod("Estante", T).Invoke(null, new object[] { (double)fs });
            double[] r2 = (double[])TM.GetMethod("PasoAlto", T).Invoke(null, new object[] { (double)fs });
            double peor = 0;
            for (int k = 0; k <= 400; k++)
            {
                double f = 10.0 * Math.Pow(0.45 * fs / 10.0, k / 400.0);
                if (f > 23999) break;
                double a = Modulo(e2, f, fs) * Modulo(r2, f, fs), b = Modulo(Estante48, f, 48000) * Modulo(Rlb48, f, 48000);
                peor = Math.Max(peor, Math.Abs(20 * Math.Log10(a / b)));
            }
            linea += fs + ": " + F(peor, "0.000") + "  ";
            if (fs >= 22050 && peor > 0.1) bien = false;
        }
        Info("desvio maximo frente a la tabla de 48 kHz, en dB: " + linea);
        Check(bien, "de 22,05 a 192 kHz la respuesta no se aparta mas de 0,1 dB de la de la norma (a 8-16 kHz se informa: cerca de Nyquist la bilineal se comprime)");
    }

    // ---------------------------------------------------------------------------------------------
    //  2) EBU TECH 3341, CASOS 1 A 6, A SEIS FRECUENCIAS Y CON LECTURAS DE CUALQUIER TAMANO
    // ---------------------------------------------------------------------------------------------
    static void Conformidad()
    {
        Console.WriteLine("\n  2) EBU TECH 3341: CASOS 1 A 6 (tolerancia de la norma: +-0,1 LU)\n");
        int[] tasas = { 22050, 32000, 44100, 48000, 88200, 96000 };
        bool todos = true; double peor = 0;
        foreach (int fs in tasas)
        {
            string linea = "";
            for (int k = 1; k <= 6; k++)
            {
                double[] p; double esp;
                float[] x = Caso(k, fs, out p, out esp);
                double l = Mide(fs, p, x, 0);
                double d = l - esp;
                linea += k + ": " + F(d, "+0.000;-0.000") + "  ";
                if (!(Math.Abs(d) <= 0.1)) todos = false;
                peor = Math.Max(peor, Math.Abs(d));
            }
            Info(fs + " Hz   " + linea);
        }
        Check(todos, "los seis casos dentro de +-0,1 LU a las seis frecuencias (peor desvio " + F(peor, "0.000") + " LU)");

        // Lecturas de cualquier tamano: el resultado tiene que ser IDENTICO, bit a bit. Los tamanos impares
        // parten cuadros entre dos lecturas (un canal en una, el otro en la siguiente).
        {
            double[] p; double esp;
            float[] x = Caso(5, 44100, out p, out esp);
            double entero = Mide(44100, p, x, 0);
            int[] trozos = { 1, 3, 441 * 2 + 1, 4096 * 2, 65537 };
            bool iguales = true;
            foreach (int t in trozos) if (Mide(44100, p, x, t) != entero) iguales = false;
            // y trozos al azar
            Med m = new Med(44100, p);
            Random rnd = new Random(7);
            for (int i = 0; i < x.Length; ) { int t = Math.Min(1 + rnd.Next(9000), x.Length - i); m.P(x, i, t); i += t; }
            if (m.Lk() != entero) iguales = false;
            Check(iguales, "caso 5 leido de golpe, de 1 en 1, de 3 en 3, de 883 en 883, de 8192, de 65537 y al azar: el mismo resultado bit a bit");
            float[] x6 = Caso(6, 48000, out p, out esp);
            double e6 = Mide(48000, p, x6, 0);
            Check(Mide(48000, p, x6, 7) == e6 && Mide(48000, p, x6, 5 * 4096 + 2) == e6,
                  "caso 6 (cinco canales) en trozos de 7 y de 20482 muestras: identico (ningun canal se pierde ni se cruza)");
        }
    }

    // ---------------------------------------------------------------------------------------------
    //  3) CANALES Y DISPOSICION
    // ---------------------------------------------------------------------------------------------
    static double[] DeMascara(int mascara, int ch) { return (double[])TD.GetMethod("DeMascara", T).Invoke(null, new object[] { mascara, ch }); }
    static double[] DeArchivo(string ruta, int ch) { return (double[])TD.GetMethod("DeArchivo", T).Invoke(null, new object[] { ruta, ch }); }
    static bool Igual(double[] a, double[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static byte[] Wav(int formato, int canales, int mascara, bool junk, bool rf64, bool corto)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter w = new BinaryWriter(ms);
        w.Write(System.Text.Encoding.ASCII.GetBytes(rf64 ? "RF64" : "RIFF")); w.Write(0); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        if (junk) { w.Write(System.Text.Encoding.ASCII.GetBytes("JUNK")); w.Write(27); w.Write(new byte[28]); }   // 27 + relleno
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bool ext = formato == 0xFFFE;
        w.Write(ext ? 40 : 16);
        w.Write((short)formato); w.Write((short)canales); w.Write(48000); w.Write(48000 * canales * 2); w.Write((short)(canales * 2)); w.Write((short)16);
        if (ext) { w.Write((short)22); w.Write((short)16); w.Write(mascara); w.Write(new byte[] { 1, 0, 0, 0, 0, 0, 0x10, 0, 0x80, 0, 0, 0xAA, 0, 0x38, 0x9B, 0x71 }); }
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(0);
        byte[] b = ms.ToArray();
        if (corto) Array.Resize(ref b, 30);
        return b;
    }
    static void Canales(string dir)
    {
        Console.WriteLine("\n  3) CANALES: CADA UNO CON EL PESO DE SU ALTAVOZ, Y SIN ADIVINAR\n");
        // Mono y el mismo tema en estereo (los dos canales iguales): BS.1770 da 3,01 LU mas al estereo,
        // pero por los altavoces suenan igual; la escala del nivelado (por canal) tiene que darlos iguales.
        {
            List<float> mono = new List<float>(), est = new List<float>(); long a = 0, b = 0;
            Tono(mono, 44100, new double[] { -20 }, 5, ref a); Tono(est, 44100, Est(-20), 5, ref b);
            double lm = Mide(44100, new double[] { 1 }, mono.ToArray(), 0), le = Mide(44100, new double[] { 1, 1 }, est.ToArray(), 0);
            Check(Math.Abs(le - lm - 10 * Math.Log10(2)) < 1e-9, "el mismo tono en estereo mide 3,0103 LU mas que en mono (" + F(le - lm, "0.0000") + ")");
            object sm = Activator.CreateInstance(TS, T, null, new object[] { 1, (float)lm, 1f, 10 }, null);
            object se = Activator.CreateInstance(TS, T, null, new object[] { 1, (float)le, 2f, 10 }, null);
            float rm = (float)TS.GetProperty("RmsPorCanal", T).GetValue(sm, null), re = (float)TS.GetProperty("RmsPorCanal", T).GetValue(se, null);
            Check(Math.Abs(rm / re - 1) < 1e-5, "y en la escala del nivelado (por canal) los dos quedan iguales: suenan igual, reciben lo mismo");
        }
        Check(Igual(DeMascara(0x3F, 6), new double[] { 1, 1, 1, 0, 1.41, 1.41 }), "5.1 clasico (FL FR FC LFE BL BR): LFE fuera, traseros como envolventes a 1,41");
        Check(Igual(DeMascara(0x60F, 6), new double[] { 1, 1, 1, 0, 1.41, 1.41 }), "5.1 con laterales (FL FR FC LFE SL SR): laterales a 1,41");
        Check(Igual(DeMascara(0x63F, 8), new double[] { 1, 1, 1, 0, 1, 1, 1.41, 1.41 }), "7.1 (con traseros Y laterales): los laterales a 1,41 y los traseros a 1");
        Check(Igual(DeMascara(0x0C, 2), new double[] { 1, 0 }), "dos canales que la mascara dice centro y LFE: el LFE no cuenta");
        Check(DeMascara(0x3F, 5) == null && DeMascara(0x3, 3) == null, "mascara que no cuadra con los canales: no se mide");
        Check(DeMascara(0x40000 | 0x3, 3) == null && DeMascara(unchecked((int)0x80000003), 3) == null, "mascara con altavoces desconocidos: no se mide");
        Check(DeMascara(0x8, 1) == null, "solo LFE: no hay nada que medir");
        Check(Igual((double[])TD.GetMethod("PorFormato", T).Invoke(null, new object[] { 6 }), null),
              "seis canales sin mascara: NO se supone 5.1 (no se deduce la disposicion del numero de canales)");
        // El LFE de verdad no suma: un 5.1 con el LFE a todo volumen mide lo mismo que sin el.
        {
            List<float> a = new List<float>(), b = new List<float>(); long n1 = 0, n2 = 0;
            Tono(a, 48000, new double[] { -28, -28, -24, -120, -30, -30 }, 3, ref n1);
            Tono(b, 48000, new double[] { -28, -28, -24, -3, -30, -30 }, 3, ref n2);
            double[] p = DeMascara(0x3F, 6);
            Check(Mide(48000, p, a.ToArray(), 0) == Mide(48000, p, b.ToArray(), 0), "un LFE a -3 dBFS no mueve la medida ni una centesima");
        }
        // Cabeceras WAV de verdad (en disco): mascara, trozos antes de 'fmt ', RF64 y cabeceras rotas.
        string f = Path.Combine(dir, "c.wav");
        File.WriteAllBytes(f, Wav(0xFFFE, 6, 0x3F, true, false, false));
        Check(Igual(DeArchivo(f, 6), new double[] { 1, 1, 1, 0, 1.41, 1.41 }), "WAV extensible 5.1 con un trozo JUNK impar delante: se lee la mascara");
        Check(DeArchivo(f, 2) == null, "si el lector dice otro numero de canales que la cabecera: no se mide");
        File.WriteAllBytes(f, Wav(0xFFFE, 6, 0, false, false, false));
        Check(DeArchivo(f, 6) == null, "WAV extensible de seis canales con mascara 0 (sin asignar): no se mide");
        File.WriteAllBytes(f, Wav(1, 6, 0, false, false, false));
        Check(DeArchivo(f, 6) == null, "WAV PCM clasico de seis canales (sin mascara): no se mide");
        File.WriteAllBytes(f, Wav(1, 2, 0, false, true, false));
        Check(Igual(DeArchivo(f, 2), new double[] { 1, 1 }), "RF64 estereo: izquierdo y derecho");
        File.WriteAllBytes(f, Wav(0xFFFE, 2, 0x0C, false, false, false));
        Check(Igual(DeArchivo(f, 2), new double[] { 1, 0 }), "WAV estereo cuya mascara dice centro+LFE: se respeta la mascara");
        File.WriteAllBytes(f, Wav(1, 2, 0, false, false, true));
        Check(DeArchivo(f, 2) == null, "WAV con la cabecera cortada: no se adivina");
        Check(Igual(DeArchivo(Path.Combine(dir, "x.mp3"), 2), new double[] { 1, 1 }) && Igual(DeArchivo(Path.Combine(dir, "x.mp3"), 1), new double[] { 1 }),
              "MP3 (y demas sin mascara): 1 canal es mono y 2 son izquierdo/derecho, por definicion del formato");
    }

    // ---------------------------------------------------------------------------------------------
    //  4) PUERTAS, SILENCIO Y ARCHIVOS CORTOS
    // ---------------------------------------------------------------------------------------------
    static void Puertas()
    {
        Console.WriteLine("\n  4) PUERTAS, SILENCIO Y LO QUE NO LLEGA A UN BLOQUE\n");
        double[] p2 = { 1, 1 };
        double l; int u;
        Med m = new Med(48000, p2); m.P(new float[48000 * 2 * 3], 0, 48000 * 2 * 3);
        Check(m.Integrada(out l, out u) && double.IsNegativeInfinity(l) && u == 0, "tres segundos de cero digital: medida valida y MUDA (-infinito), no un numero inventado");
        List<float> s = new List<float>(); long n0 = 0;
        Tono(s, 48000, Est(-80), 5, ref n0);
        m = new Med(48000, p2); m.P(s.ToArray(), 0, s.Count);
        Check(m.Integrada(out l, out u) && double.IsNegativeInfinity(l), "un tono a -80 dBFS (todo bajo la puerta absoluta de -70): muda");
        // Menos de 400 ms: ni un bloque; 400 ms justos: uno.
        m = new Med(44100, p2); m.P(new float[(17640 - 1) * 2], 0, (17640 - 1) * 2);
        Check(!m.Integrada(out l, out u) && m.Bloques == 0, "399,98 ms: no hay ni un bloque, no hay medida");
        s.Clear(); n0 = 0; Tono(s, 44100, Est(-20), 0.4, ref n0);
        m = new Med(44100, p2); m.P(s.ToArray(), 0, s.Count);
        Check(m.Integrada(out l, out u) && m.Bloques == 1 && u == 1, "400 ms justos: un bloque");
        // 11,025 kHz: 100 ms no son un numero entero de muestras; cada bloque sigue midiendo 400 ms.
        s.Clear(); n0 = 0; Tono(s, 11025, Est(-23), 20, ref n0);
        m = new Med(11025, p2); m.P(s.ToArray(), 0, s.Count);
        Check(m.Integrada(out l, out u) && m.Bloques == 197 && Math.Abs(l + 23.0) <= 0.1,
              "a 11,025 kHz: 197 bloques en 20 s y caso 1 en " + F(l, "0.000") + " LKFS");
        // La puerta relativa: un tramo 20 LU por debajo NO cuenta; uno 5 LU por debajo SI.
        // Con el tramo bajo fuera quedan 197 bloques enteros del alto y los TRES que pisan el cambio (con
        // 300, 200 y 100 ms del alto), que si pasan la puerta: la cuenta exacta es esa, no "igual que solo".
        s.Clear(); n0 = 0; Tono(s, 48000, Est(-20), 20, ref n0); Tono(s, 48000, Est(-40), 20, ref n0);
        double conBajo = Mide(48000, p2, s.ToArray(), 0);
        s.Clear(); n0 = 0; Tono(s, 48000, Est(-20), 20, ref n0);
        double solo = Mide(48000, p2, s.ToArray(), 0);
        double esperado = solo + 10 * Math.Log10((197 + 0.75 + 0.5 + 0.25 + 0.01 * (0.25 + 0.5 + 0.75)) / 200.0);
        Check(Math.Abs(conBajo - esperado) < 0.002, "20 s a -20 + 20 s a -40: la puerta relativa deja fuera el tramo bajo y cuenta los 3 bloques del cambio ("
              + F(conBajo, "0.0000") + ", esperado " + F(esperado, "0.0000") + "; con el tramo bajo dentro seria ~" + F(solo - 3.0, "0.0") + ")");
        s.Clear(); n0 = 0; Tono(s, 48000, Est(-20), 20, ref n0); Tono(s, 48000, Est(-25), 20, ref n0);
        double conMedio = Mide(48000, p2, s.ToArray(), 0);
        Check(conMedio < solo - 1.0, "20 s a -20 + 20 s a -25: ese SI cuenta y baja la media (" + F(conMedio, "0.000") + ")");
    }

    // ---------------------------------------------------------------------------------------------
    //  5) CONTRA LA REFERENCIA INDEPENDIENTE EN PYTHON
    // ---------------------------------------------------------------------------------------------
    static string Python(string args, out int codigo)
    {
        codigo = -1;
        string py = Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON");
        if (string.IsNullOrEmpty(py)) py = "python3";
        try
        {
            ProcessStartInfo si = new ProcessStartInfo(py, args);
            si.UseShellExecute = false; si.RedirectStandardOutput = true; si.RedirectStandardError = true; si.CreateNoWindow = true;
            using (Process p = Process.Start(si))
            {
                string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                p.WaitForExit(); codigo = p.ExitCode;
                return o;
            }
        }
        catch { return null; }
    }
    static void EscribeWav(string ruta, int fs, int ch, float[] x)
    {
        using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, ch)))
            w.WriteSamples(x, 0, x.Length);
    }
    static void Referencia(string dir)
    {
        Console.WriteLine("\n  5) CONTRA UNA REFERENCIA INDEPENDIENTE (pruebas/ref_bs1770.py)\n");
        int cod;
        string o = Python("-c \"import numpy\"", out cod);
        if (o == null || cod != 0)
        {
            Check(true, "(sin Python con numpy: la comparacion con la referencia independiente se salta y se dice)");
            return;
        }
        string tabla = Python("ref_bs1770.py --tabla", out cod);
        Check(cod == 0 && tabla != null && tabla.Contains("1.53512485958697"), "la referencia lleva la tabla de 48 kHz de la norma");
        double[][] programa1 = { new double[] { -20, 10 }, new double[] { -45, 5 }, new double[] { -999, 3 }, new double[] { -30, 7 }, new double[] { -80, 5.35 } };
        double[][] programa2 = { new double[] { -26, 4.1 }, new double[] { -14, 2.3 }, new double[] { -31, 6 } };
        object[][] casos = {
            new object[] { "estereo 48 kHz: tramos, cero digital y bajo la puerta absoluta", 48000, 2, programa1, new double[] { 1, 1 }, 0.002 },
            new object[] { "mono 44,1 kHz", 44100, 1, programa2, new double[] { 1 }, 0.05 },
            new object[] { "5.0 a 32 kHz con envolventes a 1,41", 32000, 5, programa2, new double[] { 1, 1, 1, 1.41, 1.41 }, 0.05 },
            new object[] { "5.1 a 48 kHz, LFE fuera (peso 0)", 48000, 6, programa2, new double[] { 1, 1, 1, 0, 1.41, 1.41 }, 0.002 },
            new object[] { "estereo 96 kHz", 96000, 2, programa2, new double[] { 1, 1 }, 0.05 },
            new object[] { "estereo 22,05 kHz", 22050, 2, programa1, new double[] { 1, 1 }, 0.1 },
        };
        uint semilla = 11;
        foreach (object[] c in casos)
        {
            int fs = (int)c[1], ch = (int)c[2]; double[] p = (double[])c[4]; double tol = (double)c[5];
            float[] x = Programa(fs, ch, (double[][])c[3], semilla++);
            string ruta = Path.Combine(dir, "ref.wav");
            EscribeWav(ruta, fs, ch, x);
            string pesos = "";
            for (int i = 0; i < p.Length; i++) pesos += (i > 0 ? "," : "") + F(p[i], "0.00");
            string r = Python("ref_bs1770.py \"" + ruta + "\" --pesos " + pesos, out cod);
            double py = double.NaN;
            if (r != null && r.StartsWith("LKFS ")) double.TryParse(r.Substring(5).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out py);
            double cs = Mide(fs, p, x, 4096 * ch + 1);
            Check(!double.IsNaN(py) && Math.Abs(cs - py) <= tol,
                  (string)c[0] + ": rocola " + F(cs, "0.0000") + ", referencia " + F(py, "0.0000") + " (tolerancia " + F(tol, "0.000") + ")");
        }
    }

    // ---------------------------------------------------------------------------------------------
    //  Acceso a Form1 (armada sin encender) por reflexion
    // ---------------------------------------------------------------------------------------------
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }
    static object Integ(int alg, float lk, float pesos, int bloques) { return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, pesos, bloques }, null); }
    static float Campo(object s, string n) { return Convert.ToSingle(TS.GetField(n, T).GetValue(s)); }
    static float RmsPorCanal(object s) { return (float)TS.GetProperty("RmsPorCanal", T).GetValue(s, null); }
    static System.Collections.IDictionary Tabla(object f) { return (System.Collections.IDictionary)Get(f, "integCache"); }
    static void SinTareas() { TF.GetField("measureCancellation", T).SetValue(null, null); }

    // ---------------------------------------------------------------------------------------------
    //  6) LA TABLA VERSIONADA
    // ---------------------------------------------------------------------------------------------
    static System.Collections.IDictionary TablaNueva()
    {
        return (System.Collections.IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase);
    }
    static bool Lee(string texto, System.Collections.IDictionary tabla, List<string> ajenas)
    {
        using (StringReader r = new StringReader(texto)) return (bool)Call(null, "LeeIntegRegistros", r, tabla, ajenas);
    }
    sealed class LectorRoto : StringReader
    {
        int quedan;
        public LectorRoto(string t, int n) : base(t) { quedan = n; }
        public override string ReadLine() { if (quedan-- <= 0) throw new IOException("disco que falla a mitad"); return base.ReadLine(); }
    }
    static void Tabla(string dir)
    {
        Console.WriteLine("\n  6) LA TABLA VERSIONADA (rocola_sonoridad.txt)\n");
        string cab = (string)TF.GetField("IntegCabecera", T).GetValue(null);
        Check(cab == "ROCOLA-SONORIDAD v1", "la tabla lleva cabecera con la version del formato (" + cab + ")");
        // Ida y vuelta.
        StringWriter w = new StringWriter();
        w.Write(cab + "\r\n");
        Call(null, "EscribeIntegRegistro", w, "C:\\musica\\a.mp3", Integ(1, -11.4819f, 2f, 2398));
        Call(null, "EscribeIntegRegistro", w, "C:\\musica\\muda.mp3", Integ(1, float.NegativeInfinity, 1f, 30));
        Call(null, "EscribeIntegRegistro", w, "C:\\musica\\cinco.wav", Integ(1, -18.25f, 5.82f, 600));
        string texto = w.ToString();
        System.Collections.IDictionary t = TablaNueva(); List<string> aj = new List<string>();
        bool ok = Lee(texto, t, aj);
        object a = t.Contains("C:\\musica\\a.mp3") ? t["C:\\musica\\a.mp3"] : null, m = t.Contains("c:\\MUSICA\\muda.mp3") ? t["c:\\MUSICA\\muda.mp3"] : null;
        Check(ok && t.Count == 3 && a != null && Math.Abs(Campo(a, "Lkfs") + 11.482f) < 0.0006f && Campo(a, "Pesos") == 2f && Campo(a, "Bloques") == 2398
              && Campo(a, "Algoritmo") == 1 && m != null && float.IsNegativeInfinity(Campo(m, "Lkfs")),
              "ida y vuelta: LKFS (a la milesima), pesos, bloques, algoritmo y la cancion MUDA; rutas sin distinguir mayusculas");
        Check(texto.Contains("1|-11.482|2|2398|C:\\musica\\a.mp3") && texto.Contains("|muda|"), "el formato escrito es el documentado: algoritmo|LKFS|pesos|bloques|ruta");
        // Un algoritmo POSTERIOR: no se usa, pero se conserva tal cual. Lo roto se salta.
        string futuro = "9|-9.5|2|100|C:\\musica\\a.mp3";   // uno de un algoritmo que aun no existe
        t = TablaNueva(); aj = new List<string>();
        ok = Lee(texto + futuro + "\r\nbasura\r\n1|NaN|2|100|x.mp3\r\n1|-10|0|100|y.mp3\r\n1|-10|2|0|z.mp3\r\n1|-10|2|5|\r\n0|-10|2|5|w.mp3\r\n", t, aj);
        a = t["C:\\musica\\a.mp3"];
        Check(ok && aj.Count == 1 && aj[0] == futuro && Math.Abs(Campo(a, "Lkfs") + 11.482f) < 0.0006f && t.Count == 3,
              "un registro de un algoritmo POSTERIOR se guarda intacto y no se usa; NaN, pesos 0, bloques 0, sin ruta y algoritmo 0 se descartan");
        // Cabecera ajena: ni se lee.
        t = TablaNueva(); aj = new List<string>();
        Check(!Lee("ROCOLA-SONORIDAD v2\r\n1|-10|2|5|a.mp3\r\n", t, aj) && t.Count == 0, "cabecera de otra version del formato: la tabla no se lee");
        // En disco: ausente, copia .bak, ajena e ilegible.
        string ruta = Path.Combine(dir, "rocola_sonoridad.txt");
        foreach (string x in new string[] { ruta, ruta + ".bak" }) if (File.Exists(x)) File.Delete(x);
        t = TablaNueva(); aj = new List<string>();
        Check((int)Call(null, "LeeIntegArchivo", ruta, null, t, aj) == 1 && t.Count == 0, "si no existe: estado 1 (se puede crear)");
        File.WriteAllText(ruta + ".bak", texto);
        Check((int)Call(null, "LeeIntegArchivo", ruta, null, t, aj) == 1 && t.Count == 3, "si el bueno falta y hay copia .bak, se lee la copia");
        File.Delete(ruta + ".bak");
        File.WriteAllText(ruta, "ROCOLA-SONORIDAD v9\r\n9|x|y|z|a.mp3\r\n");
        t = TablaNueva(); aj = new List<string>();
        Check((int)Call(null, "LeeIntegArchivo", ruta, null, t, aj) == 2, "cabecera ajena en disco: estado 2 (no se usa ni se pisa)");
        File.WriteAllText(ruta, texto);
        t = TablaNueva(); aj = new List<string>();
        Func<TextReader, TextReader> rompe = delegate(TextReader r) { string todo = r.ReadToEnd(); return new LectorRoto(todo, 2); };
        Check((int)Call(null, "LeeIntegArchivo", ruta, rompe, t, aj) == 2 && t.Count == 0, "si el disco falla a mitad: estado 2 y NADA a medias en la tabla");

        // Guardar: con estado 1 escribe cabecera, registros y los ajenos; con estado 2 no toca el archivo.
        object previa = TF.GetField("carpetaDatos", T).GetValue(null);
        try
        {
            TF.GetField("carpetaDatos", T).SetValue(null, dir);
            object f = Codigo.FormSinEncender(TF);
            Set(f, "startupReadPending", false);
            System.Collections.IDictionary tab = TablaNueva();
            tab["C:\\musica\\a.mp3"] = Integ(1, -11.4819f, 2f, 2398);
            Set(f, "integCache", tab);
            Set(f, "integAjenas", new List<string> { futuro });
            Set(f, "integEstado", 1);
            Set(f, "integCambios", 1L);
            Call(f, "SaveIntegCache");
            Codigo.EsperaGuardado(TF, f);
            string escrito = File.Exists(ruta) ? File.ReadAllText(ruta) : "";
            Check(escrito.StartsWith(cab + "\r\n") && escrito.Contains("1|-11.482|2|2398|C:\\musica\\a.mp3") && escrito.Contains(futuro),
                  "al guardar: cabecera, la medida nueva y el registro de la version posterior intacto");
            File.WriteAllText(ruta, "ROCOLA-SONORIDAD v9\r\nde otra version\r\n");
            Set(f, "integEstado", 2); Set(f, "integCambios", 2L);
            Call(f, "SaveIntegCache");
            Codigo.EsperaGuardado(TF, f);
            Check(File.ReadAllText(ruta) == "ROCOLA-SONORIDAD v9\r\nde otra version\r\n", "con una tabla ajena (estado 2) no se escribe NADA encima");
            Set(f, "integEstado", 1);
            File.WriteAllText(ruta, "no tocar");
            Set(f, "integGuardados", 2L);
            Call(f, "SaveIntegCache");
            Codigo.EsperaGuardado(TF, f);
            Check(File.ReadAllText(ruta) == "no tocar", "sin cambios desde el ultimo guardado no se reescribe (cero escrituras de mas)");
            // Arranque: la tabla 8 del trabajador, con sus estados.
            Type tLoad = TF.GetNestedType("StartupLoad", T);
            string[] rutas = new string[8];
            for (int i = 0; i < 7; i++) rutas[i] = Path.Combine(dir, "falta" + i + ".txt");
            rutas[7] = ruta;
            File.WriteAllText(ruta, texto + futuro + "\r\n");
            object load = Activator.CreateInstance(tLoad, true); tLoad.GetField("Paths", T).SetValue(load, rutas);
            object data = Call(null, "ReadStartupData", load);
            Type tData = TF.GetNestedType("StartupData", T);
            Check(data != null && (int)tData.GetField("IntegEstado", T).GetValue(data) == 1
                  && ((System.Collections.IDictionary)tData.GetField("Integ", T).GetValue(data)).Count == 3
                  && ((List<string>)tData.GetField("IntegAjenas", T).GetValue(data)).Count == 1,
                  "el trabajador del arranque lee la tabla (octava) con sus ajenas");
            File.WriteAllText(ruta, "ROCOLA-SONORIDAD v9\r\n");
            load = Activator.CreateInstance(tLoad, true); tLoad.GetField("Paths", T).SetValue(load, rutas);
            data = Call(null, "ReadStartupData", load);
            Check((int)tData.GetField("IntegEstado", T).GetValue(data) == 2, "y la marca como ajena si la cabecera no es la suya");
            string[] siete = new string[7]; Array.Copy(rutas, siete, 7);
            load = Activator.CreateInstance(tLoad, true); tLoad.GetField("Paths", T).SetValue(load, siete);
            data = Call(null, "ReadStartupData", load);
            Check((int)tData.GetField("IntegEstado", T).GetValue(data) == 0, "si le pasan solo siete tablas, dice que no la leyo (y el arranque la lee aparte)");
        }
        finally { TF.GetField("carpetaDatos", T).SetValue(null, previa); }
    }

    // ---------------------------------------------------------------------------------------------
    //  7) SOLO SE PUBLICA LO QUE SE MIDIO ENTERO; LA ESTIMACION NO CAMBIA
    // ---------------------------------------------------------------------------------------------
    static float[] Estereo(int fs, double seg, double dbL, double fL, double dbR, double fR)
    {
        int n = (int)Math.Round(seg * fs);
        float[] x = new float[n * 2];
        double aL = Math.Pow(10, dbL / 20), aR = Math.Pow(10, dbR / 20);
        for (int i = 0; i < n; i++)
        {
            x[2 * i] = (float)(aL * Math.Sin(2 * Math.PI * fL * i / fs));
            x[2 * i + 1] = (float)(aR * Math.Sin(2 * Math.PI * fR * i / fs));
        }
        return x;
    }
    static byte[] WavPcm16(int fs, int ch, int frames)
    {
        MemoryStream ms = new MemoryStream(); BinaryWriter w = new BinaryWriter(ms);
        int datos = frames * ch * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + datos); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(fs); w.Write(fs * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(datos);
        for (int i = 0; i < frames * ch; i++) w.Write((short)(8000 * Math.Sin(i * 0.01)));
        return ms.ToArray();
    }
    static bool MideEntera(object f, string ruta, out object s)
    {
        object[] a = { ruta, null };
        bool r = (bool)Call(f, "MideIntegrada", a);
        s = a[1];
        return r;
    }
    // La estimacion de siempre, rehecha aqui con los MISMOS filtros float de la rocola, cuadro a cuadro.
    static float EstimacionVieja(float[] x, int fs, int ch)
    {
        float[] c1 = (float[])Call(null, "EqShelfHi", 1681.974f, fs, 3.99984f, 0.70717f);
        float[] c2 = (float[])Call(null, "EqHighPass", 38.1355f, fs, 0.50033f);
        float[] st = new float[ch * 8]; double sum = 0; long n = 0;
        for (int i = 0; i + ch <= x.Length; i += ch)
            for (int c = 0; c < ch; c++)
            {
                int b = c * 8; float v = x[i + c];
                float y1 = c1[0]*v + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                st[b+1]=st[b]; st[b]=v; st[b+3]=st[b+2]; st[b+2]=y1;
                float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                sum += y2 * y2; n++;
            }
        return (float)Math.Sqrt(sum / n);
    }
    static void SoloEntera(string dir)
    {
        Console.WriteLine("\n  7) SOLO SE PUBLICA LO QUE SE MIDIO ENTERO; LA ESTIMACION NO CAMBIA\n");
        SinTareas();
        object f = Codigo.FormSinEncender(TF);
        int fs = 44100;
        float[] x = Estereo(fs, 20, -20, 1000, -26, 100);
        string ruta = Path.Combine(dir, "entera.wav");
        EscribeWav(ruta, fs, 2, x);
        object s;
        double ref20 = Mide(fs, new double[] { 1, 1 }, x, 0);
        Check(MideEntera(f, ruta, out s) && Math.Abs(Campo(s, "Lkfs") - ref20) < 0.0005 && Campo(s, "Pesos") == 2f && Campo(s, "Bloques") == 197,
              "cancion entera (WAV de 20 s): se publica, con la misma medida que el medidor puro (" + F(Campo(s, "Lkfs"), "0.000") + " LKFS, 197 bloques)");
        // MeasureFile de una cancion corta (se lee entera): la estimacion es EXACTAMENTE la de antes, y de paso
        // deja la integrada para quien la llamo.
        object[] args = { ruta, false, 0f, 0f, null };
        bool midio = (bool)Call(f, "MeasureFile", args);
        object[] toma = { null };
        bool hay = (bool)Call(null, "TomaIntegrada", toma);
        float vieja = EstimacionVieja(x, fs, 2);
        Check(midio && (float)args[2] == vieja, "la estimacion rapida de MeasureFile es bit a bit la cuenta de antes (" + F((float)args[2], "0.000000") + ")");
        Check(hay && Math.Abs(Campo(toma[0], "Lkfs") - Campo(s, "Lkfs")) == 0, "y como la leyo entera, deja la integrada, identica a la de la tanda larga");
        Check(!(bool)Call(null, "TomaIntegrada", new object[] { null }), "se recoge una sola vez");
        // Una cancion medida por tramos NO deja integrada: los tramos no son la cancion entera. Con 63,16 s
        // los tres tramos de 20 s mas lo que queda hasta el final suman JUSTO lo que dura el archivo, asi que
        // la cuenta de muestras no los delataria: solo la regla de "un solo tramo" impide colarlos.
        float[] larga = Estereo(fs, 63.16, -20, 1000, -26, 100);
        string rutaL = Path.Combine(dir, "larga.wav");
        EscribeWav(rutaL, fs, 2, larga);
        args = new object[] { rutaL, false, 0f, 0f, null };
        midio = (bool)Call(f, "MeasureFile", args);
        Check(midio && !(bool)Call(null, "TomaIntegrada", new object[] { null }), "una de 63,16 s medida por tramos da su estimacion pero NO se hace pasar por integrada");
        // Y lo que se lee de MAS tampoco vale (algo leido dos veces).
        object m2 = new Med(fs, new double[] { 1, 1 }).M;
        MethodInfo proc = TM.GetMethod("Procesa", T);
        proc.Invoke(m2, new object[] { x, 0, x.Length }); proc.Invoke(m2, new object[] { x, 0, x.Length / 2 });
        object[] cierra = { m2, 20.0, fs, null };
        Check(!(bool)Call(null, "CierraIntegrada", cierra), "si el medidor recibio mas audio del que dura el archivo, tampoco hay medida");
        // Archivo cortado: la cabecera dice 20 s y solo hay 15.
        string rutaC = Path.Combine(dir, "cortada.wav");
        File.Copy(ruta, rutaC, true);
        using (FileStream fsC = new FileStream(rutaC, FileMode.Open)) fsC.SetLength(fsC.Length - (long)fs * 2 * 4 * 5);
        Check(!MideEntera(f, rutaC, out s), "archivo cortado (dice 20 s y trae 15): NO se publica integrada");
        args = new object[] { rutaC, false, 0f, 0f, null };
        Call(f, "MeasureFile", args);
        Check(!(bool)Call(null, "TomaIntegrada", new object[] { null }), "tampoco de paso por MeasureFile");
        // Cancelada a mitad.
        int vueltas = 0;
        Func<bool> cancela = delegate { return ++vueltas > 20; };
        TF.GetField("measureCancellation", T).SetValue(null, cancela);
        try { Check(!MideEntera(f, ruta, out s) && vueltas > 20, "cancelada a mitad: NO se publica"); }
        finally { SinTareas(); }
        // Seis canales sin mascara: no se adivina.
        string ruta6 = Path.Combine(dir, "seis.wav");
        File.WriteAllBytes(ruta6, WavPcm16(fs, 6, fs * 2));
        Check(!MideEntera(f, ruta6, out s), "WAV de seis canales sin mascara de altavoces: no hay integrada (se queda la estimacion)");
        Check(!MideEntera(f, Path.Combine(dir, "no-existe.mp3"), out s), "archivo que no existe: no hay integrada, sin excepcion");
    }

    // ---------------------------------------------------------------------------------------------
    //  8) EL NIVELADO
    // ---------------------------------------------------------------------------------------------
    static float Ganancia(object f, string ruta) { return (float)Call(f, "NormGainFor", ruta); }
    static void Nivelado(string dir)
    {
        Console.WriteLine("\n  8) EL NIVELADO: LA INTEGRADA SI ESTA, CON LOS MISMOS TOPES\n");
        object f = Codigo.FormSinEncender(TF);
        Set(f, "normalizeOn", true); Set(f, "normStrength", 1f); Set(f, "normTarget", 0.26f);
        Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Set(f, "loudCache", loud);
        System.Collections.IDictionary tab = TablaNueva();
        Set(f, "integCache", tab);
        float maxBoost = (float)TF.GetField("NormMaxBoost", T).GetValue(null);
        Func<float, float> esperada = delegate(float rms)
        {
            float g = (float)Math.Pow(0.26f / rms, 1f);
            float cap = (float)Math.Pow(10.0, (float)Call(null, "TierCapDb", rms) / 20.0);
            if (cap > maxBoost) cap = maxBoost;
            if (g < 1f) return (float)Call(null, "GananciaNivelado", 0.26f, rms, 1f, cap, false, 0f, 0f);   // las fuertes: nivelpicos.cs
            if (g > cap) g = cap;
            return g;
        };
        loud["a.mp3"] = 0.05f;
        float soloEst = Ganancia(f, "a.mp3");
        Check(soloEst == esperada(0.05f), "sin integrada, la ganancia sale de la estimacion como siempre");
        object s = Integ(1, -20f, 2f, 500);
        tab["a.mp3"] = s;
        float rmsI = RmsPorCanal(s);
        Check(Ganancia(f, "a.mp3") == esperada(rmsI) && Ganancia(f, "a.mp3") != soloEst,
              "con integrada, manda la integrada (-20 LKFS estereo = " + F(20 * Math.Log10(rmsI), "0.00") + " dB por canal) con la MISMA cuenta y los MISMOS topes");
        tab["baja.mp3"] = Integ(1, -70f, 2f, 500);
        float gBaja = Ganancia(f, "baja.mp3");
        Check(gBaja <= maxBoost && gBaja == esperada(RmsPorCanal(tab["baja.mp3"])), "una muy baja (-70 LKFS) sube como mucho lo que dice su etapa (" + F(20 * Math.Log10(gBaja), "0.0") + " dB)");
        tab["fuerte.mp3"] = Integ(1, -5f, 2f, 500);
        float gFuerte = Ganancia(f, "fuerte.mp3");   // desde el 28/09 las muy fuertes se bajan un poco (nivelpicos.cs)
        Check(gFuerte < 1f && gFuerte >= (float)Math.Pow(10.0, -4.0 / 20.0) - 1e-6f, "una fuerte (-5 LKFS) se baja un poco, como mucho 4 dB (" + F(20 * Math.Log10(gFuerte), "0.0") + " dB)");
        tab["muda.mp3"] = Integ(1, float.NegativeInfinity, 2f, 500);
        loud["muda.mp3"] = 0.05f;
        Check(Ganancia(f, "muda.mp3") == 1f, "una integrada MUDA no se sube (ni se cae a la estimacion)");
        // Mono y el mismo tema en estereo: la misma ganancia.
        tab["mono.mp3"] = Integ(1, -23f, 1f, 500);
        tab["est.mp3"] = Integ(1, -23f + 3.0103f, 2f, 500);
        Check(Math.Abs(Ganancia(f, "mono.mp3") / Ganancia(f, "est.mp3") - 1f) < 1e-4f, "un tema mono y el mismo en estereo reciben la misma ganancia");
        Check((string)Call(f, "LoudTierName", "a.mp3") == (string)Call(f, "LoudTierName", "a.mp3") && ((string)Call(f, "LoudTierName", "a.mp3")).Length > 0,
              "la etapa que se muestra sale de la misma medida");
        // Equivalencia de escala con la estimacion en una senal estable: la diferencia es solo la del filtro.
        SinTareas();
        int fs = 44100;
        float[] x = Estereo(fs, 20, -20, 100, -20, 100);
        string ruta = Path.Combine(dir, "estable.wav");
        EscribeWav(ruta, fs, 2, x);
        object[] args = { ruta, false, 0f, 0f, null };
        Call(f, "MeasureFile", args);
        object[] toma = { null };
        Call(null, "TomaIntegrada", toma);
        double dif = 20 * Math.Log10(RmsPorCanal(toma[0]) / (float)args[2]);
        Check(Math.Abs(dif) < 0.06, "tono estable de 100 Hz: la integrada en la escala del nivelado se aparta " + F(dif, "+0.000;-0.000")
              + " dB de la estimacion: justo lo que el filtro viejo se queda corto a esa frecuencia (0,047 dB)");
        // No toca la cancion que suena.
        List<string> llamadas = Codigo.Llamadas(TF.GetMethod("PublicaIntegrada", T));
        Check(!Codigo.Llama(llamadas, "set_Gain") && !Codigo.LeeCampo(new List<MethodBase> { TF.GetMethod("PublicaIntegrada", T) }, "curTap"),
              "apuntar una integrada NO cambia el volumen de la cancion que suena (se nota la proxima vez)");
        Check(Codigo.Llama(TF.GetMethod("NormGainFor", T), "SonoridadParaNivelar") && Codigo.Llama(TF.GetMethod("LoudTierName", T), "SonoridadParaNivelar"),
              "el nivelado y la etapa preguntan por la misma medida (SonoridadParaNivelar)");
        // Una Form1 armada a pelo (sin colecciones) no revienta.
        object crudo = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
        TF.GetField("loudCache", T).SetValue(crudo, loud); TF.GetField("normalizeOn", T).SetValue(crudo, true);
        TF.GetField("normStrength", T).SetValue(crudo, 1f); TF.GetField("normTarget", T).SetValue(crudo, 0.26f);
        Check(Ganancia(crudo, "a.mp3") == soloEst, "sin tabla de integradas (nula) se usa la estimacion, sin excepcion");
    }

    // ---------------------------------------------------------------------------------------------
    //  9) LOS MINIMOS POR DURACION
    // ---------------------------------------------------------------------------------------------
    static void Minimos()
    {
        Console.WriteLine("\n  9) MINIMOS POR DURACION, A VARIAS FRECUENCIAS\n");
        bool eqBien = true, estBien = true;
        string lineaEq = "";
        foreach (int fs in new int[] { 22050, 44100, 48000, 96000 })
        {
            foreach (double seg in new double[] { 19.9, 20.1 })
            {
                object f = Codigo.FormSinEncender(TF);
                Set(f, "startupReadPending", true);
                Dictionary<string, float[]> eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
                Set(f, "eqCache", eq);
                Set(f, "eqMeasPath", "x.mp3"); Set(f, "eqMeasRate", fs); Set(f, "eqMeasN", (long)(seg * fs));
                double[] sums = new double[8]; for (int b = 0; b < 8; b++) sums[b] = (long)(seg * fs) * 0.01 * (b + 1);
                Set(f, "eqMeasSum", sums);
                try { Call(f, "FinalizeEqMeasure"); } catch { }
                bool puesta = eq.ContainsKey("x.mp3");
                if (puesta != (seg > 20)) eqBien = false;
                if (seg < 20) lineaEq += fs + ": " + (long)(19.9 * fs) + " cuadros no, ";
            }
            foreach (int ch in new int[] { 1, 2 })
                foreach (double seg in new double[] { 9.0, 9.2 })
                {
                    object f = Codigo.FormSinEncender(TF);
                    Set(f, "startupReadPending", true);
                    Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    Set(f, "loudCache", loud);
                    long muestras = (long)(seg * fs) * ch;
                    Set(f, "curMeasurePath", "y.mp3"); Set(f, "kwRate", fs); Set(f, "kwCanales", ch);
                    Set(f, "curSamples", muestras); Set(f, "curSumSq", muestras * 0.01);
                    try { Call(f, "FinalizeLoudMeasure"); } catch { }
                    if (loud.ContainsKey("y.mp3") != (seg > 9.1)) estBien = false;
                }
        }
        Info("huella: " + lineaEq + "20,1 s si a todas");
        Check(eqBien, "la huella del EQ pide 20 s de musica a 22,05, 44,1, 48 y 96 kHz (antes: 40 s, 20 s, 18,4 s y 9,2 s)");
        Check(estBien, "la estimacion oida pide 9,07 s en mono y en estereo a las cuatro frecuencias (antes: 800.000 muestras, de 4,2 a 36 s)");
    }

    // ---------------------------------------------------------------------------------------------
    //  10) LECTURAS QUE PARTEN UN CUADRO
    // ---------------------------------------------------------------------------------------------
    static object[] EnVivo(float[] x, int fs, int ch, int[] trozos)
    {
        object f = Codigo.FormSinEncender(TF);
        Set(f, "curMeasurePath", "v.mp3"); Set(f, "eqMeasPath", "v.mp3");
        Set(f, "kwRate", 0); Set(f, "eqMeasRate", 0); Set(f, "eqMeasSum", new double[8]); Set(f, "eqMeasN", 0L);
        Set(f, "curSumSq", 0.0); Set(f, "curSamples", 0L);
        MethodInfo rep = TF.GetMethod("ReportAudioBlock", T);
        int i = 0, k = 0;
        while (i < x.Length)
        {
            int n = Math.Min(trozos[k++ % trozos.Length], x.Length - i);
            rep.Invoke(f, new object[] { x, i, n, fs, ch });
            i += n;
        }
        return new object[] { Get(f, "curSumSq"), Get(f, "curSamples"), Get(f, "eqMeasSum"), Get(f, "eqMeasN") };
    }
    static void Partidas()
    {
        Console.WriteLine("\n  10) LECTURAS QUE PARTEN UN CUADRO: NI SE PIERDE NI SE CRUZA NADA\n");
        int fs = 44100;
        // Izquierdo fuerte y agudo, derecho flojo y grave: cruzar un canal con otro se notaria mucho.
        float[] x = Estereo(fs, 3, -6, 3000, -40, 60);
        object[] entero = EnVivo(x, fs, 2, new int[] { 4096 });
        object[] partido = EnVivo(x, fs, 2, new int[] { 4095, 1, 3, 2047, 5, 4096 });
        double sa = (double)entero[0], sb = (double)partido[0];
        double[] ea = (double[])entero[2], eb = (double[])partido[2];
        bool eqIgual = (long)entero[3] == (long)partido[3];
        for (int b = 0; b < 8; b++) if (ea[b] != eb[b]) eqIgual = false;
        Check((long)entero[1] == (long)partido[1] && (long)entero[1] == x.Length && Math.Abs(sb / sa - 1) < 1e-12,
              "sonoridad oida: las mismas muestras y la misma energia leyendo en trozos impares (" + (long)partido[1] + " muestras)");
        Check(eqIgual && (long)entero[3] == x.Length / 2, "huella tonal: los mismos " + (long)entero[3] + " cuadros y la misma energia por banda, bit a bit");
        // LeeCuadrosCompletos con un lector que entrega trozos que parten cuadros.
        float[] fuente = new float[1001 * 2 + 1];   // termina con medio cuadro
        for (int i = 0; i < fuente.Length; i++) fuente[i] = i;
        int pos = 0; int[] pasos = { 3, 1, 4, 1, 5, 9, 2, 6 }; int pk = 0;
        Func<float[], int, int, int> lee = delegate(float[] b, int o, int c)
        {
            int n = Math.Min(Math.Min(c, pasos[pk++ % pasos.Length]), fuente.Length - pos);
            Array.Copy(fuente, pos, b, o, n); pos += n; return n;
        };
        MethodInfo lcc = TF.GetMethod("LeeCuadrosCompletos", T);
        List<float> junto = new List<float>(); bool pares = true;
        float[] buf = new float[64];
        while (true)
        {
            int n = (int)lcc.Invoke(null, new object[] { lee, buf, buf.Length, 2 });
            if (n <= 0) break;
            if (n % 2 != 0) pares = false;
            for (int i = 0; i < n; i++) junto.Add(buf[i]);
        }
        bool seguido = junto.Count == 1001 * 2;
        for (int i = 0; seguido && i < junto.Count; i++) if (junto[i] != i) seguido = false;
        Check(pares && seguido, "el analisis por tramos lee cuadros ENTEROS aunque el lector los parta: 1001 cuadros en orden, y el medio cuadro final fuera");
    }

    // ---------------------------------------------------------------------------------------------
    //  11) LA TANDA DE LA CANCION ENTERA RESPETA A LOS DEMAS
    // ---------------------------------------------------------------------------------------------
    static void Coordinacion(string dir)
    {
        Console.WriteLine("\n  11) LA TANDA DE LA CANCION ENTERA: DESPUES, EN REPOSO Y CEDIENDO\n");
        List<string> repaso = Codigo.ConAyudantes(TF, "StartPreScan");
        Check(Codigo.Llama(repaso, "TandaIntegrada") && Codigo.Llama(repaso, "MideIntegradaResponsive")
              && Codigo.Llama(repaso, "TomaFuenteMedida") && Codigo.Llama(repaso, "PublicaIntegradaDeFuente"),
              "el repaso de la biblioteca arma la tanda de la cancion entera, la mide y la apunta con el sello del original");
        Check(Codigo.Llama(repaso, "SaveIntegCache"), "y la guarda por tandas, como la de volumen");
        // Cede por EsperaPortadas, que espera con YieldCoverWork: un salto (Codigo.ConUnSalto).
        Check(Codigo.Llama(Codigo.ConUnSalto(TF, "MideIntegradaResponsive"), "YieldCoverWork"), "cede el disco a las caratulas que se estan cargando");
        Check(Codigo.Llama(TF.GetMethod("MideIntegrada", T), "MeasurementCancelled") && Codigo.Llama(TF.GetMethod("MideIntegrada", T), "CierraIntegrada"),
              "se puede cancelar en cada bloque y solo cierra lo que leyo entero");
        List<string> calentador = Codigo.ConAyudantes(TF, "PrecalientaSiguiente");
        Check(Codigo.Llama(calentador, "TomaIntegrada") && Codigo.Llama(calentador, "TomaFuenteMedida")
              && Codigo.Llama(calentador, "PublicaIntegradaDeFuente"),
              "el calentador de la siguiente tambien apunta la integrada entera con el sello del original");
        // El wrapper NO sustituye la publicacion por una llamada vacia: la conserva,
        // pero solo despues de validar que el original no cambio mientras se media.
        MethodInfo publica = TF.GetMethod("PublicaIntegradaDeFuente", T);
        List<string> pasos = Codigo.Repasa(publica);
        Check(pasos.IndexOf("AceptaFuenteMedida") >= 0
              && pasos.IndexOf("PublicaIntegrada") > pasos.IndexOf("AceptaFuenteMedida")
              && pasos.IndexOf("ActualizaFuenteMedida") > pasos.IndexOf("PublicaIntegrada"),
              "el wrapper valida la fuente, publica la integrada y conserva su procedencia, en ese orden");
        object f = Codigo.FormSinEncender(TF);
        Set(f, "startupReadPending", true); // ninguna invalidacion encarga guardados de esta fixture
        string ruta = Path.Combine(dir, "integrada-fuente.bin");
        File.WriteAllBytes(ruta, new byte[128]);
        object fuente = Call(null, "CapturaFuente", ruta);
        Call(f, "PublicaIntegradaDeFuente", ruta, Integ(1, -20f, 2f, 500), fuente);
        Check(Tabla(f).Contains(ruta) && Campo(Tabla(f)[ruta], "Lkfs") == -20f,
              "con el original intacto el wrapper publica realmente la integrada");
        File.SetLastWriteTimeUtc(ruta, File.GetLastWriteTimeUtc(ruta).AddSeconds(5));
        Call(f, "PublicaIntegradaDeFuente", ruta, Integ(1, -10f, 2f, 500), fuente);
        Check(Tabla(f).Contains(ruta) && Campo(Tabla(f)[ruta], "Lkfs") == -20f,
              "si el original cambia, una integrada tardia no sustituye la ya publicada");
        Check(Codigo.LeeCampo(Codigo.MetodosConAyudantes(TF, "StartPreScan"), "nowPlaying"), "con musica sonando descansa segun lo que trabajo (lee si hay cancion)");
        List<string> tanda = Codigo.Llamadas(TF.GetMethod("TandaIntegrada", T));
        Check(Codigo.Llama(tanda, "OrdenaPorUso"), "y la tanda va en el orden de interes de siempre: lo pedido, lo mas sonado, lo mas reciente");
    }

    // ---------------------------------------------------------------------------------------------
    //  12) LO QUE CUESTA
    // ---------------------------------------------------------------------------------------------
    static void Coste(string dir)
    {
        Console.WriteLine("\n  12) LO QUE CUESTA (esta maquina; no es la del bar)\n");
        int fs = 44100;
        float[] x = Programa(fs, 2, new double[][] { new double[] { -20, 60 } }, 99);
        Med m = new Med(fs, new double[] { 1, 1 });
        m.P(x, 0, 8192);   // calentar
        Stopwatch w = Stopwatch.StartNew();
        for (int r = 0; r < 3; r++) { m = new Med(fs, new double[] { 1, 1 }); for (int i = 0; i < x.Length; i += 8192) m.P(x, i, Math.Min(8192, x.Length - i)); }
        w.Stop();
        double msPorSeg = w.Elapsed.TotalMilliseconds / 3 / 60.0;
        Info("medidor BS.1770, estereo 44,1 kHz: " + F(msPorSeg, "0.000") + " ms de procesador por segundo de musica ("
             + F(msPorSeg * 240 / 1000.0, "0.00") + " s en una cancion de 4 minutos; " + F(w.Elapsed.TotalMilliseconds * 1e6 / 3 / x.Length, "0.0") + " ns por muestra)");
        Info("memoria: 10 bloques por segundo x 8 bytes = " + F(10 * 8 * 240 / 1024.0, "0.0") + " KB en 4 minutos; " + F(10 * 8 * 3600 / 1024.0 / 1024.0, "0.00") + " MB en una hora");
        Check(msPorSeg < 10, "medir un segundo de musica cuesta menos de 10 ms de un nucleo (lo que cuesta de verdad es descomprimirla)");
        // La tanda de la cancion entera frente a la estimacion por tramos, con un WAV de 4 minutos. Un WAV no
        // se descomprime: en MP3 las dos cuestan bastante mas y la diferencia la pone el descompresor.
        SinTareas();
        object f = Codigo.FormSinEncender(TF);
        float[] cancion = Estereo(44100, 240, -20, 1000, -26, 100);
        string ruta = Path.Combine(dir, "cuatro.wav");
        EscribeWav(ruta, 44100, 2, cancion);
        object[] args = { ruta, false, 0f, 0f, null };
        Call(f, "MeasureFile", args);   // calentar la cache de disco
        Stopwatch w1 = Stopwatch.StartNew(); args = new object[] { ruta, false, 0f, 0f, null }; Call(f, "MeasureFile", args); w1.Stop();
        object s;
        Stopwatch w2 = Stopwatch.StartNew(); bool entera = MideEntera(f, ruta, out s); w2.Stop();
        Info("WAV estereo de 4 min: estimacion por tramos " + w1.ElapsedMilliseconds + " ms, medida entera " + w2.ElapsedMilliseconds
             + " ms (lee 240 s en vez de 60 s y no hace huella ni FFT)");
        Check(entera, "la medida entera de 4 minutos se completa");
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TM = a.GetType("Rocola.MedidorBs1770", false);
        TD = a.GetType("Rocola.DisposicionBs1770", false);
        TS = a.GetType("Rocola.SonoridadIntegrada", false);
        if (TM == null || TD == null || TS == null) { Check(false, "no esta el medidor BS.1770 (MedidorBs1770/DisposicionBs1770/SonoridadIntegrada)"); return 1; }
        string dir = Path.Combine(Path.GetTempPath(), "rocola-sonoridad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Ponderacion();
            Conformidad();
            Canales(dir);
            Puertas();
            Referencia(dir);
            Tabla(dir);
            SoloEntera(dir);
            Nivelado(dir);
            Minimos();
            Partidas();
            Coordinacion(dir);
            Coste(dir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}

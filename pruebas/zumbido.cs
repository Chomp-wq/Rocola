using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// T3 Y T6 (25-10-2026): DONDE EMPIEZA Y ACABA CADA CANCION, Y EL ZUMBIDO DE LA CORRIENTE (algoritmo 6 de rocola_sonoridad.txt).
//
// Lo que se vigila:
//   1) el formato del algoritmo 6: ida y vuelta de los cuatro campos nuevos, lo roto se salta, lo de un algoritmo posterior se guarda;
//   2) donde empieza y acaba: un principio y un final secos, unos aplausos largos muy por debajo, y un desvanecido;
//   3) el zumbido: 60 y 50 Hz con sus armonicos se reconocen; una nota de bajo larga en Si (61,7 Hz), la musica sin zumbido y un zumbido
//      demasiado bajo, no;
//   4) la etapa: quita la linea (30 dB o mas), casi no toca lo de al lado, da lo mismo se lea como se lea, se enciende y se apaga en vivo
//      sin chasquido, "sin mejoras" la deja pasar, y en un disco continuo la siguiente sigue con los filtros de la anterior;
//   5) en la cadena: solo la llevan las canciones con zumbido medido; con el interruptor apagado, sale bit a bit como sin ella;
//   6) lo que cuesta medirlo, frente a la medida de la sonoridad entera.
// Son senales sinteticas: dicen que la cuenta hace lo que promete, no como suena (eso se oye en el bar).
class Zumbido
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TS, TSil, TM, TQ;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Call(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T))
            if (m.Name == n && m.GetParameters().Length == a.Length)
            { try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
        throw new MissingMethodException(n);
    }
    static object Get(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); } return null; }
    static void Set(object o, string n, object v) { FieldInfo f = TF.GetField(n, T); f.SetValue(f.IsStatic ? null : o, v); }
    static object Campo(object s, string n) { return s.GetType().GetField(n, T).GetValue(s); }
    static bool Prop(object s, string n) { return (bool)s.GetType().GetProperty(n, T).GetValue(s, null); }
    static IDictionary Tabla() { return (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); }
    static object Form()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 0f);
        return f;
    }

    sealed class Fuente : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf;
        public Fuente(float[] x, int ch) { d = x; wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, n); p += n; return n; }
    }

    // ---------------- las canciones de prueba (44,1 kHz, estereo) ----------------
    const int Fs = 44100;
    // Musica: acordes con armonicos y un poco de ruido, con partes fuertes (-16 dBFS) y calladas (-42 dBFS); 'callado' = fraccion.
    static float[] Musica(double seg, double callado, int semilla)
    {
        int n = (int)(seg * Fs); float[] x = new float[2 * n]; Random r = new Random(semilla);
        double[] notas = { 110.0, 138.6, 164.8, 220.0, 277.2, 329.6, 440.0, 554.4, 659.3, 880.0 };
        double[] fase = new double[notas.Length];
        int trozo = Fs * 2; double nivel = 0;
        for (int i = 0; i < n; i++)
        {
            if (i % trozo == 0) { bool quieto = r.NextDouble() < callado; nivel = Math.Pow(10, (quieto ? -42 : -16) / 20.0); }
            double v = 0;
            for (int k = 0; k < notas.Length; k++) { fase[k] += 2 * Math.PI * notas[k] * (1 + 0.002 * Math.Sin(i * 0.0003 + k)) / Fs; v += Math.Sin(fase[k]) / (1 + k * 0.3); }
            v = v * 0.32 + (r.NextDouble() - 0.5) * 0.15;
            float s = (float)(v * nivel);
            x[2 * i] = s; x[2 * i + 1] = (float)(s * 0.9);
        }
        return x;
    }
    static void Suma(float[] x, double hz, double nivelDb)
    {
        double a = Math.Pow(10, nivelDb / 20.0);
        for (int i = 0; i < x.Length / 2; i++) { float v = (float)(a * Math.Sin(2 * Math.PI * hz * i / Fs)); x[2 * i] += v; x[2 * i + 1] += v; }
    }
    static void ConZumbido(float[] x, int f0, double nivelDb) { Suma(x, f0, nivelDb); Suma(x, 2 * f0, nivelDb - 6); Suma(x, 3 * f0, nivelDb - 12); }
    static string Wav(string dir, string nombre, float[] x)
    {
        string p = Path.Combine(dir, nombre + ".wav");
        using (WaveFileWriter w = new WaveFileWriter(p, WaveFormat.CreateIeeeFloatWaveFormat(Fs, 2))) w.WriteSamples(x, 0, x.Length);
        return p;
    }
    static object Mide(string wav)
    {
        object[] a = { wav, null };
        bool ok = (bool)TF.GetMethod("MideIntegrada", T).Invoke(Form(), a);
        if (!ok) throw new Exception("MideIntegrada no midio " + wav);
        return a[1];
    }
    // La amplitud de una linea (Goertzel con ventana de Hann) en un tramo de un canal.
    static double Linea(float[] x, int canales, int canal, int desde, int largo, double hz)
    {
        double c = 2 * Math.Cos(2 * Math.PI * hz / Fs), s1 = 0, s2 = 0, sw = 0;
        for (int i = 0; i < largo; i++)
        {
            double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / largo); sw += w;
            double s0 = x[(desde + i) * canales + canal] * w + c * s1 - s2; s2 = s1; s1 = s0;
        }
        return 2 * Math.Sqrt(Math.Max(0, s1 * s1 + s2 * s2 - c * s1 * s2)) / sw;
    }
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-15)); }

    // ---------------------------------------------------------------- 1) EL FORMATO
    static object Reg6(float lk, float ini, float fin, int hz, float db)
    {
        object mudo = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { 200f });
        return Activator.CreateInstance(TS, T, null, new object[] { 6, lk, 2f, 2000, -0.5f, -2f, mudo, 8.5f, -3f, ini, fin, hz, db }, null);
    }
    static void ElFormato()
    {
        Console.WriteLine("\n  1) EL FORMATO DEL ALGORITMO 6\n");
        Check((int)TM.GetField("Algoritmo", T).GetValue(null) >= 6, "el algoritmo de ahora mide el zumbido (6 o despues; hoy " + TM.GetField("Algoritmo", T).GetValue(null) + ")");
        StringWriter w = new StringWriter();
        MethodInfo esc = TF.GetMethod("EscribeIntegRegistro", T);
        esc.Invoke(null, new object[] { w, "C:\\m\\con.mp3", Reg6(-14f, 4.2f, 181.7f, 60, 17.3f) });
        esc.Invoke(null, new object[] { w, "C:\\m\\sin.mp3", Reg6(-14f, float.NaN, float.NaN, 0, float.NaN) });
        string texto = w.ToString();
        Check(texto.Contains("|-3|4.2|181.7|60|17.3|C:\\m\\con.mp3") && texto.Contains("|-3|-|-|-|-|C:\\m\\sin.mp3"),
              "detras del P90 y antes de la ruta: inicio, fin, zumbido y cuanto sobresale ('-' si no hay)");
        IDictionary t = Tabla(); List<string> aj = new List<string>();
        bool ok = (bool)TF.GetMethod("LeeIntegRegistros", T).Invoke(null, new object[] { new StringReader((string)TF.GetField("IntegCabecera", T).GetValue(null) + "\r\n" + texto), t, aj });
        object c = t["C:\\m\\con.mp3"], s = t["C:\\m\\sin.mp3"];
        Check(ok && t.Count == 2 && aj.Count == 0 && Prop(c, "ConUtiles") && Prop(c, "ConZumbido") && (int)Campo(c, "ZumbidoHz") == 60
              && Math.Abs((float)Campo(c, "InicioUtil") - 4.2f) < 1e-4 && Math.Abs((float)Campo(c, "FinUtil") - 181.7f) < 1e-3 && Math.Abs((float)Campo(c, "ZumbidoDb") - 17.3f) < 1e-4
              && !Prop(s, "ConUtiles") && !Prop(s, "ConZumbido") && Prop(c, "ConP90"),
              "ida y vuelta: los cuatro campos vuelven, el '-' es 'no hay', y el P90 sigue valiendo para nivelar");
        string cab = (string)TF.GetField("IntegCabecera", T).GetValue(null);
        string baseR = "6|-10|2|100|-0.4|-2.1|-|-|-|-|180|7|-3|";
        string malos = baseR + "9|4|60|12|C:\\m\\alreves.mp3\r\n" + baseR + "1|9|55|12|C:\\m\\hz55.mp3\r\n" + baseR + "1|9|60|NaN|C:\\m\\nan.mp3\r\n"
                     + baseR + "1|9|-|12|C:\\m\\dbsinhz.mp3\r\n" + baseR + "1|9|60|C:\\m\\falta.mp3\r\n" + baseR + "-1|9|-|-|C:\\m\\negativo.mp3\r\n";
        string futuro = "99|-9|2|100|-0.4|-2.1|-|-|-|-|180|7|-3|1|9|60|12|x|y|C:\\m\\futuro.mp3";
        t = Tabla(); aj = new List<string>();
        TF.GetMethod("LeeIntegRegistros", T).Invoke(null, new object[] { new StringReader(cab + "\r\n" + malos + futuro + "\r\n"), t, aj });
        Check(t.Count == 0 && aj.Count == 1 && aj[0] == futuro, "un fin antes del inicio, 55 Hz, un NaN, dB sin zumbido, un campo de menos o un tiempo negativo: se saltan; uno posterior (99) se guarda intacto");
    }

    // ---------------------------------------------------------------- 2) DONDE EMPIEZA Y ACABA
    static float[] Junta(params float[][] partes) { List<float> l = new List<float>(); foreach (float[] p in partes) l.AddRange(p); return l.ToArray(); }
    static float[] Silencio(double seg) { return new float[2 * (int)(seg * Fs)]; }
    static float[] Ruido(double seg, double db, int semilla)
    {
        Random r = new Random(semilla); int n = (int)(seg * Fs); float[] x = new float[2 * n]; double a = Math.Pow(10, db / 20) * Math.Sqrt(3);
        for (int i = 0; i < x.Length; i++) x[i] = (float)((r.NextDouble() * 2 - 1) * a);
        return x;
    }
    static void DondeEmpiezaYAcaba(string dir)
    {
        Console.WriteLine("\n  2) DONDE EMPIEZA Y ACABA DE VERDAD (T3)\n");
        float[] m = Musica(30, 0, 3);
        object s = Mide(Wav(dir, "seco", Junta(Silencio(3), m, Silencio(2))));
        float ini = (float)Campo(s, "InicioUtil"), fin = (float)Campo(s, "FinUtil");
        Check(Prop(s, "ConUtiles") && ini >= 2.6f && ini <= 3.05f && fin >= 33.0f && fin <= 33.45f, "principio y final secos (la musica de 3 a 33 s): de " + F(ini, "0.0") + " a " + F(fin, "0.0") + " s");
        // Aplausos larguisimos 35 dB por debajo: no son la cancion.
        s = Mide(Wav(dir, "aplausos", Junta(Silencio(3), Musica(30, 0, 4), Ruido(15, -52, 5), Silencio(2))));
        ini = (float)Campo(s, "InicioUtil"); fin = (float)Campo(s, "FinUtil");
        Check(Prop(s, "ConUtiles") && fin >= 33.0f && fin <= 33.45f, "con 15 s de aplausos a 35 dB por debajo: acaba en " + F(fin, "0.0") + " s (la musica, en 33)");
        // Un desvanecido de 20 s (3 dB por segundo): acaba cuando lo que suena esta a unos 20 LU de la cancion.
        float[] d = Musica(20, 0, 6);
        for (int i = 0; i < d.Length / 2; i++) { double g = Math.Pow(10, -3.0 * i / Fs / 20); d[2 * i] *= (float)g; d[2 * i + 1] *= (float)g; }
        s = Mide(Wav(dir, "desvanece", Junta(Musica(30, 0, 7), d, Silencio(2))));
        fin = (float)Campo(s, "FinUtil");
        Check(Prop(s, "ConUtiles") && fin >= 35.5f && fin <= 38.5f, "con un desvanecido de 3 dB/s desde los 30 s: acaba en " + F(fin, "0.0") + " s (a unos 20 LU de la cancion: 36,7 s)");
        s = Mide(Wav(dir, "muda", Silencio(8)));
        Check(!Prop(s, "ConUtiles"), "una muda no tiene donde empezar ni acabar");
    }

    // ---------------------------------------------------------------- 3) EL ZUMBIDO
    static void ElZumbido(string dir)
    {
        Console.WriteLine("\n  3) EL ZUMBIDO DE LA CORRIENTE (T6)\n");
        float[] x = Musica(60, 0.3, 11); ConZumbido(x, 60, -50);
        object s = Mide(Wav(dir, "z60", x));
        Check(Prop(s, "ConZumbido") && (int)Campo(s, "ZumbidoHz") == 60 && (float)Campo(s, "ZumbidoDb") >= 10f,
              "60 Hz a -50 dBFS (y sus armonicos) bajo una musica con partes calladas: zumbido de " + Campo(s, "ZumbidoHz") + " Hz, sobresale " + F((float)Campo(s, "ZumbidoDb"), "0.0") + " dB");
        x = Musica(60, 0.3, 12); ConZumbido(x, 50, -50);
        s = Mide(Wav(dir, "z50", x));
        Check(Prop(s, "ConZumbido") && (int)Campo(s, "ZumbidoHz") == 50, "lo mismo a 50 Hz: " + Campo(s, "ZumbidoHz") + " Hz");
        x = Musica(60, 0.3, 13); Suma(x, 120, -48); Suma(x, 240, -60);
        s = Mide(Wav(dir, "z120", x));
        Check(Prop(s, "ConZumbido") && (int)Campo(s, "ZumbidoHz") == 60, "solo el doble (120 Hz, el de un rectificador): se reconoce como el de 60 (" + Campo(s, "ZumbidoHz") + ")");
        x = Musica(60, 0.3, 14);
        s = Mide(Wav(dir, "limpia", x));
        Check(!Prop(s, "ConZumbido") && (int)Campo(s, "ZumbidoHz") == 0, "la musica sin zumbido: nada");
        x = Musica(60, 0.3, 15); Suma(x, 61.735, -45); Suma(x, 123.47, -52);
        s = Mide(Wav(dir, "si", x));
        Check(!Prop(s, "ConZumbido"), "una nota de bajo larga en Si (61,7 Hz) y su octava, toda la cancion: no es zumbido");
        x = Musica(60, 0.3, 16); ConZumbido(x, 60, -88);
        s = Mide(Wav(dir, "bajito", x));
        Check(!Prop(s, "ConZumbido"), "un zumbido a -88 dBFS (no se oye): nada");
        x = Musica(60, 0, 17); ConZumbido(x, 60, -50);
        s = Mide(Wav(dir, "tapado", x));
        Console.WriteLine("        (con la musica fuerte de principio a fin, el zumbido queda tapado: " + (Prop(s, "ConZumbido") ? "se reconoce igual" : "no se reconoce, y no hace falta") + ")");
    }

    // ---------------------------------------------------------------- 4) LA ETAPA
    static ISampleProvider Etapa(float[] x, int hz, bool on) { return (ISampleProvider)Activator.CreateInstance(TQ, T, null, new object[] { new Fuente(x, 2), hz, on }, null); }
    static float[] Lee(ISampleProvider p, int largo, Random trozos)
    {
        float[] y = new float[largo]; int n = 0; float[] b = new float[8192];
        while (n < largo)
        {
            int pide = trozos == null ? 4096 : 1 + trozos.Next(7);
            int r = p.Read(b, 0, Math.Min(pide, largo - n)); if (r <= 0) break;
            Array.Copy(b, 0, y, n, r); n += r;
        }
        return y;
    }
    static void LaEtapa()
    {
        Console.WriteLine("\n  4) LAS MUESCAS\n");
        float[] x = Musica(12, 0.3, 21); ConZumbido(x, 60, -50); Suma(x, 220, -30); Suma(x, 61.735, -40);
        float[] y = Lee(Etapa(x, 60, true), x.Length, null);
        int desde = 4 * Fs, largo = 8 * Fs;
        double antes = Linea(x, 2, 0, desde, largo, 60), despues = Linea(y, 2, 0, desde, largo, 60);
        double a2 = Linea(x, 2, 0, desde, largo, 120), d2 = Linea(y, 2, 0, desde, largo, 120);
        double a220 = Linea(x, 2, 0, desde, largo, 220), d220 = Linea(y, 2, 0, desde, largo, 220);
        double aSi = Linea(x, 2, 0, desde, largo, 61.735), dSi = Linea(y, 2, 0, desde, largo, 61.735);
        Check(Db(antes) - Db(despues) >= 30 && Db(a2) - Db(d2) >= 30, "quita la linea de 60 Hz " + F(Db(antes) - Db(despues), "0.0") + " dB y la de 120 Hz " + F(Db(a2) - Db(d2), "0.0") + " dB");
        Check(Math.Abs(Db(a220) - Db(d220)) < 0.05 && Db(aSi) - Db(dSi) < 1.5, "a 220 Hz no cambia nada (" + F(Db(d220) - Db(a220), "0.000") + " dB); a 61,7 Hz (Si), " + F(Db(dSi) - Db(aSi), "0.00") + " dB");
        float[] yp = Lee(Etapa(x, 60, true), x.Length, new Random(5));
        bool igual = true; for (int i = 0; i < y.Length; i++) if (y[i] != yp[i]) { igual = false; break; }
        Check(igual, "leida de 1 a 7 muestras a la vez da los mismos bits que en bloques");
        float[] yo = Lee(Etapa(x, 60, false), x.Length, null);
        igual = true; for (int i = 0; i < x.Length; i++) if (yo[i] != x[i]) { igual = false; break; }
        Check(igual, "apagada: sale bit a bit lo que entra");
        // Encender y apagar en vivo: el salto entre muestras seguidas no pasa del normal.
        ISampleProvider e = Etapa(x, 60, false); MethodInfo aj = TQ.GetMethod("Ajusta", T);
        float[] b = new float[2 * Fs]; float[] z = new float[x.Length]; int n = 0; int vuelta = 0;
        while (n < x.Length) { int r = e.Read(b, 0, Math.Min(b.Length, x.Length - n)); if (r <= 0) break; Array.Copy(b, 0, z, n, r); n += r; vuelta++; if (vuelta == 2) aj.Invoke(e, new object[] { true }); if (vuelta == 4) aj.Invoke(e, new object[] { false }); }
        double saltoN = 0, saltoV = 0;
        for (int i = 2; i < x.Length; i += 2) { saltoN = Math.Max(saltoN, Math.Abs(x[i] - x[i - 2])); saltoV = Math.Max(saltoV, Math.Abs(z[i] - z[i - 2])); }
        igual = true; for (int i = 6 * Fs * 2; i < x.Length; i++) if (z[i] != x[i]) { igual = false; break; }
        Check(saltoV <= saltoN * 1.05 && igual, "encender y apagar en vivo: el mayor salto entre muestras " + F(saltoV, "0.0000") + " (lo normal " + F(saltoN, "0.0000") + "), y al apagarse vuelve a salir bit a bit");
        // "Escuchar sin mejoras" (S4): pasa tal cual.
        e = Etapa(x, 60, true); TQ.GetField("Pasa", T).SetValue(e, true);
        float[] sp = Lee(e, x.Length, null);
        igual = true; for (int i = 4 * Fs; i < x.Length; i++) if (sp[i] != x[i]) { igual = false; break; }
        Check(igual, "con \"Escuchar sin mejoras\" pasa tal cual");
        // Disco continuo: dos pistas, la segunda sigue con los filtros de la primera; da lo mismo que una sola etapa con las dos seguidas.
        int corte = 5 * Fs * 2 + 2 * 777;
        float[] p1 = new float[corte], p2 = new float[x.Length - corte];
        Array.Copy(x, 0, p1, 0, corte); Array.Copy(x, corte, p2, 0, p2.Length);
        ISampleProvider e1 = Etapa(p1, 60, true), e2 = Etapa(p2, 60, true);
        float[] q1 = Lee(e1, p1.Length, null);
        TQ.GetMethod("Sigue", T).Invoke(e2, new object[] { e1 });
        float[] q2 = Lee(e2, p2.Length, null);
        igual = true; for (int i = 0; i < q1.Length && igual; i++) if (q1[i] != y[i]) igual = false;
        for (int i = 0; i < q2.Length && igual; i++) if (q2[i] != y[corte + i]) igual = false;
        Check(igual, "disco continuo: la siguiente sigue con los filtros de la anterior y sale bit a bit como una sola");
    }

    // ---------------------------------------------------------------- 5) EN LA CADENA
    static float[] Cadena(object f, string wav)
    {
        object track = Activator.CreateInstance(TF.GetNestedType("Track", T), true);
        track.GetType().GetField("Path", T).SetValue(track, wav);
        object lector = Call(f, "OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        ultimaCadena = cad;
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        List<float> y = new List<float>(); float[] b = new float[4096]; int n;
        while ((n = sal.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) y.Add(b[i]);
        ((IDisposable)lector).Dispose();
        return y.ToArray();
    }
    static object ultimaCadena;
    static void EnLaCadena(string dir)
    {
        Console.WriteLine("\n  5) EN LA CADENA\n");
        float[] x = Musica(10, 0.3, 31); ConZumbido(x, 60, -50);
        string wav = Wav(dir, "cadena", x);
        object medida = Mide(wav);
        object f = Form();
        IDictionary tab = Tabla(); Set(f, "integCache", tab);
        ((IDictionary)Get(f, "durCache"))[wav] = (float)(x.Length / 2 / (double)Fs);
        // La misma medida sin el zumbido apuntado: lo demas (el nivelado) igual.
        object sinZ = Activator.CreateInstance(TS, T, null, new object[] { Campo(medida, "Algoritmo"), Campo(medida, "Lkfs"), Campo(medida, "Pesos"), Campo(medida, "Bloques"),
            Campo(medida, "PicoDb"), Campo(medida, "P99Db"), Campo(medida, "Silencios"), Campo(medida, "Lra"), Campo(medida, "P90Db"), Campo(medida, "InicioUtil"), Campo(medida, "FinUtil"), 0, float.NaN }, null);
        tab[wav] = sinZ;
        float[] sin = Cadena(f, wav);
        Check(Get(ultimaCadena, "Zumbido") == null, "sin zumbido apuntado, la cancion no lleva la etapa");
        tab[wav] = medida;
        Set(f, "quitaZumbido", false);
        float[] apagada = Cadena(f, wav);
        object z = Get(ultimaCadena, "Zumbido");
        bool igual = sin.Length == apagada.Length; for (int i = 0; igual && i < sin.Length; i++) if (sin[i] != apagada[i]) igual = false;
        Check(z != null && igual, "con zumbido medido y el interruptor apagado: la lleva, pero sale bit a bit como sin ella");
        Set(f, "quitaZumbido", true);
        float[] con = Cadena(f, wav);
        z = Get(ultimaCadena, "Zumbido");
        int desde = 4 * Fs, largo = 5 * Fs;
        double antes = Linea(sin, 2, 0, desde, largo, 60), despues = Linea(con, 2, 0, desde, largo, 60);
        Check(z != null && (bool)TQ.GetProperty("Activa", T).GetValue(z, null) && Db(antes) - Db(despues) >= 25,
              "encendido: la cadena quita la linea de 60 Hz " + F(Db(antes) - Db(despues), "0.0") + " dB");
    }

    // ---------------------------------------------------------------- 6) LO QUE CUESTA
    static void LoQueCuesta()
    {
        Console.WriteLine("\n  6) LO QUE CUESTA MEDIRLO\n");
        float[] x = Musica(30, 0.3, 41);
        Type tz = TF.Assembly.GetType("Rocola.MedidorZumbido", true);
        double[] pesos = { 1.0, 1.0 };
        double mejorZ = double.MaxValue, mejorM = double.MaxValue;
        for (int vez = 0; vez < 3; vez++)
        {
            object mz = Activator.CreateInstance(tz, T, null, new object[] { Fs, 2 }, null);
            MethodInfo pz = tz.GetMethod("Procesa", T);
            System.Diagnostics.Stopwatch w = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < x.Length; i += 8192) pz.Invoke(mz, new object[] { x, i, Math.Min(8192, x.Length - i) });
            mejorZ = Math.Min(mejorZ, w.Elapsed.TotalMilliseconds);
            object mm = Activator.CreateInstance(TM, T, null, new object[] { Fs, pesos }, null);
            MethodInfo pm = TM.GetMethod("Procesa", T);
            w = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < x.Length; i += 8192) pm.Invoke(mm, new object[] { x, i, Math.Min(8192, x.Length - i) });
            mejorM = Math.Min(mejorM, w.Elapsed.TotalMilliseconds);
        }
        double cuadros = x.Length / 2.0;
        Console.WriteLine("        el zumbido: " + F(mejorZ * 1e6 / cuadros, "0.0") + " ns por cuadro; la medida entera (sonoridad, picos, LRA y zumbido): " + F(mejorM * 1e6 / cuadros, "0.0") + " ns por cuadro (Mono, esta maquina)");
        Check(mejorZ < mejorM, "medir el zumbido cuesta menos que el resto de la medida");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TS = asm.GetType("Rocola.SonoridadIntegrada", true); TSil = asm.GetType("Rocola.MapaSilencios", true);
        TM = asm.GetType("Rocola.MedidorBs1770", true); TQ = asm.GetType("Rocola.QuitaZumbido", false);
        if (TQ == null) { Check(false, "no esta Rocola.QuitaZumbido"); return 1; }
        string dir = Path.Combine(Path.GetTempPath(), "rocola_zumbido_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            try { ElFormato(); } catch (Exception e) { Check(false, "el formato: " + e); }
            try { DondeEmpiezaYAcaba(dir); } catch (Exception e) { Check(false, "donde empieza y acaba: " + e); }
            try { ElZumbido(dir); } catch (Exception e) { Check(false, "el zumbido: " + e); }
            try { LaEtapa(); } catch (Exception e) { Check(false, "la etapa: " + e); }
            try { EnLaCadena(dir); } catch (Exception e) { Check(false, "en la cadena: " + e); }
            try { LoQueCuesta(); } catch (Exception e) { Check(false, "lo que cuesta: " + e); }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

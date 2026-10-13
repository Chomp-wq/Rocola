using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// S1 A S5 DEL SONIDO (13-10-2026; ArreglosGrabacion.cs, GraveMono.cs y LoQueSuena.cs). Con senales armadas a mano por las
// etapas de verdad:
//  1) S1, un canal mudo: con el otro sonando, a los 1,5 s los dos dan el que suena a media potencia (la suma de potencias
//     no cambia); antes, intacto; en una pausa de los dos no cambia nada; cuando el mudo vuelve, se quita.
//  2) S2, la continua: con -40 dBFS de continua, a los pocos segundos la salida queda centrada; sin continua, bit a bit.
//  3) Una cancion normal, con los dos encendidos: sale bit a bit. Leer de a pedazos impares da lo mismo.
//  4) Paso directo (S4) e interruptores apagados: vuelve a salir lo grabado, en rampa.
//  5) Disco continuo: la pista siguiente sigue con lo de la anterior (sin salto de continua en el cambio).
//  6) S3, estereo estrecho solo si hace falta: paneo extremo -> se estrecha al ancho pedido; una mezcla al centro -> sale
//     bit a bit como con el estrecho apagado.
//  7) El limitador apunta lo que baja y el nivel sin cambiar la salida (S5), y el texto de lo que suena.
//  8) S4 sobre una cadena: todo lo que mejora a cero y los pases puestos; al quitarlo, vuelven.
//  9) La configuracion, la cadena de cada cancion y el relevo sin pausa.
// No es escucharlo en el bar: eso solo se sabe alli.
class Fuente : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public Fuente(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class Arreglos
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TArr, TMono;
    const int FS = 44100;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static ISampleProvider Arreglo(float[] x, int ch, bool mudo, bool dc) { return (ISampleProvider)Nuevo(TArr, new Fuente(x, FS, ch), mudo, dc); }
    static object P(object o, string n) { return o.GetType().GetProperty(n, T) != null ? o.GetType().GetProperty(n, T).GetValue(o, null) : o.GetType().GetField(n, T).GetValue(o); }
    static void PonP(object o, string n, object v) { PropertyInfo p = o.GetType().GetProperty(n, T); if (p != null) p.SetValue(o, v, null); else o.GetType().GetField(n, T).SetValue(o, v); }
    static object Llama(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in o.GetType().GetMethods(T)) if (m.Name == n && m.GetParameters().Length == a.Length) return m.Invoke(o, a);
        throw new Exception("no esta " + n);
    }

    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    // Musica de mentira: tres tonos y un poco de ruido, distinto en cada canal (pero correlado).
    static float[] Musica(double seg, double gl, double gr, double dc, int semilla)
    {
        Random rnd = new Random(semilla); int n = (int)(seg * FS); float[] x = new float[n * 2];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / FS;
            double v = 0.25 * Math.Sin(2 * Math.PI * 110 * t) + 0.15 * Math.Sin(2 * Math.PI * 440 * t + 1) + 0.08 * Math.Sin(2 * Math.PI * 2500 * t);
            double w = 0.05 * (rnd.NextDouble() * 2 - 1);
            x[2 * i] = (float)(gl * (v + w) + dc); x[2 * i + 1] = (float)(gr * (0.9 * v - w + 0.03 * Math.Sin(2 * Math.PI * 700 * t)) + dc);
        }
        return x;
    }
    static double Potencia(float[] x, int desde, int hasta, int canal)
    {
        double s = 0; int k = 0;
        for (int i = desde; i < hasta; i++) { double v = x[2 * i + canal]; s += v * v; k++; }
        return k > 0 ? s / k : 0;
    }
    static double Media(float[] x, int desde, int hasta, int canal)
    {
        double s = 0; int k = 0;
        for (int i = desde; i < hasta; i++) { s += x[2 * i + canal]; k++; }
        return k > 0 ? s / k : 0;
    }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TArr = TF.GetNestedType("ArreglaGrabacion", T);
        TMono = TF.GetNestedType("GraveMono", T);
        Check(TArr != null && TMono != null, "estan la etapa de los arreglos y la del grave en mono");
        if (TArr == null || TMono == null) return 1;

        Console.WriteLine("\n  1) S1: UN CANAL MUDO, POR LOS DOS\n");
        float[] mudo = Musica(6, 1, 0, 0, 1);
        for (int i = 0; i < mudo.Length / 2; i++) mudo[2 * i + 1] = 0f;                    // el derecho, vacio
        for (int i = (int)(3.0 * FS); i < (int)(3.6 * FS); i++) { mudo[2 * i] = 0f; }      // y una pausa de los dos a los 3 s
        float[] s1 = LeeTodo(Arreglo(mudo, 2, true, true), 4096);
        int a2 = (int)(1.4 * FS), b1 = (int)(2.2 * FS), b2 = (int)(2.9 * FS);
        Check(Igual(Sub(s1, 0, a2), Sub(mudo, 0, a2)), "el primer 1,4 s sale tal cual (aun no se sabe si es un defecto)");
        double pl = Potencia(s1, b1, b2, 0), pr = Potencia(s1, b1, b2, 1), po = Potencia(mudo, b1, b2, 0);
        Info("antes: izquierdo " + F(10 * Math.Log10(po), "0.0") + " dB, derecho mudo; despues: " + F(10 * Math.Log10(pl), "0.0") + " y " + F(10 * Math.Log10(pr), "0.0") + " dB");
        Check(Math.Abs(pl - pr) < 1e-6 * Math.Max(pl, 1e-12) + 1e-9 && pr > 0, "a los 2 s los dos canales dan lo mismo");
        Check(Math.Abs((pl + pr) / po - 1.0) < 0.01, "a media potencia cada uno: la suma de potencias no cambia (el nivelado sigue valiendo)");
        int c1 = (int)(3.7 * FS), c2 = (int)(4.3 * FS);
        Check(Potencia(s1, c1, c2, 1) > 0.4 * Potencia(s1, c1, c2, 0), "despues de una pausa de los dos sigue arreglado (no se decide en silencio)");
        // El mudo vuelve a sonar: se quita.
        float[] vuelve = Musica(5, 1, 1, 0, 2);
        for (int i = 0; i < (int)(2.5 * FS); i++) vuelve[2 * i + 1] = 0f;
        float[] s1b = LeeTodo(Arreglo(vuelve, 2, true, true), 4096);
        int d1 = (int)(4.0 * FS), d2 = (int)(5.0 * FS) - 1;
        Check(Igual(Sub(s1b, d1, d2), Sub(vuelve, d1, d2)), "cuando el canal vuelve a sonar, en medio segundo sale otra vez tal cual");
        // Un instrumento a un lado con su eco en el otro (-20 dB): no es un canal mudo.
        float[] eco = Musica(4, 1, 0.1, 0, 3);
        Check(Igual(LeeTodo(Arreglo(eco, 2, true, true), 4096), eco), "un lado con su eco en el otro (-20 dB) no se toma por mudo: bit a bit");

        Console.WriteLine("\n  2) S2: LA CORRIENTE CONTINUA\n");
        float[] conDc = Musica(8, 1, 1, 0.01, 4);   // -40 dBFS de continua
        float[] s2 = LeeTodo(Arreglo(conDc, 2, true, true), 4096);
        int e1 = (int)(6.0 * FS), e2 = (int)(8.0 * FS) - 1;
        double mAntes = Media(conDc, e1, e2, 0), mDesp = Media(s2, e1, e2, 0), mDespR = Media(s2, e1, e2, 1);
        Info("media del izquierdo: " + F(mAntes, "0.00000") + " antes, " + F(mDesp, "0.00000") + " despues (derecho " + F(mDespR, "0.00000") + ")");
        Check(Math.Abs(mDesp) < 0.0015 && Math.Abs(mDespR) < 0.0015, "a los 6 s la continua ya no esta (menos de -56 dBFS)");
        Check(Igual(Sub(s2, 0, (int)(2.9 * FS)), Sub(conDc, 0, (int)(2.9 * FS))), "los primeros 2,9 s, tal cual (se espera a estar seguro)");
        double cambio = 0; for (int i = 2 * e1; i < 2 * e2; i++) cambio = Math.Max(cambio, Math.Abs((s2[i] - conDc[i]) + 0.01));
        Check(cambio < 0.003, "lo unico que cambia es la continua: la diferencia con lo grabado es la propia continua (resto " + F(cambio, "0.0000") + ")");
        float[] dcChica = Musica(6, 1, 1, 0.001, 5);   // -60 dBFS: no hace falta
        Check(Igual(LeeTodo(Arreglo(dcChica, 2, true, true), 4096), dcChica), "con -60 dBFS de continua no se toca: bit a bit");
        float[] mono = new float[(int)(6 * FS)];
        for (int i = 0; i < mono.Length; i++) mono[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 220 * i / FS) + 0.01);
        float[] sm = LeeTodo((ISampleProvider)Nuevo(TArr, new Fuente(mono, FS, 1), true, true), 4096);
        double mm = 0; for (int i = (int)(5 * FS); i < mono.Length; i++) mm += sm[i]; mm /= mono.Length - (int)(5 * FS);
        Check(Math.Abs(mm) < 0.0015, "un archivo de un canal: tambien se le quita la continua");

        Console.WriteLine("\n  3) UNA CANCION NORMAL: BIT A BIT\n");
        float[] normal = Musica(6, 1, 1, 0, 6);
        Check(Igual(LeeTodo(Arreglo(normal, 2, true, true), 4096), normal), "con los dos encendidos, una cancion sin defectos sale bit a bit");
        Check(Igual(LeeTodo(Arreglo(conDc, 2, true, true), 1023), s2), "leer de a 1023 muestras (cuadros partidos) da lo mismo que de a 4096");

        Console.WriteLine("\n  4) PASO DIRECTO (S4) E INTERRUPTORES APAGADOS\n");
        Check(Igual(LeeTodo(Arreglo(conDc, 2, false, false), 4096), conDc) && Igual(LeeTodo(Arreglo(mudo, 2, false, false), 4096), mudo),
              "con los dos interruptores apagados, sale lo grabado");
        ISampleProvider ar = Arreglo(conDc, 2, true, true);
        float[] parte = new float[(int)(5 * FS) * 2]; int leidos = 0;
        while (leidos < parte.Length) { int n = ar.Read(parte, leidos, Math.Min(4096, parte.Length - leidos)); if (n <= 0) break; leidos += n; }
        Check(Math.Abs(Convert.ToSingle(P(ar, "ContinuaAhora")) - 1f) < 1e-6, "a los 5 s esta restando la continua (lo ve Ctrl+F3)");
        PonP(ar, "Pasa", true);
        float[] resto = LeeTodo(ar, 4096);
        int off = (int)(5 * FS) * 2, desde = (int)(1.1 * FS) * 2;
        bool igual = true; for (int i = desde; i < resto.Length; i++) if (resto[i] != conDc[off + i]) { igual = false; break; }
        Check(igual, "con el paso directo, al segundo sale lo grabado otra vez, bit a bit");
        bool suave = true; float prev = parte[parte.Length - 2] - conDc[off - 2];
        for (int i = 0; i < desde; i += 2) { float d = resto[i] - conDc[off + i]; if (Math.Abs(d - prev) > 0.0005f) suave = false; prev = d; }
        Check(suave, "y entra en rampa, sin escalon");

        Console.WriteLine("\n  5) DISCO CONTINUO\n");
        float[] p1 = Musica(5, 1, 1, 0.01, 7), p2 = Musica(3, 1, 1, 0.01, 8);
        ISampleProvider e1p = Arreglo(p1, 2, true, true); LeeTodo(e1p, 4096);
        ISampleProvider e2p = Arreglo(p2, 2, true, true); Llama(e2p, "Sigue", e1p);
        float[] o2 = LeeTodo(e2p, 4096);
        Check(Math.Abs(Media(o2, 0, (int)(0.5 * FS), 0)) < 0.003, "la pista siguiente del mismo disco resta la continua desde la primera muestra (sin salto)");

        Console.WriteLine("\n  6) S3: ESTEREO ESTRECHO SOLO SI HACE FALTA\n");
        float[] pan = new float[(int)(8 * FS) * 2];
        for (int i = 0; i < pan.Length / 2; i++)
        {   // la bateria (ruido) toda a la izquierda, la voz (un tono) toda a la derecha
            pan[2 * i] = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * i / FS + Math.Sin(i * 0.001)));
            pan[2 * i + 1] = (float)(0.3 * Math.Sin(2 * Math.PI * 1500 * i / FS));
        }
        object mono1 = Nuevo(TMono, new Fuente(pan, FS, 2), 120f, .6f, 30f, true, false);
        Llama(mono1, "SoloSiHaceFalta", true);
        LeeTodo((ISampleProvider)mono1, 4096);
        float an1 = Convert.ToSingle(P(mono1, "AnchoAhora"));
        Info("paneo extremo: ancho al final " + F(an1 * 100, "0") + " %");
        Check(Math.Abs(an1 - 0.6f) < 0.01f, "una mezcla con los instrumentos a los lados se estrecha al 60 %");
        float[] centro = Musica(8, 1, 1, 0, 9);
        object m2 = Nuevo(TMono, new Fuente(centro, FS, 2), 120f, .6f, 30f, true, false);
        Llama(m2, "SoloSiHaceFalta", true);
        float[] conAuto = LeeTodo((ISampleProvider)m2, 4096);
        float[] sinAncho = LeeTodo((ISampleProvider)Nuevo(TMono, new Fuente(centro, FS, 2), 120f, .6f, 30f, false, false), 4096);
        Check(Igual(conAuto, sinAncho) && Math.Abs(Convert.ToSingle(P(m2, "AnchoAhora")) - 1f) < 1e-6,
              "una mezcla al centro: bit a bit como con el estrecho apagado");
        float[] fijo = LeeTodo((ISampleProvider)Nuevo(TMono, new Fuente(centro, FS, 2), 120f, .6f, 30f, true, false), 4096);
        object m3 = Nuevo(TMono, new Fuente(centro, FS, 2), 120f, .6f, 30f, true, false);
        Llama(m3, "SoloSiHaceFalta", false);
        Check(Igual(LeeTodo((ISampleProvider)m3, 4096), fijo), "con el automatico apagado, el estrecho de siempre, bit a bit");
        object m4 = Nuevo(TMono, new Fuente(pan, FS, 2), 0f, .6f, 30f, true, false);   // grave_mono=0: sin cruce
        Llama(m4, "SoloSiHaceFalta", true);
        LeeTodo((ISampleProvider)m4, 4096);
        Check(Math.Abs(Convert.ToSingle(P(m4, "AnchoAhora")) - 0.6f) < 0.01f, "tambien sin el grave en mono (sin cruce)");

        Console.WriteLine("\n  7) S5: EL LIMITADOR APUNTA SIN CAMBIAR NADA\n");
        Type TLim = TF.GetNestedType("LimitadorPicoReal", T);
        float[] fuerte = Musica(2, 3, 3, 0, 10);
        object lim = Nuevo(TLim, new Fuente(fuerte, FS, 2), 0.89f, 3.0);
        LeeTodo((ISampleProvider)lim, 4096);
        float baja = Convert.ToSingle(Llama(lim, "TomaBajadaDb")), nivel = Convert.ToSingle(Llama(lim, "TomaNivelDb"));
        Info("bajo " + F(-baja, "0.0") + " dB; nivel " + F(nivel, "0.0") + " dB");
        Check(baja < -3f && nivel < 0f && nivel > -20f, "dice cuanto bajo y a que nivel salio");
        Check(Convert.ToSingle(Llama(lim, "TomaBajadaDb")) == 0f && Convert.ToSingle(Llama(lim, "TomaNivelDb")) <= -100f,
              "y empieza de cero en cada lectura");
        object f = Codigo.FormSinEncender(TF);
        string txt = (string)TF.GetMethod("TextoLoQueSuena", T).Invoke(f, null);
        Check(txt.Length > 0 && txt.IndexOf("No suena nada", StringComparison.Ordinal) >= 0, "sin cancion, la hoja lo dice");

        Console.WriteLine("\n  8) S4 SOBRE UNA CADENA\n");
        Type TCad = TF.GetNestedType("CadenaCancion", T);
        object cad = Activator.CreateInstance(TCad, true);
        float[] corta = Musica(1, 1, 1, 0, 11);
        object tap = Nuevo(TF.GetNestedType("LevelTap", T), new Fuente(corta, FS, 2), null);
        object bajo = Nuevo(TF.GetNestedType("DeepBassProvider", T), new Fuente(corta, FS, 2), 0.5f);
        object arr = Nuevo(TArr, new Fuente(corta, FS, 2), true, true);
        PonP(cad, "Tap", tap); PonP(cad, "Bajo", bajo); PonP(cad, "Arreglo", arr);
        PonP(tap, "Excite", 0.5f); PonP(tap, "EqDb", new float[] { 1, 2, 3, 0, 0, 0, 0, 0 });
        FieldInfo fSin = TF.GetField("sinMejoras", T);
        fSin.SetValue(f, true);
        TF.GetMethod("AplicaSinMejoras", T).Invoke(f, new object[] { cad });
        float[] eq = (float[])P(tap, "EqDb"); bool plana = eq != null; if (eq != null) foreach (float v in eq) if (v != 0f) plana = false;
        Check(Convert.ToSingle(P(tap, "Excite")) == 0f && plana && Convert.ToSingle(P(bajo, "Amount")) == 0f && (bool)P(arr, "Pasa"),
              "sin mejoras: realce, EQ y bajo a cero, y los arreglos en pase");
        fSin.SetValue(f, false);
        TF.GetMethod("AplicaSinMejoras", T).Invoke(f, new object[] { cad });
        Check(!(bool)P(arr, "Pasa"), "y al quitarlo, los arreglos vuelven (los valores de siempre los pone ApplyNormNow)");
        Check(Codigo.Llama(TF.GetMethod("ApplyNormNow", T), "AplicaSinMejoras") && Codigo.Llama(Codigo.ConAyudantes(TF, "AplicaAPreparada"), "AplicaSinMejoras"),
              "se aplica a la que suena y a la siguiente");
        List<string> ventana = Codigo.ConAyudantes(TF, "VentanaAjustes");
        Check(Codigo.Llama(ventana, "PonSinMejoras") && Codigo.Llama(ventana, "TextoLoQueSuena"), "la ventana tiene el boton y el texto en vivo");

        Console.WriteLine("\n  9) LA CONFIGURACION Y LA CADENA\n");
        string datos = Path.Combine(Path.GetTempPath(), "rocola_arreglos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(datos);
        TF.GetField("carpetaDatos", T).SetValue(null, datos);
        string cfg = (string)TF.GetMethod("TextoConfig", T).Invoke(f, null);
        Check(cfg.Contains("canal_mudo=1") && cfg.Contains("quita_continua=1") && cfg.Contains("estereo_auto=0"), "de fabrica: S1 y S2 encendidos, S3 apagado");
        File.WriteAllText(Path.Combine(datos, "rocola_config.txt"), "canal_mudo=0\r\nquita_continua=0\r\nestereo_auto=1\r\n");
        object g = Codigo.FormSinEncender(TF);
        TF.GetMethod("LoadConfigPrefs", T).Invoke(g, null);
        Check(!(bool)TF.GetField("canalMudo", T).GetValue(null) && !(bool)TF.GetField("quitaContinua", T).GetValue(null) && (bool)TF.GetField("estereoAuto", T).GetValue(null),
              "se leen del archivo");
        TF.GetField("canalMudo", T).SetValue(null, true); TF.GetField("quitaContinua", T).SetValue(null, true); TF.GetField("estereoAuto", T).SetValue(null, false);
        try { Directory.Delete(datos, true); } catch { }
        List<string> arma = Codigo.ConAyudantes(TF, "ArmaCadena");
        Check(Codigo.Llama(arma, "ArreglaGrabacion..ctor") || Codigo.Llama(arma, "ArreglaGrabacion"), "cada cancion lleva los arreglos en su cadena");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "AplicaBocinas"), "Ajusta"), "y los interruptores se oyen al instante");

        Console.WriteLine(fallos == 0 ? "PASS arreglos (" + oks + " comprobaciones; senales armadas, no el bar)" : "FAIL arreglos: " + fallos + " fallos");
        return fallos;
    }
    static float[] Sub(float[] x, int desdeCuadro, int hastaCuadro)
    {
        int a = desdeCuadro * 2, b = Math.Min(x.Length, hastaCuadro * 2);
        float[] r = new float[Math.Max(0, b - a)]; Array.Copy(x, a, r, 0, r.Length); return r;
    }
}

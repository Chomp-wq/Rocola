using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// LAS BOCINAS DEL BILLAR (Rocola/GraveMono.cs, 11-10-2026): el subgrave fuera y el estereo un poco mas estrecho.
// Con tonos sinteticos por la etapa de verdad:
//  1) Apagado (ancho 100 %, sin subgrave) sale bit a bit como la etapa de siempre.
//  2) El subgrave: la curva de un Butterworth de cuarto orden en 30 Hz, EN FASE LINEAL desde el 24-10-2026 (sin girar la fase del
//     grave: no mueve el bombo ni sube los picos de un masterizado aplastado, que el filtro de antes subia); lo que se oye no cambia.
//  3) El ancho: lo que va todo a un lado sale por el otro 12 dB mas bajo (al 60 %); lo del centro, igual.
//  4) Un archivo de un canal: solo el subgrave.
//  5) Disco continuo: la siguiente pista empalma con la anterior sin escalon (en fase lineal la anterior acaba sin conocer la
//     siguiente: no puede salir bit a bit como una sola), y tras el silencio sale cero exacto.
//  6) La configuracion, la cadena de cada cancion y que leer no reserve memoria.
//  7) Los interruptores de Ctrl+F3 (apagados de fabrica: son para las bocinas del billar), al instante.
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

class Subgrave
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TMono;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static ISampleProvider Etapa(ISampleProvider s, float corte, float ancho, float sub) { return (ISampleProvider)Nuevo(TMono, s, corte, ancho, sub); }
    static ISampleProvider Vieja(ISampleProvider s, float corte) { return (ISampleProvider)Nuevo(TMono, s, corte); }
    static ISampleProvider ConInterruptores(ISampleProvider s, float corte, bool ancho, bool sub) { return (ISampleProvider)Nuevo(TMono, s, corte, .6f, 30f, ancho, sub); }

    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    static float[] Tono(int fs, double hz, double seg, double gl, double gr)
    {
        int n = (int)(seg * fs); float[] x = new float[n * 2];
        for (int i = 0; i < n; i++) { double v = 0.5 * Math.Sin(2 * Math.PI * hz * i / fs); x[2 * i] = (float)(gl * v); x[2 * i + 1] = (float)(gr * v); }
        return x;
    }
    static float[] Ruido(int fs, double seg, int semilla)
    {
        Random r = new Random(semilla); int n = (int)(seg * fs); float[] x = new float[n * 2];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(0.3 * (r.NextDouble() * 2 - 1));
        return x;
    }
    // Amplitud de 'hz' en un canal: proyeccion sobre seno y coseno en los ultimos 'seg' segundos (periodos enteros).
    static double Amp(float[] y, int ch, int c, int fs, double hz, double seg)
    {
        int frames = y.Length / ch, per = Math.Max(1, (int)Math.Round(seg * hz));
        int n = Math.Min(frames / 2, (int)Math.Round(per * fs / hz)), desde = frames - n;
        double s = 0, co = 0;
        for (int i = desde; i < frames; i++) { double w = 2 * Math.PI * hz * i / fs; s += y[i * ch + c] * Math.Sin(w); co += y[i * ch + c] * Math.Cos(w); }
        return 2 * Math.Sqrt(s * s + co * co) / n;
    }
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-15)); }
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
        TMono = TF.GetNestedType("GraveMono", T);
        Check(TMono != null && TMono.GetConstructor(T, null, new Type[] { typeof(ISampleProvider), typeof(float), typeof(float), typeof(float) }, null) != null,
              "la etapa del grave en mono lleva ahora el ancho y el subgrave");
        int fs = 44100;

        Console.WriteLine("\n  1) APAGADO, COMO SIEMPRE\n");
        float[] ruido = Ruido(fs, 6.0, 3);
        foreach (float corte in new float[] { 120f, 0f, 80f })
            Check(Igual(LeeTodo(Vieja(new Fuente(ruido, fs, 2), corte), 1000), LeeTodo(Etapa(new Fuente(ruido, fs, 2), corte, 1f, 0f), 1000)),
                  "con ancho 100 % y sin subgrave sale bit a bit como la etapa de siempre (corte " + corte + " Hz)");
        object vieja = Vieja(new Fuente(ruido, fs, 1), 120f);
        Check(!(bool)TMono.GetProperty("Activo", T).GetValue(vieja, null), "y un archivo de un canal, sin subgrave, ni se toca");

        Console.WriteLine("\n  2) EL SUBGRAVE (30 Hz: la curva de cuarto orden, en fase lineal)\n");
        double fc = GetF("SubDeFabrica");
        Check(Math.Abs(fc - 30) < .01, "de fabrica corta en 30 Hz");
        foreach (double hz in new double[] { 15, 20, 25, 30, 40, 50, 60, 80, 120, 1000, 8000 })
        {
            float[] x = Tono(fs, hz, 4.0, 1, 1);
            float[] y = LeeTodo(Etapa(new Fuente(x, fs, 2), 120f, 1f, 30f), 4096);
            double g = Db(Amp(y, 2, 0, fs, hz, 1.5) / Amp(x, 2, 0, fs, hz, 1.5));
            double teo = Db(1.0 / Math.Sqrt(1 + Math.Pow(30.0 / hz, 8)));
            Info(F(hz, "0") + " Hz: " + F(g, "0.00") + " dB (Butterworth: " + F(teo, "0.00") + ")");
            if (hz >= 25) Check(Math.Abs(g - teo) < .35, F(hz, "0") + " Hz como un Butterworth de cuarto orden en 30 Hz (" + F(g, "0.00") + " dB)");
            if (hz == 20) Check(g < -12, "a 20 Hz ya quita mas de 12 dB (" + F(g, "0.0") + ")");
            if (hz >= 60) Check(Math.Abs(g) < .05, F(hz, "0") + " Hz sale igual (" + F(g, "0.000") + " dB)");
        }
        Check(Math.Abs(fc - 30) < .01, "fin del barrido");
        FaseLineal(fs);

        Console.WriteLine("\n  3) EL ANCHO (60 %)\n");
        double an = GetF("AnchoDeFabrica");
        Check(Math.Abs(an - .6) < 1e-6, "de fabrica, 60 %");
        float[] izq = Tono(fs, 1000, 2.0, 1, 0);
        float[] yi = LeeTodo(Etapa(new Fuente(izq, fs, 2), 120f, .6f, 30f), 4096);
        double aL = Amp(yi, 2, 0, fs, 1000, .5), aR = Amp(yi, 2, 1, fs, 1000, .5);
        Check(Math.Abs(Db(aR / aL) - Db(.4 / 1.6)) < .05 && Math.Abs(Db(aL / Amp(izq, 2, 0, fs, 1000, .5)) - Db(.8)) < .05,
              "un instrumento todo a la izquierda sale por la derecha " + F(Db(aR / aL), "0.0") + " dB mas bajo (antes: nada), y por la izquierda a " + F(Db(.8), "0.0") + " dB");
        float[] cen = Tono(fs, 1000, 2.0, 1, 1);
        float[] yc = LeeTodo(Etapa(new Fuente(cen, fs, 2), 120f, .6f, 30f), 4096);
        bool gemelos = true; for (int i = 0; i < yc.Length; i += 2) if (yc[i] != yc[i + 1]) { gemelos = false; break; }
        Check(gemelos && Math.Abs(Db(Amp(yc, 2, 0, fs, 1000, .5) / Amp(cen, 2, 0, fs, 1000, .5))) < .01, "lo del centro (voz, bajo, bombo) sale igual y los dos canales identicos");
        float[] ys = LeeTodo(Etapa(new Fuente(izq, fs, 2), 0f, .6f, 0f), 4096);
        Check(Math.Abs(Db(Amp(ys, 2, 1, fs, 1000, .5) / Amp(ys, 2, 0, fs, 1000, .5)) - Db(.25)) < .05, "y lo mismo con el grave en mono apagado (grave_mono=0)");
        float[] ycero = LeeTodo(Etapa(new Fuente(izq, fs, 2), 120f, 0f, 0f), 4096);
        Check(Math.Abs(Db(Amp(ycero, 2, 1, fs, 1000, .5) / Amp(ycero, 2, 0, fs, 1000, .5))) < .01, "al 0 %, mono del todo");

        Console.WriteLine("\n  4) UN ARCHIVO DE UN CANAL\n");
        float[] m20 = new float[fs * 4], m1k = new float[fs * 4];
        for (int i = 0; i < m20.Length; i++) { m20[i] = (float)(.5 * Math.Sin(2 * Math.PI * 20 * i / fs)); m1k[i] = (float)(.5 * Math.Sin(2 * Math.PI * 1000 * i / fs)); }
        float[] o20 = LeeTodo(Etapa(new Fuente(m20, fs, 1), 120f, .6f, 30f), 999), o1k = LeeTodo(Etapa(new Fuente(m1k, fs, 1), 120f, .6f, 30f), 999);
        Check(Db(Amp(o20, 1, 0, fs, 20, 1.5) / .5) < -12 && Math.Abs(Db(Amp(o1k, 1, 0, fs, 1000, .5) / .5)) < .05, "un canal: el subgrave tambien, y lo que se oye igual");

        Console.WriteLine("\n  5) DISCO CONTINUO Y SILENCIO\n");
        float[] todo = Ruido(fs, 4.0, 11);
        int mitad = (todo.Length / 2) & ~1;
        float[] a1 = new float[mitad], a2 = new float[todo.Length - mitad];
        Array.Copy(todo, a1, mitad); Array.Copy(todo, mitad, a2, 0, a2.Length);
        float[] junta = LeeTodo(Etapa(new Fuente(todo, fs, 2), 120f, .6f, 30f), 2048);
        ISampleProvider ea = Etapa(new Fuente(a1, fs, 2), 120f, .6f, 30f), eb = Etapa(new Fuente(a2, fs, 2), 120f, .6f, 30f);
        float[] p1 = LeeTodo(ea, 2048);
        TMono.GetMethod("Sigue", T).Invoke(eb, new object[] { ea });
        float[] p2 = LeeTodo(eb, 2048);
        float[] seguidas = new float[p1.Length + p2.Length]; Array.Copy(p1, seguidas, p1.Length); Array.Copy(p2, 0, seguidas, p1.Length, p2.Length);
        double dif = 0, salto = 0, normal = 0; int cambio = mitad / 2;
        for (int i = 0; i < junta.Length && i < seguidas.Length; i++) dif = Math.Max(dif, Math.Abs(junta[i] - seguidas[i]));
        for (int c = 0; c < 2; c++)
        {
            for (int i = 2 * (cambio - 3) + c; i <= 2 * (cambio + 3) + c; i += 2) salto = Math.Max(salto, Math.Abs(seguidas[i] - seguidas[i - 2]));
            for (int i = 2 * 4410 + c; i < 2 * (cambio - 4410); i += 2) normal = Math.Max(normal, Math.Abs(seguidas[i] - seguidas[i - 2]));
        }
        Info("dos pistas seguidas frente a una sola: diferencia maxima " + F(dif, "0.0000") + " (el grave de la siguiente que la anterior no conocia); salto en el cambio " + F(salto, "0.0000") + ", normal " + F(normal, "0.0000"));
        Check(seguidas.Length == junta.Length && salto <= normal * 1.25, "dos pistas seguidas sin fundido: el cambio no da un golpe (el salto entre muestras, como el normal)");
        float[] tonoSilencio = new float[fs * 2 * 12];
        Array.Copy(Tono(fs, 40, 1.0, 1, .3), tonoSilencio, fs * 2);
        object es = Etapa(new Fuente(tonoSilencio, fs, 2), 120f, .6f, 30f);
        float[] tras = LeeTodo((ISampleProvider)es, 4096);
        bool ceros = tras.Length == tonoSilencio.Length; for (int i = tras.Length - fs * 2 * 8; i < tras.Length && ceros; i++) if (tras[i] != 0f) ceros = false;
        Check(ceros, "tras el silencio sale cero exacto (sin restos diminutos ni desnormales, que en un procesador viejo son lentisimos)");

        Console.WriteLine("\n  6) CONFIGURACION Y CADENA\n");
        List<string> lee = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), esc = Codigo.Textos(TF.GetMethod("TextoConfig", T));
        Check(lee.Contains("ancho_estereo=") && esc.Contains("ancho_estereo=") && lee.Contains("subgrave=") && esc.Contains("subgrave="),
              "ancho_estereo= y subgrave= se leen y se guardan en la configuracion");
        MethodInfo pa = TF.GetMethod("AnchoEstereo", T), ps = TF.GetMethod("SubgraveHz", T);
        Func<string, float> A = delegate(string s) { return (float)pa.Invoke(null, new object[] { s }); };
        Func<string, float> S = delegate(string s) { return (float)ps.Invoke(null, new object[] { s }); };
        Check(Math.Abs(A("60") - .6f) < 1e-6 && A("100") == 1f && A("150") == 1f && A("-5") == 0f && A("0") == 0f, "ancho: 60 es 60 %, de 0 a 100");
        Check(S("30") == 30f && S("0") == 0f && S("-3") == 0f && S("10") == 15f && S("200") == 60f, "subgrave: 0 lo quita; entre 15 y 60 Hz");
        FieldInfo fa = TF.GetField("anchoEstereo", T); object antes = fa.GetValue(null);
        fa.SetValue(null, .33f); Check(A("abc") == .33f && A("") == .33f, "un texto sin sentido deja lo que habia"); fa.SetValue(null, antes);
        MethodBase arma = TF.GetMethod("ArmaCadena", T);
        int l1, e1, l2, e2; Codigo.UsosDeCampo(arma, "anchoEstereo", out l1, out e1); Codigo.UsosDeCampo(TF.GetProperty("SubgraveEf", T).GetGetMethod(true), "subgraveHz", out l2, out e2);
        Check(l1 > 0 && l2 > 0 && Codigo.Llama(arma, "get_SubgraveEf"), "cada cancion arma su etapa con el ancho y el subgrave de la configuracion (en el billar, al menos 40 Hz: SubgraveEf)");
        bool reserva = false;
        foreach (string m in new string[] { "Read", "Interior", "Enteros", "Cuadros", "SinCruce", "LeeEnFaseLineal", "Bloque", "GraveDelLado", "Hereda", "Sigue", "Ajusta",
                                            "SubQuiere", "SubLee", "Anillo", "Anillo1", "Entra", "Sale", "SubBloque", "SubLeeDelInterior", "LoQueSeQuita", "GraveDe" })
            foreach (Codigo.Paso p in Codigo.Pasos(TMono.GetMethod(m, T))) if (p.Op == 0x8D || p.Op == 0x73) reserva = true;
        Check(!reserva, "leer no reserva memoria");
        // Lo que cuesta, comparado con la etapa de siempre (Mono, este equipo: orientativo).
        float[] largo = Ruido(fs, 30.0, 5); float[] buf = new float[4410];
        double tVieja = Mejor(delegate { ISampleProvider p = Vieja(new Fuente(largo, fs, 2), 120f); while (p.Read(buf, 0, buf.Length) > 0) { } });
        double tNueva = Mejor(delegate { ISampleProvider p = Etapa(new Fuente(largo, fs, 2), 120f, .6f, 30f); while (p.Read(buf, 0, buf.Length) > 0) { } });
        Info("30 s de musica: la etapa de siempre " + F(tVieja, "0.0") + " ms, con ancho y subgrave " + F(tNueva, "0.0") + " ms (" + F((tNueva - tVieja) / 30.0, "0.00") + " ms mas por segundo de musica)");
        Check(tNueva < tVieja * 3 + 20, "cuesta poco mas que la etapa de siempre");

        Interruptores(fs);
        ContinuidadMono();

        Console.WriteLine(fallos == 0 ? "\n  OK subgrave: " + oks + " comprobaciones (tonos sinteticos; no es oirlo en el bar)" : "\n  FALLO subgrave: " + fallos + " de " + (oks + fallos));
        return fallos == 0 ? 0 : 1;
    }
    static void ContinuidadMono()
    {
        const int fs = 48000, n = 4800;
        float[] todo = new float[n * 2], a = new float[n], b = new float[n];
        for (int i = 0; i < todo.Length; i++) todo[i] = (float)(.5 * Math.Cos(2 * Math.PI * 30 * i / fs));
        Array.Copy(todo, a, n); Array.Copy(todo, n, b, 0, n);
        foreach (int bloque in new int[] { 1, 137, 4800 })
        {
            ISampleProvider primera = Etapa(new Fuente(a, fs, 1), 120f, 1f, 30f);
            ISampleProvider segunda = Etapa(new Fuente(b, fs, 1), 120f, 1f, 30f);
            float[] vacio = new float[1];
            Check(primera.Read(vacio, 0, 0) == 0 && !(bool)TMono.GetField("acabo", T).GetValue(primera),
                  "mono: una consulta vacia no marca fin de pista");
            TMono.GetMethod("Sigue", T).Invoke(segunda, new object[] { primera });
            Check(segunda.Read(vacio, 0, 0) == 0 && !(bool)TMono.GetField("empezo", T).GetValue(segunda),
                  "una consulta vacia de la siguiente no consume la herencia antes de que acabe la anterior");
            float[] uno = LeeTodo(primera, bloque);
            Check((bool)TMono.GetField("acabo", T).GetValue(primera), "mono: el fin queda reconocido");
            TMono.GetMethod("Sigue", T).Invoke(segunda, new object[] { primera });
            float[] salida = LeeTodo(segunda, bloque);
            double salto = Math.Abs(salida[0] - uno[uno.Length - 1]), normal = 0;
            for (int i = 1; i < n; i++) normal = Math.Max(normal, Math.Max(Math.Abs(salida[i] - salida[i - 1]), Math.Abs(uno[i] - uno[i - 1])));
            Check(salida.Length == n && salto <= normal * 1.25, "mono: dos pistas seguidas empalman sin escalon (" + F(salto, "0.00000") + " frente a " + F(normal, "0.00000") + " de normal), bloques de " + bloque);
        }
    }
    static void Interruptores(int fs)
    {
        Console.WriteLine("\n  7) LOS INTERRUPTORES DE Ctrl+F3\n");
        FieldInfo fe = TF.GetField("estereoEstrecho", T), fsb = TF.GetField("cortaSubgrave", T);
        Check(fe != null && fsb != null && !(bool)fe.GetValue(null) && !(bool)fsb.GetValue(null), "apagados de fabrica: son para el montaje del billar");
        float[] ruido = Ruido(fs, 4.0, 21);
        foreach (float corte in new float[] { 120f, 0f })
        {
            Check(Igual(LeeTodo(Vieja(new Fuente(ruido, fs, 2), corte), 777), LeeTodo(ConInterruptores(new Fuente(ruido, fs, 2), corte, false, false), 777)),
                  "con los dos apagados sale bit a bit como antes (corte " + corte + " Hz)");
            Check(Igual(LeeTodo(Etapa(new Fuente(ruido, fs, 2), corte, .6f, 30f), 777), LeeTodo(ConInterruptores(new Fuente(ruido, fs, 2), corte, true, true), 777)),
                  "y con los dos encendidos, como la etapa con ancho y subgrave (corte " + corte + " Hz)");
        }
        float[] mono = new float[fs * 2]; Random r = new Random(4); for (int i = 0; i < mono.Length; i++) mono[i] = (float)(r.NextDouble() - .5);
        Check(Igual(mono, LeeTodo(ConInterruptores(new Fuente(mono, fs, 1), 120f, false, false), 999)), "un archivo de un canal con los dos apagados sale tal cual");
        // Al instante: encender el estereo estrecho a mitad de cancion cambia lo que sale desde la siguiente lectura.
        float[] izq = Tono(fs, 1000, 3.0, 1, 0);
        ISampleProvider et = ConInterruptores(new Fuente(izq, fs, 2), 120f, false, false);
        float[] buf = new float[fs * 2];
        int n1 = et.Read(buf, 0, buf.Length); float[] antes = new float[n1]; Array.Copy(buf, antes, n1);
        MethodInfo aj = TMono.GetMethod("Ajusta", T);
        aj.Invoke(et, new object[] { true, true });
        int n2 = et.Read(buf, 0, buf.Length); float[] despues = new float[n2]; Array.Copy(buf, despues, n2);
        double rAntes = Amp(antes, 2, 1, fs, 1000, .3) / Amp(antes, 2, 0, fs, 1000, .3), rDespues = Amp(despues, 2, 1, fs, 1000, .3) / Amp(despues, 2, 0, fs, 1000, .3);
        Check(rAntes < 1e-3 && Math.Abs(Db(rDespues) - Db(.25)) < .1, "encenderlo se oye al instante en la cancion que suena (a la derecha: de nada a " + F(Db(rDespues), "0.0") + " dB)");
        float[] grave = Tono(fs, 20, 6.0, 1, 1);
        ISampleProvider eg = ConInterruptores(new Fuente(grave, fs, 2), 120f, false, false);
        float[] g1 = new float[fs * 4]; int m1 = eg.Read(g1, 0, g1.Length);
        aj.Invoke(eg, new object[] { false, true });
        float[] g2 = new float[fs * 8]; int m2 = eg.Read(g2, 0, g2.Length);
        float[] g2r = new float[m2]; Array.Copy(g2, g2r, m2);
        Check(m1 > 0 && Db(Amp(g2r, 2, 0, fs, 20, 1.0) / .5) < -12, "y el subgrave tambien (20 Hz: " + F(Db(Amp(g2r, 2, 0, fs, 20, 1.0) / .5), "0.0") + " dB)");
        // La ventana: los dos interruptores, lo que guardan y que se aplican ya.
        List<MethodBase> ven = Codigo.MetodosConAyudantes(TF, "VentanaAjustes");
        List<string> textos = new List<string>(); foreach (MethodBase m in ven) textos.AddRange(Codigo.Textos(m));
        bool lSub = false, lEst = false; foreach (string t in textos) { if (t.StartsWith("Quitar el subgrave")) lSub = true; if (t.StartsWith("Estereo al ")) lEst = true; }
        Check(lSub && lEst && Codigo.EscribeCampo(ven, "cortaSubgrave") && Codigo.EscribeCampo(ven, "estereoEstrecho"), "Ctrl+F3 -> Sonido tiene los dos interruptores y los cambia");
        bool aplica = false; foreach (MethodBase m in ven) if (Codigo.Llama(m, "AplicaBocinas")) aplica = true;
        Check(aplica && Codigo.Llama(TF.GetMethod("AplicaBocinas", T), "Ajusta"), "y los lleva al instante a lo que suena y a la siguiente");
        List<string> lee = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), esc = Codigo.Textos(TF.GetMethod("TextoConfig", T));
        Check(lee.Contains("estereo_estrecho=") && esc.Contains("estereo_estrecho=") && lee.Contains("corta_subgrave=") && esc.Contains("corta_subgrave="),
              "se guardan en la configuracion (estereo_estrecho=, corta_subgrave=)");
        // Desde el modo automatico del local (23-10-2026) ArmaCadena los lee por LocalDe, que devuelve los mandos tal cual con el automatico apagado.
        int l1 = 0, e1, l2 = 0, e2; bool armaLeeLocalDe = Codigo.Llama(TF.GetMethod("ArmaCadena", T), "LocalDe");
        foreach (MethodInfo mm in TF.GetMethods(T)) if (mm.Name == "LocalDe" && mm.GetParameters()[0].ParameterType.Name == "Track")
        { Codigo.UsosDeCampo(mm, "estereoEstrecho", out l1, out e1); Codigo.UsosDeCampo(mm, "cortaSubgrave", out l2, out e2); }
        Check(armaLeeLocalDe && l1 > 0 && l2 > 0, "y cada cancion nueva nace con ellos como estan (por LocalDe, que sin el automatico devuelve los mandos)");
    }
    // EN FASE LINEAL: un tono sale en su sitio (sin desfase), y una onda aplanada como la de un masterizado moderno no sube de pico.
    // El filtro de antes (dos bicuadradas de Butterworth) se rehace aqui para ensenar lo que subia.
    static void FaseLineal(int fs)
    {
        foreach (double hz in new double[] { 40, 50, 60, 80, 120 })
        {
            float[] x = Tono(fs, hz, 4.0, 1, 1), y = LeeTodo(Etapa(new Fuente(x, fs, 2), 120f, 1f, 30f), 4096);
            double grados = Fase(y, fs, hz) - Fase(x, fs, hz), viejo = Fase(Antes(x, fs), fs, hz) - Fase(x, fs, hz);
            grados = Math.IEEERemainder(grados, 360); viejo = Math.IEEERemainder(viejo, 360);
            Check(Math.Abs(grados) < 0.5, F(hz, "0") + " Hz sale en su sitio: " + F(grados, "0.00") + " grados (el filtro de antes lo corria " + F(viejo, "0") + ")");
        }
        // Un bombo de 50 Hz con sus armonicos, aplanado al techo (como un masterizado con mucho limitador).
        int n = fs * 4; float[] q = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs, v = 0;
            for (int k = 1; k <= 7; k++) v += Math.Sin(2 * Math.PI * 50 * k * t) / k;
            v = Math.Max(-0.7, Math.Min(0.7, 0.9 * v));
            q[2 * i] = q[2 * i + 1] = (float)v;
        }
        double p0 = Pico(q, fs), p1 = Pico(LeeTodo(Etapa(new Fuente(q, fs, 2), 120f, 1f, 30f), 4096), fs), pv = Pico(Antes(q, fs), fs);
        Info("onda aplanada en 50 Hz: pico " + F(Db(p0), "0.00") + " dBFS; con el subgrave " + F(Db(p1), "0.00") + " (antes: " + F(Db(pv), "0.00") + ")");
        Check(Db(p1) - Db(p0) < 0.3 && Db(pv) - Db(p0) > Db(p1) - Db(p0) + 0.5, "una onda aplanada no sube de pico (" + F(Db(p1) - Db(p0), "+0.00;-0.00") + " dB; el filtro de antes la subia " + F(Db(pv) - Db(p0), "+0.00") + ")");
    }
    static double Fase(float[] y, int fs, double hz)
    {
        int frames = y.Length / 2, desde = frames / 4, hasta = frames - frames / 4; double s = 0, c = 0;
        for (int i = desde; i < hasta; i++) { double w = 2 * Math.PI * hz * i / fs; s += y[2 * i] * Math.Sin(w); c += y[2 * i] * Math.Cos(w); }
        return Math.Atan2(c, s) * 180 / Math.PI;
    }
    static double Pico(float[] y, int fs) { double m = 0; for (int i = 2 * fs; i < y.Length - 2 * fs; i++) m = Math.Max(m, Math.Abs(y[i])); return m; }
    static float[] Antes(float[] x, int fs)
    {
        double ws = 2.0 * Math.PI * 30 / fs, cs = Math.Cos(ws), sn = Math.Sin(ws);
        double[] qs = { 0.54119610, 1.30656296 };
        float[] y = new float[x.Length];
        for (int c = 0; c < 2; c++)
        {
            double[] v = new double[x.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = x[2 * i + c];
            foreach (double qq in qs)
            {
                double a0 = 1.0 + sn / (2.0 * qq), b0 = (1.0 + cs) / (2.0 * a0), a1 = -2.0 * cs / a0, a2 = (1.0 - sn / (2.0 * qq)) / a0;
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (int i = 0; i < v.Length; i++) { double xx = v[i], yy = b0 * (xx - 2 * x1 + x2) - a1 * y1 - a2 * y2; x2 = x1; x1 = xx; y2 = y1; y1 = yy; v[i] = yy; }
            }
            for (int i = 0; i < v.Length; i++) y[2 * i + c] = (float)v[i];
        }
        return y;
    }
    static double GetF(string n) { return Convert.ToDouble(TMono.GetField(n, T).GetValue(null)); }
    static double Mejor(Action a)
    {
        double m = double.MaxValue;
        for (int k = 0; k < 3; k++) { var w = System.Diagnostics.Stopwatch.StartNew(); a(); w.Stop(); m = Math.Min(m, w.Elapsed.TotalMilliseconds); }
        return m;
    }
}

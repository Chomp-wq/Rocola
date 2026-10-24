using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// EL GRAVE EN MONO (Rocola/GraveMono.cs)
//
// En el billar hay siete bocinas repartidas y no se sabe cual lleva cada canal. Por debajo de ~120 Hz la etapa pone
// el mismo grave en los dos canales: quien esta junto a la bocina "equivocada" ya no se queda sin bajo, y donde se
// juntan dos bocinas el grave en contrafase ya no se cancela. Por encima, todo sigue tal cual.
//
// LO QUE VIGILA:
//  1) La respuesta, medida con tonos por la etapa de verdad: el lado (L - R) cae como el cruce Linkwitz-Riley de
//     cuarto orden (-6 dB en el corte, 24 dB por octava por debajo); lo mono sale igual de fuerte en TODAS las
//     frecuencias; un instrumento abierto a un lado sigue a su lado por encima del corte. Y una grabacion con un
//     canal al reves no pierde el grave: se le devuelve en fase; en una normal eso nunca se activa.
//  2) Lo que no se toca: un archivo de un canal, uno de seis, grave_mono=0 y el silencio salen bit a bit.
//  3) Cualquier forma de leer da lo mismo bit a bit: lecturas de tamanos al azar, impares, de una muestra, y un
//     origen que entrega cuadros partidos.
//  4) Disco continuo: la pista que sigue sin fundido continua con los filtros de la anterior, y eso es exactamente
//     lo mismo que pasar las dos seguidas por una sola etapa. Sin eso, en el cambio queda un golpe en el grave.
//  5) Donde va en la cadena, la configuracion, que leer no reserve memoria, y lo que cuesta.
class FuenteSP : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf; readonly int trozo;
    public FuenteSP(float[] datos, int fs, int ch) : this(datos, fs, ch, 0) { }
    // 'trozo' > 0: nunca entrega mas de eso por lectura (aunque parta cuadros).
    public FuenteSP(float[] datos, int fs, int ch, int trozo) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); this.trozo = trozo; }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        if (trozo > 0 && n > trozo) n = trozo;
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class GraveMonoPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static Type TF, TMono;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, a, null); }
    static object Get(object o, string campo) { return o.GetType().GetField(campo, T).GetValue(o); }
    static object Inv(object o, string m, params object[] a) { return o.GetType().GetMethod(m, T).Invoke(o, a); }

    static ISampleProvider Mono(ISampleProvider s, float hz) { return (ISampleProvider)Nuevo(TMono, s, hz); }

    static float[] LeeTodo(ISampleProvider p, int[] bloques, Random azar)
    {
        List<float> r = new List<float>();
        float[] buf = new float[1 << 16];
        int k = 0;
        while (true)
        {
            int pide = azar != null ? 1 + azar.Next(bloques[0]) : bloques[k++ % bloques.Length];
            int n = p.Read(buf, 0, pide);
            if (n <= 0) break;
            for (int i = 0; i < n; i++) r.Add(buf[i]);
        }
        return r.ToArray();
    }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i] && !(float.IsNaN(a[i]) && float.IsNaN(b[i]))) return false;
        return true;
    }

    // Tono estereo: izquierdo = gl * sen, derecho = gr * sen.
    static float[] Tono(int fs, double hz, double seg, double gl, double gr)
    {
        int n = (int)(seg * fs); float[] x = new float[n * 2];
        for (int i = 0; i < n; i++) { double v = 0.5 * Math.Sin(2 * Math.PI * hz * i / fs); x[2 * i] = (float)(gl * v); x[2 * i + 1] = (float)(gr * v); }
        return x;
    }
    // Amplitud y fase de 'hz' en un canal, por proyeccion sobre seno y coseno (ultimos 'seg' segundos, periodos enteros).
    static void Mide(float[] y, int ch, int c, int fs, double hz, double seg, out double amp, out double fase)
    {
        int frames = y.Length / ch;
        int per = (int)Math.Round(seg * hz); if (per < 1) per = 1;
        int n = (int)Math.Round(per * fs / hz);
        if (n > frames / 2) n = frames / 2;
        int desde = frames - n;
        double s = 0, co = 0;
        for (int i = desde; i < frames; i++) { double w = 2 * Math.PI * hz * i / fs; s += y[i * ch + c] * Math.Sin(w); co += y[i * ch + c] * Math.Cos(w); }
        amp = 2 * Math.Sqrt(s * s + co * co) / n; fase = Math.Atan2(co, s);
    }
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-15)); }
    // (L - R) / 2 y (L + R) / 2 de una salida estereo, como senales de un canal.
    static float[] LadoDe(float[] y) { float[] r = new float[y.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = 0.5f * (y[2 * i] - y[2 * i + 1]); return r; }
    static float[] MedioDe(float[] y) { float[] r = new float[y.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = 0.5f * (y[2 * i] + y[2 * i + 1]); return r; }

    // El lado que tiene que quedar segun el cruce: |paso-alto Butterworth|^2 en analogico, con el corte ajustado
    // (la bilineal lleva cada frecuencia a tan(pi f / fs)).
    static double LadoTeorico(double f, double fc, int fs)
    {
        double w = Math.Tan(Math.PI * f / fs) / Math.Tan(Math.PI * fc / fs);
        double w2 = w * w;
        double hp = w2 / Math.Sqrt((1 - w2) * (1 - w2) + 2 * w2);
        return Db(hp * hp);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TMono = TF.GetNestedType("GraveMono", T);
        Console.WriteLine("\n  0) LA PIEZA\n");
        Check(TMono != null, "existe la etapa del grave en mono (GraveMono)");
        if (TMono == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return 1; }
        float fab = (float)TMono.GetField("HzDeFabrica", T).GetValue(null);
        Check(fab == 120f, "de fabrica corta en 120 Hz (" + F(fab, "0") + ")");

        LaRespuesta();
        LoQueNoSeToca();
        LasLecturas();
        ElDiscoContinuo();
        LaCadena();
        LasRampas();
        ElCoste();

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }

    // ================================================================================================
    static void LaRespuesta()
    {
        Console.WriteLine("\n  1) LA RESPUESTA, CON TONOS POR LA ETAPA DE VERDAD\n");
        foreach (int fs in new int[] { 44100, 48000 })
        {
            double[] fr = { 30, 50, 60, 80, 100, 120, 150, 200, 300, 1000, 5000, 15000 };
            double peorLado = 0, peorMedio = 0, peorPar = 0;
            string filaLado = "", filaAbierto = "";
            double abierto60 = 0, abierto1k = 0, abierto200 = 0, lado120 = 0, lado50 = 0, lado300 = 0;
            foreach (double hz in fr)
            {
                double seg = Math.Max(1.0, 40.0 / hz);
                // Solo lado: L = +s, R = -s. Lo que queda del lado es (L - R) / 2 a la salida.
                float[] y = LadoDe(LeeTodo(Mono(new FuenteSP(Tono(fs, hz, seg + 0.5, 1, -1), fs, 2), 120f), new int[] { 4096 }, null));
                double aL, fL; Mide(y, 1, 0, fs, hz, seg * 0.5, out aL, out fL);
                double lado = Db(aL / 0.5), teo = LadoTeorico(hz, 120, fs);
                if (hz <= 300) peorLado = Math.Max(peorLado, Math.Abs(lado - teo));
                filaLado += F(hz, "0") + ":" + F(lado, "0.0") + " ";
                if (hz == 120) lado120 = lado; if (hz == 50) lado50 = lado; if (hz == 300) lado300 = lado;
                // Solo medio: L = R = s. Tiene que salir igual de fuerte, y los dos canales identicos.
                float[] ym = LeeTodo(Mono(new FuenteSP(Tono(fs, hz, seg + 0.5, 1, 1), fs, 2), 120f), new int[] { 4096 }, null);
                double mL, fmL; Mide(ym, 2, 0, fs, hz, seg * 0.5, out mL, out fmL);
                peorMedio = Math.Max(peorMedio, Math.Abs(Db(mL / 0.5)));
                for (int i = 0; i < ym.Length; i += 2) peorPar = Math.Max(peorPar, Math.Abs(ym[i] - ym[i + 1]));
                // Abierto a un lado: L = s, R = 0.
                float[] ya = LeeTodo(Mono(new FuenteSP(Tono(fs, hz, seg + 0.5, 1, 0), fs, 2), 120f), new int[] { 4096 }, null);
                double bL, fbL, bR, fbR; Mide(ya, 2, 0, fs, hz, seg * 0.5, out bL, out fbL); Mide(ya, 2, 1, fs, hz, seg * 0.5, out bR, out fbR);
                double rel = Db(bR / bL);
                filaAbierto += F(hz, "0") + ":" + F(rel, "0.0") + " ";
                if (hz == 60) abierto60 = rel; if (hz == 1000) abierto1k = rel; if (hz == 200) abierto200 = rel;
            }
            string n = fs + " Hz: ";
            Info(n + "lo que queda del lado (dB): " + filaLado);
            Info(n + "abierto a la izquierda, lo que sale por la derecha (dB): " + filaAbierto);
            Check(Math.Abs(lado120 + 6.02) < 0.1 && lado50 < -29 && lado300 > -0.3,
                  n + "el lado cae como el cruce de cuarto orden: " + F(lado120, "0.00") + " dB en el corte (teoria -6,02), " + F(lado50, "0.0") + " en 50 Hz, " + F(lado300, "0.00") + " en 300 Hz");
            Check(peorLado < 0.15, n + "y de 30 a 300 Hz se aparta de la teoria " + F(peorLado, "0.000") + " dB como mucho");
            Check(peorMedio < 0.01 && peorPar == 0,
                  n + "lo mono sale igual de fuerte en todas las frecuencias (se aparta " + F(peorMedio, "0.0000") + " dB) y los dos canales salen identicos, bit a bit");
            Check(abierto60 > -1.5 && abierto200 < -20 && abierto1k < -60,
                  n + "un instrumento abierto a la izquierda: en 60 Hz ya suena casi igual por la derecha (" + F(abierto60, "0.0") + " dB), en 200 Hz sigue a su lado (" + F(abierto200, "0") + " dB) y en 1 kHz no se mueve (" + F(abierto1k, "0") + " dB)");
        }
        UnCanalAlReves();
        // El retraso del grave (paso-todo): se informa, no se exige. Es el de un cruce de dos vias.
        {
            int fs = 44100;
            float[] ya = LeeTodo(Mono(new FuenteSP(Tono(fs, 50, 3, 1, 1), fs, 2), 120f), new int[] { 4096 }, null);
            float[] yb = LeeTodo(Mono(new FuenteSP(Tono(fs, 51, 3, 1, 1), fs, 2), 120f), new int[] { 4096 }, null);
            double a1, p1, a2, p2; Mide(ya, 2, 0, fs, 50, 1.0, out a1, out p1); Mide(yb, 2, 0, fs, 51, 1.0, out a2, out p2);
            double dp = p2 - p1; while (dp > Math.PI) dp -= 2 * Math.PI; while (dp < -Math.PI) dp += 2 * Math.PI;
            Info("el grave sale " + F(Math.Abs(dp) / (2 * Math.PI * 1.0) * 1000, "0.0") + " ms mas tarde que los agudos (paso-todo del cruce, medido en 50 Hz)");
        }
    }

    // Una grabacion con un canal al reves (L = x, R = -x): su grave iria entero al lado y el grave en mono lo borraria.
    // Se detecta y se devuelve en fase. En lo normal (bajo en el centro, abierto a un lado, distinto en cada canal, o
    // en el centro con un poco de contrafase encima) no se activa.
    static void UnCanalAlReves()
    {
        int fs = 44100; double seg = 3.0; int n = (int)(seg * fs);
        float[] x = new float[n * 2];
        for (int i = 0; i < n; i++)
        {
            double v = 0.5 * Math.Sin(2 * Math.PI * 50 * i / fs) + 0.2 * Math.Sin(2 * Math.PI * 1000 * i / fs);
            x[2 * i] = (float)v; x[2 * i + 1] = (float)(-v);
        }
        object e = Mono(new FuenteSP(x, fs, 2), 120f);
        List<float> sal = new List<float>(); float[] buf = new float[441 * 2]; int k; double llega = -1;
        while ((k = ((ISampleProvider)e).Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < k; i++) sal.Add(buf[i]);
            if (llega < 0 && (double)Get(e, "vuelta") >= 1.0) llega = sal.Count / 2.0 / fs;
        }
        float[] y = sal.ToArray();
        double gL, pL, gR, pR, aL, qL, aR, qR;
        Mide(y, 2, 0, fs, 50, 1.0, out gL, out pL); Mide(y, 2, 1, fs, 50, 1.0, out gR, out pR);
        Mide(y, 2, 0, fs, 1000, 1.0, out aL, out qL); Mide(y, 2, 1, fs, 1000, 1.0, out aR, out qR);
        double dfase = Math.Abs(pL - pR); if (dfase > Math.PI) dfase = 2 * Math.PI - dfase;
        double dfase1k = Math.Abs(qL - qR); if (dfase1k > Math.PI) dfase1k = 2 * Math.PI - dfase1k;
        Info("un canal al reves: el grave de 50 Hz sale a " + F(Db(gL / 0.5), "0.00") + " / " + F(Db(gR / 0.5), "0.00") + " dB por cada canal, desfasado " + F(dfase * 180 / Math.PI, "0.0")
             + " grados entre ellos; el tono de 1 kHz, a " + F(Db(aL / 0.2), "0.00") + " dB y a " + F(dfase1k * 180 / Math.PI, "0") + " grados (como venia); se corrige del todo a los " + F(llega, "0.00") + " s");
        // Sale el grave del canal izquierdo por los dos. Por el derecho, menos lo que el cruce aun deja pasar del lado
        // en esa frecuencia (en 50 Hz, -31 dB: medio dB menos).
        Check(Math.Abs(Db(gL / 0.5)) < 0.05 && Db(gR / 0.5) > -1.0 && Db(gR / 0.5) < 0.05 && dfase < 0.05,
              "un canal al reves no pierde el grave (el grave en mono lo borraria entero): sale por los dos canales y en fase, entero por el izquierdo y a " + F(Db(gR / 0.5), "0.0") + " dB por el derecho");
        Check(Math.Abs(Db(aL / 0.2)) < 0.05 && Math.Abs(dfase1k - Math.PI) < 0.01, "y por encima del corte todo sigue como venia");
        Check(llega > 0 && llega < 1.2, "se corrige en poco mas de medio segundo (" + F(llega, "0.00") + " s)");
        // Lo normal no lo activa: bajo en el centro, abierto a un lado, distinto en cada canal, y en el centro con un
        // poco de contrafase encima.
        string[] nombres = { "el bajo en el centro", "abierto a un lado", "distinto en cada canal", "en el centro con algo de contrafase encima" };
        bool nunca = true; string cuales = "";
        for (int caso = 0; caso < 4; caso++)
        {
            Random r = new Random(40 + caso); float[] z = new float[n * 2]; double a = 0, b = 0;
            for (int i = 0; i < n; i++)
            {
                a = 0.995 * a + 0.05 * (r.NextDouble() - 0.5); b = 0.995 * b + 0.05 * (r.NextDouble() - 0.5);
                double v = 0.4 * Math.Sin(2 * Math.PI * 55 * i / fs), w = 0.15 * Math.Sin(2 * Math.PI * 70 * i / fs);
                double zl, zr;
                if (caso == 0) { zl = v; zr = v; }
                else if (caso == 1) { zl = v; zr = 0; }
                else if (caso == 2) { zl = 4 * a; zr = 4 * b; }
                else { zl = v + w; zr = v - w; }
                z[2 * i] = (float)zl; z[2 * i + 1] = (float)zr;
            }
            object ez = Mono(new FuenteSP(z, fs, 2), 120f);
            float[] bz = new float[4096]; double maxV = 0;
            while (((ISampleProvider)ez).Read(bz, 0, bz.Length) > 0) maxV = Math.Max(maxV, (double)Get(ez, "vuelta"));
            if (maxV > 0) { nunca = false; cuales += nombres[caso] + " "; }
        }
        Check(nunca, "y en una grabacion normal no se activa nunca: " + (nunca ? "el bajo en el centro, abierto a un lado, distinto en cada canal, o en el centro con algo de contrafase" : "SE ACTIVO con " + cuales));
    }

    // ================================================================================================
    static void LoQueNoSeToca()
    {
        Console.WriteLine("\n  2) LO QUE NO SE TOCA\n");
        int fs = 44100;
        Random r = new Random(7);
        float[] uno = new float[fs]; for (int i = 0; i < uno.Length; i++) uno[i] = (float)(r.NextDouble() - 0.5);
        Check(Igual(LeeTodo(Mono(new FuenteSP(uno, fs, 1), 120f), new int[] { 4096 }, null), uno), "un archivo de un canal (ya es mono) sale bit a bit");
        float[] seis = new float[fs * 6]; for (int i = 0; i < seis.Length; i++) seis[i] = (float)(r.NextDouble() - 0.5);
        Check(Igual(LeeTodo(Mono(new FuenteSP(seis, fs, 6), 120f), new int[] { 4096 }, null), seis), "uno de seis canales, tambien");
        float[] dos = new float[fs * 2]; for (int i = 0; i < dos.Length; i++) dos[i] = (float)(r.NextDouble() - 0.5);
        Check(Igual(LeeTodo(Mono(new FuenteSP(dos, fs, 2), 0f), new int[] { 4096 }, null), dos), "con grave_mono=0, un estereo sale bit a bit");
        Check(Igual(LeeTodo(Mono(new FuenteSP(dos, 8000, 2), 2000f), new int[] { 4096 }, null), dos), "y con un corte sin sentido para la frecuencia (2 kHz a 8 kHz), tambien");
        float[] mudo = new float[fs * 2];
        float[] ym = LeeTodo(Mono(new FuenteSP(mudo, fs, 2), 120f), new int[] { 4096 }, null);
        bool cero = ym.Length == mudo.Length; foreach (float v in ym) if (v != 0f) cero = false;
        Check(cero, "el silencio sale silencio exacto");
        // Lo que ya es mono en un estereo: los dos canales salen iguales bit a bit (el lado es cero exacto).
        float[] dm = new float[fs * 2]; for (int i = 0; i < fs; i++) { float v = (float)(r.NextDouble() - 0.5); dm[2 * i] = v; dm[2 * i + 1] = v; }
        float[] ydm = LeeTodo(Mono(new FuenteSP(dm, fs, 2), 120f), new int[] { 4096 }, null);
        bool par = ydm.Length == dm.Length; for (int i = 0; i + 1 < ydm.Length; i += 2) if (ydm[i] != ydm[i + 1]) par = false;
        Check(par, "un estereo que ya es mono sale con los dos canales identicos, bit a bit");
    }

    // ================================================================================================
    // Musica de mentira con mucho grave: un bajo de 55 Hz (en el centro si 'derecho' = 1; abierto o en contrafase si
    // no), grave distinto en cada canal y algo de agudo.
    static float[] Ruido(int fs, double seg, int semilla) { return Ruido(fs, seg, semilla, -0.6); }
    static float[] Ruido(int fs, double seg, int semilla, double derecho)
    {
        Random r = new Random(semilla); int n = (int)(seg * fs); float[] x = new float[n * 2];
        double a = 0, b = 0;
        for (int i = 0; i < n; i++)
        {
            a = 0.995 * a + 0.05 * (r.NextDouble() - 0.5); b = 0.995 * b + 0.05 * (r.NextDouble() - 0.5);
            double v = 0.3 * Math.Sin(2 * Math.PI * 55 * i / fs);
            x[2 * i] = (float)(v + a + 0.1 * (r.NextDouble() - 0.5));
            x[2 * i + 1] = (float)(derecho * v + b + 0.1 * (r.NextDouble() - 0.5));
        }
        return x;
    }
    static void LasLecturas()
    {
        Console.WriteLine("\n  3) CUALQUIER FORMA DE LEER DA LO MISMO\n");
        int fs = 44100;
        float[] x = Ruido(fs, 2.0, 3);
        float[] y0 = LeeTodo(Mono(new FuenteSP(x, fs, 2), 120f), new int[] { 1 << 15 }, null);
        Check(y0.Length == x.Length, "sale lo mismo que entra: " + y0.Length + " muestras de " + x.Length);
        float[] y1 = LeeTodo(Mono(new FuenteSP(x, fs, 2), 120f), new int[] { 999 }, new Random(11));
        Check(Igual(y0, y1), "lecturas de tamanos al azar (de 1 a 999 muestras, pares e impares): lo mismo, bit a bit");
        float[] y2 = LeeTodo(Mono(new FuenteSP(x, fs, 2), 120f), new int[] { 1 }, null);
        Check(Igual(y0, y2), "de una muestra por vez: lo mismo, bit a bit");
        float[] y3 = LeeTodo(Mono(new FuenteSP(x, fs, 2, 333), 120f), new int[] { 4096 }, null);
        Check(Igual(y0, y3), "con un origen que entrega cuadros partidos (333 muestras por lectura): lo mismo, bit a bit");
        float[] y4 = LeeTodo(Mono(new FuenteSP(x, fs, 2, 7), 120f), new int[] { 999 }, new Random(12));
        Check(Igual(y0, y4), "las dos cosas a la vez (origen de 7 en 7, lecturas al azar): lo mismo, bit a bit");
        // Un archivo que acaba con medio cuadro: esa media muestra no se puede completar y se descarta.
        float[] impar = new float[x.Length + 1]; Array.Copy(x, impar, x.Length); impar[x.Length] = 0.5f;
        float[] y5 = LeeTodo(Mono(new FuenteSP(impar, fs, 2, 333), 120f), new int[] { 4096 }, null);
        Check(Igual(y0, y5), "un archivo que acaba con medio cuadro: sale todo lo entero y la media muestra suelta se descarta");
        // Y los cuadros siguen en su sitio: el izquierdo sigue siendo el izquierdo.
        float[] soloIzq = Tono(fs, 1000, 0.5, 1, 0);
        float[] ys = LeeTodo(Mono(new FuenteSP(soloIzq, fs, 2, 333), 120f), new int[] { 999 }, new Random(13));
        double izq = 0, der = 0; for (int i = fs / 4 * 2; i + 1 < ys.Length; i += 2) { izq += ys[i] * ys[i]; der += ys[i + 1] * ys[i + 1]; }   // pasado el arranque del tono
        Check(Db(Math.Sqrt(der / izq)) < -60, "leyendo a trozos los canales no se cruzan: un tono de 1 kHz a la izquierda sigue a la izquierda (" + F(Db(Math.Sqrt(der / izq)), "0") + " dB a la derecha)");
    }

    // ================================================================================================
    static void ElDiscoContinuo()
    {
        Console.WriteLine("\n  4) DISCO CONTINUO: LA PISTA SIGUIENTE SIGUE CON LOS FILTROS DE LA ANTERIOR\n");
        int fs = 44100;
        foreach (double derecho in new double[] { 1.0, -1.0 })   // el bajo en el centro, y con un canal al reves
        {
            string que = derecho > 0 ? "con el bajo en el centro" : "con un canal al reves (el grave ya devuelto en fase)";
            float[] a = Ruido(fs, 1.3, 21, derecho), b = Ruido(fs, 0.9, 22, derecho);
            float[] ab = new float[a.Length + b.Length]; Array.Copy(a, ab, a.Length); Array.Copy(b, 0, ab, a.Length, b.Length);
            float[] junta = LeeTodo(Mono(new FuenteSP(ab, fs, 2), 120f), new int[] { 4096 }, null);
            object ea = Mono(new FuenteSP(a, fs, 2), 120f), eb = Mono(new FuenteSP(b, fs, 2), 120f);
            float[] ya = LeeTodo((ISampleProvider)ea, new int[] { 4096 }, null);
            Inv(eb, "Sigue", ea);
            float[] yb = LeeTodo((ISampleProvider)eb, new int[] { 999 }, new Random(5));
            float[] seguidas = new float[ya.Length + yb.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yb, 0, seguidas, ya.Length, yb.Length);
            Check(Igual(junta, seguidas), que + ": la anterior entera y la siguiente siguiendo con sus filtros dan lo mismo, bit a bit, que las dos seguidas por una sola etapa");
            if (derecho < 0) continue;
            // Sin seguir: el golpe que se oiria en el cambio.
            object ec = Mono(new FuenteSP(b, fs, 2), 120f);
            float[] yc = LeeTodo((ISampleProvider)ec, new int[] { 4096 }, null);
            double golpe = 0; for (int i = 0; i < Math.Min(yc.Length, (int)(0.05 * fs) * 2); i++) golpe = Math.Max(golpe, Math.Abs(yc[i] - junta[a.Length + i]));
            double pico = 0; foreach (float v in b) pico = Math.Max(pico, Math.Abs(v));
            Info("empezando de cero, en los primeros 50 ms la siguiente se aparta hasta " + F(golpe, "0.000") + " de lo que tocaba (su pico es " + F(pico, "0.00") + "): ese es el golpe que se evita");
            Check(golpe > 0.02, "sin seguir, el cambio se aparta de verdad (la prueba de arriba mira algo que existe)");
            // Si la anterior no acabo (no deberia pasar), no se hereda nada: empieza de cero, como siempre.
            object ed = Mono(new FuenteSP(a, fs, 2), 120f), ee = Mono(new FuenteSP(b, fs, 2), 120f);
            float[] medio = new float[4096]; ((ISampleProvider)ed).Read(medio, 0, medio.Length);
            Inv(ee, "Sigue", ed);
            Check(Igual(LeeTodo((ISampleProvider)ee, new int[] { 4096 }, null), yc), "si la anterior aun no habia acabado, la siguiente empieza de cero (no hereda una memoria a medias)");
            // Con otro corte tampoco (la memoria de un filtro no vale para otro).
            object ef = Mono(new FuenteSP(a, fs, 2), 150f), eg = Mono(new FuenteSP(b, fs, 2), 120f);
            LeeTodo((ISampleProvider)ef, new int[] { 4096 }, null);
            Inv(eg, "Sigue", ef);
            Check(Igual(LeeTodo((ISampleProvider)eg, new int[] { 4096 }, null), yc), "ni si la anterior tenia otro corte");
        }
    }

    // ================================================================================================
    static void LaCadena()
    {
        Console.WriteLine("\n  5) EN LA CADENA DE CADA CANCION\n");
        MethodBase arma = TF.GetMethod("ArmaCadena", T);
        List<string> ll = Codigo.Llamadas(arma);
        int iTap = ll.IndexOf("LevelTap..ctor"), iMono = ll.IndexOf("GraveMono..ctor");   // (la compensacion de noche que iba en medio se retiro el 24-10-2026)
        int iBajo = ll.IndexOf("DeepBassProvider..ctor"), iLim = ll.IndexOf("LimitadorPicoReal..ctor");
        Check(iTap >= 0 && iMono > iTap && iBajo > iMono && iLim > iBajo && ll.IndexOf("CompensaNoche..ctor") < 0,
              "ArmaCadena la pone detras del nivelado, y delante del grave sintetico y del limitador");
        int lee, esc; Codigo.UsosDeCampo(arma, "graveMonoHz", out lee, out esc);
        Check(lee > 0, "con el corte de la configuracion (graveMonoHz)");
        // La cadena de verdad (ArmaCadena, con un WAV): cada etapa lee de la de antes, y el grave sale en mono.
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rocola_gravemono_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try
        {
            object f = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
            int fs = 44100;
            string wav = System.IO.Path.Combine(tmp, "izquierda.wav");
            using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2)))
            {
                float[] x = Tono(fs, 50, 3.0, 1, 0);   // un bajo de 50 Hz solo por la izquierda
                w.WriteSamples(x, 0, x.Length);
            }
            object t = Nuevo(TF.GetNestedType("Track", T)); t.GetType().GetField("Path", T).SetValue(t, wav);
            object lector = Nuevo(TF.GetNestedType("FileAudioSource", T), wav);
            TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
            object cad = TF.GetMethod("ArmaCadena", T).Invoke(f, new object[] { t, lector, false, 0.0 });
            object tap = Get(cad, "Tap"), mono = Get(cad, "Mono"), bajo = Get(cad, "Bajo");
            // Desde el 12-10-2026, entre el grave en mono y el grave sintetico va la etapa del local (SalaLocal.cs).
            FieldInfo fSala = cad.GetType().GetField("Sala", T);
            object sala = fSala != null ? fSala.GetValue(cad) : null;
            // Desde el 22-10-2026, entre el local y el grave sintetico va la presencia en sala ruidosa (PresenciaSala.cs).
            FieldInfo fPre = cad.GetType().GetField("Presencia", T);
            object presencia = fPre != null ? fPre.GetValue(cad) : null;
            Check(mono != null && object.ReferenceEquals(Get(mono, "src"), tap)
                  && object.ReferenceEquals(Get(bajo, "src"), presencia ?? sala ?? mono) && (presencia == null || object.ReferenceEquals(Get(presencia, "src"), sala ?? mono)) && (sala == null || object.ReferenceEquals(Get(sala, "src"), mono))
                  && (bool)TMono.GetProperty("Activo", T).GetValue(mono, null),
                  "en la cadena armada de verdad cada etapa lee de la de antes: nivelado, grave en mono, (el local), (la presencia), grave sintetico");
            float[] y = LeeTodo((ISampleProvider)Get(cad, "Salida"), new int[] { 4096 }, null);
            double aL, pL, aR, pR; Mide(y, 2, 0, fs, 50, 1.0, out aL, out pL); Mide(y, 2, 1, fs, 50, 1.0, out aR, out pR);
            Check(aR > 0.05 && Math.Abs(Db(aR / aL)) < 1.5, "y un bajo de 50 Hz que viene solo por la izquierda sale por las dos bocinas (" + F(Db(aR / aL), "0.0") + " dB por la derecha)");
            ((IDisposable)lector).Dispose();
        }
        finally { try { System.IO.Directory.Delete(tmp, true); } catch { } }
        // La configuracion: se lee, se guarda y se acota.
        List<string> tLee = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), tEsc = Codigo.Textos(TF.GetMethod("TextoConfig", T));
        Check(tLee.Contains("grave_mono=") && tEsc.Contains("grave_mono="), "grave_mono= se lee y se guarda en la configuracion");
        MethodInfo hz = TF.GetMethod("GraveMonoHz", T);
        FieldInfo campo = TF.GetField("graveMonoHz", T);
        object antes = campo.GetValue(null);
        Func<string, float> P = delegate(string s) { return (float)hz.Invoke(null, new object[] { s }); };
        Check(P("0") == 0f && P("-5") == 0f && P("150") == 150f && P("80.5") == 80.5f && P("10") == 40f && P("900") == 250f,
              "0 o menos la apaga; 150 y 80,5 valen tal cual; 10 sube a 40 y 900 baja a 250");
        campo.SetValue(null, 133f);
        Check(P("abc") == 133f && P("NaN") == 133f && P("") == 133f, "un texto sin sentido deja lo que habia");
        campo.SetValue(null, antes);
        // Leer no reserva memoria (se reserva al construir).
        bool reserva = false;
        foreach (string m in new string[] { "Read", "Enteros", "Cuadros", "Hereda", "Sigue" })
            foreach (Codigo.Paso p in Codigo.Pasos(TMono.GetMethod(m, T))) if (p.Op == 0x8D || p.Op == 0x73) reserva = true;
        Check(!reserva, "leer no reserva memoria (ni arreglos ni objetos)");
    }

    // ================================================================================================
    //  6b) SIN CHASQUIDO AL TOCAR EL MANDO (P6, 23-10-2026)
    // ================================================================================================
    // Una pasada de 4 s por la etapa; en el cuadro 'cambio' se llama a Ajusta(ancho1, sub1) (null = no se llama). Las lecturas se parten
    // justo en el cambio, como pasa cuando la ventana toca el mando entre dos lecturas del hilo del sonido.
    static float[] ConCambio(float[] x, int fs, float corte, bool ancho0, bool sub0, bool? ancho1, bool? sub1, int cambio)
    {
        ISampleProvider g = (ISampleProvider)Nuevo(TMono, new FuenteSP(x, fs, 2), corte, 0.6f, 30f, ancho0, sub0);
        MethodInfo aj = TMono.GetMethod("Ajusta", T);
        float[] y = new float[x.Length]; int pos = 0; float[] b = new float[4096]; bool hecho = ancho1 == null && sub1 == null;
        while (pos < y.Length)
        {
            int quiero = Math.Min(b.Length, y.Length - pos);
            if (!hecho) quiero = Math.Min(quiero, 2 * cambio - pos);
            if (quiero <= 0 && !hecho) { hecho = true; aj.Invoke(g, new object[] { ancho1 ?? ancho0, sub1 ?? sub0 }); continue; }
            int k = g.Read(b, 0, quiero); if (k <= 0) break;
            Array.Copy(b, 0, y, pos, k); pos += k;
        }
        return y;
    }
    static double SaltoEn(float[] y, int desde, int hasta)
    {
        double m = 0; for (int i = 2 * desde + 2; i < 2 * hasta && i < y.Length; i++) m = Math.Max(m, Math.Abs(y[i] - y[i - 2])); return m;
    }
    static void LasRampas()
    {
        Console.WriteLine("\n  6b) SIN CHASQUIDO AL ENCENDER O APAGAR EN VIVO (P6)\n");
        int fs = 44100, n = fs * 4; float[] x = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs, inst = 0.3 * Math.Sin(2 * Math.PI * 440 * t) + 0.1 * Math.Sin(2 * Math.PI * 1500 * t), centro = 0.15 * Math.Sin(2 * Math.PI * 110 * t) + 0.12 * Math.Sin(2 * Math.PI * 22 * t);
            x[2 * i] = (float)(inst + centro); x[2 * i + 1] = (float)centro;   // un instrumento todo a la izquierda y graves al centro
        }
        string[] casos = { "estereo estrecho: encender", "estereo estrecho: apagar", "subgrave: encender", "subgrave: apagar" };
        double[] peor = new double[4], normal = new double[4];
        for (int c = 0; c < 4; c++)
            for (int off = 0; off < 60; off++)
            {
                int cambio = fs + off * 37;
                bool a0 = c == 1, s0 = c == 3;
                bool? a1 = c < 2 ? (bool?)(c == 0) : null, s1 = c >= 2 ? (bool?)(c == 2) : null;
                float[] y = ConCambio(x, fs, 120f, a0, s0, a1, s1, cambio);
                normal[c] = Math.Max(normal[c], SaltoEn(y, cambio - 4000, cambio - 50));
                peor[c] = Math.Max(peor[c], SaltoEn(y, cambio - 1, cambio + 6));
            }
        for (int c = 0; c < 4; c++)
        {
            Info(casos[c] + ": salto normal " + F(normal[c], "0.0000") + ", peor salto al cambiar " + F(peor[c], "0.0000") + " (" + F(20 * Math.Log10(Math.Max(1e-9, peor[c] / normal[c])), "+0.0;-0.0") + " dB sobre lo normal)");
            Check(peor[c] <= normal[c] * 1.25, casos[c] + " en vivo: el salto entre muestras no pasa de 2 dB sobre el normal (antes: 6 a 16 dB)");
        }
        // Lo mismo sin corte del grave en mono (grave_mono=0): el estereo tambien va en rampa aunque arranque apagado.
        for (int c = 0; c < 2; c++)
        {
            double pe = 0, no = 0;
            for (int off = 0; off < 60; off++)
            {
                int cambio = fs + off * 37;
                float[] y = ConCambio(x, fs, 0f, c == 1, false, c == 0, null, cambio);
                no = Math.Max(no, SaltoEn(y, cambio - 4000, cambio - 50)); pe = Math.Max(pe, SaltoEn(y, cambio - 1, cambio + 6));
            }
            Info("sin corte de grave, " + (c == 0 ? "encender" : "apagar") + " el estereo: salto normal " + F(no, "0.0000") + ", peor al cambiar " + F(pe, "0.0000"));
            Check(pe <= no * 1.25, "sin corte del grave en mono (grave_mono=0), " + (c == 0 ? "encender" : "apagar") + " el estereo en vivo tampoco da chasquido");
        }
        // Despues de los 25 ms todo queda exactamente como si el mando hubiera estado asi desde el principio.
        int cam = fs + 1234, ya = cam + fs / 10;   // 100 ms despues
        float[] eOn = ConCambio(x, fs, 120f, true, false, null, null, 0), eOff = ConCambio(x, fs, 120f, false, false, null, null, 0);
        float[] aOn = ConCambio(x, fs, 120f, false, false, true, null, cam), aOff = ConCambio(x, fs, 120f, true, false, false, null, cam);
        bool igual1 = true, igual2 = true;
        for (int i = 2 * ya; i < x.Length; i++) { if (aOn[i] != eOn[i]) igual1 = false; if (aOff[i] != eOff[i]) igual2 = false; }
        Check(igual1 && igual2, "pasados 100 ms del cambio, el estereo sale bit a bit como con el mando puesto desde el principio (los dos sentidos)");
        float[] sOff = ConCambio(x, fs, 0f, false, true, null, false, cam);
        bool igual3 = true; for (int i = 2 * ya; i < x.Length; i++) if (sOff[i] != x[i]) igual3 = false;
        Check(igual3, "pasados 100 ms de apagar el subgrave (sin corte de grave en mono), la salida es exactamente lo que entra");
        float[] sOn = ConCambio(x, fs, 0f, false, false, null, true, cam), sRef = ConCambio(x, fs, 0f, false, true, null, null, 0);
        double d = 0, r = 0; for (int i = 2 * (fs * 3); i < x.Length; i++) { d = Math.Max(d, Math.Abs(sOn[i] - sRef[i])); r = Math.Max(r, Math.Abs(sRef[i])); }
        Check(d < 1e-3, "y al encenderlo, un segundo despues alcanza lo mismo que con el subgrave puesto desde el principio (diferencia maxima " + F(d, "0.000000") + ")");
        // En cualquier tamano de lectura y con cambios repetidos no sale nada raro.
        float[] mucho = ConCambio(x, fs, 120f, false, false, true, true, cam);
        bool sano = true; foreach (float v in mucho) if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2f) sano = false;
        Check(sano, "encender los dos a la vez no saca nada roto");
        // Estado estable: el camino de siempre. Con los mandos quietos (apagados, o encendidos desde el principio) la salida no cambia de
        // un ejecutable a otro: lo comprueba pruebas/compara-cadena.cs; aqui se mira que no haya reservas de memoria al leer.
        ISampleProvider q = (ISampleProvider)Nuevo(TMono, new FuenteSP(x, fs, 2), 120f, 0.6f, 30f, true, true);
        float[] bb = new float[4096]; q.Read(bb, 0, bb.Length);
        long m0 = GC.GetTotalMemory(false); for (int k = 0; k < 20; k++) q.Read(bb, 0, bb.Length); long m1 = GC.GetTotalMemory(false);
        Check(m1 - m0 < 20000, "leer con los mandos quietos no reserva memoria (" + (m1 - m0) + " bytes en 20 lecturas)");
    }

    // ================================================================================================
    static void ElCoste()
    {
        Console.WriteLine("\n  6) LO QUE CUESTA\n");
        int fs = 44100;
        float[] x = Ruido(fs, 20.0, 9);
        double mejor = double.MaxValue, base0 = double.MaxValue;
        float[] buf = new float[2205 * 2];
        for (int k = 0; k < 5; k++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            ISampleProvider s0 = new FuenteSP(x, fs, 2); while (s0.Read(buf, 0, buf.Length) > 0) { }
            base0 = Math.Min(base0, sw.Elapsed.TotalMilliseconds);
            sw = Stopwatch.StartNew();
            ISampleProvider s1 = Mono(new FuenteSP(x, fs, 2), 120f); while (s1.Read(buf, 0, buf.Length) > 0) { }
            mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds);
        }
        double ns = (mejor - base0) * 1e6 / (x.Length / 2);
        Info("un segundo de musica estereo cuesta " + F((mejor - base0) / 20.0, "0.00") + " ms (" + F(ns, "0.0") + " ns por cuadro; Mono, esta maquina, no es un procesador antiguo)");
        Check(ns < 200, "menos de 200 ns por cuadro (un bloque de 50 ms, menos de medio milisegundo)");
    }
}

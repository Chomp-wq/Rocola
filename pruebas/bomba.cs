using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
// LA MUSICA EN SU PROPIO HILO (BombaSonido.cs).
//
// La salida clasica pide la musica desde el bucle de la pantalla; ahora la cadena la lee un hilo propio que deja un
// colchon corto hecho, y la pantalla solo copia. Aqui se mira:
//   1) que sale EXACTAMENTE lo mismo, en el mismo orden, pidiendo trozos de cualquier tamano y a traves de la vista
//      flotante que NAudio pone sobre su byte[]; y que el final llega como antes (0 al acabarse);
//   2) que la cadena la lee OTRO hilo, nunca el que pide (la pantalla), y con prioridad por encima de ella;
//   3) que un fallo de la cadena le llega a la salida como antes, despues de lo que ya estaba hecho;
//   4) que si el hilo se atrasa, la pantalla espera un rato acotado y despues entrega silencio -nunca 0, que pararia
//      la salida- sin volver a esperar, y que al volver la musica no se pierde ni una muestra;
//   5) que al pararla, el hilo se va y ya nadie lee la cadena;
//   6) lo que le cuesta a la pantalla cada bloque de 50 ms, con y sin bomba;
//   7) la cadena de verdad (OpenAudio + ArmaCadena, todo encendido): por la bomba sale bit a bit lo mismo que leida a
//      mano; y por AbreSalidaClasica la salida recibe la bomba;
//   8) los enganches: la salida clasica la pone, al parar se para ANTES de soltar el lector, y el pulso, la aurora y
//      el relevo cuentan lo que la bomba lee por delante;
//   9) con musica de verdad por la medida (LevelTap), la bomba y el ritmo de WaveOut, el pulso le llega a la pantalla
//      en cada vuelta (el que desliza y hace latir las estrellas y la luna).
class Bomba
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TB;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void Set(object o, string n, object v)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(o, v); return; } }
        PropertyInfo p = o.GetType().GetProperty(n, T); if (p != null) p.SetValue(o, v, null);
    }
    static object Call(object o, string n, params object[] a)
    {
        try { return TF.GetMethod(n, T).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static MethodBase Met(Type t, string n) { return t.GetMethod(n, T); }

    // Una fuente de prueba: muestras que se pueden reconocer (la n vale n*1e-6), con su hilo apuntado, y con atrasos
    // o un fallo donde se le pida.
    class Fuente : ISampleProvider
    {
        readonly WaveFormat wf; readonly long total; long pos;
        public long FallaEn = -1; public long AtrasaEn = -1; public int AtrasoMs; public int CostoTrozoUs;
        public readonly List<int> Hilos = new List<int>(); public int Lecturas;
        public Fuente(long muestras, int fs, int ch) { total = muestras; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public static float Valor(long n) { return (float)((n % 1000003) * 1e-6); }
        public int Read(float[] b, int o, int c)
        {
            lock (Hilos) { Hilos.Add(Thread.CurrentThread.ManagedThreadId); Lecturas++; }
            if (AtrasaEn >= 0 && pos >= AtrasaEn) { AtrasaEn = -1; Thread.Sleep(AtrasoMs); }
            if (FallaEn >= 0 && pos >= FallaEn) throw new IOException("se cayo el disco");
            if (CostoTrozoUs > 0) { Stopwatch sw = Stopwatch.StartNew(); while (sw.Elapsed.TotalMilliseconds * 1000 < CostoTrozoUs) { } }
            int n = (int)Math.Min(c, total - pos); if (FallaEn >= 0) n = (int)Math.Min(n, FallaEn - pos);
            if (n <= 0) return 0;
            for (int i = 0; i < n; i++) b[o + i] = Valor(pos + i);
            pos += n; return n;
        }
    }

    static object Nueva(ISampleProvider s) { return Activator.CreateInstance(TB, T, null, new object[] { s }, null); }
    // Con tope: una bomba rota que nunca acaba (silencio sin fin) no debe dejar la bateria dando vueltas.
    static long lecturas;
    static int Lee(object b, float[] buf, int o, int c)
    {
        if (++lecturas > 3000000) throw new TimeoutException("la bomba no se acaba nunca");
        return ((ISampleProvider)b).Read(buf, o, c);
    }
    static void Suelta(object b) { ((IDisposable)b).Dispose(); }

    static void LoMismo()
    {
        Console.WriteLine("\n  1) SALE LO MISMO, EN EL MISMO ORDEN\n");
        int fs = 44100, ch = 2; long total = fs * ch * 3 + 777;   // 3 s y un pico
        Fuente src = new Fuente(total, fs, ch);
        object b = Nueva(src);
        Random r = new Random(5); float[] buf = new float[20000]; long vistos = 0; bool igual = true; int ceros = 0;
        while (true)
        {
            int pide = 1 + r.Next(9000), o = r.Next(50);
            int n = Lee(b, buf, o, pide);
            if (n == 0) { ceros++; break; }
            for (int i = 0; i < n; i++) if (buf[o + i] != Fuente.Valor(vistos + i)) igual = false;
            vistos += n;
        }
        int otra = Lee(b, buf, 0, 100);
        Check(igual && vistos == total && otra == 0, "pidiendo trozos de 1 a 9000 muestras sale todo, en orden y exacto (" + vistos + " muestras), y al acabarse devuelve 0, como antes");
        Suelta(b);

        // Por la vista flotante que NAudio pone sobre su byte[] (asi la usa la salida en coma flotante).
        Fuente s2 = new Fuente(fs * ch * 2, fs, ch);
        object b2 = Nueva(s2);
        SampleToWaveProvider w = new SampleToWaveProvider((ISampleProvider)b2);
        byte[] bytes = new byte[2205 * ch * 4]; long k = 0; bool igual2 = true;
        while (true)
        {
            int n = w.Read(bytes, 0, bytes.Length); if (n <= 0 || ++lecturas > 3000000) break;
            for (int i = 0; i + 4 <= n; i += 4) { if (BitConverter.ToSingle(bytes, i) != Fuente.Valor(k)) igual2 = false; k++; }
        }
        Check(igual2 && k == fs * ch * 2, "y a traves de la vista flotante de NAudio sobre su byte[] (bloques de 50 ms, como la salida): igual, muestra a muestra");
        Suelta(b2);
        // Tambien en mono, a 48 kHz y a 22 kHz: el colchon y el trozo se miden en cuadros.
        bool todos = true;
        int[][] formatos = { new int[] { 48000, 1 }, new int[] { 22050, 2 }, new int[] { 96000, 6 } };
        foreach (int[] fm in formatos)
        {
            Fuente s3 = new Fuente(fm[0] * fm[1] + 13, fm[0], fm[1]); object b3 = Nueva(s3); long v = 0; int n3;
            while ((n3 = Lee(b3, buf, 0, 3001)) > 0) { for (int i = 0; i < n3; i++) if (buf[i] != Fuente.Valor(v + i)) todos = false; v += n3; }
            if (v != fm[0] * fm[1] + 13) todos = false;
            int adel = (int)Get(b3, "AdelantoMs");
            if (adel < 150 || adel > 175) todos = false;
            Suelta(b3);
        }
        Check(todos, "en mono a 48 kHz, estereo a 22 kHz y 5.1 a 96 kHz, igual; y en todos lee unos 160 ms por delante (el colchon menos trozo y medio)");
    }

    static void OtroHilo()
    {
        Console.WriteLine("\n  2) LA CADENA LA LEE OTRO HILO\n");
        Fuente src = new Fuente(44100 * 2, 44100, 2);
        object b = Nueva(src); float[] buf = new float[4410];
        while (Lee(b, buf, 0, buf.Length) > 0) { }
        int yo = Thread.CurrentThread.ManagedThreadId; bool nuncaYo = src.Hilos.Count > 0; int uno = -1; bool unSolo = true;
        foreach (int h in src.Hilos) { if (h == yo) nuncaYo = false; if (uno < 0) uno = h; else if (h != uno) unSolo = false; }
        Check(nuncaYo && unSolo && (int)Get(b, "HiloLector") == uno, "quien pide (la pantalla) nunca lee la cadena: la lee siempre un mismo hilo aparte");
        Suelta(b);
        // Con la musica en marcha (el hilo vivo): su prioridad y que no retiene el cierre del programa.
        object b2 = Nueva(new Fuente(44100 * 2 * 60, 44100, 2)); Lee(b2, buf, 0, buf.Length);
        Thread h2 = (Thread)Get(b2, "hilo");
        Check(h2 != null && h2.IsAlive && h2.IsBackground && h2.Priority == ThreadPriority.AboveNormal, "y ese hilo va por delante de la pantalla (prioridad por encima de lo normal) y no retiene el cierre del programa");
        Suelta(b2);
    }

    static void Fallo()
    {
        Console.WriteLine("\n  3) UN FALLO DE LA CADENA\n");
        Fuente src = new Fuente(44100 * 2 * 2, 44100, 2); src.FallaEn = 30000;
        object b = Nueva(src); float[] buf = new float[4410]; long v = 0; Exception e = null;
        try { int n; while ((n = Lee(b, buf, 0, buf.Length)) > 0) v += n; } catch (Exception ex) { e = ex; }
        Check(v == 30000 && e != null && e.InnerException is IOException,
              "llega a la salida lo que ya estaba hecho (" + v + " muestras) y despues el fallo, con el de la cadena dentro (lo atiende la salida como antes)");
        Suelta(b);
    }

    static void Atrasada()
    {
        Console.WriteLine("\n  4) SI EL HILO SE ATRASA\n");
        int fs = 44100, ch = 2, bloque = 2205 * ch;
        // Un atraso corto (un disco lento): la pantalla lo espera y no sale silencio.
        Fuente corta = new Fuente(fs * ch * 2, fs, ch); corta.AtrasaEn = fs * ch / 2; corta.AtrasoMs = 150;
        object b = Nueva(corta); float[] buf = new float[bloque]; long v = 0; bool bien = true; int n;
        while ((n = Lee(b, buf, 0, bloque)) > 0) { for (int i = 0; i < n; i++) if (buf[i] != Fuente.Valor(v + i)) bien = false; v += n; }
        Check(bien && v == fs * ch * 2 && (long)Get(b, "Huecos") == 0, "un atraso de 150 ms (un disco que tarda): se espera y no sale ni una muestra de silencio");
        Suelta(b);
        // Uno largo: espera acotada, silencio en vez de 0, no vuelve a esperar, y al volver no se pierde nada.
        Fuente larga = new Fuente(fs * ch * 3, fs, ch); larga.AtrasaEn = fs * ch / 2; larga.AtrasoMs = 2000;
        object b2 = Nueva(larga); List<float> sale = new List<float>(); List<double> ms = new List<double>(); bool nuncaCero = true; int silencios = 0;
        while (true)
        {
            Stopwatch sw = Stopwatch.StartNew(); int m = Lee(b2, buf, 0, bloque); sw.Stop();
            if (m == 0) break;
            ms.Add(sw.Elapsed.TotalMilliseconds);
            bool todoCero = true; for (int i = 0; i < m; i++) if (buf[i] != 0f) todoCero = false;
            if (todoCero && sale.Count > 0) { silencios++; if (m != bloque) nuncaCero = false; continue; }
            for (int i = 0; i < m; i++) sale.Add(buf[i]);
            if (ms.Count > 2000000) break;   // por si acaso: nunca deberia llegar
        }
        double peor = 0, segunda = 0; int iPeor = -1;
        for (int i = 0; i < ms.Count; i++) if (ms[i] > peor) { peor = ms[i]; iPeor = i; }
        for (int i = 0; i < ms.Count; i++) if (i != iPeor && ms[i] > segunda) segunda = ms[i];
        // Lo que no es silencio, en orden: exactamente la musica entera (la muestra 0 vale 0 y se salta).
        List<float> musica = new List<float>(); foreach (float x0 in sale) if (x0 != 0f) musica.Add(x0);
        bool completa = musica.Count == fs * ch * 3 - 1; for (int i = 0; completa && i < musica.Count; i++) if (musica[i] != Fuente.Valor(i + 1)) completa = false;
        Info("atraso de 2 s: la espera mas larga " + F(peor, "0") + " ms, la siguiente " + F(segunda, "0.0") + " ms; " + silencios + " bloques de silencio");
        Check(peor >= 500 && peor < 900 && segunda < 60, "la pantalla espera como mucho unos 600 ms (la salida aun tiene casi 1 s en su colchon) y despues no vuelve a esperar");
        Check(nuncaCero && silencios > 0 && (long)Get(b2, "Huecos") == 1, "mientras tanto entrega bloques de silencio enteros, nunca 0 (la salida lo tomaria por el final de la cancion)");
        Check(completa, "y al volver la musica sigue donde iba: no se pierde ni se repite ninguna muestra");
        Suelta(b2);
    }

    static void Parar()
    {
        Console.WriteLine("\n  5) AL PARARLA\n");
        Fuente src = new Fuente(44100 * 2 * 60, 44100, 2);
        object b = Nueva(src); float[] buf = new float[4410];
        for (int i = 0; i < 5; i++) Lee(b, buf, 0, buf.Length);
        Thread.Sleep(50);
        Stopwatch sw = Stopwatch.StartNew(); Suelta(b); sw.Stop();
        int antes = src.Lecturas; Thread.Sleep(150);
        Check(!(bool)Get(b, "Viva") && src.Lecturas == antes && sw.ElapsedMilliseconds < 500,
              "el hilo se va en " + sw.ElapsedMilliseconds + " ms y nadie vuelve a leer la cadena (el lector ya se puede soltar)");
        Stopwatch sw2 = Stopwatch.StartNew(); Lee(b, buf, 0, buf.Length); sw2.Stop();
        Check(sw2.ElapsedMilliseconds < 50, "y si la salida aun pide algo despues, no se queda esperando");
        // Parada con la cadena ocupada: no espera en la pantalla; el hilo sale tras terminar su ultimo Read.
        Fuente lenta = new Fuente(44100 * 2 * 60, 44100, 2);
        object b2 = Nueva(lenta); Lee(b2, buf, 0, buf.Length);
        lenta.AtrasaEn = 0; lenta.AtrasoMs = 300; Thread.Sleep(30);
        Stopwatch sw3 = Stopwatch.StartNew(); Suelta(b2); sw3.Stop(); int l2 = lenta.Lecturas; Thread.Sleep(400);
        Check(!(bool)Get(b2, "Viva") && lenta.Lecturas == l2 && sw3.ElapsedMilliseconds < 1100,
              "con una lectura lenta a medias, se cancela (" + sw3.ElapsedMilliseconds + " ms) y despues el hilo ya no lee mas");
    }

    static void Coste()
    {
        Console.WriteLine("\n  6) LO QUE LE CUESTA A LA PANTALLA\n");
        // Una cadena que tarda 2 ms por cada 25 ms de musica (lo medido con todo encendido en un procesador de servidor).
        int fs = 44100, ch = 2, bloque = 2205 * ch;
        float[] buf = new float[bloque];
        Fuente directa = new Fuente(fs * ch * 10, fs, ch); directa.CostoTrozoUs = 2000;
        double sinB = 0; for (int i = 0; i < 20; i++) { Stopwatch sw = Stopwatch.StartNew(); directa.Read(buf, 0, bloque); directa.Read(buf, 0, 1); sw.Stop(); sinB += sw.Elapsed.TotalMilliseconds; }
        sinB /= 20;
        Fuente conHilo = new Fuente(fs * ch * 10, fs, ch); conHilo.CostoTrozoUs = 2000;
        object b = Nueva(conHilo); List<double> ms = new List<double>();
        Lee(b, buf, 0, bloque); Thread.Sleep(300);
        for (int i = 0; i < 40; i++)   // como la salida: un bloque cada 50 ms
        {
            Stopwatch sw = Stopwatch.StartNew(); Lee(b, buf, 0, bloque); sw.Stop(); ms.Add(sw.Elapsed.TotalMilliseconds);
            Thread.Sleep(50);
        }
        Suelta(b);
        double media = 0, peor = 0; foreach (double v in ms) { media += v; peor = Math.Max(peor, v); } media /= ms.Count;
        Info("cada bloque de 50 ms en la pantalla: sin bomba " + F(sinB, "0.00") + " ms; con bomba " + F(media, "0.000") + " ms de media y " + F(peor, "0.00") + " ms el peor");
        Check(media < 0.5 && media * 8 < sinB, "con la bomba la pantalla solo copia: cada bloque le cuesta menos de medio milisegundo");
    }

    // La cadena de verdad, como en 'todojunto'.
    static object f; static string wav; static object track, item;
    static void Prepara(string dir)
    {
        f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        int fs = 44100, n = fs * 12; float[] x = new float[2 * n]; Random r = new Random(9);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs, kick = 0.5 * Math.Exp(-(t % 0.5) / 0.1) * Math.Sin(2 * Math.PI * 60 * t);
            double hat = 0.15 * (r.NextDouble() * 2 - 1) * Math.Exp(-(t % 0.25) / 0.03);
            x[2 * i] = (float)(kick + hat + 0.1 * Math.Sin(2 * Math.PI * 440 * t)); x[2 * i + 1] = (float)(kick + hat * 0.7 + 0.1 * Math.Sin(2 * Math.PI * 660 * t));
        }
        wav = Path.Combine(dir, "bomba.wav");
        using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
        object[] a = { wav, null };
        Call(f, "MideIntegrada", a);
        IDictionary tab = (IDictionary)Get(f, "integCache");
        if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); Set(f, "integCache", tab); }
        tab[wav] = a[1];
        Type tt = TF.GetNestedType("Track", T); track = Activator.CreateInstance(tt, true); Set(track, "Path", wav);
        Type tp = TF.GetField("nowPlaying", T).FieldType; item = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp); Set(item, "Song", track);
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 3f);
        Set(f, "excAmount", 0.35f); Set(f, "bassAuto", false); Set(f, "bassAmount", 0.3f); Set(f, "bassSongFactor", 1f);
        Set(f, "sbrAllowed", true); Set(f, "sbrOn", true); Set(f, "declipOn", true);
        Set(f, "nocheArrancada", true); Set(f, "nocheFactor", 0.8f); Set(f, "nocheCompensa", 1f); Set(f, "crossfadeSec", 0.5f);
    }
    static ISampleProvider Cadena()
    {
        object lector = Call(f, "OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        Set(f, "nowPlaying", item);
        return (ISampleProvider)Get(cad, "Salida");
    }
    static List<float> Todo(ISampleProvider s, int bloque)
    {
        List<float> y = new List<float>(); float[] b = new float[bloque]; int n, vueltas = 0;
        while ((n = s.Read(b, 0, bloque)) > 0)
        {
            if (++vueltas > 200000) throw new TimeoutException("la cadena no se acaba nunca");
            for (int i = 0; i < n; i++) y.Add(b[i]);
        }
        return y;
    }

    static void DeVerdad(string dir)
    {
        Console.WriteLine("\n  7) LA CADENA DE VERDAD, TODO ENCENDIDO\n");
        Prepara(dir);
        List<float> directa = Todo(Cadena(), 4410);
        object b = Call(f, "ConBomba", Cadena());
        List<float> porBomba = Todo((ISampleProvider)b, 4410);
        bool igual = directa.Count == porBomba.Count && directa.Count > 44100;
        for (int i = 0; igual && i < directa.Count; i++) if (directa[i] != porBomba[i]) igual = false;
        Check(igual, "por la bomba sale bit a bit lo mismo que leyendo la cadena a mano (" + directa.Count + " muestras: nivelado, realce, DSEE, grave, noche, limitador y fundido)");
        Check(object.ReferenceEquals(Get(f, "bomba"), b) && (int)Call(f, "AdelantoBomba") == (int)Get(b, "AdelantoMs") && (int)Call(f, "AdelantoBomba") > 0,
              "la rocola se queda con ella y sabe cuanto lee por delante (" + Call(f, "AdelantoBomba") + " ms)");
        int sinB = (int)Get(f, TF.GetField("audioVisualDelayMs", T) != null ? "audioVisualDelayMs" : "AudioVisualDelayMs");
        Check((int)Call(f, "RetrasoAudible") == sinB + (int)Get(b, "AdelantoMs"), "lo que se oye va el colchon de la salida (" + sinB + " ms) mas lo que la bomba lee por delante");
        Call(f, "SueltaBomba");
        Check(Get(f, "bomba") == null && !(bool)Get(b, "Viva") && (int)Call(f, "AdelantoBomba") == 0, "al soltarla el hilo se va y ya no se suma nada al retraso");
        // Por la salida clasica, con una salida de mentira: recibe la bomba y lo que sale es lo mismo.
        List<IWaveProvider> recibio = new List<IWaveProvider>();
        Set(f, "fabricaSalidaClasica", (Func<IWavePlayer>)delegate { return new Falsa(recibio); });
        try { Call(f, "AbreSalidaClasica", Cadena(), true); }
        finally { Set(f, "fabricaSalidaClasica", null); }
        object puesta = Get(f, "bomba");
        bool exacto = recibio.Count == 1 && puesta != null;
        if (exacto)
        {
            byte[] by = new byte[2205 * 2 * 4]; List<float> y = new List<float>(); int n;
            int vueltas = 0;
            while ((n = recibio[0].Read(by, 0, by.Length)) > 0 && ++vueltas < 200000) for (int i = 0; i + 4 <= n; i += 4) y.Add(BitConverter.ToSingle(by, i));
            exacto = y.Count == directa.Count; for (int i = 0; exacto && i < y.Count; i++) if (y[i] != directa[i]) exacto = false;
        }
        Check(exacto, "abriendo la salida clasica (coma flotante), la salida lee de la bomba y le llega lo mismo, bit a bit");
        Call(f, "SueltaBomba");
    }
    class Falsa : IWavePlayer
    {
        readonly List<IWaveProvider> l; public Falsa(List<IWaveProvider> x) { l = x; }
        public void Init(IWaveProvider p) { l.Add(p); }
        public void Play() { } public void Stop() { } public void Pause() { }
        public float Volume { get { return 1f; } set { } }
        public PlaybackState PlaybackState { get { return PlaybackState.Stopped; } }
        public event EventHandler<StoppedEventArgs> PlaybackStopped { add { } remove { } }
        public void Dispose() { }
    }

    static void Enganches()
    {
        Console.WriteLine("\n  8) LOS ENGANCHES\n");
        Check(Codigo.Llama(Met(TF, "AbreSalidaClasica"), "ConBomba"), "la salida clasica (WaveOut, la de los equipos de dos nucleos) lee de la bomba");
        List<string> para = Codigo.Nombres(Met(TF, "DisposePlaybackNow"));
        int iInvalida = para.IndexOf("InvalidaPublicacionAudio"), iStop = para.LastIndexOf("Stop"), iFin = para.IndexOf("FinalizeLoudMeasure"), iLector = para.IndexOf("SueltaLectoresAudio"); // Stop del audio, no el del temporizador
        Check(iInvalida >= 0 && iStop > iInvalida && iFin > iStop && iLector > iFin,
              "al parar se invalida la publicacion, se detiene la salida, se cierra lo medido y los lectores pasan al cierre seguro");
        Check(Codigo.Llama(Met(TF, "RetrasoAudible"), "AdelantoBomba") && Codigo.Llama(Met(TF, "ConsumeLevels"), "RetrasoAudible"),
              "el pulso y la aurora (ConsumeLevels) y el relevo sin pausa (RetrasoAudible) suman lo que la bomba lee por delante");
        int crea = 0; string donde = "";
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            foreach (string s in Codigo.Llamadas(m)) if (s.EndsWith("BombaSonido..ctor")) { crea++; donde = m.Name; }
        Check(crea == 1 && donde == "ConBomba", "la bomba se crea en un solo sitio (" + donde + "), que suelta la anterior");
        Check(Codigo.Llama(Met(TF, "ConBomba"), "SueltaBomba"), "y abrir la salida otra vez nunca deja un hilo de musica viejo corriendo");
    }

    // Tres tonos (grave, medio y agudo), para que las tres bandas de la medida tengan algo.
    class Tonos : ISampleProvider
    {
        readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); long n;
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c)
        {
            for (int i = 0; i < c; i += 2, n++)
            {
                double t = n / 44100.0;
                float v = (float)(0.2 * Math.Sin(2 * Math.PI * 80 * t) + 0.15 * Math.Sin(2 * Math.PI * 1000 * t) + 0.1 * Math.Sin(2 * Math.PI * 6000 * t));
                b[o + i] = v; if (i + 1 < c) b[o + i + 1] = v;
            }
            return c;
        }
    }

    // EL PULSO LLEGA A LA PANTALLA. La medida (LevelTap) apunta cada trozo con la hora en que se leyo, y la pantalla lo
    // toma cuando ya se oye (ConsumeLevels: el colchon de la salida mas lo que la bomba lee por delante). La cola de en
    // medio tiene tope: si guarda menos tiempo del que hay que esperar, lo mas viejo se tira antes de oirse, a la
    // pantalla no le llega nada y las estrellas ni se deslizan ni laten. Con la bomba los trozos son de 25 ms (antes, los
    // bloques de 50 ms de WaveOut): con el tope de antes, 96 medidas eran 0,6 s y hay que esperar mas de 1 s.
    static void ElPulso()
    {
        Console.WriteLine("\n  9) EL PULSO LLEGA A LA PANTALLA (ESTRELLAS Y LUNA)\n");
        object f = Codigo.FormSinEncender(TF);
        Set(f, "audioVisualDelayMs", 850);   // lo que pone la salida clasica (WaveOut, 20 bloques de 50 ms)
        object tap = Activator.CreateInstance(TF.GetNestedType("LevelTap", T), T, null, new object[] { new Tonos(), f }, null);
        ISampleProvider salida = (ISampleProvider)Call(f, "ConBomba", tap);
        float[] bloque = new float[44100 * 2 / 20];   // 50 ms, como cada bloque de WaveOut
        int inicio = Environment.TickCount, pedidos = 0, vueltas = 0, conPulso = 0, primero = -1;
        try
        {
            while (Environment.TickCount - inicio < 3500)
            {
                int ya = Environment.TickCount - inicio;
                while (pedidos * 50 < ya + 1000) { salida.Read(bloque, 0, bloque.Length); pedidos++; }   // WaveOut: 1 s por delante
                Set(f, "rmsLo", 0f); Set(f, "rmsMid", 0f); Set(f, "rmsHi", 0f);
                Call(f, "ConsumeLevels", Environment.TickCount);   // lo que hace la pantalla en cada vuelta
                bool llego = (float)Get(f, "rmsLo") + (float)Get(f, "rmsMid") + (float)Get(f, "rmsHi") > 0f;
                if (llego && primero < 0) primero = ya;
                if (ya >= 2000) { vueltas++; if (llego) conPulso++; }
                Thread.Sleep(40);   // la vuelta de la pantalla (unas 24 por segundo; con el equipo cargado alguna no trae nada nuevo)
            }
        }
        finally { Call(f, "SueltaBomba"); }
        Info("espera hasta oirse: " + Call(f, "RetrasoAudible") + " ms; primer pulso a los " + primero + " ms; con pulso " + conPulso + " de " + vueltas + " vueltas");
        Check(primero >= 0 && primero < 2000, "el primer pulso le llega a la pantalla en cuanto la musica se oye");
        Check(vueltas > 0 && conPulso * 3 >= vueltas * 2, "y despues, vuelta tras vuelta: las estrellas se deslizan y laten (" + conPulso + " de " + vueltas + ")");
    }

    // LA CAJA NEGRA DEL SONIDO (22-10-2026; SalidaSonido.cs): los cortes del hilo de la musica y los avisos tardios de la salida
    // dejan UNA linea «[Cortes de sonido]» en rocola_error.txt al acabar o saltar la cancion; sin nada que contar, no se escribe.
    static void CajaNegra(string dir)
    {
        Console.WriteLine("\n  10) LA CAJA NEGRA DEL SONIDO\n");
        Type bita = TF.Assembly.GetType("Rocola.Bitacora", true);
        string error = Path.Combine(dir, "rocola_error.txt");
        Func<string> lee = delegate
        {
            bita.GetMethod("Vacia", T).Invoke(null, new object[] { 3000 });
            return File.Exists(error) ? File.ReadAllText(error) : "";
        };
        MethodInfo texto = TF.GetMethod("TextoCortes", T);
        Check(texto != null, "existe el texto de la linea (TextoCortes)");
        Func<string, long, int, int, int, string> txt = delegate (string c, long cortes, int av, int mx, int lat) { return (string)texto.Invoke(null, new object[] { c, cortes, av, mx, lat }); };
        Check(txt("tema", 0, 0, 0, 750) == null, "sin cortes ni avisos tardios: nada que contar");
        string t1 = txt("Mi tema", 1, 0, 0, 750), t2 = txt("Mi tema", 3, 2, 420, 1000);
        Check(t1.StartsWith("[Cortes de sonido] ") && t1.Contains("\"Mi tema\"") && t1.Contains("1 corte del hilo") && t1.Contains("latencia abierta 750 ms") && !t1.Contains("aviso"), "un corte: " + t1);
        Check(t2.Contains("3 cortes") && t2.Contains("2 avisos tardios") && t2.Contains("420 ms") && t2.Contains("1000 ms"), "varios: " + t2);
        Check(txt("x", 0, 1, 90, 0).Contains("latencia abierta ? ms"), "sin latencia conocida, un signo de pregunta");
        // La rocola de verdad (sin ventana): una bomba con cortes, una cancion que suena, y el apunte al acabar.
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        object tr = Activator.CreateInstance(TF.GetNestedType("Track", T), true); Set(tr, "Title", "Cumbia del billar"); Set(tr, "Path", Path.Combine(dir, "x.mp3"));
        object it = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF.GetNestedType("PlayItem", T)); Set(it, "Song", tr);
        Set(f, "nowPlaying", it); Set(f, "latenciaAbiertaMs", 750);
        object b = Nueva(new Fuente(44100 * 2 * 5, 44100, 2));
        Set(f, "bomba", b);
        Call(f, "ApuntaCortesDeSonido");
        Check(lee().IndexOf("[Cortes de sonido]") < 0 && !File.Exists(error), "sin cortes no se escribe nada (ni se crea el archivo)");
        Set(b, "huecos", 2L);
        Call(f, "ApuntaCortesDeSonido");
        string s1 = lee();
        Check(s1.IndexOf("[Cortes de sonido]") >= 0 && s1.Contains("Cumbia del billar") && s1.Contains("2 cortes") && s1.Contains("latencia abierta 750 ms"), "con 2 cortes, una linea con la cancion, la cuenta y la latencia");
        int lineas = 0; foreach (string l in s1.Split('\n')) if (l.Contains("[Cortes de sonido]")) lineas++;
        Check(lineas == 1 && s1.IndexOf("20") == 0, "es UNA sola linea, con su fecha");
        Call(f, "ApuntaCortesDeSonido");
        Check(lee() == s1, "si no hubo mas cortes, no se repite");
        Set(b, "huecos", 5L);
        Call(f, "ApuntaCortesDeSonido");
        string s2 = lee();
        Check(s2.Length > s1.Length && s2.Contains("3 cortes"), "los nuevos se cuentan desde la ultima linea (3 mas, no 5)");
        Suelta(b); Set(f, "bomba", null);
        // Los avisos tardios de la salida (el medidor del lado de la salida) tambien cuentan.
        Type medidor = TF.Assembly.GetType("Rocola.FuenteLatenciaAudio", true), perfil = TF.Assembly.GetType("Rocola.PerfilLatenciaAudio", true);
        object pol = Activator.CreateInstance(perfil, T, null, new object[] { true }, null);
        object med = Activator.CreateInstance(medidor, T, null, new object[] { new Fuente(44100 * 2 * 5, 44100, 2), pol, 1000, true }, null);
        float[] buf = new float[1000];
        ((ISampleProvider)med).Read(buf, 0, buf.Length); Thread.Sleep(450); ((ISampleProvider)med).Read(buf, 0, buf.Length);
        Check((int)medidor.GetProperty("Atrasos", T).GetValue(med, null) == 1 && (int)medidor.GetProperty("MaxAtrasoMs", T).GetValue(med, null) >= 400, "una lectura que llega 450 ms tarde con 1000 ms de latencia cuenta como aviso tardio");
        Set(f, "medidorSalida", med);
        Call(f, "ApuntaCortesDeSonido");
        string s3 = lee();
        Check(s3.Length > s2.Length && s3.Contains("1 aviso tardio"), "y deja su linea al acabar la cancion");
        // El cableado.
        Check(Codigo.Llama(TF.GetMethod("DisposePlaybackNow", T), "ApuntaCortesDeSonido"), "al soltar la cancion (acabar, saltar o parar) se apunta");
        Check(Codigo.Llama(TF.GetMethod("PasaRelevo", T), "ApuntaCortesDeSonido"), "y al pasar al relevo sin pausa, que no suelta la salida");
        Check(Codigo.Llama(TF.GetMethod("ProgressTimer_Tick", T), "ApuntaLatencia"), "el reloj de la cancion mira los cambios de nivel de la latencia");
        int le, es; Codigo.UsosDeCampo(TF.GetMethod("ConBomba", T), "cortesApuntados", out le, out es);
        Check(es > 0, "cada salida nueva empieza su cuenta de cortes de cero");
        Check(Codigo.Llama(Codigo.Llamadas(TF.GetMethod("ApuntaCajaNegra", T)), "ApuntaConSello"), "se escribe por la bitacora de fondo (la pantalla y la musica no esperan al disco)");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TB = asm.GetType("Rocola.BombaSonido");
        if (TB == null) { Check(false, "no esta la bomba de la musica (BombaSonido.cs)"); return 1; }
        string dir = Path.Combine(Path.GetTempPath(), "rocola_bomba_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            LoMismo(); OtroHilo(); Fallo(); Atrasada(); Parar(); Coste(); DeVerdad(dir); Enganches(); ElPulso(); CajaNegra(dir);
        }
        catch (Exception e) { Check(false, "la bateria reviento: " + e); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

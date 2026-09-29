using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

// LA ROCOLA DE VERDAD, CON SU VENTANA, UN GUION DE USO Y UN CRONOMETRO EN LA PANTALLA.
//
// Las demas baterias miran piezas sueltas. Esta arranca la rocola entera -su ventana, su biblioteca, su musica, sus tablas-
// y la usa como un cliente: pide canciones, pasa paginas, entra a un disco, abre el buscador, salta canciones y pide varias
// seguidas. Mientras tanto, un hilo aparte le da un toque a la pantalla cada pocos milisegundos y cronometra cuanto tarda en
// atenderlo. Si la pantalla se para mas de lo permitido, la bateria falla y dice en que momento.
//
//   - La biblioteca se genera aqui (120 discos de canciones de 3 s y uno con tres de 12 s) y las tablas del arranque tambien
//     (40.000 canciones escritas por la propia rocola), en una carpeta temporal: no se toca nada del usuario.
//   - La salida de sonido es de mentira pero pide la musica como WaveOut (sus 20 bloques al empezar y uno cada 50 ms), asi
//     que la cadena de sonido entera trabaja de verdad.
//   - Los limites: 500 ms al arrancar (lo que la rocola apunta como error) y 150 ms usandola (lo que apunta en la bitacora).
//     Medido en este servidor: al arrancar 190-280 ms; usandola, 60 ms la peor. Con la rocola anterior al arranque sin
//     esperar las tablas, el arranque se paraba 0,8-1,1 s y esta bateria falla.
//
// Lo que NO mide: Windows, la tarjeta grafica ni un disco mecanico (aqui la ventana se dibuja por CPU con Mono).
class VentanaReal
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const int LimiteArranqueMs = 500, LimiteUsoMs = 150;
    static Type TF; static Form f;
    static readonly object cer = new object();
    static string fase = "arranque";
    static readonly Dictionary<string, List<double>> lat = new Dictionary<string, List<double>>();
    static readonly List<string> orden = new List<string>();
    static readonly List<string> paradas = new List<string>();
    static readonly Stopwatch reloj = Stopwatch.StartNew();
    static volatile bool fin;
    static int fallos, oks, excepciones;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(n, T | BindingFlags.DeclaredOnly); if (fi != null) return fi.GetValue(o); } return null; }
    static void Set(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(n, T | BindingFlags.DeclaredOnly); if (fi != null) { fi.SetValue(o, v); return; } } throw new Exception("no " + n); }
    static object Call(string n, params object[] a) { MethodInfo m = TF.GetMethod(n, T); try { return m.Invoke(f, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static void Fase(string n) { lock (cer) { fase = n; if (!lat.ContainsKey(n)) { lat[n] = new List<double>(); orden.Add(n); } } Console.WriteLine("  [" + (reloj.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s] " + n); }

    // Una salida de sonido de mentira que pide como WaveOut: sus 20 bloques de 50 ms al empezar y despues uno cada 50 ms.
    internal static int arranques, finales, bloques;
    class SalidaRitmo : IWavePlayer
    {
        IWaveProvider p; byte[] b; System.Windows.Forms.Timer t; PlaybackState st = PlaybackState.Stopped;
        public void Init(IWaveProvider x) { p = x; b = new byte[x.WaveFormat.AverageBytesPerSecond / 20]; }
        public void Play()
        {
            if (st == PlaybackState.Playing) return;
            Interlocked.Increment(ref arranques); for (int i = 0; i < 20; i++) if (p.Read(b, 0, b.Length) == 0) { Acaba(null); return; }
            st = PlaybackState.Playing;
            t = new System.Windows.Forms.Timer(); t.Interval = 50;
            t.Tick += delegate { if (st != PlaybackState.Playing) return; bloques++; int n; try { n = p.Read(b, 0, b.Length); } catch (Exception e) { Acaba(e); return; } if (n == 0) Acaba(null); };
            t.Start();
        }
        void Acaba(Exception e) { finales++; st = PlaybackState.Stopped; if (t != null) t.Stop(); EventHandler<StoppedEventArgs> h = PlaybackStopped; if (h != null) h(this, new StoppedEventArgs(e)); }
        public void Stop() { st = PlaybackState.Stopped; if (t != null) t.Stop(); }
        public void Pause() { }
        public float Volume { get { return 1f; } set { } }
        public PlaybackState PlaybackState { get { return st; } }
        public event EventHandler<StoppedEventArgs> PlaybackStopped;
        public void Dispose() { if (t != null) { t.Stop(); t.Dispose(); } }
    }

    static void Vigia()
    {
        while (!fin)
        {
            double t0 = reloj.Elapsed.TotalMilliseconds;
            ManualResetEvent hecho = new ManualResetEvent(false);
            try { if (f == null || !f.IsHandleCreated) { Thread.Sleep(20); continue; } f.BeginInvoke((MethodInvoker)delegate { hecho.Set(); }); }
            catch { Thread.Sleep(20); continue; }
            while (!hecho.WaitOne(50, false)) if (fin) return;
            double d = reloj.Elapsed.TotalMilliseconds - t0;
            lock (cer)
            {
                lat[fase].Add(d);
                if (d > 40) paradas.Add(fase + "|" + (t0 / 1000.0).ToString("0.00") + "|" + d.ToString("0"));
            }
            Thread.Sleep(4);
        }
    }

    // Un WAV PCM de 8 bits mono: pequeno, y la rocola lo abre, lo mide y lo toca como cualquier otro.
    static void Wav(string ruta, int hz, double seg, int semilla)
    {
        int n = (int)(hz * seg);
        Random r = new Random(semilla);
        using (BinaryWriter w = new BinaryWriter(File.Create(ruta)))
        {
            w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n); w.Write(new char[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(hz); w.Write(hz); w.Write((short)1); w.Write((short)8);
            w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(n);
            double f1 = 110 + semilla % 220;
            for (int i = 0; i < n; i++) w.Write((byte)(128 + 50 * Math.Sin(2 * Math.PI * f1 * i / hz) + r.Next(-12, 13)));
        }
    }

    static int Main(string[] args)
    {
        string exe = args.Length > 0 ? args[0] : "rocola.exe";
        string raiz = Path.Combine(Path.GetTempPath(), "rocola_ventanareal_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        string datos = Path.Combine(raiz, "datos"), musica = Path.Combine(datos, "musica");
        Directory.CreateDirectory(musica);
        // Nada del usuario: la carpeta de datos es la temporal (antes de que la rocola la mire por primera vez).
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", datos);
        int rc = 1;
        try { rc = Corre(exe, datos, musica); }
        finally { try { Directory.Delete(raiz, true); } catch { } }
        return rc;
    }

    static int Corre(string exe, string datos, string musica)
    {
        string[] generos = { "Rock", "Pop", "Salsa", "Cumbia", "Bolero", "Jazz", "Reggaeton", "Banda" };
        Console.WriteLine("\n  LA BIBLIOTECA Y LAS TABLAS (generadas aqui)\n");
        Stopwatch gen = Stopwatch.StartNew();
        for (int d = 0; d < 120; d++)
        {
            string carpetaDisco = Path.Combine(musica, "Artista " + (d / 4).ToString("000") + " - Disco " + d.ToString("0000") + " (" + generos[d % generos.Length] + ")");
            Directory.CreateDirectory(carpetaDisco);
            for (int k = 1; k <= 10; k++) Wav(Path.Combine(carpetaDisco, k.ToString("00") + " - Cancion " + k + " del disco " + d + ".wav"), 8000, 3.0, d * 16 + k);
        }
        string largo = Path.Combine(musica, "ZZ Largo - Tres temas");
        Directory.CreateDirectory(largo);
        for (int k = 1; k <= 3; k++) Wav(Path.Combine(largo, k.ToString("00") + " - Tema largo " + k + ".wav"), 8000, 12.0, 900 + k);
        File.WriteAllText(Path.Combine(datos, "rocola_config.txt"), "diagnostico=1\r\n");

        TF = Assembly.LoadFrom(exe).GetType("Rocola.Form1", true);
        {
            object g = Codigo.FormSinEncender(TF);
            Set(g, "startupReadPending", false);
            foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(g) == null) fi.SetValue(g, Activator.CreateInstance(fi.FieldType, true));
            IDictionary loud = (IDictionary)Get(g, "loudCache"), dur = (IDictionary)Get(g, "durCache"), eq = (IDictionary)Get(g, "eqCache");
            Random r = new Random(7);
            for (int i = 0; i < 40000; i++)
            {
                string p = Path.Combine(Path.Combine(musica, "Otro disco " + (i / 12)), "Tema " + i + ".mp3");
                loud[p] = 0.05f + (float)r.NextDouble() * 0.2f; dur[p] = 120f + r.Next(200);
                float[] c = new float[12]; for (int k = 0; k < 8; k++) c[k] = -30f + (float)r.NextDouble() * 10f; c[8] = 19500f; c[10] = 16000f; eq[p] = c;
            }
            Set(g, "eqDirty", true); Set(g, "eqAvg", new float[8]);
            TF.GetMethod("SaveLoudCache", T).Invoke(g, null);
            TF.GetMethod("FlushEqIfDirty", T).Invoke(g, null);
            Codigo.EsperaGuardado(TF, g);
        }
        long kb = 0; foreach (string x in Directory.GetFiles(datos, "rocola_*.txt")) kb += new FileInfo(x).Length / 1024;
        Console.WriteLine("        121 discos, 1.203 canciones y tablas de 40.000 canciones (" + kb + " KB) en " + gen.ElapsedMilliseconds + " ms");

        Console.WriteLine("\n  LA ROCOLA, USADA\n");
        Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
        {
            Interlocked.Increment(ref excepciones);
            Console.WriteLine("  (excepcion en la pantalla: " + e.Exception.GetType().Name + ": " + e.Exception.Message + ")");
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Fase("arranque");
        f = (Form)Activator.CreateInstance(TF);
        Set(f, "fabricaSalidaClasica", (Func<IWavePlayer>)delegate { return new SalidaRitmo(); });
        // El vigia del foco pregunta a Windows quien esta delante: aqui no hay Windows.
        System.Windows.Forms.Timer sinFoco = new System.Windows.Forms.Timer(); sinFoco.Interval = 20;
        sinFoco.Tick += delegate { System.Windows.Forms.Timer ft = Get(f, "focusTimer") as System.Windows.Forms.Timer; if (ft != null && ft.Enabled) ft.Stop(); };
        sinFoco.Start();
        Thread v = new Thread(Vigia); v.IsBackground = true; v.Start();

        List<Action> guion = new List<Action>();
        int paso = 0, espera = 0, esperas = 0; object discoLargo = null; bool listo = false;
        List<object> sonaron = new List<object>();   // las canciones que llegaron a sonar (con el relevo sin pausa la salida no se reabre)
        Func<int, object> disco = delegate (int i) { IList a = (IList)Get(f, "albums"); return a[i % a.Count]; };
        Func<object, int, object> pista = delegate (object album, int i) { IList t = (IList)Get(album, "Tracks"); return t[i % t.Count]; };
        Action<int> esperaMs = delegate (int ms) { espera = Environment.TickCount + ms; };
        guion.Add(delegate
        {
            IList a = (IList)Get(f, "albums");
            if (a == null || a.Count < 121 || (bool)Get(f, "libBuilding")) { if (++esperas > 600) throw new Exception("la biblioteca no llego en 60 s"); esperaMs(100); paso--; return; }
            listo = true;
            foreach (object x in a) if (((string)Get(x, "Folder") ?? "").IndexOf("Tres temas") >= 0) discoLargo = x;
            Fase("reposo con la biblioteca lista"); esperaMs(3000);
        });
        guion.Add(delegate { Set(f, "credits", 200); Call("UpdateCredits"); Fase("pedir una cancion"); Call("AddSong", pista(discoLargo, 0), discoLargo); esperaMs(3000); });
        guion.Add(delegate { Fase("pedir dos mas"); Call("AddSong", pista(discoLargo, 1), discoLargo); Call("AddSong", pista(discoLargo, 2), discoLargo); esperaMs(3000); });
        for (int i = 0; i < 10; i++) { int k = i; guion.Add(delegate { if (k == 0) Fase("pasar paginas"); if (k < 6) Call("NextPage"); else Call("PrevPage"); esperaMs(350); }); }
        guion.Add(delegate { Fase("entrar a un disco y volver"); Call("EnterAlbum", disco(37)); esperaMs(1500); });
        guion.Add(delegate { Call("BackToAlbums"); esperaMs(1200); });
        guion.Add(delegate { Fase("abrir y cerrar el buscador"); Call("OpenSearch"); esperaMs(1500); });
        guion.Add(delegate { Call("CloseSearch"); esperaMs(1200); });
        guion.Add(delegate { Fase("saltar canciones"); Call("SkipSong"); esperaMs(2500); });
        guion.Add(delegate { Call("SkipSong"); esperaMs(2500); });
        guion.Add(delegate { Fase("pedir seis cortas seguidas"); for (int i = 0; i < 6; i++) { object a = disco(50 + i); Call("AddSong", pista(a, i), a); } esperaMs(9000); });
        guion.Add(delegate { Fase("reposo sonando"); esperaMs(3000); });
        guion.Add(delegate { Fase("cerrar"); fin = true; f.Close(); });
        System.Windows.Forms.Timer g2 = new System.Windows.Forms.Timer(); g2.Interval = 50;
        g2.Tick += delegate
        {
            object np = Get(f, "nowPlaying");
            if (np != null && !sonaron.Contains(np)) sonaron.Add(np);
            if (paso >= guion.Count || Environment.TickCount - espera < 0) return;
            try { guion[paso++](); } catch (Exception e) { Console.WriteLine("  (paso " + (paso - 1) + ": " + e.GetType().Name + ": " + e.Message + ")"); if (!listo) { fin = true; f.Close(); } }
        };
        f.Shown += delegate { g2.Start(); };
        // Una red: si algo se queda colgado, la bateria no se queda esperando para siempre.
        Thread red = new Thread(delegate () { Thread.Sleep(240000); Console.WriteLine("  FALLO la rocola no termino el guion en 4 minutos"); Environment.Exit(1); });
        red.IsBackground = true; red.Start();
        Application.Run(f);
        fin = true;

        Console.WriteLine("\n  LA PANTALLA, FASE POR FASE (cuanto tarda en atender un toque)\n");
        double peorArranque = 0, peorUso = 0; string dondeUso = "";
        lock (cer)
        {
            foreach (string n in orden)
            {
                List<double> l = lat[n]; if (l.Count == 0) continue; l.Sort();
                Console.WriteLine("  " + n.PadRight(34) + " n=" + l.Count.ToString().PadLeft(5) + "  mediana " + l[l.Count / 2].ToString("0.0").PadLeft(5)
                                  + "  p99 " + l[l.Count * 99 / 100].ToString("0").PadLeft(4) + "  peor " + l[l.Count - 1].ToString("0").PadLeft(5) + " ms");
                double peor = l[l.Count - 1];
                if (n == "arranque") peorArranque = peor;
                else if (n != "cerrar" && peor > peorUso) { peorUso = peor; dondeUso = n; }
            }
            Console.WriteLine("\n  PARADAS DE MAS DE 40 MS: " + paradas.Count);
            foreach (string p in paradas) { string[] q = p.Split('|'); Console.WriteLine("   " + q[1] + " s  " + q[0] + "  " + q[2] + " ms"); }
        }
        string diag = Path.Combine(datos, "rocola_diagnostico.txt");
        string[] propias = File.Exists(diag) ? Array.FindAll(File.ReadAllLines(diag), delegate (string s) { return s.Contains("pantalla parada"); }) : new string[0];
        Console.WriteLine("\n  LO QUE APUNTO EL VIGIA DE LA PROPIA ROCOLA: " + propias.Length);
        foreach (string s in propias) Console.WriteLine("   " + s);
        Console.WriteLine("\n  SONO: " + sonaron.Count + " canciones, " + arranques + " arranques de la salida, " + finales + " finales, " + bloques + " bloques de 50 ms\n");

        Check(listo, "la biblioteca generada llego a la pantalla (121 discos)");
        Check(sonaron.Count >= 5 && bloques > 100, "la musica sono de verdad: " + sonaron.Count + " canciones distintas, " + bloques + " bloques de 50 ms pedidos por la salida");
        Check(excepciones == 0, "sin excepciones en la pantalla");
        Check(peorArranque <= LimiteArranqueMs, "al arrancar, con tablas de 40.000 canciones, la pantalla nunca se paro mas de " + LimiteArranqueMs + " ms (la peor: " + peorArranque.ToString("0") + " ms)");
        Check(peorUso <= LimiteUsoMs, "usandola, nunca mas de " + LimiteUsoMs + " ms (la peor: " + peorUso.ToString("0") + " ms" + (dondeUso.Length > 0 ? ", en \"" + dondeUso + "\"" : "") + ")");
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

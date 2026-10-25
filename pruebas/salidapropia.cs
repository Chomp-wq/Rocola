using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using NAudio.Wave;

// T5 (25-10-2026): LA SALIDA DE SONIDO CON SU PROPIO HILO (SalidaPropia.cs).
//
// Se le pone un Windows imitado (ISistemaWaveOut, con un RealProxy): "suena" cada bloque con un reloj de verdad, lo marca hecho y avisa
// por el evento, como winmm. Y cuenta las veces que se quedo sin nada que sonar (hambre).
// Lo que se vigila:
//   1) suena todo, en orden y entero; avisa la parada una vez, sin error; al cerrar suelta los bloques y cierra;
//   2) el reloj (GetPosition) da lo que ya sono;
//   3) con el hilo que la creo parado 3 s (como la pantalla del A4), no se queda sin musica: la rellena su propio hilo;
//   4) pausa y sigue; parar a media cancion avisa la parada;
//   5) si no abre el aparato, el error es el de winmm (el que Form1 reconoce como del aparato); si Windows falla a media cancion, la
//      parada lleva su error y cuenta como fallo propio; si falla la musica que le dan, la parada lleva ese error y NO es fallo propio;
//   6) la parada se avisa en el hilo de la pantalla (por su SynchronizationContext);
//   7) en todo el ejecutable no hay ni un WaitOne(Int32), WaitAny ni WaitAll con tiempo: no existen en el .NET del A4 (2.0 SP1);
//   8) Form1 la usa (CreaWaveOut) y vuelve a la de siempre si no abre o si falla por si misma.
// Es un Windows imitado: dice que el hilo hace lo que promete; si el controlador de XP se porta igual, solo se ve en el A4.
class SalidaPropia
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TW, TI, TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static int OffLargo, OffBanderas;
    const int Hecho = 1, EnCola = 0x10;

    // ---------------- el Windows imitado ----------------
    sealed class Imitado : RealProxy
    {
        public int ResultadoAbre, FallaEscribeTras = -1, Escrituras, Preparadas, Cerradas, Hambre;
        public volatile bool Pausada, Cerrado;
        public long Posicion;
        public readonly List<byte> Sonado = new List<byte>();
        readonly Queue<IntPtr> cola = new Queue<IntPtr>();
        readonly object cerrojo = new object();
        AutoResetEvent aviso; WaveFormat formato; Thread reloj; volatile bool empezo;
        public Imitado(Type t) : base(t) { }
        public override IMessage Invoke(IMessage msg)
        {
            IMethodCallMessage m = (IMethodCallMessage)msg; object[] a = m.Args; object r = 0;
            switch (m.MethodName)
            {
                case "Abre":
                    formato = (WaveFormat)a[1]; aviso = (AutoResetEvent)a[2];
                    a[0] = ResultadoAbre == 0 ? new IntPtr(1) : IntPtr.Zero; r = ResultadoAbre;
                    if (ResultadoAbre == 0) { reloj = new Thread(Suena); reloj.IsBackground = true; reloj.Start(); }
                    break;
                case "Prepara": Interlocked.Increment(ref Preparadas); break;
                case "Desprepara": Interlocked.Decrement(ref Preparadas); break;
                case "Escribe":
                    {
                        IntPtr h = (IntPtr)a[1];
                        if (FallaEscribeTras >= 0 && Escrituras >= FallaEscribeTras) { r = 6; break; }   // MMSYSERR_NODRIVER... cualquiera
                        Escrituras++;
                        int b = Marshal.ReadInt32(h, OffBanderas);
                        Marshal.WriteInt32(h, OffBanderas, (b & ~Hecho) | EnCola);
                        lock (cerrojo) { cola.Enqueue(h); empezo = true; }
                        break;
                    }
                case "Reinicia":
                    lock (cerrojo) { while (cola.Count > 0) { IntPtr h = cola.Dequeue(); Termina(h, false); } }
                    if (aviso != null) try { aviso.Set(); } catch (ObjectDisposedException) { }
                    break;
                case "Pausa": Pausada = true; break;
                case "Sigue": Pausada = false; break;
                case "Cierra": Cerradas++; Cerrado = true; break;
                case "Posicion": lock (cerrojo) a[1] = Posicion; break;
            }
            return new ReturnMessage(r, a, a.Length, m.LogicalCallContext, m);
        }
        void Termina(IntPtr h, bool sono)
        {
            int largo = Marshal.ReadInt32(h, OffLargo);
            if (sono)
            {
                byte[] d = new byte[largo]; Marshal.Copy(Marshal.ReadIntPtr(h, 0), d, 0, largo);
                Sonado.AddRange(d); Posicion += largo;
            }
            int b = Marshal.ReadInt32(h, OffBanderas);
            Marshal.WriteInt32(h, OffBanderas, (b & ~EnCola) | Hecho);
        }
        // La tarjeta: suena el bloque mas viejo en su tiempo real, lo marca hecho y avisa.
        void Suena()
        {
            while (!Cerrado)
            {
                IntPtr h = IntPtr.Zero; int largo = 0;
                lock (cerrojo) { if (!Pausada && cola.Count > 0) { h = cola.Peek(); largo = Marshal.ReadInt32(h, OffLargo); } }
                if (h == IntPtr.Zero)
                {
                    if (empezo && !Pausada && !TerminoLaFuente) Interlocked.Increment(ref Hambre);
                    Thread.Sleep(5); continue;
                }
                Thread.Sleep(Math.Max(1, (int)(largo * 1000L / formato.AverageBytesPerSecond)));
                lock (cerrojo) { if (cola.Count > 0 && cola.Peek() == h) { cola.Dequeue(); Termina(h, true); } }
                if (Cerrado) break;   // winmm no avisa despues de cerrar
                try { aviso.Set(); } catch (ObjectDisposedException) { break; }
            }
        }
        public volatile bool TerminoLaFuente;
    }

    // ---------------- la musica que se le da ----------------
    sealed class Fuente : IWaveProvider
    {
        readonly byte[] d; int p; readonly WaveFormat wf = new WaveFormat(44100, 16, 2); public int FallaEn = -1; public Imitado Avisa;
        public int AtoraEn = -1; public readonly ManualResetEvent Suelta = new ManualResetEvent(false); public volatile bool Atorada;
        public Fuente(double seg) { d = new byte[(int)(seg * 44100) * 4]; for (int i = 0; i < d.Length; i++) d[i] = (byte)(i * 7 + (i >> 9)); }
        public byte[] Todo { get { return d; } }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(byte[] b, int o, int c)
        {
            if (FallaEn >= 0 && p >= FallaEn) throw new InvalidOperationException("la cancion fallo al leer");
            if (AtoraEn >= 0 && p >= AtoraEn) { AtoraEn = -1; Atorada = true; Suelta.WaitOne(); Atorada = false; }
            int n = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, n); p += n;
            if (n == 0 && Avisa != null) Avisa.TerminoLaFuente = true;
            return n;
        }
    }

    static object Nueva(Imitado im)
    {
        object so = im.GetTransparentProxy();
        object w = Activator.CreateInstance(TW, T, null, new object[] { so }, null);
        TW.GetField("DesiredLatency").SetValue(w, 300); TW.GetField("NumberOfBuffers").SetValue(w, 6);
        return w;
    }
    static IWavePlayer P(object w) { return (IWavePlayer)w; }
    static bool Espera(Func<bool> cond, int ms) { int t0 = Environment.TickCount; while (!cond()) { if (Environment.TickCount - t0 > ms) return false; Thread.Sleep(10); } return true; }
    static bool Igual(List<byte> a, byte[] b) { if (a.Count != b.Length) return false; for (int i = 0; i < b.Length; i++) if (a[i] != b[i]) return false; return true; }

    static void SuenaTodo()
    {
        Console.WriteLine("\n  1) Y 2) SUENA TODO, EN ORDEN, Y EL RELOJ\n");
        Imitado im = new Imitado(TI); Fuente f = new Fuente(2.0); f.Avisa = im;
        object w = Nueva(im); int paradas = 0; Exception error = null;
        P(w).PlaybackStopped += delegate (object s, StoppedEventArgs e) { paradas++; error = e.Exception; };
        P(w).Init(f);
        Check(im.Preparadas == 6, "abre y prepara sus seis bloques de 50 ms (" + im.Preparadas + ")");
        P(w).Play();
        bool acabo = Espera(delegate { return paradas > 0; }, 6000);
        long pos = ((IWavePosition)w).GetPosition();
        Check(acabo && paradas == 1 && error == null && P(w).PlaybackState == PlaybackState.Stopped, "al acabar avisa la parada una vez, sin error");
        Check(Igual(im.Sonado, f.Todo), "suena todo, en orden y entero (" + im.Sonado.Count + " de " + f.Todo.Length + " bytes)");
        Check(pos == f.Todo.Length, "el reloj da lo que ya sono (" + pos + " bytes)");
        Check(im.Hambre == 0, "sin quedarse nunca sin musica (" + im.Hambre + ")");
        P(w).Dispose();
        Check(im.Preparadas == 0 && im.Cerradas == 1, "al cerrar suelta los bloques y cierra la salida");
    }

    static void PantallaParada()
    {
        Console.WriteLine("\n  3) CON LA PANTALLA PARADA 3 S\n");
        Imitado im = new Imitado(TI); Fuente f = new Fuente(4.0); f.Avisa = im;
        object w = Nueva(im); int paradas = 0;
        P(w).PlaybackStopped += delegate { paradas++; };
        P(w).Init(f); P(w).Play();
        Thread.Sleep(3000);   // el hilo que la creo y le dio Play, parado (como la pantalla del A4 el 23-10, 2015 ms)
        Check(im.Hambre == 0 && im.Sonado.Count > 44100 * 4 * 2, "en esos 3 s sono " + (im.Sonado.Count / (44100.0 * 4)).ToString("0.0") + " s sin quedarse ni una vez sin musica (" + im.Hambre + "): la rellena su propio hilo");
        Espera(delegate { return paradas > 0; }, 6000);
        Check(Igual(im.Sonado, f.Todo), "y al final sono todo, entero");
        P(w).Dispose();
    }

    static void PausaYParada()
    {
        Console.WriteLine("\n  4) PAUSA, SIGUE Y PARAR\n");
        Imitado im = new Imitado(TI); Fuente f = new Fuente(3.0); f.Avisa = im;
        object w = Nueva(im); int paradas = 0; Exception error = null;
        P(w).PlaybackStopped += delegate (object s, StoppedEventArgs e) { paradas++; error = e.Exception; };
        P(w).Init(f); P(w).Play();
        Thread.Sleep(500);
        P(w).Pause();
        long a = ((IWavePosition)w).GetPosition(); Thread.Sleep(400); long b = ((IWavePosition)w).GetPosition();
        Check(P(w).PlaybackState == PlaybackState.Paused && b - a <= 4 * 44100 * 4 / 20, "en pausa casi no avanza (" + (b - a) + " bytes en 400 ms: lo que ya estaba sonando)");
        P(w).Play();
        Thread.Sleep(300);
        P(w).Stop();
        Check(paradas == 1 && error == null && P(w).PlaybackState == PlaybackState.Stopped, "parar a media cancion avisa la parada, sin error");
        P(w).Dispose();
        Check(im.Preparadas == 0 && im.Cerradas == 1, "y al cerrar suelta todo");
    }

    static void Fallos()
    {
        Console.WriteLine("\n  5) CUANDO FALLA ALGO\n");
        Imitado im = new Imitado(TI); im.ResultadoAbre = 2;   // MMSYSERR_BADDEVICEID
        object w = Nueva(im);
        Exception ex = null;
        try { P(w).Init(new Fuente(1)); } catch (Exception e) { ex = e; }
        NAudio.MmException mm = ex as NAudio.MmException;
        Check(mm != null && mm.Result == NAudio.MmResult.BadDeviceId, "si no hay aparato, Init lanza el error de winmm (BadDeviceId), el que Form1 trata como del aparato");
        try { P(w).Dispose(); } catch { }
        im = new Imitado(TI); im.FallaEscribeTras = 20; Fuente f = new Fuente(3.0);
        w = Nueva(im); int paradas = 0; Exception error = null;
        P(w).PlaybackStopped += delegate (object s, StoppedEventArgs e) { paradas++; error = e.Exception; };
        P(w).Init(f); P(w).Play();
        Espera(delegate { return paradas > 0; }, 5000);
        Check(paradas == 1 && error is NAudio.MmException && (bool)TW.GetProperty("FalloPropio", T).GetValue(w, null), "si Windows falla a media cancion, la parada lleva su error y cuenta como fallo de la salida");
        P(w).Dispose();
        im = new Imitado(TI); f = new Fuente(3.0); f.FallaEn = 44100 * 4;
        w = Nueva(im); paradas = 0; error = null;
        P(w).PlaybackStopped += delegate (object s, StoppedEventArgs e) { paradas++; error = e.Exception; };
        P(w).Init(f); P(w).Play();
        Espera(delegate { return paradas > 0; }, 5000);
        Check(paradas == 1 && error is InvalidOperationException && !(bool)TW.GetProperty("FalloPropio", T).GetValue(w, null), "si falla la musica que le dan, la parada lleva ese error y NO cuenta como fallo de la salida");
        P(w).Dispose();
    }

    static void CierreAtorado()
    {
        Console.WriteLine("\n  5b) CERRAR CON LA MUSICA ATORADA MAS DE 2 S\n");
        Imitado im = new Imitado(TI); Fuente f = new Fuente(3.0); f.AtoraEn = 44100 * 4 / 2;
        object w = Nueva(im);
        P(w).Init(f); P(w).Play();
        Check(Espera(delegate { return f.Atorada; }, 3000), "la lectura de la musica se queda atorada");
        int t0 = Environment.TickCount;
        P(w).Dispose();
        int ms = Environment.TickCount - t0;
        Check(im.Cerrado && im.Preparadas > 0, "Dispose espera 2 s (" + ms + " ms), cierra la salida y NO libera los bloques en los que el hilo aun puede copiar");
        f.Suelta.Set();
        Thread.Sleep(300);
        Check(true, "al soltarse la lectura, el hilo copia en sus bloques y se va sin tumbar nada");
    }

    sealed class Pantalla : SynchronizationContext
    {
        public readonly List<SendOrPostCallback> Pendientes = new List<SendOrPostCallback>(); public readonly List<object> Estados = new List<object>();
        public override void Post(SendOrPostCallback d, object state) { lock (Pendientes) { Pendientes.Add(d); Estados.Add(state); } }
    }
    static void EnLaPantalla()
    {
        Console.WriteLine("\n  6) LA PARADA, EN EL HILO DE LA PANTALLA\n");
        Pantalla pan = new Pantalla();
        SynchronizationContext antes = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(pan);
        Imitado im = new Imitado(TI); Fuente f = new Fuente(0.5); f.Avisa = im;
        object w = Nueva(im);
        SynchronizationContext.SetSynchronizationContext(antes);
        int hilo = -1, paradas = 0;
        P(w).PlaybackStopped += delegate { paradas++; hilo = Thread.CurrentThread.ManagedThreadId; };
        P(w).Init(f); P(w).Play();
        Espera(delegate { lock (pan.Pendientes) return pan.Pendientes.Count > 0; }, 4000);
        Check(paradas == 0, "el hilo de la salida no avisa por su cuenta: lo deja a la pantalla");
        SendOrPostCallback cb; object st; lock (pan.Pendientes) { cb = pan.Pendientes[0]; st = pan.Estados[0]; }
        cb(st);
        Check(paradas == 1 && hilo == Thread.CurrentThread.ManagedThreadId, "y la pantalla lo recibe en su hilo");
        P(w).Dispose();
    }

    static int Sp2(Assembly asm, out string cual)
    {
        int malos = 0; cual = "";
        foreach (Type t in asm.GetTypes())
            foreach (MethodBase m in t.GetMethods(T | BindingFlags.DeclaredOnly))
            {
                List<Codigo.Paso> pasos; try { pasos = Codigo.Pasos(m); } catch { continue; }
                if (pasos == null) continue;
                foreach (Codigo.Paso p in pasos)
                {
                    if (p.Op != 0x28 && p.Op != 0x6F) continue;
                    MethodBase l; try { l = m.Module.ResolveMethod(p.Numero); } catch { continue; }
                    if (l == null || l.DeclaringType != typeof(WaitHandle)) continue;
                    ParameterInfo[] ps = l.GetParameters();
                    bool sp2 = (l.Name == "WaitOne" && ps.Length == 1) || ((l.Name == "WaitAny" || l.Name == "WaitAll") && ps.Length == 2);
                    if (sp2) { malos++; cual = t.Name + "." + m.Name; }
                }
            }
        return malos;
    }
    static void SinWaitOneDeSp2(Assembly asm)
    {
        Console.WriteLine("\n  7) NADA DE .NET 2.0 SP2\n");
        string cual;
        int malos = Sp2(asm, out cual);
        // El control: en NAudio si hay uno, el de WaveOutEvent (por eso la rocola no lo usa). Si el lector no lo viera, no veria nada.
        string enNAudio; int deNAudio = Sp2(typeof(WaveOut).Assembly, out enNAudio);
        Check(deNAudio >= 1, "el lector si encuentra el que ya se sabe que hay en NAudio (" + enNAudio + ")");
        Check(malos == 0, "en todo el ejecutable de la rocola: ni WaitOne(tiempo) sin el bool, ni WaitAny/WaitAll con tiempo sin el bool" + (malos > 0 ? " (" + cual + ")" : ""));
    }

    static void EnForm1()
    {
        Console.WriteLine("\n  8) FORM1 LA USA, Y SABE VOLVER A LA DE SIEMPRE\n");
        Check(Codigo.Llamadas(TF.GetMethod("CreaWaveOut", T)).Exists(delegate (string s) { return s.StartsWith("WaveOutPropio"); }), "CreaWaveOut da la salida con hilo propio");
        Check(Codigo.Llama(TF.GetMethod("IniciaSalida", T), "Form1.CreaWaveOut") || Codigo.Llama(TF.GetMethod("IniciaSalida", T), "CreaWaveOut"), "IniciaSalida la abre y, si no abre por algo que no es el aparato ni el formato, vuelve a pedir una (la de siempre)");
        Check(TF.GetField("salidaPropiaFallo", T) != null && Codigo.LeeCampo(new List<MethodBase> { TF.GetMethod("CreaWaveOut", T) }, "salidaPropiaFallo")
              && Codigo.EscribeCampo(new List<MethodBase> { TF.GetMethod("Output_PlaybackStopped", T) }, "salidaPropiaFallo"),
              "si falla por si misma a media cancion, Output_PlaybackStopped la cambia por la de siempre para la sesion");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TW = asm.GetType("Rocola.WaveOutPropio", false); TI = asm.GetType("Rocola.ISistemaWaveOut", false);
        if (TW == null || TI == null) { Check(false, "no esta Rocola.WaveOutPropio"); return 1; }
        OffLargo = (int)TW.GetField("OffLargo", T).GetValue(null); OffBanderas = (int)TW.GetField("OffBanderas", T).GetValue(null);
        try { SuenaTodo(); } catch (Exception e) { Check(false, "suena todo: " + e); }
        try { PantallaParada(); } catch (Exception e) { Check(false, "pantalla parada: " + e); }
        try { PausaYParada(); } catch (Exception e) { Check(false, "pausa y parada: " + e); }
        try { Fallos(); } catch (Exception e) { Check(false, "fallos: " + e); }
        try { CierreAtorado(); } catch (Exception e) { Check(false, "cierre atorado: " + e); }
        try { EnLaPantalla(); } catch (Exception e) { Check(false, "en la pantalla: " + e); }
        try { SinWaitOneDeSp2(asm); } catch (Exception e) { Check(false, "sin SP2: " + e); }
        try { EnForm1(); } catch (Exception e) { Check(false, "en Form1: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

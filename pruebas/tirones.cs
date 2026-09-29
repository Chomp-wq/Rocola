using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;
// LOS TIRONES DE LA PANTALLA AL PONER UNA CANCION (BombaSonido.cs, SubirTodo.cs, Form1.Datos.cs; las dos ramas).
//
// Tres cosas paraban el hilo de la pantalla -el que dibuja y atiende los botones-, medidas con la rocola de verdad
// y 20.000 canciones en las tablas, en un procesador de servidor (en el equipo del bar, varias veces mas):
//   - AL EMPEZAR UNA CANCION, 44-48 ms: la salida clasica (WaveOut) llena sus 20 bloques de una vez, 1 s de musica, y
//     la pantalla esperaba a que el hilo de la musica descomprimiera y procesara ese segundo entero;
//   - AL GUARDAR LA TABLA DE VOLUMEN (al acabar una cancion recien medida, y cada tanto mientras se mide la
//     biblioteca), 23 ms: la copia de las duraciones rehacia su indice en la pantalla;
//   - EL TECHO DE "SUBIR TODO", 11-13 ms una vez por minuto mientras llegan medidas: se recontaba la tabla entera
//     en el latido de la pantalla.
// Aqui se mira, con la rocola de verdad:
//   1) el hilo de la musica deja hecho el primer segundo por su cuenta y avisa; pedirlo no espera nada, y despues la
//      salida solo copia; lo que sale es lo mismo, muestra a muestra; sonando, el colchon vuelve a sus 200 ms; y el
//      aviso llega tambien si la cancion se acaba o falla antes;
//   2) la rocola arranca la salida cuando el aviso llega a la pantalla, y no si mientras tanto se salto la cancion;
//      sin ventana a la que volver, arranca como antes;
//   3) la cancion arranca asi (StartOpenedTrack) y lo que hace despues es lo de siempre, en el mismo orden;
//   4) guardar la tabla de volumen no rehace el indice en la pantalla;
//   5) con medidas nuevas el techo se cuenta fuera: la pantalla sigue con el de antes y despues pone el mismo que
//      daria la cuenta en la pantalla; si mientras tanto cambian los ajustes, lo contado con los viejos no se pone.
class Tirones
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TB, TS;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v) { return v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(n, T); if (fi != null) return fi.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void Set(object o, string n, object v)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(n, T); if (fi != null) { fi.SetValue(o, v); return; } }
        throw new Exception("no esta " + n);
    }
    static object Call(object o, string n, params object[] a)
    {
        MethodInfo m = (o is Type ? (Type)o : o.GetType()).GetMethod(n, T);
        if (m == null) throw new Exception("no esta " + n);
        try { return m.Invoke(o is Type ? null : o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }

    // Una cancion que se hace despacio: cada trozo de 25 ms cuesta 4 ms (el primer segundo, unos 160 ms). Las muestras
    // se reconocen (la n vale n*1e-6); puede acabarse o fallar donde se le pida.
    class Lenta : ISampleProvider
    {
        readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); readonly long total; long pos;
        public long FallaEn = -1; public int CostoMs = 4;
        public Lenta(long muestras) { total = muestras; }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c)
        {
            if (FallaEn >= 0 && pos >= FallaEn) throw new IOException("el disco fallo");
            if (CostoMs > 0) Thread.Sleep(CostoMs);
            int n = (int)Math.Min(c, total - pos);
            for (int i = 0; i < n; i++) { b[o + i] = (float)(((pos + i) % 1000003) * 1e-6); }
            pos += n; return n;
        }
    }
    class Falsa : IWavePlayer
    {
        public int Plays; public void Init(IWaveProvider p) { } public void Play() { Plays++; } public void Stop() { } public void Pause() { }
        public float Volume { get { return 1f; } set { } }
        public PlaybackState PlaybackState { get { return PlaybackState.Stopped; } }
        public event EventHandler<StoppedEventArgs> PlaybackStopped { add { } remove { } }
        public void Dispose() { }
    }
    static object Bomba(ISampleProvider s) { return Activator.CreateInstance(TB, T, null, new object[] { s }, null); }
    static int Lee(object b, float[] buf, int c) { return ((ISampleProvider)b).Read(buf, 0, c); }
    static bool Espera(Func<bool> cond, int ms) { int t0 = Environment.TickCount; while (!cond()) { if (Environment.TickCount - t0 > ms) return false; Thread.Sleep(2); } return true; }

    static void ElPrimerSegundo()
    {
        Console.WriteLine("\n  1) EL PRIMER SEGUNDO, HECHO POR EL HILO DE LA MUSICA\n");
        MethodInfo cuando = TB.GetMethod("CuandoEsteLista", T);
        Check(cuando != null, "la bomba sabe avisar cuando tiene el primer segundo (CuandoEsteLista)");
        if (cuando == null) return;
        Lenta src = new Lenta(44100L * 2 * 3);
        object b = Bomba(src);
        int hiloAviso = -1; long hechoAlAvisar = -1; Stopwatch sw = Stopwatch.StartNew(); double avisoMs = -1;
        Action aviso = delegate { hiloAviso = Thread.CurrentThread.ManagedThreadId; hechoAlAvisar = (int)Get(b, "Hecho"); avisoMs = sw.Elapsed.TotalMilliseconds; };
        cuando.Invoke(b, new object[] { aviso });
        double pedir = sw.Elapsed.TotalMilliseconds;
        bool llego = Espera(delegate { return avisoMs >= 0; }, 10000);
        Info("pedirlo: " + F(pedir) + " ms; el aviso, a los " + F(avisoMs) + " ms (la cancion lenta tarda unos 160 ms por segundo)");
        Check(pedir < 50 && llego && avisoMs > pedir, "pedirlo no espera nada; el aviso llega despues, cuando el hilo lo tiene");
        Check(hiloAviso >= 0 && hiloAviso != Thread.CurrentThread.ManagedThreadId && hechoAlAvisar >= 44100 * 2,
              "desde el hilo de la musica y con el segundo entero hecho (" + hechoAlAvisar + " muestras)");
        // La salida arranca: 20 bloques de 50 ms seguidos, como WaveOut.Play. Solo copia.
        float[] bloque = new float[4410]; List<float> sale = new List<float>();
        Stopwatch sp = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) { int n = Lee(b, bloque, bloque.Length); for (int k = 0; k < n; k++) sale.Add(bloque[k]); }
        double play = sp.Elapsed.TotalMilliseconds;
        Info("los 20 bloques del arranque: " + F(play) + " ms");
        Check(play < 50, "y el arranque de la salida solo copia (antes esperaba al hilo ese segundo entero)");
        Thread.Sleep(400);
        int hecho = (int)Get(b, "Hecho");
        Check(hecho <= 44100 * 2 / 5 + 2205 * 2, "sonando, el colchon vuelve a sus 200 ms (" + hecho + " muestras): lo que se lee por delante no cambia");
        Check((int)Get(b, "AdelantoMs") == 162, "y lo que se lee por delante de lo que se oye, el de siempre (" + Get(b, "AdelantoMs") + " ms)");
        int n2; while ((n2 = Lee(b, bloque, bloque.Length)) > 0) for (int k = 0; k < n2; k++) sale.Add(bloque[k]);
        bool igual = sale.Count == 44100 * 2 * 3;
        for (int i = 0; igual && i < sale.Count; i++) if (sale[i] != (float)((i % 1000003) * 1e-6)) igual = false;
        Check(igual, "lo que sale es la cancion entera, en orden y sin un hueco (" + sale.Count + " muestras)");
        ((IDisposable)b).Dispose();

        // Mas corta que un segundo, o que falla antes: el aviso llega igual (y el fallo le llega a la salida al leer).
        Lenta corta = new Lenta(44100L * 2 / 3); object bc = Bomba(corta); bool avisoCorta = false;
        cuando.Invoke(bc, new object[] { (Action)delegate { avisoCorta = true; } });
        Lenta mala = new Lenta(44100L * 2 * 3); mala.FallaEn = 44100L * 2 / 4; object bm = Bomba(mala); bool avisoMala = false;
        cuando.Invoke(bm, new object[] { (Action)delegate { avisoMala = true; } });
        bool ambos = Espera(delegate { return avisoCorta && avisoMala; }, 10000);
        bool falla = false; try { while (Lee(bm, bloque, bloque.Length) > 0) { } } catch (InvalidOperationException) { falla = true; }
        Check(ambos && falla, "una cancion mas corta que un segundo, o que falla antes, avisa igual; y el fallo sale al leer, como antes");
        ((IDisposable)bc).Dispose(); ((IDisposable)bm).Dispose();
        // Parada antes de estar lista: no avisa nunca (la cancion se salto).
        Lenta otra = new Lenta(44100L * 2 * 3); otra.CostoMs = 20; object bp = Bomba(otra); bool avisoParada = false;
        cuando.Invoke(bp, new object[] { (Action)delegate { avisoParada = true; } });
        ((IDisposable)bp).Dispose(); Thread.Sleep(300);
        Check(!avisoParada, "si se para antes de tenerlo, no avisa: esa cancion ya no arranca");
    }

    static void LaRocola()
    {
        Console.WriteLine("\n  2) LA ROCOLA ARRANCA LA SALIDA CUANDO LLEGA EL AVISO\n");
        MethodInfo arranca = TF.GetMethod("ArrancaCuandoEsteLista", T);
        Check(arranca != null, "la rocola sabe arrancar la salida cuando la musica esta lista (ArrancaCuandoEsteLista)");
        if (arranca == null) return;
        object f = Codigo.FormSinEncender(TF);
        List<MethodInvoker> pantalla = new List<MethodInvoker>();
        Set(f, "vuelveALaPantalla", (Action<MethodInvoker>)delegate(MethodInvoker m) { lock (pantalla) pantalla.Add(m); });
        object bomba = Call(f, "ConBomba", new Lenta(44100L * 2 * 3));
        Falsa salida = new Falsa(); Set(f, "output", salida);
        int arrancadas = 0;
        bool pidio = (bool)arranca.Invoke(f, new object[] { (MethodInvoker)delegate { arrancadas++; ((IWavePlayer)Get(f, "output")).Play(); } });
        bool antes; lock (pantalla) antes = pantalla.Count == 0;
        bool vuelve = Espera(delegate { lock (pantalla) return pantalla.Count == 1; }, 10000);
        Check(pidio && antes && vuelve && (bool)Get(bomba, "Lista"), "la salida no arranca en el momento: el aviso vuelve a la pantalla cuando el primer segundo esta hecho");
        MethodInvoker m1; lock (pantalla) { m1 = pantalla[0]; pantalla.Clear(); }
        m1();
        Check(arrancadas == 1 && salida.Plays == 1, "y entonces, en la pantalla, arranca");
        // Se salta la cancion mientras se hace el primer segundo: otra salida y otra bomba. El aviso viejo no arranca nada.
        Lenta lenta = new Lenta(44100L * 2 * 3); lenta.CostoMs = 8;
        Call(f, "ConBomba", lenta); Falsa vieja = new Falsa(); Set(f, "output", vieja);
        int viejas = 0;
        arranca.Invoke(f, new object[] { (MethodInvoker)delegate { viejas++; } });
        Call(f, "ConBomba", new Lenta(44100L * 2 * 3)); Set(f, "output", new Falsa());   // la siguiente
        Thread.Sleep(600);
        lock (pantalla) { foreach (MethodInvoker m in pantalla) m(); pantalla.Clear(); }
        Check(viejas == 0, "si mientras tanto se salto la cancion, el aviso de la vieja no arranca nada");
        Call(f, "SueltaBomba");
        // Sin ventana a la que volver: como antes (Play en el momento).
        object g = Codigo.FormSinEncender(TF);
        Call(g, "ConBomba", new Lenta(44100L * 2)); Set(g, "output", new Falsa());
        Check(!(bool)arranca.Invoke(g, new object[] { (MethodInvoker)delegate { } }), "sin ventana a la que volver, la salida arranca como antes");
        Call(g, "SueltaBomba");
    }

    static void ElArranque()
    {
        Console.WriteLine("\n  3) LA CANCION ARRANCA ASI, Y LO DE DESPUES ES LO DE SIEMPRE\n");
        List<string> arranca = Codigo.Nombres(TF.GetMethod("StartOpenedTrack", T));
        int iAbre = arranca.IndexOf("AbreSalidaClasica"), iPide = arranca.IndexOf("ArrancaCuandoEsteLista"), iPlay = arranca.LastIndexOf("Play");
        Check(iAbre >= 0 && iPide > iAbre && iPlay > iPide, "la cancion abre la salida clasica y pide arrancarla cuando la musica este lista (Play en el momento solo sin ventana)");
        List<string> lista = Codigo.Nombres(TF.GetMethod("SalidaClasicaLista", T));
        string[] orden = { "Play", "SalidaArranco", "Start", "UpdateNowPlayingUI", "PrecalientaSiguiente", "ApuntaCancion" };
        int donde = -1; bool enOrden = true;
        foreach (string s in orden) { int i = lista.IndexOf(s); if (i <= donde) { enOrden = false; break; } donde = i; }
        List<string> todas = Codigo.Llamadas(TF.GetMethod("SalidaClasicaLista", T));
        Check(enOrden && Codigo.Llama(todas, "EsFalloDelDispositivo") && Codigo.Llama(todas, "SalidaCaida") && Codigo.Llama(todas, "AudioStartFailed"),
              "al llegar: Play, apuntar el aparato, la barra, la pantalla, calentar la siguiente y la bitacora; y si el aparato falla, lo de siempre");
    }

    static void LaTablaDeVolumen()
    {
        Console.WriteLine("\n  4) GUARDAR LA TABLA DE VOLUMEN NO REHACE SU INDICE EN LA PANTALLA\n");
        List<string> guarda = Codigo.Llamadas(TF.GetMethod("SaveLoudCache", T));
        bool dic = false; foreach (string s in guarda) if (s.IndexOf("Dictionary`2..ctor", StringComparison.Ordinal) >= 0) dic = true;
        Check(!dic, "en la pantalla solo se copian las dos tablas como listas; el indice de las duraciones se arma al escribir");
    }

    static object Integ(float lk, float pico, float p99) { return Activator.CreateInstance(TS, T, null, new object[] { 2, lk, 2f, 1000, pico, p99 }, null); }
    static void ElTecho()
    {
        Console.WriteLine("\n  5) EL TECHO DE \"SUBIR TODO\", CON MEDIDAS NUEVAS, FUERA DE LA PANTALLA\n");
        if (TF.GetMethod("RehaceTechoEnFondo", T) == null) { Check(false, "con medidas nuevas, el techo se cuenta fuera de la pantalla (RehaceTechoEnFondo)"); return; }
        object f = Codigo.FormSinEncender(TF);
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 6f);
        IDictionary tab = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 60; i++) tab["pop" + i + ".mp3"] = Integ(-7f, 0f, -0.5f);
        for (int i = 0; i < 5; i++) tab["din" + i + ".mp3"] = Integ(-20f, -0.5f, -6f);
        Set(f, "integCache", tab);
        List<MethodInvoker> pantalla = new List<MethodInvoker>();
        Set(f, "vuelveALaPantalla", (Action<MethodInvoker>)delegate(MethodInvoker m) { lock (pantalla) pantalla.Add(m); });
        object[] a = { 0 };
        float t0 = (float)Call(f, "TechoSubirDb", a);   // la primera vez (y con ajustes nuevos): en la pantalla, al momento
        bool alMomento; lock (pantalla) alMomento = pantalla.Count == 0 && !float.IsNaN(t0);
        Check(alMomento, "la primera cuenta (y la de un ajuste de Ctrl+F3) se hace al momento: +" + F(t0) + " dB");
        // Llegan medidas nuevas (35 dinamicas mas) y paso el minuto.
        for (int i = 5; i < 40; i++) tab["din" + i + ".mp3"] = Integ(-20f, -0.5f, -6f);
        Set(f, "integCambios", (long)Get(f, "integCambios") + 35); Set(f, "subirCuando", Environment.TickCount - 61000);
        float mientras = (float)Call(f, "TechoSubirDb", a);
        bool fuera = Espera(delegate { lock (pantalla) return pantalla.Count == 1; }, 10000);
        Check(mientras == t0 && fuera, "con medidas nuevas la pantalla sigue con el de antes y la cuenta va en otro hilo");
        lock (pantalla) { pantalla[0](); pantalla.Clear(); }
        float despues = (float)Call(f, "TechoSubirDb", a);
        object g = Codigo.FormSinEncender(TF);
        Set(g, "normalizeOn", true); Set(g, "normTarget", 0.26f); Set(g, "normStrength", 1f); Set(g, "masterDb", 6f); Set(g, "integCache", tab);
        float enPantalla = (float)Call(g, "TechoSubirDb", a);
        Check(despues == enPantalla && despues != t0, "y al volver pone el mismo techo que daria la cuenta en la pantalla (+" + F(despues) + " dB)");
        // Cambia un ajuste mientras se cuenta con los viejos: lo viejo no se pone.
        for (int i = 40; i < 45; i++) tab["din" + i + ".mp3"] = Integ(-20f, -0.5f, -6f);
        Set(f, "integCambios", (long)Get(f, "integCambios") + 5); Set(f, "subirCuando", Environment.TickCount - 61000);
        Call(f, "TechoSubirDb", a);
        Espera(delegate { lock (pantalla) return pantalla.Count == 1; }, 10000);
        Set(f, "normTarget", 0.2601f);
        float conAjuste = (float)Call(f, "TechoSubirDb", a);   // ajuste nuevo: en la pantalla, al momento
        lock (pantalla) { foreach (MethodInvoker m in pantalla) m(); pantalla.Clear(); }
        Check((float)Call(f, "TechoSubirDb", a) == conAjuste, "si mientras tanto cambia un ajuste, lo contado con el viejo no pisa lo nuevo");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TB = asm.GetType("Rocola.BombaSonido", true); TS = asm.GetType("Rocola.SonoridadIntegrada", true);
        try { ElPrimerSegundo(); LaRocola(); ElArranque(); LaTablaDeVolumen(); ElTecho(); }
        catch (Exception e) { Check(false, "la bateria reviento: " + e); }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

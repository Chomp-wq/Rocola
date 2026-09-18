using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// ABRIR LA SIGUIENTE ANTES DE QUE HAGA FALTA.
//
// Entre una cancion y otra hay un trabajo que nadie ve: abrir el lector. NAudio recorre el MP3 entero
// armando el indice de sus cuadros y monta el descompresor del sistema. Desde el 17/09 eso ya no para la
// pantalla -lo hace un trabajador aparte-, pero SIGUE estando en el camino hasta el primer sonido: la
// cancion no empieza hasta que su lector esta listo.
//
// Ahora se abre por delante, mientras suena la de antes, y cuando le toca ya esta. Lo que vigila esta
// bateria es lo unico que puede salir mal de verdad: QUE SE ENTREGUE UN ARCHIVO POR OTRO, o que un lector
// se quede tirado con el archivo agarrado.
//
// Se mira en tres partes:
//   1. EL MECANISMO, en vivo, con una fuente de mentira -igual que hace 'aperturaaudio' con el
//      trabajador-: entregar solo si la ruta coincide, no abrir dos veces, soltar lo que ya no vale, y
//      no dejar nada tirado cuando la ventana desaparece.
//   2. EL CABLEADO, leyendo el programa ya compilado: que quien abre lo consulte, que el calentador lo
//      pida, que se suelte al cambiar la siguiente y al cerrar, y que el medidor de Ctrl+F3 no mire por
//      delante (alli la apertura tiene que ser a la cara).
//   3. EL NUMERO: lo que cuesta abrir con el archivo ya caliente.
//
// LO QUE NO SE PUEDE MEDIR AQUI: esta maquina no tiene el descompresor de MP3 de Windows (Msacm32), asi
// que de un MP3 solo se puede cronometrar el recorrido de sus cuadros, que es la parte gorda pero no
// toda. El descompresor y la tarjeta de sonido hay que medirlos en el bar.
class Precarga
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    const BindingFlags UNO = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    // Una fuente de mentira: no abre nada, solo se deja contar cuantas veces la sueltan.
    public sealed class Fuente : IDisposable
    {
        public string Ruta;
        public int Sueltas;
        public void Dispose() { Interlocked.Increment(ref Sueltas); }
    }

    // EL CORREO DEL HILO DE LA PANTALLA. En la rocola de verdad esto es BeginInvoke; aqui es una cola que
    // se vacia a mano, para poder mirar el estado entre paso y paso.
    static readonly Queue<Action> correo = new Queue<Action>();
    static readonly AutoResetEvent aviso = new AutoResetEvent(false);
    static void Pon(Action a) { lock (correo) correo.Enqueue(a); aviso.Set(); }
    static bool Reparte(int esperaMs)
    {
        Action a = null;
        int vueltas = Math.Max(1, esperaMs / 10);
        for (int i = 0; i < vueltas && a == null; i++)
        {
            lock (correo) if (correo.Count > 0) a = correo.Dequeue();
            if (a == null) aviso.WaitOne(10, false);
        }
        if (a == null) return false;
        a(); return true;
    }
    static void ReparteTodo() { while (Reparte(300)) { } }

    static Type TM; static object mira;
    static void Pide(string ruta, Func<Fuente> abre)
    { TM.GetMethod("Pide", UNO).Invoke(mira, new object[] { ruta, abre, new Action<Action>(Pon) }); }
    static Fuente Toma(string ruta) { return (Fuente)TM.GetMethod("Toma", UNO).Invoke(mira, new object[] { ruta }); }
    static void Suelta() { TM.GetMethod("Suelta", UNO).Invoke(mira, null); }
    static bool Listo { get { return (bool)TM.GetProperty("Listo", UNO).GetValue(mira, null); } }
    static string Para { get { return (string)TM.GetProperty("Para", UNO).GetValue(mira, null); } }

    [STAThread]
    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type TF = a.GetType("Rocola.Form1", true);
        Type gen = a.GetType("Rocola.MiraPorDelante" + (char)96 + "1", false);
        if (gen == null) { Mal("no esta MiraPorDelante: no se abre nada por delante"); return 1; }
        TM = gen.MakeGenericType(typeof(Fuente));
        mira = Activator.CreateInstance(TM, true);
        try
        {
            Mecanismo();
            Cableado(TF);
            Numero(TF);
        }
        finally { try { Suelta(); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    // ================================================================================================
    //  1) EL MECANISMO
    // ================================================================================================
    static void Mecanismo()
    {
        Console.WriteLine("\n  1) LO QUE SE ABRE POR DELANTE, Y PARA QUIEN\n");
        int abiertas = 0;
        Fuente uno = new Fuente { Ruta = "A.mp3" };
        Pide("A.mp3", delegate { Interlocked.Increment(ref abiertas); return uno; });
        Exige(Reparte(3000), "abre en otro hilo y avisa al de la pantalla");
        Exige(Listo && Para == "A.mp3", "queda guardado, y apuntado para quien es");

        // Pedir lo mismo otra vez no vuelve a abrir.
        Pide("A.mp3", delegate { Interlocked.Increment(ref abiertas); return new Fuente(); });
        Exige(abiertas == 1, "pedir la misma dos veces no la abre dos veces (abrio " + abiertas + ")");

        // LO IMPORTANTE: solo se entrega si la ruta coincide.
        Fuente otra = Toma("B.mp3");
        Exige(otra == null, "pedir OTRA cancion no recibe la que estaba abierta");
        Exige(uno.Sueltas == 1, "y la que estaba abierta se suelta -una sola vez- en vez de quedarse tirada");
        Exige(!Listo && Para == null, "despues de eso no queda nada guardado");

        // La que si coincide, se entrega; y solo una vez.
        Fuente dos = new Fuente { Ruta = "C.mp3" };
        abiertas = 0;
        Pide("C.mp3", delegate { Interlocked.Increment(ref abiertas); return dos; });
        Reparte(3000);
        Fuente tomada = Toma("C.mp3");
        Exige(ReferenceEquals(tomada, dos), "la que SI coincide se entrega");
        Exige(dos.Sueltas == 0, "y se entrega viva: quien la recibe es su dueno");
        Exige(Toma("C.mp3") == null, "no se entrega dos veces la misma");
        dos.Dispose();

        // Mayusculas: la misma cancion escrita distinto sigue siendo la misma.
        Fuente tres = new Fuente();
        Pide("D:\\Musica\\Cancion.MP3", delegate { return tres; });
        Reparte(3000);
        Fuente igual = Toma("d:\\musica\\cancion.mp3");
        Exige(ReferenceEquals(igual, tres), "la misma ruta escrita en mayusculas sigue siendo la misma cancion");
        if (igual != null) igual.Dispose();

        // Cambiar de siguiente suelta la anterior.
        Fuente vieja = new Fuente();
        Pide("E.mp3", delegate { return vieja; });
        Reparte(3000);
        Fuente nueva = new Fuente();
        Pide("F.mp3", delegate { return nueva; });
        ReparteTodo();
        Exige(vieja.Sueltas == 1, "cambiar de siguiente suelta el lector de la anterior");
        Exige(Para == "F.mp3", "y se queda apuntado para la nueva");
        Suelta();
        Exige(nueva.Sueltas == 1, "soltar a mano libera lo guardado");

        // Un fallo al abrir no guarda nada y no revienta: cuando le toque, el camino normal la abre.
        Pide("G.mp3", delegate { throw new IOException("prueba"); });
        ReparteTodo();
        Exige(!Listo, "si falla al abrir por delante no se guarda nada roto");
        Exige(Toma("G.mp3") == null, "y no se entrega nada por ella");

        // LA VENTANA DESAPARECE: nada tirado.
        Fuente huerfana = new Fuente();
        TM.GetMethod("Pide", UNO).Invoke(mira, new object[] { "H.mp3", new Func<Fuente>(delegate { return huerfana; }),
            new Action<Action>(delegate { throw new ObjectDisposedException("ventana"); }) });
        bool soltada = false;
        for (int i = 0; i < 300 && !soltada; i++) { soltada = huerfana.Sueltas == 1; Thread.Sleep(10); }
        Exige(soltada, "si la ventana se fue mientras abria, el lector NO se queda con el archivo agarrado");
        Suelta();
    }

    // ================================================================================================
    //  2) EL CABLEADO, LEIDO DEL PROGRAMA COMPILADO
    // ================================================================================================
    static void Cableado(Type TF)
    {
        Console.WriteLine("\n  2) QUIEN LO PIDE, QUIEN LO USA Y QUIEN LO SUELTA\n");
        MethodInfo abre = TF.GetMethod("BeginAudioOpen", UNO);
        MethodInfo pide = TF.GetMethod("PideLookahead", UNO);
        MethodInfo suelta = TF.GetMethod("SueltaLookahead", UNO);
        MethodInfo cual = TF.GetMethod("RutaQueSonaria", UNO);
        MethodInfo cierra = TF.GetMethod("OnFormClosing", UNO);
        if (abre == null || pide == null || suelta == null || cual == null || cierra == null)
        { Mal("falta alguna pieza del cableado (BeginAudioOpen/PideLookahead/SueltaLookahead/RutaQueSonaria/OnFormClosing)"); return; }

        Exige(Codigo.Llama(abre, "Toma"), "quien abre pregunta primero si ya estaba abierta");
        Exige(Codigo.Llama(pide, "Pide"), "PideLookahead se lo encarga al que mira por delante");
        Exige(Codigo.Llama(suelta, "Suelta"), "SueltaLookahead lo suelta de verdad");

        // El medidor de Ctrl+F3 abre a la cara: necesita la cadena lista antes de medir, y no puede
        // dejar una apertura pendiente. Por eso PideLookahead tiene que mirar esa bandera.
        int lee, esc;
        Codigo.UsosDeCampo(pide, "audioOpeningForMeasurement", out lee, out esc);
        Exige(lee > 0, "el medidor de Ctrl+F3 no mira por delante: alli se abre a la cara");

        // La ruta que se abre por delante es la MISMA cuenta que hace PlayTrack, pero sin sus efectos.
        Exige(Codigo.Llama(pide, "RutaQueSonaria"), "se abre la ruta que de verdad va a sonar, no otra");
        Exige(!Codigo.Llama(cual, "IaRequestJob"),
              "y calcularla no le encarga trabajo a la IA: esta cancion todavia no es la que suena");

        // El calentador: pide cuando la cancion ya esta leida, y suelta cuando la siguiente cambia.
        List<string> calienta = Codigo.ConAyudantes(TF, "PrecalientaSiguiente");
        Exige(Codigo.Llama(calienta, "PideLookahead"), "el calentador de la siguiente pide su lector");
        // CADA VEZ QUE CAMBIA CUAL ES LA SIGUIENTE, HAY QUE SOLTAR. Y son dos casos, no uno: que la
        // siguiente pase a ser otra cancion, y que se quede sin ninguna. Contar las llamadas contra las
        // veces que se apunta cual es la siguiente es lo que hace que los dos esten cubiertos, y que un
        // tercer caso que aparezca manana tambien lo exija.
        MethodInfo precalienta = TF.GetMethod("PrecalientaSiguiente", UNO);
        int leeW, apunta;
        Codigo.UsosDeCampo(precalienta, "warmSongPath", out leeW, out apunta);
        int suelta2 = 0;
        foreach (string n in Codigo.Nombres(precalienta)) if (n == "SueltaLookahead") suelta2++;
        Exige(apunta > 0 && suelta2 >= apunta,
              "lo suelta en los " + apunta + " sitios donde cambia cual es la siguiente (suelta " + suelta2 + ")");
        Exige(Codigo.Llama(cierra, "SueltaLookahead"), "al cerrar la rocola se suelta el archivo");
    }

    // ================================================================================================
    //  3) EL NUMERO
    // ================================================================================================
    static void Numero(Type TF)
    {
        Console.WriteLine("\n  3) LO QUE CUESTA ABRIR, CON EL ARCHIVO YA CALIENTE\n");
        MethodInfo open = TF.GetMethod("OpenAudio", UNO);
        if (open == null) { Mal("no esta OpenAudio"); return; }
        object f = Codigo.FormSinEncender(TF);

        string dir = Path.Combine(Path.GetTempPath(), "rocola-precarga-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string wav = Path.Combine(dir, "prueba.wav");
            EscribeWav(wav, 44100, 2, 44100 * 8);   // 8 segundos, 16 bits, estereo
            Calienta(wav);
            double ms = Cronometra(delegate
            {
                object src = open.Invoke(f, new object[] { wav });
                if (src != null) ((IDisposable)src).Dispose();
            });
            Console.WriteLine("     un WAV de 8 s ya caliente: " + ms.ToString("0.000") + " ms");
            Exige(ms >= 0, "se puede abrir un WAV por el camino de la rocola");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        // Y EL MP3, POR APROXIMACION. Aqui no esta el descompresor de Windows, asi que no se puede abrir
        // de verdad; lo que si se puede es recorrer sus cuadros con la MISMA NAudio, que es lo que hace
        // Mp3FileReader antes de devolver el lector.
        string[] mp3 = Directory.Exists("mp3s") ? Directory.GetFiles("mp3s", "*.mp3") : new string[0];
        if (mp3.Length == 0)
        {
            Bien("sin mp3 de prueba a mano: el recorrido de cuadros no se mide en esta pasada");
            return;
        }
        Array.Sort(mp3);
        int mirados = 0;
        for (int i = 0; i < mp3.Length && mirados < 4; i++)
        {
            long bytes = new FileInfo(mp3[i]).Length;
            if (bytes < 200 * 1024) continue;
            Calienta(mp3[i]);
            int cuadros = 0; double segs = 0;
            string ruta = mp3[i];
            double ms = Cronometra(delegate { cuadros = Cuadros(ruta, out segs); });
            if (cuadros < 100) continue;
            mirados++;
            Console.WriteLine("     " + Path.GetFileName(ruta).PadRight(16) + (cuadros + " cuadros").PadLeft(14)
                              + ("  " + segs.ToString("0") + " s de musica").PadRight(18)
                              + "  ->  " + ms.ToString("0.00") + " ms   (4 min saldrian a "
                              + (ms * 240.0 / Math.Max(1.0, segs)).ToString("0.0") + " ms)");
        }
        Exige(mirados > 0, "se cronometra el recorrido de cuadros de " + mirados + " mp3 (la parte gorda de abrir uno)");
        Bien("y el descompresor de MP3 de Windows NO se mide aqui: eso se mide en el bar");
    }

    static double Cronometra(Action a)
    {
        a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }
    static void Calienta(string ruta)
    {
        byte[] buf = new byte[262144];
        using (FileStream fs = File.OpenRead(ruta)) while (fs.Read(buf, 0, buf.Length) > 0) { }
    }
    static int Cuadros(string ruta, out double segs)
    {
        int n = 0; double dur = 0;
        using (FileStream fs = File.OpenRead(ruta))
            while (true)
            {
                NAudio.Wave.Mp3Frame fr;
                try { fr = NAudio.Wave.Mp3Frame.LoadFromStream(fs); } catch { break; }
                if (fr == null) break;
                n++; if (fr.SampleRate > 0) dur += fr.SampleCount / (double)fr.SampleRate;
            }
        segs = dur; return n;
    }
    static void EscribeWav(string ruta, int hz, int canales, int muestras)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(ruta)))
        {
            int bytesMuestra = 2, datos = muestras * canales * bytesMuestra;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + datos);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)canales); w.Write(hz);
            w.Write(hz * canales * bytesMuestra); w.Write((short)(canales * bytesMuestra)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(datos);
            for (int i = 0; i < muestras * canales; i++) w.Write((short)(Math.Sin(i * 0.03) * 12000));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;

// LO QUE PARA LA PANTALLA CUANDO EMPIEZA UNA CANCION.
//
// El apunte del 17/09 dejo dicho que quedaba trabajo en el hilo de la pantalla al arrancar una cancion:
// "La lectura de etiquetas, la consulta de la cache IA y la inicializacion de salida/DSP". Esta bateria
// es lo que salio de ir a medirlo, pieza por pieza, y lo que hay que vigilar para que siga asi.
//
// EL CAMINO, tal como esta hoy: PlayNext guarda el estado vivo, le lee la etiqueta al MP3, pinta el
// fondo del color del genero y llama a PlayTrack, que mira la cache de IA y arranca la apertura -que
// desde la precarga ya viene hecha de antes-. Todo eso en el hilo de la pantalla.
//
// DE TODO ESO, LO UNICO QUE PUEDE ESPERAR DE VERDAD ES BAJAR EL ESTADO AL DISCO, y esta puesto a
// proposito: es lo que hace que un apagon no le quite el dinero al cliente. Decision del usuario,
// septiembre de 2026. Por eso aqui hay un GUARDIAN: si alguien se lleva esa bajada a disco por delante,
// o se la lleva a un hilo de fondo, esto se pone rojo y dice por que.
//
// Lo demas se mide en cada pasada y sale en la tabla, para que el dia que alguien vuelva a tocar el
// arranque tenga con que comparar en vez de adivinar.
//
// LO QUE NO SE PUEDE MEDIR AQUI, y se dice en la propia salida: la bajada a disco de VERDAD (la llamada
// de Windows no existe en esta maquina, asi que el numero del estado sale SIN ella), la tarjeta de
// sonido, y el disco del bar (este es el de un contenedor).
class Arranque
{
    const BindingFlags EST = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags UNO = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static Type TF;

    // Una fuente de sonido de mentira, para poder montar la cadena de efectos sin abrir nada.
    sealed class FuenteBoba : NAudio.Wave.ISampleProvider
    {
        private readonly NAudio.Wave.WaveFormat f = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public NAudio.Wave.WaveFormat WaveFormat { get { return f; } }
        public int Read(float[] b, int o, int c) { for (int i = 0; i < c; i++) b[o + i] = 0f; return c; }
    }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        Guardianes();
        NadaEspera();
        Tabla();
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    // ================================================================================================
    //  1) LOS GUARDIANES DE LO DECIDIDO
    // ================================================================================================
    static void Guardianes()
    {
        Console.WriteLine("\n  1) EL DINERO DEL CLIENTE SIGUE LLEGANDO AL DISCO ANTES DE SEGUIR\n");
        MethodInfo guarda = TF.GetMethod("SaveState", UNO);
        MethodInfo escribe = TF.GetMethod("EscribeSeguroYa", EST);
        if (guarda == null || escribe == null) { Mal("falta SaveState o EscribeSeguroYa"); return; }

        Exige(Codigo.Llama(guarda, "EscribeSeguro"), "el estado vivo se guarda por el camino seguro");
        Exige(Codigo.Llama(escribe, "FlushFileBuffers"),
              "y ESE camino baja el archivo al disco de verdad, no solo a la memoria de Windows");

        // Y SIGUE SIENDO A LA CARA. Esto no es una manía: es la decision que hace que una moneda que ya
        // entro no se pierda en un apagon. Si algun dia se lleva a un hilo de fondo queda una ventana de
        // unos milisegundos en la que esa moneda no esta en ningun lado. Que se decida a proposito, no
        // por descuido: por eso esto esta aqui.
        Exige(!Codigo.Llama(guarda, "GuardaEnFondo") && !Codigo.Llama(guarda, "QueueUserWorkItem"),
              "y se hace a la cara, no en un hilo de fondo (decision del usuario: primero el dinero)");

        // Al cerrar se espera a los guardados que SI van en fondo (los gordos), que es lo que permite
        // que los demas puedan ir en fondo sin perderse.
        //
        // CADA ROCOLA LE PUSO OTRO NOMBRE a esa barrera -EsperaGuardados() en la de Windows potente,
        // WaitDataWrites(ms) en la de XP-, asi que se preguntan las dos. Preguntar por una sola dejaria
        // a la otra rama sin vigilar y pasando en verde, que es la peor forma de fallar. Es lo mismo que
        // ya hace el ayudante de las pruebas (Codigo.EsperaGuardado) por la misma razon.
        MethodInfo cierra = TF.GetMethod("GuardaTodoAlCerrar", UNO);
        bool espera = cierra != null &&
                      (Codigo.Llama(cierra, "EsperaGuardados") || Codigo.Llama(cierra, "WaitDataWrites"));
        Exige(espera, "al cerrar se espera a los guardados de fondo antes de irse");
    }

    // ================================================================================================
    //  2) NADA MAS DEL CAMINO ESPERA AL DISCO SIN LIMITE
    // ================================================================================================
    static void NadaEspera()
    {
        Console.WriteLine("\n  2) Y NADA MAS DEL CAMINO SE QUEDA ESPERANDO AL DISCO\n");
        MethodInfo carga = TF.GetMethod("LoadCover", UNO);
        if (carga == null) Mal("falta LoadCover");
        else
        {
            // La portada de la que suena se PIDE y se sigue; cuando llega, el fondo se recolorea solo
            // (ApplyArrivedCover). Si alguien la decodificara aqui, el arranque esperaria al disco.
            List<string> ll = Codigo.Nombres(carga);
            Exige(ll.Contains("RequestCover"), "la portada se encarga y se sigue: no se decodifica en el camino");
            Exige(!ll.Contains("DecodeCoverWithIdentity") && !ll.Contains("FromFile"),
                  "y de verdad no se abre ninguna imagen aqui");
        }

        MethodInfo sigue = TF.GetMethod("PlayNext", UNO);
        if (sigue == null) Mal("falta PlayNext");
        else
        {
            List<string> ll = Codigo.Nombres(sigue);
            Exige(!ll.Contains("MeasureFile") && !ll.Contains("MeasureFileResponsive"),
                  "empezar una cancion no la mide: eso es de los trabajadores de fondo");
            Exige(!ll.Contains("OpenAudio"), "ni la abre a la cara: la apertura va por el camino de fondo");
        }

        MethodInfo abre = TF.GetMethod("BeginAudioOpen", UNO);
        Exige(abre != null && Codigo.Llama(abre, "Toma"),
              "y ademas suele venir ya abierta de antes (la precarga de la siguiente)");
    }

    // ================================================================================================
    //  3) LA TABLA
    // ================================================================================================
    static void Tabla()
    {
        Console.WriteLine("\n  3) LO QUE CUESTA CADA PIEZA DEL ARRANQUE\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola-arranque-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        int medidas = 0;
        try
        {
            // --- el estado vivo ---
            MethodInfo escribe = TF.GetMethod("EscribeSeguroYa", EST);
            if (escribe != null)
            {
                string est = Path.Combine(dir, "rocola_estado.txt");
                string texto = "ROCOLA-ESTADO v1\r\nts=638000000000000000\r\ncreditos=3\r\n"
                             + "sonando=S|d:\\musica\\tigres\\puerta negra.mp3\r\n";
                for (int i = 0; i < 8; i++)
                    texto += "cola=S|d:\\musica\\disco" + i + "\\cancion" + i + ".mp3\r\n";
                Fila("guardar el estado vivo", Corre(20, delegate { escribe.Invoke(null, new object[] { est, texto }); }));
                medidas++;
            }

            // --- la etiqueta del MP3 ---
            Type TTrack = null;
            foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
                if (t.Name == "Track") TTrack = t;
            if (TTrack != null)
            {
                MethodInfo lee = TTrack.GetMethod("ReadId3", EST);
                MethodInfo desde = TTrack.GetMethod("FromFile", EST);
                if (lee != null && desde != null)
                {
                    string mp3 = Path.Combine(dir, "con etiqueta.mp3");
                    EscribeMp3ConId3(mp3);
                    object tk = desde.Invoke(null, new object[] { mp3 });
                    Fila("leer la etiqueta del MP3", Corre(50, delegate { lee.Invoke(null, new object[] { mp3, tk }); }));
                    FieldInfo gen = TTrack.GetField("Genre");
                    Exige(gen != null && "Rock".Equals(gen.GetValue(tk)),
                          "y la etiqueta se lee bien: de ella sale el color del fondo");
                    medidas++;
                }
            }

            // --- la cache de IA: un stat y cuatro "existe?" ---
            string cache = Path.Combine(dir, "listos");
            Directory.CreateDirectory(cache);
            string cancion = Path.Combine(dir, "cancion.mp3");
            File.WriteAllBytes(cancion, new byte[4096]);
            Fila("mirar la cache de IA", Corre(200, delegate
            {
                FileInfo fi = new FileInfo(cancion);
                if (!fi.Exists) return;
                long n = fi.Length + fi.LastWriteTimeUtc.Ticks; if (n == 0) return;
                File.Exists(Path.Combine(cache, "0123456789abcdef.error"));
                File.Exists(Path.Combine(cache, "0123456789abcdef.mp3"));
                File.Exists(Path.Combine(cache, "0123456789abcdef.wav"));
                File.Exists(Path.Combine(cache, "0123456789abcdef.orig"));
            }));
            medidas++;

            // --- los colores de la portada (hasta 32x32 puntos de la imagen) ---
            MethodInfo cols = TF.GetMethod("ColoresDePortada", EST);
            if (cols != null)
                using (Bitmap bm = new Bitmap(300, 300))
                {
                    Random r = new Random(3);
                    for (int y = 0; y < 300; y++)
                        for (int x = 0; x < 300; x++)
                            bm.SetPixel(x, y, Color.FromArgb(255, (x * 7 + y) % 256, (y * 5) % 256, r.Next(256)));
                    object[] ar = new object[] { bm, Color.Empty, Color.Empty };
                    Fila("medir los colores de la portada", Corre(20, delegate { cols.Invoke(null, ar); }));
                    medidas++;
                }

            // --- montar la cadena de efectos ---
            double cadena = Cadena();
            if (cadena >= 0) { Fila("montar la cadena de efectos", cadena); medidas++; }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Exige(medidas >= 4, "se miden las piezas del arranque en cada pasada (" + medidas + ")");
        Console.WriteLine();
        Console.WriteLine("     Es una microprueba local, no la latencia hasta oir la cancion.");
        Console.WriteLine(Environment.OSVersion.Platform == PlatformID.Win32NT
            ? "     En Windows incluye la llamada de vaciado; no certifica resistencia a apagones."
            : "     Fuera de Windows no incluye FlushFileBuffers: no extrapolar al disco real.");
        Console.WriteLine("     Hay que medir tambien almacenamiento, codecs y tarjeta de sonido del destino.");
        Bien("y se dice lo que este numero NO incluye, en vez de darlo por bueno");
    }

    static double Cadena()
    {
        object f = Codigo.FormSinEncender(TF);
        Type TLim = null, TBass = null, TDeclip = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "LookaheadLimiter") TLim = t;
            if (t.Name == "DeepBassProvider") TBass = t;
            if (t.Name == "DeclipProvider") TDeclip = t;
        }
        if (TLim == null || TBass == null || TDeclip == null) return -1;
        if (f == null) return -1;
        try
        {
            NAudio.Wave.ISampleProvider fuente = new FuenteBoba();
            return Corre(50, delegate
            {
                object dc = Activator.CreateInstance(TDeclip, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                     null, new object[] { fuente }, null);
                object bs = Activator.CreateInstance(TBass, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                     null, new object[] { dc, 0.5f }, null);
                Activator.CreateInstance(TLim, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                         null, new object[] { bs, 0.98f, 3.0 }, null);
            });
        }
        catch { return -1; }
    }

    static void Fila(string que, double ms)
    { Console.WriteLine("     " + que.PadRight(34) + ms.ToString("0.000").PadLeft(8) + " ms"); }

    static double Corre(int veces, Action a)
    {
        a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    // Un mp3 de mentira con etiqueta ID3v2 de verdad: cabecera 'ID3' y un cuadro TCON con el genero.
    static void EscribeMp3ConId3(string ruta)
    {
        byte[] genero = System.Text.Encoding.ASCII.GetBytes("\0Rock\0");
        int tam = 10 + genero.Length + 200;
        using (BinaryWriter w = new BinaryWriter(File.Create(ruta)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("ID3"));
            w.Write((byte)3); w.Write((byte)0); w.Write((byte)0);
            w.Write((byte)((tam >> 21) & 0x7F)); w.Write((byte)((tam >> 14) & 0x7F));
            w.Write((byte)((tam >> 7) & 0x7F)); w.Write((byte)(tam & 0x7F));
            w.Write(System.Text.Encoding.ASCII.GetBytes("TCON"));
            int n = genero.Length;
            w.Write((byte)(n >> 24)); w.Write((byte)(n >> 16)); w.Write((byte)(n >> 8)); w.Write((byte)n);
            w.Write((short)0);
            w.Write(genero);
            w.Write(new byte[200]);
            w.Write(new byte[8192]);
        }
    }
}

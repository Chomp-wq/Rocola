using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;

// EN LINUX, LA MUSICA TIENE QUE SALIR IGUAL QUE EN WINDOWS.
//
// De toda la rocola, lo unico que le pedia a Windows para sonar eran dos cosas:
//
//   - DESCOMPRIMIR EL MP3. En Windows lo hace el propio Windows. En Linux eso no existe, asi que lo hace
//     NLayer, que descomprime en el mismo lenguaje que la rocola y no necesita nada del sistema.
//   - SACAR EL SONIDO POR LA TARJETA. En Windows es WaveOut o WASAPI; en Linux es ALSA.
//
// Todo lo de en medio -el reconstructor de agudos, el EQ automatico, el nivelado de volumen, el analizador
// que mueve el fondo de estrellas- no cambia: son cuentas sobre muestras.
//
// (Los MP3 de prueba del repositorio no llevan musica de verdad: unos son tramas validas pero en silencio y
// otros son solo cabeceras, fabricados para las pruebas de etiquetas y de frecuencias. Sirven igual para lo
// que se mira aqui, que es que el descompresor lea bien lo que hay y no invente lo que no hay.)
//
// Aqui se comprueban las dos piezas nuevas, y la segunda se comprueba de la unica forma que de verdad vale:
// mandando el sonido a un "dispositivo" de ALSA que en vez de un altavoz es un ARCHIVO, y comparando byte
// por byte lo que llego con lo que la rocola calculo. Si la salida se comiera muestras, las repitiera o las
// cambiara de sitio, se veria aqui.
class Sonido
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }

    // Una fuente de sonido conocida: 16 bits, como lo que la cadena de la rocola le entrega a la tarjeta.
    // Va guardando TODO lo que entrega, para poder comparar despues con lo que llego al otro lado.
    sealed class Tono : IWaveProvider
    {
        private readonly WaveFormat wf;
        private readonly MemoryStream copia = new MemoryStream();
        private int quedan;
        private double fase;
        public Tono(int rate, int ch, int tramas) { wf = new WaveFormat(rate, 16, ch); quedan = tramas; }
        public WaveFormat WaveFormat { get { return wf; } }
        public byte[] LoQueEntregue { get { return copia.ToArray(); } }
        public int Read(byte[] buffer, int offset, int count)
        {
            int bytesPorTrama = wf.Channels * 2;
            int tramas = Math.Min(quedan, count / bytesPorTrama);
            if (tramas <= 0) return 0;
            int p = offset;
            for (int i = 0; i < tramas; i++)
            {
                short v = (short)(Math.Sin(fase) * 12000.0);
                fase += 2.0 * Math.PI * 440.0 / wf.SampleRate;
                for (int c = 0; c < wf.Channels; c++)
                { buffer[p++] = (byte)(v & 0xFF); buffer[p++] = (byte)((v >> 8) & 0xFF); }
            }
            quedan -= tramas;
            int n = tramas * bytesPorTrama;
            copia.Write(buffer, offset, n);
            return n;
        }
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        // =========================================================================================
        Console.WriteLine("\n  1) EL MP3 NO SE LE PIDE AL SISTEMA\n");
        Type TMp3 = asm.GetType("Rocola.Mp3Linux");
        Check(TMp3 != null, "la rocola trae su propio descompresor de MP3");
        string[] donde = { "LectorMedida", "FileAudioSource", "TolerantMp3Source" };
        string[] paraQue = { "para medir la biblioteca", "para la cancion que suena", "para un MP3 remendado" };
        for (int i = 0; i < donde.Length; i++)
        {
            Type t = Nes(donde[i]);
            List<string> hace = new List<string>();
            if (t != null)
                foreach (ConstructorInfo c in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    hace.AddRange(Codigo.Llamadas(c));
            Check(t != null && Codigo.Llama(hace, "Mp3Linux..ctor"), paraQue[i] + ", el MP3 lo abre el descompresor propio");
            Check(t != null && !Codigo.Llama(hace, "Mp3FileReader..ctor"), "   ...y ya no el de Windows, que aqui no existe");
        }

        // =========================================================================================
        Console.WriteLine("\n  2) Y LO DESCOMPRIME BIEN\n");
        string[] mp3s = Directory.Exists("mp3s") ? Directory.GetFiles("mp3s", "*.mp3") : new string[0];
        Array.Sort(mp3s);
        Check(mp3s.Length > 0, "hay MP3 de prueba con los que comprobarlo");
        int buenos = 0, rotos = 0, vacios = 0;
        foreach (string f in mp3s)
        {
            object m = null;
            try { m = Activator.CreateInstance(TMp3, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { f }, null); }
            catch { rotos++; continue; }        // los hay rotos a proposito, y tienen que quejarse en vez de mentir
            try
            {
                WaveFormat wf = (WaveFormat)TMp3.GetProperty("WaveFormat").GetValue(m, null);
                TimeSpan dur = (TimeSpan)TMp3.GetProperty("TotalTime").GetValue(m, null);
                MethodInfo mRead = TMp3.GetMethod("Read");
                float[] b = new float[1 << 15];
                long total = 0;
                for (; ; ) { int n = (int)mRead.Invoke(m, new object[] { b, 0, b.Length }); if (n <= 0) break; total += n; }
                double segLeidos = (wf.SampleRate > 0 && wf.Channels > 0) ? (double)total / wf.SampleRate / wf.Channels : 0;
                bool cuadra = wf.SampleRate >= 8000 && wf.Channels >= 1 && dur.TotalSeconds > 0.1
                              && Math.Abs(segLeidos - dur.TotalSeconds) < 0.5;
                if (cuadra) buenos++;
                else if (total == 0) vacios++;   // cabeceras sin nada dentro: fabricados para otras pruebas
                else Console.WriteLine("     " + Path.GetFileName(f) + ": dice durar " + dur.TotalSeconds.ToString("0.00")
                                       + "s y entrega " + segLeidos.ToString("0.00") + "s");
            }
            finally { ((IDisposable)m).Dispose(); }
        }
        Console.WriteLine("     " + mp3s.Length + " archivos: " + buenos + " se descomprimen enteros, " + rotos
                          + " se rechazan por rotos y " + vacios + " son solo cabeceras (hechos para otras pruebas)");
        Check(buenos >= 10, "descomprime los MP3 de verdad, y lo que entrega dura lo que dice durar");
        Check(rotos > 0, "y los que estan rotos los rechaza en vez de entregar ruido");

        // Y saber ir a un punto de la cancion, que es lo que usa el repaso de la biblioteca para no medir
        // siempre el principio.
        if (buenos > 0)
        {
            string f = null;
            foreach (string x in mp3s) if (Path.GetFileName(x) == "cbr128.mp3") f = x;
            if (f == null) f = mp3s[0];
            object m = Activator.CreateInstance(TMp3, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { f }, null);
            try
            {
                PropertyInfo pT = TMp3.GetProperty("CurrentTime");
                pT.SetValue(m, TimeSpan.FromSeconds(30), null);
                double quedo = ((TimeSpan)pT.GetValue(m, null)).TotalSeconds;
                Console.WriteLine("     se le pide ir al segundo 30 y queda en el " + quedo.ToString("0.00"));
                Check(Math.Abs(quedo - 30) < 1.0, "y sabe saltar a un punto de la cancion");
            }
            finally { ((IDisposable)m).Dispose(); }
        }

        // =========================================================================================
        Console.WriteLine("\n  3) Y LO QUE LLEGA A LA TARJETA ES EXACTAMENTE LO QUE LA ROCOLA CALCULO\n");
        // ALSA deja mandar el sonido a un archivo en vez de a un altavoz. Asi se puede comprobar byte por
        // byte, sin tarjeta de sonido de por medio.
        Type TSal = asm.GetType("Rocola.SalidaAlsa");
        Check(TSal != null, "la rocola trae su salida de sonido para Linux");
        Check(TSal != null && typeof(IWavePlayer).IsAssignableFrom(TSal), "y es una salida como las de siempre (la cadena no se entera)");
        if (TSal == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        string crudo = Path.Combine(Path.GetTempPath(), "rocola_salida.raw");
        try { File.Delete(crudo); } catch { }
        string disp = "plug:\"file:FILE='" + crudo + "',FORMAT=raw\"";

        const int RATE = 44100, CH = 2, TRAMAS = RATE / 2;   // medio segundo
        Tono tono = new Tono(RATE, CH, TRAMAS);
        object salida = Activator.CreateInstance(TSal, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                 null, new object[] { 200, disp }, null);
        bool aviso = false;
        EventInfo ev = TSal.GetEvent("PlaybackStopped");
        ev.AddEventHandler(salida, new EventHandler<StoppedEventArgs>(delegate(object s, StoppedEventArgs e) { aviso = true; }));
        Exception malo = null;
        try
        {
            TSal.GetMethod("Init").Invoke(salida, new object[] { tono });
            TSal.GetMethod("Play").Invoke(salida, null);
            for (int i = 0; i < 200 && !aviso; i++) Thread.Sleep(25);
        }
        catch (TargetInvocationException tie) { malo = tie.InnerException; }
        catch (Exception ex) { malo = ex; }
        finally { try { ((IDisposable)salida).Dispose(); } catch { } }

        if (malo != null) Console.WriteLine("     la salida se quejo: " + malo.GetType().Name + ": " + malo.Message);
        Check(malo == null, "la salida arranca y entrega el sonido sin quejarse");
        Check(aviso, "y AVISA cuando se acaba la cancion (ese aviso es lo que mete la siguiente)");

        byte[] entregado = tono.LoQueEntregue;
        byte[] llegado = File.Exists(crudo) ? File.ReadAllBytes(crudo) : new byte[0];
        Console.WriteLine("     la rocola calculo " + entregado.Length + " bytes y a la tarjeta llegaron " + llegado.Length);
        Check(llegado.Length == entregado.Length, "llega TODO lo que se calculo: ni una muestra de menos ni de mas");
        int dif = 0;
        for (int i = 0; i < Math.Min(llegado.Length, entregado.Length); i++) if (llegado[i] != entregado[i]) dif++;
        Console.WriteLine("     bytes distintos: " + dif);
        Check(dif == 0, "y llega IGUAL: la salida no toca ni una muestra");

        try { File.Delete(crudo); } catch { }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

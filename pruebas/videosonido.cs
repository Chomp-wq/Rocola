using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL NIVELADOR DE VOLUMEN DE LOS VIDEOS.
//
// En la rocola de Windows los videos ya se nivelaban: el pre-analisis medía lo fuerte que suena cada
// uno y la rocola le ponía el volumen que le toca, para que un video no reviente despues de una
// cancion ni se quede en un susurro. Eso importa porque el sonido del video NO pasa por la cadena de
// audio de la rocola -lo saca el reproductor por su cuenta-, asi que el nivelado normal no le llega.
//
// Todo eso -medir, guardar, emparejar, la perilla de Ctrl+F3- ya estaba escrito en esta rama y
// probado. Lo unico que faltaba era alguien que supiera SACAR EL SONIDO de dentro de un video, que en
// Windows lo hacia Media Foundation y en Linux no existe.
//
// Lo que se comprueba aqui es lo unico que puede salir mal sin avisar: que ese sonido sea EL MISMO
// que el del video. Si saliera mas bajo, mas alto, a otra velocidad o con un canal de menos, la
// rocola mediria mal y les pondria a los videos un volumen equivocado para siempre, sin que nada
// diera error. Por eso se compara contra el MISMO tono medido por fuera.
class VideoSonido
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static bool Hay(string prog)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(prog, "--version");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            Process p = Process.Start(psi); p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(4000);
            return true;
        }
        catch { return false; }
    }

    static bool Corre(string prog, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(prog, args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            Process p = Process.Start(psi); p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit(120000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    // Lo fuerte que suena un trozo de sonido, igual que lo mide la rocola: la raiz de la media de los
    // cuadrados. Es lo que decide el volumen que se le pone.
    static double Fuerza(float[] v, int n)
    {
        double s = 0; for (int i = 0; i < n; i++) s += (double)v[i] * v[i];
        return (n > 0) ? Math.Sqrt(s / n) : 0;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);

        Console.WriteLine("\n  1) LA ROCOLA SABE QUE HACE FALTA PARA LOS VIDEOS\n");
        Type TV = asm.GetType("Rocola.Video", false);
        Type TS = asm.GetType("Rocola.SonidoDeVideo", false);
        Check(TV != null, "la rocola sabe mirar si estan los programas de video");
        Check(TS != null, "y sabe sacar el sonido de dentro de un video");
        if (TV == null || TS == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        bool hayFf = Hay("ffmpeg");
        Console.WriteLine("     en esta maquina: ffmpeg " + (hayFf ? "si" : "NO") + ", mpv " + (Hay("mpv") ? "si" : "NO"));
        bool loDice = (bool)TV.GetProperty("HayMedidor").GetValue(null, null);
        Check(loDice == hayFf, "y lo que dice la rocola de si esta ffmpeg es lo que hay de verdad");
        if (!hayFf)
        {
            Console.WriteLine("     (sin ffmpeg no se puede probar la medida; el resto se salta)");
            Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
            return fallos;
        }

        Console.WriteLine("\n  2) EL SONIDO QUE SACA ES EL DEL VIDEO, NI MAS FUERTE NI MAS FLOJO\n");
        // Se fabrican DOS archivos con el MISMO tono: un video y un wav suelto. Si la rocola mide lo
        // mismo en los dos, es que el sonido llega intacto.
        string dir = Path.Combine(Path.GetTempPath(), "rocola_videosonido");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        string vid = Path.Combine(dir, "prueba.mp4"), wav = Path.Combine(dir, "prueba.wav");
        // Un tono de 440 Hz a media fuerza, cuatro segundos, en estereo a 44100.
        string fuente = "-f lavfi -i \"sine=frequency=440:sample_rate=44100:duration=4\" -af \"volume=0.5,pan=stereo|c0=c0|c1=c0\"";
        // El wav de referencia va en 16 bits, que es lo que sabe leer el lector de siempre de la rocola.
        // Ese lector es un camino INDEPENDIENTE del de ffmpeg: si los dos dan lo mismo, es que el sonido
        // llega intacto y no que los dos se equivocan igual.
        bool ok1 = Corre("ffmpeg", "-v quiet -y " + fuente + " -c:a pcm_s16le \"" + wav + "\"");
        bool ok2 = Corre("ffmpeg", "-v quiet -y -f lavfi -i \"color=c=black:size=160x120:rate=15:duration=4\" "
                                 + fuente.Replace("-f lavfi -i", "-f lavfi -i") + " -c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -shortest \"" + vid + "\"");
        Check(ok1 && ok2 && File.Exists(vid) && File.Exists(wav), "se pudieron fabricar el video y el wav de prueba");
        if (!ok1 || !ok2) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        // El sonido SACADO DEL VIDEO, por el camino de la rocola.
        object lector = Activator.CreateInstance(TS, new object[] { vid });
        MethodInfo mRead = TS.GetMethod("Read");
        float[] buf = new float[44100 * 2];
        double fuerzaVideo = 0; long tot = 0; int vueltas = 0;
        while (vueltas < 8)
        {
            int n = (int)mRead.Invoke(lector, new object[] { buf, 0, buf.Length });
            if (n <= 0) break;
            fuerzaVideo += Fuerza(buf, n) * n; tot += n; vueltas++;
        }
        ((IDisposable)lector).Dispose();
        fuerzaVideo = (tot > 0) ? fuerzaVideo / tot : 0;

        // Y el MISMO tono, leido del wav suelto.
        double fuerzaWav = 0; long tot2 = 0;
        using (AudioFileReader r = new AudioFileReader(wav))
        {
            float[] b2 = new float[44100 * 2];
            for (int i = 0; i < 8; i++)
            {
                int n = r.Read(b2, 0, b2.Length);
                if (n <= 0) break;
                fuerzaWav += Fuerza(b2, n) * n; tot2 += n;
            }
        }
        fuerzaWav = (tot2 > 0) ? fuerzaWav / tot2 : 0;

        Console.WriteLine("     sacado del video: " + fuerzaVideo.ToString("0.0000") + "   del wav suelto: " + fuerzaWav.ToString("0.0000"));
        Console.WriteLine("     muestras: " + tot + " del video, " + tot2 + " del wav");
        // Este suelo esta solo para cazar el silencio: si del video no saliera nada, la comparacion de
        // abajo daria "iguales" (cero contra cero) y pasaria sin haber probado nada. El tono de prueba
        // mide ~0.044, asi que con 0.01 sobra para distinguir sonido de silencio.
        Check(fuerzaVideo > 0.01, "del video sale sonido de verdad (si no, lo de abajo no probaria nada)");
        double razon = (fuerzaWav > 0) ? fuerzaVideo / fuerzaWav : 0;
        Console.WriteLine("     el del video suena " + razon.ToString("0.000") + " veces lo que el wav");
        // Un 6% de margen: el video lleva el sonido comprimido (AAC) y eso mueve un poco la cuenta.
        Check(razon > 0.94 && razon < 1.06,
              "el sonido del video se mide IGUAL que el mismo tono suelto (si no, los videos quedarian a otro volumen)");

        Console.WriteLine("\n  3) Y NO SE QUEDA CORTO\n");
        // Si el lector se rindiera a la primera lectura corta, mediria solo el principio del video y el
        // volumen saldria de un trozo, no de la cancion entera.
        double segundos = (double)tot / (44100.0 * 2);
        Console.WriteLine("     saco " + segundos.ToString("0.0") + " s de un video de 4.0 s");
        Check(segundos > 3.5, "lee el video entero, no solo el principio");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}

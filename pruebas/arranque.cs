using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// CUANTO TARDA LA ROCOLA DESDE QUE ARRANCA HASTA QUE SUENA.
//
//     mono arranque.exe [ejecutable]
//
// De "se enciende la maquina y suena la primera cancion" hay dos mitades, y solo una es nuestra:
//
//   - LA DE PUPPY: encender, el arranque del sistema, y que aparezca la pantalla. Eso no depende de
//     la rocola y solo se puede medir en la maquina de verdad (el guion de instalacion lo apunta).
//   - LA NUESTRA: desde que la rocola arranca hasta que la primera muestra llega a la tarjeta. Eso
//     es lo que se mide aqui, y es lo unico que podemos mejorar.
//
// COMO SE MIDE QUE "SUENA". No vale mirar la pantalla: la rocola puede decir que esta sonando un
// segundo antes de que salga el primer sonido por la bocina. Se le manda la salida al archivo de
// ALSA -el mismo truco de la bateria 'sonido'- y se mira CUANDO LLEGA LA PRIMERA MUESTRA a la
// tarjeta. Eso ya no se puede discutir.
//
// Y POR QUE SUENA SOLA. Porque se le deja un "rocola_estado.txt" con una cancion, que es lo que la
// rocola guarda cuando esta sonando algo. Al arrancar lo encuentra y REANUDA -creditos y cola- sin
// que nadie toque un boton. O sea que esto no es un truco de laboratorio: es exactamente lo que pasa
// en el bar cuando vuelve la luz despues de un apagon.
//
// Se mide dos veces:
//   EN FRIO      sin el indice de la biblioteca. Es el primer arranque, cuando hay que leer el disco
//                entero. Pasa una vez.
//   EN CALIENTE  con el indice ya hecho. Es como arranca todos los demas dias.
class Arranque
{
    static string[] Wavs()
    {
        if (!Directory.Exists("wav")) return new string[0];
        string[] w = Directory.GetFiles("wav", "*.wav"); Array.Sort(w); return w;
    }

    // Deja una biblioteca de prueba y devuelve la ruta de la cancion que va a sonar.
    static string Prepara(string dir, string exe, int discos, int porDisco)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        File.Copy(exe, Path.Combine(dir, "rocola.exe"));
        foreach (string d in Directory.GetFiles(".", "*.dll")) File.Copy(d, Path.Combine(dir, Path.GetFileName(d)), true);

        string[] w = Wavs();
        string primera = null;
        for (int i = 0; i < discos; i++)
        {
            string d = Path.Combine(Path.Combine(dir, "musica"), "Disco " + (i + 1));
            Directory.CreateDirectory(d);
            for (int k = 0; k < porDisco; k++)
            {
                string dest = Path.Combine(d, "Cancion " + (k + 1) + ".wav");
                File.Copy(w[k % w.Length], dest, true);
                if (primera == null) primera = dest;
            }
        }
        File.WriteAllText(Path.Combine(dir, "rocola_teclas.txt"),
            "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=8\r\nmoneda=32\r\nsaltar=9\r\n");
        return primera;
    }

    // El estado que deja un apagon: una cancion sonando y un credito.
    static void DejaEstado(string dir, string cancion)
    {
        File.WriteAllText(Path.Combine(dir, "rocola_estado.txt"),
            "ROCOLA-ESTADO v1\r\nts=" + DateTime.Now.Ticks + "\r\ncreditos=1\r\nsonando=S|" + cancion + "\r\n");
    }

    class Tiempos { public double Pantalla = -1, Biblioteca = -1, Sonido = -1; public bool YaEstaba; public bool Murio; public string Dijo = ""; }

    static Tiempos Mide(string dir, string crudo, double tope)
    {
        Tiempos t = new Tiempos();
        string indice = Path.Combine(dir, "rocola_biblioteca.txt");
        // Si el indice ya estaba de antes no hay nada que cronometrar en el: se dice y punto, en vez de
        // apuntar un 0.00 que no significa nada.
        t.YaEstaba = Largo(indice) > 0;
        try { File.Delete(crudo); } catch { }

        ProcessStartInfo psi = new ProcessStartInfo("mono", "\"" + Path.Combine(dir, "rocola.exe") + "\"");
        psi.WorkingDirectory = dir;
        psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        Stopwatch sw = Stopwatch.StartNew();
        Process p = Process.Start(psi);

        // OJO CON MEDIR CARO. Mirar la pantalla es leerla entera y recorrer sus pixeles, y eso tarda mas
        // que el propio hueco entre una mirada y la siguiente: si se hiciera en cada vuelta, el reloj
        // avanzaria por culpa de la medicion y los tres tiempos saldrian tarde y pegados entre si (paso, y
        // por eso los primeros numeros no valian). Lo barato -mirar cuanto ocupan dos archivos- se hace en
        // cada vuelta; lo caro, cada cuarto de segundo y SOLO hasta que aparece la ventana.
        Rectangle pant = Screen.PrimaryScreen.Bounds;
        double proximaMirada = 0;
        while (sw.Elapsed.TotalSeconds < tope)
        {
            if (p.HasExited) { t.Murio = true; break; }
            double ahora = sw.Elapsed.TotalSeconds;
            if (t.Biblioteca < 0 && Largo(indice) > 0) t.Biblioteca = ahora;
            if (t.Sonido < 0 && Largo(crudo) > 0) t.Sonido = ahora;
            if (t.Pantalla < 0 && ahora >= proximaMirada)
            {
                if (HayVentana(pant)) t.Pantalla = sw.Elapsed.TotalSeconds;
                else proximaMirada = sw.Elapsed.TotalSeconds + 0.25;
            }
            if (t.Pantalla >= 0 && t.Biblioteca >= 0 && t.Sonido >= 0) break;
            System.Threading.Thread.Sleep(10);
        }
        if (t.Murio) { try { t.Dijo = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); } catch { } }
        try { if (!p.HasExited) { p.Kill(); p.WaitForExit(4000); } } catch { }
        return t;
    }

    static long Largo(string f) { try { FileInfo i = new FileInfo(f); return i.Exists ? i.Length : 0; } catch { return 0; } }

    // ¿Hay algo dibujado en la pantalla? El escritorio de la pantalla de pruebas es negro puro.
    static bool HayVentana(Rectangle pant)
    {
        try
        {
            using (Bitmap b = new Bitmap(pant.Width, pant.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, pant.Size);
                for (int y = pant.Height / 4; y < pant.Height * 3 / 4; y += 53)
                    for (int x = pant.Width / 4; x < pant.Width * 3 / 4; x += 53)
                    { Color c = b.GetPixel(x, y); if (c.R != 0 || c.G != 0 || c.B != 0) return true; }
            }
        }
        catch { }
        return false;
    }

    static void Escribe(string cual, Tiempos t)
    {
        Console.WriteLine("  " + cual);
        if (t.Murio)
        {
            Console.WriteLine("      la rocola se murio antes de terminar:");
            foreach (string l in t.Dijo.Replace("\r", "").Split('\n')) if (l.Trim().Length > 0) Console.WriteLine("        " + l);
            return;
        }
        Console.WriteLine("      se ve la pantalla         " + Seg(t.Pantalla));
        Console.WriteLine("      la biblioteca esta lista  " + (t.YaEstaba ? "(el indice ya estaba hecho)" : Seg(t.Biblioteca)));
        Console.WriteLine("      SUENA la primera cancion  " + Seg(t.Sonido));
    }
    static string Seg(double s) { return (s < 0) ? "(no llego)" : s.ToString("0.00") + " s"; }

    static int Main(string[] argv)
    {
        string exe = Path.GetFullPath((argv.Length > 0) ? argv[0] : "rocola.exe");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_arranque");
        string crudo = Path.Combine(Path.GetTempPath(), "rocola_arranque.raw");

        Console.WriteLine();
        Console.WriteLine("=== DE ARRANCAR LA ROCOLA A QUE SUENE ===");
        Console.WriteLine();

        const int Discos = 60, PorDisco = 12;
        string cancion = Prepara(dir, exe, Discos, PorDisco);
        if (cancion == null) { Console.WriteLine("  hacen falta los wav de prueba (pruebas/wav)"); return 1; }
        int cuantas = Directory.GetFiles(Path.Combine(dir, "musica"), "*.wav", SearchOption.AllDirectories).Length;
        Console.WriteLine("  biblioteca de prueba: " + cuantas + " canciones en " + Discos + " discos");
        Console.WriteLine("  (la salida va al archivo de ALSA: se mide cuando llega la PRIMERA MUESTRA a la tarjeta)");
        Console.WriteLine();

        // La salida a un archivo, para saber cuando sale el primer sonido de verdad.
        File.WriteAllText(Path.Combine(dir, "rocola_config.txt"),
            "salida=plug:\"file:FILE='" + crudo + "',FORMAT=raw\"\r\n");
        DejaEstado(dir, cancion);

        Tiempos frio = Mide(dir, crudo, 90);
        Escribe("EN FRIO      (primer arranque: hay que leer el disco entero)", frio);
        Console.WriteLine();

        // En caliente: el indice ya esta, y se le vuelve a dejar el estado (el arranque anterior lo borro
        // o lo consumio) para que vuelva a reanudar sola.
        DejaEstado(dir, cancion);
        Tiempos caliente = Mide(dir, crudo, 90);
        Escribe("EN CALIENTE  (todos los demas dias: el indice ya esta hecho)", caliente);
        Console.WriteLine();

        if (frio.Sonido > 0 && caliente.Sonido > 0)
        {
            double ahorro = frio.Sonido - caliente.Sonido;
            Console.WriteLine("  guardar el indice de la biblioteca ahorra " + ahorro.ToString("0.00")
                              + " s de espera en cada arranque:");
            Console.WriteLine("  el primer dia tarda " + frio.Sonido.ToString("0.00")
                              + " s en sonar, y todos los demas " + caliente.Sonido.ToString("0.00") + " s.");
        }
        Console.WriteLine();
        Console.WriteLine("  DOS AVISOS PARA LEER ESTOS NUMEROS:");
        Console.WriteLine("   - Aqui la musica son archivos chicos en la memoria de esta maquina. En el bar son");
        Console.WriteLine("     miles de MP3 en un disco que gira, y ahi la primera lectura cuesta MUCHO mas.");
        Console.WriteLine("     Por eso conviene correr esto en la maquina de la rocola, con su musica.");
        Console.WriteLine("   - Y hay que sumarle lo que tarde Puppy en encender y sacar la pantalla, que no");
        Console.WriteLine("     depende de la rocola y solo se mide alli.");
        Console.WriteLine();
        return 0;
    }
}

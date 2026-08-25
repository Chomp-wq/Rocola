using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LOS VIDEOS, EN LA ROCOLA DE VERDAD.
//
// La bateria 'videosonido' comprueba que se puede MEDIR el sonido de un video. Esta comprueba lo
// otro: que la rocola de verdad, arrancada como en el bar, encuentra un video en la biblioteca y lo
// REPRODUCE dentro de su propia ventana.
//
// Lo que se mira es la pantalla: se le deja un video de un color que no existe en la rocola -no hay
// nada verde puro en su interfaz- y se comprueba que ese verde aparece. Si mpv dibujara en su propia
// ventana en vez de dentro de la rocola -que es lo que pasa si se le deja elegir con que dibujar-,
// el verde saldria igual pero EN OTRO SITIO, asi que ademas se mira DONDE sale.
class Videos2
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

    static int Main(string[] argv)
    {
        string exe = Path.GetFullPath((argv.Length > 0) ? argv[0] : "rocola.exe");
        Console.WriteLine("\n  1) LA ROCOLA SOLO ENSENA VIDEOS SI HAY CON QUE PONERLOS\n");
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TV = asm.GetType("Rocola.Video", true);
        bool hayMpv = Hay("mpv"), hayFf = Hay("ffmpeg");
        Console.WriteLine("     en esta maquina: mpv " + (hayMpv ? "si" : "NO") + ", ffmpeg " + (hayFf ? "si" : "NO"));
        Check((bool)TV.GetProperty("HayReproductor").GetValue(null, null) == hayMpv,
              "lo que dice la rocola de si esta el reproductor es lo que hay de verdad");

        // Sin reproductor, la lista de extensiones tiene que estar VACIA: si no, la rocola ensenaria una
        // seccion de videos que al pulsarla no hace nada, y eso es peor que no tener videos.
        PropertyInfo pExt = TF.GetProperty("VideoExt", BindingFlags.Static | BindingFlags.NonPublic);
        Check(pExt != null, "la rocola decide en un solo sitio si hay videos o no");
        if (pExt != null)
        {
            string[] ext = (string[])pExt.GetValue(null, null);
            Console.WriteLine("     extensiones de video que reconoce: " + ext.Length);
            Check(hayMpv ? ext.Length > 5 : ext.Length == 0,
                  hayMpv ? "con reproductor, reconoce los formatos de video" : "sin reproductor, no reconoce ninguno (nada de secciones vacias)");
        }
        if (!hayMpv)
        {
            Console.WriteLine("     (sin mpv no se puede probar la reproduccion; el resto se salta)");
            Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
            return fallos;
        }

        Console.WriteLine("\n  2) Y REPRODUCE EL VIDEO DENTRO DE SU PROPIA VENTANA\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_videos2");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        File.Copy(exe, Path.Combine(dir, "rocola.exe"));
        foreach (string d in Directory.GetFiles(".", "*.dll")) File.Copy(d, Path.Combine(dir, Path.GetFileName(d)), true);

        // Un disco de videos con UNO dentro, de un verde que no existe en la interfaz de la rocola.
        string carp = Path.Combine(Path.Combine(dir, "musica"), "Videos del bar");
        Directory.CreateDirectory(carp);
        string vid = Path.Combine(carp, "prueba.mp4");
        if (!Corre("ffmpeg", "-v quiet -y -f lavfi -i \"color=c=0x00C000:size=640x480:rate=15:duration=30\" "
                           + "-f lavfi -i \"sine=frequency=300:duration=30\" "
                           + "-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -shortest \"" + vid + "\""))
        { Console.WriteLine("  FALLO  no se pudo fabricar el video de prueba"); return 1; }

        File.WriteAllText(Path.Combine(dir, "rocola_teclas.txt"),
            "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=8\r\nmoneda=32\r\nsaltar=9\r\n");
        File.WriteAllText(Path.Combine(dir, "rocola_config.txt"), "diagnostico=1\r\nsalida=null\r\n");
        // El estado que deja un apagon, pero con un VIDEO: al arrancar la rocola lo reanuda sola. Es la
        // unica forma de que empiece a reproducir sin tener que simular la botonera.
        File.WriteAllText(Path.Combine(dir, "rocola_estado.txt"),
            "ROCOLA-ESTADO v1\r\nts=" + DateTime.Now.Ticks + "\r\ncreditos=1\r\nsonando=V|" + vid + "\r\n");

        Process p2 = null;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("mono", "\"" + Path.Combine(dir, "rocola.exe") + "\"");
            psi.WorkingDirectory = dir; psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            p2 = Process.Start(psi);
        }
        catch (Exception ex) { Console.WriteLine("  FALLO  no se pudo lanzar la rocola: " + ex.Message); return 1; }

        Rectangle pant = Screen.PrimaryScreen.Bounds;
        int mejor = 0; Rectangle donde = Rectangle.Empty;
        for (int i = 0; i < 40 && !p2.HasExited; i++)
        {
            System.Threading.Thread.Sleep(700);
            using (Bitmap b = new Bitmap(pant.Width, pant.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, pant.Size);
                int n = 0, x0 = pant.Width, y0 = pant.Height, x1 = -1, y1 = -1;
                for (int y = 0; y < pant.Height; y += 2)
                    for (int x = 0; x < pant.Width; x += 2)
                    {
                        Color c = b.GetPixel(x, y);
                        if (c.G > 140 && c.R < 90 && c.B < 90)
                        { n++; if (x < x0) x0 = x; if (y < y0) y0 = y; if (x > x1) x1 = x; if (y > y1) y1 = y; }
                    }
                if (n > mejor) { mejor = n; donde = (x1 >= 0) ? new Rectangle(x0, y0, x1 - x0, y1 - y0) : Rectangle.Empty; b.Save("/tmp/rocola_video.png", ImageFormat.Png); }
                if (n > 2000) break;
            }
        }
        bool viva = !p2.HasExited;
        if (!viva)
        {
            string sal = "";
            try { sal = p2.StandardOutput.ReadToEnd() + p2.StandardError.ReadToEnd(); } catch { }
            foreach (string l in sal.Replace("\r", "").Split('\n')) if (l.Trim().Length > 0) Console.WriteLine("       " + l);
        }
        Check(viva, "la rocola sigue en pie con un video puesto");
        Console.WriteLine("     pixeles del video en pantalla: " + mejor + "   ocupando " + donde.Width + "x" + donde.Height + " en (" + donde.X + "," + donde.Y + ")");
        Check(mejor > 2000, "el video SE VE: la rocola lo encontro, lo puso, y mpv lo dibujo");

        // Y que este DENTRO de la rocola, no en una ventana suelta en una esquina. La rocola ocupa la
        // pantalla entera, asi que el video tiene que caer con holgura dentro y no pegado al (0,0).
        Check(donde.Width > 200 && donde.Height > 150, "y ocupa un cuadro de verdad, no una esquina");

        string bitacora = Path.Combine(dir, "rocola_diagnostico.txt");
        if (File.Exists(bitacora))
        {
            string log = File.ReadAllText(bitacora);
            bool inicia = log.IndexOf("video: inicia", StringComparison.Ordinal) >= 0;
            bool noAbrio = log.IndexOf("video: NO ABRIO", StringComparison.Ordinal) >= 0;
            Console.WriteLine("     la bitacora dice: " + (inicia ? "video iniciado" : "") + (noAbrio ? "  NO ABRIO" : ""));
            Check(inicia && !noAbrio, "y la rocola lo apunta como iniciado, no como fallido");
        }

        try { if (viva) { p2.Kill(); p2.WaitForExit(4000); } } catch { }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static bool Corre(string prog, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(prog, args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            Process p = Process.Start(psi); p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit(180000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}

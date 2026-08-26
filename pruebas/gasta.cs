using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// CUANTO SE COME LA ROCOLA SIN QUE NADIE LA TOQUE.
//
// En un bar la rocola pasa la mayor parte de la noche encendida y quieta: nadie pulsa nada, se ve la pared
// de discos o la pantalla de reposo, y suena la musica. Todo lo que el programa gaste EN ESE ESTADO es
// gasto puro, y en la maquina del bar -dos nucleos- lo que se gaste dibujando se lo quita al sonido.
//
// Las demas baterias miden piezas sueltas por reflexion, que esta muy bien para saber cuanto cuesta cada
// una, pero no dice cuanto cuesta EL CONJUNTO de verdad: los temporizadores, los repintados, los hilos de
// fondo, el recolector de basura. Aqui se enciende la rocola DE VERDAD, con su biblioteca, se la deja
// quieta, y se le mira el reloj de procesador. Es el numero que veria alguien mirando el administrador de
// tareas en el bar.
//
// Nota: esto corre en el servidor, que es mucho mas rapido que la maquina del bar. El porcentaje de alli
// es bastante mayor; lo que sirve es vigilar que este numero no SUBA cuando se toca algo.
class Gasta
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // La misma preparacion que 'arranca': una biblioteca chiquita pero de verdad, los botones ya
    // configurados (si no, arranca el asistente y no se ve la pantalla) y la bitacora encendida.
    static string Prepara(string exe)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_gasta");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        File.Copy(exe, Path.Combine(dir, "rocola.exe"));
        foreach (string d in Directory.GetFiles(".", "*.dll")) File.Copy(d, Path.Combine(dir, Path.GetFileName(d)), true);
        string[] discos = { "Los Tigres del Norte", "Banda El Recodo", "Jose Jose", "Depeche Mode" };
        string[] wavs = Directory.Exists("wav") ? Directory.GetFiles("wav", "*.wav") : new string[0];
        Array.Sort(wavs);
        for (int i = 0; i < discos.Length; i++)
        {
            string d = Path.Combine(Path.Combine(dir, "musica"), discos[i]);
            Directory.CreateDirectory(d);
            for (int k = 0; k < 3 && k < wavs.Length; k++)
                File.Copy(wavs[k], Path.Combine(d, "Cancion " + (k + 1) + ".wav"), true);
        }
        File.WriteAllText(Path.Combine(dir, "rocola_teclas.txt"),
            "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=8\r\nmoneda=32\r\nsaltar=9\r\n");
        File.WriteAllText(Path.Combine(dir, "rocola_config.txt"), "diagnostico=1\r\n");
        return dir;
    }

    // Cuanto de la pantalla esta pintado: sirve para saber si la rocola ya acabo de dibujarse.
    static double Pintado(Rectangle pant)
    {
        using (Bitmap b = new Bitmap(pant.Width, pant.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, pant.Size);
            int hay = 0, tot = 0;
            for (int y = 0; y < b.Height; y += 16)
                for (int x = 0; x < b.Width; x += 16)
                { tot++; Color c = b.GetPixel(x, y); if (c.R + c.G + c.B > 12) hay++; }
            return (tot == 0) ? 0 : (double)hay / tot;
        }
    }

    // EL RELOJ DE PROCESADOR DE LA ROCOLA, LEIDO DONDE DE VERDAD ESTA.
    //
    // Lo primero que se probo aqui fue Process.TotalProcessorTime, que es lo que uno escribiria... y en Mono
    // devuelve CERO para un proceso hijo. La bateria daba "0.0% de un nucleo" con la pantalla de atraccion
    // girando, y eso es peor que no medir: se habria dado por bueno un numero inventado. Se comprobo a mano
    // con /proc y salian 65 centesimas de segundo en 12, o sea un 5.4%.
    //
    // Asi que se lee del sistema: /proc/<pid>/stat, campos 14 y 15 (tiempo en modo usuario y en modo
    // sistema), en centesimas de segundo. El nombre del programa va entre parentesis y puede llevar espacios,
    // asi que se cuenta a partir del ULTIMO parentesis.
    static double Relojcs(int pid)
    {
        try
        {
            string t = File.ReadAllText("/proc/" + pid + "/stat");
            int cierra = t.LastIndexOf(')');
            if (cierra < 0) return -1;
            string[] c = t.Substring(cierra + 2).Split(' ');
            // Tras el parentesis, c[0] es el estado (campo 3): utime es el campo 14 -> c[11], stime -> c[12].
            if (c.Length < 13) return -1;
            return double.Parse(c[11]) + double.Parse(c[12]);
        }
        catch { return -1; }
    }

    static double Mide(Process p, int segs)
    {
        double a = Relojcs(p.Id);
        DateTime t0 = DateTime.UtcNow;
        System.Threading.Thread.Sleep(segs * 1000);
        double b = Relojcs(p.Id);
        double reloj = (DateTime.UtcNow - t0).TotalSeconds;
        if (a < 0 || b < 0)   // no es Linux: se cae al reloj del propio proceso
        {
            p.Refresh();
            return -1;
        }
        return ((b - a) / 100.0) / Math.Max(0.001, reloj) * 100.0;   // centesimas de segundo -> % de un nucleo
    }

    static int Main(string[] argv)
    {
        string exe = Path.GetFullPath((argv.Length > 0) ? argv[0] : "rocola.exe");
        Rectangle pant = Screen.PrimaryScreen.Bounds;
        Console.WriteLine("\n  LA ROCOLA ENCENDIDA Y QUIETA, EN " + pant.Width + "x" + pant.Height + "\n");

        string dir = Prepara(exe);
        Process p = null;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("mono", "\"" + Path.Combine(dir, "rocola.exe") + "\"");
            psi.WorkingDirectory = dir;
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            p = Process.Start(psi);
        }
        catch (Exception ex) { Check(false, "se pudo encender la rocola (" + ex.Message + ")"); return 1; }

        try
        {
            // Se le da tiempo a leer la biblioteca y dibujarse, y se apunta CUANTO tardo: en un equipo de
            // gama baja, lo que tarda en encender es lo primero que se nota.
            Stopwatch enc = Stopwatch.StartNew();
            double pintado = 0;
            for (int i = 0; i < 40 && !p.HasExited; i++)
            {
                System.Threading.Thread.Sleep(500);
                pintado = Pintado(pant);
                if (pintado > 0.5) break;
            }
            enc.Stop();
            Check(!p.HasExited, "la rocola arranca y sigue en pie");
            if (p.HasExited) return Math.Max(1, fallos);
            Console.WriteLine("     tardo " + (enc.Elapsed.TotalSeconds).ToString("0.0") + " s en dibujarse entera"
                              + " (" + (pintado * 100).ToString("0") + "% de la pantalla pintada)");
            Check(pintado > 0.5, "y se dibuja");

            // Se le deja acabar de calentar y de repasar la biblioteca antes de medir: lo que interesa es la
            // rocola EN REGIMEN, no los primeros segundos.
            System.Threading.Thread.Sleep(6000);

            // DOS ESTADOS, y el segundo es el que importa de verdad: la rocola pasa la mayor parte de la
            // noche en la PANTALLA DE ATRACCION, con el cielo titilando y el cubo gigante girando. Ahi es
            // donde un equipo de gama baja se puede quedar sin aire.
            Console.WriteLine();
            Check(Relojcs(p.Id) >= 0, "se le puede leer el reloj de procesador a la rocola");
            double pared = Mide(p, 8);
            Console.WriteLine("     con la pared de discos delante y nadie tocando .. " + pared.ToString("0.0") + "% de un nucleo");

            // La atraccion entra sola a los 40 s sin tocar nada (AttractIdleSecs). Se espera a que entre.
            System.Threading.Thread.Sleep(30000);
            double atrae = Mide(p, 12);
            Console.WriteLine("     con la pantalla de atraccion (lo de toda la noche) " + atrae.ToString("0.0") + "% de un nucleo");
            double peor = Math.Max(pared, atrae);

            Console.WriteLine();
            Console.WriteLine("     en la maquina del bar hay que multiplicar por 4 o 5:  hasta un "
                              + Math.Min(100.0, peor * 4.5).ToString("0") + "% de un nucleo alli");
            Console.WriteLine();

            // El limite: la rocola tiene que dejar sitio de sobra para el sonido. En una maquina de dos
            // nucleos, si el dibujo se come medio nucleo empieza a pelearle a la musica; aqui, que somos 4 o
            // 5 veces mas rapidos, eso son unos 10 puntos.
            Check(pared >= 0 && atrae >= 0, "las dos medidas salieron");
            Check(peor < 10.0, "quieta o en atraccion, la rocola deja el nucleo casi entero libre para la musica");
            Check(enc.Elapsed.TotalSeconds < 12.0, "y enciende en menos de doce segundos");
        }
        finally
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            try { p.WaitForExit(3000); } catch { }
            try { Directory.Delete(dir, true); } catch { }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}

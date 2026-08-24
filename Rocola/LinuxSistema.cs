// ====================================================================================================
//  LO QUE LA ROCOLA LE PREGUNTABA A WINDOWS, PREGUNTADO A LINUX
// ----------------------------------------------------------------------------------------------------
//  Son cuatro cosas sueltas, ninguna del corazon del programa: cuanta memoria hay, a que velocidad va el
//  procesador, obligar a que un archivo guardado baje al disco de verdad, y mandar algo a la papelera.
//  En Windows se piden a la API del sistema; aqui se leen de donde Linux las publica, que es en archivos
//  de texto de /proc y /sys. Sin capas de traduccion de por medio: son los mismos archivos que lee
//  cualquier programa de Linux.
// ====================================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static class Sistema
    {
        // ---------- cuanta memoria tiene la maquina ----------
        // /proc/meminfo trae "MemTotal:  4023456 kB". Es lo que Windows contestaba con GlobalMemoryStatusEx.
        public static ulong MemoriaTotalMb()
        {
            try
            {
                foreach (string l in File.ReadAllLines("/proc/meminfo"))
                {
                    if (!l.StartsWith("MemTotal:", StringComparison.Ordinal)) continue;
                    string[] t = l.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    ulong kb;
                    if (t.Length >= 2 && ulong.TryParse(t[1], out kb)) return kb / 1024UL;
                    break;
                }
            }
            catch { }
            return 0;   // no se pudo saber; quien pregunta ya sabe que 0 significa "no hay dato"
        }

        // ---------- a que velocidad va el procesador ----------
        // Primero la velocidad MAXIMA que declara el nucleo 0, que es la que sirve para decidir si la
        // maquina aguanta el reconstructor de agudos. Si no esta -maquinas sin gobernador de frecuencia-,
        // se usa la que /proc/cpuinfo diga en ese momento, que puede venir baja si la maquina esta ociosa.
        public static int MhzDelProcesador()
        {
            try
            {
                string f = "/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq";
                if (File.Exists(f))
                {
                    long khz;
                    if (long.TryParse(File.ReadAllText(f).Trim(), out khz) && khz > 0) return (int)(khz / 1000);
                }
            }
            catch { }
            try
            {
                foreach (string l in File.ReadAllLines("/proc/cpuinfo"))
                {
                    if (!l.StartsWith("cpu MHz", StringComparison.Ordinal)) continue;
                    int dp = l.IndexOf(':');
                    double v;
                    if (dp > 0 && double.TryParse(l.Substring(dp + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        return (int)v;
                    break;
                }
            }
            catch { }
            return 0;
        }

        // ---------- obligar a que lo guardado baje al disco de verdad ----------
        // Sin esto, lo escrito se queda un rato en la memoria del sistema y un corte de luz se lo lleva. En
        // Windows es FlushFileBuffers; en Linux es fsync, que es la MISMA llamada del nucleo que usa
        // cualquier base de datos para lo mismo.
        [DllImport("libc", SetLastError = true)] private static extern int fsync(int fd);

        public static bool BajaAlDisco(FileStream fs)
        {
            if (fs == null) return false;
            try
            {
                fs.Flush();
                // En Linux, el "manejador" de un archivo abierto ES su numero de descriptor.
                int fd = fs.SafeFileHandle.DangerousGetHandle().ToInt32();
                return fd >= 0 && fsync(fd) == 0;
            }
            catch { return false; }
        }

        // ---------- mandar archivos a la papelera ----------
        // En Windows se le pide al explorador. Aqui se mueven a una carpeta 'papelera' AL LADO de la rocola:
        // es lo mas simple que funciona igual en cualquier Linux, con la musica en el disco que sea, y con
        // la rocola corriendo como root -que es como corre Puppy-. Nada se borra: se aparta, y el dueno
        // vacia esa carpeta cuando quiera.
        public static bool ALaPapelera(List<string> rutas)
        {
            if (rutas == null || rutas.Count == 0) return true;
            try
            {
                string papelera = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "papelera");
                Directory.CreateDirectory(papelera);
                foreach (string r in rutas)
                {
                    if (string.IsNullOrEmpty(r) || !File.Exists(r)) continue;
                    string destino = Path.Combine(papelera, Path.GetFileName(r));
                    for (int k = 2; File.Exists(destino) && k < 1000; k++)
                        destino = Path.Combine(papelera, Path.GetFileNameWithoutExtension(r) + " (" + k + ")" + Path.GetExtension(r));
                    File.Move(r, destino);
                }
                return true;
            }
            catch { return false; }
        }

        // ---------- que tarjetas de sonido hay ----------
        // /proc/asound/cards las lista, una por renglon: "0 [PCH  ]: HDA-Intel - HDA Intel PCH".
        public static List<string> TarjetasDeSonido()
        {
            List<string> r = new List<string>();
            try
            {
                if (!File.Exists("/proc/asound/cards")) return r;
                foreach (string l in File.ReadAllLines("/proc/asound/cards"))
                {
                    int dp = l.IndexOf(": ", StringComparison.Ordinal);
                    if (dp <= 0) continue;                       // los renglones de continuacion no llevan ':'
                    string nombre = l.Substring(dp + 2).Trim();
                    if (nombre.Length > 0) r.Add(nombre);
                }
            }
            catch { }
            return r;
        }

        // ---------- ¿hay un servidor de sonido metido en medio? ----------
        // PipeWire y PulseAudio se ponen ENTRE los programas y la tarjeta para que varios puedan sonar a la
        // vez. En una rocola eso no hace falta -es el unico programa que suena- y solo anade retraso, trabajo
        // y una cosa mas que puede fallar de madrugada. Si esta puesto se avisa, para poder saltarselo
        // poniendo la tarjeta a mano en los ajustes (salida=hw:0).
        public static string ServidorDeSonido()
        {
            try
            {
                foreach (string d in Directory.GetDirectories("/proc"))
                {
                    string nom = Path.GetFileName(d);
                    bool esNumero = nom.Length > 0;
                    for (int i = 0; i < nom.Length && esNumero; i++) if (nom[i] < '0' || nom[i] > '9') esNumero = false;
                    if (!esNumero) continue;
                    string quien;
                    try { quien = File.ReadAllText(Path.Combine(d, "comm")).Trim(); } catch { continue; }
                    if (quien == "pipewire" || quien == "pipewire-pulse") return "PipeWire";
                    if (quien == "pulseaudio") return "PulseAudio";
                    if (quien == "jackd" || quien == "jackdbus") return "JACK";
                }
            }
            catch { }
            return "";
        }
    }
}

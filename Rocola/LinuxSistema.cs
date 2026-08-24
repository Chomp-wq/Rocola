// ====================================================================================================
//  LO QUE LA ROCOLA LE PREGUNTABA A WINDOWS, PREGUNTADO A LINUX
// ----------------------------------------------------------------------------------------------------
//  Son cosas sueltas, ninguna del corazon del programa: cuanta memoria hay, a que velocidad va el
//  procesador, obligar a que un archivo guardado baje al disco de verdad, mandar algo a la papelera,
//  cuantas veces por segundo refresca la pantalla y si hay algo esperando a que lo atiendan.
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

        // ---------- cuantas veces por segundo refresca la pantalla ----------
        //
        // El fondo por tarjeta grafica dibuja "lo que refresca la pantalla y un poco mas": pedir mas
        // cuadros de los que se van a ver es gastar procesador para nada. En Windows el numero lo daba
        // GetDeviceCaps; aqui lo da la extension RandR de X11, que es de donde lo saca cualquier programa
        // de Linux (es lo que lee xrandr). Si no se puede averiguar se devuelve 0 y quien llama toma 60,
        // que es lo de siempre.
        [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr nombre);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr dpy);
        [DllImport("libX11.so.6")] private static extern IntPtr XRootWindow(IntPtr dpy, int pantalla);
        [DllImport("libX11.so.6")] private static extern int XDefaultScreen(IntPtr dpy);
        [DllImport("libXrandr.so.2")] private static extern IntPtr XRRGetScreenInfo(IntPtr dpy, IntPtr ventana);
        [DllImport("libXrandr.so.2")] private static extern short XRRConfigCurrentRate(IntPtr config);
        [DllImport("libXrandr.so.2")] private static extern void XRRFreeScreenConfigInfo(IntPtr config);

        public static int RefrescoPantallaHz()
        {
            IntPtr dpy = IntPtr.Zero;
            try
            {
                dpy = XOpenDisplay(IntPtr.Zero);
                if (dpy == IntPtr.Zero) return 0;
                IntPtr cfg = XRRGetScreenInfo(dpy, XRootWindow(dpy, XDefaultScreen(dpy)));
                if (cfg == IntPtr.Zero) return 0;
                int hz = XRRConfigCurrentRate(cfg);
                XRRFreeScreenConfigInfo(cfg);
                return hz;
            }
            catch { return 0; }
            finally { if (dpy != IntPtr.Zero) { try { XCloseDisplay(dpy); } catch { } } }
        }

        // ---------- hay algo esperando a que lo atiendan? ----------
        //
        // El fondo por tarjeta grafica dibuja en los ratos muertos: mientras no haya nada que atender
        // -una tecla, un temporizador- sigue sacando cuadros, y en cuanto llega algo sale a atenderlo.
        // En Windows se preguntaba con PeekMessage.
        //
        // En Linux se pregunta al propio X11 con XPending, que es "cuantas cosas hay esperando en la
        // conexion con la pantalla". Es una funcion publica de Xlib de toda la vida, no una tripa de Mono.
        // Una tecla o un boton de la botonera llegan por ahi, asi que se notan en el milisegundo siguiente,
        // igual que antes.
        //
        // Y ADEMAS SE SUELTA EL HILO CADA POCO. Los temporizadores de la rocola -el que mueve la
        // animacion, el que adelanta la cancion- NO son cosas de X11: los lleva Mono por su cuenta y no
        // aparecen en esa cola. Si el bucle de dibujo se quedara dentro mientras la cola de X11 este
        // vacia, esos temporizadores no correrian NUNCA y la rocola se quedaria tiesa con un fondo
        // precioso. Por eso esta funcion contesta "hay algo que atender" tambien cuando lleva mas de un
        // pellizco de tiempo sin contestarlo: el bucle sale, Mono atiende sus temporizadores, y vuelve a
        // entrar enseguida. El ritmo de los cuadros no se resiente porque el bucle apunta la hora del
        // proximo cuadro en absoluto, no "dentro de tanto".
        [DllImport("libX11.so.6")] private static extern int XPending(IntPtr dpy);
        private const double RebanadaMs = 8.0;            // lo que como mucho se esta dentro del bucle sin soltar
        private static readonly System.Diagnostics.Stopwatch relojRebanada = System.Diagnostics.Stopwatch.StartNew();
        private static double finRebanada = -1;
        private static IntPtr pantallaX11 = IntPtr.Zero;
        private static bool pantallaBuscada;

        public static bool SinNadaQueAtender()
        {
            double ahora = relojRebanada.Elapsed.TotalMilliseconds;
            if (finRebanada < 0 || ahora >= finRebanada) { finRebanada = ahora + RebanadaMs; return false; }
            try
            {
                if (!pantallaBuscada)
                {
                    pantallaBuscada = true;
                    System.Reflection.Assembly swf = typeof(System.Windows.Forms.Control).Assembly;
                    Type tx = swf.GetType("System.Windows.Forms.XplatUIX11");
                    if (tx != null)
                    {
                        System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Static
                                                          | System.Reflection.BindingFlags.Public
                                                          | System.Reflection.BindingFlags.NonPublic;
                        System.Reflection.PropertyInfo pr = tx.GetProperty("DisplayHandle", bf);
                        if (pr != null) { try { pantallaX11 = (IntPtr)pr.GetValue(null, null); } catch { } }
                        if (pantallaX11 == IntPtr.Zero)
                        {
                            System.Reflection.FieldInfo f = tx.GetField("DisplayHandle", bf);
                            if (f != null) { try { pantallaX11 = (IntPtr)f.GetValue(null); } catch { } }
                        }
                    }
                }
                if (pantallaX11 == IntPtr.Zero) return true;   // sin pantalla no hay nada que atender
                return XPending(pantallaX11) == 0;
            }
            catch { return true; }
        }
    }
}

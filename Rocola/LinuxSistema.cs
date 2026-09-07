// ====================================================================================================
//  LO QUE LA ROCOLA LE PREGUNTABA A WINDOWS, PREGUNTADO A LINUX
// ----------------------------------------------------------------------------------------------------
//  Son cosas sueltas, ninguna del corazon del programa: cuanta memoria hay, a que velocidad va el
//  procesador, obligar a que un archivo guardado baje al disco de verdad, mandar algo a la papelera,
//  cuantas veces por segundo refresca la pantalla, si hay algo esperando a que lo atiendan, y en que
//  disco de los conectados esta un archivo.
//  En Windows se piden a la API del sistema; aqui se leen de donde Linux las publica, que es en archivos
//  de texto de /proc y /sys. Sin capas de traduccion de por medio: son los mismos archivos que lee
//  cualquier programa de Linux.
// ====================================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

        // ---------- la ventana de X11 que hay debajo de un control ----------
        // LA VENTANA DE X11 QUE HAY DEBAJO DE UN CONTROL DE LA ROCOLA.
        //
        // Ni OpenGL ni mpv se cuelgan de un control: los dos se cuelgan de una ventana del sistema de
        // ventanas (X11). Mono no
        // cuenta por ninguna via oficial cual es la ventana de X11 de un control, pero por dentro guarda
        // esa correspondencia y se le puede preguntar. Mono ya no cambia -esta congelado-, asi que esto
        // no se va a mover bajo los pies. Es lo mismo que comprueba la bateria 'opengl'.
        public static bool VentanaX11(IntPtr handle, out IntPtr dpy, out IntPtr win)
        {
            dpy = IntPtr.Zero; win = IntPtr.Zero;
            try
            {
                Assembly swf = typeof(Control).Assembly;
                Type tx = swf.GetType("System.Windows.Forms.XplatUIX11");
                if (tx == null) return false;                  // no estamos en Linux
                BindingFlags est = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo prop = tx.GetProperty("DisplayHandle", est);
                if (prop != null) { try { dpy = (IntPtr)prop.GetValue(null, null); } catch { } }
                if (dpy == IntPtr.Zero)
                {
                    FieldInfo f = tx.GetField("DisplayHandle", est);
                    if (f != null) { try { dpy = (IntPtr)f.GetValue(null); } catch { } }
                }
                Type th = swf.GetType("System.Windows.Forms.Hwnd");
                if (th == null) return false;
                MethodInfo m = th.GetMethod("ObjectFromHandle", est);
                if (m == null) return false;
                object h = null;
                try { h = m.Invoke(null, new object[] { handle }); } catch { }
                if (h == null) return false;
                foreach (string n in new string[] { "client_window", "whole_window" })
                {
                    FieldInfo f = th.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f == null) continue;
                    try { IntPtr v = (IntPtr)f.GetValue(h); if (v != IntPtr.Zero) { win = v; break; } } catch { }
                }
            }
            catch { }
            return dpy != IntPtr.Zero && win != IntPtr.Zero;
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
        [DllImport("libX11.so.6")] private static extern int XConnectionNumber(IntPtr dpy);
        private const double RebanadaMs = 8.0;            // lo que como mucho se esta dentro del bucle sin soltar
        private static readonly System.Diagnostics.Stopwatch relojRebanada = System.Diagnostics.Stopwatch.StartNew();
        private static double finRebanada = -1;
        private static IntPtr pantallaX11 = IntPtr.Zero;
        private static bool pantallaBuscada;

        // La conexion con la pantalla, buscada UNA vez. La piden las dos de aqui abajo -si hay algo que
        // atender, y la espera que se despierta sola-, asi que se busca en un solo sitio.
        private static IntPtr PantallaX11()
        {
            if (pantallaBuscada) return pantallaX11;
            pantallaBuscada = true;
            try
            {
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
            catch { }
            return pantallaX11;
        }

        public static bool SinNadaQueAtender()
        {
            double ahora = relojRebanada.Elapsed.TotalMilliseconds;
            if (finRebanada < 0 || ahora >= finRebanada) { finRebanada = ahora + RebanadaMs; return false; }
            try
            {
                IntPtr dpy = PantallaX11();
                if (dpy == IntPtr.Zero) return true;   // sin pantalla no hay nada que atender
                return XPending(dpy) == 0;
            }
            catch { return true; }
        }

        // ---------- en QUE DISCO esta un archivo ----------
        //
        // La rocola se adapta al disco que tenga: en uno con cabezal pide las cosas en fila de uno y les
        // deja respiros, y en un SSD al contrario. Y como una rocola puede tener LOS DOS -el programa en el
        // del sistema y la musica en otro colgado detras, o repartida entre varios-, el veredicto se guarda
        // por unidad y cada archivo se trata como merece el disco donde ESTA.
        //
        // En Windows la unidad es la letra, y Path.GetPathRoot la da: C:\, D:\... AQUI NO HAY LETRAS, y a
        // Path.GetPathRoot en Linux se le puede preguntar por lo que sea que siempre contesta "/". O sea que
        // en la rocola del bar habia UN SOLO VEREDICTO para todo -la musica, las miniaturas y el programa en
        // el mismo saco-, y si la musica estaba repartida entre un disco rapido y uno lento, media biblioteca
        // se trataba mal.
        //
        // Lo que en Linux separa un disco de otro es DONDE ESTA MONTADO: /mnt/sda1, /mnt/sdb1... y eso lo
        // publica el sistema en /proc/mounts, un archivo de texto con una linea por disco:
        //
        //     /dev/sdb1 /mnt/sdb1 vfat rw,relatime 0 0
        //      ^que es   ^donde esta
        //
        // El disco de un archivo es el montaje MAS HONDO que sea carpeta suya. Hace falta el mas hondo
        // porque se puede montar un disco dentro de otro: con /mnt/sdb1 y /mnt/sdb1/extra, un archivo de
        // "extra" es del segundo, no del primero.
        private static List<string> montajes;
        private static List<string> sitiosDeMentira;
        private static readonly object montajesLock = new object();

        // La TABLA, tal como la escribe el sistema, convertida en la lista de sitios montados y ordenada del
        // mas hondo al menos hondo (asi el primero que encaje es el bueno). Va aparte de leer el archivo a
        // proposito: asi las pruebas pueden darle una tabla inventada y comprobar la regla sin necesitar
        // discos de verdad.
        public static List<string> MontajesDe(string texto)
        {
            List<string> r = new List<string>();
            if (texto == null) return r;
            string[] lineas = texto.Replace("\r", "").Split('\n');
            for (int i = 0; i < lineas.Length; i++)
            {
                string[] c = lineas[i].Split(' ');
                if (c.Length < 2 || c[1].Length == 0) continue;
                string mp = Desescapa(c[1]);
                if (mp.Length > 0 && !r.Contains(mp)) r.Add(mp);
            }
            r.Sort(delegate (string a, string b) { return b.Length.CompareTo(a.Length); });
            return r;
        }

        // /proc/mounts no escribe los espacios tal cual: un disco llamado "Musica Vieja" sale como
        // "Musica\040Vieja". Lo mismo con el tabulador (\011), el salto de linea (\012) y la barra (\134).
        // Sin deshacer esto, el disco de nombre con espacio no se reconoceria nunca.
        private static string Desescapa(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 3 < s.Length)
                {
                    int v = 0; bool ok = true;
                    for (int k = 1; k <= 3; k++)
                    {
                        char c = s[i + k];
                        if (c < '0' || c > '7') { ok = false; break; }
                        v = v * 8 + (c - '0');
                    }
                    if (ok) { sb.Append((char)v); i += 3; continue; }
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        // LA REGLA: en cual de esos sitios cae esta ruta. Se compara por CARPETAS, no por letras, para que
        // /mnt/sdb11 no se tome por /mnt/sdb1.
        public static string UnidadDe(string ruta, List<string> tabla)
        {
            if (string.IsNullOrEmpty(ruta)) return "";
            string full = ruta;
            // Lo normal es que ya venga entera; solo se arma si viene relativa (que cuesta, y esto se
            // pregunta por cada portada y cada cancion).
            if (full[0] != '/') { try { full = Path.GetFullPath(full); } catch { return ""; } }
            if (tabla != null)
            {
                for (int i = 0; i < tabla.Count; i++)
                {
                    string m = tabla[i];
                    if (m.Length == 0 || m == "/") continue;
                    if (full.Length == m.Length && string.CompareOrdinal(full, m) == 0) return m;
                    if (full.Length > m.Length && full[m.Length] == '/' && string.CompareOrdinal(full, 0, m, 0, m.Length) == 0) return m;
                }
            }
            return "/";
        }

        // Y lo de verdad, con la tabla de esta maquina. Se lee UNA vez y se guarda: esto se pregunta por
        // cada portada y cada cancion, y abrir un archivo en cada consulta no tendria ningun sentido.
        public static string UnidadDe(string ruta)
        {
            List<string> puntos, mentira;
            LaTabla(out puntos, out mentira);
            return UnidadDe(ruta, puntos);
        }

        // La tabla, leyendola la primera vez que haga falta.
        //
        // DEVUELVE LAS DOS LISTAS en vez de dejar que quien pregunta vuelva a mirar los campos, y eso no es
        // manía: entre "asegurate de que esta leida" y "leela" cabe un OlvidaLasUnidades de otro hilo -que
        // es exactamente lo que pasa al rearmar la biblioteca, mientras el barrido y las portadas siguen
        // preguntando desde los suyos-. Cuando caia justo ahi, la respuesta salia vacia: el disco
        // equivocado, y /proc dado por bueno para ir a buscar musica. Medido con el fallo puesto: 23 discos
        // equivocados y 29 "/proc no es del sistema" en dos segundos y medio.
        //
        // Se entra al cerrojo siempre, tambien cuando ya esta leida. Cuesta unas decenas de nanosegundos y
        // esto se pregunta una vez por carpeta: ni se nota (medido, no se mueve ni una decima).
        private static void LaTabla(out List<string> puntos, out List<string> mentira)
        {
            lock (montajesLock)
            {
                if (montajes == null)
                {
                    string texto = null;
                    try { texto = File.ReadAllText("/proc/mounts"); } catch { }
                    sitiosDeMentira = SistemasDeMentira(texto);
                    montajes = MontajesDe(texto);
                }
                puntos = montajes; mentira = sitiosDeMentira;
            }
        }

        // ---------- carpetas donde NO puede haber musica ----------
        //
        // Si a la rocola se le dice "la musica esta en este disco entero", el recorrido se mete en /proc,
        // /sys, /dev y compania, que no son discos: son inventos del nucleo para publicar cosas, y ahi no
        // hay ni puede haber una cancion. Medido apuntando a la raiz de esta maquina: de 40.000 carpetas
        // miradas, /sys sola eran 16.719. O sea que se puede quitar la mayor parte del trabajo sin perder
        // ni un archivo de musica.
        //
        // No hay que adivinar nombres: el propio sistema dice de que tipo es cada sitio montado, en la
        // tercera columna de /proc/mounts.
        //
        //     proc /proc proc rw,relatime 0 0
        //           ^donde ^de que tipo
        //
        // OJO, ESTO ES UNA LISTA DE TIPOS CONCRETOS Y NUNCA AL REVES. La raiz de Puppy es de tipo "aufs" y
        // ahi vive absolutamente todo; dar por malo lo que no se reconozca dejaria a la rocola sin mirar
        // nada. Si aparece un tipo nuevo que no esta en la lista, lo peor que pasa es que se mire una
        // carpeta de mas.
        private static readonly string[] TiposDeMentira = {
            "proc", "sysfs", "devtmpfs", "devpts", "tmpfs", "ramfs", "cgroup", "cgroup2",
            "securityfs", "debugfs", "tracefs", "pstore", "bpf", "configfs", "fusectl",
            "mqueue", "hugetlbfs", "binfmt_misc", "efivarfs", "autofs", "nsfs", "selinuxfs", "rpc_pipefs"
        };

        // De la tabla del sistema, los sitios montados que NO son un disco. Aparte de leer el archivo, para
        // poder darle una tabla inventada desde las pruebas.
        public static List<string> SistemasDeMentira(string texto)
        {
            List<string> r = new List<string>();
            if (texto == null) return r;
            string[] lineas = texto.Replace("\r", "").Split('\n');
            for (int i = 0; i < lineas.Length; i++)
            {
                string[] c = lineas[i].Split(' ');
                if (c.Length < 3 || c[1].Length == 0) continue;
                bool mentira = false;
                for (int k = 0; k < TiposDeMentira.Length && !mentira; k++)
                    if (string.CompareOrdinal(c[2], TiposDeMentira[k]) == 0) mentira = true;
                if (!mentira) continue;
                string mp = Desescapa(c[1]);
                if (mp.Length > 0 && !r.Contains(mp)) r.Add(mp);
            }
            r.Sort(delegate (string a, string b) { return b.Length.CompareTo(a.Length); });
            return r;
        }

        // ¿Esta ruta cae en uno de esos sitios? Es la MISMA cuenta que "en que disco esta", pero mirando solo
        // los sitios de mentira, asi que se reaprovecha tal cual. La raiz nunca cuenta: si en alguna maquina
        // la propia raiz fuera de uno de esos tipos -un arranque entero en memoria-, dar por mala la raiz
        // seria dejar a la rocola sin mirar absolutamente nada.
        public static bool EsCarpetaDelSistema(string ruta, List<string> deMentira)
        {
            if (deMentira == null || deMentira.Count == 0) return false;
            string u = UnidadDe(ruta, deMentira);
            return u.Length > 0 && u != "/";
        }

        public static bool EsCarpetaDelSistema(string ruta)
        {
            List<string> puntos, mentira;
            LaTabla(out puntos, out mentira);
            return EsCarpetaDelSistema(ruta, mentira);
        }

        // ---------- esperar un poco, PERO SALIR EN CUANTO LLEGUE ALGO ----------
        //
        // El bucle del fondo apunta la hora del proximo cuadro y, si va adelantado, espera. Dormir a secas
        // ese rato seria lo facil y esta mal: una tecla o un boton de la botonera que llegan justo despues
        // de empezar a dormir se quedarian esperando hasta que se acabe la siesta, y eso se nota al tocar.
        //
        // En Windows esto lo hace MsgWaitForMultipleObjectsEx: duerme, pero se despierta si llega algo.
        // Aqui se hace lo mismo con la conexion a la pantalla: se le pregunta al sistema por ESE conducto
        // con un plazo, y si llega cualquier cosa por el, contesta antes de que se cumpla. Es lo que hace
        // cualquier programa de X11, con la funcion del sistema de toda la vida (poll).
        //
        // Y ANTES DE DORMIR SE MIRA LO QUE YA HAY. Xlib se adelanta y se trae los sucesos a su propia cola,
        // asi que puede haber una tecla esperando SIN que quede nada en el conducto: dormirse ahi seria
        // dormirse justo encima de lo que se estaba esperando. Es la misma cautela que en Windows.
        [StructLayout(LayoutKind.Sequential)] private struct Conducto { public int fd; public short pide; public short trae; }
        [DllImport("libc", SetLastError = true)] private static extern int poll(ref Conducto fds, uint cuantos, int plazoMs);
        private const short HayLectura = 0x001;   // POLLIN

        public static bool EsperaMensajes(int ms)
        {
            // El mismo acote que en Windows: como poco un milisegundo -para soltar el procesador- y como
            // mucho ocho, que es lo que se puede estar sin atender los temporizadores de la rocola.
            if (ms < 1) ms = 1; else if (ms > 8) ms = 8;
            try
            {
                IntPtr dpy = PantallaX11();
                if (dpy != IntPtr.Zero)
                {
                    if (XPending(dpy) != 0) return true;       // ya habia algo: ni se duerme
                    Conducto c = new Conducto();
                    c.fd = XConnectionNumber(dpy); c.pide = HayLectura;
                    if (c.fd >= 0)
                    {
                        int r = poll(ref c, 1, ms);
                        if (r >= 0) return r > 0;              // >0 = llego algo; 0 = se cumplio el plazo
                    }
                }
            }
            catch { }
            System.Threading.Thread.Sleep(1);   // sin pantalla o si algo fallo: al menos se suelta el procesador
            return false;
        }

        // Tira lo guardado para que la proxima consulta vuelva a mirar que discos hay. Se llama al rearmar
        // la biblioteca: es cuando alguien acaba de conectar un disco con musica nueva.
        public static void OlvidaLasUnidades()
        {
            lock (montajesLock) { montajes = null; sitiosDeMentira = null; }
        }
    }
}

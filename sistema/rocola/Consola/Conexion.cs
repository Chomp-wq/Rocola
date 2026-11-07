// ¿HAY INTERNET? (peticion del dueno del 7-11-2026, con Rocola OS ya en el A4: «no tengo forma de ver si estoy conectado a internet»).
// Un hilo de fondo (prioridad minima) mira, paso a paso, donde se corta: la tarjeta, el cable (carrier), la direccion que da el router
// (DHCP), la puerta (el router), los nombres (DNS) y que de verdad se llegue a internet (una conexion a 1.1.1.1:443 y a github.com:443).
// Cada 30 s; cada 5 s mientras Ctrl+F3 esta abierto. Lo dice la barra de Ctrl+F3 (siempre a la vista) y la hoja Detalles -> Internet.
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Rocola
{
    internal static class Conexion
    {
        internal enum Estado { Mirando, SinTarjeta, SinCable, SinDireccion, SinRouter, SinInternet, SinNombres, Internet }
        internal static volatile Estado Ahora = Estado.Mirando;
        internal static volatile string Corto = "Mirando la red...", Largo = "", Tarjeta = "", Direccion = "", Puerta = "";
        internal static volatile int Ms = -1;
        internal static DateTime UltimaConInternet = DateTime.MinValue, Mirado = DateTime.MinValue;
        internal static volatile bool Prisa;   // Ctrl+F3 abierto: cada 5 s
        private static int empezado;

        internal static void Empieza()
        {
            if (Interlocked.Exchange(ref empezado, 1) != 0) return;
            new Thread(delegate ()
            {
                Prioridad.Fondo();
                while (true)
                {
                    try { Mira(); } catch { }
                    for (int i = 0; i < 30 && !(Prisa && i >= 5); i++) Thread.Sleep(1000);
                }
            }) { IsBackground = true, Name = "mira internet" }.Start();
        }

        private static void Mira()
        {
            // 1. LA TARJETA Y EL CABLE (la primera de verdad: eth*, en*, wl*)
            string tar = "", ip = ""; bool cable = false;
            foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (!n.Name.StartsWith("eth") && !n.Name.StartsWith("en") && !n.Name.StartsWith("wl")) continue;
                string c = Lee("/sys/class/net/" + n.Name + "/carrier");
                bool este = c == "1" || (c.Length == 0 && n.OperationalStatus == OperationalStatus.Up);
                string ip4 = "";
                foreach (UnicastIPAddressInformation u in n.GetIPProperties().UnicastAddresses)
                    if (u.Address.AddressFamily == AddressFamily.InterNetwork) ip4 = u.Address.ToString();
                // se queda con la mejor: con direccion, si no con cable, si no la primera
                if (tar.Length == 0 || (ip4.Length > 0 && ip.Length == 0) || (este && !cable && ip.Length == 0)) { tar = n.Name; cable = este; ip = ip4; }
            }
            Tarjeta = tar; Direccion = ip; Puerta = PuertaDeSalida();
            Mirado = DateTime.Now;
            if (tar.Length == 0) { Pon(Estado.SinTarjeta, "Sin tarjeta de red", "Linux no ve ninguna tarjeta de red en la rocola."); return; }
            if (!cable) { Pon(Estado.SinCable, "Sin cable de red", "La tarjeta " + tar + " no tiene cable: revisa que el cable del router este bien puesto en la rocola (y la lucecita del puerto)."); return; }
            if (ip.Length == 0) { Pon(Estado.SinDireccion, "Sin dirección", "El cable esta, pero el router no le da direccion (¿el router apagado o aun prendiendo?). Se vuelve a pedir sola cada 20 s."); return; }
            // 2. INTERNET: una conexion de verdad (no un ping: hay routers que no lo dejan pasar)
            int ms = Conecta("1.1.1.1", 443);
            if (ms < 0) ms = Conecta("8.8.8.8", 53);
            if (ms < 0)
            {
                bool router = Puerta.Length > 0 && Conecta(Puerta, 80) >= 0 | Conecta(Puerta, 53) >= 0;
                if (Puerta.Length == 0) Pon(Estado.SinRouter, "Sin router", "Tiene direccion (" + ip + ") pero no sabe por donde salir: el router no dio puerta de salida.");
                else Pon(Estado.SinInternet, "Sin internet", "Direccion " + ip + ", router " + Puerta + (router ? " (contesta)" : "") + ", pero no se llega a internet: el router no tiene internet o el modem esta caido.");
                return;
            }
            // 3. LOS NOMBRES (DNS): sin ellos no se ven ni Spotify ni las actualizaciones
            if (Resuelve("github.com") == null)
            { Pon(Estado.SinNombres, "Internet sin DNS", "Llega a internet (" + ms + " ms) pero no traduce nombres (DNS): el router no da DNS. Spotify, el clima y las actualizaciones no van."); return; }
            Ms = ms; UltimaConInternet = DateTime.Now;
            Pon(Estado.Internet, "Internet: sí (" + ms + " ms)", "Con internet. Direccion " + ip + " (" + tar + "), router " + (Puerta.Length > 0 ? Puerta : "?") + ", " + ms + " ms hasta internet.");
        }
        private static void Pon(Estado e, string corto, string largo)
        {
            if (e != Estado.Internet) Ms = -1;
            Corto = corto; Largo = largo; Ahora = e;
        }
        internal static bool HayInternet { get { return Ahora == Estado.Internet; } }

        // La puerta de salida (el router): /proc/net/route, la linea con destino 00000000 (el gateway, en hexadecimal al reves)
        private static string PuertaDeSalida()
        {
            try
            {
                foreach (string l in File.ReadAllLines("/proc/net/route"))
                {
                    string[] c = l.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (c.Length < 3 || c[1] != "00000000") continue;
                    uint g = Convert.ToUInt32(c[2], 16);
                    if (g == 0) continue;
                    return (g & 0xFF) + "." + ((g >> 8) & 0xFF) + "." + ((g >> 16) & 0xFF) + "." + (g >> 24);
                }
            }
            catch { }
            return "";
        }
        // Cuanto tarda en conectar (ms), o -1 si no conecta en 3 s
        private static int Conecta(string ip, int puerto)
        {
            try
            {
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    Task t = s.ConnectAsync(IPAddress.Parse(ip), puerto);
                    if (!t.Wait(3000) || !s.Connected) return -1;
                    return (int)Math.Max(1, sw.ElapsedMilliseconds);
                }
            }
            catch { return -1; }
        }
        private static IPAddress[] Resuelve(string nombre)
        {
            try { Task<IPAddress[]> t = Dns.GetHostAddressesAsync(nombre); return t.Wait(4000) && t.Result.Length > 0 ? t.Result : null; }
            catch { return null; }
        }
        private static string Lee(string ruta) { try { return File.Exists(ruta) ? File.ReadAllText(ruta).Trim() : ""; } catch { return ""; } }
    }
}

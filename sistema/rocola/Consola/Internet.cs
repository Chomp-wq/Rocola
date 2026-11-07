// LO QUE DICE LA HOJA «INTERNET» (Ctrl+F3): la version del programa, la red por cable (su direccion y la MAC, que es la que pide el router
// para el encendido remoto) y como va la actualizacion (lo que deja rocola-actualiza en /run/rocola-actualiza.txt).
using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Rocola
{
    internal sealed partial class Controlador
    {
        internal static string TextoInternet()
        {
            StringBuilder sb = new StringBuilder();
            // (7-11-2026) lo primero: si hay internet y, si no, donde se corta (Conexion.cs)
            sb.Append(Conexion.Ahora == Conexion.Estado.Internet ? "CON INTERNET. " : Conexion.Ahora == Conexion.Estado.Mirando ? "" : "SIN INTERNET. ").Append(Conexion.Largo.Length > 0 ? Conexion.Largo : "Mirando la red...");
            if (Conexion.Ahora != Conexion.Estado.Internet && Conexion.UltimaConInternet != DateTime.MinValue) sb.Append(" (tuvo internet por ultima vez a las ").Append(Conexion.UltimaConInternet.ToString("HH:mm")).Append(')');
            sb.Append('\n');
                        sb.Append("Programa: version ").Append(Version.Numero).Append(" (").Append(Version.Commit).Append(")");
            string nueva = Leer("/rocola/datos/programa/nueva.version");
            if (nueva.Length > 0) sb.Append("   -> la ").Append(nueva).Append(" se pone cuando la rocola este libre");
            sb.Append('\n');
            string so = Leer("/etc/rocola-sistema-version"); if (so.Length == 0) so = "0";
            sb.Append("Sistema: version ").Append(so);
            string cfg = Leer("/rocola/disco/rocola-os/arranque.cfg");
            if (cfg.Length == 0) sb.Append(" (desde la memoria USB: se actualiza al instalarlo)");
            else if (File.Exists("/rocola/disco/rocola-os/probando")) sb.Append(" (nueva, en prueba: se da por buena a los 3 minutos)");
            if (File.Exists("/run/rocola-reinicio-sistema")) sb.Append("   -> uno nuevo listo: se reinicia cuando la rocola este libre");
            sb.Append('\n');
            bool hay = false;
            try
            {
                foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (!n.Name.StartsWith("eth") && !n.Name.StartsWith("en") && !n.Name.StartsWith("wl")) continue;   // solo las tarjetas de verdad
                    string ip = "";
                    foreach (UnicastIPAddressInformation u in n.GetIPProperties().UnicastAddresses)
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork) ip = u.Address.ToString();
                    byte[] mac = n.GetPhysicalAddress().GetAddressBytes();
                    string m = mac.Length == 6 ? BitConverter.ToString(mac).Replace('-', ':') : "";
                    sb.Append("Red (").Append(n.Name).Append("): ").Append(ip.Length > 0 ? "conectada, " + ip : "sin conexion (¿el cable?)")
                      .Append(m.Length > 0 ? "   MAC para el encendido remoto: " + m : "").Append('\n');
                    hay = true;
                }
            }
            catch { }
            if (!hay) sb.Append("Red: no hay tarjeta de red\n");
            string est = Leer("/run/rocola-actualiza.txt");
            sb.Append("Actualizacion: ").Append(est.Length > 0 ? est : "aun no ha revisado (lo hace al minuto de prender y cada hora)").Append('\n');
            sb.Append("Llave de GitHub: ").Append(File.Exists("/rocola/datos/rocola_github.txt") ? "puesta" : "no hace falta si el repositorio es publico (si es privado: rocola_github.txt en una memoria USB o junto a rocola_config.txt de XP)");
            string bz = Leer("/run/rocola-buzon.txt");   // el buzon para trabajar desde casa (rocola-buzon, 7-11-2026)
            sb.Append("\nDesde casa (buzon, opcional): ").Append(bz.Length > 0 ? bz : "aun no arranca (a los 90 s de prender)");
            return sb.ToString();
        }
        private static string Leer(string ruta) { try { return File.Exists(ruta) ? File.ReadAllText(ruta).Trim() : ""; } catch { return ""; } }
    }
}

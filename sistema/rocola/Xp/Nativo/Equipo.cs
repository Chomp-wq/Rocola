// LO QUE LA ROCOLA SABE DEL EQUIPO, PREGUNTADO A LINUX (port nativo de XP, 8-11-2026). En XP lo contestaba Windows: GlobalMemoryStatusEx
// (kernel32) la memoria, el registro (HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0) el nombre y los MHz del procesador, y
// EnumDisplayDevices (user32) el nombre del adaptador de la pantalla. El codigo de XP llama aqui directo (Form1.cs, CancionEnMemoria.cs,
// MedidasBitacora.cs, DiagnosticoCompleto.cs), sin las estructuras de Windows (MEMORYSTATUSEX, DISPLAY_DEVICE):
//   - Memoria: /proc/meminfo (MemTotal, MemAvailable) y el uso en % como dwMemoryLoad; Direcciones: lo que le queda libre al programa de
//     su espacio de direcciones (en XP, ullTotalVirtual / ullAvailVirtual: los 2 GB de un programa de 32 bits), de /proc/self/status.
//   - Procesador: el mismo texto que ProcessorNameString (la marca de CPUID: «model name» de /proc/cpuinfo); ProcesadorMhz, lo que en XP
//     es «~MHz» (la velocidad de fabrica que Windows mide al arrancar): la mayor que da el nucleo (cpufreq, cpuinfo_max_freq; sin cpufreq,
//     «cpu MHz» de /proc/cpuinfo).
//   - Adaptador: lo que en XP es la cadena del controlador de la tarjeta (la del Administrador de dispositivos): el nombre que da OpenGL
//     (GL_RENDERER: «AMD ARUBA (DRM 2.50.0 / 6.12...)» con r600, «llvmpipe (...)» sin tarjeta) y, entre parentesis, el controlador del
//     nucleo y el numero PCI de la tarjeta (/sys/class/drm).
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static class Equipo
    {
        // La memoria del equipo (bytes) y el uso en %, como GlobalMemoryStatusEx (ullTotalPhys, ullAvailPhys, dwMemoryLoad). false si no se pudo.
        internal static bool Memoria(out ulong total, out ulong disponible, out uint usoPct)
        {
            total = disponible = 0; usoPct = 0;
            try
            {
                foreach (string l in File.ReadAllLines("/proc/meminfo"))
                {
                    if (l.StartsWith("MemTotal:")) total = Kb(l);
                    else if (l.StartsWith("MemAvailable:")) disponible = Kb(l);
                }
                if (total == 0) return false;
                usoPct = (uint)(100 - disponible * 100 / total);
                return true;
            }
            catch { return false; }
        }
        private static ulong Kb(string l)
        {
            string[] p = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            ulong v; return p.Length >= 2 && ulong.TryParse(p[1], out v) ? v * 1024UL : 0;
        }

        // Las direcciones del programa (ullTotalVirtual, ullAvailVirtual de GlobalMemoryStatusEx): en XP, los 2 GB de un programa de 32 bits y
        // lo que le queda libre (la bitacora lo apunta en cada parada de la pantalla: direcciones_libres). En Linux de 64 bits, la mitad de
        // abajo del espacio de direcciones (47 bits, 128 TB: el A4 no tiene paginas de 5 niveles) menos lo que el programa ya tiene apartado
        // (VmSize de /proc/self/status, que cuenta tambien lo reservado sin usar, como Windows). Sin /proc, todo libre.
        internal static bool Direcciones(out ulong total, out ulong libres)
        {
            total = 1UL << 47; libres = total;
            try
            {
                foreach (string l in File.ReadAllLines("/proc/self/status"))
                    if (l.StartsWith("VmSize:")) { ulong v = Kb(l); libres = v < total ? total - v : 0; return true; }
            }
            catch { }
            return false;
        }

        // El nombre del procesador (en XP, ProcessorNameString del registro): «model name» de /proc/cpuinfo; "" si no se pudo
        internal static string Procesador()
        {
            try
            {
                foreach (string l in File.ReadAllLines("/proc/cpuinfo"))
                    if (l.StartsWith("model name")) { int i = l.IndexOf(':'); return i > 0 ? l.Substring(i + 1).Trim() : ""; }
            }
            catch { }
            return "";
        }
        // Los MHz del procesador (en XP, «~MHz» del registro); 0 si no se pudo
        internal static int ProcesadorMhz()
        {
            try
            {
                string f = "/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq";
                if (File.Exists(f)) { long khz; if (long.TryParse(File.ReadAllText(f).Trim(), out khz) && khz > 0) return (int)(khz / 1000); }
                foreach (string l in File.ReadAllLines("/proc/cpuinfo"))
                    if (l.StartsWith("cpu MHz"))
                    {
                        int i = l.IndexOf(':'); double m;
                        if (i > 0 && double.TryParse(l.Substring(i + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m)) return (int)Math.Round(m);
                        break;
                    }
            }
            catch { }
            return 0;
        }

        // El sistema, para el diagnostico (en XP, Environment.OSVersion: «Microsoft Windows NT 5.1.2600 Service Pack 3»): «Linux» y la version del
        // nucleo (/proc/sys/kernel/osrelease) y, entre parentesis, el nombre del sistema (PRETTY_NAME de /etc/os-release), si lo hay
        internal static string SistemaOperativo()
        {
            string nucleo = "", nombre = "";
            try { nucleo = File.ReadAllText("/proc/sys/kernel/osrelease").Trim(); } catch { }
            try
            {
                foreach (string l in File.ReadAllLines("/etc/os-release"))
                    if (l.StartsWith("PRETTY_NAME=")) { nombre = l.Substring(12).Trim().Trim('"'); break; }
            }
            catch { }
            if (nucleo.Length == 0) nucleo = Environment.OSVersion.Version.ToString();
            return "Linux " + nucleo + (nombre.Length > 0 ? " (" + nombre + ")" : "");
        }

        // El adaptador de la pantalla principal (en XP, EnumDisplayDevices: la cadena del controlador). OpenGL solo se pregunta desde el hilo que
        // tiene el contexto (el de la pantalla) y lo que dice se recuerda. Si no se sabe nada, false.
        [DllImport("libEGL.so.1")] private static extern IntPtr eglGetCurrentContext();
        private static string rendererGl;
        internal static bool Adaptador(out string nombre, out string conector, out string id)
        {
            string gl = rendererGl;
            if (gl == null)
            {
                try
                {
                    if (eglGetCurrentContext() != IntPtr.Zero)
                    {
                        string r = Rocola.Pantalla.GL.Texto(Rocola.Pantalla.GL.RENDERER);
                        if (!string.IsNullOrEmpty(r)) rendererGl = gl = r.Trim();
                    }
                }
                catch { }
            }
            string controlador, pci;
            TarjetaDrm("/sys/class/drm", out controlador, out pci, out conector);
            id = pci.Length > 0 ? "PCI " + pci : "";
            string drm = controlador.Length > 0 ? controlador + (pci.Length > 0 ? ", PCI " + pci : "") : "";
            nombre = gl ?? "";
            if (drm.Length > 0) nombre = nombre.Length > 0 ? nombre + " (" + drm + ")" : drm;
            return nombre.Length > 0;
        }

        // La tarjeta de /sys/class/drm (o de otra carpeta igual, para las pruebas): cardN con su controlador (DRIVER de device/uevent) y su
        // numero PCI (PCI_ID), y el primer conector enchufado (cardN-HDMI-A-1 con status «connected»). Si hay varias, la que tiene algo
        // enchufado.
        internal static bool TarjetaDrm(string raiz, out string controlador, out string pci, out string conector)
        {
            controlador = pci = conector = "";
            try
            {
                if (!Directory.Exists(raiz)) return false;
                string[] todo = Directory.GetFileSystemEntries(raiz);
                Array.Sort(todo, StringComparer.Ordinal);
                bool hallada = false;
                foreach (string ruta in todo)
                {
                    string n = Path.GetFileName(ruta);
                    if (n.Length < 5 || !n.StartsWith("card") || n.IndexOf('-') >= 0) continue;
                    bool numero = true; for (int i = 4; i < n.Length; i++) if (n[i] < '0' || n[i] > '9') numero = false;
                    if (!numero) continue;
                    string drv = "", idPci = "", con = "";
                    try
                    {
                        foreach (string l in File.ReadAllLines(Path.Combine(ruta, "device", "uevent")))
                        {
                            if (l.StartsWith("DRIVER=")) drv = l.Substring(7).Trim();
                            else if (l.StartsWith("PCI_ID=")) idPci = l.Substring(7).Trim();
                        }
                    }
                    catch { }
                    foreach (string c in todo)
                    {
                        string cn = Path.GetFileName(c);
                        if (!cn.StartsWith(n + "-")) continue;
                        try { if (File.ReadAllText(Path.Combine(c, "status")).Trim() == "connected") { con = cn.Substring(n.Length + 1); break; } }
                        catch { }
                    }
                    if (drv.Length == 0 && idPci.Length == 0) continue;
                    if (!hallada || (conector.Length == 0 && con.Length > 0)) { controlador = drv; pci = idPci; conector = con; hallada = true; }
                }
                return hallada;
            }
            catch { return false; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
namespace Rocola
{
    // LO QUE HAY EN UNA CARPETA, DE UN SOLO RECORRIDO (19-10-2026). La firma de la biblioteca (Form1.FirmaCarpeta) en .NET 3.5 (CLR 2,
    // Windows XP) pedia al sistema la lista de archivos (Directory.GetFiles), luego UNA llamada por archivo para su tamano y su fecha
    // (new FileInfo(ruta).Length), luego la lista de carpetas (Directory.GetDirectories) y UNA llamada por carpeta para ver si es un atajo
    // (DirectoryInfo.Attributes): con 30.000 canciones, 30.000 llamadas de mas en cada arranque. FindFirstFile/FindNextFile entregan nombre,
    // atributos, tamano y fecha de escritura de cada entrada en la misma pasada. Aqui esta eso, partido en dos para poder probarlo sin Windows:
    //   - ListadoCarpeta.Construye: toma entradas ya leidas (nombre, atributos, fecha, tamano), las separa en archivos y carpetas EN EL ORDEN
    //     EN QUE VIENEN (el mismo que da Directory.GetFiles/GetDirectories, que tambien recorren con FindNextFile), y sale con null ante
    //     cualquier cosa que no sea el caso limpio. Con null la firma hace EXACTAMENTE lo de siempre para esa carpeta, errores incluidos.
    // - ListadoWin32.Lee: las llamadas de Windows (solo con CLR 2 en Windows). Esta parte NO se ha podido ejecutar fuera de Windows. / EN LINUX: no esta (ver abajo).
    internal struct EntradaListado
    {
        public string Nombre;          // solo el nombre, como lo da el sistema
        public uint Atributos;         // dwFileAttributes
        public long FechaEscritura;    // FILETIME de la ultima escritura (100 ns desde 1601), como lo da el sistema
        public long Largo;             // tamano en bytes
    }
    internal delegate ListadoCarpeta FuenteListado(string carpeta);
    internal sealed class ListadoCarpeta
    {
        public string[] Archivos;      // rutas completas, en el orden del sistema
        public long[] Largos;          // lo mismo que FileInfo.Length
        public long[] Fechas;          // lo mismo que FileInfo.LastWriteTimeUtc.Ticks
        public string[] Carpetas;      // rutas completas, en el orden del sistema
        public bool[] Atajos;          // lo mismo que (DirectoryInfo.Attributes & ReparsePoint) != 0

        internal const uint Directorio = 0x10, PuntoDeReanalisis = 0x400;
        // Mas larga que esto (la carpeta mas el nombre) y se deja a lo de siempre: GetFullPath lanza PathTooLongException a partir de 260
        // y el tipo del fallo forma parte de la firma. Con margen, para no rozar el limite.
        internal const int RutaLargaMax = 240;

        // true si es una ruta de unidad ("C:\...") o de red ("\\servidor\recurso\...") normal, sin prefijos raros ("\\?\", "\\.\").
        internal static bool RutaNormal(string c)
        {
            if (c == null || c.Length < 3 || c.Length >= RutaLargaMax) return false;
            if (c[1] == ':' && c[2] == '\\') return (c[0] >= 'A' && c[0] <= 'Z') || (c[0] >= 'a' && c[0] <= 'z');
            if (c[0] == '\\' && c[1] == '\\') return c[2] != '?' && c[2] != '.' && c[2] != '\\';
            return false;
        }
        // Nombres que GetFullPath cambiaria (punto o espacio al final) o que Windows toma por un aparato (CON, NUL, COM1...): no son el caso limpio.
        internal static bool NombreLimpio(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            char u = n[n.Length - 1];
            if (u == '.' || u == ' ') return false;
            for (int i = 0; i < n.Length; i++) if (n[i] < ' ' || n[i] == '\\' || n[i] == '/' || n[i] == ':' || n[i] == '*' || n[i] == '?' || n[i] == '"' || n[i] == '<' || n[i] == '>' || n[i] == '|') return false;
            int punto = n.IndexOf('.');
            string raiz = (punto >= 0 ? n.Substring(0, punto) : n).TrimEnd(' ');
            if (raiz.Length == 3)
            {
                string m = raiz.ToLowerInvariant();
                if (m == "con" || m == "prn" || m == "aux" || m == "nul") return false;
            }
            else if (raiz.Length == 4 && raiz[3] >= '0' && raiz[3] <= '9')
            {
                string m = raiz.Substring(0, 3).ToLowerInvariant();
                if (m == "com" || m == "lpt") return false;
            }
            return true;
        }
        // null = no es el caso limpio: que lo haga el camino de siempre.
        internal static ListadoCarpeta Construye(string carpeta, IEnumerable<EntradaListado> entradas)
        {
            if (string.IsNullOrEmpty(carpeta) || entradas == null) return null;
            // Como Path.Combine(carpeta, nombre): el separador solo se anade si la carpeta no acaba ya en uno.
            char ultimo = carpeta[carpeta.Length - 1];
            string prefijo = (ultimo == Path.DirectorySeparatorChar || ultimo == Path.AltDirectorySeparatorChar) ? carpeta : carpeta + Path.DirectorySeparatorChar;
            List<string> archivos = new List<string>(), carpetas = new List<string>();
            List<long> largos = new List<long>(), fechas = new List<long>();
            List<bool> atajos = new List<bool>();
            foreach (EntradaListado e in entradas)
            {
                if (e.Nombre == "." || e.Nombre == "..") continue;
                if (!NombreLimpio(e.Nombre) || prefijo.Length + e.Nombre.Length >= RutaLargaMax) return null;
                string ruta = prefijo + e.Nombre;
                if ((e.Atributos & Directorio) != 0) { carpetas.Add(ruta); atajos.Add((e.Atributos & PuntoDeReanalisis) != 0); }
                else
                {
                    archivos.Add(ruta); largos.Add(e.Largo);
                    fechas.Add(DateTime.FromFileTimeUtc(e.FechaEscritura).Ticks);   // lo mismo que FileInfo.LastWriteTimeUtc; si falla, lo de siempre
                }
            }
            ListadoCarpeta l = new ListadoCarpeta();
            l.Archivos = archivos.ToArray(); l.Largos = largos.ToArray(); l.Fechas = fechas.ToArray();
            l.Carpetas = carpetas.ToArray(); l.Atajos = atajos.ToArray();
            return l;
        }
    }
    // EN LINUX: aqui XP tiene ListadoWin32, las llamadas de Windows (FindFirstFileW, FindNextFileW y FindClose de kernel32, con su
    // WIN32_FIND_DATA y su FILETIME) para leer una carpeta de una pasada con CLR 2. Con .NET 10 XP recorre con DirectoryInfo.GetFiles, que ya
    // trae los datos de cada entrada en el mismo recorrido (en Linux, readdir): Form1.fuenteListado queda en null (Form1.Biblioteca.cs).
        // EN LINUX: (XP) [StructLayout(LayoutKind.Sequential)]
        // EN LINUX: (XP) private struct Filetime { public uint Bajo; public uint Alto; }
        // EN LINUX: (XP) [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        // EN LINUX: (XP) private struct FindData
        // EN LINUX: (XP) {
            // EN LINUX: (XP) public uint dwFileAttributes;
            // EN LINUX: (XP) public Filetime ftCreationTime, ftLastAccessTime, ftLastWriteTime;
            // EN LINUX: (XP) public uint nFileSizeHigh, nFileSizeLow, dwReserved0, dwReserved1;
            // EN LINUX: (XP) [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
            // EN LINUX: (XP) [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
        // EN LINUX: (XP) }
        // EN LINUX: (XP) [DllImport("kernel32.dll", EntryPoint = "FindFirstFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        // EN LINUX: (XP) private static extern IntPtr FindFirstFile(string patron, out FindData datos);
        // EN LINUX: (XP) [DllImport("kernel32.dll", EntryPoint = "FindNextFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        // EN LINUX: (XP) private static extern bool FindNextFile(IntPtr buscador, out FindData datos);
        // EN LINUX: (XP) [DllImport("kernel32.dll", SetLastError = true)]
        // EN LINUX: (XP) private static extern bool FindClose(IntPtr buscador);
        // EN LINUX: (XP) private const int ErrorNoHayMas = 18;   // ERROR_NO_MORE_FILES
// EN LINUX: (XP, en blanco)
        // EN LINUX: (XP) // Lo que mide la estructura para Windows (tiene que ser 592 bytes): la bateria firmarapida lo comprueba en cualquier sistema.
        // EN LINUX: (XP) internal static int TamanoEstructura() { return Marshal.SizeOf(typeof(FindData)); }
// EN LINUX: (XP, en blanco)
        // EN LINUX: (XP) internal static bool Disponible()
        // EN LINUX: (XP) { return Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.Version.Major < 4; }
// EN LINUX: (XP, en blanco)
        // EN LINUX: (XP) internal static ListadoCarpeta Lee(string carpeta)
        // EN LINUX: (XP) {
            // EN LINUX: (XP) if (!ListadoCarpeta.RutaNormal(carpeta)) return null;   // solo rutas de unidad o de red, normales
            // EN LINUX: (XP) try { return ListadoCarpeta.Construye(carpeta, Entradas(carpeta)); }
            // EN LINUX: (XP) catch { return null; }   // cualquier fallo: lo de siempre, que lanza el error de siempre
        // EN LINUX: (XP) }
        // EN LINUX: (XP) private static IEnumerable<EntradaListado> Entradas(string carpeta)
        // EN LINUX: (XP) {
            // EN LINUX: (XP) FindData d;
            // EN LINUX: (XP) IntPtr h = FindFirstFile((carpeta[carpeta.Length - 1] == '\\' ? carpeta : carpeta + "\\") + "*", out d);
            // EN LINUX: (XP) if (h == new IntPtr(-1)) throw new IOException("FindFirstFile");   // sin permiso, desaparecida, demasiado larga...
            // EN LINUX: (XP) try
            // EN LINUX: (XP) {
                // EN LINUX: (XP) while (true)
                // EN LINUX: (XP) {
                    // EN LINUX: (XP) EntradaListado e;
                    // EN LINUX: (XP) e.Nombre = d.cFileName; e.Atributos = d.dwFileAttributes;
                    // EN LINUX: (XP) e.FechaEscritura = ((long)d.ftLastWriteTime.Alto << 32) | d.ftLastWriteTime.Bajo;
                    // EN LINUX: (XP) e.Largo = ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow;
                    // EN LINUX: (XP) yield return e;
                    // EN LINUX: (XP) if (!FindNextFile(h, out d))
                    // EN LINUX: (XP) {
                        // EN LINUX: (XP) if (Marshal.GetLastWin32Error() != ErrorNoHayMas) throw new IOException("FindNextFile");
                        // EN LINUX: (XP) yield break;
                    // EN LINUX: (XP) }
                // EN LINUX: (XP) }
            // EN LINUX: (XP) }
            // EN LINUX: (XP) finally { FindClose(h); }
        // EN LINUX: (XP) }
    // EN LINUX: (XP) }
}

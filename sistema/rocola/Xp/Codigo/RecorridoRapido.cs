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
    //   - ListadoWin32.Lee: las llamadas de Windows (solo con CLR 2 en Windows). Esta parte NO se ha podido ejecutar fuera de Windows.
//     EN LINUX: no esta (ver abajo).
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
}

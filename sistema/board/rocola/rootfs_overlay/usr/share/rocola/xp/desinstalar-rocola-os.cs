// DESINSTALAR ROCOLA OS DESDE WINDOWS XP (7-11-2026, el dueño: «Si haz un desinstalador»).
//
// Lo abre «Desinstalar_Rocola_OS.cmd» (doble clic), que lo compila con el compilador de C# que trae .NET en Windows XP: no se baja
// nada y lo que corre es este mismo texto. Hace lo mismo que «Volver a Windows XP» de Rocola OS (Ctrl+F3 -> Detalles -> «La rocola»,
// o rocola-desinstalar en la consola): devuelve al disco el arranque de Windows XP que el instalador guardo en
// \rocola-os\arranque-xp.original.bin (el codigo del primer sector y el hueco hasta la primera particion; la tabla de particiones
// no se toca). Antes lo comprueba todo y, si algo no cuadra, no escribe nada. Escribe primero el primer sector y despues el hueco:
// si se fuera la luz a medias, el primer sector ya es el de XP y XP prende (no usa el hueco). Lo lee otra vez para comprobarlo.
// Despues ofrece borrar la carpeta \rocola-os (el sistema de Rocola OS y lo que aprendio; la musica y la rocola de XP no estan ahi).
//
// NO BORRAR \rocola-os ANTES: el arranque de Rocola OS (GRUB) lee su menu de esa carpeta; sin ella la computadora no prende.
//
// C# 2.0 y .NET 2.0 SP1 (lo del A4): nada de var, lambdas ni funciones de .NET 2.0 SP2.
// Pruebas, en cualquier equipo y sin tocar discos: desinstalar-rocola-os.exe --prueba IMAGEN CARPETA [RESPUESTAS]
// (IMAGEN, un archivo que hace de disco; RESPUESTAS, una letra por pregunta: s = si, n = no; lo que diria en ventanas sale por texto).
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace RocolaOS
{
    internal interface IDisco : IDisposable
    {
        string Nombre { get; }
        int BytesPorSector { get; }
        byte[] Lee(long sector, int sectores);
        void Escribe(long sector, byte[] datos, int desde, int sectores);
        void AlDisco();
    }

    // El disco de verdad: \\.\PhysicalDriveN (hace falta una cuenta de administrador).
    internal sealed class DiscoWindows : IDisco
    {
        private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
        private const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000, IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string nombre, uint acceso, uint compartir, IntPtr seguridad, uint disposicion, uint banderas, IntPtr plantilla);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle h, uint codigo, IntPtr entrada, int tamEntrada, [Out] byte[] salida, int tamSalida, out int devueltos, IntPtr solapado);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int SetFilePointer(SafeFileHandle h, int distancia, IntPtr distanciaAlta, int metodo);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle h, [Out] byte[] datos, int cuantos, out int leidos, IntPtr solapado);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle h, byte[] datos, int cuantos, out int escritos, IntPtr solapado);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(SafeFileHandle h);

        private readonly SafeFileHandle h;
        private readonly string nombre;
        private readonly int bytesPorSector;
        public string Nombre { get { return nombre; } }
        public int BytesPorSector { get { return bytesPorSector; } }

        // El numero del disco en el que esta la unidad (por ejemplo «C:»). -1 si la unidad ocupa varios discos o no se puede saber.
        public static int DiscoDeLaUnidad(string unidad)
        {
            using (SafeFileHandle v = CreateFile(@"\\.\" + unidad, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (v.IsInvalid) throw new ErrorDeWindows("abrir la unidad " + unidad, Marshal.GetLastWin32Error());
                byte[] s = new byte[8 + 24 * 16];
                int n;
                if (!DeviceIoControl(v, IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, s, s.Length, out n, IntPtr.Zero))
                    throw new ErrorDeWindows("saber en que disco esta " + unidad, Marshal.GetLastWin32Error());
                if (BitConverter.ToInt32(s, 0) != 1) return -1;   // VOLUME_DISK_EXTENTS: cuantos tramos, y el primero (disco en el 8)
                return BitConverter.ToInt32(s, 8);
            }
        }

        public DiscoWindows(int numero)
        {
            nombre = @"\\.\PhysicalDrive" + numero;
            h = CreateFile(nombre, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h.IsInvalid) throw new ErrorDeWindows("abrir el disco " + numero, Marshal.GetLastWin32Error());
            byte[] g = new byte[24];   // DISK_GEOMETRY: los bytes por sector en el 20
            int n;
            if (!DeviceIoControl(h, IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, g, g.Length, out n, IntPtr.Zero))
            { int e = Marshal.GetLastWin32Error(); h.Close(); throw new ErrorDeWindows("leer como es el disco " + numero, e); }
            bytesPorSector = BitConverter.ToInt32(g, 20);
        }

        private void Va(long sector)
        {
            long b = sector * bytesPorSector;
            if (b < 0 || b > int.MaxValue) throw new InvalidOperationException("sector fuera de lo esperado: " + sector);
            if (SetFilePointer(h, (int)b, IntPtr.Zero, 0) == -1) throw new ErrorDeWindows("ir al sector " + sector, Marshal.GetLastWin32Error());
        }
        public byte[] Lee(long sector, int sectores)
        {
            Va(sector);
            byte[] d = new byte[sectores * bytesPorSector];
            int leidos;
            if (!ReadFile(h, d, d.Length, out leidos, IntPtr.Zero)) throw new ErrorDeWindows("leer el disco", Marshal.GetLastWin32Error());
            if (leidos != d.Length) throw new InvalidOperationException("el disco dio " + leidos + " bytes de " + d.Length);
            return d;
        }
        public void Escribe(long sector, byte[] datos, int desde, int sectores)
        {
            Va(sector);
            byte[] d = new byte[sectores * bytesPorSector];
            Array.Copy(datos, desde, d, 0, d.Length);
            int escritos;
            if (!WriteFile(h, d, d.Length, out escritos, IntPtr.Zero)) throw new ErrorDeWindows("escribir en el disco", Marshal.GetLastWin32Error());
            if (escritos != d.Length) throw new InvalidOperationException("el disco escribio " + escritos + " bytes de " + d.Length);
        }
        public void AlDisco() { FlushFileBuffers(h); }
        public void Dispose() { h.Close(); }
    }

    // Un archivo que hace de disco (las pruebas).
    internal sealed class DiscoArchivo : IDisco
    {
        private readonly FileStream f;
        private readonly string nombre;
        public DiscoArchivo(string ruta) { nombre = ruta; f = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); }
        public string Nombre { get { return nombre; } }
        public int BytesPorSector { get { return 512; } }
        public byte[] Lee(long sector, int sectores)
        {
            byte[] d = new byte[sectores * 512];
            f.Position = sector * 512;
            int hecho = 0;
            while (hecho < d.Length) { int r = f.Read(d, hecho, d.Length - hecho); if (r <= 0) throw new InvalidOperationException("la imagen se acabo"); hecho += r; }
            return d;
        }
        public void Escribe(long sector, byte[] datos, int desde, int sectores) { f.Position = sector * 512; f.Write(datos, desde, sectores * 512); }
        public void AlDisco() { f.Flush(); }
        public void Dispose() { f.Close(); }
    }

    internal sealed class ErrorDeWindows : Exception
    {
        public readonly int Codigo;
        public ErrorDeWindows(string que, int codigo) : base("No se pudo " + que + " (error " + codigo + ")") { Codigo = codigo; }
    }

    internal static class Programa
    {
        private const string Titulo = "Desinstalar Rocola OS";
        private const string Copia = "arranque-xp.original.bin";
        private static bool prueba;
        private static string respuestas = "";
        private static int respondidas;
        private static bool escrito;   // ya se escribio algo en el disco

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 3 && args[0] == "--prueba")
                {
                    prueba = true;
                    if (args.Length >= 4) respuestas = args[3];
                    return Desinstala(args[2], args[1]);
                }
                Application.EnableVisualStyles();
                return Desinstala(args.Length > 0 ? args[0] : null, null);
            }
            catch (Exception ex)
            {
                ErrorDeWindows w = ex as ErrorDeWindows;
                if (w != null && w.Codigo == 5)
                    Dice("Windows no me deja leer o escribir el disco: hace falta una cuenta de administrador.\n\nNo se tocó nada.", true);
                else if (escrito)
                    Dice("Algo falló a medias: " + ex.Message + "\n\nVuelve a abrir el desinstalador: revisa el disco y termina lo que falte. " +
                         "No borres la carpeta \\rocola-os mientras tanto.", true);
                else
                    Dice("Algo falló: " + ex.Message + "\n\nNo se tocó nada.", true);
                return 2;
            }
        }

        // ---------- lo que se ve ----------
        private static void Dice(string texto, bool malo)
        {
            if (prueba) { Console.WriteLine("[dice] " + texto.Replace("\n", "\n       ")); return; }
            Ventana(texto, malo);
        }
        private static bool Pregunta(string texto)
        {
            if (prueba)
            {
                bool si = respondidas < respuestas.Length && char.ToLowerInvariant(respuestas[respondidas]) == 's';
                respondidas++;
                Console.WriteLine("[pregunta] " + texto.Replace("\n", "\n           ") + "\n           -> " + (si ? "SI" : "NO"));
                return si;
            }
            return VentanaSiNo(texto);
        }
        // (aparte: en las pruebas no se carga nada de ventanas)
        private static void Ventana(string texto, bool malo)
        { MessageBox.Show(texto, Titulo, MessageBoxButtons.OK, malo ? MessageBoxIcon.Warning : MessageBoxIcon.Information); }
        private static bool VentanaSiNo(string texto)
        { return MessageBox.Show(texto, Titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes; }

        // ---------- el disco ----------
        // GRUB (el arranque de Rocola OS) lleva su nombre en el primer sector: «GRUB » (en boot.img, junto a «Geom», «Hard Disk» y
        // «Read»). El de Windows XP no lo lleva.
        private static bool EsGrub(byte[] d)
        {
            for (int i = 0; i + 4 <= 512 && i + 4 <= d.Length; i++)
                if (d[i] == (byte)'G' && d[i + 1] == (byte)'R' && d[i + 2] == (byte)'U' && d[i + 3] == (byte)'B') return true;
            return false;
        }
        private static bool Firma(byte[] d) { return d.Length >= 512 && d[510] == 0x55 && d[511] == 0xAA; }
        private static bool CodigoVacio(byte[] d) { for (int i = 0; i < 440; i++) if (d[i] != 0) return false; return true; }
        // Donde empieza la primera particion segun la tabla del disco (sectores). -1 si es un disco GPT.
        private static long PrimeraParticion(byte[] mbr)
        {
            long menor = long.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                int o = 446 + 16 * i;
                byte tipo = mbr[o + 4];
                if (tipo == 0) continue;
                if (tipo == 0xEE) return -1;
                long inicio = BitConverter.ToUInt32(mbr, o + 8);
                if (inicio < menor) menor = inicio;
            }
            return menor;
        }
        private static bool Iguales(byte[] a, int desdeA, byte[] b, int desdeB, int n)
        { for (int i = 0; i < n; i++) if (a[desdeA + i] != b[desdeB + i]) return false; return true; }

        // ---------- la carpeta ----------
        private static bool EnDiscoFijo(string carpeta)
        {
            try
            {
                string raiz = Path.GetPathRoot(Path.GetFullPath(carpeta));
                if (raiz == null || raiz.Length < 2 || raiz[1] != ':') return false;
                return new DriveInfo(raiz.Substring(0, 1)).DriveType == DriveType.Fixed;
            }
            catch { return false; }
        }
        // La carpeta \rocola-os: la de este archivo, si es esa; si no, se busca en los discos fijos (la que tiene la copia primero).
        private static string BuscaCarpeta(string pedida)
        {
            if (!string.IsNullOrEmpty(pedida))
            {
                try
                {
                    string p = Path.GetFullPath(pedida);
                    if (File.Exists(Path.Combine(p, Copia)) && (prueba || EnDiscoFijo(p))) return p;
                }
                catch { }
            }
            if (prueba) return pedida;
            string sinCopia = null;
            foreach (DriveInfo u in DriveInfo.GetDrives())
            {
                try
                {
                    if (u.DriveType != DriveType.Fixed || !u.IsReady) continue;
                    string c = Path.Combine(u.RootDirectory.FullName, "rocola-os");
                    if (File.Exists(Path.Combine(c, Copia))) return c;
                    if (sinCopia == null && Directory.Exists(c)) sinCopia = c;
                }
                catch { }
            }
            return sinCopia;
        }
        private static long Ocupa(string carpeta)
        {
            long t = 0;
            try { foreach (string f in Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories)) try { t += new FileInfo(f).Length; } catch { } }
            catch { }
            return t;
        }
        private static string Tamano(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0") + " GB";
            return Math.Max(1, b >> 20) + " MB";
        }
        private static void Apunta(string carpeta, string que)
        {
            try { File.AppendAllText(Path.Combine(carpeta, "sistema.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  " + que + "\n"); }
            catch { }
        }
        private static IDisco AbreDisco(string carpeta, string imagen)
        {
            if (imagen != null) return new DiscoArchivo(imagen);
            string unidad = Path.GetPathRoot(Path.GetFullPath(carpeta)).Substring(0, 2);
            int n = DiscoWindows.DiscoDeLaUnidad(unidad);
            if (n < 0) throw new InvalidOperationException("la unidad " + unidad + " ocupa varios discos (disco dinamico)");
            return new DiscoWindows(n);
        }

        // ---------- borrar la carpeta (ya sin Rocola OS en el arranque) ----------
        private static int OfreceBorrar(string carpeta, string antes)
        {
            string texto = antes + "¿Borrar también la carpeta " + carpeta + "? Ahí solo está Rocola OS: su sistema y lo que aprendió (ocupa " +
                           Tamano(Ocupa(carpeta)) + "). Tu música y la rocola de XP no están ahí.\n\n" +
                           "Si la borras, para volver a Rocola OS hay que instalarlo otra vez con la memoria USB.";
            if (!Pregunta(texto))
            {
                Dice("La carpeta " + carpeta + " se queda. La puedes borrar cuando quieras: ya no hace falta para prender.", false);
                return 0;
            }
            try { Directory.Delete(carpeta, true); }
            catch (Exception ex)
            {
                Dice("No se pudo borrar toda la carpeta (" + ex.Message + "). La rocola prende bien en XP igual; lo que quede de " + carpeta +
                     " se puede borrar a mano.", true);
                return 1;
            }
            Dice("Listo: se borró " + carpeta + ". Rocola OS quedó desinstalado.", false);
            return 0;
        }

        // ---------- todo ----------
        private static int Desinstala(string pedida, string imagen)
        {
            string carpeta = BuscaCarpeta(pedida);
            if (carpeta == null)
            {
                Dice("No encontré Rocola OS en este equipo (ninguna carpeta \\rocola-os en los discos).", false);
                return 1;
            }
            string copia = Path.Combine(carpeta, Copia);
            byte[] b = File.Exists(copia) ? File.ReadAllBytes(copia) : null;
            int n = 0;
            if (b != null)
            {
                n = b.Length / 512;
                try { n = int.Parse(File.ReadAllText(Path.Combine(carpeta, "arranque-xp.sectores")).Trim()); } catch { }
            }
            // la copia, si vale: entera, con firma, con codigo y sin GRUB (la de Windows XP, no una de Rocola OS)
            string copiaMala = null;
            if (b == null) copiaMala = "no está";
            else if (n < 1 || n > 2048 || b.Length != n * 512) copiaMala = "está incompleta";
            else if (!Firma(b) || CodigoVacio(b)) copiaMala = "no es un arranque";
            else if (EsGrub(b)) copiaMala = "es la de Rocola OS, no la de Windows XP";

            using (IDisco d = AbreDisco(carpeta, imagen))
            {
                if (d.BytesPorSector != 512)
                { Dice("El disco tiene sectores de " + d.BytesPorSector + " bytes (no de 512): no lo toco. No se tocó nada.", true); return 1; }
                byte[] a = d.Lee(0, b != null && copiaMala == null ? n : 1);
                if (!Firma(a)) { Dice("El primer sector del disco no tiene tabla de particiones (MBR): no lo toco. No se tocó nada.", true); return 1; }

                if (!EsGrub(a))
                {
                    // El disco ya no arranca con Rocola OS: borrar la carpeta no le hace nada al arranque.
                    return OfreceBorrar(carpeta, "La rocola ya prende en Windows XP: el arranque del disco ya no es el de Rocola OS.\n\n");
                }
                if (copiaMala != null)
                {
                    Dice("No puedo quitar Rocola OS del arranque: la copia del arranque de Windows XP (" + copia + ") " + copiaMala + ".\n\n" +
                         "NO borres la carpeta " + carpeta + ": sin ella la computadora no prende.\n\n" +
                         "Se arregla con el CD de Windows XP: prende con el CD, en la primera pantalla presiona R (consola de recuperación), " +
                         "elige la instalación de Windows y escribe fixmbr. Después ya se puede borrar la carpeta.\n\n" +
                         "No se tocó nada.", true);
                    return 1;
                }
                long primera = PrimeraParticion(a);
                if (primera < 0) { Dice("El disco es GPT, no el de Windows XP: no lo toco. No se tocó nada.", true); return 1; }
                if (primera < n)
                {
                    Dice("Una partición del disco empieza en el sector " + primera + ", antes de donde acaba el arranque que se devolvería (" + n +
                         " sectores): no lo toco. No se tocó nada.", true);
                    return 1;
                }
                if (!Pregunta("Esto quita Rocola OS del arranque de la rocola:\n\n" +
                              "- Se devuelve al disco el arranque de Windows XP que se guardó al instalar Rocola OS (" + n + " sectores; la tabla de particiones no se toca).\n" +
                              "- Después la rocola prende siempre directo en Windows XP, sin menú.\n\n" +
                              "Tu música y la rocola de XP no se tocan.\n\n¿Seguir?"))
                {
                    Dice("No se tocó nada.", false);
                    return 1;
                }
                // lo nuevo: el codigo de XP en el primer sector con la tabla de ahora, y el hueco de XP
                byte[] nuevo = new byte[n * 512];
                Array.Copy(b, nuevo, n * 512);
                Array.Copy(a, 446, nuevo, 446, 66);
                escrito = true;
                d.Escribe(0, nuevo, 0, 1);
                if (n > 1) d.Escribe(1, nuevo, 512, n - 1);
                d.AlDisco();
                byte[] v = d.Lee(0, n);
                if (!Iguales(v, 0, nuevo, 0, nuevo.Length))
                {
                    Dice("Lo escrito en el disco no quedó igual al leerlo otra vez. Vuelve a abrir el desinstalador. No borres la carpeta " + carpeta + ".", true);
                    return 2;
                }
            }
            Apunta(carpeta, "se devolvio el arranque de Windows XP (desinstalador de Windows XP)");
            return OfreceBorrar(carpeta, "Listo: la rocola ya prende en Windows XP (se comprobó leyendo el disco otra vez).\n\n");
        }
    }
}

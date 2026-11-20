// ROCOLA OS (fase 11): EL GESTOR DE ARCHIVOS DE LA VENTANA DE CTRL+F4, lo que no es la hoja (peticion del dueno del 20-11-2026: «un gestor de
// archivos basico solo para lo de la rocola, ahi seria mas facil meter las keys que se requieran»; FASE11_navegador-archivos.md). Solo con
// ROCOLA_OS. La hoja (lo que se ve y los botones) esta en RocolaOs.ArchivosHoja.cs; las piezas propias (la lista y el editor), en
// RocolaOs.ArchivosPiezas.cs.
//
// LO QUE ALCANZA, Y NADA MAS:
//   - LOS DATOS DE LA ROCOLA: la carpeta de datos de rocola-xp (/rocola/datos/xp, la RutaDatos de XP, ROCOLA_DATOS) con sus subcarpetas: se
//     ve, se copia a ella desde una USB, se renombra, se borra (con confirmacion) y se editan los archivos de texto chicos (hasta 64 KB).
//   - LAS MEMORIAS USB CONECTADAS, DE SOLO LECTURA: las particiones de un disco que se puede sacar (removable=1 o colgado de USB en /sys). Las
//     ya montadas se ven donde estan; las que no, se montan aqui de solo lectura (ro,nosuid,nodev,noexec) en /run/rocola-xp/usb/<particion>
//     y se desmontan al cerrar la ventana. Nunca se escribe en ellas. (Las pruebas: ROCOLA_ARCHIVOS_USB=carpeta[:carpeta...], sin montar.)
// NUNCA FUERA DE ESAS CARPETAS: cada ruta se resuelve con realpath (los «..» y los enlaces, hasta el final) y tiene que quedar dentro de su
// raiz; un enlace que sale no se lista (se cuenta) y no se sigue. Un nombre nuevo (renombrar, archivo nuevo) es un nombre solo: sin «/», ni
// «.» ni «..», sin caracteres de control, de 1 a 255 bytes. Lo que se escribe va a un archivo de paso en la misma carpeta y se cambia de nombre
// (de un tiron); un destino que es un enlace no se escribe.
//
// LAS LLAVES: un archivo cuyo nombre lleve llave, key, api, token, secret, clave, password, contrasena, credencial o credential, o .pem, o los
// conocidos (rocola_github.txt, rocola_buzon.txt, rocola_genius.txt, rocola_spotify_llave.txt, rocola_spotify_api.txt), o que este dentro de
// una carpeta soloist/ o spotify/ (las sesiones), es una LLAVE: se guarda (y se copia) con permisos 0600 y en la pantalla se ve TAPADA (cada
// renglon, salvo sus 3 primeros y sus 3 ultimos caracteres, con «•»; los renglones de 6 o menos, enteros tapados), salvo con «Mostrar», que
// pide confirmar y vale hasta cerrar el archivo. rocola_spotify.txt de una USB (la llave de Soloist, como la trae el dueno: SPOTIFY_SOLOIST.md)
// se copia como rocola_spotify_llave.txt (en los datos, rocola_spotify.txt es la bitacora de Spotify, que el buzon SI sube).
// Donde las esperan las ordenes que ya existen: las de Spotify (rocola_spotify_llave.txt y rocola_spotify_api.txt) aqui mismo, donde «spotify
// llave», rocola-soloist y el buzon las leen; las de GitHub, del buzon y de Genius (rocola_github.txt, rocola_buzon.txt, rocola_genius.txt), que
// esas ordenes guardan en /rocola/datos, se dejan aqui y rocola-actualiza y rocola-buzon las toman de aqui tambien (como de una USB).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    // UNA RAIZ: los datos de la rocola o una USB
    internal sealed class RocolaOsRaiz
    {
        internal string Nombre;        // lo que dice su boton
        internal string Ruta;          // ya resuelta (realpath)
        internal bool Escribe;         // solo los datos
        internal string Montada;       // si la monto la rocola (se desmonta al cerrar)
        internal string Clave;         // para saber si es la misma de antes (la ruta)
    }
    // UN RENGLON DE LA LISTA
    internal sealed class RocolaOsEntrada
    {
        internal string Nombre, Ruta;
        internal bool Carpeta, Subir, Llave, Enlace;
        internal long Tamano;
        internal DateTime Fecha;
    }

    internal static class RocolaOsArchivos
    {
        internal const string Nombre = "archivos";
        internal const int MaxTexto = 64 * 1024;

        // ---------- realpath ----------
        [DllImport("libc", EntryPoint = "realpath", SetLastError = true)] private static extern IntPtr realpath_([MarshalAs(UnmanagedType.LPUTF8Str)] string ruta, IntPtr resuelta);
        [DllImport("libc", EntryPoint = "free")] private static extern void free_(IntPtr p);
        internal static string Real(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return null;
            IntPtr r = realpath_(ruta, IntPtr.Zero);
            if (r == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUTF8(r); } finally { free_(r); }
        }
        // ¿la ruta (resuelta del todo) queda dentro de la raiz (ya resuelta)?
        internal static bool Dentro(string raizReal, string ruta)
        {
            string r = Real(ruta);
            return r != null && raizReal != null && (r == raizReal || r.StartsWith(raizReal.TrimEnd('/') + "/", StringComparison.Ordinal));
        }
        internal static bool EsEnlace(string ruta)
        {
            try { FileSystemInfo i = new FileInfo(ruta); if (!i.Exists) i = new DirectoryInfo(ruta); return i.Exists && i.LinkTarget != null; }
            catch { return false; }
        }
        // un nombre solo (para renombrar y archivos nuevos); null si vale, o por que no
        internal static string NombreNoVale(string n)
        {
            if (n == null || n.Trim().Length == 0) return "Escribe un nombre.";
            if (n != n.Trim()) return "Sin espacios al principio ni al final.";
            if (n == "." || n == "..") return "Ese nombre no vale.";
            if (n.IndexOf('/') >= 0) return "Sin «/»: es un nombre, no una ruta.";
            foreach (char c in n) if (c < ' ' || c == 127) return "Sin caracteres de control.";
            if (Encoding.UTF8.GetByteCount(n) > 255) return "Demasiado largo.";
            return null;
        }

        // ---------- las llaves ----------
        private static readonly string[] partesDeLlave = { "llave", "key", "api", "token", "secret", "clave", "password", "contrasena", "contraseña", "credencial", "credential", ".pem" };
        private static readonly string[] llavesConocidas = { "rocola_github.txt", "rocola_buzon.txt", "rocola_genius.txt", "rocola_spotify_llave.txt", "rocola_spotify_api.txt" };
        internal static bool EsLlave(string ruta, string raiz)
        {
            string n = Path.GetFileName(ruta).ToLowerInvariant();
            foreach (string k in llavesConocidas) if (n == k) return true;
            foreach (string p in partesDeLlave) if (n.Contains(p)) return true;
            // dentro de soloist/ o spotify/ (las sesiones de Spotify)
            string rel = raiz != null && ruta.StartsWith(raiz, StringComparison.Ordinal) ? ruta.Substring(raiz.Length) : ruta;
            foreach (string c in rel.Split('/'))
            {
                string l = c.ToLowerInvariant();
                if (c.Length > 0 && c != Path.GetFileName(ruta) && (l == "soloist" || l == "spotify")) return true;
            }
            return false;
        }
        // un renglon tapado: los 3 primeros y los 3 ultimos, lo de en medio «•» (con 6 o menos, todo); la misma cantidad de caracteres
        internal static string Tapa(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            char[] c = s.ToCharArray();
            if (c.Length <= 6) { for (int i = 0; i < c.Length; i++) c[i] = '•'; return new string(c); }
            for (int i = 3; i < c.Length - 3; i++) c[i] = '•';
            return new string(c);
        }
        internal static void Permisos(string ruta, bool llave)
        {
            try { File.SetUnixFileMode(ruta, llave ? (UnixFileMode.UserRead | UnixFileMode.UserWrite) : (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead)); }
            catch { }
        }

        // ---------- las raices ----------
        internal static RocolaOsRaiz Datos()
        {
            string d = Form1.CarpetaDatos;
            try { Directory.CreateDirectory(d); } catch { }
            string r = Real(d) ?? d;
            return new RocolaOsRaiz { Nombre = "Datos de la rocola", Ruta = r, Escribe = true, Clave = r };
        }
        private static readonly object cerrojo = new object();
        private static List<RocolaOsRaiz> usbs = new List<RocolaOsRaiz>();
        private static readonly List<string> montadas = new List<string>();
        internal static RocolaOsRaiz[] Usbs { get { lock (cerrojo) return usbs.ToArray(); } }
        internal static volatile bool BuscandoUsb;
        // MIRAR LAS USB (en un hilo): las de la prueba, o las de /sys y /proc/mounts (montando de solo lectura las que no lo estan)
        internal static void BuscaUsbs()
        {
            if (BuscandoUsb) return;
            BuscandoUsb = true;
            RocolaOs.Hilo("archivos-usb", delegate
            {
                try
                {
                    List<RocolaOsRaiz> l = new List<RocolaOsRaiz>();
                    string prueba = Environment.GetEnvironmentVariable("ROCOLA_ARCHIVOS_USB");
                    if (prueba != null)
                    {
                        int k = 0;
                        foreach (string c in prueba.Split(':'))
                        {
                            if (c.Length == 0 || !Directory.Exists(c)) continue;
                            string r = Real(c); k++;
                            l.Add(new RocolaOsRaiz { Nombre = "USB " + Path.GetFileName(r.TrimEnd('/')), Ruta = r, Clave = r });
                        }
                    }
                    else l = UsbsDelSistema();
                    lock (cerrojo) usbs = l;
                }
                catch (Exception ex) { RocolaOs.Falla(Nombre, "buscar las USB", ex); }
                finally { BuscandoUsb = false; }
            });
        }
        private static List<RocolaOsRaiz> UsbsDelSistema()
        {
            List<RocolaOsRaiz> l = new List<RocolaOsRaiz>();
            Dictionary<string, string> montaje = new Dictionary<string, string>(StringComparer.Ordinal);   // /dev/sdb1 -> donde
            try
            {
                foreach (string ln in File.ReadAllLines("/proc/mounts"))
                {
                    string[] c = ln.Split(' ');
                    if (c.Length > 2 && c[0].StartsWith("/dev/", StringComparison.Ordinal) && !montaje.ContainsKey(c[0])) montaje[c[0]] = c[1].Replace("\\040", " ");
                }
            }
            catch { }
            string[] bloques;
            try { bloques = Directory.GetDirectories("/sys/class/block"); } catch { return l; }
            Array.Sort(bloques, StringComparer.Ordinal);
            foreach (string b in bloques)
            {
                string dev = Path.GetFileName(b);
                if (dev.StartsWith("loop", StringComparison.Ordinal) || dev.StartsWith("ram", StringComparison.Ordinal) || dev.StartsWith("zram", StringComparison.Ordinal)) continue;
                bool particion = File.Exists(Path.Combine(b, "partition"));
                string real = Real(b) ?? b;
                string disco = particion ? Path.GetFileName(Path.GetDirectoryName(real)) : dev;
                // un disco entero con particiones no se usa (se usan sus particiones)
                if (!particion) { bool tiene = false; try { foreach (string h in Directory.GetDirectories(real)) if (File.Exists(Path.Combine(h, "partition"))) { tiene = true; break; } } catch { } if (tiene) continue; }
                string rem = Lee("/sys/class/block/" + disco + "/removable");
                bool usb = rem == "1" || real.Contains("/usb", StringComparison.Ordinal);
                if (!usb) continue;
                string donde;
                bool nuestra = false;
                if (montaje.TryGetValue("/dev/" + dev, out donde))
                {
                    if (donde == "/" || donde.StartsWith("/rocola/datos", StringComparison.Ordinal) || donde == "/boot") continue;   // (Rocola OS arrancado desde esa USB)
                }
                else
                {
                    donde = RocolaOs.Run("rocola-xp/usb/" + dev);
                    try { Directory.CreateDirectory(donde); } catch { continue; }
                    string sal; int r = CorreOrden("mount", new[] { "-o", "ro,nosuid,nodev,noexec", "/dev/" + dev, donde }, 15000, out sal);
                    if (r != 0) { try { Directory.Delete(donde); } catch { } continue; }   // (sin sistema de archivos que se entienda)
                    nuestra = true;
                    lock (cerrojo) if (!montadas.Contains(donde)) montadas.Add(donde);
                    RocolaOs.Apunta(Nombre, "USB " + dev + " montada de solo lectura");
                }
                string rd = Real(donde) ?? donde;
                long tam = 0; long.TryParse(Lee(b + "/size"), out tam);
                l.Add(new RocolaOsRaiz { Nombre = "USB " + dev + (tam > 0 ? " (" + Medida(tam * 512) + ")" : ""), Ruta = rd, Montada = nuestra ? donde : null, Clave = "/dev/" + dev });
            }
            return l;
        }
        // al cerrar la ventana: las que monto la rocola, fuera (en un hilo)
        internal static void Desmonta()
        {
            string[] m; lock (cerrojo) { m = montadas.ToArray(); montadas.Clear(); usbs = new List<RocolaOsRaiz>(); }
            if (m.Length == 0) return;
            RocolaOs.Hilo("archivos-desmonta", delegate
            {
                foreach (string d in m)
                {
                    string sal; int r = CorreOrden("umount", new[] { d }, 15000, out sal);
                    if (r == 0) try { Directory.Delete(d); } catch { }
                    else RocolaOs.Apunta(Nombre, "no se pudo desmontar " + d + ": " + sal.Trim());
                }
            });
        }
        private static string Lee(string ruta) { try { return File.ReadAllText(ruta).Trim(); } catch { return ""; } }
        internal static int CorreOrden(string orden, string[] args, int ms, out string salida)
        {
            salida = "";
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(orden) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string a in args) si.ArgumentList.Add(a);
                using (Process p = Process.Start(si))
                {
                    string e = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(ms)) { try { p.Kill(); } catch { } salida = "no acabo"; return -1; }
                    salida = e; return p.ExitCode;
                }
            }
            catch (Exception ex) { salida = ex.Message; return -2; }
        }

        // ---------- la lista de una carpeta ----------
        internal static List<RocolaOsEntrada> Lista(RocolaOsRaiz raiz, string dir, out int fuera, out string error)
        {
            fuera = 0; error = null;
            List<RocolaOsEntrada> l = new List<RocolaOsEntrada>();
            if (!Dentro(raiz.Ruta, dir)) { error = "Esa carpeta no es de la rocola."; return l; }
            if (dir != raiz.Ruta) l.Add(new RocolaOsEntrada { Nombre = "..", Ruta = Path.GetDirectoryName(dir), Carpeta = true, Subir = true });
            List<RocolaOsEntrada> carpetas = new List<RocolaOsEntrada>(), archivos = new List<RocolaOsEntrada>();
            try
            {
                foreach (string e in Directory.EnumerateFileSystemEntries(dir))
                {
                    try
                    {
                        FileSystemInfo i = new FileInfo(e);
                        bool esDir = (i.Attributes & FileAttributes.Directory) != 0;
                        if (esDir) i = new DirectoryInfo(e);
                        bool enlace = i.LinkTarget != null;
                        if (enlace)
                        {
                            if (!Dentro(raiz.Ruta, e)) { fuera++; continue; }   // un enlace que sale: ni se lista
                            esDir = Directory.Exists(e);
                        }
                        RocolaOsEntrada x = new RocolaOsEntrada { Nombre = i.Name, Ruta = e, Carpeta = esDir, Enlace = enlace, Fecha = i.LastWriteTime };
                        if (!esDir) { x.Tamano = enlace ? new FileInfo(Real(e)).Length : ((FileInfo)i).Length; x.Llave = EsLlave(e, raiz.Ruta); }
                        (esDir ? carpetas : archivos).Add(x);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { error = "No se pudo leer: " + ex.Message; }
            Comparison<RocolaOsEntrada> orden = (a, b) => { int c = string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : string.CompareOrdinal(a.Nombre, b.Nombre); };
            carpetas.Sort(orden); archivos.Sort(orden);
            l.AddRange(carpetas); l.AddRange(archivos);
            return l;
        }
        internal static string Medida(long b)
        {
            if (b < 1024) return b + " B";
            double v = b / 1024.0; string u = "KB";
            if (v >= 1024) { v /= 1024; u = "MB"; }
            if (v >= 1024) { v /= 1024; u = "GB"; }
            return (v < 10 ? v.ToString("0.0", CultureInfo.InvariantCulture) : Math.Round(v).ToString(CultureInfo.InvariantCulture)).Replace('.', ',') + " " + u;
        }
        internal static long Libre(string dir) { try { return new DriveInfo(dir).AvailableFreeSpace; } catch { return -1; } }

        // ---------- leer y guardar un texto ----------
        internal sealed class Texto
        {
            internal string Contenido;      // con «\n» (sin CR)
            internal bool Crlf, Bom, FinalConSalto, SoloLeer;
            internal string Aviso;          // por que solo se lee, o null
        }
        // null y el porque si no se puede abrir como texto
        internal static Texto LeeTexto(string ruta, out string error)
        {
            error = null;
            try
            {
                FileInfo i = new FileInfo(ruta);
                if (i.Length > MaxTexto) { error = "Mide " + Medida(i.Length) + ": aquí solo se abren los de texto de hasta 64 KB."; return null; }
                byte[] b = File.ReadAllBytes(ruta);
                if (b.Length > MaxTexto) { error = "Pasa de 64 KB."; return null; }
                if (Array.IndexOf(b, (byte)0) >= 0) { error = "No es de texto."; return null; }
                Texto t = new Texto();
                int ini = 0;
                if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { t.Bom = true; ini = 3; }
                string s;
                try { s = new UTF8Encoding(false, true).GetString(b, ini, b.Length - ini); }
                catch (DecoderFallbackException) { s = Encoding.Latin1.GetString(b, ini, b.Length - ini); t.SoloLeer = true; t.Aviso = "No está en UTF-8: solo se ve (al guardarlo cambiaría)."; }
                t.Crlf = s.Contains("\r\n", StringComparison.Ordinal);
                s = s.Replace("\r\n", "\n");
                t.FinalConSalto = s.EndsWith("\n", StringComparison.Ordinal);
                if (t.FinalConSalto) s = s.Substring(0, s.Length - 1);
                t.Contenido = s;
                return t;
            }
            catch (Exception ex) { error = "No se pudo leer: " + ex.Message; return null; }
        }
        // GUARDAR, de un tiron: el de paso en la misma carpeta (creado ya con sus permisos), fsync y cambio de nombre. Solo dentro de los datos.
        internal static string GuardaTexto(RocolaOsRaiz raiz, string ruta, string contenido, bool crlf, bool bom, bool finalConSalto, bool llave)
        {
            if (raiz == null || !raiz.Escribe) return "Ahí no se escribe.";
            string dir = Path.GetDirectoryName(ruta);
            if (!Dentro(raiz.Ruta, dir)) return "Esa carpeta no es de la rocola.";
            if (NombreNoVale(Path.GetFileName(ruta)) != null) return "Ese nombre no vale.";
            if (EsEnlace(ruta)) return "Es un enlace: no se escribe aquí.";
            if (Directory.Exists(ruta)) return "Es una carpeta.";
            string s = contenido.Replace("\r\n", "\n");
            if (finalConSalto && s.Length > 0 && !s.EndsWith("\n", StringComparison.Ordinal)) s += "\n";
            if (crlf) s = s.Replace("\n", "\r\n");
            byte[] datos = new UTF8Encoding(bom).GetPreamble();
            byte[] cuerpo = Encoding.UTF8.GetBytes(s);
            if (datos.Length + cuerpo.Length > MaxTexto) return "Pasa de 64 KB.";
            return Escribe(dir, ruta, delegate (Stream f) { f.Write(datos, 0, datos.Length); f.Write(cuerpo, 0, cuerpo.Length); }, llave);
        }
        private static string Escribe(string dir, string ruta, Action<Stream> que, bool llave)
        {
            string paso = Path.Combine(dir, "." + Path.GetFileName(ruta) + ".rocolaos-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            try
            {
                UnixFileMode modo = llave ? (UnixFileMode.UserRead | UnixFileMode.UserWrite) : (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                // (un archivo que no es llave conserva sus permisos de antes)
                if (!llave && File.Exists(ruta)) try { modo = File.GetUnixFileMode(ruta); } catch { }
                FileStreamOptions o = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode = modo };
                try { File.Delete(paso); } catch { }
                using (FileStream f = new FileStream(paso, o)) { que(f); f.Flush(true); }
                File.Move(paso, ruta, true);
                // (UnixCreateMode pasa por la umask: los permisos, otra vez y del todo)
                try { File.SetUnixFileMode(ruta, modo); } catch { }
                return null;
            }
            catch (Exception ex) { try { File.Delete(paso); } catch { } return "No se pudo guardar: " + ex.Message; }
        }

        // ---------- copiar de una USB a los datos ----------
        // el nombre con que queda en los datos (rocola_spotify.txt de una USB es la llave de Soloist: rocola_spotify_llave.txt)
        internal static string NombreAlCopiar(string nombre) { return string.Equals(nombre, "rocola_spotify.txt", StringComparison.OrdinalIgnoreCase) ? "rocola_spotify_llave.txt" : nombre; }
        internal static string Copia(RocolaOsRaiz origen, string ruta, RocolaOsRaiz datos, string dirDestino, bool reemplaza, out string destino)
        {
            destino = Path.Combine(dirDestino, NombreAlCopiar(Path.GetFileName(ruta)));
            if (origen == null || !Dentro(origen.Ruta, ruta)) return "Ese archivo no es de la USB.";
            if (datos == null || !datos.Escribe || !Dentro(datos.Ruta, dirDestino)) return "Esa carpeta no es de los datos.";
            if (Directory.Exists(ruta)) return "Aquí solo se copian archivos, no carpetas.";
            if (EsEnlace(destino)) return "En los datos hay un enlace con ese nombre: no se escribe.";
            if (Directory.Exists(destino)) return "En los datos hay una carpeta con ese nombre.";
            if (File.Exists(destino) && !reemplaza) return "YA_HAY";
            long tam; try { tam = new FileInfo(ruta).Length; } catch (Exception ex) { return "No se pudo leer: " + ex.Message; }
            long libre = Libre(dirDestino);
            if (libre >= 0 && tam + 16L * 1024 * 1024 > libre) return "No cabe: mide " + Medida(tam) + " y en los datos quedan " + Medida(libre) + " (se dejan 16 MB libres).";
            bool llave = EsLlave(destino, datos.Ruta) || EsLlave(ruta, origen.Ruta);
            string orig = ruta;
            return Escribe(dirDestino, destino, delegate (Stream f) { using (FileStream s = File.OpenRead(orig)) s.CopyTo(f, 1 << 16); }, llave);
        }

        // ---------- renombrar, borrar, carpeta nueva ----------
        internal static string Renombra(RocolaOsRaiz raiz, string ruta, string nuevo)
        {
            if (raiz == null || !raiz.Escribe) return "Ahí no se escribe.";
            string e = NombreNoVale(nuevo); if (e != null) return e;
            string dir = Path.GetDirectoryName(ruta);
            if (!Dentro(raiz.Ruta, dir) || ruta == raiz.Ruta) return "Eso no se renombra.";
            string destino = Path.Combine(dir, nuevo);
            if (File.Exists(destino) || Directory.Exists(destino) || EsEnlace(destino)) return "Ya hay algo que se llama así.";
            try
            {
                FileAttributes a = File.GetAttributes(ruta);
                if ((a & FileAttributes.Directory) != 0 && (a & FileAttributes.ReparsePoint) == 0) Directory.Move(ruta, destino); else File.Move(ruta, destino);
                // (si con el nombre nuevo es una llave, 0600; si deja de serlo, se queda como estaba: nada se abre de mas)
                if ((a & FileAttributes.Directory) == 0 && (a & FileAttributes.ReparsePoint) == 0 && EsLlave(destino, raiz.Ruta)) Permisos(destino, true);
                return null;
            }
            catch (Exception ex) { return "No se pudo: " + ex.Message; }
        }
        internal static string Borra(RocolaOsRaiz raiz, string ruta)
        {
            if (raiz == null || !raiz.Escribe) return "Ahí no se borra.";
            string dir = Path.GetDirectoryName(ruta);
            if (!Dentro(raiz.Ruta, dir) || Real(dir) == null || ruta.TrimEnd('/') == raiz.Ruta.TrimEnd('/')) return "Eso no se borra.";
            try
            {
                FileAttributes a = File.GetAttributes(ruta);
                if ((a & FileAttributes.Directory) != 0 && (a & FileAttributes.ReparsePoint) == 0)
                {
                    if (Directory.EnumerateFileSystemEntries(ruta).GetEnumerator().MoveNext()) return "La carpeta no está vacía: aquí solo se borran carpetas vacías.";
                    Directory.Delete(ruta, false);
                }
                else File.Delete(ruta);   // (un enlace: se borra el enlace, no a donde apunta)
                return null;
            }
            catch (Exception ex) { return "No se pudo: " + ex.Message; }
        }
    }
}

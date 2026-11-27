// ROCOLA OS (fase 11): LA PAPELERA DEL GESTOR DE ARCHIVOS DE CTRL+F4 (peticion del dueno del 21-11-2026: el gestor con un apartado de
// DESCARGAS y una PAPELERA; FASE11_navegador-archivos.md, «La papelera»; las descargas se quitaron con el navegador el 27-11-2026). Solo con ROCOLA_OS. Lo que se ve esta en RocolaOs.ArchivosHoja.cs.
//
// BORRAR en los datos de la rocola MANDA A LA PAPELERA (nada se borra de un golpe): la papelera es la carpeta «papelera» de
// los datos de rocola-xp (/rocola/datos/xp/papelera, 0700; no sale en la lista de los datos: es su propio apartado). Cada cosa tirada va en
// su carpeta (papelera/<id>/, 0700) con:
//   info.txt   de=datos, ruta=<de donde vino, relativa a su apartado, escapada>, cuando=<ms desde 1970, UTC>, bytes=, carpeta=0|1
//   c/<nombre> la cosa misma, MOVIDA (rename: misma carpeta de datos, mismo disco; conserva sus permisos: una llave sigue 0600)
// «Devolver» la pone donde estaba; si ya hay algo con ese nombre NO lo pisa: «nombre (devuelto).ext», «nombre (devuelto 2).ext»...; si su
// carpeta ya no esta, va a la raiz de su apartado (y se dice). «Borrar ya» (una) y «Vaciar papelera» (todo) piden confirmar.
// SE VACIA SOLA: lo de mas de 30 dias (por cuando se tiro, no por la fecha del archivo) y, si pasa el tope (papelera_tope_mb=, 64 MB de
// fabrica, de 16 a 256: va en lo aprendido, datos.img de 512 MB), lo mas viejo primero hasta quedar debajo. Se mira al arrancar, cada hora y
// despues de cada cosa que se tira. Una cosa que sola mide mas que el tope se borra al momento (la hoja lo avisa antes de confirmar).
// TODO EN UN HILO (RocolaOs.Hilo), nunca en el de la pantalla; un cerrojo para que dos no se pisen.
// Las llaves en la papelera: se ven SIEMPRE tapadas (sin «Mostrar»); por su ruta de antes (soloist/, spotify/) y por su nombre.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Rocola
{
    // UNA COSA EN LA PAPELERA
    internal sealed class RocolaOsTirado
    {
        internal string Id, Dir, Cosa, Nombre, De, Ruta;
        internal long Cuando, Bytes;
        internal bool Carpeta, Llave;
        internal string DeNombre { get { return "Datos de la rocola"; } }
    }

    internal static class RocolaOsPapelera
    {
        internal const string Carpeta = "papelera", ClaveTope = "papelera_tope_mb";
        internal const string DeDatos = "datos";
        internal const int Dias = 30;
        internal const long DiaMs = 24L * 3600 * 1000;
        private static readonly object cerrojo = new object();
        private const UnixFileMode Solo700 = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        internal static string Dir { get { return Path.Combine(Form1.CarpetaDatos, Carpeta); } }
        internal static long AhoraMs { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } }
        internal static long TopeBytes
        {
            get
            {
                int mb = 64;
                try { if (RocolaOs.ClaveDe(ClaveTope) != null) mb = RocolaOs.AjusteEntero(ClaveTope); } catch { }
                if (mb < 16 || mb > 256) mb = 64;
                return mb * 1024L * 1024;
            }
        }

        // la carpeta de la papelera, de verdad (no un enlace) y 0700; null si no se puede
        private static string Prepara()
        {
            string d = Dir;
            try
            {
                if (RocolaOsArchivos.EsEnlace(d)) return null;
                if (!Directory.Exists(d)) Directory.CreateDirectory(d, Solo700);
                try { File.SetUnixFileMode(d, Solo700); } catch { }
                string r = RocolaOsArchivos.Real(d);
                string datos = RocolaOsArchivos.Real(Form1.CarpetaDatos);
                if (r == null || datos == null || r != datos.TrimEnd('/') + "/" + Carpeta) return null;
                return r;
            }
            catch { return null; }
        }

        // ---------- tirar ----------
        // MANDA A LA PAPELERA lo elegido en los datos. null si se hizo (t: lo tirado), o por que no.
        internal static string Manda(RocolaOsRaiz raiz, string ruta, out RocolaOsTirado t)
        {
            t = null;
            if (raiz == null || !raiz.Escribe || raiz.Tipo != RocolaOsArchivos.TDatos) return "Ahí no se borra.";
            string nombre = Path.GetFileName(ruta.TrimEnd('/'));
            string dir = Path.GetDirectoryName(ruta.TrimEnd('/'));
            string dirReal = RocolaOsArchivos.Real(dir);
            if (string.IsNullOrEmpty(nombre) || nombre == "." || nombre == ".." || dirReal == null || !RocolaOsArchivos.Dentro(raiz.Ruta, dir) || ruta.TrimEnd('/') == raiz.Ruta.TrimEnd('/'))
                return "Eso no se borra.";
            string cual = dirReal.TrimEnd('/') + "/" + nombre;
            string pap = Prepara();
            if (pap == null) return "La papelera no se pudo preparar: no se borra nada.";
            if (cual == pap || cual.StartsWith(pap + "/", StringComparison.Ordinal)) return "Eso no se borra.";
            lock (cerrojo)
            {
                FileAttributes a;
                try { a = File.GetAttributes(cual); } catch (Exception ex) { return "No se pudo: " + ex.Message; }
                bool enlace = (a & FileAttributes.ReparsePoint) != 0;
                bool carpeta = (a & FileAttributes.Directory) != 0 && !enlace;
                if (carpeta)
                {
                    try { if (Directory.EnumerateFileSystemEntries(cual).GetEnumerator().MoveNext()) return "La carpeta no está vacía: aquí solo se borran carpetas vacías."; }
                    catch (Exception ex) { return "No se pudo: " + ex.Message; }
                }
                long bytes = 0;
                if (!carpeta && !enlace) try { bytes = new FileInfo(cual).Length; } catch { }
                string rel = cual.Length > raiz.Ruta.TrimEnd('/').Length ? cual.Substring(raiz.Ruta.TrimEnd('/').Length + 1) : nombre;
                string id = NuevoId();
                string idDir = Path.Combine(pap, id), c = Path.Combine(idDir, "c");
                try
                {
                    Directory.CreateDirectory(idDir, Solo700);
                    Directory.CreateDirectory(c, Solo700);
                    try { File.SetUnixFileMode(idDir, Solo700); File.SetUnixFileMode(c, Solo700); } catch { }
                    long cuando = AhoraMs;
                    string de = DeDatos;
                    EscribeInfo(idDir, de, rel, cuando, bytes, carpeta);
                    string dest = Path.Combine(c, nombre);
                    Mueve(cual, dest);
                    t = new RocolaOsTirado { Id = id, Dir = idDir, Cosa = dest, Nombre = nombre, De = de, Ruta = rel, Cuando = cuando, Bytes = bytes, Carpeta = carpeta, Llave = !carpeta && RocolaOsArchivos.EsLlave(cual, raiz.Ruta) };
                    return null;
                }
                catch (Exception ex)
                {
                    // (si no se movio, lo de la papelera fuera; lo elegido sigue donde estaba)
                    try { if (!File.Exists(Path.Combine(c, nombre)) && !Directory.Exists(Path.Combine(c, nombre))) Directory.Delete(idDir, true); } catch { }
                    return "No se pudo: " + ex.Message;
                }
            }
        }
        // MOVER SIN PISAR NI SEGUIR ENLACES: rename(2) de la cosa misma (un enlace se mueve el), con RENAME_NOREPLACE (renameat2, glibc 2.28+): si
        // ya hay algo en el destino falla en vez de pisarlo, sin hueco entre mirar y mover. Si el sistema de archivos no lo sabe hacer (EINVAL),
        // se mira antes y rename. Nunca copia (misma carpeta de datos, mismo disco: EXDEV seria un error).
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
        private static extern int renameat2_(int dfd1, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string viejo, int dfd2, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string nuevo, uint flags);
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "rename", SetLastError = true)]
        private static extern int rename_([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string viejo, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string nuevo);
        private const int AT_FDCWD = -100, EINVAL = 22, ENOSYS = 38, EEXIST = 17;
        internal static void Mueve(string de, string a)
        {
            int r = -1, e = ENOSYS;
            try { r = renameat2_(AT_FDCWD, de, AT_FDCWD, a, 1); if (r != 0) e = System.Runtime.InteropServices.Marshal.GetLastPInvokeError(); }
            catch (EntryPointNotFoundException) { }
            if (r == 0) return;
            if (e == EINVAL || e == ENOSYS)
            {
                if (File.Exists(a) || Directory.Exists(a) || RocolaOsArchivos.EsEnlace(a)) throw new IOException("ya hay algo en " + a);
                if (rename_(de, a) == 0) return;
                e = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            }
            throw new IOException(e == EEXIST ? "ya hay algo en " + a : "no se pudo mover (errno " + e + ")");
        }
        private static int sec;
        private static string NuevoId()
        {
            int n = Interlocked.Increment(ref sec);
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + Environment.ProcessId.ToString("x", CultureInfo.InvariantCulture) + "-" + n.ToString("x", CultureInfo.InvariantCulture);
        }
        private static bool IdVale(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (char ch in id) if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || ch == '-')) return false;
            return true;
        }
        internal static void EscribeInfo(string idDir, string de, string rel, long cuando, long bytes, bool carpeta)
        {
            string s = "de=" + de + "\nruta=" + Uri.EscapeDataString(rel) + "\ncuando=" + cuando.ToString(CultureInfo.InvariantCulture)
                + "\nbytes=" + bytes.ToString(CultureInfo.InvariantCulture) + "\ncarpeta=" + (carpeta ? "1" : "0") + "\n";
            string paso = Path.Combine(idDir, ".info.txt.paso"), info = Path.Combine(idDir, "info.txt");
            File.WriteAllText(paso, s, new UTF8Encoding(false));
            File.Move(paso, info, true);
        }

        // ---------- lo que hay ----------
        // lo que hay en la papelera, lo mas nuevo primero (en un hilo). Lo que esta a medias (sin info.txt o sin la cosa) no sale.
        internal static List<RocolaOsTirado> Lista()
        {
            List<RocolaOsTirado> l = new List<RocolaOsTirado>();
            string pap = Prepara();
            if (pap == null) return l;
            lock (cerrojo) l = ListaSinCerrojo(pap, null);
            l.Sort((a, b) => b.Cuando != a.Cuando ? b.Cuando.CompareTo(a.Cuando) : string.CompareOrdinal(b.Id, a.Id));
            return l;
        }
        private static List<RocolaOsTirado> ListaSinCerrojo(string pap, List<string> rotas)
        {
            List<RocolaOsTirado> l = new List<RocolaOsTirado>();
            string[] ds;
            try { ds = Directory.GetFileSystemEntries(pap); } catch { return l; }
            string datos = RocolaOsArchivos.Real(Form1.CarpetaDatos) ?? Form1.CarpetaDatos;
            foreach (string d in ds)
            {
                string id = Path.GetFileName(d);
                RocolaOsTirado t = Lee(d, id, datos);
                if (t != null) l.Add(t);
                else if (rotas != null) rotas.Add(d);
            }
            return l;
        }
        private static RocolaOsTirado Lee(string d, string id, string datos)
        {
            try
            {
                if (!IdVale(id) || RocolaOsArchivos.EsEnlace(d) || !Directory.Exists(d)) return null;
                string info = Path.Combine(d, "info.txt");
                if (!File.Exists(info) || RocolaOsArchivos.EsEnlace(info)) return null;
                Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string ln in File.ReadAllLines(info)) { int i = ln.IndexOf('='); if (i > 0) v[ln.Substring(0, i)] = ln.Substring(i + 1); }
                string de, ruta, s;
                if (!v.TryGetValue("de", out de) || de != DeDatos) return null;
                if (!v.TryGetValue("ruta", out ruta)) return null;
                ruta = Uri.UnescapeDataString(ruta);
                long cuando = 0, bytes = 0;
                if (!v.TryGetValue("cuando", out s) || !long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out cuando)) return null;
                if (v.TryGetValue("bytes", out s)) long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes);
                bool carpeta = v.TryGetValue("carpeta", out s) && s == "1";
                string c = Path.Combine(d, "c");
                if (RocolaOsArchivos.EsEnlace(c)) return null;
                string[] cs = Directory.GetFileSystemEntries(c);
                if (cs.Length != 1) return null;
                string cosa = cs[0], nombre = Path.GetFileName(cosa);
                if (nombre != Path.GetFileName(ruta)) return null;
                string raiz = datos;
                return new RocolaOsTirado { Id = id, Dir = d, Cosa = cosa, Nombre = nombre, De = de, Ruta = ruta, Cuando = cuando, Bytes = Math.Max(0, bytes), Carpeta = carpeta,
                    Llave = !carpeta && RocolaOsArchivos.EsLlave(Path.Combine(raiz, ruta), raiz) };
            }
            catch { return null; }
        }

        // ---------- devolver ----------
        // DEVUELVE lo tirado a donde estaba (sin pisar nada). null si se hizo (dondeQuedo: la ruta final), o por que no.
        internal static string Devuelve(RocolaOsTirado t, out string dondeQuedo, out bool otraCarpeta)
        {
            dondeQuedo = null; otraCarpeta = false;
            if (t == null) return "Elige algo de la papelera.";
            string pap = Prepara();
            if (pap == null) return "La papelera no se pudo preparar.";
            RocolaOsRaiz raiz = RocolaOsArchivos.Datos();
            lock (cerrojo)
            {
                string datos = RocolaOsArchivos.Real(Form1.CarpetaDatos) ?? Form1.CarpetaDatos;
                RocolaOsTirado ya = Lee(t.Dir, t.Id, datos);
                if (ya == null || ya.Cosa != t.Cosa) return "Ya no está en la papelera.";
                // la carpeta de antes, si sigue y es de su apartado (sin salir: realpath); si no, la raiz del apartado
                string rel = t.Ruta.Replace('\\', '/');
                string dirRel = Path.GetDirectoryName(rel) ?? "";
                string dir = raiz.Ruta;
                if (dirRel.Length > 0)
                {
                    string antes = Path.Combine(raiz.Ruta, dirRel);
                    string r = RocolaOsArchivos.Real(antes);
                    bool vale = r != null && Directory.Exists(r) && RocolaOsArchivos.Dentro(raiz.Ruta, r) && !EsDeLaRocola(r);
                    if (vale) dir = r; else otraCarpeta = true;
                }
                string destino = NombreLibre(dir, t.Nombre);
                if (destino == null) return "No hay un nombre libre para devolverlo.";
                try
                {
                    Mueve(t.Cosa, destino);
                }
                catch (Exception ex) { return "No se pudo: " + ex.Message; }
                try { Directory.Delete(t.Dir, true); } catch { }
                dondeQuedo = destino;
                return null;
            }
        }
        // (nada se devuelve dentro de la papelera)
        private static bool EsDeLaRocola(string r)
        {
            string pap = RocolaOsArchivos.Real(Dir);
            return pap != null && (r == pap || r.StartsWith(pap + "/", StringComparison.Ordinal));
        }
        // «notas.txt» o, si ya hay algo, «notas (devuelto).txt», «notas (devuelto 2).txt»...
        internal static string NombreLibre(string dir, string nombre)
        {
            string ext = Path.GetExtension(nombre), bas = Path.GetFileNameWithoutExtension(nombre);
            if (bas.Length == 0) { bas = nombre; ext = ""; }   // (.bashrc)
            for (int i = 0; i < 1000; i++)
            {
                string n = i == 0 ? nombre : bas + (i == 1 ? " (devuelto)" : " (devuelto " + i + ")") + ext;
                if (RocolaOsArchivos.NombreNoVale(n) != null) return null;
                string p = Path.Combine(dir, n);
                if (!File.Exists(p) && !Directory.Exists(p) && !RocolaOsArchivos.EsEnlace(p)) return p;
            }
            return null;
        }

        // ---------- borrar de verdad ----------
        internal static string BorraYa(RocolaOsTirado t)
        {
            if (t == null) return "Elige algo de la papelera.";
            string pap = Prepara();
            if (pap == null) return "La papelera no se pudo preparar.";
            lock (cerrojo)
            {
                if (Path.GetDirectoryName(t.Dir) != pap || !IdVale(t.Id)) return "Eso no es de la papelera.";
                try { Directory.Delete(t.Dir, true); return null; }   // (un enlace dentro se borra el, no a donde apunta)
                catch (Exception ex) { return "No se pudo: " + ex.Message; }
            }
        }
        // VACIA la papelera entera; cuantas cosas se borraron
        internal static int Vacia(out string error)
        {
            error = null;
            string pap = Prepara();
            if (pap == null) { error = "La papelera no se pudo preparar."; return 0; }
            int n = 0;
            lock (cerrojo)
            {
                foreach (string d in Directory.GetFileSystemEntries(pap))
                {
                    try
                    {
                        FileAttributes a = File.GetAttributes(d);
                        if ((a & FileAttributes.Directory) != 0 && (a & FileAttributes.ReparsePoint) == 0) Directory.Delete(d, true);
                        else File.Delete(d);
                        n++;
                    }
                    catch (Exception ex) { error = "No se pudo borrar todo: " + ex.Message; }
                }
            }
            return n;
        }

        // ---------- vaciarse sola ----------
        // lo de mas de 30 dias y, si pasa el tope, lo mas viejo primero; tambien lo que quedo a medias (de hace mas de 1 min). Devuelve cuantas
        // por vieja y cuantas por el tope.
        internal static void Purga(long ahora, long tope, out int viejas, out int porTope)
        {
            viejas = 0; porTope = 0;
            string pap = Prepara();
            if (pap == null) return;
            lock (cerrojo)
            {
                List<string> rotas = new List<string>();
                List<RocolaOsTirado> l = ListaSinCerrojo(pap, rotas);
                foreach (string d in rotas)
                {
                    try
                    {
                        // (a medias: solo si tiene mas de un minuto, por si es de otro que esta tirando ahora mismo)
                        DateTime m = Directory.Exists(d) ? Directory.GetLastWriteTimeUtc(d) : File.GetLastWriteTimeUtc(d);
                        if ((DateTime.UtcNow - m).TotalSeconds < 60) continue;
                        FileAttributes a = File.GetAttributes(d);
                        if ((a & FileAttributes.Directory) != 0 && (a & FileAttributes.ReparsePoint) == 0) Directory.Delete(d, true); else File.Delete(d);
                    }
                    catch { }
                }
                l.Sort((a, b) => a.Cuando != b.Cuando ? a.Cuando.CompareTo(b.Cuando) : string.CompareOrdinal(a.Id, b.Id));   // (lo mas viejo primero)
                List<RocolaOsTirado> quedan = new List<RocolaOsTirado>();
                foreach (RocolaOsTirado t in l)
                {
                    if (ahora - t.Cuando > Dias * DiaMs) { try { Directory.Delete(t.Dir, true); viejas++; } catch { } }
                    else quedan.Add(t);
                }
                long total = 0;
                foreach (RocolaOsTirado t in quedan) { t.Bytes = Mide(t.Cosa); total += t.Bytes; }
                foreach (RocolaOsTirado t in quedan)
                {
                    if (total <= tope) break;
                    try { Directory.Delete(t.Dir, true); total -= t.Bytes; porTope++; } catch { }
                }
            }
        }
        // lo que mide de verdad (sin seguir enlaces)
        internal static long Mide(string cosa)
        {
            try
            {
                FileAttributes a = File.GetAttributes(cosa);
                if ((a & FileAttributes.ReparsePoint) != 0) return 0;
                if ((a & FileAttributes.Directory) == 0) return new FileInfo(cosa).Length;
                long t = 0;
                foreach (string f in Directory.EnumerateFiles(cosa, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }))
                    try { FileInfo i = new FileInfo(f); if (i.LinkTarget == null) t += i.Length; } catch { }
                return t;
            }
            catch { return 0; }
        }
        internal static long Ocupa()
        {
            long t = 0;
            foreach (RocolaOsTirado x in Lista()) t += Mide(x.Cosa);
            return t;
        }
        // VACIARSE SOLA, en un hilo (al arrancar, cada hora y despues de tirar algo)
        private static int purgando;
        internal static void PurgaDeFondo(string por)
        {
            if (Interlocked.Exchange(ref purgando, 1) == 1) return;
            long tope = TopeBytes;
            RocolaOs.Hilo("archivos-papelera-sola", delegate
            {
                try
                {
                    int v, p; Purga(AhoraMs, tope, out v, out p);
                    if (v + p > 0) RocolaOs.Apunta(RocolaOsArchivos.Nombre, "la papelera se vacio sola (" + por + "): " + v + " de mas de " + Dias + " dias, " + p + " por el tope de " + (tope / 1048576) + " MB");
                }
                finally { Volatile.Write(ref purgando, 0); }
            });
        }
    }
}

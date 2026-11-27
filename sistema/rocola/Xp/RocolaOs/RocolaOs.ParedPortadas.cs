// LAS PORTADAS DE LA PARED, A LA MANO (Rocola OS, la pared con el catalogo; peticion del dueno del 27-11-2026: la pared de Spotify se quito el 22-11
// porque «cargan muy lento las caratulas»; al devolverla, «un disco del catalogo solo entra en la pared cuando su portada ya esta a la mano»).
//
// A LA MANO quiere decir una de dos cosas:
//   1. EL PAQUETE: la compilacion del catalogo (sistema/catalogo/arma_portadas.py, en .github/workflows/rocola-catalogo.yml) baja de antemano las
//      miniaturas del Cover Art Archive de los discos que van a la pared, empezando por los de los artistas mas famosos (unos 60 000, 600-900 MB),
//      y las publica junto al catalogo en la version «catalogo» de GitHub, en PARTES de unos 150 MB (el limite de GitHub es por archivo y por
//      publicacion): rocola_portadas.txt (el manifiesto: sha256 y bytes de cada parte) y rocola_portadas_NN.pak. La rocola las baja UNA VEZ, sin
//      llave y comprobadas por tamano y sha256 como el catalogo, al DISCO de la musica (ROCOLA_IA_DATOS/../catalogo/portadas-pack: nunca a los
//      512 MB de datos) y lee de ahi. Cada parte es independiente, asi que la que ya esta sirve aunque falten otras.
//   2. LA CACHE: una portada que ya se habia bajado (portadas/MBID.jpg junto al catalogo; sale del paquete al verse o del relleno de abajo).
// RocolaOsPgPortadas.ALaMano(mbid) lo dice y RocolaOsPgArma (RocolaOs.ParedGeneros.cs) solo mete en una letra los discos del catalogo que la
// tengan: nunca una casilla vacia esperando a la red.
//
// EL RELLENO: lo que el paquete no trae (o antes de que llegue) se baja de fondo, poco a poco (2 hilos con nice 19, con un tope por sesion; las
// primeras de cada letra, que son las de los artistas mas famosos) para que entre en la pared la proxima vez que se arme la letra (sube
// ManoSig y la pared se rearma, como mucho cada 30 s). Sin red, sin paquete ni cache, la pared es la de XP con lo tuyo y con nada del catalogo.
//
// EL FORMATO DE UNA PARTE (.pak, enteros de 32 bits little-endian):
//   «RPK1», cuantos, y por cada portada 16 bytes del MBID (sus 32 cifras hexadecimales), desplazamiento y largo (desde el principio de la
//   parte); despues, los JPEG uno tras otro. El MBID se compara en minusculas y sin guiones.
// EL MANIFIESTO (rocola_portadas.txt, claves=valor): formato=1, catalogo=VERSION (la del catalogo con que se armo), discos=N, bytes=N, partes=K y
//   parteN=NOMBRE,SHA256,BYTES (N desde 1).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        internal static class RocolaOsPgPaquete
        {
            internal const string Txt = "rocola_portadas.txt", Mirado = "rocola_portadas_mirado.txt";
            private struct Ent { internal int Parte; internal uint Off, Len; }
            private static readonly object cerrojo = new object();
            private static Dictionary<string, Ent> indice = new Dictionary<string, Ent>(StringComparer.Ordinal);
            private static string[] partes = new string[0];
            private static string carpeta;
            private static Thread hilo;
            private static string estado = "sin paquete";
            internal static long Bajados, Fallidos, Extraidas;
            internal static string Estado { get { return Volatile.Read(ref estado); } }
            internal static int Discos { get { lock (cerrojo) return indice.Count; } }
            internal static int Partes { get { lock (cerrojo) return partes.Length; } }
            internal static string Carpeta { get { return Volatile.Read(ref carpeta); } }

            // la clave de un MBID: 32 cifras hexadecimales en minusculas, sin guiones; null si no es un MBID
            internal static string Clave(string mbid)
            {
                if (string.IsNullOrEmpty(mbid) || mbid.Length > 40) return null;
                StringBuilder sb = new StringBuilder(32);
                foreach (char c in mbid)
                {
                    if (c == '-') continue;
                    char m = c >= 'A' && c <= 'F' ? (char)(c + 32) : c;
                    if (!((m >= '0' && m <= '9') || (m >= 'a' && m <= 'f'))) return null;
                    sb.Append(m);
                }
                return sb.Length == 32 ? sb.ToString() : null;
            }
            internal static bool Tiene(string mbid)
            {
                string k = Clave(mbid);
                if (k == null) return false;
                lock (cerrojo) return indice.ContainsKey(k);
            }

            internal static void Arranca(string dir)
            {
                lock (typeof(RocolaOsPgPaquete))
                {
                    if (hilo != null) return;
                    Volatile.Write(ref carpeta, Path.Combine(dir, "portadas-pack"));
                    hilo = RocolaOs.Hilo("pared_paquete", Corre);
                }
            }
            private static int EsperaMs
            {
                get
                {
                    string s = null; int v;
                    try { s = Environment.GetEnvironmentVariable("ROCOLA_CATALOGO_ESPERA_S"); } catch { }
                    return !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 0 ? v * 1000 : 45000;
                }
            }

            private static void Corre()
            {
                string dir = Carpeta;
                try { Directory.CreateDirectory(dir); }
                catch (Exception ex) { RocolaOs.Apunta("pared_generos", "no se pudo crear la carpeta del paquete de portadas (" + ex.GetType().Name + ")"); Volatile.Write(ref estado, "sin carpeta"); return; }
                // 1) LO QUE YA HAY (sin red): las partes comprobadas al bajarlas
                try { CargaIndice(); }
                catch (Exception ex) { RocolaOs.Falla("pared_generos", "al leer el paquete de portadas", ex); }
                // 2) EL DE LA RED: una vez al dia como mucho (con el catalogo, despues de el); sin red, a la hora
                Thread.Sleep(EsperaMs + 5000);
                while (true)
                {
                    long ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), mirado = LeeMirado(Path.Combine(dir, Mirado));
                    long falta = mirado + 86400 - ahora;
                    if (mirado > 0 && falta > 0 && mirado <= ahora) { Thread.Sleep((int)Math.Min(3600, falta) * 1000); continue; }
                    bool bien = false;
                    try { bien = Mira(dir); }
                    catch (Exception ex) { RocolaOs.Falla("pared_generos", "al mirar el paquete de portadas de la red", ex); }
                    if (bien) { try { RocolaOsPgDescarga.EscribeAtomico(Path.Combine(dir, Mirado), Encoding.ASCII.GetBytes(ahora.ToString(CultureInfo.InvariantCulture) + "\n")); } catch { } }
                    else Interlocked.Increment(ref Fallidos);
                    Thread.Sleep(3600 * 1000);
                }
            }
            private static long LeeMirado(string ruta)
            {
                try { long v; if (File.Exists(ruta) && long.TryParse(File.ReadAllText(ruta).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v; } catch { }
                return 0;
            }

            internal sealed class Parte { internal string Nombre = "", Sha = ""; internal long Bytes; }
            // el manifiesto: las partes que dice (vacio si no es uno)
            internal static List<Parte> LeeManifiesto(string texto)
            {
                List<Parte> l = new List<Parte>();
                bool formato = false;
                foreach (string r in texto.Split('\n'))
                {
                    string s = r.Trim();
                    int i = s.IndexOf('=');
                    if (i <= 0) continue;
                    string k = s.Substring(0, i).Trim().ToLowerInvariant(), v = s.Substring(i + 1).Trim();
                    if (k == "formato") formato = v == "1";
                    else if (k.StartsWith("parte", StringComparison.Ordinal) && k.Length > 5)
                    {
                        string[] c = v.Split(',');
                        long b;
                        // (el nombre es un archivo de la carpeta del paquete: sin rutas)
                        if (c.Length == 3 && c[0].Length > 0 && c[0].IndexOfAny(new[] { '/', '\\' }) < 0 && !c[0].StartsWith(".", StringComparison.Ordinal) && c[1].Trim().Length == 64
                            && long.TryParse(c[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b) && b > 16)
                            l.Add(new Parte { Nombre = c[0].Trim(), Sha = c[1].Trim().ToLowerInvariant(), Bytes = b });
                    }
                }
                if (!formato) l.Clear();
                return l;
            }

            // LA RED: el manifiesto (unos bytes) y cada parte que falte, comprobada por tamano y sha256 y puesta de una pieza
            private static bool Mira(string dir)
            {
                byte[] txt = RocolaOsPgDescarga.Baja(RocolaOsPgDescarga.Url + "/" + Txt, 256 * 1024);
                if (txt == null) { if (Estado == "sin paquete") Volatile.Write(ref estado, "no esta en la red"); return false; }
                List<Parte> remotas = LeeManifiesto(Encoding.UTF8.GetString(txt));
                if (remotas.Count == 0) { RocolaOs.Apunta("pared_generos", "el " + Txt + " de la red no vale"); return false; }
                long total = 0; foreach (Parte p in remotas) total += p.Bytes;
                bool todo = true;
                foreach (Parte p in remotas)
                {
                    string ruta = Path.Combine(dir, p.Nombre), ok = ruta + ".ok";
                    if (EstaBien(ruta, ok, p)) continue;
                    // el sitio: la parte mas 256 MB de reserva (como la cosecha de la IA)
                    try
                    {
                        long libre = new DriveInfo(dir).AvailableFreeSpace;
                        if (libre < p.Bytes + DatosGrandes.ReservaBytes) { RocolaOs.Apunta("pared_generos", "sin sitio para el paquete de portadas (" + (libre / 1048576).ToString(CultureInfo.InvariantCulture) + " MB libres)"); Volatile.Write(ref estado, "sin sitio"); return false; }
                    }
                    catch { }
                    if (!BajaParte(dir, p, ruta, ok)) { todo = false; continue; }   // (una parte mala no deja sin las demas: se vuelve a intentar en una hora)
                    Interlocked.Increment(ref Bajados);
                    CargaIndice();   // (cada parte ya sirve: no se espera a las demas)
                }
                if (!todo) return false;
                // las partes de un paquete de antes que el manifiesto ya no nombra
                try
                {
                    HashSet<string> nombres = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Parte p in remotas) { nombres.Add(p.Nombre); nombres.Add(p.Nombre + ".ok"); }
                    foreach (string f in Directory.GetFiles(dir, "rocola_portadas_*"))
                    {
                        string n = Path.GetFileName(f);
                        if ((n.EndsWith(".pak", StringComparison.Ordinal) || n.EndsWith(".ok", StringComparison.Ordinal) || n.EndsWith(".t", StringComparison.Ordinal)) && !nombres.Contains(n) && !n.Equals(Mirado, StringComparison.Ordinal))
                            try { File.Delete(f); } catch { }
                    }
                }
                catch { }
                CargaIndice();
                Volatile.Write(ref estado, "al dia");
                return true;
            }
            // una parte ya bajada: su archivo del tamano que dice el manifiesto y su «.ok» con el sha256 que dice
            private static bool EstaBien(string ruta, string ok, Parte p)
            {
                try
                {
                    FileInfo f = new FileInfo(ruta);
                    return f.Exists && f.Length == p.Bytes && File.Exists(ok) && string.Equals(File.ReadAllText(ok).Trim(), p.Sha, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }
            // (revision 27-11-2026) EL TOPE DE UNA PARTE: el Timeout del cliente (5 min) con ResponseHeadersRead solo cubre hasta las cabeceras, no la lectura:
            // una conexion colgada a media parte dejaria el hilo para siempre. Aqui, 20 minutos para cada parte (ROCOLA_PAQUETE_TOPE_S en las pruebas);
            // pasado el tope se corta la conexion, se borra lo bajado a medias y se vuelve a intentar a la hora, como los demas fallos
            internal static int TopeParteMs
            {
                get
                {
                    string s = null; int v;
                    try { s = Environment.GetEnvironmentVariable("ROCOLA_PAQUETE_TOPE_S"); } catch { }
                    return !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v > 0 ? v * 1000 : 20 * 60 * 1000;
                }
            }
            // (revision 27-11-2026) EL RITMO: unos 1 MB/s (1024 KB/s; los ~900 MB del paquete tardan unos 15 minutos), para no competir con el streaming de
            // Soloist por la red del bar (ROCOLA_PAQUETE_KBS en las pruebas; 0 = sin limite)
            internal static int LimiteKbs
            {
                get
                {
                    string s = null; int v;
                    try { s = Environment.GetEnvironmentVariable("ROCOLA_PAQUETE_KBS"); } catch { }
                    return !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 0 ? v : 1024;
                }
            }
            private static bool BajaParte(string dir, Parte p, string ruta, string ok)
            {
                string t = ruta + ".t";
                CancellationTokenSource cts = new CancellationTokenSource(TopeParteMs);
                try
                {
                    using (HttpResponseMessage r = RocolaOsPgDescarga.Http.Send(new HttpRequestMessage(HttpMethod.Get, RocolaOsPgDescarga.Url + "/" + p.Nombre), HttpCompletionOption.ResponseHeadersRead, cts.Token))
                    using (cts.Token.Register(delegate { try { r.Dispose(); } catch { } }))   // (al llegar el tope, la lectura que esta colgada se rompe)
                    {
                        if (r.StatusCode != HttpStatusCode.OK) { RocolaOsSpotifyCatalogoApi.ApuntaCallado("una parte del paquete de portadas no se bajo (" + ((int)r.StatusCode).ToString(CultureInfo.InvariantCulture) + ")"); return false; }
                        using (SHA256 sha = SHA256.Create())
                        using (Stream s = r.Content.ReadAsStream())
                        using (FileStream f = new FileStream(t, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buf = new byte[65536]; int n; long suma = 0;
                            int kbs = LimiteKbs; Stopwatch reloj = Stopwatch.StartNew();
                            while ((n = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                suma += n;
                                if (cts.IsCancellationRequested) throw new OperationCanceledException();
                                // el ritmo: lo bajado hasta ahora no puede ir mas deprisa que kbs KB por segundo
                                if (kbs > 0)
                                {
                                    long debeTardar = suma * 1000 / (kbs * 1024L), tarda = reloj.ElapsedMilliseconds;
                                    if (debeTardar > tarda) Thread.Sleep((int)Math.Min(debeTardar - tarda, 2000));
                                }
                                if (suma > p.Bytes) { RocolaOs.Apunta("pared_generos", "la parte " + p.Nombre + " mide de mas: no se usa"); f.Dispose(); try { File.Delete(t); } catch { } return false; }
                                f.Write(buf, 0, n); sha.TransformBlock(buf, 0, n, null, 0);
                            }
                            sha.TransformFinalBlock(buf, 0, 0);
                            f.Flush(true);
                            if (suma != p.Bytes) { RocolaOs.Apunta("pared_generos", "la parte " + p.Nombre + " no mide lo que dice (" + suma.ToString(CultureInfo.InvariantCulture) + " de " + p.Bytes.ToString(CultureInfo.InvariantCulture) + " bytes): no se usa"); f.Dispose(); try { File.Delete(t); } catch { } return false; }
                            string tiene = Convert.ToHexString(sha.Hash).ToLowerInvariant();
                            if (!string.Equals(tiene, p.Sha, StringComparison.OrdinalIgnoreCase)) { RocolaOs.Apunta("pared_generos", "la parte " + p.Nombre + " no es la que dice su sha256: no se usa"); f.Dispose(); try { File.Delete(t); } catch { } return false; }
                        }
                    }
                    if (File.Exists(ok)) File.Delete(ok);
                    File.Move(t, ruta, true);
                    RocolaOsPgDescarga.EscribeAtomico(ok, Encoding.ASCII.GetBytes(p.Sha + "\n"));
                    return true;
                }
                catch (Exception ex)
                {
                    try { File.Delete(t); } catch { }
                    RocolaOsSpotifyCatalogoApi.ApuntaCallado("una parte del paquete de portadas no se bajo (" + ex.GetType().Name + ")");
                    RocolaOs.Apunta("pared_generos", "la parte " + p.Nombre + " no se bajo (" + ex.GetType().Name + (cts.IsCancellationRequested ? ", tope de tiempo" : "") + "): se vuelve a intentar a la hora");
                    return false;
                }
                finally { cts.Dispose(); }
            }

            // EL INDICE de las partes que hay y valen (su «.ok»): lee solo la cabecera de cada una
            internal static void CargaIndice()
            {
                string dir = Carpeta;
                if (dir == null || !Directory.Exists(dir)) return;
                List<string> nombres = new List<string>();
                foreach (string f in Directory.GetFiles(dir, "rocola_portadas_*.pak")) if (File.Exists(f + ".ok")) nombres.Add(f);
                nombres.Sort(StringComparer.Ordinal);
                Dictionary<string, Ent> nuevo = new Dictionary<string, Ent>(StringComparer.Ordinal);
                List<string> validas = new List<string>();
                foreach (string f in nombres)
                {
                    try
                    {
                        using (FileStream s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            byte[] cab = new byte[8];
                            if (s.Read(cab, 0, 8) != 8 || cab[0] != (byte)'R' || cab[1] != (byte)'P' || cab[2] != (byte)'K' || cab[3] != (byte)'1') { RocolaOs.Apunta("pared_generos", "la parte " + Path.GetFileName(f) + " no es de portadas: no se usa"); continue; }
                            uint n = BitConverter.ToUInt32(cab, 4);
                            if (n > 4000000 || 8L + n * 24 > s.Length) { RocolaOs.Apunta("pared_generos", "la parte " + Path.GetFileName(f) + " trae un indice raro: no se usa"); continue; }
                            byte[] tab = new byte[n * 24];
                            int leido = 0; while (leido < tab.Length) { int k = s.Read(tab, leido, tab.Length - leido); if (k <= 0) break; leido += k; }
                            if (leido != tab.Length) continue;
                            int parte = validas.Count;
                            for (int i = 0; i < n; i++)
                            {
                                int o = i * 24;
                                string clave = Convert.ToHexString(tab, o, 16).ToLowerInvariant();
                                uint off = BitConverter.ToUInt32(tab, o + 16), len = BitConverter.ToUInt32(tab, o + 20);
                                if (len < 2 || len > 2 * 1024 * 1024 || (long)off + len > s.Length) continue;
                                if (!nuevo.ContainsKey(clave)) nuevo[clave] = new Ent { Parte = parte, Off = off, Len = len };
                            }
                            validas.Add(f);
                        }
                    }
                    catch (Exception ex) { RocolaOs.Falla("pared_generos", "al leer " + Path.GetFileName(f), ex); }
                }
                bool cambio;
                lock (cerrojo)
                {
                    cambio = nuevo.Count != indice.Count || validas.Count != partes.Length;
                    indice = nuevo; partes = validas.ToArray();
                }
                if (nuevo.Count > 0 && Estado == "sin paquete") Volatile.Write(ref estado, "guardado");
                if (cambio)
                {
                    RocolaOs.Apunta("pared_generos", "paquete de portadas: " + nuevo.Count.ToString(CultureInfo.InvariantCulture) + " portadas en " + validas.Count.ToString(CultureInfo.InvariantCulture) + " partes");
                    Interlocked.Increment(ref RocolaOsPgPortadas.ManoSigValor);
                }
            }

            // SACAR UNA PORTADA del paquete a un archivo (la cache de portadas): sus bytes si salio, 0 si no esta o no se pudo. Un JPEG de verdad (FF D8).
            internal static long Extrae(string mbid, string ruta)
            {
                string k = Clave(mbid);
                if (k == null) return 0;
                Ent e; string parte;
                lock (cerrojo)
                {
                    if (!indice.TryGetValue(k, out e) || e.Parte >= partes.Length) return 0;
                    parte = partes[e.Parte];
                }
                try
                {
                    byte[] b = new byte[e.Len];
                    using (FileStream s = new FileStream(parte, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        s.Seek(e.Off, SeekOrigin.Begin);
                        int leido = 0; while (leido < b.Length) { int n = s.Read(b, leido, b.Length - leido); if (n <= 0) break; leido += n; }
                        if (leido != b.Length) return 0;
                    }
                    if (b[0] != 0xFF || b[1] != 0xD8) return 0;
                    RocolaOsPgDescarga.EscribeAtomico(ruta, b);
                    Interlocked.Increment(ref Extraidas);
                    return b.Length;
                }
                catch (Exception ex) { RocolaOsSpotifyCatalogoApi.ApuntaCallado("una portada del paquete no se pudo sacar (" + ex.GetType().Name + ")"); return 0; }
            }
        }

        // EL RELLENO: las portadas del catalogo que el paquete no trae, bajadas de fondo a la cache para que sus discos entren en la pared
        internal static class RocolaOsPgRelleno
        {
            private static readonly object cerrojo = new object();
            private static readonly Queue<string> cola = new Queue<string>();
            private static readonly HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private static bool arrancado;
            internal static long Bajadas, Encargadas, Sin404;
            private static int desdeElAviso, nuevos404;
            internal static int Tope
            {
                get
                {
                    string s = null; int v;
                    try { s = Environment.GetEnvironmentVariable("ROCOLA_PARED_RELLENO"); } catch { }
                    return !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 0 ? v : 3000;
                }
            }
            internal const int Hilos = 2, CadaCuantas = 25;

            // las que faltan de una letra recien armada (en el orden del archivo: las de los artistas mas famosos primero); lo ya pedido no se repite
            internal static void Encarga(List<string> mbids)
            {
                if (mbids == null || mbids.Count == 0 || RocolaOsPgPortadas.Carpeta == null) return;
                lock (cerrojo)
                {
                    foreach (string m in mbids)
                    {
                        if (Encargadas >= Tope || Interlocked.Read(ref Sin404) >= Tope) break;
                        // (revision 27-11-2026) lo que ya se sabe que no tiene portada (404) o fallo hace poco no entra ni gasta el tope; y lo que no se pudo
                        // bajar se descuenta al terminar (Corre): el tope cuenta lo que SI avanza
                        if (RocolaOsPgPortadas.NoVale(m) || !vistos.Add(m)) continue;
                        cola.Enqueue(m); Encargadas++;
                    }
                    if (!arrancado && cola.Count > 0)
                    {
                        arrancado = true;
                        for (int i = 0; i < Hilos; i++) RocolaOs.Hilo("pared_relleno_" + i.ToString(CultureInfo.InvariantCulture), Corre);
                    }
                    Monitor.PulseAll(cerrojo);
                }
            }
            private static void Corre()
            {
                while (true)
                {
                    string m;
                    while (RocolaOsPgPortadas.Hojeando) Thread.Sleep(500);   // (no mientras alguien hojea: la pagina que se mira va primero)
                    lock (cerrojo) { while (cola.Count == 0) Monitor.Wait(cerrojo); m = cola.Dequeue(); }
                    bool bien = false, fueALaRed = false;
                    try { bien = RocolaOsPgPortadas.BajaRelleno(m, out fueALaRed); } catch (Exception ex) { RocolaOs.Falla("pared_generos", "en el relleno de portadas", ex); }
                    if (!bien)
                    {
                        // (revision 27-11-2026) un 404 o un fallo no cuentan para el tope de la sesion; si fue un fallo pasajero (no un 404), podra volver a
                        // encargarse cuando se arme otra letra
                        bool es404 = RocolaOsPgPortadas.SinPortada(m);
                        lock (cerrojo) { if (Encargadas > 0) Encargadas--; if (!es404) vistos.Remove(m); }
                        if (es404) { Interlocked.Increment(ref Sin404); Interlocked.Increment(ref nuevos404); }   // (tienen su propio tope: Tope de 404 por sesion)
                    }
                    if (bien)
                    {
                        Interlocked.Increment(ref Bajadas);
                        // (la pared se rearma por tandas: cada 25 que llegan, y al vaciarse la cola)
                        if (Interlocked.Increment(ref desdeElAviso) >= CadaCuantas) { Interlocked.Exchange(ref desdeElAviso, 0); Interlocked.Increment(ref RocolaOsPgPortadas.ManoSigValor); }
                    }
                    bool vacia; lock (cerrojo) vacia = cola.Count == 0;
                    // al vaciarse la cola, la pared se rearma si llego alguna portada o si se supo de unos 404: asi su siguiente tanda del archivo
                    // (los que estaban despues de esos 404) entra al relleno y no se queda atascado
                    if (vacia)
                    {
                        int nb = Interlocked.Exchange(ref desdeElAviso, 0), n4 = Interlocked.Exchange(ref nuevos404, 0);
                        if (nb > 0 || n4 > 0) Interlocked.Increment(ref RocolaOsPgPortadas.ManoSigValor);
                    }
                    if (fueALaRed || bien) Thread.Sleep(500);   // (sin prisa: 2 hilos, como mucho 4 por segundo; la red y la maquina son de la musica y de la pantalla primero)
                }
            }
        }
    }
}

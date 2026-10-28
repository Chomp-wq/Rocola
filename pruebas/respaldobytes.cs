using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
// EL RESPALDO DEL ARRANQUE, COMPROBADO POR BYTES Y NO POR DOS HASH (19-10-2026): Form1.Datos.cs (BackupToCapturado, CompruebaCopiaDeRespaldo).
//
// Al arrancar, BackupLearnedData copia las tablas aprendidas a respaldo\<fecha>\. Cada archivo se leia dos veces para calcular dos SHA-256
// (uno de lo que se copiaba y otro de la copia ya escrita) y se comparaban: en .NET 3.5 eso son de 2 a 8 segundos de procesador, en un hilo de
// baja prioridad pero encima del arranque de un equipo de dos nucleos. Ahora, con la copia ya en disco (FlushFileBuffers hecho), se relee y se
// compara byte a byte con el origen capturado. Aqui se mira:
//   1) el codigo: BackupToCapturado ya no usa SHA-256 y llama a CompruebaCopiaDeRespaldo antes de publicar (Move); sigue con FlushFileBuffers;
//   2) CompruebaCopiaDeRespaldo: una copia igual pasa (tamanos que no son multiplo del bloque, vacio, justo un bloque); un byte distinto en
//      cualquier sitio (primero, ultimo, entre dos bloques, el 65.536 y el 65.537), una copia mas corta o mas larga, o cambiada de sitio, dan
//      IOException "La copia del respaldo no coincide"; un origen que se trunco da "El origen del respaldo se trunco durante la copia"; y se
//      sigue cediendo al trabajo visible entre bloque y bloque;
//   3) de punta a punta (solo si aqui existe FlushFileBuffers, que es de Windows; en Linux hace falta un puente de pruebas y si no se dice que
//      se salta): el respaldo sale igual que el origen, una copia estropeada A MEDIAS (entre confirmarla y compararla) no se publica, no deja
//      carpeta a medias y conserva el respaldo anterior, y un anexo en curso queda cortado al largo capturado.
class RespaldoBytes
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static byte[] Datos(int n, int semilla)
    {
        byte[] d = new byte[n]; Random r = new Random(semilla); r.NextBytes(d); return d;
    }
    // Llama a CompruebaCopiaDeRespaldo; devuelve null si pasa o el mensaje de la IOException.
    static string Comprueba(byte[] origen, byte[] copia, int cuantosDelOrigen, List<string> cedidos)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_respbytes_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        try
        {
            string o = Path.Combine(dir, "origen.bin"), c = Path.Combine(dir, "copia.bin");
            File.WriteAllBytes(o, origen); File.WriteAllBytes(c, copia);
            using (FileStream fo = new FileStream(o, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan))
            {
                Action<string> cede = cedidos == null ? (Action<string>)null : delegate(string r) { cedidos.Add(Path.GetFileName(r)); };
                try { TF.GetMethod("CompruebaCopiaDeRespaldo", T).Invoke(null, new object[] { fo, (long)cuantosDelOrigen, c, new byte[65536], new byte[65536], cede, o }); return null; }
                catch (TargetInvocationException e) { if (e.InnerException is IOException) return e.InnerException.Message; throw; }
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    const string NoCoincide = "La copia del respaldo no coincide", Trunco = "El origen del respaldo se trunco durante la copia";

    static bool Existe(string n) { return TF.GetMethod(n, T) != null; }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        MethodInfo bk = TF.GetMethod("BackupToCapturado", T);
        Check(bk != null && Existe("CompruebaCopiaDeRespaldo"), "existen BackupToCapturado y CompruebaCopiaDeRespaldo");
        bool sha = false;
        foreach (string s in Codigo.Llamadas(bk)) if (s.IndexOf("SHA256") >= 0 || s.IndexOf("HashAlgorithm") >= 0 || s.EndsWith(".TransformBlock") || s.EndsWith(".TransformFinalBlock")) sha = true;
        Check(!sha, "BackupToCapturado ya no calcula ningun hash (antes: dos SHA-256 por archivo)");
        // (28-10-2026) La confirmacion pasa por RespaldoEnDisco, que llama a FlushFileBuffers (y fuera de Windows, sin esa llamada, basta el Flush).
        MethodInfo enDisco = TF.GetMethod("RespaldoEnDisco", T);
        string flush = Codigo.Llama(bk, "FlushFileBuffers") ? "Form1.FlushFileBuffers" : "Form1.RespaldoEnDisco";
        bool confirma = Codigo.Llama(bk, "FlushFileBuffers") || (enDisco != null && Codigo.Llama(bk, "RespaldoEnDisco") && Codigo.Llama(enDisco, "FlushFileBuffers"));
        Check(Codigo.Llama(bk, "CompruebaCopiaDeRespaldo") && confirma && Codigo.Llama(bk, "Move"),
              "pero sigue confirmando en disco, comprueba la copia y solo despues la publica por renombrado");
        List<string> orden = Codigo.Llamadas(bk);
        Check(orden.IndexOf(flush) >= 0 && orden.IndexOf("Form1.CompruebaCopiaDeRespaldo") > orden.IndexOf(flush)
              && orden.IndexOf("Directory.Move") > orden.IndexOf("Form1.CompruebaCopiaDeRespaldo"), "y en ese orden: confirmar, comprobar, publicar");

        // ---------- 2) la comprobacion ----------
        int[] tamanos = { 0, 1, 100, 65535, 65536, 65537, 131072, 200003, 1500000 };
        int malos = 0;
        foreach (int n in tamanos)
        {
            byte[] d = Datos(n, n + 1);
            if (Comprueba(d, (byte[])d.Clone(), n, null) != null) { malos++; Console.WriteLine("    deberia pasar: " + n); }
        }
        Check(malos == 0, "una copia igual pasa en " + tamanos.Length + " tamanos (vacio, 1 byte, justo un bloque, un byte mas, muchos bloques, 1,5 MB)");
        malos = 0; int pruebas = 0;
        foreach (int n in new int[] { 1, 100, 65536, 65537, 200003, 1500000 })
        {
            byte[] d = Datos(n, 7);
            List<int> sitios = new List<int> { 0, n - 1, n / 2 };
            if (n > 65536) { sitios.Add(65535); sitios.Add(65536); }
            if (n > 131072) { sitios.Add(131071); sitios.Add(131072); }
            foreach (int s in sitios)
            {
                byte[] c = (byte[])d.Clone(); c[s] ^= 0x01;
                pruebas++;
                if (Comprueba(d, c, n, null) != NoCoincide) { malos++; Console.WriteLine("    no lo vio: " + n + " byte " + s); }
            }
        }
        Check(malos == 0, "un solo bit distinto se ve en cualquier sitio (primero, ultimo, mitad, borde de bloque): " + pruebas + " copias estropeadas, todas con \"" + NoCoincide + "\"");
        byte[] o1 = Datos(300000, 3);
        byte[] corta = new byte[299999]; Array.Copy(o1, corta, corta.Length);
        byte[] larga = new byte[300001]; Array.Copy(o1, larga, o1.Length); larga[300000] = 5;
        Check(Comprueba(o1, corta, o1.Length, null) == NoCoincide && Comprueba(o1, larga, o1.Length, null) == NoCoincide, "una copia un byte mas corta o un byte mas larga tambien se rechaza");
        byte[] movida = new byte[o1.Length]; Array.Copy(o1, 1, movida, 0, o1.Length - 1); movida[o1.Length - 1] = o1[0];
        Check(Comprueba(o1, movida, o1.Length, null) == NoCoincide, "y una copia con los mismos bytes en otro orden (corrida un byte)");
        byte[] ceros = new byte[o1.Length];
        Check(Comprueba(o1, ceros, o1.Length, null) == NoCoincide, "o llena de ceros (un archivo que no llego al disco)");
        // El origen mas largo que lo capturado (un anexo en curso): solo cuentan los primeros 'cuantos' bytes.
        byte[] anexo = new byte[o1.Length + 5000]; Array.Copy(o1, anexo, o1.Length); for (int i = o1.Length; i < anexo.Length; i++) anexo[i] = 9;
        Check(Comprueba(anexo, (byte[])o1.Clone(), o1.Length, null) == null, "un anexo en curso (el origen crecio despues de capturarlo) se compara solo hasta el largo capturado");
        // Origen truncado: se capturaron mas bytes de los que quedan (la copia, escrita al copiar, mide lo capturado).
        byte[] cortoO = new byte[250000]; Array.Copy(o1, cortoO, cortoO.Length);
        Check(Comprueba(cortoO, (byte[])o1.Clone(), o1.Length, null) == Trunco, "si el origen se trunco despues de copiar, se rechaza con el mensaje del origen truncado, como al copiar");
        Check(Comprueba(cortoO, cortoO, o1.Length, null) == NoCoincide, "y una copia que no mide lo capturado se rechaza sin leerla");
        List<string> cedidos = new List<string>();
        Comprueba(o1, (byte[])o1.Clone(), o1.Length, cedidos);
        int bloques = (o1.Length + 65535) / 65536;
        Check(cedidos.Count == 2 * bloques && cedidos.Contains("copia.bin") && cedidos.Contains("origen.bin"),
              "entre bloque y bloque cede al trabajo visible (" + cedidos.Count + " cesiones en " + bloques + " bloques, de la copia y del origen)");
        bool lanza = false;
        try { TF.GetMethod("CompruebaCopiaDeRespaldo", T).Invoke(null, new object[] { null, 0L, "no_existe_ni_hay_nada.bin", new byte[10], new byte[10], null, "x" }); }
        catch (TargetInvocationException) { lanza = true; } catch (Exception) { lanza = true; }
        Check(lanza, "sin origen o sin copia legible lanza (el respaldo no se publica)");

        // ---------- 3) de punta a punta ----------
        string dir = Path.Combine(Path.GetTempPath(), "rocola_respbytes2_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        try
        {
            string fuente = Path.Combine(dir, "tabla.bin"), otra = Path.Combine(dir, "otra.txt");
            byte[] contenido = Datos(2 * 1024 * 1024 + 173, 11);
            File.WriteAllBytes(fuente, contenido); File.WriteAllBytes(otra, Datos(70000, 12));
            bool hayFlush = true;
            try
            {
                using (FileStream f = new FileStream(Path.Combine(dir, "p.bin"), FileMode.Create))
                    TF.GetMethod("FlushFileBuffers", T).Invoke(null, new object[] { f.SafeFileHandle.DangerousGetHandle() });
            }
            catch (TargetInvocationException e) { hayFlush = !(e.InnerException is EntryPointNotFoundException || e.InnerException is DllNotFoundException); }
            if (!hayFlush) Console.WriteLine("  SE SALTA la parte de punta a punta: aqui no existe FlushFileBuffers (es de Windows; en Linux hace falta un puente de pruebas). La comprobacion por bytes si se probo arriba.");
            else
            {
                object captura = TF.GetMethod("CapturaRespaldo", T).Invoke(null, new object[] { new string[] { fuente, otra } });
                try
                {
                    string destino = Path.Combine(dir, "respaldo-bueno");
                    bk.Invoke(null, new object[] { destino, captura, null });
                    Check(Iguales(File.ReadAllBytes(Path.Combine(destino, "tabla.bin")), contenido) && Iguales(File.ReadAllBytes(Path.Combine(destino, "otra.txt")), File.ReadAllBytes(otra)),
                          "el respaldo de punta a punta sale con los mismos bytes que el origen");
                    // Una copia que ya no coincide con el origen, justo entre confirmarla y compararla: se cambia un byte del ORIGEN en la primera cesion de la
                    // comprobacion (la copia, ya cerrada, mide lo mismo que el origen; antes de eso, copiando, mide menos). Para la copia estropeada
                    // directamente, ver arriba (CompruebaCopiaDeRespaldo con 28 copias estropeadas).
                    string malo = Path.Combine(dir, "respaldo-estropeado");
                    bool estropeada = false;
                    Action<string> estropea = delegate(string r)
                    {
                        if (estropeada) return;
                        try
                        {
                            FileInfo fi = new FileInfo(r);
                            if (r == fuente || !fi.Exists || fi.Length != contenido.Length || !r.EndsWith("tabla.bin")) return;
                            using (FileStream w = new FileStream(fuente, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)) { w.Position = 1000001; int b = w.ReadByte(); w.Position = 1000001; w.WriteByte((byte)(b ^ 0x10)); }
                            estropeada = true;
                        }
                        catch (IOException) { }
                    };
                    string mensaje = null;
                    try { bk.Invoke(null, new object[] { malo, captura, estropea }); }
                    catch (TargetInvocationException e) { mensaje = e.InnerException is IOException ? e.InnerException.Message : e.InnerException.GetType().Name; }
                    Check(estropeada && mensaje == NoCoincide, "una copia que deja de coincidir con el origen despues de confirmarla (un byte cambiado en medio) da \"" + NoCoincide + "\" (obtenido: " + mensaje + ")");
                    bool restos = false;
                    foreach (string d in Directory.GetDirectories(dir)) if (Path.GetFileName(d).StartsWith(".rocola_backup_pending_")) restos = true;
                    Check(!Directory.Exists(malo) && !restos, "y no se publica ni deja carpeta a medias");
                    Check(Iguales(File.ReadAllBytes(Path.Combine(destino, "tabla.bin")), contenido), "el respaldo anterior queda intacto");
                }
                finally { ((IDisposable)captura).Dispose(); }
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
    static bool Iguales(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
}

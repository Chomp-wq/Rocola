using System;
using System.IO;
using System.Reflection;
using System.Text;

// Solo datos artificiales y carpetas temporales nuevas. Sin musica, ventana o FPS.
// --negativo comprueba la ausencia de las dos optimizaciones en el Release anterior.
class DiscoAcotado
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form;
    static int checks, failures;
    static void Check(bool good, string message)
    { checks++; if (!good) failures++; Console.WriteLine((good ? "OK " : "FALLO ") + message); }
    static object Call(string method, params object[] args)
    {
        try { return form.GetMethod(method, B).Invoke(null, args); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static bool Equal(byte[] one, byte[] two)
    {
        if (one.Length != two.Length) return false;
        for (int i = 0; i < one.Length; i++) if (one[i] != two[i]) return false;
        return true;
    }
    sealed class CountedRead : MemoryStream
    {
        internal int Reads, MaxRead, ReadBytes;
        internal long Received;
        internal int ChunkSize = int.MaxValue;
        internal bool Truncated;
        internal CountedRead(byte[] bytes) : base(bytes, false) { }
        public override int Read(byte[] bytes, int offset, int count)
        {
            Reads++; if (count > MaxRead) MaxRead = count;
            int actual = Truncated ? 0 : base.Read(bytes, offset, Math.Min(count, ChunkSize)); Received += actual; return actual;
        }
        public override int ReadByte() { ReadBytes++; return base.ReadByte(); }
    }
    static long Reference(byte[] bytes)
    { for (int i = bytes.Length - 1; i >= 0; i--) if (bytes[i] == 10) return i + 1; return 0; }
    static void ReverseScan()
    {
        MethodInfo search = form.GetMethod("UltimoLfAnexo", B);
        Check(search != null, "busqueda inversa por bloques existe");
        if (search == null) return;
        int[] lengths = { 0, 1, 65535, 65536, 65537, 3 * 65536 + 7, 2 * 1024 * 1024 + 13 };
        foreach (int length in lengths)
        {
            int[] positions = { -1, 0, length / 2, length - 1, length - 65536, length - 65537 };
            foreach (int position in positions)
            {
                byte[] bytes = new byte[length]; for (int i = 0; i < bytes.Length; i++) bytes[i] = 65;
                if (position >= 0 && position < bytes.Length) bytes[position] = 10;
                using (CountedRead stream = new CountedRead(bytes))
                {
                    long expected = Reference(bytes), actual = (long)search.Invoke(null, new object[] { stream });
                    Check(actual == expected, "ultimo LF exacto: largo=" + length + " posicion=" + position);
                    Check(stream.ReadBytes == 0 && stream.MaxRead <= 65536 && stream.Reads <= (length + 65535) / 65536,
                        "lecturas acotadas por bloque, no seek/ReadByte por caracter");
                }
            }
        }
        byte[] partial = Encoding.UTF8.GetBytes("fila sana\nultima fila incompleta");
        using (CountedRead stream = new CountedRead(partial))
        {
            stream.ChunkSize = 7;
            Check((long)search.Invoke(null, new object[] { stream }) == Reference(partial), "lecturas parciales se completan antes de buscar LF");
        }
        using (CountedRead stream = new CountedRead(partial))
        {
            stream.Truncated = true; bool failed = false;
            try { search.Invoke(null, new object[] { stream }); }
            catch (TargetInvocationException error) { failed = error.InnerException is IOException; }
            Check(failed, "truncamiento durante lectura aborta, no inventa limite valido");
        }
    }
    static void Repair(string directory)
    {
        string path = Path.Combine(directory, "anexo.txt");
        byte[] prefix = Encoding.UTF8.GetBytes("primera\r\nsegunda ñ\n");
        byte[] damaged = new byte[prefix.Length + 2 * 1024 * 1024 + 19];
        Array.Copy(prefix, damaged, prefix.Length);
        for (int i = prefix.Length; i < damaged.Length; i++) damaged[i] = 90;
        File.WriteAllBytes(path, damaged);
        object capture = Call("CapturaRespaldo", (object)new string[] { path });
        try
        {
            Call("AnexaSeguro", path, "tercera\r\n");
            byte[] suffix = Encoding.UTF8.GetBytes("tercera\r\n"), expected = new byte[prefix.Length + suffix.Length];
            Array.Copy(prefix, expected, prefix.Length); Array.Copy(suffix, 0, expected, prefix.Length, suffix.Length);
            Check(Equal(expected, File.ReadAllBytes(path)), "reparacion larga conserva todos los bytes hasta LF y agrega texto completo");
            Check(Equal(damaged, File.ReadAllBytes(path + ".bak")), "respaldo anterior conserva tambien la cola interrumpida sin truncarlo");
            string destination = Path.Combine(directory, "captura-anexo-roto");
            Call("BackupToCapturado", destination, capture, null);
            Check(Equal(damaged, File.ReadAllBytes(Path.Combine(destination, "anexo.txt"))), "handle de captura previa conserva su generacion completa");
        }
        finally { ((IDisposable)capture).Dispose(); }
        File.WriteAllBytes(path, new byte[130001]);
        Call("AnexaSeguro", path, "unica\n");
        Check(Equal(Encoding.UTF8.GetBytes("unica\n"), File.ReadAllBytes(path)), "sin LF previo la ultima fila incompleta se descarta entera");
        File.WriteAllBytes(path, new byte[0]); Call("AnexaSeguro", path, "vacía\n");
        Check(Equal(Encoding.UTF8.GetBytes("vacía\n"), File.ReadAllBytes(path)), "archivo vacio sigue anexandose sin reparacion");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("sana\n")); Call("AnexaSeguro", path, "añadida\n");
        Check(Equal(Encoding.UTF8.GetBytes("sana\nañadida\n"), File.ReadAllBytes(path)), "anexo sano conserva exactamente el formato anterior");
    }
    static void Backup(string directory)
    {
        MethodInfo method = form.GetMethod("BackupToCapturado", B);
        bool writeThrough = false;
        foreach (Codigo.Paso step in Codigo.Pasos(method))
            if (step.Op == 0x20 && (unchecked((uint)step.Numero) & unchecked((uint)FileOptions.WriteThrough)) != 0) writeThrough = true;
        Check(!writeThrough, "respaldo no obliga a confirmar individualmente cada bloque");
        Check(Codigo.Llama(method, "FlushFileBuffers") && Codigo.Llama(method, "CompruebaCopiaDeRespaldo") && Codigo.Llama(method, "Move"),
            "conserva confirmacion final, copia releida y comparada con el origen (antes, hash) y publicacion por renombrado");
        string path = Path.Combine(directory, "fuente.bin"); byte[] content = new byte[2 * 1024 * 1024 + 173];
        for (int i = 0; i < content.Length; i++) content[i] = (byte)(i * 31);
        File.WriteAllBytes(path, content); object capture = Call("CapturaRespaldo", (object)new string[] { path });
        try
        {
            string destination = Path.Combine(directory, "respaldo-verificado");
            int yields = 0; Action<string> yield = delegate(string unused) { yields++; };
            Call("BackupToCapturado", destination, capture, yield);
            Check(yields > 0 && Equal(content, File.ReadAllBytes(Path.Combine(destination, "fuente.bin"))),
                "respaldo multibloque completo, mismos bytes y cesiones al trabajo visible");
            bool failed = false;
            try { Call("BackupToCapturado", destination, capture, null); } catch (IOException) { failed = true; }
            Check(failed && Equal(content, File.ReadAllBytes(Path.Combine(destination, "fuente.bin"))), "no sobrescribe ni elimina respaldo ya publicado");
            string incomplete = Path.Combine(directory, "respaldo-interrumpido");
            Action<string> fail = delegate(string unused) { throw new IOException("corte simulado"); };
            failed = false;
            try { Call("BackupToCapturado", incomplete, capture, fail); } catch (IOException) { failed = true; }
            Check(failed && !Directory.Exists(incomplete) && Equal(content, File.ReadAllBytes(Path.Combine(destination, "fuente.bin"))),
                "interrupcion antes de confirmar no publica y conserva respaldo previo");
        }
        finally { ((IDisposable)capture).Dispose(); }
    }
    static int Main(string[] args)
    {
        if (args.Length < 1) return 2;
        string directory = Path.Combine(Path.GetTempPath(), "rocola-disco-acotado-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        form = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("Rocola.Form1", true);
        if (args.Length > 1 && args[1] == "--negativo")
        {
            Check(form.GetMethod("UltimoLfAnexo", B) != null, "antes existia busqueda por bloques");
            bool writeThrough = false;
            foreach (Codigo.Paso step in Codigo.Pasos(form.GetMethod("BackupToCapturado", B)))
                if (step.Op == 0x20 && (unchecked((uint)step.Numero) & unchecked((uint)FileOptions.WriteThrough)) != 0) writeThrough = true;
            Check(!writeThrough, "antes el respaldo evitaba WriteThrough por bloque");
        }
        else { ReverseScan(); Repair(directory); Backup(directory); }
        Console.WriteLine("TOTAL " + checks + " FALLOS " + failures + "; datos artificiales: " + directory);
        return failures == 0 ? 0 : 1;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

// Ejercita exclusivamente archivos NUEVOS en un directorio temporal privado.
// No abre una ventana, ni musica, modelos, corpus o configuracion de produccion.
// --limite sirve como regresion negativa contra la compilacion anterior.
class CorpusAcotado
{
    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic;
    static Type form, originType;
    static MethodInfo append;
    static string temporary, data, index;
    static int checks, failures;

    static void Check(bool value, string text)
    { checks++; if (!value) failures++; Console.WriteLine((value ? "OK " : "FALLO ") + text); }
    static void Set(object target, string name, object value)
    { form.GetField(name, IF).SetValue(target, value); }
    static object Get(object target, string name)
    { return form.GetField(name, IF).GetValue(target); }
    static object Fresh(long limit)
    {
        string directory = Path.Combine(temporary, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        form.GetField("carpetaDatos", SF).SetValue(null, directory);
        data = Path.Combine(directory, "rocola_ia_datos.bin"); index = Path.Combine(directory, "rocola_ia_datos.idx");
        object result = FormatterServices.GetUninitializedObject(form);
        Set(result, "iaFileLock", new object()); Set(result, "iaMaxBytes", limit);
        return result;
    }
    static object Origin(int seed)
    {
        object result = Activator.CreateInstance(originType, true);
        originType.GetField("Cancion").SetValue(result, (ulong)(seed + 1));
        originType.GetField("Carpeta").SetValue(result, 3UL);
        originType.GetField("Artista").SetValue(result, 5UL);
        originType.GetField("Rate").SetValue(result, 44100);
        originType.GetField("Frame").SetValue(result, 2048);
        originType.GetField("Canal").SetValue(result, seed & 1);
        originType.GetField("Genero").SetValue(result, "rock");
        return result;
    }
    static List<float> Pairs(int rows, int seed)
    {
        List<float> result = new List<float>(rows * 40);
        for (int i = 0; i < rows * 40; i++) result.Add((i % 13 == 0) ? -0.0f : (float)Math.Sin(i * .037 + seed));
        return result;
    }
    static bool Save(object target, List<float> values, int seed)
    { return (bool)append.Invoke(target, new object[] { values, Origin(seed) }); }
    static long Length(string path) { return File.Exists(path) ? new FileInfo(path).Length : 0; }
    static bool Equal(byte[] one, byte[] two)
    {
        if (one.Length != two.Length) return false;
        for (int i = 0; i < one.Length; i++) if (one[i] != two[i]) return false;
        return true;
    }
    static uint Crc(byte[] bytes, int offset, int count)
    {
        uint result = 0xFFFFFFFFu;
        for (int i = 0; i < count; i++)
        {
            result ^= bytes[offset + i];
            for (int bit = 0; bit < 8; bit++) result = (result & 1) != 0 ? (result >> 1) ^ 0xEDB88320u : result >> 1;
        }
        return result ^ 0xFFFFFFFFu;
    }
    sealed class CountingStream : Stream
    {
        internal long Total;
        internal int Biggest, Writes;
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return Total; } }
        public override long Position { get { return Total; } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override int Read(byte[] bytes, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] bytes, int offset, int count)
        { Total += count; Writes++; if (count > Biggest) Biggest = count; }
    }
    static void Limits()
    {
        object target = Fresh(16 + 2 * 160);
        Check(Save(target, Pairs(2, 1), 1), "un lote que cabe exactamente llega completo");
        byte[] before = File.ReadAllBytes(data), indexBefore = File.ReadAllBytes(index);
        Check(!Save(target, Pairs(1, 2), 2), "lote que rebasa el limite se rechaza dentro del escritor");
        Check(Equal(before, File.ReadAllBytes(data)) && Equal(indexBefore, File.ReadAllBytes(index)), "rechazar no modifica datos ni origen previos");
        Check((int)Get(target, "iaPairsTotal") == 2, "rechazar no aumenta el contador");

        target = Fresh(16 + 3 * 160);
        Check(!Save(target, Pairs(4, 3), 3), "no se trunca un lote para ocupar los ultimos bytes");
        Check(Length(data) == 0 && !File.Exists(index), "lote imposible no publica cabecera ni origen");

        target = Fresh(0);
        Check(Save(target, Pairs(10, 4), 4), "se crea corpus previo sin limite");
        before = File.ReadAllBytes(data); indexBefore = File.ReadAllBytes(index);
        Set(target, "iaMaxBytes", 16L + 11 * 160); Set(target, "iaPairsTotal", 0);
        Check(!Save(target, Pairs(2, 5), 5), "manda el largo real, no un contador desactualizado");
        Check(Equal(before, File.ReadAllBytes(data)) && Equal(indexBefore, File.ReadAllBytes(index)), "el rechazo basado en largo real es inmutable");

        target = Fresh(16 + 6 * 160);
        object shared = target;
        ManualResetEvent ready = new ManualResetEvent(false);
        Thread[] threads = new Thread[8]; int accepted = 0, unexpected = 0;
        for (int k = 0; k < threads.Length; k++)
        {
            int seed = k;
            threads[k] = new Thread(delegate()
            {
                ready.WaitOne();
                try { if (Save(shared, Pairs(2, seed), seed)) Interlocked.Increment(ref accepted); }
                catch { Interlocked.Increment(ref unexpected); }
            });
            threads[k].Start();
        }
        ready.Set(); for (int i = 0; i < threads.Length; i++) threads[i].Join(); ready.Close();
        Check(unexpected == 0 && accepted == 3, "ocho escritores simultaneos admiten solo tres lotes enteros");
        Check(Length(data) == 16 + 6 * 160 && (int)Get(target, "iaPairsTotal") == 6, "la concurrencia no supera el limite ni pierde el contador");
        Check(Length(index) == 16 + 3 * 64, "la concurrencia publica exactamente los tres origenes admitidos");
    }
    static void Blocks()
    {
        object target = Fresh(0);
        int[] rows = { 1, 255, 256, 257, 10000 };
        long first = 0;
        object floatScratch = null, byteScratch = null;
        for (int i = 0; i < rows.Length; i++)
        {
            List<float> values = Pairs(rows[i], i + 10);
            float[] expectedFloats = values.ToArray(); byte[] expected = new byte[expectedFloats.Length * 4];
            Buffer.BlockCopy(expectedFloats, 0, expected, 0, expected.Length);
            Check(Save(target, values, i + 10), "lote de " + rows[i] + " ejemplos llega entero");
            byte[] actual = new byte[expected.Length];
            using (FileStream reader = File.OpenRead(data))
            {
                reader.Position = 16 + first * 160; int got = 0, n;
                while ((n = reader.Read(actual, got, actual.Length - got)) > 0) got += n;
                Check(got == actual.Length && Equal(expected, actual), "mismos float32 bit a bit, incluido cero negativo");
            }
            byte[] entries = File.ReadAllBytes(index); int offset = 16 + i * 64;
            Check(BitConverter.ToInt64(entries, offset) == first && BitConverter.ToInt32(entries, offset + 8) == rows[i], "origen conserva rango completo sin fragmentarlo");
            Check(BitConverter.ToUInt32(entries, offset + 28) == Crc(expected, 0, expected.Length), "CRC incremental coincide con referencia independiente");
            Check(BitConverter.ToUInt32(entries, offset + 60) == Crc(entries, offset, 60), "CRC del indice queda valido");
            FieldInfo floatsField = form.GetField("iaParesScratch", IF), bytesField = form.GetField("iaBytesScratch", IF);
            Check(floatsField != null && bytesField != null, "el escritor tiene scratch acotado en vez de copiar el lote entero");
            if (floatsField != null && bytesField != null)
            {
                float[] floats = (float[])floatsField.GetValue(target); byte[] bytes = (byte[])bytesField.GetValue(target);
                Check(floats.Length * 4 <= 65536 && bytes.Length <= 65536, "ningun scratch alcanza el LOH ni crece con el lote");
                Check(i == 0 || (ReferenceEquals(floatScratch, floats) && ReferenceEquals(byteScratch, bytes)), "se reutilizan los mismos dos arrays entre lotes");
                floatScratch = floats; byteScratch = bytes;
            }
            first += rows[i];
        }
        Check((int)Get(target, "iaPairsTotal") == first && Length(data) == 16 + first * 160, "contador y archivo completo concuerdan");
        List<float> large = Pairs(10000, 40);
        CountingStream counted = new CountingStream();
        MethodInfo blockWriter = form.GetMethod("IaEscribePares", IF);
        Check(blockWriter != null, "hay escritura por bloques verificable sin disco ni ventana");
        if (blockWriter != null)
        {
            blockWriter.Invoke(target, new object[] { counted, large });
            Check(counted.Total == 10000L * 160 && counted.Biggest <= 65536 && counted.Writes > 1,
                "un lote de 1,6 MB se escribe completo en bloques menores de 64 KiB");
        }

        target = Fresh(0);
        Check(Save(target, Pairs(1, 24), 24), "prepara corpus para probar error temporal de apertura");
        byte[] preservedData = File.ReadAllBytes(data), preservedIndex = File.ReadAllBytes(index);
        using (FileStream locked = new FileStream(data, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(!Save(target, Pairs(2, 25), 25), "un archivo temporalmente bloqueado devuelve fallo sin consumir el lote");
        Check(Equal(preservedData, File.ReadAllBytes(data)) && Equal(preservedIndex, File.ReadAllBytes(index)),
            "fallo de apertura no publica datos ni origen nuevos");
        Check(Save(target, Pairs(2, 25), 25) && Length(data) == 16 + 3 * 160, "el mismo lote se puede reintentar completo");
        Set(target, "iaPairsTotal", int.MaxValue);
        Check(Save(target, Pairs(1, 26), 26) && (int)Get(target, "iaPairsTotal") == 4,
            "el contador se calcula del archivo y no desborda por un valor viejo");

        target = Fresh(0);
        Check(Save(target, Pairs(2, 20), 20), "se prepara un corpus para recuperacion");
        using (FileStream writer = new FileStream(data, FileMode.Append, FileAccess.Write)) writer.Write(new byte[17], 0, 17);
        Set(target, "iaMaxBytes", 16L + 3 * 160);
        Check(Save(target, Pairs(1, 21), 21) && Length(data) == 16 + 3 * 160, "recupera cola interrumpida y respeta el limite con pares completos");
        byte[] recovered = File.ReadAllBytes(index);
        Check(BitConverter.ToInt64(recovered, 80) == 2 && BitConverter.ToInt32(recovered, 88) == 1, "recuperacion no desplaza el origen del nuevo lote");

        target = Fresh(0);
        byte[] alien = new byte[177]; alien[0] = 123; File.WriteAllBytes(data, alien);
        Check(!Save(target, Pairs(1, 22), 22) && Equal(alien, File.ReadAllBytes(data)) && !File.Exists(index), "archivo ajeno o corrupto no se modifica");
        Check(!Save(target, new List<float>(new float[41]), 23), "se rechaza un numero incompleto de valores");
    }
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.WriteLine("Uso: corpusacotado.exe Rocola.exe [--limite]"); return 2; }
        temporary = Path.Combine(Path.GetTempPath(), "rocola-corpus-acotado-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        string previousEnvironment = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
        string previousDirectory = null;
        try
        {
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", temporary);
            form = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("Rocola.Form1", true);
            previousDirectory = (string)form.GetField("carpetaDatos", SF).GetValue(null);
            originType = form.GetNestedType("IaOrigen", BindingFlags.NonPublic);
            append = form.GetMethod("IaAppendPairsOrigen", IF);
            Limits(); if (args.Length < 2 || args[1] != "--limite") Blocks();
            Console.WriteLine("TOTAL " + checks + " FALLOS " + failures + "; solo archivos temporales: " + temporary);
            return failures == 0 ? 0 : 1;
        }
        finally
        {
            if (form != null) form.GetField("carpetaDatos", SF).SetValue(null, previousDirectory);
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", previousEnvironment);
            // Conservar la evidencia aislada; el operador puede retirarla despues de revisarla.
        }
    }
}

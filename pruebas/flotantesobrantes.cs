using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// Solo PCM sintetico: no codec del sistema, GPU, salida de sonido ni datos del usuario.
// Un argumento: contrato independiente FIFO/memoria/ciclo de vida, sin afirmar equivalencia con otra version.
// Dos argumentos: ademas compara estado y bytes con una copia aislada de la Release anterior.
class FlotanteSobrantes
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks++; }
    static object Get(object x, string name) { return x.GetType().GetField(name, B).GetValue(x); }
    static void Set(object x, string name, object value) { x.GetType().GetField(name, B).SetValue(x, value); }
    static object Call(object x, string method, params object[] args)
    { try { return x.GetType().GetMethod(method, B).Invoke(x, args); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static float[] Values(int count, int seed)
    {
        // Incluye ceros con signo, infinitos y NaN con diferentes cargas: copiar, nunca operar con PCM.
        byte[] data = new byte[count * 4];
        Random random = new Random(seed);
        random.NextBytes(data);
        int[] special = { 0, unchecked((int)0x80000000), 0x7F800000, unchecked((int)0xFF800000), 0x7FC00001, unchecked((int)0xFFC12345) };
        for (int i = 0; i < Math.Min(count, special.Length); i++) Buffer.BlockCopy(new int[] { special[i] }, 0, data, i * 4, 4);
        float[] samples = new float[count]; Buffer.BlockCopy(data, 0, samples, 0, data.Length); return samples;
    }
    static void Bytes(byte[] a, byte[] b, string text)
    {
        Check(a.Length == b.Length, text + " longitud");
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) throw new Exception(text + " byte " + i);
        checks++;
    }
    static byte[] Bits(float[] values, int count)
    { byte[] b = new byte[count * 4]; Buffer.BlockCopy(values, 0, b, 0, b.Length); return b; }
    static object Bare(Type type, int channels)
    {
        object x = FormatterServices.GetUninitializedObject(type);
        Set(x, "canales", channels); Set(x, "sobra", new float[0]); Set(x, "tmp", new float[0]); return x;
    }
    static void PendingSame(object actual, object baseline)
    {
        if (baseline == null) return;
        int n = (int)Get(actual, "nSobra");
        Check(n == (int)Get(baseline, "nSobra"), "Mismo conteo pendiente");
        float[] a = (float[])Get(actual, "sobra"), b = (float[])Get(baseline, "sobra");
        Check(a.Length == b.Length, "No aumenta capacidad/retencion del sobrante");
        Bytes(Bits(a, n), Bits(b, n), "Sobrante logico identico");
    }
    static void Contract(object actual, int channels, List<byte> fifo, byte[] input, byte[] output, int offset, int count,
                         byte guard, ref int retained)
    {
        fifo.AddRange(input);
        int capacity = (output.Length - offset) / 4; capacity -= capacity % channels;
        Check(count == Math.Min(fifo.Count / 4, capacity) * 4, "FIFO independiente conserva alineacion y cuenta");
        for (int i = 0; i < output.Length; i++)
        {
            byte expected = i >= offset && i < offset + count ? fifo[i - offset] : guard;
            if (output[i] != expected) throw new Exception("FIFO/guardas independientes byte " + i);
        }
        checks++;
        fifo.RemoveRange(0, count);
        int pending = (int)Get(actual, "nSobra");
        Check(pending == fifo.Count / 4, "Conteo del sobrante segun FIFO independiente");
        retained = Math.Max(retained, pending);
        Check(((float[])Get(actual, "sobra")).Length == retained, "Reserva solo hasta el maximo sobrante real");
        Bytes(Bits((float[])Get(actual, "sobra"), pending), fifo.ToArray(), "FIFO pendiente independiente");
    }
    static void Sequence(Type current, Type old)
    {
        foreach (int channels in new int[] { 1, 2, 6 })
        {
            object a = Bare(current, channels), b = old != null ? Bare(old, channels) : null;
            Random random = new Random(channels * 911);
            List<byte> fifo = new List<byte>();
            int retained = 0;
            for (int step = 0; step < 450; step++)
            {
                // Todos los tamanos, no necesariamente multiplos del canal: como la entrega historica.
                int n = step < 20 ? step % 7 : random.Next(0, 80);
                float[] input = Values(n, channels * 10000 + step);
                byte[] inputBytes = Bits(input, n);
                Set(a, "tmp", input); if (b != null) Set(b, "tmp", (float[])input.Clone());
                int offset = step % 9, room = step % 13 == 0 ? 0 : random.Next(0, 600);
                byte[] outputA = new byte[offset + room], outputB = new byte[outputA.Length];
                for (int i = 0; i < outputA.Length; i++) outputA[i] = outputB[i] = 0xAD;
                int na = (int)Call(a, "Entrega", n, outputA, offset);
                Contract(a, channels, fifo, inputBytes, outputA, offset, na, 0xAD, ref retained);
                if (b != null)
                {
                    Check(na == (int)Call(b, "Entrega", n, outputB, offset), "Misma cuenta entregada");
                    Bytes(outputA, outputB, "Destino y guardas identicos");
                }
                PendingSame(a, b);
                if (step == 223) // Una entrega vacia conserva la cola; Reset real se prueba por el camino publico abajo.
                {
                    int empty = (int)Call(a, "Entrega", 0, new byte[0], 0);
                    Contract(a, channels, fifo, new byte[0], new byte[0], 0, empty, 0, ref retained);
                    if (b != null) Call(b, "Entrega", 0, new byte[0], 0); PendingSame(a, b);
                }
            }
            Set(a, "tmp", new float[0]); if (b != null) Set(b, "tmp", new float[0]);
            int rest = (int)Get(a, "nSobra");
            byte[] drainA = new byte[(rest + channels) * 4 + 3], drainB = new byte[drainA.Length];
            int drained = (int)Call(a, "Entrega", 0, drainA, 1);
            Contract(a, channels, fifo, new byte[0], drainA, 1, drained, 0, ref retained);
            Check(fifo.Count == 0, "Vaciado completo con entrada cero");
            if (b != null)
            {
                Check(drained == (int)Call(b, "Entrega", 0, drainB, 1), "Vaciado con entrada cero");
                Bytes(drainA, drainB, "Vaciado final exacto");
            }
            PendingSame(a, b);
        }
    }
    static void Reuse(Type type, Type old)
    {
        object a = Bare(type, 2), b = old != null ? Bare(old, 2) : null;
        List<byte> fifo = new List<byte>(); int retained = 0;
        float[] source = Values(48000, 87); // El temporal concatenado historico entraba en LOH.
        Set(a, "tmp", source); if (b != null) Set(b, "tmp", (float[])source.Clone());
        int first = (int)Call(a, "Entrega", source.Length, new byte[0], 0);
        Contract(a, 2, fifo, Bits(source, source.Length), new byte[0], 0, first, 0, ref retained);
        if (b != null) Call(b, "Entrega", source.Length, new byte[0], 0);
        object storage = Get(a, "sobra");
        for (int i = 0; i < 80; i++)
        {
            float[] small = Values(8, i + 700);
            Set(a, "tmp", small); if (b != null) Set(b, "tmp", (float[])small.Clone());
            byte[] outA = new byte[40], outB = new byte[40];
            int count = (int)Call(a, "Entrega", 8, outA, 8);
            Contract(a, 2, fifo, Bits(small, small.Length), outA, 8, count, 0, ref retained);
            if (b != null)
            {
                Check(count == (int)Call(b, "Entrega", 8, outB, 8), "Cuenta estable con sobrante grande");
                Bytes(outA, outB, "Entrega estable sin concatenar");
            }
            PendingSame(a, b);
            Check(Object.ReferenceEquals(storage, Get(a, "sobra")), "Sobrante estable reusa la misma reserva");
        }
        Check(Arrays(type) == 1, "Solo queda la reserva condicional de sobrante, no el array concatenado por entrega");
        if (old != null) Check(Arrays(old) == 2, "La Release de referencia aun conserva la concatenacion historica");
    }
    static int Arrays(Type type)
    {
        int count = 0;
        foreach (Codigo.Paso p in Codigo.Pasos(type.GetMethod("Entrega", B))) if (p.Op == 0x8D) count++;
        return count;
    }
    sealed class Decoder : IMp3FrameDecompressor
    {
        private readonly WaveFormat format;
        private readonly float[] samples = new float[96];
        internal bool ThrowNext;
        internal int Reads, Resets, Disposes;
        internal byte[] Last;
        private int sequence;
        internal Decoder(WaveFormat f) { format = WaveFormat.CreateIeeeFloatWaveFormat(f.SampleRate, f.Channels); }
        public WaveFormat OutputFormat { get { return format; } }
        public int DecompressFrame(Mp3Frame frame, byte[] output, int offset)
        {
            if (Disposes > 0) throw new ObjectDisposedException("fixture decoder");
            if (ThrowNext) { ThrowNext = false; throw new InvalidDataException("fallo sintetico"); }
            Reads++; int n = (Reads % 5 + 1) * format.Channels;
            for (int i = 0; i < n; i++) samples[i] = (sequence++ - 1000) / 2048f;
            Last = Bits(samples, n);
            Buffer.BlockCopy(samples, 0, output, offset, n * 4); return n * 4;
        }
        public void Reset() { Resets++; Reads = sequence = 0; }
        public void Dispose() { Disposes++; }
    }
    sealed class Pair
    {
        internal object Wrapper;
        internal Decoder Acm, Nl;
        internal Pair(Type type)
        {
            WaveFormat f = new WaveFormat(44100, 16, 2); Acm = new Decoder(f); Nl = new Decoder(f);
            Func<WaveFormat, IMp3FrameDecompressor> factory = delegate(WaveFormat ignored) { return Nl; };
            Wrapper = Activator.CreateInstance(type, B, null, new object[] { f, Acm, factory }, null);
            // Evitar cualquier bitacora del programa al provocar el fallo controlado de NLayer.
            type.Assembly.GetType("Rocola.DecodificacionFlotante", true).GetField("Apunte", B).SetValue(null, (Action<string>)delegate(string ignored) { });
        }
    }
    static void PublicPath(Type type, Type old)
    {
        Pair a = new Pair(type), b = old != null ? new Pair(old) : null;
        List<byte> fifo = new List<byte>(); int retained = 0;
        Mp3Frame frame = (Mp3Frame)FormatterServices.GetUninitializedObject(typeof(Mp3Frame));
        for (int step = 0; step < 180; step++)
        {
            if (step == 28) { a.Nl.ThrowNext = true; if (b != null) b.Nl.ThrowNext = true; }
            if (step == 61 || step == 117)
            {
                Call(a.Wrapper, "Reset"); if (b != null) Call(b.Wrapper, "Reset");
                PendingSame(a.Wrapper, b != null ? b.Wrapper : null);
                Check((int)Get(a.Wrapper, "nSobra") == 0 && (int)Get(a.Wrapper, "nHistoria") == 0, "Reset descarta sobrantes e historia");
                fifo.Clear();
            }
            int offset = step % 7, room = step % 8 == 0 ? 0 : step % 13 * 4 + 3;
            byte[] outA = new byte[offset + room], outB = new byte[outA.Length];
            for (int i = 0; i < outA.Length; i++) outA[i] = outB[i] = 0xCD;
            int count = (int)Call(a.Wrapper, "DecompressFrame", frame, outA, offset);
            // Solo el cuadro final pertenece a la salida. Calienta decodifica historia y la descarta.
            byte[] decoded = step < 28 ? a.Nl.Last : a.Acm.Last;
            Contract(a.Wrapper, 2, fifo, decoded, outA, offset, count, 0xCD, ref retained);
            Check((int)Get(a.Wrapper, "CuadrosNl") == Math.Min(step + 1, 28), "Cuenta independiente de cuadros NLayer");
            Check((int)Get(a.Wrapper, "CuadrosAcm") == Math.Max(0, step - 27), "Cuenta independiente de cuadros ACM");
            Check((bool)Get(a.Wrapper, "nlVetado") == (step >= 28), "Fallo controlado veta NLayer sin rehabilitarlo al saltar");
            Check((int)Get(a.Wrapper, "Cambios") == (step >= 28 ? 1 : 0), "Solo un cambio de motor por fallo");
            int sinceReset = step >= 117 ? step - 116 : (step >= 61 ? step - 60 : step + 1);
            Check((int)Get(a.Wrapper, "nHistoria") == Math.Min(3, sinceReset), "Historia acotada y reiniciada");
            Check(a.Acm.Resets == (step >= 28 ? 1 : 0) + (step >= 61 ? 1 : 0) + (step >= 117 ? 1 : 0) && a.Nl.Resets == 0,
                "Reset y calentamiento no se repiten innecesariamente");
            if (b != null)
            {
                Check(count == (int)Call(b.Wrapper, "DecompressFrame", frame, outB, offset), "Cuenta por descompresor publico");
                Bytes(outA, outB, "Publico PCM y guardas exactos"); PendingSame(a.Wrapper, b.Wrapper);
                foreach (string field in new string[] { "CuadrosNl", "CuadrosAcm", "Cambios", "nHistoria", "hHistoria", "nlVetado" })
                    Check(Get(a.Wrapper, field).Equals(Get(b.Wrapper, field)), "Cambio de motor conserva " + field);
                Check(a.Acm.Reads == b.Acm.Reads && a.Nl.Reads == b.Nl.Reads && a.Acm.Resets == b.Acm.Resets && a.Nl.Resets == b.Nl.Resets,
                    "Entrega/reset/fallo conservan las llamadas a ambos motores");
            }
        }
        Check(a.Nl.Disposes == 1 && a.Acm.Reads > 0 && (int)Get(a.Wrapper, "Cambios") > 0, "Fallo controlado cambia al motor de respaldo");
        byte[] pendingBeforeClose = Bits((float[])Get(a.Wrapper, "sobra"), (int)Get(a.Wrapper, "nSobra"));
        Call(a.Wrapper, "Dispose"); Call(a.Wrapper, "Dispose");
        Check(a.Acm.Disposes == 1 && a.Nl.Disposes == 1 && (bool)Get(a.Wrapper, "cerrado"), "Cierre repetido libera cada motor exactamente una vez");
        Bytes(Bits((float[])Get(a.Wrapper, "sobra"), (int)Get(a.Wrapper, "nSobra")), pendingBeforeClose, "Cierre no cambia muestras retenidas");
        Check(((float[])Get(a.Wrapper, "sobra")).Length == retained, "Cierre no amplia la reserva");
        if (b != null)
        {
            Call(b.Wrapper, "Dispose"); Call(b.Wrapper, "Dispose");
            Check(a.Acm.Disposes == b.Acm.Disposes && a.Nl.Disposes == b.Nl.Disposes, "Cierre igual a la referencia anterior");
            PendingSame(a.Wrapper, b.Wrapper);
        }
    }
    static int Main(string[] args)
    {
        try
        {
            Check(args.Length == 1 || args.Length == 2, "Uso: actual.exe [Release-anterior.exe]");
            Assembly current = Assembly.LoadFrom(args[0]);
            Type type = current.GetType("Rocola.DescompresorFlotante", true), old = null;
            if (args.Length == 2)
            {
                // La misma copia no constituye una comparativa A/B, aunque se cargue en otro Assembly.
                byte[] now = File.ReadAllBytes(args[0]), before = File.ReadAllBytes(args[1]);
                bool different = now.Length != before.Length;
                for (int i = 0; !different && i < now.Length; i++) if (now[i] != before[i]) different = true;
                Check(different, "La referencia A/B no puede ser la misma compilacion nueva");
                Assembly previous = Assembly.Load(before);
                old = previous.GetType("Rocola.DescompresorFlotante", true);
                Check(type.Assembly != old.Assembly, "Referencia anterior cargada de forma independiente");
            }
            Sequence(type, old); Reuse(type, old); PublicPath(type, old);
            Console.WriteLine("PASS flotantesobrantes: " + checks + " comprobaciones de PCM/orden/retencion/reset/cambio/cierre, solo sinteticas; "
                + (old == null ? "contrato independiente, sin referencia anterior ni afirmacion de equivalencia" : "A/B exacto contra Release anterior y contrato independiente")); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

// Reutilizacion del medidor COMPLETO: filtros K, puerta, histograma de picos y mapa de silencios.
// PCM sintetico y datos aislados. No reproduce musica ni mide FPS; admite un ejecutable anterior.
using System;
using System.IO;
using System.Reflection;
using System.Threading;
class MemoriaIntegrada
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static object Field(object x, string n) { return x.GetType().GetField(n, B).GetValue(x); }
    static object Call(Type t, object x, string n, params object[] a) { return t.GetMethod(n, B).Invoke(x, a); }
    static object Prop(object x, string n) { return x.GetType().GetProperty(n, B).GetValue(x, null); }
    static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static void Value(BinaryWriter w, object x)
    {
        if (x == null) { w.Write("null"); return; }
        Type t = x.GetType(); w.Write(t.FullName);
        if (t == typeof(int)) w.Write((int)x);
        else if (t == typeof(long)) w.Write((long)x);
        else if (t == typeof(float)) w.Write((float)x);
        else if (t == typeof(double)) w.Write((double)x);
        else if (t == typeof(bool)) w.Write((bool)x);
        else if (x is Array)
        {
            Array array = (Array)x; w.Write(array.Length);
            foreach (object item in array) Value(w, item);
        }
        else
        {
            FieldInfo[] fields = t.GetFields(B);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return String.CompareOrdinal(a.Name, b.Name); });
            foreach (FieldInfo f in fields) if (!f.IsStatic) { w.Write(f.Name); Value(w, f.GetValue(x)); }
        }
    }
    static byte[] State(object x)
    {
        using (MemoryStream s = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(s))
        { Value(w, x); w.Flush(); return s.ToArray(); }
    }
    static byte[] Result(object m)
    {
        using (MemoryStream s = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(s))
        {
            foreach (string p in new string[] { "Canales", "PesoTotal", "Cuadros", "Bloques", "CuadroPartido" }) Value(w, Prop(m, p));
            object[] a = { 0.0, 0 }; Value(w, Call(m.GetType(), m, "Integrada", a)); Value(w, a[0]); Value(w, a[1]);
            object peaks = Field(m, "Picos"); Value(w, Prop(peaks, "PicoDb")); Value(w, Prop(peaks, "P99Db"));
            Value(w, Call(peaks.GetType(), peaks, "Mapa", Prop(m, "Cuadros")));
            w.Flush(); return s.ToArray();
        }
    }
    static object New(Type meter, int rate, double[] weights)
    { return Activator.CreateInstance(meter, B, null, new object[] { rate, weights }, null); }
    static bool Reset(object m, int rate, double[] weights) { return (bool)Call(m.GetType(), m, "Reinicia", rate, weights); }
    static float Sample(long index, int rate, int channels, bool silent)
    {
        long frame = index / channels; double time = frame / (double)rate;
        if (silent || time < .05 || (time > 1.1 && time < 1.8) || (time > 3.0 && time < 4.4)) return 0f;
        double amplitude = (frame % 17011 < 5) ? 1.12 : .17;
        return (float)(amplitude * Math.Sin(time * Math.PI * 2 * (311 + index % channels * 197)));
    }
    static void Feed(object m, int rate, int channels, double seconds, int tail, bool silent)
    {
        long total = (long)(rate * seconds) * channels + tail, at = 0;
        float[] buffer = new float[4096]; int[] pieces = { 1, 7, 4096, 31, 4093, 2 }; int part = 0;
        MethodInfo process = m.GetType().GetMethod("Procesa", B);
        while (at < total)
        {
            int n = (int)Math.Min(pieces[part++ % pieces.Length], total - at);
            for (int i = 0; i < n; i++) buffer[i] = Sample(at + i, rate, channels, silent);
            process.Invoke(m, new object[] { buffer, 0, n }); at += n;
        }
    }
    static void Zero(object x, params string[] arrays)
    {
        foreach (string n in arrays)
        {
            bool zero = true;
            foreach (object v in (Array)Field(x, n)) if (Convert.ToDouble(v) != 0.0) { zero = false; break; }
            Check(zero, "Reset limpia toda la capacidad: " + n);
        }
    }
    static void ResetCases(Type meter)
    {
        int[] rates = { 8000, 11025, 44100, 48000, 96000 };
        double[][] weights = { new double[] { 1 }, new double[] { 1, 1 }, new double[] { 1, 1, 1, 0, 1.41, 1.41 } };
        foreach (int rate in rates) foreach (double[] p in weights)
        {
            object m = New(meter, rate, p); Feed(m, rate, p.Length, 5.137, p.Length - 1, false);
            Check((int)Prop(m, "Bloques") > 40, "Historia de filtros, puerta y picos realmente usada");
            if (p.Length > 1) Check((bool)Prop(m, "CuadroPartido"), "La lectura anterior deja un cuadro partido real");
            byte[] before = State(m);
            double[] other = (double[])p.Clone(); other[0] += .01;
            Check(!Reset(m, rate + 1, p) && !Reset(m, rate, null) && !Reset(m, rate, new double[p.Length + 1]) && !Reset(m, rate, other),
                "Formato o pesos incompatibles rechazados");
            Check(Equal(before, State(m)), "Rechazar formato no altera ningun dato de la medida previa");
            object state = Field(m, "estado"), blocks = Field(m, "bloques"), peaks = Field(m, "Picos"), hist = Field(peaks, "hist"), ownedWeights = Field(m, "peso");
            Check(Reset(m, rate, p), "Acepta exactamente la misma disposicion");
            Check(Object.ReferenceEquals(state, Field(m, "estado")) && Object.ReferenceEquals(blocks, Field(m, "bloques")) &&
                Object.ReferenceEquals(peaks, Field(m, "Picos")) && Object.ReferenceEquals(hist, Field(peaks, "hist")) &&
                Object.ReferenceEquals(ownedWeights, Field(m, "peso")), "Reinicio conserva filtros, bloques, histograma y pesos exclusivos");
            Zero(m, "estado", "anillo", "cuentas", "bloques"); Zero(peaks, "hist");
            Check(Equal(Result(New(meter, rate, p)), Result(m)), "Sin muestras vuelve al resultado de un constructor limpio");
            object fresh = New(meter, rate, p);
            Feed(m, rate, p.Length, 2.137, p.Length - 1, false); Feed(fresh, rate, p.Length, 2.137, p.Length - 1, false);
            Check(Equal(Result(fresh), Result(m)), "Despues del reset: K/puerta/pico/P99/silencios exactos con lecturas partidas");
            Check(Reset(m, rate, p), "Segundo reinicio");
            Feed(m, rate, p.Length, .617, 0, true); fresh = New(meter, rate, p); Feed(fresh, rate, p.Length, .617, 0, true);
            Check(Equal(Result(fresh), Result(m)), "Silencio tras musica no hereda picos ni localizacion de sonido");
        }
        // El literal -0.0 NO sirve: mcs (Mono) lo pliega a +0.0 al compilar (con csc/Roslyn si sale negativo), y entonces la prueba
        // compararia +0.0 contra +0.0 y fallaria por la herramienta, no por la rocola. Se fabrica el bit de signo a mano.
        double CeroNegativo = BitConverter.Int64BitsToDouble(long.MinValue);
        Check(BitConverter.DoubleToInt64Bits(CeroNegativo) != BitConverter.DoubleToInt64Bits(0.0), "el cero negativo de la prueba lo es de verdad");
        double[] mutable = { 1, 0.0 }; object copy = New(meter, 44100, mutable); mutable[0] = 4;
        Check(Reset(copy, 44100, new double[] { 1, 0.0 }) && !Reset(copy, 44100, new double[] { 1, CeroNegativo }), "Pesos clonados y bits de cero preservados");
        Console.WriteLine("  OK reinicio completo, formatos, pesos y lecturas partidas de 8 a 96 kHz");
    }
    static object Acquire(Type form, int rate, int channels) { return Call(form, null, "TomaTrabajoMedida", rate, channels, false); }
    static object Meter(object work, int rate, double[] p) { return Call(work.GetType(), work, "Sonoridad", rate, p); }
    static void Release(object work) { Call(work.GetType(), work, "Suelta"); }
    static void Workers(Type form, Type meter)
    {
        double[] p = { 1, 1 }; object w = Acquire(form, 44100, 2), m = Meter(w, 44100, p); Feed(m, 44100, 2, .61, 1, false);
        object nested = Acquire(form, 44100, 2), inner = Meter(nested, 44100, p);
        Check(!Object.ReferenceEquals(m, inner) && (bool)Prop(m, "CuadroPartido"), "Reentrada usa otro medidor y no borra el externo");
        Release(nested); Release(w); object again = Acquire(form, 44100, 2);
        Check(Object.ReferenceEquals(w, again) && Object.ReferenceEquals(m, Meter(again, 44100, p)) && (long)Prop(m, "Cuadros") == 0, "Trabajador reutiliza medidor limpio");
        Check(!Object.ReferenceEquals(m, Meter(again, 48000, p)), "Otra tasa reemplaza, no mantiene un diccionario por formato");
        object changed = Meter(again, 48000, p);
        Check(!Object.ReferenceEquals(changed, Meter(again, 48000, new double[] { 1, 1.41 })), "Otra asignacion de altavoces reemplaza");
        Check(Object.ReferenceEquals(Field(again, "integrada"), Meter(again, 48000, new double[] { 1, 1.41 })), "Una unica instancia retenida por trabajador"); Release(again);
        object[] parallel = new object[2]; Exception[] errors = new Exception[2]; Thread[] threads = new Thread[2];
        for (int i = 0; i < threads.Length; i++)
        {
            int slot = i; threads[i] = new Thread(delegate() {
                try { object a = Acquire(form, 8000, 1); parallel[slot] = Meter(a, 8000, new double[] { 1 }); Feed(parallel[slot], 8000, 1, .6, 0, slot == 0); Release(a); }
                catch (Exception e) { errors[slot] = e; }
            }); threads[i].Start();
        }
        foreach (Thread thread in threads) Check(thread.Join(10000), "Trabajador termina");
        for (int i = 0; i < errors.Length; i++) if (errors[i] != null) throw errors[i];
        Check(!Object.ReferenceEquals(parallel[0], parallel[1]) && !Object.ReferenceEquals(Field(parallel[0], "Picos"), Field(parallel[1], "Picos")), "Hilos no comparten medidor ni histograma");
        w = Acquire(form, 8000, 1); m = Meter(w, 8000, new double[] { 1 });
        Feed(m, 8000, 1, 820, 0, false); // 8197 bloques: la capacidad crece de 8192 a 16384.
        Check((long)Prop(m, "Cuadros") == 6560000 && (int)Prop(m, "Bloques") == 8197, "Cancion de 13 min 40 s medida hasta el ultimo cuadro");
        Check(((Array)Field(m, "bloques")).Length == 16384 && !(bool)Call(meter, m, "MemoriaRetenible", 8192), "Exceso de memoria real, no inyectado por reflexion");
        object clean = New(meter, 8000, new double[] { 1 }); Feed(clean, 8000, 1, 820, 0, false);
        Check(Equal(Result(clean), Result(m)), "Cancion larga conserva la medida completa");
        Release(w); Check(Field(w, "integrada") == null, "Al acabar descarta el bloque grande, no limita ni recorta la cancion");
        w = Acquire(form, 8000, 1); m = Meter(w, 8000, new double[] { 1 }); Feed(m, 8000, 1, 409, 0, false);
        Check(((Array)Field(m, "bloques")).Length == 4096, "Capacidad media productiva"); Release(w);
        w = Acquire(form, 8000, 1); Check(Object.ReferenceEquals(m, Meter(w, 8000, new double[] { 1 })), "Capacidad dentro del limite se reutiliza"); Release(w);
        Console.WriteLine("  OK trabajadores, reentrada, formatos y retencion limitada sin recortar canciones");
    }
    static string Wave(string root, int rate, int channels, double seconds, bool silent, bool layout)
    {
        string path = Path.Combine(root, rate + "-" + channels + "-" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + silent + "-" + layout + ".wav");
        float[] buffer = new float[4096 * channels]; byte[] bytes = new byte[buffer.Length * 4];
        long total = (long)(seconds * rate) * channels; int size = checked((int)(total * 4));
        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            // IEEE float valido tambien para 5.1: mascara explicita y subformato float, no PCM entero.
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write((layout ? 60 : 36) + size);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(layout ? 40 : 16);
            writer.Write((ushort)(layout ? 0xFFFE : 3)); writer.Write((ushort)channels); writer.Write(rate);
            writer.Write(rate * channels * 4); writer.Write((ushort)(channels * 4)); writer.Write((ushort)32);
            if (layout)
            {
                writer.Write((ushort)22); writer.Write((ushort)32); writer.Write((1 << channels) - 1);
                writer.Write(new byte[] { 3, 0, 0, 0, 0, 0, 0x10, 0, 0x80, 0, 0, 0xAA, 0, 0x38, 0x9B, 0x71 });
            }
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(size);
            for (long at = 0; at < total;)
            {
                int n = (int)Math.Min(buffer.Length, total - at);
                for (int i = 0; i < n; i++) buffer[i] = Sample(at + i, rate, channels, silent);
                Buffer.BlockCopy(buffer, 0, bytes, 0, n * 4); writer.Write(bytes, 0, n * 4); at += n;
            }
        }
        return path;
    }
    static byte[] Standalone(Type form, string file, bool cancel, out bool ok)
    {
        object owner = Codigo.FormSinEncender(form); object[] args = { file, null }; int polls = 0;
        Func<bool> stop = delegate { return ++polls >= 4; };
        FieldInfo cancellation = form.GetField("measureCancellation", B); cancellation.SetValue(null, cancel ? stop : null);
        try { ok = (bool)Call(form, owner, "MideIntegrada", args); }
        finally { cancellation.SetValue(null, null); }
        using (MemoryStream s = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(s))
        { writer.Write(ok); Value(writer, args[1]); writer.Flush(); return s.ToArray(); }
    }
    static void Equivalence(Type form, Type old, string root)
    {
        int[] rates = { 44100, 44100, 48000, 8000, 11025, 96000, 48000, 44100, 44100, 44100, 8000, 44100 };
        int[] channels = { 2, 2, 6, 1, 2, 2, 6, 2, 2, 2, 1, 2 };
        double[] seconds = { 12, .2, 1.3, 2.8, 2.317, 1.3, 1.3, 12, 12, 1.3, 820, 2.8 };
        for (int i = 0; i < rates.Length; i++)
        {
            bool silent = i == 8, cancel = i == 7, layout = i == 2;
            string file = Wave(root, rates[i], channels[i], seconds[i], silent, layout); bool expected, actual;
            byte[] previous = Standalone(old, file, cancel, out expected), current = Standalone(form, file, cancel, out actual);
            bool valid = i != 1 && i != 6 && !cancel;
            if (layout)
            {
                object p = Call(form.Assembly.GetType("Rocola.DisposicionBs1770", true), null, "DeArchivo", file, channels[i]);
                Check(Equal(State(p), State(new double[] { 1, 1, 1, 0, 1.41, 1.41 })), "WAV extensible realmente valida mascara y pesos 5.1");
                Check(actual, "WAV extensible 5.1 realmente decodificado sin depender de ACM");
                object reference = New(form.Assembly.GetType("Rocola.MedidorBs1770", true), rates[i], (double[])p);
                Feed(reference, rates[i], channels[i], seconds[i], 0, false);
                object[] close = { reference, seconds[i], rates[i], null };
                Check((bool)Call(form, null, "CierraIntegrada", close), "Referencia PCM independiente completa");
                using (MemoryStream stream = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(true); Value(writer, close[3]); writer.Flush();
                    Check(Equal(stream.ToArray(), current), "WAV 5.1 contra muestras independientes, todos los campos bit a bit");
                }
                Console.WriteLine("  WAV 5.1 extensible: lectura completa admitida por referencia=" + expected + ", actual=" + actual);
            }
            else Check(expected == valid && actual == valid, "Caso realmente medido/rechazado/cancelado " + i + ": anterior=" + expected + ", nuevo=" + actual);
            if (!layout || expected) Check(Equal(previous, current), "MideIntegrada antes/despues, todos los campos bit a bit: " + i);
            object scratch = form.GetField("trabajoMedidaDelHilo", B).GetValue(null);
            Check(scratch == null || !(bool)Field(scratch, "EnUso"), "MideIntegrada libera incluso al cancelar " + i);
            if (i == 10) Check(Field(scratch, "integrada") == null, "Ruta real descarta capacidad grande tras leer todo el WAV");
            // Misma memoria en las dos rutas productivas, sin contaminar curva ni resultado integrado.
            if (i == 0 || i == 8)
            {
                object[] a = { file, false, 0f, 0f, null };
                Check((bool)Call(form, Codigo.FormSinEncender(form), "MeasureFile", a), "Estimacion usa el mismo trabajador caliente");
                current = Standalone(form, file, false, out actual);
                Check(actual && Equal(previous, current), "Intercalar MeasureFile no altera la medida integrada siguiente");
            }
        }
        string broken = Path.Combine(root, "roto.wav"); using (BinaryWriter w = new BinaryWriter(File.Create(broken))) w.Write(1234567);
        foreach (string file in new string[] { broken, Path.Combine(root, "noexiste.wav"), null })
        { bool a, b; Check(Equal(Standalone(old, file, false, out a), Standalone(form, file, false, out b)) && !a && !b, "Archivo roto/ausente/null conserva rechazo y salida"); }
        Console.WriteLine("  OK ruta integrada real contra referencia, cancelacion, silencios, multicanal y cancion larga");
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Assembly current = Assembly.LoadFrom(args[0]); Type form = current.GetType("Rocola.Form1", true), meter = current.GetType("Rocola.MedidorBs1770", true);
            ResetCases(meter); Workers(form, meter);
            if (args.Length > 1)
            {
                Assembly baseline = Assembly.Load(File.ReadAllBytes(args[1]));
                string root = Path.Combine(Path.GetTempPath(), "rocola-memoria-integrada-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
                Equivalence(form, baseline.GetType("Rocola.Form1", true), root); Console.WriteLine("  Evidencia aislada: " + root);
            }
            Check(Codigo.Llama(form.GetMethod("MideIntegrada", B), "TomaTrabajoMedida") && Codigo.Llama(form.GetMethod("MideIntegrada", B), "Suelta"), "Ruta integrada adquiere y devuelve memoria incluso en fallos");
            Check(Codigo.Llama(form.GetMethod("MeasureFile", B), "Sonoridad"), "Estimacion reutiliza integrada sin cambiar la receta");
            Console.WriteLine("PASS memoriaintegrada: " + checks + " comprobaciones; no reproduce audio ni mide FPS"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

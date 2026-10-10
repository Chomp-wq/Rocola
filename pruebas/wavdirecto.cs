// WAV generados, datos aislados y lectores reales. No reproduce audio ni mide FPS.
using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;
class WavDirectoPrueba
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static Type form, direct, format;
    static string root;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static object Call(Type t, object o, string n, params object[] a) { return t.GetMethod(n, B).Invoke(o, a); }
    static object Prop(object o, string n) { return o.GetType().GetProperty(n, B).GetValue(o, null); }
    static void Set(object o, string n, object v) { o.GetType().GetProperty(n, B).SetValue(o, v, null); }
    static object Source(Type f, string path, bool measure)
    { return Activator.CreateInstance(f.GetNestedType(measure ? "LectorMedida" : "FileAudioSource", B), B, null, new object[] { path }, null); }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    struct FloatBits
    {
        [System.Runtime.InteropServices.FieldOffset(0)] internal float Sample;
        [System.Runtime.InteropServices.FieldOffset(0)] internal int Bits;
    }
    static bool Bits(float a, float b) { FloatBits x = new FloatBits(), y = new FloatBits(); x.Sample = a; y.Sample = b; return x.Bits == y.Bits; }
    static void Value(BinaryWriter w, object x)
    {
        if (x == null) { w.Write("null"); return; }
        Type t = x.GetType(); w.Write(t.FullName);
        if (t == typeof(int)) w.Write((int)x); else if (t == typeof(long)) w.Write((long)x);
        else if (t == typeof(float)) w.Write((float)x); else if (t == typeof(double)) w.Write((double)x);
        else if (t == typeof(bool)) w.Write((bool)x);
        else if (x is Array) { Array a = (Array)x; w.Write(a.Length); foreach (object i in a) Value(w, i); }
        else
        {
            FieldInfo[] fields = t.GetFields(B);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return String.CompareOrdinal(a.Name, b.Name); });
            foreach (FieldInfo f in fields) if (!f.IsStatic) { w.Write(f.Name); Value(w, f.GetValue(x)); }
        }
    }
    static byte[] State(object x)
    { using (MemoryStream s = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(s)) { Value(w, x); w.Flush(); return s.ToArray(); } }
    static bool Equal(byte[] a, byte[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    sealed class Case
    {
        internal int Rate = 48000, Channels, Bits, Valid, Mask, Frames = 62897;
        internal bool Float, Ext;
        internal Case(int bits, int valid, int channels, int mask, bool fp, bool ext)
        { Bits = bits; Valid = valid; Channels = channels; Mask = mask; Float = fp; Ext = ext; }
    }
    static float[] Wave(Case c, string path, int bad)
    {
        float[] expected = new float[c.Frames * c.Channels]; int align = c.Channels * (c.Bits / 8), size = expected.Length * (c.Bits / 8);
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            // Un chunk impar con padding y otro tras data: el lector no puede confundirlos con muestras.
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write((c.Ext ? 60 : 36) + size + 24 + (size & 1));
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEJUNK")); w.Write(5); w.Write(new byte[6]);
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(c.Ext ? 40 : 16);
            w.Write((ushort)(c.Ext ? 0xFFFE : (c.Float ? 3 : 1))); w.Write((ushort)c.Channels); w.Write(c.Rate);
            w.Write(c.Rate * align); w.Write((ushort)(bad == 1 ? align + 1 : align)); w.Write((ushort)c.Bits);
            if (c.Ext)
            {
                w.Write((ushort)22); w.Write((ushort)(bad == 2 ? 0 : c.Valid)); w.Write(c.Mask);
                w.Write(new Guid(bad == 3 ? "00000006-0000-0010-8000-00aa00389b71" : (c.Float ? "00000003-0000-0010-8000-00aa00389b71" : "00000001-0000-0010-8000-00aa00389b71")).ToByteArray());
            }
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(size);
            for (int i = 0; i < expected.Length; i++)
            {
                double time = (i / c.Channels) / (double)c.Rate;
                double x = time < .09 || (time > .8 && time < 1.05) ? 0 : .36 * Math.Sin(time * Math.PI * 2 * (311 + (i % c.Channels) * 197));
                if (c.Float)
                {
                    if (bad == 4 && i < 6) x = i == 0 ? double.NaN : (i == 1 ? double.PositiveInfinity : (i == 2 ? double.NegativeInfinity : (i == 3 ? 5 : (i == 4 ? -5 : 4))));
                    if (c.Bits == 64) w.Write(x); else w.Write((float)x); expected[i] = (float)x;
                }
                else
                {
                    long q = (long)Math.Round(x * Math.Pow(2, c.Valid - 1)) << (c.Bits - c.Valid);
                    expected[i] = (float)q / (float)Math.Pow(2, c.Bits - 1);
                    if (c.Bits == 8) w.Write((byte)(q + 128)); else if (c.Bits == 16) w.Write((short)q);
                    else if (c.Bits == 24) { w.Write((byte)q); w.Write((byte)(q >> 8)); w.Write((byte)(q >> 16)); }
                    else w.Write((int)q);
                }
            }
            if ((size & 1) != 0) w.Write((byte)0);
            w.Write(System.Text.Encoding.ASCII.GetBytes("JUNK")); w.Write(2); w.Write((ushort)0x5AA5);
        }
        return expected;
    }
    static void Released(string path)
    { using (FileStream s = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(s.Length > 0, "Archivo cerrado incluso ante fallo"); }
    static void ReadExact(object s, float[] expected, int at, int end, float gain, bool sanitize, bool fragments)
    {
        float[] b = new float[4109]; int[] sizes = fragments ? new int[] { 1, 7, 31, 4096, 4093, 2 } : new int[] { 4096 * ((WaveFormat)Prop(s, "WaveFormat")).Channels };
        if (!fragments) b = new float[sizes[0] + 10]; int part = 0;
        while (at < end)
        {
            b[0] = b[1] = b[2] = 12345; int count = Math.Min(sizes[part++ % sizes.Length], end - at); b[3 + count] = 12345;
            int n = (int)Call(s.GetType(), s, "Read", b, 3, count);
            Check(n == count && b[0] == 12345 && b[2] == 12345 && b[3 + count] == 12345, "Lectura completa, offset y bordes intactos");
            for (int i = 0; i < n; i++)
            {
                float x = gain == 1f ? expected[at + i] : expected[at + i] * gain;
                if (sanitize && !(x >= -4f && x <= 4f)) x = 0f;
                if (!Bits(x, b[3 + i])) throw new Exception("Muestra/canal/precision alterados en " + (at + i));
            }
            at += n;
        }
        Check(true, "Todas las muestras comparadas bit a bit");
    }
    static void Readers(Case c, string path, float[] expected, Type old)
    {
        foreach (bool measure in new bool[] { false, true })
        {
            object s = Source(form, path, measure);
            try
            {
                WaveFormat f = (WaveFormat)Prop(s, "WaveFormat");
                Check(f.SampleRate == c.Rate && f.Channels == c.Channels && f.BitsPerSample == 32, "No reduce canales ni remuestrea");
                int mask = (int)Call(format, null, "Mascara", f);
                int wanted = c.Ext ? ((bool)Call(format, null, "Valida", c.Mask, c.Channels) ? c.Mask : 0) : (c.Channels == 1 ? 4 : (c.Channels == 2 ? 3 : 0));
                Check(mask == wanted, "Mascara conservada sin inventar layout");
                double delta = Math.Abs(((TimeSpan)Prop(s, "TotalTime")).TotalSeconds - c.Frames / (double)c.Rate);
                Check(delta <= (measure ? .001 : .0000001), "Duracion de todos los cuadros conservada");
                ReadExact(s, expected, 0, expected.Length, 1, measure, true);
                float[] tail = new float[8]; Check((int)Call(s.GetType(), s, "Read", tail, 0, 7) == 0, "EOF sin leer padding ni chunk posterior");
                if (measure) Set(s, "CurrentTime", TimeSpan.Zero); else Call(s.GetType(), s, "Salta", TimeSpan.Zero);
                Set(s, "Volume", .43f); ReadExact(s, expected, 0, 29, .43f, measure, true);
                TimeSpan seek = TimeSpan.FromTicks(3123456); Set(s, "Volume", 1f);
                if (measure)
                {
                    Set(s, "CurrentTime", seek); int frame = (int)Math.Floor(seek.TotalSeconds * c.Rate);
                    ReadExact(s, expected, frame * c.Channels, frame * c.Channels + 47, 1, true, true);
                    Set(s, "CurrentTime", TimeSpan.Zero);
                }
                else
                {
                    Call(s.GetType(), s, "Salta", seek); int frame = (int)Math.Round(seek.TotalSeconds * c.Rate);
                    Check(Math.Abs(((TimeSpan)Prop(s, "CurrentTime")).TotalSeconds - frame / (double)c.Rate) < .0000001, "Seek por cuadro y tiempo exactos");
                    ReadExact(s, expected, frame * c.Channels, frame * c.Channels + 47, 1, false, true);
                    Call(s.GetType(), s, "Salta", TimeSpan.FromSeconds(-1));
                }
                ReadExact(s, expected, 0, 19, 1, measure, true); // Buscar descarta los canales pendientes del cuadro anterior.
                if (!measure) { Call(s.GetType(), s, "Salta", TimeSpan.FromDays(1)); Check((int)Call(s.GetType(), s, "Read", tail, 0, 7) == 0, "Seek mas alla del final no reproduce basura"); }
            }
            finally { ((IDisposable)s).Dispose(); ((IDisposable)s).Dispose(); }
            Released(path);
            if (!c.Ext && old != null)
            {
                object before = Source(old, path, measure);
                try { ReadExact(before, expected, 0, expected.Length, 1, measure, false); }
                finally { ((IDisposable)before).Dispose(); }
            }
        }
    }
    static void Analysis(Case c, string path, float[] expected)
    {
        object owner = Codigo.FormSinEncender(form); object[] a = { path, null };
        bool ok = (bool)Call(form, owner, "MideIntegrada", a);
        bool known = c.Channels <= 2 || c.Mask == 0x3F || c.Mask == 0x63F || c.Mask == 0x60F;
        Check(ok == known, "Integrada admite layouts conocidos, no inventa los desconocidos");
        if (ok)
        {
            double[] weights = (double[])Call(form.Assembly.GetType("Rocola.DisposicionBs1770", true), null, "DeArchivo", path, c.Channels);
            if (c.Channels == 6) Check(Equal(State(weights), State(new double[] { 1, 1, 1, 0, 1.41, 1.41 })), "LFE fuera de LUFS 5.1, surround ponderado");
            if (c.Channels == 8) Check(Equal(State(weights), State(new double[] { 1, 1, 1, 0, 1, 1, 1.41, 1.41 })), "Layout y pesos actuales de 7.1 conservados");
            if (c.Channels == 2 && c.Mask == 0xC) Check(Equal(State(weights), State(new double[] { 1, 0 })), "Par centro/LFE no se confunde con estereo izquierdo/derecho");
            object meter = Activator.CreateInstance(form.Assembly.GetType("Rocola.MedidorBs1770", true), B, null, new object[] { c.Rate, weights }, null);
            Call(meter.GetType(), meter, "Procesa", expected, 0, expected.Length);
            object[] close = { meter, c.Frames / (double)c.Rate, c.Rate, null };
            Check((bool)Call(form, null, "CierraIntegrada", close) && Equal(State(a[1]), State(close[3])), "Integrada/picos/silencios exactos contra PCM independiente");
        }
        object[] estimate = { path, false, 0f, 0f, null };
        Check((bool)Call(form, owner, "MeasureFile", estimate) && (float)estimate[2] > 0f && (float)estimate[3] > 1f && estimate[4] != null, "Estimacion RMS/EQ real tambien lee WAV extensible");
    }
    static void Failures()
    {
        Case c = new Case(32, 32, 6, 0x3F, true, true);
        for (int bad = 1; bad <= 3; bad++)
        {
            string path = Path.Combine(root, "invalido-" + bad + ".wav"); Wave(c, path, bad); bool rejected = false;
            try { object s = Call(direct, null, "Abre", path); if (s == null) rejected = true; else ((IDisposable)s).Dispose(); }
            catch (TargetInvocationException e) { rejected = e.InnerException is InvalidDataException; }
            Check(rejected, "Cabecera invalida u otro subformato no se confunde con float"); Released(path);
        }
        string truncated = Path.Combine(root, "truncado.wav"); Wave(c, truncated, 0);
        using (FileStream f = File.OpenWrite(truncated)) f.SetLength(128);
        bool fails = false; try { object s = Call(direct, null, "Abre", truncated); if (s != null) ((IDisposable)s).Dispose(); }
        catch (TargetInvocationException) { fails = true; }
        Check(fails, "Datos truncados rechazados sin aceptarlos como cancion completa"); Released(truncated);
        string broken = Path.Combine(root, "roto.wav"); using (BinaryWriter w = new BinaryWriter(File.Create(broken))) w.Write(1234);
        for (int i = 0; i < 12; i++)
        {
            bool failed = false; try { Call(direct, null, "Abre", broken); } catch (TargetInvocationException) { failed = true; }
            Check(failed, "Constructor fallido no fuga el archivo"); Released(broken);
        }
        Check(Call(direct, null, "Abre", "cancion.mp3") == null && Call(direct, null, "Abre", (object)null) == null, "Otros formatos y null mantienen su ruta");
        string nonfinite = Path.Combine(root, "no-finitos.wav"); float[] samples = Wave(c, nonfinite, 4);
        object reader = Source(form, nonfinite, true);
        try { ReadExact(reader, samples, 0, samples.Length, 1, true, true); } finally { ((IDisposable)reader).Dispose(); }
        Released(nonfinite);
        string raw = Path.Combine(root, "bytes.wav"); Wave(c, raw, 0);
        WaveStream stream = (WaveStream)Call(direct, null, "Abre", raw);
        try
        {
            byte[] b = new byte[40];
            Check(stream.Read(b, 4, 3) == 3 && stream.Position == 3, "Lectura parcial no adelanta el reloj al cuadro precargado");
            Check(stream.Read(b, 4, 0) == 0 && stream.Position == 3, "Lectura vacia no mueve posicion ni pendientes");
            Check(stream.Read(b, 4, 7) == 7 && stream.Position == 10, "Bytes pendientes continuos entre llamadas");
            bool bounds = false; try { stream.Read(b, 38, 3); } catch (ArgumentOutOfRangeException) { bounds = true; }
            Check(bounds && stream.Position == 10, "Argumentos invalidos no consumen audio");
            stream.Position = 0;
            Check(stream.Read(b, 0, 24) == 24 && stream.Position == 24, "Seek limpia pendiente y reinicia por cuadro");
        }
        finally { stream.Dispose(); stream.Dispose(); }
        bool closed = false; try { stream.Read(new byte[24], 0, 24); } catch (ObjectDisposedException) { closed = true; }
        Check(closed, "Lectura tras cierre falla de forma controlada"); Released(raw);
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Assembly assembly = Assembly.LoadFrom(args[0]); form = assembly.GetType("Rocola.Form1", true);
            direct = assembly.GetType("Rocola.WavDirecto", true); format = assembly.GetType("Rocola.FormatoAudio", true);
            Type old = args.Length > 1 ? Assembly.Load(File.ReadAllBytes(args[1])).GetType("Rocola.Form1", true) : null;
            root = Path.Combine(Path.GetTempPath(), "rocola-wav-directo-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Case[] cases = {
                new Case(8,8,1,0,false,false), new Case(16,16,2,0,false,false), new Case(24,24,2,0,false,false),
                new Case(32,32,2,0,false,false), new Case(32,32,2,0,true,false), new Case(64,64,2,0,true,false),
                new Case(16,16,6,0x3F,false,true), new Case(24,20,6,0x60F,false,true), new Case(32,24,8,0x63F,false,true),
                new Case(32,32,6,0x3F,false,true), new Case(32,32,6,0x3F,true,true), new Case(64,64,6,0x3F,true,true),
                new Case(32,32,6,0,true,true), new Case(32,32,6,3,true,true), new Case(32,32,2,0xC,true,true),
                new Case(32,32,1,0,true,true), new Case(8,8,1,4,false,true) };
            for (int i = 0; i < cases.Length; i++)
            {
                Case c = cases[i]; string path = Path.Combine(root, "caso-" + i + ".WAV"); float[] expected = Wave(c, path, 0);
                Readers(c, path, expected, old);
                // Solo canales con layout conocido permiten integrada; el resto conserva la estimacion.
                if (c.Ext) Analysis(c, path, expected);
                Console.WriteLine("  OK " + i + ": " + c.Bits + " bits, " + c.Channels + " canales, extensible=" + c.Ext);
            }
            Failures();
            foreach (string n in new string[] { "FileAudioSource", "LectorMedida" })
            {
                ConstructorInfo constructor = form.GetNestedType(n, B).GetConstructor(B, null, new Type[] { typeof(string) }, null);
                Check(Codigo.Llama(constructor, "Abre"), "Reproduccion y medicion usan la ruta WAV directa");
            }
            Console.WriteLine("PASS wavdirecto: " + checks + " comprobaciones; muestras bit a bit; sin dispositivo de audio. Evidencia: " + root); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

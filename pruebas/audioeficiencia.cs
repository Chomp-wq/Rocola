using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// Sin dispositivos ni biblioteca: exactitud del cache DSP y del no-op EQ.
class AudioEficiencia
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(Type t, object o, string n) { return t.GetField(n, F).GetValue(o); }
    static void Set(Type t, object o, string n, object v) { t.GetField(n, F).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] v) { return t.GetMethod(n, F).Invoke(o, v); }
    static void Check(bool b, string why) { if (!b) throw new Exception(why); }
    static void Exact(float[] a, float[] b, string why)
    {
        Check(a.Length == b.Length, why);
        for (int i = 0; i < a.Length; i++)
            if (BitConverter.ToInt32(BitConverter.GetBytes(a[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0))
                throw new Exception(why + " @" + i);
    }
    sealed class Input : ISampleProvider
    {
        readonly WaveFormat format; long position;
        public Input(int rate, int channels) { format = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels); }
        public WaveFormat WaveFormat { get { return format; } }
        public int Read(float[] b, int offset, int count)
        {
            for (int i = 0; i < count; i++, position++)
                b[offset+i] = (float)(.12 * Math.Sin(position * .041) + .08 * Math.Sin(position * .113));
            return count;
        }
    }
    static ISampleProvider Level(Type form, int rate, int channels)
    {
        object owner = FormatterServices.GetUninitializedObject(form);
        foreach (FieldInfo f in form.GetFields(F))
            if (!f.IsStatic && f.FieldType.Namespace != null && f.FieldType.Namespace.StartsWith("System.Collections") &&
                f.FieldType.GetConstructor(Type.EmptyTypes) != null) f.SetValue(owner, Activator.CreateInstance(f.FieldType));
        Set(form, owner, "levelLock", new object()); Set(form, owner, "audioDatosLock", new object());
        return (ISampleProvider)Activator.CreateInstance(form.GetNestedType("LevelTap", F), F, null,
            new object[] { new Input(rate, channels), owner }, null);
    }
    static void Curve(ISampleProvider tap, float[] curve)
    {
        tap.GetType().GetProperty("EqDb", F).SetValue(tap, curve, null);
    }
    static void Eq(Type form)
    {
        foreach (int rate in new int[] {22050, 44100, 48000, 96000})
        foreach (int channels in new int[] {1, 2, 6})
        {
            ISampleProvider a = Level(form, rate, channels), b = Level(form, rate, channels);
            float[] curve = {2, -3, 0, .00001f, 4, -1, 0, 1.5f};
            Curve(a, curve); Curve(b, curve);
            Type t = a.GetType(); object pending = Get(t, a, "pendingEq");
            float[] x = new float[257*channels], y = new float[x.Length];
            for (int block = 0; block < 20; block++)
            {
                if (block > 0)
                {
                    Curve(a, (float[])curve.Clone());
                    Check(Object.ReferenceEquals(pending, Get(t, a, "pendingEq")), "EQ igual recrea estados");
                }
                a.Read(x, 0, x.Length); b.Read(y, 0, y.Length); Exact(x, y, "EQ igual altera PCM en vivo");
                Check((int)Get(t, a, "eqFadeLeft") == 0, "EQ igual abre crossfade");
            }
            float[] different = (float[])curve.Clone();
            different[0] = BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(curve[0]), 0)+1), 0);
            Curve(a, different); Check(!Object.ReferenceEquals(pending, Get(t, a, "pendingEq")), "EQ distinta se pierde por tolerancia");
            Curve(a, null); Check(Get(t, a, "pendingEq") == null, "EQ no se desactiva");
        }
    }
    static int Hz(Type t, object tap, float hz) { return (int)Call(t, tap, "Hz", hz); }
    static bool Reference(Type t, object tap, int cut, int topBin, float[] features, float[] targets)
    {
        int frame = (int)Get(t, tap, "Frame"); float bin = (float)Get(t, tap, "binHz");
        float[] re = (float[])Get(t, tap, "re"), im = (float[])Get(t, tap, "im");
        float n2 = (float)frame*(float)frame, fc = cut*bin;
        if (fc < 1200f) return false;
        double sum = 0;
        for (int i = 0; i < 24; i++)
        {
            int a = Hz(t, tap, 200f*(float)Math.Pow(fc/200f, i/24.0));
            int b = Hz(t, tap, 200f*(float)Math.Pow(fc/200f, (i+1)/24.0));
            if (b <= a) b = a+1;
            double e = 0; for (int k = a; k < b; k++) e += re[k]*re[k]+im[k]*im[k];
            features[i] = (float)Math.Log10(e/((b-a)*n2)+1e-12); sum += features[i];
            Check(((int[])Get(t, tap, "iaFeatureA"))[i] == a && ((int[])Get(t, tap, "iaFeatureB"))[i] == b, "Bin de entrada distinto");
        }
        float mean = (float)(sum/24.0); for (int i = 0; i < 24; i++) features[i] -= mean;
        features[24] = mean; features[25] = fc/20000f;
        double am = 0, gm = 0;
        for (int i = 16; i < 24; i++) { am += Math.Pow(10.0, features[i]+mean); gm += features[i]+mean; }
        features[26] = (float)(gm/8.0-Math.Log10(am/8.0+1e-12)); features[27] = 0;
        int sa = Hz(t, tap, cut*bin*.92f); if (sa >= cut) sa = cut-1;
        double seam = 0; for (int k = sa; k < cut; k++) seam += re[k]*re[k]+im[k]*im[k];
        seam = seam/((cut-sa)*n2)+1e-12;
        float top = topBin*bin;
        for (int j = 0; j < 12; j++)
        {
            int a = Hz(t, tap, fc*(float)Math.Pow(top/fc, j/12.0));
            int b = Hz(t, tap, fc*(float)Math.Pow(top/fc, (j+1)/12.0));
            if (a < cut) a = cut; if (b > topBin) b = topBin;
            Check(((int[])Get(t, tap, "iaTargetA"))[j] == a && ((int[])Get(t, tap, "iaTargetB"))[j] == b, "Bin de objetivo distinto");
            if (b <= a) return false;
            double e = 0; for (int k = a; k < b; k++) e += re[k]*re[k]+im[k]*im[k];
            e = e/((b-a)*n2)+1e-12;
            float y = (float)Math.Log10(e/seam); if (y > .1f) y = .1f; else if (y < -4f) y = -4f;
            targets[j] = y;
        }
        return true;
    }
    static void Bands(Type form)
    {
        Type t = form.GetNestedType("SpectralTap", F);
        bool hd = t.GetConstructor(F, null, new Type[] {typeof(ISampleProvider), typeof(int)}, null) != null;
        int cases = 0;
        foreach (int rate in new int[] {8000, 16000, 22050, 44100, 48000, 96000})
        foreach (int size in hd ? new int[] {2048, 4096} : new int[] {2048})
        {
            object tap = Activator.CreateInstance(t, F, null, hd ? new object[] {new Input(rate, 2), size} :
                                                 new object[] {new Input(rate, 2)}, null);
            int frame = (int)Get(t, tap, "Frame"), ny = frame/2;
            float bin = (float)Get(t, tap, "binHz");
            float[] re = (float[])Get(t, tap, "re"), im = (float[])Get(t, tap, "im");
            for (int k = 0; k < frame; k++) { re[k] = (float)(.01+Math.Sin(k*.731)*.1); im[k] = (float)(Math.Cos(k*.353)*.07); }
            for (int cut = 64; cut < ny-4; cut += 7)
            {
                float cutHz = (cut+.25f)*bin;
                int actual = (int)(cutHz/bin);
                int top = Math.Min((int)(Math.Min(20500f, ny*bin*.95f)/bin), Math.Min(2*actual-2, ny-4));
                if (top <= actual+4 || actual*bin < 1200f) continue;
                float[] x = new float[28], y = new float[12], xr = new float[28], yr = new float[12];
                bool result = (bool)Call(t, tap, "MakePair", cutHz, x, y);
                bool expected = Reference(t, tap, actual, top, xr, yr);
                Check(result == expected, "Cambia validez de par IA");
                if (result) { Exact(x, xr, "Entrada IA cambia"); Exact(y, yr, "Objetivo IA cambia"); }
                Call(t, tap, "MakePair", cutHz, x, y); // hit de ambas caches conserva valores
                if (result) { Exact(x, xr, "Cache de entrada cambia"); Exact(y, yr, "Cache de objetivo cambia"); }
                Call(t, tap, "PreparaIaTargetBands", actual, top-1);
                Call(t, tap, "MakePair", cutHz, x, y); // invalidacion del tope
                if (result) Exact(y, yr, "Tope anterior queda en cache");
                cases++;
            }
        }
        Console.WriteLine("PASS: " + cases + " cortes/formato; bins, features y objetivos exactos");
    }
    static void Warmup(Type form)
    {
        for (int i = 0; i < 4; i++) Check((bool)Call(form, null, "PrecalientaTablasAudio", i), "Precarga falla");
        object a = Get(form, null, "fft2048"), b = Get(form, null, "fft4096");
        for (int i = 0; i < 4; i++) Check((bool)Call(form, null, "PrecalientaTablasAudio", i), "Precarga repetida falla");
        Check(Object.ReferenceEquals(a, Get(form, null, "fft2048")) && Object.ReferenceEquals(b, Get(form, null, "fft4096")), "Recrea planes FFT");
    }
    static int Main(string[] args)
    {
        try
        {
            Type form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
            Warmup(form); Eq(form); Bands(form);
            Console.WriteLine("PASS: precarga idempotente y EQ clonado conserva PCM bit a bit (12 formatos)");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

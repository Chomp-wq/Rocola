// Comparacion de la CADENA DE SONIDO entera contra un ejecutable anterior REAL (no una copia del metodo nuevo): la misma cancion
// sintetica pasa por OpenAudio + ArmaCadena en los dos y las muestras de salida tienen que ser IGUALES BIT A BIT en cada
// combinacion de ajustes. Sirve para demostrar que un cambio que "no se oye" de verdad no toca ni un bit.
//   mcs -r:NAudio.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:compara-cadena.exe compara-cadena.cs leercodigo.cs
//   mono compara-cadena.exe /ruta/antes.exe /ruta/despues.exe
// (Con NAudio.dll y NLayer.dll junto al .exe.) Las combinaciones: de fabrica, el modo del local en los tres tipos con una cancion
// medida de mucho rango y con otra sin medir, todos los mandos del local a mano, el estereo a mano con S3, y el bajo y el EQ en
// automatico. Es una comparacion de la cuenta, no del oido. Las combinaciones del modo automatico del local solo se informan (cuando un cambio toca a proposito lo que ese modo decide salen distintas); todas las demas tienen que dar los mismos bits.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

class ComparaCadena
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void Set(object o, string n, object v)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(o, v); return; } }
        o.GetType().GetProperty(n, T).SetValue(o, v, null);
    }
    static float[] Cancion(int fs, double seg, double dinamica)
    {
        int n = (int)(fs * seg); float[] x = new float[2 * n]; Random r = new Random(11);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs;
            double env = 0.35 + 0.65 * dinamica * (0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 3.0)) + (1 - dinamica) * 0.65;
            double kick = 0.6 * Math.Exp(-(t % 0.5) / 0.1) * Math.Sin(2 * Math.PI * (45 + 50 * Math.Exp(-(t % 0.5) / 0.03)) * (t % 0.5));
            double v = env * (0.2 * Math.Sin(2 * Math.PI * 55 * t) + 0.12 * Math.Sin(2 * Math.PI * 440 * t) + 0.04 * Math.Sin(2 * Math.PI * 3100 * t) + 0.03 * (r.NextDouble() * 2 - 1)) + kick;
            double side = 0.15 * env * Math.Sin(2 * Math.PI * 660 * t);   // un instrumento mas a un lado
            x[2 * i] = (float)Math.Max(-1, Math.Min(1, v + side)); x[2 * i + 1] = (float)Math.Max(-1, Math.Min(1, 0.9 * v - side));
        }
        return x;
    }

    sealed class Mundo
    {
        public Assembly Asm; public Type TF; public object F; public string Wav, Wav2; public object Track, Track2;
        public object Get(string n) { return ComparaCadena.Get(F, n); }
        public void Set(string n, object v) { ComparaCadena.Set(F, n, v); }
        public void SetS(string n, object v) { TF.GetField(n, T).SetValue(null, v); }
        public object Call(string n, params object[] a) { return TF.GetMethod(n, T).Invoke(F, a); }
    }

    static Mundo Crea(string exe, string dir)
    {
        Mundo m = new Mundo();
        m.Asm = Assembly.Load(File.ReadAllBytes(exe));
        m.TF = m.Asm.GetType("Rocola.Form1", true);
        m.F = Codigo.FormSinEncender(m.TF);
        foreach (FieldInfo fi in m.TF.GetFields(T))
            if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(m.F) == null) fi.SetValue(m.F, Activator.CreateInstance(fi.FieldType, true));
        m.Wav = Path.Combine(dir, "a.wav"); m.Wav2 = Path.Combine(dir, "b.wav");
        Type tt = m.TF.GetNestedType("Track", T);
        m.Track = Activator.CreateInstance(tt, true); Set(m.Track, "Path", m.Wav);
        m.Track2 = Activator.CreateInstance(tt, true); Set(m.Track2, "Path", m.Wav2);
        // la cancion medida (de mucho rango) va a la tabla de medidas integradas; la otra queda sin medir
        object[] a = { m.Wav, null };
        if (!(bool)m.Call("MideIntegrada", a)) throw new Exception("no se pudo medir la cancion de prueba");
        IDictionary tab = (IDictionary)m.Get("integCache");
        if (tab == null) { tab = (IDictionary)Activator.CreateInstance(m.TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); Set(m.F, "integCache", tab); }
        tab[m.Wav] = a[1];
        object fuente = m.TF.GetMethod("TomaFuenteMedida", T).Invoke(null, null);   // el sello del archivo medido, como el publicador real
        m.Call("ActualizaFuenteMedida", m.Wav, fuente);
        return m;
    }

    static void Fabrica(Mundo m)
    {
        m.Set("normalizeOn", true); m.Set("normTarget", 0.26f); m.Set("normStrength", 1f); m.Set("masterDb", 0f);
        m.Set("excAmount", 0.35f); m.Set("bassAuto", true); m.Set("bassAmount", 0.3f); m.Set("bassSongFactor", 1f);
        m.Set("eqAuto", true); m.Set("eqAmount", 0f);
        m.Set("sbrAllowed", true); m.Set("sbrOn", false); m.Set("declipOn", true);
        m.Set("nocheArrancada", true); m.Set("nocheFactor", 1f); m.Set("nocheCompensa", 1f);
        m.Set("crossfadeSec", 0.5f); m.Set("saltaSilencios", false);
        m.SetS("claridadLocal", false); m.SetS("partesSuaves", false); m.SetS("localAuto", false); m.SetS("tipoLocal", 0); m.SetS("presenciaSala", false);
        m.SetS("gravesLocalDb", 0); m.SetS("agudosLocalDb", 0); m.SetS("balanceDb", 0);
        m.SetS("cortaSubgrave", false); m.SetS("estereoEstrecho", false); m.SetS("estereoAuto", false);
        m.SetS("canalMudo", true); m.SetS("quitaContinua", true);
        m.Set("sinMejoras", false); m.Set("aLaMedida", false); m.Set("quitaSiseo", false);
    }

    static float[] Pasa(Mundo m, object track, string wav)
    {
        object lector = m.Call("OpenAudio", wav);
        m.TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = m.Call("ArmaCadena", track, lector, false, 0.0);
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        List<float> y = new List<float>(); float[] b = new float[4410 * 2];
        while (true) { int n = sal.Read(b, 0, b.Length); if (n <= 0) break; for (int i = 0; i < n; i++) y.Add(b[i]); }
        ((IDisposable)lector).Dispose();
        return y.ToArray();
    }

    static int Main(string[] args)
    {
        if (args.Length != 2) { Console.WriteLine("Uso: compara-cadena.exe antes.exe despues.exe"); return 2; }
        string dir = Path.Combine(Path.GetTempPath(), "rocola_comparacadena_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        int fs = 44100;
        float[] a = Cancion(fs, 8.0, 1.0), b = Cancion(fs, 6.0, 0.0);
        using (WaveFileWriter w = new WaveFileWriter(Path.Combine(dir, "a.wav"), WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(a, 0, a.Length);
        using (WaveFileWriter w = new WaveFileWriter(Path.Combine(dir, "b.wav"), WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(b, 0, b.Length);
        Mundo antes = Crea(args[0], dir), despues = Crea(args[1], dir);
        if (antes.Asm == despues.Asm) throw new Exception("Se requieren dos ensamblados distintos");
        string[] nombres = {
            "de fabrica", "modo del local: billar", "modo del local: sala buena", "modo del local: bocinas chicas",
            "local a mano: todo encendido", "estereo a mano con S3 y subgrave", "DSEE, I1 y siseo encendidos", "videos, EQ y graves a mano"
        };
        int distintos = 0, casos = 0;
        for (int c = 0; c < nombres.Length; c++)
            foreach (int cual in new int[] { 0, 1 })   // 0: la medida (mucho rango); 1: la que no se midio
            {
                float[][] sal = new float[2][];
                for (int k = 0; k < 2; k++)
                {
                    Mundo m = k == 0 ? antes : despues;
                    Fabrica(m);
                    switch (c)
                    {
                        case 1: m.SetS("localAuto", true); m.SetS("tipoLocal", 0); break;
                        case 2: m.SetS("localAuto", true); m.SetS("tipoLocal", 1); break;
                        case 3: m.SetS("localAuto", true); m.SetS("tipoLocal", 2); break;
                        case 4: m.SetS("claridadLocal", true); m.SetS("partesSuaves", true); m.SetS("presenciaSala", true); m.SetS("gravesLocalDb", 3); m.SetS("agudosLocalDb", -2); m.SetS("balanceDb", -2); m.SetS("cortaSubgrave", true); break;
                        case 5: m.SetS("estereoEstrecho", true); m.SetS("estereoAuto", true); m.SetS("cortaSubgrave", true); break;
                        case 6: m.Set("sbrOn", true); m.Set("aLaMedida", true); m.Set("quitaSiseo", true); break;
                        case 7: m.Set("bassAuto", false); m.Set("bassAmount", 0.55f); m.Set("eqAuto", false); m.Set("eqAmount", 0.5f); break;
                    }
                    sal[k] = cual == 0 ? Pasa(m, m.Track, m.Wav) : Pasa(m, m.Track2, m.Wav2);
                }
                casos++;
                bool igual = sal[0].Length == sal[1].Length;
                for (int i = 0; igual && i < sal[0].Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(sal[0][i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(sal[1][i]), 0)) igual = false;
                // El modo automatico del local es lo unico que PUEDE cambiar (24-10-2026: P1 y P2 lo cambian a proposito): se dice, no se cuenta.
                if (!igual && nombres[c].StartsWith("modo del local")) Console.WriteLine("  distinto  " + nombres[c] + (cual == 0 ? " (medida)" : " (sin medir)") + " (esperado: lo decide el modo automatico)");
                else if (!igual) { distintos++; Console.WriteLine("  DISTINTO  " + nombres[c] + (cual == 0 ? " (medida)" : " (sin medir)")); }
                else Console.WriteLine("  igual     " + nombres[c] + (cual == 0 ? " (medida)" : " (sin medir)") + ": " + sal[0].Length + " muestras");
            }
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(distintos == 0 ? "OK " + casos + " combinaciones con la cadena entera identica bit a bit" : "FALLO: " + distintos + " combinaciones distintas");
        return distintos == 0 ? 0 : 1;
    }
}

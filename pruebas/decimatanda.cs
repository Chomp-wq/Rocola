using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// DECIMA TANDA (12-10-2026): fallos y mejoras de audio.
//  1) FUNDIDO DE ENTRADA. La cancion entraba de golpe: si su primera muestra no es cero (un tema cortado de un disco en
//     vivo, una retomada a media cancion) se oia un golpe seco. Ahora entra en una media onda de coseno de 5 ms, o de
//     0,3 s al retomar a media cancion; empieza donde empieza la musica (tras el cebado de la cadena) y la pista
//     siguiente del mismo disco que sigue sin corte no lo lleva (eso lo mira 'sinpausa').
// Son senales sinteticas: miden la cuenta, no el oido.
class DecimaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;

    // Una senal fija: todas las muestras valen 'v' (lo peor para un arranque de golpe).
    class Fija : ISampleProvider
    {
        readonly WaveFormat wf; readonly float v; long quedan;
        public Fija(int fs, int ch, float v, long cuadros) { wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); this.v = v; quedan = cuadros * ch; }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int n) { int k = (int)Math.Min(n, quedan); for (int i = 0; i < k; i++) b[o + i] = v; quedan -= k; return k; }
    }

    static float[] Lee(ISampleProvider p, int bloque)
    {
        System.Collections.Generic.List<float> r = new System.Collections.Generic.List<float>();
        float[] b = new float[bloque]; int n;
        while ((n = p.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) r.Add(b[i]);
        return r.ToArray();
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_decima_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try { Entrada(); EnLaCadena(tmp); }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK decimatanda: " + ok + " comprobaciones (senales sinteticas; no es oirlo en el bar)"
                                   : "  FALLO decimatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    static void Entrada()
    {
        Type fp = form.GetNestedType("FadeProvider", B);
        ConstructorInfo ctor = fp.GetConstructor(B, null, new Type[] { typeof(ISampleProvider), typeof(long), typeof(double), typeof(long), typeof(double) }, null);
        Check(ctor != null, "el fundido sabe donde empieza la musica y cuanto dura su entrada");
        if (ctor == null) return;
        double seg = (double)fp.GetField("EntradaSeg", B).GetValue(null), ret = (double)fp.GetField("EntradaRetomadaSeg", B).GetValue(null);
        Check(seg > 0.002 && seg <= 0.01 && ret >= 0.1 && ret <= 0.5, "la entrada dura unos milisegundos (" + seg * 1000 + " ms), y al retomar algo mas (" + ret + " s)");
        foreach (int ch in new int[] { 1, 2 })
        foreach (int bloque in new int[] { 7, 64, 4096 })
        {
            int fs = 44100, cebado = 37;
            object f = ctor.Invoke(new object[] { new Fija(fs, ch, 0.8f, fs), 0L, 0.5, (long)cebado, seg });
            float[] y = Lee((ISampleProvider)f, bloque * ch);
            int largo = (int)(seg * fs);
            bool cebadoIntacto = true, sube = true, llega = true; float salto = 0, antes = 0;
            for (int i = 0; i < y.Length / ch; i++)
            {
                float v = y[i * ch];
                for (int c = 1; c < ch; c++) if (y[i * ch + c] != v) sube = false;   // los canales, igual
                if (i < cebado) { if (v != 0.8f) cebadoIntacto = false; continue; }
                if (i == cebado) antes = 0f;
                salto = Math.Max(salto, Math.Abs(v - antes)); antes = v;
                if (i < cebado + largo && i > cebado && v < y[(i - 1) * ch]) sube = false;
                if (i >= cebado + largo + 1 && v != 0.8f) llega = false;
            }
            Check(cebadoIntacto, ch + " canales, bloques de " + bloque + ": el cebado de la cadena no se toca (el fundido empieza donde empieza la musica)");
            Check(sube && salto < 0.8f * 0.02f, ch + " canales, bloques de " + bloque + ": entra subiendo sin escalon (salto mayor " + salto.ToString("0.0000") + " de 0,8)");
            Check(llega, ch + " canales, bloques de " + bloque + ": pasada la entrada, la cancion sale tal cual, bit a bit");
        }
        // Sin entrada (la siguiente del mismo disco, que sigue sin corte): tal cual desde la primera muestra.
        object g = ctor.Invoke(new object[] { new Fija(44100, 2, 0.8f, 4410), 0L, 0.5, 10L, seg });
        fp.GetMethod("SinEntrada", B).Invoke(g, null);
        float[] z = Lee((ISampleProvider)g, 512);
        bool igual = z.Length == 8820; foreach (float v in z) if (v != 0.8f) igual = false;
        Check(igual, "con SinEntrada sale tal cual desde la primera muestra (un disco continuo sigue continuo)");
        // El fundido de salida sigue como estaba.
        int tot = 4 * 44100;   // (en una de menos de 2 s no hay fundido de salida: asi esta hecho)
        object h = ctor.Invoke(new object[] { new Fija(44100, 1, 0.5f, tot), (long)tot, 0.5, 0L, seg });
        float[] w = Lee((ISampleProvider)h, 1000);
        Check(w.Length == tot && Math.Abs(w[tot - 1]) < 0.001f && w[tot - 22050 - 10] == 0.5f && w[tot - 8000] < 0.5f * 0.5f, "y el fundido de salida, igual que antes");
    }

    // En la cadena de verdad: una cancion que empieza a todo volumen ya no arranca de golpe; retomada, entra en 0,3 s.
    static void EnLaCadena(string dir)
    {
        object f = Codigo.FormSinEncender(form);
        foreach (FieldInfo fi in form.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        form.GetField("crossfadeSec", B).SetValue(f, 0.5f);
        // Todo lo demas apagado y neutro, como 'todojunto' con todo apagado (el formulario aislado no pone los de fabrica).
        foreach (string[] kv in new string[][] { new[] { "normalizeOn", "b0" }, new[] { "masterDb", "f0" }, new[] { "excAmount", "f0" }, new[] { "bassAuto", "b0" },
                 new[] { "bassAmount", "f0" }, new[] { "bassSongFactor", "f1" }, new[] { "sbrAllowed", "b0" }, new[] { "sbrOn", "b0" }, new[] { "declipOn", "b0" },
                 new[] { "nocheArrancada", "b1" }, new[] { "nocheFactor", "f1" }, new[] { "nocheCompensa", "f1" } })
        {
            FieldInfo fi = form.GetField(kv[0], B);
            if (fi == null) continue;
            fi.SetValue(f, kv[1][0] == 'b' ? (object)(kv[1][1] == '1') : (object)float.Parse(kv[1].Substring(1)));
        }
        form.GetField("saltaSilencios", B).SetValue(f, false);
        int fs = 44100; float[] x = new float[fs * 6 * 2];
        for (int i = 0; i < fs * 6; i++) { float v = (float)(0.6 * Math.Cos(2 * Math.PI * 80 * i / fs)); x[2 * i] = v; x[2 * i + 1] = v; }   // empieza en su pico
        string wav = Path.Combine(dir, "golpe.wav");
        using (WaveFileWriter wr = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) wr.WriteSamples(x, 0, x.Length);
        Type tt = form.GetNestedType("Track", B); object track = Activator.CreateInstance(tt, true); tt.GetField("Path", B).SetValue(track, wav);
        foreach (double desde in new double[] { 0.0, 2.0 })
        {
            object lector = form.GetMethod("OpenAudio", B).Invoke(f, new object[] { wav });
            form.GetNestedType("IAudioSource", B).GetProperty("Volume").SetValue(lector, 1.0f, null);
            if (desde > 0) form.GetNestedType("IAudioSource", B).GetMethod("Salta").Invoke(lector, new object[] { TimeSpan.FromSeconds(desde) });
            object cad = form.GetMethod("ArmaCadena", B).Invoke(f, new object[] { track, lector, false, desde });
            int retardo = (int)cad.GetType().GetField("Retardo", B).GetValue(cad);
            float[] y = Lee((ISampleProvider)cad.GetType().GetField("Salida", B).GetValue(cad), 4096);
            ((IDisposable)lector).Dispose();
            float primero = 0, salto = 0, prev = 0;
            for (int i = 0; i < Math.Min(y.Length / 2, retardo + fs / 2); i++)
            {
                float v = y[2 * i];
                if (i == retardo) primero = Math.Abs(v);
                if (i >= retardo && i < retardo + fs / 100) salto = Math.Max(salto, Math.Abs(v - prev));
                prev = v;
            }
            float alMedio = 0; for (int i = retardo + fs; i < retardo + fs + 1000 && 2 * i < y.Length; i++) alMedio = Math.Max(alMedio, Math.Abs(y[2 * i]));
            if (desde == 0)
                Check(primero < 0.02f && salto < 0.08f && alMedio > 0.3f, "una cancion que empieza en su pico ya no arranca de golpe: primera muestra " + primero.ToString("0.000")
                      + ", salto mayor en sus primeros 10 ms " + salto.ToString("0.000") + " (sin la entrada seria ~0,6), y despues suena entera");
            else
            {
                float a100 = 0; for (int i = retardo + fs / 10 - 50; i < retardo + fs / 10 + 50; i++) a100 = Math.Max(a100, Math.Abs(y[2 * i]));
                Check(primero < 0.01f && a100 < alMedio * 0.5f && alMedio > 0.3f, "retomada a media cancion: entra en 0,3 s (a los 100 ms va por " + a100.ToString("0.00") + " de " + alMedio.ToString("0.00") + ")");
            }
        }
    }
}

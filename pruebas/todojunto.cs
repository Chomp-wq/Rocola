using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;
// TODO EL SONIDO A LA VEZ.
//
// Cada opcion de sonido tiene su bateria. Esta mira que no choquen entre si: la cadena de verdad de una cancion
// (OpenAudio + ArmaCadena) con TODO encendido a la vez -nivelado, "Subir TODO", realce, DSEE, grave sintetico, grave en
// mono, des-recorte, modo noche, fundido y saltar silencios- y ademas cambiandolo todo en vivo mientras suena, como
// desde Ctrl+F3 (ApplyNormNow). Se mira:
//   - que no salga nada roto (NaN, infinitos) ni nada por encima del techo del limitador;
//   - que los cambios en vivo no metan chasquidos (el salto mas grande entre dos muestras seguidas no pasa del de
//     todo encendido o todo apagado) ni pierdan o repitan muestras;
//   - que lo que queda puesto en la cadena sea siempre lo que dicen los ajustes;
//   - cuanto trabaja el limitador con todo encendido frente a solo el nivelado, y lo que cuesta.
// El EQ automatico no entra: necesita una huella aprendida de la biblioteca (lo prueba 'eqauto').
// Son senales sinteticas: miden la cuenta, no el oido.
class TodoJunto
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
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
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }
    static double Db(double x) { return 20 * Math.Log10(Math.Max(x, 1e-12)); }

    // LA CANCION: 2,5 s mudos, 22 s de musica (bombo, bajo solo por la izquierda, platillos, acordes, melodia y unos
    // golpes recortados a proposito para el des-recorte) y 3 s mudos.
    static float[] Cancion(int fs)
    {
        int n = (int)(fs * 27.5); float[] x = new float[2 * n]; Random r = new Random(21);
        int a = (int)(fs * 2.5), b = (int)(fs * 24.5); double hp = 0, ant = 0, fm = 0;
        double[] notas = { 523, 659, 784, 1047, 880, 698 };
        for (int i = a; i < b; i++)
        {
            double t = (double)(i - a) / fs;
            double tb = t % 0.5, kick = 0.5 * Math.Exp(-tb / 0.12) * Math.Sin(2 * Math.PI * (50 + 60 * Math.Exp(-tb / 0.03)) * tb);
            double bajo = 0.25 * Math.Sin(2 * Math.PI * 55 * t);
            double w = r.NextDouble() * 2 - 1; hp = 0.5 * (hp + w - ant); ant = w;
            double hat = 0.2 * Math.Exp(-(t % 0.25) / 0.03) * hp;
            double acorde = 0.05 * (Math.Sin(2 * Math.PI * 220 * t) + Math.Sin(2 * Math.PI * 277 * t) + Math.Sin(2 * Math.PI * 330 * t));
            fm += 2 * Math.PI * notas[(int)(t / 0.5) % notas.Length] / fs;
            double mel = 0.12 * Math.Sin(fm);
            double l = kick + bajo + hat + acorde + mel, rr = kick + hat * 0.8 + acorde + mel * 0.7;
            if (t % 4 < 0.05) { l *= 2.2; rr *= 2.2; }   // golpes muy fuertes...
            x[2 * i] = (float)Math.Max(-1, Math.Min(1, l)); x[2 * i + 1] = (float)Math.Max(-1, Math.Min(1, rr));   // ...recortados
        }
        return x;
    }

    static object f;         // el Form1 sin encender
    static string wav;
    static object track, item;

    static void Prepara(string dir)
    {
        f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        int fs = 44100; float[] x = Cancion(fs);
        wav = Path.Combine(dir, "todo.wav");
        using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
        object[] a = { wav, null };
        Check((bool)Call(f, "MideIntegrada", a), "la cancion de prueba se mide entera (algoritmo de ahora, con picos y donde suena)");
        IDictionary tab = (IDictionary)Get(f, "integCache");
        if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); Set(f, "integCache", tab); }
        tab[wav] = a[1];
        // Una biblioteca de 25 como esta: con 20 o mas canciones medidas, "Subir TODO" ya tiene su techo.
        for (int i = 0; i < 24; i++) tab[Path.Combine(dir, "otra" + i + ".wav")] = a[1];
        Type tt = TF.GetNestedType("Track", T); track = Activator.CreateInstance(tt, true); Set(track, "Path", wav);
        Type tp = TF.GetField("nowPlaying", T).FieldType; item = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp); Set(item, "Song", track);
    }

    // Los ajustes: todo encendido o todo apagado.
    static void Ajustes(bool todo)
    {
        Set(f, "normalizeOn", todo); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f);
        Set(f, "masterDb", todo ? 6f : 0f);
        Set(f, "excAmount", todo ? 0.35f : 0f);
        Set(f, "bassAuto", false); Set(f, "bassAmount", todo ? 0.3f : 0f); Set(f, "bassSongFactor", 1f);
        Set(f, "sbrAllowed", true); Set(f, "sbrOn", todo);
        Set(f, "declipOn", todo);
        Set(f, "nocheArrancada", true); Set(f, "nocheFactor", todo ? 0.63f : 1f); Set(f, "nocheCompensa", 1f);
        Set(f, "crossfadeSec", 0.5f);
        Set(f, "saltaSilencios", todo);
    }

    class Pasada { public float[] y; public double[] grDb; public double ms; public int n; }

    // Una pasada entera por la cadena de verdad. 'cambia' (si no es null) se llama cada 0,4 s de musica.
    static Pasada Suena(bool todo, Action<int> cambia) { return Suena(todo, cambia, false); }
    static Pasada Suena(bool todo, Action<int> cambia, bool soloNivelado)
    {
        Ajustes(todo);
        if (soloNivelado) { Set(f, "excAmount", 0f); Set(f, "bassAmount", 0f); Set(f, "sbrOn", false); Set(f, "nocheFactor", 1f); }
        object lector = Call(f, "OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        Set(f, "curTap", Get(cad, "Tap")); Set(f, "curBass", Get(cad, "Bajo")); Set(f, "curNoche", Get(cad, "Noche")); Set(f, "curSbr", Get(cad, "Sbr"));
        Set(f, "nowPlaying", item); Set(f, "cadenaPreparada", null);
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        object fun = Get(cad, "Fundido");
        object lim = fun != null ? Get(fun, "src") : sal;
        FieldInfo env = null; for (Type t = lim.GetType(); t != null && env == null; t = t.BaseType) env = t.GetField("env", T);
        List<float> y = new List<float>(); List<double> gr = new List<double>();
        float[] b = new float[2205 * 2]; int bloques = 0;
        Stopwatch sw = Stopwatch.StartNew();
        while (true)
        {
            if (cambia != null && bloques > 0 && bloques % 8 == 0) cambia(bloques / 8);
            int n = sal.Read(b, 0, b.Length);
            if (n <= 0) break;
            for (int i = 0; i < n; i++) y.Add(b[i]);
            if (env != null) gr.Add(-Db((float)env.GetValue(lim)));
            bloques++;
        }
        sw.Stop();
        ((IDisposable)lector).Dispose();
        Pasada p = new Pasada(); p.y = y.ToArray(); p.grDb = gr.ToArray(); p.ms = sw.Elapsed.TotalMilliseconds; p.n = y.Count / 2;
        return p;
    }
    static double SaltoMax(float[] y, int desde)
    {
        double m = 0;
        for (int c = 0; c < 2; c++) for (int i = 2 * (desde + 1) + c; i < y.Length; i += 2) m = Math.Max(m, Math.Abs(y[i] - y[i - 2]));
        return m;
    }
    static double Pico(float[] y) { double m = 0; foreach (float v in y) m = Math.Max(m, Math.Abs(v)); return m; }
    static bool Sano(float[] y) { foreach (float v in y) if (float.IsNaN(v) || float.IsInfinity(v)) return false; return true; }
    static double Pct(double[] g, double umbral) { int k = 0; foreach (double v in g) if (v > umbral) k++; return 100.0 * k / Math.Max(1, g.Length); }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_todojunto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Console.WriteLine("\n  1) LA CANCION\n");
            Prepara(dir);
            float techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);

            Console.WriteLine("\n  2) TODO APAGADO Y TODO ENCENDIDO\n");
            Pasada apagado = Suena(false, null), encendido = Suena(true, null);
            Info("todo apagado: " + F(apagado.n / 44100.0, "0.00") + " s; todo encendido: " + F(encendido.n / 44100.0, "0.00") + " s (sin los silencios largos)");
            Check(Sano(apagado.y) && Sano(encendido.y), "no sale nada roto (ni NaN ni infinitos), ni con todo apagado ni con todo encendido");
            Check(Pico(encendido.y) <= techo + 1e-6 && Pico(apagado.y) <= techo + 1e-6,
                  "nada pasa del techo del limitador (" + F(Db(Pico(encendido.y)), "0.00") + " dBFS con todo, techo " + F(Db(techo), "0.00") + ")");
            Check(encendido.n < apagado.n - 44100 * 4 && encendido.n > apagado.n - 44100 * 6, "con todo encendido se saltan los silencios largos (" + F((apagado.n - encendido.n) / 44100.0, "0.0") + " s menos de 5,5 mudos; se dejan 0,3 y 0,5)");
            double rmsE = 0; for (int i = 0; i < encendido.y.Length; i++) rmsE += encendido.y[i] * encendido.y[i];
            rmsE = Math.Sqrt(rmsE / encendido.y.Length);
            Check(rmsE > 0.05, "y suena (" + F(Db(rmsE), "0.0") + " dBFS de media)");

            Console.WriteLine("\n  3) CAMBIANDOLO TODO MIENTRAS SUENA (como desde Ctrl+F3)\n");
            string[] quien = { "realce", "grave sintetico", "DSEE", "modo noche", "nivelado", "Subir TODO" };
            bool coherente = true; string falla = "";
            Action<int> cambia = delegate(int k)
            {
                int q = (k - 1) % quien.Length; bool enc = ((k - 1) / quien.Length) % 2 == 1;
                switch (q)
                {
                    case 0: Set(f, "excAmount", enc ? 0.35f : 0f); break;
                    case 1: Set(f, "bassAmount", enc ? 0.3f : 0f); break;
                    case 2: Set(f, "sbrOn", enc); break;
                    case 3: Set(f, "nocheFactor", enc ? 0.63f : 1f); break;
                    case 4: Set(f, "normalizeOn", enc); break;
                    case 5: Set(f, "masterDb", enc ? 6f : 0f); break;
                }
                Call(f, "ApplyNormNow");
                // Lo que queda puesto en la cadena es lo que dicen los ajustes.
                object tap = Get(f, "curTap");
                float g = (float)Get(tap, "Gain"), m = (float)Get(tap, "Master");
                float gE = (float)Call(f, "NormGainFor", wav), mE = (float)Call(f, "MasterEfectivo") * (float)Call(f, "FactorNoche");
                float bajo = (float)Get(Get(f, "curBass"), "Amount"), bajoE = (float)Call(f, "BassAmountNow");
                float nDb = (float)Get(Get(f, "curNoche"), "Db"), nDbE = (float)Call(f, "NocheCompensaDb");
                bool dsee = (bool)Get(Get(f, "curSbr"), "Active");
                bool dseeE = TF.GetMethod("DseeActivo", T) != null ? (bool)Call(f, "DseeActivo") : (bool)Get(f, "sbrOn");   // Modest: manda el interruptor
                if (g != gE || m != mE || bajo != bajoE || nDb != nDbE || dsee != dseeE) { coherente = false; if (falla.Length == 0) falla = quien[q]; }
            };
            Pasada vivo = Suena(true, cambia);
            Check(Sano(vivo.y) && Pico(vivo.y) <= techo + 1e-6, "cambiando una opcion cada 0,4 s: nada roto y nada por encima del techo");
            Check(coherente, "tras cada cambio, la cadena tiene puesto exactamente lo que dicen los ajustes (ganancia, Subir TODO con su techo y la noche, grave, noche, DSEE)" + (coherente ? "" : ": no con " + falla));
            Check(vivo.n == encendido.n, "no se pierde ni se repite ninguna muestra (" + vivo.n + " cuadros, igual que sin tocar nada)");
            int desde = 44100;   // el primer segundo es la entrada de la cancion (el fundido de entrada)
            double sV = SaltoMax(vivo.y, desde), sE = SaltoMax(encendido.y, desde), sA = SaltoMax(apagado.y, desde);
            Info("el salto mas grande entre dos muestras seguidas: " + F(sV, "0.000") + " cambiando en vivo; " + F(sE, "0.000") + " todo encendido; " + F(sA, "0.000") + " todo apagado");
            Check(sV <= Math.Max(sE, sA) * 1.15, "los cambios en vivo no meten chasquidos (el salto mas grande no pasa del de todo encendido o todo apagado)");

            Console.WriteLine("\n  4) CUANTO TRABAJA EL LIMITADOR Y LO QUE CUESTA\n");
            Pasada solo = Suena(true, null, true);
            Info("solo nivelado y Subir TODO: el limitador quita mas de 3 dB el " + F(Pct(solo.grDb, 3), "0.0") + " % del tiempo; mas de 6, el " + F(Pct(solo.grDb, 6), "0.0") + " %");
            Info("con todo encendido: mas de 3 dB el " + F(Pct(encendido.grDb, 3), "0.0") + " %; mas de 6, el " + F(Pct(encendido.grDb, 6), "0.0") + " %");
            Check(Pct(solo.grDb, 3) <= 12, "con el techo de Subir TODO el limitador queda en su tope: pasa de 3 dB solo en los golpes (" + F(Pct(solo.grDb, 3), "0.0") + " % del tiempo)");
            Check(Pct(encendido.grDb, 6) <= Pct(solo.grDb, 6) + 5, "con realce, DSEE, grave y noche encima, el limitador no pasa de 6 dB mucho mas que con el nivelado solo");
            double seg = encendido.n / 44100.0;
            Info("coste con todo encendido: " + F(encendido.ms / seg, "0.0") + " ms por segundo de musica (" + F(encendido.ms / seg / 10, "0.0") + " % de un nucleo de esta maquina); todo apagado " + F(apagado.ms / (apagado.n / 44100.0), "0.0") + " ms");
            Check(encendido.ms / seg < 250, "todo encendido cabe de sobra en tiempo real (menos de un cuarto de nucleo en esta maquina)");
        }
        catch (Exception e) { Check(false, "todo junto: " + e); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

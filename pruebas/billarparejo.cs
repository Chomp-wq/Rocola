using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL MODO BILLAR NO MUEVE EL VOLUMEN SOLO (18-11-2026; el dueno, tras oir «Roundabout»: «el volumen da demasiados brincos, el volumen
// deberia ser constante»). La cadena de verdad (OpenAudio + ArmaCadena) con el modo billar encendido y los interruptores de fabrica lee un
// tono que pasa de -30 dBFS a -6 dBFS y vuelve a -30. Se comprueba:
//   1) ninguna de las cinco etapas (presencia, partes suaves, retumbe, ataque, asperos) esta armada o encendida en la cadena;
//   2) la ganancia de la cadena (salida / entrada, en dB) es la misma en lo suave y en lo fuerte, a 0,5 dB, descontando lo que bajo el
//      limitador (que es la unica etapa que puede mover el volumen);
//   3) el control: con la presencia puesta A MANO (interruptor del dueno) y el mismo tono, la ganancia SI cambia (la prueba mira algo);
//   4) con el modo billar puesto, si el dueno enciende una de las cinco, la etapa se arma y se oye (siguen siendo suyas).
// Senales sinteticas: miden la cuenta, no el oido.
class BillarParejo
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF; static object F; static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void SetS(string n, object v) { FieldInfo f = TF.GetField(n, T); if (f == null) throw new MissingFieldException(n); f.SetValue(f.IsStatic ? null : F, v); }
    static object Call(string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == n && m.GetParameters().Length == a.Length)
        { try { return m.Invoke(F, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
        throw new MissingMethodException(n);
    }
    static double Db(double x) { return 20 * Math.Log10(Math.Max(x, 1e-12)); }

    const int Fs = 44100, SegS = 6;
    static float Amp(double db) { return (float)Math.Pow(10, db / 20.0); }
    static double[] niveles = { -30, -6, -30 };

    static void Fabrica(bool billar, bool presenciaMano)
    {
        SetS("normalizeOn", true); SetS("normTarget", 0.26f); SetS("normStrength", 1f); SetS("masterDb", 0f);
        SetS("excAmount", 0.35f); SetS("bassAuto", true); SetS("bassAmount", 0.3f); SetS("bassSongFactor", 1f);
        SetS("eqAuto", true); SetS("eqAmount", 0f);
        SetS("sbrAllowed", true); SetS("sbrOn", false); SetS("declipOn", true);
        if (TF.GetField("nocheArrancada", T) != null) { SetS("nocheArrancada", true); SetS("nocheFactor", 1f); }
        SetS("crossfadeSec", 0.5f); SetS("saltaSilencios", false);
        SetS("claridadLocal", false); SetS("partesSuaves", false); SetS("presenciaSala", presenciaMano);
        SetS("gravesLocalDb", 0); SetS("agudosLocalDb", 0); SetS("balanceDb", 0);
        SetS("cortaSubgrave", false); SetS("estereoEstrecho", false); SetS("estereoAuto", false);
        SetS("canalMudo", true); SetS("quitaContinua", true);
        SetS("retumbeDinamico", false); SetS("ataqueClaro", false); SetS("asperezaDinamica", false); SetS("bomboSinHundir", false); SetS("suavizaEses", false);
        SetS("sinMejoras", false); SetS("aLaMedida", false); SetS("quitaSiseo", false);
        SetS("localAuto", billar);
    }

    // Pasa el tono por la cadena de verdad; devuelve la ganancia de cada tramo (dB, salida/entrada, en su parte estable) y lo que bajo el limitador.
    static double[] Pasa(string wav, object track, out object cadena, out double[] bajada)
    {
        object lector = Call("OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call("ArmaCadena", track, lector, false, 0.0);
        cadena = cad;
        int retardo = (int)Get(cad, "Retardo");
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        object lim = Get(cad, "Limitador");
        MethodInfo toma = lim.GetType().GetMethod("TomaBajadaDb", T);
        List<float> y = new List<float>(); float[] b = new float[4410 * 2];
        bajada = new double[3];
        int total = Fs * SegS * 3 * 2; int hecho = 0;
        while (true)
        {
            int n = sal.Read(b, 0, b.Length); if (n <= 0) break;
            for (int i = 0; i < n; i++) y.Add(b[i]);
            int pos = Math.Max(0, y.Count / 2 - retardo);
            int seg = Math.Min(2, pos / (Fs * SegS)), dentro = pos - seg * Fs * SegS;
            object d = toma.Invoke(lim, null);
            // Solo lo que bajo DENTRO de la ventana donde se mide la ganancia (de 2 a 5 s de cada tramo): sin la claridad el tono queda 1,6 dB mas alto y el
            // limitador aprieta 0,7 dB en el tramo fuerte; su suelta se alarga hasta el tramo siguiente y no es la ganancia de ese tramo.
            if (dentro >= 2 * Fs && dentro < 5 * Fs)
            { if (d is float) bajada[seg] = Math.Max(bajada[seg], Math.Abs((float)d)); else if (d is double) bajada[seg] = Math.Max(bajada[seg], Math.Abs((double)d)); }
            hecho += n;
        }
        ((IDisposable)lector).Dispose();
        double[] g = new double[3];
        for (int s = 0; s < 3; s++)
        {
            int ini = retardo + (s * SegS * Fs + (int)(2.0 * Fs)) , fin = retardo + (s * SegS * Fs + (int)(5.0 * Fs));
            double e = 0; int cuenta = 0;
            for (int i = ini; i < fin && 2 * i + 1 < y.Count; i++) { e += y[2 * i] * y[2 * i] + y[2 * i + 1] * y[2 * i + 1]; cuenta += 2; }
            double rmsOut = Math.Sqrt(e / Math.Max(1, cuenta));
            double rmsIn = Amp(niveles[s]) / Math.Sqrt(2.0);
            g[s] = Db(rmsOut) - Db(rmsIn);
        }
        if (hecho < total / 2) throw new Exception("la cadena dio de menos");
        return g;
    }

    static bool Enc(object cad, string etapa) { object e = Get(cad, etapa); return e != null && (bool)Get(e, "Encendido"); }

    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_billarparejo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Assembly asm = Assembly.Load(File.ReadAllBytes(args.Length > 0 ? args[0] : "rocola.exe"));
            TF = asm.GetType("Rocola.Form1", true);
            F = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(T))
                if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(F) == null) fi.SetValue(F, Activator.CreateInstance(fi.FieldType, true));
            string wav = Path.Combine(dir, "tono.wav");
            using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(Fs, 2)))
            {
                float[] x = new float[2 * Fs * SegS * 3];
                for (int i = 0; i < Fs * SegS * 3; i++)
                {
                    int s = i / (Fs * SegS);
                    float v = (float)(Amp(niveles[s]) * Math.Sin(2 * Math.PI * 440.0 * i / Fs));
                    x[2 * i] = v; x[2 * i + 1] = v;
                }
                w.WriteSamples(x, 0, x.Length);
            }
            Type tt = TF.GetNestedType("Track", T);
            object track = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(track, wav);
            object[] a = { wav, null };
            Check((bool)Call("MideIntegrada", a), "el tono se midio (la rocola lo nivela con su medida, como una cancion real)");
            IDictionary tab = (IDictionary)Get(F, "integCache");
            if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); TF.GetField("integCache", T).SetValue(F, tab); }
            tab[wav] = a[1];
            object fuente = TF.GetMethod("TomaFuenteMedida", T).Invoke(null, null);
            Call("ActualizaFuenteMedida", wav, fuente);

            Console.WriteLine("\n  1) EL MODO BILLAR CON LOS INTERRUPTORES DE FABRICA: NINGUNA DE LAS CINCO ETAPAS\n");
            Fabrica(true, false);
            object cad; double[] baj; double[] g = Pasa(wav, track, out cad, out baj);
            Console.WriteLine("        ganancia -30 / -6 / -30 dBFS: " + g[0].ToString("0.00") + " / " + g[1].ToString("0.00") + " / " + g[2].ToString("0.00") + " dB; el limitador bajo " + baj[0].ToString("0.00") + " / " + baj[1].ToString("0.00") + " / " + baj[2].ToString("0.00") + " dB");
            Check(!Enc(cad, "Presencia") && !Enc(cad, "Retumbe") && !Enc(cad, "Ataque") && !Enc(cad, "Aspereza"), "en la cadena armada, presencia, retumbe, ataque y asperos NO estan encendidos");
            object auto = TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null).Invoke(null, new object[] { 20f, 18f, 10f, float.NaN });
            Check(!(bool)auto.GetType().GetField("Suaves").GetValue(auto) && !(bool)auto.GetType().GetField("Presencia").GetValue(auto), "y las partes suaves y la presencia tampoco las pone el modo (ni con 18 LU)");
            // lo que sube o baja sin el limitador: la ganancia mas lo que el limitador quito en ese tramo
            double a0 = g[0] + baj[0], a1 = g[1] + baj[1], a2 = g[2] + baj[2];
            Check(Math.Abs(a0 - a1) <= 0.5 && Math.Abs(a2 - a1) <= 0.5 && Math.Abs(a0 - a2) <= 0.5,
                  "la ganancia antes del limitador es la misma en lo suave (-30 dBFS) y en lo fuerte (-6 dBFS), a 0,5 dB: " + a0.ToString("0.00") + " / " + a1.ToString("0.00") + " / " + a2.ToString("0.00"));

            Console.WriteLine("\n  2) EL CONTROL: CON LA PRESENCIA PUESTA A MANO, LA GANANCIA SI SE MUEVE\n");
            Fabrica(true, true);
            object cadP; double[] bajP; double[] gp = Pasa(wav, track, out cadP, out bajP);
            Console.WriteLine("        ganancia -30 / -6 / -30 dBFS: " + gp[0].ToString("0.00") + " / " + gp[1].ToString("0.00") + " / " + gp[2].ToString("0.00") + " dB; el limitador bajo " + bajP[0].ToString("0.00") + " / " + bajP[1].ToString("0.00") + " / " + bajP[2].ToString("0.00") + " dB");
            Check(Enc(cadP, "Presencia"), "con el interruptor de la presencia encendido y el modo billar puesto, la etapa se arma (sigue siendo del dueno)");
            Check(Math.Abs(gp[0] - g[0]) > 0.5, "y entonces lo suave sube " + (gp[0] - g[0]).ToString("0.00") + " dB frente a lo de fabrica del modo billar (la compensacion de la presencia): la prueba mira algo");

            Console.WriteLine("\n  3) CON EL MODO BILLAR, LAS DEMAS A MANO TAMBIEN SE ARMAN\n");
            Fabrica(true, false); SetS("retumbeDinamico", true); SetS("ataqueClaro", true); SetS("asperezaDinamica", true);
            object cadR; double[] bajR; Pasa(wav, track, out cadR, out bajR);
            Check(Enc(cadR, "Retumbe") && Enc(cadR, "Ataque") && Enc(cadR, "Aspereza"), "retumbe, ataque y asperos encendidos a mano se arman con el modo billar puesto");
            Fabrica(false, false);
            object cadA; double[] bajA; Pasa(wav, track, out cadA, out bajA);
            Check(!Enc(cadA, "Presencia") && !Enc(cadA, "Retumbe") && !Enc(cadA, "Ataque") && !Enc(cadA, "Aspereza"), "con el modo billar apagado y de fabrica, tampoco (como siempre)");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

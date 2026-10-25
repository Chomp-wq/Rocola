using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL RANGO DE SONORIDAD (LRA, EBU Tech 3342) EN LA MEDIDA DEL REPASO (Rocola/Sonoridad.cs, 23-10-2026; MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md, P2).
// El medidor de sonoridad (MedidorBs1770) saca ahora, ademas de la integrada y los picos, cuanto varia el volumen de la cancion en el tiempo:
// ventanas de 3 s cada 100 ms con ponderacion K, puerta absoluta de -70 LUFS, relativa de -20 LU, percentiles 10 y 95. Se comprueba:
//  1) Los casos de la norma EBU Tech 3342 (tonos de 1 kHz escalonados), con su tolerancia de +-1 LU.
//  2) Que da lo mismo se lea como se lea (de una muestra, de tamanos raros) y que reiniciar el medidor lo deja a cero.
//  3) Contra una referencia INDEPENDIENTE en Python (pruebas/ref_lra.py): estereo, mono, 5.0, varias frecuencias.
//  4) El formato (algoritmo 4): ida y vuelta, lo viejo se lee igual, uno posterior se conserva, lo roto se salta.
//  5) Que las medidas de antes siguen sirviendo para nivelar (sus numeros son los mismos) y que el repaso las vuelve a medir.
//  6) La medida de verdad de un archivo (MideIntegrada): trae el LRA; una cancion de menos de 3 s no.
//  7) Lo que cuesta, y compatibilidad: si ROCOLA_EXE_ANTERIOR apunta a un ejecutable de antes, ese lee el archivo nuevo sin perder nada
//     (los registros del algoritmo 4 los guarda intactos como ajenos) y se mide su coste para comparar.
// Son senales sinteticas en Mono/Linux: no es el A4 ni escucharlo.
class LraPrueba
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static Assembly asm; static Type TF, TM, TS;

    // ---------------------------------------------------------------- el medidor por reflexion
    static object Medidor(Assembly a, int fs, double[] pesos) { return Activator.CreateInstance(a.GetType("Rocola.MedidorBs1770", true), T, null, new object[] { fs, pesos }, null); }
    static void Procesa(object m, float[] x, int bloque)
    {
        MethodInfo p = m.GetType().GetMethod("Procesa", T);
        for (int i = 0; i < x.Length; i += bloque) p.Invoke(m, new object[] { x, i, Math.Min(bloque, x.Length - i) });
    }
    static bool Lra(object m, out double lra) { object[] a = { 0.0 }; bool r = (bool)m.GetType().GetMethod("Lra", T).Invoke(m, a); lra = (double)a[0]; return r; }
    static double Integrada(object m) { object[] a = { 0.0, 0 }; m.GetType().GetMethod("Integrada", T).Invoke(m, a); return (double)a[0]; }
    static double LraDe(int fs, double[] pesos, float[] x, int bloque) { object m = Medidor(asm, fs, pesos); Procesa(m, x, bloque); double l; return Lra(m, out l) ? l : double.NaN; }

    // ---------------------------------------------------------------- senales
    static double calibracion = double.NaN;   // LUFS de un seno de 1 kHz de amplitud 0,1 en dos canales iguales
    static void Calibra()
    {
        int fs = 48000; float[] x = new float[fs * 5 * 2];
        for (int i = 0; i < fs * 5; i++) x[2 * i] = x[2 * i + 1] = (float)(0.1 * Math.Sin(2 * Math.PI * 1000 * i / fs));
        object m = Medidor(asm, fs, new double[] { 1, 1 }); Procesa(m, x, 4096); calibracion = Integrada(m);
    }
    // Un seno de 1 kHz, estereo igual, a 'lufs' (sonoridad integrada de ese tramo) durante 'seg'.
    static void Tono(List<float> s, int fs, double lufs, double seg, ref long n0)
    {
        double a = 0.1 * Math.Pow(10, (lufs - calibracion) / 20);
        long n = (long)(seg * fs);
        for (long i = 0; i < n; i++, n0++) { float v = (float)(a * Math.Sin(2 * Math.PI * 1000 * n0 / fs)); s.Add(v); s.Add(v); }
    }
    static float[] Escalones(double[] niveles, double seg)
    {
        List<float> s = new List<float>(); long n0 = 0; foreach (double l in niveles) Tono(s, 48000, l, seg, ref n0); return s.ToArray();
    }
    // Ruido con tramos de nivel distinto (dBFS por canal; -999 = silencio) para comparar con Python.
    static float[] Programa(int fs, int ch, double[][] tramos, uint semilla)
    {
        List<float> s = new List<float>(); uint r = semilla; double[] lp = new double[ch];
        foreach (double[] t in tramos)
        {
            double a = t[0] <= -900 ? 0 : Math.Pow(10, t[0] / 20); long n = (long)(t[1] * fs);
            for (long i = 0; i < n; i++)
                for (int c = 0; c < ch; c++)
                {
                    r = r * 1664525u + 1013904223u; double w = ((r >> 8) / 8388608.0) - 1.0;
                    lp[c] += (w - lp[c]) * (0.15 + 0.1 * c);   // un poco de color distinto por canal
                    s.Add((float)(a * (0.7 * lp[c] / 0.35 * 0.5 + 0.3 * w) * 0.6));
                }
        }
        return s.ToArray();
    }
    static void EscribeWav(string ruta, int fs, int ch, float[] x) { using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, ch))) w.WriteSamples(x, 0, x.Length); }
    static string Python(string args, out int codigo)
    {
        codigo = -1;
        string py = Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON"); if (string.IsNullOrEmpty(py)) py = "python3";
        try
        {
            ProcessStartInfo si = new ProcessStartInfo(py, args);
            si.UseShellExecute = false; si.RedirectStandardOutput = true; si.RedirectStandardError = true; si.CreateNoWindow = true;
            using (Process p = Process.Start(si)) { string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); codigo = p.ExitCode; return o; }
        }
        catch { return null; }
    }

    // ---------------------------------------------------------------- 1) LOS CASOS DE LA NORMA
    static void CasosNorma()
    {
        Console.WriteLine("\n  1) LOS CASOS DE EBU TECH 3342 (senos de 1 kHz, 20 s por escalon, tolerancia de +-1 LU)\n");
        object[][] casos = {
            new object[] { "-20 / -30 LUFS", new double[] { -20, -30 }, 10.0 },
            new object[] { "-20 / -15 LUFS", new double[] { -20, -15 }, 5.0 },
            new object[] { "-40 / -20 / -40 LUFS", new double[] { -40, -20, -40 }, 20.0 },
            new object[] { "-50 / -35 / -20 / -35 / -50 LUFS (los -50 quedan bajo la puerta relativa)", new double[] { -50, -35, -20, -35, -50 }, 15.0 },
        };
        int k = 1;
        foreach (object[] c in casos)
        {
            double l = LraDe(48000, new double[] { 1, 1 }, Escalones((double[])c[1], 20), 4096 * 2);
            Check(Math.Abs(l - (double)c[2]) <= 1.0, "caso " + (k++) + ", " + (string)c[0] + ": LRA " + F(l, "0.00") + " LU (la norma pide " + F((double)c[2], "0") + " +-1)");
        }
        double parejo = LraDe(48000, new double[] { 1, 1 }, Escalones(new double[] { -23 }, 30), 8192);
        Check(parejo >= 0 && parejo < 0.3, "un tono parejo de 30 s: LRA casi 0 (" + F(parejo, "0.00") + ")");
        object m = Medidor(asm, 48000, new double[] { 1, 1 }); Procesa(m, Escalones(new double[] { -23 }, 2.9), 4096); double nada;
        Check(!Lra(m, out nada), "menos de 3 s: no hay LRA (la primera ventana de 3 s no se llena)");
        m = Medidor(asm, 48000, new double[] { 1, 1 }); Procesa(m, new float[48000 * 2 * 10], 4096);
        Check(!Lra(m, out nada), "10 s de silencio digital: tampoco (todo queda bajo la puerta de -70 LUFS)");
        m = Medidor(asm, 48000, new double[] { 1, 1 }); Procesa(m, Escalones(new double[] { -23 }, 3.2), 4096);
        Check(Lra(m, out nada) && nada < 0.3, "con un poco mas de 3 s ya hay (y un tono parejo da " + F(nada, "0.00") + ")");
    }

    // ---------------------------------------------------------------- 2) LEER COMO SE LEA
    static void Lecturas()
    {
        Console.WriteLine("\n  2) DA LO MISMO COMO SE LEA, Y REINICIAR LO DEJA A CERO\n");
        float[] x = Programa(44100, 2, new double[][] { new double[] { -24, 7 }, new double[] { -12, 9 }, new double[] { -999, 3 }, new double[] { -30, 8 } }, 5);
        double a = LraDe(44100, new double[] { 1, 1 }, x, 4096 * 2), b = LraDe(44100, new double[] { 1, 1 }, x, 1), c = LraDe(44100, new double[] { 1, 1 }, x, 777), d = LraDe(44100, new double[] { 1, 1 }, x, x.Length);
        Check(a == b && a == c && a == d && !double.IsNaN(a), "de golpe, de a una muestra, de 777 en 777: el mismo LRA exacto (" + F(a, "0.0000") + ")");
        object m = Medidor(asm, 44100, new double[] { 1, 1 }); Procesa(m, x, 4096);
        double l1; Lra(m, out l1);
        bool re = (bool)m.GetType().GetMethod("Reinicia", T).Invoke(m, new object[] { 44100, new double[] { 1, 1 } });
        double vacio; bool hay = Lra(m, out vacio);
        Procesa(m, x, 4096); double l2; Lra(m, out l2);
        Check(re && !hay && l1 == l2, "reiniciar el medidor (se reutiliza entre canciones) borra el LRA anterior y la segunda cancion da lo mismo que la primera");
    }

    // ---------------------------------------------------------------- 3) CONTRA PYTHON
    static void ContraPython(string dir)
    {
        Console.WriteLine("\n  3) CONTRA UNA REFERENCIA INDEPENDIENTE (pruebas/ref_lra.py)\n");
        int cod; string o = Python("-c \"import numpy\"", out cod);
        if (o == null || cod != 0) { Info("AVISO (no cuenta como comprobacion): sin Python con numpy no se compara con la referencia independiente"); return; }
        double[][] p1 = { new double[] { -26, 9 }, new double[] { -14, 11 }, new double[] { -999, 4 }, new double[] { -34, 8 }, new double[] { -20, 6 } };
        double[][] p2 = { new double[] { -18, 12 }, new double[] { -30, 7 }, new double[] { -22, 10.3 } };
        double[][] p3 = { new double[] { -16, 20 }, new double[] { -17, 5 } };   // casi parejo
        object[][] casos = {
            new object[] { "estereo 48 kHz con tramos y un silencio", 48000, 2, p1, new double[] { 1, 1 }, 0.25 },
            new object[] { "estereo 44,1 kHz", 44100, 2, p2, new double[] { 1, 1 }, 0.3 },
            new object[] { "mono 44,1 kHz", 44100, 1, p2, new double[] { 1 }, 0.3 },
            new object[] { "5.0 a 32 kHz con envolventes a 1,41", 32000, 5, p1, new double[] { 1, 1, 1, 1.41, 1.41 }, 0.3 },
            new object[] { "estereo 96 kHz", 96000, 2, p2, new double[] { 1, 1 }, 0.3 },
            new object[] { "estereo casi parejo", 48000, 2, p3, new double[] { 1, 1 }, 0.3 },
        };
        uint semilla = 21;
        foreach (object[] c in casos)
        {
            int fs = (int)c[1], ch = (int)c[2]; double[] pe = (double[])c[4]; double tol = (double)c[5];
            float[] x = Programa(fs, ch, (double[][])c[3], semilla++);
            string ruta = Path.Combine(dir, "ref_lra.wav"); EscribeWav(ruta, fs, ch, x);
            string pesos = ""; for (int i = 0; i < pe.Length; i++) pesos += (i > 0 ? "," : "") + F(pe[i], "0.00");
            string r = Python("ref_lra.py \"" + ruta + "\" --pesos " + pesos, out cod);
            double py = double.NaN;
            if (r != null && r.StartsWith("LRA ") && !r.StartsWith("LRA none")) double.TryParse(r.Substring(4).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out py);
            double cs = LraDe(fs, pe, x, 4096 * ch + 1);
            Check(!double.IsNaN(py) && Math.Abs(cs - py) <= tol, (string)c[0] + ": rocola " + F(cs, "0.00") + ", referencia " + F(py, "0.00") + " (tolerancia " + F(tol, "0.00") + " LU: el histograma es de 0,1 LU)");
        }
    }

    // ---------------------------------------------------------------- 4) EL FORMATO
    static IDictionary TablaNueva() { return (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase); }
    static bool Lee(Type tf, string texto, IDictionary tabla, List<string> ajenas) { using (StringReader r = new StringReader(texto)) return (bool)tf.GetMethod("LeeIntegRegistros", T).Invoke(null, new object[] { r, tabla, ajenas }); }
    static object Reg(string n, Type ts, params object[] a) { return Activator.CreateInstance(ts, T, null, a, null); }
    static float Campo(object s, string n) { return Convert.ToSingle(s.GetType().GetField(n, T).GetValue(s)); }
    static void Formato()
    {
        Console.WriteLine("\n  4) EL FORMATO DE rocola_sonoridad.txt (algoritmo 4)\n");
        Type TSil = null; foreach (Type t in asm.GetTypes()) if (t.Name == "MapaSilencios") TSil = t;
        object mudo = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { 180f });
        Check((int)TM.GetField("Algoritmo", T).GetValue(null) >= 5, "el algoritmo de ahora es el 5 o posterior (el 4 trajo el LRA; el 5, el 24-10-2026, el P90 del nivelado al limite; el 6, el 25-10, donde empieza y acaba y el zumbido)");
        string cab = (string)TF.GetField("IntegCabecera", T).GetValue(null);
        StringWriter w = new StringWriter(); w.Write(cab + "\r\n");
        MethodInfo esc = TF.GetMethod("EscribeIntegRegistro", T);
        esc.Invoke(null, new object[] { w, "C:\\m\\viejo1.mp3", Reg("v1", TS, 1, -11.4819f, 2f, 2398) });
        esc.Invoke(null, new object[] { w, "C:\\m\\viejo2.mp3", Reg("v2", TS, 2, -12.5f, 2f, 2398, -0.4f, -2.1f) });
        esc.Invoke(null, new object[] { w, "C:\\m\\viejo3.mp3", Reg("v3", TS, 3, -13.5f, 2f, 2398, -0.4f, -2.1f, mudo) });
        esc.Invoke(null, new object[] { w, "C:\\m\\nuevo.mp3", Reg("n", TS, 4, -14.5f, 2f, 2398, -0.4f, -2.1f, mudo, 12.34f) });
        esc.Invoke(null, new object[] { w, "C:\\m\\nuevo_sin.mp3", Reg("n2", TS, 4, -15.5f, 2f, 300, -0.4f, -2.1f, mudo, float.NaN) });
        string texto = w.ToString();
        Check(texto.Contains("|12.3|C:\\m\\nuevo.mp3") && texto.Contains("|-|C:\\m\\nuevo_sin.mp3") && texto.Contains("4|-14.5|2|2398|"), "se escribe al final de los demas campos, antes de la ruta: LRA a la decima, o '-' si no hay");
        Check(texto.Contains("3|-13.5|2|2398|-0.4|-2.1|"), "los registros de los algoritmos 1, 2 y 3 se escriben como siempre (sin el campo nuevo)");
        IDictionary t1 = TablaNueva(); List<string> aj = new List<string>();
        bool ok = Lee(TF, texto, t1, aj);
        object n = t1["C:\\m\\nuevo.mp3"], n2 = t1["C:\\m\\nuevo_sin.mp3"], v3 = t1["C:\\m\\viejo3.mp3"];
        Check(ok && t1.Count == 5 && aj.Count == 0 && Math.Abs(Campo(n, "Lra") - 12.3f) < 0.001f && (bool)n.GetType().GetProperty("ConLra", T).GetValue(n, null)
              && float.IsNaN(Campo(n2, "Lra")) && !(bool)n2.GetType().GetProperty("ConLra", T).GetValue(n2, null) && float.IsNaN(Campo(v3, "Lra")) && Campo(v3, "Algoritmo") == 3,
              "ida y vuelta: las cinco se leen; el LRA vuelve a la decima, el '-' como 'no hay', y las de antes quedan sin LRA y con su algoritmo");
        Check(Math.Abs(Campo(t1["C:\\m\\viejo1.mp3"], "Lkfs") + 11.482f) < 0.001f && Math.Abs(Campo(t1["C:\\m\\viejo2.mp3"], "P99Db") + 2.1f) < 0.001f, "y lo viejo se lee exactamente igual que antes");
        // Registros rotos del algoritmo 4 se saltan; uno posterior se conserva intacto.
        string malos = "4|-10|2|100|-0.4|-2.1|-|-|-|-|180|-5|C:\\m\\a.mp3\r\n4|-10|2|100|-0.4|-2.1|-|-|-|-|180|NaN|C:\\m\\b.mp3\r\n4|-10|2|100|-0.4|-2.1|-|-|-|-|180|abc|C:\\m\\c.mp3\r\n4|-10|2|100|-0.4|-2.1|-|-|-|-|180|400|C:\\m\\d.mp3\r\n4|-10|2|100|-0.4|-2.1|-|-|-|-|C:\\m\\e.mp3\r\n";
        string futuro = "99|-9.5|2|100|-0.4|-2.1|-|-|-|-|180|7|-3|x|C:\\m\\f.mp3";
        t1 = TablaNueva(); aj = new List<string>();
        ok = Lee(TF, cab + "\r\n" + malos + futuro + "\r\n", t1, aj);
        Check(ok && t1.Count == 0 && aj.Count == 1 && aj[0] == futuro, "un LRA negativo, que no es numero, texto o absurdo, o un registro de 4 al que le falta el campo: se saltan; uno de un algoritmo posterior (99) se guarda intacto");
        // Una medida de 3 no pisa a una de 4 al leer en el orden contrario.
        t1 = TablaNueva(); aj = new List<string>();
        Lee(TF, cab + "\r\n4|-14.5|2|2398|-0.4|-2.1|-|-|-|-|180|12.3|C:\\m\\x.mp3\r\n3|-14.4|2|2398|-0.4|-2.1|-|-|-|-|180|C:\\m\\x.mp3\r\n", t1, aj);
        Check(Campo(t1["C:\\m\\x.mp3"], "Algoritmo") == 4 && Campo(t1["C:\\m\\x.mp3"], "Lra") > 12f, "una medida de antes, leida despues de una de 4 de la misma cancion, no la pisa");
    }

    // ---------------------------------------------------------------- 5) LAS DE ANTES SIGUEN SIRVIENDO, Y EL REPASO LAS VUELVE A MEDIR
    static void LasDeAntes(string dir)
    {
        Console.WriteLine("\n  5) LAS MEDIDAS DE ANTES SIGUEN SIRVIENDO PARA NIVELAR; EL REPASO LAS VUELVE A MEDIR\n");
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Type TSil = null; foreach (Type t in asm.GetTypes()) if (t.Name == "MapaSilencios") TSil = t;
        object mudo = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { 180f });
        IDictionary tab = TablaNueva(); TF.GetField("integCache", T).SetValue(f, tab);
        object a3 = Reg("a3", TS, 3, -22f, 2f, 1800, -3f, -6f, mudo), a4 = Reg("a4", TS, 4, -22f, 2f, 1800, -3f, -6f, mudo, 9.5f);
        tab["C:\\m\\tres.mp3"] = a3; tab["C:\\m\\cuatro.mp3"] = a4;
        MethodInfo para = TF.GetMethod("SonoridadParaNivelar", T), picos = TF.GetMethod("PicosParaNivelar", T);
        object[] r3 = { "C:\\m\\tres.mp3", 0f }, r4 = { "C:\\m\\cuatro.mp3", 0f };
        bool b3 = (bool)para.Invoke(f, r3), b4 = (bool)para.Invoke(f, r4);
        Check(b3 && b4 && (float)r3[1] == (float)r4[1] && (float)r3[1] > 0.0001f, "para nivelar, la de antes y la nueva con la misma sonoridad dan exactamente la misma raiz de potencia");
        object[] p3 = { "C:\\m\\tres.mp3", 0f, 0f }, p4 = { "C:\\m\\cuatro.mp3", 0f, 0f };
        Check((bool)picos.Invoke(f, p3) && (bool)picos.Invoke(f, p4) && (float)p3[1] == (float)p4[1] && (float)p3[2] == (float)p4[2], "y los mismos picos para no subirla de mas");
        // El repaso: lo que falta por medir son las de antes (algoritmo menor que el de ahora), no las que ya tienen LRA.
        Type tt = TF.GetNestedType("Track", T);
        IList pistas = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tt));
        foreach (string p in new string[] { "C:\\m\\tres.mp3", "C:\\m\\cuatro.mp3", "C:\\m\\nunca.mp3" }) { object t = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(t, p); pistas.Add(t); }
        TF.GetField("tracks", T).SetValue(f, pistas);
        FieldInfo la = TF.GetField("localAuto", T);
        la.SetValue(null, false);
        List<string> pp = new List<string>(); foreach (object s in (IList)TF.GetMethod("TandaIntegrada", T).Invoke(f, null)) pp.Add((string)s);
        // Desde el 24-10-2026 el nivelado al limite necesita el P90 (algoritmo 5) en TODAS: el repaso pide las de 3 y las de 4, con el modo
        // automatico del local encendido o apagado (hasta el 23-10, con el modo apagado no pedia las de 3). Mientras, nivelan con la regla de antes.
        Check(pp.Count == 3 && pp.Contains("C:\\m\\tres.mp3") && pp.Contains("C:\\m\\cuatro.mp3") && pp.Contains("C:\\m\\nunca.mp3"), "con el modo automatico del local APAGADO el repaso pide las de antes del algoritmo 5 (3 y 4) y la que nunca se midio");
        la.SetValue(null, true);
        pp = new List<string>(); foreach (object s in (IList)TF.GetMethod("TandaIntegrada", T).Invoke(f, null)) pp.Add((string)s);
        Check(pp.Count == 3, "y con el modo ENCENDIDO, las mismas");
        la.SetValue(null, false);
        // La medida de un archivo de verdad conserva lo que ya tenia la de antes y suma el LRA.
        string wav = Path.Combine(dir, "real.wav");
        float[] x = Programa(44100, 2, new double[][] { new double[] { -26, 9 }, new double[] { -14, 11 }, new double[] { -30, 6 } }, 99);
        EscribeWav(wav, 44100, 2, x);
        object[] med = { wav, null };
        bool ok = (bool)TF.GetMethod("MideIntegrada", T).Invoke(f, med);
        object s4 = med[1];
        int algHoy = (int)TM.GetField("Algoritmo", T).GetValue(null);
        Check(ok && Campo(s4, "Algoritmo") == algHoy && (bool)s4.GetType().GetProperty("ConLra", T).GetValue(s4, null) && (bool)s4.GetType().GetProperty("ConP90", T).GetValue(s4, null) && (bool)s4.GetType().GetProperty("ConPicos", T).GetValue(s4, null) && (bool)s4.GetType().GetProperty("ConSilencios", T).GetValue(s4, null),
              "MideIntegrada de un archivo da el algoritmo de hoy (" + algHoy + ") con LRA (" + F(Campo(s4, "Lra"), "0.0") + " LU), picos, P90 y silencios");
        float[] corto = new float[44100 * 2 * 2]; for (int i = 0; i < corto.Length; i++) corto[i] = (float)(0.2 * Math.Sin(i * 0.05));
        string wavc = Path.Combine(dir, "corto.wav"); EscribeWav(wavc, 44100, 2, corto);
        med = new object[] { wavc, null };
        ok = (bool)TF.GetMethod("MideIntegrada", T).Invoke(f, med);
        Check(ok && !(bool)med[1].GetType().GetProperty("ConLra", T).GetValue(med[1], null) && Campo(med[1], "Algoritmo") == algHoy, "una cancion de 2 s se mide (algoritmo de hoy) pero sin LRA: la regla de antes decide en el modo del local");
    }

    // ---------------------------------------------------------------- 7) COSTE Y COMPATIBILIDAD CON EL EJECUTABLE ANTERIOR
    static double NsPorMuestra(Assembly a)
    {
        int fs = 44100; float[] x = Programa(fs, 2, new double[][] { new double[] { -20, 60 } }, 7);
        double mejor = double.MaxValue;
        for (int rep = 0; rep < 5; rep++)
        {
            object m = Medidor(a, fs, new double[] { 1, 1 }); MethodInfo p = m.GetType().GetMethod("Procesa", T);
            Stopwatch sw = Stopwatch.StartNew(); object[] ar = new object[] { x, 0, x.Length }; p.Invoke(m, ar);
            sw.Stop(); mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds * 1e6 / x.Length);
        }
        return mejor;
    }
    static void CosteYAnterior(string dir)
    {
        Console.WriteLine("\n  6) LO QUE CUESTA, Y UN EJECUTABLE ANTERIOR ANTE EL ARCHIVO NUEVO\n");
        double nuevo = NsPorMuestra(asm);
        Info("medir 60 s de estereo a 44,1 kHz: " + F(nuevo, "0.00") + " ns por muestra con el LRA (Mono/Linux, no el A4)");
        string ant = Environment.GetEnvironmentVariable("ROCOLA_EXE_ANTERIOR");
        if (string.IsNullOrEmpty(ant) || !File.Exists(ant)) { Info("AVISO (no cuenta como comprobacion): sin ROCOLA_EXE_ANTERIOR no se compara el coste ni se prueba la lectura por un ejecutable anterior"); return; }
        Assembly vieja = Assembly.Load(File.ReadAllBytes(ant));
        double antes = NsPorMuestra(vieja);
        Info("el ejecutable anterior: " + F(antes, "0.00") + " ns por muestra (diferencia " + F(nuevo - antes, "+0.00;-0.00") + " ns, " + F(100.0 * (nuevo - antes) / antes, "+0.0;-0.0") + " %)");
        // Desde el 25-10-2026 la medida lleva tambien el zumbido de la corriente (T6, MedidorZumbido): unos 9 ns por muestra en Mono.
        int algAnt0 = (int)vieja.GetType("Rocola.MedidorBs1770", true).GetField("Algoritmo", T).GetValue(null);
        bool conZumbido = asm.GetType("Rocola.MedidorZumbido", false) != null && vieja.GetType("Rocola.MedidorZumbido", false) == null;
        Check(nuevo - antes < (conZumbido ? 12.0 : 1.0), conZumbido ? "el zumbido (T6) suma menos de 12 ns por muestra a la medida (algoritmo " + algAnt0 + " -> el de hoy)" : "el LRA suma menos de 1 ns por muestra a la medida");
        // El archivo nuevo, leido por el anterior.
        Type tfv = vieja.GetType("Rocola.Form1", true), tsv = vieja.GetType("Rocola.SonoridadIntegrada", true);
        Type TSil = null; foreach (Type t in asm.GetTypes()) if (t.Name == "MapaSilencios") TSil = t;
        object mudo = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { 180f });
        string cab = (string)TF.GetField("IntegCabecera", T).GetValue(null);
        StringWriter w = new StringWriter(); w.Write(cab + "\r\n");
        MethodInfo esc = TF.GetMethod("EscribeIntegRegistro", T);
        esc.Invoke(null, new object[] { w, "C:\\m\\tres.mp3", Reg("a", TS, 3, -13.5f, 2f, 2398, -0.4f, -2.1f, mudo) });
        esc.Invoke(null, new object[] { w, "C:\\m\\cuatro.mp3", Reg("b", TS, 4, -14.5f, 2f, 2398, -0.4f, -2.1f, mudo, 12.3f) });
        esc.Invoke(null, new object[] { w, "C:\\m\\cinco.mp3", Reg("c", TS, 5, -15.5f, 2f, 2398, -0.4f, -2.1f, mudo, 12.3f, -6.25f) });
        int registros = 3;
        if ((int)TM.GetField("Algoritmo", T).GetValue(null) >= 6)
        {   // y uno del 6 (T3 y T6), con donde empieza y acaba y el zumbido
            esc.Invoke(null, new object[] { w, "C:\\m\\seis.mp3", Activator.CreateInstance(TS, T, null, new object[] { 6, -15.5f, 2f, 2398, -0.4f, -2.1f, mudo, 12.3f, -6.25f, 3.2f, 177.4f, 60, 18.5f }, null) });
            registros = 4;
        }
        string texto = w.ToString();
        // Lo que el anterior entiende (su algoritmo o menos) lo usa; lo posterior lo guarda intacto para devolverlo al guardar.
        int algAnt = (int)vieja.GetType("Rocola.MedidorBs1770", true).GetField("Algoritmo", T).GetValue(null);
        IDictionary tv = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), tsv), StringComparer.OrdinalIgnoreCase);
        List<string> aj = new List<string>();
        bool lee = Lee(tfv, texto, tv, aj);
        string[] lineas = texto.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        bool bien = lee && aj.Count + tv.Count == registros;
        for (int k = 1; k < lineas.Length; k++)
        {
            int alg = int.Parse(lineas[k].Substring(0, 1)); string ruta = lineas[k].Substring(lineas[k].LastIndexOf('|') + 1);
            if (alg <= algAnt) bien &= tv.Contains(ruta) && !aj.Contains(lineas[k]);
            else bien &= aj.Contains(lineas[k]) && !tv.Contains(ruta);
        }
        Check(bien, "un ejecutable anterior (algoritmo " + algAnt + ") lee el archivo nuevo sin romperse: usa " + tv.Count + " de las " + registros + " medidas y guarda las "
              + aj.Count + " posteriores intactas para devolverlas al guardar");
    }

    [STAThread] static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_lra_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TM = asm.GetType("Rocola.MedidorBs1770", true); TS = asm.GetType("Rocola.SonoridadIntegrada", true);
        try
        {
            Calibra();
            CasosNorma();
            Lecturas();
            ContraPython(dir);
            Formato();
            LasDeAntes(dir);
            CosteYAnterior(dir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

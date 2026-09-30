using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NAudio.Wave;
// LO QUE NO ES UN NUMERO NO ENTRA EN LAS MEDIDAS NI EN LAS TABLAS.
//
// Un archivo en coma flotante con alguna muestra NaN o infinita (un WAV estropeado, por ejemplo) daba una medida
// entera NaN que se daba por buena: la huella del EQ se guardaba con NaN y, con ella, la media de TODA la biblioteca
// (ComputeEqAverage) y el EQ automatico de todas las canciones. Aqui se mira, con la rocola de verdad:
//   1) la medida de un WAV con NaN e infinitos sale finita, y es la misma que con esas muestras en silencio;
//   2) lo que se publica (PublicaMedida, CierraHuella, CierraSonoridad): nada que no sea un numero se guarda, y una
//      medida rota no cuenta como "ya medida" (se volvera a intentar);
//   3) al leer las tablas (arranque y respaldo): las filas con NaN o infinito no entran, las buenas si, y la media
//      del EQ de la biblioteca sale finita.
class NoFinitos
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    {
        try { return TF.GetMethod(n, T).Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static bool Finito(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    static bool Finitos(float[] c) { if (c == null) return false; foreach (float v in c) if (!Finito(v)) return false; return true; }

    static object FormNueva()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Set(f, "startupReadPending", false);
        return f;
    }

    // ------------------------------- 1) la medida -------------------------------
    static string Wav(string dir, string nombre, bool rotas)
    {
        int fs = 44100, n = fs * 25;   // 25 s: pasa los minimos de la huella (20 s) y del volumen
        float[] x = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            float v = (float)(0.3 * Math.Sin(2 * Math.PI * 440 * i / fs) + 0.1 * Math.Sin(2 * Math.PI * 3100 * i / fs));
            x[2 * i] = v; x[2 * i + 1] = v * 0.8f;
        }
        // Unas pocas muestras rotas, repartidas (en el original) o en silencio (la referencia)
        float[] malas = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN };
        for (int k = 0; k < 40; k++) { int i = fs + k * 12345; x[2 * i + (k & 1)] = rotas ? malas[k & 3] : 0f; }
        string ruta = Path.Combine(dir, nombre);
        using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
        return ruta;
    }
    static void Medida(string dir)
    {
        Console.WriteLine("\n  1) LA MEDIDA DE UN WAV CON MUESTRAS NaN E INFINITAS\n");
        string rota = Wav(dir, "rota.wav", true), limpia = Wav(dir, "limpia.wav", false);
        object f = FormNueva();
        object[] a = { rota, true, 0f, 0f, null };
        bool ok = (bool)Call(f, "MeasureFile", a);
        float rms = (float)a[2], dur = (float)a[3]; float[] eq = (float[])a[4];
        Check(ok && Finito(rms) && rms > 0f && Finito(dur) && dur > 24f, "la medida sale y es un numero (volumen " + rms.ToString("0.#####", CI) + ", " + dur.ToString("0.#", CI) + " s)");
        Check(eq != null && Finitos(eq), "y la huella del EQ tambien: ninguna banda NaN ni infinita");
        object g = FormNueva();
        object[] b = { limpia, true, 0f, 0f, null };
        Call(g, "MeasureFile", b);
        float rms2 = (float)b[2]; float[] eq2 = (float[])b[4];
        bool igual = eq != null && eq2 != null && Math.Abs(rms - rms2) <= 1e-5f * rms2;
        if (igual) for (int i = 0; i < 8; i++) if (Math.Abs(eq[i] - eq2[i]) > 0.01f) igual = false;
        Check(igual, "es la misma que con esas muestras en silencio: una muestra rota cuenta como silencio, que es lo que se oye");
    }

    // ------------------------------- 2) lo que se publica -------------------------------
    static float[] Curva(float v) { float[] c = new float[12]; for (int i = 0; i < 8; i++) c[i] = v + i * 0.25f; c[8] = 16000f; c[11] = 2f; return c; }
    static void Publica(string dir)
    {
        Console.WriteLine("\n  2) LO QUE SE GUARDA EN MEMORIA (y de ahi, en las tablas)\n");
        object f = FormNueva();
        Dictionary<string, float> loud = (Dictionary<string, float>)Get(f, "loudCache");
        Dictionary<string, float> dur = (Dictionary<string, float>)Get(f, "durCache");
        Dictionary<string, float[]> eq = (Dictionary<string, float[]>)Get(f, "eqCache");
        string a = "D:\\Musica\\nf\\a.mp3", b = "D:\\Musica\\nf\\b.mp3", c = "D:\\Musica\\nf\\c.mp3", d = "D:\\Musica\\nf\\d.mp3";
        float[] rota = Curva(0f); rota[3] = float.NaN;
        Call(f, "PublicaMedida", a, float.NaN, 180f, rota);
        Check(!loud.ContainsKey(a) && dur.ContainsKey(a) && !eq.ContainsKey(a),
              "PublicaMedida: el volumen NaN y la huella con una banda NaN no se guardan; la duracion buena, si");
        Check(!(bool)Call(null, "YaSeMidio", a), "y la cancion NO queda como ya medida: se volvera a intentar");
        Call(f, "PublicaMedida", b, float.PositiveInfinity, float.NaN, Curva(1f));
        Check(!loud.ContainsKey(b) && !dur.ContainsKey(b) && eq.ContainsKey(b) && !(bool)Call(null, "YaSeMidio", b),
              "un volumen infinito y una duracion NaN tampoco (la huella buena, si), y tampoco cuenta como medida");
        Call(f, "PublicaMedida", c, 0.2f, 200f, Curva(2f));
        Check(loud.ContainsKey(c) && dur.ContainsKey(c) && eq.ContainsKey(c) && (bool)Call(null, "YaSeMidio", c),
              "una medida buena se guarda entera y cuenta como medida (como siempre)");

        // La huella que se cierra mientras suena: con una suma NaN, no se guarda nada
        int antes = eq.Count;
        double[] sumas = new double[8]; for (int i = 0; i < 8; i++) sumas[i] = 1e-3 * (i + 1); sumas[2] = double.NaN;
        Call(f, "CierraHuella", d, sumas, 44100L * 30, 44100, 16000f, 40f);
        Check(!eq.ContainsKey(d) && eq.Count == antes, "CierraHuella: con una banda NaN no se guarda la huella");
        sumas[2] = 3e-3;
        Call(f, "CierraHuella", d, sumas, 44100L * 30, 44100, 16000f, 40f);
        Check(eq.ContainsKey(d) && Finitos(eq[d]), "y con las sumas buenas, si (se sigue guardando lo que hay que guardar)");

        // El volumen que se cierra mientras suena
        Call(f, "CierraSonoridad", a, double.NaN, 44100L * 2 * 30, 44100, 2);
        Check(!loud.ContainsKey(a), "CierraSonoridad: una suma NaN no se guarda como volumen");
        Call(f, "CierraSonoridad", a, double.PositiveInfinity, 44100L * 2 * 30, 44100, 2);
        Check(!loud.ContainsKey(a), "ni una infinita");
        Call(f, "CierraSonoridad", a, 0.04 * 44100 * 2 * 30, 44100L * 2 * 30, 44100, 2);
        Check(loud.ContainsKey(a) && Math.Abs(loud[a] - 0.2f) < 1e-4f, "y una buena, si (0,2 de volumen)");
        Codigo.EsperaGuardado(TF, f);
    }

    // ------------------------------- 3) al leer las tablas -------------------------------
    static object Arranque(string dir, string ruta, int tabla)
    {
        Type tLoad = TF.GetNestedType("StartupLoad", T);
        string[] rutas = new string[8];
        for (int i = 0; i < 8; i++) rutas[i] = Path.Combine(dir, "no_esta_" + i + ".txt");
        rutas[tabla] = ruta;
        object load = Activator.CreateInstance(tLoad, true);
        tLoad.GetField("Paths", T).SetValue(load, rutas);
        return Call(null, "ReadStartupData", load);
    }
    static object De(object data, string campo) { return TF.GetNestedType("StartupData", T).GetField(campo, T).GetValue(data); }
    static void Tablas(string dir)
    {
        Console.WriteLine("\n  3) AL LEER LAS TABLAS (una tabla ya estropeada se sana sola)\n");
        // Volumen: "rms|duracion|ruta"
        string vol = Path.Combine(dir, "vol.txt");
        File.WriteAllText(vol, "NaN|180|D:\\m\\a.mp3\r\nInfinity|180|D:\\m\\b.mp3\r\n0.2|NaN|D:\\m\\c.mp3\r\n0.2|-Infinity|D:\\m\\d.mp3\r\n0.25|200|D:\\m\\e.mp3\r\n-Infinity|D:\\m\\f.mp3\r\n");
        for (int camino = 0; camino < 2; camino++)
        {
            Dictionary<string, float> l, du;
            if (camino == 0) { object data = Arranque(dir, vol, 0); l = (Dictionary<string, float>)De(data, "Loud"); du = (Dictionary<string, float>)De(data, "Dur"); }
            else
            {
                l = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase); du = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                Call(null, "ReadLoudCache", vol, l, du);
            }
            string cual = camino == 0 ? "al arrancar" : "por el respaldo";
            Check(l.Count == 3 && l.ContainsKey("D:\\m\\c.mp3") && l.ContainsKey("D:\\m\\d.mp3") && l.ContainsKey("D:\\m\\e.mp3"),
                  "volumen " + cual + ": entran las tres con un volumen de verdad; NaN e infinitos, no");
            Check(du.Count == 1 && du.ContainsKey("D:\\m\\e.mp3"), "y de las duraciones, solo la que es un numero");
        }
        // EQ: 12 valores con ';' y la ruta
        string eqf = Path.Combine(dir, "eq.txt");
        StringBuilder sb = new StringBuilder();
        string buena = "1;2;0;-1;0;1;2;3;16000;0;40;1", conNaN = "1;2;NaN;-1;0;1;2;3;16000;0;40;1", conInf = "1;2;0;-1;0;1;2;3;Infinity;0;40;1";
        sb.Append(buena).Append("|D:\\m\\a.mp3\r\n").Append(conNaN).Append("|D:\\m\\b.mp3\r\n").Append(conInf).Append("|D:\\m\\c.mp3\r\n");
        sb.Append("0;0;0;0;0;0;0;NaN|D:\\m\\d.mp3\r\n");   // formato antiguo de 8 bandas
        File.WriteAllText(eqf, sb.ToString());
        for (int camino = 0; camino < 2; camino++)
        {
            Dictionary<string, float[]> e;
            if (camino == 0) e = (Dictionary<string, float[]>)De(Arranque(dir, eqf, 1), "Eq");
            else { e = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase); object[] args = { eqf, e, 0 }; TF.GetMethod("ReadEqCache", T).Invoke(null, args); }
            Check(e.Count == 1 && e.ContainsKey("D:\\m\\a.mp3"), "EQ " + (camino == 0 ? "al arrancar" : "por el respaldo") + ": solo entra la fila buena (NaN o infinito en cualquier banda: fila rota)");
            float[] media = null; int sanas = 0; bool solo = false;
            object[] ca = { e, media, sanas, solo };
            TF.GetMethod("ComputeEqAverage", T).Invoke(null, ca);
            Check(ca[1] == null || Finitos((float[])ca[1]), "y la media de la biblioteca sale finita");
        }
        // Duraciones rapidas: "duracion|ruta"
        string rap = Path.Combine(dir, "rap.txt");
        File.WriteAllText(rap, "NaN|D:\\m\\a.mp3\r\nInfinity|D:\\m\\b.mp3\r\n185.5|D:\\m\\c.mp3\r\n");
        Dictionary<string, float> fa = (Dictionary<string, float>)De(Arranque(dir, rap, 4), "Fast");
        Dictionary<string, float> fb = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Call(null, "ReadDurFast", rap, fb);
        Check(fa.Count == 1 && fa.ContainsKey("D:\\m\\c.mp3") && fb.Count == 1 && fb.ContainsKey("D:\\m\\c.mp3"),
              "duraciones rapidas (arranque y respaldo): solo la que es un numero");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_nofinitos_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            Medida(dir);
            Publica(dir);
            Tablas(dir);
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally
        {
            carpeta.SetValue(null, previa);
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

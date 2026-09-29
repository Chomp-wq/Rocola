using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
// OTRO ARCHIVO EN LA MISMA RUTA (Form1.Sonido.cs, ArmaCadena; SinSilencios.cs).
//
// Lo que la rocola aprende de cada cancion (la duracion, donde suena) se apunta por RUTA. Si en esa ruta hay hoy otro
// archivo -la cancion completa en lugar de una descarga cortada, otra version con el mismo nombre-, lo apuntado es del
// de antes. Con esa duracion vieja, el fundido de salida bajaba a cero donde acababa el de antes y el RESTO de la cancion
// pagada sonaba en silencio. Aqui, con la cadena de verdad (ArmaCadena, la misma del arranque y del relevo sin pausa):
//   1) la cancion "cortada" medida (25 s) y en su ruta ahora la completa (40 s): suena entera, con musica hasta el final,
//      y el fundido cae al final de la de ahora;
//   2) con la duracion bien apuntada, el fundido sigue saliendo de lo apuntado (como siempre) y cae en su sitio;
//   3) lo apuntado se sigue preguntando primero (turno.cs): el archivo solo manda si no cuadra;
//   4) y lo aprendido del de antes (volumen, duracion, EQ, integrada, repaso a fondo) se olvida y se vuelve a aprender
//      oyendola: antes se quedaba para siempre, porque la cancion "ya estaba medida". Lo de las demas, intacto.
class OtroArchivo
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Set(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(o, v); return; } } throw new Exception("no esta " + n); }
    static object Get(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); } PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null; }
    static object Call(object o, string n, params object[] a) { try { return TF.GetMethod(n, T).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }

    // Suena y devuelve el nivel (RMS) de cada segundo de lo que sale de la cadena.
    static List<double> Suena(object f, string ruta)
    {
        Type tt = TF.GetNestedType("Track", T); object track = Activator.CreateInstance(tt, true); Set(track, "Path", ruta);
        Type tp = TF.GetField("nowPlaying", T).FieldType; object item = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp); Set(item, "Song", track);
        object lector = Call(f, "OpenAudio", ruta);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0); Set(f, "nowPlaying", item);
        ISampleProvider s = (ISampleProvider)Get(cad, "Salida");
        int fs = s.WaveFormat.SampleRate, ch = s.WaveFormat.Channels;
        List<double> seg = new List<double>();
        float[] b = new float[fs * ch]; int r; double suma = 0; long n = 0;
        while ((r = s.Read(b, 0, b.Length)) > 0)
            for (int i = 0; i < r; i++) { suma += b[i] * (double)b[i]; if (++n == fs * ch) { seg.Add(Math.Sqrt(suma / n)); suma = 0; n = 0; } }
        if (n > 0) seg.Add(Math.Sqrt(suma / n));
        ((IDisposable)lector).Dispose();
        return seg;
    }
    // Una rocola sin encender con lo que hace falta para guardar (las colas de guardado y las tablas publicadas).
    static object Nueva()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Set(f, "startupReadPending", false);
        return f;
    }
    // Una rocola que ya aprendio la cancion de 'ruta' (con la duracion 'dura') y otra cancion mas.
    static object Aprendida(string ruta, float dura, string otra)
    {
        object f = Nueva();
        Set(f, "integEstado", 1);
        Set(f, "normalizeOn", true); Set(f, "eqAuto", true); Set(f, "excAmount", 0f); Set(f, "bassAmount", 0f); Set(f, "bassSongFactor", 1f); Set(f, "crossfadeSec", 0.5f);
        Type ts = TF.Assembly.GetType("Rocola.SonoridadIntegrada", true);
        IDictionary integ = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase);
        Set(f, "integCache", integ);
        Dictionary<string, float> loud = (Dictionary<string, float>)Get(f, "loudCache"), dur = (Dictionary<string, float>)Get(f, "durCache");
        Dictionary<string, float[]> eq = (Dictionary<string, float[]>)Get(f, "eqCache");
        HashSet<string> afondo = (HashSet<string>)Get(f, "deepDone");
        foreach (string r in new string[] { ruta, otra })
        {
            float[] c = new float[12]; for (int i = 0; i < 8; i++) c[i] = 0.5f * i; c[8] = 16000f; c[11] = 1f;
            loud[r] = 0.2f; dur[r] = (r == ruta) ? dura : 200f; eq[r] = c; afondo.Add(r);
            integ[r] = Activator.CreateInstance(ts, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 1, -14f, 2f, 250 }, null);
        }
        TF.GetMethod("MedidaPuesta", T).Invoke(null, new object[] { ruta });
        return f;
    }
    static object Arma(object f, string ruta)
    {
        Type tt = TF.GetNestedType("Track", T); object track = Activator.CreateInstance(tt, true); Set(track, "Path", ruta);
        object lector = Call(f, "OpenAudio", ruta);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        ((IDisposable)lector).Dispose();
        return cad;
    }
    static void Escribe(string ruta, int segundos)
    {
        int fs = 44100, n = fs * segundos; float[] x = new float[2 * n];
        for (int i = 0; i < n; i++) { float v = (float)(0.25 * Math.Sin(2 * Math.PI * 330 * i / fs)); x[2 * i] = v; x[2 * i + 1] = v; }
        using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
    }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_otro_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);   // lo que se guarde, aqui (nunca junto a las pruebas)
            string ruta = Path.Combine(dir, "cancion.wav");
            object f = Nueva();
            Set(f, "normalizeOn", false); Set(f, "excAmount", 0f); Set(f, "bassAmount", 0f); Set(f, "bassSongFactor", 1f);
            Set(f, "crossfadeSec", 0.5f);   // el fundido de fabrica
            Dictionary<string, float> dur = (Dictionary<string, float>)Get(f, "durCache");

            Console.WriteLine("\n  1) EN LA RUTA HAY AHORA OTRO ARCHIVO, MAS LARGO\n");
            Escribe(ruta, 40);
            dur[ruta] = 25f;   // lo que se midio de la descarga cortada que habia antes
            List<double> a = Suena(f, ruta);
            double minimo = 1; for (int s = 26; s < 39 && s < a.Count; s++) minimo = Math.Min(minimo, a[s]);
            Check(a.Count >= 40 && minimo > 0.1, "suena entera: de los 26 a los 39 s sigue habiendo musica (lo mas bajo: " + minimo.ToString("0.000") + "; antes, silencio)");
            Check(a.Count >= 40 && a[a.Count - 1] < a[a.Count - 3] * 0.9, "y el fundido cae al final de la de ahora (" + a.Count + " s)");

            Console.WriteLine("\n  2) CON LA DURACION BIEN APUNTADA, COMO SIEMPRE\n");
            dur[ruta] = 40f;
            List<double> b = Suena(f, ruta);
            double min2 = 1; for (int s = 1; s < 39 && s < b.Count; s++) min2 = Math.Min(min2, b[s]);
            Check(b.Count >= 40 && min2 > 0.1 && b[b.Count - 1] < b[b.Count - 3] * 0.9, "suena entera y el fundido cae al final");
            dur[ruta] = 40.4f;   // unas decimas de diferencia con lo que dice el lector: es el mismo archivo
            List<double> c = Suena(f, ruta);
            Check(c.Count == b.Count, "con unas decimas de diferencia manda lo apuntado (es el mismo archivo)");

            Console.WriteLine("\n  3) LO APUNTADO SE PREGUNTA PRIMERO\n");
            List<string> pasos = Codigo.Nombres(TF.GetMethod("ArmaCadena", T));
            int iA = pasos.IndexOf("SegundosDe"), iB = pasos.IndexOf("get_TotalTime");
            Check(iA >= 0 && iB > iA, "ArmaCadena pregunta a lo apuntado y luego lo compara con lo que dice el lector ya abierto");

            Console.WriteLine("\n  4) LO APRENDIDO DEL DE ANTES SE OLVIDA Y SE VUELVE A APRENDER\n");
            string otra = "D:\\m\\otra.mp3";
            object g = Aprendida(ruta, 25f, otra);
            object cad = Arma(g, ruta);
            Dictionary<string, float> loud = (Dictionary<string, float>)Get(g, "loudCache"), dur2 = (Dictionary<string, float>)Get(g, "durCache");
            Dictionary<string, float[]> eq = (Dictionary<string, float[]>)Get(g, "eqCache");
            IDictionary integ = (IDictionary)Get(g, "integCache");
            HashSet<string> afondo = (HashSet<string>)Get(g, "deepDone");
            bool yaMedida = (bool)TF.GetMethod("YaSeMidio", T).Invoke(null, new object[] { ruta });
            Check(!loud.ContainsKey(ruta) && !dur2.ContainsKey(ruta) && !eq.ContainsKey(ruta) && !integ.Contains(ruta) && !afondo.Contains(ruta) && !yaMedida,
                  "se olvida todo lo suyo: volumen, duracion, EQ, integrada, repaso a fondo y la medida de la sesion");
            Check(loud.ContainsKey(otra) && dur2.ContainsKey(otra) && eq.ContainsKey(otra) && integ.Contains(otra) && afondo.Contains(otra),
                  "lo de las demas canciones no se toca");
            Check((string)Get(cad, "MedidaRuta") == ruta && (string)Get(cad, "EqRuta") == ruta, "y se vuelve a aprender oyendola esta misma vez (volumen y huella del EQ)");
            Dictionary<string, float> rapida = (Dictionary<string, float>)Get(g, "durFast");
            Check(rapida.ContainsKey(ruta) && Math.Abs(rapida[ruta] - 40f) < 0.2f, "la lista y la espera de la cola ya dicen lo que dura de verdad (" + (rapida.ContainsKey(ruta) ? rapida[ruta].ToString("0.0") : "-") + " s)");
            Codigo.EsperaGuardado(TF, g);
            string vol = Path.Combine(dir, "rocola_volumen.txt"), errores = Path.Combine(dir, "rocola_error.txt");
            string tv = File.Exists(vol) ? File.ReadAllText(vol) : "";
            Check(tv.Contains("otra.mp3") && !tv.Contains("cancion.wav"), "y queda guardado asi: la tabla de volumen ya no la trae");
            Check(File.Exists(errores) && File.ReadAllText(errores).Contains("Otro archivo en la misma ruta"), "y apuntado en rocola_error.txt");
            object h = Aprendida(ruta, 40.3f, otra);
            Call(h, "ActualizaFuenteMedida", ruta, Call(null, "CapturaFuente", ruta)); // certificado de la fuente actual, no una tabla antigua
            object cad2 = Arma(h, ruta);
            Check(((Dictionary<string, float>)Get(h, "loudCache")).ContainsKey(ruta) && Get(cad2, "MedidaRuta") == null,
                  "el mismo archivo (unas decimas de diferencia) no se olvida ni se vuelve a medir");
            Check(Codigo.Nombres(TF.GetMethod("ArmaCadena", T)).IndexOf("OlvidaSiEsOtroArchivo") >= 0
                  && Codigo.Nombres(TF.GetMethod("ArmaCadena", T)).IndexOf("OlvidaSiEsOtroArchivo") < Codigo.Nombres(TF.GetMethod("ArmaCadena", T)).IndexOf("GananciaDe"),
                  "se mira al ponerla, antes de calcular la ganancia, el EQ y el bajo");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { carpeta.SetValue(null, previa); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}

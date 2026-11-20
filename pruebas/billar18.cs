using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// LAS CUATRO MEJORAS DEL MODO BILLAR DEL 18-11-2026 (despues de la de «el volumen deberia ser constante»):
//   1) VOLUMEN PAREJO DESDE CERO: sin la marca parejo_v2=1, rocola_billar_nivel.txt se aparta (rocola_billar_nivel.antes-2026-11-18.txt, sin
//      pisar nada: .1, .2...) y se empieza vacio; con la marca no se toca nada; una rocola nueva solo pone la marca.
//   2) CLARIDAD Y ESES: LocalAutomatico da Claridad=false y Eses=false; LocalDe las toma de los interruptores del dueno con el modo encendido.
//   3) BAJO PSICOACUSTICO: con el modo billar manda bassAuto / bassAmount.
//   4) LO MAS QUE BAJO EL LIMITADOR EN 10 S: un maximo de las propias lecturas de la ventana (sin segundo lector de lo que se pone a cero).
// Senales sinteticas: miden la cuenta, no el oido.
class Billar18
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
    static object GetS(string n) { FieldInfo f = TF.GetField(n, T); return f.GetValue(f.IsStatic ? null : F); }
    static object Call(string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == n && m.GetParameters().Length == a.Length)
        { try { return m.Invoke(F, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
        throw new MissingMethodException(n);
    }
    static bool B(object a, string n) { return (bool)a.GetType().GetField(n).GetValue(a); }
    static string Texto(string rel) { string p = Path.Combine("..", "Rocola", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
    const int Fs = 44100;

    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_billar18_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Assembly asm = Assembly.Load(File.ReadAllBytes(args.Length > 0 ? args[0] : "rocola.exe"));
            TF = asm.GetType("Rocola.Form1", true);
            F = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(T))
                if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(F) == null) fi.SetValue(F, Activator.CreateInstance(fi.FieldType, true));
            Console.WriteLine("\n  1) VOLUMEN PAREJO DESDE CERO (marca parejo_v2)\n");
            Parejo(dir);
            Console.WriteLine("\n  2) CLARIDAD Y ESES: DEL DUENO\n");
            Interruptores();
            Console.WriteLine("\n  3) BAJO: COMO LO DEJO EL DUENO, TAMBIEN CON EL MODO BILLAR\n");
            Bajo();
            Console.WriteLine("\n  4) LO MAS QUE BAJO EL LIMITADOR EN 10 S\n");
            Maximo10(dir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }

    static string Nivel(string dir) { return Path.Combine(dir, "rocola_billar_nivel.txt"); }
    static void Parejo(string dir)
    {
        MethodInfo aparta = TF.GetMethod("ParejoApartaViejo", T);
        Check(aparta != null, "esta ParejoApartaViejo");
        string viejo = "1.5|C:\\musica\\a.mp3\r\n-0.7|C:\\musica\\b.mp3\r\n";
        // la funcion sola: renombra con el contenido intacto, nunca pisa
        string d1 = Path.Combine(dir, "solo"); Directory.CreateDirectory(d1);
        string arch = Path.Combine(d1, "rocola_billar_nivel.txt");
        Check(aparta.Invoke(null, new object[] { arch }) == null, "sin archivo, no hace nada");
        File.WriteAllText(arch, viejo);
        string r1 = (string)aparta.Invoke(null, new object[] { arch });
        Check(r1 != null && Path.GetFileName(r1) == "rocola_billar_nivel.antes-2026-11-18.txt" && !File.Exists(arch) && File.ReadAllText(r1) == viejo, "con archivo: queda como rocola_billar_nivel.antes-2026-11-18.txt, intacto, y el original ya no esta");
        File.WriteAllText(arch, "otro|x\r\n");
        string r2 = (string)aparta.Invoke(null, new object[] { arch });
        Check(Path.GetFileName(r2) == "rocola_billar_nivel.antes-2026-11-18.1.txt" && File.ReadAllText(r1) == viejo && File.ReadAllText(r2) == "otro|x\r\n", "si el .antes ya existe no lo pisa: usa .1");
        File.WriteAllText(arch, "tercero|y\r\n");
        string r3 = (string)aparta.Invoke(null, new object[] { arch });
        Check(Path.GetFileName(r3) == "rocola_billar_nivel.antes-2026-11-18.2.txt" && File.ReadAllText(r1) == viejo && File.ReadAllText(r2) == "otro|x\r\n", "y despues .2; los dos anteriores intactos");

        // la carga de verdad: carpeta de datos = dir (ROCOLA_DATOS)
        string real = Nivel(dir);
        Check(Path.GetFullPath((string)TF.GetProperty("CarpetaDatos", T).GetValue(null, null)) == Path.GetFullPath(dir), "la carpeta de datos de la prueba es la temporal");
        IDictionary db = (IDictionary)GetS("parejoDb");
        File.WriteAllText(real, viejo);
        SetS("parejoV2", false);
        Call("CargaParejo");
        db = (IDictionary)GetS("parejoDb");
        Check(!File.Exists(real) && File.ReadAllText(Path.Combine(dir, "rocola_billar_nivel.antes-2026-11-18.txt")) == viejo, "sin la marca y con archivo viejo: renombrado, contenido intacto");
        Check(db.Count == 0 && (bool)GetS("parejoV2"), "parejoDb vacio y la marca puesta");
        Check(GetS("parejoModelo") == null && (int)GetS("parejoModeloN") == -1000, "el modelo de la recta sin datos");
        float est = (float)Call("ParejoEstimado", "C:\\musica\\a.mp3");
        Check(est == 0f && (float)TF.GetMethod("ParejoDe", T).Invoke(null, new object[] { "C:\\musica\\a.mp3" }) == 0f, "nada aprendido: lo que se aplica a esa cancion es 0");
        // con la marca: no se toca
        File.WriteAllText(real, "0.9|C:\\musica\\c.mp3\r\n");
        Call("CargaParejo");
        db = (IDictionary)GetS("parejoDb");
        Check(File.Exists(real) && File.ReadAllText(real) == "0.9|C:\\musica\\c.mp3\r\n" && db.Count == 1 && Math.Abs((float)db["C:\\musica\\c.mp3"] - 0.9f) < 1e-6f, "con la marca: el archivo no se renombra y se lee como siempre");
        Check(!File.Exists(Path.Combine(dir, "rocola_billar_nivel.antes-2026-11-18.1.txt")), "y no aparecio otro .antes");
        // un .antes ya existente y sin marca: no se pisa
        SetS("parejoV2", false);
        Call("CargaParejo");
        string ant = Path.Combine(dir, "rocola_billar_nivel.antes-2026-11-18.txt"), ant1 = Path.Combine(dir, "rocola_billar_nivel.antes-2026-11-18.1.txt");
        Check(File.ReadAllText(ant) == viejo && File.Exists(ant1) && File.ReadAllText(ant1) == "0.9|C:\\musica\\c.mp3\r\n" && !File.Exists(real), "sin marca y con un .antes ya existente: no lo pisa, el nuevo va a .1");
        // rocola nueva: sin archivo, solo la marca
        SetS("parejoV2", false);
        Call("CargaParejo");
        Check((bool)GetS("parejoV2") && !File.Exists(real) && ((IDictionary)GetS("parejoDb")).Count == 0, "sin archivo: solo se pone la marca");
        // la marca se lee y se guarda en la configuracion
        string dat = Texto("Form1.Datos.cs");
        Check(dat.Contains("StartsWith(\"parejo_v2=\")") && dat.Contains("sb.Append(\"parejo_v2=\").Append(parejoV2"), "la configuracion lee y guarda parejo_v2=");
        string vp = Texto("VolumenParejo.cs");
        Check(vp.IndexOf("if (!parejoV2)") > 0 && vp.IndexOf("if (!parejoV2)") < vp.IndexOf("File.ReadAllLines(archivo)"), "el renombrado va ANTES de leer el archivo, en el mismo hilo (CargaParejo)");
        string pr = Texto("Form1.cs");
        Check(pr.IndexOf("LoadConfigPrefs();   //") > 0 && pr.IndexOf("LoadConfigPrefs();   //") < pr.IndexOf("CargaParejo(); });"), "la carga se encola DESPUES de leer la configuracion");
    }

    static void Interruptores()
    {
        MethodInfo auto4 = TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null);
        object a = auto4.Invoke(null, new object[] { 8f, 5f, 10f, float.NaN }), fl = auto4.Invoke(null, new object[] { 8f, 5f, 10f, 9f });
        Check(!B(a, "Claridad") && !B(a, "Eses") && !B(fl, "Claridad") && !B(fl, "Eses"), "LocalAutomatico da Claridad=false y Eses=false (y la flaca tambien)");
        Check(B(a, "Subgrave") && B(a, "EstrechoSoloSiHaceFalta") && B(fl, "Cuerpo") && !B(a, "Cuerpo"), "lo demas igual: subgrave, estereo solo si hace falta, cuerpo de la flaca");
        Type TTrack = TF.GetNestedType("Track", T);
        object tr = Activator.CreateInstance(TTrack, true);
        MethodInfo localDe = null;
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "LocalDe" && m.GetParameters()[0].ParameterType == TTrack) localDe = m;
        SetS("localAuto", true);
        SetS("claridadLocal", false); SetS("suavizaEses", false);
        object o = localDe.Invoke(F, new object[] { tr });
        Check(!B(o, "Claridad") && !B(o, "Eses"), "con el modo billar y los interruptores de fabrica: ni claridad ni eses");
        SetS("claridadLocal", true); SetS("suavizaEses", false);
        o = localDe.Invoke(F, new object[] { tr });
        Check(B(o, "Claridad") && !B(o, "Eses"), "solo la claridad del dueno encendida: la lleva y las eses no");
        SetS("claridadLocal", false); SetS("suavizaEses", true);
        o = localDe.Invoke(F, new object[] { tr });
        Check(!B(o, "Claridad") && B(o, "Eses"), "solo las eses del dueno encendidas: las lleva y la claridad no");
        SetS("localAuto", false);
        o = localDe.Invoke(F, new object[] { tr });
        Check(B(o, "Eses") && !B(o, "Claridad"), "con el modo apagado, igual que siempre (los interruptores)");
        SetS("claridadLocal", false); SetS("suavizaEses", false);
        string aj = Texto("Form1.Ajustes.cs");
        Check(!aj.Contains("Necesita(swCla, sinAuto") && !aj.Contains("Necesita(swEses, sinAuto"), "en la ventana, la claridad y las eses ya no van en gris por el modo");
        Check(!aj.Contains("El modo billar ya no pone nada que suba o baje el volumen\\nsolo: la presencia va con") && aj.Contains("ni presencia, ni claridad, ni eses"), "el texto de Tono y presencia ya no dice que el modo pone la claridad o las eses");
    }

    static void Bajo()
    {
        SetS("bassAmount", 0.9f); SetS("bassAutoBase", 0.3f);
        MethodInfo bajo = TF.GetMethod("BassBaseNow", T);
        float[] r = new float[4]; int k = 0;
        foreach (bool auto in new bool[] { true, false }) foreach (bool billar in new bool[] { false, true })
        { SetS("bassAuto", auto); SetS("localAuto", billar); r[k++] = (float)bajo.Invoke(F, null); }
        Check(r[0] == 0.3f && r[1] == 0.3f, "Auto encendido (de fabrica): la base automatica, con el modo apagado o encendido");
        Check(r[2] == 0.9f && r[3] == 0.9f, "Auto apagado: la barra, con el modo apagado o encendido (antes el billar la ignoraba)");
        MethodInfo para = TF.GetMethod("BassAmountPara", T, null, new Type[] { typeof(float), typeof(float) }, null);
        SetS("localAuto", true); SetS("bassAuto", false); float pm = (float)para.Invoke(F, new object[] { 1f, 0.3f });
        SetS("bassAuto", true); float pa = (float)para.Invoke(F, new object[] { 1f, 0.3f });
        Check(pm > pa + 0.3f, "BassAmountPara con el modo billar: manda la barra (" + pm.ToString("0.00") + ") o la base (" + pa.ToString("0.00") + ") segun Auto");
        SetS("localAuto", false); SetS("bassAuto", true);
    }

    static void Maximo10(string dir)
    {
        // el maximo, solo
        Type tm = TF.GetNestedType("MaximoReciente", T);
        object m = Activator.CreateInstance(tm, true);
        MethodInfo anota = tm.GetMethod("Anota", T), maxi = tm.GetMethod("Maximo", T);
        float v0 = (float)maxi.Invoke(m, new object[] { 0 });
        anota.Invoke(m, new object[] { 1000, 0f }); anota.Invoke(m, new object[] { 1500, 4.2f }); anota.Invoke(m, new object[] { 2000, 1.0f });
        float v1 = (float)maxi.Invoke(m, new object[] { 2500 }), v2 = (float)maxi.Invoke(m, new object[] { 11500 }), v3 = (float)maxi.Invoke(m, new object[] { 11501 });
        Check(v0 == 0f && Math.Abs(v1 - 4.2f) < 1e-6f && Math.Abs(v2 - 4.2f) < 1e-6f && Math.Abs(v3 - 1.0f) < 1e-6f, "el maximo de la ventana: 4,2 hasta 10 s despues de esa lectura y luego 1,0 (salio " + v1 + ", " + v2 + ", " + v3 + ")");
        for (int i = 0; i < 200; i++) anota.Invoke(m, new object[] { 20000 + i * 500, 0.5f });
        Check(Math.Abs((float)maxi.Invoke(m, new object[] { 20000 + 199 * 500 }) - 0.5f) < 1e-6f, "el anillo da la vuelta sin crecer ni fallar (200 lecturas)");
        int pasoTick = Int32.MaxValue - 3000;
        object m2 = Activator.CreateInstance(tm, true); anota.Invoke(m2, new object[] { pasoTick, 2.5f });
        Check(Math.Abs((float)maxi.Invoke(m2, new object[] { unchecked(pasoTick + 5000) }) - 2.5f) < 1e-6f, "y aguanta que el reloj del sistema de la vuelta");
        // el texto, con la cadena de verdad
        string wav = Path.Combine(dir, "fuerte.wav");
        using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(Fs, 2)))
        {
            float[] x = new float[2 * Fs * 18];
            for (int i = 0; i < Fs * 18; i++)
            {
                double amp = i / (Fs * 6) == 1 ? 0.501 : 0.0316;   // -30, -6 y -30 dBFS: en el tramo fuerte, con el modo billar, el limitador baja (como en billarparejo)
                float v = (float)(amp * Math.Sin(2 * Math.PI * 440.0 * i / Fs));
                x[2 * i] = v; x[2 * i + 1] = v;
            }
            w.WriteSamples(x, 0, x.Length);
        }
        // lo mismo que 'Fabrica' de billarparejo.cs: todo de fabrica y el modo billar encendido
        SetS("normalizeOn", true); SetS("normTarget", 0.26f); SetS("normStrength", 1f); SetS("masterDb", 0f);
        SetS("excAmount", 0.35f); SetS("bassAuto", true); SetS("bassAmount", 0.3f); SetS("bassSongFactor", 1f);
        SetS("eqAuto", true); SetS("eqAmount", 0f);
        SetS("sbrAllowed", true); SetS("sbrOn", false); SetS("declipOn", true);
        if (TF.GetField("nocheArrancada", T) != null) { SetS("nocheArrancada", true); SetS("nocheFactor", 1f); }
        SetS("crossfadeSec", 0.5f); SetS("saltaSilencios", false);
        SetS("claridadLocal", false); SetS("partesSuaves", false); SetS("presenciaSala", false);
        SetS("gravesLocalDb", 0); SetS("agudosLocalDb", 0); SetS("balanceDb", 0);
        SetS("cortaSubgrave", false); SetS("estereoEstrecho", false); SetS("estereoAuto", false);
        SetS("canalMudo", true); SetS("quitaContinua", true);
        SetS("retumbeDinamico", false); SetS("ataqueClaro", false); SetS("asperezaDinamica", false); SetS("bomboSinHundir", false); SetS("suavizaEses", false);
        SetS("sinMejoras", false); SetS("aLaMedida", false); SetS("quitaSiseo", false);
        SetS("localAuto", true);
        Type tt = TF.GetNestedType("Track", T);
        object track = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(track, wav); tt.GetField("Title", T).SetValue(track, "Prueba");
        object[] a = { wav, null };
        Call("MideIntegrada", a);
        IDictionary tab = (IDictionary)Get(F, "integCache");
        if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); TF.GetField("integCache", T).SetValue(F, tab); }
        tab[wav] = a[1];
        object lector = Call("OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call("ArmaCadena", track, lector, false, 0.0);
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        float[] b = new float[4410 * 2]; while (sal.Read(b, 0, b.Length) > 0) { }   // el limitador anota lo que bajo (nadie lo ha leido)
        Type tp = TF.GetNestedType("PlayItem", T);
        object item = Activator.CreateInstance(tp, true); tp.GetField("Song", T).SetValue(item, track);
        SetS("bajadaReciente", Activator.CreateInstance(tm, true));   // (el constructor de Form1 no corre en la prueba: sus campos no se iniciaron)
        SetS("cadenaSonando", cad); SetS("nowPlaying", item);
        string t1 = (string)Call("TextoLoQueSuena");
        Console.WriteLine("        " + t1.Replace("\n", " / "));
        Check(t1.Contains("El limitador baja: ") && t1.Contains(" (10 s: "), "primera lectura: junto a lo que baja ahora sale «(10 s: Y dB)»");
        string t2 = (string)Call("TextoLoQueSuena");
        Console.WriteLine("        " + t2.Replace("\n", " / "));
        Check(t2.Contains("El limitador baja: nada") && t2.Contains(" (10 s: "), "la lectura siguiente ya no baja (se puso a cero al leerla) pero el maximo de 10 s se acuerda");
        // las dos lecturas y nadie mas: el limitador solo se lee en LoQueSuena
        int lectores = 0;
        foreach (string f in Directory.GetFiles(Path.Combine("..", "Rocola"), "*.cs")) { string s = File.ReadAllText(f); int i = 0; while ((i = s.IndexOf(".TomaBajadaDb()", i)) >= 0) { lectores++; i++; } }
        Check(lectores == 1, "TomaBajadaDb (que se pone a cero al leerla) sigue teniendo UN solo lector en el codigo (" + lectores + ")");
        System.Threading.Thread.Sleep(10600);
        string t3 = (string)Call("TextoLoQueSuena");
        Console.WriteLine("        " + t3.Replace("\n", " / "));
        Check(t3.Contains("El limitador baja: nada") && !t3.Contains("(10 s:"), "a los 10 s ya no sale");
        ((IDisposable)lector).Dispose();
        SetS("cadenaSonando", null); SetS("nowPlaying", null);
    }
}

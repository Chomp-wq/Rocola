using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Windows.Forms;
using NAudio.Wave;
using Label = System.Windows.Forms.Label;

// LOS MODOS AUTOMATICOS DEL SONIDO (revision del 23-10-2026; MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md).
// Ctrl+F3 -> Sonido tiene varios modos automaticos: el volumen de los videos, el EQ, los graves, el estereo estrecho solo si hace
// falta (S3) y el modo automatico del local. Se comprueba, con la ventana y la cadena de verdad:
//  1) Los ajustes de esos modos se guardan y se leen igual, y un valor fuera de rango no los rompe.
//  2) Los grises de la ventana dicen la verdad: un mando en gris es porque NO puede actuar, y uno que SI manda no lo esta
//     (S3 con el modo del local; la barra de los videos cuando el automatico no elige nada, que en Windows XP es siempre; «a la
//     medida» de la IA sin su modelo).
//  3) «Escuchar sin mejoras» tambien vale para una cancion armada mientras dura (el relevo sin pausa, la siguiente).
//  4) El reparto de los rangos de la biblioteca (rocola_calidad.txt), con el que se ve si los cortes del modo del local le sirven.
//  5) MEDIDAS PARA LAS PROPUESTAS: solo informan (no fallan). Con senales sinteticas por la cadena de verdad.
// No es escucharlo en el bar ni medirlo en Windows XP: eso solo se sabe alli.
class ModosAuto
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static int oks, fallos;
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
    static object GetS(string n) { return TF.GetField(n, T).GetValue(null); }
    static void SetS(string n, object v) { TF.GetField(n, T).SetValue(null, v); }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }

    // ---------------------------------------------------------------- 1) CONFIGURACION
    static void Configuracion(string dir)
    {
        object f = Codigo.FormSinEncender(TF);
        string archivo = (string)Get(f, "ConfigFile");
        // a) Guardar y leer da lo mismo.
        Set(f, "videoVol", 37); Set(f, "videoAuto", false); Set(f, "eqAmount", 0.42f); Set(f, "eqAuto", false);
        Set(f, "bassAmount", 0.6f); Set(f, "bassAuto", false); SetS("estereoAuto", true); SetS("localAuto", true); SetS("tipoLocal", 2);
        string texto = (string)Call(f, "TextoConfig");
        File.WriteAllText(archivo, texto);
        Set(f, "videoVol", 85); Set(f, "videoAuto", true); Set(f, "eqAmount", 0f); Set(f, "eqAuto", true);
        Set(f, "bassAmount", 0.3f); Set(f, "bassAuto", true); SetS("estereoAuto", false); SetS("localAuto", false); SetS("tipoLocal", 0);
        Call(f, "LoadConfigPrefs");
        Check((int)Get(f, "videoVol") == 37 && !(bool)Get(f, "videoAuto") && !(bool)Get(f, "eqAuto") && !(bool)Get(f, "bassAuto"), "videos, EQ y graves: el volumen base y el automatico se guardan y se leen igual");
        Check(Math.Abs((float)Get(f, "eqAmount") - 0.42f) < 0.006f && Math.Abs((float)Get(f, "bassAmount") - 0.6f) < 0.0001f, "y la fuerza del EQ (en %) y el bajo a mano");
        Check((bool)GetS("estereoAuto") && (bool)GetS("localAuto") && (int)GetS("tipoLocal") == 2, "S3, el modo del local y su tipo tambien");
        // b) Valores fuera de rango: se acotan, no rompen nada.
        File.WriteAllText(archivo, "video_vol=500\r\neq_auto=250\r\nbajo=-3\r\ntipo_local=9\r\nvideo_automatico=2\r\neq_automatico=0\r\nbajo_automatico=x\r\nlocal_auto=2\r\nestereo_auto=2\r\n");
        Call(f, "LoadConfigPrefs");
        Check((int)Get(f, "videoVol") == 100 && Math.Abs((float)Get(f, "eqAmount") - 1f) < 1e-6f && (float)Get(f, "bassAmount") == 0f, "volumen de videos, EQ y bajo fuera de rango se acotan (100, 100 %, 0)");
        Check((int)GetS("tipoLocal") == 2 && (bool)Get(f, "videoAuto") && !(bool)Get(f, "eqAuto") && (bool)Get(f, "bassAuto"), "el tipo de local se acota; un automatico con valor raro queda en su valor de fabrica (videos y graves si, EQ solo con 0)");
        Check(!(bool)GetS("localAuto") && !(bool)GetS("estereoAuto"), "y los dos que nacen apagados solo se encienden con un 1");
        File.WriteAllText(archivo, "tipo_local=-5\r\nbajo=NaN\r\neq_auto=Infinity\r\nvideo_vol=abc\r\n");
        SetS("tipoLocal", 1); Set(f, "videoVol", 61);
        Call(f, "LoadConfigPrefs");
        Check((int)GetS("tipoLocal") == 0 && (float)Get(f, "bassAmount") == 0f && (float)Get(f, "eqAmount") == 1f && (int)Get(f, "videoVol") == 61, "un menos, un NaN, un infinito y un texto: se acotan o se ignoran, sin dejar nada roto");
        File.Delete(archivo);
        SetS("localAuto", false); SetS("tipoLocal", 0); SetS("estereoAuto", false);
    }

    // ---------------------------------------------------------------- 3) LA CADENA Y «ESCUCHAR SIN MEJORAS»
    static object f2, track2; static string wav2;
    static void PreparaCadena(string dir)
    {
        f2 = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f2) == null) fi.SetValue(f2, Activator.CreateInstance(fi.FieldType, true));
        int fs = 44100; int n = fs * 3; float[] x = new float[2 * n]; Random r = new Random(7);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs;
            double v = 0.25 * Math.Sin(2 * Math.PI * 55 * t) + 0.15 * Math.Sin(2 * Math.PI * 440 * t) + 0.05 * (r.NextDouble() * 2 - 1);
            x[2 * i] = (float)v; x[2 * i + 1] = (float)(0.8 * v);
        }
        wav2 = Path.Combine(dir, "cadena.wav");
        using (WaveFileWriter w = new WaveFileWriter(wav2, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
        Type tt = TF.GetNestedType("Track", T); track2 = Activator.CreateInstance(tt, true); Set(track2, "Path", wav2);
    }
    static void AjustesCadena(bool sin)
    {
        Set(f2, "normalizeOn", true); Set(f2, "normTarget", 0.26f); Set(f2, "normStrength", 1f); Set(f2, "masterDb", 0f);
        Set(f2, "excAmount", 0.35f); Set(f2, "bassAuto", false); Set(f2, "bassAmount", 0.3f); Set(f2, "bassSongFactor", 1f);
        Set(f2, "sbrAllowed", true); Set(f2, "sbrOn", true); Set(f2, "declipOn", true);
        Set(f2, "crossfadeSec", 0.5f); Set(f2, "saltaSilencios", false);
        SetS("claridadLocal", true); SetS("partesSuaves", true); SetS("localAuto", false); SetS("presenciaSala", false);
        SetS("gravesLocalDb", 0); SetS("agudosLocalDb", 0); SetS("balanceDb", 0);
        Set(f2, "sinMejoras", sin);
        Set(f2, "sinMejorasHasta", sin ? DateTime.Now.AddSeconds(15) : DateTime.MinValue);
    }
    static object Arma()
    {
        object lector = Call(f2, "OpenAudio", wav2);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        return Call(f2, "ArmaCadena", track2, lector, false, 0.0);
    }
    static void SinMejoras()
    {
        AjustesCadena(false);
        object normal = Arma();
        Check((float)Get(Get(normal, "Tap"), "Excite") > 0.2f && (float)Get(Get(normal, "Bajo"), "Amount") > 0.2f && (bool)Get(Get(normal, "Sala"), "ConClaridad") && (bool)Get(Get(normal, "Sbr"), "Active"),
              "referencia: sin pedir nada, la cancion armada lleva su realce, su bajo, su claridad y su DSEE");
        AjustesCadena(true);
        SetS("gravesLocalDb", 3); SetS("agudosLocalDb", -2);
        object sin = Arma();
        SetS("gravesLocalDb", 0); SetS("agudosLocalDb", 0);
        Check((double)Get(Get(sin, "Sala"), "mezcla") == 0.0 && (double)Get(Get(sin, "Sala"), "GravesAhora") == 0.0 && (double)Get(Get(sin, "Sala"), "AgudosAhora") == 0.0, "y nace SIN la claridad ni los graves y agudos del local (sin rampa que los desvanezca desde el principio)");
        Check((float)Get(Get(sin, "Tap"), "Excite") == 0f, "armada mientras dura «Escuchar sin mejoras», nace sin realce");
        Check((float)Get(Get(sin, "Bajo"), "Amount") == 0f && !(bool)Get(Get(sin, "Sala"), "ConClaridad") && !(bool)Get(Get(sin, "Sbr"), "Active"), "ni bajo, ni claridad, ni DSEE");
        float[] eq = (float[])Get(Get(sin, "Tap"), "EqDb"); float mx = 0f; if (eq != null) foreach (float v in eq) mx = Math.Max(mx, Math.Abs(v));
        Check(mx == 0f, "y su EQ, plano");
    }

    // ---------------------------------------------------------------- 4) EL REPARTO DE LOS RANGOS
    static void Rangos()
    {
        MethodInfo txt = TF.GetMethod("TextoRangos", T, null, new Type[] { typeof(float[]), typeof(int) }, null);
        MethodInfo txt4 = TF.GetMethod("TextoRangos", T, null, new Type[] { typeof(float[]), typeof(int), typeof(float[]), typeof(int) }, null);
        Check(txt != null && txt4 != null && TF.GetMethod("RangosMedidos", T, null, new Type[] { typeof(int).MakeByRefType() }, null) != null, "esta el reparto de los rangos para rocola_calidad.txt");
        if (txt == null) return;
        string vacio = (string)txt.Invoke(null, new object[] { new float[0], 0 });
        Check(vacio.Contains("Aun no hay ninguna"), "sin medidas lo dice, sin dividir entre cero");
        float[] r = new float[100];
        for (int i = 0; i < r.Length; i++) r[i] = 4f + i * 0.2f;   // de 4 a 23,8 dB
        string t = (string)txt.Invoke(null, new object[] { r, 3 });
        Check(t.Contains("Medidas con picos: 100") && t.Contains("otras 3 son de una version vieja"), "cuenta las medidas con picos y las de version vieja");
        Check(t.Contains("menos de 6 dB") && t.Contains("16 dB o mas") && t.Contains("Mediana 14.0 dB"), "reparte por franjas y da la mediana");
        int altas = 0, muy = 0; foreach (float v in r) { if (v >= 12f) altas++; if (v >= 16f) muy++; }
        Check(t.Contains("(" + F(altas, "0") + " canciones") || t.Contains(altas + " canciones (" + F(100.0 * altas / 100, "0.0") + " %)"), "y cuantas pasarian los cortes de 12 y 16 dB (" + altas + " y " + muy + " de 100)");
        // con medidas de verdad: tres canciones, una vieja y una muda
        Type TInt = null; foreach (Type tp in TF.Assembly.GetTypes()) if (tp.Name == "SonoridadIntegrada") TInt = tp;
        object f = Codigo.FormSinEncender(TF);
        IDictionary dic = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TInt), new object[] { StringComparer.OrdinalIgnoreCase });
        dic["a"] = Activator.CreateInstance(TInt, T, null, new object[] { 3, -24f, 2f, 1800, -3f, -6f }, null);          // 18 dB
        dic["b"] = Activator.CreateInstance(TInt, T, null, new object[] { 3, -9f, 2f, 1800, -0.2f, -1f }, null);          // 8 dB
        dic["c"] = Activator.CreateInstance(TInt, T, null, new object[] { 1, -14f, 2f, 1800 }, null);                      // vieja
        dic["d"] = Activator.CreateInstance(TInt, T, null, new object[] { 3, float.NegativeInfinity, 2f, 1800, -80f, -80f }, null);   // muda
        Set(f, "integCache", dic);
        object[] a = { 0 };
        float[] medidos = (float[])TF.GetMethod("RangosMedidos", T, null, new Type[] { typeof(int).MakeByRefType() }, null).Invoke(f, a);
        Array.Sort(medidos);   // ya no vienen ordenados: se ordenan detras, al armar el texto
        Check(medidos.Length == 2 && Math.Abs(medidos[0] - 8f) < 0.01f && Math.Abs(medidos[1] - 18f) < 0.01f && (int)a[0] == 1, "de la tabla real saca los rangos (8 y 18 dB), cuenta la vieja y no cuenta la muda");
        // El rango de sonoridad (LRA, 23-10-2026): el reparto y las medidas de antes que aun no lo tienen.
        float[] lras = new float[50]; for (int i = 0; i < lras.Length; i++) lras[i] = i * 0.4f;   // de 0 a 19,6 LU
        string tl = (string)txt4.Invoke(null, new object[] { r, 0, lras, 7 });
        Check(tl.Contains("RANGO DE SONORIDAD") && tl.Contains("50 medidas") && tl.Contains("faltan 7") && tl.Contains("menos de 3 LU") && tl.Contains("20 LU o mas") && tl.Contains("Mediana 10.0 LU"), "el informe trae el reparto del rango de sonoridad y cuantas medidas de antes faltan por volver a medir");
        int p10 = 0, s14 = 0; foreach (float v in lras) { if (v >= 10f) p10++; if (v >= 14f) s14++; }
        Check(tl.Contains("(10 LU o mas): " + p10) && tl.Contains("(14 LU o mas): " + s14), "y cuantas llevarian la presencia del billar (" + p10 + ") y las partes suaves de la sala (" + s14 + ")");
        Check(((string)txt4.Invoke(null, new object[] { new float[0], 0, new float[0], 3 })).Contains("faltan 3"), "sin ninguna medida con picos tambien cuenta las que faltan");
        Type TSil = null; foreach (Type tp in TF.Assembly.GetTypes()) if (tp.Name == "MapaSilencios") TSil = tp;
        object mud = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { 180f });
        dic["e"] = Activator.CreateInstance(TInt, T, null, new object[] { 4, -20f, 2f, 1800, -8f, -12f, mud, 18.3f }, null);
        dic["f"] = Activator.CreateInstance(TInt, T, null, new object[] { 4, -12f, 2f, 1800, -1f, -2f, mud, 2.5f }, null);
        object[] b = { 0, null, 0 };
        float[] m2 = (float[])TF.GetMethod("RangosMedidos", T, null, new Type[] { typeof(int).MakeByRefType(), typeof(float[]).MakeByRefType(), typeof(int).MakeByRefType() }, null).Invoke(f, b);
        float[] l2 = (float[])b[1]; Array.Sort(l2);
        Check(m2.Length == 4 && l2.Length == 2 && Math.Abs(l2[0] - 2.5f) < 0.001f && Math.Abs(l2[1] - 18.3f) < 0.001f && (int)b[2] == 3, "de la tabla real saca tambien los LRA (2,5 y 18,3) y cuenta las 3 medidas validas de antes que aun no lo tienen");
    }

    // ---------------------------------------------------------------- 5) MEDIDAS PARA LAS PROPUESTAS (solo informan)
    const int FS = 44100;
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-12)); }
    // Cuatro canciones sinteticas de 24 s (estereo): lo que importa es el nivel en el tiempo, no el timbre.
    //   0 pop comprimido: nivel parejo de principio a fin y cresta baja.
    //   1 pop con intro suave: la misma compresion, pero 6 s de intro 22 dB mas bajita que el resto.
    //   2 rock con golpes: un lecho parejo y un golpe fuerte cada medio segundo (cresta alta, nada suave).
    //   3 balada: oleadas lentas entre muy bajito y medio, con notas de piano (mucho rango de nivel de verdad).
    static float[] Archetipo(int tipo)
    {
        int n = FS * 24; float[] x = new float[2 * n]; Random r = new Random(100 + tipo);
        double l1 = 0, l2 = 0, l3 = 0; float[] mono = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / FS, w = r.NextDouble() * 2 - 1;
            l1 += (w - l1) * 0.2; l2 += (w - l2) * 0.02; l3 += (w - l3) * 0.002;
            double rosa = 0.5 * l1 + 1.2 * l2 + 2.5 * l3;
            double tonal = 0.5 * Math.Sin(2 * Math.PI * 110 * t) + 0.3 * Math.Sin(2 * Math.PI * 220 * t + 1) + 0.2 * Math.Sin(2 * Math.PI * 440 * t + 2) + 0.1 * Math.Sin(2 * Math.PI * 880 * t + 3) + 0.05 * Math.Sin(2 * Math.PI * 2200 * t);
            double b = 0.55 * tonal + 0.45 * rosa, v;
            if (tipo == 0) v = 0.95 * Math.Tanh(1.1 * b);
            else if (tipo == 1) { double env = t < 6 ? 0.08 : (t < 10 ? 0.08 + (t - 6) / 4.0 * 0.92 : 1.0); v = env * 0.95 * Math.Tanh(1.1 * b); }
            else if (tipo == 2)
            {
                double tg = t % 0.5, golpe = tg < 0.02 ? 0.9 * (r.NextDouble() * 2 - 1) * (1 - tg / 0.02) : 0.0;
                v = 0.10 * b + golpe;
            }
            else
            {
                double env = 0.04 + 0.40 * Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 12.0 - 1.2), 2);
                double tp = t % 1.5, nota = 0.5 * Math.Exp(-tp / 0.3) * (Math.Sin(2 * Math.PI * 330 * t) + 0.4 * Math.Sin(2 * Math.PI * 660 * t));
                v = env * b + 0.6 * env * nota;
            }
            mono[i] = (float)Math.Max(-1, Math.Min(1, v));
        }
        for (int i = 0; i < n; i++) { x[2 * i] = mono[i]; x[2 * i + 1] = (float)(0.95 * mono[Math.Max(0, i - 5)]); }
        return x;
    }
    static object TrackDe(string wav) { object t = Activator.CreateInstance(TF.GetNestedType("Track", T), true); Set(t, "Path", wav); return t; }
    static object MideYApunta(object f, string wav)
    {
        object[] a = { wav, null };
        if (!(bool)Call(f, "MideIntegrada", a)) throw new Exception("no se pudo medir " + wav);
        IDictionary tab = (IDictionary)Get(f, "integCache");
        if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); Set(f, "integCache", tab); }
        tab[wav] = a[1];
        Call(f, "ActualizaFuenteMedida", wav, TF.GetMethod("TomaFuenteMedida", T).Invoke(null, null));
        return a[1];
    }
    static void Fabrica(object f)
    {
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 0f);
        Set(f, "excAmount", 0.35f); Set(f, "bassAuto", true); Set(f, "bassAmount", 0.3f); Set(f, "bassSongFactor", 1f);
        Set(f, "eqAuto", true); Set(f, "eqAmount", 0f); Set(f, "sbrAllowed", true); Set(f, "sbrOn", false); Set(f, "declipOn", true);
        Set(f, "crossfadeSec", 0.5f); Set(f, "saltaSilencios", false); Set(f, "sinMejoras", false); Set(f, "aLaMedida", false); Set(f, "quitaSiseo", false);
        SetS("claridadLocal", false); SetS("partesSuaves", false); SetS("localAuto", false); SetS("tipoLocal", 0); SetS("presenciaSala", false);
        SetS("gravesLocalDb", 0); SetS("agudosLocalDb", 0); SetS("balanceDb", 0); SetS("cortaSubgrave", false); SetS("estereoEstrecho", false); SetS("estereoAuto", false);
        SetS("canalMudo", true); SetS("quitaContinua", true);
    }
    static float[] Corre(object f, string wav) { return Corre(f, wav, null); }
    static float[] Corre(object f, string wav, Action<object> antes)
    {
        object lector = Call(f, "OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", TrackDe(wav), lector, false, 0.0);
        if (antes != null) antes(cad);
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        List<float> y = new List<float>(); float[] b = new float[4410 * 2];
        while (true) { int n = sal.Read(b, 0, b.Length); if (n <= 0) break; for (int i = 0; i < n; i++) y.Add(b[i]); }
        ((IDisposable)lector).Dispose();
        return y.ToArray();
    }
    static double Rms(float[] y, double desde, double hasta)
    {
        int a = (int)(desde * FS), b = Math.Min(y.Length / 2, (int)(hasta * FS)); double s = 0;
        for (int i = a; i < b; i++) s += (double)y[2 * i] * y[2 * i] + (double)y[2 * i + 1] * y[2 * i + 1];
        return Math.Sqrt(s / Math.Max(1, 2 * (b - a)));
    }
    static string Ganancia(float[] sin, float[] con, double a, double b) { return F(Db(Rms(con, a, b)) - Db(Rms(sin, a, b)), "+0.0;-0.0;0.0"); }
    // La mayor subida (dB) en una ventana de 1 s y la mayor bajada, de 'con' frente a 'sin'.
    static void Extremos(float[] sin, float[] con, out double sube, out double baja)
    {
        sube = -99; baja = 99;
        for (double t = 0; t + 1 <= 23.5; t += 0.5)
        {
            double a = Rms(sin, t, t + 1), b = Rms(con, t, t + 1);
            if (Db(a) < -50) continue;   // lo casi mudo no cuenta
            double d = Db(b) - Db(a); sube = Math.Max(sube, d); baja = Math.Min(baja, d);
        }
    }
    // Aproximacion del rango de sonoridad en el tiempo (LRA de la norma EBU 3342, sin ponderacion K): ventanas de 3 s cada 1 s,
    // sin las que quedan 20 dB bajo la media, y la distancia entre el percentil 10 y el 95.
    static double Lra(float[] x)
    {
        List<double> v = new List<double>(); double suma = 0;
        for (double t = 0; t + 3 <= 24.001; t += 1) { double r = Rms(x, t, t + 3); if (Db(r) < -70) continue; v.Add(Db(r)); suma += r * r; }
        if (v.Count < 3) return 0;
        double media = 10 * Math.Log10(suma / v.Count), corte = media - 20;
        List<double> g = new List<double>(); foreach (double d in v) if (d >= corte) g.Add(d);
        g.Sort();
        return g[(int)Math.Min(g.Count - 1, Math.Floor(g.Count * 0.95))] - g[(int)Math.Floor(g.Count * 0.10)];
    }

    // Energia de 8 kHz para arriba (dB): un paso-alto de Butterworth de cuarto orden a 8 kHz (dos biquads), del canal izquierdo.
    static double Agudos(float[] y, double desde)
    {
        double[] k = { 0.5412, 1.3066 }; double s = 0; int n = 0;
        double w0 = 2 * Math.PI * 8000 / FS, cw = Math.Cos(w0), sw = Math.Sin(w0);
        double[] x1 = new double[2], x2 = new double[2], y1 = new double[2], y2 = new double[2];
        double[] bb0 = new double[2], bb1 = new double[2], bb2 = new double[2], aa1 = new double[2], aa2 = new double[2];
        for (int e = 0; e < 2; e++)
        {
            double al = sw / (2 * k[e]); double a0 = 1 + al;
            bb0[e] = (1 + cw) / 2 / a0; bb1[e] = -(1 + cw) / a0; bb2[e] = (1 + cw) / 2 / a0; aa1[e] = -2 * cw / a0; aa2[e] = (1 - al) / a0;
        }
        int ini = (int)(desde * FS);
        for (int i = 0; i < y.Length / 2; i++)
        {
            double v = y[2 * i];
            for (int e = 0; e < 2; e++)
            {
                double o = bb0[e] * v + bb1[e] * x1[e] + bb2[e] * x2[e] - aa1[e] * y1[e] - aa2[e] * y2[e];
                x2[e] = x1[e]; x1[e] = v; y2[e] = y1[e]; y1[e] = o; v = o;
            }
            if (i >= ini) { s += v * v; n++; }
        }
        return 10 * Math.Log10(Math.Max(1e-20, s / Math.Max(1, n)));
    }
    class FuenteMem : ISampleProvider
    {
        readonly float[] x; int pos; readonly WaveFormat wf;
        public FuenteMem(float[] d, int fs, int ch) { x = d; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int n) { int k = Math.Min(n, x.Length - pos); if (k <= 0) return 0; Array.Copy(x, pos, b, o, k); pos += k; return k; }
    }
    static float[] Rosa(int segundos, int semilla)
    {
        int n = FS * segundos; float[] x = new float[2 * n]; Random r = new Random(semilla); double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
        for (int i = 0; i < n; i++)
        {
            double w = r.NextDouble() * 2 - 1;   // ruido rosa (el filtro de Paul Kellet)
            b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520; b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
            double p = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362) * 0.11; b6 = w * 0.115926;
            x[2 * i] = x[2 * i + 1] = (float)p;
        }
        return x;
    }

    static void MedidasIaYEq(string dir)
    {
        // I1 y el EQ automatico miran la MISMA carencia en los agudos (10 y 15,8 kHz): el EQ la sube hasta 4 dB por la fuerza, y I1 sube el
        // realce hasta un 25 % por esa misma carencia. Aqui: cuanto suma cada uno de 8 kHz para arriba, por la cadena de verdad.
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        string wav = Path.Combine(dir, "rosa.wav"); float[] x = Rosa(12, 5);
        for (int i = 0; i < x.Length; i++) x[i] *= 0.5f;
        using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(FS, 2))) w.WriteSamples(x, 0, x.Length);
        MideYApunta(f, wav);
        float[] eq = { 0, 0, 0, 0, 0, 0, 3.4f, 3.4f };   // 4 dB de carencia x 0,85 de fuerza (la fuerza de una biblioteca de 60 canciones medidas con el modelo)
        Fabrica(f); Set(f, "normalizeOn", false);
        double[] r = new double[4]; float[] exc = { 0.35f, 0.35f, 0.35f * 1.25f, 0.35f * 1.25f }; bool[] conEq = { false, true, false, true };
        for (int k = 0; k < 4; k++)
        {
            float e = exc[k]; bool q = conEq[k];
            r[k] = Agudos(Corre(f, wav, delegate (object cad)
            {
                object tap = Get(cad, "Tap"); Set(tap, "Excite", e);
                if (q) tap.GetType().GetProperty("EqDb").SetValue(tap, eq, null);
            }), 2);
        }
        Info("agudos de 8 kHz para arriba, frente a no tocar nada (realce 35 %): EQ solo " + F(r[1] - r[0], "+0.0;-0.0;0.0") + " dB;  I1 solo (realce +25 %) " + F(r[2] - r[0], "+0.0;-0.0;0.0")
             + " dB;  los dos " + F(r[3] - r[0], "+0.0;-0.0;0.0") + " dB  (por separado sumarian " + F((r[1] - r[0]) + (r[2] - r[0]), "+0.0;-0.0;0.0") + " dB)");
    }

    static void MedidasS3()
    {
        Type tm = TF.GetNestedType("GraveMono", T);
        int n = FS * 8; float[] x = new float[2 * n]; Random r = new Random(3);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / FS, inst = 0.3 * Math.Sin(2 * Math.PI * 440 * t) + 0.15 * (r.NextDouble() * 2 - 1), centro = 0.2 * Math.Sin(2 * Math.PI * 80 * t);
            x[2 * i] = (float)(inst + centro); x[2 * i + 1] = (float)centro;   // un instrumento todo a la izquierda y el bajo al centro
        }
        ISampleProvider g = (ISampleProvider)Activator.CreateInstance(tm, T, null, new object[] { new FuenteMem(x, FS, 2), 120f, 0.6f, 30f, true, false }, null);
        tm.GetMethod("SoloSiHaceFalta", T).Invoke(g, new object[] { true });
        FieldInfo anchoAhora = tm.GetField("AnchoAhora", T);
        float[] b = new float[FS / 10 * 2]; double t05 = -1, t99 = -1; int bloques = 0;
        while (g.Read(b, 0, b.Length) > 0)
        {
            bloques++; float a = (float)anchoAhora.GetValue(g);
            if (t05 < 0 && a < 0.97f) t05 = bloques * 0.1;
            if (t99 < 0 && a <= 0.601f) t99 = bloques * 0.1;
        }
        Info("S3 con una cancion de un instrumento a un lado: empieza a estrechar a los " + F(t05, "0.0") + " s y llega al 60 % a los " + F(t99, "0.0") + " s (cada cancion nueva sin fundido ni disco continuo arranca ancha)");
    }

    static void MedidasVideo()
    {
        object f = Codigo.FormSinEncender(TF);
        string v = @"C:\videos\medido.mp4";
        ((IDictionary)Get(f, "loudCache"))[v] = 0.13f;
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "videoAuto", false); Set(f, "videoVol", 40); Set(f, "masterDb", 0f);
        string r = "";
        foreach (float fuerza in new float[] { 1f, 0.5f, 0f })
        {
            Set(f, "normStrength", fuerza);
            int vol = (int)Call(f, "VideoVolumeFor", v);
            double cancion = Math.Pow(0.26 / 0.13, fuerza);   // lo que se le sube a una cancion de la misma sonoridad (sin topes)
            r += "  fuerza " + F(fuerza * 100, "0") + " %: video al " + vol + " (" + F(Db((double)vol / 40), "+0.0;-0.0;0.0") + " dB), una cancion igual " + F(Db(cancion), "+0.0;-0.0;0.0") + " dB;";
        }
        Info("Un video medido 6 dB bajo el objetivo, con el base en 40:" + r);
    }

    static void MedidasBajoYSubgrave()
    {
        Type tm = TF.GetNestedType("GraveMono", T), tb = TF.GetNestedType("DeepBassProvider", T);
        int n = FS * 6; float[] x = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / FS, v = 0.2 * Math.Sin(2 * Math.PI * 25 * t) + 0.25 * Math.Sin(2 * Math.PI * 55 * t) + 0.05 * Math.Sin(2 * Math.PI * 440 * t);
            x[2 * i] = x[2 * i + 1] = (float)v;
        }
        // A: el bajo psicoacustico directo. B: con el subgrave quitado (30 Hz) delante, como en la cadena con el local del billar.
        float[] a = Todo((ISampleProvider)Activator.CreateInstance(tb, T, null, new object[] { new FuenteMem(x, FS, 2), 0.3f }, null));
        float[] entradaB = Todo((ISampleProvider)Activator.CreateInstance(tm, T, null, new object[] { new FuenteMem(x, FS, 2), 0f, 1f, 30f, false, true }, null));
        ISampleProvider mono2 = (ISampleProvider)Activator.CreateInstance(tm, T, null, new object[] { new FuenteMem(x, FS, 2), 0f, 1f, 30f, false, true }, null);
        float[] bb = Todo((ISampleProvider)Activator.CreateInstance(tb, T, null, new object[] { mono2, 0.3f }, null));
        double ha = Armonicos(a, x), hb = Armonicos(bb, entradaB);
        Info("bajo psicoacustico (30 % de fabrica) sobre 25 + 55 Hz: lo que anade, de 95 a 500 Hz: " + F(ha, "0.0") + " dB sin quitar el subgrave y " + F(hb, "0.0") + " dB quitandolo (" + F(hb - ha, "+0.0;-0.0;0.0") + " dB)");
    }
    static float[] Todo(ISampleProvider p) { List<float> y = new List<float>(); float[] b = new float[4096]; int n; while ((n = p.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) y.Add(b[i]); return y.ToArray(); }
    // Energia (dB) de 95 a 500 Hz de la diferencia salida - entrada, del canal izquierdo, desde el segundo 1.
    static double Armonicos(float[] sal, float[] ent)
    {
        double s = 0; int n = 0; double lp1 = 0, lp2 = 0, hp = 0; double aL = 1 - Math.Exp(-2 * Math.PI * 500 / FS), aH = 1 - Math.Exp(-2 * Math.PI * 95 / FS);
        for (int i = FS; i < Math.Min(sal.Length, ent.Length) / 2; i++)
        {
            double d = sal[2 * i] - ent[2 * i]; lp1 += (d - lp1) * aL; lp2 += (lp1 - lp2) * aL; hp += (lp2 - hp) * aH; double y = lp2 - hp; s += y * y; n++;
        }
        return 10 * Math.Log10(Math.Max(1e-20, s / Math.Max(1, n)));
    }

    static void MedidasCoste()
    {
        Type ts = TF.GetNestedType("SalaLocal", T), tp = TF.GetNestedType("PresenciaSala", T);
        float[] x = Rosa(20, 9);
        double sinS = Tiempo(delegate { return (ISampleProvider)Activator.CreateInstance(ts, T, null, new object[] { new FuenteMem(x, FS, 2), null, 0.26f, true, false, 0 }, null); }, x.Length);
        double conS = Tiempo(delegate { return (ISampleProvider)Activator.CreateInstance(ts, T, null, new object[] { new FuenteMem(x, FS, 2), null, 0.26f, true, true, 0 }, null); }, x.Length);
        double pres = Tiempo(delegate { return (ISampleProvider)Activator.CreateInstance(tp, T, null, new object[] { new FuenteMem(x, FS, 2), null, 0.26f, true }, null); }, x.Length);
        Info("coste en Mono/Linux (no es el A4): la etapa del local con la claridad " + F(sinS, "0.0") + " ns por muestra; con las partes suaves ademas " + F(conS, "0.0") + "; la presencia sola " + F(pres, "0.0"));
    }
    static double Tiempo(Func<ISampleProvider> crea, int muestras)
    {
        double mejor = double.MaxValue; float[] b = new float[4096];
        for (int rep = 0; rep < 3; rep++)
        {
            ISampleProvider p = crea(); System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (p.Read(b, 0, b.Length) > 0) { }
            sw.Stop(); mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds * 1e6 / muestras);
        }
        return mejor;
    }

    static double SonoridadDe(object f, float[] y, string dir)
    {
        string wav = Path.Combine(dir, "salida_" + Guid.NewGuid().ToString("N") + ".wav");
        using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(FS, 2))) w.WriteSamples(y, 0, y.Length);
        object[] a = { wav, null };
        if (!(bool)Call(f, "MideIntegrada", a)) return double.NaN;
        double lk = (float)Get(a[1], "Lkfs");
        File.Delete(wav);
        return lk;
    }
    // El volumen parejo se calcula sobre la cancion ORIGINAL; lo que se pone detras (local, graves, claridad...) puede moverlo. Cuanto, en LU.
    static void MedidasDeriva(string dir)
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        int[] tipos = { 0, 3 }; string[] wavs = new string[2];
        for (int k = 0; k < 2; k++)
        {
            wavs[k] = Path.Combine(dir, "deriva" + k + ".wav"); float[] x = Archetipo(tipos[k]);
            using (WaveFileWriter w = new WaveFileWriter(wavs[k], WaveFormat.CreateIeeeFloatWaveFormat(FS, 2))) w.WriteSamples(x, 0, x.Length);
            MideYApunta(f, wavs[k]);
        }
        string[] cfg = { "de fabrica", "modo del local: billar", "modo del local: sala buena", "modo del local: bocinas chicas", "claridad", "graves del local +6 dB", "agudos del local +6 dB", "subgrave fuera", "bajo (graves) a 60 %", "presencia en sala ruidosa" };
        double[,] lk = new double[cfg.Length, 2];
        for (int c = 0; c < cfg.Length; c++)
            for (int k = 0; k < 2; k++)
            {
                Fabrica(f);
                switch (c)
                {
                    case 1: SetS("localAuto", true); SetS("tipoLocal", 0); break;
                    case 2: SetS("localAuto", true); SetS("tipoLocal", 1); break;
                    case 3: SetS("localAuto", true); SetS("tipoLocal", 2); break;
                    case 4: SetS("claridadLocal", true); break;
                    case 5: SetS("gravesLocalDb", 6); break;
                    case 6: SetS("agudosLocalDb", 6); break;
                    case 7: SetS("cortaSubgrave", true); break;
                    case 8: Set(f, "bassAuto", false); Set(f, "bassAmount", 0.6f); break;
                    case 9: SetS("presenciaSala", true); break;
                }
                lk[c, k] = SonoridadDe(f, Corre(f, wavs[k]), dir);
            }
        Info("sonoridad que sale (LU frente a de fabrica):  pop comprimido / balada");
        for (int c = 1; c < cfg.Length; c++)
            Info("  " + cfg[c].PadRight(32) + F(lk[c, 0] - lk[0, 0], "+0.0;-0.0;0.0").PadLeft(6) + " /" + F(lk[c, 1] - lk[0, 1], "+0.0;-0.0;0.0").PadLeft(6));
        Fabrica(f);
    }

    static void MedidasLocal(string dir)
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        string[] nombres = { "pop comprimido", "pop con intro suave", "rock con golpes", "balada" };
        Info("cancion                sonoridad    P99  rango(P99-son.)  LRA aprox.  LRA medido | pasa 12 / 16 dB | partes suaves: sube hasta / baja hasta | presencia: sube hasta / baja hasta");
        for (int tipo = 0; tipo < 4; tipo++)
        {
            float[] x = Archetipo(tipo);
            string wav = Path.Combine(dir, "arq" + tipo + ".wav");
            using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(FS, 2))) w.WriteSamples(x, 0, x.Length);
            object med = MideYApunta(f, wav);
            float lk = (float)Get(med, "Lkfs"), p99 = (float)Get(med, "P99Db");
            float rango = (float)TF.GetMethod("RangoDe", T).Invoke(null, new object[] { med });
            Fabrica(f); float[] base_ = Corre(f, wav);
            Fabrica(f); SetS("partesSuaves", true); float[] suaves = Corre(f, wav);
            Fabrica(f); SetS("presenciaSala", true); float[] pres = Corre(f, wav);
            double s1, s2, p1, p2; Extremos(base_, suaves, out s1, out s2); Extremos(base_, pres, out p1, out p2);
            Info(nombres[tipo].PadRight(22) + F(lk, "0.0").PadLeft(7) + F(p99, "0.0").PadLeft(8) + F(rango, "0.0").PadLeft(12) + F(Lra(x), "0.0").PadLeft(14) + F(Convert.ToSingle(med.GetType().GetField("Lra", T).GetValue(med)), "0.0").PadLeft(11) + "     |   " + (rango >= 12f ? "si" : "no") + " / " + (rango >= 16f ? "si" : "no")
                 + "        |   " + F(s1, "+0.0;-0.0;0.0").PadLeft(5) + " / " + F(s2, "+0.0;-0.0;0.0").PadLeft(5) + " dB                |   " + F(p1, "+0.0;-0.0;0.0").PadLeft(5) + " / " + F(p2, "+0.0;-0.0;0.0").PadLeft(5) + " dB");
        }
        Fabrica(f);
    }

    // ---------------------------------------------------------------- 2) LA VENTANA
    static Form dlg;
    static Control Busca(Control raiz, string nombre) { Control[] c = raiz.Controls.Find(nombre, true); return c.Length > 0 ? c[0] : null; }
    static Control PorTexto(Control p, string tipo, string prefijo)
    {
        foreach (Control c in p.Controls)
        {
            if (c.GetType().Name == tipo && c.Text != null && c.Text.StartsWith(prefijo)) return c;
            Control r = PorTexto(c, tipo, prefijo); if (r != null) return r;
        }
        return null;
    }
    static void Marca(Control sw, bool on) { sw.GetType().GetProperty("Checked").SetValue(sw, on, null); }
    static bool Marcado(Control sw) { return (bool)sw.GetType().GetProperty("Checked").GetValue(sw, null); }
    static string Falta(Control sw) { return (string)sw.GetType().GetField("Necesita").GetValue(sw); }

    static void MiraVentana(object form)
    {
        Control s3 = Busca(dlg, "SonidoEstereoAuto"), local = Busca(dlg, "SonidoLocalAuto"), estrecho = PorTexto(dlg, "Interruptor", "Estereo al ");
        Check(s3 != null && local != null && estrecho != null, "la ventana trae S3, el modo del local y el estereo estrecho");
        if (s3 == null || local == null || estrecho == null) return;
        // S3: solo actua con el estereo estrecho; con el modo del local manda ese modo.
        Check(!s3.Enabled && Falta(s3).Contains("Estereo al"), "S3 sin el estereo estrecho: en gris y dice cual le falta");
        Marca(estrecho, true);
        Check(s3.Enabled, "con el estereo estrecho a mano encendido, S3 manda");
        Marca(local, true);
        Check(!s3.Enabled && Falta(s3).Contains("modo automatico del local"), "con el modo automatico del local encendido, S3 queda en gris diciendo que decide ese modo (antes seguia como si mandara)");
        Marca(estrecho, false);
        Check(!s3.Enabled && Falta(s3).Contains("modo automatico del local") && !Falta(s3).Contains("Estereo al"), "y sin el estereo a mano el motivo sigue siendo el del modo del local, no que falte el estereo");
        Marca(local, false);
        Check(!s3.Enabled && Falta(s3).Contains("Estereo al"), "al apagar el modo del local vuelve el motivo de siempre");
        Marca(estrecho, true);
        Check(s3.Enabled, "y lo guardado en el estereo a mano sigue ahi");
        Marca(estrecho, false);

        // La barra de los videos: en gris solo si el automatico de verdad elige.
        Control nivelar = PorTexto(dlg, "Interruptor", "Nivelar el volumen"), barra = null, auto = null;
        if (nivelar != null)
        {   // en la tarjeta del volumen: la cuarta barra es la de los videos y su "Auto" es el unico interruptor con ese texto
            int k = 0;
            foreach (Control c in nivelar.Parent.Controls)
            {
                if (c.GetType().Name == "Deslizador" && k++ == 3) barra = c;
                if (c.GetType().Name == "Interruptor" && c.Text == "Auto") auto = c;
            }
        }
        Check(barra != null && auto != null && nivelar != null, "la ventana trae la barra de los videos, su Auto y el nivelado");
        if (barra == null || auto == null || nivelar == null) return;
        Check(Marcado(auto) && Marcado(nivelar), "de fabrica: Auto y nivelado encendidos");
        Check(barra.Enabled, "sin videos medidos (Windows XP no los mide) el automatico no elige nada: la barra manda y NO esta en gris");
        object f = form;
        // Ocho videos medidos: ahora si elige el programa.
        Type tAlbum = TF.GetNestedType("Album", T);
        IList albumes = (IList)Get(f, "videoAlbums");
        object alb = Activator.CreateInstance(tAlbum, true);
        IList videos = (IList)alb.GetType().GetField("VideoFiles").GetValue(alb);
        IDictionary loud = (IDictionary)Get(f, "loudCache");
        for (int i = 0; i < 8; i++) { string p = @"C:\videos\v" + i + ".mp4"; videos.Add(p); loud[p] = 0.1f + 0.02f * i; }
        albumes.Add(alb);
        Call(f, "InvalidateVideoMeasurements");
        Marca(auto, false); Marca(auto, true);   // se repasan los grises
        Check(!barra.Enabled, "con ocho videos medidos, Auto y nivelado, el programa elige y la barra va en gris");
        Marca(nivelar, false);
        Check(barra.Enabled, "sin nivelar no hay nada que emparejar (VideoBase devuelve la barra): la barra manda y no esta en gris");
        Marca(nivelar, true);
        Check(!barra.Enabled, "al volver a nivelar, otra vez en gris");
        Marca(auto, false);
        Check(barra.Enabled, "a mano la barra manda");
        Marca(auto, true);

        // I1, «a la medida» de la IA: sin el modelo del EQ no cambia nada.
        Control medida = Busca(dlg, "SonidoIaMedida");
        Check(medida != null, "la ventana trae «a la medida»");
        if (medida != null)
        {
            object eqNet = GetS("EqNet"); bool modelo = (bool)eqNet.GetType().GetField("Loaded").GetValue(eqNet);
            Check(modelo || !medida.Enabled, "sin el modelo del EQ cargado, «a la medida» va en gris (no puede actuar)");
            Label aviso = null;
            foreach (Label l in Todas<Label>(dlg)) if (l.Text != null && l.Text.StartsWith("Necesita: el modelo del EQ")) aviso = l;
            Check(modelo || aviso != null, "y dice cual: el modelo del EQ");
            Marca(medida, true);
            Check(!modelo ? Marcado(medida) : true, "lo guardado se conserva aunque este en gris");
            Marca(medida, false);
        }
    }
    static List<C> Todas<C>(Control raiz) where C : Control
    {
        List<C> r = new List<C>();
        foreach (Control c in raiz.Controls) { if (c is C) r.Add((C)c); r.AddRange(Todas<C>(c)); }
        return r;
    }

    static void Ventana(string dir)
    {
        object f = Codigo.FormSinEncender(TF);
        DynamicMethod dm = new DynamicMethod("BaseF3", typeof(void), new Type[] { typeof(object) }, typeof(ModosAuto), true);
        ILGenerator il = dm.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, typeof(Form));
        il.Emit(OpCodes.Call, typeof(Form).GetConstructor(Type.EmptyTypes)); il.Emit(OpCodes.Ret);
        dm.Invoke(null, new object[] { f });
        TF.GetMethod("BuildUi", T).Invoke(f, null);
        try { TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null); } catch { }
        SetS("localAuto", false); SetS("tipoLocal", 0); SetS("estereoEstrecho", false); SetS("estereoAuto", false);
        Set(f, "videoAuto", true); Set(f, "normalizeOn", true); Set(f, "videoVol", 85);
        Timer t = new Timer(); t.Interval = 400; int n = 0;
        t.Tick += delegate
        {
            n++;
            foreach (Form o in Application.OpenForms) if (!ReferenceEquals(o, f)) { dlg = o; break; }
            if (dlg == null) { if (n > 30) { t.Stop(); Check(false, "la ventana de ajustes se abrio"); } return; }
            t.Stop(); dlg.Location = new Point(0, 0);
            try { MiraVentana(f); }
            catch (Exception ex) { Check(false, "la ventana se pudo revisar: " + ex.GetType().Name + " " + ex.Message); }
            dlg.Close();
        };
        t.Start();
        TF.GetMethod("VentanaAjustes", T).Invoke(f, null);
        t.Stop(); t.Dispose();
    }

    [STAThread] static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_modosauto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        bool soloMedidas = args.Length > 1 && args[1] == "medidas";
        try
        {
            if (soloMedidas) { Console.WriteLine("\n  5) MEDIDAS PARA LAS PROPUESTAS (solo informan)\n"); MedidasLocal(dir); MedidasIaYEq(dir); MedidasDeriva(dir); MedidasS3(); MedidasVideo(); MedidasBajoYSubgrave(); MedidasCoste(); return 0; }
            Console.WriteLine("\n  1) LA CONFIGURACION DE LOS MODOS AUTOMATICOS\n");
            Configuracion(dir);
            Console.WriteLine("\n  2) «ESCUCHAR SIN MEJORAS» Y LA CANCION QUE SE ARMA MIENTRAS DURA\n");
            PreparaCadena(dir);
            SinMejoras();
            Console.WriteLine("\n  3) EL REPARTO DE LOS RANGOS\n");
            Rangos();
            Console.WriteLine("\n  4) LA VENTANA: LOS GRISES DICEN LA VERDAD\n");
            Ventana(dir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

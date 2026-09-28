using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
// LAS TABLAS GRANDES, SIN REESCRIBIRLAS ENTERAS (DiarioTablas.cs).
//
// Las tablas de volumen, EQ y sonoridad integrada se guardaban ENTERAS cada 25 canciones medidas. Ahora se anade al
// final solo lo nuevo o lo que cambio, con el formato de siempre, y al leer manda la ULTIMA linea de cada cancion. Aqui
// se mira, con la rocola de verdad:
//   1) el volumen: la primera vez entera y con los bytes de siempre; despues solo lo nuevo; sin cambios, nada; entera
//      si se quita una cancion o si alguien toco el archivo; compactar cuando hay demasiadas repetidas; y que al leer
//      sale exactamente lo que habia en memoria (por el arranque y por el camino de respaldo);
//   2) el apagon: una ultima linea a medias o ceros al final no se leen; lo completo de antes, si;
//   3) el EQ y la integrada, lo mismo (la integrada conserva su cabecera y los registros de una version posterior);
//   4) cuanto se escribe en la primera medicion de 20.000 canciones (medido aqui) contra lo de antes (calculado);
//   5) el codigo: los tres guardados pasan por el diario, y lo anadido se baja al disco de verdad.
class Diario
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static Type TF, TS;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }

    static object FormNueva()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Set(f, "startupReadPending", false);
        return f;
    }
    static void Espera(object f) { Codigo.EsperaGuardado(TF, f); }
    static object DiarioDe(object f, string campo) { return Get(f, campo); }
    static int Cuenta(object f, string campo, string que)
    {
        object d = DiarioDe(f, campo);
        return d == null ? -1 : (int)d.GetType().GetField(que, T).GetValue(d);
    }
    static long Bytes(object f, string campo)
    {
        object d = DiarioDe(f, campo);
        return d == null ? -1 : (long)d.GetType().GetField("Bytes", T).GetValue(d);
    }

    // ------------------------------- volumen -------------------------------
    static string FilaVol(float rms, float du, string ruta)
    {
        return rms.ToString("0.######", CI) + "|" + du.ToString("0.##", CI) + "|" + ruta + "\r\n";
    }
    static string EnteraVol(Dictionary<string, float> loud, Dictionary<string, float> dur)
    {
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<string, float> kv in loud) { float du; if (!dur.TryGetValue(kv.Key, out du)) du = 0f; sb.Append(FilaVol(kv.Value, du, kv.Key)); }
        return sb.ToString();
    }
    static string Ruta(int i) { return "D:\\Musica\\Artista " + (i % 97) + "\\Disco de ejemplo " + (i % 13) + "\\" + (i % 20 + 1) + " - Cancion numero " + i + ".mp3"; }
    static string Lee(string p) { return File.Exists(p) ? Encoding.UTF8.GetString(File.ReadAllBytes(p)) : null; }
    static int Lineas(string p) { string s = Lee(p); if (s == null) return 0; int n = 0; foreach (char c in s) if (c == '\n') n++; return n; }

    // Lo que dice el archivo, leido por la rocola por los dos caminos (el del arranque y el de respaldo).
    static Dictionary<string, float>[] LeeVolumen(string ruta, bool arranque)
    {
        Dictionary<string, float> l = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (arranque)
        {
            object data = Arranque(ruta, 0);
            Type tD = TF.GetNestedType("StartupData", T);
            l = (Dictionary<string, float>)tD.GetField("Loud", T).GetValue(data);
            d = (Dictionary<string, float>)tD.GetField("Dur", T).GetValue(data);
        }
        else Call(null, "ReadLoudCache", ruta, l, d);
        return new Dictionary<string, float>[] { l, d };
    }
    static object Arranque(string ruta, int tabla)
    {
        Type tLoad = TF.GetNestedType("StartupLoad", T);
        string dir = Path.GetDirectoryName(ruta);
        string[] rutas = new string[8];
        for (int i = 0; i < 8; i++) rutas[i] = Path.Combine(dir, "no_esta_" + i + ".txt");
        rutas[tabla] = ruta;
        object load = Activator.CreateInstance(tLoad, true);
        tLoad.GetField("Paths", T).SetValue(load, rutas);
        return Call(null, "ReadStartupData", load);
    }
    static bool MismoVolumen(Dictionary<string, float> l1, Dictionary<string, float> d1, Dictionary<string, float> l2, Dictionary<string, float> d2)
    {
        if (l1.Count != l2.Count) return false;
        foreach (KeyValuePair<string, float> kv in l1)
        {
            float v2; if (!l2.TryGetValue(kv.Key, out v2)) return false;
            float a, b; if (!d1.TryGetValue(kv.Key, out a)) a = 0f; if (!d2.TryGetValue(kv.Key, out b)) b = 0f;
            if (FilaVol(kv.Value, a, kv.Key) != FilaVol(v2, b, kv.Key)) return false;
        }
        return true;
    }

    static void Volumen(string dir)
    {
        Console.WriteLine("\n  1) EL VOLUMEN (rocola_volumen.txt)\n");
        string ruta = (string)Call(FormNueva(), "LoudFile");
        object f = FormNueva();
        Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Random rnd = new Random(7);
        for (int i = 0; i < 300; i++) { loud[Ruta(i)] = (float)(0.01 + rnd.NextDouble() * 0.2); if (i % 5 != 0) dur[Ruta(i)] = (float)(120 + rnd.NextDouble() * 200); }
        Set(f, "loudCache", loud); Set(f, "durCache", dur);
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == EnteraVol(loud, dur) && Cuenta(f, "diarioVolumen", "Enteros") == 1,
              "la primera vez de la sesion: entera, con los mismos bytes de siempre");

        string antes = Lee(ruta);
        StringBuilder nuevas = new StringBuilder();
        for (int i = 300; i < 325; i++) { loud[Ruta(i)] = 0.05f + i * 1e-4f; dur[Ruta(i)] = 200f + i; nuevas.Append(FilaVol(loud[Ruta(i)], dur[Ruta(i)], Ruta(i))); }
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == antes + nuevas && Cuenta(f, "diarioVolumen", "Anadidos") == 1 && Cuenta(f, "diarioVolumen", "Enteros") == 1,
              "25 canciones nuevas: solo esas 25 lineas, anadidas al final (lo de antes, intacto)");

        antes = Lee(ruta);
        loud[Ruta(3)] = 0.123f; loud[Ruta(150)] = 0.0456f; dur[Ruta(77)] = 999.5f;   // la 77 cambia SOLO de duracion
        Call(f, "SaveLoudCache"); Espera(f);
        string ahora = Lee(ruta);
        Check(ahora != null && ahora.StartsWith(antes) && ahora.Substring(antes.Length) ==
              FilaVol(loud[Ruta(3)], dur.ContainsKey(Ruta(3)) ? dur[Ruta(3)] : 0f, Ruta(3)) + FilaVol(loud[Ruta(77)], dur[Ruta(77)], Ruta(77))
              + FilaVol(loud[Ruta(150)], dur.ContainsKey(Ruta(150)) ? dur[Ruta(150)] : 0f, Ruta(150)),
              "tres cambios: solo sus tres lineas (tambien la que cambio solo de duracion)");

        Dictionary<string, float>[] a = LeeVolumen(ruta, true), b = LeeVolumen(ruta, false);
        Check(MismoVolumen(loud, dur, a[0], a[1]) && MismoVolumen(loud, dur, b[0], b[1]),
              "al leerla (arranque y respaldo) manda la ultima linea de cada cancion: sale lo que hay en memoria");

        antes = Lee(ruta); DateTime t0 = File.GetLastWriteTimeUtc(ruta);
        int sin = Cuenta(f, "diarioVolumen", "SinCambios");
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == antes && Cuenta(f, "diarioVolumen", "SinCambios") == sin + 1 && File.GetLastWriteTimeUtc(ruta) == t0,
              "sin cambios: no se escribe nada");

        loud.Remove(Ruta(10)); dur.Remove(Ruta(10));
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == EnteraVol(loud, dur) && Cuenta(f, "diarioVolumen", "Enteros") == 2,
              "si se quita una cancion: entera y limpia (quitar no se puede decir anadiendo)");

        File.AppendAllText(ruta, "alguien escribio esto\r\n");
        loud[Ruta(20)] = 0.2f;
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == EnteraVol(loud, dur) && Cuenta(f, "diarioVolumen", "Enteros") == 3,
              "si alguien toco el archivo (un respaldo restaurado, un editor): entera");

        File.AppendAllText(ruta, "otra vez desde fuera\r\n");   // y ahora sin que cambie nada en memoria
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == EnteraVol(loud, dur) && Cuenta(f, "diarioVolumen", "Enteros") == 4,
              "tocado desde fuera aunque en memoria no cambio nada: se vuelve a dejar como esta en memoria (como siempre)");

        File.Delete(ruta);
        loud[Ruta(21)] = 0.21f;
        Call(f, "SaveLoudCache"); Espera(f);
        Check(Lee(ruta) == EnteraVol(loud, dur) && Cuenta(f, "diarioVolumen", "Enteros") == 5,
              "si el archivo desaparecio (borrado, disco cambiado): se escribe entero en ese mismo guardado, no se pierde nada");

        int n = loud.Count, maxLineas = 0, enterosAntes = Cuenta(f, "diarioVolumen", "Enteros");
        List<string> claves = new List<string>(loud.Keys);
        for (int vuelta = 0; vuelta < 8; vuelta++)
        {
            foreach (string k in claves) loud[k] = loud[k] * 1.01f;
            Call(f, "SaveLoudCache"); Espera(f);
            maxLineas = Math.Max(maxLineas, Lineas(ruta));
        }
        Check(Cuenta(f, "diarioVolumen", "Enteros") > enterosAntes && maxLineas <= n + n / 2 + 1000 + n,
              "con demasiadas repetidas se compacta: el archivo nunca pasa de " + (n + n / 2 + 1000 + n) + " lineas (llego a " + maxLineas + ")");
        a = LeeVolumen(ruta, true);
        Check(MismoVolumen(loud, dur, a[0], a[1]), "y despues de compactar y anadir, se sigue leyendo lo mismo que hay en memoria");

        // Otra sesion: empieza entera.
        object g = FormNueva();
        a = LeeVolumen(ruta, true);
        Set(g, "loudCache", a[0]); Set(g, "durCache", a[1]);
        Call(g, "SaveLoudCache"); Espera(g);
        Check(Cuenta(g, "diarioVolumen", "Enteros") == 1 && Lee(ruta) == EnteraVol(a[0], a[1]),
              "cada sesion empieza escribiendola entera: el archivo queda limpio, sin repetidas");
    }

    static void Apagon(string dir)
    {
        Console.WriteLine("\n  2) SI SE VA LA LUZ JUSTO AL ANADIR\n");
        string ruta = Path.Combine(dir, "apagon_volumen.txt");
        Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 40; i++) { loud[Ruta(i)] = 0.01f * (i + 1); dur[Ruta(i)] = 100f + i; }
        string sano = EnteraVol(loud, dur);

        File.WriteAllText(ruta, sano + "0.0123|240.5|D:\\Musica\\a medi");
        Dictionary<string, float>[] a = LeeVolumen(ruta, true), b = LeeVolumen(ruta, false);
        Check(MismoVolumen(loud, dur, a[0], a[1]) && MismoVolumen(loud, dur, b[0], b[1]),
              "una ultima linea a medias (sin salto de linea) no se lee; todo lo de antes, si");

        File.WriteAllText(ruta, sano + "0.5|200|" + Ruta(900) + "\r\n" + new string('\0', 700));
        a = LeeVolumen(ruta, true);
        bool conCeros = false; foreach (string k in a[0].Keys) if (k.IndexOf('\0') >= 0) conCeros = true;
        Check(!conCeros && a[0].Count == loud.Count + 1 && a[0].ContainsKey(Ruta(900)),
              "ceros al final (Windows alargo el archivo y no llego a escribirlo): no se leen; la linea completa de antes, si");

        File.WriteAllText(ruta, sano.Substring(0, 20) + new string('\0', 20) + sano.Substring(40));   // dentro de la primera linea
        a = LeeVolumen(ruta, true);
        conCeros = false; foreach (string k in a[0].Keys) if (k.IndexOf('\0') >= 0) conCeros = true;
        Check(!conCeros && a[0].Count == loud.Count - 1, "una linea con ceros en medio tampoco se lee (las demas si)");

        Type tl = TF.GetNestedType("LineasSanas", T);
        string leido;
        using (TextReader lr = (TextReader)Activator.CreateInstance(tl, T, null, new object[] { new StringReader("uno\r\ndos\r\ntr"), true }, null)) leido = lr.ReadToEnd();
        string mixto;
        using (TextReader lr = (TextReader)Activator.CreateInstance(tl, T, null, new object[] { new StringReader("uno\r\n\0\0\r\ndos\r\n"), false }, null))
        { char c0 = (char)lr.Peek(); char c1 = (char)lr.Read(); mixto = c0.ToString() + c1 + "|" + lr.ReadLine() + "|" + lr.ReadToEnd(); }
        Check(leido == "uno\ndos\n" && mixto == "uu|no|dos\n",
              "leyendo a letras o de golpe (ReadToEnd) sale lo mismo que linea a linea: sin la ultima a medias ni las de ceros");

        File.WriteAllText(ruta, sano.TrimEnd('\r', '\n'));
        a = LeeVolumen(ruta, true);
        Check(a[0].Count == loud.Count - 1, "(la regla es la misma para todo el archivo: sin salto de linea al final, la ultima no cuenta)");
    }

    // ------------------------------- EQ -------------------------------
    static string FilaEq(float[] c, string ruta)
    {
        StringBuilder sb = new StringBuilder();
        for (int b = 0; b < 12; b++) { if (b > 0) sb.Append(';'); sb.Append(c[b].ToString("0.##", CI)); }
        return sb.Append('|').Append(ruta).Append("\r\n").ToString();
    }
    static string EnteraEq(Dictionary<string, float[]> eq)
    {
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<string, float[]> kv in eq) sb.Append(FilaEq(kv.Value, kv.Key));
        return sb.ToString();
    }
    static float[] Curva(int i, float mas)
    {
        float[] c = new float[12];
        for (int b = 0; b < 12; b++) c[b] = (float)Math.Round(-6 + ((i * 7 + b * 3) % 50) * 0.25 + mas, 2);
        c[8] = 16000f + i; c[9] = 0f; c[10] = 40f; c[11] = 0f;
        return c;
    }
    static void Eq(string dir)
    {
        Console.WriteLine("\n  3a) EL EQ (rocola_eq.txt)\n");
        object f = FormNueva();
        string ruta = (string)Call(f, "EqFile");
        Dictionary<string, float[]> eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 200; i++) eq[Ruta(i)] = Curva(i, 0f);
        Set(f, "eqCache", eq);
        Call(f, "SaveEqCache"); Espera(f);
        Check(Lee(ruta) == EnteraEq(eq) && Cuenta(f, "diarioEq", "Enteros") == 1, "la primera vez: entera, con los bytes de siempre");
        string antes = Lee(ruta);
        StringBuilder nuevas = new StringBuilder();
        for (int i = 200; i < 210; i++) { eq[Ruta(i)] = Curva(i, 0f); nuevas.Append(FilaEq(eq[Ruta(i)], Ruta(i))); }
        eq[Ruta(5)] = Curva(5, 1.5f);   // (la 5 sale en su sitio del recorrido: delante de las nuevas)
        Call(f, "SaveEqCache"); Espera(f);
        string ahora = Lee(ruta);
        Check(ahora != null && ahora.StartsWith(antes) && ahora.Substring(antes.Length) == FilaEq(eq[Ruta(5)], Ruta(5)) + nuevas,
              "una cambiada y diez nuevas: solo esas once lineas, al final");
        Dictionary<string, float[]> leida = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        int ver = 0;
        object[] args = new object[] { ruta, leida, ver };
        TF.GetMethod("ReadEqCache", T).Invoke(null, args);
        object data = Arranque(ruta, 1);
        Dictionary<string, float[]> alArrancar = (Dictionary<string, float[]>)TF.GetNestedType("StartupData", T).GetField("Eq", T).GetValue(data);
        Check(EnteraEq(leida) == EnteraEq(eq) && EnteraEq(alArrancar) == EnteraEq(eq),
              "al leerla (arranque y respaldo), manda la ultima: la 5 sale con su curva nueva");
    }

    // ------------------------------- integrada -------------------------------
    static object Integ(int alg, float lk, float pesos, int bloques) { return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, pesos, bloques }, null); }
    static string EnteraInteg(IDictionary tabla, List<string> ajenas, bool conAjenas)
    {
        StringWriter w = new StringWriter(CI); w.NewLine = "\r\n";
        w.Write((string)TF.GetField("IntegCabecera", T).GetValue(null)); w.Write("\r\n");
        MethodInfo fila = TF.GetMethod("EscribeIntegRegistro", T);
        foreach (DictionaryEntry e in tabla) fila.Invoke(null, new object[] { w, e.Key, e.Value });
        if (conAjenas) foreach (string s in ajenas) { w.Write(s); w.Write("\r\n"); }
        return w.ToString();
    }
    static IDictionary TablaInteg() { return (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase); }
    static void Integrada(string dir)
    {
        Console.WriteLine("\n  3b) LA SONORIDAD INTEGRADA (rocola_sonoridad.txt)\n");
        object f = FormNueva();
        string ruta = (string)Call(f, "IntegFile");
        IDictionary tab = TablaInteg();
        for (int i = 0; i < 150; i++) tab[Ruta(i)] = Integ(1, -20f + (i % 40) * 0.25f, 2f, 2000 + i);
        List<string> ajenas = new List<string> { "99|de una version futura|D:\\Musica\\futura.mp3" };
        Set(f, "integCache", tab); Set(f, "integAjenas", ajenas); Set(f, "integEstado", 1); Set(f, "integCambios", 1L);
        Call(f, "SaveIntegCache"); Espera(f);
        Check(Lee(ruta) == EnteraInteg(tab, ajenas, true) && Cuenta(f, "diarioInteg", "Enteros") == 1,
              "la primera vez: entera (cabecera, registros y el de una version posterior tal cual)");
        string antes = Lee(ruta);
        IDictionary nuevas = TablaInteg();
        for (int i = 150; i < 155; i++) { tab[Ruta(i)] = Integ(1, -14f, 2f, 3000 + i); nuevas[Ruta(i)] = tab[Ruta(i)]; }
        Set(f, "integCambios", 2L);
        Call(f, "SaveIntegCache"); Espera(f);
        string ahora = Lee(ruta);
        string cola = EnteraInteg(nuevas, ajenas, false);
        cola = cola.Substring(cola.IndexOf("\r\n") + 2);   // sin la cabecera
        Check(ahora != null && ahora.StartsWith(antes) && ahora.Substring(antes.Length) == cola && Cuenta(f, "diarioInteg", "Anadidos") == 1,
              "cinco nuevas: solo sus cinco lineas al final; la cabecera sigue arriba y el registro futuro, una vez");
        IDictionary leida = TablaInteg(); List<string> aj = new List<string>();
        int estado = (int)Call(null, "LeeIntegArchivo", ruta, null, leida, aj);
        Check(estado == 1 && EnteraInteg(leida, aj, true) == EnteraInteg(tab, ajenas, true),
              "al leerla sale lo mismo que hay en memoria, con el registro futuro conservado");
        File.WriteAllText(ruta, Lee(ruta) + "1|-11.4|2|2398|D:\\Musica\\a medi");   // apagon justo al anadir
        leida = TablaInteg(); aj = new List<string>();
        estado = (int)Call(null, "LeeIntegArchivo", ruta, null, leida, aj);
        Check(estado == 1 && leida.Count == tab.Count && !leida.Contains("D:\\Musica\\a medi"),
              "una ultima linea a medias tampoco se lee aqui (ni al arrancar, que lee por el mismo sitio)");
    }

    // ------------------------------- cuanto se escribe -------------------------------
    static void Cuanto(string dir)
    {
        Console.WriteLine("\n  4) CUANTO SE ESCRIBE EN LA PRIMERA MEDICION (20.000 canciones, de 25 en 25)\n");
        object f = FormNueva();
        string ruta = (string)Call(f, "LoudFile");
        try { File.Delete(ruta); File.Delete(ruta + ".bak"); } catch { }
        Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, float> dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Set(f, "loudCache", loud); Set(f, "durCache", dur);
        long antes = 0, tabla = 0;
        Random rnd = new Random(3);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20000; i++)
        {
            string r = Ruta(i);
            loud[r] = (float)(0.01 + rnd.NextDouble() * 0.2); dur[r] = (float)(120 + rnd.NextDouble() * 200);
            tabla += Encoding.UTF8.GetByteCount(FilaVol(loud[r], dur[r], r));
            if ((i + 1) % 25 == 0) { Call(f, "SaveLoudCache"); Espera(f); antes += tabla; }
        }
        sw.Stop();
        long ahora = Bytes(f, "diarioVolumen");
        Console.WriteLine("     tabla final " + (tabla / 1e6).ToString("0.0", CI) + " MB; antes se escribian " + (antes / 1e6).ToString("0", CI)
                          + " MB (la tabla entera en cada guardado, calculado); ahora " + (ahora / 1e6).ToString("0.0", CI)
                          + " MB (medido); " + (sw.ElapsedMilliseconds / 800.0).ToString("0.0", CI) + " ms por guardado aqui");
        Check(ahora > 0 && ahora <= tabla + 1024 && antes / Math.Max(1, ahora) >= 100,
              "se escribe la tabla una sola vez (" + (antes / Math.Max(1, ahora)) + " veces menos que antes)");
        Check(Lee(ruta) == EnteraVol(loud, dur), "y el archivo queda identico, byte a byte, al que se escribia entero");
    }

    static void Codigo_()
    {
        Console.WriteLine("\n  5) EL CODIGO\n");
        bool todos = true; string cuales = "";
        foreach (string m in new string[] { "SaveLoudCache", "SaveEqCache", "SaveIntegCache" })
        {
            List<string> ll = Codigo.ConAyudantes(TF, m);
            bool bien = Codigo.Llama(ll, "Guarda") && !Codigo.Llama(ll, "EscribeFlujoSeguro");
            if (!bien) { todos = false; cuales += " " + m; }
        }
        Check(todos, "los tres guardados pasan por el diario, no por la escritura entera directa" + (todos ? "" : " (falla:" + cuales + ")"));
        Type td = TF.GetNestedType("DiarioTabla", T);
        MethodInfo anade = td == null ? null : td.GetMethod("Anade", T);
        Check(anade != null && Codigo.Llama(anade, "FlushFileBuffers"), "lo anadido se baja al disco de verdad (FlushFileBuffers), como el guardado entero");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TS = asm.GetType("Rocola.SonoridadIntegrada", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_diario_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        try
        {
            carpeta.SetValue(null, dir);
            Volumen(dir);
            Apagon(dir);
            Eq(dir);
            Integrada(dir);
            Cuanto(dir);
            Codigo_();
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

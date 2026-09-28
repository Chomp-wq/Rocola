using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
// EL NIVELADO QUE MIRA LOS PICOS (Sonoridad.cs: PicosBloques y GananciaNivelado).
//
// El nivelado sube las canciones bajitas. Antes no miraba hasta donde llegaban sus picos, y en una cancion con golpes
// el limitador del final acababa aplastandolos casi todo el rato. Ahora:
//   1) la medida integrada (algoritmo 2) guarda el pico de la cancion y su P99 (el pico que solo supera el 1 % de
//      los bloques de 100 ms con sonido);
//   2) la subida no pasa de lo que deja al limitador quitar 3 dB en el P99 y 6 dB en el pico;
//   3) las muy fuertes se bajan un poco: la mitad de lo que pasen de 2 dB sobre el objetivo, como mucho 4 dB.
// Aqui se comprueba cada pieza y, con el limitador de la rocola de verdad, cuanto trabaja antes y despues en
// canciones de prueba con golpes. Son senales sinteticas: dicen que la cuenta hace lo que promete, no como suena
// tu musica (eso se oye en el bar).
class NivelPicos
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TP, TS, TM;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }

    // ---------------- las piezas ----------------
    static object Picos(int tasa, int canales) { return Activator.CreateInstance(TP, T, null, new object[] { tasa, canales }, null); }
    static void Procesa(object p, float[] d, int desde, int n) { TP.GetMethod("Procesa", T).Invoke(p, new object[] { d, desde, n }); }
    static float Pico(object p) { return (float)TP.GetProperty("PicoDb", T).GetValue(p, null); }
    static float P99(object p) { return (float)TP.GetProperty("P99Db", T).GetValue(p, null); }
    static float Lin(double db) { return (float)Math.Pow(10.0, db / 20.0); }
    static double Db(double lin) { return 20.0 * Math.Log10(lin); }
    static float Ganancia(float objetivo, float rms, float fuerza, float tope, bool hay, float pico, float p99)
    {
        return (float)Call(null, "GananciaNivelado", objetivo, rms, fuerza, tope, hay, pico, p99);
    }
    static float TopeEtapa(float rms)
    {
        float cap = (float)Math.Pow(10.0, (float)Call(null, "TierCapDb", rms) / 20.0);
        float max = (float)Get(null, "NormMaxBoost");
        return cap > max ? max : cap;
    }

    // Bloques de 100 ms a 44,1 kHz estereo, cada uno con su pico.
    static float[] Bloques(float[] picos)
    {
        int por = 4410 * 2;
        float[] d = new float[picos.Length * por];
        for (int b = 0; b < picos.Length; b++)
        {
            float v = picos[b];
            d[b * por + 17] = v; d[b * por + 18] = -v * 0.5f;   // el pico (en un canal), y algo mas bajo en el otro
            for (int i = 100; i < por; i += 97) d[b * por + i] = v * 0.1f;
        }
        return d;
    }
    static void ElMedidor()
    {
        Console.WriteLine("\n  1) LOS PICOS DE LA CANCION (PicosBloques)\n");
        // 1000 bloques a -20 dBFS y 5 golpes a -1: el P99 es -20 (los golpes son el 0,5 %), el pico -1.
        float[] pk = new float[1005];
        for (int i = 0; i < pk.Length; i++) pk[i] = Lin(-20);
        for (int i = 0; i < 5; i++) pk[100 + 200 * i] = Lin(-1);
        float[] d = Bloques(pk);
        object p = Picos(44100, 2); Procesa(p, d, 0, d.Length);
        Check(Math.Abs(Pico(p) + 1f) < 0.01f && P99(p) >= -20f && P99(p) <= -19.89f,
              "5 golpes a -1 dBFS entre 1000 bloques a -20: pico " + F(Pico(p), "0.00") + ", P99 " + F(P99(p), "0.00") + " (los golpes sueltos no mandan)");
        // 20 golpes (el 2 %): ya son parte de la cancion, y el P99 los ve.
        for (int i = 0; i < 20; i++) pk[3 + 50 * i] = Lin(-1);
        d = Bloques(pk); p = Picos(44100, 2); Procesa(p, d, 0, d.Length);
        Check(P99(p) >= -1.01f && P99(p) <= -0.89f, "con 20 golpes (el 2 % de los bloques) el P99 sube a los golpes: " + F(P99(p), "0.00"));
        float p99Todo = P99(p), picoTodo = Pico(p);
        // El P99 se redondea hacia ARRIBA a la decima, nunca por encima del pico.
        Check(P99(p) <= Pico(p) + 1e-4f, "el P99 nunca pasa del pico");
        float[] casi = new float[200]; for (int i = 0; i < casi.Length; i++) casi[i] = Lin(-20.05); casi[7] = Lin(-3);
        float[] dc = Bloques(casi); object pc = Picos(44100, 2); Procesa(pc, dc, 0, dc.Length);
        Check(P99(pc) >= -20.05f && P99(pc) <= -19.95f, "redondeado hacia ARRIBA: bloques a -20,05 dBFS dan un P99 de " + F(P99(pc), "0.00") + " (nunca menos de lo que hay)");
        // Leido a trozos de cualquier tamano (partiendo bloques y cuadros), lo mismo.
        bool igual = true;
        foreach (int trozo in new int[] { 1, 3, 441, 8819, 8820, 8821, 100000 })
        {
            object q = Picos(44100, 2);
            for (int i = 0; i < d.Length; i += trozo) Procesa(q, d, i, Math.Min(trozo, d.Length - i));
            if (Pico(q) != picoTodo || P99(q) != p99Todo) igual = false;
        }
        Check(igual, "leido a trozos de 1, 3, 441, 8819, 8820, 8821 y 100000 muestras sale exactamente lo mismo");
        // El silencio no cuenta para el P99: una cancion con media hora de silencio y un minuto de musica.
        float[] sil = new float[18000 + 100];   // la musica es el 0,55 % de los bloques: si el silencio contara, el P99 seria el silencio
        for (int i = 0; i < sil.Length; i++) sil[i] = (i < 100) ? Lin(-12) : Lin(-75);
        d = Bloques(sil); p = Picos(44100, 2); Procesa(p, d, 0, d.Length);
        Check(P99(p) >= -12f && P99(p) <= -11.89f, "los bloques de -60 dBFS o menos no cuentan: con 30 min casi mudos y 10 s de musica, P99 " + F(P99(p), "0.00"));
        // El bloque a medias del final cuenta.
        d = new float[4410 * 2 * 3 + 100]; for (int i = 0; i < 4410 * 2 * 3; i += 50) d[i] = Lin(-30); d[4410 * 2 * 3 + 50] = 0.5f;
        p = Picos(44100, 2); Procesa(p, d, 0, d.Length);
        Check(Math.Abs(Pico(p) - (float)Db(0.5)) < 0.01f && P99(p) > -6.1f, "el golpe del ultimo trozo de bloque cuenta (pico y P99: " + F(P99(p), "0.00") + "; sin el trozo seria -30)");
        // Nada que medir, un NaN o un disparate no rompen nada.
        p = Picos(44100, 2);
        Check(Pico(p) == -200f && P99(p) == -200f, "sin muestras: -200 dB (nada)");
        d = new float[] { float.NaN, 0.25f, float.NaN, -0.5f };
        p = Picos(44100, 2); Procesa(p, d, 0, d.Length);
        Check(Math.Abs(Pico(p) - (float)Db(0.5)) < 0.01f, "un NaN no cuenta como pico");
        d = new float[] { 1e6f, 0f };
        p = Picos(44100, 2); Procesa(p, d, 0, d.Length);
        Check(Pico(p) == 20f, "un disparate queda en +20 dBFS (el tope del registro)");
        // Dentro del medidor BS.1770: de paso, sin tocar su cuenta (la bateria 'sonoridad' vigila la cuenta).
        object m = Activator.CreateInstance(TM, T, null, new object[] { 44100, new double[] { 1.0, 1.0 } }, null);
        d = Bloques(pk);
        TM.GetMethod("Procesa", T).Invoke(m, new object[] { d, 0, d.Length });
        object mp = TM.GetField("Picos", T).GetValue(m);
        Check(mp != null && Pico(mp) == picoTodo && P99(mp) == p99Todo, "el medidor de la sonoridad lleva sus picos, iguales a los medidos aparte");
    }

    // ---------------- la tabla ----------------
    static object Integ(object[] a) { return Activator.CreateInstance(TS, T, null, a, null); }
    static float Campo(object s, string n) { return Convert.ToSingle(TS.GetField(n, T).GetValue(s)); }
    static bool ConPicos(object s) { return (bool)TS.GetProperty("ConPicos", T).GetValue(s, null); }
    static IDictionary TablaNueva() { return (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase); }
    static void LaTabla()
    {
        Console.WriteLine("\n  2) LA TABLA (rocola_sonoridad.txt, algoritmo 2)\n");
        int alg = (int)TM.GetField("Algoritmo", T).GetValue(null);
        Check(alg >= 2, "el algoritmo es 2 o posterior (lleva los picos): la tanda larga vuelve a medir las de 1");
        StringWriter w = new StringWriter();
        string cab = (string)Get(null, "IntegCabecera");
        w.Write(cab + "\r\n");
        Call(null, "EscribeIntegRegistro", w, "C:\\m\\a.mp3", Integ(new object[] { 2, -14.25f, 2f, 1800, -0.31f, -3.4f }));
        Call(null, "EscribeIntegRegistro", w, "C:\\m\\vieja.mp3", Integ(new object[] { 1, -11.5f, 2f, 900 }));
        Call(null, "EscribeIntegRegistro", w, "C:\\m\\muda.mp3", Integ(new object[] { 2, float.NegativeInfinity, 1f, 30, -200f, -200f }));
        string texto = w.ToString();
        Check(texto.Contains("2|-14.25|2|1800|-0.31|-3.4|C:\\m\\a.mp3") && texto.Contains("1|-11.5|2|900|C:\\m\\vieja.mp3"),
              "formato: algoritmo|LKFS|pesos|bloques|pico|P99|ruta (el 1 sigue sin picos, como siempre)");
        string futuro = "9|-9|2|100|x|y|z|C:\\m\\a.mp3";
        string rotas = "2|-10|2|100|-1|-2|\r\n2|-10|2|100|-3|-1|C:\\m\\p99mayor.mp3\r\n2|-10|2|100|NaN|-2|C:\\m\\nan.mp3\r\n2|-10|2|100|-1|C:\\m\\corta.mp3\r\n2|-10|2|100|25|-1|C:\\m\\alto.mp3\r\n";
        IDictionary t = TablaNueva(); List<string> aj = new List<string>();
        bool ok;
        using (StringReader r = new StringReader(texto + futuro + "\r\n" + rotas)) ok = (bool)Call(null, "LeeIntegRegistros", r, t, aj);
        object a = t.Contains("c:\\M\\A.mp3") ? t["c:\\M\\A.mp3"] : null, v = t.Contains("C:\\m\\vieja.mp3") ? t["C:\\m\\vieja.mp3"] : null;
        object mu = t.Contains("C:\\m\\muda.mp3") ? t["C:\\m\\muda.mp3"] : null;
        Check(ok && a != null && ConPicos(a) && Math.Abs(Campo(a, "PicoDb") + 0.31f) < 1e-4f && Math.Abs(Campo(a, "P99Db") + 3.4f) < 1e-4f
              && Math.Abs(Campo(a, "Lkfs") + 14.25f) < 1e-4f,
              "ida y vuelta de una de 2: LKFS, pico y P99");
        Check(v != null && !ConPicos(v) && Campo(v, "Algoritmo") == 1, "una de 1 se sigue leyendo, sin picos (se nivela como antes hasta que se vuelva a medir)");
        Check(mu != null && ConPicos(mu) && Campo(mu, "PicoDb") == -200f, "la cancion muda: picos -200 (nada)");
        Check(t.Count == 3 && aj.Count == 1 && aj[0] == futuro,
              "de un algoritmo posterior se guarda la linea intacta; sin ruta, P99 mayor que el pico, NaN, un campo de menos y +25 dBFS se descartan");
    }

    // ---------------- la ganancia ----------------
    static void LaGanancia()
    {
        Console.WriteLine("\n  3) LA GANANCIA (GananciaNivelado)\n");
        float obj = 0.26f;
        float techo = (float)Get(null, "TechoPicoReal");
        // Sin picos: exactamente lo de antes en las que se suben.
        bool comoAntes = true;
        for (double db = -40; db <= -12; db += 0.5)
        {
            float rms = Lin(db), cap = TopeEtapa(rms);
            float antes = (float)Math.Pow(obj / rms, 1f); if (antes < 1f) antes = 1f; else if (antes > cap) antes = cap;
            if (Ganancia(obj, rms, 1f, cap, false, 0f, 0f) != antes) comoAntes = false;
        }
        Check(comoAntes, "sin picos medidos, las bajitas se suben exactamente como antes");
        // Con picos: la subida deja al limitador 3 dB en el P99 y 6 dB en el pico, como mucho.
        float r18 = Lin(-18), c18 = TopeEtapa(r18);
        float g1 = Ganancia(obj, r18, 1f, c18, true, Lin(-0.5), Lin(-6));
        double grP99 = Db(Lin(-6) * g1 / techo), grPico = Db(Lin(-0.5) * g1 / techo);
        Check(grP99 <= 3.0001 && grPico <= 6.0001 && (Math.Abs(grP99 - 3) < 0.01 || Math.Abs(grPico - 6) < 0.01),
              "-18 dBFS con pico -0,5 y P99 -6: sube " + F(Db(g1), "0.0") + " dB; al limitador le quedan " + F(grP99, "0.0") + " dB en el P99 y " + F(grPico, "0.0") + " en el pico");
        float g2 = Ganancia(obj, r18, 1f, c18, true, Lin(-20), Lin(-22));
        Check(Math.Abs(g2 - Ganancia(obj, r18, 1f, c18, false, 0f, 0f)) < 1e-6f, "si los picos caben de sobra, sube lo de siempre (no se pierde nada)");
        float g3 = Ganancia(obj, r18, 1f, c18, true, Lin(0), Lin(0));
        Check(g3 >= 1f && Db(g3) > 0.5, "con los picos pegados al maximo aun sube algo (" + F(Db(g3), "0.0") + " dB) y nunca baja por culpa de los picos");
        // Las muy fuertes: la mitad de lo que pase de 2 dB, como mucho 4.
        double objDb = Db(obj);
        double a8 = Db(Ganancia(obj, Lin(-8), 1f, 1f, false, 0f, 0f)), a4 = Db(Ganancia(obj, Lin(-4), 1f, 1f, false, 0f, 0f));
        double a0 = Db(Ganancia(obj, Lin(3), 1f, 1f, false, 0f, 0f)), a10 = Db(Ganancia(obj, Lin(objDb + 1.9), 1f, 1f, false, 0f, 0f));
        Check(Math.Abs(a8 - (-(-8 - objDb - 2) * 0.5)) < 0.01 && Math.Abs(a4 - (-(-4 - objDb - 2) * 0.5)) < 0.01 && Math.Abs(a0 + 4) < 0.001 && Math.Abs(a10) < 1e-6,
              "las fuertes: a -8 dBFS baja " + F(-a8, "0.00") + " dB, a -4 " + F(-a4, "0.00") + ", a +3 el tope de 4; a 1,9 dB del objetivo, nada");
        // Nunca se invierte el orden: la que viene mas fuerte sale igual o mas fuerte.
        bool orden = true; double prev = -1e9;
        for (double db = -45; db <= 6; db += 0.1)
        {
            float rms = Lin(db);
            double sale = db + Db(Ganancia(obj, rms, 1f, TopeEtapa(rms), false, 0f, 0f));
            if (sale < prev - 1e-4) orden = false;
            prev = sale;
        }
        Check(orden, "de -45 a +6 dBFS, la que entra mas fuerte nunca sale mas bajita que otra");
        // Con la fuerza a la mitad, la bajada tambien es a medias (la sobra se cuenta con la fuerza).
        double m8 = Db(Ganancia(obj, Lin(-4), 0.5f, 1f, false, 0f, 0f));
        Check(m8 > a4 && m8 < 0, "con la fuerza a la mitad baja menos (" + F(-m8, "0.00") + " dB)");
        Check(Ganancia(obj, 0f, 1f, 4f, false, 0f, 0f) == 4f && Ganancia(obj, Lin(-20), 1f, 4f, true, float.NaN, float.NaN) >= 1f,
              "una muda o picos raros no dan NaN ni bajan");
    }

    // ---------------- subir todo ----------------
    static object Integ2(float lk, float pico, float p99) { return Integ(new object[] { 2, lk, 2f, 1000, pico, p99 }); }
    static void SubirTodo()
    {
        Console.WriteLine("\n  6) SUBIR TODO EL VOLUMEN: PAREJO Y CON TECHO (SubirTodo.cs)\n");
        MethodInfo techo = TF.GetMethod("TechoDeHolguras", T), holg = TF.GetMethod("HolguraDb", T);
        float[] h = new float[100]; for (int i = 0; i < 100; i++) h[i] = i < 10 ? 0f : 4f;
        float t1 = (float)techo.Invoke(null, new object[] { h, 100 });
        float[] h2 = new float[100]; for (int i = 0; i < 100; i++) h2[i] = i < 11 ? 0f : 4f;
        float t2 = (float)techo.Invoke(null, new object[] { h2, 100 });
        Check(t1 == 4f && t2 == 0f, "el techo deja fuera como mucho el 10 %: 10 de 100 sin holgura, +4 dB; 11 de 100, 0 dB");
        float[] h3 = new float[30]; for (int i = 0; i < 30; i++) h3[i] = 2.37f + i * 0.01f;
        Check((float)techo.Invoke(null, new object[] { h3, 30 }) == 2.3f, "y se redondea a la decima hacia abajo (2,40 da +2,3): nunca se pasa");
        float hol = (float)holg.Invoke(null, new object[] { 1f, Lin(-20), Lin(-24) });
        double esperado = Db(Math.Min((float)Get(null, "TechoPicoReal") * Lin(3) / Lin(-24), (float)Get(null, "TechoPicoReal") * Lin(6) / Lin(-20)));
        Check(Math.Abs(hol - esperado) < 0.01 && (float)holg.Invoke(null, new object[] { 8f, Lin(0), Lin(0) }) == 0f,
              "la holgura de una cancion es lo que le queda hasta el tope del limitador (" + F(hol, "0.0") + " dB); si ya esta en el tope, 0");
        // Con una biblioteca de mentira: 60 comprimidas (bajadas por el nivelado, con sitio) y 40 dinamicas (ya en su tope).
        object f = Codigo.FormSinEncender(TF);
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 6f);
        IDictionary tab = TablaNueva();
        for (int i = 0; i < 60; i++) tab["pop" + i + ".mp3"] = Integ2(-7f, 0f, -0.5f);
        for (int i = 0; i < 5; i++) tab["din" + i + ".mp3"] = Integ2(-20f, -0.5f, -6f);
        Set(f, "integCache", tab);
        float apl = (float)Call(f, "SubirAplicadoDb");
        Check(apl < 6f && apl > 0.5f, "biblioteca de 65 con 5 dinamicas (8 %): pides +6, se aplican +" + F(apl, "0.0") + " dB (lo que aguantan las comprimidas)");
        float mE = (float)Call(f, "MasterEfectivo");
        Check(Math.Abs(Db(mE) - apl) < 0.01, "y es lo mismo para TODAS las canciones: el volumen sigue parejo");
        for (int i = 5; i < 40; i++) tab["din" + i + ".mp3"] = Integ2(-20f, -0.5f, -6f);
        Set(f, "normTarget", 0.2601f);   // un ajuste distinto obliga a rehacer la cuenta (y las medidas nuevas, cada minuto)
        float apl2 = (float)Call(f, "SubirAplicadoDb");
        Check(apl2 == 0f, "con 40 dinamicas de 100 ya en su tope, no se sube nada: aplastaria a la mayoria (" + F(apl2, "0.0") + " dB)");
        Set(f, "masterDb", 0f);
        Check((float)Call(f, "SubirAplicadoDb") == 0f && (float)Call(f, "MasterEfectivo") == 1f, "en 0 dB no hace nada");
        object g = Codigo.FormSinEncender(TF);
        Set(g, "normalizeOn", true); Set(g, "normTarget", 0.26f); Set(g, "normStrength", 1f); Set(g, "masterDb", 6f);
        IDictionary poca = TablaNueva(); for (int i = 0; i < 19; i++) poca["d" + i + ".mp3"] = Integ2(-20f, -0.5f, -6f);
        poca["vieja.mp3"] = Integ(new object[] { 1, -20f, 2f, 1000 });
        Set(g, "integCache", poca);
        Check((float)Call(g, "SubirAplicadoDb") == 6f, "con menos de 20 canciones con los picos medidos no se sabe: se aplica lo pedido, como antes");
        // Al arrancar, la tabla se carga entera de golpe (sin contar como medida nueva): el techo se rehace ya.
        IDictionary cargada = TablaNueva(); for (int i = 0; i < 40; i++) cargada["d" + i + ".mp3"] = Integ2(-20f, -0.5f, -6f);
        Set(g, "integCache", cargada);
        Check((float)Call(g, "SubirAplicadoDb") == 0f, "al cargar la tabla del arranque el techo se rehace en el acto, sin esperar a medidas nuevas");
        // Las mudas no cuentan: 18 comprimidas y 3 dinamicas (14 % sin holgura) no suben, aunque haya 30 mudas.
        IDictionary conMudas = TablaNueva();
        for (int i = 0; i < 18; i++) conMudas["p" + i + ".mp3"] = Integ2(-7f, 0f, -0.5f);
        for (int i = 0; i < 3; i++) conMudas["d" + i + ".mp3"] = Integ2(-20f, -0.5f, -6f);
        for (int i = 0; i < 30; i++) conMudas["m" + i + ".mp3"] = Integ(new object[] { 2, float.NegativeInfinity, 2f, 100, -200f, -200f });
        Set(g, "integCache", conMudas);
        Check((float)Call(g, "SubirAplicadoDb") == 0f, "las canciones mudas no cuentan para el techo (con 30 mudas y 3 de 21 sin holgura, no se sube)");
        string txt = (string)Call(f, "TextoSubir", 6);
        Check(txt.StartsWith("Tu musica ya va al limite"), "si no aguanta nada, lo dice asi: \"" + txt.Replace("\n", " ") + "\"");
        Set(f, "masterDb", 6f); Set(f, "normTarget", 0.26f);
        for (int i = 5; i < 40; i++) tab.Remove("din" + i + ".mp3");
        string txt2 = (string)Call(f, "TextoSubir", 6);
        Check(txt2.StartsWith("Pides +6 dB; tu musica aguanta +") && txt2.Contains("se aplican +"), "Ctrl+F3 lo dice: \"" + txt2.Replace("\n", " ") + "\"");
        // Coste: 50 000 canciones.
        IDictionary grande = TablaNueva(); Random r = new Random(4);
        for (int i = 0; i < 50000; i++) grande["c" + i + ".mp3"] = Integ2(-6f - (float)r.NextDouble() * 18f, -(float)r.NextDouble() * 3f, -3f - (float)r.NextDouble() * 6f);
        object k = Codigo.FormSinEncender(TF);
        Set(k, "normalizeOn", true); Set(k, "normTarget", 0.26f); Set(k, "normStrength", 1f); Set(k, "masterDb", 6f); Set(k, "integCache", grande);
        Call(k, "SubirAplicadoDb"); Set(k, "normTarget", 0.2601f);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Call(k, "SubirAplicadoDb");
        sw.Stop();
        sw = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 1000; i++) Call(k, "SubirAplicadoDb"); sw.Stop();
        Info("rehacer el techo con 50 000 canciones: medido abajo; ya hecho, " + F(sw.Elapsed.TotalMilliseconds, "0.0") + " ms las 1000 veces");
        Set(k, "normTarget", 0.2602f);
        System.Diagnostics.Stopwatch s2 = System.Diagnostics.Stopwatch.StartNew(); Call(k, "SubirAplicadoDb"); s2.Stop();
        Check(s2.Elapsed.TotalMilliseconds < 400 && sw.Elapsed.TotalMilliseconds < 50, "50 000 canciones: el techo se rehace en " + F(s2.Elapsed.TotalMilliseconds, "0") + " ms (en la pantalla, al cambiar ajustes o cada minuto con medidas nuevas); guardado, casi nada");
        // Enchufado: los cuatro sitios que ponen el Master pasan por MasterEfectivo; ninguno pone MasterLin a pelo.
        bool todos = Codigo.Llama(TF.GetMethod("ArmaCadena", T), "MasterEfectivo") && Codigo.Llama(TF.GetMethod("AplicaAPreparada", T), "MasterEfectivo")
                     && Codigo.Llama(TF.GetMethod("ApplyNormNow", T), "MasterEfectivo") && Codigo.Llama(TF.GetMethod("AvanzaModoNoche", T), "MasterEfectivo");
        bool ningunoSolo = true;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            int lee, esc; Codigo.UsosDeCampo(m, "Master", out lee, out esc);
            if (esc > 0 && Codigo.Llama(m, "MasterLin")) ningunoSolo = false;
        }
        List<MethodBase> ven = Codigo.MetodosConAyudantes(TF, "VentanaAjustes");
        bool texto = false; foreach (MethodBase m in ven) if (Codigo.Llama(m, "TextoSubir")) texto = true;
        Check(todos && ningunoSolo && texto, "la cadena, la siguiente preparada, el ajuste en vivo y la rampa de la noche pasan por el techo; Ctrl+F3 lo explica");
    }

    // ---------------- conectado ----------------
    static void Conectado()
    {
        Console.WriteLine("\n  4) CONECTADO\n");
        MethodInfo ng = TF.GetMethod("NormGainFor", T);
        Check(Codigo.Llama(ng, "PicosParaNivelar") && Codigo.Llama(ng, "GananciaNivelado") && Codigo.Llama(ng, "TierCapDb"),
              "NormGainFor pide los picos y la cuenta a GananciaNivelado, con el tope de su etapa");
        int leePicos, escribePicos;
        Codigo.UsosDeCampo(TF.GetMethod("CierraIntegrada", T), "Picos", out leePicos, out escribePicos);
        Check(leePicos > 0 && Codigo.Llama(TF.GetMethod("CierraIntegrada", T), "get_P99Db"), "CierraIntegrada lee los picos del medidor");
        // Y de verdad: 2 s medidos se cierran con sus picos.
        object med = Activator.CreateInstance(TM, T, null, new object[] { 44100, new double[] { 1.0, 1.0 } }, null);
        float[] dos = new float[44100 * 2 * 2];
        for (int i = 0; i < 44100 * 2; i++) { float v = 0.1f * (float)Math.Sin(2 * Math.PI * 440 * i / 44100.0); dos[2 * i] = v; dos[2 * i + 1] = v; }
        dos[2 * 30000] = 0.5f;
        TM.GetMethod("Procesa", T).Invoke(med, new object[] { dos, 0, dos.Length });
        object[] ca = { med, 2.0, 44100, null };
        bool cerro = (bool)TF.GetMethod("CierraIntegrada", T).Invoke(null, ca);
        object si = ca[3];
        Check(cerro && ConPicos(si) && Math.Abs(Campo(si, "PicoDb") - (float)Db(0.5)) < 0.01f && Math.Abs(Campo(si, "P99Db") - (float)Db(0.5)) < 0.11f
              && Campo(si, "Algoritmo") >= 2,
              "CierraIntegrada guarda la medida con algoritmo 2 o posterior y sus picos (un golpe en 1 de 20 bloques es el 5 %: manda en el P99; pico " + (cerro ? F(Campo(si, "PicoDb"), "0.00") : "?") + ", P99 " + (cerro ? F(Campo(si, "P99Db"), "0.00") : "?") + ")");
        Check(Codigo.Llama(TM.GetMethod("Procesa", T), "Procesa"), "el medidor BS.1770 mide los picos en cada lectura");
        // De punta a punta: una cancion con integrada de 2 se sube menos que la misma con la de 1.
        object f = Codigo.FormSinEncender(TF);
        IDictionary tab = TablaNueva();
        tab["c2.mp3"] = Integ(new object[] { 2, -18f, 2f, 1000, -0.5f, -4f });
        tab["c1.mp3"] = Integ(new object[] { 1, -18f, 2f, 1000 });
        Set(f, "integCache", tab); Set(f, "normalizeOn", true); Set(f, "normStrength", 1f); Set(f, "normTarget", 0.26f);
        float gc2 = (float)Call(f, "NormGainFor", "c2.mp3"), gc1 = (float)Call(f, "NormGainFor", "c1.mp3");
        Check(gc2 < gc1 && gc2 >= 1f, "de punta a punta: con picos medidos se sube " + F(Db(gc2), "0.0") + " dB; con la medida de antes, " + F(Db(gc1), "0.0"));
        // La tanda larga vuelve a medir las de un algoritmo anterior al de ahora (con el 3, tambien las de 2).
        Type tt = TF.GetNestedType("Track", T);
        IList tracks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tt));
        foreach (string r in new string[] { "c1.mp3", "c2.mp3", "nueva.mp3" })
        {
            object tr = Activator.CreateInstance(tt, true);
            tt.GetField("Path", T).SetValue(tr, r);
            tracks.Add(tr);
        }
        Set(f, "tracks", tracks);
        List<string> pend = (List<string>)Call(f, "TandaIntegrada");
        int algAhora = (int)TM.GetField("Algoritmo", T).GetValue(null);
        Check(pend.Contains("c1.mp3") && pend.Contains("nueva.mp3") && pend.Contains("c2.mp3") == (algAhora > 2),
              "la tanda larga vuelve a medir las de un algoritmo anterior (y las nuevas), no las del de ahora");
    }

    // ---------------- con el limitador de verdad ----------------
    sealed class Fuente : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public Fuente(float[] x) { d = x; }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, n); p += n; return n; }
    }
    // Una cancion de 60 s: un colchon sostenido (ruido filtrado) y golpes graves que decaen, 'golpes' por segundo.
    static float[] Cancion(double picoDb, double golpes, int semilla)
    {
        int fr = 44100 * 60; float[] x = new float[fr * 2]; Random r = new Random(semilla);
        double lp = 0, lp2 = 0;
        for (int i = 0; i < fr; i++) { double w = r.NextDouble() * 2 - 1; lp += (w - lp) * 0.08; lp2 += (lp - lp2) * 0.3; x[2 * i] = (float)lp2; x[2 * i + 1] = (float)(lp2 * 0.9 + 0.1 * lp); }
        int per = (int)(44100 / golpes);
        for (int s = 0; s < fr; s += per)
            for (int k = 0; k < 4410 && s + k < fr; k++)
            {
                double e = Math.Exp(-k / 600.0), v = e * (Math.Sin(2 * Math.PI * 70 * k / 44100.0) * 0.7 + (r.NextDouble() * 2 - 1) * 0.3 * Math.Exp(-k / 150.0));
                x[2 * (s + k)] += (float)v; x[2 * (s + k) + 1] += (float)v;
            }
        double pk = 0; foreach (float v in x) pk = Math.Max(pk, Math.Abs(v));
        float kp = (float)(Math.Pow(10, picoDb / 20) / pk); for (int i = 0; i < x.Length; i++) x[i] *= kp;
        return x;
    }
    // Lo que hace el limitador con la cancion subida 'g': peor reduccion, y en que parte del tiempo pasa de 1 y de 3 dB.
    static void Limita(float[] x, float g, out double peor, out double mas1, out double mas3)
    {
        float[] y = new float[x.Length]; for (int i = 0; i < x.Length; i++) y[i] = x[i] * g;
        Type L = TF.GetNestedType("LimitadorPicoReal", T);
        object lim = Activator.CreateInstance(L, T, null, new object[] { new Fuente(y), (float)Get(null, "TechoPicoReal"), 3.0 }, null);
        MethodInfo rd = L.GetMethod("Read"); FieldInfo env = L.GetField("env", T);
        float[] b = new float[882]; int n1 = 0, n3 = 0, tot = 0; peor = 0;
        object[] args = { b, 0, 882 };
        while ((int)rd.Invoke(lim, args) > 0)
        {
            double gr = -Db((float)env.GetValue(lim)); tot++;
            if (gr > peor) peor = gr; if (gr > 1) n1++; if (gr > 3) n3++;
        }
        mas1 = n1 / (double)tot; mas3 = n3 / (double)tot;
    }
    static void ConElLimitador()
    {
        Console.WriteLine("\n  5) CON EL LIMITADOR DE LA ROCOLA (canciones de prueba de 60 s con golpes)\n");
        Info("cancion               |  antes: sube  peor   >1 dB  >3 dB  |  ahora: sube  peor   >1 dB  >3 dB");
        double[][] casos = { new double[] { -0.5, 2, 1 }, new double[] { -0.3, 4, 2 }, new double[] { -0.1, 8, 3 }, new double[] { -1, 1, 4 } };
        bool todas = true, menos = true;
        foreach (double[] c in casos)
        {
            float[] x = Cancion(c[0], c[1], (int)c[2]);
            double ss = 0; foreach (float v in x) ss += v * v; float rms = (float)Math.Sqrt(ss / x.Length);
            object p = Picos(44100, 2); Procesa(p, x, 0, x.Length);
            float cap = TopeEtapa(rms);
            float gA = Ganancia(0.26f, rms, 1f, cap, false, 0f, 0f), gN = Ganancia(0.26f, rms, 1f, cap, true, Lin(Pico(p)), Lin(P99(p)));
            double pA, a1, a3, pN, n1, n3;
            Limita(x, gA, out pA, out a1, out a3); Limita(x, gN, out pN, out n1, out n3);
            Info(F(Db(rms), "0.0") + " dBFS, pico " + F(c[0], "0.0") + "   |  " + F(Db(gA), "0.0").PadLeft(10) + F(pA, "0.0").PadLeft(6) + (a1 * 100).ToString("0").PadLeft(6) + " %" + (a3 * 100).ToString("0").PadLeft(5) + " %  |  "
                 + F(Db(gN), "0.0").PadLeft(10) + F(pN, "0.0").PadLeft(6) + (n1 * 100).ToString("0").PadLeft(6) + " %" + (n3 * 100).ToString("0").PadLeft(5) + " %");
            if (!(pN <= 6.3 && n3 <= 0.03)) todas = false;
            if (!(pN < pA && n1 <= a1)) menos = false;
        }
        Check(todas, "ahora el limitador no quita mas de ~6 dB en ninguna, y pasa de 3 dB como mucho el 3 % del tiempo");
        Check(menos, "y en todas trabaja menos que antes");
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TP = a.GetType("Rocola.PicosBloques", false);
        TS = a.GetType("Rocola.SonoridadIntegrada", false);
        TM = a.GetType("Rocola.MedidorBs1770", false);
        if (TP == null || TF.GetMethod("GananciaNivelado", T) == null) { Check(false, "no estan PicosBloques ni GananciaNivelado"); return 1; }
        try { ElMedidor(); } catch (Exception e) { Check(false, "el medidor: " + e); }
        try { LaTabla(); } catch (Exception e) { Check(false, "la tabla: " + e); }
        try { LaGanancia(); } catch (Exception e) { Check(false, "la ganancia: " + e); }
        try { Conectado(); } catch (Exception e) { Check(false, "conectado: " + e); }
        try { ConElLimitador(); } catch (Exception e) { Check(false, "con el limitador: " + e); }
        try { SubirTodo(); } catch (Exception e) { Check(false, "subir todo: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

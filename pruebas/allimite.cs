using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// CADA CANCION HASTA SU LIMITE, SIN RASPARSE (decision del dueno del 24-10-2026; Sonoridad.cs: GananciaAlLimite, algoritmo 5).
//
// Ya no hay volumen objetivo para lo medido con el algoritmo 5: cada cancion sube lo mas que aguanta sin que el limitador del final
// la raspe (su P90 a 0,5 dB del techo como mucho, su P99 a 3 dB y su golpe mas fuerte a 6 dB: los dos ultimos, los topes de picos de
// antes). Lo medido antes (algoritmos 1 a 4, o
// solo la estimacion) nivela con la regla de antes mientras la tanda larga lo vuelve a medir: nada se queda sin nivelar. Y una tabla
// de una version vieja, sin registros de identidad, ya no se borra al sonar la cancion (FuentesMedidas.cs).
//
// Lo que se vigila:
//   1) el P90 sale del mismo histograma que el P99, y el P99 no cambia;
//   2) el formato del algoritmo 5: ida y vuelta, y lo roto se salta;
//   3) la regla: margenes, fuerza, techo de seguridad, nunca NaN;
//   4) con el limitador de la rocola de verdad, en ocho canciones de prueba: sube sin que el limitador trabaje de mas (mas de 1 dB
//      como mucho el 10 % del tiempo, mas de 3 dB como mucho el 1 %), la ya aplastada casi no se sube, y sin el P90 si se rasparia;
//   5) cada medida va por su regla (5 al limite; 2 a 4 la de antes con sus topes; 1 y la estimacion, la de antes sin picos);
//   6) de punta a punta con archivos: una tabla vieja sin identidad nivela al sonar y no se borra;
//   7) la tanda larga pide todo lo que no es del algoritmo 5, y el disco en racha sube hasta el limite de su pista mas exigente;
//   8) y en la cadena de fabrica entera (con lo que va detras del nivelado), el limitador sigue dentro del criterio.
// Son senales sinteticas: dicen que la cuenta hace lo que promete, no como suena tu musica (eso se oye en el bar).
class AlLimite
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TP, TS, TSil;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static double Db(double x) { return 20 * Math.Log10(Math.Max(x, 1e-12)); }
    static float Lin(double db) { return (float)Math.Pow(10, db / 20); }
    static object S(string n) { return TF.GetField(n, T).GetValue(null); }
    static object Call(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T))
            if (m.Name == n && m.GetParameters().Length == a.Length)
            { try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
        throw new MissingMethodException(n);
    }
    static object Get(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); } return null; }
    static void Set(object o, string n, object v) { FieldInfo f = TF.GetField(n, T); f.SetValue(f.IsStatic ? null : o, v); }
    static float Campo(object s, string n) { object v = s.GetType().GetField(n, T).GetValue(s); return v is int ? (int)v : (float)v; }
    static float Rms(object s) { return (float)s.GetType().GetProperty("RmsPorCanal", T).GetValue(s, null); }
    static bool Prop(object s, string n) { return (bool)s.GetType().GetProperty(n, T).GetValue(s, null); }

    sealed class Fuente : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public Fuente(float[] x) { d = x; }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, n); p += n; return n; }
    }

    // ---------------- las canciones de prueba (60 s, 44,1 kHz, estereo) ----------------
    static float[] Normaliza(float[] x, double picoDb)
    {
        double pk = 0; foreach (float v in x) pk = Math.Max(pk, Math.Abs(v));
        float k = (float)(Math.Pow(10, picoDb / 20) / pk); for (int i = 0; i < x.Length; i++) x[i] *= k;
        return x;
    }
    static float[] Golpes(double picoDb, double porSeg, double colchon, double variacion, int semilla)
    {
        int fr = 44100 * 60; float[] x = new float[fr * 2]; Random r = new Random(semilla);
        double lp = 0, lp2 = 0;
        for (int i = 0; i < fr; i++) { double w = r.NextDouble() * 2 - 1; lp += (w - lp) * 0.08; lp2 += (lp - lp2) * 0.3; x[2 * i] = (float)(lp2 * colchon); x[2 * i + 1] = (float)((lp2 * 0.9 + 0.1 * lp) * colchon); }
        int per = (int)(44100 / porSeg);
        for (int s = 0; s < fr; s += per)
        {
            double fuerza = 1.0 - variacion * r.NextDouble();
            for (int k = 0; k < 4410 && s + k < fr; k++)
            {
                double e = Math.Exp(-k / 600.0), v = fuerza * e * (Math.Sin(2 * Math.PI * 70 * k / 44100.0) * 0.7 + (r.NextDouble() * 2 - 1) * 0.3 * Math.Exp(-k / 150.0));
                x[2 * (s + k)] += (float)v; x[2 * (s + k) + 1] += (float)v;
            }
        }
        return Normaliza(x, picoDb);
    }
    static float[] Balada(double picoDb, bool golpeSuelto, int semilla)
    {
        int fr = 44100 * 60; float[] x = new float[fr * 2]; Random r = new Random(semilla); double lp = 0;
        for (int i = 0; i < fr; i++)
        {
            double t = i / 44100.0, env = 0.25 + 0.75 * Math.Pow(Math.Sin(Math.PI * (t % 8.0) / 8.0), 2);
            double w = r.NextDouble() * 2 - 1; lp += (w - lp) * 0.05;
            double v = env * (0.5 * Math.Sin(2 * Math.PI * 196 * t) + 0.3 * Math.Sin(2 * Math.PI * 294 * t) + 0.6 * lp);
            x[2 * i] = (float)v; x[2 * i + 1] = (float)(v * 0.95);
        }
        if (golpeSuelto) for (int k = 0; k < 8820; k++) { int i = 44100 * 40 + k; double v = 2.2 * Math.Exp(-k / 2000.0) * (r.NextDouble() * 2 - 1); x[2 * i] += (float)v; x[2 * i + 1] += (float)v; }
        return Normaliza(x, picoDb);
    }
    static float[] Aplastada(double picoDb, int semilla)
    {
        float[] x = Golpes(0, 4, 3.0, 0.1, semilla);
        for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Tanh(x[i] * 2.5);
        return Normaliza(x, picoDb);
    }

    static object Picos(float[] x)
    {
        object p = Activator.CreateInstance(TP, T, null, new object[] { 44100, 2 }, null);
        TP.GetMethod("Procesa", T).Invoke(p, new object[] { x, 0, x.Length });
        return p;
    }
    static float PDb(object p, string n) { return (float)TP.GetProperty(n, T).GetValue(p, null); }

    // El limitador de la rocola con la cancion subida 'g': peor reduccion y parte del tiempo (cada 1 ms) por encima de 1 y de 3 dB.
    static void Limita(float[] x, float g, out double peor, out double m1, out double m3)
    {
        float[] y = new float[x.Length]; for (int i = 0; i < x.Length; i++) y[i] = x[i] * g;
        Type L = TF.GetNestedType("LimitadorPicoReal", T);
        object lim = Activator.CreateInstance(L, T, null, new object[] { new Fuente(y), (float)S("TechoPicoReal"), 3.0 }, null);
        MethodInfo rd = L.GetMethod("Read"); FieldInfo env = L.GetField("env", T);
        float[] b = new float[88]; int n1 = 0, n3 = 0, tot = 0; peor = 0;
        object[] args = { b, 0, 88 };
        while ((int)rd.Invoke(lim, args) > 0)
        {
            double gr = -Db((float)env.GetValue(lim)); tot++;
            if (gr > peor) peor = gr; if (gr > 1) n1++; if (gr > 3) n3++;
        }
        m1 = n1 / (double)tot; m3 = n3 / (double)tot;
    }

    static object Reg(int alg, float lk, float pico, float p99, float p90, int bloques)
    {
        object mudo = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { bloques / 10f });
        if (alg >= 5) return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, 2f, bloques, pico, p99, mudo, 8.5f, p90 }, null);
        if (alg == 4) return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, 2f, bloques, pico, p99, mudo, 8.5f }, null);
        if (alg == 3) return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, 2f, bloques, pico, p99, mudo }, null);
        if (alg == 2) return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, 2f, bloques, pico, p99 }, null);
        return Activator.CreateInstance(TS, T, null, new object[] { alg, lk, 2f, bloques }, null);
    }
    static IDictionary Tabla() { return (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); }
    static object Form()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T)) if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 0f);
        return f;
    }

    // ---------------------------------------------------------------- 1) EL P90
    static void ElP90()
    {
        Console.WriteLine("\n  1) EL P90 DE LOS PICOS\n");
        // 100 bloques de 100 ms con un tono a picos conocidos: 80 a -12, 15 a -6, 5 a -2 dBFS.
        float[] x = new float[4410 * 2 * 100];
        for (int b = 0; b < 100; b++)
        {
            double pk = b < 80 ? -12 : (b < 95 ? -6 : -2), a = Math.Pow(10, pk / 20);
            for (int i = 0; i < 4410; i++) { float v = (float)(a * Math.Sin(2 * Math.PI * 441 * i / 44100.0 + Math.PI / 2)); x[2 * (b * 4410 + i)] = v; x[2 * (b * 4410 + i) + 1] = v; }
        }
        object p = Picos(x);
        Check(Math.Abs(PDb(p, "P90Db") - (-6f)) < 0.11f && Math.Abs(PDb(p, "P99Db") - (-2f)) < 0.11f && Math.Abs(PDb(p, "PicoDb") - (-2f)) < 0.01f,
              "con 80 bloques a -12, 15 a -6 y 5 a -2 dBFS: P90 " + F(PDb(p, "P90Db"), "0.0") + ", P99 " + F(PDb(p, "P99Db"), "0.0") + ", pico " + F(PDb(p, "PicoDb"), "0.0"));
        MethodInfo pc = TP.GetMethod("PercentilDb", T);
        Check(pc != null && (float)pc.Invoke(p, new object[] { (double)TP.GetField("Fraccion", T).GetValue(null) }) == PDb(p, "P99Db")
              && (float)pc.Invoke(p, new object[] { 0.10 }) == PDb(p, "P90Db"), "el P99 y el P90 salen de la misma cuenta (PercentilDb): el P99 es el de siempre");
    }

    // ---------------------------------------------------------------- 2) EL FORMATO
    static void ElFormato()
    {
        Console.WriteLine("\n  2) EL FORMATO DEL ALGORITMO 5 (rocola_sonoridad.txt)\n");
        Type tm = TF.Assembly.GetType("Rocola.MedidorBs1770", true);
        Check((int)tm.GetField("Algoritmo", T).GetValue(null) >= 5, "el algoritmo de ahora es el 5 o posterior (el 6, del 25-10, suma donde empieza y acaba y el zumbido)");
        string cab = (string)S("IntegCabecera");
        StringWriter w = new StringWriter(); w.Write(cab + "\r\n");
        TF.GetMethod("EscribeIntegRegistro", T).Invoke(null, new object[] { w, "C:\\m\\cinco.mp3", Reg(5, -14.2f, -0.4f, -1.5f, -4.25f, 2000) });
        TF.GetMethod("EscribeIntegRegistro", T).Invoke(null, new object[] { w, "C:\\m\\cuatro.mp3", Reg(4, -14.2f, -0.4f, -1.5f, 0f, 2000) });
        string texto = w.ToString();
        Check(texto.Contains("|8.5|-4.25|C:\\m\\cinco.mp3") && texto.Contains("|8.5|C:\\m\\cuatro.mp3"), "el P90 va detras del LRA, antes de la ruta; las de 4 se escriben como siempre");
        IDictionary t = Tabla(); List<string> aj = new List<string>();
        bool ok = (bool)TF.GetMethod("LeeIntegRegistros", T).Invoke(null, new object[] { new StringReader(texto), t, aj });
        object c5 = t["C:\\m\\cinco.mp3"], c4 = t["C:\\m\\cuatro.mp3"];
        Check(ok && aj.Count == 0 && Prop(c5, "ConP90") && Math.Abs(Campo(c5, "P90Db") + 4.25f) < 1e-4 && !Prop(c4, "ConP90") && Campo(c4, "Algoritmo") == 4,
              "ida y vuelta: la de 5 con su P90, la de 4 sin el (nivelara con la regla de antes)");
        string malos = cab + "\r\n5|-10|2|100|-0.4|-2.1|-|-|-|-|180|7|-1|C:\\m\\p90mayor.mp3\r\n5|-10|2|100|-0.4|-2.1|-|-|-|-|180|7|NaN|C:\\m\\nan.mp3\r\n"
                     + "5|-10|2|100|-0.4|-2.1|-|-|-|-|180|7|C:\\m\\falta.mp3\r\n99|-9|2|100|-0.4|-2.1|-|-|-|-|180|7|-3|x|C:\\m\\futuro.mp3\r\n";
        t = Tabla(); aj = new List<string>();
        TF.GetMethod("LeeIntegRegistros", T).Invoke(null, new object[] { new StringReader(malos), t, aj });
        Check(t.Count == 0 && aj.Count == 1, "un P90 por encima del P99, que no es numero o que falta: se salta; uno de un algoritmo posterior se guarda intacto");
    }

    // ---------------------------------------------------------------- 3) LA REGLA
    static float AlLim(float fuerza, double pico, double p99, double p90) { return (float)Call(null, "GananciaAlLimite", fuerza, Lin(pico), Lin(p99), Lin(p90)); }
    static void LaRegla()
    {
        Console.WriteLine("\n  3) LA REGLA (GananciaAlLimite)\n");
        float a = (float)S("LimP90Db"), b = (float)S("LimP99Db"), c = (float)S("LimPicoDb"), techo = (float)S("TechoPicoReal");
        Check(a == 0.5f && b == 3f && c == 6f && b == (float)S("SubeGrP99Db") && c == (float)S("SubeGrPicoDb"),
              "los margenes: el P90 a 0,5 dB del techo (medido con la cadena de fabrica entera), el P99 a 3 dB y el golpe mas fuerte a 6 dB (los dos ultimos, los topes de picos de antes)");
        float g = AlLim(1f, -1, -2, -9);
        double gP90 = Db(Lin(-9) * g / techo), gP99 = Db(Lin(-2) * g / techo), gPico = Db(Lin(-1) * g / techo);
        Check(gP90 <= a + 1e-4 && gP99 <= b + 1e-4 && gPico <= c + 1e-4 && (Math.Abs(gP90 - a) < 0.01 || Math.Abs(gP99 - b) < 0.01 || Math.Abs(gPico - c) < 0.01),
              "pico -1, P99 -2, P90 -9: sube " + F(Db(g), "+0.0;-0.0") + " dB, justo hasta el primero de sus tres margenes");
        float aplastada = AlLim(1f, -0.2, -0.2, -0.4);
        Check(Math.Abs(Db(aplastada) - (Db(techo) + a + 0.4)) < 0.01 && Db(aplastada) < 0.5, "una ya aplastada (P90 a -0,4 dBFS) casi no se sube (" + F(Db(aplastada), "+0.0;-0.0") + " dB): la frena su P90, no sus picos");
        float caliente = AlLim(1f, 1.5, 1.2, 1.0);
        Check(caliente < 1f && Math.Abs(Db(caliente) - (Db(techo) + a - 1.0)) < 0.01, "una que llega por encima de su limite (un MP3 muy caliente, P90 a +1 dBFS) baja lo justo (" + F(Db(caliente), "+0.0;-0.0") + " dB)");
        float sinTope = AlLim(1f, -12, -14, -18);
        Check(Math.Abs(Db(sinTope) - Math.Min(Math.Min(Db(techo) + a + 18, Db(techo) + b + 14), Db(techo) + c + 12)) < 0.01,
              "sin volumen objetivo: con los picos lejos sube lo que dejan sus picos (" + F(Db(sinTope), "+0.0;-0.0") + " dB)");
        Check(Math.Abs(Db(AlLim(1f, -70, -72, -75)) - 24.08) < 0.05, "una casi muda no sube mas de +24 dB");
        Check(Math.Abs(Db(AlLim(0.5f, -1, -2, -9)) - Db(g) / 2) < 0.01 && AlLim(0f, -1, -2, -9) == 1f, "la fuerza recorre ese camino a medias (50 %) o nada (0 %)");
        Check(AlLim(1f, double.NaN, -2, -9) == 1f, "picos raros: 1, nunca NaN");
    }

    // ---------------------------------------------------------------- 4) CON EL LIMITADOR DE VERDAD
    static void ConElLimitador()
    {
        Console.WriteLine("\n  4) CON EL LIMITADOR DE LA ROCOLA (ocho canciones de prueba de 60 s)\n");
        string[] nombres = { "golpes parejos 4/s", "golpes desiguales 2/s", "golpes sueltos 1/s", "balada sin golpes", "balada con un golpe suelto", "aplastada moderna", "vieja bajita", "comprimida grabada baja" };
        List<float[]> canciones = new List<float[]> {
            Golpes(-0.3, 4, 1.0, 0.1, 1), Golpes(-0.5, 2, 1.0, 0.6, 2), Golpes(-1.0, 1, 0.7, 0.4, 3),
            Balada(-1.0, false, 4), Balada(-0.5, true, 5), Aplastada(-0.2, 6), Golpes(-9.0, 2, 1.5, 0.5, 7), Aplastada(-7.0, 8) };
        Info("cancion                      sube    peor   >1 dB  >3 dB");
        bool limpio = true, aplastadaQuieta = false, vieja = false, comprimida = false, sinP90Raspa = true; double peorSuelto = 0, peorAplastadas = 0;
        for (int k = 0; k < canciones.Count; k++)
        {
            object p = Picos(canciones[k]);
            float g = AlLim(1f, PDb(p, "PicoDb"), PDb(p, "P99Db"), PDb(p, "P90Db"));
            double peor, m1, m3; Limita(canciones[k], g, out peor, out m1, out m3);
            Info(nombres[k].PadRight(28) + F(Db(g), "+0.0;-0.0").PadLeft(6) + F(peor, "0.0").PadLeft(8) + (F(m1 * 100, "0.0") + " %").PadLeft(8) + (F(m3 * 100, "0.0") + " %").PadLeft(7));
            if (!(m1 <= 0.10 && m3 <= 0.01)) limpio = false;
            if (k == 4) peorSuelto = peor;   // un unico golpe suelto: lo mas que baja, un instante
            else if (peor > 4.5) limpio = false;
            if (k == 5 || k == 7) peorAplastadas = Math.Max(peorAplastadas, peor);
            if (k == 5 && Db(g) <= 0.5) aplastadaQuieta = true;
            if (k == 6 && Db(g) > 10) vieja = true;
            if (k == 7 && Db(g) > 6) comprimida = true;
            if (k == 5 || k == 7)
            {   // la misma con solo los topes de picos de antes y sin objetivo: el limitador trabajaria casi todo el tiempo
                float sin = (float)Call(null, "TopeSubidaPorPicos", Lin(PDb(p, "PicoDb")), Lin(PDb(p, "P99Db")));
                double ps, s1, s3; Limita(canciones[k], sin, out ps, out s1, out s3);
                if (!(s1 > 0.9)) sinP90Raspa = false;
            }
        }
        Check(limpio, "el limitador quita mas de 1 dB como mucho el 10 % del tiempo y mas de 3 dB como mucho el 1 %; fuera del golpe suelto, nunca mas de 4,5 dB");
        Check(peorSuelto < 10.0, "un golpe suelto muy por encima del resto se recorta solo en su instante (" + F(peorSuelto, "0.0") + " dB)");
        Check(aplastadaQuieta && peorAplastadas < 2.0, "la moderna aplastada casi no se sube y en las aplastadas el limitador no pasa de " + F(peorAplastadas, "0.0") + " dB");
        Check(vieja && comprimida, "la grabacion vieja bajita sube mas de 10 dB y la comprimida grabada baja mas de 6 dB: sin volumen objetivo que las frene");
        Check(sinP90Raspa, "sin el P90 (solo los topes de picos), las dos aplastadas quedarian limitadas mas de 1 dB mas del 90 % del tiempo");
    }

    // ---------------------------------------------------------------- 5) CADA MEDIDA, SU REGLA
    static void CadaMedida()
    {
        Console.WriteLine("\n  5) CADA MEDIDA, SU REGLA\n");
        object f = Form(); IDictionary tab = Tabla(); Set(f, "integCache", tab);
        IDictionary est = (IDictionary)Get(f, "loudCache");
        // La misma cancion (-20 dBFS por canal, picos -1 / -4 / -10) medida con cada algoritmo.
        float lk = -20f + 3.0103f - 0.691f;   // LKFS de -20 dB por canal en estereo
        for (int alg = 1; alg <= 5; alg++) tab["C:\\m\\a" + alg + ".mp3"] = Reg(alg, lk, -1f, -4f, -10f, 2000);
        float rms = Lin(-20);
        est["C:\\m\\estimada.mp3"] = rms;
        float g5 = (float)Call(f, "NormGainFor", "C:\\m\\a5.mp3"), g4 = (float)Call(f, "NormGainFor", "C:\\m\\a4.mp3"), g2 = (float)Call(f, "NormGainFor", "C:\\m\\a2.mp3");
        float g1 = (float)Call(f, "NormGainFor", "C:\\m\\a1.mp3"), gE = (float)Call(f, "NormGainFor", "C:\\m\\estimada.mp3"), g0 = (float)Call(f, "NormGainFor", "C:\\m\\nunca.mp3");
        float cap = (float)Call(null, "TierCapDb", rms);
        float viejaConPicos = (float)Call(null, "GananciaNivelado", 0.26f, rms, 1f, Math.Min(Lin(cap), 16f), true, Lin(-1), Lin(-4));
        float viejaSinPicos = (float)Call(null, "GananciaNivelado", 0.26f, rms, 1f, Math.Min(Lin(cap), 16f), false, 0f, 0f);
        Check(Math.Abs(g5 - AlLim(1f, -1, -4, -10)) < 1e-5, "la del algoritmo 5 sube hasta su limite (" + F(Db(g5), "+0.0;-0.0") + " dB)");
        Check(Math.Abs(g4 - viejaConPicos) < 1e-5 && Math.Abs(g2 - viejaConPicos) < 1e-5, "las de 2 a 4 nivelan con la regla de antes y sus topes de picos (" + F(Db(g4), "+0.0;-0.0") + " dB) mientras se vuelven a medir");
        Check(Math.Abs(g1 - viejaSinPicos) < 1e-5 && Math.Abs(gE - viejaSinPicos) < 1e-4 && g0 == 1f,
              "la de 1 y la estimacion, la de antes sin picos (" + F(Db(g1), "+0.0;-0.0") + " dB); sin medir, tal cual: ninguna se queda sin nivelar por ser de antes");
        Set(f, "normalizeOn", false);
        Check((float)Call(f, "NormGainFor", "C:\\m\\a5.mp3") == 1f, "con el nivelado apagado, nada");
    }

    // ---------------------------------------------------------------- 6) DE PUNTA A PUNTA, CON UNA TABLA VIEJA
    static void Fabrica(object f)
    {
        foreach (string s in new string[] { "excAmount", "bassAmount", "eqAmount" }) if (TF.GetField(s, T) != null) Set(f, s, 0f);
        foreach (string s in new string[] { "bassAuto", "eqAuto", "sbrOn", "saltaSilencios", "claridadLocal", "partesSuaves", "localAuto", "presenciaSala",
                                            "cortaSubgrave", "estereoEstrecho", "estereoAuto", "sinMejoras", "aLaMedida", "quitaSiseo" })
            if (TF.GetField(s, T) != null) Set(f, s, false);
        Set(f, "crossfadeSec", 0.5f);
    }
    static void DePuntaAPunta(string dir)
    {
        Console.WriteLine("\n  6) DE PUNTA A PUNTA: UNA TABLA DE UNA VERSION VIEJA\n");
        float[] x = Golpes(-9.0, 2, 1.5, 0.5, 7);
        string wav = Path.Combine(dir, "vieja.wav");
        using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))) w.WriteSamples(x, 0, 44100 * 2 * 20);
        object[] a = { wav, null };
        TF.GetMethod("MideIntegrada", T).Invoke(Form(), a);   // la medida de hoy, como la toma la tanda larga
        StringWriter sw = new StringWriter();
        TF.GetMethod("EscribeIntegRegistro", T).Invoke(null, new object[] { sw, wav, a[1] });
        string linea5 = sw.ToString().TrimEnd('\r', '\n');
        string[] p = linea5.Split('|');
        string linea2 = "2|" + string.Join("|", p, 1, 5) + "|" + wav;   // la misma medida, como la escribia una version de septiembre
        int algHoy = (int)TF.Assembly.GetType("Rocola.MedidorBs1770", true).GetField("Algoritmo", T).GetValue(null);
        Check(Prop(a[1], "ConP90") && p.Length == (algHoy >= 6 ? 18 : 14), "la medida de hoy sale del algoritmo de hoy (" + algHoy + "), con su P90");
        Type tt = TF.GetNestedType("Track", T);
        foreach (string linea in new string[] { linea2, linea5 })
        {
            object g = Form(); Fabrica(g); IDictionary tab = Tabla(); List<string> aj = new List<string>();
            TF.GetMethod("LeeIntegRegistros", T).Invoke(null, new object[] { new StringReader((string)S("IntegCabecera") + "\r\n" + linea + "\r\n"), tab, aj });
            Set(g, "integCache", tab);
            ((IDictionary)Get(g, "durCache"))[wav] = 20f;   // la duracion apuntada por la version vieja (no se vuelve a medir al sonar)
            bool vieja = linea == linea2;
            object track = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(track, wav);
            float esperada = (float)Call(g, "NormGainFor", wav);
            object lector = Call(g, "OpenAudio", wav);
            TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
            object cad = Call(g, "ArmaCadena", track, lector, false, 0.0);   // como al sonar: OlvidaSiEsOtroArchivo, la identidad, el nivelado
            float puesta = (float)Get(Get(cad, "Tap"), "Gain");
            ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
            float[] b = new float[8820]; double pk = 0; int n;
            while ((n = sal.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) pk = Math.Max(pk, Math.Abs(b[i]));
            ((IDisposable)lector).Dispose();
            bool sigue = tab.Contains(wav) && ((IDictionary)Get(g, "fuentesMedidas")).Contains(wav);
            Check(sigue && Math.Abs(puesta - esperada) < 1e-6 && Db(puesta) > 6 && Db(pk) <= Db((float)S("TechoPicoReal")) + 0.05,
                  (vieja ? "medida de septiembre (algoritmo 2) sin registro de identidad" : "medida de hoy (algoritmo 5)")
                  + ": al sonar conserva su medida y sube " + F(Db(puesta), "+0.0;-0.0") + " dB; sale con pico " + F(Db(pk), "0.0") + " dBFS, sin pasar del techo");
            if (vieja) Check((bool)Call(g, "PorVerificar", wav), "y queda apuntada para volver a medirla");
        }
    }

    // ---------------------------------------------------------------- 7) EL REPASO Y EL DISCO
    static void Pon(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(n); }
    static object Pista(Type tt, string ruta) { object x = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(x, ruta); return x; }
    static void RepasoYDisco()
    {
        Console.WriteLine("\n  7) LA TANDA LARGA Y EL DISCO\n");
        Type tt = TF.GetNestedType("Track", T);
        foreach (bool modo in new bool[] { false, true })
        {
            object f = Form(); IDictionary tab = Tabla(); Set(f, "integCache", tab);
            if (TF.GetField("localAuto", T) != null) Set(f, "localAuto", modo);
            IList pistas = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tt));
            int hoy = (int)TF.Assembly.GetType("Rocola.MedidorBs1770", true).GetField("Algoritmo", T).GetValue(null);
            string[] rutas = new string[hoy + 1];
            for (int alg = 1; alg <= hoy; alg++) rutas[alg - 1] = "C:\\m\\d\\" + alg + ".mp3";
            rutas[hoy] = "C:\\m\\nunca.mp3";
            foreach (string r in rutas) pistas.Add(Pista(tt, r));
            for (int alg = 1; alg <= hoy; alg++) tab[rutas[alg - 1]] = Reg(alg, -14f, -1f, -4f, -10f, 2000);
            Set(f, "tracks", pistas);
            List<string> pp = new List<string>(); foreach (object s in (IEnumerable)Call(f, "TandaIntegrada")) pp.Add(s is string ? (string)s : (string)tt.GetField("Path", T).GetValue(s));
            Check(pp.Count == hoy && !pp.Contains(rutas[hoy - 1]), "la tanda larga pide todo lo que no es del algoritmo de hoy (" + hoy + ": de la 1 a la " + (hoy - 1) + " y la nunca medida), con el modo del local " + (modo ? "encendido" : "apagado"));
        }
        if (TF.GetField("localAuto", T) != null) TF.GetField("localAuto", T).SetValue(null, false);   // es de la clase: que no se quede encendido para lo que sigue
        // El disco: dos pistas del algoritmo 5 con los picos lejos del techo (con la regla de antes las frenaria el objetivo), una mas exigente. (Rutas del sistema: el disco es la carpeta.)
        string R1 = Path.Combine(Path.Combine("m", "x"), "1.mp3"), R2 = Path.Combine(Path.Combine("m", "x"), "2.mp3");
        object g = Form(); IDictionary t2 = Tabla(); Set(g, "integCache", t2);
        Type ta = TF.GetNestedType("Album", T), ti = TF.GetNestedType("PlayItem", T);
        object D = Activator.CreateInstance(ta, true); Pon(D, "Folder", Path.GetDirectoryName(R1));
        object x1 = Pista(tt, R1), x2 = Pista(tt, R2);
        IList l = (IList)Get(D, "Tracks"); l.Add(x1); l.Add(x2);
        t2[R1] = Reg(5, -13f, -6f, -8f, -10f, 2000); t2[R2] = Reg(5, -16f, -6f, -9f, -13f, 2000);
        object i1 = Activator.CreateInstance(ti, true); Pon(i1, "Song", x1); Pon(i1, "Disc", D);
        object i2 = Activator.CreateInstance(ti, true); Pon(i2, "Song", x2); Pon(i2, "Disc", D);
        Set(g, "nowPlaying", i1); IList q = (IList)Get(g, "queue"); q.Clear(); q.Add(i2);
        float gd1 = (float)Call(g, "GananciaDe", x1), gd2 = (float)Call(g, "GananciaDe", x2), sola2 = (float)Call(g, "NormGainFor", R2);
        Check(gd1 == gd2 && Math.Abs(gd1 - AlLim(1f, -6, -8, -10)) < 1e-5 && sola2 > gd2,
              "en racha, el disco sube hasta el limite de su pista mas exigente (" + F(Db(gd1), "+0.0;-0.0") + " dB) y la bajita sigue por debajo, como en el disco (sola subiria " + F(Db(sola2), "+0.0;-0.0") + ")");
        // Una pista del disco aun con la medida de antes: el disco entero, con la regla de antes.
        t2[R2] = Reg(4, -16f, -6f, -9f, 0f, 2000);
        float gv = (float)Call(g, "GananciaDe", x1);
        float r1 = Rms(t2[R1]), r2 = Rms(t2[R2]);
        float rmsD = (float)Math.Sqrt((r1 * (double)r1 + r2 * (double)r2) / 2);
        float capD = Math.Min(Lin((float)Call(null, "TierCapDb", rmsD)), 16f);
        float antes = (float)Call(null, "GananciaNivelado", 0.26f, rmsD, 1f, capD, true, Lin(-6), Lin(-8));
        Check(Math.Abs(gv - antes) < 1e-5 && Math.Abs(gv - AlLim(1f, -6, -8, -10)) > 0.1f, "si una pista del disco aun tiene la medida de antes, el disco nivela con la regla de antes (" + F(Db(gv), "+0.0;-0.0") + " dB) hasta que se vuelva a medir");
    }

    // ---------------------------------------------------------------- 8) EN LA CADENA DE FABRICA
    // Una mezcla completa (bombo, bajo, caja, platillos, colchon, voz y guitarra), de 20 s.
    static float[] Mezcla(int semilla)
    {
        int fs = 44100, n = fs * 20; float[] x = new float[2 * n]; Random r = new Random(semilla);
        double hp = 0, ant = 0, v1 = 0, v2 = 0, v3 = 0, fbajo = 0, fgui = 0;
        double[] notas = { 41.2, 41.2, 55, 49, 36.7, 41.2, 61.7, 55 };
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs, beat = t % 0.5, bar = t % 2.0;
            double kick = 0.9 * Math.Exp(-beat / 0.15) * Math.Sin(2 * Math.PI * (45 + 80 * Math.Exp(-beat / 0.025)) * beat);
            fbajo += 2 * Math.PI * notas[(int)t % notas.Length] / fs;
            double bajo = 0.55 * (Math.Sin(fbajo) + 0.3 * Math.Sin(2 * fbajo) + 0.15 * Math.Sin(3 * fbajo)) * (0.6 + 0.4 * Math.Exp(-(t % 0.25) / 0.2));
            double w = r.NextDouble() * 2 - 1; hp = 0.6 * (hp + w - ant); ant = w;
            double sn = (bar > 0.5 && bar < 0.75) || (bar > 1.5 && bar < 1.75) ? 0.5 * Math.Exp(-((bar % 1.0) - 0.5) / 0.08) * (0.6 * w + 0.4 * Math.Sin(2 * Math.PI * 190 * t)) : 0;
            double hat = 0.18 * Math.Exp(-(t % 0.125) / 0.02) * hp;
            double pad = 0.12 * (Math.Sin(2 * Math.PI * 220 * t) + Math.Sin(2 * Math.PI * 277.2 * t) + Math.Sin(2 * Math.PI * 329.6 * t));
            double e = (Math.Sin(2 * Math.PI * 3 * t) > -0.3) ? 1 : 0.1, ex = (r.NextDouble() * 2 - 1) * e;
            v1 += 0.02 * (ex - v1); v2 += 0.08 * (ex - v2); v3 += 0.25 * (ex - v3);
            double voz = 1.8 * (v1 - 0.6 * v2 + 0.3 * v3);
            fgui += 2 * Math.PI * 330 / fs; double gui = 0.15 * Math.Tanh(3 * Math.Sin(fgui)) * Math.Exp(-(t % 0.25) / 0.15);
            x[2 * i] = (float)(kick + bajo + sn + hat * 0.8 + pad + voz + gui * 1.2); x[2 * i + 1] = (float)(kick + bajo * 0.97 + sn + hat * 1.1 + pad * 0.9 + voz + gui * 0.4);
        }
        return x;
    }
    // Masterizada: subida 'db' y pasada por el limitador de la rocola con el techo a -0,3 dBFS (como un maestro "a tope").
    static float[] Masteriza(float[] x, double db)
    {
        double pk = 0; foreach (float v in x) pk = Math.Max(pk, Math.Abs(v));
        float g = (float)(Math.Pow(10, (db - 0.3) / 20) / pk); float[] y = new float[x.Length]; for (int i = 0; i < x.Length; i++) y[i] = x[i] * g;
        Type L = TF.GetNestedType("LimitadorPicoReal", T);
        ISampleProvider lim = (ISampleProvider)Activator.CreateInstance(L, T, null, new object[] { new Fuente(y), (float)Math.Pow(10, -0.3 / 20), 1.5 }, null);
        List<float> z = new List<float>(); float[] b = new float[4096]; int n; while ((n = lim.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) z.Add(b[i]);
        float[] w = new float[x.Length]; z.CopyTo(z.Count - x.Length, w, 0, x.Length); return w;
    }
    // La cadena de verdad de fabrica (realce 35 %, bajo automatico, des-recorte, grave en mono), con la cancion medida y nivelada al limite:
    // lo que hace el limitador cada 2 ms.
    static void EnLaCadena(float[] x, string dir, string nombre, out double sube, out double peor, out double m1, out double m3)
    { EnLaCadena(x, dir, nombre, 0, 0, out sube, out peor, out m1, out m3); }
    // Con el tono del local (graves y agudos en dB, de -6 a 6) puesto despues de dejar lo demas de fabrica.
    static void EnLaCadena(float[] x, string dir, string nombre, int graves, int agudos, out double sube, out double peor, out double m1, out double m3)
    {
        string wav = Path.Combine(dir, nombre + ".wav");
        using (WaveFileWriter wr = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))) wr.WriteSamples(x, 0, x.Length);
        object f = Form();
        Set(f, "excAmount", 0.35f); Set(f, "bassAuto", true); Set(f, "bassAmount", 0.30f); Set(f, "eqAuto", false); Set(f, "eqAmount", 0f);
        Set(f, "declipOn", true); Set(f, "crossfadeSec", 0f); Set(f, "saltaSilencios", false);
        // Lo del local, como de fabrica: apagado (son de la clase; otra seccion pudo dejarlos de otra manera).
        foreach (string q in new string[] { "localAuto", "claridadLocal", "partesSuaves", "presenciaSala", "estereoEstrecho", "cortaSubgrave", "estereoAuto", "quitaSiseo", "sinMejoras" })
            if (TF.GetField(q, T) != null) Set(f, q, false);
        foreach (string q in new string[] { "balanceDb", "gravesLocalDb", "agudosLocalDb" }) if (TF.GetField(q, T) != null) Set(f, q, 0);
        if (graves != 0 || agudos != 0) { Set(f, "gravesLocalDb", graves); Set(f, "agudosLocalDb", agudos); }
        object[] a = { wav, null }; TF.GetMethod("MideIntegrada", T).Invoke(f, a);
        IDictionary tab = Tabla(); tab[wav] = a[1]; Set(f, "integCache", tab);
        ((IDictionary)Get(f, "durCache"))[wav] = (float)(x.Length / 2 / 44100.0);
        object track = Pista(TF.GetNestedType("Track", T), wav);
        object lector = Call(f, "OpenAudio", wav);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        sube = Db((float)Get(Get(cad, "Tap"), "Gain"));
        object lim = Get(cad, "Limitador"); FieldInfo env = lim.GetType().GetField("env", T);
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        float[] b = new float[176]; int n1 = 0, n3 = 0, tot = 0; peor = 0;
        while (sal.Read(b, 0, b.Length) > 0) { double gr = -Db((float)env.GetValue(lim)); tot++; if (gr > peor) peor = gr; if (gr > 1) n1++; if (gr > 3) n3++; }
        ((IDisposable)lector).Dispose();
        m1 = n1 / (double)tot; m3 = n3 / (double)tot;
    }
    static void CadenaDeFabrica(string dir)
    {
        Console.WriteLine("\n  8) EN LA CADENA DE FABRICA (realce, bajo, des-recorte y grave en mono detras del nivelado)\n");
        string[] nom = { "masterizada con 9 dB de limitador", "aplastada moderna", "golpes parejos 4/s", "vieja bajita" };
        float[][] can = { Masteriza(Mezcla(3), 9), Aplastada(-0.2, 6), Golpes(-0.3, 4, 1.0, 0.1, 1), Golpes(-9.0, 2, 1.5, 0.5, 7) };
        Info("cancion                              sube    peor   >1 dB  >3 dB");
        bool bien = true;
        for (int k = 0; k < can.Length; k++)
        {
            double g, p, m1, m3; EnLaCadena(can[k], dir, "fab" + k, out g, out p, out m1, out m3);
            Info(nom[k].PadRight(36) + F(g, "+0.0;-0.0").PadLeft(6) + F(p, "0.0").PadLeft(8) + (F(m1 * 100, "0.0") + " %").PadLeft(8) + (F(m3 * 100, "0.0") + " %").PadLeft(7));
            if (!(m1 <= 0.10 && m3 <= 0.01)) bien = false;
        }
        Check(bien, "con todo lo de fabrica detras del nivelado, el limitador quita mas de 1 dB como mucho el 10 % del tiempo y mas de 3 dB como mucho el 1 % (con el P90 a 1 dB, el masterizado pasaba del 26 %)");
    }

    // 9) EL MARGEN DEL TONO DEL LOCAL (T1, 25-10-2026). Con los graves o los agudos del local subidos, el nivelado deja el margen que
    // piden; sin eso, con graves +3 el limitador quitaba mas de 1 dB el 96 % del tiempo en la masterizada (+6: mas de 3 dB el 84 %).
    static void ConElTono(string dir)
    {
        Console.WriteLine("\n  9) CON LOS GRAVES Y AGUDOS DEL LOCAL SUBIDOS (T1)\n");
        if (TF.GetNestedType("SalaLocal", T) == null || TF.GetNestedType("SalaLocal", T).GetMethod("MargenTonoDb", T) == null) { Check(false, "no esta SalaLocal.MargenTonoDb"); return; }
        string[] nom = { "masterizada con 9 dB de limitador", "aplastada moderna", "golpes parejos 4/s", "vieja bajita", "mezcla sin masterizar", "balada con un golpe" };
        float[][] can = { Masteriza(Mezcla(3), 9), Aplastada(-0.2, 6), Golpes(-0.3, 4, 1.0, 0.1, 1), Golpes(-9.0, 2, 1.5, 0.5, 7), Mezcla(5), Golpes(-12.0, 1, 2.0, 0.8, 9) };
        int[][] tonos = { new int[] { 3, 0 }, new int[] { 6, 0 }, new int[] { 0, 3 }, new int[] { 0, 6 }, new int[] { 6, 6 }, new int[] { 3, 6 }, new int[] { 1, 1 } };
        double[] sube0 = new double[can.Length];
        for (int k = 0; k < can.Length; k++) { double p, m1, m3; EnLaCadena(can[k], dir, "tono" + k, 0, 0, out sube0[k], out p, out m1, out m3); }
        foreach (int[] tn in tonos)
        {
            Info("graves " + tn[0].ToString("+0;-0;0") + ", agudos " + tn[1].ToString("+0;-0;0") + ":  cancion                         sube    peor   >1 dB  >3 dB");
            bool bien = true, baja = true;
            double esperado = (double)TF.GetNestedType("SalaLocal", T).GetMethod("MargenTonoDb", T).Invoke(null, new object[] { tn[0], tn[1] });
            for (int k = 0; k < can.Length; k++)
            {
                double g, p, m1, m3; EnLaCadena(can[k], dir, "tono" + k, tn[0], tn[1], out g, out p, out m1, out m3);
                Info("                     " + nom[k].PadRight(32) + F(g, "+0.0;-0.0").PadLeft(6) + F(p, "0.0").PadLeft(8) + (F(m1 * 100, "0.0") + " %").PadLeft(8) + (F(m3 * 100, "0.0") + " %").PadLeft(7));
                if (!(m1 <= 0.10 && m3 <= 0.01)) bien = false;
                if (Math.Abs((sube0[k] - g) - esperado) > 0.05) baja = false;
            }
            Check(bien, "graves " + tn[0] + " y agudos " + tn[1] + ": el limitador quita mas de 1 dB como mucho el 10 % del tiempo y mas de 3 dB como mucho el 1 %");
            Check(baja, "  y el nivelado baja lo que pide el tono (" + F(esperado, "0.00") + " dB) en todas");
        }
        // Lo negativo no da nada, y con el tono en 0 la ganancia es la de siempre (la misma cuenta, bit a bit).
        bool negativo = true;
        for (int k = 0; k < can.Length; k++)
        {
            double g, p, m1, m3; EnLaCadena(can[k], dir, "tono" + k, -6, -6, out g, out p, out m1, out m3);
            if (g != sube0[k]) negativo = false;
        }
        Check(negativo, "con graves y agudos en negativo, el nivelado sube lo mismo que con el tono en 0");
        object f = Form();
        Check((double)TF.GetNestedType("SalaLocal", T).GetMethod("MargenTonoDb", T).Invoke(null, new object[] { 0, 0 }) == 0.0, "con el tono en 0 no se baja nada");
        // Con "Escuchar sin mejoras" el tono no suena: tampoco se baja.
        Set(f, "gravesLocalDb", 6); Set(f, "agudosLocalDb", 6);
        object track = Pista(TF.GetNestedType("Track", T), Path.Combine(dir, "tono0.wav"));
        Set(f, "sinMejoras", true);
        float conSin = (float)TF.GetMethod("MargenDelTono", T).Invoke(f, new object[] { track });
        Set(f, "sinMejoras", false);
        float conTono = (float)TF.GetMethod("MargenDelTono", T).Invoke(f, new object[] { track });
        Set(f, "gravesLocalDb", 0); Set(f, "agudosLocalDb", 0);
        double pide66 = (double)TF.GetNestedType("SalaLocal", T).GetMethod("MargenTonoDb", T).Invoke(null, new object[] { 6, 6 });
        Check(conSin == 1f && Math.Abs(Db(conTono) + pide66) < 0.01, "con \"Escuchar sin mejoras\" no se baja (" + F(Db(conSin), "0.00") + " dB); sin eso, con +6 y +6, " + F(Db(conTono), "0.00") + " dB");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TP = asm.GetType("Rocola.PicosBloques", true); TS = asm.GetType("Rocola.SonoridadIntegrada", true);
        TSil = asm.GetType("Rocola.MapaSilencios", true);
        if (TF.GetMethod("GananciaAlLimite", T) == null) { Check(false, "no esta GananciaAlLimite"); return 1; }
        string dir = Path.Combine(Path.GetTempPath(), "rocola_allimite_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            try { ElP90(); } catch (Exception e) { Check(false, "el P90: " + e); }
            try { ElFormato(); } catch (Exception e) { Check(false, "el formato: " + e); }
            try { LaRegla(); } catch (Exception e) { Check(false, "la regla: " + e); }
            try { ConElLimitador(); } catch (Exception e) { Check(false, "con el limitador: " + e); }
            try { CadaMedida(); } catch (Exception e) { Check(false, "cada medida: " + e); }
            try { DePuntaAPunta(dir); } catch (Exception e) { Check(false, "de punta a punta: " + e); }
            try { RepasoYDisco(); } catch (Exception e) { Check(false, "repaso y disco: " + e); }
            try { CadenaDeFabrica(dir); } catch (Exception e) { Check(false, "en la cadena de fabrica: " + e); }
            try { ConElTono(dir); } catch (Exception e) { Check(false, "con el tono del local: " + e); }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// LA CADENA DE SONIDO ENTERA, LEIDA DE CUALQUIER MANERA (24-10-2026; revision de la cadena completa).
//
// Cada etapa tiene su bateria; esta mira la cadena de verdad de punta a punta (OpenAudio + ArmaCadena) en varios formatos, de
// fabrica y con TODO encendido (nivelado, Subir TODO, realce, DSEE, siseo, grave sintetico, grave en mono con el estereo estrecho y
// el subgrave, el local entero con balance, graves y agudos, la presencia, el des-recorte y el fundido):
//   1) leerla en bloques, a tamanos al azar o de a una muestra da los mismos bits (la entrada de 5 ms y el fundido caen donde deben);
//   2) nada roto (NaN, infinitos) ni por encima del techo del limitador;
//   3) sale la cancion entera mas su cebado, ni una muestra de menos;
//   4) el retardo que declara la cadena (lo que el relevo sin pausa descarta) es el de verdad: donde sale un clic;
//   5) las canciones mono salen como estereo de dos canales iguales (24-10-2026): bit a bit las del archivo, leidas como se lean, con
//      relevo sin pausa junto a las estereo, al retomar en cuadros enteros, y con el balance del local;
//   6) y cada etapa, sola, con lecturas de 1 a 7 muestras sueltas o entre bloques, en mono, estereo y 6 canales;
//   7) EL CAMBIO DE PISTA EN UN DISCO CONTINUO (24-10-2026): un disco partido en dos pistas, por las dos cadenas de verdad y el relevo
//      (la siguiente sin su cebado, siguiendo con las etapas de la anterior), frente a la cancion entera. Con la version publicada
//      (1d04b70) el cambio daba un escalon de 0,33 de fabrica (0,16 a 0,33 con el local; 0,46 con el limitador trabajando), con esta
//      musica a 0,3 de pico: el cebado de los retardos era un hueco para las etapas que heredan (y el paso-todo del grave en mono lo
//      volvia un golpe), y el realce, el EQ, el bajo y el limitador empezaban de cero. Ahora, 0,0002 a 0,0003 (0,0024 a tope).
//      Y CONVERTIDO A OTRA FRECUENCIA (24-10-2026, revision de la cadena): con la salida abierta a 48 kHz y el disco a 44,1 (una de
//      48 sono antes, o la frecuencia de la tarjeta), cada pista pasaba por su convertidor y el cambio daba un escalon de 0,04 a 0,07
//      con la musica a 0,3 de pico, y un cuadro de mas. Ahora el convertidor y su limitador siguen con los de la anterior.
// La salida de la rocola pide siempre cuadros enteros, asi que lo de las lecturas partidas no sonaba; pero la cadena tiene que
// valer pidase como se pida (como el limitador desde el 22-10-2026). Con la version anterior fallan el fundido (la entrada de
// 5 ms caia en medio de la cancion) y el des-recorte (canales corridos y, en 6 canales, los ultimos 68 cuadros perdidos).
// Los MP3 no entran: en Linux no hay descompresor del sistema (ACM). Son senales sinteticas: miden la cuenta, no el oido.
class CadenaEntera
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF; static object F; static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void Set(object o, string n, object v)
    {
        FieldInfo f = TF.GetField(n, T);
        if (f == null) throw new MissingFieldException(n);
        f.SetValue(f.IsStatic ? null : o, v);
    }
    static object Call(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == n && m.GetParameters().Length == a.Length)
        { try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
        throw new MissingMethodException(n);
    }
    static double Db(double x) { return 20 * Math.Log10(Math.Max(x, 1e-12)); }

    static Dictionary<string, object> estaticos = new Dictionary<string, object>();
    static void GuardaEstaticos() { foreach (FieldInfo f in TF.GetFields(T)) if (f.IsStatic && !f.IsLiteral && !f.IsInitOnly) estaticos[f.Name] = f.GetValue(null); }
    static void RestauraEstaticos() { foreach (KeyValuePair<string, object> kv in estaticos) TF.GetField(kv.Key, T).SetValue(null, kv.Value); }

    static object Form(bool todo)
    {
        RestauraEstaticos();
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(T))
            if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        // de fabrica (los inicializadores de instancia no corren en un objeto sin construir)
        Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 0f);
        Set(f, "excAmount", 0.35f); Set(f, "bassAuto", true); Set(f, "bassAmount", 0.30f); Set(f, "eqAuto", true); Set(f, "eqAmount", 0f);
        Set(f, "declipOn", true); Set(f, "crossfadeSec", 0.5f); Set(f, "saltaSilencios", true);
        if (todo)
        {
            Set(f, "sbrAllowed", true); Set(f, "sbrOn", true); Set(f, "quitaSiseo", true); Set(f, "masterDb", 3f); Set(f, "excAmount", 0.8f);
            Set(f, "bassAuto", false); Set(f, "bassAmount", 0.8f);
            Set(f, "estereoEstrecho", true); Set(f, "cortaSubgrave", true); Set(f, "claridadLocal", true); Set(f, "partesSuaves", true);
            Set(f, "balanceDb", 3); Set(f, "gravesLocalDb", 4); Set(f, "agudosLocalDb", -3); Set(f, "presenciaSala", true);
        }
        return f;
    }

    // ---- las canciones de prueba ----
    static float[] Musica(int fs, int ch, double seg, int clic, int semilla)
    {
        int n = (int)(fs * seg); float[] x = new float[n * ch]; Random r = new Random(semilla); double hp = 0, ant = 0, fm = 0;
        double[] notas = { 523, 659, 784, 1047, 880, 698 };
        int a = fs / 2, b = n - fs / 2;
        for (int i = a; i < b; i++)
        {
            double t = (double)(i - a) / fs;
            double tb = t % 0.5, kick = 0.5 * Math.Exp(-tb / 0.12) * Math.Sin(2 * Math.PI * (50 + 60 * Math.Exp(-tb / 0.03)) * tb);
            double bajo = 0.25 * Math.Sin(2 * Math.PI * 55 * t);
            double w = r.NextDouble() * 2 - 1; hp = 0.5 * (hp + w - ant); ant = w;
            double hat = 0.2 * Math.Exp(-(t % 0.25) / 0.03) * hp, sis = 0.004 * (r.NextDouble() * 2 - 1);
            double acorde = 0.05 * (Math.Sin(2 * Math.PI * 220 * t) + Math.Sin(2 * Math.PI * 277 * t) + Math.Sin(2 * Math.PI * 330 * t));
            fm += 2 * Math.PI * notas[(int)(t / 0.5) % notas.Length] / fs;
            double mel = 0.12 * Math.Sin(fm);
            double l = kick + bajo + hat + acorde + mel + sis, rr = kick + hat * 0.8 + acorde + mel * 0.7 + sis;
            if (t % 4 < 0.05) { l *= 2.2; rr *= 2.2; }
            l = Math.Max(-0.98, Math.Min(0.98, l)); rr = Math.Max(-0.98, Math.Min(0.98, rr));   // recortes de origen (des-recorte)
            float[] v = new float[ch];
            if (ch == 1) v[0] = (float)(0.5 * (l + rr));
            else { v[0] = (float)l; v[1] = (float)rr; for (int c = 2; c < ch; c++) v[c] = (float)(0.3 * (c % 2 == 0 ? l : rr)); }
            for (int c = 0; c < ch; c++) x[i * ch + c] = v[c];
        }
        if (clic > 0) for (int c = 0; c < ch; c++) x[clic * ch + c] = 0.9f;
        return x;
    }
    static string Escribe(string dir, string nombre, float[] x, WaveFormat wf)
    {
        string p = Path.Combine(dir, nombre);
        using (WaveFileWriter w = new WaveFileWriter(p, wf)) w.WriteSamples(x, 0, x.Length);
        return p;
    }

    // ---- leer la cadena entera ----
    static float[] Lee(object f, string ruta, int modo, out int retardo, out int canales, out int hz)
    {
        Type tt = TF.GetNestedType("Track", T);
        object track = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(track, ruta);
        object lector = Call(f, "OpenAudio", ruta);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        retardo = (int)Get(cad, "Retardo");
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
        canales = sal.WaveFormat.Channels; hz = sal.WaveFormat.SampleRate;
        List<float> y = new List<float>(); float[] b = new float[20000]; Random r = new Random(5);
        int seguidos = 0;
        while (true)
        {
            int pide = modo == 0 ? 4096 : (modo == 1 ? 1 + r.Next(5000) : 1);
            if (modo == 2 && y.Count > hz * canales) pide = 4096;   // de a una muestra el primer segundo
            int n = sal.Read(b, 0, pide);
            if (n <= 0) { if (++seguidos > 2) break; continue; }
            seguidos = 0;
            for (int i = 0; i < n; i++) y.Add(b[i]);
        }
        ((IDisposable)lector).Dispose();
        return y.ToArray();
    }

    static void Caso(string nombre, string ruta, int framesIn, int clic, bool todo)
    {
        float techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);
        float[][] y = new float[3][]; int ret = 0, ch = 0, hz = 0;
        for (int modo = 0; modo < 3; modo++)
        {
            object f = Form(todo);
            int r0, c0, h0;
            try { y[modo] = Lee(f, ruta, modo, out r0, out c0, out h0); }
            catch (Exception e) { Check(false, nombre + (todo ? " TODO" : " fabrica") + ": la cadena revienta (" + e.GetType().Name + ": " + e.Message + ")\n" + e.StackTrace); return; }
            ret = r0; ch = c0; hz = h0;
        }
        string cual = nombre + (todo ? ", todo encendido" : ", de fabrica");
        for (int m = 1; m < 3; m++)
        {
            bool iguales = y[0].Length == y[m].Length; int primera = -1; double dmax = 0;
            for (int i = 0; i < Math.Min(y[0].Length, y[m].Length); i++) if (y[0][i] != y[m][i]) { if (primera < 0) primera = i; dmax = Math.Max(dmax, Math.Abs(y[0][i] - y[m][i])); }
            Check(iguales && primera < 0, cual + ": leer " + (m == 1 ? "a tamanos al azar" : "de a una muestra (1 s) y luego en bloques") + " da los mismos bits que en bloques"
                  + (primera >= 0 ? " (difieren desde " + (primera / ch) / (double)hz + " s, canal " + (primera % ch) + ", hasta " + Db(dmax).ToString("0") + " dBFS)" : "") + (iguales ? "" : " (largos " + y[0].Length + "/" + y[m].Length + ")"));
        }
        float[] s = y[0]; double pk = 0; bool roto = false;
        for (int i = 0; i < s.Length; i++) { if (float.IsNaN(s[i]) || float.IsInfinity(s[i])) roto = true; else pk = Math.Max(pk, Math.Abs(s[i])); }
        int framesOut = s.Length / ch;
        Check(!roto && pk <= techo * 1.0001, cual + ": nada roto y pico " + Db(pk).ToString("0.00") + " dBFS (techo " + Db(techo).ToString("0.00") + ")");
        // la longitud: lo que entra mas el cebado, que la cola del fundido entrega
        Check(framesOut == framesIn + ret, cual + ": salen " + framesOut + " cuadros = " + framesIn + " de la cancion + " + ret + " de cebado" + (framesOut == framesIn + ret ? "" : " (diferencia " + (framesOut - framesIn - ret) + ")"));
    }

    static void Retardo(string dir, int hz, bool todo)
    {
        // Retardo real: la misma cancion con y sin un clic; la diferencia tiene su maximo donde sale el clic.
        int ch = 2; float[] a = Musica(hz, ch, 6, 0, 3), b = Musica(hz, ch, 6, hz * 3, 3);
        string pa = Escribe(dir, "sin_" + hz + ".wav", a, WaveFormat.CreateIeeeFloatWaveFormat(hz, ch));
        string pb = Escribe(dir, "con_" + hz + ".wav", b, WaveFormat.CreateIeeeFloatWaveFormat(hz, ch));
        int r1, c1, h1, r2, c2, h2;
        float[] ya = Lee(Form(todo), pa, 0, out r1, out c1, out h1), yb = Lee(Form(todo), pb, 0, out r2, out c2, out h2);
        int arg = -1; double mx = 0;
        for (int i = 0; i < Math.Min(ya.Length, yb.Length); i++) { double d = Math.Abs(yb[i] - ya[i]); if (d > mx) { mx = d; arg = i / ch; } }
        Check(arg - hz * 3 == r1, (todo ? "todo encendido" : "de fabrica") + " a " + hz + " Hz: el clic sale " + (arg - hz * 3) + " cuadros despues; el retardo declarado (lo que el relevo descarta) es " + r1);
    }

    sealed class Fuente : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf;
        public Fuente(float[] x, int hz, int ch) { d = x; wf = ch <= 2 ? WaveFormat.CreateIeeeFloatWaveFormat(hz, ch) : (WaveFormat)Fmt(hz, ch); }
        static WaveFormat Fmt(int hz, int ch)
        { return (WaveFormat)TF.Assembly.GetType("Rocola.FormatoAudio").GetMethod("Flotante", T).Invoke(null, new object[] { hz, ch, (1 << ch) - 1 }); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); for (int i = 0; i < n; i++) b[o + i] = d[p + i]; p += n; return n; }
    }
    static float[] MusicaEtapa(int hz, int ch, double seg)
    {
        int n = (int)(hz * seg); float[] x = new float[n * ch]; Random r = new Random(4);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / hz, kick = 0.6 * Math.Exp(-(t % 0.5) / 0.1) * Math.Sin(2 * Math.PI * 55 * t);
            for (int c = 0; c < ch; c++)
            {
                double v = kick + 0.25 * Math.Sin(2 * Math.PI * (220 + 70 * c) * t) + 0.05 * (r.NextDouble() * 2 - 1) + (c == 1 ? 0.03 : 0.0);
                if (t % 2 < 0.03) v *= 2.5;
                x[i * ch + c] = (float)Math.Max(-1.0, Math.Min(1.0, v));
            }
        }
        return x;
    }
    static object NuevoEtapa(string tipo, params object[] a)
    {
        Type t = TF.GetNestedType(tipo, T) ?? TF.Assembly.GetType("Rocola." + tipo);
        foreach (ConstructorInfo c in t.GetConstructors(T))
        {
            ParameterInfo[] p = c.GetParameters(); if (p.Length != a.Length) continue;
            bool ok = true; for (int i = 0; i < p.Length; i++) if (a[i] != null && !p[i].ParameterType.IsAssignableFrom(a[i].GetType())) ok = false;
            if (ok) return c.Invoke(a);
        }
        throw new MissingMethodException(tipo);
    }
    static void Pon(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(o, v); return; } } throw new MissingFieldException(n); }

    delegate ISampleProvider Arma(ISampleProvider s);
    static float[] LeeEtapa(ISampleProvider s, int modo)
    {
        List<float> y = new List<float>(); float[] b = new float[9000]; Random r = new Random(11); int vacias = 0, k = 0;
        while (true)
        {
            int pide;
            if (modo == 0) pide = 4096;
            else if (modo == 1) pide = 1 + r.Next(7);                 // de 1 a 7, siempre
            else pide = (k++ % 3 == 0) ? 1 + r.Next(5) : 4096;        // uno suelto cada tanto, entre bloques
            int n = s.Read(b, 0, pide);
            if (n <= 0) { if (++vacias > 3) break; continue; }
            vacias = 0; for (int i = 0; i < n; i++) y.Add(b[i]);
        }
        return y.ToArray();
    }
    static void Prueba(string nombre, Arma arma, int hz, int ch)
    {
        float[] x = MusicaEtapa(hz, ch, 3);
        float[][] y = new float[3][];
        for (int m = 0; m < 3; m++)
        {
            try { y[m] = LeeEtapa(arma(new Fuente(x, hz, ch)), m); }
            catch (Exception e) { Check(false, nombre + " (" + ch + " canales): revienta al leer (" + (e.InnerException ?? e).GetType().Name + ")"); return; }
        }
        string q = "";
        for (int m = 1; m < 3; m++)
        {
            int primera = -1; for (int i = 0; i < Math.Min(y[0].Length, y[m].Length); i++) if (y[0][i] != y[m][i]) { primera = i; break; }
            if (primera >= 0 || y[m].Length != y[0].Length)
                q += (m == 1 ? " [de 1 a 7: " : " [sueltos entre bloques: ") + (primera >= 0 ? "difiere desde el cuadro " + primera / ch + " canal " + primera % ch : "") + (y[m].Length != y[0].Length ? " largo " + y[m].Length + " en vez de " + y[0].Length : "") + "]";
        }
        Check(q.Length == 0, "la etapa " + nombre + " (" + ch + " canales), leida de 1 a 7 muestras o con sueltas entre bloques, da los mismos bits" + q);
    }
    // ---------------------------------------------------------------- LAS CANCIONES MONO, COMO ESTEREO (24-10-2026)
    static float[] LeeLector(object lector, Random r)
    {
        ISampleProvider s = (ISampleProvider)lector; List<float> y = new List<float>(); float[] b = new float[9000]; int n;
        while ((n = s.Read(b, 0, r == null ? 4096 : 1 + r.Next(7))) > 0) for (int i = 0; i < n; i++) y.Add(b[i]);
        return y.ToArray();
    }
    static void Mono(string dir)
    {
        Console.WriteLine("\n  LAS CANCIONES MONO, COMO ESTEREO DE DOS CANALES IGUALES\n");
        int hz = 44100; float[] x = Musica(hz, 1, 3, 0, 9);
        string p = Escribe(dir, "mono16.wav", x, new WaveFormat(hz, 16, 1));
        object f = Form(false);
        // 1) El lector: dos canales, iguales, y cada uno la muestra del archivo (16 bits: la misma cuenta que mono).
        object lector = Call(f, "OpenAudio", p);
        WaveFormat wf = ((ISampleProvider)lector).WaveFormat;
        float[] a = LeeLector(lector, null); ((IDisposable)lector).Dispose();
        object lm = Call(f, "OpenAudio", p); float[] b = LeeLector(lm, new Random(3)); ((IDisposable)lm).Dispose();
        bool iguales = a.Length == 2 * x.Length, deArchivo = true;
        for (int i = 0; iguales && i < x.Length; i++) { if (a[2 * i] != a[2 * i + 1]) iguales = false; if (Math.Abs(a[2 * i] - x[i]) > 3.0 / 32768) deArchivo = false; }   // el WAV de 16 bits: NAudio escribe x 32767 truncando y se lee / 32768
        bool partidas = b.Length == a.Length; for (int i = 0; partidas && i < a.Length; i++) if (a[i] != b[i]) partidas = false;
        Check(wf.Channels == 2 && iguales && deArchivo, "un archivo mono se abre como estereo: dos canales identicos, cada uno la muestra del archivo (" + wf.Channels + " canales)");
        Check(partidas, "y leido de 1 a 7 muestras (cuadros partidos), lo mismo bit a bit");
        Type sp = TF.Assembly.GetType("Rocola.RemuestreoContinuo", true);
        bool mismo = (bool)sp.GetMethod("MismoLayout", T).Invoke(null, new object[] { wf, WaveFormat.CreateIeeeFloatWaveFormat(hz, 2) });
        Check(mismo, "con la misma disposicion que una estereo: entre las dos hay relevo sin pausa (antes se cerraba y reabria la salida)");
        // 2) Retomar a media cancion: lo que sale sigue en cuadros enteros (no queda media muestra colgando de antes del salto).
        object ls = Call(f, "OpenAudio", p); ISampleProvider ss = (ISampleProvider)ls; float[] t = new float[3]; ss.Read(t, 0, 3);
        TF.GetNestedType("IAudioSource", T).GetMethod("Salta").Invoke(ls, new object[] { TimeSpan.FromSeconds(1.0) });
        float[] tras = LeeLector(ls, null); ((IDisposable)ls).Dispose();
        bool pares = tras.Length % 2 == 0; for (int i = 0; pares && i + 1 < tras.Length; i += 2) if (tras[i] != tras[i + 1]) pares = false;
        Check(pares && tras.Length > 0, "al retomar a media cancion sigue en cuadros enteros y con los dos canales iguales");
        // 3) El balance del local se le aplica (antes solo con dos canales).
        object fb = Form(false);   // (Form deja la clase de fabrica: el balance se pone despues)
        TF.GetField("balanceDb", T).SetValue(null, 4);
        int r0, c0, h0; float[] y = Lee(fb, p, 0, out r0, out c0, out h0);
        RestauraEstaticos();
        double eL = 0, eR = 0; for (int i = 0; i + 1 < y.Length; i += 2) { eL += y[i] * (double)y[i]; eR += y[i + 1] * (double)y[i + 1]; }
        double bal = 10 * Math.Log10(Math.Max(eR, 1e-30) / Math.Max(eL, 1e-30));
        Check(c0 == 2 && Math.Abs(bal) > 1.0, "y lleva el balance del local: con el balance movido, un canal sale " + Math.Abs(bal).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " dB por debajo del otro");
    }

    // ---- 7) el cambio de pista en un disco continuo ----
    static object ArmaPista(object f, string ruta, out object lector)
    {
        Type tt = TF.GetNestedType("Track", T);
        object track = Activator.CreateInstance(tt, true); tt.GetField("Path", T).SetValue(track, ruta);
        lector = Call(f, "OpenAudio", ruta);
        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
        object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
        object tap = Get(cad, "Tap"); tap.GetType().GetField("Gain", T).SetValue(tap, 1.0f);   // la misma ganancia en las tres: se mira el empalme, no el nivelado
        return cad;
    }
    static float[] Todo(object cad)
    {
        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida"); List<float> y = new List<float>(); float[] b = new float[2205 * 2]; int n;
        while ((n = sal.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) y.Add(b[i]);
        return y.ToArray();
    }
    static void Fundido(object cad, string m) { object fu = Get(cad, "Fundido"); fu.GetType().GetMethod(m, T).Invoke(fu, null); }
    static double Empalme(string dir, Action<object> pon, double nivel, out double aparta, out double normal)
    { int largo; return Empalme(dir, pon, nivel, 0, out aparta, out normal, out largo); }
    // Con 'salidaHz' > 0 las tres van convertidas a esa frecuencia, como en el relevo con la salida abierta a otra (ConvierteSalida).
    // 'largo': los cuadros de las dos pistas seguidas menos los de la entera (0 = los mismos).
    static double Empalme(string dir, Action<object> pon, double nivel, int salidaHz, out double aparta, out double normal, out int largo)
    {
        int fs = 44100, n = fs * 8, corte = fs * 4 + 1037;
        float[] x = Musica(fs, 2, 8, 0, 11);
        for (int i = 0; i < n; i++)
        {   // y un bajo abierto a un lado (lado con grave), para el grave en mono y el subgrave
            double t = (double)i / fs, bj = 0.25 * Math.Sin(2 * Math.PI * 38 * t) + 0.2 * Math.Sin(2 * Math.PI * 61 * t);
            x[2 * i] = (float)(nivel * (x[2 * i] + bj)); x[2 * i + 1] = (float)(nivel * (x[2 * i + 1] + 0.3 * bj));
        }
        float[] xa = new float[2 * corte], xb = new float[2 * (n - corte)];
        Array.Copy(x, xa, xa.Length); Array.Copy(x, 2 * corte, xb, 0, xb.Length);
        WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, 2);
        string pa = Escribe(dir, "ea.wav", xa, wf), pb = Escribe(dir, "eb.wav", xb, wf), pc = Escribe(dir, "ec.wav", x, wf);
        object f = Form(false); Set(f, "saltaSilencios", false); pon(f);
        object la, lb, lc;
        object cA = ArmaPista(f, pa, out la), cB = ArmaPista(f, pb, out lb), cC = ArmaPista(f, pc, out lc);
        Fundido(cA, "Quita"); Fundido(cC, "Quita");
        if (salidaHz > 0)
        {
            Type rc = TF.Assembly.GetType("Rocola.RemuestreoContinuo", true);
            WaveFormat a = (WaveFormat)TF.Assembly.GetType("Rocola.FormatoAudio", true).GetMethod("Flotante", T).Invoke(null, new object[] { salidaHz, 2, 3 });
            double[] tabla = (double[])rc.GetMethod("CreaFiltro", T).Invoke(null, new object[] { fs, salidaHz });
            foreach (object c in new object[] { cA, cB, cC }) TF.GetMethod("ConvierteSalida", T).Invoke(null, new object[] { c, a, tabla });
            fs = salidaHz;
        }
        TF.GetMethod("SigueA", T).Invoke(null, new object[] { cB, cA });   // lo que hace el relevo de un disco continuo
        Fundido(cB, "SinEntrada");
        float[] yA = Todo(cA), yB = Todo(cB), yC = Todo(cC);
        ((IDisposable)la).Dispose(); ((IDisposable)lb).Dispose(); ((IDisposable)lc).Dispose();
        int rA = (int)Get(cA, "Retardo"), rB = (int)Get(cB, "Retardo"), rC = (int)Get(cC, "Retardo");
        int nA = yA.Length / 2 - rA, nB = yB.Length / 2 - rB;
        largo = nA + nB - (yC.Length / 2 - rC);
        double escalon = 0; aparta = 0; normal = 0;
        for (int c = 0; c < 2; c++)
        {
            double antes = 0;
            for (int i = 0; i < nA + nB && rC + i < yC.Length / 2; i++)
            {
                double v = i < nA ? yA[2 * (rA + i) + c] : yB[2 * (rB + i - nA) + c], d = v - yC[2 * (rC + i) + c];
                aparta = Math.Max(aparta, Math.Abs(d));
                if (i == nA) escalon = Math.Max(escalon, Math.Abs(d - antes));
                antes = d;
                if (i > nA - fs / 10 && i < nA - 10) normal = Math.Max(normal, Math.Abs(yC[2 * (rC + i) + c] - yC[2 * (rC + i - 1) + c]));
            }
        }
        return escalon;
    }
    static void DiscoContinuo(string dir)
    {
        Console.WriteLine("\n  EL CAMBIO DE PISTA EN UN DISCO CONTINUO (dos pistas seguidas frente a la cancion entera)\n");
        string[] nom = { "de fabrica", "con la claridad", "modo automatico del local: billar", "con los graves del local +3 y el subgrave" };
        Action<object>[] pon = {
            delegate(object f) { },
            delegate(object f) { Set(f, "claridadLocal", true); },
            delegate(object f) { Set(f, "localAuto", true); },
            delegate(object f) { Set(f, "gravesLocalDb", 3); Set(f, "cortaSubgrave", true); } };
        for (int k = 0; k < nom.Length; k++)
        {
            double aparta, normal, e = Empalme(dir, pon[k], 0.3, out aparta, out normal);
            Check(e < 0.002, nom[k] + ": el cambio no da escalon (" + e.ToString("0.00000") + "; el salto normal entre muestras es " + normal.ToString("0.0000")
                  + "; lo que mas se aparta de la cancion entera, " + aparta.ToString("0.0000") + ", lo que la anterior no podia saber de la siguiente)");
        }
        {   // (28-10-2026) el modo billar fuerte: el limitador que respira, los agudos asperos y el oido del volumen parejo en el cambio
            double aparta, normal, e = Empalme(dir, delegate(object f) { Set(f, "localAuto", true); }, 1.6, out aparta, out normal);
            Check(e < 0.005, "modo billar y fuerte, con el limitador trabajando en el cambio: escalon " + e.ToString("0.0000") + "; salto normal " + normal.ToString("0.0000"));
        }
        {
            double aparta, normal, e = Empalme(dir, delegate(object f) { }, 1.6, out aparta, out normal);
            Check(e < 0.005, "y fuerte, con el limitador trabajando en el cambio: escalon " + e.ToString("0.0000") + " (la version publicada, 0,46); salto normal " + normal.ToString("0.0000"));
        }
        foreach (int hz in new int[] { 48000, 32000 })
        {
            int largo; double aparta, normal, e = Empalme(dir, delegate(object f) { }, 0.3, hz, out aparta, out normal, out largo);
            Check(e < 0.002 && largo == 0, "convertido a " + (hz / 1000) + " kHz (la salida abierta a otra frecuencia): el cambio no da escalon (" + e.ToString("0.00000")
                  + "; con un convertidor por pista, 0,04 a 0,07; salto normal " + normal.ToString("0.0000") + ") y salen los cuadros de la entera ("
                  + (largo == 0 ? "los mismos" : (largo > 0 ? "+" : "") + largo) + ")");
        }
        {   // Y si la que iba a seguir se retira sin empezar (al cambiar un interruptor en Ctrl+F3), la que suena acaba entera.
            Type rc = TF.Assembly.GetType("Rocola.RemuestreoContinuo", true);
            WaveFormat a = (WaveFormat)TF.Assembly.GetType("Rocola.FormatoAudio", true).GetMethod("Flotante", T).Invoke(null, new object[] { 48000, 2, 3 });
            double[] tabla = (double[])rc.GetMethod("CreaFiltro", T).Invoke(null, new object[] { 44100, 48000 });
            ConstructorInfo k = rc.GetConstructor(T, null, new Type[] { typeof(ISampleProvider), typeof(WaveFormat), typeof(int), typeof(double[]) }, null);
            float[] x = Musica(44100, 2, 1, 0, 5);
            Func<bool, int> largoDe = delegate(bool retira)
            {
                ISampleProvider ra = (ISampleProvider)k.Invoke(new object[] { new Fuente(x, 44100, 2), a, 0, tabla });
                object rb = k.Invoke(new object[] { new Fuente(x, 44100, 2), a, 0, tabla });
                rc.GetMethod("Sigue", T).Invoke(rb, new object[] { ra });
                if (retira) rc.GetMethod("NoSigue", T).Invoke(rb, null);
                float[] b = new float[4096]; int n, t = 0; while ((n = ra.Read(b, 0, b.Length)) > 0) t += n; return t / 2;
            };
            ISampleProvider sola = (ISampleProvider)k.Invoke(new object[] { new Fuente(x, 44100, 2), a, 0, tabla });
            float[] bs = new float[4096]; int ns, ts = 0; while ((ns = sola.Read(bs, 0, bs.Length)) > 0) ts += ns; ts /= 2;
            int cede = largoDe(false), retirada = largoDe(true);
            Check(retirada == ts && cede < ts, "si la que iba a seguir se retira, la que suena acaba entera (" + retirada + " cuadros, sola " + ts
                  + "; la que cede a la siguiente, " + cede + ")");
        }
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        GuardaEstaticos();
        string dir = Path.Combine(Path.GetTempPath(), "rocola_cadenaentera_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Console.WriteLine("\n  LA CADENA ENTERA (OpenAudio + ArmaCadena)\n");
            List<object[]> casos = new List<object[]>();
            casos.Add(new object[] { "estereo flotante 44,1 kHz", 44100, 2, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2) });
            casos.Add(new object[] { "estereo 16 bits 48 kHz", 48000, 2, new WaveFormat(48000, 16, 2) });
            casos.Add(new object[] { "estereo 24 bits 96 kHz", 96000, 2, new WaveFormat(96000, 24, 2) });
            casos.Add(new object[] { "mono 16 bits 22,05 kHz", 22050, 1, new WaveFormat(22050, 16, 1) });
            casos.Add(new object[] { "6 canales 24 bits 48 kHz", 48000, 6, new WaveFormatExtensible(48000, 24, 6) });
            foreach (object[] c in casos)
            {
                int hz = (int)c[1], ch = (int)c[2];
                float[] x = Musica(hz, ch, 6, 0, 7);
                string p = Escribe(dir, "c_" + hz + "_" + ch + ".wav", x, (WaveFormat)c[3]);
                foreach (bool todo in new bool[] { false, true }) Caso((string)c[0], p, x.Length / ch, 0, todo);
            }
            Console.WriteLine("\n  EL RETARDO DECLARADO, CONTRA EL DE VERDAD\n");
            foreach (int hz in new int[] { 44100, 48000 }) foreach (bool todo in new bool[] { false, true }) Retardo(dir, hz, todo);
            Mono(dir);
            DiscoContinuo(dir);
            Console.WriteLine("\n  CADA ETAPA SOLA, CON LECTURAS PARTIDAS\n");
            F = Form(false);
            float techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);
            foreach (int ch in new int[] { 1, 2, 6 })
            {
                int hz = 44100;
                Prueba("arreglos S1/S2", s => (ISampleProvider)NuevoEtapa("ArreglaGrabacion", s, true, true), hz, ch);
                Prueba("des-recorte", s => (ISampleProvider)NuevoEtapa("DeclipProvider", s), hz, ch);
                Prueba("quitar el siseo", s => (ISampleProvider)NuevoEtapa("QuitaSiseo", s), hz, ch);
                Prueba("DSEE encendido", s => { object t = NuevoEtapa("SpectralTap", s); Pon(t, "Active", true); return (ISampleProvider)t; }, hz, ch);
                Prueba("nivel + realce", s => { object t = NuevoEtapa("LevelTap", s, F); Pon(t, "Gain", 1.5f); Pon(t, "Excite", 0.6f); Pon(t, "Master", 1f); return (ISampleProvider)t; }, hz, ch);
                Prueba("grave en mono + estrecho + subgrave", s => (ISampleProvider)NuevoEtapa("GraveMono", s, 120f, 0.6f, 35f, true, true), hz, ch);
                Prueba("el local (todo)", s => { object tap = NuevoEtapa("LevelTap", s, F); return (ISampleProvider)NuevoEtapa("SalaLocal", tap, tap, 0.26f, true, true, 3, 4, -3); }, hz, ch);
                Prueba("presencia", s => { object tap = NuevoEtapa("LevelTap", s, F); return (ISampleProvider)NuevoEtapa("PresenciaSala", tap, tap, 0.26f, true); }, hz, ch);
                Prueba("bajo psicoacustico", s => (ISampleProvider)NuevoEtapa("DeepBassProvider", s, 0.8f), hz, ch);
                Prueba("limitador de pico real", s => (ISampleProvider)NuevoEtapa("LimitadorPicoReal", s, techo * 0.5f, 3.0), hz, ch);
                Prueba("fundido (entrada y salida)", s => (ISampleProvider)NuevoEtapa("FadeProvider", s, (long)(hz * 3), 0.5, 100L, 0.005), hz, ch);
            }
        }
        finally { RestauraEstaticos(); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

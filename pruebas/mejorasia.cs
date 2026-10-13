using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// LAS MEJORAS CON IA DEL SONIDO (13-10-2026; IA_EN_EL_SONIDO_MODEST_2026-10-13.md). Las tres con su interruptor en
// Ctrl+F3 (Sonido -> Mejoras con IA), apagadas de fabrica:
//   I1 "Bajo y brillo a la medida de cada cancion": el bajo y el realce se comparan con el objetivo de ESTA cancion (la red
//      del EQ) y no con el promedio. Apagado, o sin modelo del EQ, todo como antes.
//   I2 "Quitar el siseo de grabaciones viejas" (Siseo.cs): baja hasta 6 dB el piso fijo de ruido de los agudos. Lo que no
//      toca sale con los mismos bits; en la cadena solo con el interruptor.
//   I3 "Modelos del DSEE por genero": rocola_ia_genero_<genero>.bin para las canciones de ese genero; el nombre sale de la
//      misma cuenta que ia/entrenar_ia.py (slug_genero) y se comprueba con los mismos ejemplos que ia/test_genero_dsee.py.
// Con modelos de prueba hechos aqui (pesos a mano) y senales sinteticas: miden la cuenta, no el oido.
class MejorasIa
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type TF; static object f;
    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo x = t.GetField(n, T | BindingFlags.DeclaredOnly); if (x != null) return x.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void Set(object o, string n, object v)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo x = t.GetField(n, T | BindingFlags.DeclaredOnly); if (x != null) { x.SetValue(o, v); return; } }
        throw new Exception("no hay " + n);
    }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }
    static object Estatico(string n) { return TF.GetField(n, T).GetValue(null); }

    // Un modelo RIA1 de una capa: pesos a cero y los sesgos que se le den (la salida es siempre esa).
    static void Ria1(string ruta, int entradas, float[] salida)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(ruta)))
        {
            w.Write(new byte[] { (byte)'R', (byte)'I', (byte)'A', (byte)'1' }); w.Write(1);
            w.Write(salida.Length); w.Write(entradas);
            for (int i = 0; i < salida.Length * entradas; i++) w.Write(0f);
            foreach (float b in salida) w.Write(b);
        }
    }

    class Arr : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf;
        public Arr(float[] d, int fs, int ch) { this.d = d; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int k = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, k); p += k; return k; }
    }
    // 0-6 s: un piano suave (sin agudos); 6-14 s: banda con platillos; 14-20 s: coda suave. Con siseo de 'ruido' (rms).
    static float[] Musica(int fs, int ch, double ruido, int semilla)
    {
        int n = fs * 20; float[] x = new float[n * ch]; Random r = new Random(semilla), rn = new Random(semilla + 99); double hp = 0, ant = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs, v;
            if (t < 6) v = 0.08 * Math.Sin(2 * Math.PI * 262 * t) * Math.Exp(-(t % 1.0) * 2);
            else if (t < 14)
            {
                double tb = t % 0.5; v = 0.4 * Math.Exp(-tb / 0.1) * Math.Sin(2 * Math.PI * 60 * tb) + 0.15 * Math.Sin(2 * Math.PI * 110 * t);
                double w = r.NextDouble() * 2 - 1; hp = 0.5 * (hp + w - ant); ant = w; v += 0.25 * Math.Exp(-(t % 0.25) / 0.02) * hp + 0.06 * Math.Sin(2 * Math.PI * 880 * t);
            }
            else v = 0.05 * Math.Sin(2 * Math.PI * 330 * t) * Math.Exp(-(t - 14) / 3);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(v * (c == 0 ? 1 : 0.9) + ruido * (rn.NextDouble() * 2 - 1) * 1.732);
        }
        return x;
    }
    // Energia por encima de unos 3 kHz (paso-alto de cuatro polos) entre dos segundos, canal 0, en dB.
    static double Agudos(float[] y, int fs, int ch, double desde, double hasta)
    {
        double a = Math.Exp(-2 * Math.PI * 3000.0 / fs), e = 0; int n = 0; double[] s = new double[4], xp = new double[4];
        for (int i = (int)(desde * fs); i < (int)(hasta * fs); i++)
        {
            double x = y[i * ch]; for (int k = 0; k < 4; k++) { double o = a * (s[k] + x - xp[k]); xp[k] = x; s[k] = o; x = o; }
            if (i > desde * fs + 2000) { e += x * x; n++; }
        }
        return 10 * Math.Log10(e / Math.Max(1, n) + 1e-30);
    }
    static float[] Pasa(Type tq, float[] x, int fs, int ch, out object q)
    {
        q = Activator.CreateInstance(tq, T, null, new object[] { new Arr(x, fs, ch) }, null);
        ISampleProvider sp = (ISampleProvider)q; List<float> y = new List<float>(); float[] b = new float[2205 * ch + 1]; int k;
        while ((k = sp.Read(b, 0, b.Length)) > 0) for (int i = 0; i < k; i++) y.Add(b[i]);
        return y.ToArray();
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_mejorasia_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            f = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));

            // ======================= I1 =======================
            float[] avg = { -2f, 0f, 0f, 0f, 0f, 0f, -6f, -12f };
            Set(f, "eqAvg", avg);
            Set(f, "eqAuto", false); Set(f, "eqAmount", 1f);   // el EQ a mano, al 100 %: para ver su correccion
            IDictionary eqCache = (IDictionary)Get(f, "eqCache");
            int eqLen = (int)Estatico("EqLen"), eqCut = (int)Estatico("EqCut");
            float[] c = new float[eqLen];
            float[] huella = { -8f, 1f, 0f, -1f, 0f, -2f, -14f, -22f };   // poco grave profundo y poco aire
            Array.Copy(huella, c, 8); c[eqCut] = 19000f; c[eqLen - 1] = (float)Estatico("EqVersionOriginal");
            string cancion = Path.Combine(dir, "a_la_medida.mp3");
            eqCache[cancion] = c;
            Func<float> bajo = delegate { return (float)Call(f, "BassFactorFor", cancion); };
            Func<float> brillo = delegate { return (float)Call(f, "ExciteFactorFor", cancion); };
            object eqNet = Estatico("EqNet");
            Check(!(bool)eqNet.GetType().GetField("Loaded", T).GetValue(eqNet), "sin modelo del EQ cargado al empezar");
            Set(f, "aLaMedida", false); float b0 = bajo(), e0 = brillo();
            Set(f, "aLaMedida", true); float b1 = bajo(), e1 = brillo();
            Check(b0 == b1 && e0 == e1, "I1 sin modelo del EQ: encendido o apagado, el bajo y el brillo como siempre");
            // Un modelo del EQ que dice: a esta cancion le tocan -9 dB a 50 Hz, -15 a 10 kHz y -20 a 15,8 kHz.
            string modeloEq = Path.Combine(dir, "eq_prueba.bin");
            Ria1(modeloEq, 5, new float[] { -9f, -15f, -20f });
            eqNet.GetType().GetMethod("TryLoad", T).Invoke(eqNet, new object[] { modeloEq });
            Check((bool)eqNet.GetType().GetField("Loaded", T).GetValue(eqNet), "el modelo del EQ de prueba se carga");
            Set(f, "aLaMedida", false); float b2 = bajo(), e2 = brillo();
            Check(b2 == b0 && e2 == e0, "I1 apagado con modelo: el bajo y el brillo no cambian (la vara sigue siendo el promedio)");
            object eqApagado = Call(f, "EqCorrFor", cancion);
            Set(f, "aLaMedida", true); float b3 = bajo(), e3 = brillo();
            object eqEncendido = Call(f, "EqCorrFor", cancion);
            // La vara: el promedio con 50 Hz, 10 y 15,8 kHz del modelo, a 6 dB como mucho del promedio (-8, -12 y -18).
            float[] vara = { -8f, 0f, 0f, 0f, 0f, 0f, -12f, -18f };
            float relEsperado = (huella[0] - vara[0]) - (huella[1] - vara[1]);
            float bEsperado = Math.Max(0.45f, Math.Min(1.55f, 1f + relEsperado / 12f));
            Check(Math.Abs(b3 - bEsperado) < 1e-5f, "I1 encendido: el bajo con la vara de ESTA cancion (" + b3 + ", esperado " + bEsperado + "; con el promedio era " + b2 + ")");
            float falta = ((vara[6] - huella[6]) + (vara[7] - huella[7])) / 2f;
            float eEsperado = e2 * Math.Max(0.80f, Math.Min(1.25f, 1f + falta / 12f));
            Check(Math.Abs(e3 - eEsperado) < 1e-5f, "I1 encendido: el brillo segun el aire que le falta frente a su objetivo (" + e3 + ", esperado " + eEsperado + ")");
            Check(e3 > e2, "I1: a la que le falta aire se le da mas brillo que con el promedio");
            float[] ea = (float[])eqApagado, ee = (float[])eqEncendido;
            bool eqIgual = (ea == null) == (ee == null);
            for (int i = 0; eqIgual && ea != null && i < ea.Length; i++) eqIgual = ea[i] == ee[i];
            Check(eqIgual && ea != null, "I1 no cambia el EQ automatico: ya usaba el objetivo de la cancion");
            // Un modelo desbocado no se pasa de 6 dB del promedio.
            Ria1(modeloEq + "2", 5, new float[] { -40f, 30f, 30f });
            eqNet.GetType().GetMethod("TryLoad", T).Invoke(eqNet, new object[] { modeloEq + "2" });
            float b4 = bajo();
            float rel4 = (huella[0] - (avg[0] - 6f)) - (huella[1] - avg[1]);
            Check(Math.Abs(b4 - Math.Max(0.45f, Math.Min(1.55f, 1f + rel4 / 12f))) < 1e-5f, "I1: el objetivo de la red se queda a 6 dB del promedio como mucho");
            float e4 = brillo();
            Check(e4 <= e2 * 1.25f + 1e-5f && e4 >= e2 * 0.80f - 1e-5f, "I1: el brillo se mueve como mucho un 25 % arriba o un 20 % abajo");
            // En vivo: al cambiar el interruptor, el bajo de la que suena se recalcula.
            Type tt = TF.GetNestedType("Track", T); object track = Activator.CreateInstance(tt, true); Set(track, "Path", cancion);
            Type tp = TF.GetField("nowPlaying", T).FieldType; object item = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp); Set(item, "Song", track);
            Set(f, "nowPlaying", item); Set(f, "bassSongFactor", 1f);
            Set(f, "aLaMedida", true); Call(f, "AplicaALaMedida");
            Check((float)Get(f, "bassSongFactor") == bajo(), "I1 en vivo: el bajo de la cancion que suena toma la vara nueva");
            Set(f, "nowPlaying", null);

            // ======================= I2 =======================
            Type tq = asm.GetType("Rocola.QuitaSiseo", true);
            const int fs = 44100;
            object q;
            float[] limpia = Musica(fs, 2, 0, 7);
            float[] yl = Pasa(tq, limpia, fs, 2, out q);
            int h = (int)tq.GetProperty("DelayFrames", T).GetValue(q, null);
            Check(h > 0 && yl.Length == limpia.Length + h * 2, "I2: sale lo que entra mas su retraso (" + h + " cuadros)");
            int distintas = 0;
            for (int i = 0; i < limpia.Length; i++) if (yl[i + 2 * h] != limpia[i]) distintas++;
            Check(distintas == 0 && (long)tq.GetField("TrozosConQuita", T).GetValue(q) == 0, "I2: en musica limpia no toca nada: los mismos bits, solo retrasados (" + distintas + " distintas)");
            foreach (double dbRuido in new double[] { -55, -65 })
            {
                float[] sucia = Musica(fs, 2, Math.Pow(10, dbRuido / 20), 7);
                float[] ys = Pasa(tq, sucia, fs, 2, out q);
                float[] al = new float[sucia.Length]; Array.Copy(ys, 2 * h, al, 0, sucia.Length);
                double codaE = Agudos(sucia, fs, 2, 18, 20), codaS = Agudos(al, fs, 2, 18, 20);   // tras aprender (unos 3 s de coda)
                double bandaE = Agudos(sucia, fs, 2, 7, 14), bandaS = Agudos(al, fs, 2, 7, 14);
                Check(codaE - codaS >= 3.0 && codaE - codaS <= 6.5, "I2 siseo a " + dbRuido + " dB: en la coda suave baja entre 3 y 6 dB (" + (codaE - codaS).ToString("0.0") + " dB)");
                Check(Math.Abs(bandaE - bandaS) < 0.5, "I2 siseo a " + dbRuido + " dB: con la banda sonando fuerte, los agudos casi igual (" + (bandaE - bandaS).ToString("0.00") + " dB)");
                bool sano = true; foreach (float v in ys) if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2f) sano = false;
                Check(sano, "I2: nada roto en la salida");
            }
            // Disco continuo: la pista siguiente hereda el piso aprendido y su intro ya sale sin siseo; sola, espera 3 s.
            {
                double ruido = Math.Pow(10, -55 / 20.0);
                object qa; Pasa(tq, Musica(fs, 2, ruido, 11), fs, 2, out qa);
                float[] intro = Musica(fs, 2, ruido, 12);
                object qb = Activator.CreateInstance(tq, T, null, new object[] { new Arr(intro, fs, 2) }, null);
                tq.GetMethod("Sigue", T).Invoke(qb, new object[] { qa });
                List<float> yb = new List<float>(); float[] bb = new float[4410]; int kb;
                while ((kb = ((ISampleProvider)qb).Read(bb, 0, bb.Length)) > 0) for (int i = 0; i < kb; i++) yb.Add(bb[i]);
                float[] heredada = new float[intro.Length]; Array.Copy(yb.ToArray(), 2 * h, heredada, 0, intro.Length);
                object qs; float[] ys2 = Pasa(tq, intro, fs, 2, out qs);
                float[] sola = new float[intro.Length]; Array.Copy(ys2, 2 * h, sola, 0, intro.Length);
                double ei = Agudos(intro, fs, 2, 0.5, 3.0), eh = Agudos(heredada, fs, 2, 0.5, 3.0), es = Agudos(sola, fs, 2, 0.5, 3.0);
                Check(ei - eh >= 3.0, "I2 disco continuo: la intro de la siguiente ya sale sin siseo (" + (ei - eh).ToString("0.0") + " dB)");
                Check(Math.Abs(ei - es) < 0.5, "I2 sin heredar: la intro espera a aprender (" + (ei - es).ToString("0.0") + " dB)");
            }
            // Mono, y con mas de dos canales pasa tal cual.
            float[] mono = Musica(fs, 1, Math.Pow(10, -55 / 20.0), 9);
            float[] ym = Pasa(tq, mono, fs, 1, out q);
            float[] alm = new float[mono.Length]; Array.Copy(ym, h, alm, 0, mono.Length);
            Check(Agudos(mono, fs, 1, 18, 20) - Agudos(alm, fs, 1, 18, 20) >= 3.0, "I2 en mono tambien baja el siseo");
            float[] seis = new float[fs * 6]; for (int i = 0; i < seis.Length; i++) seis[i] = (float)Math.Sin(i * 0.01);
            float[] y6 = Pasa(tq, seis, fs, 6, out q);
            bool igual6 = y6.Length == seis.Length; for (int i = 0; igual6 && i < seis.Length; i++) igual6 = y6[i] == seis[i];
            Check(igual6 && (int)tq.GetProperty("DelayFrames", T).GetValue(q, null) == 0, "I2 con 6 canales pasa tal cual, sin retraso");
            // En la cadena de verdad: solo con el interruptor, y su retraso se suma al de la cadena.
            string wav = Path.Combine(dir, "siseo.wav");
            using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(limpia, 0, limpia.Length);
            object track2 = Activator.CreateInstance(tt, true); Set(track2, "Path", wav);
            foreach (bool on in new bool[] { false, true })
            {
                Set(f, "quitaSiseo", on);
                object lector = Call(f, "OpenAudio", wav);
                TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
                object cad = Call(f, "ArmaCadena", track2, lector, false, 0.0);
                bool hay = false; object nodo = Get(cad, "Salida");
                for (int paso = 0; nodo != null && paso < 40; paso++) { if (nodo.GetType() == tq) hay = true; object up = null; try { up = Get(nodo, "src"); } catch { } nodo = up; }
                int ret = (int)Get(cad, "Retardo");
                Check(hay == on, "I2 en la cadena: " + (on ? "con el interruptor, esta" : "apagado, ni se pone"));
                Check((Get(cad, "Siseo") != null) == on, "I2: la cadena lo apunta para que la siguiente del disco herede");
                if (on) Check(ret >= h, "I2: su retraso cuenta en la cola de la cadena (" + ret + ")");
                ((IDisposable)lector).Dispose();
            }
            Set(f, "quitaSiseo", false);

            // ======================= I3 =======================
            MethodInfo slug = TF.GetMethod("SlugGenero", T);
            string[][] ejemplos = {
                new[] { "Cumbia", "cumbia" }, new[] { "  Regional Mexicano ", "regional_mexicano" }, new[] { "Rock & Roll", "rock_roll" },
                new[] { "Música Tropical", "musica_tropical" }, new[] { "Baño de Pop", "bano_de_pop" }, new[] { "Unknown", "" },
                new[] { "desconocido", "" }, new[] { "", "" }, new[] { "80's", "80_s" }, new[] { "Salsa/Merengue", "salsa_merengue" },
                new[] { "__x__", "x" }, new[] { new string('a', 60), new string('a', 40) } };
            int iguales = 0;
            foreach (string[] e in ejemplos) { string r = (string)slug.Invoke(null, new object[] { e[0] }); if (r == e[1]) iguales++; else Console.WriteLine("        slug('" + e[0] + "') = '" + r + "', esperado '" + e[1] + "'"); }
            Check(iguales == ejemplos.Length, "I3: el nombre de cada genero sale igual que en ia/entrenar_ia.py (" + iguales + " de " + ejemplos.Length + ")");
            float[] doce = new float[12]; for (int i = 0; i < 12; i++) doce[i] = -1f - i * 0.1f;
            Ria1(Path.Combine(dir, "rocola_ia_genero_cumbia.bin"), 28, doce);
            Ria1(Path.Combine(dir, "rocola_ia_genero_Rock.bin"), 28, doce);                 // un nombre que no sale de una etiqueta
            File.WriteAllBytes(Path.Combine(dir, "rocola_ia_genero_salsa.bin"), new byte[] { 1, 2, 3 });   // roto
            Ria1(Path.Combine(dir, "rocola_ia_genero_bolero.bin"), 5, new float[] { 1, 2, 3 });   // de otro contrato
            Call(null, "CargaModelosPorGenero");
            Check((int)Call(null, "ModelosPorGenero") == 1, "I3: carga solo el modelo valido y bien nombrado (" + Call(null, "ModelosPorGenero") + ")");
            object cumbia = Activator.CreateInstance(tt, true); Set(cumbia, "Path", Path.Combine(dir, "c.mp3")); Set(cumbia, "Genre", "Cumbia");
            object rock = Activator.CreateInstance(tt, true); Set(rock, "Path", Path.Combine(dir, "r.mp3")); Set(rock, "Genre", "Rock");
            Set(f, "dseePorGenero", false);
            Check(Call(f, "ModeloDseePara", cumbia) == null, "I3 apagado: el modelo general para todas");
            Set(f, "dseePorGenero", true);
            object m = Call(f, "ModeloDseePara", cumbia);
            Check(m != null && (bool)m.GetType().GetField("Loaded", T).GetValue(m), "I3 encendido: una cumbia usa el modelo de cumbia");
            Check(Call(f, "ModeloDseePara", rock) == null, "I3: un genero sin modelo usa el general");
            // En la cadena: el DSEE de la cancion lleva su modelo.
            Set(f, "sbrAllowed", true); Set(f, "sbrOn", true);
            Set(cumbia, "Path", wav);
            object lec = Call(f, "OpenAudio", wav);
            TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lec, 1.0f, null);
            object cad2 = Call(f, "ArmaCadena", cumbia, lec, false, 0.0);
            object sbr = Get(cad2, "Sbr");
            Check(sbr != null && object.ReferenceEquals(Get(sbr, "Red"), m), "I3: el DSEE de la cumbia lleva el modelo de cumbia");
            // Instalado con la rocola abierta (instalar_dsee_genero.bat): al abrir la hoja de Ctrl+F3 se vuelven a buscar y ya cuenta.
            Ria1(Path.Combine(dir, "rocola_ia_genero_salsa.bin"), 28, doce);
            Call(null, "CargaModelosPorGenero");
            Set(rock, "Genre", "Salsa");
            Check((int)Call(null, "ModelosPorGenero") == 2 && Call(f, "ModeloDseePara", rock) != null,
                  "I3: un modelo instalado con la rocola abierta se encuentra al volver a buscar (" + Call(null, "ModelosPorGenero") + ")");
            ((IDisposable)lec).Dispose();
            Set(f, "dseePorGenero", false);
            lec = Call(f, "OpenAudio", wav);
            TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lec, 1.0f, null);
            cad2 = Call(f, "ArmaCadena", cumbia, lec, false, 0.0);
            Check(Get(Get(cad2, "Sbr"), "Red") == null, "I3 apagado: el DSEE lleva el general");
            ((IDisposable)lec).Dispose();

            // Las tres, apagadas de fabrica y guardadas en la configuracion.
            object nuevo = Codigo.FormSinEncender(TF);
            Check(!(bool)Get(nuevo, "aLaMedida") && !(bool)Get(nuevo, "quitaSiseo") && !(bool)Get(nuevo, "dseePorGenero"), "las tres apagadas de fabrica");
        }
        catch (Exception e) { Exception z = e is TargetInvocationException ? e.InnerException : e; Check(false, "excepcion: " + z.GetType().Name + ": " + z.Message + "\n" + z.StackTrace); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK mejorasia: " + ok + " comprobaciones (modelos de prueba y senales sinteticas; no es oirlo en el bar)"
                                   : "  mejorasia: " + mal + " FALLOS de " + (ok + mal));
        return mal;
    }
}

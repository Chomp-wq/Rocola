using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// LOS RESTOS DIMINUTOS DE LOS FILTROS, A CERO (Desnormales.cs, 12-10-2026).
//
// En el silencio exacto, los filtros con memoria se apagan sin llegar a cero y acaban en numeros "desnormales", que el
// procesador cuenta decenas de veces mas despacio. La cadena de fabrica gastaba siete veces mas en una cola muda que
// con musica. Aqui se arma la cadena de verdad (OpenAudio + ArmaCadena, con todo lo de fabrica, el modo noche, la medida
// del original y la huella del EQ aprendiendo, y la aurora pidiendo su espectro) y se le da una cancion de 3 s de musica
// y 6 s de ceros exactos, sin saltar el silencio. Se mira:
//   1) que tras dos segundos de silencio la salida sea cero exacto (antes: restos de 1e-20 y menos, sin fin);
//   2) que ningun estado de ninguna etapa quede en desnormal ni en un resto por debajo del tope (lo que se limpia);
//   3) que el silencio no cueste mas que la musica (con margen: es una medida de tiempo y la maquina puede ir cargada);
//   4) lo mismo en el repaso a fondo de la biblioteca (MeasureFile entero): la huella del EQ y la sonoridad.
// Que con musica lo que suena sea el mismo, muestra a muestra y byte a byte en 16 bits, se comprobo comparando el
// ejecutable de antes y el de ahora sobre canciones enteras (ver RENDIMIENTO_SILENCIOS_MODEST_2026-10-12.md).
// Son senales sinteticas: miden la cuenta, no el oido.
class Desnormales
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) { ok++; Console.WriteLine("  OK    " + s); } else { mal++; Console.WriteLine("  FALLO " + s); } }
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
    static void SetSiHay(object o, string n, object v) { try { Set(o, n, v); } catch { } }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }

    // 3 s de musica (bombo, bajo, platillos, acordes y melodia) y 6 s de ceros exactos.
    static float[] Cancion(int fs, double musica, double silencio)
    {
        int n = (int)(fs * (musica + silencio)), m = (int)(fs * musica); float[] x = new float[2 * n]; Random r = new Random(21);
        double hp = 0, ant = 0;
        for (int i = 0; i < m; i++)
        {
            double t = (double)i / fs, tb = t % 0.5;
            double kick = 0.5 * Math.Exp(-tb / 0.12) * Math.Sin(2 * Math.PI * (50 + 60 * Math.Exp(-tb / 0.03)) * tb);
            double bajo = 0.25 * Math.Sin(2 * Math.PI * 55 * t);
            double w = r.NextDouble() * 2 - 1; hp = 0.5 * (hp + w - ant); ant = w;
            double hat = 0.2 * Math.Exp(-(t % 0.25) / 0.03) * hp;
            double acorde = 0.05 * (Math.Sin(2 * Math.PI * 220 * t) + Math.Sin(2 * Math.PI * 277 * t) + Math.Sin(2 * Math.PI * 330 * t));
            double mel = 0.12 * Math.Sin(2 * Math.PI * 659 * t);
            x[2 * i] = (float)(kick + bajo + hat + acorde + mel); x[2 * i + 1] = (float)(kick + hat * 0.8 + acorde + mel * 0.7);
        }
        return x;
    }

    // Cuenta, en un objeto, los valores de punto flotante que quedaron entre cero y el tope (o en desnormal).
    static int Restos(object o, string nombre, List<string> donde) { return Restos(o, nombre, donde, null); }
    static int Restos(object o, string nombre, List<string> donde, string[] solo)
    {
        if (o == null) return 0;
        int n = 0;
        for (Type t = o.GetType(); t != null && t != typeof(object); t = t.BaseType)
            foreach (FieldInfo x in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (solo != null && Array.IndexOf(solo, x.Name) < 0) continue;
                object v = x.GetValue(o); int k = 0;
                if (v is float) k = Resto((float)v) ? 1 : 0;
                else if (v is double) k = Resto((double)v) ? 1 : 0;
                else if (v is float[]) foreach (float e in (float[])v) { if (Resto(e)) k++; }
                else if (v is double[]) foreach (double e in (double[])v) { if (Resto(e)) k++; }
                else if (v is double[][]) foreach (double[] a in (double[][])v) foreach (double e in a) { if (Resto(e)) k++; }
                if (k > 0) donde.Add(nombre + "." + x.Name + " (" + k + ")");
                n += k;
            }
        return n;
    }
    static bool Resto(float v) { return v != 0f && v < 1e-15f && v > -1e-15f; }
    static bool Resto(double v) { return v != 0.0 && v < 1e-30 && v > -1e-30; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        if (asm.GetType("Rocola.Desnormales", false) == null) Console.WriteLine("  (esta rocola no tiene Desnormales.cs)");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_desnormales_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            const int fs = 44100;
            f = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
            string wav = Path.Combine(dir, "cola_muda.wav");
            float[] x = Cancion(fs, 3.0, 6.0);
            using (WaveFileWriter w = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
            object[] a = { wav, null };
            Check((bool)Call(f, "MideIntegrada", a), "la cancion de prueba se mide (para que el nivelado trabaje)");
            IDictionary tab = (IDictionary)Get(f, "integCache");
            if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); Set(f, "integCache", tab); }
            tab[wav] = a[1];
            Call(f, "ActualizaFuenteMedida", wav, Call(null, "TomaFuenteMedida"));

            // Lo de fabrica, aprendiendo sonoridad y huella, sin saltar silencios; el modo noche (volvio el 7-11-2026), de dia.
            SetSiHay(f, "nocheArrancada", true); SetSiHay(f, "nocheFactor", 1f);
            Set(f, "normalizeOn", true); Set(f, "normTarget", 0.26f); Set(f, "normStrength", 1f); Set(f, "masterDb", 0f);
            Set(f, "excAmount", 0.35f); Set(f, "bassAuto", false); Set(f, "bassAmount", 0.3f); Set(f, "bassSongFactor", 1f);
            Set(f, "sbrAllowed", false); Set(f, "sbrOn", false); Set(f, "declipOn", true);
            Set(f, "crossfadeSec", 0.5f); Set(f, "saltaSilencios", false); Set(f, "eqAuto", true);
            SetSiHay(f, "auroraAmount", 1f);   // la aurora pide su espectro (solo con tarjeta grafica)

            Type tt = TF.GetNestedType("Track", T); object track = Activator.CreateInstance(tt, true); Set(track, "Path", wav);
            Type tp = TF.GetField("nowPlaying", T).FieldType; object item = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp); Set(item, "Song", track);
            object lector = Call(f, "OpenAudio", wav);
            TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
            object cad = Call(f, "ArmaCadena", track, lector, false, 0.0);
            Set(f, "curTap", Get(cad, "Tap")); Set(f, "curBass", Get(cad, "Bajo")); Set(f, "curSbr", Get(cad, "Sbr"));
            Set(f, "nowPlaying", item); Set(f, "cadenaPreparada", null);
            ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
            Check(Get(cad, "Mono") != null && Get(cad, "Bajo") != null && Get(cad, "Tap") != null, "la cadena lleva grave en mono, bajo y nivelado");
            Check(Get(f, "curMeasurePath") != null || Get(cad, "MedidaRuta") != null, "la cadena aprende la sonoridad del original (K-weighting)");

            // Leer como la bomba: bloques de 50 ms, cronometrando cada uno.
            int bloque = 2205 * 2; float[] b = new float[bloque];
            List<float> y = new List<float>(); List<double> msBloque = new List<double>(); List<int> inicio = new List<int>();
            while (true)
            {
                long t0 = Stopwatch.GetTimestamp();
                int n = sal.Read(b, 0, bloque);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if (n <= 0) break;
                inicio.Add(y.Count); msBloque.Add(ms);
                for (int i = 0; i < n; i++) y.Add(b[i]);
            }
            Check(y.Count >= 2 * fs * 8, "sale la cancion entera (" + (y.Count / 2 / (double)fs).ToString("0.0") + " s)");
            bool sano = true; foreach (float v in y) if (float.IsNaN(v) || float.IsInfinity(v)) sano = false;
            Check(sano, "nada roto en la salida");

            // 1) La salida, cero exacto tras dos segundos de silencio.
            int desde = 2 * fs * 5, distintas = 0; double mayor = 0;
            for (int i = desde; i < y.Count; i++) if (y[i] != 0f) { distintas++; mayor = Math.Max(mayor, Math.Abs(y[i])); }
            Check(distintas == 0, "tras 2 s de silencio la salida es cero exacto (" + distintas + " muestras con restos, el mayor " + mayor.ToString("0.0e0") + ")");

            // 2) Ningun estado en desnormal ni en un resto por debajo del tope.
            List<string> donde = new List<string>();
            object tap = Get(cad, "Tap");
            int restos = Restos(Get(cad, "Mono"), "GraveMono", donde) + Restos(Get(cad, "Bajo"), "Bajo", donde)
                       + Restos(tap, "LevelTap", donde) + Restos(Get(tap, "realce"), "Realce", donde)
                       + Restos(Get(tap, "currentEq"), "EQ", donde) + Restos(f, "Form1", donde, new string[] { "fLp1", "fLp2", "specLp", "kwSt", "eqMeasSt" });
            Check(restos == 0, "ningun estado se queda en un resto diminuto o desnormal (" + restos + (donde.Count > 0 ? ": " + string.Join(", ", donde.ToArray()) : "") + ")");

            // 3) El silencio no cuesta mas que la musica. Musica: de 1 a 3 s; silencio: de 5 a 9 s.
            double msMus = 0, msSil = 0; int nMus = 0, nSil = 0;
            for (int k = 0; k < msBloque.Count; k++)
            {
                double s = inicio[k] / 2.0 / fs;
                if (s >= 1.0 && s < 2.9) { msMus += msBloque[k]; nMus++; }
                else if (s >= 5.0 && s < 8.5) { msSil += msBloque[k]; nSil++; }
            }
            double porMus = msMus / Math.Max(1, nMus), porSil = msSil / Math.Max(1, nSil);
            Console.WriteLine("        medida: " + (porMus * 20).ToString("0.0") + " ms por segundo con musica, " + (porSil * 20).ToString("0.0") + " en silencio");
            Check(nMus > 10 && nSil > 10 && porSil <= 2.5 * porMus, "el silencio no cuesta mas que la musica (con margen de 2,5 veces; antes eran unas siete)");
            ((IDisposable)lector).Dispose();

            // 4) EL REPASO A FONDO DE LA BIBLIOTECA (MeasureFile entero): la huella del EQ y la sonoridad que mide tambien se
            //    apagaban en restos diminutos. Una cancion de 3 s de musica y 20 de silencio frente a otra de 23 s de musica.
            string mus = Path.Combine(dir, "musica23.wav"), sil = Path.Combine(dir, "musica3_silencio20.wav");
            float[] xm = Cancion(fs, 23.0, 0.0), xs = Cancion(fs, 3.0, 20.0);
            using (WaveFileWriter w = new WaveFileWriter(mus, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(xm, 0, xm.Length);
            using (WaveFileWriter w = new WaveFileWriter(sil, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(xs, 0, xs.Length);
            SetSiHay(f, "iaHarvestOn", false);
            MethodInfo mf = null; foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "MeasureFile" && m.GetParameters().Length == 5) mf = m;
            double msMusica = double.MaxValue, msSilencio = double.MaxValue;
            for (int vuelta = 0; vuelta < 3; vuelta++)
                foreach (string ruta in new string[] { mus, sil })
                {
                    long t0 = Stopwatch.GetTimestamp();
                    bool medido = (bool)mf.Invoke(f, new object[] { ruta, true, 0f, 0f, null });
                    double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                    if (vuelta == 0) Check(medido, "el repaso a fondo mide " + Path.GetFileName(ruta));
                    if (ruta == mus) msMusica = Math.Min(msMusica, ms); else msSilencio = Math.Min(msSilencio, ms);
                    if (ruta == sil && vuelta == 2)
                    {
                        object trabajo = TF.GetField("trabajoMedidaDelHilo", T).GetValue(null);
                        List<string> d2 = new List<string>();
                        int r2 = Restos(trabajo, "TrabajoMedida", d2, new string[] { "Estado", "EstadoBandas" });
                        Check(trabajo != null && r2 == 0, "tras el repaso a fondo, ningun estado de la huella ni de la sonoridad en un resto diminuto (" + r2 + (d2.Count > 0 ? ": " + string.Join(", ", d2.ToArray()) : "") + ")");
                    }
                }
            Console.WriteLine("        repaso a fondo: " + msMusica.ToString("0") + " ms la de musica, " + msSilencio.ToString("0") + " ms la del silencio (mejor de tres)");
            Check(msSilencio <= 2.0 * msMusica, "en el repaso a fondo, 20 s de silencio no cuestan mas que 20 s de musica (con margen de 2 veces; antes eran unas ocho)");

            // 5) EL MODO BILLAR (28-10-2026, septima tanda): la cadena con todas sus etapas (local, retumbe, presencia, ataque, agudos
            //    asperos, eses, bombo, el oido del volumen parejo y el limitador que respira) y 30 s de silencio exacto. Cada segundo de
            //    silencio se mira cada etapa (recorriendo la cadena desde la salida): ninguna con restos diminutos o desnormales.
            string wb = Path.Combine(dir, "billar_cola_muda.wav");
            float[] xb = Cancion(fs, 3.0, 30.0);
            using (WaveFileWriter w = new WaveFileWriter(wb, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(xb, 0, xb.Length);
            object[] ab = { wb, null };
            Call(f, "MideIntegrada", ab); tab[wb] = ab[1];
            SetSiHay(null, "localAuto", true); SetSiHay(f, "localAuto", true);
            object trB = Activator.CreateInstance(tt, true); Set(trB, "Path", wb);
            object lecB = Call(f, "OpenAudio", wb);
            TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lecB, 1.0f, null);
            object cadB = Call(f, "ArmaCadena", trB, lecB, false, 0.0);
            ISampleProvider salB = (ISampleProvider)Get(cadB, "Salida");
            List<object> etapas = new List<object>(); List<string> nombresE = new List<string>();
            object eo = salB; HashSet<object> vistos = new HashSet<object>();
            while (eo != null && vistos.Add(eo))
            {
                etapas.Add(eo); nombresE.Add(eo.GetType().Name);
                object sig = null;
                foreach (string nm in new string[] { "src", "source", "fuente", "entrada" }) { try { sig = Get(eo, nm); } catch { } if (sig is ISampleProvider) break; sig = null; }
                eo = sig;
            }
            bool hayEses = nombresE.Contains("SuavizaEses"), hayAta = nombresE.Contains("AtaqueClaro"), hayLim = nombresE.Contains("LimitadorPicoReal");
            Check(etapas.Count >= 10 && hayEses && hayAta && hayLim, "la cadena del billar, etapa por etapa (" + etapas.Count + ": " + string.Join(" <- ", nombresE.ToArray()) + ")");
            int restosB = 0; List<string> dondeB = new List<string>();
            double msMusB = 0, msSilB = 0; int nMusB = 0, nSilB = 0; long leidas = 0; int segVisto = -1;
            while (true)
            {
                long t0 = Stopwatch.GetTimestamp();
                int n = salB.Read(b, 0, bloque);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if (n <= 0) break;
                double s0 = leidas / 2.0 / fs; leidas += n;
                if (s0 >= 1.0 && s0 < 2.9) { msMusB += ms; nMusB++; } else if (s0 >= 5.0) { msSilB += ms; nSilB++; }
                int seg = (int)s0;
                if (s0 >= 6.0 && seg != segVisto)
                {
                    segVisto = seg; List<string> d2 = new List<string>(); int r2 = 0;
                    for (int q = 0; q < etapas.Count; q++) r2 += Restos(etapas[q], nombresE[q], d2);
                    // Fuera de cuenta: las casillas viejas de la cola de picos del limitador (peaks, fuera de su ventana activa): no se calcula con ellas.
                    for (int q = d2.Count - 1; q >= 0; q--) if (d2[q].StartsWith("LimitadorPicoReal.peaks ") || d2[q].StartsWith("LimitadorPicoReal.fpeaks ")) { r2 -= int.Parse(d2[q].Substring(d2[q].IndexOf('(') + 1).TrimEnd(')')); d2.RemoveAt(q); }
                    restosB += r2; foreach (string sd in d2) { string clave = sd.Substring(0, sd.IndexOf(" (")); if (!dondeB.Exists(z => z.StartsWith(clave))) dondeB.Add(sd + " a los " + seg + " s"); }
                }
            }
            Check(restosB == 0, "en el billar, tras 3 s de silencio (y hasta 30 s), ninguna etapa queda en un resto diminuto o desnormal (" + restosB + (dondeB.Count > 0 ? ": " + string.Join(", ", dondeB.ToArray()) : "") + ")");
            double porMusB = msMusB / Math.Max(1, nMusB), porSilB = msSilB / Math.Max(1, nSilB);
            Console.WriteLine("        billar: " + (porMusB * 20).ToString("0.0") + " ms por segundo con musica, " + (porSilB * 20).ToString("0.0") + " en silencio");
            Check(nMusB > 10 && nSilB > 100 && porSilB <= 2.5 * porMusB, "en el billar, el silencio tampoco cuesta mas que la musica");
            ((IDisposable)lecB).Dispose();
            SetSiHay(null, "localAuto", false); SetSiHay(f, "localAuto", false);
        }
        catch (Exception e) { Exception z = e is TargetInvocationException ? e.InnerException : e; Check(false, "excepcion: " + z.GetType().Name + ": " + z.Message + "\n" + z.StackTrace); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK desnormales: " + ok + " comprobaciones (senales sinteticas; el tiempo es de esta maquina, no del bar)"
                                   : "  desnormales: " + mal + " FALLOS de " + (ok + mal));
        return mal;
    }
}

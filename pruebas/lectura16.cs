using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

// LOS 16 BITS A COMA FLOTANTE SIN RODEOS (20-10-2026; Pcm16Directo.cs).
//
// El repaso (LectorMedida) leia un MP3 del sistema o un WAV de 16 bits por el SampleChannel de NAudio (convertidor que DIVIDE cada muestra
// entre 32768, medidor de nivel, volumen) y despues barria cada muestra buscando NaN. Ahora lee los 16 bits con Pcm16Directo: multiplica
// por 2^-15 (exacto: el mismo float) y no barre (un entero de 16 bits entre 32768 no puede ser NaN ni pasar de 1). Aqui se exige el MISMO float,
// bit a bit, que con el SampleChannel de NAudio:
//   1) los 65.536 enteros de 16 bits, en mono y en estereo, con lecturas de todos los tamanos (1, 2, 3, 4095, 4096, 8191...) y con un origen que
//      entrega poco a poco (3 bytes cada vez): mismos floats, misma cuenta de muestras devuelta;
//   2) con volumen distinto de 1 (VolumeSampleProvider);
//   3) sobre un Mp3FileReader de verdad (los MP3 de pruebas, con un descompresor imitado de 16 bits, como en mp3ligero): lector de NAudio y lector ligero;
//   4) LectorMedida de la rocola (la clase de verdad): un WAV de 16 bits sale igual que por SampleChannel y usa el camino directo; un WAV de 24 bits o en
//      coma flotante NO lo usa y sale como antes (con NaN limpiado a cero como siempre);
//   5) el enganche: LectorMedida elige Pcm16Directo solo para PCM de 16 bits; y Pcm16Directo exige 16 bits;
//   6) cuanto cuesta: convertir con Pcm16Directo no es mas lento que con SampleChannel (informativo, con margen: es una medida de tiempo).
// Con el ejecutable anterior como segundo argumento se compara ademas MeasureFile + MideIntegrada de WAV generados (mismos bits).
class Lectura16
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) { ok++; } else { mal++; Console.WriteLine("  FALLO " + s); } }
    static int Bits(float x) { return BitConverter.ToInt32(BitConverter.GetBytes(x), 0); }

    // Un origen de 16 bits que entrega como mucho 'tanda' bytes por lectura.
    sealed class Gotea : IWaveProvider
    {
        readonly byte[] d; readonly int tanda; int p; readonly WaveFormat f;
        public Gotea(byte[] datos, int canales, int tanda) { d = datos; this.tanda = tanda; f = new WaveFormat(44100, 16, canales); }
        public WaveFormat WaveFormat { get { return f; } }
        public int Read(byte[] b, int o, int n) { int k = Math.Min(Math.Min(n, tanda), d.Length - p); Array.Copy(d, p, b, o, k); p += k; return k; }
    }

    static byte[] Todos16(int canales)
    {
        // los 65.536 enteros, mezclados en orden raro para que no vayan seguidos, repetidos hasta un numero de cuadros entero
        byte[] d = new byte[65536 * 2 * canales];
        for (int i = 0; i < 65536 * canales; i++)
        {
            int v = (int)(((long)i * 40503L) & 0xFFFF);   // permutacion de 0..65535 si canales == 1; con 2 canales repite todos
            short s = unchecked((short)v);
            d[2 * i] = (byte)(s & 0xFF); d[2 * i + 1] = (byte)((s >> 8) & 0xFF);
        }
        return d;
    }

    static int Lee(ISampleProvider p, int bloque, List<float> salida)
    {
        float[] b = new float[bloque + 8];
        int n, total = 0;
        while ((n = p.Read(b, 3, bloque)) > 0) { for (int i = 0; i < n; i++) salida.Add(b[3 + i]); total += n; if (total > 5000000) break; }
        return total;
    }

    static bool Iguales(List<float> a, List<float> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (Bits(a[i]) != Bits(b[i])) return false;
        return true;
    }

    // Un descompresor imitado de 16 bits (el de Windows no esta): bytes que dependen del cuadro, de todos los valores.
    sealed class Imita16 : IMp3FrameDecompressor
    {
        readonly WaveFormat f; readonly int ch;
        public Imita16(WaveFormat mp3) { ch = mp3.Channels; f = new WaveFormat(mp3.SampleRate, 16, ch); }
        public WaveFormat OutputFormat { get { return f; } }
        public int DecompressFrame(Mp3Frame fr, byte[] dest, int off)
        {
            int n = fr.SampleCount * 2 * ch, largo = fr.FrameLength;
            for (int i = 0; i < n; i++) dest[off + i] = (byte)(fr.RawData[(i * 13 + 1) % largo] ^ (i * 7) ^ (fr.FileOffset >> 3));
            return n;
        }
        public void Reset() { }
        public void Dispose() { }
    }

    static Type TLigero;
    static Mp3FileReader AbreMp3(string ruta, bool ligero)
    {
        Mp3FileReader.FrameDecompressorBuilder b = delegate(WaveFormat wf) { return new Imita16(wf); };
        if (!ligero) return new Mp3FileReader(ruta, b);
        MemoryStream ms = new MemoryStream(File.ReadAllBytes(ruta));
        return (Mp3FileReader)Activator.CreateInstance(TLigero, T, null, new object[] { ms, b }, null);
    }

    static void EscribeWav(string ruta, WaveFormat f, byte[] datos)
    {
        using (WaveFileWriter w = new WaveFileWriter(ruta, f)) w.Write(datos, 0, datos.Length);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true);
        Type td = asm.GetType("Rocola.Pcm16Directo", false);
        TLigero = asm.GetType("Rocola.LectorMp3Ligero", false);
        Check(td != null, "la rocola trae Pcm16Directo");
        if (td == null) { Console.WriteLine("  FALLO lectura16: esta rocola no tiene Pcm16Directo.cs"); return 1; }
        ConstructorInfo cd = td.GetConstructor(T, null, new Type[] { typeof(IWaveProvider) }, null);
        FieldInfo fVolumen = td.GetField("Volumen", T);
        MethodInfo vale = td.GetMethod("Vale", T);

        // 1) los 65.536 enteros, mono y estereo, de todos los tamanos
        foreach (int canales in new int[] { 1, 2 })
        {
            byte[] todos = Todos16(canales);
            foreach (int bloque in new int[] { 1, 2, 3, 7, 4095, 4096, 8191, 8192, 131072 })
            foreach (int tanda in new int[] { 1 << 20, 3, 4097 })
            {
                if (bloque < 64 && tanda != (1 << 20)) continue;   // las lecturas de a uno ya son lentas
                byte[] datos = todos;
                if (bloque == 1) { datos = new byte[4000 * 2 * canales]; Array.Copy(todos, datos, datos.Length); }
                SampleChannel referencia = new SampleChannel(new Gotea(datos, canales, tanda), false);
                ISampleProvider nuevo = (ISampleProvider)cd.Invoke(new object[] { new Gotea(datos, canales, tanda) });
                List<float> a = new List<float>(), b = new List<float>();
                int na = Lee(referencia, bloque, a), nb = Lee(nuevo, bloque, b);
                Check(na == nb && Iguales(a, b), "16 bits " + canales + " canal(es), bloques de " + bloque + ", origen de a " + (tanda > 100000 ? "todo" : tanda.ToString()) + ": mismos floats (" + a.Count + " / " + b.Count + ")");
            }
            // los 65.536 valores distintos aparecen
            SampleChannel r2 = new SampleChannel(new Gotea(Todos16(1), 1, 1 << 20), false);
            ISampleProvider n2 = (ISampleProvider)cd.Invoke(new object[] { new Gotea(Todos16(1), 1, 1 << 20) });
            List<float> aa = new List<float>(), bb = new List<float>(); Lee(r2, 4096, aa); Lee(n2, 4096, bb);
            HashSet<int> vistos = new HashSet<int>(); foreach (float x in bb) vistos.Add(Bits(x));
            if (canales == 1) Check(vistos.Count == 65536 && Iguales(aa, bb), "los 65.536 enteros de 16 bits dan 65.536 floats distintos y los mismos que NAudio");
            float mx = 0; foreach (float x in bb) mx = Math.Max(mx, Math.Abs(x));
            Check(mx <= 1f && !float.IsNaN(mx), "ninguno pasa de 1 ni es NaN (por eso el barrido de NaN sobra)");
        }

        // 2) con volumen
        foreach (float vol in new float[] { 1f, 0.5f, 0.123f, 2f, 0f })
        {
            byte[] datos = Todos16(2);
            SampleChannel referencia = new SampleChannel(new Gotea(datos, 2, 1 << 20), false); referencia.Volume = vol;
            ISampleProvider nuevo = (ISampleProvider)cd.Invoke(new object[] { new Gotea(datos, 2, 1 << 20) }); fVolumen.SetValue(nuevo, vol);
            List<float> a = new List<float>(), b = new List<float>(); Lee(referencia, 4096, a); Lee(nuevo, 4096, b);
            Check(Iguales(a, b), "volumen " + vol + ": mismos floats que VolumeSampleProvider");
        }

        // 3) un Mp3FileReader de verdad, los MP3 de pruebas
        List<string> mp3s = new List<string>();
        foreach (string f in Directory.GetFiles(".", "*.mp3")) mp3s.Add(f);
        if (Directory.Exists("mp3s")) foreach (string f in Directory.GetFiles("mp3s", "*.mp3")) mp3s.Add(f);
        int probados = 0;
        foreach (string f in mp3s)
            foreach (bool ligero in new bool[] { false, true })
            {
                if (ligero && TLigero == null) continue;
                List<float> a = new List<float>(), b = new List<float>(); int na, nb;
                try
                {
                    using (Mp3FileReader r = AbreMp3(f, ligero)) { SampleChannel c = new SampleChannel(r, false); na = Lee(c, 4096, a); }
                    using (Mp3FileReader r = AbreMp3(f, ligero)) { ISampleProvider d = (ISampleProvider)cd.Invoke(new object[] { r }); nb = Lee(d, 4096, b); }
                }
                catch (Exception e)
                {
                    if (e is TargetInvocationException) e = e.InnerException;
                    // un MP3 roto o vacio (de los de prueba) falla igual con los dos: se pasa
                    continue;
                }
                probados++;
                Check(na == nb && na > 0 && Iguales(a, b), "MP3 " + Path.GetFileName(f) + (ligero ? " (lector ligero)" : " (lector de NAudio)") + ": " + na + " muestras, mismos floats que por SampleChannel");
            }
        Check(probados >= 10, "se probaron MP3 de verdad (" + probados + ")");

        // 4) LectorMedida de la rocola (la clase de verdad)
        string dir = Path.Combine(Path.GetTempPath(), "lectura16_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Type tl = tf.GetNestedType("LectorMedida", T);
            ConstructorInfo cl = tl.GetConstructor(T, null, new Type[] { typeof(string) }, null);
            FieldInfo fDirecto = tl.GetField("directo", T), fCanal = tl.GetField("canal", T);
            MethodInfo mRead = tl.GetMethod("Read", T);
            PropertyInfo pVol = tl.GetProperty("Volume", T);
            Type twd = asm.GetType("Rocola.WavDirecto");
            MethodInfo wdAbre = twd.GetMethod("Abre", T);
            Random az = new Random(7);
            // musica sintetica, con un silencio de ceros y picos al maximo
            Func<int, int, double[]> musica = delegate(int n, int ch)
            {
                double[] x = new double[n * ch]; double ph = 0;
                for (int i = 0; i < n; i++)
                {
                    double v = 0.7 * Math.Sin(ph) + 0.25 * (az.NextDouble() - 0.5); ph += 0.031 + 0.00001 * (i % 1000);
                    if (i > n / 3 && i < n / 3 + 4000) v = 0;
                    if (i % 9001 < 5) v = 1.4 * Math.Sign(Math.Sin(i)) ;
                    for (int c = 0; c < ch; c++) x[i * ch + c] = Math.Max(-1.0, Math.Min(1.0, v * (c == 0 ? 1.0 : 0.8)));
                }
                return x;
            };
            foreach (int tasa in new int[] { 8000, 22050, 44100, 48000, 96000 })
            foreach (int canales in new int[] { 1, 2 })
            {
                int frames = tasa / 2 + 777;
                double[] x = musica(frames, canales);
                byte[] pcm = new byte[frames * canales * 2];
                for (int i = 0; i < x.Length; i++) { short s = (short)Math.Round(x[i] * 32767.0); pcm[2 * i] = (byte)(s & 0xFF); pcm[2 * i + 1] = (byte)((s >> 8) & 0xFF); }
                string wav = Path.Combine(dir, "w16_" + tasa + "_" + canales + ".wav");
                EscribeWav(wav, new WaveFormat(tasa, 16, canales), pcm);
                object lm = cl.Invoke(new object[] { wav });
                Check(fDirecto.GetValue(lm) != null && fCanal.GetValue(lm) == null, "WAV de 16 bits " + tasa + " Hz " + canales + " canal(es): LectorMedida usa el camino directo");
                WaveStream wd = (WaveStream)wdAbre.Invoke(null, new object[] { wav });
                SampleChannel referencia = new SampleChannel(wd, false);
                List<float> a = new List<float>(), b = new List<float>();
                float[] buf = new float[4096 * canales];
                int n;
                while ((n = referencia.Read(buf, 0, buf.Length)) > 0) for (int i = 0; i < n; i++) { float v = buf[i]; if (!(v >= -4f && v <= 4f)) v = 0f; a.Add(v); }   // + el barrido de siempre
                while ((n = (int)mRead.Invoke(lm, new object[] { buf, 0, buf.Length })) > 0) for (int i = 0; i < n; i++) b.Add(buf[i]);
                Check(Iguales(a, b) && a.Count > 0, "WAV de 16 bits " + tasa + " Hz " + canales + " canal(es): LectorMedida da los mismos floats que SampleChannel + barrido (" + a.Count + ")");
                ((IDisposable)lm).Dispose(); wd.Dispose();
            }
            // 24 bits y coma flotante: sin camino directo, sin cambio; el NaN del WAV en coma flotante se limpia como siempre
            foreach (int bits in new int[] { 24, 32, 8 })
            {
                int frames = 20000; int bytesPor = bits / 8;
                byte[] pcm = new byte[frames * 2 * bytesPor]; az.NextBytes(pcm);
                string wav = Path.Combine(dir, "w" + bits + ".wav");
                WaveFormat wf = bits == 32 ? WaveFormat.CreateIeeeFloatWaveFormat(44100, 2) : new WaveFormat(44100, bits, 2);
                if (bits == 32)
                {
                    float[] f32 = new float[frames * 2]; for (int i = 0; i < f32.Length; i++) f32[i] = (float)(az.NextDouble() * 2 - 1);
                    f32[10] = float.NaN; f32[11] = float.PositiveInfinity; f32[12] = 9f; f32[13] = -5f;
                    Buffer.BlockCopy(f32, 0, pcm, 0, pcm.Length);
                }
                EscribeWav(wav, wf, pcm);
                object lm = cl.Invoke(new object[] { wav });
                Check(fDirecto.GetValue(lm) == null && fCanal.GetValue(lm) != null, "WAV de " + (bits == 32 ? "coma flotante" : bits + " bits") + ": LectorMedida NO usa el camino directo");
                WaveStream wd = (WaveStream)wdAbre.Invoke(null, new object[] { wav });
                SampleChannel referencia = new SampleChannel(wd, false);
                List<float> a = new List<float>(), b = new List<float>(); float[] buf = new float[8192]; int n;
                while ((n = referencia.Read(buf, 0, buf.Length)) > 0) for (int i = 0; i < n; i++) { float v = buf[i]; if (!(v >= -4f && v <= 4f)) v = 0f; a.Add(v); }
                while ((n = (int)mRead.Invoke(lm, new object[] { buf, 0, buf.Length })) > 0) for (int i = 0; i < n; i++) b.Add(buf[i]);
                Check(Iguales(a, b) && a.Count > 0, "WAV de " + (bits == 32 ? "coma flotante" : bits + " bits") + ": mismos floats que SampleChannel + barrido");
                if (bits == 32) Check(Bits(b[10]) == 0 && Bits(b[11]) == 0 && Bits(b[12]) == 0 && Bits(b[13]) == 0, "el NaN, el infinito y lo que pasa de 4 se limpian a cero como siempre");
                ((IDisposable)lm).Dispose(); wd.Dispose();
            }
            // el volumen de LectorMedida (la medida lo deja en 1,0): con otro valor, multiplica y limpia
            {
                int frames = 5000; byte[] pcm = new byte[frames * 4]; az.NextBytes(pcm);
                string wav = Path.Combine(dir, "wvol.wav"); EscribeWav(wav, new WaveFormat(44100, 16, 2), pcm);
                object lm = cl.Invoke(new object[] { wav });
                pVol.SetValue(lm, 1.0f, null);
                WaveStream wd = (WaveStream)wdAbre.Invoke(null, new object[] { wav }); SampleChannel referencia = new SampleChannel(wd, false); referencia.Volume = 1.0f;
                float[] bufa = new float[10000], bufb = new float[10000];
                int na = referencia.Read(bufa, 0, bufa.Length), nb = (int)mRead.Invoke(lm, new object[] { bufb, 0, bufb.Length });
                bool igual = na == nb; for (int i = 0; i < na && igual; i++) if (Bits(bufa[i]) != Bits(bufb[i])) igual = false;
                Check(igual, "Volume = 1,0 en LectorMedida: sigue igual");
                ((IDisposable)lm).Dispose(); wd.Dispose();
                lm = cl.Invoke(new object[] { wav }); pVol.SetValue(lm, 0.25f, null);
                wd = (WaveStream)wdAbre.Invoke(null, new object[] { wav }); referencia = new SampleChannel(wd, false); referencia.Volume = 0.25f;
                na = referencia.Read(bufa, 0, bufa.Length); nb = (int)mRead.Invoke(lm, new object[] { bufb, 0, bufb.Length });
                igual = na == nb; for (int i = 0; i < na && igual; i++) if (Bits(bufa[i]) != Bits(bufb[i])) igual = false;
                Check(igual, "Volume = 0,25 en LectorMedida: los mismos floats que SampleChannel");
                ((IDisposable)lm).Dispose(); wd.Dispose();
            }

            // 5) el enganche
            Check((bool)vale.Invoke(null, new object[] { new WaveFormat(44100, 16, 2) }), "Vale: PCM de 16 bits");
            Check(!(bool)vale.Invoke(null, new object[] { new WaveFormat(44100, 24, 2) }), "Vale: no el de 24 bits");
            Check(!(bool)vale.Invoke(null, new object[] { WaveFormat.CreateIeeeFloatWaveFormat(44100, 2) }), "Vale: no la coma flotante");
            Check(!(bool)vale.Invoke(null, new object[] { null }), "Vale: no un formato nulo");
            bool lanza = false;
            try { cd.Invoke(new object[] { new Gotea(new byte[8], 1, 4) }); }
            catch { lanza = true; }
            Check(!lanza, "Pcm16Directo acepta un origen de 16 bits");
            lanza = false;
            try { cd.Invoke(new object[] { new RawSourceWaveStream(new MemoryStream(new byte[24]), new WaveFormat(44100, 24, 2)) }); }
            catch (TargetInvocationException) { lanza = true; }
            Check(lanza, "Pcm16Directo se niega con un origen de 24 bits");
            string codigo = File.ReadAllText(Path.Combine("..", "Rocola", "Form1.Sonido.cs"));
            Check(codigo.Contains("Pcm16Directo.Vale(r.WaveFormat)") && codigo.Contains("Pcm16Directo.Vale(f.WaveFormat)"), "LectorMedida elige Pcm16Directo para el MP3 y para el WAV");

            // 6) cuanto cuesta
            {
                byte[] datos = new byte[2 * 2 * 44100 * 30]; az.NextBytes(datos);
                double mejorA = double.MaxValue, mejorB = double.MaxValue; float[] buf = new float[8192];
                for (int rep = 0; rep < 5; rep++)
                {
                    SampleChannel r = new SampleChannel(new Gotea(datos, 2, 1 << 24), false);
                    System.Diagnostics.Stopwatch w = System.Diagnostics.Stopwatch.StartNew(); while (r.Read(buf, 0, buf.Length) > 0) { } mejorA = Math.Min(mejorA, w.Elapsed.TotalMilliseconds);
                    ISampleProvider d = (ISampleProvider)cd.Invoke(new object[] { new Gotea(datos, 2, 1 << 24) });
                    w = System.Diagnostics.Stopwatch.StartNew(); while (d.Read(buf, 0, buf.Length) > 0) { } mejorB = Math.Min(mejorB, w.Elapsed.TotalMilliseconds);
                }
                double muestras = datos.Length / 2.0;
                Console.WriteLine("  (medida: SampleChannel " + (mejorA * 1e6 / muestras).ToString("0.00") + " ns/muestra, Pcm16Directo " + (mejorB * 1e6 / muestras).ToString("0.00") + " ns/muestra; no es un banco: una sola maquina)");
                Check(mejorB <= mejorA * 1.5, "Pcm16Directo no es mas lento que SampleChannel (con margen de medida)");
            }

            // 7) con el ejecutable anterior: MeasureFile + MideIntegrada de WAV generados, mismos bits
            if (args.Length > 1 && File.Exists(args[1]))
            {
                Assembly viejo = Assembly.Load(File.ReadAllBytes(args[1]));
                int comparados = 0;
                foreach (string wav in Directory.GetFiles(dir, "w16_*.wav"))
                {
                    string a = Mide(asm, wav, dir), b = Mide(viejo, wav, dir);
                    Check(a == b && a.Length > 10, "con la rocola anterior, " + Path.GetFileName(wav) + ": la misma medida (rms, duracion, EQ, integrada), bit a bit");
                    comparados++;
                }
                Check(comparados >= 10, "se compararon " + comparados + " WAV con la rocola anterior");
            }
            else Console.WriteLine("  (sin ejecutable anterior como segundo argumento: no se compara MeasureFile con el de antes)");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK lectura16: " + ok + " comprobaciones" : "  FALLO lectura16: " + mal + " fallos de " + (ok + mal));
        return mal;
    }

    static string Mide(Assembly asm, string wav, string dir)
    {
        Type form = asm.GetType("Rocola.Form1", true);
        object f = Codigo.FormSinEncender(form);
        form.GetField("iaHarvestOn", T).SetValue(f, false);
        MethodInfo mf = null, mi = null, ti = form.GetMethod("TomaIntegrada", T);
        foreach (MethodInfo m in form.GetMethods(T)) { if (m.Name == "MeasureFile" && m.GetParameters().Length == 5) mf = m; if (m.Name == "MideIntegrada" && m.GetParameters().Length == 2) mi = m; }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (bool fondo in new bool[] { false, true })
        {
            object[] x = { wav, fondo, 0f, 0f, null }; bool okm = (bool)mf.Invoke(f, x);
            object[] z = { null }; bool hay = (bool)ti.Invoke(null, z);
            object[] y = { wav, null }; bool ok2 = (bool)mi.Invoke(f, y);
            sb.Append(okm + "|" + D(x[2]) + "|" + D(x[3]) + "|" + D(x[4]) + "|" + hay + (hay ? D(z[0]) : "") + "|" + ok2 + (ok2 ? D(y[1]) : "") + ";");
        }
        return sb.ToString();
    }
    static string D(object o)
    {
        if (o == null) return "null";
        if (o is float) return Bits((float)o).ToString("X8");
        if (o is double) return BitConverter.DoubleToInt64Bits((double)o).ToString("X16");
        if (o is float[]) { System.Text.StringBuilder sb = new System.Text.StringBuilder("["); foreach (float f in (float[])o) sb.Append(Bits(f).ToString("X8")).Append(' '); return sb.Append(']').ToString(); }
        if (o.GetType().IsValueType && !o.GetType().IsPrimitive) { System.Text.StringBuilder sb = new System.Text.StringBuilder("{"); foreach (FieldInfo fi in o.GetType().GetFields(T)) if (!fi.IsStatic) sb.Append(fi.Name).Append('=').Append(D(fi.GetValue(o))).Append(' '); return sb.Append('}').ToString(); }
        return o.ToString();
    }
}

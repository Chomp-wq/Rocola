using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
// MP3 EN COMA FLOTANTE EN MODEST, CON NLAYER (FlotanteNLayer.cs).
//
// Aqui no hay descompresor de Windows (ACM): se imita con el propio NLayer recortado a enteros de 16 bits, que es lo
// que hace el de Windows con lo que pasa del maximo. NLayer si es el de verdad (packages/NLayer.1.12.0). Se comprueba:
//   1) que NLayer, cuadro a cuadro por el lector de NAudio, da lo mismo que NLayer leyendo el archivo entero;
//   2) que deja pasar lo que supera 1,0 (flotante_overs.mp3, un master recortado a proposito y pasado por LAME);
//   3) la comparacion con el de siempre y el freno del gasto;
//   4) los frenos de cuando se usa: sin ruta, sin saber que canciones lo necesitan, las que no llegan al maximo, sin
//      NLayer.dll; y que en esos casos el lector se arma EXACTAMENTE como antes (el mismo descompresor);
//   5) con el lector de NAudio de verdad: la misma duracion que con el de siempre y los picos por encima de 1,0;
//   6) que canciones lo necesitan (las que llegan al maximo) y que este conectado.
// NO es XP: lo que gasta NLayer en el equipo del bar y lo que decide alli lo dice la linea "MP3 en coma flotante" de
// rocola_error.txt.
class FlotanteXp
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TD, TN, TDF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }

    // El de siempre, imitado: NLayer recortado a 16 bits (entero / 32768, como NAudio).
    sealed class AcmImitado : IMp3FrameDecompressor
    {
        readonly IMp3FrameDecompressor nl; readonly WaveFormat f; byte[] tmp = new byte[1152 * 2 * 4 * 4];
        public int Desplaza;   // cuadros de retraso (uno "malo")
        readonly Queue<byte[]> cola = new Queue<byte[]>();
        public AcmImitado(WaveFormat mp3) { nl = (IMp3FrameDecompressor)Activator.CreateInstance(TN, T, null, new object[] { mp3 }, null); f = new WaveFormat(mp3.SampleRate, 16, mp3.Channels); }
        public WaveFormat OutputFormat { get { return f; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int off)
        {
            int b = nl.DecompressFrame(frame, tmp, 0);
            byte[] pcm = new byte[b / 2];
            for (int i = 0, j = 0; i < b; i += 4, j += 2)
            {
                float v = BitConverter.ToSingle(tmp, i) * 32768f;
                int q = (int)Math.Round(v); if (q > 32767) q = 32767; else if (q < -32768) q = -32768;
                pcm[j] = (byte)(q & 0xFF); pcm[j + 1] = (byte)((q >> 8) & 0xFF);
            }
            cola.Enqueue(pcm);
            if (cola.Count <= Desplaza) return 0;
            byte[] sale = cola.Dequeue();
            Buffer.BlockCopy(sale, 0, dest, off, sale.Length);
            return sale.Length;
        }
        public void Reset() { nl.Reset(); cola.Clear(); }
        public void Dispose() { }
    }
    static int desplazaAcm;
    static IMp3FrameDecompressor NuevoAcm(WaveFormat f) { AcmImitado a = new AcmImitado(f); a.Desplaza = desplazaAcm; return a; }

    static void Reinicia()
    {
        ((IDictionary)TD.GetField("veredictos", T).GetValue(null)).Clear();
        TD.GetField("ausente", T).SetValue(null, false);
        TD.GetField("fallosEnVivo", T).SetValue(null, 0);
        TD.GetField("Comprobaciones", T).SetValue(null, 0);
        TD.GetField("FabricaNLayer", T).SetValue(null, Delegate.CreateDelegate(typeof(Func<WaveFormat, IMp3FrameDecompressor>), typeof(FlotanteXp).GetMethod("NuevoNLayer", T)));
        desplazaAcm = 0;
    }
    static IMp3FrameDecompressor NuevoNLayer(WaveFormat f) { return (IMp3FrameDecompressor)Activator.CreateInstance(TN, T, null, new object[] { f }, null); }

    // Todos los cuadros de un MP3 por un descompresor (como el lector: sin la etiqueta ID3; el cuadro Info tambien pasa).
    static float[] Cuadros(string ruta, IMp3FrameDecompressor d, out WaveFormat mp3)
    {
        List<float> r = new List<float>(); mp3 = null;
        using (FileStream fs = File.OpenRead(ruta))
        {
            try { Id3v2Tag.ReadTag(fs); } catch { }
            byte[] b = new byte[1152 * 2 * 4 * 4];
            bool pcm = d.OutputFormat.Encoding == WaveFormatEncoding.Pcm;
            while (true)
            {
                Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs); } catch (EndOfStreamException) { f = null; }
                if (f == null) break;
                int n = d.DecompressFrame(f, b, 0);
                if (pcm) for (int i = 0; i < n; i += 2) r.Add((short)(b[i] | (b[i + 1] << 8)) / 32768f);
                else for (int i = 0; i < n; i += 4) r.Add(BitConverter.ToSingle(b, i));
            }
        }
        return r.ToArray();
    }

    static void ElDescompresor()
    {
        Console.WriteLine("\n  1) NLAYER CUADRO A CUADRO\n");
        // NLayer leyendo el archivo entero (su propio lector), por reflexion: la referencia.
        Assembly nlA = Assembly.LoadFrom("NLayer.dll");
        Type mf = nlA.GetType("NLayer.MpegFile");
        List<float> refe = new List<float>();
        object arch = Activator.CreateInstance(mf, new object[] { "relleno_p0.mp3" });
        MethodInfo lee = mf.GetMethod("ReadSamples", new Type[] { typeof(float[]), typeof(int), typeof(int) });
        float[] b = new float[4096]; int n;
        while ((n = (int)lee.Invoke(arch, new object[] { b, 0, b.Length })) > 0) for (int i = 0; i < n; i++) refe.Add(b[i]);
        ((IDisposable)arch).Dispose();
        WaveFormat fmt;
        float[] mio = Cuadros("relleno_p0.mp3", NuevoNLayer(new WaveFormat(44100, 16, 2)), out fmt);
        // El primer cuadro es el de la etiqueta (Info): el lector de NLayer se lo salta; aqui sale como silencio.
        int salto = mio.Length - refe.Count;
        bool igual = salto == 1152 * 2;
        for (int i = 0; igual && i < refe.Count; i++) if (mio[salto + i] != refe[i]) igual = false;
        Check(igual, "cuadro a cuadro por el lector de NAudio da lo mismo que NLayer con el archivo entero, bit a bit (" + refe.Count / 2 + " muestras)");
        float[] ov = Cuadros("flotante_overs.mp3", NuevoNLayer(new WaveFormat(44100, 16, 2)), out fmt);
        float mayor = 0; foreach (float v in ov) mayor = Math.Max(mayor, Math.Abs(v));
        float[] ac = Cuadros("flotante_overs.mp3", NuevoAcm(new WaveFormat(44100, 16, 2)), out fmt);
        float mayorA = 0; foreach (float v in ac) mayorA = Math.Max(mayorA, Math.Abs(v));
        Check(mayor > 1.2f && mayorA <= 1f, "deja pasar lo que supera el maximo: NLayer llega a " + F(20 * Math.Log10(mayor), "+0.0") + " dBFS; el de siempre (imitado) se queda en " + F(20 * Math.Log10(mayorA), "0.0"));
    }

    static int Compara(string ruta, out string detalle)
    {
        object[] a = { new WaveFormat(44100, 16, 2), File.OpenRead(ruta), (Func<WaveFormat, IMp3FrameDecompressor>)NuevoAcm, TD.GetField("FabricaNLayer", T).GetValue(null), null };
        try { return (int)TD.GetMethod("Compara", T).Invoke(null, a); }
        finally { ((Stream)a[1]).Dispose(); detalle = (string)a[4]; }
    }
    static void LaComparacion()
    {
        Console.WriteLine("\n  2) LA COMPARACION CON EL DE SIEMPRE, Y LO QUE GASTA\n");
        Reinicia();
        string d;
        int r = Compara("flotante_overs.mp3", out d);
        Check(r == 1 && d.Contains("en uso") && d.Contains("gasta el"), "NLayer da lo mismo que el de siempre salvo lo recortado: " + d);
        desplazaAcm = 1;
        r = Compara("flotante_overs.mp3", out d);
        Check(r == -1, "si el de siempre fuera un cuadro por detras (o NLayer por delante), NO se usa: " + d);
        desplazaAcm = 0;
        System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        while (reloj.ElapsedMilliseconds < 20) { }
        reloj.Stop();
        object[] g = { 1, "da lo mismo", reloj, 441L, 2, 44100, null };
        int rg = (int)TD.GetMethod("Gasto", T).Invoke(null, g);
        Check(rg == -1 && ((string)g[6]).Contains("tope 3 %"), "si gasta mas del 3 % de un nucleo (aqui: 20 ms para 5 ms de musica), NO se usa: " + g[6]);
        g = new object[] { 1, "da lo mismo", reloj, 441000L * 100, 2, 44100, null };
        Check((int)TD.GetMethod("Gasto", T).Invoke(null, g) == 1, "y si gasta poco, si: " + g[6]);
        g = new object[] { -1, "se aparta", reloj, 441000L * 100, 2, 44100, null };
        Check((int)TD.GetMethod("Gasto", T).Invoke(null, g) == -1, "lo que no vale no se arregla por gastar poco");
        Check((double)TD.GetField("GastoMaximo", T).GetValue(null) == 0.03 && (int)TD.GetField("CuadrosMedidos", T).GetValue(null) >= 100,
              "el tope es el 3 % de un nucleo, medido en al menos 100 cuadros (~2,6 s de musica)");
    }

    static IMp3FrameDecompressor Envuelve(WaveFormat f, IMp3FrameDecompressor acm, string ruta)
    {
        return (IMp3FrameDecompressor)TD.GetMethod("Envuelve", T).Invoke(null, new object[] { f, acm, ruta, (Func<WaveFormat, IMp3FrameDecompressor>)NuevoAcm });
    }
    static void LosFrenos()
    {
        Console.WriteLine("\n  3) CUANDO SE USA\n");
        List<string> apuntes = new List<string>();
        TD.GetField("Apunte", T).SetValue(null, (Action<string>)delegate(string s) { apuntes.Add(s); });
        FieldInfo necesita = TD.GetField("LaNecesita", T);
        try
        {
            Reinicia();
            WaveFormat f = new WaveFormat(44100, 16, 2);
            string ov = Path.GetFullPath("flotante_overs.mp3");
            IMp3FrameDecompressor acm = NuevoAcm(f);
            necesita.SetValue(null, null);
            Check(ReferenceEquals(Envuelve(f, acm, ov), acm), "sin saber que canciones lo necesitan: el de siempre, el mismo objeto (el lector queda como antes)");
            necesita.SetValue(null, (Func<string, bool>)delegate(string r) { return false; });
            Check(ReferenceEquals(Envuelve(f, acm, ov), acm), "una cancion que no llega al maximo: el de siempre, el mismo objeto");
            necesita.SetValue(null, (Func<string, bool>)delegate(string r) { return true; });
            Check(ReferenceEquals(Envuelve(f, acm, null), acm), "sin ruta (MP3 reconstruido en memoria, el clip de medir el relleno): el de siempre");
            IMp3FrameDecompressor e = Envuelve(f, acm, ov);
            Check(e != null && e.GetType().Name == "DescompresorFlotante" && e.OutputFormat.Encoding == WaveFormatEncoding.IeeeFloat
                  && (int)TD.GetField("Comprobaciones", T).GetValue(null) == 1 && apuntes.Count == 1 && apuntes[0].Contains("en uso"),
                  "una que llega al maximo: se compara una vez, se apunta (\"" + (apuntes.Count > 0 ? apuntes[0] : "") + "\") y descomprime en coma flotante");
            IMp3FrameDecompressor e2 = Envuelve(f, NuevoAcm(f), ov);
            Check(e2.GetType().Name == "DescompresorFlotante" && (int)TD.GetField("Comprobaciones", T).GetValue(null) == 1, "la siguiente ya no se compara: lo decidido vale toda la sesion");
            // Sin NLayer.dll: la fabrica revienta como revienta la carga de un ensamblado que falta.
            Reinicia(); apuntes.Clear();
            TD.GetField("FabricaNLayer", T).SetValue(null, (Func<WaveFormat, IMp3FrameDecompressor>)delegate(WaveFormat x) { throw new FileNotFoundException("Could not load file or assembly 'NLayer'"); });
            IMp3FrameDecompressor sin = Envuelve(f, acm, ov);
            Check(ReferenceEquals(sin, acm) && apuntes.Count == 1 && apuntes[0].Contains("no se pudo preparar") && apuntes[0].Contains("sigue con el de siempre"), "sin NLayer.dll: el de siempre y lo apunta (\"" + (apuntes.Count > 0 ? apuntes[0] : "") + "\")");
            Check(ReferenceEquals(Envuelve(f, acm, ov), acm) && ReferenceEquals(Envuelve(new WaveFormat(48000, 16, 2), acm, ov), acm) && apuntes.Count == 1,
                  "y no se vuelve a intentar en la sesion, ni con otro formato");
        }
        finally { necesita.SetValue(null, null); TD.GetField("Apunte", T).SetValue(null, null); Reinicia(); }
    }

    static void ConElLector()
    {
        Console.WriteLine("\n  4) CON EL LECTOR DE NAUDIO DE VERDAD\n");
        Reinicia();
        Mp3FileReader.FrameDecompressorBuilder deSiempre = delegate(WaveFormat wf) { return NuevoAcm(wf); };
        Mp3FileReader.FrameDecompressorBuilder flotante = delegate(WaveFormat wf)
        { return (IMp3FrameDecompressor)Activator.CreateInstance(TDF, T, null, new object[] { wf, NuevoAcm(wf), TD.GetField("FabricaNLayer", T).GetValue(null) }, null); };
        float mA, mF; long nA, nF;
        Lee("flotante_overs.mp3", deSiempre, out mA, out nA);
        Lee("flotante_overs.mp3", flotante, out mF, out nF);
        Check(nA == nF && nA > 0, "la misma duracion con los dos (" + nA / 2 + " muestras)");
        Check(mF > 1.2f && mA <= 1f, "y los picos por encima de 1,0 llegan a la cadena (" + F(20 * Math.Log10(mF), "+0.0") + " dBFS)");
        // Un salto a media cancion: sigue saliendo lo mismo que con el de siempre (salvo lo recortado).
        using (Mp3FileReader r = new Mp3FileReader("flotante_overs.mp3", flotante))
        {
            r.CurrentTime = TimeSpan.FromSeconds(3);
            SampleChannel c = new SampleChannel(r, false); float[] b = new float[8820];
            int n = c.Read(b, 0, b.Length);
            using (Mp3FileReader r2 = new Mp3FileReader("flotante_overs.mp3", deSiempre))
            {
                r2.CurrentTime = TimeSpan.FromSeconds(3);
                SampleChannel c2 = new SampleChannel(r2, false); float[] b2 = new float[8820];
                int n2 = c2.Read(b2, 0, b2.Length);
                double peor = 0; for (int i = 0; i < Math.Min(n, n2); i++) if (Math.Abs(b2[i]) < 0.999f) peor = Math.Max(peor, Math.Abs(b[i] - b2[i]));
                Check(n == n2 && peor < 0.001, "tras un salto a los 3 s sale lo mismo que con el de siempre (se aparta " + F(peor, "0.00000") + ")");
            }
        }
    }
    static void Lee(string ruta, Mp3FileReader.FrameDecompressorBuilder b, out float mayor, out long n)
    {
        mayor = 0; n = 0;
        using (Mp3FileReader r = new Mp3FileReader(ruta, b))
        {
            SampleChannel c = new SampleChannel(r, false); float[] x = new float[4096]; int k;
            while ((k = c.Read(x, 0, x.Length)) > 0) { n += k; for (int i = 0; i < k; i++) mayor = Math.Max(mayor, Math.Abs(x[i])); }
        }
    }

    static void Conectado()
    {
        Console.WriteLine("\n  5) QUE CANCIONES, Y CONECTADO\n");
        object f = Codigo.FormSinEncender(TF);
        Type ts = TF.Assembly.GetType("Rocola.SonoridadIntegrada");
        IDictionary tab = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), ts), StringComparer.OrdinalIgnoreCase);
        tab["cero.mp3"] = Activator.CreateInstance(ts, T, null, new object[] { 2, -9f, 2f, 100, 0f, -3f }, null);
        tab["casi.mp3"] = Activator.CreateInstance(ts, T, null, new object[] { 2, -9f, 2f, 100, -0.04f, -3f }, null);
        tab["baja.mp3"] = Activator.CreateInstance(ts, T, null, new object[] { 2, -9f, 2f, 100, -0.3f, -3f }, null);
        tab["vieja.mp3"] = Activator.CreateInstance(ts, T, null, new object[] { 1, -9f, 2f, 100 }, null);
        TF.GetField("integCache", T).SetValue(f, tab);
        MethodInfo llega = TF.GetMethod("LlegaAlMaximo", T);
        Check((bool)llega.Invoke(f, new object[] { "cero.mp3" }) && (bool)llega.Invoke(f, new object[] { "casi.mp3" }) && !(bool)llega.Invoke(f, new object[] { "baja.mp3" })
              && !(bool)llega.Invoke(f, new object[] { "vieja.mp3" }) && !(bool)llega.Invoke(f, new object[] { "nunca.mp3" }),
              "lo necesitan las que llegan al maximo (pico de -0,05 dBFS o mas); las de -0,3, las medidas sin picos y las sin medir, no");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "AbreMp3"), "Envuelve"), "AbreMp3 (al armar el descompresor de cada cancion) pregunta a Envuelve");
        int lee = 0, escribe = 0;
        foreach (ConstructorInfo c in TF.GetConstructors(T)) { int l, e; Codigo.UsosDeCampo(c, "LaNecesita", out l, out e); escribe += e; lee += l; }
        Check(escribe > 0, "la rocola le dice al arrancar que canciones lo necesitan");
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TD = a.GetType("Rocola.DecodificacionFlotante", false); TN = a.GetType("Rocola.MotorNLayer", false); TDF = a.GetType("Rocola.DescompresorFlotante", false);
        if (TD == null || TN == null || TDF == null) { Check(false, "no esta FlotanteNLayer (DecodificacionFlotante, MotorNLayer, DescompresorFlotante)"); return 1; }
        try { ElDescompresor(); } catch (Exception e) { Check(false, "el descompresor: " + e); }
        try { LaComparacion(); } catch (Exception e) { Check(false, "la comparacion: " + e); }
        try { LosFrenos(); } catch (Exception e) { Check(false, "los frenos: " + e); }
        try { ConElLector(); } catch (Exception e) { Check(false, "con el lector: " + e); }
        try { Conectado(); } catch (Exception e) { Check(false, "conectado: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

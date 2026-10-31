using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL PRE-ECO DE LOS MP3 (R2, Rocola/PreEco.cs, 31-10-2026). Con dos MP3 de verdad de los mismos golpes secos (3,2 s, preeco_lame.mp3 con
// LAME a 128 kbps y preeco_shine.mp3 con shine a 128 kbps, un codificador sin bloques cortos), descomprimidos con NLayer:
//  1) La medida del repaso: la de shine trae pre-eco (los golpes tardan en subir) y la de LAME no; sin golpes no se opina.
//  2) La tabla (algoritmo 7): guardar y leer el pre-eco; las medidas de antes (6) se siguen leyendo.
//  3) La etapa: en la de shine los golpes vuelven a subir de golpe y el golpe mismo casi no cambia; con Pasa (o sin golpes) sale
//     la entrada tal cual, bit a bit, retrasada; cualquier tamano de lectura da lo mismo.
//  4) La cadena, el interruptor (apagado de fabrica), «Escuchar sin mejoras» y la ventana.
// Cuentas con senales sinteticas en Linux/Mono; nada oido en el billar.
class FuentePE : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuentePE(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class PreEcoPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TMed, TQ, TSon;
    static string F(double v) { return v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
    static string Texto(string archivo) { return File.ReadAllText(Path.Combine(Path.Combine("..", "Rocola"), archivo)); }

    static float[] Mp3(string ruta, out int fs, out int ch)
    {
        Assembly nl = Assembly.LoadFrom(Path.GetFullPath("NLayer.dll"));
        Type tm = nl.GetType("NLayer.MpegFile", true);
        object mf = Activator.CreateInstance(tm, new object[] { ruta });
        fs = (int)tm.GetProperty("SampleRate").GetValue(mf, null); ch = (int)tm.GetProperty("Channels").GetValue(mf, null);
        MethodInfo lee = tm.GetMethod("ReadSamples", new Type[] { typeof(float[]), typeof(int), typeof(int) });
        List<float> r = new List<float>(); float[] b = new float[4608]; int n;
        while ((n = (int)lee.Invoke(mf, new object[] { b, 0, b.Length })) > 0) for (int i = 0; i < n; i++) r.Add(b[i]);
        ((IDisposable)mf).Dispose();
        return r.ToArray();
    }
    static bool Mide(float[] x, int fs, int ch, out float ms, out int golpes)
    {
        object m = Activator.CreateInstance(TMed, T, null, new object[] { fs, ch }, null);
        TMed.GetMethod("Procesa", T).Invoke(m, new object[] { x, 0, x.Length });
        object[] a = { 0f, 0 };
        bool ok = (bool)TMed.GetMethod("Resultado", T).Invoke(m, a);
        ms = (float)a[0]; golpes = (int)a[1];
        return ok;
    }
    static float[] Pasa(float[] x, int fs, int ch, bool pasa, Random r, out object etapa)
    {
        etapa = Activator.CreateInstance(TQ, T, null, new object[] { new FuentePE(x, fs, ch) }, null);
        TQ.GetField("Pasa", T).SetValue(etapa, pasa);
        ISampleProvider sp = (ISampleProvider)etapa;
        List<float> y = new List<float>(); float[] b = new float[8192]; int n;
        while ((n = sp.Read(b, 0, r == null ? 4096 : 1 + r.Next(5000))) > 0) for (int i = 0; i < n; i++) y.Add(b[i]);
        return y.ToArray();
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TMed = asm.GetType("Rocola.MedidorPreEco"); TQ = TF.GetNestedType("QuitaPreEco", T); TSon = asm.GetType("Rocola.SonoridadIntegrada");
        Check(TMed != null && TQ != null, "estan la medida del pre-eco y la etapa que lo quita");
        int fs, ch, fs2, ch2;
        float[] shine = Mp3("preeco_shine.mp3", out fs, out ch), lame = Mp3("preeco_lame.mp3", out fs2, out ch2);

        Console.WriteLine("\n  1) LA MEDIDA DEL REPASO\n");
        float msS, msL; int gS, gL;
        bool hayS = Mide(shine, fs, ch, out msS, out gS), hayL = Mide(lame, fs2, ch2, out msL, out gL);
        float minMs = (float)TMed.GetField("SubidaMinMs", T).GetValue(null);
        Check(hayS && msS >= minMs && gS >= 8, "shine (sin bloques cortos): los golpes tardan " + F(msS) + " ms en subir (" + gS + " golpes): trae pre-eco");
        Check(hayL && msL < minMs, "LAME a 128 kbps: suben en " + F(msL) + " ms (" + gL + " golpes): no trae");
        float[] tono = new float[44100 * 2 * 4]; for (int i = 0; i < tono.Length / 2; i++) tono[2 * i] = tono[2 * i + 1] = (float)(0.3 * Math.Sin(i * 0.05));
        float msT; int gT;
        Check(!Mide(tono, 44100, 2, out msT, out gT) && gT == 0, "sin golpes (un tono) no opina");
        // lecturas partidas: el mismo resultado
        object m2 = Activator.CreateInstance(TMed, T, null, new object[] { fs, ch }, null); Random rr = new Random(3);
        for (int i = 0; i < shine.Length; ) { int k = Math.Min(shine.Length - i, 1 + rr.Next(3000)); TMed.GetMethod("Procesa", T).Invoke(m2, new object[] { shine, i, k }); i += k; }
        object[] a2 = { 0f, 0 }; TMed.GetMethod("Resultado", T).Invoke(m2, a2);
        Check((float)a2[0] == msS && (int)a2[1] == gS, "leida en trozos de cualquier tamano (partiendo cuadros) da lo mismo");
        Check(Texto("Sonoridad.cs").Contains("PreEco.Procesa(datos, desde, cuantos);") && (int)asm.GetType("Rocola.MedidorBs1770").GetField("Algoritmo", T).GetValue(null) == 7,
              "se mide de paso con la sonoridad: algoritmo 7");

        Console.WriteLine("\n  2) LA TABLA\n");
        object mapa = asm.GetType("Rocola.MapaSilencios").GetMethod("Mudo", T).Invoke(null, new object[] { 30f });
        object s6 = Activator.CreateInstance(TSon, T, null, new object[] { 6, -11f, 2f, 30, -0.5f, -1f, mapa, 6.5f, -2f, 0.1f, 3.0f, 0, float.NaN }, null);
        object s7 = Activator.CreateInstance(TSon, T, null, new object[] { 7, -12.5f, 2f, 30, -0.5f, -1f, mapa, 6.5f, -2f, 0.1f, 3.0f, 0, float.NaN, 11f, 14 }, null);
        MethodInfo esc = TF.GetMethod("EscribeIntegRegistro", T), lee = TF.GetMethod("LeeIntegRegistros", T);
        StringWriter sw = new StringWriter(); sw.Write((string)TF.GetField("IntegCabecera", T).GetRawConstantValue() + "\r\n");
        esc.Invoke(null, new object[] { sw, "a.mp3", s7 });
        esc.Invoke(null, new object[] { sw, "b.mp3", s6 });
        sw.Write("8|-11|2|30|nueva|c.mp3\r\n");
        IDictionary tabla = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TSon), new object[] { StringComparer.OrdinalIgnoreCase });
        List<string> ajenas = new List<string>();
        bool leida = (bool)lee.Invoke(null, new object[] { new StringReader(sw.ToString()), tabla, ajenas });
        bool aOk = tabla.Contains("a.mp3"), bOk = tabla.Contains("b.mp3");
        float pa = aOk ? (float)TSon.GetField("PreEcoMs", T).GetValue(tabla["a.mp3"]) : 0f;
        bool conA = aOk && (bool)TSon.GetProperty("ConPreEco", T).GetValue(tabla["a.mp3"], null), conB = bOk && (bool)TSon.GetProperty("ConPreEco", T).GetValue(tabla["b.mp3"], null);
        Check(leida && aOk && pa == 11f && conA, "se guarda y se lee el pre-eco (" + sw.ToString().Split('\n')[1].Trim() + ")");
        Check(bOk && !conB && ajenas.Count == 1, "las medidas de antes (6) se leen sin pre-eco y las de un algoritmo futuro quedan intactas");

        Console.WriteLine("\n  3) LA ETAPA\n");
        object et;
        float[] ys = Pasa(shine, fs, ch, false, null, out et);
        int D = (int)TQ.GetProperty("DelayFrames", T).GetValue(et, null);
        long quitados = (long)TQ.GetField("Quitados", T).GetValue(et);
        float[] ysAl = new float[shine.Length]; Array.Copy(ys, D * ch, ysAl, 0, shine.Length);
        float msD; int gD; Mide(ysAl, fs, ch, out msD, out gD);
        Check(ys.Length == shine.Length + D * ch && D == 64 * 44, "retrasa 64 ms y no pierde ni una muestra (" + D + " cuadros)");
        Check(quitados >= 8 && msD <= msS * 0.6f, "en la de shine los golpes vuelven a subir de golpe: de " + F(msS) + " a " + F(msD) + " ms (" + quitados + " golpes)");
        // el golpe mismo: desde el bloque de 1 ms mas fuerte de cada golpe, 10 ms, casi no cambian (solo se toca lo de antes del arranque)
        double sumaA = 0, sumaD = 0; int L = 44;
        for (int g = (int)(0.15 * fs); g < shine.Length / ch - fs / 10; g += (int)(0.25 * fs))
        {
            int mejor = g; double mx = -1;
            for (int b0 = g - 1800; b0 < g + 1800; b0 += L)
            {
                double e = 0; for (int i = b0; i < b0 + L; i++) e += (double)shine[ch * i] * shine[ch * i];
                if (e > mx) { mx = e; mejor = b0; }
            }
            for (int i = mejor; i < mejor + 10 * L; i++) { sumaA += (double)shine[ch * i] * shine[ch * i]; sumaD += (double)ysAl[ch * i] * ysAl[ch * i]; }
        }
        double cambio = 10 * Math.Log10(sumaD / sumaA);
        Check(Math.Abs(cambio) < 0.5, "el golpe mismo casi no cambia: los 10 ms desde su milisegundo mas fuerte, " + cambio.ToString("+0.00;-0.00") + " dB");
        float[] yp = Pasa(shine, fs, ch, true, null, out et);
        bool igual = yp.Length == shine.Length + D * ch; for (int i = 0; i < shine.Length && igual; i++) igual = yp[D * ch + i] == shine[i];
        for (int i = 0; i < D * ch && igual; i++) igual = yp[i] == 0f;
        Check(igual, "con Pasa sale la entrada tal cual, bit a bit, retrasada (y antes, silencio)");
        float[] yt = Pasa(tono, 44100, 2, false, null, out et);
        bool igualT = true; for (int i = 0; i < tono.Length && igualT; i++) igualT = yt[44 * 64 * 2 + i] == tono[i];
        Check(igualT, "sin golpes no toca ni un bit");
        float[] yr = Pasa(shine, fs, ch, false, new Random(5), out et);
        bool igualR = yr.Length == ys.Length; for (int i = 0; i < ys.Length && igualR; i++) igualR = yr[i] == ys[i];
        Check(igualR, "leida en trozos de cualquier tamano (de 1 a 5000 muestras) da lo mismo");
        float[] mono = new float[shine.Length / 2]; for (int i = 0; i < mono.Length; i++) mono[i] = shine[2 * i];
        float[] ym = Pasa(mono, fs, 1, false, null, out et);
        Check(ym.Length == mono.Length + D && (long)TQ.GetField("Quitados", T).GetValue(et) >= 8, "en mono tambien");

        Console.WriteLine("\n  4) LA CADENA Y LA VENTANA\n");
        string son = Texto("Form1.Sonido.cs"), dat = Texto("Form1.Datos.cs"), aj = Texto("Form1.Ajustes.cs"), lq = Texto("LoQueSuena.cs");
        Check(son.Contains("if (quitaPreEco && PreEcoDe(t.Path))") && son.Contains("audioTailDelay += pe.DelayFrames; c.PreEco = pe;"),
              "en la cadena solo con el interruptor y en las canciones medidas con pre-eco (si no, ni se pone: bit a bit como antes)");
        Check(!(bool)TF.GetField("quitaPreEco", T).GetValue(null) && dat.Contains("quita_preeco=") && dat.Contains("Append(quitaPreEco ? \"1\" : \"0\")"), "el interruptor, apagado de fabrica y guardado en la configuracion");
        Check(lq.Contains("if (c.PreEco != null) c.PreEco.Pasa = sin || !quitaPreEco;") && Texto("GraveMono.cs").Contains("c.PreEco.Pasa = !quitaPreEco || sinMejoras;"),
              "«Escuchar sin mejoras» y apagarlo la dejan pasar al instante");
        Check(aj.Contains("\"SonidoPreEco\"") && aj.Contains("quitaPreEco = swPreEco.Checked;") && aj.Contains("quitaPreEco = bkPreEco;"), "el interruptor en Lo que suena ahora (y Cancelar lo deja como estaba)");

        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos (cuentas en Linux/Mono; nada oido en el billar)");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

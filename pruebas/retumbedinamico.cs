using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// RETUMBE DINAMICO (Rocola/RetumbeDinamico.cs, 27-10-2026): un ecualizador de 260 Hz que solo corta cuando el grave medio se pasa de su
// propia media. Con ruido y tonos sinteticos por la etapa de verdad:
//  1) Apagado (de fabrica), o encendido sin que nada se pase: sale bit a bit lo que entra, con uno, dos y seis canales.
//  2) Lo que hace: un salto de +8 dB en la banda de 260 Hz corta de 2 a 3 dB en ~100 ms; lo estacionario vuelve a ~0 en unos 6 s;
//     lo que esta fuera de la banda (1 kHz, 3 kHz y mas) queda a menos de 0,1 dB.
//  3) El presupuesto: con la claridad el tope es 3 dB; el EQ automatico y la claridad cortan juntos 250-500 Hz y el dinamico solo puede
//     llegar a lo que falte para 6 dB en total.
//  4) Sin chasquidos al encender y apagar sonando; al apagar vuelve a 0 en 40 ms y a bit a bit.
//  5) Disco continuo, lecturas de cualquier tamano, sin reservar memoria, cuanto cuesta.
//  6) La cadena, el modo automatico, la configuracion y «Escuchar sin mejoras».
// Todo sintetico en Linux/Mono. No es escucharlo en el bar: eso solo se sabe alli.
class FuenteRD : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteRD(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class RetumbeDinamicoPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TRD;
    const int FS = 44100;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static ISampleProvider Rd(ISampleProvider s, bool on, bool cla, double estatico) { return (ISampleProvider)Nuevo(TRD, s, on, cla, estatico); }
    static ISampleProvider Rd(ISampleProvider s, bool on) { return Rd(s, on, false, 0.0); }
    static void Ajusta(object p, bool on, bool cla, double estatico) { TRD.GetMethod("Ajusta", T).Invoke(p, new object[] { on, cla, estatico }); }
    static double Prop(object p, string n) { return (double)TRD.GetProperty(n, T).GetValue(p, null); }
    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    // Ruido blanco (uniforme) por los dos canales (el mismo en ambos) + un tono continuo de 1 kHz; en 'seg' segundos.
    static float[] Fondo(double seg, int ch, int semilla, double amp, double amp1k)
    {
        Random r = new Random(semilla); int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++)
        {
            double v = amp * (r.NextDouble() * 2 - 1) + amp1k * Math.Sin(2 * Math.PI * 1000 * i / FS);
            for (int c = 0; c < ch; c++) x[ch * i + c] = (float)v;
        }
        return x;
    }
    static void SumaTono(float[] x, int ch, double hz, double amp, double desde, double hasta)
    {
        for (int i = (int)(desde * FS); i < Math.Min(x.Length / ch, (int)(hasta * FS)); i++)
            for (int c = 0; c < ch; c++) x[ch * i + c] += (float)(amp * Math.Sin(2 * Math.PI * hz * i / FS));
    }
    // Amplitud del componente de 'hz' entre t0 y t1, canal c: 2/N |sum x e^-jwt|.
    static double Comp(float[] y, int ch, int c, double hz, double t0, double t1)
    {
        int a = (int)(t0 * FS), b = Math.Min(y.Length / ch, (int)(t1 * FS)); double sr = 0, si = 0;
        for (int i = a; i < b; i++)
        {
            double w = 2 * Math.PI * hz * i / FS, v = y[i * ch + c];
            sr += v * Math.Cos(w); si += v * Math.Sin(w);
        }
        return 2.0 * Math.Sqrt(sr * sr + si * si) / Math.Max(1, b - a);
    }
    // La energia (dB) de la banda de 260 Hz (Q 1, paso-banda de 0 dB) de la suma mono entre t0 y t1: lo mismo que mide el detector.
    static double BandaDb(float[] x, int ch, double t0, double t1)
    {
        double w = 2 * Math.PI * 260 / FS, al = Math.Sin(w) / 2.0, a0 = 1 + al, b0 = al / a0, a1 = -2 * Math.Cos(w) / a0, a2 = (1 - al) / a0;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0, e = 0; int n = 0;
        for (int i = 0; i < Math.Min(x.Length / ch, (int)(t1 * FS)); i++)
        {
            double m = 0; for (int c = 0; c < ch; c++) m += x[i * ch + c]; m /= ch;
            double y = b0 * (m - x2) - a1 * y1 - a2 * y2; x2 = x1; x1 = m; y2 = y1; y1 = y;
            if (i >= (int)(t0 * FS)) { e += y * y; n++; }
        }
        return 10 * Math.Log10(Math.Max(1e-20, e / Math.Max(1, n)));
    }
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-15)); }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(a[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0)) return false;
        return true;
    }
    static double SaltoMax(float[] y, int ch, int c)
    {
        double m = 0; for (int i = ch + c; i < y.Length; i += ch) m = Math.Max(m, Math.Abs(y[i] - y[i - ch])); return m;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TRD = asm.GetType("Rocola.RetumbeDinamico");
        Check(TRD != null, "esta la etapa del retumbe dinamico");
        if (TRD == null) { Console.WriteLine("  FALLO retumbedinamico: falta la etapa"); return 1; }

        Console.WriteLine("\n  1) APAGADO, O SIN NADA QUE CORTAR: NI TOCA LA MUESTRA\n");
        float[] rx = Fondo(2.0, 2, 3, 0.3, 0.1);
        Check(Igual(LeeTodo(Rd(new FuenteRD(rx, FS, 2), false), 1000), rx), "apagado, estereo: sale bit a bit lo que entra");
        float[] rm = new float[FS]; Array.Copy(rx, rm, rm.Length);
        Check(Igual(LeeTodo(Rd(new FuenteRD(rm, FS, 1), false), 777), rm), "apagado, un canal: igual");
        float[] r6 = new float[6 * 4410]; Array.Copy(rx, r6, r6.Length);
        Check(Igual(LeeTodo(Rd(new FuenteRD(r6, FS, 6), true), 960), r6), "seis canales: no se toca aunque este encendido (no sabe que bocina es cual)");
        float[] r8k = new float[8000]; Array.Copy(rx, r8k, r8k.Length);
        Check(Igual(LeeTodo(Rd(new FuenteRD(r8k, 4000, 2), true), 500), r8k), "a menos de 8 kHz tampoco se toca");
        Check(!(bool)TF.GetField("retumbeDinamico", T).GetValue(null), "de fabrica el interruptor esta apagado");
        {   // encendido con un fondo parejo y quieto: nada que cortar, sale bit a bit
            float[] par = Fondo(12.0, 2, 5, 0.2, 0.05);
            ISampleProvider s = Rd(new FuenteRD(par, FS, 2), true);
            float[] y = LeeTodo(s, 2048);
            double dif = 0; int cambiadas = 0; for (int i = 0; i < y.Length; i++) { double d = Math.Abs(y[i] - par[i]); if (d > 0) cambiadas++; dif = Math.Max(dif, d); }
            Console.WriteLine("        fondo parejo 12 s: muestras tocadas " + cambiadas + " de " + y.Length + ", diferencia maxima " + dif.ToString("0.000000"));
            Check(cambiadas == 0 || dif < 0.01, "encendido con un fondo parejo no corta nada audible (diferencia maxima " + dif.ToString("0.0000") + ")");
        }

        Console.WriteLine("\n  2) LO QUE HACE: CORTA EL RETUMBE Y SE VA SOLO\n");
        {
            // Fondo de ruido blanco a 0,12 + 1 kHz a 0,05. Se busca el tono de 250 Hz que sube 8 dB la banda de 260 Hz.
            double amp = 0.0, bajo = 0.0, alto = 0.5;
            float[] fondo = Fondo(10.0, 2, 7, 0.12, 0.05);
            double b0 = BandaDb(fondo, 2, 4.0, 9.0);
            for (int it = 0; it < 30; it++)
            {
                amp = 0.5 * (bajo + alto);
                float[] t = (float[])fondo.Clone(); SumaTono(t, 2, 250, amp, 4.0, 9.0);
                double subida = BandaDb(t, 2, 6.0, 9.0) - b0;
                if (subida > 8.0) alto = amp; else bajo = amp;
            }
            float[] x = Fondo(20.0, 2, 7, 0.12, 0.05);
            SumaTono(x, 2, 250, amp, 8.0, 20.0);
            Console.WriteLine("        tono de 250 Hz de " + F(amp) + " (sube la banda " + F(BandaDb(x, 2, 12.0, 14.0) - BandaDb(x, 2, 4.0, 6.0)) + " dB)");
            float[] y = LeeTodo(Rd(new FuenteRD(x, FS, 2), true), 4096);
            Func<double, double, double> corte = delegate (double t0, double t1) { return Db(Comp(x, 2, 0, 250, t0, t1)) - Db(Comp(y, 2, 0, 250, t0, t1)); };
            Console.WriteLine("        el corte del tono: antes del salto " + F(corte(6.0, 7.9)) + " dB; 50 ms despues " + F(corte(8.0, 8.05)) + "; 100-200 ms " + F(corte(8.1, 8.2)) + "; 0,5 s " + F(corte(8.5, 8.7))
                + "; 2 s " + F(corte(10.0, 10.2)) + "; 4 s " + F(corte(12.0, 12.2)) + "; 6 s " + F(corte(14.0, 14.2)) + "; 8 s " + F(corte(16.0, 16.2)) + "; 11 s " + F(corte(19.0, 19.5)));
            Check(Math.Abs(corte(6.0, 7.9)) < 0.05, "antes del salto no corta nada (" + F(corte(6.0, 7.9)) + " dB)");
            Check(corte(8.1, 8.2) > 1.5 && corte(8.1, 8.2) < 3.2, "a los 100-200 ms del salto de +8 dB corta de 2 a 3 dB (" + F(corte(8.1, 8.2)) + ")");
            double tope = 0; for (double t = 8.2; t < 14.0; t += 0.25) tope = Math.Max(tope, corte(t, t + 0.1));
            Check(tope > 2.0 && tope <= 3.2, "el corte no pasa de ~3 dB en todo el salto (maximo " + F(tope) + ")");
            Check(corte(8.5, 8.7) > corte(14.0, 14.2), "se va aflojando mientras el grave dura (la media lo alcanza)");
            Check(corte(15.0, 20.0) < 0.3, "un grave que se queda: pasados unos 6 s el corte se fue (" + F(corte(15.0, 20.0)) + " dB a los 7-12 s)");
            // fuera de la banda
            Func<double, double, double> dif = delegate (double hz, double t0) { return Db(Comp(y, 2, 0, hz, t0, t0 + 0.5)) - Db(Comp(x, 2, 0, hz, t0, t0 + 0.5)); };
            double d1k = dif(1000, 8.3), d1kb = dif(1000, 8.1);
            Console.WriteLine("        el 1 kHz queda a " + F(d1k) + " / " + F(d1kb) + " dB de su sitio en pleno corte");
            Check(Math.Abs(d1k) < 0.12 && Math.Abs(d1kb) < 0.12, "fuera de la banda, el 1 kHz queda a ~0,1 dB (" + F(d1k) + " y " + F(d1kb) + ")");
            float[] x3 = Fondo(20.0, 2, 7, 0.12, 0.05); SumaTono(x3, 2, 250, amp, 8.0, 20.0); SumaTono(x3, 2, 3000, 0.05, 0.0, 20.0); SumaTono(x3, 2, 80, 0.05, 0.0, 20.0);
            float[] y3 = LeeTodo(Rd(new FuenteRD(x3, FS, 2), true), 4096);
            double d3k = Db(Comp(y3, 2, 0, 3000, 8.3, 8.8)) - Db(Comp(x3, 2, 0, 3000, 8.3, 8.8)), d80 = Db(Comp(y3, 2, 0, 80, 8.3, 9.3)) - Db(Comp(x3, 2, 0, 80, 8.3, 9.3));
            Check(Math.Abs(d3k) < 0.1, "el 3 kHz tampoco se entera (" + F(d3k) + " dB)");
            Check(Math.Abs(d80) < 0.8, "ni el bajo de 80 Hz casi (" + F(d80) + " dB)");
            // los dos canales llevan el mismo corte (la imagen no se mueve)
            bool igualLR = true; for (int i = 0; i < y.Length; i += 2) if (y[i] != y[i + 1]) { igualLR = false; break; }
            Check(igualLR, "los dos canales llevan el mismo corte (un solo filtro para los dos)");
            // Con la claridad puesta, el tope baja a 3 dB
            float[] xf = Fondo(14.0, 2, 7, 0.12, 0.05); SumaTono(xf, 2, 250, 3 * amp, 8.0, 14.0);   // un salto mucho mayor: pide el tope
            Func<bool, double, double> topes = delegate (bool cla, double est)
            {
                float[] yf = LeeTodo(Rd(new FuenteRD(xf, FS, 2), true, cla, est), 4096);
                double mx = 0; for (double t = 8.1; t < 12.0; t += 0.1) mx = Math.Max(mx, Db(Comp(xf, 2, 0, 250, t, t + 0.1)) - Db(Comp(yf, 2, 0, 250, t, t + 0.1))); return mx;
            };
            double t0c = topes(false, 0.0), t1c = topes(true, 2.5), t2c = topes(true, 5.3), t3c = topes(false, 7.0);
            Console.WriteLine("        el tope: sin nada que cortar ya " + F(t0c) + " dB; con la claridad (2,5 ya cortados) " + F(t1c) + "; si ya se cortan 5,3 dB " + F(t2c) + "; si ya se cortan 7 dB " + F(t3c));
            Check(t0c > 3.0 && t0c <= 4.3, "con un salto grande llega al tope de 4 dB (" + F(t0c) + ")");
            Check(t1c <= 3.3 && t1c > 2.0, "con la claridad puesta el tope es 3 dB (" + F(t1c) + ")");
            Check(t2c <= 1.0, "si el EQ y la claridad ya cortan 5,3 dB, el dinamico queda en menos de 1 dB (" + F(t2c) + ")");
            Check(t3c < 0.05, "y si ya cortan 6 dB o mas, no actua (" + F(t3c) + ")");
        }
        {   // lo que cortan juntos el EQ automatico y la claridad, en 250-500 Hz
            MethodInfo est = TRD.GetMethod("CorteEstaticoDb", T);
            Func<float[], bool, double> st = delegate (float[] eq, bool cla) { return (double)est.Invoke(null, new object[] { eq, cla, FS }); };
            float[] eq0 = new float[8], eq400 = new float[8], eqSube = new float[8]; eq400[2] = -3.4f; eqSube[2] = 3f; eqSube[1] = 2f;
            double n0 = st(null, false), c0 = st(null, true), e0 = st(eq400, false), ec = st(eq400, true), s0 = st(eqSube, false), z = st(eq0, false);
            Console.WriteLine("        corte estatico en 250-500 Hz: nada " + F(n0) + "; claridad " + F(c0) + "; EQ de -3,4 dB en 400 Hz " + F(e0) + "; los dos " + F(ec) + "; un EQ que sube " + F(s0));
            Check(n0 == 0.0 && z == 0.0, "sin EQ ni claridad no hay corte estatico");
            Check(c0 > 2.3 && c0 < 2.6, "la claridad corta unos 2,5 dB en esa zona (" + F(c0) + ")");
            Check(e0 > 3.0 && e0 < 3.6, "el EQ de -3,4 dB en 400 Hz corta unos 3,4 (" + F(e0) + ")");
            Check(ec > 4.8 && ec < 5.8, "los dos juntos cortan unos 5 dB (" + F(ec) + ")");
            Check(s0 == 0.0, "un EQ que sube esa zona no cuenta como corte");
            Func<double, bool, double> cap = delegate (double e, bool c) { return (double)TRD.GetMethod("CapDinamicoDb", T).Invoke(null, new object[] { e, c }); };
            Check(cap(0, false) == 4.0 && cap(0, true) == 3.0 && Math.Abs(cap(5.3, true) - 0.7) < 1e-9 && cap(6.0, false) == 0.0 && cap(9.0, true) == 0.0 && Math.Abs(cap(4.0, false) - 2.0) < 1e-9, "el tope: 4 dB, 3 con claridad, y lo que falte para 6 dB en total");
        }

        Console.WriteLine("\n  3) SIN CHASQUIDOS AL ENCENDER Y APAGAR\n");
        {
            float[] x = Fondo(20.0, 2, 9, 0.12, 0.05); SumaTono(x, 2, 250, 0.35, 8.0, 20.0);
            ISampleProvider s = Rd(new FuenteRD(x, FS, 2), false);
            float[] b = new float[2048]; List<float> y = new List<float>(); int n, k = 0; int kOn = 5, kOff = (int)(14.0 * FS * 2 / 2048);
            while ((n = s.Read(b, 0, b.Length)) > 0)
            {
                for (int i = 0; i < n; i++) y.Add(b[i]); k++;
                if (k == kOn) Ajusta(s, true, false, 0.0);
                if (k == kOff) Ajusta(s, false, false, 0.0);
            }
            float[] ya = y.ToArray();
            double salto = Math.Max(SaltoMax(ya, 2, 0), SaltoMax(ya, 2, 1));
            double saltoEnt = Math.Max(SaltoMax(x, 2, 0), SaltoMax(x, 2, 1));
            Console.WriteLine("        salto maximo de la salida " + F(salto) + " frente al de la entrada " + F(saltoEnt));
            Check(salto < saltoEnt * 1.02 + 0.001, "encender y apagar sonando no da golpe (salto " + F(salto) + " frente a " + F(saltoEnt) + ")");
            float[] fin = new float[FS / 2 * 2]; Array.Copy(ya, ya.Length - fin.Length, fin, 0, fin.Length);
            float[] xfin = new float[fin.Length]; Array.Copy(x, x.Length - fin.Length, xfin, 0, fin.Length);
            Check(Igual(fin, xfin), "apagado otra vez, en unos 40 ms vuelve a salir bit a bit");
            Check(Prop(s, "CorteAhora") == 0.0, "y su corte es 0 exacto");
        }

        Console.WriteLine("\n  4) DISCO CONTINUO, LECTURAS Y COSTE\n");
        {
            float[] a = Fondo(9.0, 2, 11, 0.12, 0.05), b2 = Fondo(6.0, 2, 12, 0.12, 0.05); SumaTono(b2, 2, 250, 0.3, 0.5, 6.0);
            float[] ab = new float[a.Length + b2.Length];
            Array.Copy(a, ab, a.Length); Array.Copy(b2, 0, ab, a.Length, b2.Length);
            float[] junta = LeeTodo(Rd(new FuenteRD(ab, FS, 2), true), 4096);
            ISampleProvider sa = Rd(new FuenteRD(a, FS, 2), true), sb = Rd(new FuenteRD(b2, FS, 2), true);
            float[] ya = LeeTodo(sa, 4096);
            TRD.GetMethod("Sigue", T).Invoke(sb, new object[] { sa });
            float[] yb = LeeTodo(sb, 4096);
            float[] seguidas = new float[ya.Length + yb.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yb, 0, seguidas, ya.Length, yb.Length);
            Check(Igual(junta, seguidas), "dos pistas seguidas del mismo disco suenan como una sola (heredan detector, media y filtro)");
            ISampleProvider sc = Rd(new FuenteRD(b2, FS, 2), true);
            Check(!Igual(LeeTodo(sc, 4096), yb), "(y sin Sigue la segunda arranca de cero y sale distinta: la prueba mira algo)");
            float[] x = Fondo(12.0, 2, 21, 0.12, 0.05); SumaTono(x, 2, 250, 0.3, 6.0, 12.0);
            float[] enteros = LeeTodo(Rd(new FuenteRD(x, FS, 2), true), 4096);
            ISampleProvider s = Rd(new FuenteRD(x, FS, 2), true);
            List<float> yy = new List<float>(); float[] buf = new float[777]; int n, k = 0;
            while ((n = s.Read(buf, 0, (k++ % 3 == 0) ? 1 : (k % 2 == 0 ? 777 : 333))) > 0) for (int i = 0; i < n; i++) yy.Add(buf[i]);
            Check(Igual(enteros, yy.ToArray()), "pedir de a una muestra o de a numeros impares da lo mismo, bit a bit");
            float[] xm = Fondo(12.0, 1, 22, 0.12, 0.05); SumaTono(xm, 1, 250, 0.3, 6.0, 12.0);
            float[] ym1 = LeeTodo(Rd(new FuenteRD(xm, FS, 1), true), 4096), ym2 = LeeTodo(Rd(new FuenteRD(xm, FS, 1), true), 333);
            double gm = Db(Comp(xm, 1, 0, 250, 6.2, 6.4)) - Db(Comp(ym1, 1, 0, 250, 6.2, 6.4));
            Check(Igual(ym1, ym2) && gm > 1.0, "con un canal tambien actua (" + F(gm) + " dB) y da lo mismo con cualquier lectura");
            MethodInfo asignado = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (asignado != null)
            {
                ISampleProvider sm = Rd(new FuenteRD(Fondo(3.0, 2, 5, 0.12, 0.05), FS, 2), true);
                float[] bm = new float[4096]; sm.Read(bm, 0, bm.Length);
                long antes = (long)asignado.Invoke(null, null);
                for (int i = 0; i < 20; i++) sm.Read(bm, 0, bm.Length);
                long despues = (long)asignado.Invoke(null, null);
                Check(despues - antes < 1024, "leer no reserva memoria (" + (despues - antes) + " bytes en 20 lecturas)");
            }
            else Console.WriteLine("        (este Mono no cuenta la memoria reservada por hilo)");
            float[] cero = LeeTodo(Rd(new FuenteRD(new float[FS * 2], FS, 2), true), 4096); bool todoCero = true; foreach (float v in cero) if (v != 0f) { todoCero = false; break; }
            Check(todoCero, "en el silencio exacto sale cero exacto");
            float[] musica = Fondo(10.0, 2, 6, 0.2, 0.05);                              // parejo: el camino de casi siempre (sin corte)
            float[] cortando = Fondo(10.0, 2, 6, 0.2, 0.05); SumaTono(cortando, 2, 250, 0.4, 2.0, 10.0);   // con el retumbe: cortando casi todo el rato
            double[] on = { 1e9, 1e9 }, off = { 1e9, 1e9 };
            float[][] fu = { musica, cortando };
            for (int q = 0; q < 2; q++)
                for (int rep = 0; rep < 3; rep++)
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    LeeTodo(Rd(new FuenteRD(fu[q], FS, 2), true), 4410);
                    sw.Stop(); on[q] = Math.Min(on[q], sw.Elapsed.TotalMilliseconds / 10.0);
                    sw = System.Diagnostics.Stopwatch.StartNew();
                    LeeTodo(Rd(new FuenteRD(fu[q], FS, 2), false), 4410);
                    sw.Stop(); off[q] = Math.Min(off[q], sw.Elapsed.TotalMilliseconds / 10.0);
                }
            Console.WriteLine("        encendido: " + F(on[0]) + " ms por segundo de musica (sin cortar) y " + F(on[1]) + " (cortando); apagado (la copia de la prueba): " + F(off[0]) + " ms");
            Console.WriteLine("        la etapa cuesta " + F(on[0] - off[0]) + " y " + F(on[1] - off[1]) + " ms/s en este equipo (el A4 es unas 3 o 4 veces mas lento)");
            Check(on[0] - off[0] < 6.0 && on[1] - off[1] < 6.0, "cuesta poco (menos de 6 ms por segundo de musica en este equipo)");
        }

        Console.WriteLine("\n  5) LA CADENA, EL MODO AUTOMATICO, LA CONFIGURACION Y «ESCUCHAR SIN MEJORAS»\n");
        {
            List<string> ll = Codigo.Llamadas(TF.GetMethod("ArmaCadena", T));
            int iSala = ll.IndexOf("SalaLocal..ctor"), iRd = ll.FindIndex(delegate (string s) { return s.StartsWith("RetumbeDinamico"); }), iPre = ll.IndexOf("PresenciaSala..ctor");
            Check(iSala >= 0 && iRd > iSala && iPre > iRd, "va detras del local (EQ automatico y claridad) y delante de la presencia (el compresor)");
            Check(ll.Exists(delegate (string s) { return s.EndsWith("CorteEstaticoDb"); }), "al armarla se descuenta lo que ya cortan el EQ y la claridad");
            Check(Codigo.Llamadas(TF.GetMethod("AplicaBocinas", T)).Exists(delegate (string s) { return s.EndsWith("AjustaVozGolpe"); }), "el interruptor se oye al instante en la que suena y en la siguiente");
            Check(Codigo.Llamadas(TF.GetMethod("AplicaSinMejoras", T)).Exists(delegate (string s) { return s.EndsWith("AjustaVozGolpe"); }), "«Escuchar sin mejoras» pasa por el mismo sitio (y quita el retumbe)");
            Check(Codigo.Llama(TF.GetMethod("SigueA", T), "RetumbeDinamico.Sigue"), "el disco continuo enlaza la etapa");
            List<string> tl = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            Check(tl.Contains("retumbe_dinamico=") && ts.Contains("retumbe_dinamico="), "la configuracion lee y guarda retumbe_dinamico=");
            bool vol = false;
            foreach (MethodInfo m in TRD.GetMethods(T | BindingFlags.DeclaredOnly))
                foreach (string c in Codigo.Llamadas(m)) if (c.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("waveOutSet", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("Mixer", StringComparison.OrdinalIgnoreCase) >= 0) vol = true;
            Check(!vol, "la etapa no llama a nada de volumen (ni el de Windows ni el de la salida)");
            Check(Codigo.FuenteDeForm1().Contains("retumbeDinamico = false;"), "«Dejar como de fabrica» la apaga");
            MethodInfo auto = TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(float), typeof(float), typeof(float) }, null);
            object aj = auto.Invoke(null, new object[] { 8f, 5f, 10f }), aj2 = auto.Invoke(null, new object[] { -1f, -1f, 10f });
            Type tAj = aj.GetType();
            Check((bool)tAj.GetField("Retumbe").GetValue(aj) && (bool)tAj.GetField("Retumbe").GetValue(aj2), "el modo automatico del local la enciende (siempre, en el billar)");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK retumbedinamico: " + oks + " comprobaciones (ruido y tonos sinteticos en Linux/Mono; no es oirlo en el bar)" : "  FALLO retumbedinamico: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}

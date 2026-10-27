using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// QUE EL BOMBO NO HUNDA LA VOZ (Rocola/BomboSinHundir.cs, 27-10-2026): el bajo con su propio margen, delante del limitador de pico real.
// Con tonos y ruido sinteticos por la etapa de verdad (y por el limitador de verdad detras):
//  1) Apagada (de fabrica), o encendida sin que nada pase del techo: sale bit a bit lo que entra, con uno, dos y seis canales.
//  2) Lo que hace: un tono de 1 kHz con un bajo que pasa del techo. Solo con el limitador la voz (el 1 kHz) se hunde varios dB;
//     con la etapa, el bajo cede y el 1 kHz queda en su sitio. Si la que pasa del techo es la voz, el bajo no se toca.
//  3) La distorsion de un bajo sostenido 6 dB por encima del techo (40, 60 y 100 Hz): la espera de 20 ms evita modular dentro de un ciclo.
//  4) Los tiempos: ataque de 0,5 ms, espera de 20 ms, suelta de 80 ms; el piso de -6 dB.
//  5) Sin chasquidos al encender y apagar sonando; al apagar vuelve a 1 y a bit a bit.
//  6) Disco continuo, lecturas de cualquier tamano, sin reservar memoria, cuanto cuesta.
//  7) La cadena, el modo automatico, la configuracion y «Escuchar sin mejoras».
// Todo sintetico en Linux/Mono. No es escucharlo en el bar: eso solo se sabe alli.
class FuenteBH : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteBH(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class BomboSinHundirPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TBH, TLim;
    const int FS = 44100;
    static float techo;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static ISampleProvider Bh(ISampleProvider s, bool on) { return (ISampleProvider)Nuevo(TBH, s, on); }
    static ISampleProvider Lim(ISampleProvider s) { return (ISampleProvider)Nuevo(TLim, s, techo, 3.0); }
    static void Ajusta(object p, bool on) { TBH.GetMethod("Ajusta", T).Invoke(p, new object[] { on }); }
    static double Ganancia(object p) { return (double)TBH.GetProperty("GananciaAhora", T).GetValue(p, null); }
    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    static float[] Ruido(double seg, int semilla, double amp, int ch)
    {
        Random r = new Random(semilla); int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(amp * (r.NextDouble() * 2 - 1));
        return x;
    }
    // Suma de senos (frecuencia, amplitud), igual por los dos canales.
    static float[] Mezcla(double seg, int ch, params double[] fa)
    {
        int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++)
        {
            double v = 0;
            for (int k = 0; k < fa.Length; k += 2) v += fa[k + 1] * Math.Sin(2 * Math.PI * fa[k] * i / FS);
            for (int c = 0; c < ch; c++) x[ch * i + c] = (float)v;
        }
        return x;
    }
    // Amplitud del componente de 'hz' entre t0 y t1 (segundos), canal c: 2/N |sum x e^-jwt|.
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
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-15)); }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(a[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0)) return false;
        return true;
    }
    static double SaltoMax(float[] y, int ch, int c, int desde)
    {
        double m = 0; for (int i = ch * desde + ch + c; i < y.Length; i += ch) m = Math.Max(m, Math.Abs(y[i] - y[i - ch])); return m;
    }
    static double Thd(float[] y, double hz, double t0, double t1)
    {
        double f = Comp(y, 1, 0, hz, t0, t1), h = 0;
        for (int k = 2; k <= 8; k++) { double a = Comp(y, 1, 0, hz * k, t0, t1); h += a * a; }
        return Db(Math.Sqrt(h) / f);
    }
    static double Pico(float[] y, double t0, double t1) { double m = 0; for (int i = (int)(t0 * FS); i < Math.Min(y.Length, (int)(t1 * FS)); i++) m = Math.Max(m, Math.Abs(y[i])); return m; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TBH = asm.GetType("Rocola.BomboSinHundir"); TLim = TF.GetNestedType("LimitadorPicoReal", T);
        Check(TBH != null && TLim != null, "esta la etapa «Que el bombo no hunda la voz» y el limitador de pico real");
        if (TBH == null || TLim == null) { Console.WriteLine("  FALLO bombosinhundir: falta la etapa"); return 1; }
        techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);

        Console.WriteLine("\n  1) APAGADA, O SIN NADA QUE BAJAR: NI TOCA LA MUESTRA\n");
        float[] rx = Ruido(2.0, 3, 1.6, 2);   // ruido de hasta 1,6: pasa del techo a cada rato
        Check(Igual(LeeTodo(Bh(new FuenteBH(rx, FS, 2), false), 1000), rx), "apagada, estereo: sale bit a bit lo que entra (aunque pase del techo)");
        float[] rm = new float[FS]; Array.Copy(rx, rm, rm.Length);
        Check(Igual(LeeTodo(Bh(new FuenteBH(rm, FS, 1), false), 777), rm), "apagada, un canal: igual");
        float[] r6 = new float[6 * 4410]; Array.Copy(rx, r6, r6.Length);
        Check(Igual(LeeTodo(Bh(new FuenteBH(r6, FS, 6), true), 960), r6), "seis canales: no se toca aunque este encendida (no sabe que bocina es cual)");
        float[] r8k = new float[8000]; Array.Copy(rx, r8k, r8k.Length);
        Check(Igual(LeeTodo(Bh(new FuenteBH(r8k, 4000, 2), true), 500), r8k), "a menos de 8 kHz tampoco se toca");
        float[] bajoo = Ruido(2.0, 4, 0.8, 2);     // todo por debajo del techo (0,88)
        Check(Igual(LeeTodo(Bh(new FuenteBH(bajoo, FS, 2), true), 1000), bajoo), "encendida, con todo por debajo del techo: bit a bit, no se recombina nada");
        float[] sub = Mezcla(2.0, 2, 60, 0.5, 1000, 0.3);
        Check(Igual(LeeTodo(Bh(new FuenteBH(sub, FS, 2), true), 1000), sub), "encendida, un bajo fuerte pero que cabe: bit a bit");
        {   // la cota (bajo + resto en el peor de los casos) pasa del techo, pero la suma de verdad nunca: bit a bit
            float[] cota = Mezcla(3.0, 2, 60, 0.5, 1000, 0.36);
            double mx = 0; foreach (float v in cota) mx = Math.Max(mx, Math.Abs(v));
            ISampleProvider sc = Bh(new FuenteBH(cota, FS, 2), true);
            Check(mx < 0.88 && Igual(LeeTodo(sc, 1000), cota), "si nada pasa de verdad del techo (pico " + F(mx) + ") sale bit a bit, aunque bajo y voz juntos pudieran pasar");
        }
        Check(!(bool)TF.GetField("bomboSinHundir", T).GetValue(null), "de fabrica el interruptor esta apagado");
        {   // solo la voz pasa del techo: el bajo no se toca
            float[] voz = Mezcla(2.0, 2, 60, 0.15, 1000, 1.3);
            ISampleProvider s = Bh(new FuenteBH(voz, FS, 2), true);
            float[] y = LeeTodo(s, 1000);
            Check(Igual(y, voz) && Ganancia(s) == 1.0, "si la que pasa del techo es la voz (1 kHz a 1,3 y un bajo suave), el bajo no se toca: bit a bit");
        }

        Console.WriteLine("\n  2) LO QUE HACE: EL BAJO CEDE Y LA VOZ SE QUEDA\n");
        {   // bajo de 60 Hz a 1,2 (pasa del techo 0,88) y una voz de 1 kHz a 0,2
            float[] x = Mezcla(3.0, 2, 60, 1.2, 1000, 0.2);
            float[] solo = LeeTodo(Lim(new FuenteBH(x, FS, 2)), 1000);
            float[] sola = LeeTodo(Bh(new FuenteBH(x, FS, 2), true), 1000);
            Console.WriteLine("        la etapa sola deja un pico de " + F(Pico(sola, 1.5, 2.5)) + " (entrada " + F(Pico(x, 1.5, 2.5)) + ", techo " + F(techo) + ")");
            ISampleProvider b = Bh(new FuenteBH(x, FS, 2), true);
            float[] con = LeeTodo(Lim(b), 1000);
            double v0 = Db(Comp(x, 2, 0, 1000, 1.5, 2.5)), vSolo = Db(Comp(solo, 2, 0, 1000, 1.5, 2.5)), vCon = Db(Comp(con, 2, 0, 1000, 1.5, 2.5));
            double bSolo = Db(Comp(solo, 2, 0, 60, 1.5, 2.5)) - Db(Comp(x, 2, 0, 60, 1.5, 2.5)), bCon = Db(Comp(con, 2, 0, 60, 1.5, 2.5)) - Db(Comp(x, 2, 0, 60, 1.5, 2.5));
            Console.WriteLine("        el 1 kHz: solo con el limitador " + F(vSolo - v0) + " dB; con la etapa delante " + F(vCon - v0) + " dB. El bajo: " + F(bSolo) + " y " + F(bCon) + " dB");
            Check(vSolo - v0 < -2.0, "solo con el limitador, la voz (1 kHz) se hunde varios dB (" + F(vSolo - v0) + ")");
            Check(Math.Abs(vCon - v0) <= 0.3, "con la etapa delante, el 1 kHz queda a menos de 0,3 dB de su sitio (" + F(vCon - v0) + ")");
            Check(bCon < -3.0 && bCon > -6.5, "y es el bajo el que cede (" + F(bCon) + " dB, hasta 6)");
            Check(Pico(con, 1.0, 3.0) <= techo + 0.002, "la salida no pasa del techo (" + F(Pico(con, 1.0, 3.0)) + " frente a " + F(techo) + ")");
            // Las dos mitades izquierda y derecha iguales: una sola ganancia
            bool igualLR = true; for (int i = 0; i < con.Length; i += 2) if (con[i] != con[i + 1]) { igualLR = false; break; }
            Check(igualLR, "los dos canales llevan la misma ganancia (la imagen no se mueve)");
            // Un bajo apenas pasado del techo con la voz encima: la etapa baja poco el bajo
            float[] x2 = Mezcla(3.0, 2, 60, 0.95, 1000, 0.05);
            float[] c2 = LeeTodo(Bh(new FuenteBH(x2, FS, 2), true), 1000);
            double b2 = Db(Comp(c2, 2, 0, 60, 1.5, 2.5)) - Db(Comp(x2, 2, 0, 60, 1.5, 2.5));
            Check(b2 > -2.5 && b2 < -0.1, "con el bajo apenas pasado (0,95) baja poco, el margen que deja el resto con un pelo de sobra (" + F(b2) + " dB; lo justo seria -0,7)");
            // Con un canal al reves o el balance movido: cada canal con su bajo, la misma ganancia
            float[] x3 = Mezcla(2.0, 2, 60, 1.2, 1000, 0.2);
            for (int i = 0; i < x3.Length; i += 2) x3[i + 1] *= 0.5f;   // la derecha a la mitad (balance)
            float[] c3 = LeeTodo(Bh(new FuenteBH(x3, FS, 2), true), 1000);
            double gl = Comp(c3, 2, 0, 60, 1.0, 2.0) / Comp(x3, 2, 0, 60, 1.0, 2.0), gr = Comp(c3, 2, 1, 60, 1.0, 2.0) / Comp(x3, 2, 1, 60, 1.0, 2.0);
            Check(gl < 0.9 && Math.Abs(gl - gr) < 0.02 * gl, "con un canal mas bajo que el otro, los dos cambian lo mismo, por canal (" + F(gl) + " y " + F(gr) + ")");
        }

        foreach (double ataqueMs in new double[] { 0.0, 12.0 })
        {   // golpes de bajo (60 Hz, barrido hacia abajo, cae en 80 ms) cada 0,5 s con una voz de 1 kHz encima: de ataque seco (un bombo electronico)
            // y de ataque blando (12 ms: una nota de bajo). La etapa no mira hacia adelante: el primer pico de un golpe seco ya paso.
            int n = 8 * FS; float[] x = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)FS, tk = t % 0.5;
                double subida = ataqueMs <= 0 ? 1.0 : 1.0 - Math.Exp(-tk / (ataqueMs / 3000.0));
                double kick = 1.0 * subida * Math.Exp(-tk / 0.08) * Math.Sin(2 * Math.PI * (45 + 40 * Math.Exp(-tk / 0.03)) * tk);
                double voz = 0.4 * Math.Sin(2 * Math.PI * 1000 * t);
                x[2 * i] = x[2 * i + 1] = (float)(kick + voz);
            }
            float[] solo = LeeTodo(Lim(new FuenteBH(x, FS, 2)), 1000), con = LeeTodo(Lim(Bh(new FuenteBH(x, FS, 2), true)), 1000), st = LeeTodo(Bh(new FuenteBH(x, FS, 2), true), 1000);
            double dipSolo = 0, dipCon = 0, pkIn = 0, pkSt = 0; int golpes = 0;
            for (double c = 2.0; c < 7.5; c += 0.5)
            {   // la voz en los 30-110 ms de cada golpe (donde el limitador la hunde), contra la voz de la entrada
                double v0 = Comp(x, 2, 0, 1000, c + 0.03, c + 0.11);
                dipSolo += Db(Comp(solo, 2, 0, 1000, c + 0.03, c + 0.11)) - Db(v0); dipCon += Db(Comp(con, 2, 0, 1000, c + 0.03, c + 0.11)) - Db(v0); golpes++;
                pkIn += Pico(x, c + 0.02, c + 0.1); pkSt += Pico(st, c + 0.02, c + 0.1);
            }
            dipSolo /= golpes; dipCon /= golpes; pkIn /= golpes; pkSt /= golpes;
            Console.WriteLine("        golpes de bajo " + (ataqueMs <= 0 ? "secos" : "de ataque de " + ataqueMs + " ms") + " cada 0,5 s con la voz encima: la voz (1 kHz) baja de media " + F(dipSolo) + " dB solo con el limitador y " + F(dipCon) + " dB con la etapa delante; pico a los 20-100 ms: entrada " + F(pkIn) + ", etapa " + F(pkSt));
            Check(dipSolo < -1.0, "solo con el limitador, cada golpe hunde la voz (" + F(dipSolo) + " dB de media)");
            Check(pkSt < pkIn - 0.1, "la etapa baja los picos que siguen al primero (de " + F(pkIn) + " a " + F(pkSt) + ")");
            if (ataqueMs <= 0) Check(dipCon >= dipSolo - 0.1, "con el golpe seco no empeora la voz (" + F(dipCon) + " frente a " + F(dipSolo) + " dB): el primer pico, sin mirar hacia adelante, lo sigue recortando el limitador");
            else Check(dipCon > dipSolo + 0.8, "con el ataque blando la voz se hunde claramente menos (" + F(dipCon) + " frente a " + F(dipSolo) + " dB)");
            Check(Pico(con, 2.0, 8.0) <= techo + 0.002, "y nada pasa del techo (" + F(Pico(con, 2.0, 8.0)) + ")");
        }

        Console.WriteLine("\n  3) LA DISTORSION DE UN BAJO SOSTENIDO 6 DB POR ENCIMA DEL TECHO\n");
        foreach (double hz in new double[] { 40, 60, 100 })
        {
            float[] x = Mezcla(4.0, 1, hz, 2.0 * techo);
            float[] st = LeeTodo(Bh(new FuenteBH(x, FS, 1), true), 1000);
            float[] conL = LeeTodo(Lim(Bh(new FuenteBH(x, FS, 1), true)), 1000);
            float[] soloL = LeeTodo(Lim(new FuenteBH(x, FS, 1)), 1000);
            double dSt = Thd(st, hz, 2.0, 4.0), dCon = Thd(conL, hz, 2.0, 4.0), dSolo = Thd(soloL, hz, 2.0, 4.0);
            Console.WriteLine("        " + hz + " Hz: la etapa sola " + F(dSt) + " dB; con el limitador detras " + F(dCon) + "; solo el limitador " + F(dSolo));
            Check(dSt <= -60.0 && dCon <= -60.0, hz + " Hz: distorsion " + F(dCon) + " dB (la etapa sola " + F(dSt) + ") <= -60 dB");
            Check(Pico(conL, 2.0, 4.0) <= techo + 0.002, hz + " Hz: y no pasa del techo");
        }

        Console.WriteLine("\n  4) LOS TIEMPOS Y EL PISO\n");
        {   // 0,1 s de silencio, 60 Hz a 1,76 durante 1 s, luego a 0,3. Se lee de a un cuadro para ver la ganancia cuadro a cuadro.
            int n0 = FS / 10, n1 = FS, n2 = FS; float[] x = new float[(n0 + n1 + n2) * 2];
            for (int i = n0; i < n0 + n1 + n2; i++) { double a = i < n0 + n1 ? 2.0 * techo : 0.3; x[2 * i] = x[2 * i + 1] = (float)(a * Math.Sin(2 * Math.PI * 60 * (i - n0) / FS)); }
            // el ultimo ciclo fuerte acaba justo en un cruce por cero (1 s son 60 ciclos enteros)
            ISampleProvider s = Bh(new FuenteBH(x, FS, 2), true);
            float[] b = new float[2]; double[] g = new double[n0 + n1 + n2];
            for (int i = 0; i < n0 + n1 + n2; i++) { s.Read(b, 0, 2); g[i] = Ganancia(s); }
            int tAta = -1; for (int i = n0; i < n0 + n1; i++) if (g[i] <= 0.6) { tAta = i - n0; break; }
            Console.WriteLine("        baja a 0,6 a los " + F(1000.0 * tAta / FS) + " ms de empezar el bajo; en regimen " + F(g[n0 + n1 - 1]));
            Check(tAta >= 0 && 1000.0 * tAta / FS < 12.0, "ataque rapido: llega a 0,6 en menos de 12 ms (el primer pico de 60 Hz tarda 4 ms en llegar) (" + F(1000.0 * tAta / FS) + " ms)");
            Check(g[n0 + n1 - 1] >= 0.499 && g[n0 + n1 - 1] <= 0.51, "el piso es 0,5 (-6 dB): " + F(g[n0 + n1 - 1]));
            // despues del ultimo pico fuerte (a ~1 s - 4 ms): espera de 20 ms y suelta de 80 ms
            int ult = n0 + n1 - 1 - (int)(0.0042 * FS);
            Func<double, double> en = delegate (double ms) { return g[Math.Min(g.Length - 1, ult + (int)(ms * 0.001 * FS))]; };
            Console.WriteLine("        tras el ultimo golpe: a los 10 ms " + F(en(10)) + ", 20 ms " + F(en(20)) + ", 30 ms " + F(en(30)) + ", 100 ms " + F(en(100)) + ", 200 ms " + F(en(200)) + ", 1 s " + F(g[g.Length - 1]));
            Check(en(15) < 0.51, "espera de 20 ms quieta: a los 15 ms sigue en el piso (" + F(en(15)) + ")");
            Check(en(40) > 0.51 && en(40) < 0.75, "luego suelta despacio: a los 40 ms " + F(en(40)));
            double objetivo = 1.0 - 0.5; double r80 = (en(20 + 80) - 0.5) / objetivo;   // 63 % a los 80 ms despues de la espera
            Check(r80 > 0.5 && r80 < 0.75, "a los 80 ms de soltar ha vuelto el " + F(100 * r80) + " % (la constante de 80 ms da 63 %)");
            Check(g[g.Length - 1] == 1.0, "y al cabo de un rato la ganancia es 1 exacto (" + g[g.Length - 1].ToString("0.000000") + ")");
        }

        Console.WriteLine("\n  5) SIN CHASQUIDOS AL ENCENDER Y APAGAR\n");
        {
            float[] x = Mezcla(5.0, 2, 60, 1.6, 1000, 0.1);
            ISampleProvider s = Bh(new FuenteBH(x, FS, 2), false);
            float[] b = new float[2048]; List<float> y = new List<float>(); int n, k = 0;
            while ((n = s.Read(b, 0, b.Length)) > 0)
            {
                for (int i = 0; i < n; i++) y.Add(b[i]); k++;
                if (k == 10) Ajusta(s, true);
                if (k == 70) Ajusta(s, false);
            }
            float[] ya = y.ToArray();
            double pend = (1.6 * 2 * Math.PI * 60 + 0.1 * 2 * Math.PI * 1000) / FS;   // la pendiente maxima de la entrada por muestra
            double salto = Math.Max(SaltoMax(ya, 2, 0, 0), SaltoMax(ya, 2, 1, 0));
            Console.WriteLine("        salto maximo " + F(salto) + " frente a la pendiente de la entrada " + F(pend));
            Check(salto < pend * 1.5, "encender y apagar sonando no da golpe (salto " + F(salto) + " frente a " + F(pend) + ")");
            float[] fin = new float[FS / 4 * 2]; Array.Copy(ya, ya.Length - fin.Length, fin, 0, fin.Length);
            float[] xfin = new float[fin.Length]; Array.Copy(x, x.Length - fin.Length, xfin, 0, fin.Length);
            Check(Igual(fin, xfin), "apagada otra vez, en unos 40 ms vuelve a salir bit a bit");
            Check(Ganancia(s) == 1.0, "y su ganancia es 1 exacto");
        }

        Console.WriteLine("\n  6) DISCO CONTINUO, LECTURAS Y COSTE\n");
        {
            float[] a = Mezcla(1.0, 2, 60, 1.4, 1000, 0.2), b2 = Mezcla(1.0, 2, 60, 1.4, 1000, 0.2), ab = new float[a.Length + b2.Length];
            Array.Copy(a, ab, a.Length); Array.Copy(b2, 0, ab, a.Length, b2.Length);
            float[] junta = LeeTodo(Bh(new FuenteBH(ab, FS, 2), true), 4096);
            ISampleProvider sa = Bh(new FuenteBH(a, FS, 2), true), sb = Bh(new FuenteBH(b2, FS, 2), true);
            float[] ya = LeeTodo(sa, 4096);
            TBH.GetMethod("Sigue", T).Invoke(sb, new object[] { sa });
            float[] yb = LeeTodo(sb, 4096);
            float[] seguidas = new float[ya.Length + yb.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yb, 0, seguidas, ya.Length, yb.Length);
            Check(Igual(junta, seguidas), "dos pistas seguidas del mismo disco suenan como una sola (heredan filtros y ganancia)");
            ISampleProvider sc = Bh(new FuenteBH(b2, FS, 2), true);
            float[] sinSigue = LeeTodo(sc, 4096);
            Check(!Igual(sinSigue, yb), "(y sin Sigue la segunda arranca de cero y sale distinta: la prueba mira algo)");
            float[] x = Mezcla(1.0, 2, 60, 1.3, 1000, 0.3);
            float[] enteros = LeeTodo(Bh(new FuenteBH(x, FS, 2), true), 4096);
            ISampleProvider s = Bh(new FuenteBH(x, FS, 2), true);
            List<float> yy = new List<float>(); float[] buf = new float[777]; int n, k = 0;
            while ((n = s.Read(buf, 0, (k++ % 3 == 0) ? 1 : (k % 2 == 0 ? 777 : 333))) > 0) for (int i = 0; i < n; i++) yy.Add(buf[i]);
            Check(Igual(enteros, yy.ToArray()), "pedir de a una muestra o de a numeros impares da lo mismo, bit a bit");
            float[] xm = Mezcla(1.0, 1, 60, 1.4, 1000, 0.2);
            float[] ym1 = LeeTodo(Bh(new FuenteBH(xm, FS, 1), true), 4096), ym2 = LeeTodo(Bh(new FuenteBH(xm, FS, 1), true), 333);
            double gm = Db(Comp(ym1, 1, 0, 60, 0.5, 1.0)) - Db(Comp(xm, 1, 0, 60, 0.5, 1.0));
            Check(Igual(ym1, ym2) && gm < -2.0, "con un canal tambien actua (" + F(gm) + " dB) y da lo mismo con cualquier lectura");
            MethodInfo asignado = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (asignado != null)
            {
                ISampleProvider sm = Bh(new FuenteBH(Mezcla(3.0, 2, 60, 1.4, 1000, 0.2), FS, 2), true);
                float[] bm = new float[4096]; sm.Read(bm, 0, bm.Length);
                long antes = (long)asignado.Invoke(null, null);
                for (int i = 0; i < 20; i++) sm.Read(bm, 0, bm.Length);
                long despues = (long)asignado.Invoke(null, null);
                Check(despues - antes < 1024, "leer no reserva memoria (" + (despues - antes) + " bytes en 20 lecturas)");
            }
            else Console.WriteLine("        (este Mono no cuenta la memoria reservada por hilo)");
            // el silencio exacto no deja restos (desnormales) y el coste: encendida contra apagada con la misma copia
            ISampleProvider ss = Bh(new FuenteBH(new float[FS * 2], FS, 2), true);
            float[] cero = LeeTodo(ss, 4096); bool todoCero = true; foreach (float v in cero) if (v != 0f) { todoCero = false; break; }
            Check(todoCero, "en el silencio exacto sale cero exacto");
            float[] musica = Ruido(10.0, 6, 0.4, 2);                       // sin nada que bajar: el camino de casi siempre
            float[] fuerte = Mezcla(10.0, 2, 60, 1.4, 1000, 0.2);          // con el bajo pasado: bajando a todas horas
            double[] on = { 1e9, 1e9 }, off = { 1e9, 1e9 };
            float[][] fu = { musica, fuerte };
            for (int q = 0; q < 2; q++)
                for (int rep = 0; rep < 3; rep++)
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    LeeTodo(Bh(new FuenteBH(fu[q], FS, 2), true), 4410);
                    sw.Stop(); on[q] = Math.Min(on[q], sw.Elapsed.TotalMilliseconds / 10.0);
                    sw = System.Diagnostics.Stopwatch.StartNew();
                    LeeTodo(Bh(new FuenteBH(fu[q], FS, 2), false), 4410);
                    sw.Stop(); off[q] = Math.Min(off[q], sw.Elapsed.TotalMilliseconds / 10.0);
                }
            Console.WriteLine("        encendida: " + F(on[0]) + " ms por segundo de musica (nada que bajar) y " + F(on[1]) + " (bajando siempre); apagada (la copia de la prueba): " + F(off[0]) + " ms");
            Console.WriteLine("        la etapa cuesta " + F(on[0] - off[0]) + " y " + F(on[1] - off[1]) + " ms/s en este equipo (el A4 es unas 3 o 4 veces mas lento)");
            Check(on[0] - off[0] < 4.0 && on[1] - off[1] < 4.0, "cuesta poco (menos de 4 ms por segundo de musica en este equipo)");
        }

        Console.WriteLine("\n  7) LA CADENA, EL MODO AUTOMATICO, LA CONFIGURACION Y «ESCUCHAR SIN MEJORAS»\n");
        {
            List<string> ll = Codigo.Llamadas(TF.GetMethod("ArmaCadena", T));
            int iBajo = ll.IndexOf("DeepBassProvider..ctor"), iBh = ll.FindIndex(delegate (string s) { return s.StartsWith("BomboSinHundir"); }), iLim = ll.IndexOf("LimitadorPicoReal..ctor");
            Check(iBajo >= 0 && iBh > iBajo && iLim > iBh, "va detras del bajo psicoacustico y justo delante del limitador de pico real");
            Check(Codigo.Llamadas(TF.GetMethod("AplicaBocinas", T)).Exists(delegate (string s) { return s.EndsWith("AjustaVozGolpe"); }), "el interruptor se oye al instante en la que suena y en la siguiente");
            Check(Codigo.Llamadas(TF.GetMethod("AplicaSinMejoras", T)).Exists(delegate (string s) { return s.EndsWith("AjustaVozGolpe"); }), "«Escuchar sin mejoras» pasa por el mismo sitio (y deja el bombo encendido)");
            Check(Codigo.Llama(TF.GetMethod("SigueA", T), "BomboSinHundir.Sigue"), "el disco continuo enlaza la etapa");
            List<string> tl = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            Check(tl.Contains("bombo_sin_hundir=") && ts.Contains("bombo_sin_hundir="), "la configuracion lee y guarda bombo_sin_hundir=");
            bool vol = false;
            foreach (MethodInfo m in TBH.GetMethods(T | BindingFlags.DeclaredOnly))
                foreach (string c in Codigo.Llamadas(m)) if (c.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("waveOutSet", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("Mixer", StringComparison.OrdinalIgnoreCase) >= 0) vol = true;
            Check(!vol, "la etapa no llama a nada de volumen (ni el de Windows ni el de la salida)");
            string fuenteF3 = Codigo.FuenteDeForm1();
            Check(fuenteF3.Contains("bomboSinHundir = false;"), "«Dejar como de fabrica» la apaga");
            // El modo automatico del local la enciende; apagado, manda el interruptor
            MethodInfo auto = TF.GetMethod("LocalAutomatico", T);
            object aj = auto.Invoke(null, new object[] { 8f, 5f, 10f });
            Type tAj = aj.GetType();
            Check((bool)tAj.GetField("Bombo").GetValue(aj), "el modo automatico del local la enciende (siempre, en el billar)");
            object aj2 = auto.Invoke(null, new object[] { -1f, -1f, 10f });
            Check((bool)tAj.GetField("Bombo").GetValue(aj2), "y tambien en una cancion sin medir");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK bombosinhundir: " + oks + " comprobaciones (tonos sinteticos en Linux/Mono; no es oirlo en el bar)" : "  FALLO bombosinhundir: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}

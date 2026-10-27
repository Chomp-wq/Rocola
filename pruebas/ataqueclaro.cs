using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// ATAQUE MAS CLARO (Rocola/AtaqueClaro.cs, 27-10-2026): menos cola detras de cada golpe y, con sitio bajo el techo, algo mas de ataque.
// Con tonos, ruido y rafagas sinteticas por la etapa de verdad:
//  1) Apagado (de fabrica): sale bit a bit lo que entra, con uno, dos y seis canales. Un tono parejo o un ruido parejo no se tocan.
//  2) Lo que hace: rafagas que se apagan (golpes con cola) bajan 2-3 dB en la cola; el pico del golpe no cambia cuando no hay sitio; con
//     sitio, el ataque sube como mucho 1,5 dB; nunca pasa del techo; el bajo (por debajo de 120 Hz) no se mueve.
//  3) Sin chasquidos al encender y apagar sonando; al apagar vuelve a 1 en 40 ms y a bit a bit.
//  4) Disco continuo, lecturas de cualquier tamano, sin reservar memoria, cuanto cuesta.
//  5) La cadena, el modo automatico, la configuracion y «Escuchar sin mejoras».
// Todo sintetico en Linux/Mono. No es escucharlo en el bar: eso solo se sabe alli.
class FuenteAC : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteAC(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class AtaqueClaroPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TAC;
    const int FS = 44100;
    static float techo;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static ISampleProvider Ac(ISampleProvider s, bool on) { return (ISampleProvider)Nuevo(TAC, s, on); }
    static void Ajusta(object p, bool on) { TAC.GetMethod("Ajusta", T).Invoke(p, new object[] { on }); }
    static double Prop(object p, string n) { return (double)TAC.GetProperty(n, T).GetValue(p, null); }
    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    static float[] Tono(double hz, double seg, double amp, int ch)
    {
        int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++) { float v = (float)(amp * Math.Sin(2 * Math.PI * hz * i / FS)); for (int c = 0; c < ch; c++) x[ch * i + c] = v; }
        return x;
    }
    static float[] Ruido(double seg, int semilla, double amp, int ch)
    {
        Random r = new Random(semilla); int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++) { float v = (float)(amp * (r.NextDouble() * 2 - 1)); for (int c = 0; c < ch; c++) x[ch * i + c] = v; }
        return x;
    }
    // Golpes: cada 'cada' s, una rafaga de 'hz' que dura 'dura' s a amplitud 'amp' y luego se apaga como e^(-t/tau).
    static float[] Golpes(double seg, int ch, double hz, double amp, double cada, double dura, double tau, double suelo)
    {
        int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++)
        {
            double t = (i / (double)FS) % cada, a = t < dura ? 1.0 : Math.Exp(-(t - dura) / tau);
            float v = (float)(amp * a * Math.Sin(2 * Math.PI * hz * i / FS) + suelo * Math.Sin(2 * Math.PI * 3300 * i / FS));
            for (int c = 0; c < ch; c++) x[ch * i + c] = v;
        }
        return x;
    }
    static double Ef(float[] y, int ch, int c, double desde, double hasta)
    {
        int a = (int)(desde * FS), b = Math.Min(y.Length / ch, (int)(hasta * FS)); double s = 0;
        for (int i = a; i < b; i++) s += (double)y[i * ch + c] * y[i * ch + c];
        return Math.Sqrt(s / Math.Max(1, b - a));
    }
    static double Pico(float[] y, int ch, double t0, double t1) { double m = 0; for (int i = (int)(t0 * FS) * ch; i < Math.Min(y.Length, (int)(t1 * FS) * ch); i++) m = Math.Max(m, Math.Abs(y[i])); return m; }
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
    // Ganancia (dB) en ventanas de 'w' ms a lo largo de 'seg': la salida contra la entrada.
    static double Gan(float[] x, float[] y, double t0, double t1) { return Db(Ef(y, 2, 0, t0, t1)) - Db(Ef(x, 2, 0, t0, t1)); }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TAC = asm.GetType("Rocola.AtaqueClaro");
        Check(TAC != null, "esta la etapa del ataque mas claro");
        if (TAC == null) { Console.WriteLine("  FALLO ataqueclaro: falta la etapa"); return 1; }
        techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);

        Console.WriteLine("\n  1) APAGADO, O SIN NADA QUE HACER: NI TOCA LA MUESTRA\n");
        float[] rx = Ruido(2.0, 3, 0.5, 2);
        Check(Igual(LeeTodo(Ac(new FuenteAC(rx, FS, 2), false), 1000), rx), "apagado, estereo: sale bit a bit lo que entra");
        float[] rm = new float[FS]; Array.Copy(rx, rm, rm.Length);
        Check(Igual(LeeTodo(Ac(new FuenteAC(rm, FS, 1), false), 777), rm), "apagado, un canal: igual");
        float[] r6 = new float[6 * 4410]; Array.Copy(rx, r6, r6.Length);
        Check(Igual(LeeTodo(Ac(new FuenteAC(r6, FS, 6), true), 960), r6), "seis canales: no se toca aunque este encendido (no sabe que bocina es cual)");
        float[] r8k = new float[8000]; Array.Copy(rx, r8k, r8k.Length);
        Check(Igual(LeeTodo(Ac(new FuenteAC(r8k, 4000, 2), true), 500), r8k), "a menos de 8 kHz tampoco se toca");
        Check(!(bool)TF.GetField("ataqueClaro", T).GetValue(null), "de fabrica el interruptor esta apagado");
        foreach (double hz in new double[] { 200, 1000, 5000 })
        {
            float[] x = Tono(hz, 4.0, 0.3, 2);
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            double dmax = 0; for (double t = 0.5; t < 3.9; t += 0.1) dmax = Math.Max(dmax, Math.Abs(Gan(x, y, t, t + 0.1)));
            Check(dmax < 0.1, "un tono parejo de " + hz + " Hz no se toca: a menos de 0,1 dB todo el rato (" + F(dmax) + " dB)");
        }
        {
            float[] x = Ruido(8.0, 5, 0.3, 2);
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            double dmax = 0; for (double t = 0.5; t < 7.9; t += 0.05) dmax = Math.Max(dmax, Math.Abs(Gan(x, y, t, t + 0.05)));
            double dm = Gan(x, y, 1.0, 8.0);
            Console.WriteLine("        ruido blanco parejo 8 s: ganancia media " + F(dm) + " dB; en ventanas de 50 ms hasta " + F(dmax) + " dB");
            Check(Math.Abs(dm) < 0.15 && dmax < 1.0, "un ruido parejo casi no se toca (media " + F(dm) + " dB, ventanas de 50 ms hasta " + F(dmax) + " dB)");
        }

        Console.WriteLine("\n  2) LO QUE HACE: MENOS COLA Y, SI HAY SITIO, MAS ATAQUE\n");
        {
            // Golpes de 1 kHz (40 ms, luego se apagan con 50 ms de constante), cada 0,4 s, con poca amplitud: hay sitio bajo el techo.
            float[] x = Golpes(12.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.0);
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            Func<double, double, double> g = delegate (double a, double b) { double s = 0; int n = 0; for (double c = 4.0; c < 11.0; c += 0.4) { s += Gan(x, y, c + a, c + b); n++; } return s / n; };
            Console.WriteLine("        ganancia por tramo del golpe (media de 17 golpes): 0-5 ms " + F(g(0, 0.005)) + "; 5-20 ms " + F(g(0.005, 0.02)) + "; 20-40 ms " + F(g(0.02, 0.04))
                + "; cola 40-60 ms " + F(g(0.04, 0.06)) + "; 60-100 ms " + F(g(0.06, 0.10)) + "; 100-200 ms " + F(g(0.10, 0.20)) + "; 200-300 ms " + F(g(0.20, 0.30)));
            double picoIn = Pico(x, 2, 6.0, 6.2), picoOut = Pico(y, 2, 6.0, 6.2);
            double cola = 0; for (double a = 0.04; a < 0.3; a += 0.02) cola = Math.Min(cola, g(a, a + 0.02));
            double alza = 0; for (double a = 0.0; a < 0.04; a += 0.005) alza = Math.Max(alza, g(a, a + 0.005));
            Console.WriteLine("        pico del golpe: entrada " + F(picoIn) + ", salida " + F(picoOut) + " (" + F(Db(picoOut / picoIn)) + " dB); la cola baja hasta " + F(cola) + " dB; el ataque sube hasta " + F(alza) + " dB");
            Check(cola <= -2.0 && cola >= -3.3, "la cola baja de 2 a 3 dB (" + F(cola) + " dB)");
            Check(alza >= 0.2 && alza <= 1.6, "con sitio bajo el techo el ataque sube (" + F(alza) + " dB, como mucho 1,5)");
            Check(Pico(y, 2, 0.0, 12.0) < techo, "y no pasa del techo del limitador (" + F(Pico(y, 2, 0.0, 12.0)) + " frente a " + F(techo) + ")");
        }
        {
            // Los mismos golpes pero ya al techo: no hay sitio, el pico no cambia y la cola sigue bajando.
            float[] x = Golpes(12.0, 2, 1000, 0.86, 0.4, 0.04, 0.05, 0.0);
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            double picoIn = Pico(x, 2, 6.0, 8.0), picoOut = Pico(y, 2, 6.0, 8.0);
            Func<double, double, double> g = delegate (double a, double b) { double s = 0; int n = 0; for (double c = 4.0; c < 11.0; c += 0.4) { s += Gan(x, y, c + a, c + b); n++; } return s / n; };
            double cola = 0; for (double a = 0.04; a < 0.3; a += 0.02) cola = Math.Min(cola, g(a, a + 0.02));
            Console.WriteLine("        al techo: pico de entrada " + F(picoIn) + ", de salida " + F(picoOut) + " (" + F(Db(picoOut / picoIn)) + " dB); la cola baja hasta " + F(cola) + " dB");
            Check(Math.Abs(Db(picoOut / picoIn)) < 0.1, "sin sitio bajo el techo el pico del golpe se queda a menos de 0,1 dB (" + F(Db(picoOut / picoIn)) + " dB)");
            Check(cola < -2.0, "y la cola baja igual (" + F(cola) + " dB)");
            Check(Pico(y, 2, 0.0, 12.0) <= Pico(x, 2, 0.0, 12.0) + 0.002, "nunca sube el pico de todo el tema (" + F(Pico(y, 2, 0.0, 12.0)) + " frente a " + F(Pico(x, 2, 0.0, 12.0)) + ")");
        }
        {
            // El bajo no se mueve: golpes de 80 Hz.
            float[] x = Golpes(12.0, 2, 80, 0.25, 0.4, 0.04, 0.05, 0.0);
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            double dmax = 0; for (double c = 4.0; c < 11.0; c += 0.4) for (double a = 0.0; a < 0.35; a += 0.05) dmax = Math.Max(dmax, Math.Abs(Gan(x, y, c + a, c + a + 0.05)));
            Console.WriteLine("        golpes de 80 Hz: la ganancia llega a " + F(dmax) + " dB");
            Check(dmax < 1.2, "el bajo de 80 Hz casi no se mueve (" + F(dmax) + " dB como mucho; el paso-alto de 120 Hz aun deja pasar una parte)");
            // el mismo bajo con un fondo agudo parejo encima: los 80 Hz no bombean aunque el fondo mande
            float[] x2 = Golpes(12.0, 2, 80, 0.25, 0.4, 0.04, 0.05, 0.1);
            float[] y2 = LeeTodo(Ac(new FuenteAC(x2, FS, 2), true), 4096);
            double r1 = 0; for (double c = 4.0; c < 11.0; c += 0.4) r1 = Math.Max(r1, Math.Abs(Gan(x2, y2, c + 0.1, c + 0.3)));
            Check(r1 < 1.5, "con un fondo agudo parejo el resto no bombea (" + F(r1) + " dB)");
        }
        {   // los dos canales llevan la misma ganancia
            float[] x = Golpes(6.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.0);
            for (int i = 0; i < x.Length; i += 2) x[i + 1] *= 0.4f;   // la derecha mas baja
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            double dif = 0; for (double c = 2.0; c < 5.0; c += 0.4) dif = Math.Max(dif, Math.Abs(Gan(x, y, c, c + 0.2) - (Db(Ef(y, 2, 1, c, c + 0.2)) - Db(Ef(x, 2, 1, c, c + 0.2)))));
            Check(dif < 0.05, "los dos canales llevan la misma ganancia aunque uno suene mas bajo (" + F(dif) + " dB de diferencia)");
        }
        {   // lo callado no se toca: por debajo de -55 dBFS
            float[] x = Golpes(8.0, 2, 1000, 0.0006, 0.4, 0.04, 0.05, 0.0);
            float[] y = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            Check(Igual(y, x), "por debajo de -55 dBFS no se toca nada (el soplido de la grabacion se queda como esta): bit a bit");
        }

        Console.WriteLine("\n  3) SIN CHASQUIDOS AL ENCENDER Y APAGAR\n");
        {
            float[] x = Golpes(8.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.0);
            ISampleProvider s = Ac(new FuenteAC(x, FS, 2), false);
            float[] b = new float[2048]; List<float> y = new List<float>(); int n, k = 0;
            while ((n = s.Read(b, 0, b.Length)) > 0)
            {
                for (int i = 0; i < n; i++) y.Add(b[i]); k++;
                if (k == 40) Ajusta(s, true);
                if (k == 140) Ajusta(s, false);
            }
            float[] ya = y.ToArray();
            double salto = Math.Max(SaltoMax(ya, 2, 0), SaltoMax(ya, 2, 1)), saltoEnt = Math.Max(SaltoMax(x, 2, 0), SaltoMax(x, 2, 1));
            float[] siempre = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 2048);
            double saltoOn = Math.Max(SaltoMax(siempre, 2, 0), SaltoMax(siempre, 2, 1));
            Console.WriteLine("        salto maximo de la salida " + F(salto) + "; encendido todo el rato " + F(saltoOn) + "; de la entrada " + F(saltoEnt));
            Check(salto <= saltoOn * 1.02, "encender y apagar sonando no da golpe: el salto mayor (" + F(salto) + ") no pasa del que da encendido todo el rato (" + F(saltoOn) + ")");
            float[] fin = new float[FS / 2 * 2]; Array.Copy(ya, ya.Length - fin.Length, fin, 0, fin.Length);
            float[] xfin = new float[fin.Length]; Array.Copy(x, x.Length - fin.Length, xfin, 0, fin.Length);
            Check(Igual(fin, xfin), "apagado otra vez, en unos 40 ms vuelve a salir bit a bit");
            Check(Prop(s, "GananciaDb") == 0.0, "y su ganancia es 0 dB exacto");
        }

        Console.WriteLine("\n  4) DISCO CONTINUO, LECTURAS Y COSTE\n");
        {
            float[] a = Golpes(2.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.0), b2 = Golpes(2.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.0), ab = new float[a.Length + b2.Length];
            Array.Copy(a, ab, a.Length); Array.Copy(b2, 0, ab, a.Length, b2.Length);
            float[] junta = LeeTodo(Ac(new FuenteAC(ab, FS, 2), true), 4096);
            ISampleProvider sa = Ac(new FuenteAC(a, FS, 2), true), sb = Ac(new FuenteAC(b2, FS, 2), true);
            float[] ya = LeeTodo(sa, 4096);
            TAC.GetMethod("Sigue", T).Invoke(sb, new object[] { sa });
            float[] yb = LeeTodo(sb, 4096);
            float[] seguidas = new float[ya.Length + yb.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yb, 0, seguidas, ya.Length, yb.Length);
            Check(Igual(junta, seguidas), "dos pistas seguidas del mismo disco suenan como una sola (heredan envolventes, ganancia y pico)");
            Check(!Igual(LeeTodo(Ac(new FuenteAC(b2, FS, 2), true), 4096), yb), "(y sin Sigue la segunda arranca de cero y sale distinta: la prueba mira algo)");
            float[] x = Golpes(2.0, 2, 1000, 0.3, 0.4, 0.04, 0.05, 0.05);
            float[] enteros = LeeTodo(Ac(new FuenteAC(x, FS, 2), true), 4096);
            ISampleProvider s = Ac(new FuenteAC(x, FS, 2), true);
            List<float> yy = new List<float>(); float[] buf = new float[777]; int n, k = 0;
            while ((n = s.Read(buf, 0, (k++ % 3 == 0) ? 1 : (k % 2 == 0 ? 777 : 333))) > 0) for (int i = 0; i < n; i++) yy.Add(buf[i]);
            Check(Igual(enteros, yy.ToArray()), "pedir de a una muestra o de a numeros impares da lo mismo, bit a bit");
            float[] xm = Golpes(3.0, 1, 1000, 0.25, 0.4, 0.04, 0.05, 0.0);
            float[] ym1 = LeeTodo(Ac(new FuenteAC(xm, FS, 1), true), 4096), ym2 = LeeTodo(Ac(new FuenteAC(xm, FS, 1), true), 333);
            Check(Igual(ym1, ym2) && !Igual(ym1, xm), "con un canal tambien actua y da lo mismo con cualquier lectura");
            MethodInfo asignado = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (asignado != null)
            {
                ISampleProvider sm = Ac(new FuenteAC(Golpes(3.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.0), FS, 2), true);
                float[] bm = new float[4096]; sm.Read(bm, 0, bm.Length);
                long antes = (long)asignado.Invoke(null, null);
                for (int i = 0; i < 20; i++) sm.Read(bm, 0, bm.Length);
                long despues = (long)asignado.Invoke(null, null);
                Check(despues - antes < 1024, "leer no reserva memoria (" + (despues - antes) + " bytes en 20 lecturas)");
            }
            else Console.WriteLine("        (este Mono no cuenta la memoria reservada por hilo)");
            float[] cero = LeeTodo(Ac(new FuenteAC(new float[FS * 2], FS, 2), true), 4096); bool todoCero = true; foreach (float v in cero) if (v != 0f) { todoCero = false; break; }
            Check(todoCero, "en el silencio exacto sale cero exacto");
            float[] musica = Ruido(10.0, 6, 0.3, 2);
            float[] golpes = Golpes(10.0, 2, 1000, 0.25, 0.4, 0.04, 0.05, 0.02);
            double[] on = { 1e9, 1e9 }, off = { 1e9, 1e9 };
            float[][] fu = { musica, golpes };
            for (int q = 0; q < 2; q++)
                for (int rep = 0; rep < 3; rep++)
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    LeeTodo(Ac(new FuenteAC(fu[q], FS, 2), true), 4410);
                    sw.Stop(); on[q] = Math.Min(on[q], sw.Elapsed.TotalMilliseconds / 10.0);
                    sw = System.Diagnostics.Stopwatch.StartNew();
                    LeeTodo(Ac(new FuenteAC(fu[q], FS, 2), false), 4410);
                    sw.Stop(); off[q] = Math.Min(off[q], sw.Elapsed.TotalMilliseconds / 10.0);
                }
            Console.WriteLine("        encendido: " + F(on[0]) + " ms por segundo de musica (ruido) y " + F(on[1]) + " (golpes); apagado (la copia de la prueba): " + F(off[0]) + " ms");
            Console.WriteLine("        la etapa cuesta " + F(on[0] - off[0]) + " y " + F(on[1] - off[1]) + " ms/s en este equipo (el A4 es unas 3 o 4 veces mas lento)");
            Check(on[0] - off[0] < 6.0 && on[1] - off[1] < 6.0, "cuesta poco (menos de 6 ms por segundo de musica en este equipo)");
        }

        Console.WriteLine("\n  5) LA CADENA, EL MODO AUTOMATICO, LA CONFIGURACION Y «ESCUCHAR SIN MEJORAS»\n");
        {
            List<string> ll = Codigo.Llamadas(TF.GetMethod("ArmaCadena", T));
            int iPre = ll.IndexOf("PresenciaSala..ctor"), iAc = ll.FindIndex(delegate (string s) { return s.StartsWith("AtaqueClaro"); }), iEs = ll.FindIndex(delegate (string s) { return s.StartsWith("SuavizaEses"); }), iBajo = ll.IndexOf("DeepBassProvider..ctor");
            Check(iPre >= 0 && iAc > iPre && iEs > iAc && iBajo > iEs, "va detras de la presencia (el compresor) y delante de las eses, del bajo psicoacustico y del limitador");
            Check(Codigo.Llamadas(TF.GetMethod("AplicaBocinas", T)).Exists(delegate (string s) { return s.EndsWith("AjustaVozGolpe"); }), "el interruptor se oye al instante en la que suena y en la siguiente");
            Check(Codigo.Llamadas(TF.GetMethod("AplicaSinMejoras", T)).Exists(delegate (string s) { return s.EndsWith("AjustaVozGolpe"); }), "«Escuchar sin mejoras» pasa por el mismo sitio (y quita el ataque)");
            Check(Codigo.Llama(TF.GetMethod("SigueA", T), "AtaqueClaro.Sigue"), "el disco continuo enlaza la etapa");
            List<string> tl = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            Check(tl.Contains("ataque_claro=") && ts.Contains("ataque_claro="), "la configuracion lee y guarda ataque_claro=");
            bool vol = false;
            foreach (MethodInfo m in TAC.GetMethods(T | BindingFlags.DeclaredOnly))
                foreach (string c in Codigo.Llamadas(m)) if (c.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("waveOutSet", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("Mixer", StringComparison.OrdinalIgnoreCase) >= 0) vol = true;
            Check(!vol, "la etapa no llama a nada de volumen (ni el de Windows ni el de la salida)");
            Check(Codigo.FuenteDeForm1().Contains("ataqueClaro = false;"), "«Dejar como de fabrica» la apaga");
            MethodInfo auto = TF.GetMethod("LocalAutomatico", T);
            object aj = auto.Invoke(null, new object[] { 8f, 5f, 10f }), aj2 = auto.Invoke(null, new object[] { -1f, -1f, 10f });
            Type tAj = aj.GetType();
            Check((bool)tAj.GetField("Ataque").GetValue(aj) && (bool)tAj.GetField("Ataque").GetValue(aj2), "el modo automatico del local la enciende (siempre, en el billar)");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK ataqueclaro: " + oks + " comprobaciones (tonos, ruido y rafagas sinteticos en Linux/Mono; no es oirlo en el bar)" : "  FALLO ataqueclaro: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}

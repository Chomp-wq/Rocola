using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// EL LOCAL Y SUS BOCINAS (Rocola/SalaLocal.cs, 12-10-2026). Con tonos sinteticos por la etapa de verdad:
//  1) Todo apagado: sale bit a bit lo que entra (ni toca la muestra).
//  2) Claridad: -2,5 dB a 300 Hz, +1,5 dB a 3 kHz; el bajo (60 Hz) y los agudos altos casi igual. Encenderla a media
//     cancion no da golpe.
//  3) Partes suaves: lo bajito sube hasta 4 dB, despacio; lo fuerte no sube; el silencio tampoco; al volver lo fuerte
//     se suelta deprisa; el volumen general (Master) no cuenta.
//  4) Balance: baja un canal lo que dice, nunca sube; con un canal no hace nada.
//  5) Disco continuo (hereda los filtros), sin desnormales en el silencio y sin reservar memoria al leer.
//  6) La prueba de bocinas: por donde dice, a -20 dB, la polaridad igual y al reves, sin recortar; solo con la rocola
//     callada; y se para.
//  7) La cadena, la configuracion y la ventana.
// No es escucharlo en el bar: eso solo se sabe alli.
class FuenteL : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteL(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}
class SalidaFalsa : IWavePlayer
{
    internal static int creadas, tocadas, soltadas;
    public SalidaFalsa() { creadas++; }
    public void Init(IWaveProvider p) { }
    public void Play() { tocadas++; }
    public void Stop() { }
    public void Pause() { }
    public float Volume { get { return 1f; } set { } }
    public PlaybackState PlaybackState { get { return PlaybackState.Playing; } }
    public event EventHandler<StoppedEventArgs> PlaybackStopped { add { } remove { } }
    public void Dispose() { soltadas++; }
}

class SalaLocalPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TSala, TPrueba, TModo, TTap;
    const int FS = 44100;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static ISampleProvider Sala(ISampleProvider s, object tap, bool cla, bool sua, int bal) { return (ISampleProvider)Nuevo(TSala, s, tap, 0.26f, cla, sua, bal); }
    static void Ajusta(object sala, bool cla, bool sua, int bal) { TSala.GetMethod("Ajusta", T).Invoke(sala, new object[] { cla, sua, bal }); }
    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    static float[] Tono(double hz, double seg, double amp, int ch)
    {
        int n = (int)(seg * FS); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++) { double v = amp * Math.Sin(2 * Math.PI * hz * i / FS); for (int c = 0; c < ch; c++) x[ch * i + c] = (float)v; }
        return x;
    }
    static float[] Ruido(double seg, int semilla)
    {
        Random r = new Random(semilla); int n = (int)(seg * FS); float[] x = new float[n * 2];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(0.3 * (r.NextDouble() * 2 - 1));
        return x;
    }
    // Eficaz de un canal entre dos tiempos (segundos).
    static double Ef(float[] y, int ch, int c, double desde, double hasta)
    {
        int a = (int)(desde * FS), b = Math.Min(y.Length / ch, (int)(hasta * FS)); double s = 0;
        for (int i = a; i < b; i++) s += (double)y[i * ch + c] * y[i * ch + c];
        return Math.Sqrt(s / Math.Max(1, b - a));
    }
    static double Db(double v) { return 20 * Math.Log10(Math.Max(v, 1e-15)); }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static double SaltoMax(float[] y, int ch, int c)
    {
        double m = 0; for (int i = ch + c; i < y.Length; i += ch) m = Math.Max(m, Math.Abs(y[i] - y[i - ch])); return m;
    }
    // La ganancia en dB de la etapa a 'hz' (tono estereo, ultimo medio segundo).
    static double Ganancia(double hz, bool cla, bool sua, int bal, int canal)
    {
        float[] x = Tono(hz, 1.5, 0.25, 2);
        float[] y = LeeTodo(Sala(new FuenteL(x, FS, 2), null, cla, sua, bal), 4096);
        return Db(Ef(y, 2, canal, 1.0, 1.5)) - Db(Ef(x, 2, canal, 1.0, 1.5));
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TSala = TF.GetNestedType("SalaLocal", T); TPrueba = TF.GetNestedType("PruebaBocinas", T); TModo = TF.GetNestedType("PruebaModo", T);
        TTap = TF.GetNestedType("LevelTap", T);
        Check(TSala != null && TPrueba != null && TModo != null && TTap != null, "estan la etapa del local y la prueba de bocinas");
        if (TSala == null || TPrueba == null) { Console.WriteLine("  FALLO salalocal: falta la etapa"); return 1; }

        Console.WriteLine("\n  1) TODO APAGADO\n");
        float[] rx = Ruido(2.0, 3);
        Check(Igual(LeeTodo(Sala(new FuenteL(rx, FS, 2), null, false, false, 0), 1000), rx), "estereo: sale bit a bit lo que entra");
        float[] rm = new float[FS]; Array.Copy(rx, rm, rm.Length);
        Check(Igual(LeeTodo(Sala(new FuenteL(rm, FS, 1), null, false, false, 0), 777), rm), "un canal: igual");
        float[] r6 = new float[6 * 4410]; Array.Copy(rx, r6, r6.Length);
        Check(Igual(LeeTodo(Sala(new FuenteL(r6, FS, 6), null, true, true, 4), 960), r6), "seis canales: no se toca aunque se pida (no sabe que bocina es cual)");

        {   // Lecturas de una sola muestra o impares: los canales no se cruzan (mismo resultado que de a cuadros).
            float[] x = Ruido(0.5, 21);
            float[] enteros = LeeTodo(Sala(new FuenteL(x, FS, 2), null, true, true, 3), 4096);
            ISampleProvider s = Sala(new FuenteL(x, FS, 2), null, true, true, 3);
            List<float> y = new List<float>(); float[] b = new float[777]; int n, k = 0;
            while ((n = s.Read(b, 0, (k++ % 3 == 0) ? 1 : (k % 2 == 0 ? 777 : 333))) > 0) for (int i = 0; i < n; i++) y.Add(b[i]);
            Check(Igual(enteros, y.ToArray()), "pedir de a una muestra o de a numeros impares da lo mismo (los canales no se cruzan)");
        }

        Console.WriteLine("\n  2) CLARIDAD PARA EL LOCAL\n");
        double g300 = Ganancia(300, true, false, 0, 0), g3k = Ganancia(3000, true, false, 0, 1), g60 = Ganancia(60, true, false, 0, 0), g12k = Ganancia(12000, true, false, 0, 0);
        Console.WriteLine("        60 Hz " + F(g60) + " dB, 300 Hz " + F(g300) + " dB, 3 kHz " + F(g3k) + " dB, 12 kHz " + F(g12k) + " dB");
        Check(Math.Abs(g300 + 2.5) < 0.15, "a 300 Hz baja 2,5 dB (el retumbe)");
        Check(Math.Abs(g3k - 1.5) < 0.15, "a 3 kHz sube 1,5 dB (la voz)");
        Check(Math.Abs(g60) < 0.5, "el bajo de 60 Hz casi no cambia");
        Check(Math.Abs(g12k) < 0.5, "los agudos altos casi no cambian");
        // Encenderla y apagarla a media cancion: sin golpe (el mayor salto entre muestras, como el de un tono limpio).
        {
            float[] x = Tono(300, 1.0, 0.5, 2);
            ISampleProvider s = Sala(new FuenteL(x, FS, 2), null, false, false, 0);
            float[] b = new float[2048]; List<float> y = new List<float>(); int n, k = 0;
            while ((n = s.Read(b, 0, b.Length)) > 0) { for (int i = 0; i < n; i++) y.Add(b[i]); k++; if (k == 5) Ajusta(s, true, false, 0); if (k == 12) Ajusta(s, false, false, 0); }
            float[] ya = y.ToArray();
            double tono = 0.5 * 2 * Math.PI * 300 / FS;
            Check(SaltoMax(ya, 2, 0) < tono * 1.05, "encender y apagar la claridad sonando no da golpe (salto " + F(SaltoMax(ya, 2, 0)) + " frente a " + F(tono) + ")");
            float[] fin = new float[FS / 4 * 2]; Array.Copy(ya, ya.Length - fin.Length, fin, 0, fin.Length);
            float[] xfin = new float[fin.Length]; Array.Copy(x, x.Length - fin.Length, xfin, 0, fin.Length);
            Check(Igual(fin, xfin), "apagada otra vez, vuelve a salir bit a bit");
        }

        Console.WriteLine("\n  3) QUE LAS PARTES SUAVES SE OIGAN\n");
        {
            object umbralSala = Sala(new FuenteL(new float[2], FS, 2), null, false, true, 0);
            double umbral = (double)TSala.GetProperty("UmbralDb", T).GetValue(umbralSala, null);
            Console.WriteLine("        umbral " + F(umbral) + " dB (12 dB por debajo del nivel objetivo)");
            // Un tono bajito (-34 dB eficaces): sube 4 dB, pero despacio (no a la primera).
            double ampBaja = Math.Sqrt(2) * Math.Pow(10, -34 / 20.0);
            float[] xb = Tono(1000, 8.0, ampBaja, 2);
            float[] yb = LeeTodo(Sala(new FuenteL(xb, FS, 2), null, false, true, 0), 4096);
            double sube1 = Db(Ef(yb, 2, 0, 0.0, 0.3)) - Db(Ef(xb, 2, 0, 0.0, 0.3)), sube8 = Db(Ef(yb, 2, 0, 7.0, 8.0)) - Db(Ef(xb, 2, 0, 7.0, 8.0));
            Console.WriteLine("        tono a -34 dB: al principio +" + F(sube1) + " dB, a los 8 s +" + F(sube8) + " dB");
            Check(Math.Abs(sube8 - 4.0) < 0.2, "lo bajito sube 4 dB (no mas)");
            Check(sube1 < 1.0, "y despacio: el primer tercio de segundo casi igual");
            // Lo fuerte (-14 dB) no sube.
            double ampFuerte = Math.Sqrt(2) * Math.Pow(10, -14 / 20.0);
            float[] xf = Tono(1000, 4.0, ampFuerte, 2);
            float[] yf = LeeTodo(Sala(new FuenteL(xf, FS, 2), null, false, true, 0), 4096);
            Check(Math.Abs(Db(Ef(yf, 2, 0, 3.0, 4.0)) - Db(Ef(xf, 2, 0, 3.0, 4.0))) < 0.05, "lo fuerte no sube");
            // El silencio y lo casi mudo (-62 dB) no suben: no se levanta el soplido.
            float[] xs = Tono(1000, 6.0, Math.Sqrt(2) * Math.Pow(10, -62 / 20.0), 2);
            float[] ys = LeeTodo(Sala(new FuenteL(xs, FS, 2), null, false, true, 0), 4096);
            Check(Math.Abs(Db(Ef(ys, 2, 0, 5.0, 6.0)) - Db(Ef(xs, 2, 0, 5.0, 6.0))) < 0.05, "lo casi mudo no sube");
            // Bajito y luego fuerte: la subida se suelta deprisa (en medio segundo casi nada).
            float[] xbf = new float[(int)(12.0 * FS) * 2];
            for (int i = 0; i < xbf.Length / 2; i++)
            {
                double a = i < 8 * FS ? ampBaja : ampFuerte;
                double v = a * Math.Sin(2 * Math.PI * 1000 * i / FS); xbf[2 * i] = (float)v; xbf[2 * i + 1] = (float)v;
            }
            float[] ybf = LeeTodo(Sala(new FuenteL(xbf, FS, 2), null, false, true, 0), 4096);
            double tras = Db(Ef(ybf, 2, 0, 8.5, 9.0)) - Db(Ef(xbf, 2, 0, 8.5, 9.0));
            Console.WriteLine("        al volver lo fuerte, medio segundo despues: +" + F(tras) + " dB");
            Check(tras < 0.5, "al volver lo fuerte, la subida se suelta deprisa");
            // El volumen general no cuenta: con el Master a la mitad (-6 dB) un tono a -40 dB es "bajito" como uno a -34.
            object tap = FormatterServices.GetUninitializedObject(TTap);
            TTap.GetField("Master", T).SetValue(tap, 0.5f);
            float[] xm = Tono(1000, 8.0, ampBaja * 0.5, 2);
            float[] ym = LeeTodo(Sala(new FuenteL(xm, FS, 2), tap, false, true, 0), 4096);
            double subeM = Db(Ef(ym, 2, 0, 7.0, 8.0)) - Db(Ef(xm, 2, 0, 7.0, 8.0));
            Check(Math.Abs(subeM - 4.0) < 0.2, "con el volumen general bajado sube lo mismo, no mas (+" + F(subeM) + " dB)");
            float[] xm2 = Tono(1000, 4.0, ampFuerte * 0.5, 2);
            float[] ym2 = LeeTodo(Sala(new FuenteL(xm2, FS, 2), tap, false, true, 0), 4096);
            Check(Math.Abs(Db(Ef(ym2, 2, 0, 3.0, 4.0)) - Db(Ef(xm2, 2, 0, 3.0, 4.0))) < 0.05, "y lo fuerte con el volumen bajado (modo noche) tampoco sube");
            // Apagarlo sonando: vuelve a 1 y deja de tocar la muestra.
            ISampleProvider s = Sala(new FuenteL(Tono(1000, 10.0, ampBaja, 2), FS, 2), null, false, true, 0);
            float[] bb = new float[4096]; int k = 0;
            while (s.Read(bb, 0, bb.Length) > 0) { k++; if (k == (int)(6.0 * FS * 2 / 4096)) Ajusta(s, false, false, 0); }
            Check((double)TSala.GetProperty("Subida", T).GetValue(s, null) == 1.0, "al apagarlo la subida vuelve a cero y la etapa se aquieta");
        }

        Console.WriteLine("\n  4) EL BALANCE\n");
        {
            double gl = Ganancia(1000, false, false, 3, 0), gr = Ganancia(1000, false, false, 3, 1);
            Check(Math.Abs(gl + 3.0) < 0.05 && Math.Abs(gr) < 0.01, "+3: la izquierda baja 3 dB y la derecha igual (" + F(gl) + " / " + F(gr) + ")");
            gl = Ganancia(1000, false, false, -6, 0); gr = Ganancia(1000, false, false, -6, 1);
            Check(Math.Abs(gr + 6.0) < 0.05 && Math.Abs(gl) < 0.01, "-6: la derecha baja 6 dB y la izquierda igual");
            gl = Ganancia(1000, false, false, 99, 0);
            Check(Math.Abs(gl + 6.0) < 0.05, "no pasa de 6 dB aunque se pida mas");
            float[] xm = Tono(1000, 1.0, 0.25, 1);
            Check(Igual(LeeTodo(Sala(new FuenteL(xm, FS, 1), null, false, false, 5), 1000), xm), "con un canal el balance no hace nada");
            float[] x = Tono(200, 1.0, 0.5, 2);
            ISampleProvider s = Sala(new FuenteL(x, FS, 2), null, false, false, 0);
            float[] b = new float[2048]; List<float> y = new List<float>(); int n, k = 0;
            while ((n = s.Read(b, 0, b.Length)) > 0) { for (int i = 0; i < n; i++) y.Add(b[i]); if (++k == 6) Ajusta(s, false, false, 6); }
            double tono = 0.5 * 2 * Math.PI * 200 / FS;
            Check(SaltoMax(y.ToArray(), 2, 0) < tono * 1.05, "mover el balance sonando no da golpe");
            double mx = 0; foreach (float v in LeeTodo(Sala(new FuenteL(Tono(1000, 1.0, 0.99, 2), FS, 2), null, false, false, -6), 4096)) mx = Math.Max(mx, Math.Abs(v));
            Check(mx <= 0.99 + 1e-6, "solo baja: nunca pasa de lo que entra");
        }

        Console.WriteLine("\n  5) DISCO CONTINUO, SILENCIO Y MEMORIA\n");
        {
            float[] a = Ruido(1.0, 11), b2 = Ruido(1.0, 12), ab = new float[a.Length + b2.Length];
            Array.Copy(a, ab, a.Length); Array.Copy(b2, 0, ab, a.Length, b2.Length);
            float[] junta = LeeTodo(Sala(new FuenteL(ab, FS, 2), null, true, true, 2), 4096);
            ISampleProvider sa = Sala(new FuenteL(a, FS, 2), null, true, true, 2), sb = Sala(new FuenteL(b2, FS, 2), null, true, true, 2);
            float[] ya = LeeTodo(sa, 4096);
            TSala.GetMethod("Sigue", T).Invoke(sb, new object[] { sa });
            float[] yb = LeeTodo(sb, 4096);
            float[] seguidas = new float[ya.Length + yb.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yb, 0, seguidas, ya.Length, yb.Length);
            Check(Igual(junta, seguidas), "dos pistas seguidas del mismo disco suenan como una sola (heredan filtros y medida)");
            float[] x = new float[(int)(3.0 * FS) * 2]; Array.Copy(Ruido(0.5, 4), x, (int)(0.5 * FS) * 2);
            ISampleProvider s = Sala(new FuenteL(x, FS, 2), null, true, true, 0);
            LeeTodo(s, 4096);
            double[] eq = (double[])TSala.GetField("eq", T).GetValue(s);
            bool ceros = true; foreach (double v in eq) if (v != 0.0) ceros = false;
            Check(ceros, "tras 2,5 s de silencio los filtros quedan en cero exacto (sin desnormales)");
            MethodInfo asignado = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (asignado != null)
            {
                ISampleProvider sm = Sala(new FuenteL(Ruido(3.0, 5), FS, 2), null, true, true, 3);
                float[] bm = new float[4096]; sm.Read(bm, 0, bm.Length);
                long antes = (long)asignado.Invoke(null, null);
                for (int i = 0; i < 20; i++) sm.Read(bm, 0, bm.Length);
                long despues = (long)asignado.Invoke(null, null);
                Check(despues - antes < 1024, "leer no reserva memoria (" + (despues - antes) + " bytes en 20 lecturas)");
            }
            else Console.WriteLine("        (este Mono no cuenta la memoria reservada por hilo)");
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            ISampleProvider sc = Sala(new FuenteL(Ruido(10.0, 6), FS, 2), null, true, true, 3);
            LeeTodo(sc, 4410);
            sw.Stop();
            Console.WriteLine("        con todo encendido: " + F(sw.Elapsed.TotalMilliseconds / 10.0) + " ms por segundo de musica (este equipo, con la copia de la prueba)");
            Check(sw.Elapsed.TotalMilliseconds / 10.0 < 25.0, "cuesta poco (menos de 25 ms por segundo, incluida la copia de la prueba)");
        }

        Console.WriteLine("\n  6) LA PRUEBA DE BOCINAS\n");
        {
            string[] modos = { "Izquierda", "Derecha", "Ambas", "Polaridad" };
            foreach (string m in modos)
            {
                ISampleProvider p = (ISampleProvider)Nuevo(TPrueba, Enum.Parse(TModo, m));
                float[] y = LeeTodo(p, 4410);
                double seg = y.Length / 2.0 / FS, l = Db(Ef(y, 2, 0, 0, seg)), r = Db(Ef(y, 2, 1, 0, seg));
                double mx = 0; foreach (float v in y) mx = Math.Max(mx, Math.Abs(v));
                Console.WriteLine("        " + m + ": " + F(seg) + " s, izquierda " + F(l) + " dB, derecha " + F(r) + " dB, pico " + F(Db(mx)) + " dB");
                Check(mx < 0.7, m + ": sin recortar (pico bajo -3 dB)");
                Check(Math.Abs(y[0]) < 1e-3 && Math.Abs(y[y.Length - 1]) < 1e-3 && Math.Abs(y[y.Length - 2]) < 1e-3, m + ": empieza y acaba en cero (sin chasquido)");
                if (m == "Izquierda") Check(Math.Abs(l + 20) < 1.0 && r < -200, "solo izquierda: a -20 dB por la izquierda y nada por la derecha");
                if (m == "Derecha") Check(Math.Abs(r + 20) < 1.0 && l < -200, "solo derecha: a -20 dB por la derecha y nada por la izquierda");
                if (m == "Ambas") { bool igual = true; for (int i = 0; i < y.Length; i += 2) if (y[i] != y[i + 1]) igual = false; Check(igual && Math.Abs(l + 20) < 1.0, "las dos: lo mismo por las dos, a -20 dB"); }
                if (m == "Polaridad")
                {
                    Check(Math.Abs(seg - 8.0) < 0.01, "polaridad: 8 s (igual, al reves, igual, al reves)");
                    bool ok = true;
                    for (int t = 0; t < 4; t++)
                    {
                        int a = (int)((t * 2.0 + 0.5) * FS), b = (int)((t * 2.0 + 1.5) * FS);
                        double signo = (t & 1) == 1 ? -1 : 1;
                        for (int i = a; i < b; i++) if (Math.Abs(y[2 * i + 1] - signo * y[2 * i]) > 1e-6) { ok = false; break; }
                    }
                    Check(ok, "polaridad: en el medio de cada tramo, igual o justo al reves");
                    double cruce = Ef(y, 2, 0, 1.999, 2.001);
                    Check(cruce < 0.01, "polaridad: al cambiar pasa por cero (sin golpe)");
                    Check(Math.Abs(l + 20) < 1.5, "polaridad: a -20 dB");
                }
            }
            {   // El letrero de la polaridad dice lo que SE OYE: lo leido va por delante lo que guarda la salida.
                ISampleProvider pp = (ISampleProvider)Nuevo(TPrueba, Enum.Parse(TModo, "Polaridad"));
                float[] bf = new float[(int)(2.5 * FS) * 2]; pp.Read(bf, 0, bf.Length);
                MethodInfo ar = TPrueba.GetMethod("AlReves", T);
                Check((bool)ar.Invoke(pp, new object[] { 0 }) && !(bool)ar.Invoke(pp, new object[] { 1000 }),
                      "leidos 2,5 s: sin retraso ya es AL REVES; con el segundo de la salida, aun suena IGUAL");
            }
            // ProbarBocinas: solo con la rocola callada, con su propia salida, y se para.
            object f = Codigo.FormSinEncender(TF);
            TF.GetField("fabricaSalidaClasica", T).SetValue(f, (Func<IWavePlayer>)delegate { return new SalidaFalsa(); });
            MethodInfo probar = TF.GetMethod("ProbarBocinas", T), parar = TF.GetMethod("ParaPruebaBocinas", T), estado = TF.GetMethod("EstadoPrueba", T);
            object np = FormatterServices.GetUninitializedObject(TF.GetNestedType("PlayItem", T));
            TF.GetField("nowPlaying", T).SetValue(f, np);
            string r1 = (string)probar.Invoke(f, new object[] { Enum.Parse(TModo, "Izquierda") });
            Check(r1.StartsWith("Hay musica") && SalidaFalsa.creadas == 0, "con musica sonando no suena la prueba (lo dice)");
            TF.GetField("nowPlaying", T).SetValue(f, null);
            string r2 = (string)probar.Invoke(f, new object[] { Enum.Parse(TModo, "Polaridad") });
            Check(r2.StartsWith("Sonando") && SalidaFalsa.creadas == 1 && SalidaFalsa.tocadas == 1, "con la rocola callada suena, por su propia salida");
            Check(TF.GetField("output", T).GetValue(f) == null, "la salida de la musica ni se toca");
            Check(((string)estado.Invoke(f, null)).StartsWith("Polaridad"), "la ventana dice que esta sonando");
            probar.Invoke(f, new object[] { Enum.Parse(TModo, "Derecha") });
            Check(SalidaFalsa.creadas == 2 && SalidaFalsa.soltadas == 1, "pedir otra para la que sonaba");
            parar.Invoke(f, null);
            Check(SalidaFalsa.soltadas == 2 && (string)estado.Invoke(f, null) == "", "y al cerrar la ventana se para");
        }

        Console.WriteLine("\n  7) LA CADENA, LA CONFIGURACION Y LA VENTANA\n");
        {
            MethodBase arma = TF.GetMethod("ArmaCadena", T);
            List<string> ll = Codigo.Llamadas(arma);
            int iMono = ll.IndexOf("GraveMono..ctor"), iSala = ll.IndexOf("SalaLocal..ctor"), iBajo = ll.IndexOf("DeepBassProvider..ctor"), iLim = ll.IndexOf("LimitadorPicoReal..ctor");
            Check(iMono >= 0 && iSala > iMono && iBajo > iSala && iLim > iBajo, "va detras del grave en mono y delante del grave sintetico y del limitador");
            List<string> aplica = Codigo.Llamadas(TF.GetMethod("AplicaBocinas", T));
            Check(Codigo.Llama(aplica, "SalaLocal.Ajusta"), "los mandos se oyen al instante en la que suena y en la siguiente");
            Check(Codigo.Llama(TF.GetMethod("SigueA", T), "SalaLocal.Sigue"), "el disco continuo enlaza la etapa del local");
            List<string> tl = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            foreach (string k in new string[] { "claridad_local=", "partes_suaves=", "balance=" })
                Check(tl.Contains(k) && ts.Contains(k), "la configuracion lee y guarda " + k);
            Check(!(bool)TF.GetField("claridadLocal", T).GetValue(null) && !(bool)TF.GetField("partesSuaves", T).GetValue(null) && (int)TF.GetField("balanceDb", T).GetValue(null) == 0,
                  "de fabrica: claridad y partes suaves apagadas, balance centrado");
            MethodInfo bd = TF.GetMethod("BalanceDb", T);
            Check((int)bd.Invoke(null, new object[] { "4" }) == 4 && (int)bd.Invoke(null, new object[] { "-9" }) == -6 && (int)bd.Invoke(null, new object[] { "x" }) == 0, "balance= se lee acotado (-6..6) y lo raro se ignora");
            List<string> ventana = new List<string>();
            foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "VentanaAjustes")) ventana.AddRange(Codigo.Textos(m));
            foreach (string s in new string[] { "El local y sus bocinas...", "Claridad para el local", "Que las partes suaves se oigan", "Solo izquierda", "Solo derecha", "Las dos", "Polaridad", "Volver a las canciones" })
                Check(ventana.Contains(s), "en Ctrl+F3: «" + s + "»");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK salalocal: " + oks + " comprobaciones (tonos sinteticos; no es oirlo en el bar)" : "  FALLO salalocal: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}

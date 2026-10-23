using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// PRESENCIA EN SALA RUIDOSA (Rocola/PresenciaSala.cs, 22-10-2026): el compresor suave detras del local. Con tonos y
// ruido sinteticos por la etapa de verdad:
//  1) Apagado (de fabrica): sale bit a bit lo que entra, con uno, dos y seis canales, y no mira el volumen general.
//  2) Lo que hace: lo bajito sube (hasta 3 dB), lo fuerte se aprieta 2 a 1 y se le devuelve la ganancia, el casi mudo no
//     sube, y el volumen general (Master, modo noche) no cambia el umbral.
//  3) Tiempos: ataque de unos 15 ms, suelta de unos 250 ms.
//  4) Enlazado: una sola ganancia para los dos canales (la imagen no se mueve).
//  5) El techo del limitador: la compensacion nunca lleva el pico mas alla de TechoPicoReal.
//  6) Sin chasquidos al encender y apagar sonando; al apagar vuelve a bit a bit.
//  7) Disco continuo, lecturas de cualquier tamano, sin reservar memoria, cuanto cuesta.
//  8) La cadena, la configuracion, la ventana, y que no toca el volumen de Windows.
// No es escucharlo en el bar: eso solo se sabe alli.
class FuentePS : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuentePS(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class PresenciaSalaPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TPre, TTap;
    const int FS = 44100;
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static object TapMaster(float master)
    {
        object tap = FormatterServices.GetUninitializedObject(TTap);
        TTap.GetField("Master", T).SetValue(tap, master);
        return tap;
    }
    static ISampleProvider Pre(ISampleProvider s, bool on, float master) { return (ISampleProvider)Nuevo(TPre, s, TapMaster(master), 0.26f, on); }
    static ISampleProvider Pre(ISampleProvider s, bool on) { return Pre(s, on, 1f); }
    static void Ajusta(object p, bool on) { TPre.GetMethod("Ajusta", T).Invoke(p, new object[] { on }); }
    static double Prop(object p, string n) { return (double)TPre.GetProperty(n, T).GetValue(p, null); }
    static float[] LeeTodo(ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true) { int n = p.Read(buf, 0, bloque); if (n <= 0) break; for (int i = 0; i < n; i++) r.Add(buf[i]); }
        return r.ToArray();
    }
    // Tono con eficaz en dB (por canal), estereo igual por los dos o con otro nivel por la derecha.
    static float[] Tono(double hz, double seg, double rmsDb, int ch) { return Tono(hz, seg, rmsDb, rmsDb, ch); }
    static float[] Tono(double hz, double seg, double dbIzq, double dbDer, int ch)
    {
        int n = (int)(seg * FS); float[] x = new float[n * ch];
        double al = Math.Sqrt(2) * Math.Pow(10, dbIzq / 20.0), ar = Math.Sqrt(2) * Math.Pow(10, dbDer / 20.0);
        for (int i = 0; i < n; i++)
        {
            double v = Math.Sin(2 * Math.PI * hz * i / FS);
            x[ch * i] = (float)(al * v);
            if (ch > 1) x[ch * i + 1] = (float)(ar * v);
        }
        return x;
    }
    static float[] Ruido(double seg, int semilla, double amp)
    {
        Random r = new Random(semilla); int n = (int)(seg * FS); float[] x = new float[n * 2];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(amp * (r.NextDouble() * 2 - 1));
        return x;
    }
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
        for (int i = 0; i < a.Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(a[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0)) return false;
        return true;
    }
    static double SaltoMax(float[] y, int ch, int c)
    {
        double m = 0; for (int i = ch + c; i < y.Length; i += ch) m = Math.Max(m, Math.Abs(y[i] - y[i - ch])); return m;
    }
    // Ganancia (dB) de un tono estable a rmsDb: ultimos 0,5 s de 4 s.
    static double Gan(double rmsDb, float master)
    {
        float[] x = Tono(1000, 4.0, rmsDb, 2);   // rmsDb: el nivel sin el volumen general; lo que llega es ese por el Master
        for (int i = 0; i < x.Length; i++) x[i] *= master;
        float[] y = LeeTodo(Pre(new FuentePS(x, FS, 2), true, master), 4096);
        return Db(Ef(y, 2, 0, 3.5, 4.0)) - Db(Ef(x, 2, 0, 3.5, 4.0));
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TPre = TF.GetNestedType("PresenciaSala", T); TTap = TF.GetNestedType("LevelTap", T);
        Check(TPre != null && TTap != null, "esta la etapa de la presencia en sala ruidosa");
        if (TPre == null) { Console.WriteLine("  FALLO presenciasala: falta la etapa"); return 1; }
        float techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);

        Console.WriteLine("\n  1) APAGADO: NI TOCA LA MUESTRA\n");
        float[] rx = Ruido(2.0, 3, 0.3);
        Check(Igual(LeeTodo(Pre(new FuentePS(rx, FS, 2), false), 1000), rx), "estereo: sale bit a bit lo que entra");
        float[] rm = new float[FS]; Array.Copy(rx, rm, rm.Length);
        Check(Igual(LeeTodo(Pre(new FuentePS(rm, FS, 1), false), 777), rm), "un canal: igual");
        float[] r6 = new float[6 * 4410]; Array.Copy(rx, r6, r6.Length);
        Check(Igual(LeeTodo(Pre(new FuentePS(r6, FS, 6), true), 960), r6), "seis canales: no se toca aunque este encendido (no sabe que bocina es cual)");
        float[] r8k = new float[8000]; Array.Copy(rx, r8k, r8k.Length);
        Check(Igual(LeeTodo((ISampleProvider)Nuevo(TPre, new FuentePS(r8k, 4000, 2), null, 0.26f, true), 500), r8k), "a menos de 8 kHz tampoco se toca");
        Check(!(bool)TF.GetField("presenciaSala", T).GetValue(null), "de fabrica esta apagada");
        {   // Encendida y vuelta a apagar: al llegar a 1 vuelve a salir bit a bit, y el Master sin tap no rompe nada.
            ISampleProvider s = (ISampleProvider)Nuevo(TPre, new FuentePS(rx, FS, 2), null, 0.26f, true);
            float[] b = new float[2048]; int n = s.Read(b, 0, b.Length);
            Check(n == 2048, "con el tap en null (sin volumen general) tambien lee");
        }

        Console.WriteLine("\n  2) LO QUE HACE\n");
        {
            object tmp = Pre(new FuentePS(new float[2], FS, 2), true);
            double umbral = Prop(tmp, "UmbralDb");
            Console.WriteLine("        umbral " + F(umbral) + " dB (6 dB por debajo del volumen objetivo)");
            Check(Math.Abs(umbral - (20 * Math.Log10(0.26) - 6)) < 0.001, "el umbral esta 6 dB por debajo del volumen objetivo del nivelado");
            double bajo = Gan(-40, 1f), medio = Gan(-17.7, 1f), fuerte = Gan(-8, 1f), muyFuerte = Gan(-3, 1f), casiMudo = Gan(-62, 1f);
            Console.WriteLine("        -40 dB: " + F(bajo) + " dB;  -17,7 dB (el umbral): " + F(medio) + ";  -8 dB: " + F(fuerte) + ";  -3 dB: " + F(muyFuerte) + ";  -62 dB: " + F(casiMudo));
            Check(Math.Abs(bajo - 3.0) < 0.2, "lo bajito sube 3 dB (la compensacion), sin pasar de ahi");
            Check(Math.Abs(fuerte - (3.0 - (-8 - umbral) / 2.0)) < 0.35, "lo fuerte se aprieta 2 a 1 por encima del umbral y se le devuelve la compensacion (" + F(fuerte) + " dB)");
            Check(muyFuerte < fuerte && muyFuerte > -5.0, "y mas fuerte aun, se aprieta mas (" + F(muyFuerte) + " dB), pero con calma");
            Check(bajo - fuerte > 3.5, "la distancia entre lo bajito y lo fuerte se acorta " + F(bajo - fuerte) + " dB");
            Check(Math.Abs(casiMudo) < 0.05, "lo casi mudo (-62 dB) no sube: no se levanta el soplido");
            Check(medio > 2.2 && medio < 3.0, "en el umbral (rodilla suave) casi toda la compensacion y casi nada de reduccion (" + F(medio) + " dB)");
            // El volumen general no cuenta: con el Master a 0,5 un tono a -8 dB por el Master se comporta como uno a -8 dB sin Master.
            double conMaster = Gan(-8, 0.5f);
            Check(Math.Abs(conMaster - fuerte) < 0.3, "con el volumen general (Master) a la mitad, lo mismo que sin el (" + F(conMaster) + " frente a " + F(fuerte) + ")");
            double bajoMaster = Gan(-40, 0.5f);
            Check(Math.Abs(bajoMaster - bajo) < 0.3, "y lo bajito igual (" + F(bajoMaster) + ")");
        }

        Console.WriteLine("\n  3) LOS TIEMPOS\n");
        {   // Bajito 2 s y de golpe fuerte (-8 dB): la ganancia baja con ~15 ms de ataque; al volver a bajito, sube con ~250 ms.
            double rmsBajo = -40, rmsFuerte = -8;
            int n1 = (int)(2.0 * FS), n2 = (int)(2.0 * FS), n3 = (int)(3.0 * FS);
            float[] x = new float[(n1 + n2 + n3) * 2];
            double ab = Math.Sqrt(2) * Math.Pow(10, rmsBajo / 20), af = Math.Sqrt(2) * Math.Pow(10, rmsFuerte / 20);
            for (int i = 0; i < n1 + n2 + n3; i++)
            {
                double a = (i >= n1 && i < n1 + n2) ? af : ab, v = a * Math.Sin(2 * Math.PI * 1000 * i / FS);
                x[2 * i] = (float)v; x[2 * i + 1] = (float)v;
            }
            float[] y = LeeTodo(Pre(new FuentePS(x, FS, 2), true), 4096);
            // Ganancia en ventanas de 4 ms (4 ciclos de 1 kHz), en dB.
            Func<double, double> gan = delegate (double t) { return Db(Ef(y, 2, 0, t, t + 0.004)) - Db(Ef(x, 2, 0, t, t + 0.004)); };
            double ini = gan(1.9), fin = gan(3.9);
            double objetivo = fin - ini;   // lo que baja del todo
            double a5 = gan(2.0 + 0.005) - ini, a15 = gan(2.0 + 0.015) - ini, a80 = gan(2.0 + 0.080) - ini;
            Console.WriteLine("        al subir a -8 dB: la ganancia baja " + F(-(fin - ini)) + " dB del todo; a los 5 ms " + F(-a5) + ", a los 15 ms " + F(-a15) + ", a los 80 ms " + F(-a80));
            Check(a5 / objetivo < 0.55, "ataque suave: a los 5 ms aun no ha bajado ni la mitad (" + F(100 * a5 / objetivo) + " %)");
            Check(a15 / objetivo > 0.4 && a15 / objetivo < 0.9, "a los 15 ms lleva una buena parte (" + F(100 * a15 / objetivo) + " %), la del ataque");
            Check(a80 / objetivo > 0.9, "y a los 80 ms ya esta casi del todo (" + F(100 * a80 / objetivo) + " %)");
            double v0 = gan(3.9);
            double r50 = gan(4.0 + 0.05), r250 = gan(4.0 + 0.25), r1500 = gan(4.0 + 1.5), fin2 = gan(6.9);
            double sube = fin2 - v0;
            Console.WriteLine("        al volver a bajito: sube " + F(sube) + " dB del todo; a los 50 ms " + F(r50 - v0) + ", a los 250 ms " + F(r250 - v0) + ", a los 1,5 s " + F(r1500 - v0));
            Check((r50 - v0) / sube < 0.35, "suelta lenta: a los 50 ms ha vuelto menos de un tercio (" + F(100 * (r50 - v0) / sube) + " %)");
            Check((r250 - v0) / sube > 0.45 && (r250 - v0) / sube < 0.85, "a los 250 ms lleva ~63 % (" + F(100 * (r250 - v0) / sube) + " %)");
            Check((r1500 - v0) / sube > 0.95, "y a los 1,5 s ya volvio (" + F(100 * (r1500 - v0) / sube) + " %)");
        }

        Console.WriteLine("\n  4) LOS DOS CANALES LLEVAN LA MISMA GANANCIA\n");
        {
            float[] x = Tono(1000, 4.0, -8, -45, 2);   // la izquierda fuerte, la derecha bajita
            float[] y = LeeTodo(Pre(new FuentePS(x, FS, 2), true), 4096);
            double gi = Db(Ef(y, 2, 0, 3.5, 4.0)) - Db(Ef(x, 2, 0, 3.5, 4.0)), gd = Db(Ef(y, 2, 1, 3.5, 4.0)) - Db(Ef(x, 2, 1, 3.5, 4.0));
            Console.WriteLine("        izquierda -8 dB: " + F(gi) + " dB; derecha -45 dB: " + F(gd) + " dB");
            Check(Math.Abs(gi - gd) < 0.001, "los dos canales llevan la misma ganancia (la imagen no se mueve)");
            Check(gi < 0.5, "y la fuerte manda: la izquierda a -8 dB aprieta (" + F(gi) + " dB)");
        }

        Console.WriteLine("\n  5) NUNCA PASA DEL TECHO DEL LIMITADOR\n");
        {   // Pulsos cortos de 0,8 sobre un fondo bajito: poco eficaz y mucho pico. La compensacion no puede subirlos mas de lo que cabe.
            int n = 6 * FS; float[] x = new float[n * 2];
            Random r = new Random(5);
            for (int i = 0; i < n; i++)
            {
                double v = 0.012 * (r.NextDouble() * 2 - 1);   // fondo a ~-45 dB
                if (i % (FS / 5) == 100) v = 0.80;               // un pico cada 200 ms
                x[2 * i] = (float)v; x[2 * i + 1] = (float)v;
            }
            float[] y = LeeTodo(Pre(new FuentePS(x, FS, 2), true), 4096);
            double mx = 0, mxIn = 0;
            for (int i = 0; i < y.Length; i++) { mx = Math.Max(mx, Math.Abs(y[i])); mxIn = Math.Max(mxIn, Math.Abs(x[i])); }
            Console.WriteLine("        pico de entrada " + F(mxIn) + ", de salida " + F(mx) + " (techo " + F(techo) + ")");
            Check(mx <= techo, "con picos de 0,8 y un fondo bajo, la salida no pasa del techo (" + F(mx) + " <= " + F(techo) + ")");
            // Y con un fondo que pide los 3 dB enteros y picos bajos, si los sube.
            float[] x2 = Tono(1000, 4.0, -40, 2);
            float[] y2 = LeeTodo(Pre(new FuentePS(x2, FS, 2), true), 4096);
            Check(Db(Ef(y2, 2, 0, 3.5, 4.0)) - Db(Ef(x2, 2, 0, 3.5, 4.0)) > 2.7, "con picos bajos, la compensacion llega a sus 3 dB");
            // Una cancion fuerte de verdad (pico 0,85, eficaz alto): la salida queda por debajo de lo que entra.
            float[] x3 = Tono(200, 4.0, -6, 2);
            float[] y3 = LeeTodo(Pre(new FuentePS(x3, FS, 2), true), 4096);
            double m3 = 0; for (int i = (int)(1.5 * FS) * 2; i < y3.Length; i++) m3 = Math.Max(m3, Math.Abs(y3[i]));
            double m3in = 0; for (int i = 0; i < x3.Length; i++) m3in = Math.Max(m3in, Math.Abs(x3[i]));
            Check(m3 < m3in, "una cancion fuerte sale mas baja de pico, no mas alta (" + F(m3) + " frente a " + F(m3in) + ")");
        }

        Console.WriteLine("\n  6) SIN CHASQUIDOS AL ENCENDER Y APAGAR\n");
        {
            float[] x = Tono(300, 5.0, -10, 2);
            ISampleProvider s = Pre(new FuentePS(x, FS, 2), false);
            float[] b = new float[2048]; List<float> y = new List<float>(); int n, k = 0;
            while ((n = s.Read(b, 0, b.Length)) > 0)
            {
                for (int i = 0; i < n; i++) y.Add(b[i]); k++;
                if (k == 10) Ajusta(s, true);
                if (k == 70) Ajusta(s, false);
            }
            float[] ya = y.ToArray();
            double tono = Math.Sqrt(2) * Math.Pow(10, -10 / 20.0) * 2 * Math.PI * 300 / FS;
            double salto = Math.Max(SaltoMax(ya, 2, 0), SaltoMax(ya, 2, 1));
            Check(salto < tono * 1.1, "encender y apagar sonando no da golpe (salto " + F(salto) + " frente a " + F(tono) + ")");
            float[] fin = new float[FS / 4 * 2]; Array.Copy(ya, ya.Length - fin.Length, fin, 0, fin.Length);
            float[] xfin = new float[fin.Length]; Array.Copy(x, x.Length - fin.Length, xfin, 0, fin.Length);
            Check(Igual(fin, xfin), "apagada otra vez, en unos 40 ms vuelve a salir bit a bit");
            Check(Prop(s, "GananciaAhora") == 1.0, "y su ganancia es 1 exacto");
            // La subida inicial sin saltos tambien con un tono bajito (la compensacion entra despacio).
            float[] xq = Tono(300, 3.0, -40, 2);
            ISampleProvider sq = Pre(new FuentePS(xq, FS, 2), true);
            float[] yq = LeeTodo(sq, 2048);
            double tq = Math.Sqrt(2) * Math.Pow(10, -40 / 20.0) * 2 * Math.PI * 300 / FS * 1.5;
            Check(SaltoMax(yq, 2, 0) < tq, "encendida desde el principio con una intro bajita, la compensacion entra sin golpes");
        }

        // Activar despues de leer SOLO el izquierdo en bypass: la ganancia sigue
        // perteneciendo al cuadro real, no al par formado por derecha + izquierda siguiente.
        foreach (int fs in new int[] { 8000, 44100, 48000, 96000 })
        {
            float[] x = new float[fs * 2];
            for (int i = 0; i < x.Length; i++) x[i] = 0.2f;
            ISampleProvider p = Pre(new FuentePS(x, fs, 2), false);
            float[] y = new float[x.Length];
            p.Read(y, 0, 1);
            Ajusta(p, true);
            int leidos = 1;
            while (leidos < y.Length)
            {
                int n = p.Read(y, leidos, Math.Min(777, y.Length - leidos));
                if (n <= 0) break;
                leidos += n;
            }
            bool iguales = leidos == y.Length;
            for (int i = 0; i < leidos - 1; i += 2) iguales &= y[i] == y[i + 1];
            Check(iguales, "activar tras lectura impar conserva ganancia estereo a " + fs + " Hz");
        }

        Console.WriteLine("\n  7) DISCO CONTINUO, LECTURAS Y COSTE\n");
        {
            float[] a = Ruido(1.0, 11, 0.2), b2 = Ruido(1.0, 12, 0.05), ab = new float[a.Length + b2.Length];
            Array.Copy(a, ab, a.Length); Array.Copy(b2, 0, ab, a.Length, b2.Length);
            float[] junta = LeeTodo(Pre(new FuentePS(ab, FS, 2), true), 4096);
            ISampleProvider sa = Pre(new FuentePS(a, FS, 2), true), sb = Pre(new FuentePS(b2, FS, 2), true);
            float[] ya = LeeTodo(sa, 4096);
            TPre.GetMethod("Sigue", T).Invoke(sb, new object[] { sa });
            float[] yb = LeeTodo(sb, 4096);
            float[] seguidas = new float[ya.Length + yb.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yb, 0, seguidas, ya.Length, yb.Length);
            Check(Igual(junta, seguidas), "dos pistas seguidas del mismo disco suenan como una sola (heredan ganancia y medida)");
            // Cualquier tamano de lectura da lo mismo, bit a bit.
            float[] x = Ruido(1.0, 21, 0.25);
            float[] enteros = LeeTodo(Pre(new FuentePS(x, FS, 2), true), 4096);
            ISampleProvider s = Pre(new FuentePS(x, FS, 2), true);
            List<float> yy = new List<float>(); float[] buf = new float[777]; int n, k = 0;
            while ((n = s.Read(buf, 0, (k++ % 3 == 0) ? 1 : (k % 2 == 0 ? 777 : 333))) > 0) for (int i = 0; i < n; i++) yy.Add(buf[i]);
            Check(Igual(enteros, yy.ToArray()), "pedir de a una muestra o de a numeros impares da lo mismo, bit a bit");
            float[] xm = Tono(500, 1.0, -5, 1);
            float[] ym1 = LeeTodo(Pre(new FuentePS(xm, FS, 1), true), 4096), ym2 = LeeTodo(Pre(new FuentePS(xm, FS, 1), true), 333);
            double gm = Db(Ef(ym1, 1, 0, 0.5, 1.0)) - Db(Ef(xm, 1, 0, 0.5, 1.0));
            Check(Igual(ym1, ym2) && Math.Abs(gm) > 0.5, "con un canal tambien actua (" + F(gm) + " dB) y da lo mismo con cualquier lectura");
            MethodInfo asignado = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (asignado != null)
            {
                ISampleProvider sm = Pre(new FuentePS(Ruido(3.0, 5, 0.25), FS, 2), true);
                float[] bm = new float[4096]; sm.Read(bm, 0, bm.Length);
                long antes = (long)asignado.Invoke(null, null);
                for (int i = 0; i < 20; i++) sm.Read(bm, 0, bm.Length);
                long despues = (long)asignado.Invoke(null, null);
                Check(despues - antes < 1024, "leer no reserva memoria (" + (despues - antes) + " bytes en 20 lecturas)");
            }
            else Console.WriteLine("        (este Mono no cuenta la memoria reservada por hilo)");
            // El silencio exacto no deja restos y el coste: encendida contra apagada con la misma copia.
            float[] musica = Ruido(10.0, 6, 0.25);
            double mejorOn = 1e9, mejorOff = 1e9;
            for (int rep = 0; rep < 3; rep++)
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                LeeTodo(Pre(new FuentePS(musica, FS, 2), true), 4410);
                sw.Stop(); mejorOn = Math.Min(mejorOn, sw.Elapsed.TotalMilliseconds / 10.0);
                sw = System.Diagnostics.Stopwatch.StartNew();
                LeeTodo(Pre(new FuentePS(musica, FS, 2), false), 4410);
                sw.Stop(); mejorOff = Math.Min(mejorOff, sw.Elapsed.TotalMilliseconds / 10.0);
            }
            Console.WriteLine("        encendida: " + F(mejorOn) + " ms por segundo de musica; apagada (solo la copia de la prueba): " + F(mejorOff) + " ms; la etapa cuesta " + F(mejorOn - mejorOff) + " ms/s");
            Check(mejorOn - mejorOff < 6.0, "cuesta poco (menos de 6 ms por segundo de musica en este equipo)");
        }

        Console.WriteLine("\n  8) LA CADENA, LA CONFIGURACION Y LA VENTANA\n");
        {
            MethodBase arma = TF.GetMethod("ArmaCadena", T);
            List<string> ll = Codigo.Llamadas(arma);
            int iSala = ll.IndexOf("SalaLocal..ctor"), iPre = ll.IndexOf("PresenciaSala..ctor"), iBajo = ll.IndexOf("DeepBassProvider..ctor"), iLim = ll.IndexOf("LimitadorPicoReal..ctor");
            Check(iSala >= 0 && iPre > iSala && iBajo > iPre && iLim > iBajo, "va detras del local y delante del grave sintetico y del limitador");
            List<string> aplica = Codigo.Llamadas(TF.GetMethod("AplicaBocinas", T));
            Check(Codigo.Llama(aplica, "PresenciaSala.Ajusta"), "el interruptor se oye al instante en la que suena y en la siguiente");
            Check(Codigo.Llama(TF.GetMethod("SigueA", T), "PresenciaSala.Sigue"), "el disco continuo enlaza la etapa");
            Check(Codigo.Llama(TF.GetMethod("AplicaSinMejoras", T), "PresenciaSala.Ajusta"), "«Escuchar sin mejoras» la apaga mientras dura");
            List<string> tl = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            Check(tl.Contains("presencia_sala=") && ts.Contains("presencia_sala="), "la configuracion lee y guarda presencia_sala=");
            // Nunca toca el volumen de Windows ni el de la salida: la etapa no llama a nada de volumen.
            bool vol = false;
            foreach (MethodInfo m in TPre.GetMethods(T | BindingFlags.DeclaredOnly))
                foreach (string c in Codigo.Llamadas(m)) if (c.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("waveOutSet", StringComparison.OrdinalIgnoreCase) >= 0 || c.IndexOf("Mixer", StringComparison.OrdinalIgnoreCase) >= 0) vol = true;
            Check(!vol, "la etapa no llama a nada de volumen (ni el de Windows ni el de la salida)");
            string fuenteF3 = Codigo.FuenteDeForm1();
            Check(fuenteF3.Contains("swPre.Checked = false;"), "«Dejar como de fabrica» la apaga");
            Check(fuenteF3.Contains("presenciaSala = bkPre;"), "y al Cancelar por un fallo se restaura el valor de antes");
            List<string> ventana = new List<string>();
            foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "VentanaAjustes")) ventana.AddRange(Codigo.Textos(m));
            foreach (string s in new string[] { "Presencia en sala ruidosa", "Volver al local y sus bocinas", "Tono y presencia del local..." })
                Check(ventana.Contains(s), "en Ctrl+F3: «" + s + "»");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK presenciasala: " + oks + " comprobaciones (tonos sinteticos; no es oirlo en el bar)" : "  FALLO presenciasala: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}

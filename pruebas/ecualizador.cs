using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL ECUALIZADOR DEL DUENO (17-11-2026; EcualizadorDueno.cs y Form1.EcualizadorDueno.cs).
//
// Diez bandas (31 Hz estante, 63 a 8 kHz campanas de una octava, 16 kHz estante), de -12 a +12 dB a pasos de 0,5 dB, entre el bombo y el limitador, con
// preganancia automatica para no raspar, una curva para el sonido normal y otra para el modo billar.
//
// LO QUE ESTA BATERIA VIGILA:
//  1) TODO EN 0 PASA TAL CUAL, bit a bit (estereo, mono y mas canales), y la etapa esta en la cadena entre el bombo y el limitador.
//  2) +6 dB EN 63 Hz: la curva sube 6 dB ahi y casi nada en 1 kHz; con la preganancia el tono de 63 Hz sale en 0 dB y el de 1 kHz en -6.
//  3) -6 dB EN 1 kHz: baja 6 dB y la preganancia es 0.
//  4) UN CAMBIO EN VIVO NO DA ESCALON (la etapa llega sola al objetivo en unos 50 ms).
//  5) DISCO CONTINUO: dos pistas encadenadas con el estado heredado no dan escalon, ni cuando la nueva pide otra curva.
//  6) LOS ESTANTES: 31 Hz sube lo de 20 Hz y 16 kHz lo de 18 kHz; una banda por encima de 0,45 de la frecuencia no se pone.
//  7) LA CONFIGURACION: eq_dueno= y eq_dueno_billar= se leen, se acotan (-12 a 12, a medio dB) y se guardan; lo que falta o viene mal es 0.
//  8) LA CURVA DE LO QUE YA HACE LA ROCOLA en el billar: el subgrave a 40 Hz (25 Hz muy abajo) y 1 kHz cerca de 0.
// 10) EL MARGEN SEGUN LA CANCION (E3): PreganCancionDb con la huella de tono de la cancion; todo en 0 = 0; sin huella = el maximo; mucho grave =
//     casi el maximo; sin graves = mucho menos; solo bajar = 0; la etapa baja lo que dice; en un disco continuo la preganancia va a 2 dB/s
//     sin escalon y, si el dueno mueve un mando, rapido.
class FuenteQ : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteQ(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class Ecualizador
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string Texto(string rel) { string p = Path.Combine("..", "Rocola", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

    static Type TE;
    static object Nueva(ISampleProvider s, int[] m) { return Activator.CreateInstance(TE, T, null, new object[] { s, m }, null); }
    static void Ajusta(object e, int[] m) { TE.GetMethod("Ajusta", T).Invoke(e, new object[] { m }); }
    static object Nueva3(ISampleProvider s, int[] m, float[] pesos) { return Activator.CreateInstance(TE, T, null, new object[] { s, m, pesos }, null); }
    static double PreganC(int[] m, int fs, float[] pesos) { return (double)TE.GetMethod("PreganCancionDb", T).Invoke(null, new object[] { m, fs, pesos }); }
    // Pesos de amplitud como los arma ArmaCadena: 10^(dB/20) por banda de la huella (EqCentros).
    static float[] Pesos(params double[] l) { float[] p = new float[8]; for (int i = 0; i < 8; i++) p[i] = (float)Math.Pow(10.0, (i < l.Length ? l[i] : 0.0) / 20.0); return p; }
    static double Pregan(int[] m, int fs) { return (double)TE.GetMethod("PreganDb", T).Invoke(null, new object[] { m, fs }); }
    static double[] Resp(int[] m, int fs, double[] hz) { return (double[])TE.GetMethod("Respuesta", T).Invoke(null, new object[] { m, fs, hz }); }
    static int[] Medios(params double[] db) { int[] m = new int[10]; for (int i = 0; i < db.Length && i < 10; i++) m[i] = (int)Math.Round(db[i] * 2.0); return m; }
    static float[] Tono(double hz, double amp, int fs, int ch, double seg, double fase0)
    {
        int n = (int)(seg * fs); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++) { float v = (float)(amp * Math.Sin(2.0 * Math.PI * hz * i / fs + fase0)); for (int c = 0; c < ch; c++) x[i * ch + c] = v; }
        return x;
    }
    static float[] Lee(ISampleProvider s, int total, int trozo)
    {
        float[] r = new float[total], b = new float[trozo]; int n = 0;
        while (n < total) { int k = s.Read(b, 0, Math.Min(trozo, total - n)); if (k <= 0) break; Array.Copy(b, 0, r, n, k); n += k; }
        if (n < total) Array.Resize(ref r, n);
        return r;
    }
    static double Rms(float[] x, int desde, int hasta, int paso, int c)
    {
        double s = 0; int n = 0;
        for (int i = desde; i < hasta; i += paso) { s += (double)x[i + c] * x[i + c]; n++; }
        return Math.Sqrt(s / Math.Max(1, n));
    }
    // El cambio de nivel (dB) de un tono: la salida contra la entrada en el ultimo cuarto de la lectura.
    static double CambioDb(int[] m, double hz, int fs)
    {
        float[] x = Tono(hz, 0.1, fs, 2, 3.0, 0.0);
        object e = Nueva(new FuenteQ(x, fs, 2), m);
        float[] y = Lee((ISampleProvider)e, x.Length, 4096);
        int d = (int)(x.Length * 0.75) / 2 * 2;
        return 20.0 * Math.Log10(Rms(y, d, y.Length, 2, 0) / Rms(x, d, x.Length, 2, 0));
    }
    static double CambioDb2(int[] m, float[] pesos, double hz, int fs)
    {
        float[] x = Tono(hz, 0.1, fs, 2, 3.0, 0.0);
        object e = Nueva3(new FuenteQ(x, fs, 2), m, pesos);
        float[] y = Lee((ISampleProvider)e, x.Length, 4096);
        int d = (int)(x.Length * 0.75) / 2 * 2;
        return 20.0 * Math.Log10(Rms(y, d, y.Length, 2, 0) / Rms(x, d, x.Length, 2, 0));
    }
    static double PasoMax(float[] y, int desde, int hasta, int ch, int c)
    {
        double mx = 0;
        for (int i = desde + ch; i < hasta; i += ch) { double d = Math.Abs((double)y[i + c] - y[i - ch + c]); if (d > mx) mx = d; }
        return mx;
    }
    static double Pico(float[] y, int desde, int hasta) { double p = 0; for (int i = desde; i < hasta; i++) if (Math.Abs(y[i]) > p) p = Math.Abs(y[i]); return p; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        TE = asm.GetType("Rocola.EcualizadorDueno", false);
        Check(TE != null, "la etapa EcualizadorDueno existe");
        if (fallos > 0) return fallos;
        const int fs = 44100;
        Random az = new Random(17);

        Console.WriteLine("\n  1) TODO EN 0 PASA TAL CUAL\n");
        {
            float[] x = new float[fs * 2 * 2]; for (int i = 0; i < x.Length; i++) x[i] = (float)(az.NextDouble() * 1.6 - 0.8);
            float[] y = Lee((ISampleProvider)Nueva(new FuenteQ(x, fs, 2), new int[10]), x.Length, 1000);
            bool igual = y.Length == x.Length; for (int i = 0; igual && i < x.Length; i++) if (BitConverter.DoubleToInt64Bits(y[i]) != BitConverter.DoubleToInt64Bits(x[i])) igual = false;
            Check(igual, "estereo, todo en 0: la salida es la entrada bit a bit");
            float[] y2 = Lee((ISampleProvider)Nueva(new FuenteQ(x, fs, 2), new int[10]), x.Length, 1);
            igual = y2.Length == x.Length; for (int i = 0; igual && i < x.Length; i++) if (y2[i] != x[i]) igual = false;
            Check(igual, "lecturas de una muestra: igual");
            float[] xm = new float[fs]; for (int i = 0; i < xm.Length; i++) xm[i] = (float)(az.NextDouble() - 0.5);
            float[] ym = Lee((ISampleProvider)Nueva(new FuenteQ(xm, fs, 1), new int[10]), xm.Length, 777);
            igual = ym.Length == xm.Length; for (int i = 0; igual && i < xm.Length; i++) if (ym[i] != xm[i]) igual = false;
            Check(igual, "mono, todo en 0: igual");
            float[] x3 = new float[fs * 3]; for (int i = 0; i < x3.Length; i++) x3[i] = (float)(az.NextDouble() - 0.5);
            float[] y3 = Lee((ISampleProvider)Nueva(new FuenteQ(x3, fs, 3), Medios(0, 6)), x3.Length, 999);
            igual = y3.Length == x3.Length; for (int i = 0; igual && i < x3.Length; i++) if (y3[i] != x3[i]) igual = false;
            Check(igual, "con tres canales (no se sabe cual bocina es cual) pasa tal cual aunque haya bandas");
            // Subir y bajar a 0 en vivo: acaba otra vez sin tocar nada.
            object e = Nueva(new FuenteQ(Tono(300, 0.2, fs, 2, 6.0, 0), fs, 2), Medios(0, 6));
            Lee((ISampleProvider)e, fs * 2, 512);
            Ajusta(e, new int[10]);
            Lee((ISampleProvider)e, fs * 2, 512);
            float[] c = Lee((ISampleProvider)e, fs * 2, 512);
            float[] orig = Tono(300, 0.2, fs, 2, 6.0, 0);
            igual = true; for (int i = 0; i < c.Length; i++) if (c[i] != orig[fs * 4 + i]) { igual = false; break; }
            Check(igual && Math.Abs((double)TE.GetMethod("get_PreganAhoraDb", T).Invoke(e, null)) < 1e-12, "puesto y quitado en vivo: pasados 2 s vuelve a pasar tal cual (bit a bit) y sin preganancia");
            string son = Texto("Form1.Sonido.cs");
            int iH = son.IndexOf("c.Hundir = new BomboSinHundir"), iE = son.IndexOf("c.EqDueno = new EcualizadorDueno(c.Hundir, EqDuenoPuesto(), c.EqDuenoPesos)"), iL = son.IndexOf("new LimitadorPicoReal(alLimitador");
            Check(iH > 0 && iE > iH && iL > iE && son.Contains("alLimitador = c.EqDueno;") && son.Contains("new MideMargen(c.EqDueno,"),
                  "en la cadena: justo despues del bombo y antes de medir el margen y del limitador");
            Check(!son.Substring(iE, Math.Max(0, iL - iE)).Contains("sinMejoras"), "«Escuchar sin mejoras» no lo toca al armar la cadena");
            Check(Texto("Form1.SinPausa.cs").Contains("nueva.EqDueno.Sigue(vieja.EqDueno)") && Texto("GraveMono.cs").Contains("AplicaEqDueno();"),
                  "el disco continuo lo hereda (SigueA) y AplicaBocinas lo lleva al modo puesto");
        }

        Console.WriteLine("\n  2) +6 dB EN 63 Hz, CON SU PREGANANCIA\n");
        {
            int[] m = Medios(0, 6);
            double[] hz = { 63.0, 1000.0, 20.0 };
            double[] r = Resp(m, fs, hz);
            Console.WriteLine("    curva: 63 Hz " + r[0].ToString("0.00") + " dB, 1 kHz " + r[1].ToString("0.00") + " dB");
            Check(Math.Abs(r[0] - 6.0) < 0.05 && Math.Abs(r[1]) < 0.3, "la curva sube 6 dB en 63 Hz y casi nada en 1 kHz (antes de la preganancia)");
            double p = Pregan(m, fs);
            Check(Math.Abs(p + 6.0) < 0.05, "la preganancia es -6 dB (el maximo de la curva con el signo cambiado): " + p.ToString("0.000"));
            double c63 = CambioDb(m, 63, fs), c1k = CambioDb(m, 1000, fs);
            Console.WriteLine("    tono de 63 Hz: " + c63.ToString("0.00") + " dB; tono de 1 kHz: " + c1k.ToString("0.00") + " dB");
            Check(Math.Abs(c63) < 0.2, "con la preganancia, el pico de la curva queda en 0 dB (el tono de 63 Hz sale a 0 dB de cambio)");
            Check(Math.Abs(c1k + 6.0) < 0.4, "y el de 1 kHz a unos -6 dB");
            double[] cuatro = { 63.0, 63.0 };
            double[] unaBanda = Resp(Medios(0, 12), fs, cuatro);
            double[] juntas = Resp(Medios(0, 12, 12), fs, new double[] { 90.0 });
            Check(Pregan(Medios(0, 12, 12), fs) < -(juntas[0] - 0.01) && unaBanda[0] > 11.9, "dos bandas vecinas a +12: la preganancia sigue al maximo de la curva ENTERA (" + Pregan(Medios(0, 12, 12), fs).ToString("0.0") + " dB)");
            Check(Pregan(new int[10], fs) == 0.0, "todo en 0: preganancia 0");
            Check(Pregan(Medios(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), fs) == 0.0 && Pregan(Medios(-12, -3, 0, 6, -12, 0, 0, 0, 0, -12), fs) <= 0.0, "nunca es positiva");
        }

        Console.WriteLine("\n  3) -6 dB EN 1 kHz\n");
        {
            int[] m = Medios(0, 0, 0, 0, 0, -6);
            double c1k = CambioDb(m, 1000, fs), c200 = CambioDb(m, 100, fs);
            Console.WriteLine("    tono de 1 kHz: " + c1k.ToString("0.00") + " dB; tono de 100 Hz: " + c200.ToString("0.00") + " dB");
            Check(Math.Abs(c1k + 6.0) < 0.15, "baja unos 6 dB en 1 kHz");
            Check(Math.Abs(c200) < 0.3, "y casi nada dos octavas y media mas abajo");
            Check(Pregan(m, fs) == 0.0, "la preganancia es 0 si la curva solo baja");
        }

        Console.WriteLine("\n  4) UN CAMBIO EN VIVO NO DA ESCALON\n");
        {
            double[] tonos = { 440.0, 63.0, 1000.0, 5000.0 };
            int[][] cambios = { Medios(0, 0, 0, 0, 12), Medios(12), Medios(0, 0, 0, 0, 0, -12), Medios(0, 0, 0, 0, 0, 0, 0, 12, 0, 0) };
            for (int t = 0; t < tonos.Length; t++)
            {
                float[] x = Tono(tonos[t], 0.4, fs, 2, 2.0, 0.3);
                object e = Nueva(new FuenteQ(x, fs, 2), new int[10]);
                float[] y1 = Lee((ISampleProvider)e, fs, 333);
                Ajusta(e, cambios[t]);
                float[] y2 = Lee((ISampleProvider)e, fs, 333);   // lecturas de 333 muestras: el cambio cae a mitad de bloque
                float[] y = new float[y1.Length + y2.Length]; Array.Copy(y1, y, y1.Length); Array.Copy(y2, 0, y, y1.Length, y2.Length);
                double paso = PasoMax(y, 0, y.Length, 2, 0), pico = Pico(y, 0, y.Length), natural = pico * 2.0 * Math.PI * tonos[t] / fs;
                double mismoPico = Pico(y1, 0, y1.Length);
                Check(paso <= natural * 1.15 + 1e-6, "tono de " + tonos[t] + " Hz: el paso mayor entre muestras (" + paso.ToString("0.00000") + ") no pasa del natural del tono (" + natural.ToString("0.00000") + ")");
                // el valor que llega: pasado el cambio, la salida no sigue creciendo ni oscilando (rampa de 50 ms)
                Check(mismoPico > 0.39, "el tono de antes del cambio pasa tal cual (pico " + mismoPico.ToString("0.000") + ")");
            }
            // Quitar la banda en vivo tambien (la rampa baja y la banda se apaga sin chasquido).
            float[] z = Tono(100.0, 0.4, fs, 2, 3.0, 0.1);
            object e2 = Nueva(new FuenteQ(z, fs, 2), Medios(0, 0, 12));
            float[] a = Lee((ISampleProvider)e2, fs, 256);
            Ajusta(e2, new int[10]);
            float[] b = Lee((ISampleProvider)e2, fs * 2, 256);
            float[] ab = new float[a.Length + b.Length]; Array.Copy(a, ab, a.Length); Array.Copy(b, 0, ab, a.Length, b.Length);
            double p2 = PasoMax(ab, 0, ab.Length, 2, 0), n2 = Pico(ab, 0, ab.Length) * 2.0 * Math.PI * 100.0 / fs;
            Check(p2 <= n2 * 1.15 + 1e-6, "quitar la banda en vivo: sin escalon (" + p2.ToString("0.00000") + " contra " + n2.ToString("0.00000") + ")");
            Check(Math.Abs(Pico(b, b.Length - 4000, b.Length) - 0.4) < 0.01, "y al final el tono vuelve a su nivel de antes");
        }

        Console.WriteLine("\n  5) DISCO CONTINUO: LA SIGUIENTE SIGUE CON LA DE ANTES\n");
        {
            int n1 = fs * 2, n2 = fs * 2;
            float[] largo = Tono(63.0, 0.4, fs, 2, 4.0, 0.0);
            float[] p1 = new float[n1 * 2], p2 = new float[n2 * 2]; Array.Copy(largo, 0, p1, 0, p1.Length); Array.Copy(largo, p1.Length, p2, 0, p2.Length);
            int[] curva = Medios(0, 12, 6);
            object a = Nueva(new FuenteQ(p1, fs, 2), curva);
            float[] ya = Lee((ISampleProvider)a, p1.Length, 1024);
            object b = Nueva(new FuenteQ(p2, fs, 2), curva);
            TE.GetMethod("Sigue", T).Invoke(b, new object[] { a });
            float[] yb = Lee((ISampleProvider)b, p2.Length, 1024);
            float[] junto = new float[ya.Length + yb.Length]; Array.Copy(ya, junto, ya.Length); Array.Copy(yb, 0, junto, ya.Length, yb.Length);
            double natural = Pico(junto, 0, junto.Length) * 2.0 * Math.PI * 63.0 / fs;
            double pasoCon = PasoMax(junto, ya.Length - 40, ya.Length + 40, 2, 0);
            Console.WriteLine("    paso en el empalme: " + pasoCon.ToString("0.000000") + " (natural del tono " + natural.ToString("0.000000") + ")");
            Check(pasoCon <= natural * 1.1 + 1e-7, "dos pistas encadenadas con el estado heredado: sin escalon en el empalme");
            // Sin heredar, la nueva parte de la musica de ese momento; la prueba solo pide que siga siendo un tono sin saltos grandes.
            object c = Nueva(new FuenteQ(p2, fs, 2), curva);
            float[] yc = Lee((ISampleProvider)c, p2.Length, 1024);
            double pasoSin = PasoMax(yc, 0, 400, 2, 0);
            Console.WriteLine("    (sin heredar, para comparar: paso mayor de los primeros cuadros " + pasoSin.ToString("0.000000") + ")");
            // La nueva pide OTRA curva: hereda la ganancia de las bandas y llega con su rampa, sin escalon.
            object d = Nueva(new FuenteQ(p2, fs, 2), new int[10]);
            TE.GetMethod("Sigue", T).Invoke(d, new object[] { a });
            Ajusta(d, new int[10]);   // el dueno movio un mando: la preganancia heredada vuelve a la rampa rapida (sin tocar, iria a 2 dB por segundo)
            float[] yd = Lee((ISampleProvider)d, p2.Length, 1024);
            float[] j2 = new float[ya.Length + yd.Length]; Array.Copy(ya, j2, ya.Length); Array.Copy(yd, 0, j2, ya.Length, yd.Length);
            double pasoOtra = PasoMax(j2, ya.Length - 40, ya.Length + 2500, 2, 0);
            Check(pasoOtra <= natural * 1.1 + 1e-7, "la siguiente pide otra curva (todo en 0): la rampa la lleva sin escalon (" + pasoOtra.ToString("0.000000") + ")");
            Check(Math.Abs(Pico(yd, yd.Length - 4000, yd.Length) - 0.4) < 0.005, "y acaba pasando tal cual (0 dB)");
            // Cebado: los cuadros de delante pasan sin tocar lo heredado.
            object g = Nueva(new FuenteQ(p2, fs, 2), curva);
            TE.GetMethod("Sigue", T).Invoke(g, new object[] { a });
            FieldInfo fCeb = TE.GetField("Cebado", T);
            fCeb.SetValue(g, 100);
            float[] yg = Lee((ISampleProvider)g, 400, 400);
            bool tal = true; for (int i = 0; i < 200; i++) if (yg[i] != p2[i]) { tal = false; break; }
            Check(tal, "el cebado de delante (100 cuadros) pasa tal cual y no entra en lo heredado");
        }

        Console.WriteLine("\n  6) LOS ESTANTES Y EL LIMITE DE FRECUENCIA\n");
        {
            double[] hz = { 20.0, 1000.0, 18000.0 };
            double[] r = Resp(Medios(6), fs, hz);
            Console.WriteLine("    estante de 31 Hz +6: 20 Hz " + r[0].ToString("0.00") + ", 1 kHz " + r[1].ToString("0.00"));
            Check(r[0] > 5.0 && Math.Abs(r[1]) < 0.2, "31 Hz es un estante de graves: 20 Hz sube con +6 y 1 kHz no");
            double d = CambioDb(Medios(6), 20, fs) - CambioDb(Medios(6), 1000, fs);
            Check(d > 4.5 && d < 6.2, "con la senal: 20 Hz queda " + d.ToString("0.00") + " dB sobre 1 kHz (el estante da +6 en su meseta; a 20 Hz, algo menos)");
            double[] r2 = Resp(Medios(0, 0, 0, 0, 0, 0, 0, 0, 0, 6), fs, hz);
            Console.WriteLine("    estante de 16 kHz +6: 18 kHz " + r2[2].ToString("0.00") + ", 1 kHz " + r2[1].ToString("0.00"));
            Check(r2[2] > 4.5 && Math.Abs(r2[1]) < 0.2 && Math.Abs(r2[0]) < 0.1, "16 kHz es un estante de agudos: 18 kHz sube con +6 y 1 kHz y 20 Hz no");
            double d2 = CambioDb(Medios(0, 0, 0, 0, 0, 0, 0, 0, 0, 6), 18000, fs) - CambioDb(Medios(0, 0, 0, 0, 0, 0, 0, 0, 0, 6), 1000, fs);
            Check(d2 > 4.0, "con la senal: 18 kHz queda " + d2.ToString("0.00") + " dB sobre 1 kHz");
            // 22,05 kHz: 0,45 de la frecuencia es 9,9 kHz: la banda de 16 kHz no se pone.
            int[] m = Medios(0, 0, 0, 0, 0, 0, 0, 0, 0, 12);
            float[] x = Tono(5000, 0.3, 22050, 2, 1.0, 0);
            float[] y = Lee((ISampleProvider)Nueva(new FuenteQ(x, 22050, 2), m), x.Length, 700);
            bool igual = y.Length == x.Length; for (int i = 0; igual && i < x.Length; i++) if (y[i] != x[i]) igual = false;
            Check(igual && Pregan(m, 22050) == 0.0, "a 22,05 kHz la banda de 16 kHz (mas alla de 0,45 de la frecuencia) no se pone: pasa tal cual y sin preganancia");
            Check(Resp(m, 22050, new double[] { 5000.0 })[0] == 0.0, "y tampoco entra en la curva");
            // Mono: una campana en 1 kHz.
            float[] xm = Tono(1000, 0.2, fs, 1, 2.0, 0);
            float[] ym = Lee((ISampleProvider)Nueva(new FuenteQ(xm, fs, 1), Medios(0, 0, 0, 0, 0, -6)), xm.Length, 600);
            double cm = 20.0 * Math.Log10(Rms(ym, xm.Length / 2, xm.Length, 1, 0) / Rms(xm, xm.Length / 2, xm.Length, 1, 0));
            Check(Math.Abs(cm + 6.0) < 0.2, "en mono tambien (" + cm.ToString("0.00") + " dB)");
        }

        Console.WriteLine("\n  7) LA CONFIGURACION: eq_dueno= Y eq_dueno_billar=\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_eq_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            object f = Codigo.FormSinEncender(TF);
            int[] normal = (int[])TF.GetField("eqDuenoNormal", T).GetValue(null), billar = (int[])TF.GetField("eqDuenoBillar", T).GetValue(null);
            Check(normal != null && billar != null && normal.Length == 10 && billar.Length == 10, "hay dos memorias de diez bandas");
            bool z = true; foreach (int v in normal) if (v != 0) z = false; foreach (int v in billar) if (v != 0) z = false;
            Check(z, "de fabrica todo en 0");
            Action<string> carga = txt => { File.WriteAllText(Path.Combine(dir, "rocola_config.txt"), txt); TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null); };
            carga("eq_dueno=0.0;1.5;-3;20;-20;abc;;0.25;-0.25;12\r\neq_dueno_billar=6.0;-6;0.5\r\n");
            Check(normal[0] == 0 && normal[1] == 3 && normal[2] == -6 && normal[3] == 24 && normal[4] == -24, "se leen (a medio dB) y se acotan a -12..12");
            Check(normal[5] == 0 && normal[6] == 0 && normal[7] == 1 && normal[8] == -1 && normal[9] == 24, "lo que viene mal o vacio es 0; 0,25 se redondea a 0,5");
            Check(billar[0] == 12 && billar[1] == -12 && billar[2] == 1 && billar[3] == 0 && billar[9] == 0, "el del billar es otro, y lo que falta es 0");
            carga("eq_dueno=\r\neq_dueno_billar=x\r\n");
            Check(normal[9] == 0 && normal[1] == 0 && billar[0] == 0, "vacio o malo: todo 0");
            carga("eq_dueno=1;2;3;4;5;6;7;8;9;10.5\r\neq_dueno_billar=-1.5;0;0;0;0;0;0;0;0;12\r\n");
            TF.GetMethod("SaveConfigPrefs", T).Invoke(f, null);
            Codigo.EsperaGuardado(TF, f);
            string nuevo = File.ReadAllText(Path.Combine(dir, "rocola_config.txt"));
            Check(nuevo.Contains("eq_dueno=1.0;2.0;3.0;4.0;5.0;6.0;7.0;8.0;9.0;10.5\r\n") && nuevo.Contains("eq_dueno_billar=-1.5;0.0;0.0;0.0;0.0;0.0;0.0;0.0;0.0;12.0\r\n"),
                  "se guardan siempre, con un decimal y punto decimal");
            // Las funciones de la ventana.
            MethodInfo cambia = TF.GetMethod("EqDuenoCambiaBanda", T), cero = TF.GetMethod("EqDuenoCero", T), db = TF.GetMethod("EqDuenoDb", T), puesto = TF.GetMethod("EqDuenoPuesto", T);
            FieldInfo fLa = TF.GetField("localAuto", T);
            fLa.SetValue(null, false);
            cambia.Invoke(f, new object[] { 3, 4.3 });
            Check(normal[3] == 9 && billar[3] == 0 && (double)db.Invoke(f, new object[] { false, 3 }) == 4.5, "cambiar una banda con el modo normal puesto cambia la del normal (a medio dB)");
            fLa.SetValue(null, true);
            cambia.Invoke(f, new object[] { 3, -30.0 });
            Check(billar[3] == -24 && normal[3] == 9 && ((int[])puesto.Invoke(f, null))[3] == -24, "con el billar puesto cambia la del billar (acotada a -12)");
            cero.Invoke(f, new object[] { true });
            bool todo0 = true; foreach (int v in billar) if (v != 0) todo0 = false;
            Check(todo0 && normal[3] == 9, "poner en 0 un modo no toca el otro");
            Check((double)TF.GetMethod("EqDuenoPreganDb", T).Invoke(f, new object[] { false }) < 0.0 && (double)TF.GetMethod("EqDuenoPreganDb", T).Invoke(f, new object[] { true }) == 0.0,
                  "la preganancia de cada modo se pregunta por separado");

            Console.WriteLine("\n  8) LA CURVA DE LO QUE YA HACE LA ROCOLA\n");
            MethodInfo fijo = TF.GetMethod("EqDuenoFijoRespuesta", T);
            double[] hz = { 25.0, 40.0, 60.0, 1000.0, 5600.0 };
            FieldInfo fCorta = TF.GetField("cortaSubgrave", T);
            fLa.SetValue(null, true);
            double[] r = (double[])fijo.Invoke(f, new object[] { hz });
            Console.WriteLine("    billar: 25 Hz " + r[0].ToString("0.0") + ", 40 Hz " + r[1].ToString("0.0") + ", 60 Hz " + r[2].ToString("0.0") + ", 1 kHz " + r[3].ToString("0.0") + ", 5,6 kHz " + r[4].ToString("0.0"));
            Check(r[0] < -10.0 && Math.Abs(r[1] + 3.0) < 1.0 && r[2] > -2.0, "en el billar el subgrave corta a 40 Hz: 25 Hz muy abajo, 40 Hz a -3 dB, 60 Hz casi libre");
            Check(Math.Abs(r[3]) < 1.0, "y 1 kHz cerca de 0");
            Check(r[4] < -0.9 && r[4] > -2.5, "5,6 kHz lleva la inclinacion fija del billar (-1,5 dB)");
            fLa.SetValue(null, false); fCorta.SetValue(null, false);
            r = (double[])fijo.Invoke(f, new object[] { hz });
            bool cero8 = true; foreach (double v in r) if (Math.Abs(v) > 1e-9) cero8 = false;
            Check(cero8, "con el modo normal y todo apagado la curva fija es plana (0 dB)");
            fCorta.SetValue(null, true);
            r = (double[])fijo.Invoke(f, new object[] { hz });
            Check(r[0] < -5.0 && r[0] > -9.0 && Math.Abs(r[3]) < 0.01, "con el subgrave a mano (30 Hz de fabrica): 25 Hz baja unos 7 dB, 1 kHz no (" + r[0].ToString("0.0") + ")");
            fCorta.SetValue(null, false);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine("\n  9) LA HOJA DE CTRL+F3 (la ventana real la recorre ventanaf3: diez deslizadores, flechas, Todo en 0, fabrica, nada encimado)\n");
        string aj = Texto("Form1.Ajustes.cs");
        Check(aj.Contains("\"SonidoEqAbrir\"") && aj.Contains("\"SonidoEq\"") && aj.Contains("\"SonidoEqBanda\" + i") && aj.Contains("new Deslizador[EcualizadorDueno.Bandas]"),
              "la ventana tiene el boton Ecualizador, la hoja SonidoEq y un deslizador por banda");
        Check(aj.Contains("EqDuenoCambiaBanda(banda,") && aj.Contains("pEq.Visible = (f3Pagina == 0 && verEq"), "mover un deslizador cambia la banda al momento y la hoja se muestra con su bool");
        Check(aj.Contains("int[] bkEqN = (int[])eqDuenoNormal.Clone(), bkEqB = (int[])eqDuenoBillar.Clone();"), "al abrir la ventana se respaldan las dos memorias");
        int iCatch = aj.IndexOf("Array.Copy(bkEqN, eqDuenoNormal"), iApl = iCatch < 0 ? -1 : aj.IndexOf("AplicaBocinas();", iCatch);
        Check(iCatch > 0 && aj.Contains("Array.Copy(bkEqB, eqDuenoBillar") && iApl > iCatch && iApl - iCatch < 400, "al cancelar se devuelven las dos memorias ANTES de AplicaBocinas");
        Check(aj.Contains("EqDuenoCero(false); EqDuenoCero(true); refrescaEq();"), "«Dejar como de fabrica» pone las dos memorias en 0");

        Console.WriteLine("\n  10) EL MARGEN SEGUN LA CANCION (E3)\n");
        {
            float[] mucho = Pesos(6, 0, 0, 0, 0, -3, -6, -12), poco = Pesos(-20, -6, 0, 0, 0, -3, -6, -12);
            // Ojo: con +6 dB en 63 Hz la curva da +6 en 63 Hz pero solo +4,15 en la banda de 50 Hz de la huella y +0,64 en la de 158 Hz (las ocho bandas de
            // la huella estan lejos unas de otras), asi que ni una cancion de pura banda de 50 Hz pasa del 77 % del maximo: «casi todo grave» es eso.
            float[] casiTodoGrave = Pesos(30, -10, -30, -30, -30, -30, -30, -30);
            int[] m63 = Medios(0, 6);
            double max = -Pregan(m63, fs);
            double pm = PreganC(m63, fs, mucho), pp = PreganC(m63, fs, poco);
            Console.WriteLine("    maximo de la curva (+6 dB en 63 Hz): " + max.ToString("0.000") + " dB");
            Console.WriteLine("    cancion de mucho grave: margen " + (-pm).ToString("0.000") + " dB (" + (-pm / max * 100.0).ToString("0.0") + " % del maximo)");
            Console.WriteLine("    cancion sin graves:     margen " + (-pp).ToString("0.000") + " dB (" + (-pp / max * 100.0).ToString("0.0") + " % del maximo)");
            double pt = PreganC(m63, fs, casiTodoGrave);
            Console.WriteLine("    cancion casi toda de graves (50 Hz +30 dB sobre el resto): margen " + (-pt).ToString("0.000") + " dB (" + (-pt / max * 100.0).ToString("0.0") + " % del maximo)");
            Check(PreganC(new int[10], fs, mucho) == 0.0 && PreganC(new int[10], fs, null) == 0.0, "todo en 0: el margen es 0 con huella y sin ella");
            Check(PreganC(m63, fs, null) == Pregan(m63, fs) && PreganC(m63, fs, new float[8]) == Pregan(m63, fs) && PreganC(m63, fs, new float[] { float.NaN, 1, 1, 1, 1, 1, 1, 1 }) == Pregan(m63, fs),
                  "sin huella (null, suma 0 o no finita) es lo maximo de siempre (PreganDb)");
            Check(-pt >= 0.75 * max && -pt <= max + 1e-9, "una cancion casi toda de graves baja casi lo maximo (al menos el 75 %)");
            Check(-pm > -pp + 0.5 && -pm < max, "la de mucho grave baja mas que la que no tiene graves (y menos que el maximo)");
            Check(-pp < 0.5 * max && pp <= 0.0 && -pp <= max + 1e-9 && -pp >= 0.25 * max - 1e-9, "una cancion sin graves baja mucho menos (menos del 50 % del maximo, nunca mas que el maximo ni positivo; con su 25 % de seguridad)");
            Check(PreganC(Medios(-6, -3, 0, -12, 0, -6), fs, mucho) == 0.0 && PreganC(Medios(-12, -12, -12, -12, -12, -12, -12, -12, -12, -12), fs, poco) == 0.0, "solo bajar bandas: 0");
            // cualquier huella: nunca positivo ni mayor que el maximo
            bool acota = true; Random r10 = new Random(5);
            for (int k = 0; k < 400; k++)
            {
                double[] l = new double[8]; for (int i = 0; i < 8; i++) l[i] = r10.NextDouble() * 80.0 - 60.0;
                int[] mm = new int[10]; for (int i = 0; i < 10; i++) mm[i] = r10.Next(-24, 25);
                double mx = -Pregan(mm, fs), v = PreganC(mm, fs, Pesos(l));
                if (v > 0.0 || -v > mx + 1e-9 || double.IsNaN(v)) { acota = false; break; }
            }
            Check(acota, "400 huellas y curvas al azar: el margen nunca es positivo ni pasa del maximo");
            // la etapa baja lo que dice PreganCancionDb: el tono de 1 kHz (donde la curva casi no sube)
            double c1k = CambioDb2(m63, mucho, 1000, fs), e1k = pm + Resp(m63, fs, new double[] { 1000.0 })[0];
            double c1kP = CambioDb2(m63, poco, 1000, fs), e1kP = pp + Resp(m63, fs, new double[] { 1000.0 })[0];
            Console.WriteLine("    tono de 1 kHz: mucho grave " + c1k.ToString("0.000") + " dB (esperado " + e1k.ToString("0.000") + "); sin graves " + c1kP.ToString("0.000") + " dB (esperado " + e1kP.ToString("0.000") + ")");
            Check(Math.Abs(c1k - e1k) < 0.05 && Math.Abs(c1kP - e1kP) < 0.05, "la etapa con pesos baja el tono de 1 kHz lo que dice PreganCancionDb (a 0,05 dB)");
            Check(Math.Abs(CambioDb(m63, 1000, fs) - CambioDb2(m63, null, 1000, fs)) < 1e-9, "el constructor de dos argumentos equivale a pesos null");
            // todo en 0 con pesos: bit a bit
            float[] x0 = new float[fs * 2 * 2]; for (int i = 0; i < x0.Length; i++) x0[i] = (float)(az.NextDouble() * 1.6 - 0.8);
            float[] y0 = Lee((ISampleProvider)Nueva3(new FuenteQ(x0, fs, 2), new int[10], mucho), x0.Length, 1000);
            bool igual0 = y0.Length == x0.Length; for (int i = 0; igual0 && i < x0.Length; i++) if (y0[i] != x0[i]) igual0 = false;
            Check(igual0, "todo en 0 con huella: la salida es la entrada bit a bit");

            // DISCO CONTINUO con pesos distintos: sin escalon en el empalme y la preganancia va a 2 dB por segundo
            float[] largo = Tono(1000.0, 0.4, fs, 2, 8.0, 0.0);
            float[] p1 = new float[fs * 2 * 2], p2 = new float[fs * 5 * 2]; Array.Copy(largo, 0, p1, 0, p1.Length); Array.Copy(largo, p1.Length, p2, 0, p2.Length);
            object a = Nueva3(new FuenteQ(p1, fs, 2), m63, casiTodoGrave);
            float[] ya = Lee((ISampleProvider)a, p1.Length, 1024);
            object b = Nueva3(new FuenteQ(p2, fs, 2), m63, poco);
            TE.GetMethod("Sigue", T).Invoke(b, new object[] { a });
            double paA = (double)TE.GetMethod("get_PreganAhoraDb", T).Invoke(a, null), metaB = (double)TE.GetMethod("get_PreganMetaDb", T).Invoke(b, null);
            float[] yb1 = Lee((ISampleProvider)b, fs * 2, 777);   // 1 s (estereo)
            double p1s = (double)TE.GetMethod("get_PreganAhoraDb", T).Invoke(b, null);
            Console.WriteLine("    empalme: la de antes termina en " + paA.ToString("0.000") + " dB, la nueva pide " + metaB.ToString("0.000") + " dB; a 1 s de la nueva va en " + p1s.ToString("0.000") + " dB");
            Check(Math.Abs(metaB - pp) < 1e-9 && metaB - paA > 2.5, "la nueva pide otra preganancia (mas de 2,5 dB de diferencia)");
            Check(Math.Abs((p1s - paA) - 2.0) < 0.02, "a 1 s del empalme la preganancia heredada se movio 2 dB (" + (p1s - paA).ToString("0.000") + "): 2 dB por segundo");
            float[] resto = Lee((ISampleProvider)b, p2.Length, 777);
            float[] junto = new float[ya.Length + yb1.Length + resto.Length]; Array.Copy(ya, junto, ya.Length); Array.Copy(yb1, 0, junto, ya.Length, yb1.Length); Array.Copy(resto, 0, junto, ya.Length + yb1.Length, resto.Length);
            double natural = Pico(junto, 0, junto.Length) * 2.0 * Math.PI * 1000.0 / fs;
            double pasoE = PasoMax(junto, ya.Length - 60, ya.Length + 60, 2, 0), pasoTodo = PasoMax(junto, 0, junto.Length, 2, 0);
            Console.WriteLine("    paso en el empalme " + pasoE.ToString("0.000000") + ", paso mayor de todo el trozo " + pasoTodo.ToString("0.000000") + " (natural del tono " + natural.ToString("0.000000") + ")");
            Check(pasoE <= natural * 1.1 + 1e-7 && pasoTodo <= natural * 1.1 + 1e-7, "disco continuo con pesos distintos: sin escalon en el empalme ni despues");
            double pf = (double)TE.GetMethod("get_PreganAhoraDb", T).Invoke(b, null);
            Check(Math.Abs(pf - metaB) < 1e-9, "y pasados los segundos llega a la preganancia de la nueva (" + pf.ToString("0.000") + ")");
            // sin heredar la nueva parte de su margen; y si el dueno mueve un mando a media rampa, vuelve a la rampa rapida
            object c = Nueva3(new FuenteQ(p2, fs, 2), m63, poco);
            TE.GetMethod("Sigue", T).Invoke(c, new object[] { a });
            Lee((ISampleProvider)c, fs / 2 * 2, 777);   // 0,5 s: va despacio
            double pmed = (double)TE.GetMethod("get_PreganAhoraDb", T).Invoke(c, null);
            Ajusta(c, m63);   // el dueno toca un mando (la misma curva)
            Lee((ISampleProvider)c, fs / 5 * 2, 777);     // 0,2 s
            double pdesp = (double)TE.GetMethod("get_PreganAhoraDb", T).Invoke(c, null);
            Console.WriteLine("    mover un mando a media rampa: antes " + pmed.ToString("0.000") + " dB, 0,2 s despues " + pdesp.ToString("0.000") + " dB (meta " + pp.ToString("0.000") + ")");
            Check(Math.Abs(pmed - paA) < 1.1 && Math.Abs(pdesp - pp) < 1e-9, "al mover un mando la preganancia salta a la rampa rapida y llega a su meta en menos de 0,2 s");
            string eqs = Texto("EcualizadorDueno.cs");
            Check(eqs.Contains("RampaPreLentaDbSeg = 2.0"), "la rampa lenta es de 2 dB por segundo (RampaPreLentaDbSeg)");
        }

        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        return fallos;
    }
}

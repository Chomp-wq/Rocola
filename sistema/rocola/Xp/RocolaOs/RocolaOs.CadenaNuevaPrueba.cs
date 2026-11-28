// LA ORDEN DE PRUEBA DE LA CADENA NUEVA (Pruebas/RocolaOs/cadena-nueva/prueba.sh; CADENA_NUEVA_A.md). No hace nada en la rocola:
// `rocola-xp --prueba-cadena-nueva ...` corre aqui y acaba el programa (RocolaOs.Ordenes.cs). Cada renglon de resultado empieza por su parte en
// mayusculas y lleva «clave=valor» para que el guion compruebe.
//   decodifica MP3...       el decodificado de hoy (16 bits) frente al nuevo (coma flotante): picos, muestras recortadas, diferencia
//   sinhuecos WAV MP3...    cada MP3 por la apertura nueva (coma flotante y sin huecos) frente al WAV original: desfase y largo
//   mide ARCHIVO...         la medida nueva entera (RocolaOsMideCancion, cadena A): LUFS, pico real, LRA, arreglos, lo que tardo
//   medidor                 el medidor con senales conocidas: senos, ruido rosa (contra MedidorBs1770 de XP), el pico real de un seno cerca de fs/4
//   ganancia ARCHIVO...     cada uno medido, a su ganancia, por la cadena: la sonoridad y el pico real de lo que sale
//   constante               ruido rosa (mono) con escalones de +-10 dB: la ganancia por bloque de 10 ms de la cadena nueva y del limitador de hoy
//   disco MP3 MP3 WAV       dos pistas seguidas de un disco por dos cadenas con el disco continuo: frente al WAV de las dos juntas
//   arreglos DIR            arma WAV con un canal mudo, uno invertido y continua, y los mide (detecta, segunda pasada, lo que sale)
//   (28-11-2026, los arreglos de CADENA_NUEVA_A.md) eqdueno ARCHIVO... (a1: lo que sube el pico con curvas del dueno, de verdad, estimado ahora y
//                           como antes) | eqvivo ARCHIVO... (a1: la curva sube a media cancion, con y sin el tope) | convierte (a5: la conversion de la
//                           salida de hoy con una cadena nueva y con una de XP) | dsee DIR (a7: el corte medido y el DSEE con el corte fijo o el que
//                           sigue a la musica) | noche (a8: la ganancia con la noche que publica el reloj)
//   costo MP3               tiempo de procesador del hilo por segundo de musica: decodificar y la cadena nueva, frente a hoy (16 bits y las
//                           etapas de XP que se arman sin la rocola: grave en mono, el local, el bajo psicoacustico, el EQ del dueno y el limitador)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        private static class RocolaOsCadenaNuevaPrueba
        {
            private static string N(double v, string f = "0.000") { return double.IsNegativeInfinity(v) ? "-inf" : v.ToString(f, CultureInfo.InvariantCulture); }
            internal static int Corre(string[] a)
            {
                try
                {
                    if (a.Length >= 2 && a[0] == "decodifica") { for (int i = 1; i < a.Length; i++) Decodifica(a[i]); return 0; }
                    if (a.Length >= 3 && a[0] == "sinhuecos") { for (int i = 2; i < a.Length; i++) SinHuecos(a[1], a[i]); return 0; }
                    if (a.Length >= 2 && a[0] == "mide") { for (int i = 1; i < a.Length; i++) Mide(a[i]); return 0; }
                    if (a.Length == 1 && a[0] == "medidor") return Medidor();
                    if (a.Length >= 2 && a[0] == "ganancia") { for (int i = 1; i < a.Length; i++) Ganancia(a[i]); return 0; }
                    if (a.Length == 1 && a[0] == "constante") return Constante();
                    if (a.Length == 4 && a[0] == "disco") return Disco(a[1], a[2], a[3]);
                    if (a.Length == 2 && a[0] == "arreglos") return Arreglos(a[1]);
                    if (a.Length == 2 && a[0] == "costo") return Costo(a[1]);
                    if (a.Length >= 2 && a[0] == "eqdueno") { for (int i = 1; i < a.Length; i++) EqDueno(a[i]); return 0; }
                    if (a.Length >= 2 && a[0] == "eqvivo") { for (int i = 1; i < a.Length; i++) EqVivo(a[i]); return 0; }
                    if (a.Length == 1 && a[0] == "convierte") return Convierte();
                    if (a.Length == 2 && a[0] == "dsee") return Dsee(a[1]);
                    if (a.Length == 1 && a[0] == "noche") return Noche();
                }
                catch (Exception ex) { Console.WriteLine("ERROR " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); return 1; }
                Console.Error.WriteLine("uso: rocola-xp --prueba-cadena-nueva decodifica MP3... | sinhuecos WAV MP3... | mide ARCHIVO... | medidor | ganancia ARCHIVO... | constante | disco MP3 MP3 WAV | arreglos DIR | costo MP3");
                return 2;
            }

            // ---------------- lo que hay alrededor
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct Timespec { public long Seg; public long Nseg; }
            [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "clock_gettime")]
            private static extern int ClockGettime(int reloj, out Timespec t);
            private static double CpuMs() { Timespec t; return ClockGettime(3, out t) == 0 ? t.Seg * 1000.0 + t.Nseg / 1e6 : Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency; }
            private static float[] LeeTodo(ISampleProvider s)
            {
                List<float> l = new List<float>(1 << 20);
                float[] b = new float[8192]; int n;
                while ((n = s.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) l.Add(b[i]);
                return l.ToArray();
            }
            // un arreglo entrelazado estereo como fuente
            private sealed class DeArreglo : ISampleProvider
            {
                private readonly float[] d; private int p; private readonly WaveFormat f;
                internal DeArreglo(float[] datos, int tasa, WaveFormat formato = null) { d = datos; f = formato ?? WaveFormat.CreateIeeeFloatWaveFormat(tasa, 2); }
                public WaveFormat WaveFormat { get { return f; } }
                public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }
                public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); if (n <= 0) return 0; Array.Copy(d, p, b, o, n); p += n; return n; }
            }
            private static float[] LeeWav(string ruta, out int tasa)
            {
                using (WaveFileReader w = new WaveFileReader(ruta))
                {
                    tasa = w.WaveFormat.SampleRate;
                    ISampleProvider s = new NAudio.Wave.SampleProviders.SampleChannel(w, false);
                    float[] x = LeeTodo(s);
                    if (w.WaveFormat.Channels == 1) { float[] y = new float[x.Length * 2]; for (int i = 0; i < x.Length; i++) { y[2 * i] = x[i]; y[2 * i + 1] = x[i]; } return y; }
                    return x;
                }
            }
            private static void EscribeWav(string ruta, float[] x, int tasa)
            {
                using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(tasa, 2))) w.WriteSamples(x, 0, x.Length);
            }
            private static double Db(double lin) { return lin > 1e-12 ? 20 * Math.Log10(lin) : -240; }
            private static RocolaOsMedidaNueva MideArreglo(float[] x, int tasa)
            {
                RocolaOsMedidor m = new RocolaOsMedidor(tasa);
                m.Procesa(x, 0, x.Length);
                RocolaOsMedidaNueva r = new RocolaOsMedidaNueva(); m.Cierra(r); return r;
            }
            // la cadena nueva A entera sin la rocola (sin el pulso ni la noche: owner null), a una ganancia dada
            private static ISampleProvider CadenaA(ISampleProvider x, RocolaOsReceta r, double gDb)
            {
                x = r.Anota(new RocolaOsPiezaEntrada(x, null, false));
                x = RocolaOsNucleo(r, x, r.ZumbidoHz);
                x = r.Anota(new RocolaOsEqDuenoPieza(x, null, false, null));
                return r.Anota(new RocolaOsGananciaPieza(x, null, gDb));
            }

            // ---------------- decodifica
            private static void Decodifica(string ruta)
            {
                float[] v16, vf;
                using (Mp3FileReader r = AbreMp3(ruta)) v16 = LeeTodo(new NAudio.Wave.SampleProviders.SampleChannel(r, false));
                RocolaOsFuenteFlotante f = new RocolaOsFuenteFlotante(ruta, false, false);
                try { vf = LeeTodo(f); } finally { f.Dispose(); }
                int n = Math.Min(v16.Length, vf.Length);
                double pf = 0, p16 = 0, e = 0, s = 0; long sobre = 0, tope16 = 0;
                for (int i = 0; i < vf.Length; i++) { double a = Math.Abs(vf[i]); if (a > pf) pf = a; if (a > 1.0) sobre++; }
                for (int i = 0; i < v16.Length; i++) { double a = Math.Abs(v16[i]); if (a > p16) p16 = a; if (a >= 32767f / 32768f) tope16++; }
                for (int i = 0; i < n; i++) { double d = vf[i] - v16[i]; e += d * d; s += vf[i] * (double)vf[i]; }
                Console.WriteLine("DECODIFICA " + Path.GetFileName(ruta) + " motor=" + RocolaOsDescompresorFlotante.Motor.Replace(' ', '_') + " muestras_16=" + v16.Length + " muestras_f=" + vf.Length
                    + " pico_f_db=" + N(Db(pf), "0.00") + " pico_16_db=" + N(Db(p16), "0.00") + " sobre_0dbfs_f=" + sobre + " al_tope_16=" + tope16
                    + " dif_db=" + N(10 * Math.Log10(Math.Max(1e-30, e / Math.Max(1, n))), "0.0") + " dif_rel_db=" + N(10 * Math.Log10(Math.Max(1e-30, e / Math.Max(1e-30, s))), "0.0")
                    + " cabeceras_saltadas=" + (f.Descompresor != null ? f.Descompresor.CabecerasSaltadas : -1));
            }

            // ---------------- sinhuecos
            private static void SinHuecos(string wav, string mp3)
            {
                int tasa; float[] o = LeeWav(wav, out tasa);
                IAudioSource s = ComoEstereo(new RocolaOsFuenteFlotante(mp3, false, false));
                string como;
                s = RocolaOsSinHuecos(mp3, s, out como);
                float[] d;
                try { d = LeeTodo(s); } finally { s.Dispose(); }
                RocolaOsMarcaGapless m; RocolaOsGapless.Lee(mp3, out m);
                // el desfase: la correlacion del canal izquierdo en los primeros 3 s, de -3000 a +3000 muestras
                int fr = Math.Min(o.Length, d.Length) / 2, mejor = 0; double mejorC = double.MinValue;
                int ventana = Math.Min(fr - 3200, tasa * 3);
                for (int k = -3000; k <= 3000; k++)
                {
                    double c = 0;
                    for (int i = 3200; i < 3200 + ventana; i += 2) { int j = i + k; if (j < 0 || j >= fr) continue; c += o[2 * i] * (double)d[2 * j]; }
                    if (c > mejorC) { mejorC = c; mejor = k; }
                }
                // el error con el desfase quitado (en lo que se solapa)
                double e = 0, p = 0;
                for (int i = 3200; i < fr - 3200; i++) { int j = i + mejor; if (j < 0 || j >= d.Length / 2) continue; double q = d[2 * j] - o[2 * i]; e += q * q; p += o[2 * i] * (double)o[2 * i]; }
                Console.WriteLine("SINHUECOS " + Path.GetFileName(mp3) + " marca=" + (m.Marca ?? "ninguna") + " version=" + m.Version + " retraso=" + m.Retraso + " relleno=" + m.Relleno
                    + " como=" + como.Replace(' ', '_') + " extra=" + RocolaOsExtraFlotante() + " desfase=" + mejor + " largo_wav=" + o.Length / 2 + " largo_mp3=" + d.Length / 2
                    + " sobra=" + (d.Length / 2 - o.Length / 2) + " error_db=" + N(10 * Math.Log10(Math.Max(1e-30, e / Math.Max(1e-30, p))), "0.0"));
            }

            // ---------------- mide
            private static void Mide(string ruta)
            {
                double c0 = CpuMs(); Stopwatch sw = Stopwatch.StartNew();
                RocolaOsMedidaNueva m = RocolaOsMideCancion(ruta, 'A', 0, null, null);
                double cpu = CpuMs() - c0, ms = sw.Elapsed.TotalMilliseconds;
                if (m == null) { Console.WriteLine("MIDE " + Path.GetFileName(ruta) + " fallo=si"); return; }
                StringBuilder sb = new StringBuilder();
                RocolaOsMedidaTabla.Renglon(sb, m);
                Console.WriteLine("MIDE " + Path.GetFileName(ruta) + " lufs=" + N(m.Lufs, "0.00") + " tp=" + N(m.TpDb, "0.00") + " pico=" + N(m.PicoDb, "0.00") + " lra=" + N(m.Lra, "0.0")
                    + " mudo=" + m.CanalMudo + " invertido=" + (m.Invertido ? 1 : 0) + " continuaL=" + N(m.ContinuaL, "0.0000") + " continuaR=" + N(m.ContinuaR, "0.0000")
                    + " segundos=" + N(m.Segundos, "0.00") + " tercios=" + (m.Tercios != null ? m.Tercios.Length : 0) + " ms=" + N(ms, "0") + " cpu_ms=" + N(cpu, "0")
                    + " veces_tiempo_real=" + N(m.Segundos * 1000.0 / Math.Max(1, cpu), "0") + " renglon_campos=" + sb.ToString().TrimEnd('\n').Split('\t').Length);
            }

            // ---------------- medidor
            private static float[] Seno(int tasa, double hz, double amp, double fase, double seg)
            {
                int n = (int)(tasa * seg); float[] x = new float[2 * n];
                int f = tasa / 100;   // (10 ms de entrada y de salida: un corte seco subiria el pico real en los bordes)
                for (int i = 0; i < n; i++)
                {
                    double e = i < f ? i / (double)f : i >= n - f ? (n - 1 - i) / (double)f : 1.0;
                    float v = (float)(e * amp * Math.Sin(2 * Math.PI * hz * i / tasa + fase)); x[2 * i] = v; x[2 * i + 1] = v;
                }
                return x;
            }
            internal static float[] Rosa(int tasa, double seg, int semilla, bool mono)
            {
                // ruido rosa por el filtro de Paul Kellet, normalizado a RMS 0,1 por canal
                Random rnd = new Random(semilla);
                int n = (int)(tasa * seg); float[] x = new float[2 * n];
                for (int ch = 0; ch < (mono ? 1 : 2); ch++)
                {
                    double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, s2 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double w = rnd.NextDouble() * 2 - 1;
                        b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
                        b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
                        double v = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
                        x[2 * i + ch] = (float)v; s2 += v * v;
                    }
                    double k = 0.1 / Math.Sqrt(s2 / n);
                    for (int i = 0; i < n; i++) x[2 * i + ch] = (float)(x[2 * i + ch] * k);
                }
                if (mono) for (int i = 0; i < n; i++) x[2 * i + 1] = x[2 * i];
                return x;
            }
            private static int Medidor()
            {
                int tasa = 44100;
                // 1) un seno de 997 Hz a -20 dBFS en los dos canales: -20,0 LUFS (BS.1770: un seno de 1 kHz en un canal a 0 dBFS da -3,01)
                RocolaOsMedidaNueva s1 = MideArreglo(Seno(tasa, 997, 0.1, 0, 20), tasa);
                Console.WriteLine("MEDIDOR seno997 lufs=" + N(s1.Lufs, "0.00") + " tp=" + N(s1.TpDb, "0.00") + " pico=" + N(s1.PicoDb, "0.00") + " lra=" + N(s1.Lra, "0.00"));
                // 2) un seno en fs/4 con fase de 45 grados a 0,5: las muestras caen en +-0,354 (-9,03 dBFS) y el pico de verdad entre ellas es 0,5 (-6,02)
                RocolaOsMedidaNueva s2 = MideArreglo(Seno(tasa, tasa / 4.0, 0.5, Math.PI / 4, 5), tasa);
                Console.WriteLine("MEDIDOR fs4 tp=" + N(s2.TpDb, "0.00") + " pico=" + N(s2.PicoDb, "0.00"));
                // y uno cerca de fs/4 (11 kHz) a -1 dBFS con fase al azar: el pico real a 0,2 dB o menos del de verdad (4x)
                RocolaOsMedidaNueva s3 = MideArreglo(Seno(tasa, 11000, Math.Pow(10, -1 / 20.0), 0.3, 5), tasa);
                Console.WriteLine("MEDIDOR 11k tp=" + N(s3.TpDb, "0.00") + " pico=" + N(s3.PicoDb, "0.00"));
                // 3) ruido rosa estereo (sin correlacion) y uno a 48 kHz: contra MedidorBs1770 de XP (la misma norma)
                foreach (int t in new int[] { 44100, 48000 })
                {
                    float[] x = Rosa(t, 30, 7, false);
                    // con escalones (para el LRA): 10 s a 0 dB, 10 s a -10, 10 s a -5
                    int n = x.Length / 2;
                    for (int i = n / 3; i < 2 * n / 3; i++) { x[2 * i] *= 0.316f; x[2 * i + 1] *= 0.316f; }
                    for (int i = 2 * n / 3; i < n; i++) { x[2 * i] *= 0.562f; x[2 * i + 1] *= 0.562f; }
                    RocolaOsMedidaNueva r = MideArreglo(x, t);
                    MedidorBs1770 xp = new MedidorBs1770(t, new double[] { 1, 1 });
                    xp.Procesa(x, 0, x.Length);
                    double lx; int us; xp.Integrada(out lx, out us);
                    double lrax; xp.Lra(out lrax);
                    Console.WriteLine("MEDIDOR rosa" + t + " lufs=" + N(r.Lufs, "0.000") + " xp=" + N(lx, "0.000") + " lra=" + N(r.Lra, "0.00") + " lra_xp=" + N(lrax, "0.00") + " tp=" + N(r.TpDb, "0.00") + " pico=" + N(r.PicoDb, "0.00"));
                }
                // 4) silencio: muda
                RocolaOsMedidaNueva s4 = MideArreglo(new float[2 * tasa * 5], tasa);
                Console.WriteLine("MEDIDOR silencio muda=" + (s4.Muda ? 1 : 0));
                return 0;
            }

            // ---------------- ganancia: medir, nivelar, medir lo que sale
            private static void Ganancia(string ruta)
            {
                RocolaOsMedidaNueva m = RocolaOsMideCancion(ruta, 'A', 0, null, null);
                if (m == null) { Console.WriteLine("GANANCIA " + Path.GetFileName(ruta) + " fallo=si"); return; }
                double g = RocolaOsG(m.Lufs, m.TpDb);
                IAudioSource s = RocolaOsAbreParaMedir(ruta);
                RocolaOsReceta r = new RocolaOsReceta { Cadena = 'A', Ruta = ruta, Tasa = s.WaveFormat.SampleRate, CanalMudo = m.CanalMudo, Invertido = m.Invertido, ContinuaL = m.ContinuaL, ContinuaR = m.ContinuaR };
                float[] y;
                try { y = LeeTodo(CadenaA(s, r, g)); } finally { s.Dispose(); }
                RocolaOsMedidaNueva o = MideArreglo(y, r.Tasa);
                string sale = Environment.GetEnvironmentVariable("ROCOLA_CN_SALIDA");   // (la prueba lo compara con el ebur128 de ffmpeg)
                if (!string.IsNullOrEmpty(sale)) EscribeWav(Path.Combine(sale, Path.GetFileNameWithoutExtension(ruta) + ".sale.wav"), y, r.Tasa);
                Console.WriteLine("GANANCIA " + Path.GetFileName(ruta) + " lufs=" + N(m.Lufs, "0.00") + " tp=" + N(m.TpDb, "0.00") + " g=" + N(g, "0.00") + " sale_lufs=" + N(o.Lufs, "0.00")
                    + " sale_tp=" + N(o.TpDb, "0.00") + " objetivo=" + N(RocolaOsCadenasNuevas.T, "0") + " techo=" + N(RocolaOsCadenasNuevas.C, "0") + " cabe=" + (RocolaOsCadenasNuevas.T - m.Lufs <= RocolaOsCadenasNuevas.C - m.TpDb ? 1 : 0));
            }

            // ---------------- constante: la ganancia por bloque de 10 ms
            private static int Constante()
            {
                int tasa = 44100;
                float[] x = Rosa(tasa, 24, 11, true);
                int n = x.Length / 2, tramo = 2 * tasa;
                for (int i = 0; i < n; i++) { int k = (i / tramo) % 3; float f = k == 0 ? 1f : k == 1 ? 3.162f : 0.3162f; x[2 * i] *= f; x[2 * i + 1] *= f; }   // 0, +10, -10 dB cada 2 s
                RocolaOsMedidaNueva m = MideArreglo(x, tasa);
                double g = RocolaOsG(m.Lufs, m.TpDb);
                RocolaOsReceta r = new RocolaOsReceta { Cadena = 'A', Ruta = "rosa", Tasa = tasa };
                float[] y = LeeTodo(CadenaA(new DeArreglo(x, tasa), r, g));
                Bloques("CONSTANTE nueva", x, y, tasa, g);
                // la de hoy, lo que mueve el nivel: el limitador de pico real (PicoReal.cs, techo 0,88) con la ganancia de hoy (al limite: el pico
                // a 6 dB del techo, GananciaAlLimite) sobre los mismos escalones
                float pico = 0; foreach (float v in x) pico = Math.Max(pico, Math.Abs(v));
                double gHoy = 20 * Math.Log10(TechoPicoReal / pico) + 6.0;
                float gl = (float)Math.Pow(10, gHoy / 20);
                float[] xg = new float[x.Length]; for (int i = 0; i < x.Length; i++) xg[i] = x[i] * gl;
                LimitadorPicoReal lim = new LimitadorPicoReal(new DeArreglo(xg, tasa), TechoPicoReal, 3.0);
                float[] z = LeeTodo(lim);
                // (el limitador retrasa: se compara con la entrada retrasada lo mismo)
                int ret = lim.DelayFrames;
                float[] zz = new float[z.Length - 2 * ret]; Array.Copy(z, 2 * ret, zz, 0, zz.Length);
                Bloques("CONSTANTE hoy", x, zz, tasa, gHoy);
                return 0;
            }
            private static void Bloques(string que, float[] x, float[] y, int tasa, double gDb)
            {
                int b = tasa / 100, n = Math.Min(x.Length, y.Length) / 2;
                double mn = double.MaxValue, mx = double.MinValue; int bloques = 0;
                for (int i0 = b; i0 + b < n - b; i0 += b)
                {
                    double ex = 0, ey = 0;
                    for (int i = i0; i < i0 + b; i++) { ex += x[2 * i] * (double)x[2 * i]; ey += y[2 * i] * (double)y[2 * i]; }
                    if (ex < 1e-12) continue;
                    double gb = 10 * Math.Log10(ey / ex);
                    if (gb < mn) mn = gb; if (gb > mx) mx = gb; bloques++;
                }
                Console.WriteLine(que + " g=" + N(gDb, "0.000") + " bloques=" + bloques + " min_db=" + N(mn, "0.000") + " max_db=" + N(mx, "0.000") + " vaiven_db=" + N(mx - mn, "0.000"));
            }

            // ---------------- disco: dos pistas seguidas con el disco continuo
            // y: la primera por su cadena y la segunda por la suya, que sigue a la primera (Sigue, como el relevo); z: las dos decodificadas
            // seguidas por UNA cadena (sin union). La diferencia es lo que hace la union; el largo, que no hay hueco.
            private static int Disco(string mp3a, string mp3b, string wav)
            {
                int tasa; float[] o = LeeWav(wav, out tasa);
                float[] ya, yb, da, db; double g = -3.0;
                RocolaOsReceta ra = new RocolaOsReceta { Cadena = 'A', Tasa = tasa }, rb = new RocolaOsReceta { Cadena = 'A', Tasa = tasa };
                IAudioSource sa = RocolaOsAbreParaMedir(mp3a), sb = RocolaOsAbreParaMedir(mp3b);
                try
                {
                    ISampleProvider ca = CadenaA(sa, ra, g), cb = CadenaA(sb, rb, g);
                    ya = LeeTodo(ca);
                    RocolaOsSiguePiezas(rb.Piezas, ra.Piezas);
                    yb = LeeTodo(cb);
                }
                finally { sa.Dispose(); sb.Dispose(); }
                sa = RocolaOsAbreParaMedir(mp3a); sb = RocolaOsAbreParaMedir(mp3b);
                try { da = LeeTodo(sa); db = LeeTodo(sb); } finally { sa.Dispose(); sb.Dispose(); }
                float[] y = new float[ya.Length + yb.Length]; Array.Copy(ya, y, ya.Length); Array.Copy(yb, 0, y, ya.Length, yb.Length);
                float[] d = new float[da.Length + db.Length]; Array.Copy(da, d, da.Length); Array.Copy(db, 0, d, da.Length, db.Length);
                RocolaOsReceta rr = new RocolaOsReceta { Cadena = 'A', Tasa = tasa };
                float[] z = LeeTodo(CadenaA(new DeArreglo(d, tasa), rr, g));
                int n = Math.Min(y.Length, z.Length) / 2, union = ya.Length / 2;
                double eUnion = 0, eResto = 0, p = 0, maxUnion = 0; int nu = 0, nr = 0;
                for (int i = 0; i < n; i++)
                    for (int ch = 0; ch < 2; ch++)
                    {
                        double q = y[2 * i + ch] - z[2 * i + ch];
                        if (Math.Abs(i - union) < 2205) { eUnion += q * q; nu++; maxUnion = Math.Max(maxUnion, Math.Abs(q)); } else { eResto += q * q; nr++; }
                        p += z[2 * i + ch] * (double)z[2 * i + ch];
                    }
                double pm = p / Math.Max(1, 2 * n);
                Console.WriteLine("DISCO union=" + union + " largo=" + y.Length / 2 + " largo_wav=" + o.Length / 2 + " largo_una=" + z.Length / 2
                    + " err_union_db=" + N(10 * Math.Log10(Math.Max(1e-30, eUnion / Math.Max(1, nu) / Math.Max(1e-30, pm))), "0.0")
                    + " err_resto_db=" + N(10 * Math.Log10(Math.Max(1e-30, eResto / Math.Max(1, nr) / Math.Max(1e-30, pm))), "0.0")
                    + " err_union_max_db=" + N(Db(maxUnion / Math.Sqrt(Math.Max(1e-30, pm))), "0.0"));
                return 0;
            }

            // ---------------- arreglos
            private static int Arreglos(string dir)
            {
                int tasa = 44100;
                Directory.CreateDirectory(dir);
                float[] baseX = Rosa(tasa, 20, 3, false);
                // grave comun (para la correlacion): 60 Hz en los dos canales
                for (int i = 0; i < baseX.Length / 2; i++) { float b = (float)(0.2 * Math.Sin(2 * Math.PI * 60 * i / tasa)); baseX[2 * i] += b; baseX[2 * i + 1] += b; }
                Dictionary<string, float[]> casos = new Dictionary<string, float[]>();
                float[] inv = (float[])baseX.Clone(); for (int i = 0; i < inv.Length / 2; i++) inv[2 * i + 1] = -inv[2 * i + 1]; casos["invertido"] = inv;
                float[] mudo = (float[])baseX.Clone(); for (int i = 0; i < mudo.Length / 2; i++) mudo[2 * i] *= 0.001f; casos["mudo"] = mudo;
                float[] dc = (float[])baseX.Clone(); for (int i = 0; i < dc.Length / 2; i++) { dc[2 * i] += 0.02f; dc[2 * i + 1] -= 0.01f; } casos["continua"] = dc;
                casos["sano"] = baseX;
                foreach (KeyValuePair<string, float[]> kv in casos)
                {
                    string p = Path.Combine(dir, kv.Key + ".wav");
                    EscribeWav(p, kv.Value, tasa);
                    RocolaOsMedidaNueva m = RocolaOsMideCancion(p, 'A', 0, null, null);
                    // lo que sale de la cadena con los arreglos puestos: la correlacion del grave y lo que hay en el canal izquierdo
                    IAudioSource s = RocolaOsAbreParaMedir(p);
                    RocolaOsReceta r = new RocolaOsReceta { Cadena = 'A', Tasa = tasa, CanalMudo = m.CanalMudo, Invertido = m.Invertido, ContinuaL = m.ContinuaL, ContinuaR = m.ContinuaR };
                    float[] y; try { y = LeeTodo(CadenaA(s, r, 0)); } finally { s.Dispose(); }
                    RocolaOsCrudo c = new RocolaOsCrudo(tasa); c.Procesa(y, 0, y.Length);
                    double sl = 0, sr = 0, el = 0, er = 0; int n = y.Length / 2;
                    for (int i = 0; i < n; i++) { sl += y[2 * i]; sr += y[2 * i + 1]; el += y[2 * i] * (double)y[2 * i]; er += y[2 * i + 1] * (double)y[2 * i + 1]; }
                    Console.WriteLine("ARREGLOS " + kv.Key + " mudo=" + m.CanalMudo + " invertido=" + (m.Invertido ? 1 : 0) + " continuaL=" + N(m.ContinuaL, "0.0000") + " continuaR=" + N(m.ContinuaR, "0.0000")
                        + " lufs=" + N(m.Lufs, "0.00") + " sale_correlacion=" + N(c.Correlacion, "0.00") + " sale_continuaL=" + N(sl / n, "0.0000") + " sale_continuaR=" + N(sr / n, "0.0000")
                        + " sale_izq_db=" + N(10 * Math.Log10(Math.Max(1e-30, el / n)), "0.0") + " sale_der_db=" + N(10 * Math.Log10(Math.Max(1e-30, er / n)), "0.0"));
                }
                return 0;
            }

            // ---------------- costo
            private static int Costo(string mp3)
            {
                double mejorNueva = double.MaxValue, mejorHoy = double.MaxValue, mejorDecF = double.MaxValue, mejorDec16 = double.MaxValue, seg = 0;
                for (int vuelta = 0; vuelta < 5; vuelta++)
                {
                    // la nueva: decodificar en coma flotante y sin huecos, la cadena A entera
                    double c0 = CpuMs();
                    IAudioSource s = RocolaOsAbreParaMedir(mp3);
                    RocolaOsReceta r = new RocolaOsReceta { Cadena = 'A', Tasa = s.WaveFormat.SampleRate };
                    int n = Cuenta(CadenaA(s, r, -6)); s.Dispose();
                    double ms = CpuMs() - c0; seg = n / 2.0 / r.Tasa;
                    mejorNueva = Math.Min(mejorNueva, ms);
                    c0 = CpuMs(); s = RocolaOsAbreParaMedir(mp3); Cuenta(s); s.Dispose(); mejorDecF = Math.Min(mejorDecF, CpuMs() - c0);
                    // hoy (lo que se arma sin la rocola): 16 bits, grave en mono, el local, el bajo psicoacustico, el EQ del dueno y el limitador
                    c0 = CpuMs();
                    Mp3FileReader m = AbreMp3(mp3);
                    ISampleProvider x = new NAudio.Wave.SampleProviders.SampleChannel(m, false);
                    x = new GraveMono(x, GraveMono.HzDeFabrica, GraveMono.AnchoDeFabrica, GraveMono.SubDeFabrica, false, false);
                    x = new SalaLocal(x, null, 0.26f, false, false, 0, 0, 0);
                    x = new DeepBassProvider(x, 0.30f);
                    x = new EcualizadorDueno(x, new int[EcualizadorDueno.Bandas]);
                    x = new LimitadorPicoReal(x, TechoPicoReal, 3.0);
                    Cuenta(x); m.Dispose();
                    mejorHoy = Math.Min(mejorHoy, CpuMs() - c0);
                    c0 = CpuMs(); m = AbreMp3(mp3); Cuenta(new NAudio.Wave.SampleProviders.SampleChannel(m, false)); m.Dispose(); mejorDec16 = Math.Min(mejorDec16, CpuMs() - c0);
                }
                Console.WriteLine("COSTO " + Path.GetFileName(mp3) + " segundos=" + N(seg, "0.0") + " nueva_ms_por_s=" + N(mejorNueva / seg, "0.000") + " hoy_ms_por_s=" + N(mejorHoy / seg, "0.000")
                    + " decodifica_f_ms_por_s=" + N(mejorDecF / seg, "0.000") + " decodifica_16_ms_por_s=" + N(mejorDec16 / seg, "0.000")
                    + " cadena_nueva_ms_por_s=" + N((mejorNueva - mejorDecF) / seg, "0.000") + " cadena_hoy_ms_por_s=" + N((mejorHoy - mejorDec16) / seg, "0.000"));
                return 0;
            }
            private static int Cuenta(ISampleProvider s) { float[] b = new float[8192]; int n, t = 0; while ((n = s.Read(b, 0, b.Length)) > 0) t += n; return t; }

            // ================================================================ los arreglos del 28-11-2026 (CADENA_NUEVA_A.md «Arreglos»)
            // ---------------- eqdueno (a1): cuanto se supone que sube el pico con la curva del dueno, frente a lo que sube de verdad
            private static readonly string[] CurvasNombre = { "63+6", "31+4_63+4", "4k+3", "63+6_125+6", "2k4k8k+12", "todo+6", "16k+6" };
            private static int[] CurvaDe(string n)
            {
                int[] e = new int[EcualizadorDueno.Bandas];
                switch (n)
                {
                    case "63+6": e[1] = 12; break;
                    case "31+4_63+4": e[0] = 8; e[1] = 8; break;
                    case "4k+3": e[7] = 6; break;
                    case "63+6_125+6": e[1] = 12; e[2] = 12; break;
                    case "2k4k8k+12": e[6] = 24; e[7] = 24; e[8] = 24; break;
                    case "todo+6": for (int b = 0; b < e.Length; b++) e[b] = 12; break;
                    case "16k+6": e[9] = 12; break;
                }
                return e;
            }
            private static void EqDueno(string ruta)
            {
                RocolaOsMedidaNueva m = RocolaOsMideCancion(ruta, 'A', 0, null, null);
                if (m == null) { Console.WriteLine("EQDUENO " + Path.GetFileName(ruta) + " fallo=si"); return; }
                Console.WriteLine("EQDUENO " + Path.GetFileName(ruta) + " curva=ninguna lufs=" + N(m.Lufs, "0.00") + " tp=" + N(m.TpDb, "0.00") + " picos_eq=" + (m.PicosEq != null ? string.Join(";", Array.ConvertAll(m.PicosEq, v => N(v - m.TpDb, "0.0"))) : "-"));
                foreach (string n in CurvasNombre)
                {
                    int[] eq = CurvaDe(n);
                    IAudioSource s = RocolaOsAbreParaMedir(ruta);
                    RocolaOsReceta r = new RocolaOsReceta { Cadena = 'A', Ruta = ruta, Tasa = s.WaveFormat.SampleRate, CanalMudo = m.CanalMudo, Invertido = m.Invertido, ContinuaL = m.ContinuaL, ContinuaR = m.ContinuaR };
                    double dl, dtp, ot;
                    RocolaOsDespuesCon(r, eq, m.Tercios, m.TpDb, m.PicosEq, out dl, out dtp, out ot);
                    double gN = RocolaOsG(m.Lufs + dl, m.TpDb + dtp);
                    double dtpHoy = -EcualizadorDueno.PreganDb(eq, r.Tasa);                       // lo de antes: lo maximo que sube la curva
                    double gH = RocolaOsG(m.Lufs + dl, m.TpDb + dtpHoy);
                    // lo que sale de verdad con la curva (a ganancia 0: la ganancia es lineal y no hay limitador)
                    float[] y;
                    try
                    {
                        ISampleProvider x = r.Anota(new RocolaOsPiezaEntrada(s, null, false));
                        x = RocolaOsNucleo(r, x, 0);
                        x = r.Anota(new RocolaOsEqDuenoPieza(x, null, false, eq));
                        y = LeeTodo(x);
                    }
                    finally { s.Dispose(); }
                    RocolaOsMedidaNueva o = MideArreglo(y, r.Tasa);
                    // la sonoridad sin espectro (sin nada medido: el de un ruido rosa) frente a lo de antes (lo maximo de la curva)
                    double dlSin = RocolaOsDeltaL(null, EcualizadorDueno.Respuesta(eq, r.Tasa, RocolaOsHzTercios()), r.Tasa);
                    Console.WriteLine("EQDUENO " + Path.GetFileName(ruta) + " curva=" + n + " sube_real=" + N(o.TpDb - m.TpDb, "0.00") + " sube_nueva=" + N(dtp, "0.00") + " sube_hoy=" + N(dtpHoy, "0.00")
                        + " g_nueva=" + N(gN, "0.00") + " g_hoy=" + N(gH, "0.00") + " sale_tp_nueva=" + N(o.TpDb + gN, "0.00") + " sale_tp_hoy=" + N(o.TpDb + gH, "0.00")
                        + " sale_lufs_nueva=" + N(o.Lufs + gN, "0.00") + " sale_lufs_hoy=" + N(o.Lufs + gH, "0.00") + " dl_sin_espectro=" + N(dlSin, "0.00") + " dl_sin_espectro_hoy=" + N(dtpHoy, "0.00"));
                }
            }

            // ---------------- eqvivo (a1): el dueno sube la curva a media cancion; con el tope, la cancion no pasa el techo
            private sealed class Contador : ISampleProvider
            {
                private readonly ISampleProvider s; internal long Leidos;
                internal Contador(ISampleProvider x) { s = x; }
                public WaveFormat WaveFormat { get { return s.WaveFormat; } }
                public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }
                public int Read(float[] b, int o, int c) { int n = s.Read(b, o, c); if (n > 0) Leidos += n; return n; }
            }
            private static void EqVivo(string ruta)
            {
                RocolaOsMedidaNueva m = RocolaOsMideCancion(ruta, 'A', 0, null, null);
                if (m == null) { Console.WriteLine("EQVIVO " + Path.GetFileName(ruta) + " fallo=si"); return; }
                int[] plano = new int[EcualizadorDueno.Bandas], sube = CurvaDe("2k4k8k+12");
                foreach (bool conTope in new[] { true, false })
                {
                    IAudioSource s = RocolaOsAbreParaMedir(ruta);
                    RocolaOsReceta r = new RocolaOsReceta { Cadena = 'A', Ruta = ruta, Tasa = s.WaveFormat.SampleRate };
                    double g = RocolaOsAnota(r, m.Lufs, m.TpDb, m.PicosEq, 0, 0);   // con el EQ plano al armarse
                    long mitad = (long)(m.Segundos * r.Tasa);   // (muestras: la mitad de los cuadros por dos canales)
                    float[] y;
                    try
                    {
                        Contador k = new Contador(s);
                        ISampleProvider x = r.Anota(new RocolaOsPiezaEntrada(k, null, false));
                        x = RocolaOsNucleo(r, x, 0);
                        RocolaOsEqDuenoPieza e = r.Anota(new RocolaOsEqDuenoPieza(x, null, false, plano));
                        e.Pide = () => k.Leidos >= mitad ? sube : plano;
                        RocolaOsGananciaPieza gp = r.Anota(new RocolaOsGananciaPieza(e, null, g));
                        if (conTope) { e.TpAntesDb = r.TpAntesEqDb; e.Picos = r.PicosEq; e.OtrosDb = r.DtpOtrosDb; e.CDb = RocolaOsCadenasNuevas.C; e.Tope = gp; }
                        y = LeeTodo(gp);
                        Console.Write("EQVIVO " + Path.GetFileName(ruta) + " tope=" + (conTope ? 1 : 0) + " g=" + N(g, "0.00") + " efectiva_al_final=" + N(gp.EfectivaDb, "0.00") + " topes=" + e.Topes);
                    }
                    finally { s.Dispose(); }
                    int fr = y.Length / 2, h = fr / 2;
                    float[] a = new float[2 * h], b = new float[y.Length - 2 * h];
                    Array.Copy(y, 0, a, 0, a.Length); Array.Copy(y, a.Length, b, 0, b.Length);
                    RocolaOsMedidaNueva ma = MideArreglo(a, r.Tasa), mb = MideArreglo(b, r.Tasa);
                    Console.WriteLine(" tp_primera_mitad=" + N(ma.TpDb, "0.00") + " tp_segunda_mitad=" + N(mb.TpDb, "0.00"));
                }
            }

            // ---------------- convierte (a5): la conversion de frecuencia de la salida de hoy con una cadena nueva, sin el limitador de XP
            private static int Convierte()
            {
                WaveFormat de = FormatoAudio.Flotante(44100, 2, 3), a = FormatoAudio.Flotante(48000, 2, 3);
                double[] tabla = null;
                for (int i = 0; i < 40 && (tabla = RemuestreoContinuo.CapturaTabla(de, a)) == null; i++) System.Threading.Thread.Sleep(50);
                if (tabla == null) tabla = TablaComoXp(44100, 48000);   // (el calentador de fondo no la dio a tiempo: la misma cuenta que CreaFiltro de XP)
                float[] x = Seno(44100, 1000, 0.95, 0.3, 3);   // pico 0,95: por encima del techo del limitador de XP (0,88)
                foreach (bool nueva in new[] { false, true })
                {
                    CadenaCancion c = new CadenaCancion();
                    c.Salida = new DeArreglo(x, 44100, de); c.Retardo = 0;
                    if (nueva) rocolaOsArmadas.AddOrUpdate(c, new RocolaOsArmada { Receta = new RocolaOsReceta() });
                    ConvierteSalida(c, a, tabla);
                    float[] y = LeeTodo(c.Salida);
                    float pk = 0; for (int i = 0; i < y.Length; i++) pk = Math.Max(pk, Math.Abs(y[i]));
                    Console.WriteLine("CONVIERTE " + (nueva ? "nueva" : "xp") + " limitador=" + (c.LimiteSalida != null ? 1 : 0) + " retardo=" + c.Retardo + " tasa=" + c.Salida.WaveFormat.SampleRate
                        + " pico_db=" + N(Db(pk), "0.00") + " entra_db=" + N(Db(0.95), "0.00") + " techo_xp_db=" + N(Db(TechoPicoReal), "0.00"));
                }
                return 0;
            }

            // la tabla del convertidor de XP (SinPausa.cs, CreaFiltro: 512 fases, 96 coeficientes por lado, Blackman-Harris de 4 terminos)
            private static double[] TablaComoXp(int tasaIn, int tasaOut)
            {
                const int Fases = 512;
                int mitad = (int)Math.Ceiling(96.0 * Math.Max(1.0, (double)tasaIn / tasaOut)), taps = mitad * 2;
                double[] f = new double[(Fases + 1) * taps];
                double corte = 0.47 * Math.Min(1.0, (double)tasaOut / tasaIn);
                for (int fase = 0; fase <= Fases; fase++)
                {
                    double fr = (double)fase / Fases, suma = 0;
                    for (int j = 0; j < taps; j++)
                    {
                        double dt = fr - (j - mitad + 1), u = dt / mitad, h = 0;
                        if (Math.Abs(u) <= 1)
                        {
                            double q = 2 * corte * dt, sinc = Math.Abs(q) < 1e-12 ? 1 : Math.Sin(Math.PI * q) / (Math.PI * q);
                            h = 2 * corte * sinc * (0.35875 + 0.48829 * Math.Cos(Math.PI * u) + 0.14128 * Math.Cos(2 * Math.PI * u) + 0.01168 * Math.Cos(3 * Math.PI * u));
                        }
                        f[fase * taps + j] = h; suma += h;
                    }
                    for (int j = 0; j < taps; j++) f[fase * taps + j] /= suma;
                }
                return f;
            }

            // ---------------- dsee (a7): el corte medido (fijo por cancion) frente al que sigue a la musica
            private static int Dsee(string dir)
            {
                int tasa = 44100;
                Directory.CreateDirectory(dir);
                // 6 s de ruido con agudos hasta 16 kHz (el corte del codificador) y 6 s apagados (hasta 11 kHz): las partes apagadas no son el corte
                float[] x = Rosa(tasa, 12, 21, false);
                int n = x.Length / 2, mitad = n / 2;
                x = PasoBajoFft(x, tasa, 16000, 0, mitad);
                x = PasoBajoFft(x, tasa, 11000, mitad, n);
                string p = Path.Combine(dir, "dsee.wav"); EscribeWav(p, x, tasa);
                RocolaOsMedidaNueva m = RocolaOsMideCancion(p, 'A', 0, null, null);
                Console.WriteLine("DSEE medida corte=" + N(m.CorteHz, "0"));
                foreach (bool fijo in new[] { true, false })
                {
                    SpectralTap t = new SpectralTap(new DeArreglo(x, tasa));
                    t.Active = true;
                    if (fijo) RocolaOsDseeFija(t, m.CorteHz);
                    float[] y = LeeTodo(t);
                    // en la parte apagada: lo que hay de 11,5 a 15,5 kHz (en la entrada, nada) frente a lo de 4 a 8 kHz
                    double e1 = BandaDb(y, tasa, mitad + tasa / 2, n - tasa / 2, 11500, 15500), e0 = BandaDb(y, tasa, mitad + tasa / 2, n - tasa / 2, 4000, 8000);
                    double en = BandaDb(x, tasa, mitad + tasa / 2, n - tasa / 2, 11500, 15500);
                    // y en la parte con agudos: lo que hay de 16,5 a 19,5 kHz (lo que recrea el DSEE)
                    double r1 = BandaDb(y, tasa, tasa / 2, mitad - tasa / 2, 16500, 19500) - BandaDb(y, tasa, tasa / 2, mitad - tasa / 2, 4000, 8000);
                    Console.WriteLine("DSEE " + (fijo ? "fijo" : "sigue") + " apagada_11k5_15k5_db=" + N(e1 - e0, "0.0") + " entrada_db=" + N(en - e0, "0.0") + " con_agudos_16k5_19k5_db=" + N(r1, "0.0"));
                }
                return 0;
            }
            // (para dsee) paso bajo de ladrillo por FFT de un tramo [a, b) de cuadros
            private static float[] PasoBajoFft(float[] x, int tasa, double hz, int a, int b)
            {
                int n = b - a, M = 1; while (M < n) M <<= 1;
                float[] y = (float[])x.Clone();
                for (int ch = 0; ch < 2; ch++)
                {
                    double[] re = new double[M], im = new double[M];
                    for (int i = 0; i < n; i++) re[i] = x[2 * (a + i) + ch];
                    RocolaOsFft.Hace(re, im, false);
                    for (int k = 0; k < M; k++) { double f = (k <= M / 2 ? k : M - k) * (double)tasa / M; if (f > hz) { re[k] = 0; im[k] = 0; } }
                    RocolaOsFft.Hace(re, im, true);
                    for (int i = 0; i < n; i++) y[2 * (a + i) + ch] = (float)re[i];
                }
                return y;
            }
            private static double BandaDb(float[] x, int tasa, int a, int b, double f0, double f1)
            {
                int N2 = 4096; double e = 0; int cuantas = 0;
                double[] re = new double[N2], im = new double[N2];
                for (int i0 = a; i0 + N2 <= b; i0 += N2)
                {
                    for (int i = 0; i < N2; i++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N2); re[i] = x[2 * (i0 + i)] * w; im[i] = 0; }
                    RocolaOsFft.Hace(re, im, false);
                    for (int k = 1; k < N2 / 2; k++) { double f = k * (double)tasa / N2; if (f >= f0 && f < f1) e += re[k] * re[k] + im[k] * im[k]; }
                    cuantas++;
                }
                return 10 * Math.Log10(Math.Max(1e-30, e / Math.Max(1, cuantas)));
            }

            // ---------------- noche (a8): la pieza de la ganancia lee la noche que publica el reloj, sin llamar a FactorNoche
            private static int Noche()
            {
                int tasa = 44100;
                float[] x = Seno(tasa, 1000, 0.1, 0, 2);
                foreach (float publicada in new[] { 1f, 0.5f })
                {
                    RocolaOsPublicaNoche(publicada);
                    RocolaOsGananciaPieza g = new RocolaOsGananciaPieza(new DeArreglo(x, tasa), null, -6.0) { ConNoche = true };
                    float[] y = LeeTodo(g);
                    double ex = 0, ey = 0; for (int i = tasa; i < y.Length; i++) { ex += x[i] * (double)x[i]; ey += y[i] * (double)y[i]; }
                    Console.WriteLine("NOCHE publicada=" + N(publicada, "0.00") + " sale_db=" + N(10 * Math.Log10(ey / ex), "0.00") + " espera_db=" + N(-6.0 + 20 * Math.Log10(publicada), "0.00"));
                }
                RocolaOsPublicaNoche(1f);
                return 0;
            }
        }
    }
}

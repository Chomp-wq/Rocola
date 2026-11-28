// ROCOLA OS (cadena de audio nueva; CADENA_NUEVA.md §4, decision D12 «medir el PROZOR primero»; CADENA_NUEVA_salida.md): MEDIR EL CONVERTIDOR.
// Las medidas publicadas de un PROZOR (GoldenSound) dicen 5,4 % de distorsion a 0 dBFS y que recorta hasta unos -10 dBFS: si el de la rocola
// es asi, el techo de la cadena nueva (techo_dbtp=, -1 dBTP de fabrica) tiene que bajar. Esto lo mide en la rocola misma, sin aparatos:
//   - por la salida elegida (la de la salida nueva) suenan tonos de 1 kHz a 0, -3, -6, -10 y -20 dBFS (1,6 s cada uno, con 0,6 s de silencio
//     entre uno y otro; sin el volumen del dueno ni la proteccion: tal cual);
//   - a la vez se graba la ENTRADA DE LINEA de la placa (la azul), con un cable RCA desde la salida del PROZOR (el dueno lo conecta);
//   - de cada tono, 1 s del medio: se ajusta una senoidal (amplitud, fase, corriente continua y la frecuencia exacta: los relojes de la
//     salida y de la entrada no son el mismo) y lo que sobra es la distorsion mas el ruido (THD+N); y su nivel en la entrada;
//   - dice desde que nivel deja de recortar (el mas alto con la distorsion cerca de la mejor y sin comprimirse) y PROPONE el techo: ese
//     nivel menos 1 dB (como mucho -1 dBTP). Si la entrada misma recorta o no llega senal, lo dice.
// Desde la hoja Sonido de Ctrl+F4 (con la salida nueva: el tono lo da su motor y la musica sigue su curso en silencio) o desde la consola de
// mantenimiento con «rocola-mide-dac» (= rocola-xp --mide-convertidor, con la rocola cerrada: abre la tarjeta el mismo).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static unsafe class SalidaNuevaMide
    {
        internal static readonly double[] Niveles = { 0, -3, -6, -10, -20 };
        internal const double Hz = 1000.0, InicioSeg = 0.6, TonoSeg = 1.6, PausaSeg = 0.6, RampaSeg = 0.003;
        internal static double TotalSeg { get { return InicioSeg + Niveles.Length * (TonoSeg + PausaSeg); } }

        // ---------- el tono (lo pide el motor en exclusiva, o la orden de consola) ----------
        internal sealed class Generador : ISalidaNuevaGenerador
        {
            private readonly int tasa; private long pos; private readonly long total;
            internal volatile bool Acabo;
            internal Generador(int tasa) { this.tasa = tasa; total = (long)Math.Ceiling(TotalSeg * tasa); }
            internal double Avance { get { return Math.Min(1.0, pos / (double)total); } }
            public int Genera(float[] mezcla, int cuadros)
            {
                for (int c = 0; c < cuadros; c++, pos++)
                {
                    double t = pos / (double)tasa, v = 0;
                    double u = t - InicioSeg;
                    if (u >= 0)
                    {
                        int k = (int)(u / (TonoSeg + PausaSeg));
                        double dentro = u - k * (TonoSeg + PausaSeg);
                        if (k < Niveles.Length && dentro < TonoSeg)
                        {
                            double a = Math.Pow(10.0, Niveles[k] / 20.0), r = 1.0;
                            if (dentro < RampaSeg) r = 0.5 - 0.5 * Math.Cos(Math.PI * dentro / RampaSeg);
                            else if (TonoSeg - dentro < RampaSeg) r = 0.5 - 0.5 * Math.Cos(Math.PI * (TonoSeg - dentro) / RampaSeg);
                            v = a * r * Math.Sin(2 * Math.PI * Hz * dentro);
                            if (v > 1.0) v = 1.0; else if (v < -1.0) v = -1.0;
                        }
                    }
                    mezcla[c * 2] = (float)v; mezcla[c * 2 + 1] = (float)v;
                }
                if (pos >= total) Acabo = true;
                return cuadros;
            }
        }

        // ---------- lo que salio ----------
        internal sealed class Nivel
        {
            internal double Pedido, LlegaDb = double.NaN, ThdN = double.NaN, PicoEntrada;
            internal bool EntradaRecorta, Limpio, Medido;
        }
        internal sealed class Informe
        {
            internal string Salida = "", Entrada = "", Problema = "";
            internal int TasaEntrada;
            internal readonly List<Nivel> Niveles = new List<Nivel>();
            internal double Techo = double.NaN;
            internal string Texto()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Salida: ").Append(Salida).Append(" · entrada: ").Append(Entrada).Append(TasaEntrada > 0 ? " (" + TasaEntrada + " Hz)" : "").Append('\n');
                foreach (Nivel n in Niveles)
                {
                    sb.Append(D(n.Pedido)).Append(" dBFS: ");
                    if (!n.Medido) { sb.Append("no se pudo medir\n"); continue; }
                    sb.Append("llega a ").Append(D(n.LlegaDb)).Append(" dBFS, THD+N ").Append(Pct(n.ThdN)).Append(" (").Append(D(20 * Math.Log10(Math.Max(1e-9, n.ThdN)))).Append(" dB)");
                    if (n.EntradaRecorta) sb.Append(" — la ENTRADA recorta (baja su ganancia)");
                    else sb.Append(n.Limpio ? " — limpio" : " — RECORTA o distorsiona");
                    sb.Append('\n');
                }
                if (Problema.Length > 0) sb.Append(Problema).Append('\n');
                if (!double.IsNaN(Techo)) sb.Append("Propuesta: techo_dbtp=").Append(Techo.ToString("0", CultureInfo.InvariantCulture)).Append(" (el nivel mas alto limpio menos 1 dB)\n");
                return sb.ToString();
            }
        }
        internal static string D(double x) { return double.IsNaN(x) ? "?" : x.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture).Replace('.', ','); }
        internal static string Pct(double x) { return double.IsNaN(x) ? "?" : (x * 100).ToString(x * 100 >= 10 ? "0.0" : x * 100 >= 0.1 ? "0.00" : "0.000", CultureInfo.InvariantCulture).Replace('.', ',') + " %"; }

        // ================================================================== EN LA ROCOLA (con la salida nueva; en un hilo de fondo)
        internal static volatile string Estado = "";
        internal static volatile Informe Ultimo;
        internal static volatile bool Midiendo;
        internal static double Avance;
        // la entrada de linea de la placa: la primera tarjeta que no es HDMI ni USB con una entrada (pcm0c); con su id
        internal static string EntradaAutomatica(out int tarjeta)
        {
            tarjeta = -1;
            string e = null; try { e = Environment.GetEnvironmentVariable("ROCOLA_SALIDA_NUEVA_ENTRADA"); } catch { }
            if (!string.IsNullOrEmpty(e)) return e;
            foreach (RocolaOsSalida s in RocolaOs.SalidasDeSonido())
            {
                if (s.Hdmi || s.Usb || tarjeta == s.Tarjeta) continue;
                string c = System.IO.Path.Combine(AsoundRaiz(), "card" + s.Tarjeta.ToString(CultureInfo.InvariantCulture));
                for (int d = 0; d < 8; d++)
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(c, "pcm" + d + "c")))
                    {
                        tarjeta = s.Tarjeta;
                        string id = SalidaNueva.IdDe(s.Tarjeta);
                        return id.Length > 0 ? "hw:CARD=" + id + ",DEV=" + d : "hw:" + s.Tarjeta + "," + d;
                    }
            }
            return null;
        }
        private static string AsoundRaiz() { string v = null; try { v = Environment.GetEnvironmentVariable("ROCOLA_ASOUND"); } catch { } return string.IsNullOrEmpty(v) ? "/proc/asound" : v; }
        // LA ENTRADA LISTA PARA MEDIR (amixer, best-effort): la de linea como fuente de grabacion, la ganancia en 0 dB y sin refuerzo
        internal static string PreparaEntrada(int tarjeta)
        {
            if (tarjeta < 0) return "";
            string t = tarjeta.ToString(CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder();
            foreach (string o in new[] { "-q sset 'Input Source' Line", "-q sset 'Input Source',0 Line", "-q sset 'Capture Source' Line", "-q sset Capture 0dB cap", "-q sset 'Line Boost' 0", "-q sset 'Line Boost Volume' 0", "-q sset 'Mic Boost' 0" })
                if (SalidaNueva.Amixer("-c " + t + " " + o) != null) sb.Append(o.Substring(8)).Append("; ");
            return sb.ToString();
        }
        internal static bool EmpiezaEnLaRocola()
        {
            if (Midiendo || !SalidaNueva.Va || !SalidaNueva.Abierta || SalidaNueva.Exclusiva != null) return false;
            Midiendo = true; Estado = "Preparando la entrada..."; Avance = 0;
            RocolaOs.Hilo("salida-nueva-mide", ThreadPriority.AboveNormal, delegate
            {
                try { Ultimo = MideEnLaRocola(); }
                catch (Exception ex) { Estado = "Fallo la medida: " + ex.Message; RocolaOs.Falla(SalidaNueva.NombreFuncion, "medir el convertidor", ex); }
                finally { if (SalidaNueva.Exclusiva is Generador) SalidaNueva.Exclusiva = null; Midiendo = false; }
            });
            return true;
        }
        private static Informe MideEnLaRocola()
        {
            Informe inf = new Informe();
            int tarjeta;
            string entrada = EntradaAutomatica(out tarjeta);
            inf.Salida = SalidaNueva.PcmNombre + " (" + SalidaNueva.TextoFormato() + ")";
            if (entrada == null) { inf.Problema = "No hay entrada de linea en la placa (ninguna tarjeta con grabacion que no sea HDMI ni USB)."; Estado = inf.Problema; return inf; }
            inf.Entrada = entrada;
            string puestos = PreparaEntrada(tarjeta);
            if (puestos.Length > 0) RocolaOs.Apunta(SalidaNueva.NombreFuncion, "medida: la entrada " + entrada + ": " + puestos);
            int err; string paso;
            SalidaNuevaPcm cap = AbreEntrada(entrada, out err, out paso);
            if (cap == null) { inf.Problema = "No se pudo abrir la entrada " + entrada + ": " + SalidaNuevaPcm.Texto(err) + " en " + paso; Estado = inf.Problema; return inf; }
            inf.TasaEntrada = cap.Tasa;
            List<float> l = new List<float>(), r = new List<float>();
            Generador g = new Generador(SalidaNueva.Tasa);
            using (cap)
            {
                cap.Arranca();
                long hasta = -1;
                byte[] b = new byte[cap.BytesPorCuadro * 4096];
                long empezo = Environment.TickCount64;
                bool tono = false;
                while (true)
                {
                    long ahora = Environment.TickCount64;
                    if (!tono && ahora - empezo >= 300)
                    {
                        if (SalidaNueva.Exclusiva != null) { inf.Problema = "Hay otra prueba de sonido sonando."; break; }
                        SalidaNueva.ExclusivaConVolumen = false; SalidaNueva.Exclusiva = g; tono = true; Estado = "Sonando los tonos (no toques nada)...";
                    }
                    if (tono && !g.Acabo && !object.ReferenceEquals(SalidaNueva.Exclusiva, g)) { inf.Problema = "Se corto: empezo a sonar una cancion o un video (la musica manda). Vuelve a medir con la rocola callada."; break; }
                    if (tono && g.Acabo && hasta < 0) hasta = ahora + 800 + SalidaNueva.RetrasoTarjetaMs;
                    if (hasta >= 0 && ahora >= hasta) break;
                    if (ahora - empezo > (long)(TotalSeg * 1000) + 15000) { inf.Problema = "La salida nueva no dio los tonos a tiempo (¿se cayo la tarjeta?)."; break; }
                    Avance = g.Avance;
                    if (!LeeUnPoco(cap, b, l, r)) { inf.Problema = "La entrada dejo de grabar."; break; }
                }
            }
            SalidaNueva.SueltaPrueba(g);
            Estado = "Analizando...";
            Analiza(inf, l.ToArray(), r.ToArray(), inf.TasaEntrada);
            Estado = inf.Problema.Length > 0 && inf.Niveles.Count == 0 ? inf.Problema : "Listo.";
            RocolaOs.Apunta(SalidaNueva.NombreFuncion, "medida del convertidor: " + inf.Texto().Replace('\n', ' '));
            return inf;
        }
        private static readonly int[] FormatosEntrada = { SalidaNuevaPcm.S32_LE, SalidaNuevaPcm.S16_LE, SalidaNuevaPcm.S24_3LE };
        internal static SalidaNuevaPcm AbreEntrada(string nombre, out int err, out string paso)
        {
            SalidaNuevaPcm c = null; err = 0; paso = "";
            foreach (int hz in new[] { 44100, 48000 })
            {
                c = SalidaNuevaPcm.Abre(nombre, true, FormatosEntrada, hz, 2, hz / 50, hz / 2, false, out err, out paso);
                if (c != null) return c;
            }
            return null;
        }
        // lo que haya en la entrada (esperando como mucho 50 ms)
        private static bool LeeUnPoco(SalidaNuevaPcm cap, byte[] b, List<float> l, List<float> r)
        {
            int w = cap.Espera(50);
            if (w < 0) return false;
            long disp = cap.Libres();
            if (disp < 0) return false;
            int cuantos = (int)Math.Min(disp, b.Length / cap.BytesPorCuadro);
            if (cuantos <= 0) return true;
            long hechos;
            fixed (byte* p = b) hechos = cap.Lee(p, cuantos);
            if (hechos < 0) return false;
            Agrega(cap, b, (int)hechos, l, r);
            return true;
        }
        private static void Agrega(SalidaNuevaPcm cap, byte[] b, int cuadros, List<float> l, List<float> r)
        {
            int bpm = cap.BytesPorMuestra, ch = cap.Canales;
            for (int c = 0; c < cuadros; c++)
                for (int k = 0; k < Math.Min(2, ch); k++)
                {
                    int o = (c * ch + k) * bpm;
                    float v = cap.Formato == SalidaNuevaPcm.S16_LE ? (short)(b[o] | (b[o + 1] << 8)) / 32768f
                        : cap.Formato == SalidaNuevaPcm.S24_3LE ? (((b[o] | (b[o + 1] << 8) | (b[o + 2] << 16)) << 8) >> 8) / 8388608f
                        : BitConverter.ToInt32(b, o) / 2147483648f;
                    (k == 0 ? l : r).Add(v);
                    if (ch == 1) r.Add(v);
                }
        }

        // ================================================================== EL ANALISIS
        internal static void Analiza(Informe inf, float[] izq, float[] der, int fs)
        {
            inf.Niveles.Clear();
            if (izq.Length < fs) { inf.Problema = "Se grabo muy poco (" + izq.Length + " cuadros)."; return; }
            // el canal que llega mas fuerte (con el cable en uno solo, ese)
            float[] x = Energia(der) > Energia(izq) * 1.2 ? der : izq;
            // donde empieza el primer tono (el mas fuerte): ventanas de 5 ms, el primero que pasa de 1/10 del maximo
            int v = Math.Max(1, fs / 200), nv = x.Length / v;
            double[] rms = new double[nv]; double max = 0;
            for (int i = 0; i < nv; i++)
            {
                double s = 0; for (int j = 0; j < v; j++) { double y = x[i * v + j]; s += y * y; }
                rms[i] = Math.Sqrt(s / v); if (rms[i] > max) max = rms[i];
            }
            if (max < 0.001) { inf.Problema = "No llega senal a la entrada de linea (" + D(20 * Math.Log10(Math.Max(1e-9, max))) + " dBFS): revisa el cable RCA de la salida del convertidor a la entrada AZUL de la placa y que la entrada este en «Line»."; return; }
            int primero = -1;
            for (int i = 0; i < nv; i++) if (rms[i] > max * 0.1) { primero = i; break; }
            double t0 = primero * (double)v / fs;
            double mejor = double.MaxValue;
            for (int k = 0; k < Niveles.Length; k++)
            {
                Nivel n = new Nivel { Pedido = Niveles[k] };
                inf.Niveles.Add(n);
                int a = (int)((t0 + k * (TonoSeg + PausaSeg) + 0.35) * fs), largo = fs;
                if (a < 0 || a + largo > x.Length) continue;
                double pico = 0; for (int i = a; i < a + largo; i++) pico = Math.Max(pico, Math.Abs(x[i]));
                n.PicoEntrada = pico;
                n.EntradaRecorta = pico >= 0.985;
                double amp, thd;
                AjustaSeno(x, a, largo, fs, out amp, out thd);
                n.LlegaDb = 20 * Math.Log10(Math.Max(1e-9, amp));
                n.ThdN = thd; n.Medido = true;
                if (!n.EntradaRecorta && thd < mejor) mejor = thd;
            }
            // limpio: la distorsion cerca de la mejor (el doble, o 0,05 %) y no mas de 1 %, y sin comprimirse frente al de -20 (a 1 dB)
            Nivel bajo = inf.Niveles[inf.Niveles.Count - 1];
            foreach (Nivel n in inf.Niveles)
            {
                if (!n.Medido || n.EntradaRecorta) continue;
                bool lineal = !bajo.Medido || Math.Abs((n.LlegaDb - bajo.LlegaDb) - (n.Pedido - bajo.Pedido)) <= 1.0;
                n.Limpio = n.ThdN <= Math.Max(2 * mejor, 0.0005) && n.ThdN <= 0.01 && lineal;
            }
            // la propuesta: el nivel mas alto limpio (sin uno que no lo sea por encima de el... se toma el primero limpio de arriba abajo), menos 1 dB
            foreach (Nivel n in inf.Niveles)
            {
                if (!n.Medido || n.EntradaRecorta) continue;
                if (n.Limpio) { inf.Techo = Math.Min(-1.0, Math.Round(n.Pedido - 1.0)); break; }
            }
            bool algunaRecorta = false; foreach (Nivel n in inf.Niveles) if (n.EntradaRecorta) algunaRecorta = true;
            if (algunaRecorta) inf.Problema = "La entrada de la placa recorta en los tonos mas fuertes: baja su ganancia (amixer: Capture) y vuelve a medir; lo de esos niveles no vale.";
            else if (double.IsNaN(inf.Techo)) inf.Problema = "Ningun nivel salio limpio (ni a -20 dBFS): revisa la conexion, la tierra (el cargador del convertidor) y la ganancia de la entrada.";
        }
        private static double Energia(float[] x) { double s = 0; for (int i = 0; i < x.Length; i += 7) s += x[i] * (double)x[i]; return s; }

        // la senoidal de la frecuencia mas cercana a 1 kHz (+-1,5 Hz, los relojes) que mejor ajusta, con su continua: su amplitud y lo que sobra
        // (la distorsion y el ruido) frente a ella
        internal static void AjustaSeno(float[] x, int a, int n, int fs, out double amp, out double thdN)
        {
            double mejorF = Hz, mejorR = double.MaxValue;
            for (double f = Hz - 1.5; f <= Hz + 1.5001; f += 0.05) { double r = Residuo(x, a, n, fs, f, out double _); if (r < mejorR) { mejorR = r; mejorF = f; } }
            // afinado: parabola en los tres de alrededor, dos veces mas fino
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                double h = vuelta == 0 ? 0.05 : 0.01;
                double r0 = Residuo(x, a, n, fs, mejorF - h, out double _a), r1 = Residuo(x, a, n, fs, mejorF, out double _b), r2 = Residuo(x, a, n, fs, mejorF + h, out double _c);
                double den = r0 - 2 * r1 + r2;
                if (den > 0) mejorF += h * 0.5 * (r0 - r2) / den;
            }
            double rf = Residuo(x, a, n, fs, mejorF, out amp);
            double fund = amp / Math.Sqrt(2.0);
            thdN = fund > 0 ? Math.Sqrt(Math.Max(0, rf) / n) / fund : double.NaN;
        }
        // minimos cuadrados con cos, sen y 1 a la frecuencia f: la suma de los cuadrados que sobran y la amplitud
        private static double Residuo(float[] x, int a, int n, int fs, double f, out double amp)
        {
            double w = 2 * Math.PI * f / fs, cw = Math.Cos(w), sw = Math.Sin(w);
            double c = 1, s = 0;
            double cc = 0, ss = 0, cs = 0, c1 = 0, s1 = 0, xc = 0, xs = 0, x1 = 0, xx = 0;
            for (int i = 0; i < n; i++)
            {
                double y = x[a + i];
                cc += c * c; ss += s * s; cs += c * s; c1 += c; s1 += s; xc += y * c; xs += y * s; x1 += y; xx += y * y;
                double c2 = c * cw - s * sw; s = s * cw + c * sw; c = c2;
                if ((i & 1023) == 1023) { double m = 1.0 / Math.Sqrt(c * c + s * s); c *= m; s *= m; }
            }
            // [cc cs c1; cs ss s1; c1 s1 n] [A B C] = [xc xs x1]
            double[,] M = { { cc, cs, c1 }, { cs, ss, s1 }, { c1, s1, n } };
            double[] v = { xc, xs, x1 };
            double[] sol = Resuelve3(M, v);
            if (sol == null) { amp = 0; return xx; }
            amp = Math.Sqrt(sol[0] * sol[0] + sol[1] * sol[1]);
            return xx - (sol[0] * xc + sol[1] * xs + sol[2] * x1);
        }
        private static double[] Resuelve3(double[,] m, double[] v)
        {
            double det = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
            if (Math.Abs(det) < 1e-12) return null;
            double[] r = new double[3];
            for (int k = 0; k < 3; k++)
            {
                double[,] t = (double[,])m.Clone();
                for (int i = 0; i < 3; i++) t[i, k] = v[i];
                r[k] = (t[0, 0] * (t[1, 1] * t[2, 2] - t[1, 2] * t[2, 1]) - t[0, 1] * (t[1, 0] * t[2, 2] - t[1, 2] * t[2, 0]) + t[0, 2] * (t[1, 0] * t[2, 1] - t[1, 1] * t[2, 0])) / det;
            }
            return r;
        }

        // ================================================================== DESDE LA CONSOLA (rocola-mide-dac = rocola-xp --mide-convertidor)
        // Con la rocola CERRADA (el guion la cierra conservando su estado y la vuelve a abrir al acabar). Todo en este hilo (las ordenes corren
        // antes del Main: sin otros hilos): la salida y la entrada sin espera, una detras de otra.
        //   rocola-xp --mide-convertidor [--salida PCM] [--entrada PCM]   (sin ellas: la de la salida nueva y la entrada de linea de la placa)
        internal static int Orden(string[] a)
        {
            string salida = null, entrada = null;
            for (int i = 0; i + 1 < a.Length; i += 2) { if (a[i] == "--salida") salida = a[i + 1]; else if (a[i] == "--entrada") entrada = a[i + 1]; }
            int tarjetaEntrada = -1;
            if (salida == null) { SalidaNueva.Eleccion e = SalidaNueva.Elige(); if (e == null) { Console.WriteLine("Linux no ve ninguna tarjeta de sonido."); return 1; } salida = e.Pcm; Console.WriteLine("Salida: " + e.Pcm + " (" + e.Nombre + "; " + e.Porque + ")"); }
            if (entrada == null) { entrada = EntradaAutomatica(out tarjetaEntrada); if (entrada == null) { Console.WriteLine("No hay entrada de linea en la placa."); return 1; } }
            Console.WriteLine("MEDIR EL CONVERTIDOR (unos " + Math.Ceiling(TotalSeg + 2) + " s)");
            Console.WriteLine("  1. Un cable RCA de la SALIDA del convertidor (PROZOR) a la entrada de linea AZUL de la placa (izquierdo y derecho, o solo uno).");
            Console.WriteLine("  2. El amplificador APAGADO o al minimo (suenan tonos de 1 kHz hasta 0 dBFS).");
            if (tarjetaEntrada >= 0)
                Console.WriteLine("  3. La entrada en «Line» y sin refuerzo; si no lo hace solo: amixer -c " + tarjetaEntrada + " sset 'Input Source' Line; amixer -c " + tarjetaEntrada + " sset Capture 0dB cap");
            Console.WriteLine("Entrada: " + entrada);
            int err; string paso;
            SalidaNuevaPcm sal = SalidaNuevaPcm.Abre(salida, false, new[] { SalidaNuevaPcm.S32_LE, SalidaNuevaPcm.S24_3LE, SalidaNuevaPcm.S16_LE }, SalidaNueva.Tasa, 2, SalidaNueva.Tasa / 50, SalidaNueva.Tasa / 5, false, out err, out paso);
            if (sal == null) { Console.WriteLine("No abrio la salida " + salida + ": " + SalidaNuevaPcm.Texto(err) + " en " + paso); return 2; }
            SalidaNuevaPcm cap = AbreEntrada(entrada, out err, out paso);
            if (cap == null) { sal.Dispose(); Console.WriteLine("No abrio la entrada " + entrada + ": " + SalidaNuevaPcm.Texto(err) + " en " + paso); return 2; }
            Informe inf = new Informe { Salida = salida + " (" + SalidaNuevaPcm.NombreFormato(sal.Formato) + ")", Entrada = entrada, TasaEntrada = cap.Tasa };
            List<float> l = new List<float>(), r = new List<float>();
            Generador g = new Generador(SalidaNueva.Tasa);
            float[] mezcla = new float[sal.Periodo * 2];
            byte[] bs = new byte[sal.Periodo * 2 * sal.BytesPorMuestra];
            byte[] bc = new byte[cap.BytesPorCuadro * 4096];
            uint azar = 0x2545F491u;
            using (sal) using (cap)
            {
                cap.Arranca();
                long empezo = Environment.TickCount64, hasta = -1;
                while (true)
                {
                    long ahora = Environment.TickCount64;
                    if (g.Acabo && hasta < 0) hasta = ahora + 800 + sal.Bufer * 1000L / SalidaNueva.Tasa;
                    if (hasta >= 0 && ahora >= hasta) break;
                    if (ahora - empezo > (long)(TotalSeg * 1000) + 15000) { inf.Problema = "La salida no tomo los tonos a tiempo."; break; }
                    long libres = sal.Libres();
                    if (libres < 0) { inf.Problema = "La salida fallo: " + SalidaNuevaPcm.Texto((int)libres); break; }
                    while (libres >= sal.Periodo)
                    {
                        Array.Clear(mezcla, 0, mezcla.Length);
                        if (!g.Acabo) g.Genera(mezcla, sal.Periodo);
                        CuantizaConsola(mezcla, bs, sal.Formato, sal.Periodo, ref azar);
                        long w; fixed (byte* p = bs) w = sal.Escribe(p, sal.Periodo);
                        if (w < 0) { inf.Problema = "La salida fallo: " + SalidaNuevaPcm.Texto((int)w); break; }
                        libres = sal.Libres();
                    }
                    if (inf.Problema.Length > 0) break;
                    long disp = cap.Libres();
                    if (disp < 0) { inf.Problema = "La entrada fallo: " + SalidaNuevaPcm.Texto((int)disp); break; }
                    int cuantos = (int)Math.Min(disp, bc.Length / cap.BytesPorCuadro);
                    if (cuantos > 0) { long h; fixed (byte* p = bc) h = cap.Lee(p, cuantos); if (h > 0) Agrega(cap, bc, (int)h, l, r); }
                    Thread.Sleep(5);
                }
            }
            Analiza(inf, l.ToArray(), r.ToArray(), inf.TasaEntrada);
            Console.WriteLine();
            Console.Write(inf.Texto());
            if (!double.IsNaN(inf.Techo)) Console.WriteLine("Para usarlo: en Ctrl+F4 -> Sonido -> la salida nueva -> «Guardar el techo», o la orden «ajuste techo_dbtp=" + inf.Techo.ToString("0", CultureInfo.InvariantCulture) + "».");
            return double.IsNaN(inf.Techo) ? 3 : 0;
        }
        private static void CuantizaConsola(float[] m, byte[] b, int f, int n, ref uint s)
        {
            for (int i = 0; i < n * 2; i++)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5; double t1 = s / 4294967296.0;
                s ^= s << 13; s ^= s >> 17; s ^= s << 5; double t = t1 - s / 4294967296.0;
                if (f == SalidaNuevaPcm.S16_LE) { int v = (int)Math.Round(m[i] * 32767.0 + t); v = Math.Max(-32768, Math.Min(32767, v)); b[i * 2] = (byte)v; b[i * 2 + 1] = (byte)(v >> 8); }
                else
                {
                    int v = (int)Math.Round(m[i] * 8388607.0 + t); v = Math.Max(-8388608, Math.Min(8388607, v));
                    if (f == SalidaNuevaPcm.S24_3LE) { b[i * 3] = (byte)v; b[i * 3 + 1] = (byte)(v >> 8); b[i * 3 + 2] = (byte)(v >> 16); }
                    else { int w = v << 8; b[i * 4] = (byte)w; b[i * 4 + 1] = (byte)(w >> 8); b[i * 4 + 2] = (byte)(w >> 16); b[i * 4 + 3] = (byte)(w >> 24); }
                }
            }
        }
    }
}

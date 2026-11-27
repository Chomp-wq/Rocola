// LA ORDEN DE PRUEBA DE LAS PIEZAS DE LA CADENA NUEVA DEL BILLAR (B; Pruebas/RocolaOs/billar-nuevo/prueba.sh; CADENA_NUEVA_B.md). No hace nada en la
// rocola: `rocola-xp --prueba-billar2 PARTE...` corre aqui con SENALES SINTETICAS y acaba el programa (RocolaOs.Ordenes.cs). Cada comprobacion
// es una linea «BIEN ...» o «MAL ...» (y «INFO ...» para los numeros que se apuntan); sale con 0 solo si todo es BIEN. Las partes:
//   fft         la FFT real contra la DFT directa, y ida y vuelta
//   mono        el mono: lo mono pasa igual, un canal al reves no se cancela (volteado), las dos leyes (-6 y -3 dB) frente a la ganancia de despues
//   pasoalto    el paso alto solo: su respuesta contra el Butterworth de 24 dB/oct, la fase lineal (simetrica: retardo de grupo constante, 0) y el
//               eco previo de un golpe de bombo
//   curva       la curva del billar sola (objetivo + afinacion + claridad) contra lo pedido; total: con el paso alto
//   noche       el corte de noche en la misma curva: dia, noche entera, a medias y una rampa sin escalones
//   bajo        el bajo virtual: homogeneo (a -20 y a 0 dBFS, 20 dB exactos), sin continua, sin aliasing (nada fuera de los armonicos)
//   constante   ganancia constante: ruido rosa con escalones de +-10 dB, cada pieza y todo junto, contra la misma sin escalones (y un compresor
//               de juguete, que tiene que fallar: la prueba si ve la dinamica)
//   disco       disco continuo: dos pistas seguidas contra una sola, sin escalon ni chasquido en el cambio (y sin heredar, el escalon que habria)
//   afinacion   la afinacion de varios puntos (formas, recorte de los extremos, huecos, topes)
//   guarda DIR  la afinacion y los ajustes en rocola_os.txt de DIR (con ROCOLA_OS=billar_nuevo y ROCOLA_DATOS=DIR): guardar y volver a leer
//   cpu         lo que cuesta (ms de procesador por segundo de musica, la mejor de 3) y lo que tarda el diseno
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Rocola
{
    internal static class RocolaOsBillar2Prueba
    {
        private static int bien, mal;
        private const int Tasa = 44100;
        private static string N(double v, string f = "0.000") { return v.ToString(f, CultureInfo.InvariantCulture); }
        private static void Si(string que, bool ok) { if (ok) { bien++; Console.WriteLine("BIEN  " + que); } else { mal++; Console.WriteLine("MAL   " + que); } }
        private static void Info(string que) { Console.WriteLine("INFO  " + que); }
        private static double Db(double a) { return 20.0 * Math.Log10(Math.Max(1e-300, Math.Abs(a))); }

        internal static int Corre(string[] a)
        {
            bien = 0; mal = 0;
            if (a.Length == 0) a = new[] { "fft", "mono", "pasoalto", "curva", "noche", "bajo", "constante", "disco", "afinacion", "cpu" };
            for (int i = 0; i < a.Length; i++)
            {
                string p = a[i];
                Console.WriteLine("== " + p);
                switch (p)
                {
                    case "fft": Fft(); break;
                    case "mono": Mono(); break;
                    case "pasoalto": PasoAlto(); break;
                    case "curva": Curva(); break;
                    case "noche": Noche(); break;
                    case "bajo": Bajo(); break;
                    case "constante": Constante(); break;
                    case "disco": Disco(); break;
                    case "afinacion": Afinacion(); break;
                    case "guarda": if (i + 1 < a.Length) Guarda(a[++i]); else { Si("guarda: falta la carpeta", false); } break;
                    case "cpu": Cpu(); break;
                    default: Console.Error.WriteLine("parte desconocida: " + p); return 2;
                }
            }
            Console.WriteLine(mal == 0 ? "TODO BIEN (" + bien + " comprobaciones)" : mal + " MAL de " + (bien + mal));
            return mal == 0 ? 0 : 1;
        }

        // ================================================================ lo comun
        // UN ORIGEN de un arreglo intercalado; entrega a trozos de los tamanos dados (para probar lecturas partidas)
        private sealed class Origen : NAudio.Wave.ISampleProvider
        {
            private readonly float[] d; private int p; private readonly int[] trozos; private int t;
            private readonly NAudio.Wave.WaveFormat f;
            internal Origen(float[] datos, int canales, int tasa, params int[] trozosMax) { d = datos; f = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(tasa, canales); trozos = trozosMax; }
            public NAudio.Wave.WaveFormat WaveFormat { get { return f; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }
            public int Read(float[] b, int o, int c)
            {
                if (trozos != null && trozos.Length > 0) { c = Math.Min(c, trozos[t % trozos.Length]); t++; }
                int n = Math.Min(c, d.Length - p);
                if (n <= 0) return 0;
                Array.Copy(d, p, b, o, n); p += n; return n;
            }
        }
        // todo lo que sale de una etapa, leyendo a trozos raros (441, 1031, 3, 4096...)
        private static float[] Saca(NAudio.Wave.ISampleProvider s, int esperadas)
        {
            List<float> r = new List<float>(esperadas + 16);
            float[] b = new float[8192];
            int[] pide = { 441, 1031, 3, 4096, 882, 1 };
            int k = 0;
            while (true)
            {
                int n = s.Read(b, 0, pide[k++ % pide.Length]);
                if (n <= 0) break;
                for (int i = 0; i < n; i++) r.Add(b[i]);
            }
            return r.ToArray();
        }
        private static RocolaOsBillar2Params Plano() { RocolaOsBillar2Params p = new RocolaOsBillar2Params(); p.ConPasoAlto = false; p.ConCurva = false; return p; }
        private static RocolaOsBillar2Params SoloPasoAlto(double hz) { RocolaOsBillar2Params p = new RocolaOsBillar2Params(); p.PasoAltoHz = hz; p.ConCurva = false; return p; }
        private static readonly double[] AfinaPrueba = { -3.0, 2.0, -2.5, 0.0, 1.0, -1.5, 1.5, -4.0 };
        private static RocolaOsBillar2Params Completa(bool bajo)
        {
            RocolaOsBillar2Params p = new RocolaOsBillar2Params(); p.Afinacion = (double[])AfinaPrueba.Clone(); p.BajoVirtual = bajo; return p;
        }
        private static RocolaOsBillar2Tramo Tramo(RocolaOsBillar2Params p, float[] estereo, bool voltear, Func<double> mezcla, params int[] trozos)
        {
            return new RocolaOsBillar2Tramo(new Origen(estereo, 2, Tasa, trozos), RocolaOsBillar2Diseno.Para(Tasa, p), voltear, mezcla);
        }
        private static float[] Pasa(RocolaOsBillar2Params p, float[] estereo, bool voltear = false, Func<double> mezcla = null)
        {
            return Saca(Tramo(p, estereo, voltear, mezcla), estereo.Length);
        }
        private static float[] Izquierdo(float[] e) { float[] r = new float[e.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = e[2 * i]; return r; }
        private static float[] Estereo(float[] l, float[] r) { float[] e = new float[2 * l.Length]; for (int i = 0; i < l.Length; i++) { e[2 * i] = l[i]; e[2 * i + 1] = r[i]; } return e; }
        private static double Rms(float[] x, int desde, int hasta) { double s = 0; for (int i = desde; i < hasta; i++) s += (double)x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, hasta - desde)); }
        private static double Pico(float[] x) { double m = 0; foreach (float v in x) m = Math.Max(m, Math.Abs(v)); return m; }
        // la amplitud de un tono de f Hz (Goertzel, ventana rectangular de [desde, hasta))
        private static double Tono(float[] x, int desde, int hasta, double f)
        {
            double w = 2.0 * Math.PI * f / Tasa, c = 2.0 * Math.Cos(w), s1 = 0, s2 = 0;
            for (int i = desde; i < hasta; i++) { double s = x[i] + c * s1 - s2; s2 = s1; s1 = s; }
            double re = s1 - s2 * Math.Cos(w), im = s2 * Math.Sin(w);
            return 2.0 * Math.Sqrt(re * re + im * im) / (hasta - desde);
        }
        // ruido rosa (el filtro de Paul Kellet, como el de AfinarLocal.cs), a 'rms' eficaz
        private static float[] Rosa(int n, uint semilla, double rms)
        {
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            double[] y = new double[n]; double s2 = 0;
            for (int i = 0; i < n; i++)
            {
                semilla = semilla * 1664525u + 1013904223u;
                double w = (semilla >> 8) * (2.0 / 16777216.0) - 1.0;
                b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
                b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
                double p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
                y[i] = p; s2 += p * p;
            }
            double k = rms / Math.Sqrt(s2 / n);
            float[] r = new float[n]; for (int i = 0; i < n; i++) r[i] = (float)(y[i] * k);
            return r;
        }
        private static float[] Seno(int n, double f, double amp, double fase = 0)
        {
            float[] r = new float[n]; for (int i = 0; i < n; i++) r[i] = (float)(amp * Math.Sin(2.0 * Math.PI * f * i / Tasa + fase)); return r;
        }
        // LA RESPUESTA AL IMPULSO de una etapa (los dos canales con el mismo impulso en el centro de 2^17 cuadros): lo que sale en L
        private const int LargoImpulso = 131072, CentroImpulso = 65536;
        private static float[] Impulso(RocolaOsBillar2Params p)
        {
            float[] l = new float[LargoImpulso]; l[CentroImpulso] = 1f;
            return Izquierdo(Pasa(p, Estereo(l, l)));
        }
        // su espectro (la FFT de la respuesta, con el centro del impulso en 0): real si la fase es cero
        private static void Espectro(float[] h, out double[] re, out double[] im)
        {
            double[] x = new double[LargoImpulso];
            for (int i = 0; i < LargoImpulso; i++) x[i] = h[(i + CentroImpulso) % LargoImpulso];
            re = new double[LargoImpulso / 2 + 1]; im = new double[LargoImpulso / 2 + 1];
            new RocolaOsBillar2Fft(LargoImpulso).Adelante(x, 0, re, im);
        }
        private static double[] Frecuencias(double desde, double hasta, int n) { double[] f = new double[n]; for (int i = 0; i < n; i++) f[i] = desde * Math.Pow(hasta / desde, i / (n - 1.0)); return f; }
        private static double Interp(double[] re, double f) { double k = f * LargoImpulso / Tasa; int i = (int)k; double t = k - i; return re[i] + (re[Math.Min(re.Length - 1, i + 1)] - re[i]) * t; }

        // ================================================================ fft
        private static void Fft()
        {
            Random r = new Random(7);
            foreach (int n in new[] { 16, 64, 1024 })
            {
                double[] x = new double[n]; for (int i = 0; i < n; i++) x[i] = r.NextDouble() * 2 - 1;
                double[] re = new double[n / 2 + 1], im = new double[n / 2 + 1];
                RocolaOsBillar2Fft f = new RocolaOsBillar2Fft(n);
                f.Adelante(x, 0, re, im);
                double err = 0, mx = 0;
                for (int k = 0; k <= n / 2; k++)
                {
                    double sr = 0, si = 0;
                    for (int j = 0; j < n; j++) { double a = -2.0 * Math.PI * k * j / n; sr += x[j] * Math.Cos(a); si += x[j] * Math.Sin(a); }
                    err = Math.Max(err, Math.Max(Math.Abs(sr - re[k]), Math.Abs(si - im[k]))); mx = Math.Max(mx, Math.Sqrt(sr * sr + si * si));
                }
                double[] y = new double[n]; f.Atras(re, im, y, 0);
                double ev = 0; for (int i = 0; i < n; i++) ev = Math.Max(ev, Math.Abs(y[i] - x[i]));
                Si("fft " + n + ": contra la DFT directa, error " + err.ToString("0.0e0", CultureInfo.InvariantCulture) + " de " + N(mx, "0.0") + "; ida y vuelta " + ev.ToString("0.0e0", CultureInfo.InvariantCulture), err < 1e-10 * Math.Max(1, mx) && ev < 1e-12);
            }
        }

        // ================================================================ mono
        private static void Mono()
        {
            int n = 44100;
            float[] x = Rosa(n, 11, 0.1), z = new float[n], menos = new float[n];
            for (int i = 0; i < n; i++) menos[i] = -x[i];
            RocolaOsBillar2Params p = Plano();
            float[] o = Pasa(p, Estereo(x, x));
            double e = 0; bool igual = true;
            for (int i = 0; i < n; i++) { e = Math.Max(e, Math.Abs(o[2 * i] - x[i])); if (o[2 * i] != o[2 * i + 1]) igual = false; }
            Si("mono: lo que ya es mono (L = R) sale igual (error " + e.ToString("0.0e0", CultureInfo.InvariantCulture) + ") y L = R bit a bit", e < 1e-6 && igual);
            o = Pasa(p, Estereo(x, menos), false);
            Si("mono: un canal al reves SIN voltear se cancela (lo que sale: " + N(Db(Rms(o, 0, o.Length) / Rms(x, 0, n)), "0.0") + " dB): por eso la medida tiene que decirlo", Rms(o, 0, o.Length) < 1e-6 * Rms(x, 0, n));
            o = Pasa(p, Estereo(x, menos), true);
            e = 0; for (int i = 0; i < n; i++) e = Math.Max(e, Math.Abs(o[2 * i] - x[i]));
            Si("mono: un canal al reves, volteado ((L - R)/2): no se cancela, sale la musica entera (error " + e.ToString("0.0e0", CultureInfo.InvariantCulture) + ")", e < 1e-6);
            o = Pasa(p, Estereo(x, z));
            e = 0; for (int i = 0; i < n; i++) e = Math.Max(e, Math.Abs(o[2 * i] - 0.5f * x[i]));
            Si("mono: un canal mudo da la mitad (-6 dB; la ganancia de despues lo sube; los arreglos copian el que suena)", e < 1e-6);

            // LAS DOS LEYES: (L + R)/2 (-6 dB) y (L + R)/raiz de 2 (-3 dB), con cuatro materiales. La ganancia de la cancion se mide DESPUES (con la
            // sonoridad y el pico real de lo que sale): una constante delante no cambia nada de lo que se oye. Lo que importa es no subir picos.
            float[] voz = Rosa(n, 21, 0.1), anchoL = Rosa(n, 31, 0.1), anchoR = Rosa(n, 41, 0.1);
            string[] nombre = { "mono (L = R)", "estereo sin correlacion", "mezcla (voz al centro + ancho)", "todo a un lado" };
            bool plr = true, picos = true;
            for (int m = 0; m < 4; m++)
            {
                float[] L = new float[n], R = new float[n];
                for (int i = 0; i < n; i++)
                {
                    if (m == 0) { L[i] = voz[i]; R[i] = voz[i]; }
                    else if (m == 1) { L[i] = anchoL[i]; R[i] = anchoR[i]; }
                    else if (m == 2) { L[i] = voz[i] + 0.6f * anchoL[i]; R[i] = voz[i] + 0.6f * anchoR[i]; }
                    else { L[i] = voz[i]; R[i] = 0; }
                }
                double rIn = Math.Sqrt(0.5 * (Rms(L, 0, n) * Rms(L, 0, n) + Rms(R, 0, n) * Rms(R, 0, n))), pIn = Math.Max(Pico(L), Pico(R));
                double[] ley = { 0.5, Math.Sqrt(0.5) };
                double[] plrDe = new double[2];
                string t = "";
                for (int k = 0; k < 2; k++)
                {
                    float[] s = new float[n]; for (int i = 0; i < n; i++) s[i] = (float)(ley[k] * (L[i] + R[i]));
                    double rr = Rms(s, 0, n), pp = Pico(s);
                    plrDe[k] = Db(pp) - Db(rr);
                    t += (k == 0 ? " -6 dB: " : "; -3 dB: ") + "eficaz " + N(Db(rr / rIn), "+0.0;-0.0") + " dB, pico " + N(Db(pp / pIn), "+0.0;-0.0") + " dB";
                    if (k == 0 && pp > pIn * (1 + 1e-6)) picos = false;
                }
                if (Math.Abs(plrDe[0] - plrDe[1]) > 0.01) plr = false;
                Info("mono, " + nombre[m] + ":" + t + " (pico/eficaz " + N(plrDe[0], "0.00") + " dB con las dos)");
            }
            Si("mono: la proporcion pico/eficaz es la misma con las dos leyes (la ganancia de despues las deja iguales)", plr);
            Si("mono: con -6 dB el pico nunca pasa del de la entrada (con -3 dB lo mono sube 3 dB)", picos);
        }

        // ================================================================ paso alto
        private static void PasoAlto()
        {
            foreach (double fc in new[] { 80.0, 50.0, 120.0 })
            {
                float[] h = Impulso(SoloPasoAlto(fc));
                double hm = 0, asim = 0;
                for (int k = 0; k < 20000; k++) { hm = Math.Max(hm, Math.Abs(h[CentroImpulso + k])); asim = Math.Max(asim, Math.Abs(h[CentroImpulso + k] - h[CentroImpulso - k])); }
                double[] re, im; Espectro(h, out re, out im);
                double fase = 0;
                for (int k = 1; k < re.Length; k++) { double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); if (mag > 1e-3) fase = Math.Max(fase, Math.Abs(Math.Atan2(im[k], Math.Abs(re[k])))); }
                Si("paso alto " + fc + " Hz: fase lineal (la respuesta es simetrica alrededor del impulso: " + asim.ToString("0.0e0", CultureInfo.InvariantCulture) + " de " + N(hm, "0.00")
                    + "; fase " + fase.ToString("0.0e0", CultureInfo.InvariantCulture) + " rad: retardo de grupo constante, 0 muestras, sobre -60 dB)", asim < 1e-6 * hm && fase < 1e-4);
                double err20 = 0, err40 = 0, rizo = 0; string tabla = "";
                foreach (double f in Frecuencias(10, 20000, 300))
                {
                    double t = Db(RocolaOsBillar2Curva.PasoAlto(f, fc)), g = Db(Interp(re, f));
                    if (t > -20) err20 = Math.Max(err20, Math.Abs(g - t));
                    if (t > -40) err40 = Math.Max(err40, Math.Abs(g - t));
                    if (f > 2 * fc) rizo = Math.Max(rizo, Math.Abs(g));
                }
                foreach (double f in new[] { 20.0, fc / 2, fc, 1.5 * fc, 2 * fc, 1000.0 }) tabla += " " + N(f, "0") + " Hz " + N(Db(Interp(re, f)), "0.00");
                Info("paso alto " + fc + " Hz:" + tabla + " dB");
                Si("paso alto " + fc + " Hz: contra el Butterworth de cuarto orden, " + N(err20, "0.0000") + " dB donde esta sobre -20 dB y " + N(err40, "0.000") + " sobre -40; rizo arriba de 2 fc " + N(rizo, "0.0000") + " dB",
                    err20 < 0.02 && err40 < 0.2 && rizo < 0.03);
                double a40 = -Db(Interp(re, fc / 2));
                Si("paso alto " + fc + " Hz: 24 dB por octava (una octava abajo quita " + N(a40, "0.0") + " dB)", a40 > 23.5 && a40 < 24.6);
            }
            // EL ORDEN (12, 18 o 24 dB por octava) Y EL ECO PREVIO DE UN GOLPE (el precio de la fase lineal): un bombo de 50 Hz que empieza de golpe en
            // el cuadro c (y uno de 60 Hz); lo que sale ANTES de c, frente al golpe que entra; y lo que se le quita al golpe (amplificador y conos)
            int n = 65536, c = 32768;
            double ecoDe4 = 0;
            foreach (int orden in new[] { 2, 3, 4 })
            {
                RocolaOsBillar2Params po = SoloPasoAlto(80); po.Orden = orden;
                string t = "";
                foreach (double fb in new[] { 50.0, 60.0 })
                {
                    float[] b = new float[n];
                    for (int i = c; i < n; i++) { double tt = (i - c) / (double)Tasa; b[i] = (float)(0.5 * Math.Sin(2 * Math.PI * fb * tt) * Math.Exp(-tt / 0.08)); }
                    float[] o = Izquierdo(Pasa(po, Estereo(b, b)));
                    double antes = 0, entra = 0, sale = 0, pk = 0; int primero = c;
                    for (int i = 0; i < c; i++) antes += (double)o[i] * o[i];
                    for (int i = c; i < n; i++) { entra += (double)b[i] * b[i]; sale += (double)o[i] * o[i]; pk = Math.Max(pk, Math.Abs(o[i])); }
                    for (int i = 0; i < c; i++) if (Math.Abs(o[i]) > pk * 1e-3) { primero = i; break; }
                    double eco = 10 * Math.Log10(antes / entra);
                    if (orden == 4) ecoDe4 = Math.Max(ecoDe4 == 0 ? -999 : ecoDe4, eco);
                    t += "; bombo de " + N(fb, "0") + " Hz: le quita " + N(-10 * Math.Log10((antes + sale) / entra), "0.0") + " dB, eco previo " + N(eco, "0.0") + " dB del golpe desde "
                        + N((c - primero) * 1000.0 / Tasa, "0") + " ms antes";
                }
                Info("paso alto 80 Hz de " + (6 * orden) + " dB/oct: a 40 Hz " + N(Db(RocolaOsBillar2Curva.PasoAlto(40, 80, orden)), "0.0") + " dB, a 120 Hz " + N(Db(RocolaOsBillar2Curva.PasoAlto(120, 80, orden)), "0.00")
                    + ", a 160 Hz " + N(Db(RocolaOsBillar2Curva.PasoAlto(160, 80, orden)), "0.00") + t);
            }
            Si("paso alto 24 dB/oct: el eco previo de un bombo de 50-60 Hz queda 20 dB o mas por debajo del golpe que entra (" + N(ecoDe4, "0.0") + " dB)", ecoDe4 < -20);
        }

        // ================================================================ curva
        private static void Curva()
        {
            RocolaOsBillar2Params p = Completa(false); p.ConPasoAlto = false;
            float[] h = Impulso(p);
            double[] re, im; Espectro(h, out re, out im);
            double err = 0; string tabla = "";
            foreach (double f in Frecuencias(20, 20000, 300)) err = Math.Max(err, Math.Abs(Db(Interp(re, f)) - RocolaOsBillar2Curva.CurvaDb(f, p)));
            foreach (double f in RocolaOsBillar2Curva.Centros) tabla += " " + N(f, "0") + " " + N(RocolaOsBillar2Curva.CurvaDb(f, p), "+0.0;-0.0");
            Info("curva de prueba (afinacion " + string.Join(" ", Array.ConvertAll(AfinaPrueba, v => N(v, "+0.0;-0.0"))) + ", objetivo -0.75 dB/oct, claridad -2/+2):" + tabla);
            Si("curva sola: lo que hace contra lo pedido, " + N(err, "0.0000") + " dB de 20 Hz a 20 kHz", err < 0.05);
            double asim = 0, hm = 0;
            for (int k = 0; k < 20000; k++) { hm = Math.Max(hm, Math.Abs(h[CentroImpulso + k])); asim = Math.Max(asim, Math.Abs(h[CentroImpulso + k] - h[CentroImpulso - k])); }
            Si("curva sola: fase lineal (simetrica, " + asim.ToString("0.0e0", CultureInfo.InvariantCulture) + ")", asim < 1e-6 * hm);
            // los topes: cortes hasta -9, subidas hasta +3
            RocolaOsBillar2Params q = new RocolaOsBillar2Params(); q.Afinacion = new double[] { -6, 3, 3, -6, -6, 3, 3, 2 }; q.ClaridadMenos = 4; q.ClaridadMas = 3; q.Inclinacion = -1.5;
            double mn = 99, mx = -99;
            foreach (double f in Frecuencias(20, 20000, 400)) { double v = RocolaOsBillar2Curva.CurvaDb(f, q); mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
            Si("curva: con todo al extremo, de " + N(mn, "0.0") + " a " + N(mx, "+0.0") + " dB (topes -9 y +3)", mn >= -9.0001 && mx <= 3.0001);
            // y la de fabrica sin medir
            RocolaOsBillar2Params d = new RocolaOsBillar2Params();
            tabla = ""; foreach (double f in new[] { 63.0, 125, 250, 315, 500, 1000, 2000, 2828, 4000, 8000, 16000 }) tabla += " " + N(f, "0") + " " + N(RocolaOsBillar2Curva.TotalDb(f, d, false), "+0.0;-0.0");
            Info("la de fabrica sin afinacion (paso alto 80, objetivo -0.75, claridad -2/+2), en dB:" + tabla);
            // con el paso alto: todo junto
            RocolaOsBillar2Params t = Completa(false);
            h = Impulso(t); Espectro(h, out re, out im);
            err = 0;
            foreach (double f in Frecuencias(20, 20000, 300)) { double tt = RocolaOsBillar2Curva.TotalDb(f, t, false); if (tt > -20) err = Math.Max(err, Math.Abs(Db(Interp(re, f)) - tt)); }
            Si("total (paso alto y curva en un solo filtro): contra lo pedido, " + N(err, "0.0000") + " dB donde esta sobre -20 dB", err < 0.05);
        }

        // ================================================================ noche
        private static void Noche()
        {
            RocolaOsBillar2Params p = Completa(false); p.NocheHz = 110;
            RocolaOsBillar2Diseno d = RocolaOsBillar2Diseno.Para(Tasa, p);
            Si("noche: con el corte de noche (110 Hz) mayor que el paso alto (80), el diseno trae el filtro de noche", d.Noche != null);
            RocolaOsBillar2Params q = Completa(false); q.NocheHz = 70;
            Si("noche: con el corte de noche (70 Hz) menor que el paso alto (80), no hay otro filtro (el de dia ya corta mas)", RocolaOsBillar2Diseno.Para(Tasa, q).Noche == null);
            foreach (double m in new[] { 0.0, 0.5, 1.0 })
            {
                double mm = m;
                float[] l = new float[LargoImpulso]; l[CentroImpulso] = 1f;
                float[] h = Izquierdo(Pasa(p, Estereo(l, l), false, () => mm));
                double[] re, im; Espectro(h, out re, out im);
                double err = 0;
                foreach (double f in Frecuencias(20, 20000, 300))
                {
                    double dia = Math.Pow(10, RocolaOsBillar2Curva.TotalDb(f, p, false) / 20), noche = Math.Pow(10, RocolaOsBillar2Curva.TotalDb(f, p, true) / 20);
                    double t = Db(dia + m * (noche - dia));
                    if (t > -20) err = Math.Max(err, Math.Abs(Db(Interp(re, f)) - t));
                }
                Si("noche: con la mezcla en " + N(m, "0.0") + ", una sola curva (" + (m == 0 ? "la de dia" : m == 1 ? "el paso alto en 110 Hz" : "entre las dos") + "): " + N(err, "0.0000") + " dB", err < 0.05);
            }
            // la rampa: de dia a noche entera en 2 s sobre un tono de 95 Hz; lo que sale no da saltos (la derivada no pasa de la del tono)
            int n = 6 * Tasa;
            float[] s = Seno(n, 95, 0.5);
            long cuadro = 0; int bb = d.B;
            Func<double> rampa = delegate { double v = Math.Max(0, Math.Min(1, (cuadro - Tasa) / (2.0 * Tasa))); cuadro += bb; return v; };   // (se pide una vez por bloque de B cuadros: asi avanza como el reloj)
            float[] o = Izquierdo(Saca(new RocolaOsBillar2Tramo(new Origen(Estereo(s, s), 2, Tasa), d, false, rampa), 2 * n));
            double salto = 0, natural = 2 * Math.PI * 95 / Tasa * 0.5;
            for (int i = Tasa / 2; i < n - Tasa / 2; i++) salto = Math.Max(salto, Math.Abs(o[i + 1] - o[i]));
            Si("noche: la mezcla en rampa (0 a 1 en 2 s, tomada por bloque y en linea recta dentro de el): el paso mas grande entre dos muestras es " + N(salto / natural, "0.000") + " del de un tono de 95 Hz", salto <= natural * 1.02);
        }

        // ================================================================ bajo virtual
        private static void Bajo()
        {
            RocolaOsBillar2Params con = Completa(true), sin = Completa(false);
            RocolaOsBillar2Diseno dc = RocolaOsBillar2Diseno.Para(Tasa, con), ds = RocolaOsBillar2Diseno.Para(Tasa, sin);
            Si("bajo: el diseno con y sin el bajo virtual tiene el mismo filtro de dia (K " + dc.K + " y " + ds.K + ")", dc.K == ds.K && dc.TapsDia.Length == ds.TapsDia.Length);
            string tf = ""; foreach (double f in new[] { 20.0, 40, 80, 160, 320, 640, 1280 }) tf += " " + N(f, "0") + " " + N(Db(dc.Bajo.Respuesta(Tasa, f)), "0.0");
            Info("bajo: los filtros de los armonicos (paso alto 24 dB/oct en 80 Hz, paso bajo 12 dB/oct en 320 Hz), dB:" + tf);
            // un tono de 67 periodos en 65 536 cuadros (45,08 Hz), estable: lo que el bajo virtual suma = con - sin
            int fftN = 65536, k0 = 67; double f0 = k0 * (double)Tasa / fftN;
            int n = 4 * Tasa;
            double[] arm = new double[2]; float[][] bv = new float[2][];
            for (int a = 0; a < 2; a++)
            {
                double amp = a == 0 ? 0.1 : 1.0;   // -20 dBFS y 0 dBFS
                float[] s = Seno(n, f0, amp);
                float[] oc = Izquierdo(Pasa(con, Estereo(s, s))), os = Izquierdo(Pasa(sin, Estereo(s, s)));
                float[] d = new float[n]; for (int i = 0; i < n; i++) d[i] = oc[i] - os[i];
                bv[a] = d;
                arm[a] = Tono(d, Tasa, Tasa + fftN, 2 * f0);
            }
            double dif = Db(arm[1]) - Db(arm[0]);
            Si("bajo: homogeneo: el 2.o armonico a 0 dBFS y a -20 dBFS se lleva " + N(dif, "0.0000") + " dB (20 exactos)", Math.Abs(dif - 20.0) < 0.001);
            double maxRel = 0, ref1 = Pico(bv[1]);
            for (int i = 0; i < n; i++) maxRel = Math.Max(maxRel, Math.Abs(bv[1][i] - 10.0 * bv[0][i]));
            Si("bajo: y muestra a muestra: lo de 0 dBFS es 10 veces lo de -20 (" + (maxRel / ref1).ToString("0.0e0", CultureInfo.InvariantCulture) + " del pico)", maxRel < 1e-5 * ref1);
            double rel = Db(arm[1] / 1.0);
            Info("bajo: el 2.o armonico (90 Hz) de un tono de 45 Hz a 0 dBFS queda a " + N(rel, "0.0") + " dB del tono (mezcla -8; la fuente toma " + N(Db(1 - RocolaOsBillar2Curva.PasoAlto(f0, 80)), "0.0") + " dB del tono)"
                + "; 4.o: " + N(Db(Tono(bv[1], Tasa, Tasa + fftN, 4 * f0)), "0.0") + " dB; 6.o: " + N(Db(Tono(bv[1], Tasa, Tasa + fftN, 6 * f0)), "0.0") + " dB");
            Si("bajo: el 2.o armonico sale donde dice la mezcla (de -8 a -11 dB del tono)", rel > -11.5 && rel < -7.5);
            double media = 0, rms = Rms(bv[1], Tasa, Tasa + fftN);
            for (int i = Tasa; i < Tasa + fftN; i++) media += bv[1][i];
            media /= fftN;
            Si("bajo: sin continua (la media de lo que suma: " + (Math.Abs(media) / rms).ToString("0.0e0", CultureInfo.InvariantCulture) + " de su eficaz)", Math.Abs(media) < 1e-4 * rms);
            // EL ALIASING: con 67 periodos justos en la ventana, todo lo armonico cae en los multiplos de 67 de la FFT; lo que se doblo por arriba de
            // 22 kHz cae fuera (65 536 no es multiplo de 67). Lo mas alto fuera de los armonicos, frente al 2.o armonico:
            foreach (int kk in new[] { 52, 67, 104 })
            {
                double ff = kk * (double)Tasa / fftN;
                float[] s = Seno(n, ff, 1.0);
                float[] oc = Izquierdo(Pasa(con, Estereo(s, s))), os = Izquierdo(Pasa(sin, Estereo(s, s)));
                double[] x = new double[fftN];
                for (int i = 0; i < fftN; i++) x[i] = oc[Tasa + i] - os[Tasa + i];
                double[] re = new double[fftN / 2 + 1], im = new double[fftN / 2 + 1];
                new RocolaOsBillar2Fft(fftN).Adelante(x, 0, re, im);
                double h2 = Math.Sqrt(re[2 * kk] * re[2 * kk] + im[2 * kk] * im[2 * kk]), fuera = 0; int donde = 0;
                for (int b = 1; b <= fftN / 2; b++) { if (b % kk == 0) continue; double m = Math.Sqrt(re[b] * re[b] + im[b] * im[b]); if (m > fuera) { fuera = m; donde = b; } }
                Si("bajo: sin aliasing con un tono de " + N(ff, "0.0") + " Hz: lo mas alto fuera de sus armonicos queda a " + N(Db(fuera / h2), "0.0") + " dB del 2.o armonico (en " + N(donde * (double)Tasa / fftN, "0") + " Hz)", Db(fuera / h2) < -90);
            }
        }

        // ================================================================ ganancia constante
        // ruido rosa (estereo, cada canal el suyo) a -20 dBFS con escalones de +-10 dB cada 2 s; la referencia: la misma etapa con el ruido sin
        // escalones, por los escalones. En bloques de 10 ms, fuera de lo que el filtro alcanza de cada escalon, la ganancia tiene que ser la misma.
        private static void Constante()
        {
            int seg = 2 * Tasa, tramos = 8, n = seg * tramos;
            float[] l = Rosa(n, 5, 0.1), r = Rosa(n, 6, 0.1);
            double[] nivel = { 0, -10, 0, 10, 0, -10, 10, 0 };
            float[] le = new float[n], re = new float[n]; double[] a = new double[n];
            for (int i = 0; i < n; i++) { a[i] = Math.Pow(10, nivel[i / seg] / 20); le[i] = (float)(l[i] * a[i]); re[i] = (float)(r[i] * a[i]); }
            var casos = new List<Tuple<string, RocolaOsBillar2Params, Func<double>>>();
            casos.Add(Tuple.Create("mono solo", Plano(), (Func<double>)null));
            casos.Add(Tuple.Create("paso alto solo", SoloPasoAlto(80), (Func<double>)null));
            RocolaOsBillar2Params cs = Completa(false); cs.ConPasoAlto = false;
            casos.Add(Tuple.Create("curva sola", cs, (Func<double>)null));
            casos.Add(Tuple.Create("paso alto y curva", Completa(false), (Func<double>)null));
            casos.Add(Tuple.Create("con el bajo virtual", Completa(true), (Func<double>)null));
            RocolaOsBillar2Params cn = Completa(true); cn.NocheHz = 110;
            casos.Add(Tuple.Create("con la noche a 0,7 y el bajo virtual", cn, (Func<double>)(() => 0.7)));
            foreach (var c in casos)
            {
                float[] y = Izquierdo(Pasa(c.Item2, Estereo(le, re), false, c.Item3));
                float[] yr0 = Izquierdo(Pasa(c.Item2, Estereo(l, r), false, c.Item3));
                double peor = Peor(y, yr0, a, seg, n, RocolaOsBillar2Diseno.Para(Tasa, c.Item2).K);
                Si("ganancia constante, " + c.Item1 + ": en bloques de 10 ms, a lo mas " + N(peor, "0.000000") + " dB de la referencia (tope 0,05)", peor < 0.05);
            }
            // el control: un compresor de juguete (envolvente eficaz de 50 ms, 2:1 sobre -25 dBFS) TIENE que fallar
            float[] cy = Comprime(le), cr = Comprime(l);
            double pc = Peor(cy, cr, a, seg, n, 2205);
            Si("ganancia constante, control: un compresor de juguete se sale " + N(pc, "0.0") + " dB (la prueba si ve la dinamica)", pc > 1.0);
        }
        private static double Peor(float[] y, float[] yr0, double[] a, int seg, int n, int K)
        {
            int blq = Tasa / 100, margen = K + (int)(0.15 * Tasa);
            double peor = 0;
            for (int b = 0; b + blq <= n; b += blq)
            {
                int esc = (b + blq / 2) / seg;
                if (b - esc * seg < margen || (esc + 1) * seg - (b + blq) < margen) continue;   // cerca de un escalon
                double s1 = 0, s2 = 0;
                for (int i = b; i < b + blq; i++) { s1 += (double)y[i] * y[i]; double q = yr0[i] * a[i]; s2 += q * q; }
                if (s2 <= 0) continue;
                peor = Math.Max(peor, Math.Abs(10 * Math.Log10(s1 / s2)));
            }
            return peor;
        }
        private static float[] Comprime(float[] x)
        {
            float[] y = new float[x.Length]; double e = 0, al = 1 - Math.Exp(-1.0 / (0.05 * Tasa)), u = Math.Pow(10, -25 / 20.0);
            for (int i = 0; i < x.Length; i++)
            {
                e += (x[i] * (double)x[i] - e) * al;
                double r = Math.Sqrt(e), g = r > u ? Math.Sqrt(u / r) : 1.0;
                y[i] = (float)(x[i] * g);
            }
            return y;
        }

        // ================================================================ disco continuo
        private static void Disco()
        {
            int n = 20 * Tasa, corte = 10 * Tasa + 13;
            float[] L = new float[n], R = new float[n];
            float[] ruido = Rosa(n, 77, 0.03), ruido2 = Rosa(n, 78, 0.03);
            double[] notas = { 41.2, 55.0, 61.7, 73.4, 82.4 };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Tasa;
                double nota = notas[(int)(t / 0.5) % notas.Length];
                double bajo = 0.25 * Math.Sin(2 * Math.PI * nota * t);
                double tb = t % 0.5, bombo = 0.4 * Math.Sin(2 * Math.PI * 50 * tb) * Math.Exp(-tb / 0.06);
                double voz = 0.1 * Math.Sin(2 * Math.PI * 1000 * t);
                L[i] = (float)(bajo + bombo + voz + ruido[i]); R[i] = (float)(bajo + bombo + voz + ruido2[i]);
            }
            float[] e = Estereo(L, R);
            foreach (bool bv in new[] { false, true })
            {
                RocolaOsBillar2Params p = Completa(bv);
                RocolaOsBillar2Diseno d = RocolaOsBillar2Diseno.Para(Tasa, p);
                float[] una = Izquierdo(Pasa(p, e));
                double rmsM = Rms(una, 0, n);
                float[] e1 = new float[2 * corte], e2 = new float[e.Length - 2 * corte];
                Array.Copy(e, 0, e1, 0, e1.Length); Array.Copy(e, e1.Length, e2, 0, e2.Length);
                string q = bv ? " (con el bajo virtual)" : "";
                // EL MODO EXACTO: Sigue antes de que acabe la primera (como el relevo: al armar la siguiente)
                {
                    RocolaOsBillar2Tramo a = new RocolaOsBillar2Tramo(new Origen(e1, 2, Tasa, 1000, 7), d, false, null);
                    RocolaOsBillar2Tramo b = new RocolaOsBillar2Tramo(new Origen(e2, 2, Tasa, 999, 5), d, false, null);
                    b.Sigue(a);
                    float[] y1 = Izquierdo(Saca(a, e1.Length)), y2 = Izquierdo(Saca(b, e2.Length));
                    Si("disco" + q + ", modo exacto: las dos pistas salen enteras (" + y1.Length + " + " + y2.Length + " cuadros), la segunda heredo y empezo con lo que la primera ya leyo", y1.Length == corte && y2.Length == n - corte && b.Heredo && b.Exacto);
                    double dmax = 0; int donde = 0;
                    for (int i = 0; i < n; i++) { double v = Math.Abs((i < corte ? y1[i] : y2[i - corte]) - una[i]); if (v > dmax) { dmax = v; donde = i; } }
                    Si("disco" + q + ", modo exacto: sale igual que las dos pegadas en una sola, cuadro a cuadro (a lo mas " + dmax.ToString("0.0e0", CultureInfo.InvariantCulture) + ", " + N(Db(dmax / rmsM), "0") + " dB del eficaz, en " + N((donde - corte) * 1000.0 / Tasa, "0.0") + " ms del cambio): sin escalon ni chasquido", dmax < 1e-5 * Math.Max(1, rmsM));
                }
                // SI NO (Sigue cuando la primera ya acabo): el empalme
                {
                    RocolaOsBillar2Tramo a = new RocolaOsBillar2Tramo(new Origen(e1, 2, Tasa, 1000, 7), d, false, null);
                    float[] y1 = Izquierdo(Saca(a, e1.Length));
                    RocolaOsBillar2Tramo b = new RocolaOsBillar2Tramo(new Origen(e2, 2, Tasa, 999), d, false, null);
                    b.Sigue(a);
                    float[] y2 = Izquierdo(Saca(b, e2.Length));
                    RocolaOsBillar2Tramo c = new RocolaOsBillar2Tramo(new Origen(e2, 2, Tasa), d, false, null);   // sin heredar
                    float[] y3 = Izquierdo(Saca(c, e2.Length));
                    Si("disco" + q + ", con empalme (Sigue tarde): las dos pistas salen enteras y la segunda heredo, sin el modo exacto", y1.Length == corte && y2.Length == n - corte && b.Heredo && !b.Exacto);
                    double pasoRef = una[corte] - una[corte - 1], pasoDos = y2[0] - y1[corte - 1], pasoSin = y3[0] - y1[corte - 1];
                    double esc = Math.Abs(pasoDos - pasoRef), escSin = Math.Abs(pasoSin - pasoRef);
                    Si("disco" + q + ", con empalme: sin escalon en el cambio: " + N(esc / rmsM, "0.00000") + " del eficaz de mas (sin heredar seria " + N(escSin / rmsM, "0.000") + "; el empalme quito " + N(b.EmpalmeDia, "0.0000") + (bv ? " y " + N(b.EmpalmeBajo, "0.0000") + " de la fuente del bajo" : "") + ")", esc < 0.02 * rmsM);
                    double[] dif = new double[n];
                    for (int i = 0; i < n; i++) dif[i] = (i < corte ? y1[i] : y2[i - corte]) - una[i];
                    double cl = Chasquido(dif, corte - d.K - 2000, corte + 4000);
                    double tras = 0, entre = 0, cola = 0;
                    for (int i = corte + (int)(0.2 * Tasa); i < n; i++) tras = Math.Max(tras, Math.Abs(dif[i]));
                    for (int i = corte + (int)(0.03 * Tasa); i < corte + (int)(0.2 * Tasa); i++) entre = Math.Max(entre, Math.Abs(dif[i]));
                    for (int i = corte - d.K; i < corte; i++) cola = Math.Max(cola, Math.Abs(dif[i]));
                    Si("disco" + q + ", con empalme: pasados 200 ms sale igual que una sola pista (" + tras.ToString("0.0e0", CultureInfo.InvariantCulture) + ")", tras < 1e-5);
                    Info("disco" + q + ", con empalme (lo que el modo exacto evita): la cola de la primera (sus ultimos " + N(d.K * 1000.0 / Tasa, "0") + " ms) se aparta hasta " + N(Db(cola / rmsM), "0.0")
                        + " dB del eficaz (el grave que el paso alto no quita sin saber lo que sigue); lo de arriba de 1 kHz alrededor del cambio, " + N(Db(cl / rmsM), "0.0") + " dB; de 30 a 200 ms despues, " + N(Db(entre / rmsM), "0.0") + " dB");
                }
            }
        }
        // lo mas alto de 'x' por arriba de 1 kHz (un paso alto de segundo orden en 1 kHz) entre desde y hasta
        private static double Chasquido(double[] x, int desde, int hasta)
        {
            RocolaOsBillar2Bajo.Bicuadrada h = RocolaOsBillar2Bajo.Bicuadrada.PasoAlto(Tasa, 1000, 0.7071);
            double z1 = 0, z2 = 0, m = 0;
            for (int i = Math.Max(0, desde - 4000); i < Math.Min(x.Length, hasta); i++)
            {
                double v = x[i], y = h.B0 * v + z1; z1 = h.B1 * v - h.A1 * y + z2; z2 = h.B2 * v - h.A2 * y;
                if (i >= desde) m = Math.Max(m, Math.Abs(y));
            }
            return m;
        }

        // ================================================================ afinacion
        private static void Afinacion()
        {
            double[] base1 = { 72, 74, 73, 71, 70, 69, 67, 61 };   // en dBC: un local con grave de mas y agudo de menos
            RocolaOsBillar2Afinacion uno = new RocolaOsBillar2Afinacion();
            Array.Copy(base1, uno.Lecturas[0], 8);
            string[] mot; double[] c1 = uno.Correccion(out mot);
            Info("afinacion, un punto (" + string.Join(" ", Array.ConvertAll(base1, v => N(v, "0"))) + " dBC): correccion " + string.Join(" ", Array.ConvertAll(c1, v => N(v, "+0.0;-0.0"))) + " (fuerza " + N(uno.Fuerza, "0.00") + ")");
            Si("afinacion: un punto se corrige al 70 %", Math.Abs(uno.Fuerza - 0.7) < 1e-9 && c1 != null);
            Si("afinacion: 63 Hz nunca sube (" + N(c1[0], "+0.0;-0.0") + ")", c1[0] <= 0);
            // ocho puntos iguales: la misma forma, sin dispersion
            RocolaOsBillar2Afinacion ocho = new RocolaOsBillar2Afinacion();
            for (int p = 0; p < 8; p++) Array.Copy(base1, ocho.Lecturas[p], 8);
            double[] f1, f8, d8; int[] n1, n8; double[] d1;
            uno.Forma(out f1, out n1, out d1); ocho.Forma(out f8, out n8, out d8);
            bool igual = true; for (int b = 0; b < 8; b++) if (Math.Abs(f1[b] - f8[b]) > 1e-9 || d8[b] > 1e-9) igual = false;
            Si("afinacion: ocho puntos iguales dan la misma forma que uno, sin dispersion (y la fuerza sube a " + N(ocho.Fuerza, "0.00") + ")", igual && Math.Abs(ocho.Fuerza - 0.8) < 1e-9);
            // un punto pegado a una bocina (todo 10 dB mas) no cambia la forma
            RocolaOsBillar2Afinacion cerca = new RocolaOsBillar2Afinacion();
            for (int p = 0; p < 3; p++) Array.Copy(base1, cerca.Lecturas[p], 8);
            for (int b = 0; b < 8; b++) cerca.Lecturas[3][b] = base1[b] + 10;
            double[] fc_; int[] nc; double[] dc; cerca.Forma(out fc_, out nc, out dc);
            igual = true; for (int b = 0; b < 8; b++) if (Math.Abs(fc_[b] - f1[b]) > 1e-9) igual = false;
            Si("afinacion: un punto junto a una bocina (todo +10 dB) pesa por su forma: la forma no cambia", igual);
            // seis puntos, uno con 63 Hz de mas (junto al woofer): con cinco o mas, se quita
            RocolaOsBillar2Afinacion seis = new RocolaOsBillar2Afinacion();
            for (int p = 0; p < 6; p++) Array.Copy(base1, seis.Lecturas[p], 8);
            seis.Lecturas[2][0] += 15;
            double[] fs; int[] ns; double[] ds; seis.Forma(out fs, out ns, out ds);
            Si("afinacion: seis puntos, uno con 63 Hz +15 dB: se quitan el mas alto y el mas bajo (63 Hz queda en " + N(fs[0] - f1[0], "+0.00;-0.00") + " dB de los demas; dispersion " + N(ds[0], "0.0") + ")", Math.Abs(fs[0] - f1[0]) < 1e-9);
            // un hueco: 1 kHz 6 dB por debajo de sus vecinas -> no sube
            RocolaOsBillar2Afinacion hueco = new RocolaOsBillar2Afinacion();
            double[] h1 = { 70, 70, 70, 70, 63, 70, 70, 70 };
            Array.Copy(h1, hueco.Lecturas[0], 8);
            double[] ch = hueco.Correccion(out mot);
            Si("afinacion: un hueco del local (1 kHz 7 dB abajo) no se sube (" + N(ch[4], "+0.0;-0.0") + ", «" + mot[4] + "»)", ch[4] <= 0 && mot[4] != null && mot[4].StartsWith("hueco"));
            // lo que cambia mucho de un punto a otro (con 3 o mas) tampoco sube
            RocolaOsBillar2Afinacion varia = new RocolaOsBillar2Afinacion();
            double[][] vv = { new double[] { 70, 73, 70, 70, 70, 70, 70, 70 }, new double[] { 70, 63, 70, 70, 70, 70, 70, 70 }, new double[] { 70, 68, 70, 70, 70, 70, 70, 70 } };
            for (int p = 0; p < 3; p++) Array.Copy(vv[p], varia.Lecturas[p], 8);
            double[] cv = varia.Correccion(out mot);
            Si("afinacion: una banda que cambia 4 dB o mas de un punto a otro no se sube (125 Hz: " + N(cv[1], "+0.0;-0.0") + ", «" + mot[1] + "»)", cv[1] <= 0 && mot[1] != null && mot[1].StartsWith("cambia"));
            // los topes
            RocolaOsBillar2Afinacion topes = new RocolaOsBillar2Afinacion();
            double[] t1 = { 64, 66, 70, 70, 70, 70, 90, 70 };   // 4 kHz +20
            Array.Copy(t1, topes.Lecturas[0], 8);
            double[] ct = topes.Correccion(out mot);
            RocolaOsBillar2Afinacion topes2 = new RocolaOsBillar2Afinacion();
            double[] t2b = { 62, 62, 70, 70, 70, 70, 64, 61 };   // 63 y 125 Hz -8, 4 kHz -6, 8 kHz -9 (no son huecos: 3 dB o menos bajo su vecina)
            Array.Copy(t2b, topes2.Lecturas[0], 8);
            string[] mot2; double[] ct2 = topes2.Correccion(out mot2);
            Si("afinacion: los topes: 4 kHz con +20 dB baja " + N(ct[6], "0.0") + " (lo mas, -6); con -8 dB, 63 Hz sube " + N(ct2[0], "+0.0") + " (nunca), 125 Hz " + N(ct2[1], "+0.0") + " (hasta +3); 8 kHz con -9 sube " + N(ct2[7], "+0.0") + " (hasta +2)",
                Math.Abs(ct[6] + 6) < 1e-9 && ct2[0] <= 0 && Math.Abs(ct2[1] - 3) < 1e-9 && Math.Abs(ct2[7] - 2) < 1e-9);
            // sin las bandas de la voz no hay correccion
            RocolaOsBillar2Afinacion falta = new RocolaOsBillar2Afinacion();
            double[] t2 = { 70, 70, double.NaN, 70, 70, 70, 70, 70 };
            Array.Copy(t2, falta.Lecturas[0], 8);
            Si("afinacion: un punto sin 250 Hz (de la voz) no vale: sin correccion", falta.Correccion() == null && falta.Validos == 0);
            // dBA: se le devuelve lo que la A le quita al grave
            RocolaOsBillar2Afinacion a = new RocolaOsBillar2Afinacion(); a.Pond = 0;
            for (int b = 0; b < 8; b++) a.Lecturas[0][b] = 70 + RocolaOsBillar2Afinacion.Ponderacion[0][b];
            double[] fa; int[] na; double[] da; a.Forma(out fa, out na, out da);
            double mx = 0; for (int b = 0; b < 8; b++) mx = Math.Max(mx, Math.Abs(fa[b]));
            Si("afinacion: un local plano medido en dBA sale plano (" + N(mx, "0.0000") + " dB)", mx < 1e-9);
            // el texto de rocola_os.txt, ida y vuelta
            double[] tx = { 71.5, double.NaN, 80, 79.2, 78, 77, 70, 64 };
            string s = RocolaOsBillar2Afinacion.Texto(tx);
            double[] back = RocolaOsBillar2Afinacion.Lee(s);
            igual = true; for (int b = 0; b < 8; b++) if (!(double.IsNaN(tx[b]) ? double.IsNaN(back[b]) : Math.Abs(back[b] - tx[b]) < 1e-9)) igual = false;
            Si("afinacion: el texto «" + s + "» va y vuelve, y vale", igual && RocolaOsBillar2Afinacion.TextoVale(s) && !RocolaOsBillar2Afinacion.TextoVale("1;2;3") && !RocolaOsBillar2Afinacion.TextoVale("x;x;x;x;x;x;x;200"));
        }

        // ================================================================ guardar y leer (rocola_os.txt)
        private static void Guarda(string dir)
        {
            Si("guarda: ROCOLA_OS puesto (" + RocolaOs.Variable + ") y la carpeta de datos es " + dir, RocolaOs.Activo && Path.GetFullPath(Form1.CarpetaDatos) == Path.GetFullPath(dir));
            double[][] pts = { new double[] { 72, 74, 73, 71, 70, 69, 67, 61 }, new double[] { 80, 79, 81, 80, 79, 78, 76, 70 }, new double[] { 65, double.NaN, 66, 64, 64, 62, 60, 52 } };
            bool ok = true;
            for (int p = 0; p < pts.Length; p++) ok &= RocolaOs.PonAjuste(RocolaOsBillar2.KPunto + (p + 1), RocolaOsBillar2Afinacion.Texto(pts[p]));
            ok &= RocolaOs.PonAjuste(RocolaOsBillar2.KPond, "A");
            ok &= RocolaOs.PonAjuste(RocolaOsBillar2.KPasoAlto, "75");
            ok &= RocolaOs.PonAjuste(RocolaOsBillar2.KInclinacion, "-1.25");
            ok &= RocolaOs.PonAjuste(RocolaOsBillar2.KBajo, "1");
            Si("guarda: los ajustes se aceptan", ok);
            Si("guarda: los que no valen no se aceptan (200 dB, -3 dB/oct, 30 Hz)", !RocolaOs.PonAjuste(RocolaOsBillar2.KPunto + "4", "x;x;x;x;x;x;x;200") && !RocolaOs.PonAjuste(RocolaOsBillar2.KInclinacion, "-3") && !RocolaOs.PonAjuste(RocolaOsBillar2.KPasoAlto, "30"));
            RocolaOs.GuardaYa();
            string archivo = Path.Combine(dir, "rocola_os.txt");
            string[] lineas = File.Exists(archivo) ? File.ReadAllLines(archivo) : new string[0];
            Func<string, bool> tiene = l => Array.IndexOf(lineas, l) >= 0;
            Si("guarda: rocola_os.txt tiene los tres puntos, la ponderacion y los demas (" + lineas.Length + " renglones)",
                tiene("billar_afina_1=72;74;73;71;70;69;67;61") && tiene("billar_afina_3=65;x;66;64;64;62;60;52") && tiene("billar_afina_pond=A") && tiene("billar_paso_alto=75") && tiene("billar_inclinacion=-1.25") && tiene("billar_bajo_virtual=1"));
            RocolaOsBillar2Afinacion a = RocolaOsBillar2.AfinacionDeAjustes();
            bool igual = a.Pond == 0 && a.Validos == 3 && !a.Tiene(3);
            for (int p = 0; p < 3; p++) for (int b = 0; b < 8; b++) igual &= double.IsNaN(pts[p][b]) ? double.IsNaN(a.Lecturas[p][b]) : Math.Abs(a.Lecturas[p][b] - pts[p][b]) < 1e-9;
            Si("guarda: y se vuelve a leer igual (3 puntos validos, dBA)", igual);
            RocolaOsBillar2Params pr = RocolaOsBillar2.ParamsDeAjustes();
            Si("guarda: los parametros de la cadena salen de ahi (paso alto 75, -1.25 dB/oct, bajo virtual, con afinacion)", pr.PasoAltoHz == 75 && Math.Abs(pr.Inclinacion + 1.25) < 1e-9 && pr.BajoVirtual && pr.Afinacion != null);
            RocolaOs.PonAjuste(RocolaOsBillar2.KPond, "C");   // (lo que se anoto parece dBC)
            string h1 = RocolaOsBillar2.Huella();
            RocolaOs.PonAjuste(RocolaOsBillar2.KPunto + "2", "80;85;81;80;79;78;76;70");   // (125 Hz +6 en un punto: su media sube 2 dB y su correccion 1,6)
            string h2 = RocolaOsBillar2.Huella();
            RocolaOs.PonAjuste(RocolaOsBillar2.KAfinacion, "0");
            string h3 = RocolaOsBillar2.Huella();
            Si("guarda: la huella cambia al cambiar una lectura que cambia la curva y al quitar la afinacion (la medida de la cancion ya no vale); una que no la cambia (un hueco, que no sube), no", h1 != h2 && h2 != h3 && HuellaSinCambio());
            Info("guarda: huella " + h3);
        }

        // una lectura que no cambia la curva (8 kHz de un punto: esa banda es un hueco del local y no sube) deja la misma huella: la medida sigue valiendo
        private static bool HuellaSinCambio()
        {
            RocolaOs.PonAjuste(RocolaOsBillar2.KAfinacion, "1");
            string a = RocolaOsBillar2.Huella();
            RocolaOs.PonAjuste(RocolaOsBillar2.KPunto + "2", "80;85;81;80;79;78;76;64");
            string b = RocolaOsBillar2.Huella();
            return a == b;
        }

        // ================================================================ cpu
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Timespec { public long Seg; public long Nseg; }
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "clock_gettime")]
        private static extern int ClockGettime(int reloj, out Timespec t);
        private static double CpuMs() { Timespec t; if (ClockGettime(3, out t) == 0) return t.Seg * 1000.0 + t.Nseg / 1e6; return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency; }
        private static void Cpu()
        {
            int seg = 30, n = seg * Tasa;
            float[] e = Estereo(Rosa(n, 1, 0.1), Rosa(n, 2, 0.1));
            RocolaOsBillar2Params frio = Completa(true); frio.PasoAltoHz = 81; frio.NocheHz = 100;
            double c0 = CpuMs(); RocolaOsBillar2Diseno df = RocolaOsBillar2Diseno.Para(Tasa, frio); double cd = CpuMs() - c0;
            Info("cpu: hacer el diseno (con noche y bajo virtual, en frio): " + N(cd, "0.0") + " ms de procesador (" + N(df.MsDiseno, "0.0") + " de reloj); K=" + df.K + " N=" + df.N + " B=" + df.B + "; la primera lectura pide " + (df.B + df.K) + " cuadros (" + N((df.B + df.K) * 1000.0 / Tasa, "0") + " ms)");
            var casos = new List<Tuple<string, RocolaOsBillar2Params, Func<double>>>();
            casos.Add(Tuple.Create("de dia (paso alto y curva)", Completa(false), (Func<double>)null));
            RocolaOsBillar2Params pn = Completa(false); pn.NocheHz = 100;
            casos.Add(Tuple.Create("en la bajada de la noche (dos filtros)", pn, (Func<double>)(() => 0.5)));
            casos.Add(Tuple.Create("con el bajo virtual", Completa(true), (Func<double>)null));
            RocolaOsBillar2Params pt = Completa(true); pt.NocheHz = 100;
            casos.Add(Tuple.Create("todo (noche a medias y bajo virtual)", pt, (Func<double>)(() => 0.5)));
            float[] b = new float[4410];
            foreach (var c in casos)
            {
                RocolaOsBillar2Diseno.Para(Tasa, c.Item2);
                double mejor = double.MaxValue;
                for (int v = 0; v < 3; v++)
                {
                    RocolaOsBillar2Tramo t = Tramo(c.Item2, e, false, c.Item3);
                    double a = CpuMs();
                    while (t.Read(b, 0, b.Length) > 0) { }
                    mejor = Math.Min(mejor, CpuMs() - a);
                }
                double ms = mejor / seg;
                Si("cpu: " + c.Item1 + ": " + N(ms, "0.00") + " ms de procesador por segundo de musica aqui (" + N(ms / 10.0, "0.00") + " % de un nucleo; tope 10 ms)", ms < 10.0);
            }
        }
    }
}

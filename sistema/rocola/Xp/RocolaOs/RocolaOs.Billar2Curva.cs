// ROCOLA OS (la cadena nueva del billar, B; CADENA_NUEVA.md §3, CADENA_NUEVA_B.md): LA CURVA DEL BILLAR Y EL DISENO DE SU FILTRO. Solo cuentas
// (sin NAudio, sin Form1, sin ajustes): lo que se oye lo arma RocolaOs.Billar2Etapa.cs con lo que sale de aqui.
//
// LAS TRES PARTES DE LA CURVA (todo junto en UNA sola etapa de tono, con la del paso alto: un solo filtro FIR de FASE LINEAL):
//   T(f) LA CURVA OBJETIVO: plana hasta 1 kHz y despues «Inclinacion» dB por octava (-0,75 de fabrica, de 0 a -1,5), con la rodilla suave
//        (un cuarto de octava). Sin afinacion medida, T es lo unico del local (lo que se supone de unas bocinas planas).
//   A(f) LA AFINACION CON EL CELULAR (RocolaOsBillar2Afinacion: de 1 a 8 puntos del local, promediados): lo que el local se aparta de plano,
//        corregido al 70 % (un punto) o al 80 % (tres o mas), con 1 dB de zona muerta; cada banda baja hasta 6 dB y sube hasta 3 (8 kHz hasta 2,
//        63 Hz nunca); una banda que es un HUECO del local (3 dB por debajo de sus vecinas, o que cambia mas de 4 dB de un punto a otro) no sube.
//        Entre bandas, media curva de coseno en octavas; debajo de 63 Hz, lo de 63; encima de 8 kHz se va a la mitad en 16 kHz.
//   V(f) LA CLARIDAD DE LA VOZ, fija: una campana de -2 dB en 315 Hz (lo de 250 a 400, donde se embarra la voz en una nave) y otra de +2 dB en
//        2,8 kHz (lo de 2 a 4, donde se entienden las consonantes), de 0,4 octavas de sigma (a media altura en 230-430 Hz y 2-3,8 kHz).
//   C(f) = T + A + V, acotada de -9 a +3 dB: la curva del billar. Y por debajo, EL PASO ALTO (PasoAlto: Butterworth de cuarto orden, 24 dB por
//        octava, en el punto donde las bocinas dejan de sonar; 80 Hz de fabrica). De noche, el corte de noche del modo noche (80 Hz) va en ESTA
//        misma curva: el paso alto de noche es el mayor de los dos (no dos filtros en serie).
//
// EL FILTRO: la respuesta de fase cero M(f) = PasoAlto(f)·10^(C(f)/20), muestreada en L = 8K puntos, su respuesta al impulso (real y simetrica)
// recortada a 2K + 1 coeficientes con una ventana de Tukey (plana en el 70 % del centro, coseno en el 30 % de las orillas) y pasada a la FFT de
// la etapa (N, potencia de 2 de 2K + 1 024 o mas: 8 192 a 80 Hz). K = 4 periodos del paso alto (2 205 coeficientes a cada lado a 80 Hz y 44,1 kHz: 50 ms): con eso el
// error contra la curva pedida no pasa de 0,005 dB donde la curva esta sobre -20 dB (medido con numpy y en --prueba-billar2; con la ventana de
// Kaiser del subgrave de hoy eran 0,8 dB: Kaiser estrecha la respuesta del grave). El rizo de la banda de paso: 0,016 dB.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rocola
{
    // LO QUE DECIDE LA FORMA DEL TRAMO B (todo fijo por cancion). Se arma con lo de rocola_os.txt (RocolaOsBillar2.ParamsDeAjustes) o a mano (las pruebas).
    internal sealed class RocolaOsBillar2Params
    {
        internal double PasoAltoHz = RocolaOsBillar2Curva.PasoAltoDeFabrica;   // donde las bocinas dejan de sonar (de 50 a 150)
        internal double NocheHz;                 // el corte del grave de noche (0: sin noche; si no es mayor que el paso alto, no cambia nada)
        internal bool ConPasoAlto = true;        // (solo las pruebas lo apagan, para medir la curva sola)
        internal int Orden = 4;                  // el del Butterworth del paso alto: 4 = 24 dB por octava (las pruebas comparan 2 y 3)
        internal bool ConCurva = true;           // (las pruebas: el paso alto solo)
        internal double Inclinacion = RocolaOsBillar2Curva.InclinacionDeFabrica;   // dB por octava sobre 1 kHz (de 0 a -1,5)
        internal double ClaridadMenos = RocolaOsBillar2Curva.ClaridadMenosDeFabrica;  // dB que se quitan en 315 Hz (de 0 a 4)
        internal double ClaridadMas = RocolaOsBillar2Curva.ClaridadMasDeFabrica;      // dB que se ponen en 2,8 kHz (de 0 a 3)
        internal double[] Afinacion;             // la correccion del local por banda (8, en dB; RocolaOsBillar2Afinacion.Correccion) o null
        internal bool BajoVirtual;               // el bajo virtual fijo (apagado de fabrica: decide la prueba ciega)
        internal double BajoVirtualDb = RocolaOsBillar2Bajo.MezclaDeFabricaDb;   // su mezcla (de -14 a -4 dB)

        // LA HUELLA: todo lo que cambia lo que sale DE DIA (sin la noche, que es del reloj y no se mide). El marco la junta con la medida de cada
        // cancion (la medida se hace sobre la salida del tramo, de dia): si cambia, esa medida ya no vale (CADENA_NUEVA.md §8).
        internal string Huella()
        {
            StringBuilder sb = new StringBuilder("b2v1");
            Action<double> n = v => sb.Append(';').Append(v.ToString("0.###", CultureInfo.InvariantCulture));
            n(ConPasoAlto ? PasoAltoHz : 0); if (Orden != 4) sb.Append(";o").Append(Orden);
            if (ConCurva) { n(Inclinacion); n(ClaridadMenos); n(ClaridadMas); if (Afinacion != null) foreach (double a in Afinacion) n(a); else sb.Append(";-"); }
            else sb.Append(";plana");
            if (BajoVirtual) { sb.Append(";bv"); n(BajoVirtualDb); }
            return sb.ToString();
        }
        // la clave del diseno hecho: la huella y el corte de noche (que si cambia el filtro de noche)
        internal string Clave() { return Huella() + ";n" + (NocheHz > PasoAltoHz && ConPasoAlto ? NocheHz : 0).ToString("0.###", CultureInfo.InvariantCulture); }
        internal RocolaOsBillar2Params Copia() { RocolaOsBillar2Params p = (RocolaOsBillar2Params)MemberwiseClone(); if (Afinacion != null) p.Afinacion = (double[])Afinacion.Clone(); return p; }
    }

    internal static class RocolaOsBillar2Curva
    {
        internal const double PasoAltoDeFabrica = 80.0, PasoAltoMin = 50.0, PasoAltoMax = 150.0;
        internal const double InclinacionDeFabrica = -0.75, InclinacionMin = -1.5, InclinacionMax = 0.0;
        internal const double ClaridadMenosDeFabrica = 2.0, ClaridadMenosMax = 4.0, ClaridadMasDeFabrica = 2.0, ClaridadMasMax = 3.0;
        internal const double ClaridadGraveHz = 315.0, ClaridadVozHz = 2828.0, ClaridadSigmaOct = 0.4;
        internal const double RodillaHz = 1000.0, RodillaOct = 0.25;
        internal const double CurvaMinDb = -9.0, CurvaMaxDb = 3.0;
        internal const double PeriodosK = 4.0, TukeyAlfa = 0.3;
        internal static readonly double[] Centros = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };

        // EL PASO ALTO: Butterworth de cuarto orden (solo la magnitud: la fase la pone el filtro, cero). -3 dB en fc, -24 dB una octava abajo.
        internal static double PasoAlto(double f, double fc)
        {
            if (fc <= 0) return 1.0;
            if (f <= 0) return 0.0;
            double r = fc / f, r2 = r * r, r4 = r2 * r2;
            return 1.0 / Math.Sqrt(1.0 + r4 * r4);
        }
        // el de otro orden (las pruebas: 2 = 12 dB/oct, 3 = 18)
        internal static double PasoAlto(double f, double fc, int orden)
        {
            if (orden == 4) return PasoAlto(f, fc);
            if (fc <= 0) return 1.0;
            if (f <= 0) return 0.0;
            return 1.0 / Math.Sqrt(1.0 + Math.Pow(fc / f, 2 * orden));
        }
        // un Butterworth de segundo orden (para lo de debajo de 25 Hz de lo que va al bajo virtual)
        internal static double PasoAlto2(double f, double fc)
        {
            if (fc <= 0) return 1.0;
            if (f <= 0) return 0.0;
            double r = fc / f, r2 = r * r;
            return 1.0 / Math.Sqrt(1.0 + r2 * r2);
        }
        // LA CURVA OBJETIVO: 0 hasta 1 kHz y despues s dB por octava, con la rodilla suave (softplus de un cuarto de octava)
        internal static double Objetivo(double f, double dbPorOct)
        {
            if (dbPorOct == 0 || f <= 0) return 0.0;
            double u = Math.Log(f / RodillaHz, 2.0) / RodillaOct;
            double sp = u > 30 ? u : Math.Log(1.0 + Math.Exp(u));
            return dbPorOct * RodillaOct * sp;
        }
        // UNA CAMPANA en octavas (Gauss en log2 f), de altura 1
        internal static double Campana(double f, double centro, double sigmaOct)
        {
            if (f <= 0) return 0.0;
            double u = Math.Log(f / centro, 2.0) / sigmaOct;
            return Math.Exp(-0.5 * u * u);
        }
        internal static double Claridad(double f, double menos, double mas)
        {
            return -menos * Campana(f, ClaridadGraveHz, ClaridadSigmaOct) + mas * Campana(f, ClaridadVozHz, ClaridadSigmaOct);
        }
        // LA AFINACION entre bandas: media curva de coseno en octavas; debajo de 63 Hz, lo de 63; de 8 a 16 kHz baja a la mitad (y se queda)
        internal static double Afinacion(double f, double[] a)
        {
            if (a == null || a.Length < 8) return 0.0;
            if (f <= Centros[0]) return a[0];
            if (f >= Centros[7])
            {
                double u = Math.Min(1.0, Math.Log(f / Centros[7], 2.0));
                return a[7] * (1.0 - 0.5 * 0.5 * (1.0 - Math.Cos(Math.PI * u)));
            }
            int i = 0; while (i < 6 && f >= Centros[i + 1]) i++;
            double t = Math.Log(f / Centros[i], 2.0) / Math.Log(Centros[i + 1] / Centros[i], 2.0);
            double w = 0.5 * (1.0 - Math.Cos(Math.PI * Math.Max(0.0, Math.Min(1.0, t))));
            return a[i] + (a[i + 1] - a[i]) * w;
        }
        // LA CURVA DEL BILLAR (C, dB): objetivo + afinacion + claridad, acotada de -9 a +3
        internal static double CurvaDb(double f, RocolaOsBillar2Params p)
        {
            if (!p.ConCurva) return 0.0;
            double c = Objetivo(f, p.Inclinacion) + Afinacion(f, p.Afinacion) + Claridad(f, p.ClaridadMenos, p.ClaridadMas);
            return c < CurvaMinDb ? CurvaMinDb : (c > CurvaMaxDb ? CurvaMaxDb : c);
        }
        // LO QUE HACE EL TRAMO (sin el bajo virtual, que no es lineal): la curva y el paso alto, en dB, de dia (o con la noche entera)
        internal static double TotalDb(double f, RocolaOsBillar2Params p, bool deNoche)
        {
            double hp = 1.0;
            if (p.ConPasoAlto) hp = PasoAlto(f, deNoche && p.NocheHz > p.PasoAltoHz ? p.NocheHz : p.PasoAltoHz, p.Orden);
            return 20.0 * Math.Log10(Math.Max(1e-12, hp)) + CurvaDb(f, p);
        }
    }

    // EL DISENO DEL FILTRO DEL TRAMO para una tasa y unos parametros: lo que la etapa necesita, ya hecho. Se guarda (hasta 8) por huella y tasa:
    // cancion tras cancion es el mismo. Hacerlo cuesta unos milisegundos (RocolaOsBillar2.Precalienta lo hace de fondo al arrancar y al cambiar
    // un ajuste): el que arma la cadena en el hilo de la pantalla casi siempre lo encuentra hecho.
    internal sealed class RocolaOsBillar2Diseno
    {
        internal readonly int Tasa, K, N, B;
        internal readonly double[] Dia;        // el filtro de dia en la FFT de N (real: fase cero), k = 0..N/2
        internal readonly double[] Noche;      // con el corte de noche (null si no cambia nada)
        internal readonly double[] Fuente;     // lo que va al bajo virtual: lo que el paso alto quita, sin lo de debajo de 25 Hz (null sin bajo virtual)
        internal readonly double[] TapsDia, TapsNoche, TapsFuente;   // sus coeficientes h[0..K] (simetricos: h[-n] = h[n]), para el empalme
        internal readonly RocolaOsBillar2Bajo.Coefs Bajo;   // los filtros del bajo virtual (null sin el)
        internal readonly double MezclaBajo;   // lo que se suma del bajo virtual
        internal readonly string Huella, Clave;   // la de la medida (sin la noche) y la del diseno (con ella)
        internal readonly double MsDiseno;     // lo que tardo hacerlo
        internal const double FuenteBajoHz = 25.0;

        private RocolaOsBillar2Diseno(int tasa, RocolaOsBillar2Params p)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Tasa = tasa;
            Huella = p.Huella(); Clave = p.Clave();
            double fc = p.ConPasoAlto ? p.PasoAltoHz : RocolaOsBillar2Curva.PasoAltoMin;   // (sin paso alto, el largo de uno de 50 Hz: la curva tambien tiene graves)
            K = Math.Max(64, (int)Math.Ceiling(RocolaOsBillar2Curva.PeriodosK * tasa / fc));
            int n = 4096; while (n < 2 * K + 1024) n <<= 1;   // (B de 1 024 o mas: con Spotify, que llega a su ritmo con unos 250 ms de colchon, la primera lectura -B + K- no pasa de 0,16 s a 44,1 kHz)
            N = n; B = N - 2 * K;
            int L = 16; while (L < 8 * K) L <<= 1;
            RocolaOsBillar2Fft fL = new RocolaOsBillar2Fft(L), fN = new RocolaOsBillar2Fft(N);
            double[] ventana = new double[K + 1];
            int plana = (int)Math.Floor((1.0 - RocolaOsBillar2Curva.TukeyAlfa) * K), orilla = Math.Max(1, K - plana);
            for (int i = 0; i <= K; i++) ventana[i] = i <= plana ? 1.0 : 0.5 * (1.0 + Math.Cos(Math.PI * (i - plana) / orilla));
            RocolaOsBillar2Params pp = p;
            TapsDia = Taps(fL, ventana, L, tasa, f => (pp.ConPasoAlto ? RocolaOsBillar2Curva.PasoAlto(f, pp.PasoAltoHz, pp.Orden) : 1.0) * Math.Pow(10.0, RocolaOsBillar2Curva.CurvaDb(f, pp) / 20.0));
            Dia = EnLaFft(fN, TapsDia);
            if (p.ConPasoAlto && p.NocheHz > p.PasoAltoHz)
            {
                TapsNoche = Taps(fL, ventana, L, tasa, f => RocolaOsBillar2Curva.PasoAlto(f, pp.NocheHz, pp.Orden) * Math.Pow(10.0, RocolaOsBillar2Curva.CurvaDb(f, pp) / 20.0));
                Noche = EnLaFft(fN, TapsNoche);
            }
            if (p.BajoVirtual && p.ConPasoAlto)
            {
                TapsFuente = Taps(fL, ventana, L, tasa, f => (1.0 - RocolaOsBillar2Curva.PasoAlto(f, pp.PasoAltoHz, pp.Orden)) * RocolaOsBillar2Curva.PasoAlto2(f, FuenteBajoHz));
                Fuente = EnLaFft(fN, TapsFuente);
                Bajo = RocolaOsBillar2Bajo.Coefs.Para(tasa, p.PasoAltoHz);
                MezclaBajo = RocolaOsBillar2Bajo.Mezcla(p.BajoVirtualDb);
            }
            MsDiseno = sw.Elapsed.TotalMilliseconds;
        }

        // los coeficientes h[0..K] de una respuesta de fase cero M(f): muestreada en L puntos, su respuesta al impulso y la ventana de Tukey
        private static double[] Taps(RocolaOsBillar2Fft f, double[] ventana, int L, int tasa, Func<double, double> m)
        {
            int h = L / 2;
            double[] re = new double[h + 1], im = new double[h + 1], y = new double[L];
            for (int k = 0; k <= h; k++) re[k] = m((double)k * tasa / L);
            f.Atras(re, im, y, 0);
            int K = ventana.Length - 1;
            double[] t = new double[K + 1];
            for (int i = 0; i <= K; i++) t[i] = y[i] * ventana[i];   // (y[L - i] = y[i]: la respuesta es par)
            return t;
        }
        // esos coeficientes, centrados en 0 (h[n] en n y en N - n), por la FFT de N: la respuesta real que multiplica al espectro de cada bloque
        private static double[] EnLaFft(RocolaOsBillar2Fft f, double[] taps)
        {
            int n = f.N, K = taps.Length - 1;
            double[] x = new double[n], re = new double[n / 2 + 1], im = new double[n / 2 + 1];
            x[0] = taps[0];
            for (int i = 1; i <= K; i++) { x[i] = taps[i]; x[n - i] = taps[i]; }
            f.Adelante(x, 0, re, im);
            return re;   // (im: restos de redondeo de 1e-17)
        }

        // LO QUE HACE EL FILTRO HECHO en una frecuencia (la suma de sus coeficientes con el coseno): para la prueba y el dibujo
        internal static double Respuesta(double[] taps, int tasa, double f)
        {
            double w = 2.0 * Math.PI * f / tasa, s = taps[0];
            for (int i = 1; i < taps.Length; i++) s += 2.0 * taps[i] * Math.Cos(w * i);
            return s;
        }

        // ---------- LOS HECHOS (por huella y tasa) ----------
        private static readonly List<RocolaOsBillar2Diseno> hechos = new List<RocolaOsBillar2Diseno>();
        private static readonly object cerrojo = new object();
        internal static RocolaOsBillar2Diseno Para(int tasa, RocolaOsBillar2Params p)
        {
            string h = p.Clave();
            lock (cerrojo)
                foreach (RocolaOsBillar2Diseno d in hechos) if (d.Tasa == tasa && d.Clave == h) return d;
            RocolaOsBillar2Diseno nuevo = new RocolaOsBillar2Diseno(tasa, p.Copia());   // (fuera del cerrojo: dos a la vez solo hacen el mismo dos veces)
            lock (cerrojo)
            {
                foreach (RocolaOsBillar2Diseno d in hechos) if (d.Tasa == tasa && d.Clave == h) return d;
                hechos.Insert(0, nuevo);
                if (hechos.Count > 8) hechos.RemoveAt(hechos.Count - 1);
            }
            return nuevo;
        }
        internal static bool YaHecho(int tasa, RocolaOsBillar2Params p)
        {
            string h = p.Clave();
            lock (cerrojo) foreach (RocolaOsBillar2Diseno d in hechos) if (d.Tasa == tasa && d.Clave == h) return true;
            return false;
        }
    }

    // LA AFINACION CON EL CELULAR, DE VARIOS PUNTOS (la de Ctrl+F3 tenia uno; el dueno pidio el 27-10-2026 «lo mas simple posible»: aqui se puede
    // usar uno solo, o hasta ocho). Cada punto: lo que marco la app en las ocho bandas (63 Hz a 8 kHz; NaN = sin anotar), con la ponderacion de la
    // app (A o C: se le devuelve lo que la A le quita al grave). De cada punto vale su FORMA (cada banda frente a la zona de la voz, 250 Hz a 2 kHz,
    // del mismo punto): un punto pegado a una bocina, que marca todo 10 dB mas, pesa igual que uno lejano. La forma del local, por banda, es la
    // media de los puntos que la tienen; con cinco o mas se quitan la mas alta y la mas baja (un punto junto al woofer no manda en 63 Hz).
    internal sealed class RocolaOsBillar2Afinacion
    {
        internal const int Puntos = 8, Bandas = 8;
        internal const double ZonaMuerta = 1.0, BajaMax = -6.0, HuecoDb = 3.0, DispersionMax = 4.0;
        internal const double FuerzaUno = 0.7, FuerzaVarios = 0.8;
        internal static readonly double[] SubeMax = { 0, 3, 3, 3, 3, 3, 3, 2 };
        // lo que la ponderacion de la app le quita a cada banda (dB, IEC 61672): A y C (los mismos de AfinarLocal.cs)
        internal static readonly double[][] Ponderacion = {
            new double[] { -26.2, -16.1, -8.6, -3.2, 0, 1.2, 1.0, -1.1 },
            new double[] { -0.8, -0.2, 0, 0, 0, -0.2, -0.8, -3.0 } };
        internal readonly double[][] Lecturas = new double[Puntos][];
        internal int Pond = 1;   // 0 = dBA, 1 = dBC (la que se recomienda)
        internal RocolaOsBillar2Afinacion() { for (int p = 0; p < Puntos; p++) { Lecturas[p] = new double[Bandas]; for (int b = 0; b < Bandas; b++) Lecturas[p][b] = double.NaN; } }

        // un punto vale si tiene las cuatro bandas de la voz
        internal bool Vale(int p)
        {
            double[] l = Lecturas[p];
            for (int b = 2; b <= 5; b++) if (double.IsNaN(l[b])) return false;
            return true;
        }
        internal bool Tiene(int p) { foreach (double v in Lecturas[p]) if (!double.IsNaN(v)) return true; return false; }
        internal int Validos { get { int n = 0; for (int p = 0; p < Puntos; p++) if (Vale(p)) n++; return n; } }

        // LO QUE SE SABE DEL LOCAL por banda: la forma (dB frente a la voz; NaN si ningun punto la tiene), cuantos puntos y cuanto cambia entre ellos
        internal void Forma(out double[] forma, out int[] cuantos, out double[] dispersion)
        {
            forma = new double[Bandas]; cuantos = new int[Bandas]; dispersion = new double[Bandas];
            List<double>[] v = new List<double>[Bandas];
            for (int b = 0; b < Bandas; b++) v[b] = new List<double>();
            double[] w = Ponderacion[Pond == 0 ? 0 : 1];
            for (int p = 0; p < Puntos; p++)
            {
                if (!Vale(p)) continue;
                double[] l = Lecturas[p];
                double r = 0; for (int b = 2; b <= 5; b++) r += l[b] - w[b];
                r /= 4.0;
                for (int b = 0; b < Bandas; b++) if (!double.IsNaN(l[b])) v[b].Add(l[b] - w[b] - r);
            }
            for (int b = 0; b < Bandas; b++)
            {
                List<double> x = v[b];
                cuantos[b] = x.Count;
                if (x.Count == 0) { forma[b] = double.NaN; dispersion[b] = 0; continue; }
                double m = 0; foreach (double a in x) m += a; m /= x.Count;
                double s = 0; foreach (double a in x) s += (a - m) * (a - m);
                dispersion[b] = x.Count > 1 ? Math.Sqrt(s / (x.Count - 1)) : 0;
                if (x.Count >= 5)
                {
                    x.Sort();
                    double t = 0; for (int i = 1; i < x.Count - 1; i++) t += x[i];
                    m = t / (x.Count - 2);
                }
                forma[b] = m;
            }
        }
        internal double Fuerza { get { int n = Validos; return n <= 1 ? FuerzaUno : FuerzaUno + (FuerzaVarios - FuerzaUno) * Math.Min(1.0, (n - 1) / 2.0); } }

        // LA CORRECCION por banda (dB, lo que va en A de la curva), o null si ningun punto tiene las bandas de la voz. Por que no sube cada una: en 'motivo'.
        internal double[] Correccion() { string[] m; return Correccion(out m); }
        internal double[] Correccion(out string[] motivo)
        {
            motivo = new string[Bandas];
            if (Validos == 0) return null;
            double[] f, d; int[] n;
            Forma(out f, out n, out d);
            double fz = Fuerza;
            int puntos = Validos;
            double[] c = new double[Bandas];
            for (int b = 0; b < Bandas; b++)
            {
                if (double.IsNaN(f[b])) { motivo[b] = "sin medir"; continue; }
                double x = -fz * f[b];
                if (x > ZonaMuerta) x -= ZonaMuerta; else if (x < -ZonaMuerta) x += ZonaMuerta; else x = 0;
                if (x < BajaMax) { x = BajaMax; motivo[b] = "baja lo mas (" + BajaMax.ToString("0", CultureInfo.InvariantCulture) + " dB)"; }
                if (x > 0)
                {
                    // un hueco del local no se sube: o queda 3 dB por debajo de sus vecinas medidas, o cambia mucho de un punto a otro
                    double izq = b > 0 ? f[b - 1] : double.NaN, der = b < Bandas - 1 ? f[b + 1] : double.NaN;
                    double vecina = double.IsNaN(izq) ? der : (double.IsNaN(der) ? izq : Math.Min(izq, der));
                    if (!double.IsNaN(vecina) && f[b] < vecina - HuecoDb) { x = 0; motivo[b] = "hueco del local: no se sube"; }
                    else if (puntos >= 3 && d[b] > DispersionMax) { x = 0; motivo[b] = "cambia mucho de un punto a otro: no se sube"; }
                    else if (x > SubeMax[b]) { x = SubeMax[b]; motivo[b] = SubeMax[b] == 0 ? "63 Hz nunca sube" : "sube lo mas (+" + SubeMax[b].ToString("0", CultureInfo.InvariantCulture) + " dB)"; }
                }
                c[b] = x;
            }
            return c;
        }

        // ---------- rocola_os.txt: «billar_afina_N=63;125;...;8000» (los dB de la app, «x» = sin anotar) y «billar_afina_pond=A» o «C» ----------
        internal static string Texto(double[] l)
        {
            StringBuilder sb = new StringBuilder();
            for (int b = 0; b < Bandas; b++)
            {
                if (b > 0) sb.Append(';');
                double v = l[b];
                sb.Append(double.IsNaN(v) ? "x" : v.ToString("0.#", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
        internal const double LecturaMin = 40, LecturaMax = 120;
        internal static double[] Lee(string texto)
        {
            double[] l = new double[Bandas];
            string[] p = (texto ?? "").Split(';');
            for (int b = 0; b < Bandas; b++)
            {
                double v;
                l[b] = (b < p.Length && double.TryParse(p[b].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= LecturaMin && v <= LecturaMax) ? v : double.NaN;
            }
            return l;
        }
        internal static bool TextoVale(string t)
        {
            if (t == null) return false;
            if (t.Length == 0) return true;
            string[] p = t.Split(';');
            if (p.Length != Bandas) return false;
            foreach (string s in p)
            {
                if (s == "x") continue;
                double v;
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || v < LecturaMin || v > LecturaMax) return false;
            }
            return true;
        }
    }
}

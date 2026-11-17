// EL ECUALIZADOR DEL DUENO (17 de noviembre de 2026): diez bandas, de 31 Hz a 16 kHz, de -12 a +12 dB en pasos de 0,5 dB, para que el
// dueno ajuste a mano el sonido de la rocola (el mueble si sabe hacer bajo). Va al FINAL de la cadena, justo DESPUES de «que el bombo no
// hunda la voz» y ANTES del limitador de pico real (Form1.Sonido.cs, ArmaCadena): es la correccion de las bocinas, igual que el limitador,
// asi que «Escuchar sin mejoras» y la comparacion a ciegas NO la quitan.
//
// LAS BANDAS: 31 Hz es un estante de graves (low shelf), 16 kHz un estante de agudos (high shelf) y las otras ocho, campanas (peaking)
// con Q = 1,41 (una octava). Todo biquad de las notas de Robert Bristow-Johnson en coma doble, como SalaLocal. Una banda en 0 dB no se calcula;
// una banda por encima de 0,45 de la frecuencia de muestreo no se pone. Uno o dos canales a 8 kHz o mas; con otro numero, pasa tal cual.
//
// CON TODAS LAS BANDAS EN 0 PASA TAL CUAL, bit a bit: Quieta() es verdadera y la etapa ni toca la muestra (ni multiplica por 1).
//
// EN VIVO Y SIN CHASQUIDO. Los cambios llegan desde la ventana mientras suena (Ajusta, con enteros de medio dB: atomicos tambien en 32 bits).
// Cada banda va de su valor de ahora a su objetivo con una rampa de 12 dB cada 50 ms (como TonoSeg de SalaLocal), y los coeficientes se
// recalculan por bloques de 32 cuadros mientras la banda se mueve (y solo entonces). Una banda que acaba de llegar a 0 dB sigue 0,35 s
// con coeficientes de identidad (el filtro asienta su memoria) y solo despues se salta; una banda que arranca de 0 toma su memoria del
// continuo de la musica (la ganancia de continua del filtro por la muestra), asi que no hay escalon al encenderla.
//
// SIN RASPAR (PREGANANCIA AUTOMATICA). Lo que el ecualizador sube llegaria mas fuerte al limitador y lo raspa; delante de los filtros va una
// ganancia de -(lo maximo que sube la curva entera de las 10 bandas juntas), calculada con la respuesta en magnitud sobre 200 puntos
// logaritmicos de 20 Hz a 20 kHz (PreganDb). Solo si ese maximo es mayor que 0 dB; si todo esta en 0 o solo baja, la preganancia es 0 dB
// y no se aplica. Tambien va con su rampa (misma velocidad) y entre cuadros se interpola muestra a muestra. La ventana la ensena (PreganDb).
//
// DISCO CONTINUO (Sigue, como GraveMono y SalaLocal): la pista que entra sin pausa hereda la memoria de los filtros, las ganancias de las
// bandas, la preganancia y las colas de la anterior (Hereda); si lo que pide la nueva es otra cosa, la rampa lo lleva sin escalon. El
// cebado de delante pasa tal cual (EtapaPorCuadros). Cuesta lo de cinco multiplicaciones por banda activa y canal; con todo en cero, nada.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class EcualizadorDueno : EtapaPorCuadros
    {
        internal const int Bandas = 10;
        internal static readonly double[] Hz = { 31.0, 63.0, 125.0, 250.0, 500.0, 1000.0, 2000.0, 4000.0, 8000.0, 16000.0 };
        internal const int MediosMax = 24;                  // +-12 dB en pasos de medio dB
        internal const double Q = 1.41, QEstante = 0.7071;  // campana de una octava; los estantes como los del local (SalaLocal.TonoQ)
        internal const double RampaDb = 12.0, RampaSeg = 0.05, ColaSeg = 0.35;
        internal const int Bloque = 32;                     // cuadros entre recalculos de coeficientes
        internal const int PuntosCurva = 200;
        internal const double CurvaMinHz = 20.0, CurvaMaxHz = 20000.0;

        // ---- Lo que ve la ventana y las pruebas (puro) ----

        // Medios de dB acotados a +-12 dB.
        internal static int Acota(int medios) { return medios > MediosMax ? MediosMax : (medios < -MediosMax ? -MediosMax : medios); }

        // ¿Se pone esta banda a esta frecuencia de muestreo? (0,45 de la frecuencia, como el resto de las etapas.)
        internal static bool Vale(int banda, int tasa) { return banda >= 0 && banda < Bandas && Hz[banda] < 0.45 * tasa; }

        // Coeficientes (normalizados, a0 = 1) de la banda con 'db' dB: b0 b1 b2 a1 a2 en c[o..o+4].
        internal static void Coef(int banda, double db, int tasa, double[] c, int o)
        {
            double A = Math.Pow(10.0, db / 40.0), w0 = 2.0 * Math.PI * Hz[banda] / tasa, sen = Math.Sin(w0), cos = Math.Cos(w0);
            double b0, b1, b2, a0, a1, a2;
            if (banda == 0 || banda == Bandas - 1)
            {
                double k = 2.0 * Math.Sqrt(A) * (sen / (2.0 * QEstante)), Am = A - 1.0, Ap = A + 1.0;
                if (banda == 0)
                {   // estante de graves
                    b0 = A * (Ap - Am * cos + k); b1 = 2.0 * A * (Am - Ap * cos); b2 = A * (Ap - Am * cos - k);
                    a0 = Ap + Am * cos + k; a1 = -2.0 * (Am + Ap * cos); a2 = Ap + Am * cos - k;
                }
                else
                {   // estante de agudos
                    b0 = A * (Ap + Am * cos + k); b1 = -2.0 * A * (Am + Ap * cos); b2 = A * (Ap + Am * cos - k);
                    a0 = Ap - Am * cos + k; a1 = 2.0 * (Am - Ap * cos); a2 = Ap - Am * cos - k;
                }
            }
            else
            {   // campana
                double al = sen / (2.0 * Q);
                b0 = 1.0 + al * A; b1 = -2.0 * cos; b2 = 1.0 - al * A; a0 = 1.0 + al / A; a1 = -2.0 * cos; a2 = 1.0 - al / A;
            }
            c[o] = b0 / a0; c[o + 1] = b1 / a0; c[o + 2] = b2 / a0; c[o + 3] = a1 / a0; c[o + 4] = a2 / a0;
        }

        // |H| en dB de un biquad normalizado a la frecuencia angular w.
        internal static double MagDb(double[] c, int o, double w)
        {
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2.0 * w), s2 = Math.Sin(2.0 * w);
            double nr = c[o] + c[o + 1] * c1 + c[o + 2] * c2, ni = -(c[o + 1] * s1 + c[o + 2] * s2);
            double dr = 1.0 + c[o + 3] * c1 + c[o + 4] * c2, di = -(c[o + 3] * s1 + c[o + 4] * s2);
            double d = dr * dr + di * di;
            if (d < 1e-300) return 0.0;
            return 10.0 * Math.Log10((nr * nr + ni * ni) / d);
        }

        // La respuesta en magnitud (dB) de las diez bandas juntas (medios de dB) a cada frecuencia de 'hz', a esa frecuencia de muestreo.
        // Sin preganancia: es la curva que el dueno dibuja.
        internal static double[] Respuesta(int[] medios, int tasa, double[] hz)
        {
            double[] r = new double[hz.Length];
            double[] c = new double[5];
            for (int b = 0; b < Bandas; b++)
            {
                int m = (medios != null && b < medios.Length) ? Acota(medios[b]) : 0;
                if (m == 0 || !Vale(b, tasa)) continue;
                Coef(b, m * 0.5, tasa, c, 0);
                for (int i = 0; i < hz.Length; i++)
                {
                    double f = hz[i];
                    if (f <= 0.0 || f >= 0.5 * tasa) continue;
                    r[i] += MagDb(c, 0, 2.0 * Math.PI * f / tasa);
                }
            }
            return r;
        }

        // La preganancia, en dB (0 o negativa): lo maximo que sube la curva, con el signo cambiado; 0 si la curva no sube (o todo en 0).
        internal static double PreganDb(int[] medios, int tasa)
        {
            bool alguna = false;
            if (medios != null) for (int b = 0; b < Bandas && b < medios.Length; b++) if (medios[b] != 0 && Vale(b, tasa)) { alguna = true; break; }
            if (!alguna) return 0.0;
            double tope = Math.Min(CurvaMaxHz, 0.49 * tasa);
            double[] hz = new double[PuntosCurva];
            double razon = Math.Log(tope / CurvaMinHz);
            for (int i = 0; i < PuntosCurva; i++) hz[i] = CurvaMinHz * Math.Exp(razon * i / (PuntosCurva - 1));
            double[] r = Respuesta(medios, tasa, hz);
            double max = r[0];
            for (int i = 1; i < r.Length; i++) if (r[i] > max) max = r[i];
            return max > 0.0 ? -max : 0.0;
        }

        // ---- La etapa ----

        private readonly int[] obj = new int[Bandas];                 // lo pedido (medios de dB); lo escribe la ventana
        private volatile int version;                                 // sube con cada Ajusta
        private int visto = -1;
        private double preMeta;                                       // dB (<= 0): a donde va la preganancia
        private double pre;                                           // dB (<= 0): la preganancia de ahora
        private readonly double[] act = new double[Bandas];           // dB de cada banda ahora
        private readonly int[] cola = new int[Bandas];                // cuadros que aun corre una banda ya en 0 dB
        private readonly double[] co = new double[Bandas * 5];        // coeficientes de cada banda
        private readonly double[] coPara = new double[Bandas];        // para que dB estan calculados (NaN = nunca)
        private readonly double[] st = new double[2 * Bandas * 4];    // x1 x2 y1 y2 por canal y banda
        private readonly bool[] prim = new bool[2 * Bandas];          // la memoria de ese canal y banda sigue la musica
        private readonly bool[] valida = new bool[Bandas];
        private readonly bool[] activo = new bool[Bandas];          // las bandas que corren en este bloque
        private readonly bool[] dcPrimero = new bool[Bandas];         // la memoria de esta banda parte del estado asentado (nacio con ganancia)
        private readonly double paso1, colaCuadros;                   // dB por cuadro; cuadros de cola
        private volatile EcualizadorDueno anterior;

        internal EcualizadorDueno(ISampleProvider s, int[] medios) : base(s)
        {
            for (int b = 0; b < Bandas; b++) { coPara[b] = double.NaN; valida[b] = vale && Vale(b, tasa); }
            paso1 = RampaDb / (RampaSeg * Math.Max(1, tasa));
            colaCuadros = ColaSeg * Math.Max(1, tasa);
            if (!vale) return;
            // Arranca donde piden los mandos, sin rampa: lo de la primera muestra ya es lo que se pidio.
            for (int b = 0; b < Bandas; b++) { obj[b] = (medios != null && b < medios.Length && valida[b]) ? Acota(medios[b]) : 0; act[b] = obj[b] * 0.5; dcPrimero[b] = obj[b] != 0; }
            preMeta = pre = PreganDb(obj, tasa);
            visto = version;
        }

        // Los mandos en vivo: 10 numeros en medios de dB. Se oyen en unos 50 ms, sin chasquido.
        internal void Ajusta(int[] medios)
        {
            if (!vale) return;
            for (int b = 0; b < Bandas; b++) obj[b] = (medios != null && b < medios.Length && valida[b]) ? Acota(medios[b]) : 0;
            version++;
        }

        internal double PreganAhoraDb { get { return pre; } }      // la preganancia que lleva ahora (dB)
        internal double PreganMetaDb { get { Sincroniza(); return preMeta; } }
        internal double BandaAhoraDb(int b) { return act[b]; }

        internal void Sigue(EcualizadorDueno previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        protected override bool Hereda()
        {
            EcualizadorDueno a = anterior; anterior = null;
            if (a == null || !a.vale || a.tasa != tasa || a.canales != canales) return false;
            Array.Copy(a.act, act, Bandas); Array.Copy(a.cola, cola, Bandas); Array.Copy(a.co, co, co.Length); Array.Copy(a.coPara, coPara, Bandas);
            Array.Copy(a.st, st, st.Length); Array.Copy(a.prim, prim, prim.Length);
            pre = a.pre;
            return true;
        }

        // Si lo pedido cambio, recalcula a donde va la preganancia (aqui, en el hilo del sonido, una vez por cambio).
        private void Sincroniza()
        {
            int v = version;
            if (v == visto) return;
            visto = v;
            int[] copia = new int[Bandas];
            for (int b = 0; b < Bandas; b++) copia[b] = obj[b];
            preMeta = PreganDb(copia, tasa);
        }

        protected override bool Quieta()
        {
            Sincroniza();
            bool algo = pre != 0.0 || preMeta != 0.0;
            for (int b = 0; b < Bandas && !algo; b++) if (obj[b] != 0 || act[b] != 0.0 || cola[b] > 0) algo = true;
            if (algo) return false;
            for (int i = 0; i < prim.Length; i++) prim[i] = false;   // al volver a encender, parte de la musica de ese momento
            for (int i = 0; i < Bandas; i++) dcPrimero[i] = false;
            return true;
        }

        protected override void Proceso(float[] b, int o, int cuadros)
        {
            int fin = o + cuadros * canales, i = o, ch = canales;
            while (i < fin)
            {
                int m = Math.Min(Bloque, (fin - i) / ch);
                // ---- el bloque: rampas, coeficientes y lista de bandas que corren ----
                double paso = paso1 * m;
                for (int k = 0; k < Bandas; k++)
                {
                    activo[k] = false;
                    if (!valida[k]) continue;
                    double a = act[k], meta = obj[k] * 0.5;
                    if (a != meta) { double d = meta - a; if (d > paso) a += paso; else if (d < -paso) a -= paso; else a = meta; act[k] = a; }
                    if (a != 0.0) cola[k] = (int)colaCuadros; else if (cola[k] > 0) cola[k] -= m;
                    if (a != 0.0 || cola[k] > 0)
                    {
                        if (a != coPara[k]) { Coef(k, a, tasa, co, k * 5); coPara[k] = a; }
                        activo[k] = true;
                    }
                }
                double p0 = pre, p1 = pre;
                if (pre != preMeta) { double d = preMeta - pre; if (d > paso) p1 = pre + paso; else if (d < -paso) p1 = pre - paso; else p1 = preMeta; pre = p1; }
                bool usaPre = p0 != 0.0 || p1 != 0.0;
                double g = usaPre ? Math.Pow(10.0, p0 / 20.0) : 1.0, dg = usaPre ? (Math.Pow(10.0, p1 / 20.0) - g) / m : 0.0;
                // ---- las muestras ----
                for (int f = 0; f < m; f++)
                {
                    for (int c = 0; c < ch; c++)
                    {
                        double v = b[i + c];
                        if (usaPre) v *= g;
                        for (int k = 0; k < Bandas; k++)
                        {
                            if (!valida[k]) continue;
                            int so = (c * Bandas + k) * 4, c5 = k * 5, pk = c * Bandas + k;
                            if (!prim[pk])
                            {   // su memoria parte del continuo de la musica: de fabrica, la de un filtro ya asentado; si no, la de un filtro que apenas empieza (identidad)
                                double dc = dcPrimero[k] ? (co[c5] + co[c5 + 1] + co[c5 + 2]) / (1.0 + co[c5 + 3] + co[c5 + 4]) : 1.0;
                                st[so] = st[so + 1] = v; st[so + 2] = st[so + 3] = dc * v;
                                prim[pk] = true;
                            }
                            if (!activo[k])
                            {   // sin correr: su memoria sigue la musica como un filtro de identidad, para que al empezar a subir no haya escalon
                                st[so + 1] = st[so]; st[so] = v; st[so + 3] = st[so + 2]; st[so + 2] = v;
                                continue;
                            }
                            double y = co[c5] * v + co[c5 + 1] * st[so] + co[c5 + 2] * st[so + 1] - co[c5 + 3] * st[so + 2] - co[c5 + 4] * st[so + 3];
                            if (y < 1e-30 && y > -1e-30) y = 0.0;
                            st[so + 1] = st[so]; st[so] = v; st[so + 3] = st[so + 2]; st[so + 2] = y;
                            v = y;
                        }
                        b[i + c] = (float)v;
                    }
                    g += dg; i += ch;
                }
            }
        }
    }
}

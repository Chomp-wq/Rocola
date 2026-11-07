// EL PRE-ECO DE LOS MP3 (R2, 31-10-2026; AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md)
//
// Un MP3 reparte su ruido de cuantificacion por todo el bloque que codifica. Si en ese bloque hay un golpe (una caja, unas palmas, un
// charles), el ruido aparece ANTES del golpe: un siseo que lo anuncia y le quita lo seco. Los codificadores buenos (LAME, los de
// Fraunhofer) cambian a bloques cortos en los golpes y el pre-eco queda a unos -34 dB del golpe, tapado por el propio golpe. Los
// codificadores simples, sin bloques cortos (los de muchos MP3 viejos de internet), lo dejan a unos -5 dB: la bateria suena blanda.
// Medido con golpes secos sobre un fondo suave, codificados con LAME y con shine (un codificador sin bloques cortos):
//     el error en los 11 ms antes del golpe: LAME 320 -41 dB, LAME 128 -34, LAME 96 -34; shine 128 -5 dB.
//
// 1) LA MEDIDA (MedidorPreEco, de paso en el repaso de la biblioteca: algoritmo 7 de Sonoridad.cs). Sin el original no se puede restar
//    nada, pero el pre-eco tiene una huella clara: el golpe TARDA en subir. Se miran los agudos (de 3 kHz arriba, la suma de los canales)
//    en bloques de 1 ms; un golpe es un bloque que es el mayor de los 10 ms de cada lado y queda 15 dB o mas por encima de lo que habia
//    de 40 a 60 ms antes. Su subida es cuanto hay que ir hacia atras para quedar 12 dB por debajo. Mediana de las subidas:
//        golpes secos sin codificar 2 ms; LAME de 64 a 320 kbps 2 ms; shine 128 y 96 kbps 10 ms.
//    Con al menos GolpesMin golpes y una mediana de SubidaMinMs o mas, la cancion trae pre-eco.
//
// 2) QUITARLO (QuitaPreEco, en la cadena justo despues de los arreglos de la grabacion): con el interruptor "quita_preeco=" (Ctrl+F3 ->
//    Sonido -> Lo que suena ahora; APAGADO de fabrica) y SOLO en las canciones medidas con pre-eco. Mirando PreEscuchaMs por delante:
//    los agudos (de 700 Hz arriba: el pre-eco no es solo siseo) se separan con un paso-alto y el resto queda intacto (salida = entrada +
//    (g - 1) x agudos: con g = 1 sale la muestra tal cual, bit a bit). En cada golpe (los mismos criterios que la medida) se busca donde
//    empieza de verdad (el ultimo bloque antes del pico que esta a menos de 6 dB de el) y en los 26 ms de antes se bajan los agudos
//    hasta 3 dB por encima del fondo que habia (de 40 a 60 ms antes): lo que ya sonaba sigue sonando, solo se quita lo que sobra. La
//    ganancia se suaviza en 0,25 ms. Medido en Python con la misma receta (r2 del 31-10): con shine 128 kbps el pre-eco que se oye (de 4
//    a 20 ms antes del golpe) pasa de -9,6 a -21 dB, y el golpe mismo cambia a -28 dB; con LAME 128, de -42 a -46 dB.
//    Riesgo: un redoble con golpes a menos de 26 ms (un "flam") pierde algo del primero. Por eso solo en las canciones que lo traen.
//    Retrasa PreEscuchaMs (como el des-recorte o el siseo, entra en la cola del fundido). Sin estado heredado en un disco continuo: el
//    cebado de la siguiente lo tira el relevo. "Escuchar sin mejoras" la deja pasar (Pasa). Encenderla se nota desde la siguiente
//    cancion que se arme; apagarla, al instante.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal static class PreEcoComun
    {
        internal const double ContrasteDb = 15.0;   // el golpe sobre el fondo de 40 a 60 ms antes
        internal const int VentanaPico = 10;        // bloques (ms) a cada lado en los que el golpe es el mayor
        internal const int FondoDesde = 60, FondoHasta = 40;   // el fondo: la mediana de esos bloques antes
        internal const int Refractario = 40;        // bloques tras un golpe sin buscar otro
        // Un paso-alto de segundo orden (RBJ, Q = 0,7071) a 'hz'. Devuelve b0, b1, b2, a1, a2.
        internal static double[] PasoAlto(double hz, double fs)
        {
            double w = 2.0 * Math.PI * hz / fs, c = Math.Cos(w), al = Math.Sin(w) / (2.0 * 0.70710678118654752), a0 = 1.0 + al;
            return new double[] { (1.0 + c) / 2.0 / a0, -(1.0 + c) / a0, (1.0 + c) / 2.0 / a0, -2.0 * c / a0, (1.0 - al) / a0 };
        }
        // La mediana de los bloques [desde, hasta) de un anillo (sin reservar: con el arreglo de trabajo que se le pasa).
        internal static double Mediana(double[] anillo, int largo, long desde, long hasta, double[] trabajo)
        {
            int n = 0;
            for (long q = desde; q < hasta; q++) trabajo[n++] = anillo[(int)(q % largo)];
            Array.Sort(trabajo, 0, n);
            return (n & 1) == 1 ? trabajo[n / 2] : 0.5 * (trabajo[n / 2 - 1] + trabajo[n / 2]);
        }
    }

    // ---------------------------------------------------------------- 1) LA MEDIDA
    internal sealed class MedidorPreEco
    {
        internal const double CorteHz = 3000.0, CaidaDb = 12.0;
        internal const int SubidaMax = 40;          // ms que se mira hacia atras
        internal const int GolpesMin = 8;
        internal const float SubidaMinMs = 7f;      // desde aqui, la cancion trae pre-eco
        private const int Anillo = 128;
        private readonly int canales, largoBloque;
        private readonly double b0, b1, b2, a1, a2;
        private double x1, x2, y1, y2;
        private int fase, enBloque;
        private double sumaCuadro, sumaBloque;
        private readonly double[] edb = new double[Anillo];
        private readonly double[] trabajo = new double[32];
        private long bloques, ultimoGolpe = long.MinValue / 2;
        private readonly int[] subidas = new int[SubidaMax + 1];
        private int golpes;

        internal MedidorPreEco(int tasa, int canales)
        {
            this.canales = Math.Max(1, canales);
            largoBloque = Math.Max(8, (int)Math.Round(tasa / 1000.0));
            double[] c = PreEcoComun.PasoAlto(CorteHz, tasa);
            b0 = c[0]; b1 = c[1]; b2 = c[2]; a1 = c[3]; a2 = c[4];
        }
        internal void Reinicia()
        {
            x1 = x2 = y1 = y2 = 0.0; fase = enBloque = 0; sumaCuadro = sumaBloque = 0.0;
            Array.Clear(edb, 0, Anillo); bloques = 0; ultimoGolpe = long.MinValue / 2; Array.Clear(subidas, 0, subidas.Length); golpes = 0;
        }
        // Muestras entrelazadas; 'cuantos' no tiene que ser multiplo de los canales (el canal que toca se recuerda).
        internal void Procesa(float[] datos, int desde, int cuantos)
        {
            int ch = canales;
            for (int i = desde, fin = desde + cuantos; i < fin; i++)
            {
                sumaCuadro += datos[i];
                if (++fase < ch) continue;
                fase = 0;
                double x = sumaCuadro; sumaCuadro = 0.0;
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                if (y < 1e-30 && y > -1e-30) y = 0.0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                sumaBloque += y * y;
                if (++enBloque < largoBloque) continue;
                enBloque = 0;
                edb[(int)(bloques % Anillo)] = 10.0 * Math.Log10(sumaBloque / largoBloque + 1e-14);
                sumaBloque = 0.0;
                bloques++;
                Mira();
            }
        }
        // El bloque k = el ultimo menos VentanaPico: ya se conocen sus 10 ms de despues.
        private void Mira()
        {
            long k = bloques - 1 - PreEcoComun.VentanaPico;
            if (k < PreEcoComun.FondoDesde || k < ultimoGolpe + PreEcoComun.Refractario) return;
            double e = edb[(int)(k % Anillo)];
            for (long q = k - PreEcoComun.VentanaPico; q <= k + PreEcoComun.VentanaPico; q++) if (edb[(int)(q % Anillo)] > e) return;
            double fondo = PreEcoComun.Mediana(edb, Anillo, k - PreEcoComun.FondoDesde, k - PreEcoComun.FondoHasta, trabajo);
            if (e < fondo + PreEcoComun.ContrasteDb) return;
            long j = k;
            while (j > k - SubidaMax && edb[(int)(j % Anillo)] > e - CaidaDb) j--;
            subidas[(int)(k - j)]++; golpes++; ultimoGolpe = k;
        }
        // La mediana de las subidas (ms) si hubo golpes suficientes.
        internal bool Resultado(out float subidaMs, out int cuantos)
        {
            subidaMs = float.NaN; cuantos = golpes;
            if (golpes < GolpesMin) return false;
            int mitad = (golpes + 1) / 2, acum = 0;
            for (int s = 0; s <= SubidaMax; s++) { acum += subidas[s]; if (acum >= mitad) { subidaMs = s; return true; } }
            return false;
        }
    }

    public partial class Form1
    {
        // "quita_preeco=" (Ctrl+F3 -> Sonido -> Lo que suena ahora): APAGADO de fabrica. Solo actua en las canciones medidas con pre-eco.
        private static bool quitaPreEco;

        // ---------------------------------------------------------------- 2) QUITARLO
        internal sealed class QuitaPreEco : ISampleProvider
        {
            internal const double CorteHz = 700.0, CaidaArranqueDb = 6.0, SobreFondoDb = 3.0, SuaveMs = 0.25;
            internal const int AntesMs = 26;            // lo que se baja antes del arranque
            internal const int PreEscuchaMs = 64;       // VentanaPico + 2 x AntesMs + margen
            private readonly ISampleProvider src;
            private readonly int ch, L, nBloques, D;    // D = cuadros de retardo
            private readonly bool vale;
            private readonly double b0, b1, b2, a1, a2, aSuave;
            private readonly double[] z;                // por canal: x1 x2 y1 y2
            private readonly float[] retX, retH;        // el retardo: la muestra y sus agudos
            private readonly double[] edb, gan;         // por bloque (anillo)
            private readonly double[] trabajo = new double[32];
            private int dpos, enBloque, pasados;        // pasados: cuadros que ya entraron (hasta llenar el retardo)
            private double sumaBloque, g = 1.0;
            private long bloques, ultimoGolpe = long.MinValue / 2;
            private bool acabo, huboEntrada;
            private int vaciar;
            private readonly float[] resto, sale; private int restoN, saleIdx, saleN;
            internal volatile bool Pasa;
            internal long Quitados;                     // golpes en los que se quito pre-eco (para Ctrl+F3)

            internal QuitaPreEco(ISampleProvider s)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                int tasa = s.WaveFormat.SampleRate;
                vale = (ch == 1 || ch == 2) && tasa >= 8000;
                L = Math.Max(8, (int)Math.Round(tasa / 1000.0));
                nBloques = PreEscuchaMs + PreEcoComun.FondoDesde + 8;
                D = vale ? PreEscuchaMs * L : 0;
                double[] c = PreEcoComun.PasoAlto(CorteHz, tasa);
                b0 = c[0]; b1 = c[1]; b2 = c[2]; a1 = c[3]; a2 = c[4];
                aSuave = 1.0 - Math.Exp(-1.0 / (SuaveMs * 0.001 * tasa));
                z = new double[4 * ch];
                retX = new float[Math.Max(1, D) * ch]; retH = new float[Math.Max(1, D) * ch];
                edb = new double[nBloques]; gan = new double[nBloques];
                for (int i = 0; i < nBloques; i++) { gan[i] = 1.0; edb[i] = -140.0; }
                resto = new float[ch]; sale = new float[ch];
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            internal int DelayFrames { get { return D; } }

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (!vale) return src.Read(buffer, offset, count);
                if (count <= 0) return 0;
                int hechos = 0;
                if (saleN > 0)
                {
                    int k = Math.Min(count, saleN);
                    for (int i = 0; i < k; i++) buffer[offset + i] = sale[saleIdx + i];
                    saleIdx += k; saleN -= k; hechos = k;
                    if (saleN > 0 || hechos == count) return hechos;
                }
                int faltan = count - hechos;
                if (faltan >= ch) { int n = Lee(buffer, offset + hechos, faltan); return n > 0 ? hechos + n : hechos; }
                int m = Lee(sale, 0, ch);
                if (m <= 0) return hechos;
                int kk = Math.Min(faltan, m);
                for (int i = 0; i < kk; i++) buffer[offset + hechos + i] = sale[i];
                saleIdx = kk; saleN = m - kk;
                return hechos + kk;
            }

            // Cuadros enteros: lo que sale es lo que entro hace D cuadros.
            private int Lee(float[] buffer, int offset, int count)
            {
                int want = count - count % ch;
                while (true)
                {
                    if (acabo)
                    {
                        int fr = Math.Min(vaciar, want / ch);
                        for (int f = 0; f < fr; f++) Paso(null, 0, buffer, offset + f * ch);
                        vaciar -= fr;
                        return fr * ch;
                    }
                    int tiene = restoN;
                    for (int i = 0; i < restoN; i++) buffer[offset + i] = resto[i];
                    restoN = 0;
                    bool fin = false;
                    while (tiene < want)
                    {
                        int r = src.Read(buffer, offset + tiene, want - tiene);
                        if (r <= 0) { fin = true; break; }
                        tiene += r;
                        if (tiene % ch == 0) break;
                    }
                    int frames = tiene / ch;
                    if (fin) { acabo = true; vaciar = (huboEntrada || frames > 0) ? D : 0; }
                    else { restoN = tiene - frames * ch; for (int i = 0; i < restoN; i++) resto[i] = buffer[offset + frames * ch + i]; }
                    if (frames > 0)
                    {
                        huboEntrada = true;
                        for (int f = 0; f < frames; f++) Paso(buffer, offset + f * ch, buffer, offset + f * ch);
                        return frames * ch;
                    }
                }
            }

            // Entra un cuadro (o silencio al vaciar) y sale el de hace D cuadros.
            private void Paso(float[] entrada, int o, float[] salida, int so)
            {
                int di = dpos * ch;
                // lo que entra (la entrada y la salida pueden ser el mismo sitio: se lee antes de escribir)
                double x0 = entrada == null ? 0.0 : entrada[o], x1 = (ch > 1 && entrada != null) ? entrada[o + 1] : 0.0;
                // lo que sale
                double gb = gan[(int)(((pasados - D) / L % nBloques + nBloques) % nBloques)];
                if (Pasa || pasados < D) gb = 1.0;
                g += (gb - g) * aSuave;
                if (g > 1.0 - 1e-7 && gb == 1.0) g = 1.0;
                for (int c = 0; c < ch; c++)
                {
                    float x = retX[di + c];
                    salida[so + c] = pasados < D ? 0f : (g == 1.0 ? x : (float)(x + (g - 1.0) * retH[di + c]));
                }
                // lo que entra: su muestra, sus agudos y la energia del bloque
                double e = 0.0;
                for (int c = 0; c < ch; c++)
                {
                    double x = c == 0 ? x0 : x1;
                    int k = 4 * c;
                    double y = b0 * x + b1 * z[k] + b2 * z[k + 1] - a1 * z[k + 2] - a2 * z[k + 3];
                    if (y < 1e-30 && y > -1e-30) y = 0.0;
                    z[k + 1] = z[k]; z[k] = x; z[k + 3] = z[k + 2]; z[k + 2] = y;
                    retX[di + c] = (float)x; retH[di + c] = (float)y;
                    e += y * y;
                }
                if (++dpos == D) dpos = 0;
                pasados++;
                sumaBloque += e;
                if (++enBloque < L) return;
                enBloque = 0;
                int nb = (int)(bloques % nBloques);
                edb[nb] = 10.0 * Math.Log10(sumaBloque / (L * ch) + 1e-14); gan[nb] = 1.0;
                sumaBloque = 0.0;
                bloques++;
                Mira();
            }

            private double E(long q) { return edb[(int)(q % nBloques)]; }
            // El bloque k = el ultimo menos VentanaPico. Si es un golpe, se bajan los agudos de los AntesMs bloques antes de su arranque.
            private void Mira()
            {
                long k = bloques - 1 - PreEcoComun.VentanaPico;
                if (k < PreEcoComun.FondoDesde || k < ultimoGolpe + PreEcoComun.Refractario) return;
                double e = E(k);
                for (long q = k - PreEcoComun.VentanaPico; q <= k + PreEcoComun.VentanaPico; q++) if (E(q) > e) return;
                double fondo = PreEcoComun.Mediana(edb, nBloques, k - PreEcoComun.FondoDesde, k - PreEcoComun.FondoHasta, trabajo);
                if (e < fondo + PreEcoComun.ContrasteDb) return;
                ultimoGolpe = k;
                long j = k;
                while (j > k - AntesMs && E(j - 1) > e - CaidaArranqueDb) j--;
                double tope = fondo + SobreFondoDb;
                bool quito = false;
                for (long q = Math.Max(0, j - AntesMs); q < j; q++)
                {
                    double eq = E(q);
                    if (eq <= tope) continue;
                    double gq = Math.Sqrt(Math.Pow(10.0, (tope - eq) / 10.0));
                    int i = (int)(q % nBloques);
                    if (gq < gan[i]) { gan[i] = gq; quito = true; }
                }
                if (quito) Quitados++;
            }
        }
    }
}

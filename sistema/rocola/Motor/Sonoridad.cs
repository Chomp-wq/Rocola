// SONORIDAD INTEGRADA DE LA CANCION ENTERA (ITU-R BS.1770)
//
// La rocola tiene DOS medidas de volumen de cada cancion, y no son lo mismo:
//
//   LA ESTIMACION RAPIDA (rocola_volumen.txt). El repaso mide tres tramos de 20 s repartidos por la
//   cancion -o la cancion entera si dura menos de un minuto- con un filtro PARECIDO a la ponderacion K
//   y sin puertas; la escucha en vivo apunta lo que se oyo. Es barata y llega pronto: con ella la cancion
//   ya suena nivelada la primera vez. Pero es una estimacion: el filtro se aparta del de la norma hasta
//   0,49 dB cerca de 1,5 kHz, los silencios y los fundidos cuentan como musica, y lo que no cae en los
//   tramos no se mide.
//
//   LA MEDIDA INTEGRADA (rocola_sonoridad.txt, este archivo). La cancion ENTERA, de la primera muestra a
//   la ultima, como la define la Recomendacion ITU-R BS.1770-4/-5:
//     - ponderacion K exacta: el estante de +4 dB y el paso-alto RLB, en doble precision;
//     - bloques de 400 ms que se solapan un 75 % (uno nuevo cada 100 ms);
//     - puerta ABSOLUTA: fuera los bloques de -70 LKFS o menos;
//     - puerta RELATIVA: fuera los que quedan 10 LU o mas por debajo de la media de los que pasaron;
//     - cada canal con el peso de su altavoz: 1 los de delante, 1,41 los envolventes laterales, y el de
//       efectos de graves (LFE) no cuenta.
//   Cuesta descomprimir la cancion completa, asi que se hace despues de las estimaciones, con la rocola en
//   reposo, cediendo a la musica, a la pantalla y a las caratulas. Cuando existe, el nivelado usa esta;
//   mientras no, la estimacion, que NO se borra: se conserva hasta que haya una integrada valida.
//
// Aqui no se toca el sonido: solo se mide. Y lo que se publica es solo lo que se midio ENTERO: una
// lectura cancelada, rota o que se quedo corta no deja nada apuntado.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Rocola
{
    // El medidor. Puro: recibe muestras entrelazadas y devuelve LKFS. Sin reservas por muestra; la unica
    // memoria que crece es la lista de bloques (10 por segundo, 8 bytes cada uno: 19 KB en cuatro minutos).
    internal sealed class MedidorBs1770
    {
        // Sube cuando cambia algo que altere el resultado. Va escrito en cada medida guardada: una medida de
        // otro algoritmo se distingue sin adivinar.
        //   1: la sonoridad integrada.
        //   2: la misma sonoridad (la cuenta no cambia) y ADEMAS los picos de la cancion (PicosBloques). Las de 1
        //      siguen valiendo para nivelar mientras la tanda larga las vuelve a medir; un ejecutable anterior
        //      guarda las de 2 como ajenas, sin tocarlas.
        //   3  (28 de septiembre de 2026) ademas DONDE SUENA: primer y ultimo bloque con sonido y el silencio mas largo
        //      de en medio (PicosBloques.Mapa), para saltar los silencios largos (SinSilencios.cs). Las de 2 siguen
        //      valiendo para nivelar mientras se vuelven a medir; un ejecutable anterior guarda las de 3 como ajenas.
        //   4  (23 de octubre de 2026) ademas el RANGO DE SONORIDAD (LRA, EBU Tech 3342): cuanto varia el volumen de la cancion en el
        //      tiempo (ventanas de 3 s, puertas de -70 LUFS y de -20 LU, percentiles 10 y 95). Las de 3 (y 2, y 1) siguen valiendo
        //      para nivelar mientras el repaso las vuelve a medir; sin LRA, el modo del local usa su regla de antes (P99 - sonoridad).
        //      Un ejecutable anterior guarda las de 4 como ajenas, intactas.
        //   5  (24 de octubre de 2026) ademas el P90 de los picos por bloque (el pico que solo superan el 10 % de los bloques de 100 ms con
        //      sonido): con el, cada cancion se sube hasta su limite sin rasparse (GananciaAlLimite). Las de 4 (y 3, 2, 1) siguen nivelando
        //      con la regla de antes mientras la tanda larga las vuelve a medir. Un ejecutable anterior guarda las de 5 como ajenas.
        //   6  (25 de octubre de 2026) ademas DONDE EMPIEZA Y ACABA DE VERDAD (T3): el primer y el ultimo momento en que la sonoridad de corto
        //      plazo (3 s) esta a menos de 20 LU de la integrada (Utiles), y EL ZUMBIDO DE LA CORRIENTE (T6, MedidorZumbido): 50 o 60 Hz y
        //      cuanto sobresale. La cuenta de la sonoridad, los picos, el LRA y el P90 no cambian: las de 5 nivelan igual mientras la tanda
        //      larga las vuelve a medir. Un ejecutable anterior guarda las de 6 como ajenas.
        //   7  (31 de octubre de 2026) ademas EL PRE-ECO de los MP3 de codificadores sin bloques cortos (R2, PreEco.cs, MedidorPreEco): cuanto
        //      tardan en subir los golpes (mediana, ms) y cuantos golpes se midieron. Lo demas no cambia: las de 6 nivelan y quitan el zumbido
        //      igual mientras la tanda larga las vuelve a medir. Un ejecutable anterior guarda las de 7 como ajenas.
        internal const int Algoritmo = 7;
        internal const double PuertaAbsoluta = -70.0;   // LKFS
        internal const double PuertaRelativa = -10.0;   // LU respecto de la media de lo que paso la absoluta
        internal const double Desfase = -0.691;         // la constante de la definicion de LKFS
        internal const double Envolvente = 1.41;        // peso de un altavoz lateral (+1,5 dB)

        private readonly int canales;
        private readonly long tasa;
        private readonly double[] peso;
        private readonly double pesoTotal;
        private readonly double b0, b1, b2, a1, a2;     // etapa 1: el estante
        private readonly double r1, r2;                 // etapa 2: el paso-alto RLB (numerador 1, -2, 1)
        // Por canal, de 8 en 8: x[n-1], x[n-2], s[n-1], s[n-2], t[n-1], t[n-2], energia del subbloque, libre.
        // Forma directa I: la historia de entrada de la etapa 2 es la de salida de la etapa 1.
        private readonly double[] estado;
        private int fase;                   // canal que toca: sobrevive entre llamadas (lecturas partidas)
        private long cuadros;               // cuadros completos
        private long indiceSub, inicioSub, finSub;
        private readonly double[] anillo = new double[3];   // energia ponderada de los tres subbloques anteriores
        private readonly long[] cuentas = new long[3];
        private int previos;
        private double[] bloques = new double[64];
        private int nBloques;
        // EL RANGO DE SONORIDAD (LRA, EBU Tech 3342). Sonoridad "de corto plazo" = la de los ultimos 3 s (30 subbloques de 100 ms),
        // una cada 100 ms (solape de 2,9 s); las que pasan la puerta absoluta de -70 LUFS van a un histograma de 0,1 LU (-70 a +10, fijo:
        // la memoria no crece con la cancion). Al cerrar: puerta relativa 20 LU bajo la media (en potencia) de las que pasaron, y la
        // distancia entre el percentil 95 y el 10 de lo que queda. Menos de 3 s de cancion: no hay LRA.
        internal const int LraSub = 30, LraBins = 800;
        internal const double LraPuertaRelativa = -20.0;
        private readonly double[] lraAnillo = new double[LraSub];
        private readonly long[] lraCuentas = new long[LraSub];
        private readonly int[] lraHist = new int[LraBins];
        private int lraPos, lraLleno;
        // DONDE EMPIEZA Y ACABA (T3, algoritmo 6): el primer y el ultimo bloque de 400 ms (los que ya se guardan para la integrada, uno
        // cada 100 ms) a menos de 20 LU de la integrada. Con bloques cortos un final seco sale a menos de 0,4 s de donde acaba la musica
        // (con las ventanas de 3 s del LRA salia casi 3 s tarde: una ventana con un 1 % de musica ya esta a menos de 20 LU).
        internal const double UtilBajoIntegradaLu = 20.0;

        internal MedidorBs1770(int tasaMuestreo, double[] pesos)
        {
            if (tasaMuestreo < 8000) throw new ArgumentOutOfRangeException("tasaMuestreo");
            if (pesos == null || pesos.Length < 1) throw new ArgumentException("pesos");
            canales = pesos.Length;
            tasa = tasaMuestreo;
            peso = (double[])pesos.Clone();
            double s = 0;
            for (int c = 0; c < canales; c++)
            {
                if (!(peso[c] >= 0.0) || peso[c] > 10.0) throw new ArgumentException("pesos");
                s += peso[c];
            }
            if (!(s > 0.0)) throw new ArgumentException("pesos");
            pesoTotal = s;
            double[] e1 = Estante(tasaMuestreo), e2 = PasoAlto(tasaMuestreo);
            b0 = e1[0]; b1 = e1[1]; b2 = e1[2]; a1 = e1[3]; a2 = e1[4];
            r1 = e2[3]; r2 = e2[4];
            estado = new double[canales * 8];
            finSub = tasa / 10;
            Picos = new PicosBloques(tasaMuestreo, canales);
            Zumbido = new MedidorZumbido(tasaMuestreo, canales);
            PreEco = new MedidorPreEco(tasaMuestreo, canales);
        }

        // Los picos de la cancion, de paso (no tocan la cuenta de la sonoridad).
        internal readonly PicosBloques Picos;
        // Y el zumbido de la corriente (T6, Zumbido.cs), tambien de paso.
        internal readonly MedidorZumbido Zumbido;
        // Y el pre-eco de los MP3 (R2, PreEco.cs), tambien de paso.
        internal readonly MedidorPreEco PreEco;

        // Solo para memoria exclusiva del trabajador de analisis. No cambia receta ni coeficientes:
        // otra tasa/disposicion requiere otra instancia. Un retorno false no toca la medida existente.
        internal bool Reinicia(int tasaMuestreo, double[] pesos)
        {
            if (tasaMuestreo != tasa || pesos == null || pesos.Length != canales) return false;
            for (int c = 0; c < canales; c++)
                if (BitConverter.DoubleToInt64Bits(peso[c]) != BitConverter.DoubleToInt64Bits(pesos[c])) return false;
            Array.Clear(estado, 0, estado.Length);
            Array.Clear(anillo, 0, anillo.Length); Array.Clear(cuentas, 0, cuentas.Length);
            Array.Clear(bloques, 0, bloques.Length);
            Array.Clear(lraAnillo, 0, LraSub); Array.Clear(lraCuentas, 0, LraSub); Array.Clear(lraHist, 0, LraBins); lraPos = lraLleno = 0;
            Zumbido.Reinicia();
            PreEco.Reinicia();
            fase = 0; cuadros = 0; indiceSub = inicioSub = 0; finSub = tasa / 10;
            previos = nBloques = 0;
            Picos.Reinicia();
            return true;
        }
        internal bool MemoriaRetenible(int maxBloques) { return bloques.Length <= maxBloques; }

        internal int Canales { get { return canales; } }
        internal double PesoTotal { get { return pesoTotal; } }
        internal long Cuadros { get { return cuadros; } }
        internal int Bloques { get { return nBloques; } }

        // LA PONDERACION K A CUALQUIER FRECUENCIA DE MUESTREO.
        //
        // La norma publica los coeficientes solo a 48 kHz. Estos son los parametros del prototipo analogico
        // (frecuencia, ganancia y Q de cada etapa) que, pasados por la transformacion bilineal con
        // predistorsion, dan EXACTAMENTE esa tabla: la bateria pruebas/sonoridad.cs lo comprueba contra los
        // numeros de la norma (diferencia por debajo de 1e-12). A otra frecuencia salen los coeficientes que
        // dan la misma respuesta. Devuelven b0, b1, b2, a1, a2 (a0 = 1).
        internal static double[] Estante(double fs)
        {
            const double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
            double K = Math.Tan(Math.PI * f0 / fs);
            double Vh = Math.Pow(10.0, G / 20.0);
            double Vb = Math.Pow(Vh, 0.4996667741545416);
            double a0 = 1.0 + K / Q + K * K;
            return new double[] { (Vh + Vb * K / Q + K * K) / a0, 2.0 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0,
                                  2.0 * (K * K - 1.0) / a0, (1.0 - K / Q + K * K) / a0 };
        }
        internal static double[] PasoAlto(double fs)
        {
            const double f0 = 38.13547087602444, Q = 0.5003270373238773;
            double K = Math.Tan(Math.PI * f0 / fs);
            double a0 = 1.0 + K / Q + K * K;
            return new double[] { 1.0, -2.0, 1.0, 2.0 * (K * K - 1.0) / a0, (1.0 - K / Q + K * K) / a0 };
        }

        // Muestras entrelazadas. 'cuantos' NO tiene que ser multiplo de los canales: el canal que toca se
        // recuerda entre llamadas, asi que un cuadro partido entre dos lecturas se completa en la siguiente
        // y ningun canal se pierde ni se cruza con otro.
        //
        // Mono y estereo -casi toda la musica- van por un camino con el estado en variables locales: la
        // MISMA cuenta en el MISMO orden que el camino general (la bateria lo comprueba bit a bit leyendo en
        // trozos de todos los tamanos), pero sin tocar el arreglo en cada muestra. Medido en la maquina de
        // pruebas (Mono): de 10,6 a 5,9 ns por muestra en estereo.
        internal void Procesa(float[] datos, int desde, int cuantos)
        {
            int i = desde, fin = desde + cuantos;
            Picos.Procesa(datos, desde, cuantos);             // aparte, sin tocar nada de lo de abajo
            Zumbido.Procesa(datos, desde, cuantos);           // tambien aparte (T6)
            PreEco.Procesa(datos, desde, cuantos);            // y el pre-eco (R2)
            if (fase != 0) i = Suelto(datos, i, fin, true);   // primero, el cuadro que quedo partido
            if (canales == 2) i = Estereo(datos, i, fin);
            else if (canales == 1) i = Mono(datos, i, fin);
            if (i < fin) Suelto(datos, i, fin, false);        // mas canales, o el trozo de cuadro del final
        }

        // Muestra a muestra, con el estado en el arreglo. Con 'unCuadro' para en cuanto completa uno.
        private int Suelto(float[] datos, int i, int fin, bool unCuadro)
        {
            int c = fase;
            double[] e = estado;
            while (i < fin)
            {
                int q = c << 3;
                double x = datos[i++];
                double s = b0 * x + b1 * e[q] + b2 * e[q + 1] - a1 * e[q + 2] - a2 * e[q + 3];
                double t = s - 2.0 * e[q + 2] + e[q + 3] - r1 * e[q + 4] - r2 * e[q + 5];
                e[q + 1] = e[q]; e[q] = x;
                e[q + 3] = e[q + 2]; e[q + 2] = s;
                e[q + 5] = e[q + 4]; e[q + 4] = t;
                e[q + 6] += t * t;
                if (++c < canales) continue;
                c = 0;
                if (++cuadros == finSub) CierraSubbloque();
                if (unCuadro) break;
            }
            fase = c;
            return i;
        }

        // Cuadros estereo ENTEROS (llega con fase 0 y deja fase 0).
        private int Estereo(float[] datos, int i, int fin)
        {
            int fin2 = i + ((fin - i) & ~1);
            if (fin2 <= i) return i;
            double[] e = estado;
            double B0 = b0, B1 = b1, B2 = b2, A1 = a1, A2 = a2, R1 = r1, R2 = r2;
            double lx1 = e[0], lx2 = e[1], ls1 = e[2], ls2 = e[3], lt1 = e[4], lt2 = e[5], le = e[6];
            double rx1 = e[8], rx2 = e[9], rs1 = e[10], rs2 = e[11], rt1 = e[12], rt2 = e[13], re = e[14];
            long cu = cuadros, meta = finSub;
            for (; i < fin2; i += 2)
            {
                double x = datos[i];
                double s = B0 * x + B1 * lx1 + B2 * lx2 - A1 * ls1 - A2 * ls2;
                double t = s - 2.0 * ls1 + ls2 - R1 * lt1 - R2 * lt2;
                lx2 = lx1; lx1 = x; ls2 = ls1; ls1 = s; lt2 = lt1; lt1 = t; le += t * t;
                x = datos[i + 1];
                s = B0 * x + B1 * rx1 + B2 * rx2 - A1 * rs1 - A2 * rs2;
                t = s - 2.0 * rs1 + rs2 - R1 * rt1 - R2 * rt2;
                rx2 = rx1; rx1 = x; rs2 = rs1; rs1 = s; rt2 = rt1; rt1 = t; re += t * t;
                if (++cu != meta) continue;
                e[0] = lx1; e[1] = lx2; e[2] = ls1; e[3] = ls2; e[4] = lt1; e[5] = lt2; e[6] = le;
                e[8] = rx1; e[9] = rx2; e[10] = rs1; e[11] = rs2; e[12] = rt1; e[13] = rt2; e[14] = re;
                cuadros = cu;
                CierraSubbloque();
                lx1 = e[0]; lx2 = e[1]; ls1 = e[2]; ls2 = e[3]; lt1 = e[4]; lt2 = e[5]; le = e[6];
                rx1 = e[8]; rx2 = e[9]; rs1 = e[10]; rs2 = e[11]; rt1 = e[12]; rt2 = e[13]; re = e[14];
                meta = finSub;
            }
            e[0] = lx1; e[1] = lx2; e[2] = ls1; e[3] = ls2; e[4] = lt1; e[5] = lt2; e[6] = le;
            e[8] = rx1; e[9] = rx2; e[10] = rs1; e[11] = rs2; e[12] = rt1; e[13] = rt2; e[14] = re;
            cuadros = cu;
            return i;
        }

        private int Mono(float[] datos, int i, int fin)
        {
            if (fin <= i) return i;
            double[] e = estado;
            double B0 = b0, B1 = b1, B2 = b2, A1 = a1, A2 = a2, R1 = r1, R2 = r2;
            double x1 = e[0], x2 = e[1], s1 = e[2], s2 = e[3], t1 = e[4], t2 = e[5], en = e[6];
            long cu = cuadros, meta = finSub;
            for (; i < fin; i++)
            {
                double x = datos[i];
                double s = B0 * x + B1 * x1 + B2 * x2 - A1 * s1 - A2 * s2;
                double t = s - 2.0 * s1 + s2 - R1 * t1 - R2 * t2;
                x2 = x1; x1 = x; s2 = s1; s1 = s; t2 = t1; t1 = t; en += t * t;
                if (++cu != meta) continue;
                e[0] = x1; e[1] = x2; e[2] = s1; e[3] = s2; e[4] = t1; e[5] = t2; e[6] = en;
                cuadros = cu;
                CierraSubbloque();
                x1 = e[0]; x2 = e[1]; s1 = e[2]; s2 = e[3]; t1 = e[4]; t2 = e[5]; en = e[6];
                meta = finSub;
            }
            e[0] = x1; e[1] = x2; e[2] = s1; e[3] = s2; e[4] = t1; e[5] = t2; e[6] = en;
            cuadros = cu;
            return i;
        }

        // Cada 100 ms. Los limites caen en floor(k*fs/10): a 11,025 kHz, donde 100 ms no son un numero entero
        // de muestras, los subbloques alternan 1102 y 1103 y cada bloque de 400 ms mide exactamente 4410.
        private void CierraSubbloque()
        {
            double w = 0;
            for (int c = 0; c < canales; c++)
            {
                int q = c << 3;
                w += peso[c] * estado[q + 6];
                estado[q + 6] = 0;
                // En el silencio los filtros decaen hacia cero y acabarian en numeros desnormalizados, que el
                // procesador trata muchisimo mas despacio. Por debajo de 1e-25 (unos -500 dB) se cortan.
                for (int k = 2; k < 6; k++) if (Math.Abs(estado[q + k]) < 1e-25) estado[q + k] = 0;
            }
            long n = cuadros - inicioSub;
            // El corto plazo (LRA): el subbloque entra al anillo de 30 y, con el anillo lleno, cuenta la sonoridad de los ultimos 3 s.
            lraAnillo[lraPos] = w; lraCuentas[lraPos] = n;
            if (++lraPos == LraSub) lraPos = 0;
            if (lraLleno < LraSub) lraLleno++;
            if (lraLleno == LraSub)
            {
                double sw = 0; long sc = 0;
                for (int k = 0; k < LraSub; k++) { sw += lraAnillo[k]; sc += lraCuentas[k]; }
                double pc = sc > 0 ? sw / (double)sc : 0.0;
                if (pc > Potencia(PuertaAbsoluta))
                {
                    int b = (int)Math.Floor((Lkfs(pc) + 70.0) * 10.0);
                    lraHist[b < 0 ? 0 : (b >= LraBins ? LraBins - 1 : b)]++;
                }
            }
            if (previos == 3)
            {
                // Bloque de 400 ms = los tres subbloques anteriores y este. Potencia media ponderada por canal:
                // sum_i G_i z_i, con z_i la media de los cuadrados del canal i en el bloque.
                double p = (anillo[0] + anillo[1] + anillo[2] + w) / (double)(cuentas[0] + cuentas[1] + cuentas[2] + n);
                if (nBloques == bloques.Length)
                {
                    double[] mas = new double[bloques.Length * 2];
                    Array.Copy(bloques, mas, nBloques);
                    bloques = mas;
                }
                bloques[nBloques++] = p;
                anillo[0] = anillo[1]; anillo[1] = anillo[2]; anillo[2] = w;
                cuentas[0] = cuentas[1]; cuentas[1] = cuentas[2]; cuentas[2] = n;
            }
            else { anillo[previos] = w; cuentas[previos] = n; previos++; }
            indiceSub++;
            inicioSub = cuadros;
            finSub = (indiceSub + 1) * tasa / 10;
        }

        // La potencia (sum_i G_i z_i) que corresponde a una sonoridad en LKFS, y al reves.
        internal static double Potencia(double lkfs) { return Math.Pow(10.0, (lkfs - Desfase) / 10.0); }
        internal static double Lkfs(double potencia) { return Desfase + 10.0 * Math.Log10(potencia); }

        // El rango de sonoridad (LU). Falso si no hay ninguna ventana de 3 s por encima de la puerta absoluta (cancion de menos de 3 s,
        // o muda). Los valores de cada ventana son los del centro de su casilla de 0,1 LU.
        internal bool Lra(out double lra)
        {
            lra = 0; long total = 0; double suma = 0;
            for (int i = 0; i < LraBins; i++)
            {
                if (lraHist[i] == 0) continue;
                total += lraHist[i]; suma += lraHist[i] * Potencia(i / 10.0 - 70.0 + 0.05);
            }
            if (total == 0) return false;
            double puerta = Lkfs(suma / total) + LraPuertaRelativa;
            long n = 0;
            for (int i = 0; i < LraBins; i++) if (lraHist[i] > 0 && i / 10.0 - 70.0 + 0.05 > puerta) n += lraHist[i];
            if (n == 0) return false;
            double rango10 = 0.10 * (n - 1), rango95 = 0.95 * (n - 1);
            double bajo = double.NaN, alto = double.NaN; long acum = 0;
            for (int i = 0; i < LraBins; i++)
            {
                if (lraHist[i] == 0 || !(i / 10.0 - 70.0 + 0.05 > puerta)) continue;
                acum += lraHist[i];   // la casilla cubre los puestos acum - cuenta .. acum - 1
                if (double.IsNaN(bajo) && acum - 1 >= Math.Floor(rango10)) bajo = i / 10.0 - 70.0 + 0.05;
                if (acum - 1 >= Math.Floor(rango95)) { alto = i / 10.0 - 70.0 + 0.05; break; }
            }
            lra = alto - bajo;
            return true;
        }

        // DONDE EMPIEZA Y ACABA DE VERDAD (T3), con la integrada 'lkfs': el principio del primer bloque de 400 ms y el final del ultimo que
        // estan a menos de 20 LU de ella (y sobre la puerta absoluta), en segundos desde el principio. El bloque k va de k/10 a k/10 + 0,4 s.
        // Falso si no hay ninguno (cancion muda, o de menos de 400 ms).
        internal bool Utiles(double lkfs, out double inicioS, out double finS)
        {
            inicioS = finS = 0;
            if (double.IsNaN(lkfs) || double.IsInfinity(lkfs) || nBloques == 0) return false;
            double umbral = Math.Max(Potencia(lkfs - UtilBajoIntegradaLu), Potencia(PuertaAbsoluta));
            int primero = -1, ultimo = -1;
            for (int k = 0; k < nBloques; k++) if (bloques[k] >= umbral) { if (primero < 0) primero = k; ultimo = k; }
            if (ultimo < 0) return false;
            inicioS = primero / 10.0;
            finS = ultimo / 10.0 + 0.4;
            return true;
        }

        // Sonoridad integrada. Falso si no hubo ni un bloque completo (menos de 400 ms de audio). Verdadero
        // con -infinito si ningun bloque paso la puerta absoluta (silencio). 'usados' = bloques que pasaron
        // las dos puertas.
        internal bool Integrada(out double lkfs, out int usados)
        {
            lkfs = double.NegativeInfinity; usados = 0;
            if (nBloques == 0) return false;
            double umbralAbs = Potencia(PuertaAbsoluta);
            double suma = 0; int n = 0;
            for (int j = 0; j < nBloques; j++) if (bloques[j] > umbralAbs) { suma += bloques[j]; n++; }
            if (n == 0) return true;
            // Puerta relativa: Gr = -0,691 + 10 log10(media) - 10, o sea, en potencia, media * 10^(-10/10).
            double umbral = Math.Max(umbralAbs, (suma / n) * Math.Pow(10.0, PuertaRelativa / 10.0));
            suma = 0; n = 0;
            for (int j = 0; j < nBloques; j++) if (bloques[j] > umbral) { suma += bloques[j]; n++; }
            usados = n;
            lkfs = Lkfs(suma / n);
            return true;
        }
    }

    // LOS PICOS DE UNA CANCION, para no subirla mas de lo que aguanta.
    //
    // El nivelado sube las canciones bajitas. Si la cancion trae golpes fuertes (bateria, metales, una grabacion
    // con buen rango), al subirla esos golpes quedan por encima del techo y el limitador del final los aplasta
    // todo el rato: el volumen "respira" con cada golpe. Para saber cuanto se puede subir hace falta saber hasta
    // donde llegan sus picos, y no basta con el mas alto: un chasquido suelto dejaria toda la cancion sin subir.
    //
    // Se mira el pico (el valor absoluto mas alto, de cualquier canal) de cada bloque de 100 ms y se guardan dos
    // cosas: el PICO de toda la cancion y el que solo supera el 1 % de los bloques con sonido (P99). Los bloques
    // de -60 dBFS o menos (silencio, colas) no cuentan para el P99. Los picos de cada bloque van a un histograma de
    // decimas de dB (de -80 a +20 dBFS), asi que la memoria no crece con la cancion; el P99 sale redondeado hacia
    // ARRIBA a la decima (se pasa de prudente, nunca de atrevido). Es el pico de las MUESTRAS: entre muestras la
    // onda puede subir algo mas, y de eso ya se ocupa el techo del limitador de pico real (PicoReal.cs).
    internal sealed class PicosBloques
    {
        internal const float DbMin = -80f, DbMax = 20f, Paso = 0.1f;
        internal const float Sonando = -60f;     // un bloque con el pico por debajo no cuenta para el P99
        internal const double Fraccion = 0.01;   // el P99: lo supera como mucho el 1 % de los bloques con sonido
        internal const float Nada = -200f;       // dB de "no hubo nada" (silencio digital)
        private readonly int porBloque;          // muestras (todas las de todos los canales) de un bloque
        private int enBloque;
        private float maxBloque, maxTotal;
        private readonly int[] hist = new int[(int)((DbMax - DbMin) / Paso) + 1];
        private int nSonando;
        // DONDE SUENA (desde el algoritmo 3): bloques cerrados, el primero y el ultimo con sonido y el silencio mas
        // largo entre dos con sonido. Un bloque suena si su pico pasa de -60 dBFS (lo mismo que cuenta para el P99).
        private readonly int tasa, cuadrosBloque;
        private int nBloques, primerSonido = -1, ultimoSonido = -1, huecoIni = -1, huecoFin = -1;

        internal PicosBloques(int tasa, int canales)
        {
            porBloque = Math.Max(1, tasa / 10) * Math.Max(1, canales);
            this.tasa = Math.Max(1, tasa); cuadrosBloque = Math.Max(1, tasa / 10);
        }

        internal void Reinicia()
        {
            Array.Clear(hist, 0, hist.Length);
            enBloque = nSonando = nBloques = 0; maxBloque = maxTotal = 0f;
            primerSonido = ultimoSonido = huecoIni = huecoFin = -1;
        }

        internal void Procesa(float[] datos, int desde, int cuantos)
        {
            int i = desde, fin = desde + cuantos, e = enBloque;
            float m = maxBloque;
            while (i < fin)
            {
                int hasta = i + (porBloque - e); if (hasta > fin) hasta = fin;
                e += hasta - i;
                for (; i < hasta; i++)
                {
                    float v = datos[i]; if (v < 0f) v = -v;
                    if (v > m) m = v;   // un NaN no pasa la comparacion: no cuenta
                }
                if (e == porBloque) { CierraBloque(m); m = 0f; e = 0; }
            }
            maxBloque = m; enBloque = e;
        }
        private void CierraBloque(float m)
        {
            if (m > maxTotal) maxTotal = m;
            Marca(nBloques++, Db(m) > Sonando, ref primerSonido, ref ultimoSonido, ref huecoIni, ref huecoFin);
            int b = Casilla(m);
            if (b < 0) return;
            hist[b]++; nSonando++;
        }
        // La casilla del histograma de un pico, o -1 si el bloque no cuenta (silencio).
        private int Casilla(float m)
        {
            float db = Db(m);
            if (db <= Sonando) return -1;
            int b = (int)((db - DbMin) / Paso);
            if (b < 0) b = 0; else if (b >= hist.Length) b = hist.Length - 1;
            return b;
        }
        internal static float Db(float lin)
        {
            if (!(lin > 1e-10f)) return Nada;
            float db = (float)(20.0 * Math.Log10(lin));
            return db > DbMax ? DbMax : db;
        }
        // El pico de toda la cancion (con el bloque que quedo a medias al final).
        internal float PicoDb { get { return Db(Math.Max(maxTotal, maxBloque)); } }

        private static void Marca(int i, bool suena, ref int prim, ref int ult, ref int hIni, ref int hFin)
        {
            if (!suena) return;
            if (prim < 0) prim = i;
            else if (i - ult - 1 > hFin - hIni) { hIni = ult + 1; hFin = i; }
            ult = i;
        }
        // Donde suena, en segundos, con el bloque a medias del final. 'cuadros' es lo que duro la cancion entera.
        internal MapaSilencios Mapa(long cuadros)
        {
            int prim = primerSonido, ult = ultimoSonido, hIni = huecoIni, hFin = huecoFin;
            if (enBloque > 0) Marca(nBloques, Db(maxBloque) > Sonando, ref prim, ref ult, ref hIni, ref hFin);
            double dura = (double)cuadros / tasa, bloque = (double)cuadrosBloque / tasa;
            if (prim < 0 || !(dura > 0)) return MapaSilencios.Mudo((float)Math.Round(dura, 2));
            MapaSilencios m;
            m.Dura = (float)Math.Round(dura, 2);
            m.Desde = (float)Math.Round(prim * bloque, 2);
            m.Hasta = (float)Math.Round(Math.Min((ult + 1) * bloque, dura), 2);
            if (hIni >= 0) { m.HuecoDesde = (float)Math.Round(hIni * bloque, 2); m.HuecoHasta = (float)Math.Round(hFin * bloque, 2); }
            else { m.HuecoDesde = -1f; m.HuecoHasta = -1f; }
            return m;
        }
        // El P99, contando tambien el bloque a medias del final. Sin bloques con sonido, el pico.
        internal float P99Db { get { return PercentilDb(Fraccion); } }
        // El P90 (desde el algoritmo 5): donde esta el grueso de la cancion. Con el se distingue una cancion con golpes (solo ellos
        // llegan arriba) de una ya aplastada (todo esta arriba): ver GananciaAlLimite.
        internal const double FraccionP90 = 0.10;
        internal float P90Db { get { return PercentilDb(FraccionP90); } }
        // El pico que superan como mucho 'fraccion' de los bloques con sonido (del mismo histograma de 0,1 dB).
        internal float PercentilDb(double fraccion)
        {
            int extra = (enBloque > 0) ? Casilla(maxBloque) : -1;
            int n = nSonando + (extra >= 0 ? 1 : 0);
            if (n == 0) return PicoDb;
            int permitidos = (int)Math.Floor(n * fraccion);   // cuantos bloques pueden quedar por encima
            int encima = 0;
            for (int b = hist.Length - 1; b >= 0; b--)
            {
                encima += hist[b] + (b == extra ? 1 : 0);
                if (encima > permitidos) return Math.Min(PicoDb, DbMin + (b + 1) * Paso);
            }
            return PicoDb;
        }
    }

    // DE QUE ALTAVOZ ES CADA CANAL.
    //
    // No se deduce del numero de canales. Un archivo mono o estereo lo dice su propio formato (en WAV sin
    // mascara, en MP3...: 1 canal es mono, 2 son izquierdo y derecho), pero con tres o mas el orden de los
    // canales solo lo fija la MASCARA de altavoces de WAVE_FORMAT_EXTENSIBLE. Sin ella, seis canales pueden
    // ser un 5.1 o seis microfonos: no se sabe, y entonces NO se mide la integrada (queda la estimacion).
    internal static class DisposicionBs1770
    {
        // Bits de dwChannelMask, en el orden en que aparecen los canales en el archivo.
        internal const int FL = 0x1, FR = 0x2, FC = 0x4, LFE = 0x8, BL = 0x10, BR = 0x20, FLC = 0x40, FRC = 0x80,
                           BC = 0x100, SL = 0x200, SR = 0x400, TC = 0x800, TFL = 0x1000, TFC = 0x2000, TFR = 0x4000,
                           TBL = 0x8000, TBC = 0x10000, TBR = 0x20000;
        private const int Conocidos = 0x3FFFF;

        // Pesos segun la regla de posicion de BS.1770-4/-5: 1,41 a los altavoces a menos de 30 grados de
        // altura y entre 60 y 120 grados de acimut; 1 al resto; el LFE fuera. En una mascara de Windows los
        // laterales (SL/SR) son los envolventes; si no los hay, los traseros (BL/BR) hacen de envolventes,
        // como en el 5.1 clasico (KSAUDIO_SPEAKER_5POINT1). En un 7.1 los traseros quedan a 1. Los de arriba
        // y el trasero central, a 1. Una mascara con bits desconocidos o que no cuadra con los canales: null.
        internal static double[] DeMascara(int mascara, int canales)
        {
            if (canales < 1 || mascara == 0 || (mascara & ~Conocidos) != 0) return null;
            int bits = 0;
            for (int m = mascara; m != 0; m &= m - 1) bits++;
            if (bits != canales) return null;
            bool laterales = (mascara & (SL | SR)) != 0;
            double[] p = new double[canales];
            double suma = 0;
            int i = 0;
            for (int bit = 1; bit <= TBR; bit <<= 1)
            {
                if ((mascara & bit) == 0) continue;
                double g = 1.0;
                if (bit == LFE) g = 0.0;
                else if (bit == SL || bit == SR) g = MedidorBs1770.Envolvente;
                else if ((bit == BL || bit == BR) && !laterales) g = MedidorBs1770.Envolvente;
                p[i++] = g; suma += g;
            }
            return suma > 0 ? p : null;   // solo LFE: no hay nada que medir
        }

        // Lo que dice el formato cuando no trae mascara.
        internal static double[] PorFormato(int canales)
        {
            if (canales == 1) return new double[] { 1.0 };
            if (canales == 2) return new double[] { 1.0, 1.0 };
            return null;
        }

        // Para un archivo concreto. Solo el WAV puede traer mascara; se lee de su cabecera, sin descomprimir.
        internal static double[] DeArchivo(string ruta, int canales)
        {
            if (canales < 1) return null;
            if (ruta != null && ruta.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                int formato, chArchivo, mascara;
                if (!LeeFmtWav(ruta, out formato, out chArchivo, out mascara)) return null;   // cabecera ilegible: no se adivina
                if (chArchivo != canales) return null;
                if (mascara != 0) return DeMascara(mascara, canales);
            }
            return PorFormato(canales);
        }

        // Busca el trozo 'fmt ' de un RIFF/RF64 WAVE y devuelve formato, canales y mascara (0 si no tiene).
        internal static bool LeeFmtWav(string ruta, out int formato, out int canales, out int mascara)
        {
            formato = 0; canales = 0; mascara = 0;
            try
            {
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    return LeeFmtWav(fs, out formato, out canales, out mascara);
            }
            catch { return false; }
        }
        internal static bool LeeFmtWav(Stream fs, out int formato, out int canales, out int mascara)
        {
            formato = 0; canales = 0; mascara = 0;
            byte[] h = new byte[12];
            if (Lee(fs, h, 12) != 12) return false;
            bool riff = h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F';
            bool rf64 = h[0] == 'R' && h[1] == 'F' && h[2] == '6' && h[3] == '4';
            if (!(riff || rf64) || h[8] != 'W' || h[9] != 'A' || h[10] != 'V' || h[11] != 'E') return false;
            byte[] ch = new byte[8];
            for (int vueltas = 0; vueltas < 64; vueltas++)
            {
                if (Lee(fs, ch, 8) != 8) return false;
                long largo = (uint)(ch[4] | (ch[5] << 8) | (ch[6] << 16) | (ch[7] << 24));
                if (ch[0] == 'f' && ch[1] == 'm' && ch[2] == 't' && ch[3] == ' ')
                {
                    if (largo < 16) return false;
                    byte[] f = new byte[(int)Math.Min(largo, 40)];
                    if (Lee(fs, f, f.Length) != f.Length) return false;
                    formato = f[0] | (f[1] << 8);
                    canales = f[2] | (f[3] << 8);
                    if (formato == 0xFFFE && f.Length >= 24) mascara = f[20] | (f[21] << 8) | (f[22] << 16) | (f[23] << 24);
                    return canales > 0;
                }
                long salto = largo + (largo & 1);   // los trozos van alineados a 2 bytes
                if (fs.Position + salto > Math.Min(fs.Length, 16L * 1024 * 1024)) return false;
                fs.Seek(salto, SeekOrigin.Current);
            }
            return false;
        }
        private static int Lee(Stream s, byte[] b, int n)
        {
            int hecho = 0;
            while (hecho < n)
            {
                int r = s.Read(b, hecho, n - hecho);
                if (r <= 0) break;
                hecho += r;
            }
            return hecho;
        }
    }

    // Una medida integrada ya cerrada, tal como se guarda.
    internal struct SonoridadIntegrada
    {
        internal readonly int Algoritmo;
        internal readonly float Lkfs;     // -infinito: ningun bloque paso la puerta absoluta (silencio)
        internal readonly float Pesos;    // suma de los pesos de los canales medidos (1 mono, 2 estereo)
        internal readonly int Bloques;    // bloques de 400 ms que se midieron (10 por segundo)
        internal readonly float PicoDb;   // desde el algoritmo 2: el pico de la cancion y su P99 (PicosBloques),
        internal readonly float P99Db;    // en dBFS; NaN si la medida es de antes (no se sabe)
        internal readonly MapaSilencios Silencios;   // desde el algoritmo 3: donde suena (SinSilencios.cs)
        internal readonly float Lra;      // desde el algoritmo 4: el rango de sonoridad en LU (EBU Tech 3342); NaN si no hay (cancion corta, muda, o medida de antes)
        internal readonly float P90Db;    // desde el algoritmo 5: el P90 de los picos por bloque, en dBFS (GananciaAlLimite); NaN si la medida es de antes
        internal readonly float InicioUtil, FinUtil;   // desde el 6 (T3): donde empieza y acaba de verdad, en s (MedidorBs1770.Utiles); NaN si no hay
        internal readonly int ZumbidoHz;               // desde el 6 (T6): 50 o 60 si trae zumbido de la corriente (MedidorZumbido); 0 si no
        internal readonly float ZumbidoDb;             //   y cuanto sobresale en lo callado, en dB; NaN si no hay
        internal readonly float PreEcoMs;              // desde el 7 (R2): cuanto tardan en subir los golpes (mediana, ms); NaN si no hubo golpes suficientes
        internal readonly int PreEcoGolpes;            //   y cuantos golpes se midieron
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques)
        { Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = float.NaN; P99Db = float.NaN; Silencios = MapaSilencios.Mudo(0f); Lra = float.NaN; P90Db = float.NaN; InicioUtil = FinUtil = float.NaN; ZumbidoHz = 0; ZumbidoDb = float.NaN; PreEcoMs = float.NaN; PreEcoGolpes = 0; }
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques, float picoDb, float p99Db)
        { Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = picoDb; P99Db = p99Db; Silencios = MapaSilencios.Mudo(0f); Lra = float.NaN; P90Db = float.NaN; InicioUtil = FinUtil = float.NaN; ZumbidoHz = 0; ZumbidoDb = float.NaN; PreEcoMs = float.NaN; PreEcoGolpes = 0; }
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques, float picoDb, float p99Db, MapaSilencios silencios)
        { Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = picoDb; P99Db = p99Db; Silencios = silencios; Lra = float.NaN; P90Db = float.NaN; InicioUtil = FinUtil = float.NaN; ZumbidoHz = 0; ZumbidoDb = float.NaN; PreEcoMs = float.NaN; PreEcoGolpes = 0; }
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques, float picoDb, float p99Db, MapaSilencios silencios, float lra)
        { Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = picoDb; P99Db = p99Db; Silencios = silencios; Lra = lra; P90Db = float.NaN; InicioUtil = FinUtil = float.NaN; ZumbidoHz = 0; ZumbidoDb = float.NaN; PreEcoMs = float.NaN; PreEcoGolpes = 0; }
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques, float picoDb, float p99Db, MapaSilencios silencios, float lra, float p90Db)
        { Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = picoDb; P99Db = p99Db; Silencios = silencios; Lra = lra; P90Db = p90Db; InicioUtil = FinUtil = float.NaN; ZumbidoHz = 0; ZumbidoDb = float.NaN; PreEcoMs = float.NaN; PreEcoGolpes = 0; }
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques, float picoDb, float p99Db, MapaSilencios silencios, float lra, float p90Db,
                                    float inicioUtil, float finUtil, int zumbidoHz, float zumbidoDb)
        {
            Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = picoDb; P99Db = p99Db; Silencios = silencios; Lra = lra; P90Db = p90Db;
            InicioUtil = inicioUtil; FinUtil = finUtil; ZumbidoHz = zumbidoHz; ZumbidoDb = zumbidoDb; PreEcoMs = float.NaN; PreEcoGolpes = 0;
        }
        internal SonoridadIntegrada(int algoritmo, float lkfs, float pesos, int bloques, float picoDb, float p99Db, MapaSilencios silencios, float lra, float p90Db,
                                    float inicioUtil, float finUtil, int zumbidoHz, float zumbidoDb, float preEcoMs, int preEcoGolpes)
        {
            Algoritmo = algoritmo; Lkfs = lkfs; Pesos = pesos; Bloques = bloques; PicoDb = picoDb; P99Db = p99Db; Silencios = silencios; Lra = lra; P90Db = p90Db;
            InicioUtil = inicioUtil; FinUtil = finUtil; ZumbidoHz = zumbidoHz; ZumbidoDb = zumbidoDb; PreEcoMs = preEcoMs; PreEcoGolpes = preEcoGolpes;
        }
        // Trae pre-eco: medido con golpes suficientes y subidas de SubidaMinMs o mas (PreEco.cs).
        internal bool ConPreEco { get { return Algoritmo >= 7 && !float.IsNaN(PreEcoMs) && PreEcoGolpes >= MedidorPreEco.GolpesMin && PreEcoMs >= MedidorPreEco.SubidaMinMs; } }
        internal bool ConP90 { get { return Algoritmo >= 5 && ConPicos && !float.IsNaN(P90Db); } }
        internal bool ConUtiles { get { return Algoritmo >= 6 && !float.IsNaN(InicioUtil) && !float.IsNaN(FinUtil); } }
        internal bool ConZumbido { get { return Algoritmo >= 6 && (ZumbidoHz == 50 || ZumbidoHz == 60); } }
        internal bool ConLra { get { return Algoritmo >= 4 && !float.IsNaN(Lra) && !float.IsInfinity(Lra) && Lra >= 0f; } }
        internal bool ConSilencios { get { return Algoritmo >= 3 && Silencios.Valido; } }
        internal bool ConPicos { get { return Algoritmo >= 2 && !float.IsNaN(PicoDb) && !float.IsNaN(P99Db); } }
        internal bool Valida { get { return Algoritmo >= 1 && Pesos > 0f && Bloques > 0; } }
        internal bool Muda { get { return float.IsNegativeInfinity(Lkfs); } }

        // LA MISMA ESCALA QUE LA ESTIMACION. El nivelado trabaja con la raiz de la potencia K media POR CANAL
        // (asi un tema mono y el mismo tema en estereo, que suenan igual por los dos altavoces, reciben la
        // misma ganancia). BS.1770 suma los canales: aqui se divide entre la suma de sus pesos. Para una
        // senal estable que pasa todas las puertas, en mono o estereo, da exactamente la cuenta de antes.
        internal float RmsPorCanal
        {
            get
            {
                if (!Valida || Muda) return 0f;
                return (float)Math.Sqrt(MedidorBs1770.Potencia(Lkfs) / Pesos);
            }
        }
    }

    public partial class Form1
    {
        // (De Rocola/Sonoridad.cs solo la medida y la tabla; la cola, las caches y el repaso son de la biblioteca.)
        internal const float SubeGrP99Db = 3f, SubeGrPicoDb = 6f;
        internal const float BajaDesdeDb = 2f, BajaFraccion = 0.5f, BajaMaxDb = 4f;
        internal const float LimP90Db = 0.5f, LimP99Db = SubeGrP99Db, LimPicoDb = SubeGrPicoDb;
        private const float NormMaxBoost = 16.0f;   // (Rocola/Form1.cs) techo absoluto de subida: +24 dB
        private static string Pool(string s) { return string.Intern(s); }   // en XP comparte las rutas (rutasCompartidas); aqui, el intern de .NET
        internal const string IntegCabecera = "ROCOLA-SONORIDAD v1";
        // Lee registros. Falso = la cabecera no es la de este formato (esa tabla no se usa ni se pisa).
        internal static bool LeeIntegRegistros(TextReader r, Dictionary<string, SonoridadIntegrada> tabla, List<string> ajenas)
        {
            string linea = r.ReadLine();
            if (linea == null) return true;   // vacia: como si no existiera
            if (linea != IntegCabecera) return false;
            char[] barra = { '|' };
            while ((linea = r.ReadLine()) != null)
            {
                int corte = linea.IndexOf('|');
                if (corte <= 0) continue;
                int alg;
                if (!int.TryParse(linea.Substring(0, corte), NumberStyles.None, CultureInfo.InvariantCulture, out alg) || alg < 1) continue;
                if (alg > MedidorBs1770.Algoritmo) { ajenas.Add(linea); continue; }
                int campos = (alg >= 7) ? 20 : (alg >= 6) ? 18 : (alg >= 5) ? 14 : (alg >= 4) ? 13 : (alg >= 3) ? 12 : (alg >= 2) ? 7 : 5;   // desde el 2: alg|lkfs|pesos|bloques|pico|p99|ruta
                string[] p = linea.Split(barra, campos);
                if (p.Length != campos || p[campos - 1].Length == 0) continue;
                float lk, pesos; int bloques;
                if (p[1] == "muda") lk = float.NegativeInfinity;
                else if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lk)
                         || float.IsNaN(lk) || lk < -200f || lk > 30f) continue;
                if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out pesos)
                    || float.IsNaN(pesos) || !(pesos > 0f) || pesos > 64f) continue;
                if (!int.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out bloques) || bloques < 1) continue;
                float pico = float.NaN, p99 = float.NaN;
                if (alg >= 2 && (!LeePico(p[4], out pico) || !LeePico(p[5], out p99) || p99 > pico)) continue;
                MapaSilencios sil = MapaSilencios.Mudo(0f);
                if (alg >= 3 && (!MapaSilencios.Lee(p[6], out sil.Desde) || !MapaSilencios.Lee(p[7], out sil.Hasta)
                                 || !MapaSilencios.Lee(p[8], out sil.HuecoDesde) || !MapaSilencios.Lee(p[9], out sil.HuecoHasta)
                                 || !MapaSilencios.Lee(p[10], out sil.Dura) || !sil.Valido)) continue;
                float lra = float.NaN;   // desde el 4: el rango de sonoridad ("-" = no hay)
                if (alg >= 4 && p[11] != "-" && (!float.TryParse(p[11], NumberStyles.Float, CultureInfo.InvariantCulture, out lra)
                                                 || float.IsNaN(lra) || float.IsInfinity(lra) || lra < 0f || lra > 200f)) continue;
                float p90 = float.NaN;   // desde el 5: el P90 de los picos (nunca por encima del P99)
                if (alg >= 5 && (!LeePico(p[12], out p90) || p90 > p99)) continue;
                float ini = float.NaN, fin = float.NaN, zdb = float.NaN; int zhz = 0;   // desde el 6: donde empieza y acaba, y el zumbido
                if (alg >= 6)
                {
                    if (p[13] != "-" || p[14] != "-")
                    {
                        if (!LeeSegundos(p[13], out ini) || !LeeSegundos(p[14], out fin) || ini > fin) continue;
                    }
                    if (p[15] != "-")
                    {
                        if (p[15] == "50") zhz = 50; else if (p[15] == "60") zhz = 60; else continue;
                        if (!float.TryParse(p[16], NumberStyles.Float, CultureInfo.InvariantCulture, out zdb) || float.IsNaN(zdb) || zdb < 0f || zdb > 120f) continue;
                    }
                    else if (p[16] != "-") continue;
                }
                float pems = float.NaN; int pegol = 0;   // desde el 7: el pre-eco
                if (alg >= 7)
                {
                    if (!int.TryParse(p[18], NumberStyles.None, CultureInfo.InvariantCulture, out pegol) || pegol < 0) continue;
                    if (p[17] != "-" && (!float.TryParse(p[17], NumberStyles.Float, CultureInfo.InvariantCulture, out pems) || float.IsNaN(pems) || pems < 0f || pems > 1000f)) continue;
                }
                string ruta = Pool(p[campos - 1]);
                SonoridadIntegrada antes;
                if (tabla.TryGetValue(ruta, out antes) && antes.Algoritmo > alg) continue;   // ya hay una de algoritmo mas nuevo
                tabla[ruta] = (alg >= 7) ? new SonoridadIntegrada(alg, lk, pesos, bloques, pico, p99, sil, lra, p90, ini, fin, zhz, zdb, pems, pegol)
                            : (alg >= 6) ? new SonoridadIntegrada(alg, lk, pesos, bloques, pico, p99, sil, lra, p90, ini, fin, zhz, zdb)
                            : (alg >= 5) ? new SonoridadIntegrada(alg, lk, pesos, bloques, pico, p99, sil, lra, p90)
                            : (alg >= 4) ? new SonoridadIntegrada(alg, lk, pesos, bloques, pico, p99, sil, lra)
                            : (alg >= 3) ? new SonoridadIntegrada(alg, lk, pesos, bloques, pico, p99, sil)
                            : (alg >= 2) ? new SonoridadIntegrada(alg, lk, pesos, bloques, pico, p99)
                                         : new SonoridadIntegrada(alg, lk, pesos, bloques);
            }
            return true;
        }
        internal static void EscribeIntegRegistro(TextWriter w, string ruta, SonoridadIntegrada s)
        {
            w.Write(s.Algoritmo.ToString(CultureInfo.InvariantCulture)); w.Write('|');
            w.Write(s.Muda ? "muda" : s.Lkfs.ToString("0.###", CultureInfo.InvariantCulture)); w.Write('|');
            w.Write(s.Pesos.ToString("0.###", CultureInfo.InvariantCulture)); w.Write('|');
            w.Write(s.Bloques.ToString(CultureInfo.InvariantCulture)); w.Write('|');
            if (s.Algoritmo >= 2)
            {
                w.Write(s.PicoDb.ToString("0.##", CultureInfo.InvariantCulture)); w.Write('|');
                w.Write(s.P99Db.ToString("0.##", CultureInfo.InvariantCulture)); w.Write('|');
            }
            if (s.Algoritmo >= 3)
            {
                MapaSilencios m = s.Silencios;
                w.Write(MapaSilencios.Texto(m.Desde)); w.Write('|'); w.Write(MapaSilencios.Texto(m.Hasta)); w.Write('|');
                w.Write(MapaSilencios.Texto(m.HuecoDesde)); w.Write('|'); w.Write(MapaSilencios.Texto(m.HuecoHasta)); w.Write('|');
                w.Write(MapaSilencios.Texto(m.Dura)); w.Write('|');
            }
            if (s.Algoritmo >= 4) { w.Write(s.ConLra ? s.Lra.ToString("0.#", CultureInfo.InvariantCulture) : "-"); w.Write('|'); }
            if (s.Algoritmo >= 5) { w.Write(s.P90Db.ToString("0.##", CultureInfo.InvariantCulture)); w.Write('|'); }
            if (s.Algoritmo >= 6)
            {
                if (s.ConUtiles) { w.Write(s.InicioUtil.ToString("0.#", CultureInfo.InvariantCulture)); w.Write('|'); w.Write(s.FinUtil.ToString("0.#", CultureInfo.InvariantCulture)); w.Write('|'); }
                else w.Write("-|-|");
                if (s.ConZumbido) { w.Write(s.ZumbidoHz.ToString(CultureInfo.InvariantCulture)); w.Write('|'); w.Write(s.ZumbidoDb.ToString("0.#", CultureInfo.InvariantCulture)); w.Write('|'); }
                else w.Write("-|-|");
            }
            if (s.Algoritmo >= 7)
            {
                w.Write(float.IsNaN(s.PreEcoMs) ? "-" : s.PreEcoMs.ToString("0.#", CultureInfo.InvariantCulture)); w.Write('|');
                w.Write(s.PreEcoGolpes.ToString(CultureInfo.InvariantCulture)); w.Write('|');
            }
            w.Write(ruta); w.Write("\r\n");
        }
        // Unos segundos guardados (donde empieza o acaba): de 0 a 100.000 s.
        private static bool LeeSegundos(string t, out float seg)
        {
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out seg) && !float.IsNaN(seg) && seg >= 0f && seg <= 100000f;
        }
        // Un pico guardado: dBFS entre "nada" (-200) y +20.
        private static bool LeePico(string t, out float db)
        {
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out db)
                   && !float.IsNaN(db) && db >= PicosBloques.Nada && db <= PicosBloques.DbMax;
        }
        private static float DbALin(float db) { return (float)Math.Pow(10.0, db / 20.0); }
        // Cuanto se puede subir, como mucho, una cancion con esos picos. Nunca menos de 1 (este tope no baja).
        internal static float TopeSubidaPorPicos(float picoLin, float p99Lin)
        {
            float a = TechoPicoReal * DbALin(SubeGrP99Db) / Math.Max(p99Lin, 1e-6f);
            float b = TechoPicoReal * DbALin(SubeGrPicoDb) / Math.Max(picoLin, 1e-6f);
            float t = Math.Min(a, b);
            return (t > 1f) ? t : 1f;
        }
        // LA GANANCIA DEL NIVELADO, pura (la usa NormGainFor): 'rms' en la escala de siempre, 'tope' el de su etapa.
        internal static float GananciaNivelado(float objetivo, float rms, float fuerza, float tope,
                                               bool hayPicos, float picoLin, float p99Lin)
        {
            float g = (float)Math.Pow(objetivo / rms, fuerza);
            if (float.IsNaN(g)) return 1f;   // (una muda da infinito: se queda en su tope, como siempre)
            if (g >= 1f)
            {
                if (hayPicos) tope = Math.Min(tope, TopeSubidaPorPicos(picoLin, p99Lin));
                if (g > tope) g = tope;
                return g < 1f ? 1f : g;
            }
            double sobra = -20.0 * Math.Log10(g);   // cuanto pasa del objetivo (con la fuerza aplicada)
            double baja = (sobra - BajaDesdeDb) * BajaFraccion;
            if (!(baja > 0.0)) return 1f;
            if (baja > BajaMaxDb) baja = BajaMaxDb;
            return (float)Math.Pow(10.0, -baja / 20.0);
        }
        internal static float LimiteSinRaspar(float picoLin, float p99Lin, float p90Lin)
        {
            float a = TechoPicoReal * DbALin(LimP90Db) / Math.Max(p90Lin, 1e-6f);
            float b = TechoPicoReal * DbALin(LimP99Db) / Math.Max(p99Lin, 1e-6f);
            float c = TechoPicoReal * DbALin(LimPicoDb) / Math.Max(picoLin, 1e-6f);
            return Math.Min(a, Math.Min(b, c));
        }
        // La ganancia: el limite, elevado a la "Fuerza del nivelado" (100 %, todo el camino), con el techo absoluto de seguridad.
        internal static float GananciaAlLimite(float fuerza, float picoLin, float p99Lin, float p90Lin)
        {
            float lim = LimiteSinRaspar(picoLin, p99Lin, p90Lin);
            if (float.IsNaN(lim) || !(lim > 0f)) return 1f;
            if (lim > NormMaxBoost) lim = NormMaxBoost;   // casi muda: no se amplifica sin fin
            float g = (float)Math.Pow(lim, fuerza);
            return (float.IsNaN(g) || float.IsInfinity(g)) ? 1f : g;
        }
        // Cierra una medida. Solo vale si se leyo la cancion ENTERA y nada mas: si el lector se quedo corto
        // respecto de lo que el archivo dice durar (roto a mitad, descompresor que se rinde), o si entrego de
        // MAS (algo se leyo dos veces), no hay medida. El margen (100 ms o una milesima) cubre el ultimo cuadro
        // que algunos descompresores de MP3 no entregan.
        internal static bool CierraIntegrada(MedidorBs1770 m, double totalSeg, int tasa, out SonoridadIntegrada s)
        {
            s = new SonoridadIntegrada();
            if (m == null || tasa < 8000 || !(totalSeg > 0)) return false;
            long esperados = (long)(totalSeg * tasa);
            long margen = Math.Max(tasa / 10, esperados / 1000);
            if (m.Cuadros + margen < esperados || m.Cuadros > esperados + margen) return false;
            double lk; int usados;
            if (!m.Integrada(out lk, out usados)) return false;   // ni un bloque de 400 ms
            float pico = m.Picos.PicoDb, p99 = m.Picos.P99Db, p90 = m.Picos.P90Db;
            double ini, fin; float fIni = float.NaN, fFin = float.NaN;
            if (m.Utiles(lk, out ini, out fin)) { fIni = (float)Math.Round(ini, 1); fFin = (float)Math.Round(fin, 1); if (fIni > fFin) fIni = fFin; }
            int zhz; float zdb;
            if (!m.Zumbido.Resultado(out zhz, out zdb)) { zhz = 0; zdb = float.NaN; }
            float pems; int pegol;
            if (!m.PreEco.Resultado(out pems, out pegol)) pems = float.NaN;
            s = new SonoridadIntegrada(MedidorBs1770.Algoritmo, double.IsNegativeInfinity(lk) ? float.NegativeInfinity : (float)lk,
                                       (float)m.PesoTotal, m.Bloques, (float)Math.Round(pico, 2), (float)Math.Round(p99, 2),
                                       m.Picos.Mapa(m.Cuadros), LraDeMedidor(m), (float)Math.Round(Math.Min(p90, p99), 2), fIni, fFin, zhz, zdb, pems, pegol);
            return true;
        }
        // El rango de sonoridad de lo medido (NaN si no hay; al decimo de LU, como se guarda).
        private static float LraDeMedidor(MedidorBs1770 m)
        {
            double l;
            if (!m.Lra(out l) || double.IsNaN(l) || double.IsInfinity(l) || l < 0.0) return float.NaN;
            return (float)Math.Round(l, 1);
        }
        // LEER CUADROS ENTEROS. Un lector puede devolver un trozo que parte un cuadro (un canal en esta
        // lectura y el otro en la siguiente). Tomado tal cual, ese cuadro se perdia y a partir de ahi cada
        // canal se leia como si fuera el otro. Aqui se sigue leyendo hasta completarlo. Solo al final del
        // archivo puede quedar un trozo de cuadro, y se descarta: no hay con que completarlo.
        internal static int LeeCuadrosCompletos(Func<float[], int, int, int> lee, float[] buf, int max, int canales)
        {
            if (canales < 1) canales = 1;
            max -= max % canales;
            int n = 0;
            while (n < max)
            {
                int r = lee(buf, n, max - n);
                if (r <= 0) break;
                n += r;
                if (n % canales == 0) break;
            }
            return n - n % canales;
        }
    }
}

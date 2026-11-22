// LOS HUECOS DE UN MP3, LEIDOS DEL PROPIO MP3 (R1, «rellenar los huecos de los MP3 viejos»; FASE11_relleno-huecos.md). En XP no se pudo
// (AUDIO_R2_Y_SEPARACION_VOZ_MODEST_2026-10-31.md, §2): el ACM de Windows no dice que bandas vinieron vacias, y mirando el sonido ya
// descomprimido no se distinguen de los valles normales. Aqui se le pregunta al MP3: este lector entiende la capa III de MPEG-1 (44,1, 48
// y 32 kHz) lo justo para saber, en cada granulo (576 muestras, 13 ms a 44,1 kHz) y en cada banda de factores de escala, si el codificador
// la mando A CERO, y con que paso de cuantizacion: la informacion lateral, el deposito de bits, los factores de escala y los valores de
// Huffman (RocolaOsHuecosTablas). El sonido lo sigue dando libmpg123 (o NLayer): este lector no lo sustituye, solo le SUMA el relleno.
//
// EL RELLENO (como el «noise filling» del AAC y el «gap filling» del MPEG-H, pero sobre lo que da el descompresor): en un granulo largo
// (los cortos, los mixtos y los de estereo de intensidad no se tocan), de 3 kHz para arriba y por debajo de la ultima linea con algo de
// ese canal (su corte en ese granulo), lo que el codificador mando A CERO se rellena con ruido, en el dominio del propio MP3 (L/R, o M/S si
// viene asi: cada canal con lo suyo, y lo que era casi mono sigue casi mono). Dos clases de hueco (BuscaHuecos):
//   - LINEAS A CERO en una banda que trae algo: valian menos que medio paso de su banda (por eso salieron cero); van con un ruido de
//     LineaPaso (0,1) pasos de amplitud eficaz, 20 dB bajo el paso: por debajo del error que el propio codificador dio por tapado (el de
//     un ruido parejo de +-medio paso es 0,29 pasos), y nunca a mas de 6 dB bajo lo que traen las lineas con algo de esa banda. Los huecos
//     de un MP3 son casi todos de estos (unos 80 Hz: dos lineas; AUDIO_R2_..._2026-10-31.md): con bandas enteras solas no se arreglaba casi
//     nada (shine 128, del 19 al 18 % de lo que se oye 15 dB por debajo; con las lineas, al 5 %);
//   - UNA BANDA ENTERA A CERO con algo en la de debajo y en una de las dos de encima (un hueco de como mucho dos bandas), corta (hasta 4
//     granulos, 52 ms, entera; se apaga hasta el 8: desde ahi ya es la musica, no un hueco): la media geometrica de la energia por linea de
//     sus vecinas, 6 dB por debajo, y como mucho la de un ruido parejo de +-medio paso.
// El relleno pasa por la misma sintesis del MP3 (alias, IMDCT con la ventana del granulo, inversion de frecuencia, banco polifasico) y
// cae justo en su granulo y en su banda, con el mismo retardo que lo de libmpg123. Se suma a lo que dio el descompresor.
// Modo «Propio» (solo las pruebas): sintetiza la cancion entera, no solo el relleno, para comprobar contra libmpg123 que este lector y su
// sintesis dicen lo mismo que el descompresor de verdad (la misma cancion, la misma escala y el mismo momento).
using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Rocola
{
    internal sealed class RocolaOsHuecosMp3
    {
        internal const double DesdeHz = 3000.0;            // los huecos de mas abajo casi no existen (menos del 1 % entre 0,5 y 2 kHz) y se oirian
        internal const double NivelBajoVecinasDb = 6.0;    // el relleno, por debajo de la media de sus vecinas
        internal const int GranulosEnteros = 4, GranulosMax = 8;   // hasta 4 granulos entero; desde el 8, nada
        internal const int AnchoMax = 2;                   // un hueco de como mucho dos bandas

        // ---------- lo que se cuenta (para la medida del repaso y para las pruebas) ----------
        internal long Cuadros, CuadrosRaros, Granulos, GranulosLargos, Candidatas, Huecos, Rellenos;
        internal double EnergiaAltos, EnergiaRelleno;        // lo que traen las bandas de 3 kHz al corte, y lo que les pondria el relleno
        internal double RellenoDb { get { return EnergiaAltos > 0 && EnergiaRelleno > 0 ? 10 * Math.Log10(EnergiaRelleno / EnergiaAltos) : -99; } }
        internal bool Propio;                                // (pruebas) sintetiza todo, no solo el relleno
        internal float PropioEscala = 1f;                    // (pruebas) y por cuanto (-1: el envuelto debe dejar casi silencio)

        // ---------- el cuadro ----------
        private int canales, tasaIdx = -1, modo, modoExt;
        private readonly byte[] deposito = new byte[2048 + 4096];
        private int depositoN;
        private readonly byte[] datos = new byte[8192];      // los datos principales de este cuadro, desde main_data_begin
        private int bitPos, bitFin;
        // la informacion lateral, por granulo y canal
        private readonly int[,] part23 = new int[2, 2], bigValues = new int[2, 2], ganancia = new int[2, 2], scfComp = new int[2, 2], tipo = new int[2, 2];
        private readonly bool[,] mixto = new bool[2, 2], preflag = new bool[2, 2], escalaSf = new bool[2, 2], count1B = new bool[2, 2];
        private readonly int[,,] tablaSel = new int[2, 2, 3], subGanancia = new int[2, 2, 3];
        private readonly int[,] region0 = new int[2, 2], region1 = new int[2, 2];
        private readonly bool[,] scfsi = new bool[2, 4];
        private readonly int[][] sfLarga = { new int[22], new int[22] };
        private readonly int[][,] sfCorta = { new int[13, 3], new int[13, 3] };
        // el espectro de un granulo
        private readonly int[][] valor = { new int[576], new int[576] };   // los enteros de Huffman
        private readonly float[][] xr = { new float[576], new float[576] }; // lo que se sintetiza (el relleno, o todo con Propio)
        private readonly double[][] energia = { new double[22], new double[22] };
        private readonly double[][] paso2 = { new double[22], new double[22] };   // el paso de cada banda, al cuadrado
        private readonly int[][] corrida = { new int[22], new int[22] };         // cuantos granulos lleva cada banda a cero
        private uint azar = 0x9E3779B9u;
        // la sintesis, por canal de salida
        private readonly float[][] solape = { new float[576], new float[576] };
        private readonly float[][] vbuf = { new float[2048], new float[2048] };   // (doble: cada V va en p y en p+1024, y se lee sin dar la vuelta)
        private readonly int[] vPos = new int[2];
        private readonly int[] quieto = { 99, 99 };     // franjas de 32 sin nada que sintetizar (desde 16 la salida es cero sin calcular)
        private readonly float[][] tiempo = { new float[576], new float[576] };   // la salida de la IMDCT: [sb*18 + t]
        private readonly bool[] algoEnGranulo = new bool[2];
        private readonly bool[] solapeConAlgo = new bool[2];

        // tablas que se calculan una vez
        private static readonly float[] pot43 = new float[8207 + 16];
        private static readonly float[] cos36 = new float[18 * 40],   // (por columnas: cos36[k*40 + i], i = 0..35 y 4 de relleno, para los vectores)
            cos12 = new float[12 * 6], dct32 = new float[32 * 32];
        private static readonly float[][] ventanas = new float[4][];
        private static readonly float[] ventanaCorta = new float[12];
        private static readonly float[] cs = new float[8], ca = new float[8];
        static RocolaOsHuecosMp3()
        {
            for (int i = 0; i < pot43.Length; i++) pot43[i] = (float)Math.Pow(i, 4.0 / 3.0);
            for (int i = 0; i < 36; i++) for (int k = 0; k < 18; k++) cos36[k * 40 + i] = (float)Math.Cos(Math.PI / 72.0 * (2 * i + 1 + 18) * (2 * k + 1));
            for (int i = 0; i < 12; i++) for (int k = 0; k < 6; k++) cos12[i * 6 + k] = (float)Math.Cos(Math.PI / 24.0 * (2 * i + 1 + 6) * (2 * k + 1));
            for (int j = 0; j < 32; j++) for (int k = 0; k < 32; k++) dct32[k * 32 + j] = (float)Math.Cos(j * (2 * k + 1) * Math.PI / 64.0);   // (por columnas)
            for (int t = 0; t < 4; t++) ventanas[t] = new float[36];
            for (int i = 0; i < 36; i++) ventanas[0][i] = (float)Math.Sin(Math.PI / 36.0 * (i + 0.5));
            for (int i = 0; i < 36; i++)
            {
                ventanas[1][i] = i < 18 ? ventanas[0][i] : i < 24 ? 1f : i < 30 ? (float)Math.Sin(Math.PI / 12.0 * (i - 18 + 0.5)) : 0f;
                ventanas[3][i] = i < 6 ? 0f : i < 12 ? (float)Math.Sin(Math.PI / 12.0 * (i - 6 + 0.5)) : i < 18 ? 1f : ventanas[0][i];
            }
            for (int i = 0; i < 12; i++) ventanaCorta[i] = (float)Math.Sin(Math.PI / 12.0 * (i + 0.5));
            double[] ci = { -0.6, -0.535, -0.33, -0.185, -0.095, -0.041, -0.0142, -0.0037 };
            for (int i = 0; i < 8; i++) { double r = Math.Sqrt(1 + ci[i] * ci[i]); cs[i] = (float)(1 / r); ca[i] = (float)(ci[i] / r); }
        }

        internal RocolaOsHuecosMp3(uint semilla) { azar = semilla == 0 ? 0x9E3779B9u : semilla; }

        // AL SALTAR (el Reset del descompresor): sin deposito ni memoria de la sintesis
        internal void Reinicia()
        {
            depositoN = 0;
            for (int c = 0; c < 2; c++)
            {
                Array.Clear(solape[c], 0, 576); Array.Clear(vbuf[c], 0, 2048); Array.Clear(corrida[c], 0, 22);
                quieto[c] = 99; solapeConAlgo[c] = false;
            }
        }

        // UN CUADRO: los bytes del cuadro entero (desde la cabecera). Deja en «salida» (intercalado, 1152 por canal) lo que hay que sumar y
        // devuelve cuantas muestras por canal (1152), o 0 si no es un cuadro que este lector entienda (otra capa, MPEG-2/2.5, o roto): quien
        // llama suma ceros. conSintesis = false: solo cuenta (la medida del repaso; no gasta en la sintesis).
        internal int Cuadro(byte[] b, int largo, float[] salida, bool conSintesis)
        {
            Cuadros++;
            if (b == null || largo < 4 || b[0] != 0xFF || (b[1] & 0xE0) != 0xE0) { CuadrosRaros++; return 0; }
            int version = (b[1] >> 3) & 3, capa = (b[1] >> 1) & 3;
            bool crc = (b[1] & 1) == 0;
            int kbpsIdx = b[2] >> 4, ti = (b[2] >> 2) & 3;
            if (version != 3 || capa != 1 || ti == 3 || kbpsIdx == 0 || kbpsIdx == 15) { CuadrosRaros++; return 0; }   // solo MPEG-1 capa III
            modo = b[3] >> 6; modoExt = (b[3] >> 4) & 3;
            int ch = modo == 3 ? 1 : 2;
            if (tasaIdx != ti || canales != ch) { Reinicia(); tasaIdx = ti; canales = ch; }
            int lateral = ch == 1 ? 17 : 32, inicio = 4 + (crc ? 2 : 0);
            if (largo < inicio + lateral) { CuadrosRaros++; return 0; }
            // la informacion lateral
            bitPos = 0;
            int mdb = LeeLateral(b, inicio, ch);
            // los datos principales: lo que pide del deposito y lo de este cuadro
            int propio = largo - inicio - lateral;
            bool vale = mdb <= depositoN && mdb + propio <= datos.Length;
            if (vale)
            {
                Buffer.BlockCopy(deposito, depositoN - mdb, datos, 0, mdb);
                Buffer.BlockCopy(b, inicio + lateral, datos, mdb, propio);
            }
            // el deposito para los siguientes: los ultimos bytes de datos (main_data_begin es de 9 bits: 511 como mucho)
            if (propio > 0)
            {
                if (depositoN + propio > deposito.Length) { int q = depositoN + propio - deposito.Length; Buffer.BlockCopy(deposito, q, deposito, 0, depositoN - q); depositoN -= q; }
                Buffer.BlockCopy(b, inicio + lateral, deposito, depositoN, propio); depositoN += propio;
                if (depositoN > 1024) { Buffer.BlockCopy(deposito, depositoN - 1024, deposito, 0, 1024); depositoN = 1024; }
            }
            if (salida != null) Array.Clear(salida, 0, 1152 * ch);
            if (!vale) { CuadrosRaros++; DosGranulosVacios(salida, ch, conSintesis); return 1152; }   // (tras un salto: sin lo de antes no se lee; como libmpg123, suena lo que suene)
            int total = (mdb + propio) * 8, pos = 0;
            for (int gr = 0; gr < 2; gr++)
            {
                Granulos++;
                for (int c = 0; c < ch; c++)
                {
                    int ini = pos; bitPos = ini; bitFin = Math.Min(ini + part23[gr, c], total);
                    if (bitFin < ini) bitFin = ini;
                    LeeFactores(gr, c);
                    LeeHuffman(gr, c);
                    pos = ini + part23[gr, c];
                }
                bool largoTodo = true;
                for (int c = 0; c < ch; c++) if (tipo[gr, c] == 2) largoTodo = false;
                bool intensidad = modo == 1 && (modoExt & 1) != 0, ms = modo == 1 && (modoExt & 2) != 0;
                Recuantiza(gr, ch);
                if (largoTodo && !intensidad) { GranulosLargos++; BuscaHuecos(ch, ms, conSintesis || Propio); }
                else for (int c = 0; c < ch; c++) Array.Clear(corrida[c], 0, 22);
                if (!conSintesis) continue;
                // lo que se sintetiza: el relleno (ya en xr) o, con Propio, todo
                if (Propio) { for (int c = 0; c < ch; c++) EspectroEntero(gr, c); if (intensidad) CuadrosRaros++; }
                if (ms) for (int i = 0; i < 576; i++) { float m = xr[0][i], s = xr[1][i]; xr[0][i] = (m + s) * 0.70710678f; xr[1][i] = (m - s) * 0.70710678f; }
                for (int c = 0; c < ch; c++) Sintetiza(gr, c, salida, ch);
            }
            return 1152;
        }

        private void DosGranulosVacios(float[] salida, int ch, bool conSintesis)
        {
            if (!conSintesis) return;
            for (int gr = 0; gr < 2; gr++)
                for (int c = 0; c < ch; c++) { Array.Clear(xr[c], 0, 576); tipo[gr, c] = 0; mixto[gr, c] = false; Sintetiza(gr, c, salida, ch); }
        }

        // ---------- la lectura de bits ----------
        private byte[] fuente;
        private int Bits(int n)
        {
            int v = 0;
            for (int i = 0; i < n; i++)
            {
                int p = bitPos++;
                int bit = (fuente[p >> 3] >> (7 - (p & 7))) & 1;
                v = (v << 1) | bit;
            }
            return v;
        }
        private int Bit()
        {
            int p = bitPos++;
            if (p >= bitFin) return 0;
            return (fuente[p >> 3] >> (7 - (p & 7))) & 1;
        }

        private int LeeLateral(byte[] b, int inicio, int ch)
        {
            fuente = b; bitPos = inicio * 8;
            int mdb = Bits(9);
            Bits(ch == 1 ? 5 : 3);
            for (int c = 0; c < ch; c++) for (int g = 0; g < 4; g++) scfsi[c, g] = Bits(1) == 1;
            for (int gr = 0; gr < 2; gr++)
                for (int c = 0; c < ch; c++)
                {
                    part23[gr, c] = Bits(12); bigValues[gr, c] = Math.Min(288, Bits(9)); ganancia[gr, c] = Bits(8); scfComp[gr, c] = Bits(4);
                    bool ventana = Bits(1) == 1;
                    if (ventana)
                    {
                        tipo[gr, c] = Bits(2); mixto[gr, c] = Bits(1) == 1;
                        for (int r = 0; r < 2; r++) tablaSel[gr, c, r] = Bits(5);
                        tablaSel[gr, c, 2] = 0;
                        for (int w = 0; w < 3; w++) subGanancia[gr, c, w] = Bits(3);
                        region0[gr, c] = (tipo[gr, c] == 2 && !mixto[gr, c]) ? 8 : 7; region1[gr, c] = 36;
                        if (tipo[gr, c] == 0) tipo[gr, c] = 1;   // (un cuadro roto: la norma lo prohibe; que no se lea como largo de los de siempre)
                    }
                    else
                    {
                        tipo[gr, c] = 0; mixto[gr, c] = false;
                        for (int r = 0; r < 3; r++) tablaSel[gr, c, r] = Bits(5);
                        for (int w = 0; w < 3; w++) subGanancia[gr, c, w] = 0;
                        region0[gr, c] = Bits(4); region1[gr, c] = Bits(3);
                    }
                    preflag[gr, c] = Bits(1) == 1; escalaSf[gr, c] = Bits(1) == 1; count1B[gr, c] = Bits(1) == 1;
                }
            fuente = datos;
            return mdb;
        }

        // los factores de escala (MPEG-1)
        private void LeeFactores(int gr, int c)
        {
            int s1 = RocolaOsHuecosTablas.Slen1[scfComp[gr, c]], s2 = RocolaOsHuecosTablas.Slen2[scfComp[gr, c]];
            int[] L = sfLarga[c]; int[,] S = sfCorta[c];
            if (tipo[gr, c] == 2)
            {
                if (mixto[gr, c])
                {
                    for (int sfb = 0; sfb < 8; sfb++) L[sfb] = BitsAcotados(s1);
                    for (int sfb = 3; sfb < 12; sfb++) for (int w = 0; w < 3; w++) S[sfb, w] = BitsAcotados(sfb < 6 ? s1 : s2);
                }
                else for (int sfb = 0; sfb < 12; sfb++) for (int w = 0; w < 3; w++) S[sfb, w] = BitsAcotados(sfb < 6 ? s1 : s2);
                for (int w = 0; w < 3; w++) S[12, w] = 0;
                return;
            }
            int[] desde = { 0, 6, 11, 16, 21 };
            for (int g = 0; g < 4; g++)
            {
                if (gr == 1 && scfsi[c, g]) continue;   // los del granulo 0, que ya estan en L
                int n = g < 2 ? s1 : s2;
                for (int sfb = desde[g]; sfb < desde[g + 1]; sfb++) L[sfb] = BitsAcotados(n);
            }
            L[21] = 0;
        }
        private int BitsAcotados(int n)
        {
            if (n == 0) return 0;
            int v = 0;
            for (int i = 0; i < n; i++) v = (v << 1) | Bit();
            return v;
        }

        // los valores de Huffman: la region grande (tres tablas) y la de cuatro valores
        private void LeeHuffman(int gr, int c)
        {
            int[] v = valor[c];
            Array.Clear(v, 0, 576);
            short[] bl = RocolaOsHuecosTablas.BandasLargas[tasaIdx];
            int r1, r2;
            if (tipo[gr, c] != 0 || mixto[gr, c]) { r1 = 36; r2 = 576; }
            else
            {
                int a = Math.Min(22, region0[gr, c] + 1), z = Math.Min(22, region0[gr, c] + region1[gr, c] + 2);
                r1 = bl[a]; r2 = bl[z];
            }
            int grandes = bigValues[gr, c] * 2, i = 0;
            while (i < grandes && bitPos < bitFin)
            {
                int t = i < r1 ? tablaSel[gr, c, 0] : i < r2 ? tablaSel[gr, c, 1] : tablaSel[gr, c, 2];
                int arbol = RocolaOsHuecosTablas.ArbolDeTabla[t];
                if (arbol < 0) { v[i] = 0; v[i + 1] = 0; i += 2; continue; }
                int xy = Simbolo(arbol), x = xy >> 4, y = xy & 15, lb = RocolaOsHuecosTablas.LinBits[t];
                if (lb > 0 && x == 15) x += BitsAcotados(lb);
                if (x != 0 && Bit() == 1) x = -x;
                if (lb > 0 && y == 15) y += BitsAcotados(lb);
                if (y != 0 && Bit() == 1) y = -y;
                v[i] = x; v[i + 1] = y; i += 2;
            }
            int arbol1 = count1B[gr, c] ? 16 : 15;
            while (i + 4 <= 576 && bitPos < bitFin)
            {
                int vwxy = Simbolo(arbol1);
                int a = (vwxy >> 3) & 1, bb = (vwxy >> 2) & 1, x = (vwxy >> 1) & 1, y = vwxy & 1;
                if (a != 0 && Bit() == 1) a = -1;
                if (bb != 0 && Bit() == 1) bb = -1;
                if (x != 0 && Bit() == 1) x = -1;
                if (y != 0 && Bit() == 1) y = -1;
                if (bitPos > bitFin) break;   // se paso del final: ese cuarteto no es de verdad (como libmpg123)
                v[i] = a; v[i + 1] = bb; v[i + 2] = x; v[i + 3] = y; i += 4;
            }
        }
        // un simbolo con el arbol de la norma (la forma de dist10)
        private int Simbolo(int arbol)
        {
            byte[] h = RocolaOsHuecosTablas.Arboles[arbol];
            int nodos = h.Length / 2, p = 0;
            for (int vueltas = 0; vueltas < 64; vueltas++)
            {
                if (p < 0 || p >= nodos) return 0;
                if (h[2 * p] == 0) return h[2 * p + 1];
                if (Bit() == 1) { while (p < nodos && h[2 * p + 1] >= 250) p += h[2 * p + 1]; if (p >= nodos) return 0; p += h[2 * p + 1]; }
                else { while (p < nodos && h[2 * p] >= 250) p += h[2 * p]; if (p >= nodos) return 0; p += h[2 * p]; }
            }
            return 0;
        }

        // ---------- la energia y el paso de cada banda (solo granulos largos; con Propio, el espectro entero) ----------
        private void Recuantiza(int gr, int ch)
        {
            short[] bl = RocolaOsHuecosTablas.BandasLargas[tasaIdx];
            for (int c = 0; c < ch; c++)
            {
                int[] v = valor[c];
                double mult = escalaSf[gr, c] ? 1.0 : 0.5;
                ultima[c] = 0;
                for (int sfb = 0; sfb < 22; sfb++)
                {
                    double e = 0; int n = 0;
                    for (int i = bl[sfb]; i < bl[sfb + 1]; i++) { int a = v[i]; if (a != 0) { float p = pot43[a < 0 ? -a : a]; e += (double)p * p; n++; ultima[c] = i + 1; } }
                    double g = Math.Pow(2.0, 0.25 * (ganancia[gr, c] - 210) - mult * (sfLarga[c][sfb] + (preflag[gr, c] ? RocolaOsHuecosTablas.Pretab[sfb] : 0)));
                    energia[c][sfb] = e * g * g; paso2[c][sfb] = g * g; conAlgo[c][sfb] = n;
                }
                Array.Clear(xr[c], 0, 576);
            }
        }

        // ---------- los huecos y el relleno (en el dominio del MP3: L/R, o M/S si viene asi; cada canal con lo suyo) ----------
        // Dos clases de hueco, de 3 kHz para arriba y por debajo de la ultima linea con algo de ese canal en ese granulo:
        //   LINEAS A CERO en una banda que trae algo: el codificador las dejo en cero porque eran menores que medio paso; van con un ruido
        //     de LineaPaso pasos de amplitud eficaz (lo que el propio codificador dio por tapado: su error de cuantizacion es de ese orden), y
        //     nunca a mas de 6 dB bajo lo que traen las lineas con algo de esa banda;
        //   UNA BANDA ENTERA A CERO con algo debajo y algo en una de las dos de encima, corta (hasta 4 granulos entera, se apaga hasta el 8):
        //     la media geometrica de sus vecinas, 6 dB por debajo, y como mucho el ruido parejo de +-medio paso.
        internal static double LineaPaso = LineaPasoDe();
        private static double LineaPasoDe()
        {
            string v = Environment.GetEnvironmentVariable("ROCOLA_HUECOS_PASO");   // (solo para afinar en las pruebas)
            double d;
            return v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) && d >= 0 && d <= 0.5 ? d : 0.1;
        }
        private readonly int[] ultima = new int[2];
        private readonly int[][] conAlgo = { new int[22], new int[22] };
        private void BuscaHuecos(int ch, bool ms, bool rellena)
        {
            short[] bl = RocolaOsHuecosTablas.BandasLargas[tasaIdx];
            double hzPorLinea = RocolaOsHuecosTablas.TasaMpeg1[tasaIdx] / 2.0 / 576.0;
            int desde = 0;
            while (desde < 21 && bl[desde] * hzPorLinea < DesdeHz) desde++;
            double bajo = Math.Pow(10.0, -NivelBajoVecinasDb / 10.0), cuant = 1.0 / 12.0, linea = LineaPaso * LineaPaso;
            for (int c = 0; c < ch; c++)
            {
                int[] v = valor[c]; float[] x = xr[c];
                int fin = ultima[c];
                // (la banda 21, sobre ~16 kHz, no trae factor de escala: sin paso no se sabe cuanto pudo haber)
                for (int sfb = desde; sfb <= 20 && bl[sfb] < fin; sfb++)
                {
                    int a = bl[sfb], z = Math.Min(bl[sfb + 1], fin), ancho = z - a, n = conAlgo[c][sfb];
                    Candidatas += ancho; EnergiaAltos += energia[c][sfb];
                    if (n > 0)
                    {
                        corrida[c][sfb] = 0;
                        int ceros = ancho - n;   // (todas las lineas con algo van antes de «fin»)
                        if (ceros <= 0) continue;
                        Huecos += ceros;
                        double t = Math.Min(linea * paso2[c][sfb], bajo * energia[c][sfb] / n);
                        if (!(t > 0)) continue;
                        EnergiaRelleno += t * ceros;
                        if (!rellena) continue;
                        float amp = (float)(Math.Sqrt(t) * 1.7320508);   // ruido parejo de varianza 1, por la amplitud
                        for (int i = a; i < z; i++) if (v[i] == 0) x[i] = amp * Azar();
                        Rellenos++;
                        continue;
                    }
                    corrida[c][sfb]++;
                    int racha = corrida[c][sfb];
                    if (sfb == 0 || conAlgo[c][sfb - 1] == 0) continue;   // nada debajo
                    int arriba = -1;
                    for (int k = 1; k <= AnchoMax && sfb + k <= 21 && bl[sfb + k] < fin; k++) if (conAlgo[c][sfb + k] > 0) { arriba = sfb + k; break; }
                    if (arriba < 0) continue;   // nada arriba: es el corte, no un hueco
                    if (racha >= GranulosMax) continue;
                    double apaga = racha <= GranulosEnteros ? 1.0 : (double)(GranulosMax - racha) / (GranulosMax - GranulosEnteros);
                    Huecos += ancho;
                    double ab = energia[c][sfb - 1] / (bl[sfb] - bl[sfb - 1]), ar = energia[c][arriba] / (bl[arriba + 1] - bl[arriba]);
                    double tb = Math.Min(bajo * Math.Sqrt(ab * ar), cuant * paso2[c][sfb]) * apaga * apaga;
                    if (!(tb > 0)) continue;
                    EnergiaRelleno += tb * ancho;
                    if (!rellena) continue;
                    float ampb = (float)(Math.Sqrt(tb) * 1.7320508);
                    for (int i = a; i < z; i++) x[i] = ampb * Azar();
                    Rellenos++;
                }
                for (int sfb = 0; sfb < desde; sfb++) corrida[c][sfb] = 0;
            }
        }
        private float Azar()
        {
            uint x = azar; x ^= x << 13; x ^= x >> 17; x ^= x << 5; azar = x;
            return (int)x * (1.0f / 2147483648.0f);
        }

        // (Propio) el espectro entero del granulo, recuantizado (y ordenado como lo espera la sintesis en los cortos)
        private readonly float[] orden = new float[576];
        private void EspectroEntero(int gr, int c)
        {
            int[] v = valor[c]; float[] x = xr[c];
            double mult = escalaSf[gr, c] ? 1.0 : 0.5;
            EspectroEnteroSinEscala(gr, c, v, x, mult);
            if (PropioEscala != 1f) for (int i = 0; i < 576; i++) x[i] *= PropioEscala;
        }
        private void EspectroEnteroSinEscala(int gr, int c, int[] v, float[] x, double mult)
        {
            if (tipo[gr, c] != 2)
            {
                short[] bl = RocolaOsHuecosTablas.BandasLargas[tasaIdx];
                for (int sfb = 0; sfb < 22; sfb++)
                {
                    float g = (float)Math.Pow(2.0, 0.25 * (ganancia[gr, c] - 210) - mult * (sfLarga[c][sfb] + (preflag[gr, c] ? RocolaOsHuecosTablas.Pretab[sfb] : 0)));
                    for (int i = bl[sfb]; i < bl[sfb + 1]; i++) { int a = v[i]; x[i] = a == 0 ? 0f : (a < 0 ? -pot43[-a] : pot43[a]) * g; }
                }
                return;
            }
            if (mixto[gr, c]) { CuadrosRaros++; Array.Clear(x, 0, 576); return; }   // (mixtos: LAME no los hace; aqui no se sintetizan)
            short[] bs = RocolaOsHuecosTablas.BandasCortas[tasaIdx];
            int pos = 0;
            for (int sfb = 0; sfb < 13; sfb++)
            {
                int ancho = bs[sfb + 1] - bs[sfb];
                for (int w = 0; w < 3; w++)
                {
                    float g = (float)Math.Pow(2.0, 0.25 * (ganancia[gr, c] - 210 - 8 * subGanancia[gr, c, w]) - mult * sfCorta[c][sfb, w]);
                    for (int j = 0; j < ancho; j++, pos++)
                    {
                        int a = v[pos]; float val = a == 0 ? 0f : (a < 0 ? -pot43[-a] : pot43[a]) * g;
                        int f = bs[sfb] + j;           // la linea de la ventana w
                        orden[3 * f + w] = val;       // la sintesis la quiere por subbanda: [sb*18 + 3k + w]
                    }
                }
            }
            Array.Copy(orden, x, 576);
        }

        // ---------- la sintesis del MP3 (alias, IMDCT, inversion y banco polifasico) ----------
        private readonly float[] franja = new float[32];
        private void Sintetiza(int gr, int c, float[] salida, int ch)
        {
            float[] x = xr[c], ov = solape[c], tt = tiempo[c];
            int bt = tipo[gr, c];
            bool algo = false;
            for (int i = 0; i < 576; i++) if (x[i] != 0f) { algo = true; break; }
            if (!algo && !solapeConAlgo[c])
            {
                // nada que sintetizar ni que soltar: solo el banco, si aun le queda algo
                if (quieto[c] >= 16) { quieto[c] += 18; return; }
                Array.Clear(tt, 0, 576);
            }
            else
            {
                if (bt != 2)
                    for (int sb = 1; sb < 32; sb++)
                        for (int i = 0; i < 8; i++)
                        {
                            int a = sb * 18 - 1 - i, b = sb * 18 + i;
                            float bu = x[a], bd = x[b];
                            x[a] = bu * cs[i] - bd * ca[i]; x[b] = bd * cs[i] + bu * ca[i];
                        }
                bool solapeQueda = false;
                float[] res = imdct;
                for (int sb = 0; sb < 32; sb++)
                {
                    int o = sb * 18;
                    bool hay = false;
                    for (int k = 0; k < 18; k++) if (x[o + k] != 0f) { hay = true; break; }
                    if (!hay) Array.Clear(res, 0, 36);
                    else if (bt != 2)
                    {
                        float[] w = ventanas[bt];
                        Imdct36(x, o, res);
                        for (int i = 0; i < 36; i++) res[i] *= w[i];
                    }
                    else
                    {
                        Array.Clear(res, 0, 36);
                        for (int w = 0; w < 3; w++)
                            for (int i = 0; i < 12; i++)
                            {
                                float s = 0f;
                                for (int k = 0; k < 6; k++) s += x[o + 3 * k + w] * cos12[i * 6 + k];
                                res[6 + 6 * w + i] += s * ventanaCorta[i];
                            }
                    }
                    for (int i = 0; i < 18; i++)
                    {
                        float v = res[i] + ov[o + i];
                        if ((sb & 1) == 1 && (i & 1) == 1) v = -v;
                        tt[o + i] = v;
                        float sig = res[18 + i]; ov[o + i] = sig;
                        if (sig != 0f) solapeQueda = true;
                    }
                }
                solapeConAlgo[c] = solapeQueda;
            }
            // el banco polifasico: 18 franjas de 32 muestras
            float[] V = vbuf[c];
            for (int t = 0; t < 18; t++)
            {
                bool hay = false;
                for (int sb = 0; sb < 32; sb++) { float v = tt[sb * 18 + t]; franja[sb] = v; if (v != 0f) hay = true; }
                if (hay) quieto[c] = 0; else quieto[c]++;
                int p = vPos[c] = (vPos[c] - 64) & 1023;
                if (!hay) { Array.Clear(V, p, 64); Array.Clear(V, p + 1024, 64); }
                else
                {
                    // V[i] = suma de cos((16+i)(2k+1)pi/64) S[k], i = 0..63, sale de los 32 de la DCT-II de S (C[j], j = 0..31): C[32] = 0,
                    // C[64-j] = -C[j] y C[64+m] = -C[m]. La mitad de cuentas que con la matriz de 64 (y con vectores)
                    Dct32(franja, dctSal);
                    for (int i = 0; i < 64; i++)
                    {
                        float v = i < 16 ? dctSal[i + 16] : i == 16 ? 0f : i < 48 ? -dctSal[48 - i] : -dctSal[i - 48];
                        V[p + i] = v; V[p + 1024 + i] = v;
                    }
                }
                if (quieto[c] >= 16) continue;   // todo el banco en cero: la salida tambien
                float[] D = RocolaOsHuecosTablas.Ventana;
                int baseOut = (gr * 576 + t * 32) * ch + c;
                // las 32 salidas a la vez: la del punto j suma V[p + 128h + j] D[64h + j] y V[p + 128h + 96 + j] D[64h + 32 + j], h = 0..7
                Ventana32(V, p, sale32);
                if (salida != null) for (int j = 0; j < 32; j++) salida[baseOut + j * ch] = sale32[j];
            }
        }
        private readonly float[] dctSal = new float[32], sale32 = new float[32];
        private readonly float[] imdct = new float[40];

        // LAS TRES CUENTAS GRANDES, con vectores de 4 (SSE: lo que tiene el A4 sin AVX2) y las sumas en registros
        private static void Dct32(float[] s, float[] c)   // c[j] = suma de cos(j(2k+1)pi/64) s[k], j = 0..31
        {
            Vector128<float> a0 = default, a1 = default, a2 = default, a3 = default, a4 = default, a5 = default, a6 = default, a7 = default;
            ref float m = ref MemoryMarshal.GetArrayDataReference(dct32);
            for (int k = 0; k < 32; k++)
            {
                float sk = s[k];
                if (sk == 0f) continue;
                Vector128<float> v = Vector128.Create(sk); nuint o = (nuint)(k * 32);
                a0 += v * Vector128.LoadUnsafe(ref m, o); a1 += v * Vector128.LoadUnsafe(ref m, o + 4);
                a2 += v * Vector128.LoadUnsafe(ref m, o + 8); a3 += v * Vector128.LoadUnsafe(ref m, o + 12);
                a4 += v * Vector128.LoadUnsafe(ref m, o + 16); a5 += v * Vector128.LoadUnsafe(ref m, o + 20);
                a6 += v * Vector128.LoadUnsafe(ref m, o + 24); a7 += v * Vector128.LoadUnsafe(ref m, o + 28);
            }
            ref float r = ref MemoryMarshal.GetArrayDataReference(c);
            a0.StoreUnsafe(ref r, 0); a1.StoreUnsafe(ref r, 4); a2.StoreUnsafe(ref r, 8); a3.StoreUnsafe(ref r, 12);
            a4.StoreUnsafe(ref r, 16); a5.StoreUnsafe(ref r, 20); a6.StoreUnsafe(ref r, 24); a7.StoreUnsafe(ref r, 28);
        }
        private static void Ventana32(float[] V, int p, float[] sale)   // sale[j] = suma de V[p+128h+j] D[64h+j] + V[p+128h+96+j] D[64h+32+j]
        {
            Vector128<float> a0 = default, a1 = default, a2 = default, a3 = default, a4 = default, a5 = default, a6 = default, a7 = default;
            ref float vv = ref MemoryMarshal.GetArrayDataReference(V);
            ref float dd = ref MemoryMarshal.GetArrayDataReference(RocolaOsHuecosTablas.Ventana);
            for (int h = 0; h < 8; h++)
            {
                nuint q = (nuint)(p + h * 128), d = (nuint)(h * 64), q2 = q + 96, d2 = d + 32;
                a0 += Vector128.LoadUnsafe(ref vv, q) * Vector128.LoadUnsafe(ref dd, d) + Vector128.LoadUnsafe(ref vv, q2) * Vector128.LoadUnsafe(ref dd, d2);
                a1 += Vector128.LoadUnsafe(ref vv, q + 4) * Vector128.LoadUnsafe(ref dd, d + 4) + Vector128.LoadUnsafe(ref vv, q2 + 4) * Vector128.LoadUnsafe(ref dd, d2 + 4);
                a2 += Vector128.LoadUnsafe(ref vv, q + 8) * Vector128.LoadUnsafe(ref dd, d + 8) + Vector128.LoadUnsafe(ref vv, q2 + 8) * Vector128.LoadUnsafe(ref dd, d2 + 8);
                a3 += Vector128.LoadUnsafe(ref vv, q + 12) * Vector128.LoadUnsafe(ref dd, d + 12) + Vector128.LoadUnsafe(ref vv, q2 + 12) * Vector128.LoadUnsafe(ref dd, d2 + 12);
                a4 += Vector128.LoadUnsafe(ref vv, q + 16) * Vector128.LoadUnsafe(ref dd, d + 16) + Vector128.LoadUnsafe(ref vv, q2 + 16) * Vector128.LoadUnsafe(ref dd, d2 + 16);
                a5 += Vector128.LoadUnsafe(ref vv, q + 20) * Vector128.LoadUnsafe(ref dd, d + 20) + Vector128.LoadUnsafe(ref vv, q2 + 20) * Vector128.LoadUnsafe(ref dd, d2 + 20);
                a6 += Vector128.LoadUnsafe(ref vv, q + 24) * Vector128.LoadUnsafe(ref dd, d + 24) + Vector128.LoadUnsafe(ref vv, q2 + 24) * Vector128.LoadUnsafe(ref dd, d2 + 24);
                a7 += Vector128.LoadUnsafe(ref vv, q + 28) * Vector128.LoadUnsafe(ref dd, d + 28) + Vector128.LoadUnsafe(ref vv, q2 + 28) * Vector128.LoadUnsafe(ref dd, d2 + 28);
            }
            ref float r = ref MemoryMarshal.GetArrayDataReference(sale);
            a0.StoreUnsafe(ref r, 0); a1.StoreUnsafe(ref r, 4); a2.StoreUnsafe(ref r, 8); a3.StoreUnsafe(ref r, 12);
            a4.StoreUnsafe(ref r, 16); a5.StoreUnsafe(ref r, 20); a6.StoreUnsafe(ref r, 24); a7.StoreUnsafe(ref r, 28);
        }
        private static void Imdct36(float[] x, int o, float[] res)   // res[i] = suma de x[o+k] cos(pi/72 (2i+1+18)(2k+1)), i = 0..35 (y 4 de relleno)
        {
            Vector128<float> a0 = default, a1 = default, a2 = default, a3 = default, a4 = default, a5 = default, a6 = default, a7 = default, a8 = default, a9 = default;
            ref float m = ref MemoryMarshal.GetArrayDataReference(cos36);
            for (int k = 0; k < 18; k++)
            {
                float xk = x[o + k];
                if (xk == 0f) continue;
                Vector128<float> v = Vector128.Create(xk); nuint b = (nuint)(k * 40);
                a0 += v * Vector128.LoadUnsafe(ref m, b); a1 += v * Vector128.LoadUnsafe(ref m, b + 4); a2 += v * Vector128.LoadUnsafe(ref m, b + 8);
                a3 += v * Vector128.LoadUnsafe(ref m, b + 12); a4 += v * Vector128.LoadUnsafe(ref m, b + 16); a5 += v * Vector128.LoadUnsafe(ref m, b + 20);
                a6 += v * Vector128.LoadUnsafe(ref m, b + 24); a7 += v * Vector128.LoadUnsafe(ref m, b + 28); a8 += v * Vector128.LoadUnsafe(ref m, b + 32);
                a9 += v * Vector128.LoadUnsafe(ref m, b + 36);
            }
            ref float r = ref MemoryMarshal.GetArrayDataReference(res);
            a0.StoreUnsafe(ref r, 0); a1.StoreUnsafe(ref r, 4); a2.StoreUnsafe(ref r, 8); a3.StoreUnsafe(ref r, 12); a4.StoreUnsafe(ref r, 16);
            a5.StoreUnsafe(ref r, 20); a6.StoreUnsafe(ref r, 24); a7.StoreUnsafe(ref r, 28); a8.StoreUnsafe(ref r, 32); a9.StoreUnsafe(ref r, 36);
        }
    }
}

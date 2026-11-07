// EL DSEE (el reconstructor de agudos, Rocola/Form1.Sonido.cs: SpectralTap), tal cual de XP.
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // ==========  RECONSTRUCTOR ESPECTRAL DE AGUDOS (el DSEE "serio")  ==========
        // Parte el audio en ventanas (STFT de 2048 puntos, solape 50 %, raiz de Hann en analisis y sintesis),
        // localiza el "acantilado" donde el codec corto los agudos y re-crea la banda que falta copiando hacia
        // arriba, por BLOQUES y con la fase corregida, lo que sobrevivio bajo el corte (como el SBR de HE-AAC),
        // con la envolvente de la curva clasica o la que dice la IA propia (ver Regenerate). Archivos ya
        // completos (corte >= ~19,3 kHz) pasan intactos: sin acantilado no se toca ni un bin.
        private sealed class SpectralTap : NAudio.Wave.ISampleProvider
        {
            private const int Frame = 2048, Hop = Frame / 2;
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, rate;
            private readonly bool[] musicales;
            private readonly bool disposicionSegura;
            private readonly float[] cortesGrupo, gravesGrupo;
            private readonly int[] recreadosGrupo;
            private FuenteOriginal corteOriginal;
            private int retardoOriginal, canalDeteccion, ventanasCorte;
            private float[] originalRe, originalIm, originalReB, originalImB;
            private long inicioOriginal;
            internal long VentanasOriginales;
            internal RedChica Red;   // el modelo de esta cancion (I3, el de su genero); null = el general (NeuralEnv)
            internal void ConectaOriginal(FuenteOriginal original, int retardo)
            {
                // Sin etapa que cambie PCM, su FFT YA es original: no duplicar.
                if (retardo <= 0 || !disposicionSegura) return;
                corteOriginal = original; retardoOriginal = retardo;
                original.PreparaCorte(Frame, retardo);
                originalRe = new float[Frame]; originalIm = new float[Frame];
                if (ch > 1) { originalReB = new float[Frame]; originalImB = new float[Frame]; }
            }
            private readonly float binHz;
            public volatile bool Active;                   // false = pase directo (costo ~cero)
            // ENCENDER Y APAGAR EN VIVO SIN SALTOS (Ctrl+F3, A/B). Antes, al alternar se tiraba el colchon del
            // STFT: al APAGARLO se perdian de golpe los 23-93 ms de musica que ya se habian leido (un salto
            // hacia delante con chasquido) y al ENCENDERLO la banda recreada entraba de golpe. Ahora:
            //  - la banda recreada entra y sale con un fundido de ~100 ms, y nunca en menos de 4 ventanas (con las
            //    de 4096 del nivel HD, 171-186 ms); va por ventana y el solape lo suaviza;
            //  - al apagar, el STFT sigue hasta que el fundido llega a cero; entonces se entrega lo que ya
            //    estaba leido, tal cual, y se sigue en pase directo: no se pierde ni se repite ni una muestra;
            //  - al empezar la cancion NO hay fundido: si empieza encendido, reconstruye desde la primera muestra.
            private bool stft;                             // el STFT esta en marcha (encendido, o apagandose)
            private bool arrancado;                        // ya se leyo algo: la primera lectura no hace fundido
            private bool saliendo; private int saleIdx;    // apagado del todo: entregando lo ya leido, tal cual
            private float regen = 1f, regenObjetivo = 1f;  // cuanto de la banda recreada entra (0..1) y hacia donde va;
                                                           // 1 si nadie lo toca (el analizador de la cosecha no lee)
            private float pasoRegen;                       // cuanto cambia por ventana (~100 ms; al menos 4 ventanas)
            private bool ultimoCero;                       // la ultima ventana no recreo nada: se puede salir sin salto
            // CUADROS PARTIDOS. Si una lectura trae un cuadro a medias, lo que sobra espera al principio de 'inter'
            // (restoN muestras) y se completa con la siguiente; antes se tiraba y los canales quedaban cruzados el
            // resto de la cancion. (Arrancar el STFT a media trama no hace falta evitarlo: trata igual a todos los
            // canales y devuelve cada muestra a su sitio; solo cambiaria cual llama canal 0.)
            private int restoN;
            private bool firstFrame, detectPair;
            private readonly float[][] inBuf, outAcc;      // por canal: ventana de entrada y acumulador de solape
            private int fill;                              // muestras acumuladas (por canal) en inBuf
            private float[] re, im;                        // espectro de trabajo (se INTERCAMBIA con el del otro canal del par)
            private float[] reB, imB;                      // espectro del segundo canal del par (solo si hay 2 o mas)
            private readonly float[] win;                  // ventana raiz-de-Hann
            private readonly int[] bitrev; private readonly float[] twCos, twSin, twSinNeg;   // tablas de la FFT (el seno tambien cambiado de signo: ver Fft)
            private readonly float[] inter;                // buffer intercalado para leer del origen
            private readonly float[] outQ; private int qHead, qLen;   // FIFO de salida (intercalado)
            private float cutHz;                           // acantilado suavizado (0 = aun sin medir)
            private bool srcEnded;
            private long sourceFrames, emittedFrames;      // conserva tambien la fraccion final menor que un Hop
            private readonly float[] iaIn = new float[28], iaOut = new float[12];   // IA propia: entrada/salida de la red (reusables)
            // Limites exactos: solo dependen del corte y de esta FFT, no de las muestras.
            // Conservar la aritmetica original al prepararlos evita cambiar un solo bin.
            private int iaFeatureCut = -1, iaTargetCut = -1, iaTargetTop = -1;
            private readonly int[] iaFeatureA = new int[24], iaFeatureB = new int[24];
            private readonly int[] iaTargetA = new int[12], iaTargetB = new int[12];
            // LA COPIA POR BLOQUES (ver Regenerate)
            private int canalRegen;                        // canal que se esta recreando (lo pone ProcessFrame)
            private bool ventanaImpar;                     // paridad de la ventana: mantiene la fase de la copia
            private int corteRecreado;                     // el corte con el que se recrea, con histeresis (0 = aun ninguno)
            private float[] ganHoy;                        // por bin sobre el corte: la escala de la copia de una octava abajo
            private double[][] numSuave, denSuave;         // por canal y sub-banda: las dos energias de la escala, suavizadas
            private bool[] suaveListo;                     // por canal: ya hay energias previas con las que suavizar
            private readonly int[] bordes = new int[160];  // bordes (bin) de las sub-bandas de la copia
            private int[] marcas;                          // (para armar los bordes)
            private int nBordes, bordesCorte = -1, bordesTop = -1, bordesLargo = -1;
            private float alfaSuave;                       // cuanto se mueven esas energias por ventana (~50 ms)
            private readonly float[] ampSub = new float[160];   // por sub-banda: la escala de esta ventana, en amplitud
            private float[] fuenteSuave;                   // bajo el corte: cuanto suena la fuente por ahi, sin picos ni huecos
            private readonly double[] bandaFuente = new double[12];   // (las 12 bandas de 1/12 de octava de la fuente)
            private readonly int[] bordeFuente = new int[13];
            private readonly float[] medFuente = new float[12], cenFuente = new float[12];   // (su mediana y su centro)
            // EL NIVEL QUE DE VERDAD SONABA. La copia de antes perdia energia al solapar las ventanas: cada ventana
            // salia con otra forma en el tiempo, que la ventana de sintesis recortaba, y dos ventanas vecinas no se
            // sumaban en fase. Sonaba 10*log10(8/3) = 4,26 dB por debajo de la energia que le pedia la envolvente
            // (la copia por bloques sin esta escala salia de +3,8 a +4,2 dB mas fuerte, medido con ruido). La copia
            // por bloques no pierde nada, asi que se escala a lo que sonaba cuando funcionaba. (Ojo: casi siempre
            // funcionaba poco, porque media la costura en un trozo vacio; ver EnvolventeClasica.) Si el brillo se
            // quiere mas bajo o mas alto, es esta constante y nada mas.
            private const float PotenciaDeAntes = 0.375f;  // 3/8, en potencia

            private volatile bool mejoro;                  // ya reconstruyo al menos UNA vez en esta cancion

            public SpectralTap(NAudio.Wave.ISampleProvider s)
            {
                src = s;
                ch = Math.Max(1, s.WaveFormat.Channels);
                musicales = FormatoAudio.Musicales(s.WaveFormat);
                disposicionSegura = ch <= 2 || FormatoAudio.Mascara(s.WaveFormat) != 0;
                cortesGrupo = new float[(ch + 1) / 2]; gravesGrupo = new float[cortesGrupo.Length];
                recreadosGrupo = new int[cortesGrupo.Length];
                rate = Math.Max(8000, s.WaveFormat.SampleRate);
                binHz = (float)rate / Frame;
                inBuf = new float[ch][]; outAcc = new float[ch][];
                for (int c = 0; c < ch; c++) { inBuf[c] = new float[Frame]; outAcc[c] = new float[Frame]; }
                re = new float[Frame]; im = new float[Frame];
                ganHoy = new float[Frame / 2];
                fuenteSuave = new float[Frame / 2];
                if (ch >= 2) { reB = new float[Frame]; imB = new float[Frame]; }   // los canales se procesan de dos en dos (ver ProcessFrame)
                AudioFftPlan plan = GetAudioFftPlan(Frame);
                win = plan.Window; bitrev = plan.Reverse;
                twCos = plan.Cos; twSin = plan.Sin; twSinNeg = plan.SinNeg;
                inter = new float[Hop * ch];
                outQ = new float[Frame * ch * 2];          // FIFO holgado (nunca acumula mas de Hop*ch)
            }

            // Tablas de la FFT (inversion de bits y factores de giro). Las comparten el camino de
            // reproduccion y el de solo analisis, para que ambos calculen EXACTAMENTE el mismo espectro.
            // Las tablas se crean en GetAudioFftPlan; no se modifican al procesar muestras.

            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                // Sin layout multicanal no adivinar LFE ni enlazar canales.
                if (!disposicionSegura) return src.Read(buffer, offset, count);
                bool act = Active;
                if (!arrancado)
                {
                    arrancado = true;
                    pasoRegen = Math.Min(0.25f, Hop / (0.1f * rate));   // ~100 ms de fundido, y nunca menos de 4 ventanas
                    regen = act ? 1f : 0f;                         // al empezar la cancion, tal cual: sin fundido
                    if (act) EmpiezaStft();
                }
                else if (act && !stft) { regen = 0f; EmpiezaStft(); }   // encendido a media cancion: entra con fundido
                regenObjetivo = act ? 1f : 0f;
                if (!stft) return src.Read(buffer, offset, count);   // pase directo

                int written = 0;
                while (written < count)
                {
                    if (qLen > 0)                                    // hay salida lista: entregarla
                    {
                        // De golpe, no muestra a muestra. Esto corre por CADA muestra que sale del
                        // reconstructor (88.200 por segundo en estereo) y antes llevaba una comprobacion de
                        // vuelta al principio del circular en cada una. Como mucho son dos copias en bloque:
                        // hasta el final del circular y, si daba la vuelta, lo que quede desde el principio.
                        // A MANO, NO CON Array.Copy, Y NO ES CAPRICHO. Lo que llega aqui como float[] es
                        // EL MISMO OBJETO que NAudio le entrega al reproductor como byte[]: su WaveBuffer
                        // solapa las dos vistas en la misma memoria a proposito. Escribir muestra a muestra
                        // funciona -para numeros no se comprueba el tipo-, pero Array.Copy SI lo comprueba,
                        // ve un byte[] donde le dicen float[] y lanza un error que MATA la cancion. Eso era
                        // lo que cortaba la musica al encender el DSEE.
                        int take = Math.Min(qLen, count - written);
                        int hasta = outQ.Length - qHead;
                        int t1 = (take < hasta) ? take : hasta;
                        int dst = offset + written;
                        for (int k = 0; k < t1; k++) buffer[dst + k] = outQ[qHead + k];
                        if (t1 < take)
                        {
                            int dst2 = dst + t1, resto = take - t1;
                            for (int k = 0; k < resto; k++) buffer[dst2 + k] = outQ[k];
                        }
                        qHead += take; if (qHead >= outQ.Length) qHead -= outQ.Length;
                        qLen -= take; written += take;
                        continue;
                    }
                    if (saliendo)
                    {
                        // Lo que ya estaba leido del origen y aun no salio, tal cual (el fundido ya llego a cero, asi
                        // que la reconstruccion de esas muestras seria ellas mismas). Despues, pase directo.
                        int enBuf = fill * ch, total = enBuf + restoN;
                        while (written < count && saleIdx < total)
                        {
                            buffer[offset + written] = saleIdx < enBuf ? inBuf[saleIdx % ch][saleIdx / ch] : inter[saleIdx - enBuf];
                            saleIdx++; written++;
                        }
                        if (saleIdx < total) break;
                        saliendo = false; stft = false; restoN = 0;
                        if (written < count) { int d = src.Read(buffer, offset + written, count - written); if (d > 0) written += d; }
                        break;
                    }
                    if (!srcEnded && regenObjetivo <= 0f && regen <= 0f && ultimoCero)
                    { saliendo = true; saleIdx = 0; continue; }       // apagado y ya sin nada recreado: salir sin salto
                    if (srcEnded)
                    {
                        long quedan = sourceFrames - emittedFrames;
                        if (quedan <= 0) break;
                        // Cada ventana normal emite Hop cuadros. Al final pueden quedar menos; se procesa
                        // otra ventana solapada y solo se publica lo que realmente pertenecia al origen.
                        for (int c = 0; c < ch; c++) Array.Clear(inBuf[c], fill, Frame - fill);
                        fill = Frame;
                        ProcessFrame((int)Math.Min((long)Hop, quedan));
                        continue;
                    }
                    int need = (Frame - fill) * ch - restoN;         // llenar la ventana de analisis
                    if (need > inter.Length - restoN) need = inter.Length - restoN;
                    int got = src.Read(inter, restoN, need);
                    if (got <= 0)
                    {
                        srcEnded = true;                              // un cuadro que quedo a medias al final no se completa
                        continue;
                    }
                    LimpiaMuestras(inter, restoN, got);   // antes de FFT, solapes y suavizados
                    got += restoN;
                    int frames = got / ch;
                    sourceFrames += frames;
                    if (ch == 2)
                    {   // el caso normal, con los dos arrays sacados FUERA del bucle (dentro, cada muestra
                        // volvia a buscar inBuf[c] en la tabla de arrays y a comprobar sus limites)
                        float[] b0 = inBuf[0], b1 = inBuf[1];
                        for (int f = 0, k = 0; f < frames; f++, k += 2) { b0[fill + f] = inter[k]; b1[fill + f] = inter[k + 1]; }
                    }
                    else
                        for (int f = 0; f < frames; f++)
                            for (int c = 0; c < ch; c++) inBuf[c][fill + f] = inter[f * ch + c];
                    restoN = got - frames * ch;                       // lo que sobra de un cuadro partido: al principio
                    for (int k = 0; k < restoN; k++) inter[k] = inter[frames * ch + k];
                    fill += frames;
                    if (fill >= Frame) ProcessFrame(Hop);
                }
                return written;
            }

            // Pone el STFT en marcha desde donde va el origen. Un Hop de ceros ANTES del audio completa el solape
            // inicial: se procesa, pero no se entrega, y la primera muestra real conserva sus dos ventanas y su
            // ataque. No se pierde ni se repite nada: lo que sale sigue justo donde iba el pase directo.
            private void EmpiezaStft()
            {
                inicioOriginal = corteOriginal != null ? corteOriginal.CuadrosOriginales - retardoOriginal : 0;
                ventanasCorte = 0;
                stft = true; saliendo = false; ultimoCero = false; restoN = 0;
                ventanaImpar = false;
                if (suaveListo != null) for (int c = 0; c < suaveListo.Length; c++) suaveListo[c] = false;
                fill = Hop; firstFrame = true; qHead = 0; qLen = 0;
                srcEnded = false; sourceFrames = 0; emittedFrames = 0;
                for (int c = 0; c < ch; c++)
                { Array.Clear(outAcc[c], 0, Frame); Array.Clear(inBuf[c], 0, Frame); }
            }

            // Procesa UNA ventana completa: FFT -> (detector + regeneracion) -> iFFT -> solape-y-suma.
            // Emite Hop muestras por canal al FIFO y desliza entrada/salida media ventana.
            // ---------- COSECHA DE MATERIAL PARA LA IA ----------
            // Para ENSEÑARLE a la red hacen falta pares "esto queda / estos eran los agudos reales". En un MP3
            // dañado los agudos reales YA NO EXISTEN, asi que solo sirven las canciones que aun los conservan:
            // a esas se les corta el agudo A PROPOSITO y se guarda lo que habia arriba. Todo esto usa las MISMAS
            // funciones que la reproduccion (BuildIaFeatures, Hz, la FFT), asi que el material de entrenamiento
            // coincide EXACTAMENTE con lo que la red vera en vivo: si no, lo aprendido no serviria de nada.
            public SpectralTap(int rateHz, int channels)   // solo analisis: no reproduce, no lee de ninguna fuente
            {
                src = null; ch = channels < 1 ? 1 : channels; rate = rateHz < 8000 ? 44100 : rateHz;
                musicales = new bool[ch]; for (int c = 0; c < ch; c++) musicales[c] = true;
                binHz = (float)rate / Frame;
                re = new float[Frame]; im = new float[Frame];
                ganHoy = new float[Frame / 2];
                fuenteSuave = new float[Frame / 2];
                AudioFftPlan plan = GetAudioFftPlan(Frame);
                win = plan.Window; bitrev = plan.Reverse;
                twCos = plan.Cos; twSin = plan.Sin; twSinNeg = plan.SinNeg;
            }
            public int FrameLen { get { return Frame; } }
            internal bool ReiniciaAnalisis(int rateHz)
            {
                // Solo el constructor de medidas. Jamás resetear aqui el STFT de reproduccion.
                int normal = rateHz < 8000 ? 44100 : rateHz;
                if (src != null || inBuf != null || cortesGrupo != null || corteOriginal != null ||
                    ch != 1 || rate != normal || FrameLen != 2048) return false;
                cutHz = cutLoHz = 0f;
                // AnalyzeFrame sobrescribe re/im completos; las caches de bins son pura geometria.
                return true;
            }
            public float CutHz { get {
                if (cortesGrupo == null) return cutHz; // analizador de cosecha, un canal
                float mayor = 0; for (int i = 0; i < cortesGrupo.Length; i++) mayor = Math.Max(mayor, cortesGrupo[i]);
                return mayor;
            } }
            public float CutLoHz { get {
                if (gravesGrupo == null) return cutLoHz;
                float menor = 0; for (int i = 0; i < gravesGrupo.Length; i++)
                    if (gravesGrupo[i] > 0 && (menor == 0 || gravesGrupo[i] < menor)) menor = gravesGrupo[i];
                return menor;
            } }

            // Analiza UN cuadro (Frame muestras de UN canal) y actualiza el acantilado. Igual que en vivo.
            public void AnalyzeFrame(float[] mono, int off)
            {
                for (int i = 0; i < Frame; i++) { re[i] = mono[off + i] * win[i]; im[i] = 0f; }
                Fft(false);
                DetectCliff();
            }
            // Con el cuadro ya analizado, arma un par de entrenamiento simulando un corte en cutHzSim:
            // entrada = lo que la red veria (solo mira POR DEBAJO del corte) y objetivo = la energia REAL que
            // hay ARRIBA, en la misma escala que la red debe predecir (log10 de la banda contra la costura).
            public bool MakePair(float cutHzSim, float[] featOut, float[] tgtOut)
            {
                int ny = Frame / 2;
                int cut = (int)(cutHzSim / binHz);
                if (cut < 64) return false;
                int topBin = Math.Min((int)(Math.Min(20500f, ny * binHz * 0.95f) / binHz), Math.Min(2 * cut - 2, ny - 4));
                if (topBin <= cut + 4) return false;
                if (!BuildIaFeatures(cut)) return false;
                for (int i = 0; i < 28; i++) featOut[i] = iaIn[i];
                float n2 = (float)Frame * (float)Frame;
                int sa = Hz(cut * binHz * 0.92f); if (sa >= cut) sa = cut - 1;
                double seamE = 0; for (int k = sa; k < cut; k++) seamE += re[k] * re[k] + im[k] * im[k];
                seamE = seamE / ((cut - sa) * n2) + 1e-12;
                PreparaIaTargetBands(cut, topBin);
                for (int j = 0; j < 12; j++)
                {
                    int a = iaTargetA[j], b = iaTargetB[j];
                    if (b <= a) return false;
                    double realE = 0; for (int k = a; k < b; k++) realE += re[k] * re[k] + im[k] * im[k];
                    realE = realE / ((b - a) * n2) + 1e-12;
                    float y = (float)Math.Log10(realE / seamE);
                    if (y > 0.1f) y = 0.1f; else if (y < -4f) y = -4f;   // mismos topes que aplica la reproduccion
                    tgtOut[j] = y;
                }
                return true;
            }

            // DOS CANALES POR EL PRECIO DE UNA FFT.
            //
            // Esto era, con diferencia, lo mas caro de toda la cadena de sonido: cuatro FFT de 2048 puntos por
            // ventana en estereo (ida y vuelta, por canal), el 73% del coste del audio... y todo ello en el
            // MISMO hilo que dibuja la pantalla, o sea tiempo robado a los cuadros.
            //
            // El truco es viejo y exacto: la musica es una senal REAL, asi que su espectro es simetrico y media
            // FFT compleja se estaba desperdiciando. Metiendo el canal izquierdo en la parte real y el derecho
            // en la imaginaria, UNA sola FFT trae los dos espectros mezclados, y se desenredan con esa misma
            // simetria (ver SeparaCanales). A la vuelta igual: se juntan los dos espectros ya arreglados en uno
            // solo y una sola FFT inversa devuelve los dos canales, uno en la parte real y otro en la imaginaria.
            //
            // Cuatro FFT pasan a ser DOS. No es una aproximacion ni un atajo: da lo mismo que antes salvo el
            // ultimo pelo de redondeo de los flotantes (~1e-7, unas mil veces por debajo del ultimo bit de 16).
            // Con un numero impar de canales, el que sobra va por el camino de siempre.
            private void ProcessFrame(int emitFrames)
            {
                ventanaImpar = !ventanaImpar;   // la copia por bloques corrige la fase segun la paridad de la ventana
                if (!firstFrame)   // la ventana de arranque no recrea nada: no gasta un paso del fundido
                {
                    if (regen < regenObjetivo) regen = Math.Min(regenObjetivo, regen + pasoRegen);
                    else if (regen > regenObjetivo) regen = Math.Max(regenObjetivo, regen - pasoRegen);
                }
                int cn = 0;
                for (; cn + 1 < ch; cn += 2)
                {
                    float[] x0 = inBuf[cn], x1 = inBuf[cn + 1];
                    for (int i = 0; i < Frame; i++) { float w = win[i]; re[i] = x0[i] * w; im[i] = x1[i] * w; }
                    Fft(false);
                    SeparaCanales();             // re/im = espectro del canal par ; reB/imB = el del impar
                    // La ventana de arranque (media ventana de ceros y media de musica) NO mide el acantilado: el
                    // salto de cero a musica en su centro salpica energia hasta 20 kHz y la daba por "completa";
                    // la reconstruccion no empezaba hasta que esa medida falsa se diluia (cientos de ms) y entonces
                    // aparecia de golpe. Se mide desde la primera ventana de musica entera. Y por lo mismo, tampoco las
                    // del final, rellenadas con ceros (srcEnded): el corte seco de la ultima nota salpica igual y
                    // movia el corte con el que se recreaban los ultimos milisegundos.
                    PreparaCorteGrupo(cn, true);
                    canalRegen = cn; if (musicales[cn]) Regenerate();
                    Intercambia(); canalRegen = cn + 1; if (musicales[cn + 1]) Regenerate(); Intercambia();
                    // se vuelven a juntar: parte real = canal par, parte imaginaria = canal impar
                    for (int i = 0; i < Frame; i++) { float t = re[i] - imB[i]; im[i] = im[i] + reB[i]; re[i] = t; }
                    Fft(true);
                    float[] a0 = outAcc[cn], a1 = outAcc[cn + 1];
                    for (int i = 0; i < Frame; i++) { float w = win[i]; a0[i] += re[i] * w; a1[i] += im[i] * w; }
                }
                for (; cn < ch; cn++)            // canal suelto (mono, o un numero impar de canales)
                {
                    float[] xin = inBuf[cn];
                    for (int i = 0; i < Frame; i++) { re[i] = xin[i] * win[i]; im[i] = 0f; }
                    Fft(false);
                    PreparaCorteGrupo(cn, false);
                    canalRegen = cn; if (musicales[cn]) Regenerate();
                    Fft(true);
                    float[] acc = outAcc[cn];
                    for (int i = 0; i < Frame; i++) acc[i] += re[i] * win[i];
                }
                ventanasCorte++;
                if (firstFrame) { emitFrames = 0; firstFrame = false; } // descarta solo el prefijo artificial
                if (emitFrames < 0) emitFrames = 0; else if (emitFrames > Hop) emitFrames = Hop;
                for (int f = 0; f < emitFrames; f++) // la primera mitad ya recibio sus DOS solapes: lista para salir
                    for (int c = 0; c < ch; c++)
                    { int tail = qHead + qLen; if (tail >= outQ.Length) tail -= outQ.Length; outQ[tail] = outAcc[c][f]; qLen++; }
                emittedFrames += emitFrames;
                for (int c = 0; c < ch; c++)
                {
                    Array.Copy(outAcc[c], Hop, outAcc[c], 0, Frame - Hop); Array.Clear(outAcc[c], Frame - Hop, Hop);
                    Array.Copy(inBuf[c], Hop, inBuf[c], 0, Frame - Hop);
                }
                fill = Frame - Hop;
                ultimoCero = regen <= 0f;
            }

            // Desenreda los dos espectros que vienen dentro de una sola FFT. Se apoya en que el espectro de una
            // senal real es simetrico: la mitad de arriba es el reflejo de la de abajo. Cada pareja de bins
            // (k y su reflejo N-k) se lee ENTERA antes de escribir nada, porque las cuatro salidas se calculan
            // con esos mismos dos numeros y se escriben justo encima.
            private void SeparaCanales()
            {
                int n = Frame, h = n >> 1;
                float[] r0 = re, i0 = im, r1 = reB, i1 = imB;
                r1[0] = i0[0]; i1[0] = 0f; i0[0] = 0f;       // el bin 0 y el del medio son su propio reflejo
                r1[h] = i0[h]; i1[h] = 0f; i0[h] = 0f;
                for (int k = 1; k < h; k++)
                {
                    int m = n - k;
                    float ar = r0[k], ai = i0[k], br = r0[m], bi = i0[m];
                    float pr = (ar + br) * 0.5f, pi = (ai - bi) * 0.5f;   // canal par
                    float qr = (ai + bi) * 0.5f, qi = (br - ar) * 0.5f;   // canal impar
                    r0[k] = pr; i0[k] = pi; r0[m] = pr; i0[m] = -pi;
                    r1[k] = qr; i1[k] = qi; r1[m] = qr; i1[m] = -qi;
                }
            }

            // Pone el espectro del otro canal del par en re/im (y viceversa). Son cuatro asignaciones de
            // referencia: asi Regenerate -y todo lo que cuelga de el- trabaja sobre el canal que toca sin
            // enterarse de nada ni copiar un solo numero.
            private void Intercambia() { float[] tr = re, ti = im; re = reB; im = imB; reB = tr; imB = ti; }

            // Cada pareja conserva su medida, sin que el frente gobierne los traseros
            // ni el LFE decida el corte. Con Declip la FFT extra usa PCM ORIGINAL,
            // alineado al retardo real; una de cada cuatro ventanas limita el costo.
            private void PreparaCorteGrupo(int canal, bool pareja)
            {
                int g = canal / 2;
                cutHz = cortesGrupo[g]; cutLoHz = gravesGrupo[g]; corteRecreado = recreadosGrupo[g];
                canalDeteccion = canal; detectPair = pareja;
                if (!firstFrame && !srcEnded && (musicales[canal] || (pareja && musicales[canal + 1])))
                {
                    if (corteOriginal == null) { DetectCliff(); ActualizaCorte(); }
                    else if ((cutHz <= 0 || (ventanasCorte & 3) == 1) &&
                        corteOriginal.CuadroOriginal(inicioOriginal + sourceFrames, canal, originalRe, originalIm, win))
                    {
                        float[] r = re, i = im, rb = reB, ib = imB;
                        re = originalRe; im = originalIm; reB = originalReB; imB = originalImB;
                        Fft(false); if (pareja) SeparaCanales();
                        DetectCliff(); ActualizaCorte(); VentanasOriginales++;
                        originalRe = re; originalIm = im; originalReB = reB; originalImB = imB;
                        re = r; im = i; reB = rb; imB = ib;
                    }
                }
                cortesGrupo[g] = cutHz; gravesGrupo[g] = cutLoHz; recreadosGrupo[g] = corteRecreado;
                detectPair = false;
            }

            private int Hz(float hz) { int b = (int)(hz / binHz); int ny = Frame / 2; return b < 1 ? 1 : (b > ny - 1 ? ny - 1 : b); }

            // Localiza el "acantilado" del codec: la frecuencia MAS ALTA donde todavia hay energia comparable
            // a la banda media (4-8 kHz). Suavizado entre ventanas para que no baile; en silencio no se mide.
            private double DetectionPower(int k)
            {
                double p = musicales[canalDeteccion] ? re[k] * re[k] + im[k] * im[k] : 0.0;
                if (!detectPair) return p;
                int n = musicales[canalDeteccion] ? 1 : 0;
                if (musicales[canalDeteccion + 1]) { p += reB[k] * reB[k] + imB[k] * imB[k]; n++; }
                return n > 0 ? p / n : 0.0;
            }
            private void DetectCliff()
            {
                int b4 = Hz(4000f), b8 = Hz(8000f);
                if (b8 <= b4 + 4) return;
                double refE = 0; for (int b = b4; b < b8; b++) refE += DetectionPower(b);
                refE /= (b8 - b4);
                if (refE < 1e-10) return;                        // silencio: conservar la medida previa
                // ...O CASI: de 4 a 8 kHz no hay musica, solo lo que la ventana derrama de un grave fuerte (un bajo o un
                // bombo solos, o la pausa entre dos golpes de charles). Ese derrame baja muy despacio hasta 20 kHz, asi
                // que en cada pausa el acantilado "subia" hacia los 20 kHz, el corte con el que se recrea se movia y
                // hasta se dejaba de recrear, por creer la cancion completa. En musica de verdad esta banda queda como
                // mucho a unos 40 dB del cuerpo (100 Hz - 4 kHz) y el derrame de un grave solo, a 52-88 dB (medido): mas
                // de 45 dB por debajo no es musica, y se conserva la medida previa.
                int b1 = Hz(100f); double cuerpo = 0;
                for (int b = b1; b < b4; b++) cuerpo += DetectionPower(b);
                if (refE < cuerpo / (b4 - b1) * 3e-5) return;
                float top = Math.Min(20000f, (Frame / 2) * binHz * 0.94f);
                float found = 0f;
                for (float hz = top - 300f; hz >= 9000f; hz -= 300f)
                {
                    int a = Hz(hz), b = Hz(hz + 300f); if (b <= a) b = a + 1;
                    double e = 0; for (int k = a; k < b; k++) e += DetectionPower(k);
                    e /= (b - a);
                    if (e > refE * 0.002) { found = hz + 300f; break; }   // ~-27 dB de la banda media: aqui sigue VIVO
                }
                if (found <= 0f) found = 9000f;                  // nada vivo arriba de 9k: el corte anda por ahi
                float alfa = corteOriginal == null ? .15f : .478f; // 1-.85^4: misma escala de suavizado temporal
                cutHz = (cutHz <= 0f) ? found : (cutHz * (1-alfa) + found * alfa);
                DetectCliffLow(refE);
            }

            // El acantilado de ABAJO. Muchas grabaciones viejas (y los vinilos pasados a digital) no tienen NADA
            // por debajo de 60-80 Hz: ahi el grave profundo no esta en el archivo, no es que la bocina no lo de.
            // Saber donde empieza a haber grave de verdad sirve para decidir cuanta ayuda necesita cada cancion.
            private float cutLoHz;
            private void DetectCliffLow(double refE)
            {
                float found = 0f;
                for (float hz = 25f; hz <= 140f; hz += 5f)
                {
                    int a = Hz(hz), b = Hz(hz + 5f); if (b <= a) b = a + 1;
                    double e = 0; for (int k = a; k < b; k++) e += DetectionPower(k);
                    e /= (b - a);
                    if (e > refE * 0.02) { found = hz; break; }   // aqui ya hay grave de verdad
                }
                if (found <= 0f) found = 140f;                   // nada vivo por debajo de 140: grabacion muy flaca
                float alfa = corteOriginal == null ? .15f : .478f;
                cutLoHz = (cutLoHz <= 0f) ? found : (cutLoHz * (1-alfa) + found * alfa);
            }



            private bool BuildIaFeatures(int cut)
            {
                float n2 = (float)Frame * (float)Frame;
                float fc = cut * binHz;
                if (fc < 1200f) return false;
                PreparaIaFeatureBands(cut);
                double sum = 0;
                for (int i = 0; i < 24; i++)
                {
                    int a = iaFeatureA[i], b = iaFeatureB[i];
                    double e = 0; for (int k = a; k < b; k++) e += re[k] * re[k] + im[k] * im[k];
                    iaIn[i] = (float)Math.Log10(e / ((b - a) * n2) + 1e-12);
                    sum += iaIn[i];
                }
                float mean = (float)(sum / 24.0);
                for (int i = 0; i < 24; i++) iaIn[i] -= mean;
                iaIn[24] = mean;
                iaIn[25] = fc / 20000f;
                double am = 0, gm = 0;
                for (int i = 16; i < 24; i++) { am += Math.Pow(10.0, iaIn[i] + mean); gm += iaIn[i] + mean; }
                iaIn[26] = (float)(gm / 8.0 - Math.Log10(am / 8.0 + 1e-12));
                iaIn[27] = 0f;
                return true;
            }



            

            private void PreparaIaFeatureBands(int cut)
            {
                if (iaFeatureCut == cut) return;
                float fc = cut * binHz;
                for (int i = 0; i < 24; i++)
                {
                    int a = Hz(200f * (float)Math.Pow(fc / 200f, i / 24.0));
                    int b = Hz(200f * (float)Math.Pow(fc / 200f, (i + 1) / 24.0));
                    if (b <= a) b = a + 1;
                    iaFeatureA[i] = a; iaFeatureB[i] = b;
                }
                iaFeatureCut = cut;
            }
            private void PreparaIaTargetBands(int cut, int topBin)
            {
                if (iaTargetCut == cut && iaTargetTop == topBin) return;
                float fc = cut * binHz, top = topBin * binHz;
                for (int j = 0; j < 12; j++)
                {
                    int a = Hz(fc * (float)Math.Pow(top / fc, j / 12.0));
                    int b = Hz(fc * (float)Math.Pow(top / fc, (j + 1) / 12.0));
                    if (a < cut) a = cut; if (b > topBin) b = topBin;
                    iaTargetA[j] = a; iaTargetB[j] = b;
                }
                iaTargetCut = cut; iaTargetTop = topBin;
            }

            private bool RegenNeural(int cut, int topBin)
            {
                if (!BuildIaFeatures(cut)) return false;
                if (!(Red ?? NeuralEnv).Predict(iaIn, iaOut)) return false;
                for (int t = cut; t <= topBin; t++) ganHoy[t - cut] = 0f;
                float n2 = (float)Frame * (float)Frame;
                int sa = Hz(cut * binHz * 0.92f); if (sa >= cut) sa = cut - 1;
                double seamE = 0; for (int k = sa; k < cut; k++) seamE += re[k] * re[k] + im[k] * im[k];
                seamE = seamE / ((cut - sa) * n2) + 1e-12;
                PreparaIaTargetBands(cut, topBin);
                for (int j = 0; j < 12; j++)
                {
                    int a = iaTargetA[j], b = iaTargetB[j]; if (b <= a) continue;
                    double mapped = 0;                               // energia de la fuente transpuesta en esta sub-banda (sin picos: ver FuenteSuave)
                    for (int t = a; t < b; t++) mapped += fuenteSuave[t >> 1];
                    mapped = mapped / ((b - a) * n2);
                    if (mapped < 1e-14) continue;                    // fuente muda: nada que transponer aqui
                    float yj = iaOut[j]; if (yj > 0.1f) yj = 0.1f; else if (yj < -4f) yj = -4f;   // tope: nunca mas fuerte que la costura (+~1dB)
                    float sc = (float)Math.Sqrt(seamE * Math.Pow(10.0, yj) / mapped) * 0.9f;      // 0.9 = colchon
                    if (sc > 3f) sc = 3f;
                    for (int t = a; t < b; t++) ganHoy[t - cut] = sc;   // la envolvente; la copia la hace CopiaPorBloques
                }
                return true;
            }

            // RE-CREA LA BANDA POR ENCIMA DEL ACANTILADO.
            //
            // CUANTO suena cada parte de la banda nueva (la envolvente) se decide como siempre: la IA propia si hay
            // modelo cargado (rocola_ia.bin) y, si no, la curva clasica (costura empatada y ~-9 dB por octava), las
            // dos sobre lo que daria copiar la octava de abajo. DE DONDE sale el detalle fino, en cambio, ya no es de
            // ahi (23 de septiembre de 2026):
            //
            // Antes cada bin copiaba al de la mitad de su frecuencia (t <- t/2). Dos bins vecinos salian de la MISMA
            // fuente desplazada distinto, y la fase no seguia de una ventana a la siguiente: un tono estable recreado
            // salia con bandas laterales casi tan fuertes como el (medido con un tono de 7 kHz: solo el 54 % de su
            // energia limpia con ventanas de 2048 y el 11 % con las de 4096). Eso se oye como aspereza o tremolo en
            // los agudos recreados de voces, cuerdas y sintetizadores.
            //
            // Ahora se copia por BLOQUES, como el SBR de los codecs AAC: el bloque de la octava bajo el corte sube
            // entero con un desplazamiento CONSTANTE y, como las ventanas avanzan media ventana, un desplazamiento
            // impar cambia de signo en las ventanas impares para que la fase siga de una a otra. Un tono estable sale
            // como UN tono estable. Cada sub-banda (de ~500 Hz, sin cruzar los bordes de las bandas de la IA ni los de
            // los bloques) se escala a la energia que le pide la envolvente, medida sobre cuanto suena la fuente por
            // ahi sin contar picos ni huecos sueltos (ver FuenteSuave), y a la potencia a la que sonaba la copia de
            // antes cuando funcionaba (ver PotenciaDeAntes). Las dos energias de esa PROPORCION son de la misma ventana
            // y se suavizan entre ventanas (~50 ms): quita el temblor de medir con pocos bins sin retrasar un golpe de
            // platillo, que llega en la misma ventana a los dos lados de la proporcion.
            private void Regenerate()
            {
                if (cutHz <= 0f || cutHz >= 19300f) return;      // sin medida aun, o archivo ya completo
                if (regen <= 0f) return;                          // apagandose y ya en cero: la ventana sale tal cual
                int ny = Frame / 2;
                int cut = corteRecreado > 0 ? corteRecreado : (int)(cutHz / binHz);
                if (cut < 64) return;                             // corte absurdamente bajo: no inventar tanto
                mejoro = true;                                    // desde aqui SI se reconstruye: el letrero queda toda la cancion
                int topBin = Math.Min((int)(Math.Min(20500f, ny * binHz * 0.95f) / binHz), Math.Min(2 * cut - 2, ny - 4));
                if (topBin <= cut + 4) return;
                FuenteSuave(cut);                                 // cuanto suena la fuente, sin picos ni huecos: lo usan las dos de abajo
                // La envolvente: la IA propia (aprendida) o, si no hay modelo o no puede, la curva clasica.
                if (!((Red ?? NeuralEnv).Loaded && RegenNeural(cut, topBin)) && !EnvolventeClasica(cut, topBin)) return;
                CopiaPorBloques(cut, topBin);
            }

            // LA CURVA CLASICA, bin a bin: la costura empatada con colchon (que NUNCA suba) y ~-9 dB por octava, sobre la
            // copia de una octava abajo. false = no hay nada que recrear (la fuente esta muda).
            //
            // LA COSTURA se mide justo DEBAJO de donde empieza la caida del filtro del codec (el mismo borde de arriba
            // del bloque que se copia), no en los 6 bins de justo debajo del corte. El corte medido puede quedar hasta
            // 300 Hz por encima de donde acaba la musica y ahi esos 6 bins estan casi vacios: con el corte ya quieto, la
            // cancion entera se quedaba sin agudos recreados (medido con tono y ruido cortados en 12 kHz: 31 dB menos
            // que antes). Antes lo tapaba, a medias, el propio baile del corte: en algunas ventanas caia mas abajo y los
            // agudos aparecian de golpe (en 4096, una de cada diez ventanas sonaba ~25 dB mas fuerte que las demas).
            private bool EnvolventeClasica(int cut, int topBin)
            {
                double mBelow = 0, mSrc = 0;                      // costura: magnitud media bajo la caida del codec vs banda fuente
                int s1 = cut - Math.Max(2, cut / 32);
                for (int k = s1 - 6; k < s1; k++) mBelow += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                int s0 = cut >> 1;
                for (int k = s0; k < s0 + 6; k++) mSrc += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                if (mSrc < 1e-9) return false;
                float g = (float)(mBelow / mSrc) * 0.85f;         // empatada con colchon (que la costura NUNCA suba)
                if (g > 1.1f) g = 1.1f;
                if (g <= 1e-6f) return false;
                float step = (float)Math.Pow(10.0, -0.45 / cut);  // ~-9 dB por octava (una octava = cut bins)
                float f = g;
                for (int t = cut; t <= topBin; t++) { ganHoy[t - cut] = f; f *= step; }
                return true;
            }

            // EL CORTE CON EL QUE SE RECREA. El medido baila unos bins de una ventana a otra (en un pasaje callado la
            // energia de arriba baja y parece que corta antes), y cada cambio del corte usado cambia los bloques y su
            // fase. Solo se mueve si el medido se aparta mas de un 2 %.
            private void ActualizaCorte()
            {
                if (cutHz <= 0f) return;
                int medido = (int)(cutHz / binHz);
                if (corteRecreado <= 0 || Math.Abs(medido - corteRecreado) > Math.Max(2, corteRecreado / 50)) corteRecreado = medido;
            }

            // CUANTO SUENA LA FUENTE POR CADA ZONA, SIN PICOS NI HUECOS SUELTOS, en la octava bajo el corte, que es de
            // donde leen la envolvente y los bloques. La proporcion de cada sub-banda compara dos zonas DISTINTAS de la
            // fuente (la de la copia de antes, una octava abajo, y la del bloque), asi que no puede mirar los picos: un
            // tono de 7 kHz, que la copia de antes subia a 14 kHz y la de bloques sube a 13,1, hacia que la escala
            // apagara el tono recreado e inflara hasta el tope el ruido de 14 kHz (con la energia bin a bin) o movia el
            // ruido recreado de alrededor hasta 23 dB (con la energia promediada en media octava). Aqui la octava se
            // parte en 12 bandas de 1/12 de octava, de cada banda se toma su energia media y de cada tres vecinas la
            // del medio (mediana): un tono o un hueco que caiga en una banda no cuenta. Entre los centros de las bandas
            // se interpola. Medido con ese tono (pruebas/dsee.cs, seccion 11): el ruido recreado de alrededor queda a
            // menos de 1 dB del que sale sin el tono. Los bordes van en enteros (2^(i/12) * 1024, redondeado) para que
            // salgan iguales en cualquier procesador.
            private static readonly int[] DozavoDeOctava = { 1024, 1085, 1149, 1218, 1290, 1367, 1448, 1534, 1625, 1722, 1825, 1933, 2048 };
            private void FuenteSuave(int cut)
            {
                // Con cut >= 64 (Regenerate no recrea por debajo) la banda mas estrecha tiene 2 bins: ninguna queda vacia.
                int k0 = cut >> 1;
                int[] e = bordeFuente; double[] m = bandaFuente; float[] v = medFuente, cc = cenFuente;
                for (int i = 0; i < 12; i++) e[i] = (k0 * DozavoDeOctava[i] + 512) >> 10;
                e[12] = cut;
                for (int i = 0; i < 12; i++)
                {
                    double s = 0; for (int k = e[i]; k < e[i + 1]; k++) s += re[k] * re[k] + im[k] * im[k];
                    m[i] = s / (e[i + 1] - e[i]);
                    cc[i] = (e[i] + e[i + 1] - 1) * 0.5f;
                }
                for (int i = 0; i < 12; i++) v[i] = Mediana3(m, i);
                float[] fs = fuenteSuave;
                int j = 0;
                for (int k = k0; k < cut; k++)
                {
                    while (j < 12 && cc[j] <= k) j++;             // j = la primera banda con el centro a la derecha de k
                    fs[k] = j == 0 ? v[0] : (j == 12 ? v[11] : v[j - 1] + (v[j] - v[j - 1]) * ((k - cc[j - 1]) / (cc[j] - cc[j - 1])));
                }
            }
            // La mediana de la banda i y sus dos vecinas (en los extremos, las tres del extremo).
            private static float Mediana3(double[] m, int i)
            {
                int j = i - 1; if (j < 0) j = 0; else if (j > 9) j = 9;
                double a = m[j], b = m[j + 1], c = m[j + 2];
                double med = a > b ? (b > c ? b : (a > c ? c : a)) : (a > c ? a : (b > c ? c : b));
                return (float)med;
            }

            // LA COPIA POR BLOQUES, con la envolvente que dejo ganHoy (ver Regenerate).
            private void CopiaPorBloques(int cut, int topBin)
            {
                int srcLo = cut >> 1;                             // el bloque fuente: desde una octava abajo...
                int srcHi = cut - Math.Max(2, cut / 32);          // ...hasta casi el corte (sin la caida del filtro del codec)
                int largo = srcHi - srcLo;                        // lo que sube cada bloque sobre el anterior
                if (largo < 8) return;
                ArmaSubbandas(cut, topBin, largo);
                int c = canalRegen;
                if (numSuave == null || numSuave.Length < ch) { numSuave = new double[ch][]; denSuave = new double[ch][]; suaveListo = new bool[ch]; }
                double[] ns = numSuave[c], ds = denSuave[c];
                if (ns == null) { ns = numSuave[c] = new double[bordes.Length]; ds = denSuave[c] = new double[bordes.Length]; }
                if (alfaSuave <= 0f) alfaSuave = (float)(1.0 - Math.Exp(-(double)Hop / (0.05 * rate)));
                bool previa = suaveListo[c];
                int nsb = nBordes - 1;
                // 1) La escala de cada sub-banda: la energia que le pide la envolvente sobre la copia de antes, entre la
                //    del bloque (las dos sobre FuenteSuave), al nivel al que sonaba la copia de antes. Se suavizan las DOS
                //    ENERGIAS por separado, no la escala: asi cuentan las ventanas con musica y no las casi mudas, cuya
                //    escala es ruido de medir. Suavizando la escala, una mezcla con charles en corcheas pasada a MP3 de
                //    128 kbps llevaba la escala de los silencios (hasta el tope) al golpe siguiente y sus agudos recreados
                //    salian hasta 40 dB mas fuertes que antes (medido). Y como son energias, el promedio es de POTENCIA,
                //    como el que hacia la copia de antes al sumar ventanas sueltas (suavizar la amplitud quedaba ~1 dB
                //    por debajo cuando la costura baila de una ventana a otra, medido).
                for (int k = 0; k < nsb; k++)
                {
                    int a = bordes[k], b = bordes[k + 1];
                    int d = cut + ((a - cut) / largo) * largo - srcLo;   // desplazamiento de su bloque
                    double eHoy = 0, eBlk = 0;
                    for (int t = a; t < b; t++)
                    {
                        float gh = ganHoy[t - cut];
                        eHoy += fuenteSuave[t >> 1] * gh * gh;
                        eBlk += fuenteSuave[t - d];
                    }
                    if (previa) { ns[k] += (eHoy - ns[k]) * alfaSuave; ds[k] += (eBlk - ds[k]) * alfaSuave; }
                    else { ns[k] = eHoy; ds[k] = eBlk; }
                    float pk = ds[k] > 1e-24 ? (float)(ns[k] / ds[k]) * PotenciaDeAntes : 0f;
                    if (pk > 36f) pk = 36f;                       // (x6 en amplitud) un hueco en la fuente no se infla sin limite
                    ampSub[k] = (float)Math.Sqrt(pk);
                }
                suaveListo[c] = true;
                // 2) La copia, bin a bin: la escala interpolada entre los centros de las sub-bandas de SU bloque, y el
                //    signo que mantiene la fase de una ventana a la siguiente.
                for (int k = 0; k < nsb; k++)
                {
                    int a = bordes[k], b = bordes[k + 1];
                    int r = (a - cut) / largo, d = cut + r * largo - srcLo;
                    float signo = ((d & 1) == 1 && ventanaImpar) ? -regen : regen;   // con el fundido de encendido (1 = tal cual)
                    float centro = (a + b - 1) * 0.5f;
                    bool conAnt = k > 0 && (bordes[k - 1] - cut) / largo == r;        // la sub-banda de antes es del mismo bloque
                    bool conSig = k + 1 < nsb && (bordes[k + 1] - cut) / largo == r;  // y la de despues
                    float cAnt = conAnt ? (bordes[k - 1] + a - 1) * 0.5f : 0f;
                    float cSig = conSig ? (b + bordes[k + 2] - 1) * 0.5f : 0f;
                    for (int t = a; t < b; t++)
                    {
                        float g = ampSub[k];
                        if (t < centro && conAnt) g = ampSub[k - 1] + (ampSub[k] - ampSub[k - 1]) * ((t - cAnt) / (centro - cAnt));
                        else if (t > centro && conSig) g = ampSub[k] + (ampSub[k + 1] - ampSub[k]) * ((t - centro) / (cSig - centro));
                        float fr = g * signo;
                        int s = t - d;                            // fuente: siempre bajo el corte (contenido real)
                        re[t] += re[s] * fr; im[t] += im[s] * fr;
                        int m = Frame - t;                        // espejo (frecuencias negativas): la señal sigue siendo real
                        re[m] = re[t]; im[m] = -im[t];
                    }
                }
            }

            // Las sub-bandas de la copia: bordes en el corte, al empezar cada bloque, en las bandas de la IA (las mismas
            // de RegenNeural y MakePair) y, si quedan anchas, partidas en trozos de ~500 Hz. Se rehacen solo si cambia
            // el corte, y entonces se olvida el suavizado, que era de otras sub-bandas.
            private void ArmaSubbandas(int cut, int topBin, int largo)
            {
                if (cut == bordesCorte && topBin == bordesTop && largo == bordesLargo) return;
                bordesCorte = cut; bordesTop = topBin; bordesLargo = largo;
                if (suaveListo != null) for (int c = 0; c < suaveListo.Length; c++) suaveListo[c] = false;
                if (marcas == null) marcas = new int[64];
                int n = 0, fin = topBin + 1;
                marcas[n++] = cut; marcas[n++] = fin;
                for (int p = cut + largo; p < fin && n < marcas.Length; p += largo) marcas[n++] = p;
                float fc = cut * binHz, top = topBin * binHz;
                for (int j = 1; j < 12 && n < marcas.Length; j++)
                {
                    int e = Hz(fc * (float)Math.Pow(top / fc, j / 12.0));
                    if (e > cut && e < fin) marcas[n++] = e;
                }
                Array.Sort(marcas, 0, n);
                int w = Math.Max(4, (int)(500f / binHz));
                nBordes = 0;
                for (int i = 0; i < n; i++)
                {
                    int e = marcas[i];
                    if (nBordes > 0 && e <= bordes[nBordes - 1]) continue;   // repetido
                    if (nBordes > 0)
                    {
                        int prev = bordes[nBordes - 1], ancho = e - prev, partes = (ancho + w - 1) / w;
                        for (int p = 1; p < partes && nBordes < bordes.Length - 1; p++) bordes[nBordes++] = prev + ancho * p / partes;
                    }
                    if (nBordes < bordes.Length) bordes[nBordes++] = e;
                }
            }

            // FFT radix-2 iterativa en sitio (tablas precalculadas). inverse=true incluye la escala 1/N.
            //
            // Es lo mas caro del reconstructor, y el bucle de mas adentro se recorre unas once mil veces por
            // cada FFT. Dos detalles que ahi dentro cuestan:
            //   - el signo del seno depende de si es ida o vuelta, y se estaba decidiendo EN CADA VUELTA del
            //     bucle. Ahora hay una segunda tabla con el seno ya cambiado de signo y se elige UNA vez.
            //   - los arreglos se leian del objeto en cada acceso. Sacados a variables locales, el traductor
            //     puede dejarlos en registros y ademas se ahorra comprobar sus limites tantas veces.
            private void Fft(bool inverse)
            {
                int n = Frame;
                float[] r = re, m = im;
                int[] br = bitrev;
                for (int i = 0; i < n; i++)
                {
                    int j = br[i];
                    if (j > i) { float t = r[i]; r[i] = r[j]; r[j] = t; t = m[i]; m[i] = m[j]; m[j] = t; }
                }
                float[] tc = twCos, ts = inverse ? twSin : twSinNeg;
                for (int size = 2; size <= n; size <<= 1)
                {
                    int half = size >> 1, tstep = n / size;
                    for (int i = 0; i < n; i += size)
                    {
                        int k = 0;
                        for (int j = i; j < i + half; j++, k += tstep)
                        {
                            float wr = tc[k], wi = ts[k];
                            int b = j + half;
                            float rb = r[b], mb = m[b];
                            float tr = rb * wr - mb * wi;
                            float ti = rb * wi + mb * wr;
                            r[b] = r[j] - tr; m[b] = m[j] - ti;
                            r[j] += tr; m[j] += ti;
                        }
                    }
                }
                if (inverse)
                {
                    float sc = 1f / n;
                    for (int i = 0; i < n; i++) { r[i] *= sc; m[i] *= sc; }
                }
            }
        }
    }
}

// MAS ETAPAS DE LA CADENA, copiadas tal cual de Rocola/Form1.Sonido.cs: el bajo psicoacustico (DeepBassProvider), el des-recorte
// (DeclipProvider), el fundido de salida y de entrada (FadeProvider) y el limitador viejo (LookaheadLimiter, el del convertidor).
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // DES-RECORTE (declipper): muchos MP3 de la "guerra del volumen" vienen con los picos APLASTADOS
        // de fabrica (el maestro se grabo recortado): se oye aspero en las partes fuertes. Esto detecta los
        // "techos planos" (varias muestras seguidas pegadas al maximo digital) y RECONSTRUYE el pico redondo
        // que deberia haber ahi, continuando la trayectoria de la onda (arco parabolico) hasta aterrizar en
        // la salida del techo. Solo actua en recortes al TOPE digital (~0.985+), el caso tipico; el pico
        // reconstruido puede pasar de 1.0 y eso esta bien: el limitador del final lo baja LIMPIO. Si la senal
        // no viene recortada, pasa BIT-EXACTA (solo con ~1.5 ms de retardo). Por canal, sin tocar el estereo.
        // BAJO PSICOACUSTICO. Las bocinas de un local no bajan de ~80 Hz: el grave profundo de la musica
        // simplemente NO SALE por ellas. Subirlo con EQ no sirve -solo gasta amplificador y hace toser al cono-,
        // asi que en vez de eso se generan los ARMONICOS de esa nota grave (el doble y el triple de su
        // frecuencia), que la bocina SI reproduce, y el oido reconstruye solo la nota que falta. Es un efecto
        // conocido: la "fundamental ausente". Era la mitad que le faltaba a la cadena: habia realce de agudos
        // (DSEE) y nada abajo.
        // El grave se toma de la MEZCLA MONO -en casi toda la musica el bajo va al centro- y los mismos
        // armonicos se suman a los dos canales: el grave sale centrado y sin rarezas de fase.
        // Con Amount = 0 devuelve el audio tal cual (costo cero), asi que se deja SIEMPRE en la cadena y se
        // mueve en vivo desde Ctrl+F3 para comparar A/B sin cambiar de cancion.
        // CAMBIOS EN VIVO SIN ESCALON: antes la mezcla nueva entraba de golpe en el siguiente bloque (un escalon
        // del grave cada vez que se tocaba la perilla) y, al apagarlo, sus filtros se quedaban congelados: al
        // volver a encenderlo arrancaba con el estado de hacia minutos. Ahora la mezcla va en rampa de 20 ms
        // hasta el valor pedido exacto, y una vez usado en la cancion sus filtros siguen la musica aunque este en
        // cero (sin sumar nada: sale intacto). Si la cancion EMPIEZA en cero, ni se calcula: pase directo. Al
        // empezar la cancion la mezcla es la pedida desde la primera muestra (sin fundido que debilite el golpe).
        // El grave de un cuadro se suma al cuadro SIGUIENTE (1 muestra, 23 us: a 320 Hz son 2,6 grados): asi un
        // cuadro que llega partido entre dos lecturas se completa sin tocar lo ya entregado, y da igual como
        // lleguen las lecturas; antes suponia cuadros enteros (una muestra suelta se quedaba sin su grave).
        // DISCO CONTINUO (24-10-2026): la pista siguiente del mismo disco empezaba con los filtros en cero, y en el cambio el grave
        // sintetico daba un escalon (medido en la cadena entera: 0,003 con la musica a 0,3 de pico). Ahora sigue con los de la anterior
        // (Sigue, desde SigueA) y su cebado pasa tal cual: el grave que la anterior dejo pendiente cae en la primera muestra de musica.
        private sealed class DeepBassProvider : NAudio.Wave.ISampleProvider
        {
            private readonly bool[] musicales;
            private readonly int nMusicales, primeroMusical;
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            public volatile float Amount;      // 0..1 (perilla de Ctrl+F3)
            private float lp1, lp2, lp3;       // aislar el grave profundo (3 polos = 18 dB/oct)
            private float env;                 // energia del grave (ataque rapido, caida lenta)
            private float hpS, lpS;            // banda por donde salen los armonicos
            private readonly float aLow, aHp, aLp, aAtk, aRel;
            private readonly int rampa;        // cuadros de la rampa de la mezcla (20 ms)
            private float mix, objetivo, paso; private int quedan;   // mezcla actual, pedida y su rampa
            private bool empezo, vivo;         // empezo: ya leyo; vivo: sus filtros siguen la musica
            private int fase; private float suma, hSal;   // cuadro que va llegando: canal, suma mono y lo que se le suma
            private readonly float[] cuadro;   // para completar un cuadro que llego partido (se reserva al construir)
            private int limpia;                // cuadros desde la ultima limpieza de restos diminutos
            private volatile DeepBassProvider anterior;   // la pista anterior del mismo disco, de la que seguir
            internal int Cebado;               // el cebado de delante (lo pone ArmaCadena): tras heredar pasa tal cual
            private int cebadoPorPasar;
            internal void Sigue(DeepBassProvider previa) { if (previa != null && !ReferenceEquals(previa, this)) anterior = previa; }
            private void Hereda()
            {
                DeepBassProvider a = anterior; anterior = null;
                if (a == null || !a.empezo || a.ch != ch || a.aLow != aLow || a.fase != 0) return;
                lp1 = a.lp1; lp2 = a.lp2; lp3 = a.lp3; env = a.env; hpS = a.hpS; lpS = a.lpS; hSal = a.hSal;
                if (a.vivo) vivo = true;
                cebadoPorPasar = Math.Max(0, Cebado) * ch;
            }
            public DeepBassProvider(NAudio.Wave.ISampleProvider s, float amount)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); Amount = amount;
                musicales = FormatoAudio.Musicales(s.WaveFormat);
                primeroMusical = -1;
                if (ch <= 2 || FormatoAudio.Mascara(s.WaveFormat) != 0)
                    for (int c = 0; c < ch; c++) if (musicales[c]) { nMusicales++; if (primeroMusical < 0) primeroMusical = c; }
                int fs = s.WaveFormat.SampleRate; if (fs < 8000) fs = 44100;
                aLow = Coef(100f, fs);    // por debajo de aqui esta lo que la bocina no puede dar
                aHp = Coef(95f, fs);      // los armonicos salen de ~95 Hz...
                aLp = Coef(320f, fs);     // ...a ~320 Hz, donde cualquier bocina responde bien
                aAtk = Coef(1f / (2f * (float)Math.PI * 0.004f), fs);   // 4 ms: agarra el golpe del bombo
                aRel = Coef(1f / (2f * (float)Math.PI * 0.150f), fs);   // 150 ms: no bombea
                rampa = Math.Max(1, (int)(fs * .020));
                cuadro = new float[ch];
            }
            private static float Coef(float hz, int fs)
            {
                float a = (float)(1.0 - Math.Exp(-2.0 * Math.PI * hz / fs));
                return a < 0f ? 0f : (a > 1f ? 1f : a);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0 || nMusicales == 0) return n;   // layout desconocido: no mezclar canales
                float amt = Amount, obj;
                if (float.IsNaN(amt) || float.IsInfinity(amt)) obj = objetivo;   // un valor roto no mueve nada
                else obj = amt > 0.001f ? amt * 0.85f : 0f;
                if (!empezo) { empezo = true; mix = objetivo = obj; Hereda(); }  // al empezar: la pedida, sin rampa (y lo de la anterior del disco)
                else if (obj != objetivo) { objetivo = obj; quedan = rampa; paso = (objetivo - mix) / rampa; }
                int i = offset, end = offset + n;
                if (cebadoPorPasar > 0)
                {   // el cebado de la siguiente del mismo disco: tal cual, y lo pendiente de la anterior espera a la musica
                    int salta = Math.Min(n, cebadoPorPasar); cebadoPorPasar -= salta; i += salta;
                    if (i == end) return n;
                }
                if (!vivo)
                {
                    if (mix == 0f && quedan == 0) return n;   // nunca encendido en esta cancion: pase directo, sin coste
                    vivo = true;
                }
                // Un cuadro que quedo a medias en la lectura anterior (lecturas que parten cuadros; en la cadena
                // normal no pasa): se completa, y su suma pasa por la misma cuenta que los cuadros enteros.
                if (fase != 0)
                {
                    int k = Math.Min(ch - fase, n);
                    for (int c = 0; c < k; c++) { float x = buffer[i + c]; if (musicales[fase + c]) { suma = (float)(suma + x); buffer[i + c] = x + hSal; } }
                    i += k; fase += k;
                    if (fase < ch) return n;
                    for (int c = 0; c < ch; c++) cuadro[c] = 0f; cuadro[primeroMusical] = suma;
                    fase = 0; suma = 0f;
                    Cuadros(cuadro, 0, 1);
                }
                int enteros = (end - i) / ch;
                Cuadros(buffer, i, enteros);
                i += enteros * ch;
                // Lo que sobra de un cuadro partido: recibe el grave del cuadro anterior y espera a la lectura siguiente.
                for (; i < end; i++) { float x = buffer[i]; if (musicales[fase]) { suma = (float)(suma + x); buffer[i] = x + hSal; } fase++; }
                return n;
            }
            // Cuadros enteros: a cada canal se le suma el grave del cuadro ANTERIOR (0 exacto si esta apagado) y con la
            // suma de este se calcula el del siguiente. Los redondeos a float son explicitos para que la cuenta sea la
            // misma se lea como se lea, tambien en el FPU x87 de XP (que si no guarda las variables con mas precision).
            // Por cuadros y no muestra a muestra: en Mono, la misma cuenta muestra a muestra costaba el doble (33 ns
            // por muestra frente a 16, lo mismo que antes de esta etapa).
            private void Cuadros(float[] b, int i, int cuadros)
            {
                float mx = mix, hs = hSal; int q = quedan;
                for (int f = 0; f < cuadros; f++, i += ch)
                {
                    float m = b[i + primeroMusical];
                    for (int c = primeroMusical + 1; c < ch; c++) if (musicales[c]) m = (float)(m + b[i + c]);
                    if (nMusicales > 1) m /= nMusicales;
                    for (int c = 0; c < ch; c++) if (musicales[c]) b[i + c] += hs;
                    // 1) aislar el grave que la bocina NO puede reproducir
                    lp1 += (m - lp1) * aLow;
                    lp2 += (lp1 - lp2) * aLow;
                    lp3 += (lp2 - lp3) * aLow;
                    float low = lp3;
                    // 2) seguir su energia: asi los armonicos respiran con el bajo y no con el resto
                    float a = low < 0f ? -low : low;
                    env += (a - env) * (a > env ? aAtk : aRel);
                    float h = 0f;
                    if (env > 1e-4f)   // en silencio no se inventa nada (no amplificar el ruido de fondo)
                    {
                        // 3) NORMALIZAR antes de generar: asi el timbre del bajo no cambia con el volumen.
                        //    x^2 da el 2o armonico y x^3 el 3o. Hacen falta DOS armonicos SEGUIDOS para que
                        //    el oido reconstruya la nota; solo con el 2o se oiria una octava mas arriba.
                        float nz = low / env;
                        if (nz > 1.5f) nz = 1.5f; else if (nz < -1.5f) nz = -1.5f;
                        float n2 = nz * nz;
                        h = ((n2 - 0.5f) * 0.8f + n2 * nz * 0.6f) * env;
                    }
                    // 4) dejar SOLO la banda que la bocina si da (el paso-alto tambien quita el continuo del x^2)
                    hpS += (h - hpS) * aHp; h -= hpS;
                    lpS += (h - lpS) * aLp;
                    if (q > 0) { mx = (float)(mx + paso); if (--q == 0) mx = objetivo; }
                    hs = (float)(lpS * mx);                   // el mismo grave a todos los canales: centrado
                    if (++limpia == Desnormales.Cada)   // en el silencio, los restos diminutos a cero (Desnormales.cs)
                    {
                        limpia = 0;
                        lp1 = Desnormales.Limpia(lp1); lp2 = Desnormales.Limpia(lp2); lp3 = Desnormales.Limpia(lp3);
                        env = Desnormales.Limpia(env); hpS = Desnormales.Limpia(hpS); lpS = Desnormales.Limpia(lpS);
                    }
                }
                hSal = hs; mix = mx; quedan = q;
            }
        }
        private sealed class DeclipProvider : NAudio.Wave.ISampleProvider
        {
            private const float Thresh = 0.985f;   // umbral del techo digital
            private const int MinRun = 3;          // menos de 3 muestras al tope = pico sano (no se toca)
            private const int MaxRun = 64;         // mas de ~1.5 ms plano = a proposito (sintetizador); no se toca
            private const int D = MaxRun + 4;      // retardo: el techo entero cabe dentro antes de salir
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            private readonly float[] delay;        // circular: D cuadros * canales
            private int dpos;                      // cuadro de escritura actual
            private int primed;                    // cuadros cargados (mientras se llena sale silencio: ~1.5 ms una vez)
            private bool srcEnded, hadInput;
            private int drainSamples;              // muestras que quedan por vaciar del retardo al acabar (D cuadros enteros)
            private int canal;                     // canal de la muestra siguiente dentro del cuadro 'dpos' (0 = cuadro entero)
            private readonly int[] runLen;         // por canal: muestras seguidas pegadas al techo
            private readonly int[] runSign;        // por canal: signo del techo actual (+1/-1)
            private readonly float[] preVal;       // por canal: la muestra justo ANTES del techo
            private readonly float[] preSlope;     // por canal: pendiente de entrada al techo
            private readonly float[] last1, last2; // por canal: las dos muestras anteriores (para la pendiente)
            private readonly bool[] tooLong;       // por canal: el techo se paso de MaxRun (abandonar)
            public DeclipProvider(NAudio.Wave.ISampleProvider s)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                delay = new float[D * ch];
                runLen = new int[ch]; runSign = new int[ch]; preVal = new float[ch]; preSlope = new float[ch];
                last1 = new float[ch]; last2 = new float[ch]; tooLong = new bool[ch];
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int DelayFrames { get { return D; } }
            // Al acabar sale lo que queda en el retardo, muestra a muestra desde donde va (24-10-2026): antes se vaciaba por cuadros
            // enteros, y una lectura de menos de un cuadro devolvia 0 -que quiere decir "se acabo"- con 68 cuadros aun dentro. Son
            // copias: con cuadros enteros sale exactamente lo mismo que antes.
            private int Drain(float[] buffer, int offset, int count)
            {
                int k = Math.Min(drainSamples, count), w = dpos * ch + canal, largo = delay.Length;
                for (int i = 0; i < k; i++) { buffer[offset + i] = delay[w]; delay[w] = 0f; if (++w == largo) w = 0; }
                drainSamples -= k; dpos = w / ch; canal = w % ch;
                return k;
            }
            // S4 ("Escuchar sin mejoras"): con Pasa no se repara nada; sale lo grabado con su mismo retraso. Y lo que se
            // reparo, para que Ctrl+F3 lo ensene (S5).
            internal volatile bool Pasa;
            internal long Reparados;
            private void Repair(int c, float landing)
            {
                int L = runLen[c];
                if (L < MinRun || L > MaxRun || tooLong[c] || Pasa) return;
                Reparados++;
                // arco: p(i) = preVal + preSlope*i + a2*i^2, con p(L+1) = landing (aterriza en la salida del techo)
                float lp1 = L + 1f;
                float a2 = (landing - preVal[c] - preSlope[c] * lp1) / (lp1 * lp1);
                int sgn = runSign[c];
                for (int i = 1; i <= L; i++)
                {
                    int idx = dpos - L + (i - 1); if (idx < 0) idx += D;   // posicion del i-esimo cuadro del techo
                    int q = idx * ch + c;
                    float orig = delay[q];
                    float p = preVal[c] + preSlope[c] * i + a2 * i * i;
                    if (p > 4f) p = 4f; else if (p < -4f) p = -4f;         // tope de cordura del arco
                    // nunca por DEBAJO del techo grabado: en el peor caso queda igual que venia (cero riesgo)
                    if (sgn > 0) { if (p > orig) delay[q] = p; }
                    else { if (p < orig) delay[q] = p; }
                }
            }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (srcEnded) return Drain(buffer, offset, count);
                int n = src.Read(buffer, offset, count);
                if (n <= 0)
                {
                    srcEnded = true;
                    drainSamples = hadInput ? D * ch : 0;   // vacia TODO el retardo; no corta las ultimas muestras
                    return Drain(buffer, offset, count);
                }
                // UN CUADRO PARTIDO (24-10-2026). Si una lectura dejaba un cuadro a medias, sus muestras sueltas salian sin retardo ni
                // reparar y las lecturas siguientes iban con los canales corridos. Entonces se sigue muestra a muestra, sabiendo el
                // canal de cada una (MuestraAMuestra), con las mismas cuentas; con cuadros enteros, el camino de siempre.
                if (canal != 0 || n % ch != 0) { MuestraAMuestra(buffer, offset, n); return n; }
                int frames = n / ch;
                if (frames > 0) hadInput = true;
                for (int f = 0; f < frames; f++)
                {
                    int bi = offset + f * ch, di = dpos * ch;
                    for (int c = 0; c < ch; c++)
                    {
                        float x = buffer[bi + c];
                        float ax = x < 0f ? -x : x;
                        int sgn = x >= 0f ? 1 : -1;
                        if (ax >= Thresh)
                        {
                            if (runLen[c] > 0 && sgn != runSign[c])   // salto de techo + a techo - (onda cuadrada): cerrar y abrir
                            { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                            if (runLen[c] == 0)
                            { runSign[c] = sgn; preVal[c] = last1[c]; preSlope[c] = last1[c] - last2[c]; }
                            runLen[c]++;
                            if (runLen[c] > MaxRun) tooLong[c] = true;
                        }
                        else if (runLen[c] > 0)
                        { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                        last2[c] = last1[c]; last1[c] = x;
                        float outv = delay[di + c];            // sale la muestra de hace D cuadros (ya reparada si toco)
                        delay[di + c] = x;
                        buffer[bi + c] = (primed < D) ? 0f : outv;
                    }
                    dpos++; if (dpos >= D) dpos = 0;
                    if (primed < D) primed++;
                }
                return n;
            }
            // Lo mismo que el bucle de Read, una muestra cada vez y sabiendo de que canal es: el cuadro 'dpos' avanza al llegar su
            // ultimo canal. Solo cuando una lectura deja un cuadro a medias (con cuadros enteros no se usa).
            private void MuestraAMuestra(float[] buffer, int offset, int n)
            {
                hadInput = true;
                for (int i = 0; i < n; i++)
                {
                    int c = canal, q = dpos * ch + c, bi = offset + i;
                    float x = buffer[bi];
                    float ax = x < 0f ? -x : x;
                    int sgn = x >= 0f ? 1 : -1;
                    if (ax >= Thresh)
                    {
                        if (runLen[c] > 0 && sgn != runSign[c])
                        { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                        if (runLen[c] == 0)
                        { runSign[c] = sgn; preVal[c] = last1[c]; preSlope[c] = last1[c] - last2[c]; }
                        runLen[c]++;
                        if (runLen[c] > MaxRun) tooLong[c] = true;
                    }
                    else if (runLen[c] > 0)
                    { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                    last2[c] = last1[c]; last1[c] = x;
                    float outv = delay[q];
                    delay[q] = x;
                    buffer[bi] = (primed < D) ? 0f : outv;
                    if (++canal == ch)
                    {
                        canal = 0;
                        dpos++; if (dpos >= D) dpos = 0;
                        if (primed < D) primed++;
                    }
                }
            }
        }
        // FUNDIDO de SALIDA (fade suave al TERMINAR cada cancion): baja el volumen suave en vez de cortar en
        // seco, para que el paso al siguiente tema no sea un tajo. NO lleva fundido de ENTRADA a proposito:
        // arrancar desde silencio se oia "bajo" al principio en la rocola. NO solapa dos canciones (un crossfade
        // con solape pediria rehacer el motor y arriesgar la fiabilidad del negocio). Va al FINAL de la cadena
        // (tras el limitador); la medicion de volumen no se ve afectada (va antes, en LevelTap). Lineal = sin clics.
        private sealed class FadeProvider : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            private readonly long fadeFrames;    // frames de rampa del fundido de salida
            private long totalFrames;            // frames totales (0 = desconocido o muy corta: sin fundido de salida)
            private long pos;                    // frames ya entregados
            private int canal;                   // canal de la muestra siguiente dentro del cuadro 'pos' (0 = cuadro entero)
            // RELEVO SIN PAUSA a la pista siguiente del MISMO disco: el fundido se quita si aun no empezo (un disco
            // continuo tiene que seguir continuo). 0 = normal, 1 = quitado a tiempo, 2 = ya empezo y se termina.
            private int estadoFundido;
            // FUNDIDO DE ENTRADA (decima tanda, 12-10-2026). La cancion entraba de golpe: si su primera muestra no es
            // cero (un tema cortado de un disco en vivo, una retomada a media cancion) la bocina pasa de cero a donde va la
            // onda en una muestra y se oye un golpe seco. Ahora entra en una curva de 5 ms (inaudible en el ataque: es lo
            // que hace cualquier editor de audio en un corte) o de 0,3 s si se retoma a media cancion. Empieza donde
            // empieza la musica, despues del cebado de la cadena (sus primeros cuadros son ceros). La pista siguiente del
            // mismo disco que sigue sin corte no lleva entrada (SinEntrada): un disco continuo sigue continuo.
            private readonly long entradaDesde, entradaHasta;
            private int sinEntrada;
            internal const double EntradaSeg = 0.005, EntradaRetomadaSeg = 0.3;
            internal void SinEntrada() { System.Threading.Interlocked.Exchange(ref sinEntrada, 1); }
            // true: la cancion acaba entera, quitado ahora o ya antes (si la siguiente se retiro y se vuelve a preparar,
            // SiguienteConElInterruptor). false: el fundido ya empezo y se termina.
            internal bool Quita() { return System.Threading.Interlocked.CompareExchange(ref estadoFundido, 1, 0) != 2; }
            public FadeProvider(NAudio.Wave.ISampleProvider s, long total, double fadeSec) : this(s, total, fadeSec, 0, 0) { }
            public FadeProvider(NAudio.Wave.ISampleProvider s, long total, double fadeSec, long musicaDesde, double entradaSec)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                fadeFrames = (long)(fadeSec * rate); if (fadeFrames < 1) fadeFrames = 1;
                totalFrames = total;
                if (totalFrames > 0 && totalFrames < fadeFrames * 4) totalFrames = 0;   // cancion muy corta: sin fundido de salida
                entradaDesde = Math.Max(0L, musicaDesde);
                entradaHasta = entradaSec > 0 ? entradaDesde + Math.Max(1L, (long)(entradaSec * rate)) : entradaDesde;
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                // UN CUADRO PARTIDO (24-10-2026). La cuenta iba por cuadros enteros (n / ch): una lectura que dejaba un cuadro a medias
                // no avanzaba la posicion y las siguientes iban con los canales corridos. Leida de a una muestra, la entrada de 5 ms
                // caia en medio de la cancion (un hueco de hasta -6 dB) y el fundido de salida llegaba tarde. Entonces se sigue
                // muestra a muestra, sabiendo el cuadro y el canal de cada una; con cuadros enteros, el camino de siempre.
                if (canal != 0 || n % ch != 0) { MuestraAMuestra(buffer, offset, n); return n; }
                int frames = n / ch;
                if (pos < entradaHasta && System.Threading.Volatile.Read(ref sinEntrada) == 0) Entrada(buffer, offset, frames);
                // Fuera de los ultimos segundos no hay nada que hacer, y eso es TODA la cancion menos el final.
                // Antes se recorria muestra a muestra igualmente para acabar multiplicando por uno.
                if (totalFrames <= 0 || pos + frames <= totalFrames - fadeFrames) { pos += frames; return n; }
                if (System.Threading.Interlocked.CompareExchange(ref estadoFundido, 2, 0) == 1) { pos += frames; return n; }   // quitado a tiempo
                for (int f = 0; f < frames; f++)
                {
                    long rem = totalFrames - (pos + f);                                 // frames que faltan para el final
                    if (rem >= fadeFrames) continue;
                    float g = (rem <= 0) ? 0f : (float)((double)rem / fadeFrames);      // fundido de SALIDA
                    int bi = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bi + c] *= g;
                }
                pos += frames;
                return n;
            }
            // Lo mismo que Read y Entrada, una muestra cada vez: la de entrada y luego la de salida, cada una del cuadro de su muestra.
            private void MuestraAMuestra(float[] buffer, int offset, int n)
            {
                bool entra = System.Threading.Volatile.Read(ref sinEntrada) == 0;
                double largo = entradaHasta - entradaDesde + 1;
                for (int i = 0; i < n; i++)
                {
                    long p = pos;
                    if (entra && p < entradaDesde) buffer[offset + i] = 0f;   // el cebado: silencio exacto (ver Entrada)
                    else if (entra && p < entradaHasta)
                        buffer[offset + i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * (p - entradaDesde + 1) / largo));
                    if (totalFrames > 0 && totalFrames - p < fadeFrames
                        && System.Threading.Interlocked.CompareExchange(ref estadoFundido, 2, 0) != 1)
                    {
                        long rem = totalFrames - p;
                        buffer[offset + i] *= (rem <= 0) ? 0f : (float)((double)rem / fadeFrames);
                    }
                    if (++canal == ch) { canal = 0; pos++; }
                }
            }
            // Media onda de coseno: arranca sin escalon y llega a uno sin quiebre.
            // EL CEBADO, EN SILENCIO EXACTO (24-10-2026). Antes de la musica solo hay el retardo de la cadena: ceros, salvo los ecos previos
            // de lo que lee por delante (la pre-oscilacion del realce, el eco de la banda que recrea el DSEE y, desde que el grave en mono es
            // de fase lineal, el del grave del lado, hasta 15 ms). En el relevo sin pausa ese cebado se descarta; en la primera cancion despues
            // de un silencio sonaba antes de la musica. Ahora sale en cero.
            private void Entrada(float[] buffer, int offset, int frames)
            {
                double largo = entradaHasta - entradaDesde + 1;
                for (int f = 0; f < frames; f++)
                {
                    long p = pos + f;
                    if (p < entradaDesde) { int bz = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bz + c] = 0f; continue; }   // el cebado de la cadena
                    if (p >= entradaHasta) break;
                    float g = (float)(0.5 - 0.5 * Math.Cos(Math.PI * (p - entradaDesde + 1) / largo));
                    int bi = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bi + c] *= g;
                }
            }
        }
        // LIMITADOR CON PRE-ESCUCHA (lookahead): en vez de recortar el pico en seco (lo que mete distorsion
        // aspera al subir el volumen), mira unos milisegundos HACIA ADELANTE y baja la ganancia SUAVE antes de
        // que llegue el pico, recuperandola despacio. Asi se puede subir mucho el volumen y que suene fuerte pero
        // LIMPIO. Aplica la MISMA ganancia a los dos canales (no mueve la imagen estereo). ~5 operaciones por
        // muestra: barato hasta en la XP. Va al final de la cadena, tras el nivelado/realce.
        //
        // Ya NO va en la cadena: lo reemplaza LimitadorPicoReal (PicoReal.cs), que hace lo mismo mirando la onda
        // reconstruida entre muestras. Se conserva como referencia: las pruebas (limite, picoreal) comparan
        // contra el lo que se gano.
        private sealed class LookaheadLimiter : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, look;
            private readonly float ceiling, atk, rel;
            private readonly float[] delay;
            // Maximo de la ventana [salida actual, entrada actual]. Cola monotona:
            // cada pico entra/sale una vez; no hay barridos de look muestras ni asignaciones en Read.
            private readonly float[] peaks;
            private readonly long[] peakFrames;
            private int peakHead, peakCount;
            private long frameIndex;
            private int dpos, primed;
            private bool srcEnded, hadInput;
            private int drainFrames;
            private float env = 1f;
            public LookaheadLimiter(NAudio.Wave.ISampleProvider s, float ceilingLin, double lookMs)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); ceiling = ceilingLin;
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                look = Math.Max(16, (int)(lookMs * 0.001 * rate));
                delay = new float[look * ch];
                peaks = new float[look + 1]; peakFrames = new long[look + 1];
                atk = 1f - (float)Math.Exp(-9.0 / look);
                rel = 1f - (float)Math.Exp(-1.0 / (0.12 * rate));
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int DelayFrames { get { return look; } }
            private void AdvanceGain(float peak)
            {
                long oldest = frameIndex - look;
                while (peakCount > 0 && peakFrames[peakHead] < oldest)
                { peakHead = (peakHead + 1) % peaks.Length; peakCount--; }
                while (peakCount > 0)
                {
                    int last = (peakHead + peakCount - 1) % peaks.Length;
                    if (peaks[last] > peak) break;
                    peakCount--;
                }
                int tail = (peakHead + peakCount) % peaks.Length;
                peaks[tail] = peak; peakFrames[tail] = frameIndex++; peakCount++;
                float max = peaks[peakHead];
                float target = max > ceiling ? ceiling / max : 1f;
                env += (target < env ? atk : rel) * (target - env);
            }
            private float OutputGain(int di)
            {
                float peak = 0f;
                for (int c = 0; c < ch; c++)
                { float a = Math.Abs(delay[di + c]); if (a > peak) peak = a; }
                // La exponencial se acerca al objetivo sin alcanzarlo exactamente. La correccion
                // residual tambien es una GANANCIA ESTEREO, nunca un recorte distinto por canal.
                if (peak > 0f && peak * env > ceiling)
                    env = (ceiling / peak) * 0.9999999f;
                return env;
            }
            private int Drain(float[] buffer, int offset, int count)
            {
                int frames = Math.Min(drainFrames, count / ch);
                for (int f = 0; f < frames; f++)
                {
                    AdvanceGain(0f);
                    int bi = offset + f * ch, di = dpos * ch;
                    float g = OutputGain(di);
                    for (int c = 0; c < ch; c++)
                    { buffer[bi + c] = delay[di + c] * g; delay[di + c] = 0f; }
                    dpos++; if (dpos >= look) dpos = 0;
                }
                drainFrames -= frames;
                return frames * ch;
            }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (count < ch) return 0;   // una lectura vacia no marca fin del origen
                if (srcEnded) return Drain(buffer, offset, count);
                int n = src.Read(buffer, offset, count - count % ch);
                if (n <= 0)
                {
                    srcEnded = true;
                    drainFrames = hadInput ? look : 0;
                    return Drain(buffer, offset, count);
                }
                int frames = n / ch;
                if (frames > 0) hadInput = true;
                for (int f = 0; f < frames; f++)
                {
                    int bi = offset + f * ch, di = dpos * ch;
                    float peak = 0f;
                    for (int c = 0; c < ch; c++)
                    { float a = Math.Abs(buffer[bi + c]); if (a > peak) peak = a; }
                    AdvanceGain(peak);
                    float g = OutputGain(di);
                    for (int c = 0; c < ch; c++)
                    {
                        float outv = delay[di + c] * g;
                        delay[di + c] = buffer[bi + c];
                        buffer[bi + c] = primed < look ? 0f : outv;
                    }
                    dpos++; if (dpos >= look) dpos = 0;
                    if (primed < look) primed++;
                }
                return n;
            }
        }
    }
}

// LA MEDIDA COMPLETA DEL REPASO Y EL MATERIAL PARA LA IA (de XP, tal cual: Form1.Sonido.cs MeasureFile y Form1.Ia.cs). Al repasar cada
// cancion se mide, ademas de su sonoridad, la huella tonal del EQ automatico, donde el MP3 le corto los agudos (la calidad de «Tu
// biblioteca»), sus picos recortados y, si ia_datos= esta encendido, se juntan pares de entrenamiento para la IA del DSEE en
// rocola_ia_datos.bin (con su indice .idx y lo hecho en rocola_ia_hechas.txt), como en XP.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        private bool MeasureFile(string path, bool aFondo, out float rms, out float durSec, out float[] eqCurve)
        {
            fuenteLeida = null;
            FuenteMedida fuenteInicio = CapturaFuente(path);
            rms = 0f; durSec = 0f; eqCurve = null;
            integradaLista = false;   // lo que deja para TomaIntegrada: nada, salvo que lea la cancion entera
            if (MeasurementCancelled()) return false;
            // De un VIDEO solo interesa CUANTO SUENA. Su sonido lo saca el reproductor de Windows, no
            // nosotros: no pasa por el ecualizador ni por el DSEE ni por los graves. Medirle la huella tonal
            // o cosechar cuadros para la IA seria trabajo y disco gastados en algo que nunca se usaria.
            bool esVideo = EsVideo(path);
            LectorMedida rd = null;
            TrabajoMedida trabajo = null;
            try
            {
                rd = new LectorMedida(path);
                rd.Volume = 1.0f;
                int rate = rd.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                int ch = rd.WaveFormat.Channels; if (ch < 1) ch = 1;
                double total = rd.TotalTime.TotalSeconds;
                if (total <= 0.05 || total > 36000) return false;   // vacio o absurdo (archivo roto)
                durSec = (float)total;
                trabajo = TomaTrabajoMedida(rate, ch, !esVideo);
                float[] c1 = trabajo.C1, c2 = trabajo.C2;   // misma receta K; estados nuevos por cancion
                float[] st = trabajo.Estado, buf = trabajo.Buffer;
                double sumSq = 0; long nSamp = 0;
                // RECORTE DE ORIGEN: muestras pegadas al techo digital en rachas de 3 o mas. No es distorsion
                // que metamos nosotros: viene asi masterizada. Se cuenta aqui, en el recorrido que ya se hacia.
                long clipN = 0; int[] clipRun = trabajo.Recorte;
                float[][] bp = trabajo.Bandas; float[] bpSt = trabajo.EstadoBandas;
                double[] bpSum = trabajo.SumaBandas; long bpN = 0;
                // COSECHA PARA LA IA: se analiza el canal 0 con el MISMO motor del DSEE. Los pares se guardan
                // en memoria y solo se escriben al final, si resulta que la cancion SI conserva sus agudos
                // (una cancion dañada no puede enseñar nada: los agudos reales ya no estan en el archivo).
                SpectralTap hv = null; float[] hvFrame = null, hvFeat = null, hvTgt = null;
                int hvFill = 0; List<float> hvPairs = null;
                // El presupuesto total no crece: cada ventana elige alternadamente
                // un canal y cada lista conserva el indice correcto en su origen.
                SpectralTap[] hvCanales = null; float[][] hvCuadros = null;
                List<float>[] hvPares = null; long hvTurno = 0;
                long hvVistos = 0, hvVistosMax = IaFramesPerSong;   // cuadros con sonido que han pasado / hasta donde mirar
                int hvStride = 1, hvFase = 0;
                // El ACANTILADO se mide SIEMPRE, cosechemos o no para la IA. Es el dato que dice si una cancion
                // llego completa o el codec le talo los agudos, y de eso dependen dos cosas importantes: si sirve
                // para fijar el objetivo del EQ automatico, y que bandas suyas tiene sentido corregir. Cuesta unas
                // pocas decenas de FFT por cancion, nada al lado de descomprimirla entera.
                bool soloCorte = false;
                float[] cortesIa = IaCutsHz;   // la lista de esta cancion (CortesIa.cs), fija mientras se cosecha
                // tramos: si el tema es corto se analiza entero; si no, PreSegs ventanas repartidas evitando
                // el arranque y el final (intros/silencios finales no representan la sonoridad del tema).
                // A FONDO: un solo "tramo" que abarca la cancion entera. En automatico: PreSegs ventanas.
                // (Se decide AQUI, antes de la cosecha, porque de cuanto audio se va a descomprimir depende
                // cada cuantos cuadros conviene tomar uno.)
                int segs = (aFondo || total <= PreSegSec * PreSegs) ? 1 : PreSegs;
                double segLen = (segs == 1) ? total : PreSegSec;
                // Y SI ESTE RECORRIDO LEE LA CANCION ENTERA (corta, o repaso a fondo), de paso se le mide la
                // sonoridad integrada BS.1770 (ver Sonoridad.cs). No cambia nada de lo que se mide aqui.
                MedidorBs1770 integ = null;
                if (segs == 1 && !esVideo && rd.WaveFormat.SampleRate >= 8000)
                {
                    double[] pesosInteg = DisposicionBs1770.DeArchivo(path, ch);
                    if (pesosInteg != null) { try { integ = trabajo.Sonoridad(rd.WaveFormat.SampleRate, pesosInteg); } catch { integ = null; } }
                }
                Func<float[], int, int, int> lee = rd.Read;
                int cuadroLen = 2048;   // el de SpectralTap; se confirma al crearlo
                long framesTot = (long)(segs * segLen * rate / cuadroLen);
                int diana = aFondo ? IaDeepFrames : IaFramesPerSong;
                // El paso se REDONDEA HACIA ARRIBA a proposito: con la division a secas el ultimo cuadro caia
                // antes del final y se quedaba sin mirar el ultimo 15% de cada cancion.
                long pasoIa = (framesTot - IaWarmFrames + diana - 1) / Math.Max(1, diana);
                int strideIa = (pasoIa > 1) ? (int)pasoIa : 1;
                // ----- CUANTAS VECES SE MIRA CADA CANCION -----
                // Niveles apuntados en rocola_ia_hechas.txt: 0 = nunca; 1 = una pasada del modo PASIVO;
                // 2 = repasada A FONDO (esa ya dio todo lo que tenia); 3..9 = 2..8 pasadas del pasivo.
                //
                // Antes, en cuanto una cancion llegaba al nivel 1 el modo pasivo NO volvia a mirarla nunca. Con
                // la biblioteca entera en nivel 1 el archivo de material dejaba de crecer y ahi se quedaba para
                // siempre: por eso despues de dias encendida seguia en los mismos gigas. Ahora cada pasada mira
                // cuadros DISTINTOS de la misma cancion (ver hvFase), asi que el material sigue creciendo sin
                // que el repaso cueste ni un milisegundo mas por cancion. Se para cuando las fases se agotan
                // (pasado 'strideIa' ya se habrian repetido cuadros, que es engordar el archivo por gusto).
                int nivelPrevio; lock (iaDoneLock) { if (!iaDone.TryGetValue(path, out nivelPrevio)) nivelPrevio = 0; }
                int pasadas = IaPasadasDe(nivelPrevio);
                int nivelAhora; bool cosechar;
                if (aFondo) { nivelAhora = 2; cosechar = (nivelPrevio != 2); }
                else { nivelAhora = IaNivelDePasada(pasadas); cosechar = pasadas < Math.Min(IaPasadasMax, strideIa); }
                cosechar = cosechar && iaHarvestOn && !IaBudgetFull() && !esVideo;
                if (!cosechar && !esVideo)
                {
                    // Solo hace falta el acantilado: se analizan unos pocos cuadros y se para (ni se guardan pares).
                    try { hv = trabajo.Analizador(0, rate); hvFrame = trabajo.Cuadro(0, hv.FrameLen); soloCorte = true; hvVistosMax = IaWarmFrames + 24; }
                    catch { hv = null; }
                }
                if (cosechar)
                {
                    try
                    {
                        hv = trabajo.Analizador(0, rate);
                        hvFrame = trabajo.Cuadro(0, hv.FrameLen); hvFeat = trabajo.Features; hvTgt = trabajo.Target;
                        hvPairs = trabajo.Pares(0);
                        int canalesIa = Math.Min(2, ch);
                        hvCanales = new SpectralTap[canalesIa]; hvCuadros = new float[canalesIa][];
                        hvPares = new List<float>[canalesIa];
                        hvCanales[0] = hv; hvCuadros[0] = hvFrame; hvPares[0] = hvPairs;
                        if (canalesIa == 2)
                        {
                            hvCanales[1] = trabajo.Analizador(1, rate);
                            hvCuadros[1] = trabajo.Cuadro(1, hv.FrameLen); hvPares[1] = trabajo.Pares(1);
                        }
                        // Los cuadros se toman REPARTIDOS por todo lo que se va a descomprimir, no seguidos
                        // desde el principio. Antes el modo pasivo cosechaba los 192 PRIMEROS: eso son 8,9
                        // segundos, siempre del mismo sitio de cada cancion, o sea que la red solo llegaba a
                        // ver esa rebanada de toda la biblioteca. Y dos cuadros pegados se parecen tanto que
                        // apilarlos engorda el archivo sin ensenar nada nuevo.
                        if (hv.FrameLen != cuadroLen)   // por si el cuadro cambiara: se rehace la cuenta
                        {
                            cuadroLen = hv.FrameLen;
                            framesTot = (long)(segs * segLen * rate / cuadroLen);
                            pasoIa = (framesTot - IaWarmFrames + diana - 1) / Math.Max(1, diana);
                            strideIa = (pasoIa > 1) ? (int)pasoIa : 1;
                        }
                        hvStride = strideIa;
                        hvFase = (hvStride > 1) ? (pasadas % hvStride) : 0;   // cada pasada, cuadros distintos
                        hvVistosMax = IaWarmFrames + (long)hvStride * diana;
                    }
                    catch { hv = null; }
                }
                // La ficha de calidad usa la misma ventana tanto si se cosecha IA como si no.
                // El analizador puede seguir avanzando para cosechar, pero no cambia esa ficha.
                int qualityFrames = IaWarmFrames + 24;
                // Calidad tiene SU estado y solo canal 0; alternar pares L/R, un canal mudo o
                // el paso de cosecha no pueden cambiar su ventana ni el calor del detector.
                SpectralTap qualityTap = hvCanales != null ? trabajo.Analizador(2, rate) : hv;
                long qualitySeen = 0;
                float measuredCut = qualityTap != null ? qualityTap.CutHz : 0f, measuredCutLo = qualityTap != null ? qualityTap.CutLoHz : 0f;
                for (int s = 0; s < segs && !preScanStop; s++)
                {
                    if (segs > 1)
                    {
                        double span = total * 0.80;                       // zona util: del 10% al 90% del tema
                        double pos = total * 0.10 + span * s / (double)segs;
                        if (pos + segLen > total) pos = Math.Max(0, total - segLen);
                        try { rd.CurrentTime = TimeSpan.FromSeconds(pos); } catch { }
                    }
                    long want = (long)(segLen * rate) * ch;
                    long got = 0;
                    while (got < want)
                    {
                        if (MeasurementCancelled()) return false;
                        int want2 = (int)Math.Min(trabajo.LecturaMax, want - got);
                        int n = LeeCuadrosCompletos(lee, buf, want2, ch);   // cuadros enteros: ningun canal se cruza con otro
                        if (n <= 0) break;
                        got += n;
                        if (integ != null) integ.Procesa(buf, 0, n);
                        // La huella del EQ de todo el trozo de una vez, banda por banda (HuellaDelTrozo): la misma cuenta, en el
                        // mismo orden y con la limpieza de restos en los mismos cuadros que cuadro a cuadro, en la mitad de tiempo.
                        if (!esVideo) HuellaDelTrozo(buf, n / ch, ch, bp, bpSt, bpSum, bpN);
                        // Y la sonoridad K y los recortes, igual de una vez (SonoridadDelTrozo): cuadro a cuadro y canal a canal, como
                        // siempre, porque su suma es una sola para los dos canales; solo sin ir y venir de la IA en cada cuadro.
                        SonoridadDelTrozo(buf, n / ch, ch, c1, c2, st, ref sumSq, ref nSamp, ref clipN, clipRun, bpN);
                        for (int i = 0; i + ch <= n; i += ch)
                        {
                            bpN++;
                            // canal 0 hacia el analizador de la IA (el DSEE tambien trabaja por canal)
                            if (hv != null && ((soloCorte ? false : hvVistos < hvVistosMax) || qualitySeen < qualityFrames))
                            {
                                hvFrame[hvFill] = buf[i];
                                if (hvCuadros != null && hvCuadros.Length == 2) hvCuadros[1][hvFill] = buf[i + 1];
                                hvFill++;
                                if (hvFill >= hvFrame.Length)
                                {
                                    hvFill = 0;
                                    if (qualitySeen < qualityFrames)
                                    {
                                        double qualityEnergy = 0;
                                        for (int q = 0; q < hvFrame.Length; q++) qualityEnergy += hvFrame[q] * hvFrame[q];
                                        if (qualityEnergy / hvFrame.Length > 1e-8)
                                        {
                                            qualityTap.AnalyzeFrame(hvFrame, 0); qualitySeen++;
                                            measuredCut = qualityTap.CutHz; measuredCutLo = qualityTap.CutLoHz;
                                        }
                                    }
                                    // Alternar por grupos del paso: con stride par, alternar cada
                                    // cuadro escogeria siempre el mismo canal para todos los pares.
                                    int canalIa = hvCanales == null ? 0 : (int)((hvTurno++ / hvStride) % hvCanales.Length);
                                    SpectralTap analizador = hvCanales == null ? hv : hvCanales[canalIa];
                                    float[] cuadroIa = hvCuadros == null ? hvFrame : hvCuadros[canalIa];
                                    List<float> paresIa = hvPares == null ? hvPairs : hvPares[canalIa];
                                    double en = 0; for (int q = 0; q < cuadroIa.Length; q++) en += cuadroIa[q] * cuadroIa[q];
                                    if (!soloCorte && hvVistos < hvVistosMax && en / cuadroIa.Length > 1e-8)   // saltar cuadros practicamente mudos
                                    {
                                        hvVistos++;
                                        // SOLO se le hace la FFT a los cuadros que van a servir. Antes se
                                        // analizaban TODOS los que pasaban hasta el tope y despues se tiraba
                                        // el trabajo de los que no tocaban: en el repaso a fondo eso eran mas
                                        // de tres cuartas partes de las FFT hechas para nada. Los primeros si
                                        // van seguidos: el acantilado necesita unos cuantos para asentarse.
                                        bool sirve = (hvVistos <= qualityFrames) || hvStride == 1
                                                     || (((hvVistos + hvFase) % hvStride) == 0);
                                        if (sirve)
                                        {
                                            analizador.AnalyzeFrame(cuadroIa, 0);
                                            if (!soloCorte && hvVistos > IaWarmFrames &&
                                                (hvStride == 1 || ((hvVistos + hvFase) % hvStride) == 0))
                                                for (int q = 0; q < cortesIa.Length; q++)
                                                    if (analizador.MakePair(cortesIa[q], hvFeat, hvTgt))
                                                    {
                                                        for (int z = 0; z < 28; z++) paresIa.Add(hvFeat[z]);
                                                        for (int z = 0; z < 12; z++) paresIa.Add(hvTgt[z]);
                                                    }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // Hasta el final del archivo, SOLO para la integrada: lo de arriba queda exactamente como antes. Si
                // el archivo se acaba antes de lo que dice durar, la integrada no vale y no se publica.
                if (integ != null && !preScanStop)
                {
                    while (true)
                    {
                        if (MeasurementCancelled()) return false;
                        int n = LeeCuadrosCompletos(lee, buf, trabajo.LecturaMax, ch);
                        if (n <= 0) break;
                        integ.Procesa(buf, 0, n);
                    }
                    SonoridadIntegrada si;
                    if (!MeasurementCancelled() && CierraIntegrada(integ, total, rd.WaveFormat.SampleRate, out si)) { integradaValor = si; integradaLista = true; }
                }
                if (MeasurementCancelled()) { integradaLista = false; return false; } // no publicar ni cosechar una medida cancelada
                // Solo las canciones COMPLETAS (sin acantilado, el mismo umbral que usa el DSEE para no tocarlas)
                // sirven de maestras: en las dañadas los agudos reales ya no existen y no hay nada que enseñar.
                if (hv != null && !soloCorte && hvPares != null)
                {
                    // Con su ORIGEN (cancion, carpeta, frecuencia, cuadro, canal 0) para que el entrenador pueda
                    // separar por cancion. Ver IaApuntaOrigenes. El origen se saca UNA vez por cancion (la etiqueta del archivo, el genero, las
                    // huellas: lo mismo para los dos canales salvo el numero de canal) y los canales se anexan juntos, con una confirmacion en
                    // disco por archivo (IaAppendPairsLote).
                    bool escribio = false, completas = true;
                    List<List<float>> lotesIa = new List<List<float>>(); List<int> canalesIa = new List<int>();
                    for (int canalIa = 0; canalIa < hvPares.Length; canalIa++)
                        if (hvPares[canalIa].Count >= 40 && hvCanales[canalIa].CutHz >= EqSanaHz) { lotesIa.Add(hvPares[canalIa]); canalesIa.Add(canalIa); }
                    if (lotesIa.Count > 0)
                    {
                        IaOrigen origenBase = IaOrigenDe(path, total, rate, hv.FrameLen, canalesIa[0]);
                        IaOrigen[] origenesIa = new IaOrigen[lotesIa.Count];
                        for (int k = 0; k < origenesIa.Length; k++) origenesIa[k] = IaOrigenParaCanal(origenBase, canalesIa[k]);
                        bool[] oksIa = IaAppendPairsLote(lotesIa.ToArray(), origenesIa);
                        for (int k = 0; k < oksIa.Length; k++) { escribio |= oksIa[k]; completas &= oksIa[k]; }
                    }
                    if (escribio && completas) MarkIaDone(path, nivelAhora);
                }
                float corteHz = measuredCut;
                float corteLo = measuredCutLo;
                float recorte = (nSamp > 0) ? (float)((double)clipN / nSamp) : 0f;
                if (nSamp < 40000) return durSec > 0.01f && TerminaFuenteLeida(path, fuenteInicio);   // muy poco audio util
                rms = (float)Math.Sqrt(sumSq / nSamp);
                // SILENCIO DE VERDAD. Un archivo sin nada de sonido da cero, y con cero no se guardaba: se
                // quedaba como "sin medir", se volvia a medir en cada arranque para siempre, y no aparecia
                // nunca en la limpieza de videos mudos. Se le pone un valor minusculo -por debajo del umbral
                // con el que TODO el programa decide "esto no tiene medida util", asi que nadie va a intentar
                // subirlo cien mil veces- para que quede APUNTADO como medido y callado.
                if (rms < LoudMudo) rms = LoudMudo;
                // Huella tonal en dB por banda, referida a la MEDIANA del cuerpo (158 Hz - 5.6 kHz): exactamente
                // la misma cuenta que hace FinalizeEqMeasure al aprenderla oyendo la cancion.
                if (bpN >= 40000 && !esVideo)
                {
                    float[] curve = new float[EqLen];
                    curve[EqCut] = corteHz; curve[EqClip] = recorte; curve[EqCutLo] = corteLo; curve[EqFingerprint] = EqVersionOriginal;
                    for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(bpSum[b] / bpN + 1e-12));
                    float[] cuerpo = trabajo.Cuerpo;
                    for (int b = 0; b < cuerpo.Length; b++) cuerpo[b] = curve[b + 1];
                    Array.Sort(cuerpo);
                    float refDb = cuerpo[2];
                    for (int b = 0; b < 8; b++) curve[b] -= refDb;
                    eqCurve = curve;
                }
                return TerminaFuenteLeida(path, fuenteInicio);
            }
            catch { return durSec > 0.01f && TerminaFuenteLeida(path, fuenteInicio); }
            finally
            {
                if (rd != null) { try { rd.Dispose(); } catch { } }
                if (trabajo != null) trabajo.Suelta(); // tambien tras cancelacion, lectura rota o retorno temprano
            }
        }


        // De que canciones se saco material para la IA y HASTA QUE NIVEL: 1 = el muestreo del modo automatico
        // (192 cuadros), 2 = el repaso A FONDO (12 veces mas). Se guarda el nivel, no un simple "ya esta",
        // porque los dos modos ACUMULAN: lo que junto el modo automatico se queda, y el repaso a fondo le
        // anade el resto. Dentro del MISMO nivel una cancion aporta una sola vez, para no sesgar hacia ella.
        private Dictionary<string, int> iaDone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // MATERIAL DE ENTRENAMIENTO PARA LA IA: mientras el pre-analisis recorre la biblioteca, las canciones
        // que AUN conservan sus agudos (320k/FLAC/WAV) sirven de maestras: se les corta el agudo a proposito y
        // se guarda "lo que queda / lo que habia". Ese archivo se lleva en una USB a una PC potente, se entrena
        // alli con ia/entrenar_ia.py y se regresa el rocola_ia.bin nuevo. Asi la IA aprende de TODA la
        // biblioteca sin tener que mover el disco de la rocola. "ia_datos=0" en config lo apaga.
        private bool iaHarvestOn = true;
        private volatile int iaPairsTotal = 0;          // pares ya guardados (solo para mostrar el avance)
        // ---------- Archivo con el material de entrenamiento de la IA ----------
        // Formato binario compacto (millones de pares caben sin problema): cabecera "RIAD" + version + tamaños,
        // y luego cada par como 28 float de entrada + 12 float de objetivo. numpy lo lee de un tiron.
        private const int IaFramesPerSong = 192;   // cuadros COSECHADOS por cancion y pasada (repartidos por lo descomprimido)
        private const int IaPasadasMax = 8;        // pasadas del modo pasivo por cancion (cada una, cuadros distintos)
        // Traduccion entre el NIVEL apuntado en rocola_ia_hechas.txt y cuantas pasadas pasivas lleva la cancion:
        //   nivel 0 = ninguna;  1 = una;  2 = repasada A FONDO (ya dio todo lo suyo);  3..9 = 2..8 pasadas.
        // Los archivos viejos se siguen leyendo tal cual: en ellos solo hay niveles 1 y 2, con el mismo
        // significado que aqui.
        private static int IaPasadasDe(int nivel)
        { return (nivel <= 0) ? 0 : (nivel == 1) ? 1 : (nivel == 2) ? int.MaxValue : (nivel - 1); }
        private static int IaNivelDePasada(int pasadas)
        { return (pasadas <= 0) ? 1 : (pasadas + 2); }
        // CUANTO VALE cada nivel. No es su numero: el 2 -repasada A FONDO- es el TOPE, porque de esa
        // cancion ya no queda nada por sacar, aunque antes hubiera acumulado varias pasadas del pasivo
        // (que llevan numeros mas altos). Con esto el apunte sigue siendo de una sola direccion: una
        // cancion nunca baja de nivel, asi que no se puede volver a cosechar lo que ya se cosecho.
        private static int IaRango(int nivel) { return (nivel == 2) ? 100 : nivel; }
        private const int IaDeepFrames = 1536;     // A FONDO: cuadros APROVECHADOS, repartidos por TODA la cancion
        private const int IaWarmFrames = 64;       // los primeros no se usan: el acantilado aun se esta midiendo
        // TOPE del archivo de material. Existe para que el archivo siga siendo MANEJABLE: hay que poder copiarlo
        // a una USB y cargarlo en la PC donde se entrena. 24 GB cubren de sobra una biblioteca de ~20 mil
        // canciones aptas cosechadas a fondo, y caben holgados en una memoria de 64 GB (con formato exFAT o NTFS;
        // en FAT32 ningun archivo puede pasar de 4 GB, asi que ahi habria que bajarlo). El entrenador NO necesita
        // 24 GB de RAM: toma una muestra repartida por todo el archivo (ver ia/entrenar_ia.py). Se cambia con
        // ia_datos_mb= en la configuracion; 0 lo quita del todo.
        private long iaMaxBytes = 24L * 1024 * 1024 * 1024;
        private bool IaBudgetFull() { return iaMaxBytes > 0 && (long)iaPairsTotal * 160L + 16L >= iaMaxBytes; }
        private string IaInfoText()
        {
            long mb = ((long)iaPairsTotal * 160L + 16L) / (1024 * 1024);
            string tam = (mb >= 1024) ? ((mb / 1024.0).ToString("0.0") + " GB") : (mb.ToString("#,0") + " MB");
            return iaPairsTotal.ToString("#,0") + " ejemplos (" + tam
                   + (IaBudgetFull() ? ", tope alcanzado" : "") + ")";
        }
        // los cortes simulados: IaCutsHz, los cinco de siempre y los mas comunes de la biblioteca (CortesIa.cs)
        private string IaDataFile() { return RutaDatos("rocola_ia_datos.bin"); }
        private readonly object iaDoneLock = new object();
        private volatile bool iaDoneDirty = false;   // hay canciones nuevas cosechadas sin apuntar en disco
        private readonly List<string> iaPend = new List<string>();   // lineas apuntadas y aun sin escribir
        private bool iaDoneSaving;
        private static bool IaHeaderValida(FileStream fs)
        {
            try
            {
                long old = fs.Position; fs.Position = 0;
                byte[] h = new byte[16]; int got = 0, n;
                while (got < h.Length && (n = fs.Read(h, got, h.Length - got)) > 0) got += n;
                fs.Position = old;
                return got == 16 && h[0] == (byte)'R' && h[1] == (byte)'I'
                    && h[2] == (byte)'A' && h[3] == (byte)'D'
                    && BitConverter.ToInt32(h, 4) == 1 && BitConverter.ToInt32(h, 8) == 28
                    && BitConverter.ToInt32(h, 12) == 12;
            }
            catch { return false; }
        }
        private void IaCountPairs()
        {
            lock (iaFileLock)
            try
            {
                string f = IaDataFile();
                if (!File.Exists(f)) { iaPairsTotal = 0; return; }
                using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    if (!IaHeaderValida(fs)) { iaPairsTotal = 0; return; }
                    long completos = (fs.Length - 16) / 160;
                    long valido = 16 + completos * 160;
                    if (fs.Length != valido)
                    {
                        fs.SetLength(valido); fs.Flush();
                        try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                    }
                    iaPairsTotal = (completos > int.MaxValue) ? int.MaxValue : (int)completos;
                }
            }
            catch { iaPairsTotal = 0; }
        }
        // Añade al archivo los pares de UNA cancion (solo se llama con canciones que si conservan sus agudos).
        // Con el repaso a fondo en varios hilos, esto lo llaman VARIOS a la vez: dos anadidos solapados
        // dejarian el archivo de material entrelazado y por tanto inservible. El candado cuesta nada al lado
        // de descomprimir una cancion, que es lo que hace el hilo justo antes de llegar aqui.
        private readonly object iaFileLock = new object();
        // Con el ORIGEN de esos pares (ver IaApuntaOrigenes). Sin origen -como la llaman las pruebas de los
        // carriles- se guarda igual y esos ejemplos quedan "sin origen": nunca se inventa de donde vienen.
        // El lote sigue siendo una sola entrada de origen. Solo su copia de trabajo se divide en bloques:
        // ambos arrays quedan por debajo del LOH y se reutilizan bajo iaFileLock, aunque un repaso a fondo
        // entregue millones de bytes. No cambia ni un float ni se elimina ningun ejemplo del lote.
        private const int IaScratchFloats = 40 * 256;
        private float[] iaParesScratch;
        private byte[] iaBytesScratch;
        private void IaPreparaScratch()
        {
            if (iaParesScratch == null) iaParesScratch = new float[IaScratchFloats];
            if (iaBytesScratch == null) iaBytesScratch = new byte[IaScratchFloats * 4];
        }
        private uint IaEscribePares(Stream destino, List<float> pairs)
        {
            uint crc = 0xFFFFFFFFu;
            for (int desde = 0; desde < pairs.Count; )
            {
                int cuantos = Math.Min(iaParesScratch.Length, pairs.Count - desde), bytes = cuantos * 4;
                pairs.CopyTo(desde, iaParesScratch, 0, cuantos);
                Buffer.BlockCopy(iaParesScratch, 0, iaBytesScratch, 0, bytes);
                crc = IaCrc32Acumula(crc, iaBytesScratch, 0, bytes);
                destino.Write(iaBytesScratch, 0, bytes);
                desde += cuantos;
            }
            return crc ^ 0xFFFFFFFFu;
        }
        private bool IaAppendPairs(List<float> pairs) { return IaAppendPairsOrigen(pairs, null); }
        private bool IaAppendPairsOrigen(List<float> pairs, IaOrigen origen)
        {
            return IaAppendPairsLote(new List<float>[] { pairs }, new IaOrigen[] { origen })[0];
        }
        // LOS CANALES DE UNA CANCION, UNA CONFIRMACION POR ARCHIVO (19-10-2026). La cosecha escribia cada canal por su cuenta: datos, confirmacion en
        // disco (FlushFileBuffers), entrada del indice, confirmacion, linea de genero, confirmacion: seis confirmaciones por cancion en estereo, cada una
        // una espera de disco en un hilo de fondo. Aqui los lotes (uno por canal, en orden) se anexan igual que antes, uno tras otro y con las mismas
        // comprobaciones, pero cada archivo se confirma UNA vez al final: los datos, luego (solo despues) las entradas del indice, luego las lineas de
        // genero. Los mismos bytes en los tres archivos y el mismo orden; lo que se pierde, si se va la luz entre dos canales, es que el canal 0 ya no
        // tiene su entrada de origen cuando el 1 no llego al disco (esos ejemplos quedan "sin origen", que el entrenador no usa para validar, y nunca
        // se atribuyen mal). Devuelve, por lote, si se escribio. Un lote invalido o que no cabe no impide los siguientes, como antes.
        private bool[] IaAppendPairsLote(List<float>[] lotes, IaOrigen[] origenes)
        {
            bool[] oks = new bool[lotes.Length];
            lock (iaFileLock)
            {
                long[] primeros = new long[lotes.Length]; uint[] crcs = new uint[lotes.Length]; bool[] nuevos = new bool[lotes.Length];
                long totalFinal = -1;
                FileStream fs = null;
                bool pendiente = false;   // hay escrito sin confirmar en 'fs'
                try
                {
                    for (int i = 0; i < lotes.Length; i++)
                    {
                        List<float> pairs = lotes[i];
                        if (pairs == null || pairs.Count < 40 || (pairs.Count % 40) != 0) continue;
                        try
                        {
                            long loteBytes = (long)pairs.Count * 4L, tope = iaMaxBytes;
                            // Un lote se admite completo o no se escribe: nunca recortar cuadros o canales.
                            // Esta comprobacion temprana evita crear una cabecera si el lote ni siquiera cabe
                            // en un corpus vacio. La decisiva usa el largo REAL, dentro del mismo candado.
                            if (tope > 0 && loteBytes > Math.Max(0L, tope - 16L)) continue;
                            if (fs == null) fs = new FileStream(IaDataFile(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                            bool nuevo = fs.Length == 0;
                            if (!nuevo && !IaHeaderValida(fs)) continue;   // no anexar encima de un archivo ajeno/corrupto
                            long completos = nuevo ? 0L : Math.Max(0L, (fs.Length - 16) / 160);
                            long valido = 16 + completos * 160;
                            tope = iaMaxBytes;
                            if (tope > 0 && valido + loteBytes > tope) continue;
                            IaPreparaScratch();   // asignar antes de modificar una cabecera/cola interrumpida
                            if (nuevo)
                            {
                                byte[] head = new byte[16];
                                head[0] = (byte)'R'; head[1] = (byte)'I'; head[2] = (byte)'A'; head[3] = (byte)'D';
                                Buffer.BlockCopy(BitConverter.GetBytes(1), 0, head, 4, 4);
                                Buffer.BlockCopy(BitConverter.GetBytes(28), 0, head, 8, 4);
                                Buffer.BlockCopy(BitConverter.GetBytes(12), 0, head, 12, 4);
                                fs.Write(head, 0, head.Length);
                            }
                            if (fs.Length != valido) fs.SetLength(valido);   // recupera una ultima escritura interrumpida
                            fs.Position = valido;
                            // Los mismos float32 little-endian y el mismo CRC, sin duplicar todo el lote en RAM.
                            crcs[i] = IaEscribePares(fs, pairs);
                            pendiente = true;
                            primeros[i] = completos; nuevos[i] = nuevo; oks[i] = true;
                            totalFinal = completos + pairs.Count / 40;
                        }
                        catch
                        {
                            // Como antes: este lote no se escribe; el siguiente reabre el archivo y recupera la cola interrumpida. Lo ya escrito se confirma.
                            oks[i] = false;
                            if (fs != null) { ConfirmaDatosIa(fs, ref pendiente); try { fs.Dispose(); } catch { } fs = null; }
                        }
                    }
                    if (fs != null) ConfirmaDatosIa(fs, ref pendiente);
                }
                catch { }
                finally { if (fs != null) { try { fs.Dispose(); } catch { } } }
                if (totalFinal >= 0) iaPairsTotal = totalFinal > int.MaxValue ? int.MaxValue : (int)totalFinal;
                // Los datos ya estan en disco: ahora sus entradas de origen (y despues los generos), cada archivo confirmado una vez.
                List<int> conOrigen = new List<int>();
                for (int i = 0; i < lotes.Length; i++) if (oks[i] && origenes != null && i < origenes.Length && origenes[i] != null) conOrigen.Add(i);
                if (conOrigen.Count > 0) IaApuntaOrigenes(conOrigen, lotes, primeros, crcs, origenes, nuevos);
            }
            return oks;
        }
        private void ConfirmaDatosIa(FileStream fs, ref bool pendiente)
        {
            if (!pendiente) return;
            fs.Flush();
            try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
            pendiente = false;
        }

        // ---------- DE QUE CANCION VIENE CADA EJEMPLO: rocola_ia_datos.idx ----------
        // Para validar la IA de verdad hay que separar por CANCION antes de mirar un solo ejemplo: si cuadros
        // de la misma cancion caen en entrenamiento y en validacion, la red se "sabe" la respuesta y el
        // numero de validacion miente. rocola_ia_datos.bin no dice de que cancion viene cada ejemplo, y por
        // su posicion no se puede adivinar. Asi que al lado se apunta UNA entrada de 64 bytes por cancion
        // cosechada (lo lee ia/origen_dsee.py):
        //     0 primer registro (int64)     24 canal                     48 huella del artista (0 = no se sabe)
        //     8 cuantos registros           28 CRC32 de esos registros   56 reservado
        //    12 version del extractor       32 huella de la cancion      60 CRC32 de los bytes 0..59
        //    16 frecuencia   20 cuadro      40 huella de la carpeta
        // Solo HUELLAS (FNV-1a de 64 bits), ni rutas ni nombres. La cancion es su nombre de archivo mas su
        // duracion: mover la biblioteca de sitio no la convierte en otra. La carpeta es el "album" tal como lo
        // arma la rocola. El artista, SOLO si viene en la etiqueta del MP3 (Track.SoloArtistaGenero: nunca se
        // adivina del nombre); sin etiqueta, o con comodines como "Various Artists", queda en 0 = no se sabe.
        //
        // Se escribe DESPUES de que los ejemplos llegaron al disco. Si se corta la luz entre las dos cosas, esos
        // ejemplos se quedan sin entrada: "sin origen", que el entrenador no usa para validar. Una entrada a
        // medias, con su CRC malo o que no cuadra con los datos, se ignora al leer. Nunca se atribuye mal.
        // Coste: un CRC de lo que ya estaba en memoria y 64 bytes mas por cancion, en el hilo de fondo.
        private const int IaExtractorVersion = 2;   // la geometria de MakePair (ia/contrato_dsee.py, EXTRACTOR)
        private string IaIndexFile() { return RutaDatos("rocola_ia_datos.idx"); }
        private sealed class IaOrigen
        {
            public ulong Cancion, Carpeta, Artista;
            public int Rate, Frame, Canal;
            public string Genero;   // metadata opcional: no es una entrada de la red
        }
        private static IaOrigen IaOrigenDe(string path, double durSec, int rate, int frame, int canal)
        {
            IaOrigen o = new IaOrigen();
            o.Rate = rate; o.Frame = frame; o.Canal = canal;
            o.Artista = 0;   // 0 es "desconocido": nunca se inventa
            try
            {
                string artista, genero;
                Track.SoloArtistaGenero(path, out artista, out genero);
                if (IaArtistaFiable(artista)) o.Artista = IaHuella(artista);
                o.Genero = IaGeneroFiable(genero);
                if (o.Genero.Length == 0) o.Genero = IaGeneroFiable(GeneroInterpretado(path));   // sin etiqueta: el de su disco (GenerosIa)
            }
            catch { }
            try
            {
                long seg = (long)Math.Round(durSec);
                o.Cancion = IaHuella(Path.GetFileName(path) + "|" + seg.ToString(CultureInfo.InvariantCulture));
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                o.Carpeta = string.IsNullOrEmpty(dir) ? 0UL : IaHuella(dir);
            }
            catch { return null; }
            return o;
        }
        // El mismo origen para otro canal de la misma cancion: todo igual salvo el numero de canal (null sigue siendo null).
        private static IaOrigen IaOrigenParaCanal(IaOrigen o, int canal)
        {
            if (o == null) return null;
            if (o.Canal == canal) return o;
            IaOrigen c = new IaOrigen();
            c.Cancion = o.Cancion; c.Carpeta = o.Carpeta; c.Artista = o.Artista; c.Rate = o.Rate; c.Frame = o.Frame; c.Canal = canal; c.Genero = o.Genero;
            return c;
        }
        // Lo que ponen los programas cuando NO saben el artista. Agrupar por eso juntaria canciones que no
        // tienen nada que ver (y una recopilacion entera "Various Artists" son artistas distintos).
        private static readonly string[] IaArtistasComodin = { "unknown", "unknown artist", "various", "various artists",
            "va", "v.a.", "varios", "varios artistas", "desconocido", "artista desconocido", "sin artista", "<unknown>" };
        private static bool IaArtistaFiable(string a)
        {
            if (string.IsNullOrEmpty(a)) return false;
            string n = a.Trim().ToLowerInvariant();
            if (n.Length == 0) return false;
            for (int i = 0; i < IaArtistasComodin.Length; i++) if (n == IaArtistasComodin[i]) return false;
            return true;
        }
        private static ulong IaHuella(string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s.ToLowerInvariant());
            ulong h = 14695981039346656037UL;
            unchecked { for (int i = 0; i < b.Length; i++) { h ^= b[i]; h *= 1099511628211UL; } }
            return h == 0 ? 1UL : h;
        }
        // Sidecar opcional: preserva RIAX/64 y los datos existentes. Solo huella y etiqueta fiable;
        // sirve para auditar cobertura por genero, nunca para partir una misma cancion entre conjuntos.
        private void IaApuntaGeneros(List<IaOrigen> origenes, bool datosNuevos)
        {
            try
            {
                string ruta = RutaDatos("rocola_ia_datos.generos.tsv");
                if (datosNuevos && File.Exists(ruta))
                {
                    string aparte = ruta + ".anterior";
                    if (File.Exists(aparte)) aparte += "-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                    File.Move(ruta, aparte);
                }
                // El salto inicial separa una ultima linea interrumpida por un apagon; la lectura ignora vacias.
                StringBuilder lineas = new StringBuilder();
                for (int k = 0; k < origenes.Count; k++)
                    lineas.Append("\n").Append(origenes[k].Cancion.ToString("x16", CultureInfo.InvariantCulture)).Append("\t").Append(IaGeneroFiable(origenes[k].Genero)).Append("\n");
                byte[] texto = Encoding.UTF8.GetBytes(lineas.ToString());
                using (FileStream fs = new FileStream(ruta, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    fs.Write(texto, 0, texto.Length); fs.Flush();
                    try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                }
            }
            catch { }   // No poder escribir metadata no invalida pares/index ya verificados.
        }
        // LOS GENEROS INTERPRETADOS, TAMBIEN PARA LA IA (13-10-2026). Una cancion sin genero en la etiqueta no tiene por
        // que quedarse sin el: la rocola ya deduce el de su disco (GeneroDeCancion: lo que vota el paseo de fondo con las
        // etiquetas de las demas canciones del disco, lo del artista, o lo que dice el nombre del disco o de la carpeta).
        // Es la misma pastilla que se ve en pantalla. El material de la IA lo apunta ahora en rocola_ia_datos.generos.tsv
        // para esas canciones, y el modelo del DSEE de ese genero se usa al sonar (ModeloDseePara): las dos cosas con el
        // mismo genero. Las etiquetas de verdad siguen mandando: lo interpretado solo entra donde no hay etiqueta fiable.
        //
        // Esos datos son del hilo de la pantalla y el material se junta en el de fondo, asi que se deja una COPIA (GenerosIa)
        // que nadie toca despues: por carpeta de la cancion, y por la huella de las canciones sin etiqueta con su duracion
        // ya medida (para completar el material juntado antes: CompletaGenerosIa).
        private static uint[] iaCrcTabla;
        private static uint IaCrc32(byte[] d, int off, int n)
        { return IaCrc32Acumula(0xFFFFFFFFu, d, off, n) ^ 0xFFFFFFFFu; }
        private static uint IaCrc32Acumula(uint crc, byte[] d, int off, int n)
        {
            uint[] t = iaCrcTabla;
            if (t == null)
            {
                t = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++) c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                    t[i] = c;
                }
                iaCrcTabla = t;
            }
            for (int i = 0; i < n; i++) crc = t[(crc ^ d[off + i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }
        private static void IaPon(byte[] e, int off, long v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 8); }
        private static void IaPon(byte[] e, int off, int v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 4); }
        private static void IaPon(byte[] e, int off, uint v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 4); }
        private static void IaPon(byte[] e, int off, ulong v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 8); }
        // Solo se llama con iaFileLock tomado (desde IaAppendPairsLote). Las entradas de varios lotes (los canales de una cancion) en orden, con UNA
        // confirmacion del indice; despues sus lineas de genero, con UNA confirmacion. Cada entrada es exactamente la de siempre.
        private void IaApuntaOrigenes(List<int> cuales, List<float>[] lotes, long[] primeros, uint[] crcs, IaOrigen[] origenes, bool[] nuevos)
        {
            try
            {
                string idx = IaIndexFile();
                bool datosNuevos = false;
                for (int k = 0; k < cuales.Count; k++) datosNuevos |= nuevos[cuales[k]];
                if (datosNuevos && File.Exists(idx))
                {
                    // El archivo de datos empieza de cero: el indice que habia describia OTRO archivo. Se aparta,
                    // no se borra (son datos del dueno), y se empieza uno nuevo.
                    string aparte = idx + ".anterior";
                    if (File.Exists(aparte)) aparte = idx + ".anterior-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                    File.Move(idx, aparte);
                }
                using (FileStream fs = new FileStream(idx, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    byte[] cab = new byte[16];
                    if (fs.Length == 0)
                    {
                        cab[0] = (byte)'R'; cab[1] = (byte)'I'; cab[2] = (byte)'A'; cab[3] = (byte)'X';
                        IaPon(cab, 4, 1); IaPon(cab, 8, 64); IaPon(cab, 12, 0);
                        fs.Write(cab, 0, 16);
                    }
                    else
                    {
                        // Un archivo que no es un indice nuestro no se toca: esos ejemplos quedan sin origen.
                        if (fs.Length < 16 || fs.Read(cab, 0, 16) != 16 || cab[0] != (byte)'R' || cab[1] != (byte)'I'
                            || cab[2] != (byte)'A' || cab[3] != (byte)'X' || BitConverter.ToInt32(cab, 4) != 1
                            || BitConverter.ToInt32(cab, 8) != 64) return;
                    }
                    long enteras = (fs.Length - 16) / 64, valido = 16 + enteras * 64;
                    if (fs.Length != valido) fs.SetLength(valido);   // una entrada a medias de un apagon
                    fs.Position = valido;
                    byte[] e = new byte[64];
                    for (int k = 0; k < cuales.Count; k++)
                    {
                        int i = cuales[k]; IaOrigen o = origenes[i];
                        IaPon(e, 0, primeros[i]); IaPon(e, 8, lotes[i].Count / 40); IaPon(e, 12, IaExtractorVersion);
                        IaPon(e, 16, o.Rate); IaPon(e, 20, o.Frame); IaPon(e, 24, o.Canal); IaPon(e, 28, crcs[i]);
                        IaPon(e, 32, o.Cancion); IaPon(e, 40, o.Carpeta); IaPon(e, 48, o.Artista); IaPon(e, 56, 0);
                        IaPon(e, 60, IaCrc32(e, 0, 60));
                        fs.Write(e, 0, 64);
                    }
                    fs.Flush();
                    try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                }
                List<IaOrigen> paraGeneros = new List<IaOrigen>();
                for (int k = 0; k < cuales.Count; k++) paraGeneros.Add(origenes[cuales[k]]);
                IaApuntaGeneros(paraGeneros, datosNuevos);
            }
            catch { }   // sin entrada, esos ejemplos quedan "sin origen": el entrenador no los usa para validar
        }

        // ==========  RECONSTRUCTOR ESPECTRAL DE AGUDOS (el DSEE "serio"; NIVEL 2)  ==========
        // Trabaja como el DSEE de Sony pero sin red neuronal: parte el audio en ventanas (STFT de 2048 puntos,
        // solape 50%, ventana raiz-de-Hann en analisis Y sintesis: con el espectro intacto la reconstruccion
        // es EXACTA), LOCALIZA el "acantilado" donde el codec corto los agudos y RE-CREA la banda faltante
        // transponiendola UNA OCTAVA hacia arriba (bin destino t <- bin fuente t/2, como el SBR de HE-AAC),
        // con la magnitud EMPATADA en la costura y decayendo ~9 dB/octava (como decae la musica real). La fase
        // se hereda de la fuente (suena natural, no metalico). Archivos ya completos (WAV / 320kbps, corte
        // >= ~19.3 kHz) pasan INTACTOS: sin acantilado no se toca ni un bin. Costo: ~3-5% de CPU (4 FFT de
        // 2048 por ciclo de ~23ms en estereo); por eso SOLO se ofrece si la maquina cumple requisitos
        // (ProbeAudioTier). Active se alterna EN VIVO desde Ctrl+F3 para comparar A/B (al alternar hay un
        // microcorte de ~46ms: es el colchon del STFT llenandose; normal y esperado).

        // Red neuronal chica cargada de un archivo RIA1 (capas de pesos+sesgos, ocultas con tanh y la de salida
        // lineal). La usan DOS cosas distintas, cada una con su modelo y su contrato de entradas/salidas: el DSEE
        // para la envolvente de los agudos que re-crea, y el EQ automatico para su objetivo por cancion.
        // Los dos modelos. El del DSEE re-crea la envolvente de los agudos (28 datos de la ventana -> 12 sub-bandas).
        // El del EQ mira la parte del espectro que los codecs NO tocan (el cuerpo, 158 Hz - 5.6 kHz) y dice cuanto
        // grave y cuanto aire tendria una cancion BIEN GRABADA con ese mismo cuerpo: 5 entradas -> 3 salidas.

        // de Form1.Biblioteca.cs y AudioControles.cs (lo que la medida del repaso necesita)
        private volatile bool preScanStop;
        private const double PreSegSec = 20.0;    // duracion de cada tramo analizado
        private const int PreSegs = 3;            // cuantos tramos por cancion
        [ThreadStatic] private static bool integradaLista;
        [ThreadStatic] private static SonoridadIntegrada integradaValor;
        private static void HuellaDelTrozo(float[] buffer, int cuadros, int channels, float[][] coefficients, float[] state, double[] sums, long contados)
        { HuellaDelTrozoDesde(buffer, 0, cuadros, channels, coefficients, state, sums, contados); }
        private static void SonoridadDelTrozo(float[] buf, int cuadros, int ch, float[] c1, float[] c2, float[] st,
                                              ref double sumSq, ref long nSamp, ref long clipN, int[] clipRun, long contados)
        {
            const int m = Desnormales.Cada - 1;
            float p0 = c1[0], p1 = c1[1], p2 = c1[2], p3 = c1[3], p4 = c1[4];
            float q0 = c2[0], q1 = c2[1], q2 = c2[2], q3 = c2[3], q4 = c2[4];
            double suma = sumSq; long muestras = nSamp, recortes = clipN;
            for (int f = 0, i = 0; f < cuadros; f++, i += ch)
            {
                if (((contados + f + 1) & m) == 0) Desnormales.Limpia(st);
                for (int c = 0; c < ch; c++)
                {
                    int b = c * 8; float x = buf[i + c];
                    float y1 = p0 * x + p1 * st[b] + p2 * st[b + 1] - p3 * st[b + 2] - p4 * st[b + 3];
                    st[b + 1] = st[b]; st[b] = x; st[b + 3] = st[b + 2]; st[b + 2] = y1;
                    float y2 = q0 * y1 + q1 * st[b + 4] + q2 * st[b + 5] - q3 * st[b + 6] - q4 * st[b + 7];
                    st[b + 5] = st[b + 4]; st[b + 4] = y1; st[b + 7] = st[b + 6]; st[b + 6] = y2;
                    suma += y2 * y2; muestras++;
                    float ax = x < 0f ? -x : x;
                    if (ax >= 0.985f) { clipRun[c]++; if (clipRun[c] == 3) recortes += 3; else if (clipRun[c] > 3) recortes++; }
                    else clipRun[c] = 0;
                }
            }
            sumSq = suma; nSamp = muestras; clipN = recortes;
        }
        private void LoadIaDone()
        {
            try { ReadIaDone(IaDoneFile(), iaDone); } catch { }
        }

        private static void ReadIaDone(string f, Dictionary<string, int> iaDone)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadIaDoneRecords(reader, iaDone);
            }
            catch { }
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadIaDoneRecords(TextReader reader, Dictionary<string, int> iaDone)
        {
            Dictionary<string, int> pending = new Dictionary<string, int>(iaDone.Comparer);
            FillIaDoneRecords(reader, pending, iaDone);
            foreach (KeyValuePair<string, int> item in pending) iaDone[item.Key] = item.Value;
        }

        private static void FillIaDoneRecords(TextReader reader, Dictionary<string, int> pending, Dictionary<string, int> iaDone)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string r = line.Trim(); if (r.Length == 0) continue;
                int niv = 1;
                if (r.Length > 2 && r[1] == '|') { int d = r[0] - '0'; if (d >= 1 && d <= 9) { niv = d; r = r.Substring(2); } }
                // Si una cancion aparece dos veces manda la de MAS VALOR (ver IaRango): normalmente es
                // la ultima, porque el archivo se escribe anadiendo al final, pero lo que decide es el
                // valor y no la posicion. Asi un archivo escrito a medias o desordenado no puede hacer
                // que se vuelva a cosechar algo ya cosechado.
                r = Pool(r);
                int ant; if ((pending.TryGetValue(r, out ant) || (iaDone != null && iaDone.TryGetValue(r, out ant))) && IaRango(ant) >= IaRango(niv)) continue;
                pending[r] = niv;
            }
        }
        // Apunta que de esta cancion ya se cosecho hasta "nivel". false si ya estaba en ese nivel o mas.
        private bool MarkIaDone(string path, int nivel)
        {
            lock (iaDoneLock)
            {
                int ant; if (iaDone.TryGetValue(path, out ant) && IaRango(ant) >= IaRango(nivel)) return false;
                iaDone[path] = nivel;
                iaPend.Add((nivel > 1 ? (nivel.ToString() + "|") : "") + path);
            }
            iaDoneDirty = true;
            return true;
        }
        // Se ANADE al final en vez de reescribir el archivo entero. Con cien mil canciones, reescribirlo cada
        private void SaveIaDone()
        {
            List<string> nuevas;
            lock (iaDoneLock)
            {
                if (iaPend.Count == 0 || iaDoneSaving) return;
                nuevas = new List<string>(iaPend); iaDoneSaving = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                bool ok = false;
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    AnexaSeguro(IaDoneFile(), sb.ToString());
                    ok = true;
                }
                catch { }
                finally
                {
                    lock (iaDoneLock)
                    {
                        if (ok) RetiraPrefijoGuardado(iaPend, nuevas);
                        iaDoneSaving = false; iaDoneDirty = iaPend.Count > 0;
                    }
                }
            });
        }

        // de Sonoridad.cs y Form1.Biblioteca.cs de XP: lo que MeasureFile dejo de la cancion entera, y la marca de «repasada a fondo»
        private static bool TomaIntegrada(out SonoridadIntegrada s)
        {
            s = integradaValor; bool hay = integradaLista;
            integradaLista = false; integradaValor = new SonoridadIntegrada();
            return hay && s.Valida;
        }
        private void MarcaAFondo(string path)
        {
            lock (deepDoneLock) { if (!deepDone.Add(path)) return; deepPend.Add(path); }
            deepDoneDirty = true;
        }
        // Lo que dice la tarjeta de la biblioteca del material (IaInfoText de XP)
        public string TextoMaterialIa { get { return iaHarvestOn ? "Material para la IA: " + IaInfoText() + "." : "Material para la IA: apagado (lo demas se sigue midiendo igual)."; } }
        public bool JuntaMaterialIa { get { return iaHarvestOn; } }

        // PRUEBA (rocola-consola --mide-repaso archivo...): cuanto tarda cada forma de medir una cancion. No publica nada.
        public string PruebaRepaso(string ruta)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            double tD; using (LectorMedida rd = new LectorMedida(ruta)) { float[] b = new float[16384]; string vol = Environment.GetEnvironmentVariable("ROCOLA_VUELCA"); System.IO.FileStream fv = vol != null ? System.IO.File.Create(vol) : null; int k; while ((k = rd.Read(b, 0, b.Length)) > 0) { if (fv != null) { byte[] bb = new byte[k * 4]; Buffer.BlockCopy(b, 0, bb, 0, bb.Length); fv.Write(bb, 0, bb.Length); } } if (fv != null) fv.Dispose(); tD = sw.Elapsed.TotalMilliseconds; }
            sw.Restart();
            SonoridadIntegrada si; bool okI = MideIntegrada(ruta, out si); double tI = sw.Elapsed.TotalMilliseconds;
            bool antes = iaHarvestOn; float rms, dur; float[] eq; SonoridadIntegrada e;
            iaHarvestOn = false; sw.Restart(); bool okA = MeasureFile(ruta, true, out rms, out dur, out eq); double tA = sw.Elapsed.TotalMilliseconds; TomaIntegrada(out e);
            lock (iaDoneLock) iaDone.Remove(ruta);
            iaHarvestOn = true; sw.Restart(); bool okB = MeasureFile(ruta, true, out rms, out dur, out eq); double tB = sw.Elapsed.TotalMilliseconds; TomaIntegrada(out e);
            iaHarvestOn = antes;
            return string.Format(CultureInfo.InvariantCulture, "{0}: dura {1:0.0} s | solo descomprimir {9:0} ms | solo volumen {2:0} ms ({3}) | completa sin IA {4:0} ms ({5}) | completa con IA {6:0} ms ({7}) | corte {8:0} Hz\n   medida: {10:0.000} LUFS, pico {11:0.000}, P99 {12:0.000}, P90 {13:0.000} dBFS, LRA {14:0.00}, bloques {15}, inicio {16:0.00} fin {17:0.00} s, zumbido {18}, pre-eco {19:0.0} ms/{20}, rms {21:0.00000}, eq[0..3] {22}",
                Path.GetFileName(ruta), dur, tI, okI, tA, okA, tB, okB, eq != null && eq.Length > EqCut ? eq[EqCut] : 0f, tD,
                si.Lkfs, si.PicoDb, si.P99Db, si.P90Db, si.Lra, si.Bloques, si.InicioUtil, si.FinUtil, si.ZumbidoHz, si.PreEcoMs, si.PreEcoGolpes, rms,
                eq == null ? "-" : string.Join(" ", Array.ConvertAll(eq.Length > 4 ? new[] { eq[0], eq[1], eq[2], eq[3] } : eq, v => v.ToString("0.000", CultureInfo.InvariantCulture))));
        }
    }
}

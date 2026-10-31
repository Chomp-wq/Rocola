// LO QUE LA CADENA NECESITA DE LA VENTANA DE XP (campos, medidas, tablas), sacado tal cual de Rocola/ (cada trozo dice de donde).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // de AudioControles.cs
        // Potencia media de los canales, NO potencia de su mezcla: evita cancelar
        // instrumentos abiertos/en contrafase. Estados independientes; cero reservas.
        private static void AccumulateEqFrame(float[] buffer, int offset, int channels,
            float[][] coefficients, float[] state, double[] sums)
        {
            double weight = 1.0 / channels;
            for (int c = 0; c < channels; c++)
            {
                float input = buffer[offset + c];
                for (int b = 0; b < 8; b++)
                {
                    float[] cf = coefficients[b]; if (cf == null) continue;
                    int q = c * 32 + b * 4;
                    float output = cf[0]*input + cf[1]*state[q] + cf[2]*state[q+1] - cf[3]*state[q+2] - cf[4]*state[q+3];
                    state[q+1]=state[q]; state[q]=input; state[q+3]=state[q+2]; state[q+2]=output;
                    sums[b] += (output * output) * weight;
                }
            }
        }
        // de AfinarLocal.cs
        // ¿Se usa la afinacion ahora? Solo con el modo billar, el interruptor y las medidas de la voz.
        private static bool AfinadaActiva { get { return localAuto && afinadoUsar && CorreccionAfinada(AfinadoPromedio(), afinadoPonderacion) != null; } }
        // de Form1.Biblioteca.cs
        private static bool AllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++) { char c = s[i]; if (c != ' ' && (c < '0' || c > '9')) return false; }
            return true;
        }
        // de LoQueSuena.cs
        // Sobre lo ya puesto (ApplyNormNow / AplicaAPreparada): con «sin mejoras», todo lo que mejora a cero; sin el, las
        // etapas sin valor propio en esas dos (pases y sala) vuelven a lo suyo.
        private void AplicaSinMejoras(CadenaCancion c)
        {
            if (c == null) return;
            bool sin = sinMejoras;
            if (c.Arreglo != null) c.Arreglo.Pasa = sin;
            if (c.Zumbido != null) c.Zumbido.Pasa = sin;
            if (c.PreEco != null) c.PreEco.Pasa = sin || !quitaPreEco;   // R2: el interruptor apagado tambien la deja pasar
            if (c.Eses != null) c.Eses.Pasa = sin;
            if (c.Declip != null) c.Declip.Pasa = sin;
            if (c.Siseo != null) c.Siseo.Pasa = sin;
            AjusteLocal lo = LocalDe(c);
            if (c.Sala != null) { c.Sala.Ajusta(sin ? false : lo.Claridad, sin ? false : lo.Suaves, balanceDb); c.Sala.AjustaTono(sin ? 0 : lo.Graves, sin ? 0 : lo.Agudos); }
            if (c.Presencia != null) c.Presencia.Ajusta(sin ? false : lo.Presencia);
            AjustaVozGolpe(c);   // bombo (se queda), retumbe y ataque (se quitan) - VozGolpeYRetumbe.cs
            if (!sin) return;
            if (c.Tap != null) { c.Tap.Excite = 0f; c.Tap.EqDb = EqPlana; }
            if (c.Bajo != null) c.Bajo.Amount = 0f;
            if (c.Sbr != null) c.Sbr.Active = false;
        }
        // de Form1.cs
        // CUANTO bajo profundo le toca de partida a ESTA cancion cuando el bajo se deja en AUTOMATICO. Sale de
        // lo que el analisis del DSEE ya sabe del archivo, no de una perilla:
        //   - Si viene RECORTADA (masterizada a tope), meterle mas grave solo hace trabajar al limitador y
        //     emborrona: se le baja.
        //   - Si el DSEE le esta RECONSTRUYENDO los agudos, es una copia vieja y pobre; esas casi siempre estan
        //     tambien flojas de abajo y es justo donde el bajo psicoacustico mas se agradece: se le sube.
        // Encima de esto sigue actuando BassFactorFor, que mira cuanto grave trae de verdad frente al resto de
        // la biblioteca. Entre los dos, cada cancion recibe lo suyo sin que nadie toque nada.
        private float BajoAuto(string path)
        {
            float bas = 0.30f;   // punto de partida: el mismo que traia la perilla de fabrica
            float[] c = null;
            if (path != null) eqCache.TryGetValue(path, out c);
            if (c != null && c.Length >= EqLen)
            {
                if (c[EqClip] > 0.0015f) bas *= 0.75f;                          // picos aplastados: menos
                if (c[EqCut] > 100f && c[EqCut] < 16000f) bas *= 1.20f;         // agudos cortados: copia pobre, mas
            }
            return Clampf(bas, 0f, 1f);
        }
        // de AfinarLocal.cs
        private static float BajoMedido { get { return AfinadaActiva ? FactorBajoMedido(AfinadoPromedio(), afinadoPonderacion) : 1f; } }
        // de Form1.Sonido.cs
        // Lo mismo para una cancion que aun no suena (la siguiente, preparada para entrar sin pausa).
        private float BassAmountPara(float factor, float autoBase) { return Clampf((bassAuto || localAuto ? autoBase : bassAmount) * factor * BajoMedido, 0f, 1f); }
        // de Form1.Sonido.cs
        // CUANTO bajo psicoacustico le toca a ESTA cancion (multiplica a la perilla). Sale de la misma huella
        // tonal que ya se le mide a cada cancion: la banda de 50 Hz es el grave profundo que las bocinas del
        // local no dan (lo que hay que rescatar) y la de 158 Hz es donde caen los armonicos que generamos (si
        // ahi ya viene lleno, meter mas solo emborrona). Se compara contra el PROMEDIO de la biblioteca, asi
        // que se calibra solo al gusto musical del local: la cancion promedio recibe exactamente lo que dice
        // la perilla, la mas grave hasta un 55% mas y la que no lo necesita hasta un 55% menos.
        // Sin huella todavia (cancion nueva) o con menos de 10 canciones medidas: 1, o sea como toda la vida.
        private float BassFactorFor(string path)
        {
            float[] c = null;
            if (path != null) eqCache.TryGetValue(path, out c);
            float f = 1f;
            // I1 (A la medida, Form1.Datos.cs): con el interruptor y el modelo del EQ, la vara es el objetivo de ESTA
            // cancion (cuanto grave tendria una bien grabada como ella) en vez del promedio de la biblioteca.
            float[] vara = MetaALaMedida(c) ?? eqAvg;
            if (c != null && c.Length >= EqLen && vara != null && c[0] > -25f && c[1] > -25f)
            {
                float rel = (c[0] - vara[0]) - (c[1] - vara[1]);   // dB de grave profundo DE MAS que la cancion tipica
                f = Clampf(1f + rel / 12f, 0.45f, 1.55f);
            }
            // Y ademas: si la GRABACION viene cortada por abajo (muchas viejas no tienen nada bajo 80 Hz), ese
            // grave no esta en el archivo y ninguna bocina lo va a sacar. Ahi el bajo psicoacustico es lo unico
            // que puede devolver la sensacion de fondo, asi que se le da mas. Una que baja a 30 Hz necesita menos.
            if (c != null && c.Length >= EqLen && c[EqCutLo] > 10f)
                f *= Clampf(0.90f + (c[EqCutLo] - 40f) / 110f, 0.90f, 1.35f);
            return Clampf(f, 0.40f, 1.70f);
        }
        // de Form1.Datos.cs
        private static object CandadoDe(string ruta)
        {
            lock (candados)
            {
                object c;
                if (!candados.TryGetValue(ruta, out c)) { c = new object(); candados[ruta] = c; }
                return c;
            }
        }
        // de Form1.Datos.cs
        // Lo mismo con lo que se guardo de una cancion que acabo por relevo sin pausa (Form1.SinPausa.cs).
        private void CierraHuella(string path, double[] sums, long n, int tasa, float corte, float corteBajo)
        {
            if (path == null || sums == null || tasa <= 0 || n < (long)(EqMinSec * tasa)) return;   // 20 s a la frecuencia que sea
            float[] curve = new float[EqLen];
            for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(sums[b] / n + 1e-12));
            // referencia = MEDIANA de las bandas del CUERPO (158 Hz - 5.6 kHz): invariante al volumen y
            // ademas inmune a una banda desbocada (un lodo de +8 dB no arrastra la referencia consigo).
            float[] cuerpo = { curve[1], curve[2], curve[3], curve[4], curve[5] };
            Array.Sort(cuerpo);
            float refDb = cuerpo[2];
            for (int b = 0; b < 8; b++) curve[b] -= refDb;
            float[] prev; bool had = eqCache.TryGetValue(path, out prev);
            if (had && HasStereoEq(path))
            {
                float maxDelta = 0f;
                for (int b = 0; b < 8; b++) { curve[b] = prev[b] * 0.5f + curve[b] * 0.5f; float d = Math.Abs(curve[b] - prev[b]); if (d > maxDelta) maxDelta = d; }
                if (maxDelta < 0.2f && prev[EqCut] > 0f) return;   // ya aprendida y estable: no reescribir el archivo (cero churn)
            }
            // CORTE: lo mide el propio DSEE mientras la cancion suena, este encendido o no (el acantilado es del
            // ARCHIVO, no del efecto). Si no lo supimos esta vez, se conserva el que ya estuviera guardado.
            curve[EqCut] = (corte > 0f) ? corte : (had ? prev[EqCut] : 0f);
            curve[EqCutLo] = (corteBajo > 0f) ? corteBajo : (had ? prev[EqCutLo] : 0f);
            curve[EqClip] = had ? prev[EqClip] : 0f;   // el recorte solo lo mide el pre-analisis (aqui ya viene reparado)
            curve[EqFingerprint] = EqVersionOriginal;   // nunca promediar con huellas mono o post-DSP
            if (!Finitos(curve)) return;   // una medida rota no se guarda (envenenaria la media de la biblioteca)
            eqCache[path] = curve; eqCacheVer++; MarcarEq(path);
            EnsureEqAverage();
            SaveEqCache();
        }
        // de Form1.Sonido.cs
        // Lo mismo con lo que se guardo de una cancion que acabo por relevo sin pausa (Form1.SinPausa.cs).
        private void CierraSonoridad(string path, double sumSq, long samples, int tasa, int canales)
        {
            if (path == null || tasa <= 0 || canales <= 0 || samples < (long)(NormMinSec * tasa * canales)) return;   // por duracion
            float rms = (float)Math.Sqrt(sumSq / samples);
            if (!(rms > 0.0001f) || !Finito(rms)) return;   // (tambien NaN: una medida rota no se guarda)
            float prev; bool had = loudCache.TryGetValue(path, out prev);
            float val = had ? (prev * 0.5f + rms * 0.5f) : rms;   // promedia con la medida anterior: se estabiliza si la cancion se salto en distinto punto
            // Si ya estaba medida y el valor casi no cambia, NO reescribir el archivo: en una biblioteca ya
            // "aprendida" el guardado deja de dispararse en cada cancion (menos escrituras a disco, cero churn).
            if (had && Math.Abs(val - prev) < 0.004f) return;
            loudCache[path] = val;
            MarcarVolumen(path);
            InvalidateVideoMeasurements();
            SaveLoudCache();
        }
        // de Form1.Datos.cs
        // ¿Vale la pena poner el des-recorte en esta cancion? Solo si de verdad viene con picos aplastados. Una
        // cancion aun sin medir se trata como sospechosa (mas vale ponerlo). El umbral es bajo a proposito: con
        // una de cada diez mil muestras recortadas ya se oye la aspereza en un pasaje fuerte.
        private bool DeclipVale(string path)
        {
            float[] c;
            if (path == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen) return true;
            if (c[EqCut] <= 100f) return true;   // ficha vieja o sin analizar: no se sabe, se pone
            return c[EqClip] > 0.0001f;
        }
        // de Form1.Fondo.cs
        internal static float[] EqBpf(float fc, int rate)
        {
            double w = 2.0 * Math.PI * fc / Math.Max(8000, rate);
            if (w >= Math.PI * 0.95) return null;                       // el centro no cabe en este samplerate
            double al = Math.Sin(w) / 4.0, cw = Math.Cos(w), a0 = 1.0 + al;   // Q = 2
            return new float[] { (float)(al / a0), 0f, (float)(-al / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al) / a0) };
        }
        // de Form1.Datos.cs
        // Correccion en dB por banda para una cancion (o null si apagado / aun sin huella / sin objetivo).
        // Correccion = (objetivo - huella) con tope +-4 dB, escalada por la fuerza. Una banda casi MUDA
        // (>25 dB bajo la referencia de su cancion) no se sube: seria inflar piso de ruido, no musica.
        // En el modo billar con la afinacion medida, la del local va encima (AfinarLocal.cs); si no, la de la cancion tal cual.
        private float[] EqCorrFor(string path) { return localAuto ? ConLocalAfinado(EqCorrCancion(path)) : EqCorrCancion(path); }
        // de Form1.Fondo.cs
        // FICHA de cada cancion: 8 bandas de huella tonal en dB (relativas a la mediana de su cuerpo) y tres
        // datos de CALIDAD que salen del mismo analisis y antes se tiraban:
        //   [8] corte de ARRIBA (Hz): donde el codec le talo los agudos. Distingue una cancion sana de una
        //       dañada; decide si sirve para fijar el objetivo del EQ y que bandas tiene sentido corregirle.
        //   [9] RECORTE (0..1): que parte de la cancion viene con los picos aplastados desde la masterizacion.
        //   [10] corte de ABAJO (Hz): donde empieza a haber grave de verdad. Una grabacion vieja cortada en
        //       80 Hz necesita mas ayuda del bajo psicoacustico que una que baja a 30.
        // 0 = todavia no se sabe. Ninguno cuesta un analisis aparte: salen del recorrido que ya se hacia.
        private const int EqLen = 12, EqCut = 8, EqClip = 9, EqCutLo = 10, EqFingerprint = 11;
        // de Form1.Fondo.cs
        internal static float[] EqHighPass(float f0, int rate, float Q)
        {
            double w = 2.0 * Math.PI * f0 / Math.Max(8000, rate);
            double cw = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), a0 = 1.0 + al;
            return new float[] {
                (float)(((1 + cw) / 2.0) / a0), (float)((-(1 + cw)) / a0), (float)(((1 + cw) / 2.0) / a0),
                (float)((-2.0 * cw) / a0), (float)((1 - al) / a0) };
        }
        // de Form1.Datos.cs
        // La huella tonal se mide siempre que haga falta para algo: en automatico hace falta SIEMPRE (de ella
        // salen tambien el bajo, el realce y el informe de calidad), y a mano solo si el EQ esta subido.
        private bool EqMideHuella { get { return eqAuto || eqAmount > 0.001f || localAuto; } }   // el modo billar lleva el EQ en automatico
        // de Form1.Fondo.cs
        // Biquads del K-weighting (BS.1770 / LUFS): un high-shelf que realza agudos (+4 dB) y un high-pass
        // que quita sub-graves. Con ellos, la sonoridad se mide como la OYE el humano. RBJ, a0-normalizado.
        internal static float[] EqShelfHi(float f0, int rate, float dB, float Q)
        {
            double w = 2.0 * Math.PI * f0 / Math.Max(8000, rate);
            double A = Math.Pow(10.0, dB / 40.0), cw = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), tsa = 2.0 * Math.Sqrt(A) * al;
            double a0 = (A + 1) - (A - 1) * cw + tsa;
            return new float[] {
                (float)(A * ((A + 1) + (A - 1) * cw + tsa) / a0),
                (float)(-2.0 * A * ((A - 1) + (A + 1) * cw) / a0),
                (float)(A * ((A + 1) + (A - 1) * cw - tsa) / a0),
                (float)(2.0 * ((A - 1) - (A + 1) * cw) / a0),
                (float)(((A + 1) - (A - 1) * cw - tsa) / a0) };
        }
        // de Form1.Fondo.cs
        // 2: potencia de canales del original, antes de cualquier DSP; 0/1 se migran al medir.
        private const float EqVersionOriginal = 2f;
        // de Form1.Datos.cs
        // Escritura acotada por registros; no publica si falla o queda obsoleta.
        private static bool EscribeFlujoSeguro(string ruta, Func<TextWriter, bool> escribe, Func<bool> vigente)
        {
            lock (CandadoDe(ruta))
            {
                if (vigente != null && !vigente()) return false;
                string tmp = ruta + ".tmp";
                bool creado = false, listo = false;
                try
                {
                    using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        creado = true;
                        using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false), 4096))
                        {
                            if (!escribe(writer) || (vigente != null && !vigente())) return false;
                            writer.Flush(); fs.Flush();
                            try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                        }
                    }
                    if (vigente != null && !vigente()) return false;
                    // Fallo al reemplazar: conservar el temporal COMPLETO para recuperacion, como antes.
                    listo = true;
                    PublicaGuardadoSeguro(ruta, tmp, ruta + ".bak");
                    return true;
                }
                finally { if (creado && !listo) { try { File.Delete(tmp); } catch { } } }
            }
        }
        // de Form1.Datos.cs
        private float ExciteAmountFor(string path) { return Clampf((localAuto ? RealceBillarCantidad : excAmount) * ExciteFactorFor(path), 0f, 1f); }
        // de Form1.Datos.cs
        // CUANTO realce de brillo le toca a ESTA cancion. El EQ no puede devolver una banda que el codec borro
        // -ahi no hay musica que subir-, pero el realce SI: fabrica armonicos a partir de lo que aun queda, asi
        // que cae justo donde el EQ no llega. Por eso una cancion dañada recibe mas realce y una completa menos:
        // esa ya trae su aire de verdad y no hace falta inventarselo.
        // Lo que recibe una cancion que ya trae todo su aire (el suelo de la escala de aqui abajo).
        private const float ExciteCompleta = 0.75f;
        // de NivelDisco.cs
        // La ganancia de nivelado de una cancion en su sitio de la cola: la del disco si esta en racha, si no la suya.
        // Y con el margen que necesita el tono del local que le toca (T1, SalaLocal.MargenTonoDb).
        private float GananciaDe(Track t)
        {
            if (t == null) return 1f;
            List<Track> disco = RachaDeDisco(t);
            float g;
            if (!(disco != null && GananciaDeDisco(disco, t.Path, out g))) g = NormGainFor(t.Path);
            float m = MargenDelTono(t);
            if (localAuto) { float pa = ParejoAplicado(t); if (pa != 0f) m *= DbALin(pa); }   // en el billar, el volumen parejo aprendido (VolumenParejo.cs)
            return m == 1f ? g : g * m;
        }
        // de Form1.Datos.cs
        private void GuardaUltimo(GuardadoReciente slot, Action work)
        {
            lock (slot)
            {
                slot.Pending = work;
                if (slot.Running) return;
                slot.Running = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                while (true)
                {
                    Action next;
                    lock (slot)
                    {
                        next = slot.Pending; slot.Pending = null;
                        if (next == null) { slot.Running = false; return; }
                    }
                    try { next(); } catch { }
                }
            });
        }
        // de AudioControles.cs
        private bool HasStereoEq(string path)
        {
            System.Collections.Generic.Dictionary<string, float[]> eqCache = lecturaPlan != null ? lecturaPlan.Eq : this.eqCache;
            float[] curve;
            return path != null && eqCache.TryGetValue(path, out curve) &&
                curve != null && curve.Length > EqFingerprint && curve[EqFingerprint] == EqVersionOriginal;
        }
        // de AudioControles.cs
        // Lo mismo desde la posicion 'desde' del arreglo (el aprendizaje en vivo de MideBloqueOriginal lee de en medio de un bloque).
        private static void HuellaDelTrozoDesde(float[] buffer, int desde, int cuadros, int channels, float[][] coefficients, float[] state, double[] sums, long contados)
        {
            const int m = Desnormales.Cada - 1;
            if (channels != 1 && channels != 2)
            {
                for (int f = 0; f < cuadros; f++)
                {
                    AccumulateEqFrame(buffer, desde + f * channels, channels, coefficients, state, sums);
                    if (((contados + f + 1) & m) == 0) Desnormales.Limpia(state);
                }
                return;
            }
            double weight = 1.0 / channels;
            for (int b = 0; b < 8; b++)
            {
                float[] cf = coefficients[b]; if (cf == null) continue;
                float k0 = cf[0], k1 = cf[1], k2 = cf[2], k3 = cf[3], k4 = cf[4];
                int qa = b * 4, qb = 32 + b * 4;
                float a0 = state[qa], a1 = state[qa + 1], a2 = state[qa + 2], a3 = state[qa + 3];
                double suma = sums[b];
                if (channels == 1)
                {
                    for (int f = 0; f < cuadros; f++)
                    {
                        float x = buffer[desde + f];
                        float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                        a1 = a0; a0 = x; a3 = a2; a2 = o;
                        suma += (o * o) * weight;
                        if (((contados + f + 1) & m) == 0)
                        { a0 = Desnormales.Limpia(a0); a1 = Desnormales.Limpia(a1); a2 = Desnormales.Limpia(a2); a3 = Desnormales.Limpia(a3); }
                    }
                }
                else
                {
                    float b0 = state[qb], b1 = state[qb + 1], b2 = state[qb + 2], b3 = state[qb + 3];
                    for (int f = 0, i = desde; f < cuadros; f++, i += 2)
                    {
                        float x = buffer[i];
                        float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                        a1 = a0; a0 = x; a3 = a2; a2 = o;
                        suma += (o * o) * weight;
                        float x2 = buffer[i + 1];
                        float o2 = k0 * x2 + k1 * b0 + k2 * b1 - k3 * b2 - k4 * b3;
                        b1 = b0; b0 = x2; b3 = b2; b2 = o2;
                        suma += (o2 * o2) * weight;
                        if (((contados + f + 1) & m) == 0)
                        {
                            a0 = Desnormales.Limpia(a0); a1 = Desnormales.Limpia(a1); a2 = Desnormales.Limpia(a2); a3 = Desnormales.Limpia(a3);
                            b0 = Desnormales.Limpia(b0); b1 = Desnormales.Limpia(b1); b2 = Desnormales.Limpia(b2); b3 = Desnormales.Limpia(b3);
                        }
                    }
                    state[qb] = b0; state[qb + 1] = b1; state[qb + 2] = b2; state[qb + 3] = b3;
                }
                state[qa] = a0; state[qa + 1] = a1; state[qa + 2] = a2; state[qa + 3] = a3;
                sums[b] = suma;
            }
            // Las posiciones del arreglo que no son de ninguna banda con coeficientes (bandas nulas, canales de mas) se
            // limpiaban igual: siguen en cero, que es como empiezan.
        }
        // de AudioControles.cs
        // Cuadros ENTEROS de estereo (llega con el canal 0 por tocar y lo deja en el 0). Los tramos acaban en el cuadro que cuenta 64.
        private void KCuadrosEstereo(float[] buffer, int desde, int cuadros, float[] c1, float[] c2, float[] st, ref double sumK)
        {
            float p0 = c1[0], p1 = c1[1], p2 = c1[2], p3 = c1[3], p4 = c1[4];
            float q0 = c2[0], q1 = c2[1], q2 = c2[2], q3 = c2[3], q4 = c2[4];
            double suma = sumK;
            float lx1 = st[0], lx2 = st[1], ly1 = st[2], ly2 = st[3], lz1 = st[4], lz2 = st[5], lw1 = st[6], lw2 = st[7];
            float rx1 = st[8], rx2 = st[9], ry1 = st[10], ry2 = st[11], rz1 = st[12], rz2 = st[13], rw1 = st[14], rw2 = st[15];
            int f = 0, limpia = kwLimpia;
            while (f < cuadros)
            {
                int fin = f + (Desnormales.Cada - limpia); bool cierra = true;
                if (fin > cuadros) { fin = cuadros; cierra = false; }
                limpia += fin - f;
                for (int i = desde + f * 2; f < fin; f++, i += 2)
                {
                    float x = buffer[i];
                    float y1 = p0 * x + p1 * lx1 + p2 * lx2 - p3 * ly1 - p4 * ly2;
                    lx2 = lx1; lx1 = x; ly2 = ly1; ly1 = y1;
                    float y2 = q0 * y1 + q1 * lz1 + q2 * lz2 - q3 * lw1 - q4 * lw2;
                    lz2 = lz1; lz1 = y1; lw2 = lw1; lw1 = y2;
                    suma += y2 * y2;
                    float x2 = buffer[i + 1];
                    float u1 = p0 * x2 + p1 * rx1 + p2 * rx2 - p3 * ry1 - p4 * ry2;
                    rx2 = rx1; rx1 = x2; ry2 = ry1; ry1 = u1;
                    float u2 = q0 * u1 + q1 * rz1 + q2 * rz2 - q3 * rw1 - q4 * rw2;
                    rz2 = rz1; rz1 = u1; rw2 = rw1; rw1 = u2;
                    suma += u2 * u2;
                }
                if (cierra)
                {
                    limpia = 0;
                    lx1 = Desnormales.Limpia(lx1); lx2 = Desnormales.Limpia(lx2); ly1 = Desnormales.Limpia(ly1); ly2 = Desnormales.Limpia(ly2);
                    lz1 = Desnormales.Limpia(lz1); lz2 = Desnormales.Limpia(lz2); lw1 = Desnormales.Limpia(lw1); lw2 = Desnormales.Limpia(lw2);
                    rx1 = Desnormales.Limpia(rx1); rx2 = Desnormales.Limpia(rx2); ry1 = Desnormales.Limpia(ry1); ry2 = Desnormales.Limpia(ry2);
                    rz1 = Desnormales.Limpia(rz1); rz2 = Desnormales.Limpia(rz2); rw1 = Desnormales.Limpia(rw1); rw2 = Desnormales.Limpia(rw2);
                }
            }
            st[0] = lx1; st[1] = lx2; st[2] = ly1; st[3] = ly2; st[4] = lz1; st[5] = lz2; st[6] = lw1; st[7] = lw2;
            st[8] = rx1; st[9] = rx2; st[10] = ry1; st[11] = ry2; st[12] = rz1; st[13] = rz2; st[14] = rw1; st[15] = rw2;
            kwLimpia = limpia; sumK = suma;
        }
        // de AudioControles.cs
        // LA SONORIDAD K DEL APRENDIZAJE EN VIVO, por trozos (20-10-2026). Es la cuenta de siempre de MideBloqueOriginal -dos biquads por canal, la suma
        // de los cuadrados de la salida del segundo, la limpieza de restos diminutos (Desnormales.cs) detras del cuadro que cuenta 64- en dos formas:
        // muestra a muestra con la memoria en el arreglo (el cuadro partido de los extremos, el mono y los canales de mas) y, para los cuadros
        // enteros de estereo, con coeficientes y memoria en variables. Mismas cuentas en el mismo orden: mismos bits (bateria entradaligera).
        private void KMuestraAMuestra(float[] buffer, int desde, int hasta, int ch3, ref int c, float[] c1, float[] c2, float[] st, ref double sumK, ref long nK)
        {
            double suma = sumK; long cuenta = nK; int canal = c;
            for (int i = desde; i < hasta; i++)
            {
                int b = canal * 8; float x = buffer[i];
                float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                suma += y2 * y2; cuenta++;
                if (++canal >= ch3)
                {
                    canal = 0;
                    if (++kwLimpia == Desnormales.Cada) { kwLimpia = 0; Desnormales.Limpia(st); }   // restos diminutos a cero (Desnormales.cs)
                }
            }
            sumK = suma; nK = cuenta; c = canal;
        }
        // de Form1.Biblioteca.cs
        private static string LeafName(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return "Video";
            string s = folder.TrimEnd('\\', '/');
            string leaf = Path.GetFileName(s);
            return string.IsNullOrEmpty(leaf) ? s : leaf;
        }
        // de Form1.cs
        // Guarda el error completo (tipo + mensaje + pila) en rocola_error.txt junto al .exe,
        // para poder ver el metodo exacto que falta (p. ej. por una version equivocada de NAudio).
        // Cada apunte lleva la fecha del EJECUTABLE al lado (ver el mismo sello en Program.LogCrash): un
        // error que llega por mensaje no se puede diagnosticar si no se sabe de que version salio.
        private void LogError(string context, Exception ex)
        {
            // Lo escribe un hilo de fondo (Bitacora, en MedidasBitacora.cs), que tambien le pone el sello. Tope ~256KB.
            try
            {
                Bitacora.ApuntaConSello(RutaDatos("rocola_error.txt"), 262144, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]",
                                        "\r\n" + (ex != null ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n");
            }
            catch { }
        }
        // de SubirTodo.cs
        // Lo mismo para TODAS las canciones: el volumen sigue parejo.
        private float MasterEfectivo() { return (float)Math.Pow(10.0, SubirAplicadoDb() / 20.0); }
        // de Form1.SinPausa.cs
        // LA MEDIDA EN VIVO EN EL RELEVO. La llama el LevelTap de cada cancion antes de medir su bloque, en el
        // hilo del sonido. Con una sola cancion por salida no hace nada (la medida ya es suya). En el primer bloque
        // de la que entra por relevo, guarda lo medido de la anterior (en memoria ya reservada) y estrena lo suyo.
        private void MedidaDe(LevelTap tap)
        {
            if (object.ReferenceEquals(tap, tapMedido)) return;
            MedidaCancion nueva = tap.Medida;
            if (nueva == null) return;   // un LevelTap armado aparte (las pruebas) mide como siempre
            MedidaCancion.Cierre c = nueva.CierreAnterior;
            c.Ruta = curMeasurePath; c.SumSq = curSumSq; c.Muestras = curSamples; c.Tasa = kwRate; c.Canales = kwCanales;
            c.Fuente = curFuenteMedida;
            c.EqRuta = eqMeasPath; c.EqSumas = eqMeasSum; c.EqN = eqMeasN; c.EqTasa = eqMeasRate;
            curMeasurePath = nueva.Ruta; curSumSq = 0; curSamples = 0; kwRate = 0;   // el K-weighting empieza de cero
            curFuenteMedida = nueva.Fuente as FuenteMedida;
            eqMeasPath = nueva.EqRuta; eqMeasSum = nueva.EqSumas; eqMeasN = 0; eqMeasRate = 0; eqPendN = 0;
            tapMedido = tap;
            c.Listo = true;   // lo ultimo: la pantalla solo lo cierra con todo copiado
        }
        // de Form1.Datos.cs
        // EL OBJETIVO DE ESTA CANCION: cuanto grave (50 Hz) y cuanto aire (10 y 15,8 kHz) tendria una cancion BIEN
        // GRABADA con su mismo cuerpo, segun la red del EQ; las demas bandas, el promedio de la biblioteca. Null si no
        // hay modelo o no hay promedio. Es lo que siempre uso el EQ automatico; desde el 13-10-2026 (I1, "A la medida
        // de cada cancion") lo usan tambien el bajo psicoacustico y el realce. Arreglos propios en cada llamada: la
        // llama la pantalla y tambien quien prepara la siguiente.
        private float[] MetaDeCancion(float[] c)
        {
            float[] avg = eqAvg;
            if (c == null || c.Length < EqLen || avg == null || avg.Length < 8 || !EqNet.Loaded) return null;
            float[] ent = new float[EqNetIn], sal = new float[EqNetOut];
            for (int i = 0; i < EqNetIn; i++) ent[i] = c[i + 1];   // el cuerpo: bandas 1..5
            if (!EqNet.Predict(ent, sal)) return null;
            float[] m = new float[8];
            for (int b = 0; b < 8; b++) m[b] = avg[b];
            for (int i = 0; i < EqNetOut; i++)
            {
                float v = sal[i];
                // Cinturon de seguridad: por muy convencida que este la red, el objetivo no se aleja mas
                // de 6 dB del promedio de la biblioteca. Un modelo mal entrenado no puede desmadrar el EQ.
                float lim = avg[EqNetSalida[i]];
                if (v > lim + 6f) v = lim + 6f; else if (v < lim - 6f) v = lim - 6f;
                m[EqNetSalida[i]] = v;
            }
            return m;
        }
        // de Form1.Ia.cs
        // El modelo del DSEE para esta cancion: el de su genero si el interruptor esta encendido y lo hay; si no, null (el
        // general, como siempre).
        private RedChica ModeloDseePara(Track t)
        {
            if (!dseePorGenero || t == null) return null;
            string slug = SlugGenero(t.Genre);
            if (slug.Length == 0) slug = SlugGenero(GeneroInterpretado(t.Path));   // sin etiqueta: el mismo genero con que se junto su material
            if (slug.Length == 0) return null;
            RedChica r;
            lock (dseeGeneros) if (dseeGeneros.TryGetValue(slug, out r) && r.Loaded) return r;
            return null;
        }
        // de Form1.Ia.cs
        // Red neuronal chica cargada de un archivo RIA1 (capas de pesos+sesgos, ocultas con tanh y la de salida
        // lineal). La usan DOS cosas distintas, cada una con su modelo y su contrato de entradas/salidas: el DSEE
        // para la envolvente de los agudos que re-crea, y el EQ automatico para su objetivo por cancion.
        // Los dos modelos. El del DSEE re-crea la envolvente de los agudos (28 datos de la ventana -> 12 sub-bandas).
        // El del EQ mira la parte del espectro que los codecs NO tocan (el cuerpo, 158 Hz - 5.6 kHz) y dice cuanto
        // grave y cuanto aire tendria una cancion BIEN GRABADA con ese mismo cuerpo: 5 entradas -> 3 salidas.
        private static readonly RedChica NeuralEnv = new RedChica(28, 12);
        // de Optimizaciones.cs
        private static void OlvidaMedidaHecha(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasHechas.Remove(ruta);
        }
        // de Form1.Datos.cs
        // Olvida lo aprendido de una ruta (en el hilo de la pantalla, dueno de las tablas) y lo guarda asi.
        private void OlvidaMedidas(string ruta)
        {
            lock (fuentesCerrojo) { if (fuentesMedidas.Remove(ruta)) { fuentesPendientes[ruta] = null; fuentesCambios++; } }
            loudCache.Remove(ruta); durCache.Remove(ruta); MarcarVolumen(ruta);
            if (eqCache.Remove(ruta)) { eqCacheVer++; eqDirty = true; MarcarEq(ruta); }
            bool integ;
            lock (integCerrojo) integ = integCache != null && integCache.Remove(ruta);
            if (integ) { integCambios++; MarcarIntegrada(ruta); }
            lock (integHechasCerrojo) integHechas.Remove(ruta); // permitir reanalizar el archivo sustituido en esta sesion
            lock (deepDoneLock) deepDone.Remove(ruta);   // la marca del archivo no vale sin sus medidas (FillDeepDoneRecords)
            OlvidaMedidaHecha(ruta);                     // que el repaso y el calentador la puedan volver a medir en esta sesion
            InvalidateVideoMeasurements();
            SaveLoudCache(); SaveIntegCache(); FlushEqIfDirty();
        }
        // de Form1.Datos.cs
        // APUNTAR LO QUE SE ACABA DE MEDIR. UN SOLO SITIO, y por eso vive aqui suelto: lo llaman el
        // pre-analisis y el calentador de la siguiente cancion. Con dos sitios que escriban las mismas tres
        // tablas, el dia que alguien arregle uno se olvidara del otro.
        //
        // SOBRESCRIBE lo que hubiera: estas medidas recorren TODA la cancion -repartida en tramos, o entera si es
        // corta o va a fondo-, asi que son mejores que lo aprendido a medias oyendola (que promediaba entre
        // pasadas). Siguen siendo la ESTIMACION: la medida integrada de la cancion entera va aparte, en
        // PublicaIntegrada (ver Sonoridad.cs).
        //
        // Las tablas son del hilo de la pantalla y no llevan candado -a proposito, para no pagarlo en cada
        // consulta-, asi que quien llame a esto tiene que estar EN ese hilo.
        // OTRO ARCHIVO EN LA MISMA RUTA. Todo lo que la rocola aprende de una cancion (volumen, duracion, huella del EQ,
        // sonoridad integrada con sus picos y su mapa de silencios) se apunta por RUTA. Si en esa ruta hay hoy otro archivo
        // -la cancion completa en lugar de una descarga cortada, otra version con el mismo nombre-, lo apuntado es del de
        // antes: el nivelado, el EQ automatico y el nivel por disco trabajaban con medidas ajenas, y la cancion no se volvia
        // a medir nunca porque ya "estaba medida". Al ponerla se compara lo que dura con lo apuntado; las dos cuentas salen
        // del mismo lector (MeasureFile apunta su TotalTime), asi que en el mismo archivo coinciden. Si no cuadran (un
        // segundo de margen), se olvida todo lo suyo y se vuelve a aprender: oyendola esta misma vez (ArmaCadena mide lo
        // que no tiene duracion apuntada) y en el proximo repaso. Queda apuntado en rocola_error.txt.
        private void OlvidaSiEsOtroArchivo(string ruta, IAudioSource source)
        {
            try
            {
            float apuntada;
            if (ruta == null || source == null || !durCache.TryGetValue(ruta, out apuntada) || !(apuntada > 0.01f)) return;
            double real;
            try { FuenteSinSilencios s = source as FuenteSinSilencios; real = ((s != null) ? s.Interior : source).TotalTime.TotalSeconds; }
            catch { return; }   // si el lector no lo sabe, no se puede decir que sea otro
            if (!(real > 0.05) || Math.Abs(real - apuntada) <= 1.0) return;
            OlvidaMedidas(ruta);
            durFast[ruta] = (float)real;   // lo que dura de verdad, para la lista y la espera de la cola hasta que se vuelva a medir
#if ROCOLA_GPU
            duracionesVer++;
#endif
            LogError("Otro archivo en la misma ruta: " + Path.GetFileName(ruta) + " duraba " + apuntada.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                     + " s y ahora " + real.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s; se vuelve a medir", null);
            }
            finally { CompruebaFuenteMedida(ruta); }
        }
        // de PreEco.cs
        // Si el repaso midio pre-eco en esta cancion (algoritmo 7).
        private bool PreEcoDe(string ruta)
        {
            SonoridadIntegrada s;
            System.Collections.Generic.Dictionary<string, SonoridadIntegrada> t = integCache;
            return ruta != null && t != null && t.TryGetValue(ruta, out s) && s.ConPreEco;
        }
        // de Form1.Datos.cs
        private sealed class ProteccionRespaldo : IDisposable
        {
            private readonly EstadoRespaldo estado;
            private int liberada;
            internal ProteccionRespaldo(EstadoRespaldo estado, string ruta)
            {
                this.estado = estado;
                lock (estado)
                {
                    if (estado.Cambiando) throw new InvalidOperationException("Dos mutaciones del mismo guardado");
                    estado.Anterior = AbreArchivoRespaldo(ruta);
                    estado.Cambiando = true;
                }
            }
            public void Dispose()
            {
                if (System.Threading.Interlocked.Exchange(ref liberada, 1) != 0) return;
                ArchivoRespaldo anterior;
                lock (estado)
                {
                    if (!estado.Cambiando) return;
                    anterior = estado.Anterior; estado.Anterior = null; estado.Cambiando = false;
                }
                if (anterior != null) anterior.Libera();
            }
        }
        // de Form1.Datos.cs
        private static ProteccionRespaldo ProtegeRespaldo(string ruta)
        { return new ProteccionRespaldo(EstadoRespaldoDe(ruta), ruta); }
        // de Sonoridad.cs
        // Apunta una medida integrada. En el hilo de la pantalla, como PublicaMedida. NO toca la cancion que
        // esta sonando: su volumen cambia la proxima vez que suene, nunca a media cancion.
        private void PublicaIntegrada(string ruta, SonoridadIntegrada s)
        {
            if (ruta == null || !s.Valida || integCache == null) return;
            SonoridadIntegrada antes;
            if (integCache.TryGetValue(ruta, out antes) && antes.Algoritmo > s.Algoritmo) return;   // nunca pisar una de algoritmo mas nuevo
            lock (integCerrojo) integCache[ruta] = s;
            MarcarIntegrada(ruta);
            integCambios++;
            IntegradaPuesta(ruta);
            float est;
            if (!s.Muda && loudCache != null && loudCache.TryGetValue(ruta, out est) && est > 0.0001f && s.RmsPorCanal > 0.0001f)
            {
                double d = 20.0 * Math.Log10(s.RmsPorCanal / est);
                integDifSuma += Math.Abs(d); integDifN++;
                if (Math.Abs(d) > Math.Abs(integDifMayor)) integDifMayor = d;
                if ((integDifN % 50) == 0)
                    Diag("sonoridad integrada: " + integDifN + " canciones; la estimacion se aparta en promedio "
                         + (integDifSuma / integDifN).ToString("0.00", CultureInfo.InvariantCulture) + " dB (la que mas, "
                         + integDifMayor.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " dB)");
            }
        }
        // de Form1.Datos.cs
        private void PublicaMedida(string ruta, float rms, float dur, float[] eqCurve)
        {
            if (ruta == null) return;
            bool puesto = false;
            if (dur > 0.01f && Finito(dur))
            {
                durCache[ruta] = dur; puesto = true;
#if ROCOLA_GPU
                duracionesVer++;
#endif
            }
            if (rms > 0f && Finito(rms)) { loudCache[ruta] = rms; InvalidateVideoMeasurements(); puesto = true; }   // incluido el silencio (ver LoudMudo)
            if (puesto) MarcarVolumen(ruta);   // captura juntos RMS + duracion, aunque solo cambio uno
            if (eqCurve != null && Finitos(eqCurve)) { eqCache[ruta] = eqCurve; eqCacheVer++; eqDirty = true; puesto = true; MarcarEq(ruta); }   // huella tonal del EQ automatico
            // Y se apunta en la libreta compartida, para que nadie la vuelva a medir en esta sesion. Solo
            // si de verdad quedo algo escrito: un archivo roto no da nada, y darlo por medido seria no
            // volver a intentarlo nunca.
            if (puesto && rms > 0f && Finito(rms) && dur > 0.01f && Finito(dur)) MedidaPuesta(ruta);
            if (panelSongs != null && panelSongs.Visible) RefrescaLista();   // que aparezca la duracion recien sabida
        }
        // de NivelDisco.cs
        // Las pistas del disco de 't' si suena en racha con otra de su disco (la de antes o la de despues); null si no.
        private List<Track> RachaDeDisco(Track t)
        {
            PlayItem np = nowPlaying, yo, antes, despues;
            if (np != null && object.ReferenceEquals(np.Song, t)) { yo = np; antes = sonoAntes; despues = queue.Count > 0 ? queue[0] : null; }
            else if (queue.Count > 0 && queue[0] != null && object.ReferenceEquals(queue[0].Song, t)) { yo = queue[0]; antes = np; despues = queue.Count > 1 ? queue[1] : null; }
            else return null;
            if (yo.IsVideo || t.Path == null) return null;
            string carpeta = CarpetaDe(t.Path);
            if (string.IsNullOrEmpty(carpeta) || !(MismaCarpeta(antes, carpeta) || MismaCarpeta(despues, carpeta))) return null;   // sin carpeta no hay disco
            return PistasDeCarpeta(yo.Disc, carpeta);
        }
        // de Form1.Datos.cs
        private static string RutaDatos(string nombre) { return Path.Combine(CarpetaDatos, nombre); }
        // de AperturaAudio.cs
        private string RutaOriginalDe(string ruta) { return ruta; }   // sin Apollo, el archivo que suena es la cancion
        // de Form1.Biblioteca.cs
        // CUANTO DURA UNA CANCION, Y EL UNICO SITIO QUE LO SABE.
        //
        // Manda la del analisis completo (que midio la cancion entera); si aun no la tiene, la del
        // barrido rapido. Devuelve 0 cuando no se sabe, y eso NO es lo mismo que cero segundos: quien
        // pregunta tiene que mirarlo y callarse en vez de inventar.
        //
        // Y aqui NO se le pregunta al archivo. El descompresor no sabe cuanto dura un MP3 hasta
        // recorrerlo entero cuando no trae cabecera Xing -medido: 3.87 ms de procesador en una cancion
        // de 4 minutos, mas la lectura de sus 9 MB del disco-. Multiplicado por toda la cola, y justo
        // en el instante en que alguien acaba de pulsar un boton. La duracion ya esta apuntada.
        private float SegundosDe(string path)
        {
            float d;
            if (path == null) return 0f;
            if (durCache.TryGetValue(path, out d) && d > 0.01f) return d;
            if (durFast.TryGetValue(path, out d) && d > 0.01f) return d;
            return 0f;
        }
        // de Form1.Biblioteca.cs
        private static readonly char[] SepChars = { '\\', '/' };
        // de Sonoridad.cs
        // LA SONORIDAD CON LA QUE SE NIVELA, en la escala de siempre (la raiz de la potencia K media por
        // canal): la integrada si esta; si no, la estimacion. Falso = no hay nada util (o es silencio).
        private bool SonoridadParaNivelar(string path, out float rms)
        {
            rms = 0f;
            if (path == null) return false;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            SonoridadIntegrada s;
            if (t != null && t.TryGetValue(path, out s) && s.Valida)
            {
                rms = s.Muda ? LoudMudo : s.RmsPorCanal;
                return rms > 0.0001f;
            }
            Dictionary<string, float> l = loudCache;
            return l != null && l.TryGetValue(path, out rms) && rms > 0.0001f;
        }
        // de Form1.Biblioteca.cs
        // Separa "Artista - Titulo" o "Artista-Titulo" (guion con o sin espacios). Toma el PRIMER guion.
        private static bool SplitArtist(string s, out string left, out string right)
        {
            left = (s ?? "").Trim(); right = "";
            if (string.IsNullOrEmpty(s)) return false;
            int idx = s.IndexOf(" - ", StringComparison.Ordinal); int len = 3;
            if (idx < 0) { idx = s.IndexOf('-'); len = 1; }
            if (idx <= 0) return false;
            left = s.Substring(0, idx).Trim();
            right = s.Substring(idx + len).Trim();
            return left.Length > 0;
        }
        // de CompararCiegas.cs
        // La comparacion a ciegas (CompararCiegas.cs) aun no esta en Rocola OS: nunca hay una en marcha.
        internal void TerminaCiego(string porQue) { ciegoActivo = false; }
        // de ArreglosGrabacion.cs
        // El zumbido medido de una cancion (50 o 60 Hz), o 0 si no lo trae o aun no se sabe (T6, algoritmo 6 del repaso).
        private int ZumbidoDe(string ruta)
        {
            SonoridadIntegrada s;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            if (ruta == null || t == null || !t.TryGetValue(ruta, out s) || !s.ConZumbido) return 0;
            return s.ZumbidoHz;
        }
        // de BombaSonido.cs
        // Calculo de audio separado de la cola visual: la UI no espera filtros por muestra.
        // Orden unico si se necesitan ambas puertas: audioDatosLock -> levelLock.
        private readonly object audioDatosLock = new object();
        // de Form1.SinPausa.cs
        private CadenaCancion cadenaPreparada;   // la siguiente: lista, o entrando y aun sin recoger
        // de CompararCiegas.cs
        private bool ciegoActivo, ciegoGuardado;
        // de Form1.Sonido.cs
        private float crossfadeSec = 0.5f;   // FUNDIDO de salida entre canciones: segundos de rampa al terminar ("fundido=" en config; 0 = corte seco)
        // de Form1.Fondo.cs
        private string curMeasurePath = null;                                                // cancion que se esta MIDIENDO ahora (null = no medir)
        // de Form1.Fondo.cs
        private double curSumSq = 0; private long curSamples = 0;                             // acumuladores del RMS integrado de la cancion actual (los llena el hilo de audio)
        // de Form1.Sonido.cs
        private bool declipOn = true;        // DES-RECORTE (declipper): repara picos aplastados de origen en archivos mal masterizados ("declip=" en config)
        // de Form1.Datos.cs
        // Canciones que YA recibieron el repaso A FONDO (cancion completa). Se guarda en disco para que el
        // entrenamiento a fondo CONTINUE donde iba al detenerlo y volverlo a encender, al cerrar la rocola o
        // si se va la luz. Sin esto, cada arranque volvia a empezar por la primera cancion de la biblioteca y
        // con una biblioteca grande el repaso no terminaba nunca.
        private HashSet<string> deepDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Datos.cs
        private readonly object deepDoneLock = new object();
        // de Form1.Datos.cs
        // DURACION de cada cancion (segundos). La calcula el PRE-ANALISIS junto con la sonoridad y se guarda en
        // el mismo archivo. Ademas de mostrarse en la lista, sirve de marca de "ya pre-analizada": esas canciones
        // ya tienen una medida COMPLETA y fiable, asi que sonar la cancion no vuelve a tocar su volumen medido.
        private Dictionary<string, float> durCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);     // ruta -> duracion en segundos
        // de Form1.Sonido.cs
        private float[] eqAvg = null;              // curva PROMEDIO de la biblioteca (el objetivo); null hasta tener 10 canciones medidas
        // de Form1.Sonido.cs
        private Dictionary<string, float[]> eqCache = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Sonido.cs
        private float[][] eqMeasCoef = null; private float[] eqMeasSt = null; private int eqMeasRate = 0;   // medicion: 4 estados por banda y canal
        // de Form1.Sonido.cs
        private string eqMeasPath = null;          // cancion cuya huella se esta midiendo (null = no medir)
        // de Form1.Sonido.cs
        private double[] eqMeasSum = new double[8]; private long eqMeasN = 0;   // energia acumulada por banda (hilo de audio)
        // de Form1.Sonido.cs
        private float[] eqPend; private int eqPendN;   // un cuadro que una lectura dejo partido, para completarlo con la siguiente
        // de Sonoridad.cs
        private Dictionary<string, SonoridadIntegrada> integCache = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
        // de Sonoridad.cs
        private long integCambios;           // cuantas veces cambio la tabla (hilo de la pantalla)
        // de Sonoridad.cs
        private static readonly HashSet<string> integHechas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // de Sonoridad.cs
        // Libreta de la sesion: lo que ya tiene integrada, para que la tanda larga no la repita si otro
        // (el calentador de la siguiente, o el repaso normal en una cancion corta) la midio mientras tanto.
        private static readonly object integHechasCerrojo = new object();
        // de Form1.Fondo.cs
        private int kwCanal, kwCanales;                                                       // canal que toca en el K-weighting en vivo (sobrevive a las lecturas partidas) y cuantos hay
        // de Form1.Fondo.cs
        private float[] kwCoef1, kwCoef2; private float[] kwSt; private int kwRate = 0;   // K-weighting del nivelado (2 biquads/canal, 8 estados)
        // de Form1.Sonido.cs
        private Dictionary<string, float> loudCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);   // ruta -> ESTIMACION de sonoridad (tramos u oida); la integrada BS.1770 va en integCache
        // de Form1.Fondo.cs
        // "SUBIR EL PISO": las canciones BAJITAS se SUBEN hacia un volumen objetivo; las que ya suenan FUERTE
        // NO se bajan (se prioriza subir las flojas, como pidio el operador). Desde el 28/09 las muy fuertes si se bajan
        // un poco, pedido tambien por el operador (ver GananciaNivelado en Sonoridad.cs). Ajustable desde la interfaz (Ctrl+F3).
        //   ganancia = (objetivo / rms) ^ fuerza , recortada a [1 , tope]   (fuerza 0..1; nunca por debajo de 1 = no baja)
        private float normTarget = 0.26f;                                                     // volumen OBJETIVO (RMS lineal) al que se suben las bajitas; mas alto = todo mas fuerte
        // de Form1.Fondo.cs
        // ---- NIVELADO DE VOLUMEN (que todas las canciones suenen PAREJO) ----
        // Cada cancion viene masterizada a distinto volumen. Aqui se mide la sonoridad (RMS integrado) de cada
        // cancion MIENTRAS suena (aprovechando la medicion que ya se hace para el beat) y se guarda por ruta en
        // rocola_volumen.txt. En reproducciones siguientes se aplica una ganancia para llevarla a un nivel objetivo
        // COMUN, con recorte suave de picos. Asi, tras oirse una vez, cada cancion suena al mismo volumen que las
        // demas. La PRIMERA vez que se oye una cancion nueva va a su volumen original (aun no hay medida): se
        // aprende en esa pasada y ya queda pareja para siempre. Se puede apagar con normalizar=0 en rocola_config.txt.
        private bool normalizeOn = true;                                                     // nivelado activo (por defecto si)
        // de CompararCiegas.cs
        private bool presenciaSinGrave = true;   // la presencia del billar mide sin el grave (P1); false solo en un tramo "de antes"
        // de Form1.cs
        private readonly List<PlayItem> queue = new List<PlayItem>();
        // de Form1.Sonido.cs
        private bool sbrAllowed = false;     // la maquina cumple los requisitos (se mide al arrancar)
        // de Form1.Sonido.cs
        // ===== NIVELES DE MEJORA DE AUDIO segun la maquina (Ctrl+F3) =====
        // NIVEL 1: exciter (excAmount, arriba) - corre en CUALQUIER equipo (~1-2% de CPU).
        // NIVEL 2: reconstructor espectral (SpectralTap) - solo en maquinas que cumplen requisitos (ProbeAudioTier).
        // NIVEL 3 (futuro): DSEE con red neuronal. NO se puede embarcar honestamente en XP/.NET 3.5 (no existe
        //   runtime de inferencia y el CPU no alcanza); si algun dia la rocola corre en Windows 10 + .NET moderno,
        //   ESTE es el punto de enchufe: otro tap en la cadena de PlayTrack, gateado igual por ProbeAudioTier.
        private bool sbrOn = false;          // reconstruccion espectral activa (dsee= en config; Ctrl+F3)
        // de Form1.SinPausa.cs
        private Encadenado secuencia;            // lo que alimenta la salida abierta
        // de LoQueSuena.cs
        private bool sinMejoras;
        // de Form1.cs
        private bool startupReadPending;   // evita guardar tablas vacias si se cierra antes de publicarlas
        // de Form1.Datos.cs
        // Distinto del candado largo de los escritores: aqui solo se abren handles y se retienen metadatos.
        // NUNCA abarcar formato, copia, Write, Flush, Replace ni esperas. El orden de un escritor es
        // CandadoDe -> EstadoRespaldo; la captura solo toma EstadoRespaldo, nunca CandadoDe.
        private sealed class EstadoRespaldo { internal bool Cambiando; internal ArchivoRespaldo Anterior; }
        // de Form1.Datos.cs
        private sealed class ArchivoRespaldo
        {
            internal string Ruta;
            internal FileStream Lector;
            internal long Largo;
            private int referencias = 1;
            internal ArchivoRespaldo Retiene() { System.Threading.Interlocked.Increment(ref referencias); return this; }
            internal void Libera() { if (System.Threading.Interlocked.Decrement(ref referencias) == 0) Lector.Dispose(); }
        }
        // de Form1.Datos.cs
        private static ArchivoRespaldo AbreArchivoRespaldo(string ruta)
        {
            FileStream lector;
            try { lector = new FileStream(ruta, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            try { return new ArchivoRespaldo { Ruta = ruta, Lector = lector, Largo = lector.Length }; }
            catch { lector.Dispose(); throw; }
        }
        // de AfinarLocal.cs
        internal static float[] AfinadoPromedio() { return afinadoDb; }
        // de NivelDisco.cs
        private static string CarpetaDe(string ruta)
        {
            try { return System.IO.Path.GetDirectoryName(ruta); } catch { return null; }
        }
        // de CompararCiegas.cs
        private void CiegoPon(bool a)
        {
            if (ciegoQue == CiegoPresencia)
            {
                presenciaSinGrave = a;
                CadenaCancion c = cadenaSonando; if (c != null && c.Presencia != null) c.Presencia.SinGrave = a;
                return;
            }
            if (ciegoQue == CiegoAfinacion) { if (afinadoUsar == a) return; afinadoUsar = a; }
            else { if (localAuto == a) return; localAuto = a; }
            try { AplicaBocinas(); AplicaALaMedida(); ApplyNormNow(); } catch (Exception ex) { LogError("Comparar a ciegas", ex); }
        }
        // de AfinarLocal.cs
        private float[] ConLocalAfinado(float[] cancion)
        {
            if (!AfinadaActiva) return cancion;
            float[] sala = AfinadaEnBandasEq(CorreccionAfinada(AfinadoPromedio(), afinadoPonderacion));
            if (sala == null) return cancion;
            float[] t = new float[8]; bool any = false;
            for (int b = 0; b < 8; b++)
            {
                float v = sala[b] + (cancion != null ? cancion[b] : 0f);
                if (v > AfinaTotalMax) v = AfinaTotalMax; else if (v < AfinaTotalMin) v = AfinaTotalMin;
                t[b] = v; if (v > 0.1f || v < -0.1f) any = true;
            }
            return any ? t : cancion;
        }
        // de AfinarLocal.cs
        // LA CORRECCION por banda de afinado (dB), o null si faltan las bandas de la voz (250 a 2 kHz). Pura.
        internal static float[] CorreccionAfinada(float[] lecturas, int ponderacion)
        {
            if (lecturas == null || lecturas.Length < 8 || ponderacion < 0 || ponderacion > 2) return null;
            float[] m = new float[8]; double refSum = 0; int refN = 0;
            for (int b = 0; b < 8; b++)
            {
                float v = lecturas[b];
                m[b] = float.IsNaN(v) ? float.NaN : v - AfinaPonderacion[ponderacion][b];
                if (b >= 2 && b <= 5) { if (float.IsNaN(m[b])) return null; refSum += m[b] - CurvaCasa[b]; refN++; }
            }
            float referencia = (float)(refSum / refN);
            float[] c = new float[8];
            for (int b = 0; b < 8; b++)
            {
                if (float.IsNaN(m[b])) continue;
                float dif = (CurvaCasa[b] + referencia - m[b]) * AfinaFuerza;
                if (dif > AfinaZonaMuerta) dif -= AfinaZonaMuerta; else if (dif < -AfinaZonaMuerta) dif += AfinaZonaMuerta; else dif = 0f;
                if (dif > AfinaSubeMax[b]) dif = AfinaSubeMax[b]; else if (dif < AfinaBajaMax) dif = AfinaBajaMax;
                c[b] = dif;
            }
            return c;
        }
        // de Form1.cs
        private void Diag(string msg)
        {
            if (!diagOn) return;
            // Con la hora de ahora, pero la escribe un hilo de fondo (Bitacora, en MedidasBitacora.cs). Tope ~2 MB, como siempre.
            try { Bitacora.Apunta(RutaDatos("rocola_diagnostico.txt"), 2097152, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n"); }
            catch { }
        }
        // de Form1.Datos.cs
        private void EnsureEqAverage()
        {
            if (eqAverageReady && object.ReferenceEquals(eqAverageSource, eqCache)
                && eqAverageVersion == eqCacheVer && eqAverageCount == eqCache.Count) return;
            RecomputeEqAvg();
        }
        // de Form1.Datos.cs
        private float[] EqCorrCancion(string path)
        {
            // EN EL MODO BILLAR el EQ va siempre en automatico y a la medida de esas bocinas (CadenaBillar.cs); apagado, como siempre.
            bool billar = localAuto;
            bool auto = eqAuto || billar;
            float fuerza = auto ? EqFuerzaAuto() : eqAmount;
            if (fuerza <= 0.001f || path == null || eqAvg == null) return null;
            float[] c;
            if (!eqCache.TryGetValue(path, out c) || c[EqFingerprint] != EqVersionOriginal) return null;
            if (billar) { float dur = SegundosDe(path); if (dur > 0.01f && dur < BillarEqMinSeg) return null; }
            // Banda por ENCIMA del acantilado de esta cancion: NUNCA se corrige, y por dos razones distintas
            // segun el caso. Con el DSEE apagado, ahi el codec no dejo musica: subirla seria subir ruido. Y con
            // el DSEE encendido tampoco, porque la huella guardada describe el ARCHIVO (donde esa banda esta
            // vacia) mientras que lo que suena YA lleva los agudos re-creados: corregir con un numero viejo
            // seria empujar dos veces lo mismo.
            float corte = c[EqCut];
            // OBJETIVO POR CANCION. Para el cuerpo (158 Hz - 5.6 kHz) el objetivo sigue siendo el promedio de las
            // canciones sanas: es el "sonido de la casa". Pero para el grave y los agudos, si hay modelo entrenado,
            // el objetivo lo dice la IA mirando el cuerpo de ESTA cancion: cuanto grave y cuanto aire tendria una
            // cancion bien grabada que sonara como esta. Asi una balada de piano y un corrido dejan de ser
            // empujados hacia la misma curva. Sin modelo, todo sigue como antes (el promedio para las 8 bandas).
            float[] meta = MetaDeCancion(c) ?? eqAvg;
            // CANCION RARA. Si se aleja muchisimo del objetivo, casi seguro es que QUERIA sonar asi: un disco
            // muy oscuro a proposito, una grabacion en vivo, una voz sola. Empujarla a la fuerza hacia el resto
            // la estropea, asi que cuanto mas lejos esta de la biblioteca, con mas cuidado se le toca. Solo se
            // hace en automatico: si alguien puso la perilla a mano, manda la perilla.
            if (auto)
            {
                float dif = 0f; int nb = 0;
                for (int b = 0; b < 8; b++)
                {
                    if (corte > 100f && EqCentros[b] > corte) continue;
                    if (c[b] <= -25f) continue;
                    float d = meta[b] - c[b]; if (d < 0f) d = -d;
                    dif += d; nb++;
                }
                if (nb > 0) fuerza *= Clampf(1.15f - (dif / nb) / 14f, billar ? BillarEqRaraMin : 0.45f, 1f);
            }
            bool recortada = c[EqClip] > BillarEqRecorte, afinada = billar && AfinadaActiva;
            bool flaca = false;
            if (billar) { float falta = 0.5f * ((meta[1] - c[1]) + (meta[2] - c[2])); flaca = c[1] > -25f && c[2] > -25f && falta >= CuerpoFlacaDb; }
            float[] dbs = new float[8]; bool any = false;
            for (int b = 0; b < 8; b++)
            {
                float corr = 0f;
                if (corte > 100f && EqCentros[b] > corte) { dbs[b] = 0f; continue; }
                if (billar)
                {
                    if (BandaBillar(b, c, corte) && c[b] > BillarEqMuda) corr = CorreccionBillar(b, meta[b] + (afinada ? 0f : InclinacionBillar[b]) - c[b], fuerza, recortada, flaca);   // con el local medido, sin la inclinacion estimada
                }
                else if (c[b] > -25f)
                {
                    corr = meta[b] - c[b];
                    if (corr > 4f) corr = 4f; else if (corr < -4f) corr = -4f;
                    corr *= fuerza;
                }
                dbs[b] = corr;
                if (corr > 0.1f || corr < -0.1f) any = true;
            }
            return any ? dbs : null;
        }
        // de Form1.Fondo.cs
        // 20 s de musica: con menos, la huella no es confiable. Se cuenta en SEGUNDOS: antes eran 882.000
        // cuadros, que son 20 s solo a 44,1 kHz (9,2 s a 96 kHz y 40 s a 22,05 kHz).
        private const double EqMinSec = 20.0;
        // de Form1.Ia.cs
        private static readonly RedChica EqNet = new RedChica(EqNetIn, EqNetOut);
        // de Form1.Ia.cs
        private const int EqNetIn = 5, EqNetOut = 3;
        // de Form1.Ia.cs
        private static readonly int[] EqNetSalida = { 0, 6, 7 };   // que bandas predice: 50 Hz, 10 kHz y 15.8 kHz
        // de LoQueSuena.cs
        private static readonly float[] EqPlana = new float[8];
        // de Form1.Datos.cs
        private static EstadoRespaldo EstadoRespaldoDe(string ruta)
        {
            lock (estadosRespaldo)
            {
                EstadoRespaldo estado;
                if (!estadosRespaldo.TryGetValue(ruta, out estado))
                { estado = new EstadoRespaldo(); estadosRespaldo[ruta] = estado; }
                return estado;
            }
        }
        // de Form1.Datos.cs
        private float ExciteFactorFor(string path)
        {
            float[] c;
            if (path == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen) return 1f;
            float corte = c[EqCut];
            if (corte <= 100f) return localAuto ? RealceBillarSinMedir : 1f;   // aun sin medir: como siempre
            float f = Clampf(1.45f - (corte - 15000f) / 6000f, ExciteCompleta, 1.45f);
            if (localAuto) return RealceBillar(f);   // en el modo billar: solo lo que el codec corto (CadenaBillar.cs)
            // ...PERO SI EL RECONSTRUCTOR ESPECTRAL ESTA ENCENDIDO, esos agudos ya se los devolvio EL.
            //
            // Estas dos cosas estaban decidiendo por separado y las dos miraban el MISMO dato: el realce le
            // daba mas a la cancion dañada justamente por estar dañada, y el reconstructor le re-creaba la
            // banda justamente por lo mismo. En la cadena el realce va DESPUES, asi que acababa echandole
            // brillo encima a unos agudos que ya no faltaban: empujar dos veces lo mismo, y el exceso de
            // realce suena aspero (por eso su mezcla esta topada). El EQ automatico ya evita este choque
            // -esta explicado ahi mismo, unas lineas mas arriba-; el realce era el unico que no se enteraba.
            //
            // Se le quita la MITAD del extra, no todo: el reconstructor devuelve la ENVOLVENTE de los agudos
            // transponiendo lo que sobrevivio una octava mas abajo, que no es el detalle original, asi que
            // algo de realce encima sigue teniendo sentido. Y con el reconstructor apagado no cambia nada.
            if (sbrOn && sbrAllowed && corte < EqSanaHz)
                f = ExciteCompleta + (f - ExciteCompleta) * 0.5f;
            // I1: el aire que le falta (o le sobra) a ESTA cancion frente a su objetivo, en las bandas de 10 y 15,8 kHz
            // que su archivo SI conserva (lo que el codec corto ya lo cuenta el corte, arriba). Le falta: mas realce; le
            // sobra: menos. Como mucho un 25 % arriba o un 20 % abajo.
            float[] meta = MetaALaMedida(c);
            if (meta != null)
            {
                float falta = 0f; int nb = 0;
                for (int b = 6; b < 8; b++)
                {
                    if (corte > 100f && EqCentros[b] > corte) continue;
                    if (c[b] <= -25f) continue;
                    falta += meta[b] - c[b]; nb++;
                }
                if (nb > 0) f *= Clampf(1f + (falta / nb) / 12f, 0.80f, 1.25f);
            }
            return f;
        }
        // de AfinarLocal.cs
        internal static float FactorBajoMedido(float[] lecturas, int ponderacion)
        {
            if (lecturas == null || lecturas.Length < 8 || float.IsNaN(lecturas[0]) || ponderacion < 0 || ponderacion > 2) return 1f;
            double refSum = 0; int refN = 0;
            for (int b = 2; b <= 5; b++) { if (float.IsNaN(lecturas[b])) return 1f; refSum += lecturas[b] - AfinaPonderacion[ponderacion][b] - CurvaCasa[b]; refN++; }
            double d = (lecturas[0] - AfinaPonderacion[ponderacion][0]) - refSum / refN - CurvaCasa[0];   // negativo: le falta grave
            double e = d < -BajoMedidoHolguraFalta ? d + BajoMedidoHolguraFalta : (d > BajoMedidoHolguraSobra ? d - BajoMedidoHolguraSobra : 0.0);
            double f = 1.0 - e / 12.0;
            return (float)(f < BajoMedidoMin ? BajoMedidoMin : (f > BajoMedidoMax ? BajoMedidoMax : f));
        }
        // de Form1.Datos.cs
        // Un numero de verdad (ni NaN ni infinito): lo unico que se guarda en las tablas y lo unico que se lee de ellas.
        private static bool Finito(float v) { return v - v == 0f; }
        // de MedidaFluidezGpu.cs
        private static bool Finito(double valor) { return !double.IsNaN(valor) && !double.IsInfinity(valor); }
        // de Form1.Datos.cs
        private static bool Finitos(float[] c) { if (c == null) return false; for (int i = 0; i < c.Length; i++) if (c[i] - c[i] != 0f) return false; return true; }
        // de Form1.Biblioteca.cs
        // Vuelca las huellas del EQ recien medidas. Se llama de tanto en tanto (no por cancion): recalcular el
        // promedio de la biblioteca y reescribir el archivo en cada tema seria puro desgaste de disco.
        private void FlushEqIfDirty()
        {
            if (!eqDirty) return;
            eqDirty = false;
            EnsureEqAverage();
            SaveEqCache();
        }
        // de NivelDisco.cs
        // La ganancia del disco entero. Falso si no hay bastante medido (se nivela cancion por cancion).
        private bool GananciaDeDisco(List<Track> pistas, string ruta, out float g)
        {
            g = 1f;
            if (!normalizeOn || normStrength <= 0.001f || pistas == null || integCache == null) return false;
            double peso = 0, pot = 0; float pico = 0f, p99 = 0f, p90 = 0f; bool picos = true, conP90 = true, esta = false; int medidas = 0;
            float rmsMin = float.MaxValue, rmsMax = 0f;
            for (int i = 0; i < pistas.Count; i++)
            {
                Track tr = pistas[i];
                SonoridadIntegrada s;
                if (tr == null || tr.Path == null || !integCache.TryGetValue(tr.Path, out s) || !s.Valida || s.Muda) continue;
                float rms = s.RmsPorCanal;
                if (!(rms > 0.0001f)) continue;
                double w = Math.Max(1, s.Bloques);
                pot += w * (double)rms * rms; peso += w; medidas++;
                if (s.Bloques >= MinBloquesDispersion) { if (rms < rmsMin) rmsMin = rms; if (rms > rmsMax) rmsMax = rms; }
                if (string.Equals(tr.Path, ruta, StringComparison.OrdinalIgnoreCase)) esta = true;
                if (s.ConPicos) { pico = Math.Max(pico, DbALin(s.PicoDb)); p99 = Math.Max(p99, DbALin(s.P99Db)); }
                else picos = false;
                if (s.ConP90) p90 = Math.Max(p90, DbALin(s.P90Db)); else conP90 = false;
            }
            if (!esta || medidas < 2 || medidas * 2 < pistas.Count || !(peso > 0)) return false;
            if (rmsMax > 0f && rmsMin < float.MaxValue && rmsMax > rmsMin * (float)Math.Pow(10.0, DispersionDiscoDb / 20.0)) return false;   // recopilatorio: cancion por cancion
            if (picos && conP90) { g = GananciaAlLimite(normStrength, pico, p99, p90); return true; }   // todas con la medida nueva: al limite
            float rmsDisco = (float)Math.Sqrt(pot / peso);
            float cap = (float)Math.Pow(10.0, TierCapDb(rmsDisco) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;
            g = GananciaNivelado(normTarget, rmsDisco, normStrength, cap, picos, pico, p99);
            return true;
        }
        // de Form1.Ia.cs
        // El genero de esta cancion segun su disco, si no trae etiqueta ("" si no se sabe). Cualquier hilo.
        private static string GeneroInterpretado(string path)
        {
            GenerosIa g = generosIa;
            if (g == null || string.IsNullOrEmpty(path)) return "";
            string dir, et;
            try { dir = Path.GetDirectoryName(path); } catch { return ""; }
            return dir != null && g.PorCarpeta.TryGetValue(dir, out et) ? et : "";
        }
        // de Sonoridad.cs
        private static void IntegradaPuesta(string ruta) { if (ruta != null) lock (integHechasCerrojo) integHechas.Add(ruta); }
        // de Form1.Sonido.cs
        // LA MARCA DE "MEDIDO Y EN SILENCIO". Un archivo sin nada de sonido da cero al medirlo, y con cero
        // el pre-analisis no lo guardaba: se quedaba como "sin medir" y se volvia a medir en cada arranque,
        // para siempre. Se le pone este valor minusculo -por debajo del umbral con el que TODO el programa
        // decide "esto no tiene medida util", asi que nadie va a intentar subirlo cien mil veces- para que
        // quede apuntado como lo que es.
        private const float LoudMudo = 0.00001f;   // se guarda y se relee tal cual
        // de NivelDisco.cs
        // Lo que baja el nivelado para el tono del local que de verdad suena en esta cancion (los mandos, o lo que decide el modo
        // automatico). Con "Escuchar sin mejoras" el tono no suena: no se baja. 1 = nada (tono en 0 o negativo).
        private float MargenDelTono(Track t)
        {
            if (sinMejoras) return 1f;
            AjusteLocal lo = LocalDe(t);
            double db = SalaLocal.MargenTonoDb(lo.Graves, lo.Agudos);
            return db > 0.0 ? (float)Math.Pow(10.0, -db / 20.0) : 1f;
        }
        // de Optimizaciones.cs
        // Se apunta cuando la medida ESTA PUESTA en las tablas, no cuando se pide. Un archivo roto no
        // da ni duracion ni sonoridad: darlo por medido seria no volver a intentarlo nunca.
        private static void MedidaPuesta(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasHechas.Add(ruta);
        }
        // de Form1.Datos.cs
        private float[] MetaALaMedida(float[] c) { return aLaMedida || localAuto ? MetaDeCancion(c) : null; }   // en el modo billar, siempre (CadenaBillar.cs)
        // de NivelDisco.cs
        private static bool MismaCarpeta(PlayItem x, string carpeta)
        {
            return x != null && !x.IsVideo && x.Song != null && x.Song.Path != null
                && string.Equals(CarpetaDe(x.Song.Path), carpeta, StringComparison.OrdinalIgnoreCase);
        }
        // de Form1.Sonido.cs
        // Ganancia de nivelado para una cancion, segun su sonoridad medida (si ya se oyo alguna vez). 1 = sin cambio.
        private float NormGainFor(string path)
        {
            if (!normalizeOn || normStrength <= 0.001f || path == null) return 1f;
            float rms;
            // La medida de la cancion ENTERA (BS.1770) si ya esta; si no, la estimacion por tramos o la
            // aprendida oyendola. Misma escala y mismos topes de abajo (ver SonoridadParaNivelar).
            if (!SonoridadParaNivelar(path, out rms)) return 1f;   // aun no medida: va a su volumen original y se aprende en esta pasada
            // "Subir el piso": si la cancion suena mas BAJITO que el objetivo, se SUBE (segun la fuerza), con el tope
            // de SU ETAPA y sin pasar de lo que aguantan sus picos. Si suena bastante mas FUERTE, se baja un poco. La
            // cuenta esta en GananciaNivelado (Sonoridad.cs).
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;   // NormMaxBoost sigue siendo el techo absoluto de seguridad
            float pico, p99, p90;
            bool hayPicos = PicosParaNivelar(path, out pico, out p99);
            // Con la medida nueva (algoritmo 5, con el P90): hasta su limite, sin objetivo (GananciaAlLimite, Sonoridad.cs). Con una de antes,
            // la regla de antes mientras la tanda larga la vuelve a medir.
            if (hayPicos && P90ParaNivelar(path, out p90)) return GananciaAlLimite(normStrength, pico, p99, p90);
            return GananciaNivelado(normTarget, rms, normStrength, cap, hayPicos, pico, p99);
        }
        // de Form1.cs
        // Minimo de musica oida para aceptar una medida (si se salta antes, no se guarda). En SEGUNDOS: antes
        // eran 800.000 muestras, que son 9 s en estereo a 44,1 kHz pero 18 s en mono y 4,2 s en estereo a 96 kHz.
        private const double NormMinSec = 800000.0 / (2 * 44100.0);
        // de NivelDisco.cs
        // Las pistas de esa carpeta: las del disco con el que se pidio, si es de verdad ese disco; si no (un disco
        // "lo mas sonado", o pedida desde el buscador), el disco de la biblioteca con esa carpeta.
        private List<Track> PistasDeCarpeta(Album pedido, string carpeta)
        {
            if (pedido != null && !pedido.IsTop && pedido.Tracks != null && pedido.Tracks.Count > 0
                && string.Equals(CarpetaDe(pedido.Tracks[0].Path), carpeta, StringComparison.OrdinalIgnoreCase)) return pedido.Tracks;
            List<Album> todos = albums;
            if (todos != null)
                for (int i = 0; i < todos.Count; i++)
                {
                    Album a = todos[i];
                    if (a == null || a.IsTop || a.Tracks == null || a.Tracks.Count == 0) continue;
                    if (string.Equals(CarpetaDe(a.Tracks[0].Path), carpeta, StringComparison.OrdinalIgnoreCase)) return a.Tracks;
                }
            return null;
        }
        // de Form1.Datos.cs
        private static void PublicaGuardadoSeguro(string ruta, string tmp, string bak)
        {
            // La captura no espera al formato/Flush ni al reemplazo: mientras este cambia el nombre,
            // conserva un handle del original. El candado corto se suelta antes de tocar el disco.
            using (ProteccionRespaldo cambio = ProtegeRespaldo(ruta))
            {
                if (File.Exists(ruta))
                {
                    // Replace cambia el archivo de un tiron y deja el anterior como copia. En memorias USB
                    // (FAT32) no existe, asi que ahi se hace a mano.
                    try { File.Replace(tmp, ruta, bak, true); return; }
                    catch { }
                    try { if (File.Exists(bak)) File.Delete(bak); } catch { }
                    // Si no se pudo conservar el original, ABORTAR: nunca borrarlo para forzar el reemplazo.
                    File.Move(ruta, bak);
                }
                File.Move(tmp, ruta);
            }
        }
        // de Form1.Datos.cs
        private void SaveEqCache()
        {
            SaveFuentesMedidas();
            if (startupReadPending) return;
            if (medidasDeltasActivas)
            {
                if (cambiosEq == null || !ReferenceEquals(cambiosEq.Origen, eqCache)) cambiosEq = PreparaEq(eqCache);
                CambiosTabla<RegistroEq> tabla = cambiosEq;
                long v = System.Threading.Interlocked.Increment(ref eqSaveVersion);
                DiarioTabla d = diarioEq ?? (diarioEq = new DiarioTabla());
                GuardaTablaCambios(tabla, eqSaveQueue, eqSaveLock, d, EqFile(), null,
                    delegate { return v == System.Threading.Interlocked.CompareExchange(ref eqSaveVersion, 0, 0) && ReferenceEquals(tabla, cambiosEq); },
                    delegate(string ruta, RegistroEq r) { return HuellaEq(r.Curva); },
                    delegate(TextWriter w, string ruta, RegistroEq r)
                    {
                        for (int b = 0; b < EqLen; b++) { if (b > 0) w.Write(';'); w.Write(r.Curva[b].ToString("0.##", CultureInfo.InvariantCulture)); }
                        w.Write('|'); w.Write(ruta); w.Write("\r\n");
                    }, null, null);
                return;
            }
            List<KeyValuePair<string, float[]>> snap;
            try { snap = new List<KeyValuePair<string, float[]>>(eqCache); }   // copia rapida en el hilo de UI
            catch { return; }
            int version = System.Threading.Interlocked.Increment(ref eqSaveVersion);
            DiarioTabla diario = diarioEq ?? (diarioEq = new DiarioTabla());   // solo lo nuevo (DiarioTablas.cs)
            GuardaUltimo(eqSaveQueue, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { return version == System.Threading.Interlocked.CompareExchange(ref eqSaveVersion, 0, 0); };
                    if (!vigente()) return;
                    lock (eqSaveLock)
                    {
                        // Igual que la de volumen: solo lo nuevo, anadido al final (DiarioTablas.cs).
                        diario.Guarda(EqFile(), snap, delegate(string cancion, float[] c) { return HuellaEq(c); },
                        delegate(TextWriter writer, string cancion, float[] c)
                        {
                            for (int b = 0; b < EqLen; b++)
                            { if (b > 0) writer.Write(';'); writer.Write(c[b].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)); }
                            writer.Write('|'); writer.Write(cancion); writer.Write("\r\n");
                        }, null, null, vigente);
                    }
                }
                catch { }
            });
        }
        // de Sonoridad.cs
        // Se guarda en segundo plano, como la tabla de volumen, y solo si hay algo nuevo.
        private void SaveIntegCache()
        {
            SaveFuentesMedidas();
            if (startupReadPending || integEstado != 1 || integCache == null) return;
            long hasta = integCambios;
            if (hasta == System.Threading.Interlocked.Read(ref integGuardados)) return;
            if (medidasDeltasActivas)
            {
                if (cambiosIntegrada == null || !ReferenceEquals(cambiosIntegrada.Origen, integCache)) cambiosIntegrada = PreparaIntegrada(integCache);
                CambiosTabla<RegistroIntegrada> tabla = cambiosIntegrada;
                long v = System.Threading.Interlocked.Increment(ref integGuardaVersion);
                if (integGuardaCola == null) integGuardaCola = new GuardadoReciente();
                DiarioTabla d = diarioInteg ?? (diarioInteg = new DiarioTabla());
                List<string> extranjeras = new List<string>(integAjenas ?? new List<string>());
                GuardaTablaCambios(tabla, integGuardaCola, integGuardaCerrojo, d, IntegFile(), extranjeras,
                    delegate { return integEstado == 1 && v == System.Threading.Interlocked.Read(ref integGuardaVersion) && ReferenceEquals(tabla, cambiosIntegrada); },
                    delegate(string cancion, RegistroIntegrada r) { return HuellaInteg(r.Valor); },
                    delegate(TextWriter w, string cancion, RegistroIntegrada r) { EscribeIntegRegistro(w, cancion, r.Valor); }, IntegCabecera,
                    delegate { System.Threading.Interlocked.Exchange(ref integGuardados, hasta); });
                return;
            }
            List<KeyValuePair<string, SonoridadIntegrada>> copia;
            List<string> ajenas;
            try { copia = new List<KeyValuePair<string, SonoridadIntegrada>>(integCache); ajenas = new List<string>(integAjenas ?? new List<string>()); }
            catch { return; }
            string ruta = IntegFile();
            long version = System.Threading.Interlocked.Increment(ref integGuardaVersion);
            if (integGuardaCola == null) integGuardaCola = new GuardadoReciente();
            object cerrojo = integGuardaCerrojo ?? new object();
            DiarioTabla diario = diarioInteg ?? (diarioInteg = new DiarioTabla());   // solo lo nuevo (DiarioTablas.cs)
            GuardaUltimo(integGuardaCola, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { return version == System.Threading.Interlocked.Read(ref integGuardaVersion); };
                    if (!vigente()) return;
                    bool hecho;
                    // Entera: la cabecera, los registros y los de una version posterior tal cual. Si no, solo lo nuevo,
                    // anadido al final (DiarioTablas.cs).
                    lock (cerrojo)
                        hecho = diario.Guarda(ruta, copia, delegate(string cancion, SonoridadIntegrada s) { return HuellaInteg(s); },
                                              delegate(TextWriter w, string cancion, SonoridadIntegrada s) { EscribeIntegRegistro(w, cancion, s); },
                                              IntegCabecera, ajenas, vigente);
                    if (hecho) System.Threading.Interlocked.Exchange(ref integGuardados, hasta);
                }
                catch { }
            });
        }
        // de Form1.Datos.cs
        private void SaveLoudCache()
        {
            SaveFuentesMedidas();
            if (startupReadPending) return;
            if (medidasDeltasActivas)
            {
                if (cambiosVolumen == null || !ReferenceEquals(cambiosVolumen.Origen, loudCache)) cambiosVolumen = PreparaVolumen(loudCache, durCache);
                CambiosTabla<RegistroVolumen> tabla = cambiosVolumen;
                long v = System.Threading.Interlocked.Increment(ref loudSaveVersion);
                DiarioTabla d = diarioVolumen ?? (diarioVolumen = new DiarioTabla());
                GuardaTablaCambios(tabla, loudSaveQueue, loudSaveLock, d, LoudFile(), null,
                    delegate { return v == System.Threading.Interlocked.CompareExchange(ref loudSaveVersion, 0, 0) && ReferenceEquals(tabla, cambiosVolumen); },
                    delegate(string ruta, RegistroVolumen r) { return HuellaVolumen(r.Rms, r.Duracion); },
                    delegate(TextWriter w, string ruta, RegistroVolumen r)
                    {
                        w.Write(r.Rms.ToString("0.######", CultureInfo.InvariantCulture)); w.Write('|');
                        w.Write(r.Duracion.ToString("0.##", CultureInfo.InvariantCulture)); w.Write('|'); w.Write(ruta); w.Write("\r\n");
                    }, null, null);
                return;
            }
            List<KeyValuePair<string, float>> snap;
            List<KeyValuePair<string, float>> durCopia;
            // Copias de las dos tablas (son de la pantalla y solo se tocan aqui). Como LISTAS: copiar la de duraciones como
            // diccionario rehacia su indice aqui mismo, pasando cada ruta por el comparador sin mayusculas (23 ms con
            // 20.000 canciones en un procesador de servidor, en cada guardado). El indice se arma fuera, al escribir.
            try { snap = new List<KeyValuePair<string, float>>(loudCache); durCopia = new List<KeyValuePair<string, float>>(durCache); }
            catch { return; }
            int version = System.Threading.Interlocked.Increment(ref loudSaveVersion);
            DiarioTabla diario = diarioVolumen ?? (diarioVolumen = new DiarioTabla());   // solo lo nuevo (DiarioTablas.cs)
            GuardaUltimo(loudSaveQueue, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { return version == System.Threading.Interlocked.CompareExchange(ref loudSaveVersion, 0, 0); };
                    if (!vigente()) return;
                    Dictionary<string, float> durSnap = new Dictionary<string, float>(durCopia.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, float> kv in durCopia) durSnap[kv.Key] = kv.Value;
                    lock (loudSaveLock)
                    {
                        // Anade al final solo las canciones nuevas o que cambiaron; entera cuando toca (DiarioTablas.cs).
                        // Cada linea, la de siempre: "rms|duracion|ruta".
                        diario.Guarda(LoudFile(), snap, delegate(string cancion, float rms)
                        {
                            float du; if (!durSnap.TryGetValue(cancion, out du)) du = 0f;
                            return HuellaVolumen(rms, du);
                        }, delegate(TextWriter writer, string cancion, float rms)
                        {
                            float du; if (!durSnap.TryGetValue(cancion, out du)) du = 0f;
                            writer.Write(rms.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
                            writer.Write('|'); writer.Write(du.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                            writer.Write('|'); writer.Write(cancion); writer.Write("\r\n");
                        }, null, null, vigente);
                    }
                }
                catch { }
            });
        }
        // de Form1.Ia.cs
        // El nombre de archivo de un genero: la etiqueta fiable (IaGeneroFiable) con solo letras a-z, cifras y '_'. La
        // misma cuenta que slug_genero() de ia/entrenar_ia.py.
        internal static string SlugGenero(string genero)
        {
            string g = IaGeneroFiable(genero);
            if (g.Length == 0) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c0 in g)
            {
                char c = c0;
                switch (c) { case '\u00e1': c = 'a'; break; case '\u00e9': c = 'e'; break; case '\u00ed': c = 'i'; break; case '\u00f3': c = 'o'; break; case '\u00fa': case '\u00fc': c = 'u'; break; case '\u00f1': c = 'n'; break; }
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                if (sb.Length >= 40) break;
            }
            return sb.ToString().Trim('_');
        }
        // de SubirTodo.cs
        // Los dB de "Subir TODO" que de verdad se aplican: lo pedido, sin pasar del techo de la biblioteca.
        private float SubirAplicadoDb()
        {
            if (!(masterDb > 0f)) return 0f;
            int n;
            float techo = TechoSubirDb(out n);
            if (float.IsNaN(techo)) return masterDb;   // aun no hay bastantes medidas: lo pedido, como antes
            return Math.Min(masterDb, Math.Max(0f, techo));
        }
        // de AfinarLocal.cs
        private static int afinadoPonderacion = 1;   // C de fabrica (la que se recomienda)
        // de AfinarLocal.cs
        private static bool afinadoUsar;              // "afinado_usar=": apagado de fabrica
        // de Form1.Sonido.cs
        // BAJO PSICOACUSTICO: 0..1 ("bajo=" en config, perilla en Ctrl+F3). Recrea el grave que las bocinas
        // del local NO pueden dar, generando sus armonicos. Ver DeepBassProvider.
        private float bassAmount = 0.30f;
        // de Form1.Sonido.cs
        private bool bassAuto = true;              // "bajo_automatico=" en config
        // de Form1.Datos.cs
        // UN CANDADO POR ARCHIVO. Dos hilos guardando EL MISMO archivo a la vez se pisarian el archivo de
        // paso y dejarian el bueno hecho un lio. Pasaba de verdad con las estadisticas, que tienen dos
        // escritores: uno de fondo cada tanto y otro al cerrar la rocola. El candado va AQUI DENTRO y no en
        // cada sitio que guarda, para que no dependa de que alguien se acuerde de ponerlo. Es POR ARCHIVO,
        // asi que guardar el volumen de la biblioteca -que es grande- no hace esperar a los creditos.
        private static readonly Dictionary<string, object> candados = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Ia.cs
        private static readonly Dictionary<string, RedChica> dseeGeneros = new Dictionary<string, RedChica>(StringComparer.Ordinal);
        // de Form1.Ia.cs
        // I3, EL DSEE POR GENERO (13-10-2026; interruptor "Modelos del DSEE por genero" en Ctrl+F3, apagado de fabrica:
        // "dsee_genero="). Ademas del modelo general (rocola_ia.bin), el entrenador puede sacar uno por genero con el
        // material de ese genero (ia/entrenar_ia.py --genero cumbia), con las mismas guardas, e instalar_dsee_genero.py lo deja
        // como rocola_ia_genero_<genero>.bin en la carpeta IAS de la carpeta de datos. Con el interruptor encendido, cada cancion usa el de su
        // genero (la etiqueta del archivo, la misma que se apunta junto al material); si no hay, el general. Solo se nota
        // con el DSEE encendido. Se mira al armar la cadena de cada cancion.
        private bool dseePorGenero;
        // de Form1.Datos.cs
        // DURACION RAPIDA. Sacar SOLO la duracion de un archivo cuesta ~600 veces menos que el analisis
        // completo: es leer la cabecera, no descomprimir el audio. Antes la duracion venia "de regalo" con ese
        // analisis pesado, asi que con cien mil canciones practicamente ninguna la mostraba (el analisis tarda
        // dias). Ahora va en su PROPIA pasada, que barre la biblioteca entera en minutos. Cuando el analisis
        // completo mide una cancion, SU duracion (del archivo entero) manda sobre esta.
        private Dictionary<string, float> durFast = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Sonido.cs
        private float eqAmount = 0f;               // fuerza del EQ automatico 0..1 A MANO (perilla en Ctrl+F3; "eq_auto=" en config); 0 = apagado
        // de Form1.Sonido.cs
        // AUTOMATICO (de fabrica): no hay perilla que ajustar. La fuerza del EQ y la cantidad de bajo las
        // decide el propio programa cancion por cancion, con lo que el analisis del DSEE ya midio de cada una
        // (acantilado de agudos, recorte de picos, corte por abajo y huella tonal). Ver EqFuerzaAuto/BajoAuto.
        private bool eqAuto = true;                // "eq_automatico=" en config
        // de Form1.Sonido.cs
        private int eqCacheVer;                  // sube cada vez que cambia una huella: lo usan los recuentos cacheados
        // de Form1.Sonido.cs
        // PRECALENTADOR de miniaturas: hilo de MINIMA prioridad que va construyendo en disco la
        // miniatura de TODAS las caratulas que falten. La primera sesion en una maquina tarda unos
        // minutos en completarse (y se PAUSA mientras alguien usa la rocola para no estorbar);
        // a partir de ahi, abrir cualquier pagina es practicamente instantaneo para siempre.
        // ===================== PRE-ANALISIS DE LA BIBLIOTECA =====================
        // Antes, una cancion NUEVA sonaba a su volumen original la primera vez (aun no habia medida) y solo
        // quedaba nivelada a partir de la segunda. Con el pre-analisis eso se acabo: en segundo plano se MIDE
        // toda la biblioteca (sonoridad K-weighted, la misma norma que usa el nivelado, + la DURACION), asi que
        // cada cancion ya llega nivelada la PRIMERA vez que alguien la pone.
        // Para que no le robe la maquina a la rocola: hilo de prioridad MINIMA, espera a que nadie este usando
        // la rocola, cede el paso si hay video, y de cada cancion mide TRAMOS repartidos (no el archivo entero):
        // ~60 s bien elegidos dan una buena ESTIMACION a una fraccion del costo. La medida integrada de la cancion
        // ENTERA viene despues, en una segunda tanda (ver Sonoridad.cs). Lo medido se guarda en disco cada pocas
        // canciones: se puede apagar la rocola sin perder nada.
        private bool eqDirty = false;             // hay huellas de EQ nuevas sin guardar (las escribe FlushEqIfDirty)
        // de Form1.Fondo.cs
        // REALCE DE AGUDOS (estilo DSEE de Sony, en su version honesta): el MP3 recorta el "aire" (agudos
        // altos) y eso ya NO se puede recuperar; lo que SI se puede es re-inventarlo, que es lo que hacen el
        // propio DSEE y los EXCITERS ARMONICOS clasicos (las cajas Aphex/BBE de los equipos de bar): tomar la
        // banda alta que sobrevivio (~5.5 kHz+), saturarla SUAVE (eso genera armonicos nuevos mas arriba) y
        // mezclar una pizca de vuelta. Se aplica en LevelTap tras nivelar; perilla en Ctrl+F3; "realce=" en config.
        private float excAmount = 0.35f;                                                      // intensidad del realce 0..1 (0 = apagado); 0.35 = sutil
        // de Sonoridad.cs
        // Cuanto se aparta la estimacion de la medida entera, en las canciones de esta sesion (para la bitacora).
        private double integDifSuma, integDifMayor; private int integDifN;
        // de Optimizaciones.cs
        // ==========================================================================================
        //  QUIEN MIDE QUE: una libreta con candado para que dos trabajadores no midan lo mismo.
        //
        //  En la rocola hay dos que miden canciones -sonoridad, duracion y huella tonal- y ninguno
        //  sabia del otro:
        //
        //    - EL REPASO DE LA BIBLIOTECA arma su lista de pendientes UNA SOLA VEZ, al arrancar, y en
        //      su bucle no vuelve a mirar. Esa lista vive HORAS: una biblioteca de unos miles de
        //      canciones tarda varias en repasarse entera.
        //    - EL CALENTADOR DE LA SIGUIENTE mide la cancion que viene, en cuanto alguien la pide.
        //
        //  O sea que CADA CANCION QUE UN CLIENTE PEDIA se media dos veces: una al pedirla, y otra un
        //  rato despues, cuando al repaso le llegaba su turno. Medir una de cuatro minutos cuesta
        //  335 ms aqui -y mas en el equipo del bar, que descomprime MP3 y no WAV-, y eso es trabajo
        //  tirado peleando con la musica justo cuando hay gente eligiendo.
        //
        //  Aqui se apunta QUIEN esta midiendo que, y QUE se midio ya.
        //
        //  EL REPASO A FONDO NO ENTRA EN LA SEGUNDA PARTE, y es a proposito: ese existe justamente
        //  para volver a medir, porque analiza la cancion COMPLETA en vez de por tramos y saca
        //  material para la IA. Lo que si respeta es la primera: no se pone a medir lo que otro esta
        //  midiendo en ese instante.
        //
        //  Las rutas que se guardan son LAS MISMAS de las demas tablas (pasan por Pool), asi que lo
        //  que ocupa esto es el hueco de la lista, no las cadenas: del mismo orden que durCache, que
        //  ya guarda una fila por cancion.
        // ==========================================================================================
        private static readonly object medidasCerrojo = new object();
        // de Optimizaciones.cs
        private static readonly System.Collections.Generic.HashSet<string> medidasHechas =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // de NivelDisco.cs
        private PlayItem sonoAntes;   // lo que sono justo antes de la que suena (null si hubo silencio o es la primera)
        // de Form1.SinPausa.cs
        private LevelTap tapMedido;              // de que cancion es la medida en vivo de los campos de medida
        // de AfinarLocal.cs
        internal const float AfinaBajaMax = -6f, AfinaFuerza = 0.7f, AfinaZonaMuerta = 1f;
        // de AfinarLocal.cs
        // Lo que la ponderacion de la app le quita a cada banda (dB, IEC 61672): A, C y Z (plana).
        internal static readonly float[][] AfinaPonderacion = {
            new float[] { -26.2f, -16.1f, -8.6f, -3.2f, 0f, 1.2f, 1.0f, -1.1f },
            new float[] { -0.8f, -0.2f, 0f, 0f, 0f, -0.2f, -0.8f, -3.0f },
            new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f } };
        // de AfinarLocal.cs
        internal static readonly float[] AfinaSubeMax = { 0f, 3f, 3f, 3f, 3f, 3f, 3f, 2f };
        // de AfinarLocal.cs
        // El EQ que suena: el de la cancion y, en el billar con la afinacion, la del local encima (tope total -8 / +4 dB).
        internal const float AfinaTotalMin = -8f, AfinaTotalMax = 4f;
        // de AfinarLocal.cs
        // La misma correccion en las bandas del EQ de cada cancion (EqCentros), interpolada en octavas. 10 kHz se lleva la mitad de la
        // de 8 kHz (ya no se midio) y 15,8 kHz nada. Pura.
        internal static float[] AfinadaEnBandasEq(float[] c)
        {
            if (c == null) return null;
            float[] e = new float[8];
            for (int b = 0; b < 8; b++)
            {
                float f = EqCentros[b];
                if (f <= AfinaCentros[0]) e[b] = c[0];
                else if (f >= 15000f) e[b] = 0f;
                else if (f >= AfinaCentros[7]) e[b] = 0.5f * c[7];
                else
                {
                    int i = 0; while (i < 6 && f > AfinaCentros[i + 1]) i++;
                    double t = Math.Log(f / AfinaCentros[i]) / Math.Log(AfinaCentros[i + 1] / AfinaCentros[i]);
                    e[b] = (float)(c[i] + (c[i + 1] - c[i]) * t);
                }
            }
            return e;
        }
        // de Form1.Datos.cs
        // Al cambiar el interruptor en vivo: el bajo de la que suena y el de la siguiente ya preparada se recalculan con la
        // vara nueva (el realce lo recalcula ApplyNormNow, que va detras). La rampa del bajo hace el resto.
        private void AplicaALaMedida()
        {
            if (nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null) bassSongFactor = BassFactorFor(nowPlaying.Song.Path);
            CadenaCancion p = cadenaPreparada;
            if (p != null && p.Cancion != null) p.BajoFactor = BassFactorFor(p.Cancion.Path);
        }
        // de GraveMono.cs
        // Lleva los interruptores a lo que ya suena y a la siguiente ya preparada: se oye al instante.
        private void AplicaBocinas()
        {
            CadenaCancion c = cadenaSonando, p = cadenaPreparada;
            AjusteLocal lc = LocalDe(c), lp = LocalDe(p);   // los mandos o, con el modo automatico, lo que decide para cada una (LocalAuto.cs)
            if (c != null && c.Mono != null) { c.Mono.Ajusta(lc.Estrecho, lc.Subgrave); c.Mono.SoloSiHaceFalta(lc.EstrechoSoloSiHaceFalta); }
            if (p != null && p.Mono != null) { p.Mono.Ajusta(lp.Estrecho, lp.Subgrave); p.Mono.SoloSiHaceFalta(lp.EstrechoSoloSiHaceFalta); }
            // Y los arreglos de la grabacion (ArreglosGrabacion.cs): el canal mudo y la continua.
            if (c != null && c.Arreglo != null) c.Arreglo.Ajusta(CanalMudoEf, QuitaContinuaEf);   // en el modo billar, siempre (CadenaBillar.cs)
            if (p != null && p.Arreglo != null) p.Arreglo.Ajusta(CanalMudoEf, QuitaContinuaEf);
            if (c != null && c.Zumbido != null) c.Zumbido.Ajusta(QuitaZumbidoEf);   // T6 (Zumbido.cs)
            if (p != null && p.Zumbido != null) p.Zumbido.Ajusta(QuitaZumbidoEf);
            if (c != null && c.PreEco != null) c.PreEco.Pasa = !quitaPreEco || sinMejoras;   // R2 (PreEco.cs): apagarlo se oye al instante
            if (p != null && p.PreEco != null) p.PreEco.Pasa = !quitaPreEco || sinMejoras;
            if (c != null && c.Eses != null) c.Eses.Ajusta(lc.Eses);   // T9 (Eses.cs): el mando, o lo que decide el modo del billar
            if (p != null && p.Eses != null) p.Eses.Ajusta(lp.Eses);
            // Y los del local (SalaLocal.cs): claridad, partes suaves, balance, graves y agudos; y la presencia en sala ruidosa (PresenciaSala.cs).
            if (c != null && c.Sala != null) { c.Sala.Ajusta(lc.Claridad, lc.Suaves, balanceDb); c.Sala.AjustaTono(lc.Graves, lc.Agudos); }
            if (p != null && p.Sala != null) { p.Sala.Ajusta(lp.Claridad, lp.Suaves, balanceDb); p.Sala.AjustaTono(lp.Graves, lp.Agudos); }
            if (c != null && c.Presencia != null) c.Presencia.Ajusta(lc.Presencia);
            if (p != null && p.Presencia != null) p.Presencia.Ajusta(lp.Presencia);
            AjustaVozGolpe(c); AjustaVozGolpe(p);   // bombo, retumbe y ataque (VozGolpeYRetumbe.cs)
        }
        // de Form1.Datos.cs
        // Aplica el nivelado a la cancion que suena AHORA (para oir el cambio al instante mientras se ajusta en Ctrl+F3).
        private void ApplyNormNow()
        {
            LevelTap t = curTap; if (t != null && nowPlaying != null && nowPlaying.Song != null) { t.Gain = GananciaDe(nowPlaying.Song); t.Excite = ExciteAmountFor(nowPlaying.Song.Path); t.Master = MasterEfectivo(); t.EqDb = EqCorrFor(nowPlaying.Song.Path); }
            DeepBassProvider bp = curBass; if (bp != null) bp.Amount = BassAmountNow();   // bajo psicoacustico: al instante, ya ajustado a la cancion
            SpectralTap sp = curSbr; if (sp != null) sp.Active = DseeEfectivo;   // reconstruccion espectral: alterna EN VIVO (A/B desde Ctrl+F3)
            AplicaAPreparada();   // y a la siguiente, si ya esta preparada para entrar sin pausa
            AplicaSinMejoras(cadenaSonando);   // "Escuchar sin mejoras" (S4, LoQueSuena.cs): encima de lo de siempre
        }
        // de AfinarLocal.cs
        // EL BAJO SEGUN LO MEDIDO (tercera tanda, 27-10-2026). Con la afinacion en uso se sabe cuanto grave dan de verdad las bocinas a 63 Hz
        // frente a la zona de la voz (y a la curva de casa). Si dan poco, el bajo psicoacustico (que fabrica armonicos que si se oyen) sube su
        // cantidad; si dan de sobra, la baja. El microfono de un celular suele leer de menos a 63 Hz: los primeros 4 dB que falten no
        // cuentan (y 2 dB de sobra tampoco). 12 dB que falten de verdad = el doble... topado: entre 0,75 y 1,4 veces. 1 = como siempre. Pura.
        internal const float BajoMedidoHolguraFalta = 4f, BajoMedidoHolguraSobra = 2f, BajoMedidoMin = 0.75f, BajoMedidoMax = 1.4f;
        // de CompararCiegas.cs
        internal const int CiegoBillar = 0, CiegoAfinacion = 1, CiegoPresencia = 2;   // que se compara
        // de AfinarLocal.cs
        // La curva de casa para un bar (dB frente a la zona de la voz): un poco mas de grave y menos de agudo que plano.
        internal static readonly float[] CurvaCasa = { 2f, 1.5f, 0.5f, 0f, 0f, -0.5f, -1.5f, -3f };
        // de NivelDisco.cs
        // La mas fuerte y la mas baja de un disco (dB de la potencia por canal) pueden llevarse hasta esto y seguir siendo un
        // disco; mas, es un recopilatorio y se nivela cancion por cancion. Las pistas con menos de MinBloquesDispersion
        // bloques de 400 ms (30 s) no entran en esa cuenta.
        internal const float DispersionDiscoDb = 5f;
        // de Form1.Sonido.cs
        private string EqFile() { return RutaDatos("rocola_eq.txt"); }
        // de Form1.Datos.cs
        // FUERZA del EQ cuando se deja en AUTOMATICO (de fabrica). No hay perilla que ajustar: la fuerza sale
        // de cuanto se puede uno FIAR del objetivo, que es lo unico que de verdad deberia decidirla.
        //   - Con pocas canciones medidas, el "sonido de la casa" todavia no significa nada: se corrige poco.
        //   - Segun el pre-analisis va midiendo la biblioteca, el objetivo es cada vez mas fiable y se corrige
        //     mas, hasta un tope que nunca llega al 100% (dejar un resto sin corregir conserva el caracter de
        //     cada disco: la idea es emparejar, no aplanar).
        //   - Con el modelo de IA entrenado el objetivo es POR CANCION en vez de uno para todas, asi que se
        //     puede afinar un poco mas sin miedo.
        // Menos de 10 canciones sanas medidas: cero. Ahi el EQ no toca nada, como si estuviera apagado.
        private float EqFuerzaAuto()
        {
            if (eqAvg == null || eqAvgSanas < 10) return 0f;
            float t = (eqAvgSanas - 10) / 50f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float f = 0.35f + 0.50f * t;      // 35% con 10 canciones medidas -> 85% con 60 o mas
            if (!EqNet.Loaded) f *= 0.88f;    // sin modelo el objetivo es el promedio de TODAS: mas prudente
            return f;
        }
        // de Form1.Datos.cs
        // El OBJETIVO del EQ automatico. Antes era el promedio de TODA la biblioteca, y ahi estaba el problema:
        // si la mitad de tus canciones son MP3 flojos sin agudos, ese promedio viene apagado y el EQ estaba
        // empujando a TODAS hacia esa mediocridad, incluso a las que llegaron completas. Ahora el objetivo se
        // saca SOLO de las canciones sanas -las que conservan sus agudos, que es justo lo que el DSEE ya sabe
        // detectar-, asi que el objetivo pasa de ser "el promedio de lo que tengo" a "como suena lo que esta
        // bien grabado". Si no hay suficientes sanas (biblioteca entera de baja calidad) se usan todas, como antes.
        private const float EqSanaHz = 19300f;   // mismo umbral con el que el DSEE da una cancion por completa
        // de Form1.Ia.cs
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
        private sealed class GenerosIa
        {
            internal Dictionary<string, string> PorCarpeta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<ulong, string> PorCancion = new Dictionary<ulong, string>();
        }
        // de Form1.Ia.cs
        private static string IaGeneroFiable(string genero)
        {
            string g = (genero ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim().ToLowerInvariant();
            if (g.Length == 0 || g == "unknown" || g == "desconocido" || g == "sin genero" || g == "<unknown>") return "";
            return g.Length <= 128 ? g : g.Substring(0, 128);
        }
        // de Sonoridad.cs
        private string IntegFile() { return RutaDatos("rocola_sonoridad.txt"); }
        // de Form1.Sonido.cs
        private string LoudFile() { return RutaDatos("rocola_volumen.txt"); }
        // de NivelDisco.cs
        internal const int MinBloquesDispersion = 300;
        // de Sonoridad.cs
        // El P90 con el que se nivela (lineal): solo el de una medida integrada del algoritmo 5.
        private bool P90ParaNivelar(string path, out float p90Lin)
        {
            p90Lin = 0f;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            SonoridadIntegrada s;
            if (path == null || t == null || !t.TryGetValue(path, out s) || !s.Valida || !s.ConP90) return false;
            p90Lin = DbALin(s.P90Db);
            return true;
        }
        // de Sonoridad.cs
        // LOS PICOS CON LOS QUE SE NIVELA (lineales): solo los de una medida integrada del algoritmo 2 o posterior.
        private bool PicosParaNivelar(string path, out float picoLin, out float p99Lin)
        {
            picoLin = p99Lin = 0f;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            SonoridadIntegrada s;
            if (path == null || t == null || !t.TryGetValue(path, out s) || !s.Valida || !s.ConPicos) return false;
            picoLin = DbALin(s.PicoDb); p99Lin = DbALin(s.P99Db);
            return true;
        }
        // de Form1.Datos.cs
        private void RecomputeEqAvg()
        {
            ComputeEqAverage(eqCache, ref eqAvg, ref eqAvgSanas, ref eqAvgSoloSanas);
            RememberEqAverage();   // solo despues de completar el calculo, nunca si lanza una excepcion
        }
        // de SubirTodo.cs
        // El techo de la biblioteca (dB), guardado. 'medidas' dice con cuantas canciones se calculo.
        private float TechoSubirDb(out int medidas)
        {
            int ahora = Environment.TickCount;
            bool ajustes = subirObj != normTarget || subirFuerza != normStrength || subirNivela != normalizeOn
                           || !object.ReferenceEquals(subirTabla, integCache);   // tabla nueva (la del arranque): se rehace ya
            bool nuevas = subirCambios != integCambios && unchecked(ahora - subirCuando) >= SubirRehacerMs;
            if (subirHecho && !ajustes && !nuevas) { medidas = subirMedidas; return subirTecho; }
            if (subirHecho && !ajustes && RehaceTechoEnFondo(ahora)) { medidas = subirMedidas; return subirTecho; }   // medidas nuevas: fuera
            subirHecho = true; subirCuando = ahora; subirCambios = integCambios;
            subirObj = normTarget; subirFuerza = normStrength; subirNivela = normalizeOn; subirTabla = integCache;
            Dictionary<string, SonoridadIntegrada> t = integCache;
            int n;
            subirTecho = TechoDeTabla(t != null ? t.Values : null, normalizeOn, normTarget, normStrength, out n);
            subirMedidas = n;
            medidas = n;
            return subirTecho;
        }
        // de Form1.Datos.cs
        // Tope de subida (dB) para una sonoridad dada. Entre punto y punto de la escalera se INTERPOLA en vez
        // de saltar de golpe: con topes en escalon, una cancion apenas mas fuerte que una frontera terminaba
        // MAS BAJITA que una mas callada (un salto audible justo en la frontera). Interpolando, el resultado
        // sube siempre de forma ordenada: mas fuerte de origen nunca acaba sonando mas bajo.
        private static float TierCapDb(float rms)
        {
            if (rms <= 0.0001f) return TierMaxDb[0];
            float db = (float)(20.0 * Math.Log10(rms));
            if (db <= TierDb[0]) return TierMaxDb[0];
            for (int i = 0; i < TierDb.Length - 1; i++)
            {
                if (db <= TierDb[i + 1])
                {
                    float f = (db - TierDb[i]) / (TierDb[i + 1] - TierDb[i]);
                    return TierMaxDb[i] + f * (TierMaxDb[i + 1] - TierMaxDb[i]);
                }
            }
            return TierMaxDb[TierMaxDb.Length - 1];
        }
        // de Form1.Datos.cs
        // I1, A LA MEDIDA DE CADA CANCION (interruptor de Ctrl+F3, apagado de fabrica: "a_la_medida="). Con el
        // interruptor encendido y el modelo del EQ cargado, el bajo y el realce se comparan con el objetivo de ESTA
        // cancion (MetaDeCancion) y no con el promedio de toda la biblioteca: una balada de piano y un corrido dejan de
        // medirse con la misma vara. Sin modelo, o apagado, todo como antes.
        private bool aLaMedida;
        // de AfinarLocal.cs
        // Las ocho lecturas (un solo punto desde la sexta tanda; antes hubo hasta tres, que se promediaban).
        private static readonly float[] afinadoDb = { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
        // de Form1.cs
        private List<Album> albums = new List<Album>();
        // de Form1.SinPausa.cs
        private CadenaCancion cadenaSonando;     // la de lo que la pantalla da por sonando
        // de Form1.Datos.cs
        private bool diagOn = false;      // "diagnostico=" en config: apuntar eventos clave en rocola_diagnostico.txt
        // de Form1.Sonido.cs
        private int eqAverageVersion, eqAverageCount;
        // de Form1.Sonido.cs
        private bool eqAverageReady;
        // de Form1.Sonido.cs
        private Dictionary<string, float[]> eqAverageSource;
        // de Form1.Sonido.cs
        private readonly object eqSaveLock = new object();
        // de Form1.Sonido.cs
        private readonly GuardadoReciente eqSaveQueue = new GuardadoReciente();
        // de Form1.Sonido.cs
        private int eqSaveVersion;
        // de Form1.Datos.cs
        private static readonly Dictionary<string, EstadoRespaldo> estadosRespaldo =
            new Dictionary<string, EstadoRespaldo>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Ia.cs
        private static volatile GenerosIa generosIa;
        // de Sonoridad.cs
        private List<string> integAjenas = new List<string>();   // registros de un algoritmo posterior: intactos
        // de Sonoridad.cs
        private int integEstado;             // 0 aun sin leer; 1 leida (o no existia): se puede guardar; 2 ajena o ilegible
        // de Sonoridad.cs
        private readonly object integGuardaCerrojo = new object();
        // de Sonoridad.cs
        private GuardadoReciente integGuardaCola;
        // de Sonoridad.cs
        private long integGuardaVersion;
        // de Sonoridad.cs
        private long integGuardados;         // hasta que cambio llego al disco (lo escribe el hilo que guarda)
        // de Form1.Sonido.cs
        // Guarda la tabla de sonoridades. Para no frenar el cambio de cancion en bibliotecas GRANDES (el archivo
        // puede tener miles de lineas), se toma una COPIA rapida en el hilo de la interfaz y el volcado a disco se
        // hace en SEGUNDO PLANO. Un candado asegura un solo escritor a la vez. Si algo truena, se ignora (la medida
        // se vuelve a aprender sola). Un archivo perdido/roto no rompe nada: se reconstruye oyendo las canciones.
        private readonly object loudSaveLock = new object();
        // de Form1.Sonido.cs
        private readonly GuardadoReciente loudSaveQueue = new GuardadoReciente();
        // de Form1.Sonido.cs
        private int loudSaveVersion;
        // de Form1.Fondo.cs
        private float masterDb = 0f;         // SUBIR TODO: retirado el 25-10-2026 (SubirTodo.cs); siempre 0, "subir_db=" se ignora
        // de Form1.Fondo.cs
        private float normStrength = 1.0f;                                                    // FUERZA del nivelado 0..1 (cuanto se acerca al objetivo; 0 = no toca nada)
        // de AfinarLocal.cs
        internal static readonly float[] AfinaCentros = { 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f };
        // de Form1.SinPausa.cs
        // Los ajustes en vivo de Ctrl+F3 tambien le llegan a la siguiente ya preparada: si aun no
        // suena, empieza con lo nuevo (sin rampa); si ya entro, lo toma con la rampa de siempre.
        private void AplicaAPreparada()
        {
            CadenaCancion p = cadenaPreparada;
            if (p == null || p.Cancion == null || p.Tap == null) return;
            p.Tap.Gain = GananciaDe(p.Cancion);
            p.Tap.Excite = ExcitePara(p);
            p.Tap.Master = MasterEfectivo();
            p.Tap.EqDb = EqCorrFor(p.Cancion.Path);
            if (p.Bajo != null) p.Bajo.Amount = BassAmountPara(p.BajoFactor, p.BajoBase);
            if (p.Sbr != null) p.Sbr.Active = DseeDe(p);
            AplicaSinMejoras(p);   // "Escuchar sin mejoras" (S4, LoQueSuena.cs): la siguiente tambien, si esta puesto
        }
        // de Form1.Sonido.cs
        private float BassAmountNow() { return Clampf(BassBaseNow() * bassSongFactor * BajoMedido, 0f, 1f); }   // BajoMedido: 1 salvo en el billar afinado (AfinarLocal.cs)
        // de Form1.Datos.cs
        private static void ComputeEqAverage(Dictionary<string, float[]> eqCache, ref float[] eqAvg, ref int eqAvgSanas, ref bool eqAvgSoloSanas)
        {
            if (eqCache.Count < 10) { eqAvg = null; eqAvgSanas = 0; return; }
            float[] avg = new float[8]; int n = 0;
            foreach (KeyValuePair<string, float[]> kv in eqCache)
                if (kv.Value[EqFingerprint] == EqVersionOriginal && kv.Value[EqCut] >= EqSanaHz) { for (int b = 0; b < 8; b++) avg[b] += kv.Value[b]; n++; }
            eqAvgSoloSanas = (n >= 10);
            if (!eqAvgSoloSanas)   // aun no hay bastantes sanas medidas: se usa todo, como se hacia antes
            {
                for (int b = 0; b < 8; b++) avg[b] = 0f;
                n = 0;
                foreach (KeyValuePair<string, float[]> kv in eqCache) if (kv.Value[EqFingerprint] == EqVersionOriginal) { for (int b = 0; b < 8; b++) avg[b] += kv.Value[b]; n++; }
            }
            if (n < 10) { eqAvg = null; eqAvgSanas = 0; return; }
            for (int b = 0; b < 8; b++) avg[b] /= n;
            eqAvg = avg; eqAvgSanas = n;
        }
        // de SubirTodo.cs
        // CON MEDIDAS NUEVAS, LA CUENTA FUERA DE LA PANTALLA: se copian los valores (la tabla es de la pantalla) y los
        // ajustes de ahora, se cuenta en otro hilo y el resultado vuelve a la pantalla, que lo pone si nada cambio
        // mientras. Hasta entonces vale el de antes. Sin ventana a la que volver (y sin prueba que lo pida), false: se
        // cuenta aqui, como antes.
        // Rocola OS: sin ventana a la que volver, se cuenta aqui, como en XP sin ventana (y «Subir TODO» esta retirado: masterDb = 0).
        private bool RehaceTechoEnFondo(int ahora) { return false; }
        // de Form1.Datos.cs
        private void RememberEqAverage()
        {
            eqAverageSource = eqCache; eqAverageVersion = eqCacheVer;
            eqAverageCount = eqCache.Count; eqAverageReady = true;
        }
        // de SubirTodo.cs
        private const int SubirRehacerMs = 60000;         // con medidas nuevas, se rehace como mucho una vez por minuto
        // de SubirTodo.cs
        // La cuenta entera, sin tocar nada de la ventana (sirve en la pantalla y fuera): el techo y con cuantas se hizo.
        private static float TechoDeTabla(ICollection<SonoridadIntegrada> valores, bool nivela, float objetivo, float fuerza, out int medidas)
        {
            int n = 0; float[] h = null;
            if (valores != null)
            {
                h = new float[valores.Count];
                foreach (SonoridadIntegrada s in valores)
                {
                    if (!s.Valida || !s.ConPicos || s.Muda || float.IsNaN(s.PicoDb) || s.PicoDb <= PicosBloques.Nada) continue;
                    float g = GananciaDeTabla(s, nivela, objetivo, fuerza);
                    h[n++] = s.ConP90 ? HolguraAlLimiteDb(g, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db)) : HolguraDb(g, DbALin(s.PicoDb), DbALin(s.P99Db));
                }
            }
            medidas = n;
            return (n >= SubirMinMedidas) ? TechoDeHolguras(h, n) : float.NaN;
        }
        // de Form1.Datos.cs
        // ESCALERA DE NIVELADO (etapas por que tan bajita viene la cancion). Antes habia UN solo tope de subida
        // (+15 dB) para todas; ahora cada etapa tiene el SUYO: una cancion ULTRA BAJA puede subir mucho mas (+20 dB)
        // para alcanzar al resto, mientras que una que ya viene FUERTE apenas se toca (no necesita ayuda y subirla
        // solo gastaria margen). Umbrales en dBFS de la sonoridad medida (K-weighted), de mas bajita a mas fuerte.
        private static readonly float[] TierDb = { -34f, -28f, -23f, -19f, -15f, -11f };        // puntos de la escalera (dBFS)
        // de Form1.Datos.cs
        // Tope de subida (dB) en cada punto. Las canciones BAJITAS se suben mas que antes (una grabacion vieja
        // y floja se quedaba corta y se oia menos que el resto); de la mitad de la escalera para arriba casi no
        // cambia nada, porque esas ya llegaban de sobra. Antes: 20/16/13/10/7/4.
        private static readonly float[] TierMaxDb = { 24f, 19f, 14.5f, 10.8f, 7.4f, 4.3f };
        // de Form1.Sonido.cs
        // AJUSTE POR CANCION. La perilla dice cuanto grave quiere el dueno del local; ESTE numero dice cuanto
        // le toca a la cancion que suena. No todas necesitan lo mismo: una con mucho grave profundo pierde
        // mucho en unas bocinas chicas y agradece la ayuda; una que ya viene cargada de bajo-medio no necesita
        // nada y si le metes mas, se emborrona. Sale de la huella tonal que la rocola YA le mide a cada cancion
        // (no hace falta medir nada nuevo). 1 = la perilla tal cual, que es lo que hacia antes con todas.
        private float bassSongFactor = 1f;
        // de Form1.Fondo.cs
        private DeepBassProvider curBass;   // el de la cancion que suena (para mover la perilla en vivo)
        // de Form1.cs
        private SpectralTap curSbr;          // tap de la cancion actual (para alternar A/B en vivo)
        // de Form1.Fondo.cs
        private LevelTap curTap;                                                              // tap de la cancion actual (para cambiarle la ganancia en vivo desde Ctrl+F3)
        // de Form1.Sonido.cs
        private int eqAvgSanas;                  // cuantas entraron en el promedio (se muestra en Ctrl+F3)
        // de Form1.Sonido.cs
        private bool eqAvgSoloSanas;
        // de SubirTodo.cs
        private long subirCambios = -1; private int subirCuando;
        // de SubirTodo.cs
        private float subirObj = -1f, subirFuerza = -1f; private bool subirNivela; private bool subirHecho;
        // de SubirTodo.cs
        private int subirMedidas;                         // canciones con picos que entraron en la cuenta
        // de SubirTodo.cs
        private object subirTabla;                         // la tabla con la que se calculo (al arrancar se carga entera de golpe)
        // de SubirTodo.cs
        private float subirTecho = float.NaN;             // dB; NaN = sin calcular
        // de Form1.Sonido.cs
        private float BassBaseNow() { return bassAuto || localAuto ? bassAutoBase : bassAmount; }   // el modo billar lo pone en automatico (CadenaBillar.cs)
        // de Form1.Sonido.cs
        // LO PROPIO DE ESTA RAMA PARA EL RELEVO SIN PAUSA (Form1.SinPausa.cs): sin IA en vivo, el reconstructor lo
        // decide el interruptor para todas las canciones, y el colchon de WaveOut es fijo.
        private bool DseeDe(CadenaCancion c) { return DseeEfectivo; }
        // de Form1.Sonido.cs
        private float ExcitePara(CadenaCancion c) { return ExciteAmountFor(c.Cancion.Path); }
        // de SubirTodo.cs
        private static float GananciaDeTabla(SonoridadIntegrada s, bool nivela, float objetivo, float fuerza)
        {
            if (!nivela || fuerza <= 0.001f) return 1f;
            if (s.ConP90) return GananciaAlLimite(fuerza, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db));
            float rms = s.RmsPorCanal;
            if (!(rms > 0.0001f)) return 1f;
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;
            return GananciaNivelado(objetivo, rms, fuerza, cap, true, DbALin(s.PicoDb), DbALin(s.P99Db));
        }
        // de SubirTodo.cs
        // Lo que le queda a una cancion medida con el algoritmo 5 (con el P90) hasta su limite sin rasparse, encima de su nivelado. Con
        // el nivelado al limite (24-10-2026) las niveladas ya estan en el: 0, y "Subir TODO" lo dice.
        internal static float HolguraAlLimiteDb(float ganancia, float picoLin, float p99Lin, float p90Lin)
        {
            float t = LimiteSinRaspar(picoLin, p99Lin, p90Lin) / Math.Max(ganancia, 1e-6f);
            if (!(t > 1f)) return 0f;
            return (float)(20.0 * Math.Log10(t));
        }
        // de SubirTodo.cs
        // Cuanto se puede subir UNA cancion, encima de su nivelado, sin que el limitador pase del tope (dB, >= 0).
        internal static float HolguraDb(float ganancia, float picoLin, float p99Lin)
        {
            float t = TopeSubidaPorPicos(picoLin, p99Lin) / Math.Max(ganancia, 1e-6f);
            if (!(t > 1f)) return 0f;
            return (float)(20.0 * Math.Log10(t));
        }
        // de SubirTodo.cs
        internal const int SubirMinMedidas = 20;          // con menos no se calcula techo (se aplica lo pedido)
        // de SubirTodo.cs
        // El techo: lo que aguantan todas menos el 10 % que menos aguanta. 'h' se ordena aqui mismo.
        internal static float TechoDeHolguras(float[] h, int n)
        {
            if (h == null || n <= 0) return float.NaN;
            Array.Sort(h, 0, n);
            int fuera = (int)Math.Floor(n * SubirFueraDelTope);
            if (fuera >= n) fuera = n - 1;
            return (float)(Math.Floor(h[fuera] * 10.0) / 10.0);   // a la decima, hacia abajo
        }
        // de SubirTodo.cs
        private bool subirEnFondo;                         // hay una cuenta del techo en marcha fuera de la pantalla
        // de BombaSonido.cs
        // EL ARRANQUE DE LA SALIDA CLASICA, SIN PARAR LA PANTALLA. En vez de llamar a Play ya (que espera a que el hilo
        // de la musica haga el primer segundo entero), se le pide al hilo que avise cuando lo tenga, y 'arranca' se hace
        // entonces en el hilo de la pantalla. Si mientras tanto se salto o se paro la cancion (otra salida u otra bomba),
        // no se hace nada. Sin ventana a la que volver (y sin prueba que lo pida) devuelve false: se arranca como antes.
        // de SubirTodo.cs
        internal const double SubirFueraDelTope = 0.10;   // canciones medidas que pueden pasar del tope al subir
        // de Form1.Sonido.cs
        private float bassAutoBase = 0.30f;        // lo que el programa eligio para la cancion que suena
        // de CompararCiegas.cs
        private int ciegoQue;
        // de Form1.Datos.cs
        private readonly System.Threading.ManualResetEvent dataWritesIdle = new System.Threading.ManualResetEvent(true);
        // de Form1.Datos.cs
        // Todos los volcados de datos que salen al ThreadPool pasan por aqui. Al cerrar se espera a que
        // terminen: un hilo de fondo no mantiene vivo el proceso y antes podia quedar la ultima medida a
        // medio camino. El candado evita la carrera Reset/Set cuando un trabajo diminuto termina enseguida.
        private readonly object dataWritesLock = new object();
        // de Form1.Datos.cs
        private int dataWritesPending;
        // de Sonoridad.cs
        // La medida integrada sola, para la tanda larga: descomprime la cancion entera y no hace nada mas (ni
        // huella del EQ, ni material para la IA). Cancelable en cada bloque, como MeasureFile.
        private bool MideIntegrada(string path, out SonoridadIntegrada s)
        {
            fuenteLeida = null;
            FuenteMedida fuenteInicio = CapturaFuente(path);
            s = new SonoridadIntegrada();
            if (path == null || EsVideo(path) || MeasurementCancelled()) return false;
            LectorMedida rd = null;
            TrabajoMedida trabajo = null;
            try
            {
                rd = new LectorMedida(path);
                rd.Volume = 1.0f;
                int tasa = rd.WaveFormat.SampleRate, ch = rd.WaveFormat.Channels;
                double total = rd.TotalTime.TotalSeconds;
                if (tasa < 8000 || ch < 1 || total <= 0.05 || total > 36000) return false;
                double[] pesos = DisposicionBs1770.DeArchivo(path, ch);
                if (pesos == null) return false;   // disposicion de altavoces desconocida: se queda la estimacion
                trabajo = TomaTrabajoMedida(tasa, ch, false);
                MedidorBs1770 m = trabajo.Sonoridad(tasa, pesos);
                float[] buf = trabajo.Buffer;
                while (true)
                {
                    if (MeasurementCancelled()) return false;
                    int n = rd.Read(buf, 0, trabajo.LecturaMax);
                    if (n <= 0) break;
                    m.Procesa(buf, 0, n);   // el medidor ya sabe completar un cuadro partido
                }
                if (MeasurementCancelled()) return false;
                return CierraIntegrada(m, total, tasa, out s) && TerminaFuenteLeida(path, fuenteInicio);
            }
            catch { s = new SonoridadIntegrada(); return false; }
            finally
            {
                if (rd != null) { try { rd.Dispose(); } catch { } }
                if (trabajo != null) trabajo.Suelta();
            }
        }
        // de Form1.Biblioteca.cs
        private static bool EsVideo(string ruta)
        {
            if (ruta == null) return false;
            string ext = null;
            try { ext = Path.GetExtension(ruta).ToLowerInvariant(); } catch { }
            return ext != null && Array.IndexOf(VideoExt, ext) >= 0;
        }
        // de Optimizaciones.cs
        private static bool MeasurementCancelled()
        {
            Func<bool> cancel = measureCancellation;
            return cancel != null && cancel();
        }
        // de Optimizaciones.cs
        private static TrabajoMedida TomaTrabajoMedida(int rate, int canales, bool tonal)
        {
            TrabajoMedida trabajo = trabajoMedidaDelHilo;
            if (canales > TrabajoMedida.MaxCanalesRetenidos || (trabajo != null && trabajo.EnUso))
                trabajo = new TrabajoMedida(); // formatos grandes/reentrada: temporales, sin limitar su analisis
            else if (trabajo == null) trabajoMedidaDelHilo = trabajo = new TrabajoMedida();
            trabajo.EnUso = true;
            try { trabajo.Prepara(rate, canales, tonal); return trabajo; }
            catch { trabajo.Suelta(); throw; }
        }
        // de Optimizaciones.cs
        // Solo scratch del analisis: ningun lector, formulario, ruta ni resultado publicado.
        // Una instancia por hilo; una llamada reentrante recibe otra y no pisa la medida externa.
        [ThreadStatic] private static TrabajoMedida trabajoMedidaDelHilo;
        private sealed class TrabajoMedida
        {
            internal const int MaxCanalesRetenidos = 8, MaxParesRetenidos = 65536;
            internal const int MaxBloquesRetenidos = 8192;
            internal bool EnUso;
            internal int LecturaMax;
            private int frecuencia, frecuenciaBandas;
            internal float[] C1, C2, Estado, Buffer, EstadoBandas;
            internal int[] Recorte;
            internal readonly float[][] Bandas = new float[8][];
            internal readonly double[] SumaBandas = new double[8];
            internal readonly float[] Cuerpo = new float[5], Features = new float[28], Target = new float[12];
            private readonly SpectralTap[] analisis = new SpectralTap[3];
            private readonly float[][] cuadros = new float[3][];
            private readonly System.Collections.Generic.List<float>[] pares = new System.Collections.Generic.List<float>[2];
            private MedidorBs1770 integrada;

            private static T[] Capacidad<T>(T[] buffer, int n)
            { return buffer != null && buffer.Length >= n ? buffer : new T[n]; }
            internal void Prepara(int rate, int canales, bool tonal)
            {
                if (frecuencia != rate)
                {
                    C1 = EqShelfHi(1681.974f, rate, 3.99984f, 0.70717f);
                    C2 = EqHighPass(38.1355f, rate, 0.50033f);
                    frecuencia = rate; frecuenciaBandas = 0;
                    Array.Clear(Bandas, 0, Bandas.Length);
                    Array.Clear(analisis, 0, analisis.Length); // solo se retiene un formato, no un diccionario
                }
                if (tonal && frecuenciaBandas != rate)
                {
                    for (int b = 0; b < Bandas.Length; b++) Bandas[b] = EqBpf(EqCentros[b], rate);
                    frecuenciaBandas = rate;
                }
                LecturaMax = checked(4096 * canales); // el conteo LOGICO original, no Buffer.Length
                Buffer = Capacidad(Buffer, LecturaMax); Estado = Capacidad(Estado, checked(canales * 8));
                EstadoBandas = Capacidad(EstadoBandas, checked(canales * 32)); Recorte = Capacidad(Recorte, canales);
                Array.Clear(Buffer, 0, Buffer.Length); Array.Clear(Estado, 0, Estado.Length);
                Array.Clear(EstadoBandas, 0, EstadoBandas.Length); Array.Clear(Recorte, 0, Recorte.Length);
                Array.Clear(SumaBandas, 0, SumaBandas.Length);
            }
            internal SpectralTap Analizador(int indice, int rate)
            {
                SpectralTap tap = analisis[indice];
                if (tap == null || !tap.ReiniciaAnalisis(rate)) analisis[indice] = tap = new SpectralTap(rate, 1);
                return tap;
            }
            internal float[] Cuadro(int indice, int largo)
            {
                // No sobredimensionar: el bucle usa Length como tamano de ventana.
                if (cuadros[indice] == null || cuadros[indice].Length != largo) cuadros[indice] = new float[largo];
                return cuadros[indice];
            }
            internal System.Collections.Generic.List<float> Pares(int indice)
            {
                if (pares[indice] == null) pares[indice] = new System.Collections.Generic.List<float>(4096);
                pares[indice].Clear();
                return pares[indice];
            }
            internal MedidorBs1770 Sonoridad(int rate, double[] pesos)
            {
                MedidorBs1770 m = integrada;
                if (m == null || !m.Reinicia(rate, pesos)) integrada = m = new MedidorBs1770(rate, pesos);
                return m;
            }
            internal void Suelta()
            {
                // Retener hasta 64 KiB de bloques; canciones largas se analizan enteras, sin dejarlos en cache.
                if (integrada != null && !integrada.MemoriaRetenible(MaxBloquesRetenidos)) integrada = null;
                for (int i = 0; i < pares.Length; i++)
                    if (pares[i] != null)
                    {
                        // Un entrenamiento a fondo puede crecer mucho: medirlo entero, pero no retenerlo entero.
                        if (pares[i].Capacity > MaxParesRetenidos) pares[i] = null;
                        else pares[i].Clear();
                    }
                EnUso = false;
            }
        }
        // de Form1.Video.cs
        private static readonly string[] VideoExt = { ".mp4", ".avi", ".wmv", ".mpg", ".mpeg", ".mov", ".mkv", ".flv", ".m4v", ".vob", ".divx", ".3gp", ".asf" };
        // de Optimizaciones.cs
        [ThreadStatic] private static Func<bool> measureCancellation;
    }
}

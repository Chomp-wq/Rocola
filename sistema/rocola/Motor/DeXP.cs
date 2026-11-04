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
            SpectralTap sp = curSbr; if (sp != null) sp.Active = DseePara(nowPlaying != null && nowPlaying.Song != null ? nowPlaying.Song.Path : null);   // reconstruccion espectral: alterna EN VIVO (A/B desde Ctrl+F3)
            AplicaAPreparada();   // y a la siguiente, si ya esta preparada para entrar sin pausa
            AplicaSinMejoras(cadenaSonando);   // "Escuchar sin mejoras" (S4, LoQueSuena.cs): encima de lo de siempre
        }
        // de AfinarLocal.cs
        // EL BAJO SEGUN LO MEDIDO (tercera tanda, 27-10-2026). Con la afinacion en uso se sabe cuanto grave dan de verdad las bocinas a 63 Hz
        // frente a la zona de la voz (y a la curva de casa). Si dan poco, el bajo psicoacustico (que fabrica armonicos que si se oyen) sube su
        // cantidad; si dan de sobra, la baja. El microfono de un celular suele leer de menos a 63 Hz: los primeros 4 dB que falten no
        // cuentan (y 2 dB de sobra tampoco). 12 dB que falten de verdad = el doble... topado: entre 0,75 y 1,4 veces. 1 = como siempre. Pura.
        internal const float BajoMedidoHolguraFalta = 4f, BajoMedidoHolguraSobra = 2f, BajoMedidoMin = 0.75f, BajoMedidoMax = 1.4f;
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
        private bool DseeDe(CadenaCancion c) { return DseePara(c != null && c.Cancion != null ? c.Cancion.Path : null); }
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
        // de Form1.Ia.cs
        // Al arrancar, en segundo plano (listar la carpeta y leer los modelos no para la pantalla): los modelos por genero
        // de la carpeta IAS (Form1.Datos.cs: CarpetasIas; los de fabrica no traen ninguno). Si el mismo genero esta en dos
        // sitios, manda el primero (IAS de la carpeta de datos). Hasta que esten, cada cancion usa el general.
        private static void CargaModelosPorGenero()
        {
            Dictionary<string, bool> vistos = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string carpeta in CarpetasIas())
            {
                try
                {
                    if (!Directory.Exists(carpeta)) continue;
                    foreach (string f in Directory.GetFiles(carpeta, PrefijoDseeGenero + "*.bin"))
                    {
                        if (!string.Equals(Path.GetExtension(f), ".bin", StringComparison.OrdinalIgnoreCase)) continue;
                        string slug = Path.GetFileNameWithoutExtension(f).Substring(PrefijoDseeGenero.Length);
                        if (slug.Length == 0 || slug != SlugGenero(slug)) continue;   // un nombre que no saldria de una etiqueta: no se usa
                        if (vistos.ContainsKey(slug)) continue;
                        RedChica r = new RedChica(28, 12);
                        r.TryLoad(f);
                        if (!r.Loaded) continue;   // roto o de otro contrato: puede valer el de la carpeta siguiente
                        vistos[slug] = true;
                        lock (dseeGeneros) dseeGeneros[slug] = r;
                    }
                }
                catch { }
            }
        }
        // de CancionEnMemoria.cs
        // Cada cancion en memoria es un arreglo de 1 a 32 MB: va al monton de objetos grandes, y pedir uno puede disparar una recoleccion de
        // generacion 2 (que en .NET 3.5 para todos los hilos). Con diagnostico=1 se apunta cuantas hubo mientras se pedia el arreglo, para
        // saber en la rocola real si eso es lo que se nota al cambiar de cancion (si no, no hace falta reusar los arreglos). Una diferencia de
        // contadores es una COINCIDENCIA en el tiempo (otro hilo pudo provocarla), no su duracion ni su causa.
        internal static volatile bool DiagMemoria;
        // de AfinarLocal.cs
        internal static void LeeAfinado(string texto)
        {
            if (texto == null) return;
            string[] p = texto.Split(';');
            float[] d = afinadoDb;
            for (int b = 0; b < 8; b++)
            {
                float v;
                d[b] = (b < p.Length && float.TryParse(p[b].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                                && v >= AfinaMinDb && v <= AfinaMaxDb) ? v : float.NaN;
            }
        }
        // de Form1.Biblioteca.cs
        private void LoadDeepDone()
        {
            try { ReadDeepDone(DeepDoneFile(), deepDone, loudCache, durCache, eqCache); } catch { }
        }
        // de Form1.Biblioteca.cs
        private void LoadDurFast()
        {
            try { ReadDurFast(DurFastFile(), durFast); } catch { }
        }
        // de Form1.Datos.cs
        // Tabla de huellas tonales (d0;d1;...;d7|ruta). Perdida o rota no rompe nada: se re-aprende oyendo.
        private void LoadEqCache()
        {
            try { if (!ReadEqCache(EqFile(), eqCache, ref eqCacheVer)) return; } catch { }
            if (medidasDeltasActivas) { cambiosEq = PreparaEq(eqCache); diarioEq = null; }
            EnsureEqAverage();
        }
        // de Sonoridad.cs
        // Si fallo el trabajador del arranque.
        private void LoadIntegCache()
        {
            Dictionary<string, SonoridadIntegrada> t = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
            List<string> aj = new List<string>();
            int estado;
            try { estado = LeeIntegArchivo(IntegFile(), null, t, aj); } catch { estado = 2; t.Clear(); aj.Clear(); }
            lock (integCerrojo) integCache = t;
            integAjenas = aj; integEstado = estado;
            if (medidasDeltasActivas) { cambiosIntegrada = PreparaIntegrada(t); diarioInteg = null; }
        }
        // de Form1.Datos.cs
        private void LoadLoudCache()
        {
            LoadFuentesMedidas();
            InvalidateVideoMeasurements();
            try { ReadLoudCache(LoudFile(), loudCache, durCache); } catch { }
            if (medidasDeltasActivas) { cambiosVolumen = PreparaVolumen(loudCache, durCache); diarioVolumen = null; }
        }
        // de Form1.cs
        private const float NormTargetMin = 0.06f, NormTargetMax = 0.40f;                     // rango del volumen objetivo que ofrece la interfaz (de flojo a muy fuerte)
        // de Form1.Datos.cs
        // Al arrancar, una vez, antes de cargarlos: los modelos sueltos en la carpeta de datos se pasan a IAS\. Es un
        // reemplazo en el mismo disco desde una copia temporal validada. Si IAS\ ya
        // tenia uno con ese nombre, el suelto es el que alguien acaba de dejar (como se hacia antes): manda el suelto y
        // el de IAS\ se guarda al lado como .anterior-N. Nunca se borra un modelo. Las copias .anterior-N que dejaba el
        // instalador de los de genero tambien se pasan. El reemplazo y su respaldo son atomicos: si no se puede
        // (solo lectura, archivo en uso, sistema de archivos sin reemplazo), queda activa la version anterior.
        // Si no se completa, el suelto se conserva y se puede reintentar al arrancar. Devuelve cuantos se pasaron.
        internal static int OrdenaIas()
        {
            int pasados = 0;
            try
            {
                string datos = CarpetaDatos, ias = CarpetaIas;
                if (!Directory.Exists(datos)) return 0;
                List<string> sueltos = new List<string>();
                foreach (string n in ModelosIa)
                {
                    string p = Path.Combine(datos, n);
                    if (File.Exists(p)) sueltos.Add(p);
                }
                // "*.bin" en Windows tambien encuentra ".bin<algo>": se mira la extension de verdad.
                foreach (string p in Directory.GetFiles(datos, PrefijoDseeGenero + "*.bin"))
                    if (string.Equals(Path.GetExtension(p), ".bin", StringComparison.OrdinalIgnoreCase)) sueltos.Add(p);
                List<string> anteriores = new List<string>(Directory.GetFiles(datos, PrefijoDseeGenero + "*.bin.anterior-*"));
                if (sueltos.Count == 0 && anteriores.Count == 0) return 0;
                if (!Directory.Exists(ias)) Directory.CreateDirectory(ias);
                foreach (string p in sueltos)
                {
                    string destino = Path.Combine(ias, Path.GetFileName(p)), respaldo = null, temporal = null;
                    try
                    {
                        // No admitir escrituras al suelto mientras se toma la copia. ReplaceFile necesita abrir SU
                        // fuente para escritura: usar el suelto bloqueado para lectura impediria reemplazarlo en
                        // Windows. La copia tiene nombre exclusivo, no esta abierta al promoverla y se valida entera.
                        using (FileStream guarda = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                        {
                            if (guarda.Length < 12 || guarda.Length > 9 * 1024 * 1024) continue;
                            temporal = Path.Combine(ias, ".modelo-validado-" + Guid.NewGuid().ToString("N") + ".tmp");
                            File.Copy(p, temporal, false);
                            bool eq = string.Equals(Path.GetFileName(p), "rocola_eq_ia.bin", StringComparison.OrdinalIgnoreCase);
                            RedChica candidata = new RedChica(eq ? EqNetIn : 28, eq ? EqNetOut : 12);
                            candidata.TryLoad(temporal);
                            if (!candidata.Loaded) continue;   // roto, bloqueado o de otro contrato: no desplazar al utilizable
                            if (File.Exists(destino))
                            {
                                respaldo = LibreAnterior(destino);
                                File.Replace(temporal, destino, respaldo);
                            }
                            else File.Move(temporal, destino);
                            temporal = null;
                            // Solo retirar el duplicado cuando su copia validada ya esta activa. Los bytes anteriores
                            // siguen en el respaldo. Un fallo aqui solo deja el suelto duplicado, nunca pierde el modelo.
                            try { File.Delete(p); } catch { }
                        }
                        pasados++;
                    }
                    catch
                    {
                        // Defensa adicional si el sistema de archivos fallo despues de crear el respaldo. No borrar
                        // ese respaldo ni pisar un destino que si exista. Un bloqueo ordinario no llega a mover nada.
                        if (respaldo != null && !File.Exists(destino) && File.Exists(respaldo))
                            try { File.Copy(respaldo, destino, false); } catch { }
                    }
                    finally { if (temporal != null) try { File.Delete(temporal); } catch { } }
                }
                foreach (string p in anteriores)
                {
                    try
                    {
                        string destino = Path.Combine(ias, Path.GetFileName(p));
                        if (File.Exists(destino)) continue;   // ya hay uno con ese nombre: no se pisa
                        File.Move(p, destino);
                        pasados++;
                    }
                    catch { }
                }
            }
            catch { }
            return pasados;
        }
        // de AfinarLocal.cs
        internal static int PonderacionDe(string texto)
        {
            string t = (texto ?? "").Trim().ToUpperInvariant();
            return t == "A" ? 0 : t == "Z" ? 2 : 1;
        }
        // de Form1.Datos.cs
        // Un archivo que la rocola LEE pero no escribe -un modelo de IA-: el primero que aparezca en CarpetasIas. Si no
        // hay ninguno, la ruta de IAS\ (no se carga nada y todo sigue sin esa IA, como siempre).
        private static string RutaLectura(string nombre)
        {
            string[] cs = CarpetasIas();
            foreach (string c in cs)
            {
                string a = Path.Combine(c, nombre);
                try { if (File.Exists(a)) return a; } catch { }
            }
            return Path.Combine(cs[0], nombre);
        }
        // de Form1.Sonido.cs
        private bool sbrForce = false;       // dsee_forzar=1 en config: salta la puerta (para pruebas)
        // de AfinarLocal.cs
        internal const int AfinaSinMedir = 39, AfinaMinDb = 40, AfinaMaxDb = 120;   // el deslizador: 39 = sin medir
        // de Form1.Datos.cs
        internal static string CarpetaIas { get { return Path.Combine(CarpetaDatos, NombreCarpetaIas); } }
        // de Form1.Datos.cs
        // Donde se buscan, por orden: IAS\ de la carpeta de datos, la carpeta de datos (donde iban antes: si no se pudo
        // ordenar, se siguen leyendo alli), y lo mismo junto al programa, para los de fabrica: dentro del .sfs, que es de
        // solo lectura, no se pueden mover. Sin repetir carpeta (sin ROCOLA_DATOS, datos y programa son la misma).
        private static string[] CarpetasIas()
        {
            string prog = AppContext.BaseDirectory.TrimEnd('/');   // Rocola OS: la carpeta del programa (en XP, Application.StartupPath)
            string[] todas = { CarpetaIas, CarpetaDatos, Path.Combine(prog, NombreCarpetaIas), prog };
            List<string> r = new List<string>(4);
            foreach (string c in todas)
            {
                bool ya = false;
                foreach (string x in r)
                    if (string.Equals(x.TrimEnd('\\', '/'), c.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) ya = true;
                if (!ya) r.Add(c);
            }
            return r.ToArray();
        }
        // de Form1.Datos.cs
        // ---- Canciones ya repasadas A FONDO (mismo formato que las de la IA: una ruta por linea) ----
        private string DeepDoneFile() { return RutaDatos("rocola_afondo.txt"); }
        // de Form1.Datos.cs
        // ---- Duraciones rapidas: "segundos|ruta" por linea, escritas POR ANADIDO ----
        private string DurFastFile() { return RutaDatos("rocola_duracion.txt"); }
        // de Sonoridad.cs
        // Lee la tabla de un archivo (o de su copia .bak si el bueno falta). Devuelve el estado: 1 si se puede
        // guardar encima (leida, o no habia nada), 2 si no se debe tocar (cabecera ajena o archivo ilegible).
        // Una cancelacion del arranque sale como OperationCanceledException.
        internal static int LeeIntegArchivo(string ruta, Func<TextReader, TextReader> envuelve,
                                             Dictionary<string, SonoridadIntegrada> tabla, List<string> ajenas)
        {
            string f = File.Exists(ruta) ? ruta : (File.Exists(ruta + ".bak") ? ruta + ".bak" : null);
            if (f == null) return 1;
            try
            {
                using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
                using (TextReader r = envuelve != null ? envuelve(LeeLineasSanas(fs)) : LeeLineasSanas(fs))   // solo lineas enteras
                    return LeeIntegRegistros(r, tabla, ajenas) ? 1 : 2;
            }
            catch (OperationCanceledException) { throw; }
            catch { tabla.Clear(); ajenas.Clear(); return 2; }
        }
        // de Form1.Datos.cs
        private static string LibreAnterior(string ruta)
        {
            for (int k = 1; ; k++)
            {
                string a = ruta + ".anterior-" + k.ToString(CultureInfo.InvariantCulture);
                if (!File.Exists(a)) return a;
            }
        }
        // de Form1.Datos.cs
        internal static readonly string[] ModelosIa = { "rocola_ia.bin", "rocola_eq_ia.bin" };
        // de Form1.Ia.cs
        internal const string PrefijoDseeGenero = "rocola_ia_genero_";
        // de Form1.Biblioteca.cs
        private static void ReadDeepDone(string f, HashSet<string> deepDone, Dictionary<string, float> loudCache, Dictionary<string, float> durCache, Dictionary<string, float[]> eqCache)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadDeepDoneRecords(reader, deepDone, loudCache, durCache, eqCache);
            }
            catch { }
        }
        // de Form1.Biblioteca.cs
        private static void ReadDurFast(string f, Dictionary<string, float> durFast)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadDurFastRecords(reader, durFast);
            }
            catch { }
        }
        // de Form1.Datos.cs
        private static bool ReadEqCache(string f, Dictionary<string, float[]> eqCache, ref int eqCacheVer)
        {
            try
            {
                if (!File.Exists(f)) return false;
                using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (TextReader reader = LeeLineasSanas(fs))   // solo lineas enteras (DiarioTablas.cs)
                    ReadEqCacheRecords(reader, eqCache, ref eqCacheVer);
            }
            catch { }
            return true;
        }
        // de Form1.Datos.cs
        private static void ReadLoudCache(string f, Dictionary<string, float> loudCache, Dictionary<string, float> durCache)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (TextReader reader = LeeLineasSanas(fs))   // solo lineas enteras (DiarioTablas.cs)
                    ReadLoudCacheRecords(reader, loudCache, durCache);
            }
            catch { }
        }
        // de Form1.Datos.cs
        // LAS IAS, EN SU CARPETA (14-10-2026; peticion: «como son muchas IAs, haz que el programa lea las IAs dentro de
        // una carpeta llamada IAS, para asi tener mas orden»). Los modelos entrenados -rocola_ia.bin (DSEE),
        // rocola_eq_ia.bin (EQ automatico) y rocola_ia_genero_*.bin (DSEE por genero)- viven en IAS\ dentro de la
        // carpeta de datos. El material para entrenar (rocola_ia_datos.*) no es un modelo: sigue donde estaba.
        internal const string NombreCarpetaIas = "IAS";
        // de Form1.Biblioteca.cs
        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadDeepDoneRecords(TextReader reader, HashSet<string> deepDone, Dictionary<string, float> loudCache, Dictionary<string, float> durCache, Dictionary<string, float[]> eqCache)
        {
            HashSet<string> pending = new HashSet<string>(deepDone.Comparer);
            FillDeepDoneRecords(reader, pending, loudCache, durCache, eqCache);
            foreach (string item in pending) deepDone.Add(item);
        }
        // de Form1.Biblioteca.cs
        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadDurFastRecords(TextReader reader, Dictionary<string, float> durFast)
        {
            Dictionary<string, float> pending = new Dictionary<string, float>(durFast.Comparer);
            FillDurFastRecords(reader, pending);
            foreach (KeyValuePair<string, float> item in pending) durFast[item.Key] = item.Value;
        }
        // de Form1.Datos.cs
        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadEqCacheRecords(TextReader reader, Dictionary<string, float[]> eqCache, ref int eqCacheVer)
        {
            Dictionary<string, float[]> pending = new Dictionary<string, float[]>(eqCache.Comparer);
            int updates = FillEqCacheRecords(reader, pending);
            foreach (KeyValuePair<string, float[]> item in pending) eqCache[item.Key] = item.Value;
            eqCacheVer += updates;
        }
        // de Form1.Sonido.cs
        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadLoudCacheRecords(TextReader reader, Dictionary<string, float> loudCache, Dictionary<string, float> durCache)
        {
            Dictionary<string, float> pending = new Dictionary<string, float>(loudCache.Comparer);
            Dictionary<string, float> durations = new Dictionary<string, float>(durCache.Comparer);
            FillLoudCacheRecords(reader, pending, durations);
            foreach (KeyValuePair<string, float> item in pending) loudCache[item.Key] = item.Value;
            foreach (KeyValuePair<string, float> item in durations) durCache[item.Key] = item.Value;
        }
        // de Form1.Biblioteca.cs
        private static void FillDeepDoneRecords(TextReader reader, HashSet<string> pending, Dictionary<string, float> loudCache, Dictionary<string, float> durCache, Dictionary<string, float[]> eqCache)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string r = line.Trim(); if (r.Length == 0) continue;
                r = Pool(r);
                // Una marca que sobrevivio pero cuyas tablas no llegaron al disco no es trabajo hecho.
                // Los videos no tienen huella EQ; para canciones se exigen las tres medidas.
                bool medida = loudCache.ContainsKey(r) && durCache.ContainsKey(r)
                           && (EsVideo(r) || eqCache.ContainsKey(r));
                if (medida) pending.Add(r);
            }
        }
        // de Form1.Biblioteca.cs
        private static void FillDurFastRecords(TextReader reader, Dictionary<string, float> pending)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int bar = line.IndexOf('|');
                if (bar <= 0 || bar >= line.Length - 1) continue;
                float d;
                if (!float.TryParse(line.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) || !(d > 0.01f) || !Finito(d)) continue;
                pending[Pool(line.Substring(bar + 1))] = d;
            }
        }
        // de Form1.Datos.cs
        // Solo para una tabla privada que todavia no se ha publicado.
        private static int FillEqCacheRecords(TextReader reader, Dictionary<string, float[]> pending)
        {
            int updates = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int bar = line.IndexOf('|');
                if (bar <= 0 || bar >= line.Length - 1) continue;
                float[] c = ReadEqValues(line, bar);
                if (c != null) { pending[Pool(line.Substring(bar + 1))] = c; updates++; }
            }
            return updates;
        }
        // de Form1.Sonido.cs
        private static void FillLoudCacheRecords(TextReader reader, Dictionary<string, float> pending, Dictionary<string, float> durations)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int bar = line.IndexOf('|');
                if (bar <= 0 || bar >= line.Length - 1) continue;
                float rms;
                if (!float.TryParse(line.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rms)
                    || !(rms > 0f) || !Finito(rms)) continue;   // el silencio apuntado (LoudMudo) TIENE que sobrevivir al reinicio; NaN o infinito, no
                // Formato NUEVO "rms|duracion|ruta" y el VIEJO "rms|ruta" (una ruta de Windows no puede llevar
                // '|', asi que distinguirlos es seguro): lo viejo se sigue leyendo tal cual, sin duracion.
                string rest = line.Substring(bar + 1);
                int bar2 = rest.IndexOf('|');
                float dur = 0f;
                if (bar2 > 0 && bar2 < rest.Length - 1
                    && float.TryParse(rest.Substring(0, bar2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dur))
                    rest = rest.Substring(bar2 + 1);
                else dur = 0f;
                rest = Pool(rest);
                pending[rest] = rms;
                if (dur > 0.01f && Finito(dur)) durations[rest] = dur;
            }
        }
        // de Form1.Datos.cs
        private static float[] ReadEqValues(string line, int bar)
        {
            int parts = 1;
            for (int i = 0; i < bar; i++) if (line[i] == ';') parts++;
            if (parts < 8 || parts > EqLen) return null;   // misma admision de formatos antiguos
            float[] values = new float[EqLen];   // los campos ausentes conservan +0
            int start = 0;
            for (int b = 0; b < parts; b++)
            {
                int end = line.IndexOf(';', start, bar - start);
                if (end < 0) end = bar;
                if (!float.TryParse(line.Substring(start, end - start), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out values[b]) || !Finito(values[b])) return null;   // NaN o infinito: fila rota
                start = end + 1;
            }
            return values;   // sin copiar toda la cabecera ni crear un array de subcadenas
        }
        // de Form1.Datos.cs
        // Cierra la medicion de huella tonal de la cancion que acaba de sonar (audio ya detenido).
        private void FinalizeEqMeasure()
        {
            string path = eqMeasPath; eqMeasPath = null;
            double[] sums = eqMeasSum; long n = eqMeasN; int tasa = eqMeasRate;
            eqMeasSum = new double[8]; eqMeasN = 0;
            CierraHuellaDeFuente(path, sums, n, tasa, eqMeasCut, eqMeasCutLo, curFuenteMedida);
        }
        // de Form1.Sonido.cs
        // Cierra la medida de la cancion que acaba de sonar: calcula su RMS integrado y lo guarda (si se oyo lo
        // suficiente). La llama DisposePlaybackNow, ya con el audio detenido (sin carrera con el hilo de sonido).
        private void FinalizeLoudMeasure()
        {
            string path = curMeasurePath; curMeasurePath = null;
            double sumSq = curSumSq; long samples = curSamples; curSumSq = 0; curSamples = 0;
            CierraSonoridadDeFuente(path, sumSq, samples, kwRate, kwCanales, curFuenteMedida);
        }
        // de BombaSonido.cs
        private void InvalidaPublicacionAudio() { lock (audioDatosLock) { unchecked { publicacionAudio++; } } }
        // de Form1.Biblioteca.cs
        // Igual que la lista de la IA: se ANADE al final, no se reescribe entera cada 10 canciones.
        private void SaveDeepDone()
        {
            List<string> nuevas;
            lock (deepDoneLock)
            {
                if (deepPend.Count == 0 || deepDoneSaving) return;
                nuevas = new List<string>(deepPend); deepDoneSaving = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                bool ok = false;
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    AnexaSeguro(DeepDoneFile(), sb.ToString());
                    ok = true;
                }
                catch { }
                finally
                {
                    lock (deepDoneLock)
                    {
                        if (ok) RetiraPrefijoGuardado(deepPend, nuevas);
                        deepDoneSaving = false; deepDoneDirty = deepPend.Count > 0;
                    }
                }
            });
        }
        // de Form1.Biblioteca.cs
        private void SaveDurFast()
        {
            List<string> nuevas;
            lock (durFastLock)
            {
                if (durFastPend.Count == 0 || durFastSaving) return;
                nuevas = new List<string>(durFastPend); durFastSaving = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                bool ok = false;
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    AnexaSeguro(DurFastFile(), sb.ToString());
                    ok = true;
                }
                catch { }
                finally
                {
                    lock (durFastLock)
                    {
                        if (ok) RetiraPrefijoGuardado(durFastPend, nuevas);
                        durFastSaving = false;
                    }
                }
            });
        }
        // de Form1.Sonido.cs
        private float eqMeasCut = 0f, eqMeasCutLo = 0f;   // cortes (arriba y abajo) de la cancion que suena (0 = aun no se sabe)
        // de Form1.Datos.cs
        private static void AnexaSeguro(string ruta, string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            byte[] datos = new UTF8Encoding(false).GetBytes(texto);
            lock (CandadoDe(ruta))
            {
                long valido = 0; bool reparar = false;
                try
                {
                    using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        valido = fs.Length;
                        if (valido > 0)
                        {
                            long pos = valido - 1; fs.Position = pos;
                            if (fs.ReadByte() != (byte)'\n')
                            {
                                reparar = true;
                                valido = UltimoLfAnexo(fs);
                            }
                        }
                    }
                }
                catch (FileNotFoundException) { }
                if (reparar)
                {
                    // Nunca truncar el original: una captura de respaldo puede seguir leyendo esa generacion.
                    // Solo el final corrupto exige este volcado; los anexos sanos siguen siendo O(tanda).
                    string tmp = ruta + ".tmp";
                    using (FileStream input = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (FileStream output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[65536]; long faltan = valido;
                        while (faltan > 0)
                        {
                            int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, faltan));
                            if (n == 0) throw new IOException("El anexo original se trunco durante su reparacion");
                            output.Write(buffer, 0, n); faltan -= n;
                        }
                        output.Write(datos, 0, datos.Length); output.Flush();
                        try { FlushFileBuffers(output.SafeFileHandle.DangerousGetHandle()); } catch { }
                    }
                    PublicaGuardadoSeguro(ruta, tmp, ruta + ".bak");
                    return;
                }
                using (ProteccionRespaldo cambio = ProtegeRespaldo(ruta))
                using (FileStream fs = new FileStream(ruta, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                {
                    fs.Position = fs.Length;
                    fs.Write(datos, 0, datos.Length);
                    fs.Flush();
                    try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                }
            }
        }
        // de Form1.Biblioteca.cs
        // Se ANADE al final en vez de reescribir el archivo entero. Con cien mil canciones, reescribirlo cada
        // 10 canciones durante el repaso a fondo eran megas y megas al disco cada minuto, para nada.
        // Bajo el cerrojo de la cola: mientras escribe una captura solo se ANADEN nuevas lineas.
        // Retirar una a una con Remove desplaza toda la cola por cada linea (cuadratico).
        // Confirmar el prefijo y mover el resto una sola vez conserva lo llegado durante el guardado.
        private static void RetiraPrefijoGuardado(List<string> pendientes, List<string> guardadas)
        {
            int n = guardadas.Count;
            bool prefijo = n <= pendientes.Count;
            for (int i = 0; prefijo && i < n; i++)
                prefijo = string.Equals(pendientes[i], guardadas[i], StringComparison.Ordinal);
            if (prefijo) { pendientes.RemoveRange(0, n); return; }
            // Respaldo ante una cola sustituida por otro flujo: la semantica anterior, sin
            // descartar registros diferentes ni asumir que un prefijo parcialmente coincidente se guardo.
            for (int i = 0; i < n && pendientes.Count > 0; i++) pendientes.Remove(guardadas[i]);
        }
        // de Form1.Datos.cs
        private volatile bool deepDoneDirty = false;   // hay canciones repasadas sin apuntar en disco
        // de Form1.Datos.cs
        private bool deepDoneSaving;
        // de Form1.Datos.cs
        private readonly List<string> deepPend = new List<string>();   // rutas apuntadas y aun sin escribir
        // de Form1.Datos.cs
        private readonly object durFastLock = new object();
        // de Form1.Datos.cs
        private readonly List<string> durFastPend = new List<string>();   // lineas apuntadas y aun sin escribir
        // de Form1.Datos.cs
        private bool durFastSaving;
        // de Form1.Datos.cs
        // Anade lineas sin dejarlas solamente en la cache de Windows. Se usa para los indices crecientes
        // (IA, repaso a fondo y duraciones), que no conviene reescribir enteros por cada cancion.
        private static long UltimoLfAnexo(Stream origen)
        {
            // Solo para un final incompleto. Buscar por bloques evita un seek/ReadByte por caracter
            // si un apagon dejo una cola larga. No interpreta texto ni cambia el ultimo LF elegido.
            byte[] bloque = new byte[65536];
            long hasta = origen.Length;
            while (hasta > 0)
            {
                int cuantos = (int)Math.Min(bloque.Length, hasta);
                long desde = hasta - cuantos;
                origen.Position = desde;
                int leidos = 0;
                while (leidos < cuantos)
                {
                    int n = origen.Read(bloque, leidos, cuantos - leidos);
                    if (n == 0) throw new IOException("El anexo original se trunco al comprobar su cola");
                    leidos += n;
                }
                for (int i = cuantos - 1; i >= 0; i--)
                    if (bloque[i] == (byte)'\n') return desde + i + 1;
                hasta = desde;
            }
            return 0;
        }
        // de Form1.Datos.cs
        private List<string> AllMusicRoots()
        {
            // Primero, todas las que EXISTEN ahora mismo. Las que no existen -un disco desconectado- no se
            // tiran de la configuracion: siguen guardadas y vuelven solas cuando el disco reaparezca.
            List<string> cand = new List<string>();
            try { if (Directory.Exists(MusicFolder)) cand.Add(MusicFolder); } catch { }
            for (int i = 0; i < extraMusicDirs.Count; i++)
            {
                try { if (Directory.Exists(extraMusicDirs[i])) cand.Add(extraMusicDirs[i]); } catch { }
            }
            // Y ahora se sueltan las que estan DENTRO de otra. Mirar las dos escanearia lo mismo dos veces y
            // cada cancion saldria duplicada en las listas, el buscador y las estadisticas.
            //
            // LA QUE SE SUELTA ES LA DE DENTRO, no la de fuera. Antes era al reves -se quedaba la primera de
            // la lista, que es siempre "musica"-, y eso hacia inutil lo mas normal de todo: decirle a la
            // rocola "la musica esta en ESTE disco". El disco contiene a "musica", asi que el disco entero se
            // soltaba y se seguia mirando solo la carpetita de siempre. Mirar la de fuera ya cubre a la de
            // dentro; al reves no.
            List<string> roots = new List<string>();
            for (int i = 0; i < cand.Count; i++)
            {
                bool dentroDeOtra = false;
                for (int j = 0; j < cand.Count && !dentroDeOtra; j++)
                {
                    if (j == i) continue;
                    // Si son la MISMA carpeta, cada una esta "dentro" de la otra: se queda la primera.
                    if (PathInside(cand[i], cand[j]) && (!PathInside(cand[j], cand[i]) || j < i)) dentroDeOtra = true;
                }
                if (!dentroDeOtra) roots.Add(cand[i]);
            }
            return roots;
        }
        // de Form1.Biblioteca.cs
        private void AvisaCarpetasSaltadas()
        {
            List<string> l = carpetasSaltadas; carpetasSaltadas = null;
            if (l == null || l.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            sb.Append(l.Count).Append(l.Count == 1 ? " carpeta no se pudo leer" : " carpetas no se pudieron leer").Append(" y sus canciones no estan en la biblioteca:");
            for (int i = 0; i < l.Count && i < SaltadasDichas; i++) sb.Append("\r\n  ").Append(l[i]);
            if (l.Count > SaltadasDichas) sb.Append("\r\n  ... y ").Append(l.Count - SaltadasDichas).Append(" mas");
            LogError("Biblioteca: " + sb, null);
        }
        // de Form1.Biblioteca.cs
        private void BuildAlbums(List<string> roots)
        {
            string[] folderMemo = new string[2];
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tracks.Count; i++)
            {
                Track t = tracks[i];
                string folder = AlbumFolderCached(t.Path, roots, folderMemo);
                if (folder == null) folder = "";
                Album al;
                if (!byFolder.TryGetValue(folder, out al)) { al = new Album(); al.Folder = folder; byFolder[folder] = al; }
                al.Tracks.Add(t);
            }

            albums = new List<Album>();
            foreach (KeyValuePair<string, Album> kv in byFolder)
            {
                Album al = kv.Value;
                string leaf = LeafName(al.Folder);
                string ar, nm;
                if (SplitArtist(leaf, out ar, out nm)) { al.Artist = ar; al.Name = nm; }
                else { al.Artist = leaf; al.Name = ""; }
                al.Letter = FirstLetter(al.Artist);
                al.CoverPath = FindCoverCached(al.Folder);
                al.Tracks.Sort(delegate (Track a, Track b) { return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase); });
                albums.Add(al);
            }

            albums.Sort(delegate (Album a, Album b)
            {
                int c = string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            byLetter = new Dictionary<char, List<Album>>();
            for (int i = 0; i < albums.Count; i++)
            {
                Album al = albums[i];
                List<Album> lst;
                if (!byLetter.TryGetValue(al.Letter, out lst)) { lst = new List<Album>(); byLetter[al.Letter] = lst; }
                lst.Add(al);
            }
            origenesTop = IndiceTop(albums);
            albumsDelIndiceTop = albums;
        }
        // de Form1.Datos.cs
        // Busca automaticamente una carpeta llamada "musica" junto a los datos o hasta 3 niveles arriba
        // (ej.: nueva\Release\Rocola.exe  +  nueva\musica). No requiere elegir carpeta.
        private string MusicFolder
        {
            get
            {
                try
                {
                    string dir = CarpetaDatos;
                    for (int i = 0; i < 4 && dir != null; i++)
                    {
                        string cand = Path.Combine(dir, "musica");
                        if (Directory.Exists(cand)) return cand;
                        DirectoryInfo p = Directory.GetParent(dir);
                        dir = (p != null) ? p.FullName : null;
                    }
                }
                catch { }
                return RutaDatos("musica");   // por defecto, junto a los datos
            }
        }
        // de Form1.Biblioteca.cs
        private void ScanFolders(string root, List<string> audio, List<string> video, Dictionary<string, string> covers)
        {
            Stack<string> dirs = new Stack<string>();
            dirs.Push(root);
            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                string[] files = null, subs = null;
                bool reused = scanDirectoryMemo != null && scanDirectoryMemo.Take(dir, out files, out subs);
                if (!reused) try { files = Directory.GetFiles(dir); } catch (Exception ex) { Saltada(dir, ex); }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                        if (Array.IndexOf(AudioExt, ext) >= 0) audio.Add(files[i]);
                        else if (Array.IndexOf(VideoExt, ext) >= 0) video.Add(files[i]);
                    }
                    if (!covers.ContainsKey(dir))
                    {
                        string cv = CoverFromList(dir, files);
                        if (cv != null) covers[dir] = cv;
                    }
                    // (en XP aqui se avisa del avance a la pantalla de arranque: en Rocola OS aun no hay pantalla)
                }
                if (!reused) try { subs = Directory.GetDirectories(dir); } catch (Exception ex) { Saltada(dir, ex); }
                if (subs != null)
                    foreach (string d in subs)
                    {
                        if (EsAtajo(d)) continue;                   // ver EsAtajo: si no, no se sale nunca
                        if (EsCarpetaDelSistema(d)) continue;       // Windows, los programas, la papelera...
                        dirs.Push(d);
                    }
            }
        }
        // de Form1.cs
        private Dictionary<string, string> cachedCovers;  // carpeta -> portada, del cache de biblioteca
        // de Form1.Biblioteca.cs
        // Camina el arbol UNA vez y clasifica lo que encuentra: audio, video y la portada de cada carpeta.
        // Es el reemplazo de las dos caminatas separadas + el GetFiles extra que hacia FindCover por carpeta.
        // LAS CARPETAS QUE NO SE PUEDEN LEER se saltan (sin permiso, una ruta de mas de 260 letras, un disco que se fue
        // a media lectura) y sus canciones no entran en la biblioteca. Se saltaban SIN DECIR NADA: el dueno veia que
        // faltaba un disco y no habia forma de saber por que. Ahora quedan en rocola_error.txt al acabar el recorrido,
        // con el motivo (las primeras 20 y cuantas mas), una vez por recorrido del disco.
        private List<string> carpetasSaltadas;   // (solo el hilo del recorrido)
        // de Form1.Datos.cs
        private readonly List<string> extraMusicDirs = new List<string>();   // carpetas de musica extra (ademas de "musica")
        // de Form1.Portadas.cs
        private static string AlbumFolderCached(string filePath, List<string> roots, string[] folderMemo)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) return "";
            if (string.Equals(dir, folderMemo[0], StringComparison.Ordinal)) return folderMemo[1];
            string folder = AlbumFolderFromDirectory(dir, roots);
            // El escaner entrega archivos consecutivos por carpeta: basta la ultima.
            // Dos referencias por reconstruccion, sin tabla hash ni retencion de todas las rutas.
            folderMemo[0] = dir; folderMemo[1] = folder;
            return folder;
        }
        // de Form1.Biblioteca.cs
        private static readonly string[] AudioExt = { ".mp3", ".wav" };
        // de Form1.Portadas.cs
        // La portada de una carpeta a partir de la lista de archivos que YA se leyo. Mismo criterio de siempre:
        // primero los nombres tipicos (en su orden de preferencia) y, si no hay ninguno, la primera imagen.
        private static string CoverFromList(string folder, string[] files)
        {
            for (int i = 0; i < CoverNames.Length; i++)
                for (int j = 0; j < files.Length; j++)
                    if (string.Equals(Path.GetFileName(files[j]), CoverNames[i], StringComparison.OrdinalIgnoreCase))
                        return Path.Combine(folder, CoverNames[i]);
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                if (Array.IndexOf(ImgExt, ext) >= 0) return files[i];
            }
            return null;
        }
        // de Form1.Biblioteca.cs
        // UNA CARPETA QUE EN REALIDAD ES UN ATAJO A OTRO SITIO. No se sigue, y no es capricho: en un disco de
        // verdad los hay que dan vueltas -"/usr/bin/X11" apunta a si mismo, "/proc/<numero>/root" lleva de
        // vuelta a la raiz- y el recorrido no sale de ahi JAMAS. Medido antes de esto, apuntando al disco
        // entero: 40.000 carpetas y 401.023 archivos en 19 segundos sin terminar; y en la prueba con dos
        // atajos, 77.741 canciones encontradas donde solo habia dos. Es lo mismo que hace cualquier buscador
        // de archivos del sistema.
        //
        // La carpeta que se ELIGIO si se mira aunque sea un atajo -si la eligieron, la quieren-: esto solo
        // decide si se BAJA a los atajos que haya dentro.
        //
        // Y NO SE PIERDE NADA POR EL CAMINO, esta comprobado archivo por archivo: barriendo el disco entero
        // de esta maquina, antes salian 1.657 rutas y ahora 1.582, pero los NOMBRES distintos son 102 en los
        // dos casos. Los 75 de diferencia eran el mismo archivo contado dos veces -"/lib" es un atajo a
        // "/usr/lib", asi que todo lo de dentro salia por partida doble-, que es exactamente la duplicacion
        // que todo esto existe para evitar.
        private static bool EsAtajo(string dir)
        {
            try { return (new DirectoryInfo(dir).Attributes & FileAttributes.ReparsePoint) != 0; }
            catch { return false; }
        }
        // de Form1.Biblioteca.cs
        private static bool EsCarpetaDelSistema(string dir)
        {
            try
            {
                // El ultimo tramo, cortando por las dos barras: la ruta puede venir con cualquiera de las
                // dos, y ademas asi esto se puede comprobar desde una maquina que no sea Windows.
                string t = dir.TrimEnd('\\', '/');
                int c = t.LastIndexOfAny(new char[] { '\\', '/' });
                string n = (c >= 0) ? t.Substring(c + 1) : t;
                // C:\Recovery es del sistema, pero "Recovery" dentro de una biblioteca puede ser un artista
                // o un album perfectamente valido. Solo se excluye cuando cuelga directamente de la unidad.
                if (string.Equals(n, "recovery", StringComparison.OrdinalIgnoreCase))
                {
                    string parent = null, drive = null;
                    try { DirectoryInfo di = Directory.GetParent(t); parent = di != null ? di.FullName.TrimEnd('\\', '/') : null; } catch { }
                    try { drive = Path.GetPathRoot(t); if (drive != null) drive = drive.TrimEnd('\\', '/'); } catch { }
                    if (!string.IsNullOrEmpty(parent) && string.Equals(parent, drive, StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
                for (int i = 0; i < CarpetasDelSistema.Length; i++)
                    if (string.Equals(n, CarpetasDelSistema[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }
        // de Form1.Portadas.cs
        // En arranque desde cache: portadas resueltas SIN tocar el disco.
        // La portada de una carpeta: siempre sale del mapa que dejo el recorrido del disco (el escaneo fresco lo
        // arma sobre la marcha y el arranque por cache lo trae del archivo), asi que aqui ya no se toca el disco.
        private string FindCoverCached(string folder)
        {
            string cp;
            if (cachedCovers != null && cachedCovers.TryGetValue(folder, out cp)) return cp;
            return null;
        }
        // de Form1.Biblioteca.cs
        private static char FirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s)) return '#';
            string n = s.Trim();
            if (n.Length == 0) return '#';
            char c = char.ToUpperInvariant(n[0]);
            string norm = c.ToString().Normalize(NormalizationForm.FormD);
            for (int i = 0; i < norm.Length; i++)
                if (CharUnicodeInfo.GetUnicodeCategory(norm[i]) != UnicodeCategory.NonSpacingMark) { c = char.ToUpperInvariant(norm[i]); break; }
            if (c >= 'A' && c <= 'Z') return c;
            return '#';
        }
        // de PlanBiblioteca.cs
        private static Dictionary<string, List<OrigenTop>> IndiceTop(List<Album> lista)
        {
            Dictionary<string, List<OrigenTop>> indice = new Dictionary<string, List<OrigenTop>>(StringComparer.OrdinalIgnoreCase);
            int n = 0;
            for (int i = 0; i < lista.Count; i++)
                for (int j = 0; j < lista[i].Tracks.Count; j++, n++)
                {
                    Track t = lista[i].Tracks[j]; if (t == null || t.Path == null) continue;
                    List<OrigenTop> origins;
                    if (!indice.TryGetValue(t.Path, out origins)) { origins = new List<OrigenTop>(1); indice[t.Path] = origins; }
                    OrigenTop o = new OrigenTop(); o.Pista = t; o.Disco = lista[i]; o.Orden = n; origins.Add(o);
                }
            return indice;
        }
        // de Form1.Datos.cs
        // Todas las raices de musica que EXISTEN ahora: la carpeta "musica" principal + las agregadas con Ctrl+F2.
        // Una carpeta esta DENTRO de otra (o es la misma). Compara por tramos completos, asi "musica2" NO
        // cuenta como dentro de "musica".
        private static bool PathInside(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string x = a.TrimEnd('\\', '/'), y = b.TrimEnd('\\', '/');
            if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) return true;
            return x.Length > y.Length + 1
                && x.StartsWith(y, StringComparison.OrdinalIgnoreCase)
                && (x[y.Length] == '\\' || x[y.Length] == '/');
        }
        // de Form1.Biblioteca.cs
        private void Saltada(string dir, Exception ex)
        {
            if (carpetasSaltadas == null) carpetasSaltadas = new List<string>();
            string linea = dir + "  (" + (ex != null ? ex.GetType().Name + ": " + ex.Message : "?") + ")";
            if (carpetasSaltadas.Count > 0 && carpetasSaltadas[carpetasSaltadas.Count - 1].StartsWith(dir + "  (")) return;   // la misma carpeta, dos veces
            carpetasSaltadas.Add(linea);
        }
        // de Form1.Biblioteca.cs
        private const int SaltadasDichas = 20;
        // de PlanBiblioteca.cs
        private List<Album> albumsDelIndiceTop;
        // de Form1.cs
        private Dictionary<char, List<Album>> byLetter = new Dictionary<char, List<Album>>();
        // de PlanBiblioteca.cs
        private Dictionary<string, List<OrigenTop>> origenesTop;
        // de EscaneoMemo.cs
        // Solo durante el worker de biblioteca; nunca persiste entre escaneos.
        private ScanDirectoryMemo scanDirectoryMemo;
        // de PlanBiblioteca.cs
        private sealed class OrigenTop
        {
            internal Track Pista; internal Album Disco; internal int Orden;
        }
        // de EscaneoMemo.cs
        private sealed class ScanDirectoryMemo
        {
            internal const int MaxDirectories = 2048, MaxPaths = 32768;
            internal const long MaxBytes = 8L * 1024 * 1024;
            private sealed class Entry
            {
                internal long Stamp, Bytes;
                internal string[] Files, Subdirs;
            }
            private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            internal long Bytes;
            internal int Paths, Hits, Misses;
            internal bool CanCapture(int files)
            { return entries.Count < MaxDirectories && files <= MaxPaths - Paths && Bytes < MaxBytes; }
            internal void Remember(string dir, long stamp, string[] files, string[] subdirs)
            {
                if (!CanCapture(files.Length + subdirs.Length) || entries.ContainsKey(dir)) return;
                long bytes = 192L + dir.Length * 2L;
                for (int i = 0; i < files.Length; i++) bytes += 40L + files[i].Length * 2L;
                for (int i = 0; i < subdirs.Length; i++) bytes += 40L + subdirs[i].Length * 2L;
                if (bytes > MaxBytes - Bytes) return;
                // Files ya es una copia privada. Subdirs se ordenara para la firma: conservar orden original.
                entries.Add(dir, new Entry { Stamp = stamp, Bytes = bytes, Files = files, Subdirs = (string[])subdirs.Clone() });
                Bytes += bytes; Paths += files.Length + subdirs.Length;
            }
            internal bool Take(string dir, out string[] files, out string[] subdirs)
            {
                files = subdirs = null;
                Entry entry;
                string full;
                try { full = Path.GetFullPath(dir); } catch { Misses++; return false; }
                if (!entries.TryGetValue(full, out entry)) { Misses++; return false; }
                entries.Remove(full); Bytes -= entry.Bytes; Paths -= entry.Files.Length + entry.Subdirs.Length;
                // No reutilizar nombres si hubo altas/bajas/renombres desde la enumeracion.
                try { if (Directory.GetLastWriteTimeUtc(full).Ticks != entry.Stamp) { Misses++; return false; } }
                catch { Misses++; return false; }
                files = entry.Files; subdirs = entry.Subdirs; Hits++; return true;
            }
            internal void Clear() { entries.Clear(); Bytes = 0; Paths = 0; }
        }
        // de Form1.Portadas.cs
        private static string AlbumFolderFromDirectory(string dir, List<string> roots)
        {
            if (string.IsNullOrEmpty(dir)) return "";
            if (roots != null)
                for (int r = 0; r < roots.Count; r++)
                {
                    string root = roots[r] == null ? "" : roots[r].TrimEnd('\\', '/');
                    if (root.Length == 0) continue;
                    if (string.Equals(dir, root, StringComparison.OrdinalIgnoreCase)) return dir;   // suelta en la raiz -> el disco ES la raiz
                    if (dir.Length > root.Length + 1
                        && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                        && (dir[root.Length] == '\\' || dir[root.Length] == '/'))
                    {
                        int slash = dir.IndexOfAny(SepChars, root.Length + 1);   // fin de la subcarpeta de 1er nivel
                        return (slash < 0) ? dir : dir.Substring(0, slash);      // raiz + 1a subcarpeta (lo mas profundo cae aqui)
                    }
                }
            return dir;   // fuera de toda raiz conocida: como antes (carpeta inmediata)
        }
        // de Form1.Biblioteca.cs
        // CARPETAS DONDE NO PUEDE HABER MUSICA. Si se le dice a la rocola "la musica esta en este disco
        // entero", el recorrido se mete en la carpeta de Windows y en la de los programas, que son decenas
        // de miles de archivos y ni una sola cancion. Medido en la rocola de Linux, apuntando a la raiz: de
        // 40.000 carpetas miradas, el 90% estaban en sitios donde no puede haber musica.
        //
        // QUIEN LO SABE ES EL SISTEMA, cada uno a su manera. En Linux hay respuesta exacta -el sistema dice
        // de que TIPO es cada sitio montado- y por eso alli esto lo contesta Sistema. En Windows hay que ir
        // por el nombre, que son cuatro y bien conocidos. Se compara el nombre de la carpeta en cualquier
        // sitio del disco y no solo en la raiz: nadie llama "Windows" ni "$Recycle.Bin" a una carpeta de
        // musica, y asi tambien vale para un disco que se conecte a otra maquina.
        private static readonly string[] CarpetasDelSistema = {
            "windows", "winnt", "program files", "program files (x86)",
            "archivos de programa", "archivos de programa (x86)", "programdata",
            "$recycle.bin", "recycler", "system volume information", "perflogs",
            "msocache", "config.msi", "documents and settings"
        };
        // de Form1.Portadas.cs
        private static readonly string[] CoverNames = { "portada.jpg", "portada.jpeg", "portada.png", "cover.jpg", "cover.png", "folder.jpg", "front.jpg" };
        // de Form1.Biblioteca.cs
        private static readonly string[] ImgExt = { ".jpg", ".jpeg", ".png", ".bmp" };
        // de Form1.SinPausa.cs
        // Cierra lo medido de una cancion que acabo por relevo: lo mismo que FinalizeLoudMeasure y
        // FinalizeEqMeasure con los campos, pero con lo que se guardo. Una sola vez.
        private void CierraMedida(MedidaCancion.Cierre c, float corte, float corteBajo)
        {
            if (c == null || !c.Listo) return;
            c.Listo = false;
            CierraSonoridadDeFuente(c.Ruta, c.SumSq, c.Muestras, c.Tasa, c.Canales, c.Fuente as FuenteMedida);
            CierraHuellaDeFuente(c.EqRuta, c.EqSumas, c.EqN, c.EqTasa, corte, corteBajo, c.Fuente as FuenteMedida);
        }
        // de Form1.SinPausa.cs
        // La nueva no dio ni un bloque: su medida se pone aqui (la salida ya no lee).
        private void InstalaMedida(CadenaCancion c)
        {
            curFuenteMedida = c.Fuente;
            curMeasurePath = c.MedidaRuta; curSumSq = 0; curSamples = 0; kwRate = 0;
            eqMeasPath = c.EqRuta; eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0; eqPendN = 0;
            tapMedido = c.Tap;
        }
        // de Form1.SinPausa.cs
        // Se prepara cuando a la que suena le quedan 12 s: tiempo de sobra en una maquina lenta, y lo bastante
        // tarde para que la siguiente ya este medida (el calentador la mide mientras espera en la cola).
        private const double PreparaAntesSeg = 12.0;
        // de Form1.SinPausa.cs
        private static void SellaInicioReloj(CadenaCancion c)
        { c.InicioMusica = c.Lector.CurrentTime.TotalSeconds; c.CuadroComienzo = 0; c.CebadoInicial = c.Retardo; }
        // de Form1.SinPausa.cs
        // DISCO CONTINUO: la que entra ('nueva') sigue con la memoria de los filtros de la que acaba ('vieja'): el
        // grave en mono y el local. Solo cuando la vieja acaba entera, sin fundido (si no,
        // su memoria es la de una musica que ya no se oye). Sin esto, esos filtros empezarian de cero justo en el
        // cambio, y el del grave en mono deja un golpe en el grave que se oye. Lo toman en su primera lectura.
        private static void SigueA(CadenaCancion nueva, CadenaCancion vieja)
        {
            if (nueva == null || vieja == null) return;
            if (nueva.Tap != null) nueva.Tap.Sigue(vieja.Tap);
            if (nueva.Mono != null) nueva.Mono.Sigue(vieja.Mono);
            if (nueva.Sala != null) nueva.Sala.Sigue(vieja.Sala);
            if (nueva.Presencia != null) nueva.Presencia.Sigue(vieja.Presencia);
            if (nueva.Eses != null) nueva.Eses.Sigue(vieja.Eses);
            if (nueva.Retumbe != null) nueva.Retumbe.Sigue(vieja.Retumbe);
            if (nueva.Ataque != null) nueva.Ataque.Sigue(vieja.Ataque);
            if (nueva.Aspereza != null) nueva.Aspereza.Sigue(vieja.Aspereza);
            if (nueva.Hundir != null) nueva.Hundir.Sigue(vieja.Hundir);
            if (nueva.Bajo != null) nueva.Bajo.Sigue(vieja.Bajo);
            if (nueva.Limitador != null) nueva.Limitador.Sigue(vieja.Limitador);
            if (nueva.Siseo != null) nueva.Siseo.Sigue(vieja.Siseo);
            if (nueva.Arreglo != null) nueva.Arreglo.Sigue(vieja.Arreglo);
            if (nueva.Zumbido != null && vieja.Zumbido != null) nueva.Zumbido.Sigue(vieja.Zumbido);
            // Convertidas a la frecuencia de la salida: el convertidor y su limitador, tambien (SinPausa.cs).
            if (nueva.Conversion != null && vieja.Conversion != null) nueva.Conversion.Sigue(vieja.Conversion);
            if (nueva.LimiteSalida != null && vieja.LimiteSalida != null) nueva.LimiteSalida.Sigue(vieja.LimiteSalida);
        }
        // de Form1.SinPausa.cs
        // ¿'b' es la pista que sigue a 'a' en el MISMO disco? (un disco TOP junta canciones de discos distintos)
        private static bool SiguienteDelDisco(PlayItem a, PlayItem b)
        {
            if (a == null || b == null || a.IsVideo || b.IsVideo || a.Song == null || b.Song == null) return false;
            Album d = a.Disc;
            if (d == null || !object.ReferenceEquals(d, b.Disc) || d.IsTop || d.Tracks == null) return false;
            int i = d.Tracks.IndexOf(a.Song);
            return i >= 0 && i + 1 < d.Tracks.Count && object.ReferenceEquals(d.Tracks[i + 1], b.Song);
        }
        // de Form1.cs
        private const int CreditsPerCoin = 4;                 // monedas -> creditos
        // de AperturaAudio.cs
        private bool EstaDanada(string path)
        {
            string antes;
            if (string.IsNullOrEmpty(path) || danadas.Count == 0 || !danadas.TryGetValue(path, out antes)) return false;
            if (antes == AudioFileSignature(path)) return true;
            danadas.Remove(path);   // cambio el archivo (o ya no esta): que se intente de nuevo
            return false;
        }
        // de Form1.Datos.cs
        // Registra una reproduccion pedida (algo que entro a la cola con exito).
        private void RecordPlay(PlayItem it)
        {
            if (it == null) return;
            statsTotal++;
            if (it.IsVideo)
            {
                statsVideoTotal++;
                string vp = it.VideoPath;
                if (!string.IsNullOrEmpty(vp)) { int n; videoCounts.TryGetValue(vp, out n); videoCounts[vp] = n + 1; videoSeq[vp] = ++statsSeq; }
            }
            else if (it.Song != null && !string.IsNullOrEmpty(it.Song.Path))
            {
                statsSongTotal++;
                int n; songCounts.TryGetValue(it.Song.Path, out n); songCounts[it.Song.Path] = n + 1;
                songSeq[it.Song.Path] = ++statsSeq;
                if (!string.IsNullOrEmpty(it.Song.Genre) && !songGenre.ContainsKey(it.Song.Path)) songGenre[it.Song.Path] = GenreLabel(it.Song.Genre);
                string ar = it.Song.Artist;
                if (!string.IsNullOrEmpty(ar)) { int a; artistCounts.TryGetValue(ar, out a); artistCounts[ar] = a + 1; }
                // primera cancion contada -> habilitar la seccion "Lo mas sonado" en la barra de letras
                // (en XP aqui se enciende la seccion "Lo mas sonado" de la pantalla)
            }
            statsDirty = true;
            topDirty = true;   // OPTIMIZACION (TOP): cambiaron counts/genero/artista -> reconstruir al entrar a TOP
            CuentaNoche(it);   // y lo mas pedido esta noche, para la atraccion (Noche.cs; en XP, AtraccionGpu.cs)
        }
        // de Form1.Datos.cs
        // Reconstruye creditos/cola/cancion desde el archivo de estado (si hay algo que reanudar).
        private void RestoreState()
        {
            if (stateRestored) return;
            stateRestored = true;
            stateMissingRoots = null;
            try
            {
                string f = StateFile();
                // Si el archivo bueno falta o quedo vacio -un corte de luz de los de antes- se lee la copia:
                // se pierde como mucho lo ultimo que se hizo, no el dinero entero del cliente. Y si hay un guardado
                // encargado que aun no llego al disco, manda ese: es lo ultimo que paso (ver EscribeEstado).
                string[] L = EstadoSinEscribir() ?? LeeLineasSeguro(f);
                if (L == null || L.Length < 1) return;
                if (L[0] != "ROCOLA-ESTADO v1")
                {
                    try { if (File.Exists(f + ".bak")) L = File.ReadAllLines(f + ".bak"); } catch { }
                    if (L == null || L.Length < 1 || L[0] != "ROCOLA-ESTADO v1") return;
                }
                // El estado solo vale DENTRO del mismo dia del negocio (corte a las 7 AM): lo de anoche, visto ya en
                // la manana, se descarta (arranque limpio). PERO si se guardo hace poquito (corte breve en plena
                // sesion, p.ej. un parpadeo de luz justo al dar las 7 mientras te desvelas), se reanuda igual.
                DateTime saved = SavedStateTime(L, f);
                if (BusinessDay(DateTime.Now) != BusinessDay(saved) && (DateTime.Now - saved).TotalMinutes > StateRecentMins)
                { BorraEstado(); return; }
                // mapas ruta -> cancion/disco para reconstruir los elementos (solo lo que SIGA existiendo)
                Dictionary<string, Track> byPath = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Album> discByPath = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < albums.Count; i++)
                    for (int j = 0; j < albums[i].Tracks.Count; j++)
                    {
                        Track t = albums[i].Tracks[j];
                        if (t != null && t.Path != null && !byPath.ContainsKey(t.Path)) { byPath[t.Path] = t; discByPath[t.Path] = albums[i]; }
                    }
                Dictionary<string, Album> vidDisc = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                if (videoAlbums != null)
                    for (int i = 0; i < videoAlbums.Count; i++)
                        for (int j = 0; j < videoAlbums[i].VideoFiles.Count; j++)
                        { string vp = videoAlbums[i].VideoFiles[j]; if (vp != null && !vidDisc.ContainsKey(vp)) vidDisc[vp] = videoAlbums[i]; }

                int cr = 0; PlayItem playing = null; List<PlayItem> q = new List<PlayItem>();
                List<string> faltan = new List<string>();
                for (int i = 1; i < L.Length; i++)
                {
                    string s = L[i];
                    if (s.Length == 0) continue;
                    if (s.StartsWith("creditos=")) { int.TryParse(s.Substring(9), out cr); continue; }
                    if (s.StartsWith("pos=")) continue;   // formato viejo: la posicion ya no se usa (siempre desde el principio)
                    bool isNow = s.StartsWith("sonando=");
                    if (!isNow && !s.StartsWith("cola=")) continue;
                    string v = s.Substring(s.IndexOf('=') + 1);
                    if (v.Length < 3 || v[1] != '|') continue;
                    bool isVid = (v[0] == 'V'); string path = v.Substring(2);
                    PlayItem it = null;
                    if (isVid)
                    {
                        bool exists = false; try { exists = File.Exists(path); } catch { }
                        if (exists) { it = new PlayItem(); it.IsVideo = true; it.VideoPath = path; Album d; vidDisc.TryGetValue(path, out d); it.Disc = d; }
                    }
                    else
                    {
                        Track t;
                        if (byPath.TryGetValue(path, out t)) { it = new PlayItem(); it.Song = t; Album d; discByPath.TryGetValue(path, out d); it.Disc = d; }
                    }
                    if (it == null)
                    {
                        // Un disco configurado puede estar desconectado al arrancar: en ese caso se espera y
                        // se vuelve a escanear, sin consumir el estado. Si la raiz SI esta, la seleccion se
                        // perdio de verdad y se devuelve el credito que ya habia pagado.
                        string raiz = RaizConfiguradaAusente(path);
                        if (raiz != null)
                        {
                            bool repetida = false;
                            for (int k = 0; k < faltan.Count; k++)
                                if (string.Equals(faltan[k], raiz, StringComparison.OrdinalIgnoreCase)) { repetida = true; break; }
                            if (!repetida) faltan.Add(raiz);
                        }
                        else cr++;
                        continue;
                    }
                    if (isNow) playing = it; else q.Add(it);
                }
                if (faltan.Count > 0)
                {
                    stateRestored = false;
                    stateMissingRoots = faltan;
                    stateRootRetryTick = Environment.TickCount;
                    Avisa("Esperando el disco de musica para restaurar creditos y cola...");
                    return;
                }
                if (cr <= 0 && playing == null && q.Count == 0) return;   // estado vacio: arranque limpio
                credits = Math.Max(0, cr);
                UpdateCredits();
                queue.AddRange(q); RefrescaCola(); PrecalientaSiguiente();
                if (playing != null)
                {
                    queue.Insert(0, playing);   // lo que estaba sonando vuelve a entrar y arranca DESDE EL PRINCIPIO
                    RefrescaCola();
                    PlayNext();
                    ShowToast("Reanudando la musica");
                }
                else UpdateNowPlayingUI();
            }
            catch { stateRestored = false; }
        }
        // de Form1.Datos.cs
        private void SaveState()
        {
            if (estadoCongelado) return;   // cerrando: vale lo ya guardado (Creditos.cs: CongelaEstado)
            try
            {
                StringBuilder sb = new StringBuilder(256);
                sb.Append("ROCOLA-ESTADO v1\r\n");
                string[] antes = EstadoPorRestaurar();   // lo de antes del apagon, si aun no se restauro (ver abajo)
                sb.Append("ts=").Append(antes != null ? estadoAntesTs.Ticks : DateTime.Now.Ticks).Append("\r\n");   // cuando se guardo (para descartarlo si cruza las 7 AM)
                int crAntes = 0;
                if (antes != null)
                    for (int i = 1; i < antes.Length; i++)
                        if (antes[i].StartsWith("creditos=")) { if (!int.TryParse(antes[i].Substring(9), out crAntes) || crAntes < 0) crAntes = 0; break; }
                sb.Append("creditos=").Append(crAntes + credits).Append("\r\n");
                if (antes != null)
                    for (int i = 1; i < antes.Length; i++)
                        if (antes[i].StartsWith("sonando=") || antes[i].StartsWith("cola=")) sb.Append(antes[i]).Append("\r\n");
                if (nowPlaying != null && !EsMudaDiag(nowPlaying))   // con lo de antes delante, lo de ahora va a la cola, detras (la muda del diagnostico nunca se guarda)
                    sb.Append(antes != null ? "cola=" : "sonando=").Append(nowPlaying.IsVideo ? 'V' : 'S').Append('|').Append(nowPlaying.PathKey()).Append("\r\n");
                for (int i = 0; i < queue.Count; i++)
                    sb.Append("cola=").Append(queue[i].IsVideo ? 'V' : 'S').Append('|').Append(queue[i].PathKey()).Append("\r\n");
                EscribeEstado(sb.ToString());   // lo escribe un hilo de fondo, en orden (ver abajo)
            }
            catch { }
        }
        // de Form1.Datos.cs
        private long statsTotal = 0, statsSongTotal = 0, statsVideoTotal = 0, statsCoins = 0, statsCreditsAdded = 0;
        // de Form1.Datos.cs
        private volatile bool statsDirty = false;
        // de Form1.Datos.cs
        // Borrar el estado es al momento (es raro: al cerrar a proposito, al empezar el dia o con un estado de otro dia) y
        // deja atras cualquier encargo anterior que aun no se haya escrito.
        private void BorraEstado()
        {
            string ruta = StateFile();
            int n;
            lock (estadoSolicitudLock)
            {
                n = System.Threading.Interlocked.Increment(ref estadoPedido);
                estadoUltimo = null;
                if (estadoReintento != null) { estadoReintento.Dispose(); estadoReintento = null; }
            }
            lock (CandadoDe(ruta))
            {
                if (n > estadoHecho) estadoHecho = n;
                BorraSeguro(ruta);
            }
        }
        // de Form1.Datos.cs
        private static DateTime BusinessDay(DateTime dt) { return dt.AddHours(-BusinessDayStartHour).Date; }
        // de Rendimiento.cs
        private bool EsMudaDiag(PlayItem it) { return diagMuda != null && object.ReferenceEquals(it, diagMuda); }
        // de Form1.Datos.cs
        private void EscribeEstado(string texto)
        {
            int n;
            lock (estadoSolicitudLock)
            {
                n = System.Threading.Interlocked.Increment(ref estadoPedido);
                estadoUltimo = texto;
                if (estadoReintento != null) estadoReintento.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            }
            EncolaEstado(n, texto);
        }
        // de Form1.Datos.cs
        private string[] EstadoPorRestaurar()
        {
            if (stateRestored) return null;
            if (estadoAntes == null)
            {
                string f = StateFile();
                string[] L = EstadoSinEscribir() ?? LeeLineasSeguro(f);   // lo encargado que aun no llego al disco, o el archivo
                if (L != null && L.Length > 0 && L[0] != "ROCOLA-ESTADO v1")
                    try { L = File.Exists(f + ".bak") ? File.ReadAllLines(f + ".bak") : null; } catch { L = null; }   // como RestoreState
                estadoAntes = (L != null && L.Length > 0 && L[0] == "ROCOLA-ESTADO v1") ? L : new string[0];
                estadoAntesTs = estadoAntes.Length > 0 ? SavedStateTime(estadoAntes, f) : DateTime.Now;
            }
            if (estadoAntes.Length == 0) return null;
            if (BusinessDay(DateTime.Now) != BusinessDay(estadoAntesTs) && (DateTime.Now - estadoAntesTs).TotalMinutes > StateRecentMins) return null;
            return estadoAntes;
        }
        // de Form1.Datos.cs
        // Las lineas del ultimo encargo si aun no llego al disco (como las daria File.ReadAllLines); null si el disco ya
        // esta al dia y hay que leerlo de ahi.
        private string[] EstadoSinEscribir()
        {
            string t;
            lock (estadoSolicitudLock)
            {
                t = estadoUltimo;
                if (t == null || System.Threading.Volatile.Read(ref estadoHecho) >= estadoPedido) return null;
            }
            List<string> L = new List<string>();
            using (StringReader r = new StringReader(t)) { string s; while ((s = r.ReadLine()) != null) L.Add(s); }
            return L.ToArray();
        }
        // de Form1.Fondo.cs
        // Etiqueta canonica del genero para agrupar los discos TOP (mismas familias que GenreToColor).
        private static string GenreLabel(string genre)
        {
            if (string.IsNullOrEmpty(genre)) return "Otros";
            string g = genre.ToLowerInvariant();
            // KARAOKE manda sobre todo lo demas: una cumbia de karaoke es karaoke, no cumbia. Y NAVIDAD va
            // detras por lo mismo (una "cumbia navidena" se guarda por navidad, que es cuando se pone).
            if (Has(g, "karaoke")) return "Karaoke";
            if (Has(g, "navidad", "navide", "villancico", "christmas", "xmas")) return "Navidad";
            if (Has(g, "k-pop", "kpop", "j-pop", "jpop", "anime", "idol")) return "K-Pop";
            if (Has(g, "metal", "thrash", "death", "heavy", "hardcore", "grindcore")) return "Metal";
            if (Has(g, "punk")) return "Punk";
            if (Has(g, "gothic", "goth", "gotic", "g\u00f3tic", "darkwave")) return "Gotico";
            if (Has(g, "industrial")) return "Industrial";
            if (Has(g, "reggaeton", "reggaet", "reguet", "perreo", "dembow")) return "Reggaeton";
            if (Has(g, "hip", "rap", "trap")) return "Hip-Hop";
            if (Has(g, "salsa", "mereng", "bachata", "tropical", "vallenato", "guarach", "mambo", "chachacha",
                       "cha cha", "danzon", "danz\u00f3n", "guaguanco", "timba", "marimba", "son cubano")) return "Tropical";
            if (Has(g, "cumbia", "banda", "norte", "ranch", "mariach", "regional", "tejan", "grupe", "corrido",
                       "duranguense", "sonidero", "quebradita", "sierre", "huapango", "huasteco", "zapateado")) return "Regional";
            if (Has(g, "bolero", "tango", "ranchera", "balada", "romantic", "romant", "rom\u00e1nt", "vals", "serenata")) return "Bolero";
            if (Has(g, "flamenco", "rumba", "copla")) return "Flamenco";
            if (Has(g, "latin", "latino", "espan", "samba", "sertanejo", "pagode", "forro")) return "Latino";
            if (Has(g, "reggae", "ska", "dub")) return "Reggae";
            if (Has(g, "drum", "bass", "dnb", "d&b", "jungle", "breakbeat", "garage")) return "DnB";
            if (Has(g, "electro", "electr\u00f3n", "techno", "tecno", "house", "trance", "edm", "synth", "dance", "club", "dubstep", "rave", "hardstyle", "tribal")) return "Electronica";
            if (Has(g, "ambient", "chill", "lo-fi", "lofi", "downtempo", "new age", "meditat")) return "Chill";
            if (Has(g, "jazz", "swing", "bebop", "bossa")) return "Jazz";
            if (Has(g, "blues", "soul", "funk", "r&b", "rhythm", "motown")) return "Soul";
            if (Has(g, "gospel", "christian", "worship", "cristian", "alabanza")) return "Cristiana";
            if (Has(g, "classic", "clasic", "cl\u00e1sic", "orchestr", "orquest", "piano", "opera", "\u00f3pera", "symphon", "baroque", "instrumental")) return "Clasica";
            if (Has(g, "soundtrack", "score", "cinematic", "banda sonora")) return "Soundtrack";
            if (Has(g, "country", "bluegrass", "americana")) return "Country";
            if (Has(g, "folk", "acoustic", "celt")) return "Folk";
            if (Has(g, "disco")) return "Disco";
            if (Has(g, "oldies", "50s", "60s", "doo")) return "Oldies";
            if (Has(g, "world", "ethnic", "african", "arab", "india", "asian")) return "World";
            if (Has(g, "vocal", "capella", "capela")) return "Vocal";
            if (Has(g, "comedy", "spoken", "podcast", "hablado", "narracion", "narraci\u00f3n", "audiolibro")) return "Hablado";
            if (Has(g, "child", "kids", "infantil")) return "Infantil";
            if (Has(g, "pop")) return "Pop";
            if (Has(g, "rock", "grunge", "alternative", "indie")) return "Rock";
            return "Otros";
        }
        // de Form1.Datos.cs
        // Lee un archivo guardado con EscribeSeguro. Si el bueno falta o quedo vacio -un corte de luz de los
        // de antes, o un disco con problemas- se lee la copia. Devuelve null si no hay nada que leer.
        private static string[] LeeLineasSeguro(string ruta)
        {
            try { if (File.Exists(ruta)) { string[] L = File.ReadAllLines(ruta); if (L.Length > 0) return L; } } catch { }
            try { if (File.Exists(ruta + ".bak")) { string[] L = File.ReadAllLines(ruta + ".bak"); if (L.Length > 0) return L; } } catch { }
            return null;
        }
        // de Form1.Datos.cs
        private string RaizConfiguradaAusente(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            List<string> cfg = new List<string>(); cfg.Add(MusicFolder);
            for (int i = 0; i < extraMusicDirs.Count; i++) cfg.Add(extraMusicDirs[i]);
            string mejor = null;
            for (int i = 0; i < cfg.Count; i++)
            {
                string r = cfg[i]; if (string.IsNullOrEmpty(r)) continue;
                try { r = Path.GetFullPath(r); } catch { continue; }
                if (!PathInside(path, r) || (mejor != null && mejor.Length >= r.Length)) continue;
                mejor = r;
            }
            if (mejor == null) return null;
            try { return Directory.Exists(mejor) ? null : mejor; } catch { return mejor; }
        }
        // de Form1.Datos.cs
        // Cuando se guardo el estado: la marca "ts=" (ticks); si falta (formato viejo), la fecha del archivo.
        private static DateTime SavedStateTime(string[] lines, string file)
        {
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].StartsWith("ts="))
                { long t; if (long.TryParse(lines[i].Substring(3), out t) && t > 0) { try { return new DateTime(t); } catch { } } break; }
            try { return File.GetLastWriteTime(file); } catch { return DateTime.Now; }
        }
        // de Form1.Datos.cs
        // ===================== ESTADO VIVO (reanudar tras un corte de luz) =====================
        // Guarda creditos + lo que suena + la cola en rocola_estado.txt (solo cambia en eventos: moneda,
        // encolar, cambio de cancion). Si se corta la luz, al volver la rocola RETOMA: la cancion que estaba
        // sonando arranca DESDE EL PRINCIPIO (a peticion: media cancion confunde mas de lo que ayuda), con su
        // cola y los creditos del cliente intactos. Para el CIERRE del negocio: SALTAR repetido vacia la cola
        // y, con todo vacio, una pulsacion de SALTAR borra los creditos -> estado vacio -> arranca limpia.
        // El archivo se DESCARTA por tres vias que distinguen "cierre" de "apagon": (1) apagado deliberado
        // (boton de poder -> Windows apaga, u Alt+F4) borra el archivo en OnFormClosing; (2) al cruzar las 7 AM
        // estando ociosa (CheckBusinessDayReset); (3) al restaurar, si el estado ya cruzo las 7 AM y no es reciente.
        // Un apagon no dispara nada de esto -> el archivo sobrevive y la rocola reanuda.
        private string StateFile() { return RutaDatos("rocola_estado.txt"); }
        // de Form1.Datos.cs
        private const int StateRecentMins = 30;    // si el estado se guardo hace menos de esto, se reanuda aunque cruce las 7 AM (fue un corte breve en plena sesion)
        // de Form1.cs
        private const char TopMark = '\u2605';     // "letra" especial = LO MAS SONADO (disco virtual)
        // de Form1.Biblioteca.cs
        private Dictionary<string, int> artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // de AperturaAudio.cs
        private readonly Dictionary<string, string> danadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Datos.cs
        private DateTime estadoAntesTs;     // y cuando se guardo
        // de Form1.cs
        // ---- Estadisticas (base de "Lo mas sonado") ----
        // Se cuenta lo que entra a la cola CON EXITO (lo que la gente pide y paga). Se guarda en
        // rocola_stats.txt junto al .exe: en segundo plano cada minuto si hubo cambios, y al cerrar.
        private Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // de Form1.cs
        private Dictionary<string, string> songGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // ruta -> etiqueta de genero (capturada al sonar)
        // de Form1.cs
        private Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // recencia (cuando se sono por ultima vez)
        // de Form1.Datos.cs
        private List<string> stateMissingRoots;
        // de Form1.Datos.cs
        private bool stateRestored = false;   // la restauracion corre UNA vez (al primer armado de biblioteca)
        // de Form1.Datos.cs
        private int stateRootRetryTick;
        // de Form1.Datos.cs
        private long statsSeq = 0;
        // de Form1.cs
        // OPTIMIZACION (TOP): BuildTopDiscs recorre TODA la biblioteca (100k+). Antes se rebuildeaba en
        // cada cambio de pagina dentro de "Lo mas sonado". Ahora solo cuando los datos cambian (al contar
        // una reproduccion o tras re-escanear): topDirty=true marca que hay que reconstruir; si no, se
        // devuelve el topDiscs ya armado. Hojear TOP queda instantaneo.
        private bool topDirty = true;
        // de Form1.Video.cs
        private List<Album> videoAlbums = new List<Album>();   // discos que tienen archivo(s) de video
        // de Form1.Video.cs
        private Dictionary<string, int> videoCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Video.cs
        private Dictionary<string, long> videoSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        // de Form1.Datos.cs
        // BORRAR UN GUARDADO ES BORRARLO ENTERO. Como cada guardado deja una copia al lado, borrar solo el
        // archivo bueno no borra nada: la lectura de arriba encuentra la copia y lo resucita. Paso de verdad
        // con el archivo de estado, donde "que no exista" es la senal de que la rocola se cerro a proposito:
        // se borraba el bueno, sobrevivia la copia, y al dia siguiente reaparecian los creditos y la cola.
        //
        // El ORDEN importa. Primero la copia y el de paso, y el bueno al final: si se fuera la luz a mitad de
        // esto, lo que queda es el archivo bueno -o sea, se reanuda, que es lo que hace la rocola ante un
        // apagon de toda la vida-, nunca una copia suelta capaz de resucitar sola.
        private static void BorraSeguro(string ruta)
        {
            lock (CandadoDe(ruta))
            {
                try { File.Delete(ruta + ".bak"); } catch { }
                try { File.Delete(ruta + ".tmp"); } catch { }
                try { File.Delete(ruta); } catch { }
            }
        }
        // de Form1.Datos.cs
        // El estado de recuperacion pertenece a UN "dia del negocio", que ARRANCA a las 7 AM (hora del sistema): a
        // esa hora ya cerro, y nada de la noche anterior debe cruzar las 7 AM. Todo lo de antes de las 7 AM cuenta
        // como el dia ANTERIOR. Al pasar esa hora: el estado guardado se DESCARTA al restaurar (arranque limpio en
        // la manana) y se LIMPIA si la rocola sigue encendida (creditos/cola a cero) -> cada dia arranca en cero.
        private const int BusinessDayStartHour = 7;
        // de Form1.Datos.cs
        private void EncolaEstado(int n, string texto)
        {
            string ruta = StateFile();
            lock (estadoSolicitudLock)
            {
                if (estadoCerrado || n != estadoPedido || n <= System.Threading.Volatile.Read(ref estadoHecho) || texto == null) return;
                if (estadoCola == null) estadoCola = new GuardadoReciente();
                // Validar y publicar la instantanea en el mismo candado. Un reintento
                // viejo no puede sustituir el pendiente de una moneda nueva.
                GuardaUltimo(estadoCola, delegate
                {
                    try
                    {
                        lock (CandadoDe(ruta))
                        {
                            if (n <= estadoHecho || n != System.Threading.Volatile.Read(ref estadoPedido)) return;
                            EscribeSeguroYa(ruta, texto);
                            // Solo un archivo PUBLICADO cuenta como aplicado. Un fallo sigue pendiente.
                            System.Threading.Interlocked.Exchange(ref estadoHecho, n);
                        }
                        lock (estadoSolicitudLock) { if (n == estadoPedido) estadoFallos = 0; }
                    }
                    catch (Exception ex)
                    {
                        lock (estadoSolicitudLock)
                        {
                            if (estadoCerrado || n != estadoPedido) return;
                            estadoFallos++;
                            if (estadoFallos == 1) LogError("Estado de creditos/cola pendiente: se reintentara el guardado", ex);
                            if (estadoReintento == null) estadoReintento = new System.Threading.Timer(ReintentaEstado, null,
                                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                            estadoReintento.Change(estadoFallos == 1 ? 500 : (estadoFallos < 4 ? 2000 : 10000), System.Threading.Timeout.Infinite);
                        }
                    }
                });
            }
        }
        // de Form1.Fondo.cs
        private static bool Has(string s, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++) if (s.Contains(keys[i])) return true;
            return false;
        }
        // de Rendimiento.cs
        private PlayItem diagMuda = null;       // la cancion MUDA que puso el diagnostico (nunca una de un cliente); null = ninguna
        // de Form1.Datos.cs
        // EL ESTADO QUE AUN NO SE RESTAURO. Tras un apagon, los creditos y la cola se restauran cuando la biblioteca ya
        // esta armada (o cuando vuelve el disco que faltaba), pero la moneda funciona desde el primer instante. Una
        // moneda en ese rato guardaba el estado de AHORA encima del de antes, y al restaurar se perdian los creditos y la
        // cola que los clientes ya habian pagado. Mientras no se haya restaurado, lo de antes se conserva y lo de ahora
        // se le suma: los creditos, unos encima de otros, y la cola de ahora detras de la de antes (SaveState). Con la
        // misma regla que la restauracion: lo de otro dia del negocio no se conserva (se descartaria al restaurar).
        private string[] estadoAntes;       // lo que habia guardado al arrancar (null: aun no se miro; vacio: nada)
        // de Form1.Datos.cs
        private int estadoHecho;               // numero del ultimo aplicado en el disco (se cambia bajo el candado del archivo)
        // de Form1.Datos.cs
        private int estadoPedido;              // numero del ultimo encargo o borrado pedido
        // de Form1.Datos.cs
        private System.Threading.Timer estadoReintento;
        // de Form1.Datos.cs
        private readonly object estadoSolicitudLock = new object();
        // de Form1.Datos.cs
        private string estadoUltimo;           // el texto del ultimo encargo (null si lo ultimo fue borrar)
        // de Form1.Datos.cs
        private static void EscribeSeguroYa(string ruta, string texto)
        {
            string tmp = ruta + ".tmp", bak = ruta + ".bak";
            // MISMOS BYTES que antes: UTF-8 sin marca al principio, que es lo que escribia File.WriteAllText.
            // Si cambiara, todo lo guardado hasta hoy dejaria de leerse igual.
            byte[] datos = new UTF8Encoding(false).GetBytes(texto);
            using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(datos, 0, datos.Length);
                fs.Flush();
                // Flush solo deja lo escrito en la memoria de Windows. Esto lo baja al disco de verdad, que
                // es lo unico que sobrevive a un corte de luz. Si el sistema no tiene esta llamada (o no es
                // Windows), se sigue adelante: se pierde la garantia dura, no el resto.
                try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
            }
            PublicaGuardadoSeguro(ruta, tmp, bak);
        }
        // de Form1.Datos.cs
        private void ReintentaEstado(object ignorado)
        {
            int n; string texto;
            lock (estadoSolicitudLock) { n = estadoPedido; texto = estadoUltimo; }
            EncolaEstado(n, texto);
        }
        // de Form1.Datos.cs
        private bool estadoCerrado;
        // de Form1.Datos.cs
        // EL ESTADO SE ESCRIBE DETRAS DE LA PANTALLA. Los creditos y la cola se guardan con cada moneda, cada cancion que
        // se pone y cada cambio de tema, y guardarlos es bajarlos al disco de verdad (EscribeSeguroYa: archivo de paso,
        // FlushFileBuffers y cambio de un tiron). En un disco mecanico ocupado eso tarda decenas de milisegundos, y se
        // hacia en el hilo de la pantalla: la moneda o el tema nuevo se notaban como un tiron. Ahora la pantalla arma el
        // texto -lo barato- y lo encarga; lo escribe un hilo de fondo, en el mismo archivo y de la misma forma. Si se
        // juntan varios, se escribe el ultimo (GuardaUltimo). Decision del usuario del 29 de septiembre de 2026: se
        // acepta la ventana de unos milisegundos entre encargarlo y tenerlo en el disco.
        //
        // EL ORDEN. Cada encargo y cada borrado lleva su numero, y bajo el candado del archivo solo se aplica si es mas
        // nuevo que lo ultimo aplicado: un encargo viejo que llega tarde nunca pisa a uno nuevo ni resucita un estado
        // que se borro a proposito (cierre, dia nuevo). Y lo encargado que aun no llego al disco se lee de aqui mismo
        // (EstadoSinEscribir): restaurar nunca ve lo de antes de la ultima moneda.
        private GuardadoReciente estadoCola;   // el encargo pendiente (se crea al primer uso)
        // de Form1.Datos.cs
        private int estadoFallos;
        // de Form1.Datos.cs
        private void LoadStats()
        {
            // Fallback del arranque si no se pudo crear/completar el lector.
            try
            {
                using (StreamReader reader = new StreamReader(LibStatsFile()))
                    PublishStartupStats(ReadStatsRecords(reader));
            }
            catch { }   // nada se publica si la lectura se corta antes de EOF
        }
        // de Form1.Datos.cs
        private void SaveStats(bool async)
        {
            PruneStats();   // acotar antes de escribir
            StatsPendientes snapshot;
            try { snapshot = CopiaStats(); } catch { return; }   // solo numeros/rutas; ordenar y formatear fuera
            // Este archivo tiene DOS escritores -uno de fondo cada tanto y otro sincrono al cerrar- y si se
            // pisaban se llevaban por delante "LO MAS SONADO" entero. El candado por archivo esta dentro de
            // EscribeSeguro, asi que aqui ya no hace falta acordarse de nada.
            int version = System.Threading.Interlocked.Increment(ref statsSaveVersion);
            System.Threading.WaitCallback guardar = delegate (object ignored)
            {
                try
                {
                    if (version != System.Threading.Interlocked.CompareExchange(ref statsSaveVersion, 0, 0)) return;
                    string text = TextoStats(snapshot);
                    lock (statsSaveLock)
                    {
                        if (version != System.Threading.Interlocked.CompareExchange(ref statsSaveVersion, 0, 0)) return;
                        EscribeSeguro(LibStatsFile(), text);
                    }
                }
                catch { }
            };
            if (async) QueueDataWrite(guardar); else guardar(null);
        }
        // de Form1.Datos.cs
        private StatsPendientes CopiaStats()
        {
            return new StatsPendientes { Canciones = CopiaContadores(songCounts, songSeq), Videos = CopiaContadores(videoCounts, videoSeq),
                Artistas = new List<KeyValuePair<string, int>>(artistCounts).ToArray(), Generos = new List<KeyValuePair<string, string>>(songGenre).ToArray(),
                Total = statsTotal, CancionesTotal = statsSongTotal, VideosTotal = statsVideoTotal, Monedas = statsCoins, Creditos = statsCreditsAdded,
                Cultura = CultureInfo.CurrentCulture };
        }
        // de Form1.Datos.cs
        private static void EscribeSeguro(string ruta, string texto)
        {
            lock (CandadoDe(ruta)) EscribeSeguroYa(ruta, texto);
        }
        // de Form1.Datos.cs
        // ===================== MOTOR DE ESTADISTICAS =====================
        private string LibStatsFile() { return RutaDatos("rocola_stats.txt"); }
        // de Form1.Datos.cs
        // Mantiene el archivo acotado: descarta lo menos sonado (y mas viejo) para dejar entrar lo nuevo.
        private void PruneStats()
        {
            PruneCountDict(songCounts, songSeq, songGenre, SongStatCap, SongStatTarget);
            PruneCountDict(videoCounts, videoSeq, null, VideoStatCap, VideoStatTarget);
            PruneCountDict(artistCounts, null, null, ArtistStatCap, ArtistStatTarget);
            if (songGenre.Count > songCounts.Count)   // limpiar generos huerfanos
            {
                List<string> orphans = new List<string>();
                foreach (string k in songGenre.Keys) if (!songCounts.ContainsKey(k)) orphans.Add(k);
                for (int i = 0; i < orphans.Count; i++) songGenre.Remove(orphans[i]);
            }
        }
        // de Form1.Datos.cs
        // Solo se llama en UI, antes de abrir la biblioteca y de restaurar la cola.
        private void PublishStartupStats(StartupStats data)
        {
            if (data == null) return;
            songCounts = data.songCounts;
            songSeq = data.songSeq;
            artistCounts = data.artistCounts;
            videoCounts = data.videoCounts;
            videoSeq = data.videoSeq;
            songGenre = data.songGenre;
            statsTotal = data.statsTotal;
            statsSongTotal = data.statsSongTotal;
            statsVideoTotal = data.statsVideoTotal;
            statsCoins = data.statsCoins;
            statsCreditsAdded = data.statsCreditsAdded;
            statsSeq = data.statsSeq;
        }
        // de Form1.Datos.cs
        private static StartupStats ReadStatsRecords(TextReader reader)
        {
            if (reader.ReadLine() != "ROCOLA-STATS v1") return null;
            StartupStats data = new StartupStats();
            int section = 0;   // 0=totales 1=canciones 2=artistas 3=videos
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                if (line.StartsWith("[CANCIONES]")) { section = 1; continue; }
                if (line.StartsWith("[ARTISTAS]")) { section = 2; continue; }
                if (line.StartsWith("[VIDEOS]")) { section = 3; continue; }
                if (line.StartsWith("[GENEROS]")) { section = 4; continue; }
                if (section == 0)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq); long v;
                    if (!long.TryParse(line.Substring(eq + 1), out v)) continue;
                    if (k == "total-reproducciones") data.statsTotal = v;
                    else if (k == "total-canciones") data.statsSongTotal = v;
                    else if (k == "total-videos") data.statsVideoTotal = v;
                    else if (k == "monedas") data.statsCoins = v;
                    else if (k == "creditos-cargados") data.statsCreditsAdded = v;
                }
                else
                {
                    int b = line.IndexOf('|');
                    if (b <= 0) continue;
                    string key = line.Substring(b + 1);
                    if (key.Length == 0) continue;
                    if (section == 4) { data.songGenre[key] = line.Substring(0, b); continue; }   // genero|ruta (lo de antes del | es la etiqueta, no un numero)
                    int cnt; if (!int.TryParse(line.Substring(0, b), out cnt)) continue;
                    if (section == 1) { data.songCounts[key] = cnt; data.songSeq[key] = ++data.statsSeq; }
                    else if (section == 2) data.artistCounts[key] = cnt;
                    else if (section == 3) { data.videoCounts[key] = cnt; data.videoSeq[key] = ++data.statsSeq; }
                }
            }
            return data;
        }
        // de Form1.Datos.cs
        private sealed class StatsPendientes
        {
            internal FilaStats[] Canciones, Videos;
            internal KeyValuePair<string, int>[] Artistas;
            internal KeyValuePair<string, string>[] Generos;
            internal long Total, CancionesTotal, VideosTotal, Monedas, Creditos;
            internal CultureInfo Cultura;
        }
        // de Form1.Datos.cs
        private static string TextoStats(StatsPendientes snapshot)
        {
            Array.Sort(snapshot.Canciones, OrdenStats); Array.Sort(snapshot.Videos, OrdenStats);
            StringBuilder sb = new StringBuilder(snapshot.Canciones.Length * 48 + 256);
            sb.Append("ROCOLA-STATS v1\r\n");
            sb.Append("total-reproducciones=").Append(snapshot.Total.ToString(snapshot.Cultura)).Append("\r\n");
            sb.Append("total-canciones=").Append(snapshot.CancionesTotal.ToString(snapshot.Cultura)).Append("\r\n");
            sb.Append("total-videos=").Append(snapshot.VideosTotal.ToString(snapshot.Cultura)).Append("\r\n");
            sb.Append("monedas=").Append(snapshot.Monedas.ToString(snapshot.Cultura)).Append("\r\n");
            sb.Append("creditos-cargados=").Append(snapshot.Creditos.ToString(snapshot.Cultura)).Append("\r\n");
            sb.Append("[CANCIONES] count|ruta\r\n");
            for (int i = 0; i < snapshot.Canciones.Length; i++) sb.Append(snapshot.Canciones[i].Cuenta.ToString(snapshot.Cultura)).Append('|').Append(snapshot.Canciones[i].Ruta).Append("\r\n");
            sb.Append("[ARTISTAS] count|artista\r\n");
            foreach (KeyValuePair<string, int> kv in snapshot.Artistas) sb.Append(kv.Value.ToString(snapshot.Cultura)).Append('|').Append(kv.Key).Append("\r\n");
            sb.Append("[VIDEOS] count|ruta\r\n");
            for (int i = 0; i < snapshot.Videos.Length; i++) sb.Append(snapshot.Videos[i].Cuenta.ToString(snapshot.Cultura)).Append('|').Append(snapshot.Videos[i].Ruta).Append("\r\n");
            sb.Append("[GENEROS] genero|ruta\r\n");
            foreach (KeyValuePair<string, string> kv in snapshot.Generos) sb.Append(kv.Value).Append('|').Append(kv.Key).Append("\r\n");
            return sb.ToString();
        }
        // de Form1.Datos.cs
        private readonly object statsSaveLock = new object();
        // de Form1.Datos.cs
        private int statsSaveVersion;
        // de Form1.Datos.cs
        private struct FilaStats
        {
            internal string Ruta; internal int Cuenta; internal long Secuencia;
        }
        // de Form1.Datos.cs
        private static FilaStats[] CopiaContadores(Dictionary<string, int> tabla, Dictionary<string, long> orden)
        {
            FilaStats[] filas = new FilaStats[tabla.Count]; int i = 0;
            foreach (KeyValuePair<string, int> kv in tabla)
            { long seq; orden.TryGetValue(kv.Key, out seq); filas[i++] = new FilaStats { Ruta = kv.Key, Cuenta = kv.Value, Secuencia = seq }; }
            return filas;
        }
        // de Form1.Datos.cs
        // El historial se prepara en privado, sin array de todas las lineas ni acceso al formulario.
        private sealed class StartupStats
        {
            public readonly Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> videoCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, long> videoSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string> songGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public long statsTotal, statsSongTotal, statsVideoTotal, statsCoins, statsCreditsAdded, statsSeq;
        }
        // de Form1.Biblioteca.cs
        private const int ArtistStatCap = 1000, ArtistStatTarget = 900;
        // de Form1.Datos.cs
        private static int OrdenStats(FilaStats a, FilaStats b)
        { return a.Secuencia != b.Secuencia ? (a.Secuencia < b.Secuencia ? -1 : 1) : string.Compare(a.Ruta, b.Ruta, StringComparison.OrdinalIgnoreCase); }
        // de Form1.Datos.cs
        private void PruneCountDict(Dictionary<string, int> counts, Dictionary<string, long> seq, Dictionary<string, string> extra, int cap, int target)
        {
            if (counts.Count <= cap) return;
            List<string> keys = new List<string>(counts.Keys);
            keys.Sort(delegate (string a, string b)
            {
                int ca = counts[a], cb = counts[b];
                if (ca != cb) return ca - cb;              // menos sonado primero (candidato a borrar)
                long sa = 0, sb = 0; if (seq != null) { seq.TryGetValue(a, out sa); seq.TryGetValue(b, out sb); }
                if (sa != sb) return (sa < sb) ? -1 : 1;   // mas viejo primero
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            int remove = counts.Count - target;
            for (int i = 0; i < remove && i < keys.Count; i++)
            {
                string k = keys[i];
                counts.Remove(k);
                if (seq != null) seq.Remove(k);
                if (extra != null) extra.Remove(k);
            }
        }
        // de Form1.cs
        // Topes del archivo de estadisticas: se conserva lo MAS sonado y reciente; lo viejo/poco sonado
        // se descarta solo, para que el archivo nunca crezca de mas ni alente la rocola.
        private const int SongStatCap = 2000, SongStatTarget = 1800;
        // de Form1.Video.cs
        private const int VideoStatCap = 1000, VideoStatTarget = 900;
        // de Form1.Datos.cs
        // Al cruzar las 7 AM (nuevo dia del negocio) reinicia el estado guardado. PERO nunca interrumpe una sesion
        // en uso: si te DESVELAS, tu musica NO deja de sonar y tus creditos NO se tocan. El reinicio ESPERA a que la
        // rocola quede OCIOSA (nada sonando, cola vacia y sin botones un buen rato); recien entonces borra los
        // creditos que hayan quedado (de alguien que se fue) y el archivo guardado -> el dia arranca limpio.
        private void CheckBusinessDayReset()
        {
            if (BusinessDay(DateTime.Now) == stateDay) return;                          // mismo dia del negocio: comun, nada que hacer
            if (nowPlaying != null || queue.Count > 0) return;                          // hay musica o cola: EN USO -> se aplaza
            if ((DateTime.Now - lastInput).TotalMinutes < DayResetIdleMins) return;     // botones recientes: EN USO -> se aplaza
            stateDay = BusinessDay(DateTime.Now);                                       // ociosa y dia nuevo -> reinicia UNA vez
            if (credits > 0) { credits = 0; UpdateCredits(); }                          // creditos que quedaron de anoche -> a cero
            BorraEstado();                                                              // borra el estado guardado (y su copia) -> arranque limpio
            BackupLearnedData();                                                        // cierre del dia: respalda lo aprendido (una vez al dia)
        }
        // de Form1.Datos.cs
        private void BackupLearnedData()
        {
            string sp = CarpetaDatos, second = backupDir, baseDir = BackupBaseDir();
            string[] files = {
                LoudFile(), EqFile(), LibStatsFile(), LibCacheFile(), ConfigFile, KeysFile(),
                RutaLectura("rocola_ia.bin"), RutaLectura("rocola_eq_ia.bin"),
                Path.Combine(sp, "rocola_incompatibles.txt"),
                IaDoneFile(), DeepDoneFile(),  // sin estas dos, un repaso a fondo empezaria de cero y la IA repetiria ejemplos
                DurFastFile(), LetterTintFile(),   // sin este, la barra tendria que releer el ID3 de toda la biblioteca
                IntegFile(), FuentesFile()   // medidas + firmas: deben viajar juntas al restaurar
            };
            RespaldoCapturado captura;
            try { captura = CapturaRespaldo(files); }   // antes del primer guardado de esta sesion
            catch (Exception ex) { LogError("No se pudo capturar el respaldo; anteriores conservados", ex); return; }
            QueueDataWrite(delegate(object ignorado)
            {
                System.Threading.Thread th = System.Threading.Thread.CurrentThread;
                System.Threading.ThreadPriority anterior = th.Priority;
                try
                {
                    th.Priority = System.Threading.ThreadPriority.Lowest;
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
                    BackupDestinoCapturado(baseDir, stamp, captura);
                    if (!string.IsNullOrEmpty(second)) BackupDestinoCapturado(Path.Combine(second, "respaldo_rocola"), stamp, captura);
                }
                catch { }
                finally { captura.Dispose(); try { th.Priority = anterior; } catch { } }
            });
        }
        // de Form1.Datos.cs
        private const int DayResetIdleMins = 10;   // minutos OCIOSA (sin musica/cola/botones) para aplicar el reinicio de 7 AM: si te desvelas, NO te interrumpe
        // de Form1.Datos.cs
        private DateTime stateDay = BusinessDay(DateTime.Now);   // dia del negocio del estado vivo actual
        // de Form1.Datos.cs
        // ===================== RESPALDO DE DATOS APRENDIDOS =====================
        // El disco puede morir (ya paso). La musica no se recupera, pero SI todo lo que la rocola aprendio:
        // volumenes nivelados, huellas del EQ, estadisticas de lo mas sonado, indice de biblioteca,
        // config, mapa de botones y el modelo de IA. Se copia a respaldo\<fecha>\ (y a respaldo_dir= si se
        // configuro, idealmente OTRO disco), guardando los 3 dias mas recientes. Las tablas/modelos pueden ocupar MB:
        // solo se capturan handles y longitudes aqui; copia, hashes y Flush ceden en un trabajador de baja prioridad.
        private string BackupBaseDir() { return RutaDatos("respaldo"); }
        // de Form1.Datos.cs
        private void BackupDestinoCapturado(string root, string stamp, RespaldoCapturado captura)
        {
            try { BackupToCapturado(Path.Combine(root, stamp), captura, CedeRespaldo); PruneBackups(root, 3); }
            catch (Exception ex)
            {
                LogError("Respaldo incompleto en " + root + " (se conservan los anteriores)", ex);
                try { ShowToast("Respaldo incompleto; anteriores conservados. Ver rocola_error.txt"); } catch { }   // (en XP, en la pantalla)
            }
        }
        // de Form1.Datos.cs
        private static RespaldoCapturado CapturaRespaldo(string[] files)
        {
            RespaldoCapturado captura = new RespaldoCapturado();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (string.IsNullOrEmpty(files[i])) continue;
                    // Un anexo en curso queda cortado al largo anterior (ningun registro parcial).
                    // Durante Replace o su fallback no se vuelve a abrir el nombre que esta cambiando.
                    // La referencia retenida vive hasta terminar los destinos, aunque el escritor falle.
                    EstadoRespaldo estado = EstadoRespaldoDe(files[i]);
                    lock (estado)
                    {
                        ArchivoRespaldo archivo = estado.Cambiando
                            ? (estado.Anterior == null ? null : estado.Anterior.Retiene())
                            : AbreArchivoRespaldo(files[i]);
                        if (archivo == null) continue;
                        try { captura.Archivos.Add(archivo); }
                        catch { archivo.Libera(); throw; }
                    }
                }
                return captura;
            }
            catch { captura.Dispose(); throw; }
        }
        // de Form1.Ia.cs
        private string IaDoneFile() { return RutaDatos("rocola_ia_hechas.txt"); }
        // de Form1.Ajustes.cs
        // =================== ASISTENTE DE CONFIGURACION DE BOTONES ===================
        private string KeysFile() { return RutaDatos("rocola_teclas.txt"); }
        // de Form1.Biblioteca.cs
        // Barrido RAPIDO de duraciones: abre cada archivo, lee cuanto dura y lo suelta. No descomprime nada.
        // Se salta lo que ya tiene duracion (por esta via o por el analisis completo). Prioridad minima y un
        // respiro por archivo para no quitarle disco a la musica; con video en pantalla se detiene del todo.
        // ==========  LO QUE ENSENA LA BARRA DE LETRAS  ==========
        // Debajo de cada letra va cuantas canciones hay detras, y la letra se pinta del color del genero que
        // MANDA dentro de ella. Lo primero es gratis (ya esta contado); lo segundo cuesta leer el ID3 de toda
        // la biblioteca, asi que se calcula una vez en segundo plano y se guarda.
        private string LetterTintFile() { return RutaDatos("rocola_letras.txt"); }
        // de Form1.Biblioteca.cs
        // ===================== CACHE DE BIBLIOTECA (arranque rapido) =====================
        // El arranque hacia DOS caminatas completas del disco (audio y video) MAS ~una verificacion de
        // portada por cada disco: en disco duro, minutos. Aqui se guarda el resultado en un archivo y
        // los siguientes arranques lo leen en segundos. Se invalida solo si la carpeta de musica cambio
        // (cantidad de carpetas o fecha de la raiz). F5 siempre fuerza un re-escaneo completo.
        private string LibCacheFile() { return RutaDatos("rocola_biblioteca.txt"); }
        // de Form1.Datos.cs
        private sealed class RespaldoCapturado : IDisposable
        {
            internal readonly List<ArchivoRespaldo> Archivos = new List<ArchivoRespaldo>();
            public void Dispose()
            {
                for (int i = 0; i < Archivos.Count; i++) Archivos[i].Libera();
                Archivos.Clear();
            }
        }
        // de Form1.Datos.cs
        private static void BackupToCapturado(string dir, RespaldoCapturado captura, Action<string> cede)
        {
            lock (backupLock)
            {
                dir = Path.GetFullPath(dir);
                string parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || Directory.Exists(dir) || File.Exists(dir))
                    throw new IOException("El destino de respaldo debe ser una carpeta nueva");
                Directory.CreateDirectory(parent);
                string pending = Path.Combine(parent, ".rocola_backup_pending_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(pending);
                try
                {
                    int copied = 0;
                    byte[] buffer = new byte[65536], otro = new byte[65536];
                    for (int i = 0; i < captura.Archivos.Count; i++)
                    {
                        ArchivoRespaldo archivo = captura.Archivos[i];
                        FileStream input = archivo.Lector; input.Position = 0;
                        {
                            string dst = Path.Combine(pending, Path.GetFileName(archivo.Ruta));
                            // Mientras se copia, el destino es una carpeta PRIVADA no publicada. Forzar cada
                            // bloque de 64 KiB al disco no protege nada adicional ante un apagon: la copia
                            // solo se publica despues de FlushFileBuffers exitoso + copia releida y comparada
                            // byte a byte con el origen capturado + Move. (Antes: dos SHA-256 de cada archivo,
                            // uno de lo leido y otro de la copia, 2 a 8 segundos de procesador en .NET 3.5 al
                            // arrancar; ahora, releer la copia y el origen y comparar, sin ningun hash.)
                            // Los respaldos anteriores se conservan si cualquier paso falla.
                            using (FileStream outputFile = new FileStream(dst, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.None))
                            {
                                long faltan = archivo.Largo;
                                while (faltan > 0)
                                {
                                    if (cede != null) { cede(archivo.Ruta); cede(dst); }
                                    int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, faltan));
                                    if (n == 0) throw new IOException("El origen del respaldo se trunco durante la copia");
                                    outputFile.Write(buffer, 0, n);
                                    faltan -= n;
                                }
                                outputFile.Flush();
                                if (!RespaldoEnDisco(outputFile)) throw new IOException("No se pudo confirmar el respaldo en disco");
                            }
                            CompruebaCopiaDeRespaldo(input, archivo.Largo, dst, buffer, otro, cede, archivo.Ruta);
                            copied++;
                        }
                    }
                    if (copied == 0) throw new IOException("No habia archivos legibles para respaldar");
                    // Rename en el mismo volumen: la limpieza nunca ve un respaldo a medio copiar.
                    Directory.Move(pending, dir);
                }
                finally
                {
                    // Solo la carpeta unica creada por ESTA operacion; nunca un respaldo publicado.
                    if (Directory.Exists(pending)) try { Directory.Delete(pending, true); } catch { }
                }
            }
        }
        // de Form1.Datos.cs
        private void CedeRespaldo(string ruta)
        {
            // Ni nuevos hilos lectores ni un limite fijo que frene equipos potentes.
            // Cede al trabajo visible de la MISMA unidad (a cualquier caratula en 2C).
            if (!startupClosing)
                while (YieldCoverWork(UnidadDe(ruta), CargaTrabajo.Procesadores <= 2))
                { if (startupClosing) break; System.Threading.Thread.Sleep(8); }
            long ahora = System.Diagnostics.Stopwatch.GetTimestamp();
            long limite = System.Diagnostics.Stopwatch.Frequency * (CargaTrabajo.Procesadores <= 2 ? 2 : 5) / 1000;
            if (respaldoPresupuestoInicio == 0 || ahora - respaldoPresupuestoInicio >= limite)
            {
                System.Threading.Thread.Sleep(CargaTrabajo.Procesadores <= 2 && !startupClosing ? 1 : 0);
                respaldoPresupuestoInicio = System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
        // de Form1.Datos.cs
        // Deja solo las 'keep' carpetas de respaldo mas recientes (por fecha de modificacion); borra el resto.
        private static void PruneBackups(string baseDir, int keep)
        {
            try
            {
                DirectoryInfo bd = new DirectoryInfo(baseDir);
                if (!bd.Exists) return;
                List<DirectoryInfo> validas = new List<DirectoryInfo>();
                foreach (DirectoryInfo d in bd.GetDirectories())
                {
                    DateTime fecha;
                    if ((d.Attributes & FileAttributes.ReparsePoint) != 0 || d.Name.Length < 10 ||
                        !DateTime.TryParseExact(d.Name.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ||
                        (d.Name.Length != 10 && d.Name[10] != '_')) continue;
                    if (d.GetFiles().Length > 0) validas.Add(d);
                }
                DirectoryInfo[] dirs = validas.ToArray();
                if (dirs.Length <= keep) return;
                Array.Sort(dirs, delegate (DirectoryInfo a, DirectoryInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = keep; i < dirs.Length; i++) { try { dirs[i].Delete(true); } catch { } }
            }
            catch { }
        }
        // de Form1.Datos.cs
        // La copia ya esta en disco (FlushFileBuffers hecho): se relee y se compara byte a byte con el origen capturado (los primeros 'largo'
        // bytes, que es lo que se copio). Lanza IOException("La copia del respaldo no coincide") si algun byte difiere o si la copia mide otra cosa,
        // y la de siempre si el origen se trunco. Los bloques de lectura son los mismos (64 KiB) y entre bloque y bloque se cede igual.
        private static void CompruebaCopiaDeRespaldo(FileStream origen, long largo, string copia, byte[] a, byte[] b, Action<string> cede, string rutaOrigen)
        {
            origen.Position = 0;
            using (FileStream check = File.OpenRead(copia))
            {
                if (check.Length != largo) throw new IOException("La copia del respaldo no coincide");
                long faltan = largo;
                while (faltan > 0)
                {
                    if (cede != null) { cede(copia); cede(rutaOrigen); }
                    int quiero = (int)Math.Min(a.Length, faltan);
                    int n = LeeCompleto(check, a, quiero), m = LeeCompleto(origen, b, quiero);
                    if (m < quiero) throw new IOException("El origen del respaldo se trunco durante la copia");
                    if (n < quiero) throw new IOException("La copia del respaldo no coincide");
                    for (int k = 0; k < quiero; k++) if (a[k] != b[k]) throw new IOException("La copia del respaldo no coincide");
                    faltan -= quiero;
                }
            }
        }
        // de Form1.Datos.cs
        // El respaldo exige que Windows confirme que esta en el disco. Si la llamada no existe en el sistema (no es Windows: las pruebas en
        // Linux) basta el Flush que ya se hizo; en Windows, si falla, sigue siendo un error (28-10-2026).
        private static bool RespaldoEnDisco(FileStream fs)
        {
            try { return FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); }
            catch (EntryPointNotFoundException) { return true; }
            catch (DllNotFoundException) { return true; }
        }
        // de Form1.cs
        private static string UnidadDe(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return "";
            // La letra de unidad se lee a mano: da lo mismo que GetPathRoot en Windows ("C:\", "C:") y tambien vale fuera de Windows (las
            // pruebas en Linux, donde GetPathRoot("C:\x") da "" y todas las rutas parecian de la misma unidad).
            if (ruta.Length >= 2 && ruta[1] == ':' && char.IsLetter(ruta[0]))
                return ruta.Length >= 3 && (ruta[2] == '\\' || ruta[2] == '/') ? ruta.Substring(0, 3) : ruta.Substring(0, 2);
            try { string r = Path.GetPathRoot(ruta); return r ?? ""; }
            catch { return ""; }
        }
        // (en XP: YieldCoverWork, el respaldo cede el disco a las caratulas; en Rocola OS las caratulas no van por ese despachador)
        private bool YieldCoverWork(string unit, bool smallCpu) { return false; }
        // de Form1.Datos.cs
        private static readonly object backupLock = new object();
        // de Form1.Datos.cs
        private long respaldoPresupuestoInicio;
        // de Form1.cs
        private bool startupClosing;
        // de Form1.Datos.cs
        private static int LeeCompleto(Stream s, byte[] buf, int cuantos)
        {
            int hecho = 0;
            while (hecho < cuantos)
            {
                int n = s.Read(buf, hecho, cuantos - hecho);
                if (n <= 0) break;
                hecho += n;
            }
            return hecho;
        }
        // de Form1.Datos.cs
        // Construye el disco virtual "Lo mas sonado" con las canciones MAS PEDIDAS (de las estadisticas),
        // ordenadas de mas a menos. Se arma al entrar a la seccion, asi siempre refleja lo actual.
        // Construye los discos virtuales "TOP - <genero>" con las canciones mas pedidas de cada genero.
        private void BuildTopDiscs()
        {
            if (!topDirty && topDiscs != null) return;   // OPTIMIZACION (TOP): nada cambio -> reusar lo ya armado
            if (libBuilding) return;                     // el worker esta mutando albums/videoAlbums: iterarlos seria una carrera (crash). topDirty queda puesto -> se reconstruye al terminar
            topDirty = false;                            // limpio AQUI: cubre todas las salidas (incluidas las tempranas)
            topDiscs = new List<Album>();
            if (songCounts.Count == 0) return;

            // 1) recolectar todas las canciones pedidas que EXISTEN en la biblioteca, con su disco de origen
            List<TopEntry> all = new List<TopEntry>();
            Dictionary<string, List<OrigenTop>> indice = object.ReferenceEquals(albumsDelIndiceTop, albums) ? origenesTop : null;
            if (indice == null) indice = IndiceTop(albums);   // respaldo para bibliotecas armadas sin el escaner
            List<OrigenTop> elegidos = new List<OrigenTop>();
            foreach (KeyValuePair<string, int> cuenta in songCounts)
            {
                List<OrigenTop> origen;
                if (indice.TryGetValue(cuenta.Key, out origen)) elegidos.AddRange(origen);
            }
            elegidos.Sort(delegate(OrigenTop a, OrigenTop b) { return a.Orden.CompareTo(b.Orden); });
            for (int i = 0; i < elegidos.Count; i++)
            {
                OrigenTop o = elegidos[i]; int c;
                if (!songCounts.TryGetValue(o.Pista.Path, out c)) continue;
                TopEntry te = new TopEntry(); te.T = o.Pista; te.C = c; te.A = o.Disco; all.Add(te);
            }
            if (all.Count == 0) return;

            // 2) agrupar por etiqueta de genero (las que no tengan genero capturado caen en "Otros")
            Dictionary<string, List<TopEntry>> byGenre = new Dictionary<string, List<TopEntry>>();
            List<string> order = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string gl;
                if (!songGenre.TryGetValue(all[i].T.Path, out gl) || string.IsNullOrEmpty(gl)) gl = "Otros";
                List<TopEntry> lst;
                if (!byGenre.TryGetValue(gl, out lst)) { lst = new List<TopEntry>(); byGenre[gl] = lst; order.Add(gl); }
                lst.Add(all[i]);
            }

            // 3) un disco virtual por genero
            List<Album> discs = new List<Album>();
            Dictionary<Album, long> totals = new Dictionary<Album, long>();
            for (int gi = 0; gi < order.Count; gi++)
            {
                string gl = order[gi];
                List<TopEntry> lst = byGenre[gl];
                lst.Sort(delegate (TopEntry x, TopEntry y)
                {
                    if (y.C != x.C) return y.C - x.C;
                    return string.Compare(x.T.Path, y.T.Path, StringComparison.OrdinalIgnoreCase);
                });
                int take = (lst.Count < TopMax) ? lst.Count : TopMax;
                Album d = new Album();
                d.IsTop = true; d.Letter = TopMark; d.Folder = "::TOP::" + gl;
                d.Artist = "TOP 100"; d.Name = gl;             // AlbumLabel -> "TOP 100 - Rock"
                d.Tracks = new List<Track>();
                d.TrackCovers = new List<string>();
                d.TrackInfo = new List<string>();
                d.TrackAlbums = new List<Album>();
                List<string> cycle = new List<string>();
                long total = 0;
                for (int i = 0; i < take; i++)
                {
                    TopEntry te = lst[i];
                    d.Tracks.Add(te.T);
                    d.TrackCovers.Add(te.A.CoverPath);
                    d.TrackInfo.Add(AlbumLabel(te.A));
                    d.TrackAlbums.Add(te.A);
                    if (te.A.CoverPath != null && !cycle.Contains(te.A.CoverPath)) cycle.Add(te.A.CoverPath);
                    total += te.C;
                }
                d.CycleCovers = cycle;
                d.CoverPath = (cycle.Count > 0) ? cycle[0] : null;
                totals[d] = total;
                discs.Add(d);
            }

            // 3a) discos TOP por ARTISTA muy puesto (hasta 10), RANKEADOS: "TOP #1 - <artista>" ... "TOP #10 - ..."
            {
                Dictionary<string, List<TopEntry>> byArtist = new Dictionary<string, List<TopEntry>>(StringComparer.OrdinalIgnoreCase);
                List<string> aOrder = new List<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    string ar = all[i].T.Artist;
                    if (string.IsNullOrEmpty(ar)) continue;
                    List<TopEntry> lst;
                    if (!byArtist.TryGetValue(ar, out lst)) { lst = new List<TopEntry>(); byArtist[ar] = lst; aOrder.Add(ar); }
                    lst.Add(all[i]);
                }
                // candidatos: artistas con >=2 canciones distintas puestas (un solo tema no amerita disco)
                List<string> cand = new List<string>();
                Dictionary<string, long> aTot = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < aOrder.Count; i++)
                {
                    string ar = aOrder[i];
                    List<TopEntry> lst = byArtist[ar];
                    if (lst.Count < 2) continue;
                    long t = 0; for (int j = 0; j < lst.Count; j++) t += lst[j].C;
                    aTot[ar] = t; cand.Add(ar);
                }
                cand.Sort(delegate (string a, string b)
                {
                    long ta = aTot[a], tb = aTot[b];
                    if (tb != ta) return (tb > ta) ? 1 : -1;
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });
                int amax = (cand.Count < 10) ? cand.Count : 10;   // hasta 10 artistas mas puestos
                for (int ai = 0; ai < amax; ai++)
                {
                    string ar = cand[ai];
                    List<TopEntry> lst = byArtist[ar];
                    lst.Sort(delegate (TopEntry x, TopEntry y)
                    {
                        if (y.C != x.C) return y.C - x.C;
                        return string.Compare(x.T.Path, y.T.Path, StringComparison.OrdinalIgnoreCase);
                    });
                    int take = (lst.Count < TopMax) ? lst.Count : TopMax;
                    Album d = new Album();
                    // RANKING VISIBLE: el puesto (#1..#10, por total de reproducciones) va en la etiqueta. Tambien
                    // va en Folder (la CLAVE de la celda horneada): si un artista cambia de puesto, su etiqueta se
                    // re-hornea; con la clave vieja se habria quedado el numero anterior pegado en el cache.
                    d.IsTop = true; d.Letter = TopMark; d.Folder = "::TOPART::" + (ai + 1) + "|" + ar;
                    d.Artist = "TOP #" + (ai + 1); d.Name = ar;             // AlbumLabel -> "TOP #1 - Depeche Mode"
                    d.Tracks = new List<Track>();
                    d.TrackCovers = new List<string>();
                    d.TrackInfo = new List<string>();
                    d.TrackAlbums = new List<Album>();
                    List<string> cyc = new List<string>();
                    for (int i = 0; i < take; i++)
                    {
                        TopEntry te = lst[i];
                        d.Tracks.Add(te.T);
                        d.TrackCovers.Add(te.A.CoverPath);
                        d.TrackInfo.Add(AlbumLabel(te.A));
                        d.TrackAlbums.Add(te.A);
                        if (te.A.CoverPath != null && !cyc.Contains(te.A.CoverPath)) cyc.Add(te.A.CoverPath);
                    }
                    d.CycleCovers = cyc;
                    d.CoverPath = (cyc.Count > 0) ? cyc[0] : null;
                    totals[d] = aTot[ar];
                    discs.Add(d);
                }
            }

            // 3b) disco TOP de VIDEOS (de videoCounts, recorriendo los discos con video)
            if (videoCounts.Count > 0 && videoAlbums != null)
            {
                List<TopEntry> vids = new List<TopEntry>();
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    for (int j = 0; j < va.VideoFiles.Count; j++)
                    {
                        string vf = va.VideoFiles[j];
                        int c;
                        if (vf != null && videoCounts.TryGetValue(vf, out c))
                        { TopEntry te = new TopEntry(); te.V = vf; te.C = c; te.A = va; vids.Add(te); }
                    }
                }
                if (vids.Count > 0)
                {
                    vids.Sort(delegate (TopEntry x, TopEntry y)
                    {
                        if (y.C != x.C) return y.C - x.C;
                        return string.Compare(x.V, y.V, StringComparison.OrdinalIgnoreCase);
                    });
                    int vtake = (vids.Count < TopMax) ? vids.Count : TopMax;
                    Album vd = new Album();
                    vd.IsTop = true; vd.TopIsVideo = true; vd.Letter = TopMark; vd.Folder = "::TOPVID::";
                    vd.Artist = "TOP 100"; vd.Name = "Videos";
                    vd.VideoFiles = new List<string>();
                    vd.TrackCovers = new List<string>();
                    vd.TrackInfo = new List<string>();
                    vd.TrackAlbums = new List<Album>();
                    List<string> vcyc = new List<string>();
                    long vtotal = 0;
                    for (int i = 0; i < vtake; i++)
                    {
                        TopEntry te = vids[i];
                        vd.VideoFiles.Add(te.V);
                        vd.TrackCovers.Add(te.A.CoverPath);
                        vd.TrackInfo.Add(AlbumLabel(te.A));
                        vd.TrackAlbums.Add(te.A);
                        if (te.A.CoverPath != null && !vcyc.Contains(te.A.CoverPath)) vcyc.Add(te.A.CoverPath);
                        vtotal += te.C;
                    }
                    vd.CycleCovers = vcyc;
                    vd.CoverPath = (vcyc.Count > 0) ? vcyc[0] : null;
                    totals[vd] = vtotal;
                    discs.Add(vd);
                }
            }

            // 4) ordenar por CLASE (genero primero, luego videos, luego artistas) y dentro de
            //    cada clase por total de reproducciones. Asi quedan agrupados y comodos de ver.
            discs.Sort(delegate (Album x, Album y)
            {
                int cx = x.TopIsVideo ? 1 : ((x.Folder != null && x.Folder.StartsWith("::TOPART::", StringComparison.Ordinal)) ? 2 : 0);
                int cy = y.TopIsVideo ? 1 : ((y.Folder != null && y.Folder.StartsWith("::TOPART::", StringComparison.Ordinal)) ? 2 : 0);
                if (cx != cy) return cx - cy;
                long tx = totals[x], ty = totals[y];
                if (ty != tx) return (ty > tx) ? 1 : -1;
                return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            topDiscs = discs;
        }
        // de Form1.cs
        private List<Album> topDiscs = new List<Album>();   // discos virtuales "TOP - <genero>"
        // de Form1.Biblioteca.cs
        private static string AlbumLabel(Album a)
        {
            if (a == null) return "";
            return (a.Name != null && a.Name.Length > 0) ? (a.Artist + " - " + a.Name) : a.Artist;
        }
        // de Form1.Datos.cs
        private class TopEntry { public Track T; public int C; public Album A; public string V; }
        // de Form1.cs
        private const int TopMax = 100;             // tope de canciones del disco "Lo mas sonado"
        // de Form1.Biblioteca.cs
        private volatile bool libBuilding = false;   // el worker esta armando albums/byLetter/searchIndex: la UI no debe leerlos
        // de Form1.Buscador.cs
        private class SearchItem
        {
            public int Kind;     // 0=artista, 1=album, 2=cancion
            public string Display;
            public string Key;
            public int TitleAt, TitleLength; // titulo/artista principal dentro de Key, sin otra copia
            public int Mask;    // que letras contiene Key (ver MarcaLetras)
            public int Score;
            public Album AlbumRef;
            public Track TrackRef;
            public List<Album> Albums;   // todos los discos del artista (Kind==0)
        }
        // de Form1.Buscador.cs
        // Lo que casa con cada letra SUELTA, ya puntuado. Ver RecomputeResults: la primera letra es la unica
        // que obliga a puntuar el indice entero, y para una letra el resultado no cambia mientras no cambie
        // la musica. Se vacia al rearmar la biblioteca.
        private sealed class BolsaLetra
        {
            public List<SearchItem> Lista;
            public int[] Puntos;
            public List<SearchItem> Mejores;
            public bool Approximate;
        }
        // de Form1.Biblioteca.cs
        private void BuildSearchIndex()
        {
            ClearRecentQueries();
            lastQuery = null; lastMatches = null;
            if (bolsaLetra != null) bolsaLetra.Clear();
            searchPostings = null;
            searchIndex.Clear();
            // Mapa artista -> todos sus discos (para el cubo que cicla caratulas)
            Dictionary<string, List<Album>> artistAlbums = new Dictionary<string, List<Album>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++)
            {
                Album a = albums[i];
                if (string.IsNullOrEmpty(a.Artist)) continue;
                List<Album> lst;
                if (!artistAlbums.TryGetValue(a.Artist, out lst)) { lst = new List<Album>(); artistAlbums[a.Artist] = lst; }
                lst.Add(a);
            }
            HashSet<string> artistSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++)
            {
                Album a = albums[i];
                if (!string.IsNullOrEmpty(a.Artist) && artistSeen.Add(a.Artist))
                {
                    SearchItem ar = new SearchItem();
                    ar.Kind = 0; ar.Display = a.Artist; ar.AlbumRef = a;
                    artistAlbums.TryGetValue(a.Artist, out ar.Albums);
                    searchIndex.Add(ar);
                }
                SearchItem alb = new SearchItem();
                alb.Kind = 1; alb.Display = a.Artist + " - " + a.Name; alb.AlbumRef = a;
                searchIndex.Add(alb);
                for (int j = 0; j < a.Tracks.Count; j++)
                {
                    Track t = a.Tracks[j];
                    SearchItem so = new SearchItem();
                    so.Kind = 2; so.Display = t.Artist + " - " + t.Title; so.TrackRef = t; so.AlbumRef = a;
                    searchIndex.Add(so);
                }
            }
            // La parte cara -limpiar acentos y sacar la marca de letras- es la misma cuenta repetida una vez
            // por entrada, y cada entrada solo depende de su propio texto: se puede repartir. Va aparte del
            // bucle de arriba a proposito, para que el indice quede armado y en orden ANTES de repartir.
            ClavesDelIndice();
            BuildSearchPostings();
        }
        // de Form1.Buscador.cs
        private BolsaLetra FindLetterQuery(char key)
        {
            BolsaLetra data;
            if (bolsaLetra == null || !bolsaLetra.TryGetValue(key, out data)) return null;
            if (letterQueryOrder == null) letterQueryOrder = new List<char>();
            letterQueryOrder.Remove(key); letterQueryOrder.Add(key);
            return data;
        }
        // de Form1.Buscador.cs
        private BolsaLetra FindRecentQuery(string key)
        {
            if (recentQueries == null || key.Length > 128) return null;
            for (int i = 0; i < recentQueries.Count; i++)
            {
                QueryBag entry = recentQueries[i];
                if (entry.Key != key) continue;
                recentQueries.RemoveAt(i); recentQueries.Add(entry);
                return entry.Data;
            }
            return null;
        }
        // de Form1.Biblioteca.cs
        // MARCA DE LETRAS. Un numero de 32 bits que dice QUE letras contiene un texto. Con el se descarta
        // de un plumazo, SIN mirarle el texto, toda entrada a la que le falte alguna letra de lo tecleado:
        // si al cliente le falta una sola de sus letras, esa cancion no puede casar ni entera ni por
        // palabras. Comparar dos numeros cuesta nada; recorrer el titulo tres veces, no.
        // Bits 0..25 = letras a..z ; bit 26 = alguna cifra ; bit 27 = cualquier otro caracter. Los espacios
        // no cuentan. Meter todo lo raro en un mismo bit es a proposito: puede dejar pasar candidatos de
        // mas -que luego descarta Score-, pero nunca puede tirar uno bueno.
        private static int MarcaLetras(string s)
        {
            int m = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ') continue;
                if (c >= 'a' && c <= 'z') m |= 1 << (c - 'a');
                else if (c >= '0' && c <= '9') m |= 1 << 26;
                else m |= 1 << 27;
            }
            return m;
        }
        // de Form1.Buscador.cs
        private void RememberLetterQuery(char key, BolsaLetra data)
        {
            // Maximo 16 letras y 262144 referencias, tambien con bibliotecas grandes
            // o alfabetos distintos. No es un limite de resultados ni del indice.
            if (data.Lista.Count > 131072 || bolsaLetra == null) return;
            if (letterQueryOrder == null) letterQueryOrder = new List<char>();
            int retained = 0;
            foreach (BolsaLetra old in bolsaLetra.Values) retained += old.Lista.Count;
            while (bolsaLetra.Count > 0 && (bolsaLetra.Count >= 16 || retained + data.Lista.Count > 262144))
            {
                char oldest;
                if (letterQueryOrder.Count > 0)
                { oldest = letterQueryOrder[0]; letterQueryOrder.RemoveAt(0); }
                else
                {
                    // Tambien tolerar que una prueba o el reescaneo vacie el diccionario solo.
                    oldest = key;
                    foreach (char candidate in bolsaLetra.Keys) { oldest = candidate; break; }
                }
                BolsaLetra old;
                if (bolsaLetra.TryGetValue(oldest, out old)) { retained -= old.Lista.Count; bolsaLetra.Remove(oldest); }
            }
            bolsaLetra[key] = data;
            letterQueryOrder.Remove(key); letterQueryOrder.Add(key);
        }
        // de Form1.Buscador.cs
        private void RememberQuery(string key, BolsaLetra data)
        {
            if (recentQueries == null) recentQueries = new List<QueryBag>();
            while (recentQueries.Count > 0 && (recentQueries.Count >= 8 || recentQueryItems + data.Lista.Count > 65536))
            {
                recentQueryItems -= recentQueries[0].Data.Lista.Count;
                recentQueries.RemoveAt(0);
            }
            QueryBag entry = new QueryBag(); entry.Key = key; entry.Data = data;
            recentQueries.Add(entry); recentQueryItems += data.Lista.Count;
        }
        // de Form1.Biblioteca.cs
        // OPTIMIZACION (busqueda): q.Split(' ') se calculaba DENTRO de Score, que corre por CADA
        // entrada de searchIndex (decenas de miles) en CADA tecla. Ahora se recibe ya calculado
        // (una sola vez por tecla desde RecomputeResults). Se conserva el puntaje historico,
        // salvo que una coincidencia real en etiquetas enormes ya no puede puntuar cero/negativo.
        private static int Score(string cand, string q, string[] toks)
        {
            if (q.Length == 0) return 0;
            if (cand == q) return 10000;
            if (cand.StartsWith(q, StringComparison.Ordinal)) return 6000 - Math.Min(cand.Length, 2000);
            // UNA sola pasada por el texto. Antes se recorria DOS veces: una buscando " q" (empieza palabra)
            // y otra buscando "q" (en cualquier sitio). Para los que NO casan -que son la inmensa mayoria del
            // indice- las dos recorrian el texto ENTERO antes de rendirse. Ahora se busca "q" una vez y se
            // mira si la letra de delante era un espacio, que es lo mismo que buscar " q".
            int primera = -1;
            int pos = cand.IndexOf(q, StringComparison.Ordinal);
            while (pos >= 0)
            {
                if (primera < 0) primera = pos;
                // Etiquetas muy largas no pueden convertir una coincidencia real en cero/negativo.
                if (pos > 0 && cand[pos - 1] == ' ') return Math.Max(1, 4000 - (pos - 1));
                pos = cand.IndexOf(q, pos + 1, StringComparison.Ordinal);
            }
            if (primera >= 0) return 2000 - Math.Min(primera, 1500);
            // Coincidencia por PALABRAS: todas las palabras de la consulta aparecen (en cualquier orden).
            // Asi "metallica black" encuentra "Metallica - Black Album", y "enter metallica" tambien.
            if (toks != null)
            {
                int sc = 0, real = 0;
                for (int i = 0; i < toks.Length; i++)
                {
                    string tk = toks[i];
                    if (tk.Length == 0) continue;
                    real++;
                    int at = cand.IndexOf(tk, StringComparison.Ordinal);
                    if (at < 0) return 0;   // falta una palabra -> no coincide
                    bool wordStart = (at == 0) || (cand[at - 1] == ' ');
                    sc += (wordStart ? 300 : 150) - Math.Min(at, 100);
                }
                if (real >= 2) return 800 + sc;   // menos que substring directo, pero cuenta como coincidencia
            }
            return 0;
        }
        // de Form1.Buscador.cs
        private List<SearchItem> SearchCandidates(List<SearchItem> source, int mask)
        {
            if (!object.ReferenceEquals(source, searchIndex) || searchPostings == null) return source;
            List<SearchItem> best = source;
            for (int bit = 0; bit < searchPostings.Length; bit++)
            {
                List<SearchItem> list = searchPostings[bit];
                if ((mask & (1 << bit)) != 0 && list != null && list.Count < best.Count) best = list;
            }
            return best;
        }
        // de Form1.Biblioteca.cs
        // Los N mejores de una lista, SIN ordenar el resto. Se recorre una vez manteniendo una lista corta
        // ordenada: cada candidato solo se compara con el peor de los que ya estan dentro, y solo si lo mejora
        // se le hace sitio. Da exactamente los mismos N primeros que ordenarlo todo.
        private static void TomaMejores(List<SearchItem> src, List<SearchItem> dst, int n)
        {
            int cnt = src.Count;
            if (cnt <= 0 || n <= 0) return;
            if (n > cnt) n = cnt;
            SearchItem[] top = new SearchItem[n];
            int hay = 0;
            for (int i = 0; i < cnt; i++)
            {
                SearchItem v = src[i];
                if (hay < n)
                {
                    int j = hay++;
                    while (j > 0 && ComparaHit(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; }
                    top[j] = v;
                }
                else if (ComparaHit(v, top[n - 1]) < 0)
                {
                    int j = n - 1;
                    while (j > 0 && ComparaHit(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; }
                    top[j] = v;
                }
            }
            for (int i = 0; i < hay; i++) dst.Add(top[i]);
        }
        // de Form1.cs
        private List<SearchItem> lastMatches = null;          // coincidencias previas (para acotar el re-escaneo)
        // de Form1.Buscador.cs
        private bool lastMatchesApproximate;
        // de Form1.cs
        private string lastQuery = null;                      // busqueda incremental
        // de Form1.cs
        private readonly List<SearchItem> searchIndex = new List<SearchItem>();
        // de Form1.Buscador.cs
        private void BuildSearchPostings()
        {
            searchPostings = null;
            int n = searchIndex.Count;
            if (n < 16000) return;
            int[] counts = new int[28];
            for (int i = 0; i < n; i++)
                for (int bit = 0; bit < counts.Length; bit++)
                    if ((searchIndex[i].Mask & (1 << bit)) != 0) counts[bit]++;
            List<SearchItem>[] postings = new List<SearchItem>[28];
            int limit = Math.Min(n / 8, 8192);
            int selected = 0;
            for (int bit = 0; bit < counts.Length; bit++)
                if (counts[bit] <= limit)
                {
                    postings[bit] = new List<SearchItem>(counts[bit]);
                    selected |= 1 << bit;
                }
            for (int i = 0; i < n; i++)
            {
                SearchItem item = searchIndex[i];
                int mask = item.Mask & selected;
                if (mask == 0) continue;
                for (int bit = 0; bit < postings.Length; bit++)
                    if ((mask & (1 << bit)) != 0) postings[bit].Add(item);
            }
            searchPostings = postings;
        }
        // de Form1.Biblioteca.cs
        private void ClavesDelIndice()
        {
            int n = searchIndex.Count;
            int hilos = IndexWorkers(n, CargaTrabajo.Procesadores);
            if (hilos <= 1) { TramoDeClaves(0, n); return; }

            int trozo = (n + hilos - 1) / hilos;
            System.Threading.Thread[] th = new System.Threading.Thread[hilos - 1];
            // Una excepcion sin atrapar en un hilo cierra la rocola en CLR 2: si un ayudante falla, se atrapa y su tramo se rehace aqui, en el hilo que reparte
            // (que ya tiene su propio manejo de errores). Cada tramo escribe solo dentro de sus entradas, asi que rehacerlo es seguro.
            bool[] fallo = new bool[hilos - 1];
            for (int k = 1; k < hilos; k++)
            {
                int desde = k * trozo;
                int hasta = Math.Min(n, desde + trozo);
                int yo = k - 1;
                th[k - 1] = new System.Threading.Thread(delegate () { try { TramoDeClaves(desde, hasta); } catch { fallo[yo] = true; } });
                th[k - 1].IsBackground = true;
                th[k - 1].Priority = System.Threading.ThreadPriority.BelowNormal;
                th[k - 1].Start();
            }
            TramoDeClaves(0, Math.Min(n, trozo));   // el hilo que reparte tambien lleva su tramo
            for (int k = 0; k < th.Length; k++) th[k].Join();
            for (int k = 0; k < th.Length; k++)
                if (fallo[k]) TramoDeClaves((k + 1) * trozo, Math.Min(n, (k + 2) * trozo));
        }
        // de Form1.Buscador.cs
        private void ClearRecentQueries()
        {
            if (recentQueries != null) recentQueries.Clear();
            recentQueryItems = 0;
            if (bolsaLetra != null) bolsaLetra.Clear();
            if (letterQueryOrder != null) letterQueryOrder.Clear();
        }
        // de Form1.Biblioteca.cs
        // El orden en que se ensenan los resultados: primero el que mejor casa, a igualdad antes el artista
        // que el disco y antes el disco que la cancion, y a igualdad de todo, por nombre.
        private static int ComparaHit(SearchItem a, SearchItem b)
        {
            if (b.Score != a.Score) return b.Score - a.Score;
            if (a.Kind != b.Kind) return a.Kind - b.Kind;
            return string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
        }
        // de Form1.Buscador.cs
        private sealed class QueryBag { internal string Key; internal BolsaLetra Data; }
        // de Form1.cs
        private readonly Dictionary<char, BolsaLetra> bolsaLetra = new Dictionary<char, BolsaLetra>();
        // de Form1.Buscador.cs
        private List<char> letterQueryOrder;
        // de Form1.Buscador.cs
        private List<QueryBag> recentQueries;
        // de Form1.Buscador.cs
        private int recentQueryItems;
        // de Form1.Biblioteca.cs
        // Solo listas selectivas y completas, en el orden original. Como maximo
        // 28 * 8192 referencias; las letras comunes siguen usando el recorrido normal.
        private List<SearchItem>[] searchPostings;
        // de Form1.Biblioteca.cs
        // LAS CLAVES DEL BUSCADOR, REPARTIDAS ENTRE LOS NUCLEOS.
        //
        // Cada entrada mira solo su propio texto y escribe solo dentro de si misma, asi que no hay nada
        // compartido que proteger: ni candados ni esperas. Se reparte por TRAMOS y no de una en una porque
        // el trabajo de cada entrada es minusculo y ponerse de acuerdo costaria mas que hacerlo.
        //
        // Reservar margen para interfaz y musica, tambien al reescanear durante una reproduccion.
        // En 2C/2T se usa solo el worker actual. Por debajo de 8000 entradas no compensa repartir.
        //
        // Al volver de los Join ya no queda nadie tocando el indice, y ademas Join deja a la vista de este
        // hilo todo lo que escribieron los otros: de aqui en adelante el indice se lee como siempre.
        internal static int IndexWorkers(int count, int processors)
        { return count < 8000 ? 1 : CargaTrabajo.Lectores(processors); }
        // de Form1.Biblioteca.cs
        private void TramoDeClaves(int desde, int hasta)
        {
            for (int i = desde; i < hasta; i++)
            {
                SearchItem si = searchIndex[i];
                string artist = si.Kind == 0 ? si.Display : si.Kind == 2 && si.TrackRef != null
                    ? si.TrackRef.Artist : si.AlbumRef == null ? "" : si.AlbumRef.Artist;
                string title = si.Kind == 0 ? "" : si.Kind == 2 && si.TrackRef != null
                    ? si.TrackRef.Title : si.AlbumRef == null ? si.Display : si.AlbumRef.Name;
                string artistKey = MotorBusqueda.Normaliza(artist), titleKey = MotorBusqueda.Normaliza(title);
                si.Key = artistKey.Length == 0 ? titleKey : titleKey.Length == 0 ? artistKey : artistKey + " " + titleKey;
                si.TitleAt = si.Kind == 0 || artistKey.Length == 0 || titleKey.Length == 0 ? 0 : artistKey.Length + 1;
                si.TitleLength = si.Key.Length - si.TitleAt;
                // Se conserva el numero en Key (tambien se puede buscar); solo la prioridad
                // de un titulo exacto ignora el prefijo de pista ya oculto en la presentacion.
                if (si.Kind == 2 && MotorBusqueda.PrefijoPista(title))
                {
                    int firstSpace = titleKey.IndexOf(' ');
                    if (firstSpace >= 0 && firstSpace + 1 < titleKey.Length)
                    { si.TitleAt += firstSpace + 1; si.TitleLength -= firstSpace + 1; }
                }
                si.Mask = MarcaLetras(si.Key);
            }
        }
        // de Form1.Video.cs
        private const char VideoMark = '\u25BA';   // "letra" especial = seccion VIDEOS
        // de Form1.Video.cs
        private void BuildVideoSection(List<string> roots)
        {
            InvalidateVideoMeasurements();
            string[] folderMemo = new string[2];
            // carpeta -> album de audio existente
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++) { albums[i].VideoFiles.Clear(); byFolder[albums[i].Folder] = albums[i]; }

            Dictionary<string, Album> videoOnly = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            // Fuente unica: la lista de videos que ya viene lista (del cache al arrancar, o del recorrido del
            // disco en un escaneo fresco). Antes aqui se volvia a caminar TODO el arbol solo por los videos.
            List<string> vsrc = cachedVideoPaths ?? new List<string>();
            foreach (string vf in vsrc)
            {
                string folder = AlbumFolderCached(vf, roots, folderMemo);
                Album al;
                if (byFolder.TryGetValue(folder, out al)) al.VideoFiles.Add(vf);
                else if (videoOnly.TryGetValue(folder, out al)) al.VideoFiles.Add(vf);
                else
                {
                    al = new Album();
                    al.Folder = folder;
                    al.Artist = LeafName(folder);
                    al.Name = "";
                    al.Letter = FirstLetter(al.Artist);
                    al.CoverPath = FindCoverCached(folder);
                    al.VideoFiles.Add(vf);
                    videoOnly[folder] = al;
                }
            }

            videoAlbums = new List<Album>();
            for (int i = 0; i < albums.Count; i++) if (albums[i].VideoFiles.Count > 0) videoAlbums.Add(albums[i]);
            foreach (KeyValuePair<string, Album> kv in videoOnly) videoAlbums.Add(kv.Value);
            videoAlbums.Sort(delegate (Album a, Album b) { return string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase); });
            for (int i = 0; i < videoAlbums.Count; i++) videoAlbums[i].VideoFiles.Sort(StringComparer.OrdinalIgnoreCase);
            lastVideoPaths = cachedVideoPaths;   // para guardar el cache
        }
        // de Form1.cs
        private List<string> cachedVideoPaths;            // rutas de video del cache de biblioteca (arranque rapido)
        // de Form1.Video.cs
        // VOLUMEN DE LOS VIDEOS: el video lo reproduce Windows Media Player, cuyo sonido va DIRECTO a la
        // tarjeta sin pasar por la cadena de audio de la rocola (nivelado, limitador, EQ...). Por eso los
        // videos entraban a un volumen distinto al de las canciones ya niveladas. Esta perilla (Ctrl+F3) es
        // la BASE con la que suenan; ademas se le aplican los dB de "Subir TODO" para que sigan al resto.
        private int videoVol = 85;                 // 0..100, base del volumen de los videos ("video_vol=" en config)
        // de Form1.cs
        private List<string> lastVideoPaths;              // rutas de video del ultimo armado (para guardar el cache)
        // de Form1.SinPausa.cs
        // La cadena, a otra frecuencia (la de la salida): el convertidor quita el cebado a la tasa del origen y un limitador cuida los
        // picos que la conversion pueda dejar. El cebado que queda es el de ese limitador.
        private static void ConvierteSalida(CadenaCancion c, NAudio.Wave.WaveFormat a, double[] tabla)
        {
            RemuestreoContinuo r = new RemuestreoContinuo(c.Salida, a, c.Retardo, tabla);
            LimitadorPicoReal lim = new LimitadorPicoReal(r, TechoPicoReal, 3.0);
            c.Salida = lim; c.Retardo = lim.DelayFrames; c.Conversion = r; c.LimiteSalida = lim;
        }
        // de Form1.Datos.cs
        // Los segundos que faltan para que empiece TU cancion, o -1 si NO SE PUEDE SABER.
        //
        // Basta con que de UNA de las que van por delante no se sepa la duracion -recien copiada al
        // disco, aun sin analizar- para que la cuenta entera deje de valer. Y entonces hay que decirlo,
        // no redondear: un "en ~11 min" que luego son veinte es peor que no decir nada, porque la
        // proxima vez ya nadie se lo cree.
        internal static double EsperaSegundos(double[] antesQueLaTuya, double restanDeLaQueSuena)
        {
            if (restanDeLaQueSuena < 0) return -1;
            double s = restanDeLaQueSuena;
            if (antesQueLaTuya != null)
                for (int i = 0; i < antesQueLaTuya.Length; i++)
                {
                    if (!(antesQueLaTuya[i] > 0)) return -1;
                    s += antesQueLaTuya[i];
                }
            return s;
        }
        // de Form1.cs
        // Y como se dice. Sin tiempo fiable se dice SOLO el puesto: sigue sirviendo y no miente.
        internal static string TextoTurno(int puesto, double segundos)
        {
            // Apagado desde Ctrl+F3 devuelve texto vacio, y con eso el aviso vuelve solo al "Cancion
            // agregada" de siempre: el interruptor vive AQUI, en el unico sitio que escribe el letrero,
            // y no repartido por los dos sitios que lo piden.
            if (!turnoOn || puesto <= 0) return "";
            string donde = (puesto == 1) ? "Tu cancion es la siguiente" : ("Tu cancion es la " + puesto + "a");
            if (segundos < 0) return donde;
            if (segundos < 60) return donde + " - en menos de 1 min";
            return donde + " - en ~" + (int)((segundos + 30) / 60) + " min";
        }
        // de Form1.cs
        private static bool turnoOn = true;       // "turno=" en config
    }
}

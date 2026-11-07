// VOLUMEN PAREJO APRENDIDO (27 de octubre de 2026, tercera tanda del modo billar). El nivelado al limite (Sonoridad.cs, GananciaAlLimite)
// mide los picos de cada cancion EN SU ARCHIVO, con unos margenes calibrados para la cadena de fabrica. En el billar la cadena cambia esos
// picos: el corte de 40 Hz y el EQ del billar le quitan grave (a un reggaeton le sobra 1,8 dB de margen bajo el techo) y la presencia los
// cambia segun la cancion. Resultado: canciones que podrian sonar como las demas se quedan mas bajas.
//
// Aqui se aprende, cancion por cancion, lo que de verdad hace la cadena del billar con ella:
//  - MIDE (MideMargen, pegada delante del limitador, solo en el billar): el pico de cada bloque de 100 ms de lo que le llega al limitador,
//    en un histograma de 0,1 dB (sin reservar nada al sonar). No toca ni una muestra.
//  - APRENDE al acabar la cancion, solo si sono ENTERA desde el principio (llego al final del archivo) y sin «Escuchar sin mejoras»: de
//    sus picos por bloque saca el P90, el P99 y el mayor (frente al techo del limitador) y cuanto podria subir o deberia bajar para que el
//    P90 quede a 0,75 dB, el P99 a 3 dB y el mayor a 5 dB por encima del techo (lo que el limitador quita sin oirse; la cadena de fabrica
//    deja las canciones bien masterizadas en +0,7 y +1,0). Se acerca el 70 % a eso cada vez (unas pocas vueltas y queda), sin pasar de
//    +-2 dB, y lo que cambiaria menos de 0,25 dB se deja.
//  - SE USA la siguiente vez que suena en el billar (GananciaDe). En racha con otra de su disco, el MENOR de las pistas del disco (una
//    sin aprender cuenta como 0): el disco sigue sonando parejo entre si.
// Se guarda en rocola_billar_nivel.txt (una linea "dB|ruta" por cada cosa aprendida; al leer, la ultima manda). Con el modo billar apagado
// no se mide ni se usa: la cadena sale bit a bit como antes.
//
// DESDE LA PRIMERA VEZ (cuarta tanda, 27-10-2026). Una cancion que nunca ha sonado entera (nueva, o que siempre se salta) no tiene nada
// aprendido. Con al menos ParejoMinModelo canciones aprendidas, se ajusta una recta (minimos cuadrados con un poco de freno, ParejoLambda)
// de lo aprendido frente a tres rasgos de cada cancion que ya mide el repaso: cuanto grave profundo (50 Hz) y grave (158 Hz) trae frente a la
// biblioteca (su huella del EQ) y su rango de sonoridad (LRA). Si la recta explica algo (R2 >= ParejoMinR2), a una cancion sin aprender se
// le aplica LA MITAD de lo que predice, como mucho +-1 dB; al sonar entera, aprende lo suyo como siempre. Se rehace al tener 5 aprendidas
// mas que la ultima vez, como mucho cada 10 minutos.
using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        internal const float ParejoP90Db = 0.75f, ParejoP99Db = 3f, ParejoMaxDb = 5f;
        internal const float ParejoTopeDb = 2f, ParejoPaso = 0.7f, ParejoZonaMuerta = 0.25f;
        internal const int ParejoMinBloques = 300;   // al menos 30 s de musica que no sea silencio

        private static readonly object parejoCerrojo = new object();
        private static Dictionary<string, float> parejoDb = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private string ParejoFile() { return RutaDatos("rocola_billar_nivel.txt"); }

        // Lo aprendido de esta cancion (dB), 0 si nada.
        internal static float ParejoDe(string path)
        {
            if (path == null) return 0f;
            float v;
            lock (parejoCerrojo) return parejoDb.TryGetValue(path, out v) ? v : 0f;
        }
        // Lo que se le aplica al sonar (dB): en el billar lo suyo (o lo estimado), o el menor de su disco si suena en racha con otra de el.
        private float ParejoAplicado(Track t)
        {
            if (!localAuto || t == null || t.Path == null) return 0f;
            List<Track> disco = queue != null ? RachaDeDisco(t) : null;
            if (disco == null) return ParejoEstimado(t.Path);
            float m = float.MaxValue;
            for (int i = 0; i < disco.Count; i++) { Track tr = disco[i]; if (tr != null) m = Math.Min(m, ParejoEstimado(tr.Path)); }
            return m == float.MaxValue ? 0f : m;
        }

        // ---- DESDE LA PRIMERA VEZ: la recta de lo aprendido frente a los rasgos de la cancion ----
        internal const int ParejoMinModelo = 30, ParejoRehacerCada = 5;
        internal const double ParejoLambda = 1.0, ParejoMinR2 = 0.3, ParejoModeloFraccion = 0.5, ParejoModeloTopeDb = 1.0;
        private double[] parejoModelo;          // w0 + w1 x1 + w2 x2 + w3 x3 (null: sin recta util)
        private double parejoLraMedia;          // el LRA medio de las aprendidas (para la que no lo tiene medido)
        private int parejoModeloN = -1000;      // cuantas aprendidas habia al hacerla
        private DateTime parejoModeloCuando = DateTime.MinValue;

        // Lo suyo si ya lo aprendio; si no, la mitad de lo que predice la recta (+-1 dB), o 0.
        internal float ParejoEstimado(string path)
        {
            if (path == null) return 0f;
            float v;
            lock (parejoCerrojo) if (parejoDb.TryGetValue(path, out v)) return v;
            RehaceParejoSiToca();
            double[] w = parejoModelo; double x1, x2, x3;
            if (w == null || !ParejoRasgos(path, out x1, out x2, out x3)) return 0f;
            if (double.IsNaN(x3)) x3 = parejoLraMedia;
            double pred = ParejoModeloFraccion * (w[0] + w[1] * x1 + w[2] * x2 + w[3] * x3);
            if (double.IsNaN(pred)) return 0f;
            return (float)Math.Max(-ParejoModeloTopeDb, Math.Min(ParejoModeloTopeDb, pred));
        }
        // Los rasgos: grave profundo y grave frente a la biblioteca (dB, de la huella del EQ) y el LRA (NaN si no esta medido).
        private bool ParejoRasgos(string path, out double x1, out double x2, out double x3)
        {
            x1 = x2 = x3 = double.NaN;
            float[] c, avg = eqAvg;
            if (path == null || avg == null || eqCache == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen
                || c[EqFingerprint] != EqVersionOriginal || c[0] <= -25f || c[1] <= -25f) return false;
            x1 = c[0] - avg[0]; x2 = c[1] - avg[1];
            float lra = LraDeCancion(path);
            if (lra >= 0f) x3 = lra;
            return true;
        }
        private void RehaceParejoSiToca()
        {
            int n; lock (parejoCerrojo) n = parejoDb.Count;
            if (n < ParejoMinModelo || n - parejoModeloN < ParejoRehacerCada || (DateTime.Now - parejoModeloCuando).TotalMinutes < 10.0) return;
            parejoModeloN = n; parejoModeloCuando = DateTime.Now;
            List<KeyValuePair<string, float>> todas;
            lock (parejoCerrojo) todas = new List<KeyValuePair<string, float>>(parejoDb);
            List<double[]> X = new List<double[]>(); List<double> y = new List<double>(); double sumaLra = 0; int nLra = 0;
            foreach (KeyValuePair<string, float> kv in todas)
            {
                double x1, x2, x3;
                if (!ParejoRasgos(kv.Key, out x1, out x2, out x3)) continue;
                X.Add(new double[] { x1, x2, x3 }); y.Add(kv.Value);
                if (!double.IsNaN(x3)) { sumaLra += x3; nLra++; }
            }
            double media = nLra > 0 ? sumaLra / nLra : 0.0;
            foreach (double[] f in X) if (double.IsNaN(f[2])) f[2] = media;
            double r2 = 0.0; double[] w = null;
            if (X.Count >= ParejoMinModelo) w = AjusteLineal(X, y, ParejoLambda, out r2);
            parejoLraMedia = media;
            parejoModelo = (w != null && r2 >= ParejoMinR2) ? w : null;
        }
        // Minimos cuadrados con freno (ridge, sin frenar la ordenada): devuelve { w0, w1.. } y el R2 de lo ajustado. Pura.
        internal static double[] AjusteLineal(List<double[]> X, List<double> y, double lambda, out double r2)
        {
            r2 = 0.0;
            int n = X.Count; if (n == 0 || y.Count != n) return null;
            int k = X[0].Length + 1;
            double[,] A = new double[k, k + 1];
            for (int i = 0; i < n; i++)
            {
                double[] f = new double[k]; f[0] = 1.0; for (int j = 1; j < k; j++) f[j] = X[i][j - 1];
                for (int a = 0; a < k; a++) { for (int b = 0; b < k; b++) A[a, b] += f[a] * f[b]; A[a, k] += f[a] * y[i]; }
            }
            for (int a = 1; a < k; a++) A[a, a] += lambda;
            for (int col = 0; col < k; col++)
            {
                int piv = col; for (int r = col + 1; r < k; r++) if (Math.Abs(A[r, col]) > Math.Abs(A[piv, col])) piv = r;
                if (Math.Abs(A[piv, col]) < 1e-12) return null;
                if (piv != col) for (int c = 0; c <= k; c++) { double t = A[col, c]; A[col, c] = A[piv, c]; A[piv, c] = t; }
                for (int r = 0; r < k; r++)
                {
                    if (r == col) continue;
                    double m = A[r, col] / A[col, col];
                    for (int c = col; c <= k; c++) A[r, c] -= m * A[col, c];
                }
            }
            double[] w = new double[k]; for (int a = 0; a < k; a++) w[a] = A[a, k] / A[a, a];
            double my = 0; for (int i = 0; i < n; i++) my += y[i]; my /= n;
            double sr = 0, st = 0;
            for (int i = 0; i < n; i++)
            {
                double p = w[0]; for (int j = 1; j < k; j++) p += w[j] * X[i][j - 1];
                sr += (y[i] - p) * (y[i] - p); st += (y[i] - my) * (y[i] - my);
            }
            r2 = st > 1e-12 ? 1.0 - sr / st : 0.0;
            return w;
        }

        // Cuanto deberia cambiar (dB) segun los picos por bloque medidos (P90, P99 y el mayor, en dB frente al techo). Pura.
        internal static float ParejoFalta(float p90, float p99, float max)
        {
            return Math.Min(ParejoP90Db - p90, Math.Min(ParejoP99Db - p99, ParejoMaxDb - max));
        }
        // Lo nuevo que se apunta: 'aplicado' es lo que llevaba al sonar, 'propio' lo que ya tenia apuntado (o NaN). NaN = no apuntar. Pura.
        internal static float ParejoNuevo(float propio, float aplicado, float falta)
        {
            if (float.IsNaN(falta) || float.IsInfinity(falta)) return float.NaN;
            float ideal = aplicado + falta;
            if (ideal > ParejoTopeDb) ideal = ParejoTopeDb; else if (ideal < -ParejoTopeDb) ideal = -ParejoTopeDb;
            float antes = float.IsNaN(propio) ? 0f : propio;
            float nuevo = antes + ParejoPaso * (ideal - antes);
            if (Math.Abs(nuevo - antes) < ParejoZonaMuerta && !float.IsNaN(propio)) return float.NaN;
            return (float)Math.Round(nuevo, 2);
        }

        // Al soltar una cadena (acabo, se salto o se paro): si sono entera en el billar, se aprende. En el hilo de la pantalla.
        private void AprendeParejo(CadenaCancion c)
        {
            if (c == null || c.Margen == null || c.Cancion == null || c.Cancion.Path == null) return;
            MideMargen m = c.Margen; c.Margen = null;
            float p90, p99, max;
            bool hay = m.Percentiles(out p90, out p99, out max);
            float nuevo = float.NaN;
            try
            {
                if (!m.Entera || m.Descarta || !localAuto || !hay) return;
                string path = c.Cancion.Path;
                float propio; bool habia;
                lock (parejoCerrojo) habia = parejoDb.TryGetValue(path, out propio);
                nuevo = ParejoNuevo(habia ? propio : float.NaN, m.Aplicado, ParejoFalta(p90, p99, max));
                if (float.IsNaN(nuevo)) return;
                GuardaParejo(path, nuevo);
            }
            finally { if (hay) ApuntaBitacoraBillar(c, m, p90, p99, max, nuevo); }   // la bitacora, aprenda o no (BitacoraBillar)
        }
        private void GuardaParejo(string path, float nuevo)
        {
            lock (parejoCerrojo) parejoDb[path] = nuevo;
            string linea = nuevo.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + path + "\r\n";
            string archivo = ParejoFile();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { lock (parejoCerrojo) File.AppendAllText(archivo, linea); }
                catch (Exception ex) { LogError("Volumen parejo", ex); }
            });
        }

        // Al arrancar: lo apuntado (la ultima linea de cada ruta manda). Si el archivo crecio mucho con lineas repetidas, se reescribe.
        private void CargaParejo()
        {
            string archivo = ParejoFile();
            Dictionary<string, float> d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            int lineas = 0;
            try
            {
                if (!File.Exists(archivo)) return;
                foreach (string s in File.ReadAllLines(archivo))
                {
                    int bar = s.IndexOf('|'); float v;
                    if (bar <= 0 || bar >= s.Length - 1) continue;
                    if (!float.TryParse(s.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                    if (float.IsNaN(v) || v > ParejoTopeDb || v < -ParejoTopeDb) continue;
                    d[s.Substring(bar + 1)] = v; lineas++;
                }
                lock (parejoCerrojo) parejoDb = d;
                if (lineas > 2 * d.Count + 200)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (KeyValuePair<string, float> kv in d) sb.Append(kv.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(kv.Key).Append("\r\n");
                    lock (parejoCerrojo) { File.WriteAllText(archivo + ".nuevo", sb.ToString()); File.Copy(archivo + ".nuevo", archivo, true); File.Delete(archivo + ".nuevo"); }
                }
            }
            catch (Exception ex) { LogError("Volumen parejo", ex); }
        }

        // EL OIDO: delante del limitador, el pico de cada bloque de 100 ms en un histograma de 0,1 dB frente al techo (de -40 a +20 dB). Pasa
        // tal cual. Entera = leyo hasta el final del archivo; Descarta = alguien puso «Escuchar sin mejoras» mientras sonaba.
        internal sealed class MideMargen : ISampleProvider
        {
            internal const float DesdeDb = -40f, PasoDb = 0.1f; internal const int Casillas = 600;
            private readonly ISampleProvider src;
            private readonly int[] hist = new int[Casillas];
            private readonly int bloque;   // muestras por bloque (todos los canales)
            private readonly double techoDb;
            private int enBloque; private float pico;
            private volatile bool entera;
            internal volatile bool Descarta;
            internal readonly bool DesdeElPrincipio;
            internal readonly float Aplicado;   // lo aprendido que llevaba al sonar (dB)
            internal int Bloques;
            private readonly long esperadas;    // muestras que trae la cancion entera (0 = no se sabe: hace falta llegar al final)
            private long leidas;
            internal MideMargen(ISampleProvider s, float techo, bool desdeElPrincipio, float aplicadoDb, double segundos)
            {
                src = s; DesdeElPrincipio = desdeElPrincipio; Aplicado = aplicadoDb;
                esperadas = segundos > 1.0 ? (long)(segundos * s.WaveFormat.SampleRate * s.WaveFormat.Channels) : 0L;
                bloque = Math.Max(1, s.WaveFormat.SampleRate / 10) * Math.Max(1, s.WaveFormat.Channels);
                techoDb = 20.0 * Math.Log10(techo);
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            // Entera: desde el principio y hasta el final del archivo, o al menos el 90 % de lo que dura (el fundido de la siguiente y saltar
            // los silencios largos pueden dejar sin leer el final).
            internal bool Entera { get { return DesdeElPrincipio && (entera || (esperadas > 0 && leidas >= esperadas * 9 / 10)); } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) { if (count > 0) entera = true; return n; }
                leidas += n;
                float p = pico; int k = enBloque;
                for (int i = 0; i < n; i++)
                {
                    float a = buffer[offset + i]; if (a < 0f) a = -a;
                    if (a > p) p = a;
                    if (++k >= bloque) { Apunta(p); p = 0f; k = 0; }
                }
                pico = p; enBloque = k;
                return n;
            }
            private void Apunta(float p)
            {
                if (!(p > 0f)) return;
                double db = 20.0 * Math.Log10(p) - techoDb;
                if (db < DesdeDb) return;   // silencio: no cuenta
                int c = (int)((db - DesdeDb) / PasoDb); if (c >= Casillas) c = Casillas - 1;
                hist[c]++; Bloques++;
            }
            // La fraccion (0 a 1) de los bloques cuyo pico paso 'db' por encima del techo: lo que el limitador tuvo que bajar mas que eso.
            internal float FraccionSobre(float db)
            {
                int total = Bloques; if (total <= 0) return 0f;
                int desde = (int)Math.Ceiling((db - DesdeDb) / PasoDb), n = 0;
                if (desde < 0) desde = 0;
                for (int c = desde; c < Casillas; c++) n += hist[c];
                return n / (float)total;
            }
            internal bool Percentiles(out float p90, out float p99, out float max)
            {
                p90 = p99 = max = float.NaN;
                int total = Bloques;
                if (total < ParejoMinBloques) return false;
                int a90 = (int)Math.Ceiling(total * 0.9), a99 = (int)Math.Ceiling(total * 0.99), suma = 0;
                for (int c = 0; c < Casillas; c++)
                {
                    if (hist[c] == 0) continue;
                    suma += hist[c];
                    float db = DesdeDb + (c + 0.5f) * PasoDb;
                    if (float.IsNaN(p90) && suma >= a90) p90 = db;
                    if (float.IsNaN(p99) && suma >= a99) p99 = db;
                    max = db;
                }
                return !float.IsNaN(p90) && !float.IsNaN(p99);
            }
        }
    }
}

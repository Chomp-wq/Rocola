// LA IA: la red propia y el puente con el motor de Apollo
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
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
        private static readonly float[] IaCutsHz = { 11000f, 13000f, 15000f, 16500f, 18000f };   // cortes simulados
        private string IaDataFile() { return RutaDatos("rocola_ia_datos.bin"); }
        private string IaDoneFile() { return RutaDatos("rocola_ia_hechas.txt"); }
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
        // Con el ORIGEN de esos pares (ver IaApuntaOrigen). Sin origen -como la llaman las pruebas de los
        // carriles- se guarda igual y esos ejemplos quedan "sin origen": nunca se inventa de donde vienen.
        private bool IaAppendPairs(List<float> pairs) { return IaAppendPairsOrigen(pairs, null); }
        private bool IaAppendPairsOrigen(List<float> pairs, IaOrigen origen)
        {
            if (pairs == null || pairs.Count < 40 || (pairs.Count % 40) != 0) return false;
            lock (iaFileLock)
            {
                long primero; bool nuevo; byte[] raw;
                try
                {
                    string f = IaDataFile();
                    using (FileStream fs = new FileStream(f, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                    {
                        nuevo = fs.Length == 0;
                        if (nuevo)
                        {
                            byte[] head = new byte[16];
                            head[0] = (byte)'R'; head[1] = (byte)'I'; head[2] = (byte)'A'; head[3] = (byte)'D';
                            Buffer.BlockCopy(BitConverter.GetBytes(1), 0, head, 4, 4);
                            Buffer.BlockCopy(BitConverter.GetBytes(28), 0, head, 8, 4);
                            Buffer.BlockCopy(BitConverter.GetBytes(12), 0, head, 12, 4);
                            fs.Write(head, 0, head.Length);
                        }
                        else if (!IaHeaderValida(fs)) return false;   // no anexar encima de un archivo ajeno/corrupto
                        long completos = Math.Max(0, (fs.Length - 16) / 160);
                        long valido = 16 + completos * 160;
                        if (fs.Length != valido) fs.SetLength(valido);   // recupera una ultima escritura interrumpida
                        fs.Position = valido;
                        // Los mismos bytes que antes (float32 little-endian, uno tras otro), de una sola vez:
                        // hacen falta enteros para la huella CRC32 del indice de origen.
                        float[] valores = pairs.ToArray();
                        raw = new byte[valores.Length * 4];
                        Buffer.BlockCopy(valores, 0, raw, 0, raw.Length);
                        fs.Write(raw, 0, raw.Length);
                        fs.Flush();
                        try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                        primero = completos;
                    }
                    iaPairsTotal += pairs.Count / 40;
                }
                catch { return false; }
                if (origen != null) IaApuntaOrigen(primero, pairs.Count / 40, IaCrc32(raw, 0, raw.Length), origen, nuevo);
                return true;
            }
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
        // arma la rocola. El artista se deja en 0: la lista de canciones vive en el hilo de la pantalla y
        // leerla desde este hilo no es seguro; mejor "no se sabe" que un dato a medias.
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
        }
        private static IaOrigen IaOrigenDe(string path, double durSec, int rate, int frame, int canal)
        {
            IaOrigen o = new IaOrigen();
            o.Rate = rate; o.Frame = frame; o.Canal = canal;
            o.Artista = 0;   // no se sabe (ver arriba): 0 es "desconocido", nunca se inventa
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
        private static ulong IaHuella(string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s.ToLowerInvariant());
            ulong h = 14695981039346656037UL;
            unchecked { for (int i = 0; i < b.Length; i++) { h ^= b[i]; h *= 1099511628211UL; } }
            return h == 0 ? 1UL : h;
        }
        private static uint[] iaCrcTabla;
        private static uint IaCrc32(byte[] d, int off, int n)
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
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < n; i++) crc = t[(crc ^ d[off + i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
        private static void IaPon(byte[] e, int off, long v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 8); }
        private static void IaPon(byte[] e, int off, int v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 4); }
        private static void IaPon(byte[] e, int off, uint v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 4); }
        private static void IaPon(byte[] e, int off, ulong v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, e, off, 8); }
        // Solo se llama con iaFileLock tomado (desde IaAppendPairsOrigen).
        private void IaApuntaOrigen(long primero, int cuenta, uint crcDatos, IaOrigen o, bool datosNuevos)
        {
            try
            {
                string idx = IaIndexFile();
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
                    IaPon(e, 0, primero); IaPon(e, 8, cuenta); IaPon(e, 12, IaExtractorVersion);
                    IaPon(e, 16, o.Rate); IaPon(e, 20, o.Frame); IaPon(e, 24, o.Canal); IaPon(e, 28, crcDatos);
                    IaPon(e, 32, o.Cancion); IaPon(e, 40, o.Carpeta); IaPon(e, 48, o.Artista); IaPon(e, 56, 0);
                    IaPon(e, 60, IaCrc32(e, 0, 60));
                    fs.Write(e, 0, 64);
                    fs.Flush();
                    try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                }
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
        private static readonly RedChica NeuralEnv = new RedChica(28, 12);
        private static readonly RedChica EqNet = new RedChica(EqNetIn, EqNetOut);
        private const int EqNetIn = 5, EqNetOut = 3;
        private static readonly int[] EqNetSalida = { 0, 6, 7 };   // que bandas predice: 50 Hz, 10 kHz y 15.8 kHz

        private sealed class RedChica
        {
            private readonly object modeloLock = new object();
            public volatile bool Loaded;
            private float[][] W, B;            // pesos (fila-mayor) y sesgos por capa
            private int[] rOut, cIn;           // dimensiones por capa
            private float[] buf1, buf2;        // buffers de trabajo (sin asignar por ventana)
            private readonly int nIn, nOut;    // contrato: cuantas entradas y cuantas salidas debe tener el modelo
            public RedChica(int entradas, int salidas) { nIn = entradas; nOut = salidas; }

            public void TryLoad(string path)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    byte[] d;
                    // Ocho capas de 512x512 caben en menos de 9 MiB. No cargar
                    // archivos arbitrariamente grandes antes de validar el contrato.
                    using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (file.Length < 12 || file.Length > 9 * 1024 * 1024) return;
                        using (BinaryReader input = new BinaryReader(file))
                        {
                            d = input.ReadBytes((int)file.Length);
                            if (d.Length != file.Length) return;
                        }
                    }
                    if (d.Length < 12 || d[0] != (byte)'R' || d[1] != (byte)'I' || d[2] != (byte)'A' || d[3] != (byte)'1') return;
                    int p = 4;
                    int nl = BitConverter.ToInt32(d, p); p += 4;
                    if (nl < 1 || nl > 8) return;
                    float[][] w = new float[nl][], b = new float[nl][];
                    int[] ro = new int[nl], ci = new int[nl]; int maxDim = 0;
                    for (int L = 0; L < nl; L++)
                    {
                        if (d.Length - p < 8) return;
                        int r = BitConverter.ToInt32(d, p); p += 4;
                        int c = BitConverter.ToInt32(d, p); p += 4;
                        if (r < 1 || r > 512 || c < 1 || c > 512) return;
                        if (d.Length - p < (r * c + r) * 4) return;
                        w[L] = new float[r * c]; b[L] = new float[r];
                        for (int i = 0; i < r * c; i++)
                        { float v = BitConverter.ToSingle(d, p); p += 4; if (float.IsNaN(v) || float.IsInfinity(v)) return; w[L][i] = v; }
                        for (int i = 0; i < r; i++)
                        { float v = BitConverter.ToSingle(d, p); p += 4; if (float.IsNaN(v) || float.IsInfinity(v)) return; b[L][i] = v; }
                        ro[L] = r; ci[L] = c; if (r > maxDim) maxDim = r; if (c > maxDim) maxDim = c;
                    }
                    if (p != d.Length || ci[0] != nIn || ro[nl - 1] != nOut) return;   // contrato exacto, sin bytes sobrantes
                    for (int L = 1; L < nl; L++) if (ci[L] != ro[L - 1]) return;
                    float[] work1 = new float[maxDim], work2 = new float[maxDim];
                    lock (modeloLock)
                    {
                        buf1 = work1; buf2 = work2;
                        W = w; B = b; rOut = ro; cIn = ci;
                        Loaded = true;
                    }
                }
                catch { }   // una recarga invalida no destruye el ultimo modelo valido
            }

            // y = red(x). Capas ocultas con tanh; la de salida lineal. Devuelve false si no hay modelo.
            public bool Predict(float[] x, float[] y)
            {
                lock (modeloLock)
                {
                    float[][] w = W, b = B;
                    if (!Loaded || w == null || x == null || y == null || x.Length < nIn || y.Length < nOut) return false;
                    for (int i = 0; i < nIn; i++) if (float.IsNaN(x[i]) || float.IsInfinity(x[i])) return false;
                    float[] cur = x, nxt = buf1;
                    int curN = cIn[0];
                    for (int L = 0; L < w.Length; L++)
                    {
                        int r = rOut[L]; float[] wl = w[L], bl = b[L];
                        float[] dst = nxt;   // no publicar una salida parcial si falla otra neurona
                        for (int i = 0; i < r; i++)
                        {
                            float s = bl[i]; int off = i * curN;
                            for (int j = 0; j < curN; j++) s += wl[off + j] * cur[j];
                            if (float.IsNaN(s) || float.IsInfinity(s)) return false;
                            dst[i] = (L == w.Length - 1) ? s : (float)Math.Tanh(s);
                        }
                        cur = dst; curN = r;
                        nxt = (cur == buf1) ? buf2 : buf1;   // alterna buffers (la entrada x no se toca)
                    }
                    for (int i = 0; i < nOut; i++) y[i] = cur[i];
                    return true;
                }
            }
        }
    }
}

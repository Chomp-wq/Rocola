// EL LECTOR DE CANCIONES DE ROCOLA OS (hito 3). En XP los MP3 los descomprime el ACM de Windows (y NLayer solo en las que llegan al
// maximo: FlotanteNLayer.cs); en Linux no hay ACM: los MP3 van por libmpg123 (Mpg123.cs; NLayer 3 se come un cuadro en algunos) y,
// si no esta, por NLayer 3 (los de frecuencias mezcladas, reconstruidos, tambien por libmpg123); los dos en coma flotante, sin recortar lo que pasa del maximo. Los WAV, con el lector de NAudio. Como en XP desde el 24-10-2026, un archivo mono sale como estereo de dos canales iguales.
// Los MP3 de frecuencias mezcladas se reconstruyen a la dominante, como en XP (Mp3DeUnaFrecuencia).
using System;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public sealed class LectorCancion : ISampleProvider, IDisposable
    {
        private readonly NLayer.MpegFile mp3;
        private readonly Mpg123Lector rapido;
        private readonly WaveStream wav;
        private readonly AudioDeVideo video;          // el sonido de un video, por ffmpeg (solo para medirlo: AudioDeVideo.cs)
        private readonly ISampleProvider muestras;   // las del WAV, ya en coma flotante
        private readonly int canalesArchivo;
        private readonly WaveFormat formato;          // estereo (o los canales del archivo, para medir)
        private readonly bool comoEstereo;
        private float[] mono = new float[0];

        public LectorCancion(string ruta) : this(ruta, true) { }
        // LOS «MP3» QUE NO LO SON (4-11-2026): un M4A, WMA, OGG o FLAC al que le cambiaron el nombre a .mp3. mpg123 busca cuadros MPEG en
        // cualquier cosa y en un M4A encontraba unos falsos: un golpe de ruido casi al maximo. Se mira la cabecera (despues de la etiqueta
        // ID3 si la hay): un WAV renombrado se abre como WAV; los demas no se tocan (el aviso dice que son, y se devuelve el credito).
        // null = parece MP3 (o no se sabe): como siempre.
        internal static string OtroFormato(string ruta)
        {
            try
            {
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] h = new byte[16];
                    if (fs.Read(h, 0, 10) < 10) return null;
                    long o = 0;
                    if (h[0] == 'I' && h[1] == 'D' && h[2] == '3')
                    {
                        o = 10 + ((h[6] & 0x7F) << 21 | (h[7] & 0x7F) << 14 | (h[8] & 0x7F) << 7 | (h[9] & 0x7F)) + ((h[5] & 0x10) != 0 ? 10 : 0);
                        if (o >= fs.Length) return null;
                    }
                    fs.Position = o;
                    if (fs.Read(h, 0, 16) < 12) return null;
                    if (h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p') return "M4A/AAC";
                    if (h[0] == 'O' && h[1] == 'g' && h[2] == 'g' && h[3] == 'S') return "OGG";
                    if (h[0] == 'f' && h[1] == 'L' && h[2] == 'a' && h[3] == 'C') return "FLAC";
                    if (h[0] == 0x30 && h[1] == 0x26 && h[2] == 0xB2 && h[3] == 0x75 && h[4] == 0x8E && h[5] == 0x66) return "WMA";
                    if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'A' && h[10] == 'V' && h[11] == 'E')
                        return o == 0 && WavSinComprimir(fs) ? "WAV" : null;   // un MP3 dentro de un WAV (formato 0x55): para mpg123, como siempre
                    return null;
                }
            }
            catch { return null; }
        }
        // El formato del primer «fmt » de un RIFF/WAVE (fs justo despues de «WAVE»): PCM, coma flotante o extensible.
        private static bool WavSinComprimir(FileStream fs)
        {
            byte[] c = new byte[8];
            fs.Position = 12;
            for (int i = 0; i < 16 && fs.Read(c, 0, 8) == 8; i++)
            {
                uint largo = BitConverter.ToUInt32(c, 4);
                if (c[0] == 'f' && c[1] == 'm' && c[2] == 't' && c[3] == ' ')
                {
                    if (fs.Read(c, 0, 2) < 2) return false;
                    int formato = BitConverter.ToUInt16(c, 0);
                    return formato == 1 || formato == 3 || formato == 0xFFFE;
                }
                fs.Position += largo + (largo & 1);
            }
            return false;
        }
        // comoEstereo = false: con los canales del archivo (la medida del repaso mide un mono como mono, como en XP: LectorMedida).
        public LectorCancion(string ruta, bool comoEstereo)
        {
            this.comoEstereo = comoEstereo;
            string otro = Path.GetExtension(ruta).Equals(".mp3", StringComparison.OrdinalIgnoreCase) ? OtroFormato(ruta) : null;
            if (otro != null && otro != "WAV")
            {
                // su copia en WAV (Conversiones.cs); si aun no esta, se pide y esta vez no se abre
                string copia = Conversiones.Lista(ruta);
                if (copia == null)
                {
                    string fallo = Conversiones.Fallo(ruta);
                    if (fallo == null) Conversiones.Pide(ruta);
                    throw new NoEsMp3Exception("no es un MP3: es " + otro + " con otro nombre" + (fallo != null ? " y no se pudo convertir (" + fallo + ")" : "; se esta convirtiendo"), fallo == null);
                }
                ruta = copia; otro = "WAV";
            }
            if (otro == null && Path.GetExtension(ruta).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                MemoryStream limpio = Mp3DeUnaFrecuencia(ruta);
                if (limpio == null) rapido = Mpg123Lector.Abre(ruta);
                else { rapido = DeMemoria(limpio); if (rapido != null) Reconstruido = true; }
                if (rapido != null)
                {
                    canalesArchivo = rapido.WaveFormat.Channels;
                    formato = WaveFormat.CreateIeeeFloatWaveFormat(rapido.WaveFormat.SampleRate, comoEstereo ? 2 : canalesArchivo);
                    return;
                }
                if (limpio != null) { Reconstruido = true; mp3 = new NLayer.MpegFile(limpio); }
                else mp3 = new NLayer.MpegFile(ruta);
                canalesArchivo = mp3.Channels;
                formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, comoEstereo ? 2 : canalesArchivo);
            }
            else if (!comoEstereo && EsDeVideo(ruta))
            {
                video = AudioDeVideo.Abre(ruta);
                if (video == null) throw new NotSupportedException("sin el sonido del video (ffmpeg)");
                canalesArchivo = AudioDeVideo.Canales;
                formato = WaveFormat.CreateIeeeFloatWaveFormat(AudioDeVideo.Tasa, AudioDeVideo.Canales);
            }
            else
            {
                WaveFileReader w = new WaveFileReader(ruta);
                wav = w; muestras = w.ToSampleProvider();
                canalesArchivo = w.WaveFormat.Channels;
                if (canalesArchivo > 2) { w.Dispose(); throw new NotSupportedException("mas de dos canales"); }
                formato = WaveFormat.CreateIeeeFloatWaveFormat(w.WaveFormat.SampleRate, comoEstereo ? 2 : canalesArchivo);
            }
        }

        // LOS RECONSTRUIDOS TAMBIEN POR MPG123 (3-11-2026): un MP3 de frecuencias mezcladas, ya reconstruido en memoria, iba por NLayer,
        // que en algunos archivos se come un cuadro entero (26 ms, con chasquido): justo lo que se quito al pasar a mpg123. Ahora se
        // escribe en /tmp (en la RAM), mpg123 lo abre y se borra al momento (Linux lo deja leer mientras siga abierto: no queda nada,
        // aunque la rocola se caiga). Si no se puede, NLayer como antes.
        private static int temporales;
        private static Mpg123Lector DeMemoria(MemoryStream limpio)
        {
            string t = null;
            try
            {
                t = Path.Combine(Path.GetTempPath(), "rocola-reconstruido-" + Environment.ProcessId + "-" + System.Threading.Interlocked.Increment(ref temporales) + ".mp3");
                using (FileStream f = new FileStream(t, FileMode.CreateNew, FileAccess.Write)) limpio.WriteTo(f);
                return Mpg123Lector.Abre(t);
            }
            catch { return null; }
            finally { if (t != null) try { File.Delete(t); } catch { } limpio.Position = 0; }
        }

        // Un MP3 que ya esta en memoria (el clip con el que se mide el relleno, RellenoMp3.cs).
        public LectorCancion(Stream mp3EnMemoria)
        {
            comoEstereo = true;
            mp3 = new NLayer.MpegFile(mp3EnMemoria);
            canalesArchivo = mp3.Channels;
            formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, 2);
        }

        // true si era un MP3 de frecuencias (o canales) mezcladas y se leyo reconstruido (sin el relleno del codificador, como en XP).
        public bool Reconstruido { get; private set; }

        // LOS MP3 DE FRECUENCIAS MEZCLADAS (en XP, BuildCleanMp3 de Form1.Sonido.cs): unos pocos archivos traen cuadros de otra
        // frecuencia (o mono entre estereo). En XP el ACM falla con ellos y se reconstruyen; NLayer no falla, pero toca esos cuadros a
        // la frecuencia del primero (desafinados y mas largos). Aqui se mira SIEMPRE: si hay mas de una clave (frecuencia y canales),
        // se copian a memoria solo los cuadros de la dominante, como en XP (los de la minoria se descartan: a lo sumo un salto).
        // null = de una sola frecuencia (lo normal): se lee el archivo tal cual.
        internal static MemoryStream Mp3DeUnaFrecuencia(string ruta)
        {
            System.Collections.Generic.Dictionary<int, int> claves = new System.Collections.Generic.Dictionary<int, int>();
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs, false); } catch { break; }
                    if (f == null) break;
                    int k = Clave(f); int c; claves.TryGetValue(k, out c); claves[k] = c + 1;
                }
            if (claves.Count < 2) return null;
            int dom = 0, mejor = -1;
            foreach (System.Collections.Generic.KeyValuePair<int, int> kv in claves) if (kv.Value > mejor) { mejor = kv.Value; dom = kv.Key; }
            MemoryStream ms = new MemoryStream();
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs); } catch { break; }
                    if (f == null) break;
                    if (Clave(f) == dom && f.RawData != null) ms.Write(f.RawData, 0, f.RawData.Length);
                }
            if (ms.Length < 2048) { ms.Dispose(); return null; }   // casi nada util: que se lea tal cual
            ms.Position = 0;
            return ms;
        }
        private static int Clave(Mp3Frame f) { return f.SampleRate * 4 + (f.ChannelMode == ChannelMode.Mono ? 1 : 2); }

        public WaveFormat WaveFormat { get { return formato; } }
        public TimeSpan Duracion { get { return video != null ? video.Duracion : rapido != null ? rapido.Duracion : mp3 != null ? mp3.Duration : wav.TotalTime; } }
        public TimeSpan Posicion
        {
            get { return video != null ? video.Posicion : rapido != null ? rapido.Posicion : mp3 != null ? mp3.Time : wav.CurrentTime; }
            set
            {
                if (video != null) { if (Math.Abs((value - video.Posicion).TotalSeconds) > 0.05) throw new NotSupportedException("el sonido de un video solo se lee de corrido"); }
                else if (rapido != null) rapido.Posicion = value; else if (mp3 != null) mp3.Time = value; else wav.CurrentTime = value;
                huecos.Reinicia();
            }
        }
        private static readonly string[] ExtVideo = { ".mp4", ".avi", ".wmv", ".mpg", ".mpeg", ".mov", ".mkv", ".flv", ".m4v", ".vob", ".divx", ".3gp", ".asf" };   // las de XP (VideoExt)
        private static bool EsDeVideo(string ruta) { return Array.IndexOf(ExtVideo, Path.GetExtension(ruta).ToLowerInvariant()) >= 0; }

        public int Read(Span<float> destino)
        {
            if (!comoEstereo && canalesArchivo == 1) return LeeArchivo(destino);
            int pide = destino.Length & ~1;   // cuadros enteros
            if (pide == 0) return 0;
            if (canalesArchivo == 2) return LeeArchivo(destino.Slice(0, pide));
            // mono: se lee la mitad y cada muestra sale por los dos canales
            int n = pide / 2;
            if (mono.Length < n) mono = new float[n];
            int got = LeeArchivo(mono.AsSpan(0, n));
            for (int i = got - 1; i >= 0; i--) { float v = mono[i]; destino[2 * i] = v; destino[2 * i + 1] = v; }
            return got * 2;
        }
        private float[] apoyo = new float[0];
        private int LeeArchivo(Span<float> d)
        {
            if (rapido != null) { int r = rapido.Read(d); huecos.Procesa(d.Slice(0, Math.Max(0, r)), canalesArchivo); return r; }
            if (video != null) return video.Read(d);
            if (mp3 == null) return muestras.Read(d);
            if (apoyo.Length < d.Length) apoyo = new float[d.Length];
            int n = mp3.ReadSamples(apoyo, 0, d.Length);
            if (n > 0) { apoyo.AsSpan(0, n).CopyTo(d); huecos.Procesa(d.Slice(0, n), canalesArchivo); }
            return Math.Max(0, n);
        }
        private readonly HuecosMp3 huecos = new HuecosMp3();

        public void Dispose() { if (video != null) video.Dispose(); if (rapido != null) rapido.Dispose(); if (mp3 != null) mp3.Dispose(); if (wav != null) wav.Dispose(); }
    }
    // LOS HUECOS DE UN MP3 DANADO, SIN CHASQUIDO (4-11-2026). En una zona rota del archivo el descompresor da los cuadros que no puede
    // leer como silencio exacto: la cancion cortaba de golpe a cero (un salto de hasta toda la escala: un chasquido fuerte en las
    // bocinas) y volvia de golpe al reencontrar cuadros buenos. Aqui, solo cuando la musica cae EN SECO a cero exacto desde mas de
    // -26 dBFS (en un MP3 sano el silencio digital llega bajando, nunca asi), el hueco se rellena con la ultima muestra bajando a cero en
    // 3 ms, y lo que vuelve entra en 3 ms (si vuelve antes, las dos se cruzan). Los archivos sanos salen bit a bit.
    internal sealed class HuecosMp3
    {
        internal const int Largo = 132;           // 3 ms a 44,1 kHz
        private const float Brusco = 0.05f;       // -26 dBFS
        private float u0, u1;                     // la ultima muestra (canal 0 y 1)
        private bool enHueco, volvio;
        private float c0, c1;                     // lo que se apaga
        private int k, e;                         // cuadros de la caida y de la entrada
        internal long Huecos;                     // cuantos se taparon (para la prueba)
        internal void Reinicia() { enHueco = false; u0 = u1 = 0f; }
        internal void Procesa(Span<float> d, int ch)
        {
            if (ch < 1 || ch > 2) return;
            int cuadros = d.Length / ch;
            for (int f = 0; f < cuadros; f++)
            {
                int i = f * ch;
                float a = d[i], b = ch == 2 ? d[i + 1] : a;
                if (!enHueco)
                {
                    if (a == 0f && b == 0f && (Math.Abs(u0) > Brusco || Math.Abs(u1) > Brusco))
                    { enHueco = true; volvio = false; c0 = u0; c1 = u1; k = 0; e = 0; Huecos++; }
                    else { u0 = a; u1 = b; continue; }
                }
                float gc = k < Largo ? (float)(0.5 + 0.5 * Math.Cos(Math.PI * (k + 1) / Largo)) : 0f; k++;
                if (!volvio && (a != 0f || b != 0f)) volvio = true;
                float ge = 0f;
                if (volvio) { ge = (float)(0.5 - 0.5 * Math.Cos(Math.PI * (e + 1) / Largo)); e++; }
                float o0 = a * ge + c0 * gc, o1 = b * ge + c1 * gc;
                d[i] = o0; if (ch == 2) d[i + 1] = o1;
                u0 = o0; u1 = o1;
                if (volvio && e >= Largo && k >= Largo) { enHueco = false; u0 = a; u1 = b; }
            }
        }
    }
    // Un «MP3» que no lo es (LectorCancion.OtroFormato): Convirtiendose = su copia en WAV esta en marcha (vale la pena esperarla).
    internal sealed class NoEsMp3Exception : NotSupportedException
    {
        internal readonly bool Convirtiendose;
        internal NoEsMp3Exception(string m, bool convirtiendose) : base(m) { Convirtiendose = convirtiendose; }
    }
}

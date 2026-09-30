// WASAPI EXCLUSIVO (solo HD): la rocola toma el aparato de sonido para ella sola
//
// Decision del usuario del 23 de septiembre de 2026: no importa que la rocola tome el control exclusivo del audio,
// porque el equipo no se usa para otra cosa que para la musica. En HD la musica sale por WASAPI en modo EXCLUSIVO:
// Windows no la pasa por su mezclador -ni por sus "mejoras" de sonido, ni por su conversion de formato- y llega al
// aparato tal como la entrega la rocola. Mientras suena, ningun otro programa (ni los sonidos de Windows) puede
// usar ese aparato. Los videos no cambian: antes de cada video la salida de la musica se cierra y el reproductor
// de Windows Media suena como siempre.
//
// 1) EL FORMATO. En exclusivo no hay mezclador que convierta: hay que darle al aparato un formato que admita, y
//    exactamente ese. Se le pregunta en este orden:
//      - la FRECUENCIA a la que ya trabaja el aparato (la de su formato predeterminado en Windows). Asi no cambia
//        de frecuencia entre canciones ni al volver de un video: hay receptores que tardan en reengancharse y se
//        comerian el principio de la cancion. Si la cancion viene a otra frecuencia se remuestrea como hasta ahora
//        en compartido, con el limitador de pico real detras. Si el aparato no admite esa, la de la cancion y
//        luego las habituales. (Para que las canciones de 44,1 kHz no se remuestreen: poner en Windows el formato
//        predeterminado del aparato a 44100 Hz.)
//      - la PROFUNDIDAD: 24 bits (en 4 o en 3 bytes), 32 bits enteros o coma flotante; y solo si no admite
//        ninguna de esas en ninguna frecuencia, 16 bits con dither TPDF. El dither va una sola vez, aqui al final.
//        En 24 y 32 bits se redondea sin dither: el error queda 144 dB por debajo del maximo, bajo el ruido de
//        cualquier DAC.
//      - los CANALES: los de la cancion; una cancion mono sale duplicada en los dos.
//    Si el formato no le vale, NAudio mete por su cuenta un remuestreador (DMO) DETRAS del limitador. Por eso se le
//    da un formato exacto que el aparato ya dijo que admite, y si aun asi NAudio lo cambiara, no se usa.
//
// 2) EL VOLUMEN DE WINDOWS. En compartido lo aplica el mezclador de Windows. En exclusivo no hay mezclador: si el
//    aparato tiene volumen propio (casi todos los DAC USB y las tarjetas de la placa), Windows lo mueve en el
//    aparato y funciona igual que siempre. Si NO lo tiene (muchas salidas HDMI), la musica saldria a tope: entonces
//    la rocola aplica ella misma el volumen y el silencio de Windows, en rampa, mirandolo cada medio segundo.
//    Nunca se escribe el volumen de Windows: solo se lee.
//
// 3) SI NO SE PUEDE: otro programa usando el aparato, Windows con el modo exclusivo prohibido para ese aparato,
//    ningun formato admitido, o un fallo al arrancar. La cancion sigue por WASAPI COMPARTIDO como antes (y si
//    tampoco, por WaveOut), sin perder la cancion ni tocar la cola. Queda apuntado en rocola_error.txt una vez por
//    aparato y motivo. Lo que vale para toda la sesion (prohibido, sin formato, falla al arrancar, se quedo muda)
//    no se vuelve a intentar con ese aparato; si otro programa lo estaba usando, se vuelve a probar con la siguiente
//    cancion, y una cancion de mas de dos canales que no cabe va en compartido sin quitarle el exclusivo a las
//    demas. Un aparato desenchufado es un fallo del APARATO y lo atiende la espera de siempre (SalidaSonido.cs).
//
// 4) PARA VOLVER AL COMPARTIDO de antes: salida=wasapi en la configuracion. salida=exclusivo lo usa aunque la
//    maquina no sea HD; salida=waveout, la salida clasica.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    // WAVEFORMATEXTENSIBLE con los bits validos y el tipo de muestra que se piden: el de NAudio no deja pedir 24 bits
    // en 4 bytes ni 32 bits enteros. Misma disposicion en memoria que el de NAudio: Windows recibe estos 40 bytes.
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal sealed class FormatoExacto : WaveFormat
    {
        private short validos;
        private int mascara;
        private Guid tipo;

        internal static readonly Guid TipoPcm = new Guid("00000001-0000-0010-8000-00aa00389b71");
        internal static readonly Guid TipoFlotante = new Guid("00000003-0000-0010-8000-00aa00389b71");

        internal FormatoExacto(int hz, int canales, int contenedor, int bitsValidos, bool flotante)
            : this(hz, canales, contenedor, bitsValidos, flotante, canales == 1 ? 4 : (canales == 2 ? 3 : 0)) { }

        internal FormatoExacto(int hz, int canales, int contenedor, int bitsValidos, bool flotante, int mascaraAltavoces)
        {
            waveFormatTag = WaveFormatEncoding.Extensible;
            channels = (short)canales;
            sampleRate = hz;
            bitsPerSample = (short)contenedor;
            blockAlign = (short)(canales * (contenedor / 8));
            averageBytesPerSecond = hz * blockAlign;
            extraSize = 22;
            validos = (short)bitsValidos;
            mascara = mascaraAltavoces;
            tipo = flotante ? TipoFlotante : TipoPcm;
        }

        internal int BitsValidos { get { return validos; } }
        internal bool EsFlotante { get { return tipo == TipoFlotante; } }
        internal int Mascara { get { return mascara; } }

        // Como se apunta y se ensena: "24 bits en 4 bytes, 48000 Hz, 2 canales".
        internal string Descripcion
        {
            get
            {
                string p = EsFlotante ? "coma flotante de 32 bits"
                         : (validos == bitsPerSample ? validos + " bits" : validos + " bits en " + (bitsPerSample / 8) + " bytes");
                return p + ", " + sampleRate + " Hz, " + channels + " canales";
            }
        }
        public override string ToString() { return Descripcion; }
    }

    // Los tipos de muestra, del preferido al ultimo recurso.
    internal enum MuestraExclusiva { Pcm24en32, Pcm24, Pcm32, Flotante32, Pcm16 }

    internal static class Exclusivo
    {
        private static readonly int[] Habituales = { 48000, 44100, 96000, 88200, 192000, 176400 };
        private static readonly MuestraExclusiva[] Buenas = { MuestraExclusiva.Pcm24en32, MuestraExclusiva.Pcm24, MuestraExclusiva.Pcm32, MuestraExclusiva.Flotante32 };

        internal static FormatoExacto Formato(int hz, int canales, MuestraExclusiva m)
        { return Formato(hz, canales, m, canales == 1 ? 4 : (canales == 2 ? 3 : 0)); }

        internal static FormatoExacto Formato(int hz, int canales, MuestraExclusiva m, int mascara)
        {
            switch (m)
            {
                case MuestraExclusiva.Pcm24en32: return new FormatoExacto(hz, canales, 32, 24, false, mascara);
                case MuestraExclusiva.Pcm24: return new FormatoExacto(hz, canales, 24, 24, false, mascara);
                case MuestraExclusiva.Pcm32: return new FormatoExacto(hz, canales, 32, 32, false, mascara);
                case MuestraExclusiva.Flotante32: return new FormatoExacto(hz, canales, 32, 32, true, mascara);
                default: return new FormatoExacto(hz, canales, 16, 16, false, mascara);
            }
        }

        internal static MuestraExclusiva TipoDe(FormatoExacto f)
        {
            if (f.EsFlotante) return MuestraExclusiva.Flotante32;
            if (f.BitsPerSample == 16) return MuestraExclusiva.Pcm16;
            if (f.BitsPerSample == 24) return MuestraExclusiva.Pcm24;
            return f.BitsValidos == 24 ? MuestraExclusiva.Pcm24en32 : MuestraExclusiva.Pcm32;
        }

        // Las frecuencias en el orden en que se piden: la del aparato, la de la cancion y las habituales.
        internal static List<int> Frecuencias(int hzAparato, int hzCancion)
        {
            List<int> r = new List<int>();
            if (hzAparato > 0) r.Add(hzAparato);
            if (hzCancion > 0 && !r.Contains(hzCancion)) r.Add(hzCancion);
            foreach (int h in Habituales) if (!r.Contains(h)) r.Add(h);
            return r;
        }

        // Todos los formatos a probar, en orden: las profundidades buenas en cada frecuencia y, al final, 16 bits.
        internal static List<FormatoExacto> Candidatos(int hzAparato, int hzCancion, int canales)
        { return Candidatos(hzAparato, hzCancion, canales, canales == 1 ? 4 : (canales == 2 ? 3 : 0)); }

        internal static List<FormatoExacto> Candidatos(int hzAparato, int hzCancion, int canales, int mascara)
        {
            bool monoCentral = canales == 1 && mascara == 4;
            int ch = monoCentral ? 2 : canales;   // mono central canonico conserva la duplicacion historica
            if (monoCentral) mascara = 3;
            List<FormatoExacto> r = new List<FormatoExacto>();
            List<int> hz = Frecuencias(hzAparato, hzCancion);
            if (canales > 2 && mascara == 0) return r; // sin disposicion no se inventan altavoces
            foreach (int h in hz) foreach (MuestraExclusiva m in Buenas) r.Add(Formato(h, ch, m, mascara));
            foreach (int h in hz) r.Add(Formato(h, ch, MuestraExclusiva.Pcm16, mascara));
            return r;
        }

        // El primero que el aparato admite (null si ninguno).
        internal static FormatoExacto Elige(int hzAparato, int hzCancion, int canales, Func<WaveFormat, bool> admite)
        {
            foreach (FormatoExacto f in Candidatos(hzAparato, hzCancion, canales)) if (admite(f)) return f;
            return null;
        }

        // La ganancia que aplica la rocola para respetar el volumen de Windows en exclusivo. Con volumen propio lo
        // mueve Windows en el aparato: 1. Si no, el nivel del control en dB. El silencio de Windows, si el aparato no
        // tiene silencio propio, es 0.
        internal static float GananciaDeWindows(bool volumenPropio, bool silencioPropio, float db, bool silenciado)
        {
            if (silenciado && !silencioPropio) return 0f;
            if (volumenPropio) return 1f;
            if (float.IsNaN(db) || db >= 0f) return 1f;
            if (db < -150f) return 0f;
            return (float)Math.Pow(10.0, db / 20.0);
        }

        // ¿El fallo es de ESTE formato (se prueba el siguiente)? Formato rechazado al iniciar, tamanos de bufer que el
        // aparato no acepta con ese formato, o NAudio queriendo cambiarlo.
        internal static bool EsDeFormato(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is FormatoCambiadoException) return true;
                COMException com = e as COMException;
                if (com != null)
                {
                    switch (unchecked((uint)com.ErrorCode))
                    {
                        case 0x88890008u:   // AUDCLNT_E_UNSUPPORTED_FORMAT
                        case 0x88890016u:   // AUDCLNT_E_BUFFER_SIZE_ERROR
                        case 0x88890019u:   // AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED
                        case 0x88890020u:   // AUDCLNT_E_INVALID_DEVICE_PERIOD
                        case 0x80070057u:   // E_INVALIDARG
                            return true;
                    }
                    return false;
                }
            }
            return false;
        }

        internal static uint Codigo(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                COMException com = e as COMException;
                if (com != null) return unchecked((uint)com.ErrorCode);
            }
            return 0;
        }
    }

    // NAudio acepto el formato al preguntarle y al iniciar lo cambio por otro (metiendo su remuestreador): no se usa.
    internal sealed class FormatoCambiadoException : Exception
    {
        public FormatoCambiadoException() : base("NAudio cambio el formato exclusivo por otro") { }
    }

    // La musica de la cadena (coma flotante) en el formato exacto del aparato, con el volumen de Windows si el aparato
    // no tiene volumen propio (ver arriba).
    internal sealed class SalidaExacta : IWaveProvider
    {
        private readonly ISampleProvider src;
        private readonly FormatoExacto fmt;
        private readonly MuestraExclusiva tipo;
        private readonly float paso;     // cuanto se mueve la ganancia por cuadro: de 0 a 1 en 50 ms
        private float[] fbuf;
        private uint rs;                 // xorshift del dither (solo 16 bits), como la salida clasica
        private float gan = 1f;          // la ganancia que se esta aplicando
        private volatile float pedida = 1f;
        private int lecturas;

        public SalidaExacta(ISampleProvider s, FormatoExacto f)
        {
            src = s; fmt = f; tipo = Exclusivo.TipoDe(f);
            paso = 1f / Math.Max(1f, 0.05f * f.SampleRate);
            rs = (uint)Environment.TickCount | 1u;
        }

        public WaveFormat WaveFormat { get { return fmt; } }
        internal FormatoExacto Formato { get { return fmt; } }
        // Cuantas veces la pidio la salida: la primera es el relleno antes de arrancar, asi que con una sola la salida
        // aun no habia arrancado de verdad.
        internal int Lecturas { get { return lecturas; } }
        // El volumen de Windows a respetar (lineal, 0..1). Se llega en rampa.
        internal float Ganancia { get { return pedida; } set { pedida = value < 0f ? 0f : (value > 1f ? 1f : value); } }
        // La de salida, sin rampa: antes de que suene nada (que no arranque a tope y luego baje).
        internal void GananciaInicial(float g) { Ganancia = g; gan = pedida; }

        private float Uniforme() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs >> 8) * (1f / 16777216f); }

        public int Read(byte[] buffer, int offset, int count)
        {
            lecturas++;
            int ch = fmt.Channels, bpm = fmt.BitsPerSample / 8;
            int cuadros = count / (bpm * ch);
            int need = cuadros * ch;
            if (need <= 0) return 0;
            if (fbuf == null || fbuf.Length < need) fbuf = new float[need];
            int got = src.Read(fbuf, 0, need);
            got -= got % ch;
            if (got <= 0) return 0;

            float objetivo = pedida;
            if (gan != 1f || objetivo != 1f)
            {
                for (int i = 0; i < got; i += ch)
                {
                    if (gan != objetivo)
                    {
                        float d = objetivo - gan;
                        gan = (d > paso) ? gan + paso : (d < -paso ? gan - paso : objetivo);
                    }
                    for (int c = 0; c < ch; c++) fbuf[i + c] *= gan;
                }
            }

            int o = offset;
            switch (tipo)
            {
                case MuestraExclusiva.Flotante32:
                    Buffer.BlockCopy(fbuf, 0, buffer, offset, got * 4);
                    break;
                case MuestraExclusiva.Pcm16:
                    for (int i = 0; i < got; i++)
                    {
                        float d = Uniforme() - Uniforme();                  // triangular de +-1 escalon (TPDF)
                        int q = (int)Math.Floor(fbuf[i] * 32767f + d + 0.5f);
                        if (q > 32767) q = 32767; else if (q < -32768) q = -32768;
                        buffer[o++] = (byte)q; buffer[o++] = (byte)(q >> 8);
                    }
                    break;
                case MuestraExclusiva.Pcm24:
                    for (int i = 0; i < got; i++)
                    {
                        int q = A24(fbuf[i]);
                        buffer[o++] = (byte)q; buffer[o++] = (byte)(q >> 8); buffer[o++] = (byte)(q >> 16);
                    }
                    break;
                case MuestraExclusiva.Pcm24en32:
                    for (int i = 0; i < got; i++)
                    {
                        int q = A24(fbuf[i]);                               // 24 bits validos arriba; el byte bajo, cero
                        buffer[o++] = 0; buffer[o++] = (byte)q; buffer[o++] = (byte)(q >> 8); buffer[o++] = (byte)(q >> 16);
                    }
                    break;
                default:
                    for (int i = 0; i < got; i++)
                    {
                        double v = Math.Floor(fbuf[i] * 2147483647.0 + 0.5);
                        int q = v >= 2147483647.0 ? int.MaxValue : (v <= -2147483648.0 ? int.MinValue : (int)v);
                        buffer[o++] = (byte)q; buffer[o++] = (byte)(q >> 8); buffer[o++] = (byte)(q >> 16); buffer[o++] = (byte)(q >> 24);
                    }
                    break;
            }
            return got * bpm;
        }

        private static int A24(float x)
        {
            double v = Math.Floor(x * 8388607.0 + 0.5);
            return v > 8388607.0 ? 8388607 : (v < -8388608.0 ? -8388608 : (int)v);
        }
    }

    // Lo que hace falta del aparato para el exclusivo, en funciones: en Windows las llena Form1.AparatoPredeterminado
    // con NAudio; las pruebas ponen las suyas (un aparato de mentira que admite, rechaza o falla a voluntad).
    internal sealed class AparatoExclusivo : IDisposable
    {
        internal string Id = "";
        internal Func<int> Hz;                    // la frecuencia a la que trabaja (la de su formato en Windows)
        internal Func<WaveFormat, bool> Admite;   // ¿lo admite en exclusivo?
        internal Func<IWavePlayer> Abre;          // la salida exclusiva, sin iniciar
        internal Func<int> Propio;                // lo que tiene propio: 1 = volumen, 2 = silencio (EEndpointHardwareSupport)
        internal Func<float> Db;                  // el control de volumen de Windows, en dB
        internal Func<bool> Silenciado;           // el silencio de Windows
        internal Action Suelta;                   // soltar lo que se abrio para preguntar

        internal bool VolumenPropio { get { return (Propio() & 1) != 0; } }
        internal bool SilencioPropio { get { return (Propio() & 2) != 0; } }
        public void Dispose() { Action a = Suelta; Suelta = null; if (a != null) a(); }
    }

    public partial class Form1 : Form
    {
        internal Func<object> aparatoExclusivoPrueba = null;                              // solo las pruebas: un AparatoExclusivo (null = el de Windows)
        internal Func<ISampleProvider, int, ISampleProvider> remuestreoPrueba = null;     // idem: remuestreador de mentira
        private AparatoExclusivo aparatoExclusivo;    // el de la salida exclusiva abierta (para seguir el volumen de Windows)
        private SalidaExacta salidaExacta;            // lo que le llega (volumen y si ya arranco)
        private Dictionary<string, string> exclusivoNo;   // aparato -> por que no se usa en esta sesion
        private HashSet<string> exclusivoApuntado;        // "aparato|motivo" ya apuntados en rocola_error.txt
        private bool reintentandoCompartido;              // la cancion se reabre en compartido: no probar exclusivo

        // Abre la cancion en WASAPI exclusivo. true = ya suena en exclusivo ('output' puesto y en marcha). false = no se
        // pudo, y el que llama sigue por el compartido de siempre con la MISMA cadena: de ella aun no se leyo nada.
        private bool AbreExclusiva(ISampleProvider samples)
        {
            AparatoExclusivo ap;
            try { ap = aparatoExclusivoPrueba != null ? (AparatoExclusivo)aparatoExclusivoPrueba() : AparatoPredeterminado(); }
            catch (Exception ex) { ApuntaExclusivo("?", "no se pudo preguntar por el aparato", ex); return false; }
            if (ap == null) return false;
            string id = "";
            if (ap.Id != null) id = ap.Id;
            string porQue;
            if (exclusivoNo != null && exclusivoNo.TryGetValue(id, out porQue)) { SueltaAparato(ap); return false; }
            int hzCancion = samples.WaveFormat.SampleRate, canales = samples.WaveFormat.Channels;
            bool admitioAlguno = false;
            try
            {
                int hzAparato = ap.Hz();
                foreach (FormatoExacto fmt in Exclusivo.Candidatos(hzAparato, hzCancion, canales, FormatoAudio.Mascara(samples.WaveFormat)))
                {
                    if (!ap.Admite(fmt)) continue;
                    admitioAlguno = true;
                    IWavePlayer wa = null;
                    try
                    {
                        retardoFinalReloj = 0; // cada candidato tiene su propio camino, aun tras un rechazo con DMO
                        ISampleProvider s = samples;
                        if (s.WaveFormat.Channels == 1 && fmt.Channels == 2)
                        {
                            // Duplicacion exacta, sin tocar el volumen.
                            MonoToStereoSampleProvider st = new MonoToStereoSampleProvider(s);
                            st.LeftVolume = 1; st.RightVolume = 1; s = st;
                        }
                        if (s.WaveFormat.SampleRate != fmt.SampleRate)
                        {
                            // Remuestrear reconstruye la onda y deja en muestras lo que caia ENTRE ellas: el ultimo
                            // limitador va DESPUES y mira el pico real (ver PicoReal.cs), igual que en compartido.
                            LimitadorPicoReal final = new LimitadorPicoReal(NuevoRemuestreo(s, fmt.SampleRate), TechoPicoReal, 3.0);
                            s = final; MarcaRetardoFinalReloj(final);
                        }
                        PreparaLatenciaSalida("wasapi-exclusivo:" + id, s);
                        SalidaExacta sx = new SalidaExacta(MideLatenciaSalida(s, true), fmt);
                        sx.GananciaInicial(GananciaDeWindowsDe(ap));   // desde la primera muestra: nada de arrancar a tope
                        wa = ap.Abre();
                        output = wa;
                        wa.PlaybackStopped += Output_PlaybackStopped;
                        wa.Init(sx);
                        IWavePosition pos = wa as IWavePosition;
                        if (pos != null && !object.ReferenceEquals(pos.OutputWaveFormat, fmt)) throw new FormatoCambiadoException();
                        wa.Play();
                        aparatoExclusivo = ap; salidaExacta = sx;
                        formatoSalida = "WASAPI exclusivo, " + fmt.Descripcion;
                        audioVisualDelayMs = RetardoVisualSalida();
                        ApuntaExclusivo(id, "en marcha: " + fmt.Descripcion + " (sin el mezclador de Windows)", null);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (perfilLatencia != null) perfilLatencia.Fallo();
                        if (wa != null)
                        {
                            wa.PlaybackStopped -= Output_PlaybackStopped;
                            try { wa.Dispose(); } catch { }
                        }
                        output = null;
                        SueltaRemuestreo();
                        retardoFinalReloj = 0; // el compartido/nativo no hereda el limitador de un intento fallido
                        if (Exclusivo.EsDeFormato(ex)) continue;   // este formato no: el siguiente
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                uint hr = Exclusivo.Codigo(ex);
                if (hr == 0x8889000Au)
                    // Otro programa usa el aparato: esta cancion va en compartido y con la siguiente se vuelve a probar.
                    ApuntaExclusivo(id, "otro programa estaba usando el aparato; esta cancion va en compartido", ex);
                else if (EsFalloDelDispositivo(ex))
                    ApuntaExclusivo(id, "fallo el aparato al abrirlo en exclusivo", ex);   // lo atiende el compartido (y la espera)
                else
                    NoExclusivo(id, hr == 0x8889000Eu ? "Windows no permite el modo exclusivo en este aparato" : "no se pudo abrir en exclusivo", ex);
                SueltaAparato(ap);
                return false;
            }
            if (canales > 2)
                // Es de ESTA cancion (muchos aparatos solo admiten dos canales en exclusivo): va en compartido y la
                // siguiente, si es estereo, vuelve a probar.
                ApuntaExclusivo(id, "una cancion de " + canales + " canales no cabe en exclusivo en este aparato: va en compartido", null);
            else
                NoExclusivo(id, admitioAlguno ? "ningun formato abrio en exclusivo" : "el aparato no admite en exclusivo ningun formato que la rocola sepa dar", null);
            SueltaAparato(ap);
            return false;
        }

        // El aparato predeterminado de Windows (el mismo que usaria el compartido), con NAudio. Queda en outputDevice:
        // lo suelta DisposePlaybackNow, como en compartido.
        private AparatoExclusivo AparatoPredeterminado()
        {
            NAudio.CoreAudioApi.MMDevice d;
            using (NAudio.CoreAudioApi.MMDeviceEnumerator devices = new NAudio.CoreAudioApi.MMDeviceEnumerator())
                d = devices.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console);
            outputDevice = d;
            // Un solo cliente para todas las preguntas de formato; se suelta antes de abrir la salida de verdad.
            NAudio.CoreAudioApi.AudioClient[] consulta = new NAudio.CoreAudioApi.AudioClient[1];
            Func<NAudio.CoreAudioApi.AudioClient> cliente = delegate
            {
                if (consulta[0] == null) consulta[0] = d.AudioClient;
                return consulta[0];
            };
            Action suelta = delegate
            {
                NAudio.CoreAudioApi.AudioClient c = consulta[0]; consulta[0] = null;
                if (c != null) { try { c.Dispose(); } catch { } }
            };
            int[] propio = { -1 };
            AparatoExclusivo ap = new AparatoExclusivo();
            ap.Id = d.ID;
            ap.Hz = delegate { return cliente().MixFormat.SampleRate; };
            ap.Admite = delegate(WaveFormat f) { return cliente().IsFormatSupported(NAudio.CoreAudioApi.AudioClientShareMode.Exclusive, f); };
            // Sin eventos y con 300 ms de colchon, como el compartido (NAudio lo rellena cada 150 ms).
            ap.Abre = delegate { suelta(); return new WasapiOut(d, NAudio.CoreAudioApi.AudioClientShareMode.Exclusive, false, latenciaAbiertaMs > 0 ? latenciaAbiertaMs : 300); };
            ap.Propio = delegate { if (propio[0] < 0) propio[0] = (int)d.AudioEndpointVolume.HardwareSupport; return propio[0]; };
            ap.Db = delegate { return d.AudioEndpointVolume.MasterVolumeLevel; };
            ap.Silenciado = delegate { return d.AudioEndpointVolume.Mute; };
            ap.Suelta = suelta;
            return ap;
        }

        private ISampleProvider NuevoRemuestreo(ISampleProvider s, int hz)
        {
            if (remuestreoPrueba != null) return remuestreoPrueba(s, hz);
            outputResampler = new SalidaRemuestreada(s, hz);
            return outputResampler;
        }

        private void SueltaRemuestreo()
        {
            if (outputResampler != null) { try { outputResampler.Dispose(); } catch { } outputResampler = null; }
        }

        // El exclusivo no se usa: se suelta lo que se abrio para preguntar (el compartido pregunta por su cuenta).
        private void SueltaAparato(AparatoExclusivo ap)
        {
            try { ap.Dispose(); } catch { }
            if (aparatoExclusivoPrueba == null && outputDevice != null) { try { outputDevice.Dispose(); } catch { } outputDevice = null; }
        }

        // Al cerrar la salida (DisposePlaybackNow).
        private void SueltaExclusivo()
        {
            AparatoExclusivo ap = aparatoExclusivo;
            aparatoExclusivo = null; salidaExacta = null;
            if (ap != null) { try { ap.Dispose(); } catch { } }
        }

        // En este aparato no se vuelve a probar el exclusivo en esta sesion.
        private void NoExclusivo(string id, string motivo, Exception ex)
        {
            if (exclusivoNo == null) exclusivoNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            exclusivoNo[id] = motivo;
            ApuntaExclusivo(id, motivo + "; en esta sesion sigue en compartido", ex);
        }

        // Una vez por aparato y motivo en rocola_error.txt: ahi se ve si la musica sale en exclusivo y en que formato.
        private void ApuntaExclusivo(string id, string motivo, Exception ex)
        {
            if (exclusivoApuntado == null) exclusivoApuntado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!exclusivoApuntado.Add(id + "|" + motivo)) return;
            LogError("Salida de sonido, WASAPI exclusivo: " + motivo, ex);
        }

        private static float GananciaDeWindowsDe(AparatoExclusivo ap)
        {
            bool vol = ap.VolumenPropio, sil = ap.SilencioPropio;
            if (vol && sil) return 1f;   // todo lo hace el aparato
            return Exclusivo.GananciaDeWindows(vol, sil, vol ? 0f : ap.Db(), ap.Silenciado());
        }

        // En exclusivo, si el aparato no tiene volumen propio, la rocola sigue el control de volumen de Windows. Lo
        // mira la barra de progreso cada medio segundo; la salida llega al valor nuevo en rampa.
        private void SigueVolumenWindows()
        {
            AparatoExclusivo ap = aparatoExclusivo; SalidaExacta sx = salidaExacta;
            if (ap == null || sx == null) return;
            try { sx.Ganancia = GananciaDeWindowsDe(ap); } catch { }
        }

        // ¿La salida exclusiva fallo al ARRANCAR, antes de sonar? Un error de Windows con solo el relleno inicial
        // pedido. Un fallo del aparato de verdad, como desenchufarlo, lo atiende la espera de siempre.
        private bool FalloAlArrancarExclusivo(Exception deLaSalida)
        {
            SalidaExacta sx = salidaExacta;
            return deLaSalida != null && sx != null && sx.Lecturas <= 1 && Exclusivo.Codigo(deLaSalida) != 0
                   && !EsFalloDelDispositivo(deLaSalida) && nowPlaying != null && nowPlaying.Song != null && !nowPlaying.IsVideo;
        }

        // La salida exclusiva se quedo muda (el vigia de la barra de progreso, SalidaSonido.cs): hay controladores que
        // se atascan en exclusivo y en compartido no. La cancion se retoma como siempre, pero ya en compartido.
        private void MudoEnExclusivo()
        {
            AparatoExclusivo ap = aparatoExclusivo;
            if (ap != null) NoExclusivo(ap.Id ?? "", "la salida exclusiva se quedo muda", null);
        }

        // Fallo al arrancar en exclusivo: la misma cancion, desde donde iba a empezar, en compartido. Ese aparato ya no
        // se prueba en exclusivo en esta sesion.
        private void ReintentaCompartido(Track t, Exception ex)
        {
            AparatoExclusivo ap = aparatoExclusivo;
            string id = (ap != null && ap.Id != null) ? ap.Id : "";
            NoExclusivo(id, "fallo al arrancar en exclusivo", ex);
            curMeasurePath = null; eqMeasPath = null;
            double desde = inicioSeg;
            DisposePlaybackNow();
            reintentandoCompartido = true;
            reanudarEn = desde;
            try { PlayTrack(t); }
            finally { reintentandoCompartido = false; }
        }
    }
}

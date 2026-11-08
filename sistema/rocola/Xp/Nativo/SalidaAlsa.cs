// LA TARJETA DE SONIDO POR ALSA (port nativo de XP, 8-11-2026: «que tradujeras todo el codigo para que trabajara nativamente en Linux»).
//
// En XP, la salida con hilo propio (Codigo/SalidaPropia.cs, WaveOutPropio) habla con winmm: waveOutOpen con aviso por evento, una WAVEHDR
// por bloque (waveOutPrepareHeader / waveOutWrite / waveOutUnprepareHeader), la bandera WHDR_DONE cuando el bloque ya sono, waveOutReset,
// waveOutPause / waveOutRestart, waveOutGetPosition y waveOutClose, y los errores MMSYSERR. La traduccion anterior dejaba ese codigo igual y
// debajo una imitacion de winmm sobre ALSA (Plataforma/SonidoAlsa.cs: AlsaWaveOut con las WAVEHDR en memoria y su propio hilo, MmResult,
// MmException y un WaveOut con la forma del de NAudio). Aqui no: WaveOutPropio llama DIRECTO a esta clase, que es libasound sin disfraz,
// con solo lo que hace falta:
//   - Abre: snd_pcm_open de la tarjeta de la rocola (Tarjeta) en modo sin espera, el formato tal cual (16 bits; coma flotante si alguna vez
//     se pide), la frecuencia exacta (plughw convierte si la tarjeta no la tiene, como el mapeador de ondas de Windows), un periodo del
//     tamano de un bloque de XP (50 ms) y un bufer de todos los bloques (la latencia de XP: 750 a 1000 ms). Empieza a sonar con lo primero
//     que se escribe (umbral de arranque 1), como waveOutWrite.
//   - Libres / Escribe: cuanto cabe ahora y escribir un bloque entero (snd_pcm_avail, snd_pcm_writei). Si la tarjeta se quedo sin nada
//     que sonar (XRUN: el hilo no llego a tiempo) se recupera sola y sigue, como winmm, que en ese caso solo se calla un momento.
//   - Sonado: los cuadros que ya sonaron desde el ultimo Vacia: lo escrito menos lo que la tarjeta aun tiene por delante (snd_pcm_delay).
//     Con eso WaveOutPropio sabe que bloques ya sonaron (lo que en XP era WHDR_DONE) y cuanto lleva la cancion (waveOutGetPosition).
//   - Vacia (waveOutReset): lo que no sono se tira (snd_pcm_drop) y la cuenta vuelve a 0. Pausa (waveOutPause/waveOutRestart):
//     snd_pcm_pause. Cierra (waveOutClose).
//   - Los errores son ErrorAlsa con el numero de ALSA (un -errno) y la funcion; Form1 decide con ellos lo mismo que XP decidia con los
//     MMSYSERR (Codigo/SalidaSonido.cs, EsFalloDelDispositivo y EsFormatoRechazado).
// La tarjeta: ROCOLA_ALSA si esta puesta (la pone /usr/bin/rocola con rocola-tarjeta) y si no, la primera salida que no sea HDMI, directa
// (plughw); en XP, el mapeador de ondas (WAVE_MAPPER): la que Windows tenga por predeterminada.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // Un error de ALSA: Codigo es lo que devolvio libasound (un -errno; SinAlsa si no esta libasound) y Funcion, la llamada.
    // Formato = la tarjeta no acepto el formato, los canales o la frecuencia (en XP, MMSYSERR WAVERR_BADFORMAT).
    internal sealed class ErrorAlsa : Exception
    {
        internal const int ENOENT = 2, EIO = 5, ENXIO = 6, EAGAIN = 11, ENOMEM = 12, EBUSY = 16, ENODEV = 19, EINVAL = 22, EPIPE = 32,
            ENOSYS = 38, EBADFD = 77, ESTRPIPE = 86;
        internal const int SinAlsa = int.MinValue;   // no esta libasound.so.2 (no hay sonido en el sistema)
        internal readonly int Codigo;
        internal readonly string Funcion;
        internal readonly bool Formato;
        internal ErrorAlsa(int codigo, string funcion, bool formato = false) : base(Texto(codigo) + " llamando a " + funcion)
        { Codigo = codigo; Funcion = funcion; Formato = formato; }
        private static string Texto(int codigo)
        {
            if (codigo == SinAlsa) return "no esta ALSA (libasound.so.2)";
            try { IntPtr t = SalidaAlsa.snd_strerror(codigo); if (t != IntPtr.Zero) return Marshal.PtrToStringUTF8(t) + " (" + codigo + ")"; } catch { }
            return "error " + codigo;
        }
    }

    internal sealed unsafe class SalidaAlsa
    {
        private const string Lib = "libasound.so.2";
        [DllImport(Lib)] private static extern int snd_pcm_open(out IntPtr pcm, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, int stream, int modo);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_malloc(out IntPtr p);
        [DllImport(Lib)] private static extern void snd_pcm_hw_params_free(IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_any(IntPtr pcm, IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_access(IntPtr pcm, IntPtr p, int acceso);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_format(IntPtr pcm, IntPtr p, int formato);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_channels(IntPtr pcm, IntPtr p, uint canales);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_rate_resample(IntPtr pcm, IntPtr p, uint si);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_rate(IntPtr pcm, IntPtr p, uint tasa, int dir);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_period_size_near(IntPtr pcm, IntPtr p, ref nuint cuadros, IntPtr dir);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_buffer_size_near(IntPtr pcm, IntPtr p, ref nuint cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params(IntPtr pcm, IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_get_buffer_size(IntPtr p, out nuint cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_get_period_size(IntPtr p, out nuint cuadros, IntPtr dir);
        [DllImport(Lib)] private static extern int snd_pcm_sw_params_malloc(out IntPtr p);
        [DllImport(Lib)] private static extern void snd_pcm_sw_params_free(IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_sw_params_current(IntPtr pcm, IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_sw_params_set_start_threshold(IntPtr pcm, IntPtr p, nuint cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_sw_params_set_avail_min(IntPtr pcm, IntPtr p, nuint cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_sw_params(IntPtr pcm, IntPtr p);
        [DllImport(Lib)] private static extern nint snd_pcm_writei(IntPtr pcm, byte* datos, nuint cuadros);
        [DllImport(Lib)] private static extern nint snd_pcm_avail(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_delay(IntPtr pcm, out nint cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_recover(IntPtr pcm, int error, int callado);
        [DllImport(Lib)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_prepare(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_pause(IntPtr pcm, int si);
        [DllImport(Lib)] private static extern int snd_pcm_close(IntPtr pcm);
        [DllImport(Lib)] internal static extern IntPtr snd_strerror(int error);
        private const int Reproducir = 0, SinEspera = 1, AccesoIntercalado = 3;
        private const int U8 = 1, S16_LE = 2, S32_LE = 10, FLOAT_LE = 14, S24_3LE = 32;

        private IntPtr pcm;
        private readonly int alineado;   // bytes por cuadro
        private long escritos;           // cuadros dados a la tarjeta desde el ultimo Vacia
        internal readonly int Tasa, CuadrosBufer, CuadrosPeriodo;
        internal readonly string Nombre;

        private SalidaAlsa(IntPtr pcm, string nombre, WaveFormat f, int bufer, int periodo)
        { this.pcm = pcm; Nombre = nombre; alineado = Math.Max(1, f.BlockAlign); Tasa = f.SampleRate; CuadrosBufer = bufer; CuadrosPeriodo = periodo; }

        // Abre la tarjeta de la rocola con ese formato, un periodo de un bloque y un bufer de todos los bloques. ErrorAlsa si no se puede.
        internal static SalidaAlsa Abre(WaveFormat formato, int cuadrosBloque, int bloques)
        {
            int fmt = FormatoAlsa(formato);
            if (fmt < 0) throw new ErrorAlsa(-ErrorAlsa.EINVAL, "snd_pcm_hw_params_set_format", true);
            string nombre = Tarjeta();
            IntPtr p; int r;
            try { r = snd_pcm_open(out p, nombre, Reproducir, SinEspera); }
            catch (DllNotFoundException) { throw new ErrorAlsa(ErrorAlsa.SinAlsa, "snd_pcm_open"); }
            // En Windows la salida pasa por el mezclador del sistema y el reproductor de video y la rocola suenan a la vez sin pelearse la
            // tarjeta; por ALSA directo (plughw) la tarjeta es de uno solo: si el video (mpv) aun no la solto al acabar, se espera hasta 1,5 s
            // antes de darla por ocupada.
            for (int espera = 0; espera < 15 && r == -ErrorAlsa.EBUSY; espera++) { Thread.Sleep(100); r = snd_pcm_open(out p, nombre, Reproducir, SinEspera); }
            if (r < 0) throw new ErrorAlsa(r, "snd_pcm_open");
            IntPtr hw = IntPtr.Zero, sw = IntPtr.Zero;
            try
            {
                if ((r = snd_pcm_hw_params_malloc(out hw)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params_malloc");
                if ((r = snd_pcm_hw_params_any(p, hw)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params_any");
                if ((r = snd_pcm_hw_params_set_access(p, hw, AccesoIntercalado)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params_set_access", r == -ErrorAlsa.EINVAL);
                if ((r = snd_pcm_hw_params_set_format(p, hw, fmt)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params_set_format", r == -ErrorAlsa.EINVAL);
                if ((r = snd_pcm_hw_params_set_channels(p, hw, (uint)formato.Channels)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params_set_channels", r == -ErrorAlsa.EINVAL);
                snd_pcm_hw_params_set_rate_resample(p, hw, 1);
                if ((r = snd_pcm_hw_params_set_rate(p, hw, (uint)formato.SampleRate, 0)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params_set_rate", r == -ErrorAlsa.EINVAL);
                nuint periodo = (nuint)Math.Max(1, cuadrosBloque), bufer = (nuint)Math.Max(2, cuadrosBloque * Math.Max(2, bloques));
                snd_pcm_hw_params_set_period_size_near(p, hw, ref periodo, IntPtr.Zero);
                snd_pcm_hw_params_set_buffer_size_near(p, hw, ref bufer);
                if ((r = snd_pcm_hw_params(p, hw)) < 0) throw new ErrorAlsa(r, "snd_pcm_hw_params", r == -ErrorAlsa.EINVAL);
                snd_pcm_hw_params_get_buffer_size(hw, out bufer);
                snd_pcm_hw_params_get_period_size(hw, out periodo, IntPtr.Zero);
                if ((r = snd_pcm_sw_params_malloc(out sw)) < 0) throw new ErrorAlsa(r, "snd_pcm_sw_params_malloc");
                if ((r = snd_pcm_sw_params_current(p, sw)) < 0) throw new ErrorAlsa(r, "snd_pcm_sw_params_current");
                snd_pcm_sw_params_set_start_threshold(p, sw, 1);   // suena con lo primero que llega, como waveOutWrite
                snd_pcm_sw_params_set_avail_min(p, sw, periodo);
                if ((r = snd_pcm_sw_params(p, sw)) < 0) throw new ErrorAlsa(r, "snd_pcm_sw_params");
                SalidaAlsa s = new SalidaAlsa(p, nombre, formato, (int)bufer, (int)periodo);
                p = IntPtr.Zero;
                return s;
            }
            finally
            {
                if (hw != IntPtr.Zero) snd_pcm_hw_params_free(hw);
                if (sw != IntPtr.Zero) snd_pcm_sw_params_free(sw);
                if (p != IntPtr.Zero) snd_pcm_close(p);
            }
        }
        private static int FormatoAlsa(WaveFormat f)
        {
            int bits = f.BitsPerSample;
            WaveFormatEncoding e = f.Encoding;
            if (e == WaveFormatEncoding.Extensible) e = bits == 32 && f is WaveFormatExtensible && ((WaveFormatExtensible)f).SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71") ? WaveFormatEncoding.IeeeFloat : WaveFormatEncoding.Pcm;
            if (e == WaveFormatEncoding.IeeeFloat && bits == 32) return FLOAT_LE;
            if (e != WaveFormatEncoding.Pcm) return -1;
            switch (bits) { case 8: return U8; case 16: return S16_LE; case 24: return S24_3LE; case 32: return S32_LE; }
            return -1;
        }

        // Cuantos cuadros caben ahora. Si la tarjeta se quedo sin nada (XRUN) o se suspendio, se recupera y cabe todo.
        internal int Libres()
        {
            nint a = snd_pcm_avail(pcm);
            if (a == -ErrorAlsa.EPIPE || a == -ErrorAlsa.ESTRPIPE) { Recupera((int)a, "snd_pcm_avail"); a = snd_pcm_avail(pcm); }
            if (a < 0) throw new ErrorAlsa((int)a, "snd_pcm_avail");
            return (int)Math.Min(a, int.MaxValue);
        }
        // Escribe esos bytes (cuadros enteros) a la tarjeta. Se llama con sitio para todo (Libres); si aun asi no cupiera entero, espera lo
        // que haga falta (como mucho un bufer entero de tiempo, y no deberia).
        internal void Escribe(byte[] datos, int bytes)
        {
            long cuadros = bytes / alineado, hechos = 0;
            int espera = 0, tope = Math.Max(1000, CuadrosBufer * 1000 / Math.Max(1, Tasa) * 2);
            fixed (byte* d = datos)
                while (hechos < cuadros)
                {
                    nint r = snd_pcm_writei(pcm, d + hechos * alineado, (nuint)(cuadros - hechos));
                    if (r == -ErrorAlsa.EAGAIN || r == 0)
                    {
                        if (++espera > tope) throw new ErrorAlsa(-ErrorAlsa.EIO, "snd_pcm_writei");   // la tarjeta dejo de pedir sonido
                        Thread.Sleep(1); continue;
                    }
                    if (r == -ErrorAlsa.EPIPE || r == -ErrorAlsa.ESTRPIPE) { Recupera((int)r, "snd_pcm_writei"); continue; }
                    if (r < 0) throw new ErrorAlsa((int)r, "snd_pcm_writei");
                    hechos += r; escritos += r;
                }
        }
        private void Recupera(int error, string funcion)
        {
            int r = snd_pcm_recover(pcm, error, 1);
            if (r < 0) throw new ErrorAlsa(r, funcion);
        }
        // Los cuadros que ya sonaron desde el ultimo Vacia: lo escrito menos lo que la tarjeta aun tiene por delante. Si se quedo sin nada
        // (XRUN), ya sono todo.
        internal long Sonado()
        {
            nint d;
            int r = snd_pcm_delay(pcm, out d);
            if (r == -ErrorAlsa.EPIPE || r == -ErrorAlsa.ESTRPIPE) return escritos;
            if (r < 0) throw new ErrorAlsa(r, "snd_pcm_delay");
            return Math.Max(0, escritos - Math.Max(0, (long)d));
        }
        internal long Escritos { get { return escritos; } }
        // Lo que no sono se tira y la cuenta vuelve a 0 (waveOutReset)
        internal void Vacia()
        {
            snd_pcm_drop(pcm);
            snd_pcm_prepare(pcm);
            escritos = 0;
        }
        // waveOutPause / waveOutRestart: la tarjeta se para donde va y sigue desde ahi. (Si la tarjeta no sabe pausar, sigue sonando lo que ya
        // tiene y despues se calla: WaveOutPropio no le da mas mientras este en pausa. XP no pausa nunca la musica: solo Play, Stop y Dispose.)
        internal int Pausa(bool si)
        {
            int r = snd_pcm_pause(pcm, si ? 1 : 0);
            if (r < 0 && !si) snd_pcm_prepare(pcm);
            return r;
        }
        internal void Cierra()
        {
            IntPtr p = pcm; pcm = IntPtr.Zero;
            if (p == IntPtr.Zero) return;
            snd_pcm_drop(p);
            snd_pcm_close(p);
        }

        // LA TARJETA DE LA ROCOLA (la misma eleccion que el motor de Rocola OS, Motor/SalidaAlsa.cs; en XP, WAVE_MAPPER)
        private static string elegida;
        internal static string Tarjeta()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_ALSA");
            if (!string.IsNullOrEmpty(e)) return e;
            if (elegida != null) return elegida;
            string mejor = "default";
            try
            {
                List<string> tarjetas = new List<string>(Directory.GetDirectories("/proc/asound", "card*"));
                tarjetas.Sort(CompareNumero);
                foreach (string t in tarjetas)
                {
                    int n; if (!int.TryParse(Path.GetFileName(t).Substring(4), out n)) continue;
                    List<string> pcms = new List<string>(Directory.GetDirectories(t, "pcm*p"));
                    pcms.Sort(CompareNumero);
                    foreach (string p in pcms)
                    {
                        string info = "";
                        try { info = File.ReadAllText(Path.Combine(p, "info")); } catch { }
                        if (info.IndexOf("HDMI", StringComparison.OrdinalIgnoreCase) >= 0 || info.IndexOf("DisplayPort", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        string dev = Path.GetFileName(p); dev = dev.Substring(3, dev.Length - 4);
                        mejor = "plughw:" + n + "," + dev;
                        goto hecho;
                    }
                }
            }
            catch { }
        hecho:
            elegida = mejor;
            return mejor;
        }
        private static int CompareNumero(string a, string b) { return Numero(a).CompareTo(Numero(b)); }
        private static int Numero(string r) { string f = Path.GetFileName(r); int i = 0, n = 0; while (i < f.Length && !char.IsDigit(f[i])) i++; while (i < f.Length && char.IsDigit(f[i])) n = n * 10 + (f[i++] - '0'); return n; }
    }
}

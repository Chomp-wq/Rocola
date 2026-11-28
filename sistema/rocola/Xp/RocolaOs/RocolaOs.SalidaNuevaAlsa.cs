// ROCOLA OS (cadena de audio nueva, 27-11-2026; CADENA_NUEVA.md §4, CADENA_NUEVA_salida.md): LA TARJETA POR ALSA PARA LA SALIDA NUEVA.
// libasound sin disfraz, como Nativo/SalidaAlsa.cs, pero con lo que pide la salida nueva: el dispositivo tal cual (hw:CARD=...,DEV=...: sin
// el «plug» de ALSA, que convierte la frecuencia con su interpolacion lineal), la frecuencia EXACTA (44 100: sin remuestrear), el formato
// que de la tarjeta (S32_LE, S24_3LE o S16_LE, el primero que acepte), sin espera (snd_pcm_wait con tope: una tarjeta que deja de pedir
// sonido no cuelga el hilo) y tambien la ENTRADA (captura) para medir el convertidor (RocolaOs.SalidaNuevaMide.cs).
// Lo usan SOLO el hilo del motor (RocolaOs.SalidaNuevaMotor.cs) y la medida: una tarjeta abierta no se toca desde dos hilos.
using System;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal sealed unsafe class SalidaNuevaPcm : IDisposable
    {
        private const string Lib = "libasound.so.2";
        [DllImport(Lib)] private static extern int snd_pcm_open(out IntPtr pcm, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, int stream, int modo);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_malloc(out IntPtr p);
        [DllImport(Lib)] private static extern void snd_pcm_hw_params_free(IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_any(IntPtr pcm, IntPtr p);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_set_access(IntPtr pcm, IntPtr p, int acceso);
        [DllImport(Lib)] private static extern int snd_pcm_hw_params_test_format(IntPtr pcm, IntPtr p, int formato);
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
        [DllImport(Lib)] private static extern nint snd_pcm_readi(IntPtr pcm, byte* datos, nuint cuadros);
        [DllImport(Lib)] private static extern nint snd_pcm_avail(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_delay(IntPtr pcm, out nint cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_state(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_recover(IntPtr pcm, int error, int callado);
        [DllImport(Lib)] private static extern int snd_pcm_wait(IntPtr pcm, int ms);
        [DllImport(Lib)] private static extern int snd_pcm_start(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_close(IntPtr pcm);
        [DllImport(Lib)] private static extern IntPtr snd_strerror(int error);

        internal const int Reproducir = 0, Capturar = 1;
        private const int SinEspera = 1, AccesoIntercalado = 3;
        internal const int S16_LE = 2, S32_LE = 10, FLOAT_LE = 14, S24_3LE = 32;
        internal const int EPIPE = 32, ESTRPIPE = 86, EAGAIN = 11, ENODEV = 19, EBUSY = 16, EINVAL = 22, ENOENT = 2, EIO = 5, EBADFD = 77, ENXIO = 6;
        private const int EstadoXrun = 4, EstadoPreparado = 2, EstadoCorriendo = 3;

        private IntPtr pcm;
        internal readonly string Nombre;
        internal readonly int Formato, Tasa, Canales, Periodo, Bufer, BytesPorMuestra;
        internal readonly bool Captura;
        internal int BytesPorCuadro { get { return BytesPorMuestra * Canales; } }
        internal long Xruns;

        private SalidaNuevaPcm(IntPtr p, string nombre, bool captura, int formato, int tasa, int canales, int periodo, int bufer)
        {
            pcm = p; Nombre = nombre; Captura = captura; Formato = formato; Tasa = tasa; Canales = canales; Periodo = periodo; Bufer = bufer;
            BytesPorMuestra = formato == S16_LE ? 2 : formato == S24_3LE ? 3 : 4;
        }

        internal static string Texto(int codigo)
        {
            try { IntPtr t = snd_strerror(codigo); if (t != IntPtr.Zero) return Marshal.PtrToStringUTF8(t) + " (" + codigo + ")"; } catch { }
            return "error " + codigo;
        }
        internal static string NombreFormato(int f)
        {
            switch (f) { case S16_LE: return "S16_LE"; case S32_LE: return "S32_LE"; case S24_3LE: return "S24_3LE"; case FLOAT_LE: return "FLOAT_LE"; }
            return "formato " + f;
        }
        // Los bits que de verdad lleva cada formato (S32_LE de la salida nueva lleva 24: sus 8 de abajo van en 0)
        internal static int BitsUtiles(int f) { return f == S16_LE ? 16 : 24; }

        // ABRE «nombre» (sin espera) con el primero de los formatos que acepte, a la frecuencia EXACTA (remuestrear: solo si se pide, para el
        // «plughw» de respaldo), con un periodo y un bufer cerca de los pedidos. Devuelve null con el error (un -errno de ALSA; int.MinValue: no
        // hay libasound) y en que paso.
        internal static SalidaNuevaPcm Abre(string nombre, bool captura, int[] formatos, int tasa, int canales, int periodo, int bufer, bool remuestrear,
            out int error, out string paso)
        {
            error = 0; paso = "snd_pcm_open";
            IntPtr p;
            int r;
            try { r = snd_pcm_open(out p, nombre, captura ? Capturar : Reproducir, SinEspera); }
            catch (DllNotFoundException) { error = int.MinValue; return null; }
            catch (EntryPointNotFoundException) { error = int.MinValue; return null; }
            if (r < 0) { error = r; return null; }
            IntPtr hw = IntPtr.Zero, sw = IntPtr.Zero;
            try
            {
                if ((r = snd_pcm_hw_params_malloc(out hw)) < 0) { paso = "hw_params_malloc"; error = r; return null; }
                if ((r = snd_pcm_hw_params_any(p, hw)) < 0) { paso = "hw_params_any"; error = r; return null; }
                if ((r = snd_pcm_hw_params_set_access(p, hw, AccesoIntercalado)) < 0) { paso = "set_access"; error = r; return null; }
                int fmt = -1;
                foreach (int f in formatos) if (snd_pcm_hw_params_test_format(p, hw, f) == 0) { fmt = f; break; }
                if (fmt < 0) { paso = "set_format (ninguno de los pedidos)"; error = -EINVAL; return null; }
                if ((r = snd_pcm_hw_params_set_format(p, hw, fmt)) < 0) { paso = "set_format"; error = r; return null; }
                if ((r = snd_pcm_hw_params_set_channels(p, hw, (uint)canales)) < 0) { paso = "set_channels"; error = r; return null; }
                snd_pcm_hw_params_set_rate_resample(p, hw, remuestrear ? 1u : 0u);
                if ((r = snd_pcm_hw_params_set_rate(p, hw, (uint)tasa, 0)) < 0) { paso = "set_rate " + tasa; error = r; return null; }
                nuint per = (nuint)Math.Max(16, periodo), buf = (nuint)Math.Max(periodo * 2, bufer);
                snd_pcm_hw_params_set_period_size_near(p, hw, ref per, IntPtr.Zero);
                snd_pcm_hw_params_set_buffer_size_near(p, hw, ref buf);
                if ((r = snd_pcm_hw_params(p, hw)) < 0) { paso = "hw_params"; error = r; return null; }
                snd_pcm_hw_params_get_buffer_size(hw, out buf);
                snd_pcm_hw_params_get_period_size(hw, out per, IntPtr.Zero);
                if ((r = snd_pcm_sw_params_malloc(out sw)) < 0) { paso = "sw_params_malloc"; error = r; return null; }
                if ((r = snd_pcm_sw_params_current(p, sw)) < 0) { paso = "sw_params_current"; error = r; return null; }
                // la reproduccion arranca cuando el bufer esta lleno (los 0,5 s de ceros del principio lo llenan); la captura, al pedirlo
                snd_pcm_sw_params_set_start_threshold(p, sw, captura ? 1 : buf);
                snd_pcm_sw_params_set_avail_min(p, sw, per);
                if ((r = snd_pcm_sw_params(p, sw)) < 0) { paso = "sw_params"; error = r; return null; }
                SalidaNuevaPcm s = new SalidaNuevaPcm(p, nombre, captura, fmt, tasa, canales, (int)per, (int)buf);
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

        // Cuantos cuadros caben (o hay, en la captura). Un XRUN se recupera (se cuenta) y vale como «todo libre». <0: el error (la tarjeta se
        // fue: ENODEV, EIO, EBADFD...).
        internal long Libres()
        {
            nint a = snd_pcm_avail(pcm);
            if (a == -EPIPE || a == -ESTRPIPE)
            {
                Xruns++;
                int r = snd_pcm_recover(pcm, (int)a, 1);
                if (r < 0) return r;
                if (Captura) snd_pcm_start(pcm);
                a = snd_pcm_avail(pcm);
            }
            return a;
        }
        // Espera a que haya sitio (o datos) como mucho ms. >0 listo, 0 se acabo el tiempo, <0 error.
        internal int Espera(int ms)
        {
            int r = snd_pcm_wait(pcm, ms);
            if (r == -EPIPE || r == -ESTRPIPE) { Xruns++; int q = snd_pcm_recover(pcm, r, 1); if (q < 0) return q; if (Captura) snd_pcm_start(pcm); return 1; }
            return r;
        }
        // Escribe esos cuadros (sin espera: lo que quepa). Devuelve los escritos o el error (<0). Un XRUN se recupera y se reintenta una vez.
        internal long Escribe(byte* d, int cuadros)
        {
            nint r = snd_pcm_writei(pcm, d, (nuint)cuadros);
            if (r == -EPIPE || r == -ESTRPIPE)
            {
                Xruns++;
                int q = snd_pcm_recover(pcm, (int)r, 1);
                if (q < 0) return q;
                r = snd_pcm_writei(pcm, d, (nuint)cuadros);
            }
            if (r == -EAGAIN) return 0;
            return r;
        }
        internal long Lee(byte* d, int cuadros)
        {
            nint r = snd_pcm_readi(pcm, d, (nuint)cuadros);
            if (r == -EPIPE || r == -ESTRPIPE)
            {
                Xruns++;
                int q = snd_pcm_recover(pcm, (int)r, 1);
                if (q < 0) return q;
                snd_pcm_start(pcm);
                return 0;
            }
            if (r == -EAGAIN) return 0;
            return r;
        }
        internal int Arranca() { return snd_pcm_start(pcm); }
        // Lo que la tarjeta aun tiene por delante (en la reproduccion); -1 si no se sabe
        internal long Retraso()
        {
            nint d;
            int r = snd_pcm_delay(pcm, out d);
            if (r < 0) return -1;
            return Math.Max(0, (long)d);
        }
        internal bool EnXrun { get { return snd_pcm_state(pcm) == EstadoXrun; } }
        internal bool Corriendo { get { return snd_pcm_state(pcm) == EstadoCorriendo; } }

        public void Dispose()
        {
            IntPtr p = pcm; pcm = IntPtr.Zero;
            if (p == IntPtr.Zero) return;
            try { snd_pcm_drop(p); } catch { }
            try { snd_pcm_close(p); } catch { }
        }
    }
}

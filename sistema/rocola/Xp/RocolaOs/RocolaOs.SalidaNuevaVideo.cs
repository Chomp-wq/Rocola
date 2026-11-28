// ROCOLA OS (cadena de audio nueva; CADENA_NUEVA.md §4, decision D11 «los videos por la rocola»; CADENA_NUEVA_salida.md): EL SONIDO DE LOS
// VIDEOS POR LA SALIDA NUEVA. En XP (y en rocola-xp sin la salida nueva) mpv abre la tarjeta por su cuenta y la cierra al acabar el video:
// con la tarjeta siempre abierta por la rocola eso no puede ser (hw: es de uno solo), y ademas cada video la cerraba y la abria.
//
// LA VIA (sin paquetes nuevos en el sistema: mpv no trae PulseAudio y rocola-pulso es solo para Soloist): el «file» de ALSA, que viene en
// alsa-lib (pcm.file de alsa.conf: FILE y FORMAT, con «null» detras). mpv le manda su sonido en coma flotante, a dos canales y a su
// frecuencia (audio-format=float, audio-channels=stereo) y ese «file» lo escribe con su cabecera WAV en una TUBERIA de /run que la rocola
// tiene abierta siempre (la crea al arrancar; mpv no espera nunca a que alguien la lea). La rocola la lee en su hilo, solo lo que cabe en un
// anillo de unos 60 ms (asi la tuberia se llena y mpv espera: el ritmo lo pone la tarjeta, como con una de verdad), la convierte a 44,1 kHz
// con el sinc de XP (una fuente del motor como cualquier otra: RocolaOs.SalidaNuevaCliente.cs) y la saca por el mismo flujo. El «null» no
// tiene reloj y mpv cree que lo que escribe ya suena: lo que de verdad tarda (el bufer de la tarjeta, el anillo, la tuberia de 4 KB y el
// bufer de 20 ms de ALSA, unos 270 ms) se le dice con audio-delay (negativo: retrasa la imagen) para que boca y voz vayan juntas.
//
// LA GANANCIA, FIJA POR VIDEO (como las canciones de la cadena nueva, §1.2): g = min(T - L, C - TP) con L su sonoridad integrada y TP su pico
// real, medidos UNA vez de fondo con el ebur128 de ffmpeg (prioridad minima; rocola_videos_nivel.txt: la ruta, su tamano y fecha y los dos
// numeros) en cuanto se pide (y los de la biblioteca, poco a poco). T: la de salida_nueva_video_lufs= si el dueno la cambio; si no, la de la
// cadena nueva (nivel_lufs=) si esta encendida; si no, la sonoridad tipica de la cadena de hoy ya nivelada (la mediana de tus canciones
// medidas, la misma del nivelado de Spotify); sin eso, -18. C: techo_dbtp= de la cadena nueva o -1 dBTP. Hasta +12 dB como mucho. Se decide al
// empezar el video y no cambia (sin medir todavia: la regla de hoy, la perilla que XP le ponia a mpv). Encima, el modo noche como la musica
// (FactorNoche, con su rampa) y el fundido del final de XP; mpv se queda en 100 (y su filtro de noche, noche_video, sigue igual).
//
// SI NO SE PUEDE (salida_nueva_videos=0, o no se pudo hacer la tuberia): mpv abre la tarjeta como siempre y la rocola se la SUELTA mientras
// dura el video (al cargarlo) y la vuelve a abrir al acabar (con sus 0,5 s de ceros).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static unsafe partial class SalidaNueva
    {
        // ---------- la tuberia ----------
        [DllImport("libc", SetLastError = true)] private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string ruta, uint modo);
        [DllImport("libc", EntryPoint = "open", SetLastError = true)] private static extern int AbreFd([MarshalAs(UnmanagedType.LPUTF8Str)] string ruta, int banderas);
        [DllImport("libc", EntryPoint = "read", SetLastError = true)] private static extern nint LeeFd(int fd, byte* b, nint n);
        [DllImport("libc", EntryPoint = "close")] private static extern int CierraFd(int fd);
        [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)] private static extern int Fcntl(int fd, int orden, int arg);
        [DllImport("libc", EntryPoint = "poll", SetLastError = true)] private static extern int Poll(PollFd* fds, nuint n, int ms);
        [StructLayout(LayoutKind.Sequential)] private struct PollFd { public int Fd; public short Eventos, Hubo; }
        private const int O_RDONLY = 0, O_NONBLOCK = 0x800, O_CLOEXEC = 0x80000, F_SETPIPE_SZ = 1031, POLLIN = 1, POLLHUP = 0x10, EAGAIN = 11;
        internal const int VideoAnilloMs = 60, TuberiaBytes = 4096, AlsaVideoMs = 20;
        internal static string TuberiaVideo = "";
        internal static volatile float GananciaVideo = 1f;
        internal static long VideoCuadros, VideoFlujos;
        internal static string VideoFormato = "";
        internal static volatile bool SoltadaParaVideo;

        // LA TUBERIA Y SU HILO (al arrancar). false si no se pudo (entonces el camino de soltar la tarjeta)
        internal static bool PreparaTuberia(string ruta)
        {
            try
            {
                if (File.Exists(ruta) || Directory.Exists(ruta)) { try { File.Delete(ruta); } catch { } }
                if (mkfifo(ruta, 0x180 /* 0600 */) != 0) { RocolaOs.Apunta(NombreFuncion, "no se pudo hacer la tuberia del sonido de los videos (" + ruta + ", errno " + Marshal.GetLastWin32Error() + ")"); return false; }
                int fd = AbreLectura(ruta);
                if (fd < 0) { RocolaOs.Apunta(NombreFuncion, "no se pudo abrir la tuberia del sonido de los videos (errno " + Marshal.GetLastWin32Error() + ")"); return false; }
                TuberiaVideo = ruta;
                RocolaOs.Hilo("salida-nueva-video", ThreadPriority.AboveNormal, delegate { LeeVideo(ruta, fd); });
                return true;
            }
            catch (Exception ex) { RocolaOs.Falla(NombreFuncion, "la tuberia de los videos", ex); return false; }
        }
        private static int AbreLectura(string ruta)
        {
            int fd = AbreFd(ruta, O_RDONLY | O_NONBLOCK | O_CLOEXEC);
            if (fd >= 0) Fcntl(fd, F_SETPIPE_SZ, TuberiaBytes);   // (una pagina: lo que espera en la tuberia, unos 11 ms)
            return fd;
        }
        // Lo que tarda en oirse lo que mpv escribe (para su audio-delay): la tarjeta, el anillo (de media), la tuberia y el bufer de ALSA
        internal static double RetrasoVideoSeg { get { return (RetrasoTarjetaMs + VideoAnilloMs * 0.75 + TuberiaBytes * 1000.0 / (Tasa * 8) + AlsaVideoMs) / 1000.0; } }

        // EL HILO QUE LEE LA TUBERIA: la cabecera WAV de cada flujo (cada vez que mpv abre su sonido) y lo que sigue, a una fuente del motor
        private static void LeeVideo(string ruta, int fd)
        {
            HilosRocola.Nombra("audio (video por la rocola)");
            byte[] b = new byte[65536];
            byte[] cab = new byte[256]; int nCab = 0;
            byte[] resto = new byte[64]; int nResto = 0;
            SalidaNuevaCliente cli = null; int bpc = 0;
            PollFd p;
            while (!parar)
            {
                p.Fd = fd; p.Eventos = POLLIN; p.Hubo = 0;
                int r = Poll(&p, 1, 100);
                if (cli != null) cli.Ganancia = GananciaVideo;
                if (r <= 0 || (p.Hubo & (POLLIN | POLLHUP)) == 0) continue;
                // cuanto leer: con la fuente, solo lo que cabe en su anillo de ~60 ms (el resto espera en la tuberia y mpv tambien)
                int quiere;
                if (cli == null) quiere = Math.Max(1, Math.Min(cab.Length - nCab, 44 - nCab > 0 ? 44 - nCab : 1));
                else
                {
                    long sitio = (long)Tasa * VideoAnilloMs / 1000 - cli.EnElAnillo;
                    if (sitio < Tasa / 200 && (p.Hubo & POLLIN) != 0) { Thread.Sleep(4); continue; }
                    quiere = (int)Math.Max(bpc, Math.Min(b.Length, sitio * cli.TasaIn / Tasa * bpc));
                }
                nint n;
                fixed (byte* q = b) n = LeeFd(fd, q, quiere);
                if (n < 0) { if (Marshal.GetLastWin32Error() == EAGAIN) continue; Thread.Sleep(20); continue; }
                if (n == 0)
                {
                    // mpv cerro su sonido (acabo el video, se salto o cambio de formato): lo que queda suena y se apaga en 5 ms; la tuberia se vuelve
                    // a abrir (sin escritor no se queda avisando de que se cerro)
                    if (cli != null) { cli.AcabaYSuelta(); cli = null; }
                    nCab = 0; nResto = 0;
                    CierraFd(fd);
                    fd = AbreLectura(ruta);
                    if (fd < 0) { RocolaOs.Apunta(NombreFuncion, "la tuberia de los videos no se pudo volver a abrir: los videos se quedan sin sonido hasta el siguiente arranque"); return; }
                    continue;
                }
                int i = 0;
                if (cli == null)
                {
                    // la cabecera (la de alsa-lib: RIFF, fmt de 16 y data; se lee en general por si acaso)
                    while (i < n && nCab < cab.Length) cab[nCab++] = b[i++];
                    int datos; WaveFormat f = LeeCabecera(cab, nCab, out datos);
                    if (f == null) { if (nCab >= cab.Length) nCab = 0; continue; }
                    // lo que sobro de la cabecera ya es sonido
                    int sobra = nCab - datos;
                    bpc = f.BlockAlign;
                    try { cli = new SalidaNuevaCliente("video", f, Math.Max(1, f.SampleRate / 50), 10); }
                    catch (Exception ex) { RocolaOs.Falla(NombreFuncion, "el sonido de un video (" + f + ")", ex); nCab = 0; continue; }
                    cli.Ganancia = GananciaVideo; cli.GananciaActual = GananciaVideo;
                    Enchufa(cli);
                    VideoFlujos++;
                    VideoFormato = f.Encoding + " " + f.BitsPerSample + " bits, " + f.Channels + " canales, " + f.SampleRate + " Hz";
                    RocolaOs.Apunta(NombreFuncion, "sonido de un video por la rocola: " + VideoFormato);
                    if (sobra > 0) { Buffer.BlockCopy(cab, datos, b, 0, sobra); Buffer.BlockCopy(b, i, b, sobra, (int)n - i); n = sobra + (n - i); i = 0; }
                    else if (i >= n) continue;
                    nCab = 0;
                }
                // cuadros enteros (lo que sobra de un cuadro va al siguiente)
                int disp = (int)n - i;
                byte[] todo = b; int desde = i;
                if (nResto > 0)
                {
                    byte[] j = new byte[nResto + disp];
                    Buffer.BlockCopy(resto, 0, j, 0, nResto); Buffer.BlockCopy(b, i, j, nResto, disp);
                    todo = j; desde = 0; disp += nResto; nResto = 0;
                }
                int enteros = disp - disp % bpc;
                if (enteros > 0)
                {
                    byte[] bloque = desde == 0 ? todo : null;
                    if (bloque == null) { bloque = new byte[enteros]; Buffer.BlockCopy(todo, desde, bloque, 0, enteros); }
                    try { cli.EscribeDeVideo(bloque, enteros); VideoCuadros += enteros / bpc; }
                    catch (Exception ex) { RocolaOs.Falla(NombreFuncion, "el sonido de un video", ex); }
                }
                nResto = disp - enteros;
                if (nResto > 0) Buffer.BlockCopy(todo, desde + enteros, resto, 0, nResto);
            }
            try { CierraFd(fd); } catch { }
        }
        // la cabecera WAV: el formato y donde empieza el sonido (null si aun no esta entera). alsa-lib pone 1 (PCM) aunque sea coma flotante:
        // con 32 bits es coma flotante (a mpv se le pide audio-format=float)
        private static WaveFormat LeeCabecera(byte[] c, int n, out int datos)
        {
            datos = 0;
            if (n < 12) return null;
            if (c[0] != 'R' || c[1] != 'I' || c[2] != 'F' || c[3] != 'F' || c[8] != 'W' || c[9] != 'A' || c[10] != 'V' || c[11] != 'E') { datos = 0; return null; }
            int o = 12, canales = 0, tasa = 0, bits = 0, tag = 0;
            while (o + 8 <= n)
            {
                string id = Encoding.ASCII.GetString(c, o, 4);
                int largo = BitConverter.ToInt32(c, o + 4);
                if (id == "data") { datos = o + 8; break; }
                if (o + 8 + largo > n) return null;
                if (id == "fmt " && largo >= 16)
                {
                    tag = BitConverter.ToUInt16(c, o + 8); canales = BitConverter.ToUInt16(c, o + 10); tasa = BitConverter.ToInt32(c, o + 12); bits = BitConverter.ToUInt16(c, o + 22);
                }
                o += 8 + largo + (largo & 1);
            }
            if (datos == 0 || canales <= 0 || tasa <= 0) return null;
            if (bits == 32 && (tag == 1 || tag == 3 || tag == 0xFFFE)) return WaveFormat.CreateIeeeFloatWaveFormat(tasa, canales);
            return new WaveFormat(tasa, bits, canales);
        }

        // SOLTAR LA TARJETA MIENTRAS DURA UN VIDEO (sin videos por la rocola): el motor la cierra (se espera como mucho 600 ms) y la vuelve a
        // abrir al soltar el video
        internal static void SueltaParaVideo()
        {
            if (SoltadaParaVideo) return;
            SoltadaParaVideo = true;
            long hasta = Environment.TickCount64 + 600;
            while (Abierta && Environment.TickCount64 < hasta) Thread.Sleep(5);
        }
        internal static void RetomaTrasVideo() { SoltadaParaVideo = false; }
    }

    public partial class Form1
    {
        // ================================================================== AL ARRANCAR Y EN CADA LATIDO
        internal void RocolaOsSalidaNuevaArrancaVideo()
        {
            RocolaOsVideoNivelCarga();
            bool porLaRocola = RocolaOs.AjusteSiNo(SalidaNueva.ClaveVideos) && SalidaNueva.PreparaTuberia(RocolaOs.Run("rocola-xp-video.pcm"));
            SalidaNueva.VideosPorLaRocola = porLaRocola;
            if (porLaRocola)
            {
                double retraso = SalidaNueva.RetrasoVideoSeg;
                string tuberia = SalidaNueva.TuberiaVideo;
                ReproductorVideo.RocolaOsAudio = m =>
                {
                    Mpv.mpv_set_option_string(m, "ao", "alsa");
                    Mpv.mpv_set_option_string(m, "audio-device", "alsa/file:FILE=" + tuberia + ",FORMAT=wav");
                    Mpv.mpv_set_option_string(m, "alsa-buffer-time", (SalidaNueva.AlsaVideoMs * 1000).ToString(CultureInfo.InvariantCulture));
                    Mpv.mpv_set_option_string(m, "audio-format", "float");
                    Mpv.mpv_set_option_string(m, "audio-channels", "stereo");
                    Mpv.mpv_set_option_string(m, "audio-delay", (-retraso).ToString("0.000", CultureInfo.InvariantCulture));
                };
                ReproductorVideo.RocolaOsVolumen = (v, vol) => RocolaOsSalidaNuevaVolumenVideo(v, vol);
                RocolaOs.Apunta(SalidaNueva.NombreFuncion, "videos por la rocola (tuberia " + tuberia + "; a mpv audio-delay -" + retraso.ToString("0.000", CultureInfo.InvariantCulture) + " s)");
            }
            else
            {
                ReproductorVideo.RocolaOsAntesDeCargar = v => SalidaNueva.SueltaParaVideo();
                ReproductorVideo.RocolaOsSoltado = v => SalidaNueva.RetomaTrasVideo();
                RocolaOs.Apunta(SalidaNueva.NombreFuncion, "videos con mpv directo a la tarjeta: la salida nueva la suelta mientras dura cada video");
            }
            RocolaOs.Hilo("salida-nueva-mide-videos", RocolaOsVideoNivelMide);
        }
        private long rocolaOsVideoBarrido;
        internal void RocolaOsSalidaNuevaLatidoVideo()
        {
            // los de la biblioteca, poco a poco (una lista cada 10 minutos; el hilo mide uno detras de otro, de fondo)
            long ahora = Environment.TickCount64;
            if (ahora - rocolaOsVideoBarrido < 600000 && rocolaOsVideoBarrido != 0) return;
            if (rocolaOsVideoBarrido == 0 && ahora - RocolaOs.SalidaArrancoMs < 120000) return;   // (no en los 2 primeros minutos)
            rocolaOsVideoBarrido = ahora;
            foreach (string r in RocolaOsTodosLosVideos()) RocolaOsVideoNivelPide(r, false);
        }
        // los videos de la biblioteca (los de la letra de los videos de XP: cada disco con sus VideoFiles)
        internal List<string> RocolaOsTodosLosVideos()
        {
            List<string> l = new List<string>();
            List<Album> va = videoAlbums;
            if (va != null) foreach (Album a in va.ToArray()) if (a != null && a.VideoFiles != null) foreach (string v in a.VideoFiles.ToArray()) if (v != null) l.Add(v);
            return l;
        }

        // ================================================================== LA GANANCIA DEL VIDEO
        private string rocolaOsVideoDecidido; private double rocolaOsVideoDb = double.NaN;
        // el gancho de ReproductorVideo.AplicaVolumen: el volumen de XP (0 a 100, con el modo noche y el fundido del final) -> la ganancia en la
        // rocola. true: mpv se queda en 100.
        private bool RocolaOsSalidaNuevaVolumenVideo(ReproductorVideo v, int volumen)
        {
            string ruta = v != null && v.Archivo.Length > 0 ? v.Archivo : (nowPlaying != null && nowPlaying.IsVideo ? nowPlaying.VideoPath : null);
            if (ruta == null) { SalidaNueva.GananciaVideo = volumen / 100f; return true; }
            if (ruta != rocolaOsVideoDecidido)
            {
                rocolaOsVideoDecidido = ruta;
                double l, tp;
                if (RocolaOsVideoNivel(ruta, out l, out tp))
                {
                    double t = RocolaOsVideoObjetivo(), c = RocolaOsVideoTecho();
                    rocolaOsVideoDb = Math.Min(12.0, Math.Min(t - l, c - tp));
                    RocolaOs.Apunta(SalidaNueva.NombreFuncion, "video " + Path.GetFileName(ruta) + ": " + F(l) + " LUFS, pico " + F(tp) + " dBTP: ganancia fija " + F(rocolaOsVideoDb)
                        + " dB (a " + F(t) + " LUFS, techo " + F(c) + " dBTP)");
                }
                else
                {
                    rocolaOsVideoDb = double.NaN;
                    RocolaOsVideoNivelPide(ruta, true);
                    RocolaOs.Apunta(SalidaNueva.NombreFuncion, "video " + Path.GetFileName(ruta) + ": sin medir aun (se mide de fondo): la regla de hoy");
                }
            }
            float g;
            if (double.IsNaN(rocolaOsVideoDb)) g = volumen / 100f;   // la regla de hoy: la perilla de XP (con su noche y su fundido)
            else
            {
                int baseXp = VideoVolumeFor(ruta);
                double fundido = baseXp > 0 ? Math.Max(0.0, Math.Min(1.0, volumen / (double)baseXp)) : (volumen > 0 ? 1.0 : 0.0);
                g = (float)(Math.Pow(10.0, rocolaOsVideoDb / 20.0) * FactorNoche() * fundido);
            }
            SalidaNueva.GananciaVideo = g;
            return true;
        }
        private static string F(double x) { return x.ToString("0.0", CultureInfo.InvariantCulture); }
        // T: lo que el dueno puso; si no, la cadena nueva; si no, la sonoridad tipica de la cadena de hoy ya nivelada; si no, -18
        private double RocolaOsVideoObjetivo()
        {
            if (RocolaOs.AjusteCambiado(SalidaNueva.ClaveVideoLufs)) return RocolaOs.AjusteEntero(SalidaNueva.ClaveVideoLufs);
            double v;
            RocolaOsClave k = RocolaOs.ClaveDe("nivel_lufs");
            if (k != null && RocolaOs.Encendida(k.Duena) && double.TryParse(RocolaOs.Ajuste("nivel_lufs"), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            try { double o = RocolaOsSpotifyNivelaObjetivo(); if (!double.IsNaN(o) && o > -40 && o < 0) return o; } catch { }
            return -18.0;
        }
        private double RocolaOsVideoTecho()
        {
            double v;
            RocolaOsClave k = RocolaOs.ClaveDe("techo_dbtp");
            if (k != null && RocolaOs.Encendida(k.Duena) && double.TryParse(RocolaOs.Ajuste("techo_dbtp"), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return -1.0;
        }

        // ================================================================== LA MEDIDA DE LOS VIDEOS (ffmpeg ebur128, de fondo)
        private sealed class RocolaOsNivelVideo { internal long Bytes, Fecha; internal double Lufs, Pico; internal bool Fallo; }
        private readonly Dictionary<string, RocolaOsNivelVideo> rocolaOsVideoNiveles = new Dictionary<string, RocolaOsNivelVideo>(StringComparer.Ordinal);
        private readonly BlockingCollection<string> rocolaOsVideoPendientes = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private readonly ConcurrentQueue<string> rocolaOsVideoUrgentes = new ConcurrentQueue<string>();
        private readonly HashSet<string> rocolaOsVideoEnCola = new HashSet<string>(StringComparer.Ordinal);
        internal static int RocolaOsVideosMedidos;
        private const string ArchivoVideoNivel = "rocola_videos_nivel.txt";

        private bool RocolaOsVideoNivel(string ruta, out double lufs, out double pico)
        {
            lufs = pico = double.NaN;
            RocolaOsNivelVideo n;
            lock (rocolaOsVideoNiveles) if (!rocolaOsVideoNiveles.TryGetValue(ruta, out n)) return false;
            long b, f;
            if (!Firma(ruta, out b, out f) || b != n.Bytes || f != n.Fecha || n.Fallo) return false;
            lufs = n.Lufs; pico = n.Pico;
            return !double.IsNaN(lufs) && !double.IsInfinity(lufs) && lufs > -70;
        }
        private static bool Firma(string ruta, out long bytes, out long fecha)
        {
            bytes = fecha = 0;
            try { FileInfo i = new FileInfo(ruta); if (!i.Exists) return false; bytes = i.Length; fecha = i.LastWriteTimeUtc.Ticks; return true; } catch { return false; }
        }
        private void RocolaOsVideoNivelPide(string ruta, bool urgente)
        {
            if (string.IsNullOrEmpty(ruta)) return;
            double l, p; if (RocolaOsVideoNivel(ruta, out l, out p)) return;
            lock (rocolaOsVideoNiveles)
            {
                RocolaOsNivelVideo n; long b, f;
                if (rocolaOsVideoNiveles.TryGetValue(ruta, out n) && n.Fallo && Firma(ruta, out b, out f) && b == n.Bytes && f == n.Fecha) return;   // (no se pudo: no se reintenta si no cambia)
            }
            lock (rocolaOsVideoEnCola) { if (!rocolaOsVideoEnCola.Add(ruta)) { if (urgente) rocolaOsVideoUrgentes.Enqueue(ruta); return; } }
            if (urgente) rocolaOsVideoUrgentes.Enqueue(ruta);
            rocolaOsVideoPendientes.Add(ruta);
        }
        private void RocolaOsVideoNivelMide()
        {
            while (!RocolaOs.Cerrandose)
            {
                string ruta;
                if (!rocolaOsVideoUrgentes.TryDequeue(out ruta))
                {
                    if (!rocolaOsVideoPendientes.TryTake(out ruta, 2000)) continue;
                }
                lock (rocolaOsVideoEnCola) { if (!rocolaOsVideoEnCola.Remove(ruta)) continue; }   // (ya se midio por la otra cola)
                double l0, p0; if (RocolaOsVideoNivel(ruta, out l0, out p0)) continue;
                long b, f; if (!Firma(ruta, out b, out f)) continue;
                double lufs, pico; string porque;
                bool bien = MideConFfmpeg(ruta, out lufs, out pico, out porque);
                lock (rocolaOsVideoNiveles) rocolaOsVideoNiveles[ruta] = new RocolaOsNivelVideo { Bytes = b, Fecha = f, Lufs = lufs, Pico = pico, Fallo = !bien };
                if (bien) RocolaOsVideosMedidos++;
                RocolaOs.Apunta(SalidaNueva.NombreFuncion, "medido el video " + Path.GetFileName(ruta) + ": " + (bien ? F(lufs) + " LUFS, pico real " + F(pico) + " dBTP" : "no se pudo (" + porque + ")"));
                RocolaOsVideoNivelGuarda();
                Thread.Sleep(500);
            }
        }
        private static readonly System.Text.RegularExpressions.Regex reI = new System.Text.RegularExpressions.Regex(@"^\s*I:\s*(-?[0-9.]+|-inf)\s*LUFS", System.Text.RegularExpressions.RegexOptions.Multiline);
        private static readonly System.Text.RegularExpressions.Regex rePico = new System.Text.RegularExpressions.Regex(@"^\s*Peak:\s*(-?[0-9.]+|-inf)\s*dBFS", System.Text.RegularExpressions.RegexOptions.Multiline);
        internal static bool MideConFfmpeg(string ruta, out double lufs, out double pico, out string porque)
        {
            lufs = pico = double.NaN; porque = "";
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                string af = vuelta == 0 ? "ebur128=peak=true:framelog=quiet" : "ebur128=peak=true";
                ProcessStartInfo psi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                foreach (string a in new[] { "-nostdin", "-hide_banner", "-nostats", "-i", ruta, "-map", "0:a:0", "-vn", "-sn", "-dn", "-ac", "2", "-af", af, "-f", "null", "-" }) psi.ArgumentList.Add(a);
                string err;
                try
                {
                    using (Process p = Process.Start(psi))
                    {
                        System.Threading.Tasks.Task<string> e = p.StandardError.ReadToEndAsync();
                        p.StandardOutput.ReadToEndAsync();
                        if (!p.WaitForExit(180000)) { try { p.Kill(true); } catch { } porque = "ffmpeg tardo mas de 3 min"; return false; }
                        err = e.Wait(5000) ? e.Result : "";
                    }
                }
                catch (Exception ex) { porque = "no se pudo correr ffmpeg (" + ex.Message + ")"; return false; }
                int s = err.LastIndexOf("Summary:", StringComparison.Ordinal);
                if (s < 0) { porque = "ffmpeg no dio la medida" + (err.IndexOf("does not contain any stream", StringComparison.Ordinal) >= 0 || err.IndexOf("matches no streams", StringComparison.Ordinal) >= 0 ? " (el video no tiene sonido)" : ""); continue; }
                string resumen = err.Substring(s);
                System.Text.RegularExpressions.Match mi = reI.Match(resumen), mp = rePico.Match(resumen);
                if (!mi.Success) { porque = "sin la sonoridad en la medida"; return false; }
                lufs = mi.Groups[1].Value == "-inf" ? double.NegativeInfinity : double.Parse(mi.Groups[1].Value, CultureInfo.InvariantCulture);
                pico = !mp.Success ? 0.0 : mp.Groups[1].Value == "-inf" ? -120.0 : double.Parse(mp.Groups[1].Value, CultureInfo.InvariantCulture);
                if (double.IsInfinity(lufs) || lufs < -70) { porque = "el sonido del video es silencio"; return false; }
                return true;
            }
            return false;
        }
        private void RocolaOsVideoNivelCarga()
        {
            try
            {
                string[] ls = RocolaOsLeeLineas(RocolaOs.Datos(ArchivoVideoNivel));
                if (ls == null) return;
                lock (rocolaOsVideoNiveles)
                    foreach (string l in ls)
                    {
                        string[] c = l.Split('\t');
                        if (c.Length < 5 || l.StartsWith("#", StringComparison.Ordinal)) continue;
                        RocolaOsNivelVideo n = new RocolaOsNivelVideo();
                        if (!long.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n.Bytes) || !long.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n.Fecha)) continue;
                        n.Fallo = c[3] == "-";
                        if (!n.Fallo && (!double.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out n.Lufs) || !double.TryParse(c[4], NumberStyles.Float, CultureInfo.InvariantCulture, out n.Pico))) continue;
                        rocolaOsVideoNiveles[c[0]] = n;
                        if (!n.Fallo) RocolaOsVideosMedidos++;
                    }
            }
            catch (Exception ex) { RocolaOs.Falla(SalidaNueva.NombreFuncion, "leer " + ArchivoVideoNivel, ex); }
        }
        private void RocolaOsVideoNivelGuarda()
        {
            try
            {
                StringBuilder sb = new StringBuilder("# la sonoridad de los videos para la salida nueva: ruta, bytes, fecha, LUFS integrados y pico real (dBTP) medidos con ffmpeg ebur128; «-» = no se pudo\n");
                lock (rocolaOsVideoNiveles)
                    foreach (KeyValuePair<string, RocolaOsNivelVideo> kv in rocolaOsVideoNiveles)
                        sb.Append(kv.Key.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(kv.Value.Bytes.ToString(CultureInfo.InvariantCulture)).Append('\t')
                          .Append(kv.Value.Fecha.ToString(CultureInfo.InvariantCulture)).Append('\t')
                          .Append(kv.Value.Fallo ? "-" : kv.Value.Lufs.ToString("0.00", CultureInfo.InvariantCulture)).Append('\t')
                          .Append(kv.Value.Fallo ? "-" : kv.Value.Pico.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
                RocolaOsEscribeSeguro(RocolaOs.Datos(ArchivoVideoNivel), sb.ToString());
            }
            catch (Exception ex) { RocolaOs.Falla(SalidaNueva.NombreFuncion, "guardar " + ArchivoVideoNivel, ex); }
        }
    }
}

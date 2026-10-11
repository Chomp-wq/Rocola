// EL VIDEO: reproduccion, pantalla completa y la limpieza de los que no suenan
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
#if ROCOLA_GPU
        private string paginaGpuClave, paginaGpuFirma;
        private int paginaGpuGeneracion;
#endif
        private Dictionary<string, int> videoCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private const int VideoStatCap = 1000, VideoStatTarget = 900;
        private Dictionary<string, long> videoSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private List<Album> videoAlbums = new List<Album>();   // discos que tienen archivo(s) de video
        private const char VideoMark = '\u25BA';   // "letra" especial = seccion VIDEOS
        private System.Windows.Forms.Timer videoPoll;
        private bool videoOn = false;              // hay un video cargado/reproduciendose
        // VOLUMEN DE LOS VIDEOS: el video lo reproduce Windows Media Player, cuyo sonido va DIRECTO a la
        // tarjeta sin pasar por la cadena de audio de la rocola (nivelado, limitador, EQ...). Por eso los
        // videos entraban a un volumen distinto al de las canciones ya niveladas. Esta perilla (Ctrl+F3) es
        // la BASE con la que suenan; ademas se le aplican los dB de "Subir TODO" para que sigan al resto.
        private int videoVol = 85;                 // 0..100, base del volumen de los videos ("video_vol=" en config)
        private int videoVolSet = -1;              // ultimo volumen puesto al WMP (para no repetir llamadas COM en cada sondeo)
        private bool videoAuto = true;             // "video_automatico=": el volumen base de los videos lo elige el programa
        private int videoBaseAuto = -1;            // lo que eligio (-1 = aun sin calcular)
        private int videoBaseFirma = -1;           // con que datos lo calculo (para rehacerlo cuando cambien)
        private volatile bool videoSinMedir = Environment.OSVersion.Version.Major < 6; // Media Foundation no existe antes de Vista
        private double videoLastPos = -1;          // ultima posicion del video (para detectar si NO avanza)
        private int videoProgressTick = 0;         // momento del ultimo avance real (vigia de video colgado)
        private bool videoNudged = false;          // ya se intento el empujon de rescate (play explicito)
        private double videoMaxPos = 0;            // posicion maxima alcanzada (saber si el video SI se reprodujo)
        private bool videoFull = false;            // video en pantalla completa (tras inactividad)
        private bool videoGpuOn = true;   // "video_gpu=" en config: interfaz por GPU tambien con video sonando
        private static readonly string[] VideoExt = { ".mp4", ".avi", ".wmv", ".mpg", ".mpeg", ".mov", ".mkv", ".flv", ".m4v", ".vob", ".divx", ".3gp", ".asf" };

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

        // ===================  VIDEO (Windows Media Player ActiveX, por codigo)  ===================

        // Pantalla completa del video tras inactividad (para apreciarlo mejor). Deja una franja
        // arriba para el aviso "toca una tecla" (asi no encimamos nada sobre el ActiveX del video).
        private void EnterVideoFull()
        {
            if (videoFull || !videoOn || video == null) return;
            videoFull = true;
            LayoutControls();
            video.BringToFront();
            if (lblAttract != null) { lblAttract.Visible = true; lblAttract.BringToFront(); }
        }
        private void ExitVideoFull()
        {
            if (!videoFull) return;
            videoFull = false;
            if (lblAttract != null) lblAttract.Visible = false;
            LayoutControls();
            this.Invalidate();
        }

        // EL VOLUMEN BASE DE LOS VIDEOS, ELEGIDO SOLO.
        //
        // El reproductor de Windows solo sabe BAJAR: su volumen va de 0 a 100 y el 100 es el archivo tal cual.
        // No hay forma de sacarle mas de lo que el video trae grabado. Asi que la unica manera de que todos
        // suenen IGUAL es bajar los fuertes hasta donde llegan los flojos, y para eso hace falta dejar hueco:
        // con el base en 85, un video solo puede subir un 18% -1.4 dB- antes de toparse con el 100, y uno
        // grabado 10 dB por debajo se queda tan bajo como estaba, que es justo lo que se oye en la rocola.
        //
        // Aqui se miran TODOS los videos ya medidos y se elige el base MAS ALTO con el que todavia alcanzan
        // casi todos. Se da por perdido el 15% mas flojo -por tres videos rarisimos no vale la pena bajar la
        // biblioteca entera- y nunca se baja del 40 (8 dB), que es lo mas que tiene sentido alejar los videos
        // de las canciones. Si aun no hay medidas (o el equipo no puede medir), manda la perilla y ya.
        private const float VideoAutoFuera = 0.15f;   // fraccion de videos que se da por perdida
        private const int VideoBaseMin = 40;          // -8 dB: lo mas que se acepta bajar el conjunto
        private int VideoBase()
        {
            if (!videoAuto || !normalizeOn) return videoVol;   // sin nivelado no hay nada que emparejar: manda la perilla
            int firma = loudCache.Count * 31 + videoVol;
            firma = firma * 31 + (int)(normTarget * 100000f);
            if (firma == videoBaseFirma && videoBaseAuto >= 0) return videoBaseAuto;
            videoBaseFirma = firma;
            List<float> nec = new List<float>();
            if (videoAlbums != null && normTarget > 0.0001f)
            {
                HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // el mismo video puede estar en dos discos
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    if (va == null || va.VideoFiles == null) continue;
                    for (int j = 0; j < va.VideoFiles.Count; j++)
                    {
                        string vp = va.VideoFiles[j]; float rms;
                        if (vp == null || !vistos.Add(vp)) continue;
                        if (!loudCache.TryGetValue(vp, out rms) || rms <= 0.0001f) continue;
                        float b = 100f * rms / normTarget;   // el base mas alto con el que ESTE video llega al objetivo
                        nec.Add((b > 100f) ? 100f : b);
                    }
                }
            }
            if (nec.Count < 8) { videoBaseAuto = videoVol; return videoBaseAuto; }   // sin medidas suficientes: la perilla
            nec.Sort();
            int v = (int)Math.Round(nec[(int)(nec.Count * VideoAutoFuera)]);
            if (v < VideoBaseMin) v = VideoBaseMin; else if (v > 100) v = 100;
            videoBaseAuto = v;
            return v;
        }
        private bool videoMeasurementsDirty = true;
        private int videoMeasuredCount;
        // Solo el hilo UI publica medidas y cambia la biblioteca.
        private void InvalidateVideoMeasurements()
        {
            videoMeasurementsDirty = true;
            videoBaseAuto = -1;
        }

        // Cuantos videos llevan medida (para poder decirlo en los ajustes).
        private int VideosMedidos()
        {
            if (!videoMeasurementsDirty) return videoMeasuredCount;
            int n = 0;
            if (videoAlbums == null) return 0;
            HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < videoAlbums.Count; i++)
            {
                Album va = videoAlbums[i];
                if (va == null || va.VideoFiles == null) continue;
                for (int j = 0; j < va.VideoFiles.Count; j++)
                {
                    string vp = va.VideoFiles[j]; float rms;
                    if (vp == null || !vistos.Add(vp)) continue;
                    if (loudCache.TryGetValue(vp, out rms) && rms > 0.0001f) n++;
                }
            }
            videoMeasuredCount = n; videoMeasurementsDirty = false;
            return n;
        }

        // Abre y reproduce un video en el reproductor de la derecha (lo llama PlayNext).
        // Volumen (0..100) con el que debe sonar un video: el base -la perilla, o lo que eligio el programa-,
        // mas los dB de "Subir TODO" (para que los videos sigan al resto), mas -si ese video ya fue medido-
        // el nivelado que lo lleva al MISMO volumen objetivo de las canciones.
        private int VideoVolumeFor(string path)
        {
            double v = VideoBase();
            float rms;
            if (normalizeOn && path != null && loudCache.TryGetValue(path, out rms) && rms > 0.0001f)
            {
                // NIVELADO. Este video esta medido, asi que se le pone el volumen justo para que suene como
                // los demas. Aqui "Subir TODO" NO entra, a proposito: el reproductor de Windows no pasa de
                // 100, o sea que ese de mas no lo puede dar, y lo unico que consigue es empujar los videos
                // contra el techo y deshacer el emparejado que se acaba de hacer. Con +6 dB de master, todos
                // los videos con algo de cuerpo se pegaban al 100 y volvian a sonar distinto entre si.
                double lv = normTarget / rms;
                if (lv < 0.15) lv = 0.15;        // no enterrar un video muy fuerte (max -16 dB)
                else if (lv > 4.0) lv = 4.0;     // ni intentar rescatar uno mudo (el tope real lo pone el 100 del WMP)
                v *= lv;
            }
            else v *= MasterLin();   // sin medida no hay nada que emparejar: que al menos siga a "Subir TODO"
            // EL MODO NOCHE VA AQUI FUERA, Y NO DENTRO DE MasterLin(). La rama de arriba -la del video YA
            // MEDIDO- se salta la ganancia general a proposito, asi que metiendolo alli esos videos, que en
            // una rocola usada son la mayoria, seguirian a todo volumen a las tres de la manana. Bajar si
            // se puede aunque subir no: el tope de 100 del WMP solo estorba hacia arriba.
            v *= FactorNoche();
            int r = (int)Math.Round(v);
            if (r < 0) r = 0; else if (r > 100) r = 100;   // el WMP no pasa de 100: un video muy bajito solo llega hasta ahi
            return r;
        }

        // Pone el volumen en el WMP solo si CAMBIO (el sondeo corre varias veces por segundo durante el fundido).
        private void SetVideoVolume(int v)
        {
            if (v == videoVolSet) return;
            videoVolSet = v;
            try
            {
                if (video == null) return;
                object ocx = video.GetOcxObj(); if (ocx == null) return;
                object settings = Get(ocx, "settings"); if (settings != null) Set(settings, "volume", v);
            }
            catch { }
        }

        private void StartVideoItem(string path)
        {
#if ROCOLA_GPU
            AbortDiscTrans(); AbortWinTrans();   // el video toma la pantalla: nada volando encima
#endif
            try
            {
                if (video == null)
                {
                    video = new VideoHost();
                    video.TabStop = false;
                    Controls.Add(video);
                    IntPtr forceHandle = video.Handle;   // fuerza la creacion del control ActiveX
                }
                video.Visible = !searchOpen;   // si el buscador esta abierto, el cuadro queda oculto (el audio sigue)
#if ROCOLA_GPU
                if (AsistenteVisible) video.Visible = false;
#endif

                object ocx = video.GetOcxObj();
                if (ocx == null) throw new Exception("control WMP nulo");

                Set(ocx, "uiMode", "none");
                Set(ocx, "stretchToFit", true);
                object settings = Get(ocx, "settings");
                videoVolSet = -1;   // fuerza el primer envio del volumen a este WMP recien apuntado
                int vol0 = VideoVolumeFor(path);
                if (settings != null) { Set(settings, "autoStart", true); Set(settings, "volume", vol0); }
                videoVolSet = vol0;
                Set(ocx, "URL", path);   // con autoStart=true deberia arrancar solo
                try { object c0 = Get(ocx, "controls"); if (c0 != null) Call(c0, "play"); } catch { }   // empujon explicito: autoStart no siempre engancha al instanciar el ActiveX

                videoOn = true;
                progress.Value = 0; lblTime.Text = "0:00 / 0:00";   // limpia el tiempo de la cancion anterior; VideoPoll lo llena con pos/dur del video
                videoLastPos = -1; videoProgressTick = Environment.TickCount; videoNudged = false; videoMaxPos = 0;   // arrancar el vigia de avance
                videoPoll.Interval = 400;   // sondeo normal (baja a 80 ms durante el fundido del final)
                video.BringToFront();
                videoPoll.Start();
                Diag("video: inicia");
            }
            catch (Exception ex)
            {
                videoOn = false;
                try { if (video != null) video.Visible = false; } catch { }
                RefundCredit();   // compensacion al cliente: el video no abrio
                ShowToast("No se pudo abrir el video: credito devuelto", Color.FromArgb(200, 80, 70));
                lblStatus.Text = "Video (WMP): " + ex.Message;
                Diag("video: NO ABRIO - " + ex.Message);
                // el video no arranco: pasar al siguiente sin trabar la cola (tras desenredar la pila de PlayNext)
                try { if (this.IsHandleCreated) this.BeginInvoke((MethodInvoker)delegate { PlayNext(); }); } catch { }
            }
        }

        // Detiene/oculta el video SIN tocar la cola (lo usan PlayNext y el cierre del buscador).
        private void StopVideoPlayback()
        {
            videoPoll.Stop();
            try { if (video != null) video.Visible = false; } catch { }   // ocultar PRIMERO (aunque el OCX truene al detener)
            try
            {
                if (video != null)
                {
                    object ocx = video.GetOcxObj();
                    if (ocx != null)
                    {
                        object controls = Get(ocx, "controls");
                        if (controls != null) Call(controls, "stop");
                        Set(ocx, "URL", "");
                    }
                }
            }
            catch { }
            videoOn = false;
            Diag("video: detenido");
            if (videoFull) ExitVideoFull();   // si estaba en pantalla completa, vuelve a la vista normal
        }

        private void VideoPoll_Tick(object sender, EventArgs e)
        {
            if (!videoOn || video == null) { videoPoll.Stop(); return; }
            int now = Environment.TickCount;
            int st = -1; double pos = -1, dur = -1;
            try
            {
                object ocx = video.GetOcxObj();
                if (ocx != null)
                {
                    object ps = Get(ocx, "playState");
                    if (ps != null) st = Convert.ToInt32(ps);
                    object controls = Get(ocx, "controls");
                    if (controls != null) { object cp = Get(controls, "currentPosition"); if (cp != null) pos = Convert.ToDouble(cp); }
                    object media = Get(ocx, "currentMedia");
                    if (media != null) { object d = Get(media, "duration"); if (d != null) dur = Convert.ToDouble(d); }
                }
            }
            catch { }

            if (pos > videoMaxPos) videoMaxPos = pos;

            // El tiempo/barra del VIDEO salen de aqui (ProgressTimer solo sirve a las canciones via 'reader',
            // que con video esta nulo). Sin esto el tiempo se quedaba congelado en lo ultimo de la cancion.
            if (pos >= 0 && dur > 0.5)
            {
                double frac = pos / dur; if (frac < 0) frac = 0; if (frac > 1) frac = 1;
                progress.Value = frac;
                lblTime.Text = Fmt(TimeSpan.FromSeconds(pos)) + " / " + Fmt(TimeSpan.FromSeconds(dur));
            }

            // FUNDIDO DE SALIDA del video: igual que las canciones, el final baja suave en vez de cortar en
            // seco. El video se da por terminado en dur-0.6 (ver abajo), asi que la rampa se mide contra ESE
            // final para que llegue a cero justo a tiempo. Durante el fundido se sondea mas seguido (80 ms)
            // para que la bajada se oiga continua; al terminar vuelve al sondeo normal.
            int volBase = VideoVolumeFor(nowPlaying != null ? nowPlaying.VideoPath : null);
            if (crossfadeSec > 0.01f && dur > 0.5 && pos >= 0)
            {
                double restan = (dur - 0.6) - pos;
                if (restan < crossfadeSec)
                {
                    double f = restan / crossfadeSec; if (f < 0) f = 0; else if (f > 1) f = 1;
                    videoPoll.Interval = 80;
                    SetVideoVolume((int)Math.Round(volBase * f));
                }
                else { videoPoll.Interval = 400; SetVideoVolume(volBase); }
            }
            else SetVideoVolume(volBase);

            // FIN NORMAL del video (sin reembolso, se reprodujo bien): MediaEnded (8); o la posicion
            // alcanzo la duracion (algunos archivos se congelan en el ultimo cuadro y NO reportan
            // MediaEnded -> esto era lo que dejaba la pantalla negra pegada); o Stopped CERCA del final.
            // Haber reproducido tres segundos no basta: WMP tambien usa Stopped para errores a media cancion.
            bool reachedEnd = (dur > 0.5 && pos >= dur - 0.6);
            bool stoppedAtEnd = (st == 1 && dur > 0.5 && videoMaxPos >= dur - 1.5);
            if (st == 8 || reachedEnd || stoppedAtEnd) { PlayNext(); return; }

            // Stopped (1) lejos del final (tambien si la duracion nunca llego) = fallo -> reembolso.
            if (st == 1)
            {
                try { LogError("video detenido antes del final (pos=" + videoMaxPos.ToString("0.0") + ", dur=" + dur.ToString("0.0") + "): " + (nowPlaying != null ? nowPlaying.VideoPath : "?"), null); } catch { }
                RefundCredit();
                ShowToast("Video no disponible: credito devuelto", Color.FromArgb(200, 130, 35));
                PlayNext();
                return;
            }

            // VIGIA DE VIDEO COLGADO: si la posicion AVANZA, todo bien (se reinicia el cronometro). Si NO
            // avanza 12 segundos seguidos (y sin haber llegado al final), se da por fallido y se SALTA solo.
            if (pos > videoLastPos + 0.05) { videoLastPos = pos; videoProgressTick = now; }
            else if (now - videoProgressTick > 6000 && !videoNudged)
            {
                // 6s sin avanzar: un EMPUJON de rescate (play explicito) por si fue una traba transitoria
                // (no el codec). Si el archivo es reproducible pero se atoro, esto suele revivirlo.
                videoNudged = true;
                try { object oc = video.GetOcxObj(); if (oc != null) { object c = Get(oc, "controls"); if (c != null) Call(c, "play"); } } catch { }
            }
            else if (now - videoProgressTick > 12000)
            {
                // 12s sin avanzar pese al empujon: el sistema no puede con este archivo (codec ausente o
                // archivo ilegible). Se salta solo y se anota la ruta para identificarlo.
                try { LogError("video estancado (playState=" + st + "): " + (nowPlaying != null ? nowPlaying.VideoPath : "?"), null); }
                catch { }
                RefundCredit();   // compensacion al cliente: el video no se pudo reproducir
                ShowToast("Video no disponible: credito devuelto", Color.FromArgb(200, 130, 35));
                PlayNext();
            }
        }

        // Late-binding por reflexion: le hablamos al control WMP sin referencias COM ni 'dynamic'
        // (asi funciona igual en .NET 6 y en .NET 3.5/4.0 de XP).
        private static object Get(object o, string prop)
        {
            return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null);
        }
        private static void Set(object o, string prop, object val)
        {
            o.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, o, new object[] { val });
        }
        private static object Call(object o, string method, params object[] args)
        {
            return o.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, o, args);
        }

        private void ApplyGenreBackground(Track t, Album org)
        {
            Color acc = ColorDeLoQueSuena(t, org);   // excepcion por artista (BTS=morado) y luego genero
            bgAccentTarget = acc;
            bgGeneroTarget = GeneroDeLoQueSuena(t, org);          // el genero suelto: una de las dos nebulosas
            bgSinGenero = t != null && GeneroDeCancion(t, org).Length == 0;
            bgArtistaFijo = !bgSinGenero && EsArtistaMorado(t);
            ColoresDePortadaDe(org, out bgPort1, out bgPort2);    // y los colores del disco: la otra
            Color panel = Shade(acc, 0.70);
            Color list = Shade(acc, 0.76);
            panelSongs.BackColor = panel;
            if (songPager != null) { songPager.BackColor = panel; songPager.Invalidate(); }
            cubeBox.BackColor = Color.Transparent;
            songList.BackColor = list; songList.Accent = acc;
            RefrescaLista();
        }

        private void DisposePlaybackNow()
        {
            CancelAudioOpen(); // saltar/cerrar invalida resultados que aun esten abriendo
            CancelaReintentoSalida();   // y la espera a que vuelva el aparato de sonido (SalidaSonido.cs)
            progressTimer.Stop();
            InvalidaPublicacionAudio();   // los bloques tardios ya no pueden publicar medidas ni pulso
            BombaSonido anterior = bomba; bomba = null;
            if (anterior != null) anterior.Dispose();   // tambien despierta cualquier lectura de la salida que esperaba musica
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                try { output.Stop(); } catch { }
                try { output.Dispose(); } catch { }
                output = null;
            }
            IAudioSource lectorActual = reader; reader = null;
            IAudioSource lectorPreparado;
            lock (audioDatosLock)
            {
                lectorPreparado = SueltaSinPausa();   // retira la siguiente; conserva el lector hasta acabar el ultimo Read
                FinalizeLoudMeasure();   // solo lo publicado antes de cancelar; la cancion nueva no recibe un bloque viejo
                FinalizeEqMeasure();
                ClearLevels();
            }
            SueltaLectoresAudio(anterior, lectorActual, lectorPreparado);
            curTap = null; curSbr = null; curBass = null; curNoche = null;   // ya no hay taps actuales
            rmsLo = rmsMid = rmsHi = 0f; avgLo = avgMid = avgHi = 0f;
        }

        // La llama el hilo de audio con el bloque recien leido: lo parte en sub-trozos de ~50ms, calcula el
        // RMS de cada uno y lo encola con la MARCA DE TIEMPO en que ese trozo SONARA (ahora + colchon).
        // El temporizador de UI los consume cuando les llega su momento -> el pulso va CON lo que se oye.
        // Buffers reusables del hilo de audio (4 trozos x 3 bandas). ThreadStatic: sin asignar por bloque.
        [ThreadStatic] private static float[] tmpLo; [ThreadStatic] private static float[] tmpMid; [ThreadStatic] private static float[] tmpHi;
        [ThreadStatic] private static float[] tmpEsp;   // 4 trozos x 8 bandas
        [ThreadStatic] private static double[] tmpSum;  // sumas por banda: reusado (nada de asignar en el hilo de audio)
        public void ReportAudioBlock(float[] buffer, int offset, int count, int rate, int ch)
        {
            if (count <= 0) return;
            int samplesPerSec = Math.Max(1, rate * ch);
            int now = Environment.TickCount;
            int chunks = 4; int len = count / chunks;
            if (len < 256) { chunks = 1; len = count; }   // bloque chico: un solo trozo
            float[] tLo = tmpLo ?? (tmpLo = new float[4]);
            float[] tMid = tmpMid ?? (tmpMid = new float[4]);
            float[] tHi = tmpHi ?? (tmpHi = new float[4]);
            bool espectro = EspectroActivo;
            float[] tEsp = espectro ? (tmpEsp ?? (tmpEsp = new float[4 * SpecBands])) : null;
            // ESCALERA del espectro: los coeficientes se calculan al ritmo de CUADROS (rate), porque la escalera
            // trabaja sobre la mezcla mono de cada cuadro y no muestra por muestra del stream entrelazado. Asi los
            // cortes caen en los mismos Hz de siempre pero con la MITAD de trabajo, y ademas la diferencia entre
            // canales deja de colarse como falso "aire". Se recalculan solo si cambia el formato.
            if (espectro && (specCoef == null || specRate != rate))
            {
                specRate = rate;
                specCoef = new float[SpecCortes.Length];
                for (int k = 0; k < SpecCortes.Length; k++)
                {
                    double a = 1.0 - Math.Exp(-2.0 * Math.PI * SpecCortes[k] / Math.Max(1, rate));
                    specCoef[k] = (float)(a < 0.0 ? 0.0 : (a > 1.0 ? 1.0 : a));
                }
                specLp = new float[SpecCortes.Length * SpecPolos];
            }
            float[] sc = specCoef, sl = specLp;
            double[] sSum = espectro ? (tmpSum ?? (tmpSum = new double[SpecBands])) : null;
            int chEsp = ch < 1 ? 1 : ch;
            float invCh = 1f / chEsp;
            float fSum = 0f; int fCnt = 0;   // acumulador del cuadro en curso (sigue vivo entre trozos del bloque)
            // Dos filtros paso-bajo de una via separan el audio en 3 bandas: grave = lp1, medio = lp2-lp1, agudo = x-lp2.
            // Los coeficientes marcan cortes aproximados (~150 Hz y ~2 kHz sobre el stream interleaved); es un
            // visualizador, no un ecualizador: basta que "se sienta" la separacion. El estado persiste entre bloques.
            const float A1 = 0.012f, A2 = 0.13f;
            float lp1 = fLp1, lp2 = fLp2;
#if ROCOLA_GPU
            // La aurora fina (AuroraFinaGpu.cs): el mismo espectro, apuntado ademas cada 1/60 s.
            double[] acFino = null; int nFino = 0, largoFino = 1;
            double ahoraFino = 0.0;
            if (espectro) { acFino = AuroraFinaEmpieza(rate, out nFino, out largoFino); ahoraFino = AuroraMs(); }
#endif
            for (int c = 0; c < chunks; c++)              // el RMS se calcula FUERA del lock (no retiene a la UI)
            {
                int start = offset + c * len;
                int end = (c == chunks - 1) ? offset + count : start + len;
                double sumLo = 0, sumMid = 0, sumHi = 0;
                int nEsp = 0;
                if (espectro) for (int b = 0; b < SpecBands; b++) sSum[b] = 0;
                for (int i = start; i < end; i++)
                {
                    float v = buffer[i];
                    lp1 += (v - lp1) * A1;               // paso-bajo grave
                    lp2 += (v - lp2) * A2;               // paso-bajo medio
                    float lo = lp1, mid = lp2 - lp1, hi = v - lp2;
                    sumLo += lo * lo; sumMid += mid * mid; sumHi += hi * hi;
                    if (!espectro) continue;             // aurora apagada (o sin GPU): la escalera ni se toca
                    fSum += v;                           // junta los canales del cuadro; la escalera corre UNA vez por cuadro
                    if (++fCnt < chEsp) continue;
                    float m = fSum * invCh; fSum = 0f; fCnt = 0;
                    // ESPECTRO: cada peldano de la escalera se queda con lo que el anterior dejo pasar,
                    // asi que la resta de dos peldanos consecutivos ES la banda que hay entre sus cortes.
                    float ant = 0f;
                    for (int k = 0; k < 7; k++)
                    {
                        int q = k * SpecPolos;
                        float x = m, ck = sc[k];
                        // Cada polo se lleva a una local: antes se leia su casilla del array DOS veces por
                        // vuelta (una para sumarle y otra para volver a leerla) y el coeficiente en cada
                        // vuelta. Las cuentas son las mismas y en el mismo orden: el sonido no cambia ni un bit.
                        for (int pp = 0; pp < SpecPolos; pp++)
                        {
                            float pv = sl[q + pp];
                            pv += (x - pv) * ck;
                            sl[q + pp] = pv;
                            x = pv;
                        }
                        float bnd = x - ant; ant = x;     // lo que este peldano deja pasar menos lo del anterior
                        sSum[k] += bnd * bnd;
#if ROCOLA_GPU
                        acFino[k] += bnd * bnd;
#endif
                    }
                    float top = m - ant;                 // lo que queda por encima del ultimo corte: el aire
                    sSum[7] += top * top;
#if ROCOLA_GPU
                    acFino[7] += top * top;
                    if (++nFino >= largoFino) { AuroraFinaCierra(nFino, i + 1 - offset, samplesPerSec); nFino = 0; }
#endif
                    nEsp++;
                }
                int n = Math.Max(1, end - start), ne = Math.Max(1, nEsp);
                tLo[c] = (float)Math.Sqrt(sumLo / n);
                tMid[c] = (float)Math.Sqrt(sumMid / n);
                tHi[c] = (float)Math.Sqrt(sumHi / n);
                if (espectro) for (int b = 0; b < SpecBands; b++) tEsp[c * SpecBands + b] = (float)Math.Sqrt(sSum[b] / ne);
            }
            fLp1 = lp1; fLp2 = lp2;   // persiste el estado de los filtros para el proximo bloque
            specLp = sl;
            // La cadena real mide antes del DSP; los taps sinteticos conservan este respaldo.
            if (tapMedido == null || !tapMedido.MideOriginal)
                MideBloqueOriginal(buffer, offset, count, rate, ch);
            lock (levelLock)
            {
                for (int c = 0; c < chunks; c++)
                {
                    int atMs = now + (int)((long)(c * len) * 1000 / Math.Max(1, samplesPerSec));   // desfase del trozo dentro del bloque
                    EnqueueLevel(atMs, tLo[c], tMid[c], tHi[c], espectro ? tEsp : null, c * SpecBands);
                }
#if ROCOLA_GPU
                if (espectro) AuroraFinaEncola(ahoraFino, nFino);
#endif
            }
        }

        // Detecta el golpe de UNA banda contra su propio piso movil y devuelve su pulso (0..1) con ataque instantaneo
        // y caida rapida. Como cada banda tiene MENOS energia que el total, sube algo la sensibilidad. Igual para las 3.
        private static float BandBeat(float rms, ref float avg, float lev)
        {
            avg += (rms - avg) * 0.06f;                                                     // piso movil (~0.7s)
            float beat = (rms > avg * 1.22f + 0.006f) ? Math.Min(1f, (rms - avg) * 11f) : 0f;   // cuanto SOBRESALE del piso
            float tgt = Math.Max(beat, Math.Min(1f, rms * 3.0f) * 0.22f);                  // golpe dominante + fondo suave
            if (tgt > lev) lev = tgt; else lev *= 0.78f;                                    // ataque instantaneo, caida rapida
            return lev < 0.003f ? 0f : lev;
        }

        private void UpdateNowPlayingUI()
        {
            if (nowPlaying != null)
            {
                string tit, art;
                nowPlaying.TituloArtista(out tit, out art);
#if ROCOLA_GPU
                if (gpuEnabled) tit = TituloSinPista(tit);
#endif
                nowFullText = tit; nowArtista = art;
            }
            else { nowFullText = "Nada reproduciendose"; nowArtista = ""; }
            iaBadgeKind = MejActivo() ? 2 : 0;   // dorado = el DSEE espectral reconstruyendo DE VERDAD
            CambioElPanelDeReposo();   // la insignia sale tambien en el panel de reposo
            ActualizaGenero();    // la pastilla del genero, antes de acomodar: manda en el ancho del artista
            PositionMejorada();   // pone titulo y artista y coloca el letrero MEJORADA
            lblNext.Text = (queue.Count > 0) ? "Sigue:  " + queue[0].Display() : "Sigue:  -";
#if ROCOLA_GPU
            if (gpuEnabled && queue.Count > 0 && !queue[0].IsVideo && queue[0].Song != null)
                lblNext.Text = "Sigue:  " + queue[0].Song.Artist + " - " + TituloSinPista(queue[0].Song.Title);
#endif
            pastCola.Valor = queue.Count.ToString();
            LlenaPortaditas();
            pastCola.Invalidate();
            if (miniCube != null)
            {
                if (nowPlaying != null)
                {
                    Image nueva = LoadCover(nowPlaying.Disc);
                    // EL RELEVO: si ya habia un cubo puesto con otra portada, no se cambia de golpe; el
                    // cubo da media vuelta y la nueva aparece en el camino. La primera cancion de la
                    // noche, y la portada que llega tarde a la que YA suena, entran sin giro: ahi no hay
                    // relevo de nada que enseñar.
                    if (relevoOn && miniCube.Visible && miniCube.Cover != null && nueva != null && !object.ReferenceEquals(nueva, miniCube.Cover))
                        miniCube.Relevo(nueva);
                    else if (!miniCube.Girando) miniCube.Cover = nueva;
                    miniCube.Visible = true;
                }
                else { miniCube.Cover = null; miniCube.Visible = false; }
                miniCube.Invalidate();
            }
        }

        // El letrero corresponde a una MEJORA REAL: switch prendido Y el reconstructor de la cancion
        // actual reconstruyendo de verdad (su puerta interna deja intactos los archivos ya completos).
        private bool MejActivo()
        {
            SpectralTap sp = curSbr;
            return sbrOn && nowPlaying != null && !nowPlaying.IsVideo && sp != null && sp.Mejorando;
        }

        // Parte "Reproduciendo: <titulo>" en HASTA DOS lineas y coloca el letrero "MEJORADA" justo despues de
        // la ULTIMA. Antes era una sola linea que se encogia para dejarle sitio al letrero, asi que un titulo
        // largo se cortaba con puntos suspensivos bastante antes de tiempo. Ahora la primera parte se va a la
        // linea de arriba (que esta reservada siempre) y solo el resto comparte fila con el letrero.
        // Dorado: en esta rama el DSEE espectral es el unico que mejora, asi que el letrero es uno solo.
        // El titulo va arriba y el artista debajo; el letrero MEJORADA se pone detras del ARTISTA, que es el
        // renglon corto, para no comerle sitio al titulo.
        private void PositionMejorada()
        {
            if (lblMejorada == null || lblNow == null || lblNowTop == null) return;
            if (lblNowTop.Text != nowFullText) lblNowTop.Text = nowFullText;
            if (lblNow.Text != nowArtista) lblNow.Text = nowArtista;
            if (lblNowTop.Width != mejRowW) lblNowTop.Width = mejRowW;

            bool on = MejActivo();
            int bw = on ? TextRenderer.MeasureText(lblMejorada.Text, lblMejorada.Font).Width + 4 : 0;
            // LA PASTILLA DEL GENERO va en este mismo renglon, entre el artista y el letrero MEJORADA: es de
            // la cancion, igual que el artista, y en el renglon de arriba -el titulo- no cabria sin comerle
            // sitio a lo unico que de verdad hay que leer entero.
            int gw = (pastGenero != null) ? pastGenero.AnchoQuePide() : 0;
            int anchoArt = mejRowW - (on ? bw + 12 : 0) - (gw > 0 ? gw + 12 : 0);
            if (anchoArt < 60) anchoArt = 60;
            if (lblNow.Width != anchoArt) lblNow.Width = anchoArt;

            int tw = TextRenderer.MeasureText(lblNow.Text, lblNow.Font).Width;
            if (tw > anchoArt) tw = anchoArt;
            int x = lblNow.Left + tw + 12;
            if (gw > 0)
            {
                int gh = Math.Min(pastGenero.AltoQuePide(), lblNow.Height);
                pastGenero.SetBounds(x, lblNow.Top + (lblNow.Height - gh) / 2, gw, gh);
                if (!pastGenero.Visible) pastGenero.Visible = true;
                x += gw + 12;
            }
            else if (pastGenero != null && pastGenero.Visible) pastGenero.Visible = false;

            if (!on) { if (lblMejorada.Visible) lblMejorada.Visible = false; return; }
            lblMejorada.SetBounds(x, lblNow.Top, bw, 22);
            lblMejorada.Visible = true;
        }

        // DE QUE ES LO QUE SUENA. La misma respuesta que dentro del disco y que en el buscador (GeneroDeCancion),
        // asi que las tres dicen lo mismo. Un VIDEO no lleva: no hay de donde sacarla. Si no se sabe, no sale
        // pastilla -mejor sin etiqueta que con una inventada-.
        private void ActualizaGenero()
        {
            if (pastGenero == null) return;
            string et = (nowPlaying != null && !nowPlaying.IsVideo) ? GeneroDeCancion(nowPlaying.Song, nowPlaying.Disc) : "";
            if (et == pastGenero.Texto) return;
            pastGenero.Texto = et;
            pastGenero.Tinte = (et.Length > 0) ? ColorDeEtiqueta(et) : DefaultAccent;
            CambioElPanelDeReposo();   // el panel grande lleva la pastilla del genero (SuenaVivoGpu.cs)
            PositionMejorada();   // el ancho del artista depende de si hay pastilla y de lo que mida
            // El genero se aprende en segundo plano, asi que puede llegar con la cancion ya sonando. El fondo
            // va del mismo color, asi que se entera aqui en vez de esperar a la siguiente.
            if (nowPlaying != null && !nowPlaying.IsVideo && panelSongs != null)
            {
                ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
            }
        }

        // Reparte el texto entre la linea de ARRIBA (ancho completo) y la de ABAJO (la que comparte fila con el
        // letrero, asi que va mas estrecha). Si entra entero en la de abajo, la de arriba se queda vacia y se ve
        // exactamente igual que siempre. Se corta por ESPACIO; si una sola palabra no cabe, se corta por letra
        // antes que dejar el titulo mochado. Lo que aun sobre lo elipsa la propia etiqueta.
        private static void PartirNow(string texto, Font f, int anchoArriba, int anchoAbajo, out string arriba, out string abajo)
        {
            arriba = ""; abajo = texto ?? "";
            if (abajo.Length == 0 || anchoAbajo < 20) return;
            if (TextRenderer.MeasureText(abajo, f).Width <= anchoAbajo) return;   // cabe de una: nada que partir
            int tope = anchoArriba - 6;   // margen: la textura de la GPU dibuja con GDI+ y mide un pelo mas ancho
            int corte = -1;
            for (int i = 0; i < abajo.Length; i++)
            {
                if (abajo[i] != ' ') continue;
                if (TextRenderer.MeasureText(abajo.Substring(0, i), f).Width > tope) break;
                corte = i;   // el ultimo espacio que todavia cabe arriba
            }
            if (corte > 0) { arriba = abajo.Substring(0, corte); abajo = abajo.Substring(corte + 1); return; }
            // ni un espacio util (un titulo de una sola palabra larguisima): se corta por letra
            int n = 0;
            while (n + 1 < abajo.Length && TextRenderer.MeasureText(abajo.Substring(0, n + 1), f).Width <= tope) n++;
            if (n > 0 && n < abajo.Length) { arriba = abajo.Substring(0, n); abajo = abajo.Substring(n); }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            VigiaPantalla.Hace("el reloj de la cancion");
            if (reader == null) return;
            // LA SALIDA SE QUEDO MUDA (SalidaSonido.cs): como si hubiera avisado de un fallo del aparato.
            if (SalidaMuda()) { NAudio.Wave.IWavePlayer o = output; if (o != null) Output_PlaybackStopped(o, new NAudio.Wave.StoppedEventArgs(new SalidaMudaException())); return; }
            PreparaSiguienteSinPausa();   // a la que suena le quedan pocos segundos: la siguiente, lista para entrar sin pausa
            TimeSpan cur = reader.CurrentTime, tot = reader.TotalTime;
            if (tot.TotalSeconds > 0) progress.Value = cur.TotalSeconds / tot.TotalSeconds;
            lblTime.Text = Fmt(cur) + " / " + Fmt(tot);
            // (la posicion ya NO se guarda: al reanudar tras un corte de luz la cancion arranca desde el
            // principio, asi que el estado solo se escribe en eventos -> cero escrituras periodicas a disco)
        }

        // DONDE VA LA CASILLA DE BUSCAR en la fila de arriba: en el MEDIO DE LA PANTALLA, que es donde el
        // ojo la busca. Si centrada pisaria las instrucciones se corre a la derecha lo justo, y si aun asi no
        // cabe antes del encabezado se pega al final del hueco. 'minX' es donde acaban las instrucciones y
        // 'derecha' donde acaba la barra.
        private static int SitioBusca(int W, int busW, int minX, int derecha)
        {
            int x = minX;
            int centrado = (W - busW) / 2;
            if (centrado > x) x = centrado;
            if (x + busW > derecha) { x = derecha - busW; if (x < minX) x = minX; }
            return x;
        }

#if ROCOLA_GPU
        // Donde arranca la franja de abajo. Solo lo usa el dibujado por tarjeta grafica: es donde se acaba
        // el cielo y donde empieza la tira de lo que suena.
        private int BotStripTop(int h)
        {
            int t = (lblNowTop != null) ? lblNowTop.Top - 10 : (lblNow != null ? lblNow.Top - 10 : h - 104);   // la franja arranca justo encima de la tarjeta
            if (t < h / 2) t = h / 2;
            if (t > h - 8) t = h - 8;
            if (t < 0) t = 0;   // pantalla de alto ridiculo (arrancando, o minimizada): nunca por delante del borde
            return t;
        }

        private static uint ToD3D(Color c) { return 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | (uint)c.B; }

        private bool gpuEnabled;                          // render por GPU activo. Por defecto SI (se guarda al alternar con Ctrl+F12)
        private bool gpuShowFps;                          // mostrar el contador de FPS (diagnostico). Oculto por defecto; Ctrl+F1 lo alterna
        private bool gpuDiscTry = true;                   // interior de disco por GPU (por defecto). Ctrl+F11 lo alterna a CPU como valvula de escape
        private int gpuMode = -1;                          // modo GPU actual (nav/buscador/disco/atraccion); al cambiar se rehace la UI atomica
        private int layoutStamp = -1;                      // huella del ultimo reacomodo: solo un cambio REAL rehace la textura de UI
        private Bitmap uiOverlay; private int uiOverlayVer; private long uiOverlaySig = SigNada;   // textura de las barras (UI)

        // Ctrl+F12: alterna el render entre GPU (Direct3D 9, por defecto) y CPU, y GUARDA la preferencia.
        private void ToggleAlbumGpu() { ApplyAlbumGpu(!gpuEnabled, true, true); }

        // Aplica el modo de render. 'on' = destino (GPU/CPU); 'announceFail' = avisar si el dispositivo no inicia;
        // 'persist' = guardar la preferencia (solo cuando lo pide el usuario con Ctrl+F12, no en el arranque). El
        // panel GPU (Direct3D 9) cubre toda la ventana y compone: fondo estrellado + cubos + barras + mini-cubo.
        private void ApplyAlbumGpu(bool on, bool announceFail, bool persist)
        {
            VigiaPantalla.Hace("preparar la tarjeta grafica");
            if (lblGpu == null)
            {
                lblGpu = new Label();
                lblGpu.AutoSize = false; lblGpu.Size = new Size(380, 26);
                lblGpu.Location = new Point(ClientSize.Width - 390, 46);
                lblGpu.BackColor = Color.FromArgb(200, 0, 0, 0); lblGpu.ForeColor = Color.Yellow;
                lblGpu.Font = new Font(FontFamily.GenericMonospace, 10f, FontStyle.Bold);
                lblGpu.TextAlign = ContentAlignment.MiddleCenter; lblGpu.Visible = false;
                Controls.Add(lblGpu);
            }
            if (gpuPanel == null)
            {
                gpuPanel = new GpuPanel();
                Point ultimoMouseGpu = new Point(-10000, -10000);
                gpuPanel.MouseMove += delegate(object senderMouse, MouseEventArgs mouse) {
                    if (Math.Abs(mouse.X - ultimoMouseGpu.X) + Math.Abs(mouse.Y - ultimoMouseGpu.Y) < 4) return;
                    ultimoMouseGpu = mouse.Location; lastInput = DateTime.Now; gpuPanel.IdleExpandTarget = false;
                    AsistenteMueveRaton(mouse.Location);
                };
                gpuPanel.MouseDown += delegate {
                    lastInput = DateTime.Now; gpuPanel.IdleExpandTarget = false; gpuPanel.TouchUi();
                    if (asistenteVisible) asistenteActividadTick = Environment.TickCount;   // tocar la pantalla tambien es usarla: la guia no se cierra por inactividad
                };
                gpuPanel.MouseWheel += delegate { lastInput = DateTime.Now; gpuPanel.IdleExpandTarget = false; gpuPanel.TouchUi(); };
                gpuPanel.MouseClick += delegate(object sender, MouseEventArgs e)
                {
                    // La casilla AYUDA va primero: abre la ayuda y, con la ayuda abierta, la vuelve a guardar.
                    if (e.Button == MouseButtons.Left && AyudaTocada(new Point(e.X + gpuPanel.Left, e.Y + gpuPanel.Top))) return;
                    if (AsistenteCapturaEntrada)
                    { if (e.Button == MouseButtons.Left) AsistenteClick(e.Location); return; }
                    // El control del buscador esta oculto al componer por GPU: reenviar
                    // el clic real usando sus coordenadas, sin duplicar la seleccion.
                    if (e.Button != MouseButtons.Left || !searchOpen || gpuMode != 2 ||
                        winMode != 0 || searchOverlay == null) return;
                    Point p = new Point(e.X + gpuPanel.Left - searchOverlay.Left,
                                        e.Y + gpuPanel.Top - searchOverlay.Top);
                    if (!searchOverlay.ClientRectangle.Contains(p)) return;
                    MarkSearchActivity();
                    searchOverlay.ClickAt(p);
                    gpuPanel.TouchUi();
                };
                gpuPanel.ShowFps = gpuShowFps;
                gpuPanel.ReportFluidez = delegate(string informe) { Diag(informe); };
                gpuPanel.TopeFps = fpsTope;   // 0 = que lo decida el panel por el refresco de la pantalla
                AplicaEfectosGpu();           // y las perillas de los efectos (Ctrl+F3 -> Imagen y rendimiento)
                gpuPanel.GetPulse = delegate { return audioLevel; };
                gpuPanel.GetStarMusic = delegate { return new StarMusic { Bass=levLo, Mid=levMid, High=levHi }; };
                gpuPanel.GetAurora = delegate { return AuroraDelCuadro(); };   // aurora del horizonte (espectro), avanzada a este cuadro
                gpuPanel.GetToast = delegate { return BuildToastState(); };     // aviso flotante (moneda, cancion agregada...)
                gpuPanel.GetTrans = delegate { return BuildTransState(); };   // zoom al entrar/salir de un disco
                gpuPanel.GetWin = delegate { return BuildWinState(); };        // ventana del buscador abriendo/cerrando
                // Rejilla de discos: cada celda su propio quad con textura cacheada (hojear = elegir texturas + posiciones).
                // De paso recoge lo que el cielo se corre al pasar de pagina (PantallaVivaGpu.cs).
                gpuPanel.GetGrid = delegate(GpuGridCell[] celdas)
                {
                    if (paginaGpuClave == null || paginaGpuFirma != lastPageSig || paginaGpuGeneracion != coverGen)
                    {
                        paginaGpuFirma = lastPageSig; paginaGpuGeneracion = coverGen;
                        paginaGpuClave = lastPageSig + "|" + coverGen;
                    }
                    coverGovernor.Page(paginaGpuClave, false, GpuPanel.AhoraMs());
                    int cuantas = albumView.BuildGpuGrid(celdas);
                    gpuPanel.EmpujeCielo += albumView.TomaEmpuje();
                    return cuantas;
                };
                gpuPanel.GetFoco = BuildFocoState;     // el letrero de la portada enfocada
                gpuPanel.GetFicha = BuildFichaState;   // la ficha rapida del disco
                gpuPanel.GetVinilo = null; // efecto retirado, no se hornea ni se sube
                gpuPanel.GetEq = BuildEqState;           // las barritas de la que suena
                gpuPanel.GetBoleto = BuildBoletoState;   // el boleto que vuela de la fila a la cola
                gpuPanel.GetAviso = BuildAvisoState;     // los avisos que se entienden (SuenaVivoGpu.cs)
                gpuPanel.GetInvita = BuildInvitaState;   // sin creditos: la tarjeta que invita
                gpuPanel.GetMoneda = BuildMonedaState;   // la moneda que cae en la capsula de creditos
                gpuPanel.GetReposo = BuildReposoState;   // lo que suena, en grande: el vinilo y el titulo del panel de reposo
                gpuPanel.GetAtraccion = BuildAtraccionState;   // el modo atraccion: el escaparate y lo mas pedido (AtraccionGpu.cs)
                gpuPanel.CoverControl = coverGovernor;
                gpuPanel.GetGridAhead = albumView.BuildGpuAhead;
                gpuPanel.GetContour = delegate { return albumView.ContourSpriteBmp; };          // resalte de seleccion
                gpuPanel.GetAyudaIcono = AyudaIconoRect;   // la interrogacion dorada de la casilla de AYUDA (AyudaCasillaGpu.cs)
                gpuPanel.GetContourRect = delegate {
                    Rectangle r = albumView.ContourScreenRect();
                    if (r.Width > 0) r.Offset(albumView.Left, albumView.Top);   // coords de albumView -> pantalla
                    return r;
                };
                gpuPanel.GetCubeState = delegate { return CubosDelCuadro(); };   // movimiento GPU a 24/50/60, sin depender del timer de controles
                gpuPanel.GetProgress = delegate {
                    GpuProgress p = new GpuProgress();
                    if (progress == null || !progress.Visible || attractOn) return p;
                    p.Has = true; p.Rect = progress.Bounds; p.Value = progress.Value;
                    p.Neon = progress.Neon; p.Color = ToD3D(progress.Accent); p.Key = reader != null ? (object)reader : (object)nowPlaying;
                    if (!videoOn && reader != null && reader.TotalTime.TotalSeconds > 0)
                        p.Value = reader.CurrentTime.TotalSeconds / reader.TotalTime.TotalSeconds;
                    return p;
                };
                gpuPanel.GetUi = delegate { return uiOverlay; };                    // textura PRINCIPAL de la UI (altura segun el modo)
                gpuPanel.GetUiVersion = delegate { return EnsureUiOverlay(); };
                gpuPanel.GetUiCambio = UiCambioDesde;   // y que trozo cambio (en el buscador: BuscadorLigeroGpu.cs)
                gpuPanel.GetUiBot = delegate { return uiBotOverlay; };              // franja de ESTADO de abajo (textura chica aparte: el reloj no rehace la principal)
                gpuPanel.GetIdlePanel = BuildIdlePanel;
                gpuPanel.GetCintasTexto = delegate { return cintasTexto; };
                this.FormClosed += delegate { SueltaCintasTexto(); };
                this.FormClosed += delegate { if (idlePanelBmp != null) { idlePanelBmp.Dispose(); idlePanelBmp = null; } if (idleClockUnderlay != null) { idleClockUnderlay.Dispose(); idleClockUnderlay = null; } };
                gpuPanel.GetUiBotVersion = delegate { return EnsureUiBotOverlay(); };
                gpuPanel.GetUiBotCambio = UiBotCambioDesde;   // y que trozo cambio (el reloj y la barra: FranjaLigeraGpu.cs)
                gpuPanel.GetUiGlows = FillUiGlows;                                   // halos animados (destello de boton, pulso de letra, TOP, atraccion): quads GPU, sin re-subir textura
                gpuPanel.GetAsistenteState = BuildAsistenteState;
                // Las piezas vivas del buscador (tarjeta y tecla elegidas): imagenes chicas aparte, encima de
                // la grande. Sin esto, mover el recuadro re-subia la pantalla completa (ver EnsureUiPieza).
                gpuPanel.PiezasN = NumPiezas;
                gpuPanel.GetPieza = delegate(int i) { return uiPieza[i]; };
                gpuPanel.GetPiezaRect = delegate(int i) { return uiPiezaRect[i]; };
                gpuPanel.GetPiezaVersion = delegate(int i) { return EnsureUiPieza(i); };
                gpuPanel.GetPiezaCambios = CambiosPiezaBuscador;
                gpuPanel.GetCartas = FillCartas;   // el buscador con portadas: cada tarjeta entra por su cuenta (BuscadorPortadasGpu.cs)
                gpuPanel.GetPiezasSuben = PiezasSubenConLaVentana;   // las piezas de los dos buscadores van con la ventana
                gpuPanel.GetPiezaVistaPrevia = PiezaVistaPreviaBuscador;   // y lo que se desliza de su vista previa
                gpuPanel.GetGlassMode = delegate { return CanUseSongGlass; };
                gpuPanel.GetSongSlide = delegate { return songList == null ? 0f : songList.GpuSlideOffset; };
                gpuPanel.GetSongTravel = delegate { return songList == null ? 0f : songList.GpuSlideTravel; };
                gpuPanel.GetSongVertical = delegate { return songList != null && songList.GpuSlideVertical; };
                gpuPanel.GetPaletteKey = delegate {
                    if (!coloresCache || !searchOpen || searchOverlay == null) return 0;
                    searchOverlay.Accent = bgAccentTarget;
                    return bgAccentTarget.ToArgb();
                };
                gpuPanel.GetPaletteContent = delegate {
                    if (!searchOpen || searchOverlay == null) return 0L;
                    long key = Mix(Mix(SigBase, Width), Height);
                    key = Mix(key, searchOverlay.Query); key = Mix(key, searchOverlay.DatosVer);
                    key = Mix(key, searchOverlay.FocusResults); key = Mix(key, searchOverlay.SelResult);
                    key = Mix(key, searchOverlay.SelRow); key = Mix(key, searchOverlay.SelCol);
                    key = Mix(key, searchOverlay.Escrito); key = Mix(key, colaVer);
                    key = Mix(key, searchOverlay.ToastOn); key = Mix(key, searchOverlay.ToastMsg);
                    return searchOverlay.Nuevo ? Mix(key, searchOverlay.FirmaContenido()) : key;
                };
                Controls.Add(gpuPanel);
            }
            if (on && !gpuEnabled)
            {
                albumView.InvalidateCubeCache();         // cubeCache fresco para subirlo a la GPU
                uiOverlaySig = SigNada;                     // fuerza reconstruir la textura de UI
                gpuPanel.AlbumRect = albumView.Bounds;
                gpuPanel.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);   // PANTALLA COMPLETA
                gpuPanel.SkyTop = ToD3D(SkyColor(bgAccent)); gpuPanel.SkyMid = ToD3D(SkyColorMid(bgAccent)); gpuPanel.SkyBot = ToD3D(SkyColor2(bgAccent)); gpuPanel.NebColor = ToD3D(ColorNebulosa(bgAccent));
                gpuPanel.Visible = true; gpuPanel.BringToFront();
                if (gpuPanel.StartGpu())
                {
                    gpuEnabled = true; lblGpu.Visible = false; KeepVideoOnTop();
                    gpuPanel.PrecalientaAnimaciones(ThumbPx, ToastW, ToastH);   // texturas de las animaciones creadas YA, no en la primera que se vea
                }
                else { gpuPanel.Visible = false; Diag("render: FALLO el dispositivo D3D9 (sigue CPU)"); if (announceFail) { lblGpu.Text = "GPU: fallo el dispositivo (sigue CPU)"; lblGpu.Visible = true; lblGpu.BringToFront(); } }
            }
            else if (!on && gpuEnabled)
            {
                gpuEnabled = false;
                AjustaDiscoVivo(false);   // la lista del disco vuelve a ser la de siempre: ahora la pinta el procesador
                if (songList != null) songList.pintandoGpu = false;   // y su fila elegida, la de siempre
                gpuPanel.StopGpu(); gpuPanel.Visible = false;
                SueltaPiezas();     // las piezas vivas del buscador solo existen para la tarjeta grafica
                if (albumView != null) albumView.GpuCovered = false;   // vuelve a pintar la rejilla por CPU (ya no hay panel encima)
                if (lblGpu != null) lblGpu.Visible = false;
                Invalidate(true);   // repinta la UI normal (CPU) que estaba tapada por el panel
            }
            AjustaBuscadorPortadas();   // el buscador con portadas solo existe con la tarjeta dibujando
            // Cambiar de render no debe esconder una pantalla que sigue activa.
            // No reabrirla: conservar consulta, resultados y seleccion.
            if (gpuEnabled) SyncGpuFullScreen();
            if (searchOverlay != null && searchOpen)
            {
                searchOverlay.Visible = !gpuEnabled;
                if (!gpuEnabled) { searchOverlay.BringToFront(); searchOverlay.Focus(); }
            }
            if (attractView != null && attractOn)
            {
                attractView.Visible = !gpuEnabled;
                if (!gpuEnabled) attractView.BringToFront();
            }
            if (persist) { gpuPrefOn = gpuEnabled; SaveConfigPrefs(); }   // recuerda la eleccion para el proximo arranque
        }


        // Estado de los cubos que giran (en coords de PANTALLA). En navegacion: album (seleccionado + relevo).
        // En el buscador: el cubo de vista previa. El mini-cubo de la barra inferior es comun a ambos.
        private GpuCubeState BuildGpuCubeState()
        {
            GpuCubeState st;
            if (attractOn && attractView != null)   // MODO ATRACCION: cubo gigante centrado que cicla portadas (con fundido)
            {
                st = new GpuCubeState();
                Image ca, cb; float f;
                ParCuboAtraccion(out ca, out cb, out f);   // puede dar null (portadas aun cargando): el cubo muestra relleno azulado
                RectangleF cubo = CuboAtraccionAhora();   // el de siempre, o el del escaparate (AtraccionGpu.cs): lo mismo que la animacion de entrada/salida
                st.Sel.Has = true;
                st.Sel.Key = attractView;
                st.Sel.X = cubo.X; st.Sel.Y = cubo.Y; st.Sel.W = cubo.Width; st.Sel.H = cubo.Height;
                st.Sel.Angle = AnguloAtraccion(); st.Sel.CoverA = ca as Bitmap; st.Sel.CoverB = cb as Bitmap; st.Sel.Fade = f;
                return st;   // en atraccion no hay mini-cubo (no suena nada)
            }
            if (searchOpen && searchOverlay != null)
            {
                st = new GpuCubeState();
                Rectangle area; Image ca, cb; float f;
                bool cubOk = searchOverlay.GetPreviewCube(out area, out ca, out cb, out f);
#if ROCOLA_GPU
                if (winMode == 1) cubOk = false;   // abriendo: la ventana se anima entera, un cubo suelto encima la delataria
#endif
                if (cubOk)
                {
                    area.Offset(searchOverlay.Left, searchOverlay.Top);   // coords del buscador -> pantalla
                    st.Sel.Has = true; st.Sel.X = area.X; st.Sel.Y = area.Y; st.Sel.W = area.Width; st.Sel.H = area.Height;
                    st.Sel.Key = searchOverlay;
                    st.Sel.Angle = searchOverlay.PreviewAngle; st.Sel.CoverA = ca as Bitmap; st.Sel.CoverB = cb as Bitmap; st.Sel.Fade = f;
                }
            }
            else if (panelSongs != null && panelSongs.Visible && cubeBox != null)
            {
                st = new GpuCubeState();
                if (cubeBox.Cover != null)   // el cubo GRANDE del interior (gira); coords de panelSongs -> pantalla
                {
                    st.Sel.Has = true;
                    FillCubeBoxGeom(ref st.Sel, cubeBox, panelSongs.Left + cubeBox.Left, panelSongs.Top + cubeBox.Top);
                }
            }
            else
            {
                st = albumView.BuildCubeState();
                int ox = albumView.Left, oy = albumView.Top;
                if (st.Sel.Has) { st.Sel.X += ox; st.Sel.Y += oy; }
                if (st.Relay.Has) { st.Relay.X += ox; st.Relay.Y += oy; }
                st.Pared = true;   // el elegido es el de la pared: la portada enfocada solo se aplica aqui
            }
            if (miniCube != null && miniCube.Visible && miniCube.Cover != null)   // mini-cubo (barra inferior): comun
            {
                st.Mini.Has = true;
                FillCubeBoxGeom(ref st.Mini, miniCube, miniCube.Left, miniCube.Top);
            }
            return st;
        }

        // Encuadre + bamboleo de un CubeBox (grande o mini) para que el cubo por GPU COINCIDA con el de CPU:
        // CubeBox.OnPaint dibuja el cubo al 78% del control (centrado) y lo inclina +/-10 grados segun Phase.
        // Sin esto el cubo por GPU salia mas grande (100% del control) y sin el bamboleo (giro plano).
        private static void FillCubeBoxGeom(ref GpuCubeInfo info, CubeBox cb, int screenX, int screenY)
        {
            int side = (int)(Math.Min(cb.Width, cb.Height) * 0.78);
            if (side < 1) side = 1;
            info.X = screenX + (cb.Width - side) / 2;
            info.Y = screenY + (cb.Height - side) / 2;
            info.W = side; info.H = side;
            info.Angle = cb.Angle;
            info.CoverA = cb.Cover as Bitmap;
            info.Key = cb;
            info.Tilt = (float)(10.0 * Math.Sin(cb.Phase * 0.9) * Math.PI / 180.0);   // mismo bamboleo que CubeBox.OnPaint
        }

        // Muestra/oculta el panel GPU (a pantalla completa) segun el modo: en NAVEGACION y en el BUSCADOR
        // (ambos se componen por GPU). En disco/video/atraccion/config se oculta y deja ver los controles CPU.
        // El cuadro del video (WMP) es una ventana HIJA hermana del panel GPU: si el panel acaba de
        // mostrarse, su BringToFront lo dejo por delante y taparia el video. Esto lo regresa arriba.
        private void KeepVideoOnTop() { try { if (!AsistenteVisible && videoOn && video != null && video.Visible) video.BringToFront(); } catch { } }

        private void SyncGpuFullScreen()
        {
            if (gpuPanel == null || !gpuEnabled) return;
            bool cfgOn = (lblConfig != null && lblConfig.Visible);
            // VIDEO con la interfaz por GPU (video_gpu=1, por defecto): un video en ventana YA NO manda todo
            // a render por CPU. Antes videoOn apagaba nav/disco/atraccion y la pantalla ENTERA (fondo
            // estrellado incluido) se repintaba por CPU mientras WMP decodificaba: en 2 nucleos ese era el
            // "rendimiento raro" con video, y como las animaciones son SOLO por GPU, con video sonando no
            // podian verse nunca (solo la del buscador, el unico modo que ya ignoraba el video). El cuadro
            // del video queda ENCIMA del panel (KeepVideoOnTop) y Windows recorta el panel donde el video
            // esta: no se pisan. Si en algun fierro se viera mal, video_gpu=0 vuelve al comportamiento de
            // antes. El video a PANTALLA COMPLETA si sigue apagando el panel: ahi el es dueno de todo.
            bool videoBlocks = videoOn && !videoGpuOn && !AsistenteVisible;
            bool notBlockedByVideoCfg = !videoBlocks && !videoFull && !cfgOn;
            bool noBlock = notBlockedByVideoCfg && !attractOn;
            bool disc = panelSongs != null && panelSongs.Visible;
            bool nav = albumView.Visible && !searchOpen && !disc && noBlock;
            // El VIDEO no bloquea el BUSCADOR: OpenSearch OCULTA el cuadro del video (el audio sigue), asi que
            // el panel GPU puede componer el buscador encima sin taparlo. Antes el video SI lo bloqueaba y el
            // buscador "se abria" INVISIBLE (panel oculto por video + overlay CPU oculto por GPU): el cliente
            // solo veia desaparecer y reaparecer el video. Nav/disco SI siguen bloqueados (ahi el video se VE).
            bool search = searchOpen && !attractOn && !cfgOn && !videoFull;
            // Interior de disco por GPU (por defecto). El bug de la lista era que RenderContent usaba
            // TextRenderer (GDI), que ignora la traslacion del Graphics: sobre la textura de UI (trasladada
            // a la posicion del panel) el texto caia en 0,0. Ya usa DrawString (GDI+), que la respeta.
            // Ctrl+F11 (gpuDiscTry) alterna a CPU como valvula de escape. Navegacion y buscador van por GPU.
            bool discGpu = disc && gpuDiscTry && !searchOpen && noBlock;
            // La lista del disco, grande y con sus extras, SOLO si la compone la tarjeta (con el buscador encima
            // tampoco se ve, asi que no cambia). Va antes de esconder el panel: si el disco pasa a pintarlo el
            // procesador, cuando lo pinte ya es la lista de siempre (DiscoVivoGpu.cs).
            AjustaDiscoVivo(ListaPorGpu());
            bool attract = attractOn && notBlockedByVideoCfg;   // modo atraccion (reposo): cubo gigante + estrellas por GPU
            // Al CAMBIAR de modo, rehacer la textura de UI en el MISMO cuadro que cambia lo que se dibuja
            // (ShowAlbum, cubos): si no, se ve un instante el modo viejo detras del nuevo (p.ej. la rejilla de
            // discos por los huecos del teclado al cerrar el buscador). UiUrgent salta la limitacion de franja.
            int mode = search ? 2 : discGpu ? 3 : attract ? 4 : nav ? 1 : 0;
            if (AsistenteVisible && !cfgOn && !videoFull && mode == 0) mode = 1;
            gpuPanel.SetScene(mode);
            if (mode != gpuMode) { gpuMode = mode; uiOverlaySig = SigNada; gpuPanel.UiUrgent = true; Diag("render: modo GPU " + mode + (videoOn ? " (video sonando)" : "")); }
            // Mientras el video SE VE, el panel baja a ~30 cuadros por segundo: el resto del equipo es para
            // el video (WMP decodifica por CPU en el fierro modesto). Sin video (o con el buscador abierto,
            // que lo oculta) vuelve al maximo.
            // Con el video a la vista, el panel se limita a 30 cuadros por segundo para dejarle equipo al
            // video... pero SOLO donde hace falta. En la maquina de dos nucleos, el decodificador del video y
            // el dibujado se pelean por los mismos nucleos y limitar ayuda de verdad; con cuatro o mas, el
            // video tiene los suyos y limitar el dibujado solo hace que se vea peor sin ganar nada. (Y el
            // limitador ya no se come nueve cuadros por el reloj gordo de Windows: ver Program y OnIdle.)
            gpuPanel.ThrottleMs = (videoOn && video != null && video.Visible && Environment.ProcessorCount < 4) ? 33 : 0;
            albumView.GpuCovered = (nav || search || discGpu || attract || AsistenteVisible);   // panel GPU encima: albumView no repinta por CPU (evita rebuild invisible al paginar)
            if (nav || search || discGpu || attract || AsistenteVisible)
            {
                if (gpuPanel.Left != 0 || gpuPanel.Top != 0 || gpuPanel.Width != ClientSize.Width || gpuPanel.Height != ClientSize.Height)
                    gpuPanel.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                gpuPanel.AlbumRect = albumView.Bounds;
                gpuPanel.ShowAlbum = nav;   // solo en navegacion se dibuja la rejilla de cubos (buscador/disco/atraccion la cubren)
                if (!gpuPanel.Visible) { gpuPanel.Visible = true; gpuPanel.BringToFront(); KeepVideoOnTop(); }
            }
            else if (gpuPanel.Visible) { gpuPanel.Visible = false; Invalidate(true); }   // disco(CPU)/video/etc: deja ver los controles normales
        }

        // Donde EMPIEZA la franja de estado de abajo (la que va en su propia textura chica).
        // Reconstruye la textura PRINCIPAL de la UI SOLO si cambio algo (firma). Devuelve la version.
        // La textura mide LO QUE EL MODO NECESITA, no la pantalla completa: en NAVEGACION solo la franja superior
        // (instrucciones + letras + encabezado, ~140px) -> rehacerla y subirla al paginar cuesta ~7x menos que
        // antes. Buscador/disco/atraccion usan hasta la franja de estado (que tiene su propia textura, mas abajo).
        // EL BUSCADOR, SUBIENDO SOLO LO QUE CAMBIA (rendimiento, 24-09-2026; ver BuscadorLigeroGpu.cs). La firma del
        // buscador (UiSig) en tres partes: lo que cambia la pantalla entera, lo que solo cambia la columna de resultados y lo
        // que solo cambia el aviso flotante. Si cambio solo una de las dos ultimas, a la tarjeta le basta con ese trozo; la
        // imagen se rehace entera igual que siempre. Cualquier otra cosa -tambien algo que se meta en UiSig y no aqui- cambia
        // la firma sin cambiar esas dos, y entonces se sube entera. La bateria 'buscaligero' comprueba que fuera del trozo no
        // cambia ni un pixel.
        private long uiBuscaFija = SigNada, uiBuscaRes = SigNada, uiBuscaAviso = SigNada;
        private Rectangle uiOverlayZona = Rectangle.Empty; private int uiOverlayZonaVer = -1;
        private void FirmasBuscador(int w, int h, int mainH, out long fija, out long res, out long aviso)
        {
            fija = SigBase; fija = Mix(fija, 'S'); fija = Mix(fija, w); fija = Mix(fija, h); fija = Mix(fija, mainH);
            fija = Mix(fija, searchOverlay.Left); fija = Mix(fija, searchOverlay.Top);
            fija = Mix(fija, searchOverlay.Width); fija = Mix(fija, searchOverlay.Height);
            if (searchOverlay.Nuevo)
            {
                // El buscador con portadas: las tarjetas no van en esta imagen (son la pieza 4), asi que no hay trozo de
                // resultados; el del aviso, como siempre.
                fija = Mix(fija, 'P'); fija = Mix(fija, searchOverlay.FirmaFija());
                res = SigBase;
                aviso = SigBase; aviso = Mix(aviso, searchOverlay.ToastOn); aviso = Mix(aviso, searchOverlay.ToastMsg);
                return;
            }
            fija = Mix(fija, searchOverlay.FocusResults);
            // Con los resultados enfocados, la mitad izquierda es el cubo de la portada SI HAY resultados, y el teclado si no
            // los hay (la misma condicion que RenderContent): quedarse sin resultados, o volver a tenerlos, cambia media
            // pantalla. Por eso va aqui y no solo con los resultados. (Lo encontro la bateria 'buscaligero'.)
            fija = Mix(fija, searchOverlay.FocusResults && searchOverlay.Results != null && searchOverlay.Results.Count > 0 && searchOverlay.GetCover != null);
            Color acento = searchOverlay.Accent;   // a una local: es un tipo por valor dentro de un Control
            fija = Mix(fija, acento.ToArgb());
            res = SigBase; res = Mix(res, searchOverlay.Query); res = Mix(res, searchOverlay.Results == null ? 0 : searchOverlay.Results.Count);
            res = Mix(res, colaVer); res = Mix(res, searchOverlay.DatosVer);
            aviso = SigBase; aviso = Mix(aviso, searchOverlay.ToastOn); aviso = Mix(aviso, searchOverlay.ToastMsg);
        }
        // Que trozo de la imagen cambio desde la version 'desde', que es la que tiene la tarjeta. Solo se sabe de la version
        // de justo antes; de cualquier otra, nada (vacio: se sube entera).
        private Rectangle UiCambioDesde(int desde)
        { return (desde == uiOverlayVer - 1 && uiOverlayZonaVer == uiOverlayVer) ? uiOverlayZona : Rectangle.Empty; }
        // El trozo de la imagen (w x mainH) que hay que volver a subir. Vacio: entera.
        private Rectangle ZonaBuscador(int w, int mainH, long fija, long res, long aviso)
        {
            if (fija != uiBuscaFija || (res == uiBuscaRes && aviso == uiBuscaAviso)) return Rectangle.Empty;
            Rectangle arriba, derecha, zona = Rectangle.Empty;
            if (!searchOverlay.ZonasAlEscribir(out arriba, out derecha)) return Rectangle.Empty;
            if (res != uiBuscaRes) zona = derecha;   // las tarjetas de los resultados, con su cabecera
            if (aviso != uiBuscaAviso)
            {
                Rectangle avisoZona = searchOverlay.ZonaAviso();
                // TextRenderer sobre PArgb puede modificar alfa de otras formas en estas mismas filas.
                // Subir la franja completa conserva TODOS los pixeles, sin subir la pantalla entera.
                avisoZona.X = 0; avisoZona.Width = searchOverlay.Width;
                zona = zona.IsEmpty ? avisoZona : Rectangle.Union(zona, avisoZona);
            }
            zona.Offset(searchOverlay.Left, searchOverlay.Top);
            zona.Intersect(new Rectangle(0, 0, w, mainH));
            return zona;
        }

        private int EnsureUiOverlay()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 8 || h < 8) return uiOverlayVer;
            int mainH;
            if (searchOpen || (panelSongs != null && panelSongs.Visible) || attractOn) mainH = BotStripTop(h);
            else
            {
                mainH = 60;   // navegacion: hasta el fondo del encabezado/letras/instrucciones
                if (helpBar != null && helpBar.Bottom > mainH) mainH = helpBar.Bottom;
                if (letterBar != null && letterBar.Visible && letterBar.Bottom > mainH) mainH = letterBar.Bottom;
                if (headerView != null && headerView.Bottom > mainH) mainH = headerView.Bottom;
                if (lblStatus != null && lblStatus.Text != null && lblStatus.Text.Length > 0 && lblStatus.Bottom > mainH) mainH = lblStatus.Bottom;
                mainH += 6;
            }
            if (mainH > h) mainH = h; if (mainH < 8) mainH = 8;
            long sig = UiSig();
            if (uiOverlay != null && uiOverlay.Width == w && uiOverlay.Height == mainH && sig == uiOverlaySig) return uiOverlayVer;
            long bFija = SigNada, bRes = SigNada, bAviso = SigNada;   // en el buscador: que trozo cambia (ver ZonaBuscador)
            Rectangle zona = Rectangle.Empty;
            if (searchOpen && searchOverlay != null)
            {
                FirmasBuscador(w, h, mainH, out bFija, out bRes, out bAviso);
                if (uiOverlay != null && uiOverlay.Width == w && uiOverlay.Height == mainH) zona = ZonaBuscador(w, mainH, bFija, bRes, bAviso);
            }
            if (uiOverlay == null || uiOverlay.Width != w || uiOverlay.Height != mainH)
            {
                if (uiOverlay != null) uiOverlay.Dispose();
                uiOverlay = new Bitmap(w, mainH, PixelFormat.Format32bppPArgb);
            }
            using (Graphics g = Graphics.FromImage(uiOverlay))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // gris: ClearType deja flecos sobre transparente
                if (searchOpen && searchOverlay != null)
                {
                    System.Drawing.Drawing2D.GraphicsState gs = g.Save();   // BUSCADOR (su UI sin starfield ni cubo)
                    g.TranslateTransform(searchOverlay.Left, searchOverlay.Top);
                    searchOverlay.RenderContent(g, new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height), true);
                    g.Restore(gs);
                }
                else if (panelSongs != null && panelSongs.Visible)
                {
                    DrawCtrlOverlay(g, helpBar);   // instrucciones ARRIBA tambien dentro del disco (guian al usuario, como en software)
                    System.Drawing.Drawing2D.GraphicsState gs = g.Save();   // INTERIOR DE DISCO: etiqueta + lista + barra de paginas (el cubo grande lo pone la GPU)
                    g.TranslateTransform(panelSongs.Left, panelSongs.Top);   // los controles del disco son hijos de panelSongs
                    DrawLabelOverlay(g, lblAlbum);
                    if (FastSongGlass) { /* vidrio, pagina y fila: texturas independientes */ }
                    else if (cancionesTransparentes && songList != null)
                    {
                        GraphicsState listState = g.Save(); g.TranslateTransform(songList.Left, songList.Top);
                        g.SetClip(new Rectangle(0, 0, songList.Width, songList.Height));
                        songList.GpuGlassNatural = NormalizaEstiloFondo(estiloFondo) == 1;
                        songList.RenderTransparentGpuTint(g, cancionesCristal, glassCapas, Lighten(bgAccent, .55)); g.Restore(listState);
                    }
                    else
                    {
                        GraphicsState listState = g.Save();
                        g.TranslateTransform(songList.Left, songList.Top);
                        g.SetClip(new Rectangle(0, 0, songList.Width, songList.Height));
                        songList.RenderOpaqueGpu(g); g.Restore(listState);
                    }
                    if (songPager != null && songPager.Visible) DrawSongPagerGpu(g);
                    g.Restore(gs);
                }
                else if (attractOn && EscaparateActivo()) { }   // con el escaparate, los textos los pone el (AtraccionGpu.cs)
                else if (attractOn)   // MODO ATRACCION: solo los dos textos (el cubo gigante y las estrellas los pone la GPU)
                {
                    RectangleF cuboA = AttractCubeRect();   // mismo rectangulo que el cubo y que la animacion
                    int cubeBottom = (int)(cuboA.Y + cuboA.Height);
                    string line1 = (attractView != null && attractView.SongCount > 0) ? (attractView.SongCount.ToString("#,0") + " canciones") : "Tu rocola";
                    using (Font fBig = new Font(FontFamily.GenericSansSerif, 24f, FontStyle.Bold))
                    using (Font fSmall = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold))
                    using (StringFormat sfc = new StringFormat())
                    using (SolidBrush tb = new SolidBrush(Color.White))
                    {
                        sfc.Alignment = StringAlignment.Center; sfc.LineAlignment = StringAlignment.Center;
                        g.DrawString(line1, fBig, tb, new RectangleF(0, cubeBottom + 18, w, 36), sfc);
                        tb.Color = Color.FromArgb(235, 255, 220, 120);   // FIJO: el "respiro" lo pone un halo por GPU (FillUiGlows) -> esta textura no se rehace nunca en atraccion
                        g.DrawString("toca un boton para elegir", fSmall, tb, new RectangleF(0, cubeBottom + 58, w, 28), sfc);
                    }
                }
                else
                {
                    DrawCtrlOverlay(g, helpBar);
                    DrawCtrlOverlay(g, buscaView);
                    if (ayudaView != null && ayudaView.Visible) DrawCtrlOverlay(g, ayudaView);   // AYUDA (su interrogacion la anima la tarjeta)
                    DrawCtrlOverlay(g, letterBar);
                    DrawCtrlOverlay(g, headerView);
                    DrawLabelOverlay(g, lblStatus);
                    DrawGuiaRojo(g);
                }
                // Las etiquetas de estado de abajo YA NO van aqui: tienen su propia textura chica
                // (EnsureUiBotOverlay), asi el reloj de la cancion no rehace esta textura 2 veces/seg.
                // El contador de FPS tampoco: lo pinta el GpuPanel en su propia textura pequeña.
            }
            uiOverlaySig = sig; uiOverlayVer++;
            uiOverlayZona = zona; uiOverlayZonaVer = uiOverlayVer;   // lo que cambio desde la version anterior (vacio: todo)
            uiBuscaFija = bFija; uiBuscaRes = bRes; uiBuscaAviso = bAviso;
            return uiOverlayVer;
        }

        // Reconstruye la textura de la FRANJA DE ESTADO de abajo (sonando/siguiente/tiempo/creditos/cola/
        // progreso) SOLO si cambio algo. Es una tira chica (~90px de alto): el tic del reloj y la barra de
        // progreso (~2 cambios/seg con musica) cuestan ~10x menos que rehacer la pantalla completa de antes.
        private Bitmap uiBotOverlay; private int uiBotVer; private long uiBotLastSig = SigNada;
        private int EnsureUiBotOverlay()
        {
            Size cs = tamanoFranjaPrueba.IsEmpty ? ClientSize : tamanoFranjaPrueba;   // (el de prueba solo lo ponen las baterias)
            int w = cs.Width, h = cs.Height;
            if (w < 8 || h < 8) return uiBotVer;
            int top = BotStripTop(h);
            int bh = h - top;
            long sig = UiBotSig();
            if (uiBotOverlay != null && uiBotOverlay.Width == w && uiBotOverlay.Height == bh && sig == uiBotLastSig) return uiBotVer;
            long fija = uiBotSigFija;   // la firma de todo menos el reloj y la barra (la apunta UiBotSig)
            // Si solo cambiaron el reloj o la barra, se repinta solo su recuadro y la tarjeta sube solo ese trozo (FranjaLigeraGpu.cs).
            Rectangle zona = (uiBotOverlay != null && uiBotOverlay.Width == w && uiBotOverlay.Height == bh) ? ZonaFranja(top, w, bh, fija) : Rectangle.Empty;
            if (uiBotOverlay == null || uiBotOverlay.Width != w || uiBotOverlay.Height != bh)
            {
                if (uiBotOverlay != null) uiBotOverlay.Dispose();
                uiBotOverlay = new Bitmap(w, bh, PixelFormat.Format32bppPArgb);
            }
            using (zona.IsEmpty ? CapturaTexto(0) : CapturaRecuadro())   // (por recuadro, las cintas de verdad no se tocan)
            using (Graphics g = Graphics.FromImage(uiBotOverlay))
            {
                if (zona.IsEmpty) g.Clear(Color.Transparent);
                else EmpiezaRecuadro(g, zona);   // solo el recuadro: lo de fuera ya esta como tiene que estar
                if (!attractOn)   // en atraccion la pantalla es limpia: la franja queda transparente
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // gris: ClearType deja flecos sobre transparente
                    g.TranslateTransform(0, -top);   // las etiquetas estan en coords del form; la tira empieza en 'top'
                    if (!FranjaLejos(g))   // la letra para leer de lejos (EstiloGpu.cs): el titulo grande y lo que sigue a la derecha
                    {
                        PintaTituloCompacto(g, false);
                        PintaArtistaCompacto(g);
                        if (lblMejorada != null && lblMejorada.Visible) DrawLabelOverlay(g, lblMejorada);   // letrero MEJORADA (su brillo es un halo por quad, no rehace esta textura)
                        if (pastGenero != null && pastGenero.Visible) DrawCtrlOverlay(g, pastGenero);
                        PintaSiguienteCompacto(g);
                    }
                    DrawLabelOverlay(g, lblTime);
                    DrawCtrlOverlay(g, pastCred);
                    DrawCtrlOverlay(g, pastCola);
                    if (!progresoFluido && !(neonIntenso && progresoNeon)) DrawCtrlOverlay(g, progress);
                }
            }
            uiBotLastSig = sig; uiBotVer++;
            ApuntaFranja(top, fija, zona);   // lo que tiene ahora, y que trozo cambio (FranjaLigeraGpu.cs)
            return uiBotVer;
        }

        // ---------- LAS PIEZAS VIVAS DEL BUSCADOR ----------
        //
        // La tecla elegida, en SU PROPIA imagen chica y con su sitio. Antes iba horneada dentro de la imagen
        // de pantalla completa, asi que mover el recuadro obligaba a rehacerla y re-subirla entera: 7 MB por
        // pulsacion a 1080p (ver UiSig). Ahora es lo mismo que ya se hacia con la franja de estado de abajo:
        // una imagen chica aparte, que se rehace solo cuando cambia de verdad. Una tecla son unos 38 KB.
        //
        // SOLO LA TECLA, y no la tarjeta de los resultados. Al principio eran las dos y estaba mal: moverse
        // por los resultados cambia ademas el cubo de la portada y el texto de debajo, que si van dentro de
        // la imagen grande. Sacarlo de la firma dejaba el cubo y el texto viejos, y el teclado sin volver.
        // La pieza 4 son las tarjetas del buscador con portadas (BuscadorPortadasGpu.cs): con el buscador de siempre no hay.
        internal const int NumPiezas = 5;
        private readonly Bitmap[] uiPieza = new Bitmap[NumPiezas];
        private readonly Rectangle[] uiPiezaRect = new Rectangle[NumPiezas];
        private readonly long[] uiPiezaSig = new long[NumPiezas];
        private readonly int[] uiPiezaVer = new int[NumPiezas];

        private void SueltaPiezas()
        {
            for (int i = 0; i < NumPiezas; i++)
            {
                if (uiPieza[i] != null) { uiPieza[i].Dispose(); uiPieza[i] = null; }
                uiPiezaRect[i] = Rectangle.Empty; uiPiezaSig[i] = SigNada; uiPiezaVer[i]++;
            }
        }

        // Prepara la pieza 'cual' y devuelve su version (sube cuando la imagen cambia). Con el buscador
        // cerrado no hay piezas: se sueltan y se contesta que no hay nada que dibujar.
        private int EnsureUiPieza(int cual)
        {
            if (cual < 0 || cual >= NumPiezas) return 0;
            if (gpuPanel != null)
            {
                gpuPanel.GlassTint = ToD3D(Lighten(bgAccent, .55));
            }
            if (FastSongGlass) return EnsureSongPiece(cual);
            if (!searchOpen || searchOverlay == null)
            {
                if (uiPieza[cual] != null) { uiPieza[cual].Dispose(); uiPieza[cual] = null; uiPiezaRect[cual] = Rectangle.Empty; uiPiezaVer[cual]++; uiPiezaSig[cual] = SigNada; }
                return uiPiezaVer[cual];
            }
            Rectangle donde;
            if (!searchOverlay.PiezaViva(cual, out donde) || donde.Width < 1 || donde.Height < 1)
            {
                if (uiPieza[cual] != null) { uiPieza[cual].Dispose(); uiPieza[cual] = null; uiPiezaRect[cual] = Rectangle.Empty; uiPiezaVer[cual]++; uiPiezaSig[cual] = SigNada; }
                return uiPiezaVer[cual];
            }
            long sig = SigBase;
            sig = Mix(sig, cual); sig = Mix(sig, donde.X); sig = Mix(sig, donde.Y);
            sig = Mix(sig, donde.Width); sig = Mix(sig, donde.Height);
            if (searchOverlay.Nuevo) sig = Mix(Mix(Mix(sig, 'P'), searchOverlay.FirmaPieza(cual)), textosDeslizantes);   // lo suyo de cada una (BuscadorPortadasGpu.cs)
            else {
            if (cual >= 2) sig = Mix(sig, searchOverlay.SelResult);
            sig = Mix(sig, searchOverlay.SelRow);
            if (cual >= 2) sig = Mix(sig, colaVer);
            sig = Mix(sig, searchOverlay.SelCol); sig = Mix(sig, searchOverlay.FocusResults);
            Color acPieza = searchOverlay.Accent;   // a una local: es un tipo por valor dentro de un Control
            sig = Mix(sig, searchOverlay.Query); sig = Mix(sig, acPieza.ToArgb());
            sig = Mix(sig, searchOverlay.DatosVer); sig = Mix(sig, textosDeslizantes);
            // Solo la caja de lo escrito mira LO TECLEADO. Metiendolo en la firma de todas, la tecla
            // elegida se reharia en cada letra sin que hubiera cambiado nada de ella.
            if (cual == SearchOverlay.PiezaEscrito) sig = Mix(sig, searchOverlay.Escrito);
            }
            if (sig == SigNada) sig = 1L;

            // El sitio se guarda SIEMPRE (aunque la imagen no cambie): al desplazarse la lista, la misma
            // tarjeta puede tener que dibujarse unos pixeles mas alla.
            Rectangle enPantalla = donde;
            enPantalla.Offset(searchOverlay.Left, searchOverlay.Top);
            uiPiezaRect[cual] = enPantalla;
            if (uiPieza[cual] != null && uiPieza[cual].Width == donde.Width && uiPieza[cual].Height == donde.Height && sig == uiPiezaSig[cual])
                return uiPiezaVer[cual];
            if (uiPieza[cual] == null || uiPieza[cual].Width != donde.Width || uiPieza[cual].Height != donde.Height)
            {
                if (uiPieza[cual] != null) uiPieza[cual].Dispose();
                uiPieza[cual] = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb);
            }
            using (cual == PiezaVistaPreviaBuscador() ? CapturaTexto(4) : new CapturaCintas(null))   // en el buscador con portadas, la tarjeta elegida (BuscadorPortadasGpu.cs)
            using (Graphics g = Graphics.FromImage(uiPieza[cual]))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // igual que la imagen grande
                // SE LE PASA EL DESPLAZAMIENTO; NO SE MUEVE EL LIENZO. La primera version movia el lienzo
                // (TranslateTransform) y en Mono se veia perfecto... pero en Windows el texto de la tecla lo
                // dibuja el SISTEMA (TextRenderer), y ese trabaja en pixeles de verdad: no hace caso a las
                // trasformaciones. La letra se iba fuera de la imagen chica y en el bar la tecla elegida
                // salia como un rectangulo rosa VACIO. Con el desplazamiento por delante cae donde debe en
                // los dos sitios.
                if (searchOverlay.Nuevo && cual == SearchOverlay.PiezaCartas)
                    uiCartasCambios = searchOverlay.PintaCartasConCambios(g, -donde.X, -donde.Y, uiPiezaSig[cual] == SigNada);
                else
                {
                    g.Clear(Color.Transparent);
                    searchOverlay.DibujaPiezaViva(g, cual, -donde.X, -donde.Y);
                }
            }
            uiPiezaSig[cual] = sig; uiPiezaVer[cual]++;
            if (cual == SearchOverlay.PiezaCartas) uiCartasCambiosVer = uiPiezaVer[cual];
            return uiPiezaVer[cual];
        }

        // Llena el buffer con los HALOS ANIMADOS de la UI, en coords de pantalla. La GPU los dibuja como quads
        // aditivos: TODA animacion continua (destello del boton pulsado, pulso de la letra actual, dorado del TOP,
        // respiro del texto de atraccion) vive AQUI y no en la textura de UI -> la textura solo se rehace cuando
        // cambia un TEXTO real. Antes estos pulsos re-horneaban y RE-SUBIAN la pantalla completa hasta ~13+10
        // veces/seg: ese era el stuttering de la rocola. Devuelve cuantos escribio.
        private int FillUiGlows(GpuPanel.UiGlow[] buf)
        {
            if (buf == null) return 0;
            int n = 0;
            if (!searchOpen && panelSongs != null && panelSongs.Visible && songPager != null && songPager.Visible && songPagerFlash != null)
            {
                for (int k = 0; k < 2 && n < buf.Length; k++)
                {
                    float level = songPagerFlash.Level(k);
                    if (level <= .01f) continue;
                    Rectangle r = songPager.Bounds;
                    float size = Math.Max(12f, Math.Min(20f, r.Height * .43f));
                    int cw = Math.Min(r.Width, Math.Max(170, (int)(size * 13)));
                    if ((r.Width - cw) / 2 - 12 < 90) continue;
                    buf[n].Cx = panelSongs.Left + (k == 0 ? r.Left + 8 + size * .5f : r.Right - 8 - size * .5f);
                    buf[n].Cy = panelSongs.Top + r.Top + r.Height * .5f;
                    buf[n].R = size * 1.25f;
                    buf[n].Argb = (uint)Color.FromArgb((int)(215 * level), 242, 206, 72).ToArgb(); n++;
                }
            }
            if (searchOpen && searchOverlay != null)
            {
                int sx = searchOverlay.Left, sy = searchOverlay.Top;
                if (winMode == 1) sy += (int)BuildWinState().Dy;
                for (int k = 0; k < 3 && n < buf.Length; k++)
                {
                    float cx, cy, r; int argb;
                    if (searchOverlay.GetHintGlow(k, out cx, out cy, out r, out argb))
                    { buf[n].Cx = sx + cx; buf[n].Cy = sy + cy; buf[n].R = r; buf[n].Argb = (uint)argb; n++; }
                }
                float tcx, tcy, tr; int targb;   // la tecla que se acaba de escribir (el buscador con portadas)
                if (n < buf.Length && searchOverlay.GetDestelloTecla(GpuPanel.AhoraMs(), out tcx, out tcy, out tr, out targb))
                { buf[n].Cx = sx + tcx; buf[n].Cy = sy + tcy; buf[n].R = tr; buf[n].Argb = (uint)targb; n++; }
            }
            else if (attractOn)
            {
                // Respiro DORADO detras de "toca un boton para elegir" (el texto va fijo en la textura).
                int w = ClientSize.Width, h = ClientSize.Height;
                int side = (int)(Math.Min(w, h) * 0.40); if (side < 80) side = 80;
                int cubeBottom = (h - side) / 2 - 30 + side;
                double p = 0.5 + 0.5 * Math.Sin(phase * 3.0);
                float apaga = UiAlfaAtraccion();   // se apaga con su texto cuando sale la lista de lo mas pedido (AtraccionGpu.cs)
                if (!EscaparateActivo() && apaga > 0f)   // con el escaparate, su respiro va con su mensaje
                {
                    buf[n].Cx = w / 2f; buf[n].Cy = cubeBottom + 72; buf[n].R = 150f;
                    buf[n].Argb = (uint)Color.FromArgb((int)((16 + 38 * p) * apaga), 255, 220, 120).ToArgb(); n++;
                }
            }
            else
            {
                if (helpBar != null && helpBar.Visible)   // destello del boton pulsado (navegacion e interior de disco)
                {
                    int hx = helpBar.Left, hy = helpBar.Top;
                    for (int k = 0; k < 4 && n < buf.Length; k++)
                    {
                        float cx, cy, r; int argb;
                        if (helpBar.GetFlashGlow(k, out cx, out cy, out r, out argb))
                        { buf[n].Cx = hx + cx; buf[n].Cy = hy + cy; buf[n].R = r; buf[n].Argb = (uint)argb; n++; }
                    }
                }
                if (buscaView != null && buscaView.Visible && n < buf.Length)
                {
                    float bcx, bcy, br;
                    if (buscaView.GetLatido(out bcx, out bcy, out br))
                    {
                        double p = 0.5 + 0.5 * Math.Sin(phase * 2.4);   // el mismo ritmo que por CPU
                        buf[n].Cx = buscaView.Left + bcx; buf[n].Cy = buscaView.Top + bcy; buf[n].R = br;
                        buf[n].Argb = (uint)Color.FromArgb((int)(30 + 70 * p), 90, 190, 255).ToArgb(); n++;
                    }
                }
                if (letterBar != null && letterBar.Visible)
                {
                    float cx, cy, r;
                    if (n < buf.Length && letterBar.GetCurrentGlow(out cx, out cy, out r))   // pulso de la letra actual
                    {
                        double p = 0.5 + 0.5 * Math.Sin(phase * 2.2);   // mismo ritmo que antes, pero ahora a cuadro pleno y GRATIS
                        buf[n].Cx = letterBar.Left + cx; buf[n].Cy = letterBar.Top + cy; buf[n].R = r;
                        Color pl = ColorPulsoLetra();   // el color del genero de la letra, o el azul de siempre (EstiloGpu.cs)
                        buf[n].Argb = (uint)Color.FromArgb((int)(45 + 65 * p), pl.R, pl.G, pl.B).ToArgb(); n++;
                    }
                    if (n < buf.Length && letterBar.GetTopGlow(out cx, out cy, out r))       // dorado pulsante del marcador TOP
                    {
                        double p = 0.5 + 0.5 * Math.Sin(phase * 2.6);
                        buf[n].Cx = letterBar.Left + cx; buf[n].Cy = letterBar.Top + cy; buf[n].R = r;
                        buf[n].Argb = (uint)Color.FromArgb((int)(22 + 50 * p), 255, 205, 90).ToArgb(); n++;
                    }
                }
            }
            n = FillGuiaRojoGlows(buf, n);
            // Atenuar tambien los halos de los controles, no los de la cancion
            // actual. Se aplica al alfa, sin invalidar ni rehornear la interfaz.
            if (gpuPanel != null)
                for (int i = 0; i < n; i++) buf[i].Argb = GpuPanel.FadeArgb(buf[i].Argb, gpuPanel.ControlsOpacity);
            // Letrero "MEJORADA" (franja de estado, visible en todos los modos menos atraccion): un resplandor
            // dorado suave que RESPIRA + un destello que RECORRE las letras cada ~4s (reflejo metalico).
            // El texto va FIJO en la textura de la franja: estos halos no rehacen NINGUNA textura.
            if (!attractOn && lblMejorada != null && lblMejorada.Visible && n + 1 < buf.Length)
            {
                RectangleF badgeGlow = gpuPanel != null ? gpuPanel.IdleBadgeGlow(RectMejorada()) : RectMejorada();   // donde se ve (de lejos, un renglon mas abajo)
                float cy2 = badgeGlow.Top + badgeGlow.Height / 2f;
                double pb = 0.5 + 0.5 * Math.Sin(phase * 2.8);
                buf[n].Cx = badgeGlow.Left + badgeGlow.Width / 2f; buf[n].Cy = cy2;
                buf[n].R = badgeGlow.Width * 0.72f;
                buf[n].Argb = (uint)Color.FromArgb((int)(9 + 15 * pb), 255, 210, 100).ToArgb(); n++;
                double sw = (phase * 0.22) % 1.0;   // ciclo ~4s: 55% barrido de izq a der, 45% descanso
                if (sw < 0.55)
                {
                    double t = sw / 0.55;
                    int a = (int)(95 * Math.Sin(Math.PI * t));   // entra y sale suave
                    buf[n].Cx = badgeGlow.Left + (float)(t * badgeGlow.Width); buf[n].Cy = cy2; buf[n].R = 26f;
                    buf[n].Argb = (uint)Color.FromArgb(a, 255, 232, 150).ToArgb(); n++;
                }
            }
            return n;
        }

        // FIRMA de lo que hay que dibujar. Se compara CADA CUADRO para saber si hay que rehacer la textura,
        // asi que no puede armar una cadena: eso eran dos StringBuilder y dos cadenas nuevas por cuadro (~70 KB
        // por segundo de basura para el recolector, que es justo lo que provoca microtirones). Ahora se mezcla
        // todo en un numero de 64 bits (FNV-1a), sin reservar ni un byte. Dos estados distintos podrian caer en
        // el mismo numero, pero con 64 bits eso es 1 entre 18 trillones: y ni asi se rompe nada, solo tardaria
        // un cambio de texto en verse hasta el siguiente cambio.
        private const long SigNada = 0L;                     // valor imposible de la mezcla: fuerza rehacer
        private const long SigBase = unchecked((long)1469598103934665603L);
        private static long Mix(long h, int v) { unchecked { h ^= (uint)v; h *= 1099511628211L; } return h; }
        private static long Mix(long h, long v) { return Mix(Mix(h, (int)v), (int)(v >> 32)); }
        private static long Mix(long h, bool b) { return Mix(h, b ? 1 : 0); }
        private static long Mix(long h, string t) { return Mix(h, t == null ? 0 : t.GetHashCode()); }
        private long UiSig()
        {
            long sb = SigBase;
            sb = Mix(sb, ClientSize.Width); sb = Mix(sb, ClientSize.Height);
            if (searchOpen && searchOverlay != null)   // parte superior = buscador
            {
                if (searchOverlay.Nuevo)   // el buscador con portadas: aqui solo va lo quieto (BuscadorPortadasGpu.cs)
                {
                    sb = Mix(sb, 'P'); sb = Mix(sb, searchOverlay.FirmaFija());
                    sb = Mix(sb, searchOverlay.ToastOn); sb = Mix(sb, searchOverlay.ToastMsg);
                }
                else {
                sb = Mix(sb, 'S'); sb = Mix(sb, searchOverlay.Query);
                sb = Mix(sb, searchOverlay.Results == null ? 0 : searchOverlay.Results.Count);
                sb = Mix(sb, searchOverlay.ToastOn); sb = Mix(sb, searchOverlay.ToastMsg);
                sb = Mix(sb, colaVer);   // las marcas "EN COLA" y "SUENA AHORA" van horneadas en las tarjetas
                // LA TECLA ELEGIDA YA NO ENTRA AQUI. Entraba, y eso significaba que pulsar una flecha -que
                // solo cambia QUE tecla lleva el recuadro- obligaba a rehacer y RE-SUBIR la pantalla completa:
                // 5.91 ms por pulsacion a 1080p, el 35% de un cuadro. Ahora la tecla elegida sale como PIEZA
                // VIVA aparte, en su propia imagen chica (ver SearchOverlay.PiezaViva y EnsureUiPieza),
                // exactamente igual que ya se hacia con el destello de los botones y la franja de abajo.
                //
                // PERO ESTA SI TIENE QUE ESTAR, y sacarla fue un error que se vio en el bar:
                //   FocusResults cambia MEDIA PANTALLA -donde estaba el teclado se pone el cubo de la
                //     portada-. Sin el, al volver de los resultados al teclado la imagen no se rehacia y EL
                //     TECLADO NO VOLVIA A APARECER hasta cerrar y abrir el buscador.
                //
                // SelResult ESTUVO AQUI y ya no hace falta, que no es lo mismo que "no hacia falta". Con el
                // cubo delante cambia que disco se ensena y el texto de debajo, y mientras eso se dibujaba
                // DENTRO de esta imagen, sacarlo dejaba media pantalla congelada. Ahora la vista previa es
                // una PIEZA aparte -siempre, desde que SeparateGpu dejo de tener interruptor-, asi que la
                // promesa vive en la firma de la pieza (ver EnsureUiPieza) y rehacer la pantalla entera por
                // mover la seleccion seria trabajo tirado. La bateria 'mover' vigila las dos mitades.
                sb = Mix(sb, searchOverlay.FocusResults);
                Color searchAccent = searchOverlay.Accent;
                sb = Mix(sb, searchOverlay.DatosVer); sb = Mix(sb, searchAccent.ToArgb());
                }
            }
                  // El DESTELLO del boton pulsado YA NO entra en la firma: lo dibuja la GPU como halo aparte (FillUiGlows),
                  // asi que su desvanecido no re-hornea ni re-sube la textura del teclado -> tecleo fluido con brillo suave.
                  // Tampoco la fase del teclado (carisima). En reposo la firma es constante -> 0 reconstrucciones.
            else if (panelSongs != null && panelSongs.Visible)   // INTERIOR DE DISCO: etiqueta, seleccion, pagina
                // La GEOMETRIA entra en la firma: al arrancar o terminar un video, LayoutControls REACOMODA el
                // interior (con video, la lista y el cubo se comprimen a la mitad izquierda). Sin la geometria,
                // la firma no cambiaba y la textura quedaba horneada con las posiciones VIEJAS -la lista flotando
                // donde ya no va- hasta salir y volver a entrar al disco. El cubo no lo sufria (es en vivo).
            {
                sb = Mix(sb, cancionesTransparentes);
                sb = Mix(sb, cancionesCristal);
                sb = Mix(sb, FastSongGlass);
                sb = Mix(sb, 'D'); sb = Mix(sb, lblAlbum != null ? lblAlbum.Text : ""); sb = Mix(sb, songItems.Count);
                sb = Mix(sb, songList != null ? songList.PageStart : 0);
                if (!FastSongGlass) { sb = Mix(sb, songSel); sb = Mix(sb, bgAccent.ToArgb()); }
                sb = Mix(sb, colaVer);   // sin esto, la marca "EN COLA" no saldria hasta que algo mas cambiara
                sb = Mix(sb, generoVer);   // idem para las pastillas de genero, que se aprenden en segundo plano
                // La lista grande, el ecualizador y los minutos de la cola (DiscoVivoGpu.cs) cambian lo horneado.
                if (songList != null) { sb = Mix(sb, songList.Vivo); sb = Mix(sb, songList.RowH); sb = Mix(sb, songList.EsperaVer); }
                sb = Mix(sb, panelSongs.Left); sb = Mix(sb, panelSongs.Top); sb = Mix(sb, panelSongs.Width); sb = Mix(sb, panelSongs.Height);
                // Con capas, la GPU mueve las texturas: el reloj no invalida el horneado.
                // Solo la ruta GPU compuesta conserva el repintado del desplazamiento.
                if (!FastSongGlass && songList != null && songList.Sliding) sb = Mix(sb, (int)(songList.SlidePaso * 128f));
            }
            else if (attractOn)                        // MODO ATRACCION: solo la cantidad de canciones (el pulso del texto va como halo por quad)
            { sb = Mix(sb, 'A'); sb = Mix(sb, attractView != null ? attractView.SongCount : 0); sb = Mix(sb, EscaparateActivo() ? 1 : 0); }
            else if (letterBar != null)                // parte superior = navegacion (A-Z + encabezado)
            {
                sb = Mix(sb, 'N'); sb = Mix(sb, letterBar.Current); sb = Mix(sb, letterBar.Letters);
                sb = Mix(sb, letterBar.LupaGpu);   // encender o apagar la lupa rehace la barra
                sb = Mix(sb, GuiaRojoVisible());
                sb = Mix(sb, letterBar.Available == null ? 0 : letterBar.Available.Count);
                // Los NUMEROS y los COLORES de las letras los va aprendiendo el paseo de fondo, y el
                // DESLIZAMIENTO al cambiar de letra se mueve solo. Sin esto, en modo GPU la textura no se
                // rehacia: los numeros no aparecian hasta cambiar de letra y el deslizamiento no se veia.
                sb = Mix(sb, letterBar.DatosSig());
                // La casilla de BUSCAR entra por si esta abierta o cerrada, NO por su parpadeo: el parpadeo
                // lo pone la tarjeta como halo aparte, y meterlo aqui obligaria a rehacer la textura entera
                // veinticuatro veces por segundo.
                if (buscaView != null) { sb = Mix(sb, buscaView.Visible); sb = Mix(sb, buscaView.Abierto); }
                if (ayudaView != null) { sb = Mix(sb, ayudaView.Visible); sb = Mix(sb, ayudaView.Compacta); sb = Mix(sb, ayudaView.Abierta); sb = Mix(sb, ayudaView.Width); }
                if (letterBar.AnimViva) sb = Mix(sb, (int)(letterBar.AnimPaso * 64f));
                if (headerView != null)
                {
                    Color acCab = headerView.Accent;   // a una local: es un tipo por valor dentro de un Control
                    sb = Mix(sb, headerView.Section); sb = Mix(sb, headerView.Page);
                    sb = Mix(sb, headerView.Total); sb = Mix(sb, acCab.ToArgb());
                }
                sb = Mix(sb, lblStatus != null ? lblStatus.Text : "");
            }
            // NI la fase del pulso de la letra NI el destello del boton pulsado (FlashSig) entran ya en la firma:
            // ambos los dibuja la GPU como halos aparte (FillUiGlows). Antes obligaban a rehacer Y RE-SUBIR esta
            // textura ~13 veces/seg en navegacion (pulso) y ~10 veces por CADA tecla (destello): ESE era el
            // stuttering de la rocola al paginar. Ahora, quieto en una pagina, la firma es constante -> 0 rehechas.
            // Las etiquetas de estado de abajo (sonando/tiempo/creditos/progreso) van en SU PROPIA textura chica
            // (UiBotSig/EnsureUiBotOverlay): el reloj de la cancion ya no rehace la pantalla completa 2 veces/seg.
            // El FPS tambien va en su propia textura (1/seg, diminuta).
            return (sb == SigNada) ? 1L : sb;   // que nunca coincida con "sin firma"
        }

        // Firma de la FRANJA DE ESTADO de abajo (sonando/siguiente/tiempo/creditos/cola/progreso): cambia ~2
        // veces/seg con musica sonando, pero su textura es una tira chica (~400KB vs ~4MB de pantalla completa).
        private long UiBotSig()
        {
            long sb = SigBase;
            sb = Mix(sb, ClientSize.Width); sb = Mix(sb, ClientSize.Height);
            if (attractOn) { uiBotSigFija = SigNada; return Mix(sb, 'A'); }   // en atraccion la franja va limpia (transparente)
            sb = Mix(sb, textosDeslizantes);
            sb = Mix(sb, LetraLejosActiva() ? 1 : 0);   // la letra de lejos cambia como se hornea (EstiloGpu.cs)
            if (lblNowTop != null) sb = Mix(sb, lblNowTop.Text);
            if (lblNow != null) sb = Mix(sb, lblNow.Text);
            if (lblMejorada != null && lblMejorada.Visible) { sb = Mix(sb, 'M'); sb = Mix(sb, lblMejorada.Left); sb = Mix(sb, lblMejorada.ForeColor.ToArgb()); }
            if (pastGenero != null && pastGenero.Visible) { Color tg = pastGenero.Tinte; sb = Mix(sb, pastGenero.Texto); sb = Mix(sb, tg.ToArgb()); sb = Mix(sb, pastGenero.Left); }
            if (lblNext != null) sb = Mix(sb, lblNext.Text);
            if (lblTime != null) { Rectangle rt = lblTime.Bounds; sb = Mix(sb, rt.X); sb = Mix(sb, rt.Y); sb = Mix(sb, rt.Width); sb = Mix(sb, rt.Height); }   // donde va el reloj (lo que dice, al final)
            if (pastCred != null) { Color ac = pastCred.Acento; sb = Mix(sb, pastCred.Etiqueta); sb = Mix(sb, pastCred.Valor); sb = Mix(sb, ac.ToArgb()); sb = Mix(sb, pastCred.Left); sb = Mix(sb, pastCred.Paso); }
            if (pastCola != null)
            {
                sb = Mix(sb, pastCola.Valor); sb = Mix(sb, pastCola.Left); sb = Mix(sb, pastCola.Mas);
                // Y CUALES son. Sin esto, con tarjeta grafica una portadita que llega tarde -se decodifica
                // en otro hilo- no se veria aparecer: la firma no cambiaria y la textura no se reharia.
                if (pastCola.Portadas != null)
                    for (int i = 0; i < pastCola.Portadas.Length; i++)
                        sb = Mix(sb, (pastCola.Portadas[i] != null) ? 1 : 0);
            }
            sb = Mix(sb, progresoFluido ? 1 : 0);
            sb = Mix(sb, neonIntenso); sb = Mix(sb, progresoNeon);
            if (progress != null && !progresoFluido && !(neonIntenso && progresoNeon))
            {
                Rectangle rp = progress.Bounds; sb = Mix(sb, rp.X); sb = Mix(sb, rp.Y); sb = Mix(sb, rp.Width); sb = Mix(sb, rp.Height);
                sb = Mix(sb, progress.Accent.ToArgb()); sb = Mix(sb, progress.Neon ? 1 : 0);
            }
            // EL RELOJ Y LA BARRA, AL FINAL: lo de antes es la firma de todo lo demas. Si solo cambian ellos, la franja se
            // repinta solo por su recuadro y la tarjeta sube solo ese trozo (FranjaLigeraGpu.cs).
            uiBotSigFija = sb;
            if (lblTime != null) sb = Mix(sb, lblTime.Text);
            sb = Mix(sb, BitConverter.DoubleToInt64Bits(BarraEnFranja()));   // la barra, con su valor exacto
            return (sb == SigNada) ? 1L : sb;   // que nunca coincida con "sin firma"
        }

        // Dibuja un control (HelpBar/LetterBar/ProgressView) en la textura de UI, en su posicion del form.
        private static void DrawCtrlOverlay(Graphics g, Control c)
        {
            if (c == null || c.Width < 1 || c.Height < 1) return;
            System.Drawing.Drawing2D.GraphicsState st = g.Save();
            g.TranslateTransform(c.Left, c.Top);
            HelpBar hb = c as HelpBar;
            if (hb != null) hb.RenderContent(g, true);   // forGpu: sus pulsos van como halos por quad, no en la textura
            else { HeaderView hv = c as HeaderView; if (hv != null) hv.RenderContent(g);
            else { LetterBar lb = c as LetterBar; if (lb != null) lb.RenderContent(g, true);
            else { PastillaView pa = c as PastillaView; if (pa != null) pa.RenderContentGpu(g);   // con la cancion que llega a la cola (DiscoVivoGpu.cs)
            else { ProgressView pv = c as ProgressView; if (pv != null) pv.RenderContent(g);
            else { GeneroView gv = c as GeneroView; if (gv != null) gv.RenderContent(g);
            else { SongListView sl = c as SongListView; if (sl != null) { sl.pintandoGpu = true; sl.RenderContent(g); }
            else { SongPagerBar sp = c as SongPagerBar; if (sp != null) sp.RenderContent(g);
            else { BuscaView bv = c as BuscaView; if (bv != null) bv.RenderContent(g, true);
            else { AyudaView av = c as AyudaView; if (av != null) av.RenderContent(g, true); } } } } } } } } }
            g.Restore(st);
        }

        // Dibuja el texto de una etiqueta en la textura de UI, respetando su fuente, color, alineacion y caja.
        // GDI reusables (StringFormat + pincel al que se muta el color): esto se llama ~6-7 veces por cada
        // reconstruccion de la textura de UI (que ocurre ~13fps en navegacion por el brillo de la letra).
        [ThreadStatic] private static StringFormat lblSf;
        [ThreadStatic] private static SolidBrush lblBr;
        private static void DrawLabelOverlay(Graphics g, Label l) { DrawLabelOverlay(g, l, (l != null) ? l.ForeColor : Color.White); }
        private static void DrawLabelOverlay(Graphics g, Label l, Color color)
        {
            if (l == null || string.IsNullOrEmpty(l.Text)) return;
            StringFormat sf = lblSf ?? (lblSf = new StringFormat());
            SolidBrush b = lblBr ?? (lblBr = new SolidBrush(Color.White)); b.Color = color;
            sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
            ContentAlignment a = l.TextAlign;
            if ((a & (ContentAlignment.TopLeft | ContentAlignment.MiddleLeft | ContentAlignment.BottomLeft)) != 0) sf.Alignment = StringAlignment.Near;
            else if ((a & (ContentAlignment.TopRight | ContentAlignment.MiddleRight | ContentAlignment.BottomRight)) != 0) sf.Alignment = StringAlignment.Far;
            else sf.Alignment = StringAlignment.Center;
            if ((a & (ContentAlignment.TopLeft | ContentAlignment.TopCenter | ContentAlignment.TopRight)) != 0) sf.LineAlignment = StringAlignment.Near;
            else if ((a & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0) sf.LineAlignment = StringAlignment.Far;
            else sf.LineAlignment = StringAlignment.Center;
            g.DrawString(l.Text, l.Font, b, (RectangleF)l.Bounds, sf);
        }

#endif

        // ---------- PRECALENTADO: QUE LA PRIMERA VEZ VAYA COMO LAS DEMAS ----------
        //
        // Casi todo lo que la rocola dibuja se HORNEA la primera vez que hace falta -el teclado del buscador
        // son 40 teclas con su degradado, su contorno y su letra; la lista de un disco se guarda hecha para
        // poder deslizarla- y eso cae DENTRO del primer cuadro: el cliente pulsa, la rocola se para a hornear
        // y el movimiento arranca a tirones. De la segunda vez en adelante va fino.
        //
        // Medido con pruebas/primera.cs en el servidor (en la rocola del bar hay que multiplicar por 4 o 5):
        //
        //     el buscador al abrirse .... 20.8 ms la 1a vez contra 1.9 ms despues
        //     la lista de un disco ......  3.6 ms  ...  contra 0.2 ms
        //     el encabezado y la pastilla de creditos: 0.02 y 0.06 ms de mas. NO se precalientan, no ganan nada.
        //
        // AQUI SOLO VAN LAS PANTALLAS QUE AL ENCENDER NO SE VEN. La pared de discos y la barra de letras
        // tambien cuestan su primera vez, pero es que ELLAS SON la pantalla de arranque: ese cuadro se paga
        // igual, adelantarlo no adelanta nada. El buscador, la lista de un disco y la pantalla de reposo
        // aparecen despues, y ahi si se gana entero.
        //
        // Y se calienta DIBUJANDO de verdad, una vez, en un mapa de bits de usar y tirar. Se podria llamar a
        // mano a cada Ensure*/Hornea*, pero esa lista se queda vieja en cuanto alguien anada un dibujo nuevo;
        // dibujando se pasa por el MISMO camino que el dibujo de verdad y se calienta lo que ese camino
        // calienta, hoy y dentro de un ano.
        private int precalPaso = 0;             // que pieza toca (-1 = ya esta todo caliente)
        private int precalReintentos = 0;       // si una pieza aun no tiene tamano, se reintenta un rato
        private const int PrecalQuieta = 400;   // ms sin tocar nada para ponerse a calentar

        // Dibuja un control una vez en un mapa de bits de usar y tirar. Nada de esto se ve: lo unico que hace
        // es que lo que ese dibujo hornea quede hecho. false = todavia no tiene tamano, se reintenta luego.
        private bool EnSeco(Control c, Action<Graphics> dibuja)
        {
            if (c == null || c.Width < 8 || c.Height < 8) return false;
            using (Bitmap b = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                dibuja(g);
            }
            return true;
        }

        // Al cambiar la RESOLUCION lo horneado deja de servir -esta hecho al tamano de antes- y hay que
        // volver a calentar; si no, la primera vez despues del cambio vuelve a trabarse.
        //
        // Con el color de acento NO se hace, a proposito: el acento cambia con cada cancion (es el color del
        // genero), asi que volver a calentar en cada cambio seria estar horneando toda la noche para ahorrar
        // una vez. Ahi se deja que se rehornee solo cuando de verdad se dibuje.
        private void VuelveAPrecalentar() { precalPaso = 0; precalReintentos = 0; }

        private static string Fmt(TimeSpan t) { return ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00"); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (vigiaPantalla != null) vigiaPantalla.Para();   // cerrar tarda lo que tenga que tardar: no es una parada
            DetieneVigiaFoco();
            AdvanceThumbWarm();
            CancelStartupDataLoad();
#if ROCOLA_GPU
            StopCoverReaders();
#endif
            preScanStop = true;   // que el pre-analisis suelte el archivo y termine
            unchecked { preScanGen++; durScanGen++; }
            DisposePlaybackNow();
            SueltaLookahead();   // el lector que esperaba a la siguiente: se suelta el archivo al cerrar
            GuardaTodoAlCerrar();
            // APAGADO DELIBERADO vs APAGON. El boton de poder de la rocola esta configurado en Windows para APAGAR
            // -> al presionarlo, Windows avisa y ESTE evento se dispara (CloseReason.WindowsShutDown; tambien Alt+F4
            // = UserClosing). Ahi borramos el estado vivo para que manana arranque LIMPIA. En cambio, un APAGON corta
            // la corriente de golpe y no corre NADA de esto: el archivo SOBREVIVE y, al volver la luz, la rocola
            // REANUDA (creditos + cola). Es decir, la sola presencia del archivo al arrancar delata que fue apagon.
            // Si ni siquiera publicamos los datos iniciales, conservar tambien el estado anterior.
            if (!startupReadPending && (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.UserClosing))
                BorraEstado();
            else WaitDataWrites(15000);   // cierre por otra causa: que el ultimo estado encargado llegue al disco
            Bitacora.Vacia(3000);   // y que la bitacora no se quede con apuntes en la cola
            base.OnFormClosing(e);
        }

        // ==========  VIDEO: host del control ActiveX de Windows Media Player  ==========
        // (CLSID del OCX de wmp.dll)
        private class VideoHost : AxHost
        {
            public VideoHost() : base("6bf52a52-394a-11d3-b153-00c04f79faa6") { }
            public object GetOcxObj() { return this.GetOcx(); }
        }

        // Un elemento de la cola: una cancion O un video. Disc = disco para el cubo de abajo.
        private class PlayItem
        {
            public bool IsVideo;
            public Track Song;
            public string VideoPath;
            public Album Disc;
            public string PathKey()
            {
                if (IsVideo) return VideoPath ?? "";
                return (Song != null && Song.Path != null) ? Song.Path : "";
            }
            public string Display()
            {
                if (IsVideo)
                {
                    // Mostrar "album - nombre del video": el disco (carpeta) MAS el nombre del archivo.
                    string disc = (Disc != null && !string.IsNullOrEmpty(Disc.Artist)) ? Disc.Artist : "";
                    string file = "";
                    try { file = System.IO.Path.GetFileNameWithoutExtension(VideoPath); } catch { }
                    string n;
                    if (disc.Length > 0 && file.Length > 0) n = disc + " - " + file;
                    else if (file.Length > 0) n = file;
                    else n = disc;
                    return "\u25BA Video:  " + n;
                }
                return (Song != null) ? (Song.Artist + " - " + Song.Title) : "";
            }

            // Lo mismo, pero PARTIDO: arriba lo que suena y debajo de quien es. La franja de abajo los pone en
            // dos renglones distintos, con distinto tamano y distinto color, asi que no vale con una sola
            // cadena "Artista - Titulo" cortada por el guion (hay titulos con guiones).
            public void TituloArtista(out string titulo, out string artista)
            {
                if (IsVideo)
                {
                    string disc = (Disc != null && !string.IsNullOrEmpty(Disc.Artist)) ? Disc.Artist : "";
                    string file = "";
                    try { file = System.IO.Path.GetFileNameWithoutExtension(VideoPath); } catch { }
                    titulo = (file.Length > 0) ? file : disc;
                    artista = "\u25BA Video" + ((disc.Length > 0 && file.Length > 0) ? ("  -  " + disc) : "");
                    return;
                }
                titulo = (Song != null && !string.IsNullOrEmpty(Song.Title)) ? Song.Title : "";
                artista = (Song != null && !string.IsNullOrEmpty(Song.Artist)) ? Song.Artist : "";
            }
        }

        // Un archivo de video dentro de un disco; en la lista se ve solo el nombre sin extension
        private class VideoItem
        {
            public string Path;
            public VideoItem(string p) { Path = p; }
            public override string ToString()
            {
                try { return System.IO.Path.GetFileNameWithoutExtension(Path); }
                catch { return Path; }
            }
        }
    }
}

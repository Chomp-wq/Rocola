// EL VIDEO: reproduccion, pantalla completa y la limpieza de los que no suenan
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la interfaz que se sube a la tarjeta (EnsureUiOverlay, EnsureUiBotOverlay, EnsureUiPieza, DrawCtrlOverlay, DrawLabelOverlay) y el precalentado (EnSeco) dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+, y llaman directo, con el lienzo y su estado, a las vistas ya traducidas (el buscador, la casilla de BUSCAR, la barra de ayuda y las de Form1.Vistas.cs, DiscoVivoGpu.cs y AyudaCasillaGpu.cs); lo que aun es de la capa (los Bitmap e Image que piden D3D9Star.cs y la cache de portadas, las letras de las etiquetas de WinForms, el precalentado de la atraccion) pasa por el puente (Plataforma/Puente.cs y Puente.video-form1.cs)
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
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
        private Reloj videoPoll;   // EN LINUX: Reloj de la pantalla nativa (Nativo/Reloj.cs; en XP, Timer de WinForms: SetTimer y WM_TIMER en el hilo de la ventana)
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
        // EN LINUX (traduccion de XP): en el A4, con Windows XP, los videos no se miden (Media Foundation no existe antes de Vista); aqui
        // la version del sistema seria la del nucleo (6.x) y los mediria. Igual que en el bar: sin medir (medirlos va despues, en la fase 11).
        private volatile bool videoSinMedir = true; // Media Foundation no existe antes de Vista
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
            video.AlFrente();   // EN LINUX: BringToFront: la capa del video, encima en la pantalla
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
        private const int VideoMinMedidos = 8;        // con menos videos medidos no se elige nada: manda la perilla
        // ¿El volumen base de los videos lo esta eligiendo el programa DE VERDAD? Solo en automatico, con el nivelado encendido y con
        // bastantes videos medidos; en cualquier otro caso VideoBase devuelve la perilla, y la perilla tiene que poder moverse (en la
        // rocola con Windows XP los videos no se miden: ahi el automatico de fabrica nunca elige nada). Lo usa Ctrl+F3.
        private bool VideoAutoManda(bool automatico, bool nivelar) { return automatico && nivelar && VideoHayMedidas(VideosMedidos()); }
        // Un solo predicado para VideoBase y para Ctrl+F3: con estos videos medidos (y un objetivo de nivelado con sentido) el programa elige.
        private bool VideoHayMedidas(int medidos) { return medidos >= VideoMinMedidos && normTarget > 0.0001f; }
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
            if (!VideoHayMedidas(nec.Count)) { videoBaseAuto = videoVol; return videoBaseAuto; }   // sin medidas suficientes: la perilla
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
            // EL MODO NOCHE, AQUI FUERA (y no en MasterLin): la rama del video medido se salta la ganancia general, y esos videos -la
            // mayoria- seguirian a todo volumen a las tres de la manana. A la perilla del reproductor de Windows se le aplica el % del
            // deslizador tal cual (al 80 %, la perilla al 80 % de lo que iba), siguiendo la rampa. El sondeo del video lo vuelve a leer
            // cada 400 ms, asi que el que ya suena baja solo.
            v *= FactorVideoNoche();
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
                if (!video.Disponible) return;   // EN LINUX: (GetOcxObj() == null) sin libmpv no hay reproductor (Nativo/ReproductorVideo.cs)
                video.Volumen = v;   // EN LINUX: settings.volume del WMP: el volumen del reproductor de libmpv, de 0 a 100 como el del WMP
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
                    video = new ReproductorVideo();   // EN LINUX: el reproductor de libmpv (Nativo/ReproductorVideo.cs) en lugar del control ActiveX del WMP
                    // EN LINUX: (TabStop = false) la capa del video no es una ventana: nunca toma el foco
                    video.EnPantalla(this);   // EN LINUX: (Controls.Add) la capa del video entra en la pantalla de la rocola, debajo de lo que ya hay, como el control
                    video.Inicia();   // EN LINUX: (video.Handle, que crea el control ActiveX) abre libmpv
                }
                video.Visible = !searchOpen;   // si el buscador esta abierto, el cuadro queda oculto (el audio sigue)
#if ROCOLA_GPU
                if (AsistenteVisible) video.Visible = false;
#endif

                if (!video.Disponible) throw new Exception("control WMP nulo");   // EN LINUX: (GetOcxObj() == null) sin libmpv no hay reproductor, como un XP sin el WMP
                // EN LINUX: (XP) if (ocx == null) throw new Exception("control WMP nulo");

                // EN LINUX: (uiMode "none" y stretchToFit) libmpv pinta solo la imagen, sin botones, ajustada al hueco sin deformarla
                // EN LINUX: (XP) Set(ocx, "stretchToFit", true);
                // Sin el menu del reproductor (novena tanda): con el boton derecho, o dejando el dedo en una pantalla tactil,
                // salia el de Windows Media Player, con Pausa, Detener y Pantalla completa a mano del cliente.
                // EN LINUX: (enableContextMenu = false) el video de libmpv no tiene menu: los clics los recibe la rocola
                // EN LINUX: (XP) object settings = Get(ocx, "settings");
                videoVolSet = -1;   // fuerza el primer envio del volumen a este WMP recien apuntado
                int vol0 = VideoVolumeFor(path);
                video.Volumen = vol0;   // EN LINUX: settings.volume (y settings.autoStart = true: Carga empieza a tocar en cuanto abre, como el WMP con autoStart)
                videoVolSet = vol0;
                video.Carga(path);   // con autoStart=true deberia arrancar solo   // EN LINUX: URL = path
                try { video.Toca(); } catch { }   // empujon explicito: autoStart no siempre engancha al instanciar el ActiveX   // EN LINUX: controls.play()

                videoOn = true;
                progress.Value = 0; lblTime.Text = "0:00 / 0:00";   // limpia el tiempo de la cancion anterior; VideoPoll lo llena con pos/dur del video
                videoLastPos = -1; videoProgressTick = Environment.TickCount; videoNudged = false; videoMaxPos = 0;   // arrancar el vigia de avance
                videoPoll.Interval = 400;   // sondeo normal (baja a 80 ms durante el fundido del final)
                video.AlFrente();   // EN LINUX: BringToFront: la capa del video, encima en la pantalla
                videoPoll.Start();
                Diag("video: inicia");
            }
            catch (Exception ex)
            {
                videoOn = false;
                try { if (video != null) video.Visible = false; } catch { }
                SueltaVideoRoto();   // antes de devolver el credito: eso reacomoda la pantalla y volveria a tocar el control
                RefundCredit();   // compensacion al cliente: el video no abrio
                ShowToast("No se pudo abrir el video: credito devuelto", Color.FromArgb(200, 80, 70));
                EstadoPasajero("No se pudo abrir el video: " + MotivoFallo(ex), EstadoPasajeroMs);
                LogError("Video " + Path.GetFileName(path ?? ""), ex);   // el detalle, que antes solo se veia en la linea de estado
                Diag("video: NO ABRIO - " + ex.Message);
                // el video no arranco: pasar al siguiente sin trabar la cola (tras desenredar la pila de PlayNext)
                try { if (this.IsHandleCreated) PantallaRocola.Encola(this, delegate { PlayNext(); }); } catch { }   // EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)
            }
        }

        // EL REPRODUCTOR QUE NO SE PUDO CREAR (quinta tanda, 12-10-2026). Si el reproductor de Windows no esta (un XP
        // sin Windows Media Player, o registrado a medias), el control se quedaba puesto en la ventana sin nada dentro.
        // Cada reacomodo de la pantalla (devolver el credito, una moneda, el buscador) lo volvia a mostrar u ocultar, y
        // eso puede tronar fuera de cualquier 'try': con la prueba de mono, donde el reproductor no existe, la rocola
        // se cerraba al pedir un video. Si no da su reproductor, se quita y se suelta: el siguiente video lo vuelve a
        // intentar desde cero, y si tampoco, devuelve el credito igual.
        private void SueltaVideoRoto()
        {
            ReproductorVideo v = video;   // EN LINUX: el reproductor de libmpv (VideoHost en XP)
            if (v == null) return;
            bool sirve = false;
            try { sirve = v.Disponible; } catch { }   // EN LINUX: GetOcxObj() != null
            if (sirve) return;   // el reproductor esta: fallo ESE video, el control se queda para el siguiente
            video = null;
            try { v.FueraDePantalla(); } catch { }   // EN LINUX: (Controls.Remove) la capa del video sale de la pantalla
            try { v.Dispose(); } catch { }
            Diag("video: reproductor no disponible, se suelta el control");
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
                    if (video.Disponible)   // EN LINUX: GetOcxObj() != null
                    // EN LINUX: (XP) if (ocx != null)
                    {
                        video.Para();   // EN LINUX: controls.stop()
                        video.QuitaArchivo();   // EN LINUX: URL = ""
                        // EN LINUX: (XP) Set(ocx, "URL", "");
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
                if (video.Disponible)   // EN LINUX: GetOcxObj() != null
                // EN LINUX: (XP) if (ocx != null)
                {
                    st = (int)video.Estado;   // EN LINUX: playState (EstadoVideo lleva los mismos numeros: 1 detenido, 2 en pausa, 3 sonando, 8 acabo, 9 abriendo)
                    pos = video.Posicion;   // EN LINUX: controls.currentPosition
                    if (video.TieneArchivo) dur = video.Duracion;   // EN LINUX: currentMedia.duration (sin archivo no hay currentMedia: dur se queda en -1)
                    // EN LINUX: (XP) if (controls != null) { object cp = Get(controls, "currentPosition"); if (cp != null) pos = Convert.ToDouble(cp); }
                    // EN LINUX: (XP) object media = Get(ocx, "currentMedia");
                    // EN LINUX: (XP) if (media != null) { object d = Get(media, "duration"); if (d != null) dur = Convert.ToDouble(d); }
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
                try { if (video.Disponible) video.Toca(); } catch { }   // EN LINUX: controls.play()
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

        // EN LINUX: aqui XP tiene Get, Set y Call, la reflexion (InvokeMember) con que le habla al COM del reproductor de Windows Media
        // sin referencias COM ni 'dynamic'. El reproductor de libmpv (Nativo/ReproductorVideo.cs) se llama directo: no hacen falta.
        // EN LINUX: (XP) private static object Get(object o, string prop)
        // EN LINUX: (XP) {
            // EN LINUX: (XP) return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null);
        // EN LINUX: (XP) }
        // EN LINUX: (XP) private static void Set(object o, string prop, object val)
        // EN LINUX: (XP) {
            // EN LINUX: (XP) o.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, o, new object[] { val });
        // EN LINUX: (XP) }
        // EN LINUX: (XP) private static object Call(object o, string method, params object[] args)
        // EN LINUX: (XP) {
            // EN LINUX: (XP) return o.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, o, args);
        // EN LINUX: (XP) }

        private void ApplyGenreBackground(Track t, Album org)
        {
            Color acc = ColorDeLoQueSuena(t, org);   // excepcion por artista (BTS=morado) y luego genero
            bgAccentTarget = acc;
#if ROCOLA_GPU
            if (estadoColorDirecto) CambioElAcentoDelPanel(bgAccentTarget);
#endif
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
            ApuntaCortesDeSonido();   // si la que acaba o se salta tuvo cortes, una linea en rocola_error.txt (caja negra, SalidaSonido.cs)
            progressTimer.Stop();
            quedanMs = 0;   // la siguiente cancion no tiene prisa hasta que el reloj diga lo contrario (Form1.SinPausa.cs)
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
            curTap = null; curSbr = null; curBass = null;   // ya no hay taps actuales
            rmsLo = rmsMid = rmsHi = 0f; avgLo = avgMid = avgHi = 0f;
        }

        // La llama el hilo de audio con el bloque recien leido: lo parte en sub-trozos de ~50ms, calcula el
        // RMS de cada uno y lo encola con la MARCA DE TIEMPO en que ese trozo SONARA (ahora + colchon).
        // El temporizador de UI los consume cuando les llega su momento -> el pulso va CON lo que se oye.
        // Buffers reusables del hilo de audio (4 trozos x 3 bandas). ThreadStatic: sin asignar por bloque.
        [ThreadStatic] private static float[] tmpLo; [ThreadStatic] private static float[] tmpMid; [ThreadStatic] private static float[] tmpHi;
        [ThreadStatic] private static float[] tmpEsp;   // 4 trozos x 8 bandas
        [ThreadStatic] private static double[] tmpSum;  // sumas por banda: reusado (nada de asignar en el hilo de audio)
        private int limpiaPulso;   // muestras desde la ultima limpieza de restos diminutos (hilo de audio)
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
                    if (++limpiaPulso == Desnormales.Cada)   // en el silencio, los restos diminutos a cero (Desnormales.cs)
                    {
                        limpiaPulso = 0; lp1 = Desnormales.Limpia(lp1); lp2 = Desnormales.Limpia(lp2);
                        if (espectro) Desnormales.Limpia(sl);
                    }
                    if (!espectro) continue;             // aurora apagada (o sin GPU): la escalera ni se toca
                    fSum += v;                           // junta los canales del cuadro; la escalera corre UNA vez por cuadro
                    if (++fCnt < chEsp) continue;
                    float m = fSum * invCh; fSum = 0f; fCnt = 0;
                    // ESPECTRO: cada peldano de la escalera se queda con lo que el anterior dejo pasar,
                    // asi que la resta de dos peldanos consecutivos ES la banda que hay entre sus cortes.
                    float ant = 0f;
                    for (int k = 0, q = 0; k < 7; k++, q += 3)
                    {
                        float x = m, ck = sc[k], pv;
                        // Cada polo se lleva a una local: antes se leia su casilla del array DOS veces por
                        // vuelta (una para sumarle y otra para volver a leerla) y el coeficiente en cada
                        // vuelta. Las cuentas son las mismas y en el mismo orden: el sonido no cambia ni un bit.
                        // Los TRES polos (SpecPolos = 3; lo vigila tironescambio) van uno detras de otro, sin el bucle
                        // pequeno: es el trabajo que mas pesa del hilo de audio con la aurora encendida (20-10-2026).
                        pv = sl[q]; pv += (x - pv) * ck; sl[q] = pv; x = pv;
                        pv = sl[q + 1]; pv += (x - pv) * ck; sl[q + 1] = pv; x = pv;
                        pv = sl[q + 2]; pv += (x - pv) * ck; sl[q + 2] = pv; x = pv;
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
                    Rocola.Dibujo2D.Image nueva = LoadCover(nowPlaying.Disc);   // EN LINUX: la Image de la capa, escrita entera: la da la cache de portadas (Form1.Portadas.cs, que aun la publica como Image de la capa) y la recibe el mini-cubo (Form1.Vistas.cs, que la pasa a la tarjeta); aqui solo pasa
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
            return DseeEfectivo && nowPlaying != null && !nowPlaying.IsVideo && sp != null && sp.Mejorando;
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
            int bw = on ? TextoGdi.Mide(lblMejorada.Text, PuenteDibujo.Letra(lblMejorada.Font)).Width + 4 : 0;   // EN LINUX: TextRenderer.MeasureText(texto, letra): la medida de GDI (Nativo/TextoGdi.cs; sin caja ni banderas, Bottom, como WinForms); la letra de la etiqueta (WinForms, aun sin traducir), su Letra por el puente
            // LA PASTILLA DEL GENERO va en este mismo renglon, entre el artista y el letrero MEJORADA: es de
            // la cancion, igual que el artista, y en el renglon de arriba -el titulo- no cabria sin comerle
            // sitio a lo unico que de verdad hay que leer entero.
            int gw = (pastGenero != null) ? pastGenero.AnchoQuePide() : 0;
            int anchoArt = mejRowW - (on ? bw + 12 : 0) - (gw > 0 ? gw + 12 : 0);
            if (anchoArt < 60) anchoArt = 60;
            if (lblNow.Width != anchoArt) lblNow.Width = anchoArt;

            int tw = TextoGdi.Mide(lblNow.Text, PuenteDibujo.Letra(lblNow.Font)).Width;   // EN LINUX: TextRenderer.MeasureText(texto, letra): la medida de GDI (Nativo/TextoGdi.cs), con la Letra de la etiqueta por el puente
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
        private static void PartirNow(string texto, Rocola.Nativo.Letra f, int anchoArriba, int anchoAbajo, out string arriba, out string abajo)   // EN LINUX: la letra nativa (Nativo/Letras.cs; Form1 tiene un metodo Letra) en vez de la Font (nadie la llama, tampoco en XP)
        {
            arriba = ""; abajo = texto ?? "";
            if (abajo.Length == 0 || anchoAbajo < 20) return;
            if (TextoGdi.Mide(abajo, f).Width <= anchoAbajo) return;   // cabe de una: nada que partir   // EN LINUX: TextRenderer.MeasureText(texto, letra): la medida de GDI (Nativo/TextoGdi.cs)
            int tope = anchoArriba - 6;   // margen: la textura de la GPU dibuja con GDI+ y mide un pelo mas ancho
            int corte = -1;
            for (int i = 0; i < abajo.Length; i++)
            {
                if (abajo[i] != ' ') continue;
                if (TextoGdi.Mide(abajo.Substring(0, i), f).Width > tope) break;   // EN LINUX: TextRenderer.MeasureText (Nativo/TextoGdi.cs)
                corte = i;   // el ultimo espacio que todavia cabe arriba
            }
            if (corte > 0) { arriba = abajo.Substring(0, corte); abajo = abajo.Substring(corte + 1); return; }
            // ni un espacio util (un titulo de una sola palabra larguisima): se corta por letra
            int n = 0;
            while (n + 1 < abajo.Length && TextoGdi.Mide(abajo.Substring(0, n + 1), f).Width <= tope) n++;   // EN LINUX: TextRenderer.MeasureText (Nativo/TextoGdi.cs)
            if (n > 0 && n < abajo.Length) { arriba = abajo.Substring(0, n); abajo = abajo.Substring(n); }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            VigiaPantalla.Hace("el reloj de la cancion");
            if (reader == null) return;
            // LA SALIDA SE QUEDO MUDA (SalidaSonido.cs): como si hubiera avisado de un fallo del aparato.
            if (SalidaMuda()) { NAudio.Wave.IWavePlayer o = output; if (o != null) Output_PlaybackStopped(o, new NAudio.Wave.StoppedEventArgs(new SalidaMudaException())); return; }
            ApuntaLatencia();   // y si el perfil de latencia de la salida cambio de nivel, una linea en rocola_error.txt (SalidaSonido.cs)
            MuestreaEtapas(cadenaSonando);   // lo que hizo cada etapa, para la ventana y la bitacora (BitacoraBillar.cs)
            PreparaSiguienteSinPausa();   // a la que suena le quedan pocos segundos: la siguiente, lista para entrar sin pausa
            TimeSpan cur = reader.CurrentTime, tot = reader.TotalTime;
            ApuntaQuedan(cur, tot);   // si quedan 45 s o menos, la siguiente cancion pasa antes que las caratulas (Form1.SinPausa.cs)
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
        private bool uiOverlayVacio;   // la imagen de la UI no existe a proposito (atraccion con el escaparate): ver EnsureUiOverlay
        private Rocola.Dibujo2D.Bitmap uiOverlay; private int uiOverlayVer; private long uiOverlaySig = SigNada;   // textura de las barras (UI)   // EN LINUX: el Bitmap de la capa, escrito entero: lo sube D3D9Star.cs (GetUi y SubeUi de BuscadorLigeroGpu.cs) y lo copia Form1.Biblioteca.cs (winSnap), que aun lo piden como Bitmap de la capa; aqui se pinta en sus puntos (PuenteDibujo.Mapa)
        // La de la otra medida (navegar: una franja arriba; dentro de un disco o en el buscador: casi toda la pantalla). Al cambiar de
        // modo se toma esa en vez de reservar y soltar otra imagen de 2,8 MB cada vez (24-10-2026: el diagnostico del A4 ponia el 92 %
        // de los tirones de mas de 50 ms al entrar y salir de un disco, en la fase de la interfaz). Se repinta entera igual.
        private Rocola.Dibujo2D.Bitmap uiOverlayOtro;   // EN LINUX: el Bitmap de la capa, escrito entero (es la otra uiOverlay)

        // Ctrl+F12: alterna el render entre GPU (Direct3D 9, por defecto) y CPU, y GUARDA la preferencia.
        private void ToggleAlbumGpu() { ApplyAlbumGpu(!gpuEnabled, true, true); }

        // Aplica el modo de render. 'on' = destino (GPU/CPU); 'announceFail' = avisar si el dispositivo no inicia;
        // 'persist' = guardar la preferencia (solo cuando lo pide el usuario con Ctrl+F12, no en el arranque). El
        // panel GPU (Direct3D 9) cubre toda la ventana y compone: fondo estrellado + cubos + barras + mini-cubo.
        private void ApplyAlbumGpu(bool on, bool announceFail, bool persist)
        {
            VigiaPantalla.Hace("preparar la tarjeta grafica");
            bool gpuAntes = gpuEnabled;
            if (lblGpu == null)
            {
                lblGpu = new Label();
                lblGpu.AutoSize = false; lblGpu.Size = new Size(380, 26);
                lblGpu.Location = new Point(ClientSize.Width - 390, 46);
                lblGpu.BackColor = Color.FromArgb(200, 0, 0, 0); lblGpu.ForeColor = Color.Yellow;
                lblGpu.Font = PuenteDibujo.Font(Rocola.Nativo.Letra.EnPuntos(Letras.AnchoFijo, 10f, true));   // EN LINUX: new Font(FontFamily.GenericMonospace, 10f, FontStyle.Bold): la de ancho fijo de XP (Courier New) en puntos y negrita; la etiqueta es de WinForms (aun sin traducir) y la recibe como Font por el puente
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
                    // TOCAR LA PANTALLA DESPIERTA LA ATRACCION, como cualquier boton (novena tanda, 12-10-2026). Antes el toque
                    // solo reiniciaba la cuenta del reposo y la atraccion seguia: en una pantalla tactil no habia forma de
                    // salir sin botones. Ese toque solo despierta (como la tecla que despierta): no abre nada debajo.
                    toqueDespierta = attractOn;
                    if (attractOn) ExitAttract();
                    if (asistenteVisible) asistenteActividadTick = Environment.TickCount;   // tocar la pantalla tambien es usarla: la guia no se cierra por inactividad
                };
                gpuPanel.MouseWheel += delegate { lastInput = DateTime.Now; gpuPanel.IdleExpandTarget = false; gpuPanel.TouchUi(); };
                gpuPanel.MouseClick += delegate(object sender, MouseEventArgs e)
                {
                    if (toqueDespierta) { toqueDespierta = false; return; }   // el toque que desperto la atraccion no hace nada mas
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
                gpuPanel.ReportError = delegate(string que, Exception ex) { LogError(que, ex); };
                gpuPanel.ReportPeores = delegate(string hoja) { Diag(hoja); };   // el libro de los peores cuadros (MedidaFluidezGpu.cs): solo con diagnostico=1
                gpuPanel.LibroActivo = diagOn;
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
                gpuPanel.GetEq = BuildEqState;           // las barritas de la que suena
                gpuPanel.GetBoleto = BuildBoletoState;   // el boleto que vuela de la fila a la cola
                gpuPanel.GetAviso = BuildAvisoState;     // los avisos que se entienden (SuenaVivoGpu.cs)
                gpuPanel.GetInvita = BuildInvitaState;   // sin creditos: la tarjeta que invita
                gpuPanel.GetMoneda = BuildMonedaState;   // la moneda que cae en la capsula de creditos
                gpuPanel.GetReposo = BuildReposoState;   // lo que suena, en grande: el titulo y el genero del panel de reposo
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
                gpuPanel.GetUiVacia = delegate { return uiOverlayVacio; };
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
                    if (!searchOpen || searchOverlay == null) return 0;
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
            // LA FILA DE ARRIBA DEPENDE DE LA TARJETA: la casilla de AYUDA solo existe con ella, la fila se reparte de
            // otra manera y la barra de letras deja sus carriles para las flechas. El arranque acomoda la pantalla
            // ANTES de encender la tarjeta, y nada volvia a acomodarla hasta el primer cambio de cancion o de
            // creditos: la casilla de AYUDA tardaba en salir (aviso del 11-10-2026). Se acomoda aqui mismo.
            if (gpuEnabled != gpuAntes) LayoutControls();
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
                Rocola.Dibujo2D.Image ca, cb; float f;   // EN LINUX: las Image de la capa, escritas enteras: las portadas del cubo van de la atraccion (Form1.Vistas.cs) a la tarjeta (D3D9Star.cs, que aun las sube como Bitmap de la capa); aqui solo pasan
                ParCuboAtraccion(out ca, out cb, out f);   // puede dar null (portadas aun cargando): el cubo muestra relleno azulado
                RectangleF cubo = CuboAtraccionAhora();   // el de siempre, o el del escaparate (AtraccionGpu.cs): lo mismo que la animacion de entrada/salida
                st.Sel.Has = true;
                st.Sel.Key = attractView;
                st.Sel.X = cubo.X; st.Sel.Y = cubo.Y; st.Sel.W = cubo.Width; st.Sel.H = cubo.Height;
                st.Sel.Angle = AnguloAtraccion(); st.Sel.CoverA = ca as Rocola.Dibujo2D.Bitmap; st.Sel.CoverB = cb as Rocola.Dibujo2D.Bitmap; st.Sel.Fade = f;   // EN LINUX: el Bitmap de la capa, escrito entero (GpuCubeInfo de D3D9Star.cs)
                return st;   // en atraccion no hay mini-cubo (no suena nada)
            }
            if (searchOpen && searchOverlay != null)
            {
                st = new GpuCubeState();
                Rectangle area; Rocola.Dibujo2D.Image ca, cb; float f;   // EN LINUX: las Image de la capa, escritas enteras (del buscador, Form1.Buscador.cs, a la tarjeta); aqui solo pasan
                bool cubOk = searchOverlay.GetPreviewCube(out area, out ca, out cb, out f);
#if ROCOLA_GPU
                if (winMode == 1) cubOk = false;   // abriendo: la ventana se anima entera, un cubo suelto encima la delataria
#endif
                if (cubOk)
                {
                    area.Offset(searchOverlay.Left, searchOverlay.Top);   // coords del buscador -> pantalla
                    st.Sel.Has = true; st.Sel.X = area.X; st.Sel.Y = area.Y; st.Sel.W = area.Width; st.Sel.H = area.Height;
                    st.Sel.Key = searchOverlay;
                    st.Sel.Angle = searchOverlay.PreviewAngle; st.Sel.CoverA = ca as Rocola.Dibujo2D.Bitmap; st.Sel.CoverB = cb as Rocola.Dibujo2D.Bitmap; st.Sel.Fade = f;   // EN LINUX: el Bitmap de la capa, escrito entero
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
            info.CoverA = cb.Cover as Rocola.Dibujo2D.Bitmap;   // EN LINUX: el Bitmap de la capa, escrito entero (la portada del CubeBox, Form1.Vistas.cs, a la tarjeta)
            info.Key = cb;
            info.Tilt = (float)(10.0 * Math.Sin(cb.Phase * 0.9) * Math.PI / 180.0);   // mismo bamboleo que CubeBox.OnPaint
        }

        // Muestra/oculta el panel GPU (a pantalla completa) segun el modo: en NAVEGACION y en el BUSCADOR
        // (ambos se componen por GPU). En disco/video/atraccion/config se oculta y deja ver los controles CPU.
        // El cuadro del video (WMP) es una ventana HIJA hermana del panel GPU: si el panel acaba de
        // mostrarse, su BringToFront lo dejo por delante y taparia el video. Esto lo regresa arriba.
        private void KeepVideoOnTop() { try { if (!AsistenteVisible && videoOn && video != null && video.Visible) video.AlFrente(); } catch { } }   // EN LINUX: BringToFront: la capa del video

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
            // LA ATRACCION CON EL ESCAPARATE NO TIENE INTERFAZ en esta imagen (sus textos son del escaparate, AtraccionGpu.cs): antes se
            // reservaba, se borraba y se subia a la tarjeta una imagen transparente de pantalla completa menos la barra de abajo.
            // Ahora se avisa con la version y con uiOverlayVacio, y la tarjeta no sube, no analiza ni dibuja nada (20-10-2026).
            if (attractOn && EscaparateActivo() && !(searchOpen && searchOverlay != null) && !(panelSongs != null && panelSongs.Visible))
            {
                if (uiOverlayVacio && sig == uiOverlaySig) return uiOverlayVer;
                if (uiOverlay != null) { if (uiOverlayOtro != null) uiOverlayOtro.Dispose(); uiOverlayOtro = uiOverlay; uiOverlay = null; }
                uiOverlayVacio = true; uiOverlaySig = sig; uiOverlayVer++;
                uiOverlayZona = Rectangle.Empty; uiOverlayZonaVer = uiOverlayVer;
                uiBuscaFija = uiBuscaRes = uiBuscaAviso = SigNada;
                return uiOverlayVer;
            }
            uiOverlayVacio = false;
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
                if (uiOverlayOtro != null && uiOverlayOtro.Width == w && uiOverlayOtro.Height == mainH)
                { Rocola.Dibujo2D.Bitmap t = uiOverlayOtro; uiOverlayOtro = uiOverlay; uiOverlay = t; }   // la de este modo, guardada: se repinta entera abajo   // EN LINUX: el Bitmap de la capa, escrito entero
                else
                {
                    if (uiOverlayOtro != null) uiOverlayOtro.Dispose();
                    uiOverlayOtro = uiOverlay;
                    uiOverlay = PuenteDibujo.Entrega(Mapas.Nuevo(w, mainH), Rocola.Dibujo2D.PixelFormat.Format32bppPArgb);   // EN LINUX: new Bitmap(w, mainH, Format32bppPArgb): los puntos nativos (Mapas.Nuevo: Bgra8888 premultiplicado, transparentes) en un Bitmap de la capa DUENO de ellos y con ese formato, porque lo suben (D3D9Star.cs) y lo copian (Form1.Biblioteca.cs) como Bitmap de la capa (como las piezas de AcabadoGpu.cs)
                }
            }
            using (SKCanvas g = new SKCanvas(PuenteDibujo.Mapa(uiOverlay)))   // EN LINUX: Graphics.FromImage(uiOverlay): un lienzo de Skia sobre sus puntos (los mismos, sin copia)
            {
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear: lo mismo, el color de .NET en el de Skia
                bool suave = true, copia = false, medio = false; Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable; la copia, el medio pixel y el filtro, los de fabrica de un Graphics (SourceOver, Default, Bilinear): esta imagen no los cambia
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // gris (ClearType deja flecos sobre transparente), ajustado a los pixeles: la letra chica sale mas nitida en pantallas bajas (T1, 12-10-2026; tambien la franja, las piezas y las cintas)   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                if (searchOpen && searchOverlay != null)
                {
                    int gs = g.Save();   // BUSCADOR (su UI sin starfield ni cubo)   // EN LINUX: GraphicsState gs = g.Save(): la matriz y el recorte los guarda el lienzo (el estado del dibujo va en variables y aqui no cambia)
                    g.Translate(searchOverlay.Left, searchOverlay.Top);   // EN LINUX: g.TranslateTransform
                    { Filtro filtroGuardado = filtro; searchOverlay.RenderContent(g, ajuste, copia, ref filtroGuardado, medio, SKMatrix.Identity, new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height), true); }   // EN LINUX: searchOverlay.RenderContent(g, caja, true): la traducida (Form1.Buscador.cs), directo, con el lienzo (ya trasladado), su estado y la matriz del HDC de un mapa de bits (la identidad); el filtro que le deje (el del cubo) va a una copia, porque en XP se iba con el Restore de abajo (el suavizado lo pone ella y tampoco vuelve)
                    g.RestoreToCount(gs);   // EN LINUX: g.Restore(gs)
                }
                else if (panelSongs != null && panelSongs.Visible)
                {
                    DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, helpBar);   // instrucciones ARRIBA tambien dentro del disco (guian al usuario, como en software)   // EN LINUX: con el lienzo y su estado
                    int gs = g.Save();   // INTERIOR DE DISCO: etiqueta + lista + barra de paginas (el cubo grande lo pone la GPU)   // EN LINUX: GraphicsState gs = g.Save(): la cuenta del lienzo
                    g.Translate(panelSongs.Left, panelSongs.Top);   // los controles del disco son hijos de panelSongs   // EN LINUX: g.TranslateTransform
                    DrawLabelOverlay(g, ajuste, copia, medio, lblAlbum);   // EN LINUX: con el lienzo y lo que pesa del estado en el texto
                    if (FastSongGlass) { /* vidrio, pagina y fila: texturas independientes */ }
                    else if (cancionesTransparentes && songList != null)
                    {
                        int listState = g.Save(); g.Translate(songList.Left, songList.Top);   // EN LINUX: GraphicsState listState = g.Save() (la cuenta del lienzo) y g.TranslateTransform
                        g.Save(); g.ClipRect(new SKRect(0, 0, songList.Width, songList.Height), SKClipOperation.Intersect, false);   // EN LINUX: g.SetClip(rect): no hay otro recorte en esta imagen (reemplazarlo es recortar), en un Save del lienzo como lo pone la capa (se va con el Restore de listState); de pixeles enteros, sin suavizar, como GDI+
                        songList.RenderTransparentGpuTint(g, suave, copia, filtro, medio, cancionesCristal, glassCapas, Lighten(bgAccent, .55)); g.RestoreToCount(listState);   // EN LINUX: la traducida (AcabadoGpu.cs) con el lienzo y su estado; g.Restore(listState)
                    }
                    else
                    {
                        int listState = g.Save();   // EN LINUX: GraphicsState listState = g.Save(): la cuenta del lienzo
                        g.Translate(songList.Left, songList.Top);   // EN LINUX: g.TranslateTransform
                        g.Save(); g.ClipRect(new SKRect(0, 0, songList.Width, songList.Height), SKClipOperation.Intersect, false);   // EN LINUX: g.SetClip(rect): no hay otro recorte en esta imagen (reemplazarlo es recortar), en un Save del lienzo que se va con el Restore de listState; de pixeles enteros, sin suavizar
                        songList.RenderOpaqueGpu(g, copia, filtro, medio); g.RestoreToCount(listState);   // EN LINUX: la traducida (AcabadoGpu.cs) con el lienzo y lo que pesa del estado; g.Restore(listState)
                    }
                    if (songPager != null && songPager.Visible) DrawSongPagerGpu(g, suave, ajuste, copia, medio);   // EN LINUX: la traducida (AcabadoGpu.cs) con el lienzo y su estado
                    g.RestoreToCount(gs);   // EN LINUX: g.Restore(gs)
                }
                else if (attractOn && EscaparateActivo()) { }   // con el escaparate, los textos los pone el (AtraccionGpu.cs)
                else if (attractOn)   // MODO ATRACCION: solo los dos textos (el cubo gigante y las estrellas los pone la GPU)
                {
                    RectangleF cuboA = AttractCubeRect();   // mismo rectangulo que el cubo y que la animacion
                    int cubeBottom = (int)(cuboA.Y + cuboA.Height);
                    string line1 = (attractView != null && attractView.SongCount > 0) ? (attractView.SongCount.ToString("#,0") + " canciones") : "Tu rocola";
                    using (Rocola.Nativo.Letra fBig = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 24f, true))   // EN LINUX: new Font(FontFamily.GenericSansSerif, 24f, Bold): la letra nativa (Nativo/Letras.cs), la generica sin remates de XP en puntos y negrita
                    using (Rocola.Nativo.Letra fSmall = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, 15f, true))   // EN LINUX: new Font(FontFamily.GenericSansSerif, 15f, Bold)
                    // EN LINUX: using (StringFormat sfc = new StringFormat()): es el FormatoTexto sfc de la primera linea del bloque
                    using (SKPaint tb = new SKPaint { Color = Color.White.Sk() })   // EN LINUX: using (SolidBrush tb = new SolidBrush(Color.White)): el pincel en un SKPaint con su color (el using del formato, abajo, ya no hace falta: un FormatoTexto no se suelta)
                    {
                        FormatoTexto sfc = new FormatoTexto(); sfc.Horizontal = Alinea.Centro; sfc.Vertical = Alinea.Centro;   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs) con Alignment y LineAlignment en Center
                        TextoGdiPlus.Escribe(g, line1, fBig, ajuste, tb, new RectangleF(0, cubeBottom + 18, w, 36), sfc, copia, medio);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs) con el modo de letra, la copia y el medio pixel
                        tb.Color = Color.FromArgb(235, 255, 220, 120).Sk();   // FIJO: el "respiro" lo pone un halo por GPU (FillUiGlows) -> esta textura no se rehace nunca en atraccion   // EN LINUX: el color del pincel, en el de Skia
                        TextoGdiPlus.Escribe(g, "toca un boton para elegir", fSmall, ajuste, tb, new RectangleF(0, cubeBottom + 58, w, 28), sfc, copia, medio);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                    }
                }
                else
                {
                    DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, helpBar);   // EN LINUX: con el lienzo y su estado
                    if (buscaView != null && buscaView.Visible) DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, buscaView);   // escondida, no se pinta (con video, antes asomaba una tira)   // EN LINUX: con el lienzo y su estado
                    if (ayudaView != null && ayudaView.Visible) DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, ayudaView);   // AYUDA (su interrogacion la anima la tarjeta)   // EN LINUX: con el lienzo y su estado
                    DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, letterBar);   // EN LINUX: con el lienzo y su estado
                    DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, headerView);   // EN LINUX: con el lienzo y su estado
                    DrawLabelOverlay(g, ajuste, copia, medio, lblStatus);   // EN LINUX: con el lienzo y lo que pesa del estado en el texto
                    DrawGuiaRojo(g, suave, copia, medio);   // EN LINUX: la traducida (GuiaRojoGpu.cs) con el lienzo y su estado
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
        private Rocola.Dibujo2D.Bitmap uiBotOverlay; private int uiBotVer; private long uiBotLastSig = SigNada;   // EN LINUX: el Bitmap de la capa, escrito entero: lo suben GetUiBot y SubeUiBot (D3D9Star.cs, FranjaLigeraGpu.cs), aun con el Bitmap; aqui se pinta en sus puntos (PuenteDibujo.Mapa)
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
                uiBotOverlay = PuenteDibujo.Entrega(Mapas.Nuevo(w, bh), Rocola.Dibujo2D.PixelFormat.Format32bppPArgb);   // EN LINUX: new Bitmap(w, bh, Format32bppPArgb): los puntos nativos en un Bitmap de la capa dueno de ellos y con ese formato (GetUiBot y SubeUiBot aun lo suben como Bitmap de la capa)
            }
            using (zona.IsEmpty ? CapturaTexto(0) : CapturaRecuadro())   // (por recuadro, las cintas de verdad no se tocan)
            using (SKCanvas g = new SKCanvas(PuenteDibujo.Mapa(uiBotOverlay)))   // EN LINUX: Graphics.FromImage(uiBotOverlay): un lienzo de Skia sobre sus puntos (los mismos)
            {
                if (zona.IsEmpty) g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                else EmpiezaRecuadro(g, false, false, zona);   // solo el recuadro: lo de fuera ya esta como tiene que estar   // EN LINUX: la traducida (FranjaLigeraGpu.cs) con el estado de un Graphics recien hecho (sin suavizar, medio pixel de fabrica); deja el recorte del recuadro en el lienzo, en un Save, como el SetClip de XP (nada de lo de abajo lo reemplaza)
                if (!attractOn)   // en atraccion la pantalla es limpia: la franja queda transparente
                {
                    bool suave = true, copia = false, medio = false; Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable; la copia, el medio pixel y el filtro, los de fabrica de un Graphics (la franja no los cambia)
                    AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // gris: ClearType deja flecos sobre transparente   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                    g.Translate(0, -top);   // las etiquetas estan en coords del form; la tira empieza en 'top'   // EN LINUX: g.TranslateTransform
                    if (!FranjaLejos(g, suave, ajuste, copia, medio))   // la letra para leer de lejos (EstiloGpu.cs): el titulo grande y lo que sigue a la derecha   // EN LINUX: la traducida con el lienzo y su estado
                    {
                        PintaTituloCompacto(g, suave, ajuste, copia, medio, false);   // EN LINUX: la traducida (EstiloGpu.cs) con el lienzo y su estado
                        PintaArtistaCompacto(g, ajuste, copia, medio);   // EN LINUX: la traducida (TextoMovilGpu.cs) con el lienzo y lo que pesa del estado en el texto
                        if (lblMejorada != null && lblMejorada.Visible) DrawLabelOverlay(g, ajuste, copia, medio, lblMejorada);   // letrero MEJORADA (su brillo es un halo por quad, no rehace esta textura)   // EN LINUX: con el lienzo y su estado
                        if (pastGenero != null && pastGenero.Visible) DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, pastGenero);   // EN LINUX: con el lienzo y su estado
                        PintaSiguienteCompacto(g, suave, ajuste, copia, medio);   // EN LINUX: la traducida (EstiloGpu.cs) con el lienzo y su estado
                    }
                    DrawLabelOverlay(g, ajuste, copia, medio, lblTime);   // EN LINUX: con el lienzo y su estado
                    DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, pastCred);   // EN LINUX: con el lienzo y su estado
                    DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, pastCola);   // EN LINUX: con el lienzo y su estado
                    if (!progresoFluido && !(neonIntenso && progresoNeon)) DrawCtrlOverlay(g, suave, ajuste, copia, filtro, medio, progress);   // EN LINUX: con el lienzo y su estado
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
        private readonly Rocola.Dibujo2D.Bitmap[] uiPieza = new Rocola.Dibujo2D.Bitmap[NumPiezas];   // EN LINUX: los Bitmap de la capa, escritos enteros: los suben GetPieza y SubePieza (D3D9Star.cs, BuscadorPortadasGpu.cs) y los pone tambien EnsureSongPiece (AcabadoGpu.cs, con PuenteDibujo.Entrega); aqui se pinta en sus puntos
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
                uiPieza[cual] = PuenteDibujo.Entrega(Mapas.Nuevo(donde.Width, donde.Height), Rocola.Dibujo2D.PixelFormat.Format32bppPArgb);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): los puntos nativos en un Bitmap de la capa dueno de ellos y con ese formato (como EnsureSongPiece)
            }
            using (cual == PiezaVistaPreviaBuscador() ? CapturaTexto(4) : new CapturaCintas(null))   // en el buscador con portadas, la tarjeta elegida (BuscadorPortadasGpu.cs)
            using (SKCanvas g = new SKCanvas(PuenteDibujo.Mapa(uiPieza[cual])))   // EN LINUX: Graphics.FromImage(pieza): un lienzo de Skia sobre sus puntos (los mismos)
            {
                bool suave = true, copia = false, medio = false; Filtro filtro = Filtro.Bilineal;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable; la copia, el medio pixel y el filtro, los de fabrica de un Graphics
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // igual que la imagen grande   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                // SE LE PASA EL DESPLAZAMIENTO; NO SE MUEVE EL LIENZO. La primera version movia el lienzo
                // (TranslateTransform) y en Mono se veia perfecto... pero en Windows el texto de la tecla lo
                // dibuja el SISTEMA (TextRenderer), y ese trabaja en pixeles de verdad: no hace caso a las
                // trasformaciones. La letra se iba fuera de la imagen chica y en el bar la tecla elegida
                // salia como un rectangulo rosa VACIO. Con el desplazamiento por delante cae donde debe en
                // los dos sitios.
                if (searchOverlay.Nuevo && cual == SearchOverlay.PiezaCartas)
                    uiCartasCambios = searchOverlay.PintaCartasConCambios(g, suave, ajuste, copia, filtro, medio, -donde.X, -donde.Y, uiPiezaSig[cual] == SigNada);   // EN LINUX: la traducida (BuscadorPortadasGpu.cs) con el lienzo y su estado
                else
                {
                    g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                    searchOverlay.DibujaPiezaViva(g, ref suave, ajuste, copia, filtro, medio, SKMatrix.Identity, cual, -donde.X, -donde.Y);   // EN LINUX: searchOverlay.DibujaPiezaViva(g, cual, dx, dy): la traducida (Form1.Buscador.cs), directo, con el lienzo y su estado y la matriz del HDC de un mapa de bits (la identidad: su texto de GDI no ve traslaciones, como en XP); el suavizado que le deja vuelve a la variable, como al Graphics de XP, que se tira aqui
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
                if (buscaView != null && buscaView.Visible && FilaBuscaAyudaVisible() && n < buf.Length)   // dentro de un disco la casilla no se pinta: su latido tampoco
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
                if (buscaView != null) { sb = Mix(sb, buscaView.Visible); sb = Mix(sb, buscaView.Abierto); sb = Mix(sb, buscaView.Left); sb = Mix(sb, buscaView.Width); }
                if (helpBar != null) sb = Mix(sb, helpBar.Justa);
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
        private static void DrawCtrlOverlay(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, Control c)   // EN LINUX: el lienzo de Skia y el estado del Graphics del que llama (suavizado, modo de letra, copia, filtro y medio pixel), como parametros; la matriz del HDC es la de un mapa de bits (la identidad): los que llaman pintan en uiOverlay, uiBotOverlay o la franja de lejos
        {
            if (c == null || c.Width < 1 || c.Height < 1) return;
            int st = g.Save();   // EN LINUX: GraphicsState st = g.Save(): la matriz y el recorte los guarda el lienzo; el estado del dibujo va en variables y lo llamado no las cambia (al volver, el Restore de XP lo dejaba como estaba)
            g.Translate(c.Left, c.Top);   // EN LINUX: g.TranslateTransform
            HelpBar hb = c as HelpBar;
            if (hb != null) hb.RenderContent(g, ajuste, copia, medio, true);   // forGpu: sus pulsos van como halos por quad, no en la textura   // EN LINUX: la traducida (Form1.cs) con el lienzo y su estado (el suavizado lo pone ella)
            else { HeaderView hv = c as HeaderView; if (hv != null) hv.RenderContent(g, ajuste, copia, medio);   // EN LINUX: las vistas ya traducidas (Form1.Vistas.cs, DiscoVivoGpu.cs, Form1.Buscador.cs), directo, con este lienzo (ya trasladado) y lo que pesa de su estado: el encabezado, el modo de letra, la copia y el medio pixel (el suavizado lo pone ella); lo que XP le dejaba cambiado al Graphics se iba con el Restore de abajo
            else { LetterBar lb = c as LetterBar; if (lb != null) lb.RenderContent(g, ajuste, copia, filtro, medio, true);   // EN LINUX: la barra de letras (Form1.Vistas.cs), directo: el modo de letra, la copia, el filtro y el medio pixel
            else { PastillaView pa = c as PastillaView; if (pa != null) pa.RenderContentGpu(g, ajuste, copia, filtro, medio);   // con la cancion que llega a la cola (DiscoVivoGpu.cs)   // EN LINUX: la capsula (DiscoVivoGpu.cs y Form1.Vistas.cs), directo: el modo de letra, la copia, el filtro y el medio pixel
            else { ProgressView pv = c as ProgressView; if (pv != null) pv.RenderContent(g, copia, medio);   // EN LINUX: la barra de avance (Form1.Vistas.cs), directo: la copia y el medio pixel
            else { GeneroView gv = c as GeneroView; if (gv != null) gv.RenderContent(g, ajuste, copia, medio);   // EN LINUX: la pastilla del genero (Form1.Vistas.cs), directo: el modo de letra, la copia y el medio pixel
            else { SongListView sl = c as SongListView; if (sl != null) { sl.pintandoGpu = true; sl.RenderContent(g, copia, filtro, medio); }   // EN LINUX: la lista de canciones (Form1.Vistas.cs), directo: la copia, el filtro y el medio pixel
            else { SongPagerBar sp = c as SongPagerBar; if (sp != null) sp.RenderContent(g, suave, copia, medio);   // EN LINUX: la barra de paginas (Form1.Vistas.cs), directo: el suavizado (el fondo se pinta con el que trae), la copia y el medio pixel
            else { BuscaView bv = c as BuscaView; if (bv != null) bv.RenderContent(g, ajuste, copia, medio, true);   // EN LINUX: la casilla de BUSCAR (Form1.Buscador.cs), directo: el modo de letra, la copia y el medio pixel
            else { AyudaView av = c as AyudaView; if (av != null) av.RenderContent(g, copia, medio, true); } } } } } } } } }   // EN LINUX: la traducida (AyudaCasillaGpu.cs) con el lienzo y lo que pesa del estado (el suavizado y el modo de letra los pone ella; lo que deja puesto se iba con el Restore)
            g.RestoreToCount(st);   // EN LINUX: g.Restore(st)
        }

        // Dibuja el texto de una etiqueta en la textura de UI, respetando su fuente, color, alineacion y caja.
        // GDI reusables (StringFormat + pincel al que se muta el color): esto se llama ~6-7 veces por cada
        // reconstruccion de la textura de UI (que ocurre ~13fps en navegacion por el brillo de la letra).
        [ThreadStatic] private static FormatoTexto lblSf;   // EN LINUX: el StringFormat, el formato nativo (Nativo/TextoGdiPlus.cs)
        [ThreadStatic] private static SKPaint lblBr;   // EN LINUX: el SolidBrush, un SKPaint al que se le cambia el color (TextoGdiPlus.Escribe pinta con una copia: no le cambia nada)
        private static void DrawLabelOverlay(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, Label l) { DrawLabelOverlay(g, ajuste, copia, medio, l, (l != null) ? l.ForeColor : Color.White); }   // EN LINUX: el lienzo de Skia y lo que pesa del estado del Graphics en el texto (modo de letra, copia y medio pixel), como parametros
        private static void DrawLabelOverlay(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, Label l, Color color)   // EN LINUX: el lienzo y el estado
        { if (l != null) DrawLabelOverlay(g, ajuste, copia, medio, l, color, (RectangleF)l.Bounds); }   // EN LINUX: con el lienzo y el estado
        // En una caja propia (mas angosta que la etiqueta): los puntos suspensivos van donde de verdad se acaba el sitio.
        // Recortar la etiqueta entera con un SetClip dejaba la ultima palabra cortada a la mitad (barrido del 12-10-2026).
        private static void DrawLabelOverlay(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, Label l, Color color, RectangleF caja)   // EN LINUX: el lienzo y el estado (lo llaman tambien PintaArtistaCompacto de TextoMovilGpu.cs y FranjaLejos de EstiloGpu.cs, ya traducidas)
        {
            if (l == null || string.IsNullOrEmpty(l.Text) || !(caja.Width > 0f)) return;
            FormatoTexto sf = lblSf ?? (lblSf = new FormatoTexto());   // EN LINUX: new StringFormat() es new FormatoTexto()
            SKPaint b = lblBr ?? (lblBr = new SKPaint { Color = Color.White.Sk() }); b.Color = color.Sk();   // EN LINUX: el SolidBrush en un SKPaint con su color
            sf.Banderas = BanderasTexto.SinPartir; sf.Corte = CorteTexto.PuntosPorLetra;   // EN LINUX: FormatFlags = NoWrap (reemplaza todas las banderas) y Trimming = EllipsisCharacter
            ContentAlignment a = l.TextAlign;
            if ((a & (ContentAlignment.TopLeft | ContentAlignment.MiddleLeft | ContentAlignment.BottomLeft)) != 0) sf.Horizontal = Alinea.Cerca;   // EN LINUX: sf.Alignment = StringAlignment.Near
            else if ((a & (ContentAlignment.TopRight | ContentAlignment.MiddleRight | ContentAlignment.BottomRight)) != 0) sf.Horizontal = Alinea.Lejos;   // EN LINUX: sf.Alignment = StringAlignment.Far
            else sf.Horizontal = Alinea.Centro;   // EN LINUX: sf.Alignment = StringAlignment.Center
            if ((a & (ContentAlignment.TopLeft | ContentAlignment.TopCenter | ContentAlignment.TopRight)) != 0) sf.Vertical = Alinea.Cerca;   // EN LINUX: sf.LineAlignment = StringAlignment.Near
            else if ((a & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0) sf.Vertical = Alinea.Lejos;   // EN LINUX: sf.LineAlignment = StringAlignment.Far
            else sf.Vertical = Alinea.Centro;   // EN LINUX: sf.LineAlignment = StringAlignment.Center
            TextoGdiPlus.Escribe(g, l.Text, PuenteDibujo.Letra(l.Font), ajuste, b, caja, sf, copia, medio);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs) con el modo de letra, la copia y el medio pixel; la letra de la etiqueta (WinForms, aun sin traducir), su Letra por el puente
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
        private bool EnSeco(Control c, Action<SKCanvas, bool, AjusteLetra> dibuja)   // EN LINUX: dibuja recibe el lienzo de Skia y el estado que aqui se pone (suavizado y modo de letra; la copia, el medio pixel y el filtro, los de fabrica); el buscador y la lista de los precalentados (PrecargaPaginaGpu.cs, Form1.Portadas.cs) la llaman directo con ese lienzo y ese estado; solo la atraccion (Form1.Portadas.cs), que aun pinta con el Graphics de su OnPaint, llega por la firma de XP del puente (Plataforma/Puente.video-form1.cs)
        {
            if (c == null || c.Width < 8 || c.Height < 8) return false;
            using (SKBitmap b = Mapas.Nuevo(c.Width, c.Height))   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): los puntos nativos (Bgra8888 premultiplicado, transparentes)
            using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage(b)
            {
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable
                AjusteLetra ajuste = AjusteLetra.Suave;   // EN LINUX: g.TextRenderingHint = AntiAlias, en una variable
                dibuja(g, suave, ajuste);   // EN LINUX: con el lienzo y su estado
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
#if ROCOLA_GPU
            if (gpuPanel != null) gpuPanel.VuelcaLibro();   // lo que quedaba del minuto en el libro de los peores cuadros (solo con diagnostico=1)
#endif
            Bitacora.Vacia(3000);   // y que la bitacora no se quede con apuntes en la cola
            base.OnFormClosing(e);
        }

        // ==========  VIDEO: host del control ActiveX de Windows Media Player  ==========
        // EN LINUX: aqui XP tiene VideoHost, el AxHost del control ActiveX de Windows Media Player (CLSID del OCX de wmp.dll). En Linux no hay
        // ActiveX: el video es Nativo/ReproductorVideo.cs (libmpv), con su capa en la pantalla.
        // EN LINUX: (XP) {
            // EN LINUX: (XP) public VideoHost() : base("6bf52a52-394a-11d3-b153-00c04f79faa6") { }
            // EN LINUX: (XP) public object GetOcxObj() { return this.GetOcx(); }
        // EN LINUX: (XP) }

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

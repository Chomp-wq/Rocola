// EL REPRODUCTOR DE VIDEO DE LA ROCOLA, NATIVO DE LINUX (port nativo de XP, 8-11-2026: «que tradujeras todo el codigo para que trabajara
// nativamente en Linux [...] ningun layer de traduccion»).
//
// En XP los videos los toca el control ActiveX del reproductor de Windows Media (Form1.Video.cs: VideoHost, un AxHost, y la reflexion
// sobre su COM: Get, Set y Call con InvokeMember). Aqui los toca libmpv (Mpv.cs) y el codigo de XP llama DIRECTO a esta clase, con solo lo
// que XP usa: abrir un archivo y tocarlo (lo que hacia URL con autoStart), tocar, parar, soltar el archivo, la posicion, la duracion, el
// estado que XP mira (EstadoVideo, con los numeros que XP compara: 1, 2, 3, 8 y 9) y el volumen (0 a 100: amplitud lineal, que es lo que
// supone la cuenta de XP en VideoVolumeFor; mpv tiene el volumen cubico, asi que se le pone 100 * raiz cubica (v / 100)). Y su CAPA en la
// pantalla: lo que el control del WMP hacia como ventana (el sitio, si se ve y el orden: AlFrente, lo que era BringToFront). En XP Windows
// ponia la ventana del WMP encima de la rocola; aqui libmpv pinta cada cuadro en una textura de OpenGL (su API de render, en el mismo
// contexto que la tarjeta) y el compositor de la pantalla la pone en su sitio y en su orden. El sonido lo saca mpv por ALSA (la salida de
// la rocola, ROCOLA_ALSA), fuera de la cadena, como el WMP lo sacaba por Windows. (La duracion de un video sin tocarlo, lo que en XP media
// un reproductor de Windows suelto, es MedidorVideo.cs.)
//
// Lo que dejan los arreglos del 7-11-2026 (de Plataforma/VideoMpv.cs, que esto sustituye): antes de cada llamada a mpv, OpenGL en su estado
// de fabrica (EstadoDeFabrica: con la mezcla de la tarjeta puesta el video quedaba magenta), sin girar (FLIP_Y 0) y el escalado bilineal en
// una sola pasada (sin los escaladores con tabla de pesos: con ellos el video salia negro; ver Inicia). No volver a poner escaladores con
// tabla -lanczos, spline36, hermite, mitchell...- sin arreglar antes lo de la tabla de mpv.
//
// LA PANTALLA. Ni el reproductor ni su capa saben de ventanas: lo que necesitan de la pantalla (ponerse entre sus capas, despertarla cuando
// hay cuadro nuevo y avisar de que tocaron OpenGL) se lo da quien compone la pantalla por IPantallaDelVideo (Nativo/VideoEnPantalla.cs,
// que pone el anfitrion: Nativo/Anfitrion.cs).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Rocola.Pantalla;

namespace Rocola
{
    // El estado del video que XP mira, con los numeros que XP compara (en XP, playState del WMP): 1 detenido o con error, 2 en pausa,
    // 3 sonando, 8 llego al final, 9 abriendo.
    internal enum EstadoVideo { Detenido = 1, EnPausa = 2, Sonando = 3, Acabo = 8, Abriendo = 9 }

    // Lo que la pantalla hace por el video.
    internal interface IPantallaDelVideo
    {
        ILugarDelVideo Lugar(object ventana);   // la capa del video dentro de esa ventana, abajo del todo (en XP, Controls.Add)
        void Despierta();                        // hay un cuadro o un aviso nuevo: que la pantalla vuelva a componer
        void OpenGlTocado();                     // el video cambio el estado de OpenGL: quien lo guarde (la tarjeta) lo vuelve a poner
    }
    // La capa del video entre las de la pantalla: su sitio (en la ventana), si se ve, su orden y la textura que se pinta ahi.
    internal interface ILugarDelVideo
    {
        void Pon(Rectangle sitio, bool visible);
        void AlFrente();
        void Textura(uint textura, int ancho, int alto);
        void Quita();
    }

    internal sealed unsafe class ReproductorVideo : IDisposable
    {
        internal static IPantallaDelVideo Pantalla;   // la pone el compositor al arrancar
        private static readonly List<ReproductorVideo> vivos = new List<ReproductorVideo>();
        // Los que tienen capa (los pinta el compositor antes de componer cada cuadro)
        internal static ReproductorVideo[] Vivos() { lock (vivos) return vivos.ToArray(); }

        private IntPtr mpv, render;
        private volatile int estado = (int)EstadoVideo.Detenido;
        private volatile bool hayCuadro, fin;
        private string archivo = "";
        private int volumen = 100;
        private Thread avisos;
        private uint tex, fbo; private int texW, texH;
        private GCHandle propio;
        // la capa
        private ILugarDelVideo lugar;
        private Rectangle sitio;
        private bool visible = true;   // (un control de Windows nace visible)

        internal ReproductorVideo() { }

        // ---- la capa en la pantalla (en XP, la ventana del control del WMP) ----
        // Entra en la pantalla de esa ventana, debajo de lo que ya habia (en XP: Controls.Add, que pone el control nuevo al fondo).
        internal void EnPantalla(object ventana)
        {
            if (lugar != null || Pantalla == null) return;
            lugar = Pantalla.Lugar(ventana);
            lugar.Pon(sitio, visible);
            lock (vivos) if (!vivos.Contains(this)) vivos.Add(this);
        }
        // Sale de la pantalla (en XP: Controls.Remove)
        internal void FueraDePantalla()
        {
            lock (vivos) vivos.Remove(this);
            ILugarDelVideo l = lugar; lugar = null;
            if (l != null) l.Quita();
        }
        internal Rectangle Sitio { get { return sitio; } }
        internal void PonSitio(int x, int y, int ancho, int alto)
        {
            sitio = new Rectangle(x, y, ancho, alto);
            if (lugar != null) lugar.Pon(sitio, visible);
        }
        internal bool Visible
        {
            get { return visible; }
            set { visible = value; if (lugar != null) lugar.Pon(sitio, visible); }
        }
        internal void AlFrente() { if (lugar != null) lugar.AlFrente(); }

        // ---- el reproductor ----
        // libmpv abierto y listo (en XP, el control del WMP creado: GetOcx() != null). Sin libmpv no hay reproductor, como en un XP sin el WMP.
        internal bool Disponible { get { return mpv != IntPtr.Zero; } }
        // Abre libmpv (en XP, crear el control ActiveX al pedir su Handle). Si no puede, Disponible se queda en false.
        internal void Inicia()
        {
            if (mpv != IntPtr.Zero || !Mpv.Hay()) return;
            IntPtr m = Mpv.mpv_create();
            if (m == IntPtr.Zero) return;
            Mpv.mpv_set_option_string(m, "vo", "libmpv");
            Mpv.mpv_set_option_string(m, "hwdec", "no");   // (en el A4 el WMP de XP tambien decodificaba por el procesador)
            Mpv.mpv_set_option_string(m, "idle", "yes");
            Mpv.mpv_set_option_string(m, "keep-open", "no");
            Mpv.mpv_set_option_string(m, "terminal", "no");
            Mpv.mpv_set_option_string(m, "input-default-bindings", "no");
            Mpv.mpv_set_option_string(m, "input-vo-keyboard", "no");
            Mpv.mpv_set_option_string(m, "osc", "no");          // sin botones encima (en XP, uiMode "none")
            Mpv.mpv_set_option_string(m, "osd-level", "0");
            Mpv.mpv_set_option_string(m, "volume-max", "100");
            // LA IMAGEN COMO LA PINTABA LA TARJETA CON EL WMP DE XP: el cambio de tamano con su filtro bilineal, en una sola pasada (de YUV
            // a RGB y al tamano del hueco a la vez), sin luz lineal ni tramado. Es el perfil «fast» de mpv: con esto mpv entra solo en su
            // modo de una pasada («No advanced processing required. Enabling dumb mode»; si un video no se puede asi, como uno de 10 bits
            // en una tarjeta sin texturas de 16 bits, mpv hace sus pasadas, tambien bilineales). Los escaladores de fabrica de mpv
            // (lanczos para agrandar y para el color, hermite para achicar) no se usan, por dos motivos medidos el 7-11-2026:
            //  - un fallo de mpv (0.37 de aqui y 0.40 de Rocola OS: reinit_scaler de video/out/gpu/video.c): la tabla de pesos de esos
            //    escaladores se pide con talloc_array (sin poner a cero) y, cuando el filtro tiene 6 puntos (lanczos al agrandar y en el
            //    color; hermite al achicar entre 2 y 3 veces), los dos ultimos huecos de cada fila se suben a la tarjeta con lo que
            //    hubiera en esa memoria. mpv no los usa, pero lee la tabla con filtro lineal y, con la cuenta exacta de llvmpipe, un NaN
            //    de esa basura por un peso de 0 da NaN: los pesos, el color y la imagen entera salen NaN, y en pantalla negro (0,0,0,255).
            //    Depende de lo que dejo malloc en esa memoria: de ahi el video negro en 1 de cada 3 vueltas de la rocola y en casi todas
            //    las de la prueba aislada. Pasa igual fuera de la rocola (un programa en C con libmpv: negro las 6 veces que la tabla
            //    traia NaN y bien las 2 que no; con los NaN cambiados por 0, bien 10 de 10); en una tarjeta de verdad, segun como filtre;
            //  - es mucho menos trabajo para la tarjeta del A4 (lo de fabrica pinta hasta seis pasadas en texturas intermedias de coma
            //    flotante): con llvmpipe, unos 2-3 ms por cuadro (hasta 6 con la maquina cargada) frente a 13-25 ms con un video de
            //    640x360, 38-59 ms con uno de 1280x720 y 72-78 ms con uno de 1920x1080 (que asi no llegaba a sus 25 cuadros por segundo).
            // (No volver a poner escaladores con tabla -lanczos, spline36, hermite, mitchell...- sin arreglar antes lo de la tabla.)
            // El video se ajusta al hueco sin deformarse (bandas negras donde sobra), como el WMP con stretchToFit: lo de fabrica de mpv.
            Mpv.mpv_set_option_string(m, "scale", "bilinear");
            Mpv.mpv_set_option_string(m, "cscale", "bilinear");
            Mpv.mpv_set_option_string(m, "dscale", "bilinear");
            Mpv.mpv_set_option_string(m, "correct-downscaling", "no");
            Mpv.mpv_set_option_string(m, "linear-downscaling", "no");
            Mpv.mpv_set_option_string(m, "sigmoid-upscaling", "no");
            Mpv.mpv_set_option_string(m, "dither", "no");
            string registro = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_REGISTRO");   // (pruebas) lo que dice mpv
            if (!string.IsNullOrEmpty(registro)) { Mpv.mpv_set_option_string(m, "log-file", registro); Mpv.mpv_set_option_string(m, "msg-level", "all=v"); }
            string alsa = Environment.GetEnvironmentVariable("ROCOLA_ALSA");
            Mpv.mpv_set_option_string(m, "ao", "alsa");
            if (!string.IsNullOrEmpty(alsa)) Mpv.mpv_set_option_string(m, "audio-device", "alsa/" + alsa);
            string mas = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_OPCIONES");   // (pruebas) clave=valor;clave=valor para mpv, al final
            if (!string.IsNullOrEmpty(mas)) foreach (string o in mas.Split(';')) { int k = o.IndexOf('='); if (k > 0) Mpv.mpv_set_option_string(m, o.Substring(0, k), o.Substring(k + 1)); }
            if (Mpv.mpv_initialize(m) < 0) { Mpv.mpv_terminate_destroy(m); return; }
            mpv = m;
            CreaRender();   // ANTES de cargar nada: si el video empieza sin el, mpv lo deja sin imagen (solo sonido)
            AplicaVolumen();
            avisos = new Thread(Avisos) { IsBackground = true, Name = "video: mpv" };
            avisos.Start();
        }

        // Abre ese archivo y empieza a tocarlo (en XP: URL = ruta, con autoStart). Un archivo que no se puede abrir acaba en Detenido.
        internal void Carga(string ruta)
        {
            archivo = ruta ?? "";
            if (archivo.Length == 0) { QuitaArchivo(); return; }
            if (mpv == IntPtr.Zero) return;
            Mpv.mpv_set_property_string(mpv, "pause", "no");
            estado = (int)EstadoVideo.Abriendo;
            Mpv.Orden(mpv, "loadfile", archivo, "replace");
        }
        // Suelta el archivo (en XP: URL = "")
        internal void QuitaArchivo()
        {
            archivo = "";
            if (mpv != IntPtr.Zero) Mpv.Orden(mpv, "stop");
            estado = (int)EstadoVideo.Detenido;
        }
        internal bool TieneArchivo { get { return archivo.Length > 0; } }   // (en XP: currentMedia != null)
        internal void Toca() { if (mpv == IntPtr.Zero || archivo.Length == 0) return; Mpv.mpv_set_property_string(mpv, "pause", "no"); if (estado == (int)EstadoVideo.EnPausa) estado = (int)EstadoVideo.Sonando; }
        internal void Para() { if (mpv == IntPtr.Zero) { estado = (int)EstadoVideo.Detenido; return; } Mpv.Orden(mpv, "stop"); estado = (int)EstadoVideo.Detenido; }
        internal EstadoVideo Estado { get { return (EstadoVideo)estado; } }
        // Lo que lleva tocado y lo que dura, en segundos (0 mientras no se sabe)
        internal double Posicion { get { IntPtr m = mpv; double p = m != IntPtr.Zero ? Mpv.Numero(m, "time-pos") : -1; return p > 0 ? p : 0; } }
        internal double Duracion { get { IntPtr m = mpv; double d = m != IntPtr.Zero ? Mpv.Numero(m, "duration") : -1; return d > 0 ? d : 0; } }
        internal double NumeroMpv(string nombre) { IntPtr m = mpv; return m != IntPtr.Zero ? Mpv.Numero(m, nombre) : -1; }   // (pruebas)
        // El volumen, de 0 a 100 (en XP: settings.volume del WMP)
        internal int Volumen
        {
            get { return volumen; }
            set { volumen = Math.Max(0, Math.Min(100, value)); AplicaVolumen(); }
        }
        private void AplicaVolumen()
        {
            if (mpv == IntPtr.Zero) return;
            double cubico = 100.0 * Math.Pow(volumen / 100.0, 1.0 / 3.0);   // la amplitud del WMP (v / 100) con el volumen cubico de mpv
            Mpv.mpv_set_property_string(mpv, "volume", cubico.ToString("0.###", CultureInfo.InvariantCulture));
            Mpv.mpv_set_property_string(mpv, "mute", "no");
            if (traza) Console.Error.WriteLine("video: volumen " + volumen + " (mpv " + cubico.ToString("0.###", CultureInfo.InvariantCulture) + ")");
        }

        // Los avisos de mpv, en su hilo: el estado que mira XP
        private static readonly bool traza = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_TRAZA") == "1";
        private static readonly System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        private void Avisos()
        {
            IntPtr m = mpv;
            while (!fin)
            {
                Mpv.Evento* e = (Mpv.Evento*)Mpv.mpv_wait_event(m, 1.0);
                if (e == null || fin) continue;
                if (e->Id != Mpv.AVISO_NADA && traza) Console.Error.WriteLine("video: aviso " + e->Id + " error " + e->Error);
                switch (e->Id)
                {
                    case Mpv.AVISO_CIERRE: return;
                    case Mpv.AVISO_EMPIEZA: estado = (int)EstadoVideo.Abriendo; break;
                    case Mpv.AVISO_CARGADO: case Mpv.AVISO_SIGUE: estado = (int)(Mpv.Bandera(m, "pause") ? EstadoVideo.EnPausa : EstadoVideo.Sonando); break;
                    case Mpv.AVISO_ACABA:   // al final, Acabo; detenido o con error, Detenido (XP lo trata como «el video no se pudo ver» si no llego al final)
                        int razon = e->Dato != null ? *(int*)e->Dato : 0;
                        estado = (int)(razon == 0 ? EstadoVideo.Acabo : EstadoVideo.Detenido);
                        break;
                }
                IPantallaDelVideo p = Pantalla; if (p != null) p.Despierta();
            }
        }

        // ---- el cuadro: libmpv lo pinta en una textura de OpenGL (en el hilo de la pantalla: lo llama el compositor antes de componer) ----
        [UnmanagedCallersOnly] private static void HayCuadro(IntPtr datos)
        {
            GCHandle g = GCHandle.FromIntPtr(datos);
            ReproductorVideo r = g.Target as ReproductorVideo;
            if (r != null) { r.hayCuadro = true; IPantallaDelVideo p = Pantalla; if (p != null) p.Despierta(); }
        }
        internal void Pinta()
        {
            if (mpv == IntPtr.Zero || !visible) return;
            int w = Math.Max(1, sitio.Width), h = Math.Max(1, sitio.Height);
            if (render == IntPtr.Zero) CreaRender();
            if (render == IntPtr.Zero) return;
            PintaCuadro(w, h);
        }
        // La textura con el ultimo cuadro y su medida (0 si aun no hay)
        internal uint TexturaGl { get { return tex; } }
        internal int AnchoGl { get { return texW; } }
        internal int AltoGl { get { return texH; } }
        // El contexto de render de libmpv (OpenGL, el mismo contexto que la tarjeta: en el hilo de la pantalla)
        private void CreaRender()
        {
            if (render != IntPtr.Zero || mpv == IntPtr.Zero) return;
            Mpv.InicioGl ini = new Mpv.InicioGl { Buscar = &Mpv.BuscaFuncion, Ctx = IntPtr.Zero };
            byte* api = stackalloc byte[] { (byte)'o', (byte)'p', (byte)'e', (byte)'n', (byte)'g', (byte)'l', 0 };
            Mpv.Param* p = stackalloc Mpv.Param[3];
            p[0].Tipo = Mpv.PARAM_API; p[0].Dato = api;
            p[1].Tipo = Mpv.PARAM_INIT_GL; p[1].Dato = &ini;
            p[2].Tipo = 0; p[2].Dato = null;
            IntPtr rc;
            EstadoDeFabrica(Math.Max(1, sitio.Width), Math.Max(1, sitio.Height));
            int er = Mpv.mpv_render_context_create(&rc, mpv, p);
            if (traza) Console.Error.WriteLine("video: render de libmpv = " + er);
            OpenGlTocado();
            if (er < 0) return;
            render = rc;
            propio = GCHandle.Alloc(this);
            Mpv.mpv_render_context_set_update_callback(render, &HayCuadro, GCHandle.ToIntPtr(propio));
            hayCuadro = true;
        }
        private void PintaCuadro(int w, int h)
        {
            bool otra = tex == 0 || w != texW || h != texH;
            if (otra)
            {
                if (tex == 0) { uint t, f; GL.GenTextures(1, &t); GL.GenFramebuffers(1, &f); tex = t; fbo = f; }
                GL.BindTexture(GL.TEXTURE_2D, tex);
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, w, h, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
                GL.BindFramebuffer(GL.FRAMEBUFFER, fbo);
                GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, tex, 0);
                GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
                texW = w; texH = h; hayCuadro = true;
            }
            ulong cambios = Mpv.mpv_render_context_update(render);
            if ((cambios & 1) != 0 || otra)
            {
                Mpv.Fbo destino = new Mpv.Fbo { Nombre = (int)fbo, W = w, H = h, Formato = 0 };
                int gira = 0;   // sin girar, mpv deja la fila 0 arriba, como lo pinta la tarjeta (el compositor la pone derecha); con 1 salia de cabeza
                int sinBloquear = 0;   // que no espere aqui la hora del cuadro: la pantalla sigue a su ritmo (en XP el WMP iba aparte)
                Mpv.Param* p = stackalloc Mpv.Param[4];
                p[0].Tipo = Mpv.PARAM_FBO; p[0].Dato = &destino;
                p[1].Tipo = Mpv.PARAM_GIRA_Y; p[1].Dato = &gira;
                p[2].Tipo = Mpv.PARAM_SIN_BLOQUEO; p[2].Dato = &sinBloquear;
                p[3].Tipo = 0; p[3].Dato = null;
                EstadoDeFabrica(w, h);   // mpv cuenta con el estado de fabrica de OpenGL (ver abajo)
                int rr = Mpv.mpv_render_context_render(render, p);
                GlMas.Flush();   // (el orden no lo necesita -en un mismo contexto OpenGL lo respeta-: la tarjeta empieza el cuadro de mpv mientras se compone lo demas)
                if (traza)   // (pruebas) ROCOLA_VIDEO_TRAZA=1: cada cuadro pintado, con el punto del centro
                {
                    uint ge = GlMas.GetError();
                    GL.BindFramebuffer(GL.FRAMEBUFFER, fbo);
                    uint st = GL.CheckFramebufferStatus(GL.FRAMEBUFFER);
                    byte* px = stackalloc byte[4];
                    GL.ReadPixels(w / 2, h / 2, 1, 1, GL.RGBA, GL.UNSIGNED_BYTE, px);
                    Console.Error.WriteLine("video: " + (reloj.Elapsed.TotalMilliseconds / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " s cuadro " + w + "x" + h + " = " + rr + " glError=" + ge.ToString("x") + " fbo=" + st.ToString("x") + " centro=" + px[0] + "," + px[1] + "," + px[2] + "," + px[3]);
                }
                hayCuadro = false;
            }
            if (lugar != null) lugar.Textura(tex, w, h);
            OpenGlTocado();
        }
        private static void OpenGlTocado() { IPantallaDelVideo p = Pantalla; if (p != null) p.OpenGlTocado(); }

        // EL ESTADO DE OPENGL ENTRE MPV Y LA ROCOLA (el mismo contexto). libmpv pide que cuando se le llama OpenGL este «en sus valores de
        // fabrica» (render_gl.h, «OpenGL state»): sin mezcla, sin recorte, la mascara de color entera, la unidad de textura 0... Aqui llega con
        // lo que dejo la tarjeta (la mezcla encendida con la de la rocola, el alfa enmascarado en las superficies X8, sus vertices): con la
        // mezcla puesta, cada pase de mpv se mezclaba con lo que tenia su textura intermedia del cuadro anterior y los colores se iban sumando
        // hasta quedar en magenta a los pocos segundos (medido con llvmpipe), y el alfa salia en 0. Asi que antes de cada llamada a mpv se pone
        // ese estado (con el area de dibujo del tamano del video, como la de una ventana nueva); despues, OpenGlTocado le dice a la pantalla que
        // la tarjeta vuelva a poner el suyo.
        private const uint STENCIL_TEST = 0x0B90, POLYGON_OFFSET_FILL = 0x8037, SAMPLE_ALPHA_TO_COVERAGE = 0x809E, SAMPLE_COVERAGE = 0x80A0,
            ELEMENT_ARRAY_BUFFER = 0x8893, FUNC_ADD = 0x8006;
        [DllImport("libGLESv2.so.2", EntryPoint = "glBlendEquation")] private static extern void BlendEquation(uint modo);
        [DllImport("libGLESv2.so.2", EntryPoint = "glDepthMask")] private static extern void DepthMask(byte si);
        internal static void EstadoDeFabrica(int w, int h)
        {
            GL.Viewport(0, 0, w, h);   // (la de una ventana nueva; mpv pone la suya en cada pasada: render_gl.h la deja fuera)
            GL.Disable(GL.BLEND); GL.Disable(GL.SCISSOR_TEST); GL.Disable(GlMas.DEPTH_TEST); GL.Disable(GlMas.CULL_FACE);
            GL.Disable(STENCIL_TEST); GL.Disable(POLYGON_OFFSET_FILL); GL.Disable(SAMPLE_ALPHA_TO_COVERAGE); GL.Disable(SAMPLE_COVERAGE);
            GL.BlendFunc(GL.ONE, GlMas.ZERO); BlendEquation(FUNC_ADD);
            GlMas.ColorMask(1, 1, 1, 1); DepthMask(1);
            for (uint u = 0; u < 2; u++) { GL.ActiveTexture(GL.TEXTURE0 + u); GL.BindTexture(GL.TEXTURE_2D, 0); }   // (las dos etapas de la tarjeta)
            GL.ActiveTexture(GL.TEXTURE0);
            GL.UseProgram(0);
            for (uint a = 0; a < 4; a++) GlMas.DisableVertexAttribArray(a);   // (los vertices de la tarjeta, en su memoria)
            GL.BindBuffer(GL.ARRAY_BUFFER, 0); GL.BindBuffer(ELEMENT_ARRAY_BUFFER, 0);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4); GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
        }

        // Despues de presentar la pantalla (el compositor): a mpv le sirve para llevar el ritmo de los cuadros
        internal void TrasPresentar() { IntPtr r = render; if (r != IntPtr.Zero) Mpv.mpv_render_context_report_swap(r); }

        public void Dispose()
        {
            fin = true;
            FueraDePantalla();
            IntPtr rc = render; render = IntPtr.Zero;
            if (rc != IntPtr.Zero) { EstadoDeFabrica(Math.Max(1, texW), Math.Max(1, texH)); Mpv.mpv_render_context_free(rc); OpenGlTocado(); }   // (en el hilo de la pantalla)
            if (propio.IsAllocated) propio.Free();
            IntPtr m = mpv;
            if (m != IntPtr.Zero)
            {
                Mpv.mpv_wakeup(m);
                if (avisos != null) avisos.Join(2000);   // el hilo de los avisos sale antes de soltar mpv
                mpv = IntPtr.Zero;
                Mpv.mpv_terminate_destroy(m);
            }
            if (tex != 0) { uint t = tex, f = fbo; GL.DeleteTextures(1, &t); GlMas.DeleteFramebuffers(1, &f); tex = fbo = 0; OpenGlTocado(); }
        }
    }
}

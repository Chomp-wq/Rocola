// EL VIDEO DE LA ROCOLA DE XP, TRADUCIDO A LIBMPV (traduccion de XP; decision del dueno del 7-11-2026).
//
// En XP los videos los toca el control ActiveX de Windows Media Player (wmp.dll) y la rocola le habla por reflexion (Form1.Video.cs:
// Get/Set/Call con InvokeMember) con muy pocos nombres: URL, settings.volume, settings.autoStart, controls.play, controls.stop,
// controls.currentPosition, playState, currentMedia.duration, uiMode, stretchToFit, enableContextMenu, y para medir la duracion de un
// video (Form1.Biblioteca.cs) newMedia(ruta).duration y close. Aqui esos MISMOS nombres los contesta ReproductorWmp con libmpv (el mpv que
// ya trae Rocola OS), y el resto de XP no cambia:
//  - el control del video (VideoHost, un AxHost) lo recibe de AxHost.GetOcx (AxHost.Fabrica, por el CLSID del WMP) y queda «Nativa»: el
//    anfitrion pinta en su hueco la textura donde libmpv dibuja cada cuadro (su API de render con OpenGL, en el mismo contexto que la
//    tarjeta), como Windows ponia la ventana del WMP sobre la rocola;
//  - el sonido lo saca mpv por ALSA (la salida de la rocola, ROCOLA_ALSA), como el WMP lo sacaba por Windows, fuera de la cadena;
//  - volume del WMP (0 a 100): amplitud lineal, que es lo que supone la cuenta de XP (VideoVolumeFor multiplica amplitudes). mpv tiene
//    el volumen cubico: se le pone 100 * raiz cubica (v / 100);
//  - playState: 9 al empezar (Transitioning), 3 sonando, 2 en pausa, 8 al acabar (MediaEnded) y 1 si se detuvo o fallo (Stopped, que XP
//    trata como «el video no se pudo ver» si no llego al final), como el WMP.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Rocola.Pantalla;

namespace Rocola
{
    internal static unsafe class Mpv
    {
        private const string L = "libmpv.so.2";
        [DllImport(L)] internal static extern IntPtr mpv_create();
        [DllImport(L)] internal static extern int mpv_initialize(IntPtr ctx);
        [DllImport(L)] internal static extern void mpv_terminate_destroy(IntPtr ctx);
        [DllImport(L)] internal static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, [MarshalAs(UnmanagedType.LPUTF8Str)] string valor);
        [DllImport(L)] internal static extern int mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, [MarshalAs(UnmanagedType.LPUTF8Str)] string valor);
        [DllImport(L)] internal static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string nombre, int formato, void* dato);
        [DllImport(L)] internal static extern int mpv_command(IntPtr ctx, IntPtr* args);
        [DllImport(L)] internal static extern IntPtr mpv_wait_event(IntPtr ctx, double espera);
        [DllImport(L)] internal static extern void mpv_wakeup(IntPtr ctx);
        [DllImport(L)] internal static extern int mpv_render_context_create(IntPtr* res, IntPtr mpv, Param* p);
        [DllImport(L)] internal static extern void mpv_render_context_free(IntPtr ctx);
        [DllImport(L)] internal static extern void mpv_render_context_set_update_callback(IntPtr ctx, delegate* unmanaged<IntPtr, void> cb, IntPtr datos);
        [DllImport(L)] internal static extern ulong mpv_render_context_update(IntPtr ctx);
        [DllImport(L)] internal static extern int mpv_render_context_render(IntPtr ctx, Param* p);
        [DllImport("libEGL.so.1")] internal static extern IntPtr eglGetProcAddress(sbyte* nombre);
        internal const int FORMATO_FLAG = 3, FORMATO_DOUBLE = 5;
        internal const int PARAM_API = 1, PARAM_INIT_GL = 2, PARAM_FBO = 3, PARAM_GIRA_Y = 4, PARAM_SIN_BLOQUEO = 12;   // (BLOCK_FOR_TARGET_TIME)
        [DllImport(L)] internal static extern void mpv_render_context_report_swap(IntPtr ctx);
        [StructLayout(LayoutKind.Sequential)] internal struct Param { public int Tipo; public void* Dato; }
        [StructLayout(LayoutKind.Sequential)] internal struct InicioGl { public delegate* unmanaged<IntPtr, sbyte*, IntPtr> Buscar; public IntPtr Ctx; }
        [StructLayout(LayoutKind.Sequential)] internal struct Fbo { public int Nombre, W, H, Formato; }
        [StructLayout(LayoutKind.Sequential)] internal struct Evento { public int Id, Error; public ulong Usuario; public void* Dato; }
        [UnmanagedCallersOnly] internal static IntPtr BuscaFuncion(IntPtr ctx, sbyte* nombre) { return eglGetProcAddress(nombre); }
        internal static int Orden(IntPtr ctx, params string[] partes)
        {
            IntPtr* a = stackalloc IntPtr[partes.Length + 1];
            for (int i = 0; i < partes.Length; i++) a[i] = Marshal.StringToCoTaskMemUTF8(partes[i]);
            a[partes.Length] = IntPtr.Zero;
            try { return mpv_command(ctx, a); }
            finally { for (int i = 0; i < partes.Length; i++) Marshal.FreeCoTaskMem(a[i]); }
        }
        internal static double Numero(IntPtr ctx, string nombre) { double d = 0; return mpv_get_property(ctx, nombre, FORMATO_DOUBLE, &d) >= 0 ? d : -1; }
        internal static bool Bandera(IntPtr ctx, string nombre) { int f = 0; return mpv_get_property(ctx, nombre, FORMATO_FLAG, &f) >= 0 && f != 0; }
    }

    // ---- los objetos con los nombres del WMP (los lee XP por reflexion: los nombres en minuscula son los del WMP) ----
    public sealed class AjustesWmp
    {
        private readonly ReproductorWmp r;
        internal AjustesWmp(ReproductorWmp r) { this.r = r; }
        public int volume { get { return r.Volumen; } set { r.PonVolumen(value); } }
        public bool autoStart { get; set; } = true;
        public bool mute { get { return r.Mudo; } set { r.PonMudo(value); } }
    }
    public sealed class ControlesWmp
    {
        private readonly ReproductorWmp r;
        internal ControlesWmp(ReproductorWmp r) { this.r = r; }
        public void play() { r.Toca(); }
        public void stop() { r.Para(); }
        public void pause() { r.Pausa(); }
        public double currentPosition { get { return r.Posicion; } set { r.Busca(value); } }
    }
    public sealed class MedioWmp
    {
        private readonly ReproductorWmp r; private readonly string ruta; private double fija = -1;
        internal MedioWmp(ReproductorWmp r, string ruta) { this.r = r; this.ruta = ruta; }
        internal MedioWmp(string ruta, double duracion) { this.ruta = ruta; fija = duracion; }
        public double duration { get { return fija >= 0 ? fija : (r != null ? r.Duracion : 0); } }
        public string sourceURL { get { return ruta; } }
        public string name { get { return System.IO.Path.GetFileNameWithoutExtension(ruta ?? ""); } }
    }

    public sealed unsafe class ReproductorWmp : IDisposable
    {
        internal const string ClsidWmp = "6bf52a52-394a-11d3-b153-00c04f79faa6";
        // Lo que se mantiene vivo para la reflexion de XP en NativeAOT (Form1.Video.cs lo usa con InvokeMember)
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReproductorWmp))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AjustesWmp))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ControlesWmp))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MedioWmp))]
        internal static void Engancha()
        {
            AxHost.Fabrica = delegate (string clsid, AxHost host)
            {
                if (!string.Equals(clsid, ClsidWmp, StringComparison.OrdinalIgnoreCase)) return null;
                host.Nativa = true;   // lo pinta la tarjeta: la textura de libmpv
                return new ReproductorWmp(host);
            };
        }
        internal static readonly List<ReproductorWmp> Vivos = new List<ReproductorWmp>();

        private readonly Control host;
        private IntPtr mpv, render;
        private readonly object candado = new object();
        private volatile int estado;            // playState del WMP
        private volatile bool hayCuadro, fin;
        private string url = "";
        private int volumen = 100; private bool mudo;
        private Thread eventos;
        private uint tex, fbo; private int texW, texH;
        private readonly AjustesWmp ajustes; private readonly ControlesWmp controles;

        public ReproductorWmp() : this(null) { }   // (el medidor de duraciones de XP lo crea sin control)
        internal ReproductorWmp(Control host)
        {
            this.host = host;
            ajustes = new AjustesWmp(this); controles = new ControlesWmp(this);
            if (host != null) lock (Vivos) Vivos.Add(this);
        }

        // ---- lo que lee XP ----
        public string URL { get { return url; } set { Carga(value ?? ""); } }
        public AjustesWmp settings { get { return ajustes; } }
        public ControlesWmp controls { get { return controles; } }
        public int playState { get { return estado; } }
        public MedioWmp currentMedia { get { return string.IsNullOrEmpty(url) ? null : new MedioWmp(this, url); } }
        public string uiMode { get; set; } = "full";
        public bool stretchToFit { get; set; }
        public bool enableContextMenu { get; set; } = true;
        public void close() { Para(); }
        // Medir la duracion sin tocarlo (el medidor de la biblioteca): con ffprobe, como lo mide Rocola OS
        public MedioWmp newMedia(string ruta) { return new MedioWmp(ruta, DuracionDe(ruta)); }

        internal double NumeroMpv(string nombre) { IntPtr m = mpv; return m != IntPtr.Zero ? Mpv.Numero(m, nombre) : -1; }   // (pruebas)
        internal int Volumen { get { return volumen; } }
        internal bool Mudo { get { return mudo; } }
        internal double Duracion { get { IntPtr m = mpv; double d = m != IntPtr.Zero ? Mpv.Numero(m, "duration") : -1; return d > 0 ? d : 0; } }
        internal double Posicion { get { IntPtr m = mpv; double p = m != IntPtr.Zero ? Mpv.Numero(m, "time-pos") : -1; return p > 0 ? p : 0; } }

        // ---- mpv ----
        private void Abre()
        {
            if (mpv != IntPtr.Zero) return;
            IntPtr m = Mpv.mpv_create();
            if (m == IntPtr.Zero) throw new InvalidOperationException("libmpv no abre");
            Mpv.mpv_set_option_string(m, "vo", host != null ? "libmpv" : "null");
            Mpv.mpv_set_option_string(m, "hwdec", "no");   // (en el A4 el WMP de XP tambien decodificaba por el procesador)
            Mpv.mpv_set_option_string(m, "idle", "yes");
            Mpv.mpv_set_option_string(m, "keep-open", "no");
            Mpv.mpv_set_option_string(m, "terminal", "no");
            Mpv.mpv_set_option_string(m, "input-default-bindings", "no");
            Mpv.mpv_set_option_string(m, "input-vo-keyboard", "no");
            Mpv.mpv_set_option_string(m, "osc", "no");
            Mpv.mpv_set_option_string(m, "osd-level", "0");
            Mpv.mpv_set_option_string(m, "volume-max", "100");
            string registro = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_REGISTRO");   // (pruebas) lo que dice mpv
            if (!string.IsNullOrEmpty(registro)) { Mpv.mpv_set_option_string(m, "log-file", registro); Mpv.mpv_set_option_string(m, "msg-level", "all=v"); }
            string alsa = Environment.GetEnvironmentVariable("ROCOLA_ALSA");
            Mpv.mpv_set_option_string(m, "ao", "alsa");
            if (!string.IsNullOrEmpty(alsa)) Mpv.mpv_set_option_string(m, "audio-device", "alsa/" + alsa);
            string mas = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_OPCIONES");   // (pruebas) clave=valor;clave=valor para mpv, al final
            if (!string.IsNullOrEmpty(mas)) foreach (string o in mas.Split(';')) { int k = o.IndexOf('='); if (k > 0) Mpv.mpv_set_option_string(m, o.Substring(0, k), o.Substring(k + 1)); }
            if (Mpv.mpv_initialize(m) < 0) { Mpv.mpv_terminate_destroy(m); throw new InvalidOperationException("libmpv no inicia"); }
            mpv = m;
            if (host != null) CreaRender();   // ANTES de cargar nada: si el video empieza sin el, mpv lo deja sin imagen (solo sonido)
            AplicaVolumen();
            eventos = new Thread(Eventos) { IsBackground = true, Name = "video: mpv" };
            eventos.Start();
        }
        private static readonly bool traza = Environment.GetEnvironmentVariable("ROCOLA_VIDEO_TRAZA") == "1";
        private void Eventos()
        {
            IntPtr m = mpv;
            while (!fin)
            {
                Mpv.Evento* e = (Mpv.Evento*)Mpv.mpv_wait_event(m, 1.0);
                if (e == null || fin) continue;
                if (e->Id != 0 && traza) Console.Error.WriteLine("video: aviso " + e->Id + " error " + e->Error);
                switch (e->Id)
                {
                    case 1: return;                                 // SHUTDOWN
                    case 6: estado = 9; break;                      // START_FILE: Transitioning
                    case 8: case 21: estado = Mpv.Bandera(m, "pause") ? 2 : 3; break;   // FILE_LOADED, PLAYBACK_RESTART
                    case 7:                                         // END_FILE: al final, MediaEnded; detenido o con error, Stopped
                        int razon = e->Dato != null ? *(int*)e->Dato : 0;
                        estado = razon == 0 ? 8 : 1;
                        break;
                }
                Escritorio.Despierta();
            }
        }
        private void Carga(string ruta)
        {
            url = ruta;
            if (ruta.Length == 0) { if (mpv != IntPtr.Zero) Mpv.Orden(mpv, "stop"); estado = 1; return; }
            Abre();
            Mpv.mpv_set_property_string(mpv, "pause", ajustes.autoStart ? "no" : "yes");
            estado = 9;
            Mpv.Orden(mpv, "loadfile", ruta, "replace");
        }
        internal void Toca() { if (mpv == IntPtr.Zero || url.Length == 0) return; Mpv.mpv_set_property_string(mpv, "pause", "no"); if (estado == 2) estado = 3; }
        internal void Pausa() { if (mpv == IntPtr.Zero) return; Mpv.mpv_set_property_string(mpv, "pause", "yes"); if (estado == 3) estado = 2; }
        internal void Para() { if (mpv == IntPtr.Zero) { estado = 1; return; } Mpv.Orden(mpv, "stop"); estado = 1; }
        internal void Busca(double s) { if (mpv != IntPtr.Zero) Mpv.Orden(mpv, "seek", s.ToString("0.###", CultureInfo.InvariantCulture), "absolute"); }
        internal void PonVolumen(int v) { volumen = Math.Max(0, Math.Min(100, v)); AplicaVolumen(); }
        internal void PonMudo(bool m) { mudo = m; AplicaVolumen(); }
        private void AplicaVolumen()
        {
            if (mpv == IntPtr.Zero) return;
            double cubico = 100.0 * Math.Pow(volumen / 100.0, 1.0 / 3.0);   // la amplitud del WMP (v / 100) con el volumen cubico de mpv
            Mpv.mpv_set_property_string(mpv, "volume", cubico.ToString("0.###", CultureInfo.InvariantCulture));
            Mpv.mpv_set_property_string(mpv, "mute", mudo ? "yes" : "no");
        }

        // ---- el cuadro: libmpv lo pinta en una textura de OpenGL (en el hilo de la pantalla, lo llama el anfitrion antes de componer) ----
        [UnmanagedCallersOnly] private static void HayCuadro(IntPtr datos)
        {
            GCHandle g = GCHandle.FromIntPtr(datos);
            ReproductorWmp r = g.Target as ReproductorWmp;
            if (r != null) { r.hayCuadro = true; Escritorio.Despierta(); }
        }
        private GCHandle propio;
        internal void Pinta()
        {
            if (mpv == IntPtr.Zero || host == null || host.IsDisposed || !host.Visible) return;
            int w = Math.Max(1, host.Width), h = Math.Max(1, host.Height);
            if (render == IntPtr.Zero) CreaRender();
            if (render == IntPtr.Zero) return;
            PintaCuadro(w, h);
        }
        // El contexto de render de libmpv (OpenGL, el mismo contexto que la tarjeta: en el hilo de la pantalla)
        private void CreaRender()
        {
            if (render != IntPtr.Zero || mpv == IntPtr.Zero) return;
            {
                Mpv.InicioGl ini = new Mpv.InicioGl { Buscar = &Mpv.BuscaFuncion, Ctx = IntPtr.Zero };
                byte* api = stackalloc byte[] { (byte)'o', (byte)'p', (byte)'e', (byte)'n', (byte)'g', (byte)'l', 0 };
                Mpv.Param* p = stackalloc Mpv.Param[3];
                p[0].Tipo = Mpv.PARAM_API; p[0].Dato = api;
                p[1].Tipo = Mpv.PARAM_INIT_GL; p[1].Dato = &ini;
                p[2].Tipo = 0; p[2].Dato = null;
                IntPtr rc;
                EstadoDeFabrica(Math.Max(1, host.Width), Math.Max(1, host.Height));
                if (vuelcaEstado > 0) VuelcaEstado();
                int er = Mpv.mpv_render_context_create(&rc, mpv, p);
                if (Environment.GetEnvironmentVariable("ROCOLA_VIDEO_TRAZA") == "1") Console.Error.WriteLine("video: render de libmpv = " + er);
                if (er < 0) return;
                render = rc;
                propio = GCHandle.Alloc(this);
                Mpv.mpv_render_context_set_update_callback(render, &HayCuadro, GCHandle.ToIntPtr(propio));
                hayCuadro = true;
                D3D9.EnGl.EstadoTocado();
            }
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
                int gira = 0;   // sin girar, mpv deja la fila 0 arriba, como lo pinta la tarjeta (el anfitrion la pone derecha); con 1 salia de cabeza
                int sinBloquear = 0;   // que no espere aqui la hora del cuadro: el panel de la tarjeta sigue a su ritmo (en XP el WMP iba aparte)
                Mpv.Param* p = stackalloc Mpv.Param[4];
                p[0].Tipo = Mpv.PARAM_FBO; p[0].Dato = &destino;
                p[1].Tipo = Mpv.PARAM_GIRA_Y; p[1].Dato = &gira;
                p[2].Tipo = Mpv.PARAM_SIN_BLOQUEO; p[2].Dato = &sinBloquear;
                p[3].Tipo = 0; p[3].Dato = null;
                EstadoDeFabrica(w, h);   // mpv cuenta con el estado de fabrica de OpenGL (ver abajo)
                if (vuelcaEstado > 0) { vuelcaEstado--; VuelcaEstado(); }
                int rr = Mpv.mpv_render_context_render(render, p);
                if (Environment.GetEnvironmentVariable("ROCOLA_VIDEO_FINISH") == "1") GL.Finish(); else GlMas.Flush();   // sin esto, al componer la textura aun salia negra (medido con llvmpipe)
                if (Environment.GetEnvironmentVariable("ROCOLA_VIDEO_TRAZA") == "1")
                {
                    uint ge = GlMas.GetError();
                    GL.BindFramebuffer(GL.FRAMEBUFFER, fbo);
                    uint st = GL.CheckFramebufferStatus(GL.FRAMEBUFFER);
                    byte* px = stackalloc byte[4];
                    GL.ReadPixels(w / 2, h / 2, 1, 1, GL.RGBA, GL.UNSIGNED_BYTE, px);
                    Console.Error.WriteLine("video: " + (Escritorio.AhoraMs / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " s cuadro " + w + "x" + h + " = " + rr + " glError=" + ge.ToString("x") + " fbo=" + st.ToString("x") + " centro=" + px[0] + "," + px[1] + "," + px[2] + "," + px[3]);
                }
                hayCuadro = false;
            }
            host.TexturaGl = tex; host.AnchoGl = w; host.AltoGl = h; host.TexturaGlRgba = true;
            D3D9.EnGl.EstadoTocado();
        }

        // EL ESTADO DE OPENGL ENTRE MPV Y LA ROCOLA (el mismo contexto). libmpv pide que cuando se le llama OpenGL este «en sus valores de
        // fabrica» (render_gl.h, «OpenGL state»): sin mezcla, sin recorte, la mascara de color entera, la unidad de textura 0... Aqui llega con
        // lo que dejo la traduccion de Direct3D (la mezcla encendida con la de la rocola, el alfa enmascarado en las superficies X8, sus
        // vertices): con la mezcla puesta, cada pase de mpv se mezclaba con lo que tenia su textura intermedia del cuadro anterior y los
        // colores se iban sumando hasta quedar en magenta a los pocos segundos (medido con llvmpipe), y el alfa salia en 0. Asi que antes de
        // cada llamada a mpv se pone ese estado (con el area de dibujo del tamano del video, como la de una ventana nueva); despues,
        // EstadoTocado le dice a la traduccion que vuelva a poner el suyo.
        private const uint STENCIL_TEST = 0x0B90, POLYGON_OFFSET_FILL = 0x8037, SAMPLE_ALPHA_TO_COVERAGE = 0x809E, SAMPLE_COVERAGE = 0x80A0,
            ELEMENT_ARRAY_BUFFER = 0x8893, FUNC_ADD = 0x8006;
        [DllImport("libGLESv2.so.2", EntryPoint = "glBlendEquation")] private static extern void BlendEquation(uint modo);
        [DllImport("libGLESv2.so.2", EntryPoint = "glDepthMask")] private static extern void DepthMask(byte si);
        // (pruebas) ROCOLA_VIDEO_ESTADO=N: el estado de OpenGL antes de las N primeras llamadas a mpv
        private static int vuelcaEstado = int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_VIDEO_ESTADO"), out int ve) ? ve : 0;
        [DllImport("libGLESv2.so.2", EntryPoint = "glIsEnabled")] private static extern byte IsEnabled(uint c);
        [DllImport("libGLESv2.so.2", EntryPoint = "glGetVertexAttribfv")] private static extern void GetVertexAttribfv(uint i, uint q, float* v);
        [DllImport("libGLESv2.so.2", EntryPoint = "glGetVertexAttribiv")] private static extern void GetVertexAttribiv(uint i, uint q, int* v);
        private static void VuelcaEstado()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("video: estado");
            uint[] enteros = { 0x84E0, 0x8894, 0x8895, 0x85B5, 0x8B8D, 0x8CA6, 0x8CAA, 0x8CA7, 0x8069, 0x88ED, 0x88EF, 0x8A28, 0x8E25, 0x8919,
                0x0CF5, 0x0D05, 0x0CF2, 0x0CF3, 0x0CF4, 0x806E, 0x806D, 0x0D02, 0x0D03, 0x0D04, 0x8825, 0x0C02, 0x8192, 0x8B8B, 0x0B44, 0x0B45, 0x0B46,
                0x0B74, 0x0B92, 0x0B93, 0x0B94, 0x0B95, 0x0B96, 0x0B97, 0x0B98, 0x0BC1, 0x8009, 0x883D, 0x80C8, 0x80C9, 0x80CA, 0x80CB, 0x0B21, 0x0BC0, 0x0BC2 };
            int* v = stackalloc int[4];
            foreach (uint e in enteros) { v[0] = -12345; GL.GetIntegerv(e, v); sb.Append(' ').Append(e.ToString("x")).Append('=').Append(v[0]); GlMas.GetError(); }
            uint[] capacidades = { 0x0BE2, 0x0C11, 0x0B71, 0x0B44, 0x0B90, 0x0BD0, 0x8037, 0x809E, 0x80A0, 0x8D69, 0x8C89, 0x8DB9, 0x8E50 };
            foreach (uint c in capacidades) { sb.Append(" en").Append(c.ToString("x")).Append('=').Append(IsEnabled(c)); GlMas.GetError(); }
            GL.GetIntegerv(0x0BA2, v); sb.Append(" vista=").Append(v[0]).Append(',').Append(v[1]).Append(',').Append(v[2]).Append(',').Append(v[3]);
            GL.GetIntegerv(0x0C10, v); sb.Append(" recorte=").Append(v[0]).Append(',').Append(v[1]).Append(',').Append(v[2]).Append(',').Append(v[3]);
            GL.GetIntegerv(0x0C23, v); 
            float* f = stackalloc float[4];
            for (uint a = 0; a < 8; a++)
            {
                int on = 0; GetVertexAttribiv(a, 0x8622, &on);
                GetVertexAttribfv(a, 0x8626, f);
                sb.Append(" atr").Append(a).Append('=').Append(on).Append(':').Append(f[0]).Append(',').Append(f[1]).Append(',').Append(f[2]).Append(',').Append(f[3]);
            }
            for (uint u = 0; u < 4; u++) { GL.ActiveTexture(GL.TEXTURE0 + u); int t = 0; GL.GetIntegerv(0x8069, &t); int sm = 0; GL.GetIntegerv(0x8919, &sm); sb.Append(" u").Append(u).Append('=').Append(t).Append('/').Append(sm); }
            GL.ActiveTexture(GL.TEXTURE0);
            sb.Append(" error=").Append(GlMas.GetError().ToString("x"));
            Console.Error.WriteLine(sb.ToString());
        }
        internal static void EstadoDeFabrica(int w, int h)
        {
            GL.Viewport(0, 0, w, h);   // (el de fabrica es el tamano de la ventana; sin el -0x0 en una pantalla fuera de pantalla recien abierta- los
                                       // escaladores de calidad de mpv pintaban negro: medido con llvmpipe)
            GL.Disable(GL.BLEND); GL.Disable(GL.SCISSOR_TEST); GL.Disable(GlMas.DEPTH_TEST); GL.Disable(GlMas.CULL_FACE);
            GL.Disable(STENCIL_TEST); GL.Disable(POLYGON_OFFSET_FILL); GL.Disable(SAMPLE_ALPHA_TO_COVERAGE); GL.Disable(SAMPLE_COVERAGE);
            GL.BlendFunc(GL.ONE, GlMas.ZERO); BlendEquation(FUNC_ADD);
            GlMas.ColorMask(1, 1, 1, 1); DepthMask(1);
            for (uint u = 0; u < 2; u++) { GL.ActiveTexture(GL.TEXTURE0 + u); GL.BindTexture(GL.TEXTURE_2D, 0); }   // (las dos etapas de la rocola)
            GL.ActiveTexture(GL.TEXTURE0);
            GL.UseProgram(0);
            for (uint a = 0; a < 4; a++) GlMas.DisableVertexAttribArray(a);   // (los vertices de la rocola, en su memoria)
            GL.BindBuffer(GL.ARRAY_BUFFER, 0); GL.BindBuffer(ELEMENT_ARRAY_BUFFER, 0);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4); GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
        }

        // Despues de presentar la pantalla (el anfitrion): a mpv le sirve para llevar el ritmo de los cuadros
        internal void TrasPresentar() { IntPtr r = render; if (r != IntPtr.Zero) Mpv.mpv_render_context_report_swap(r); }

        // La duracion con ffprobe (lo mismo que mide el WMP al abrirlo: la del contenedor)
        private static double DuracionDe(string ruta)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo i = new System.Diagnostics.ProcessStartInfo("ffprobe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string a in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", ruta }) i.ArgumentList.Add(a);
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(i))
                {
                    string s = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return 0; }
                    double d; return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
                }
            }
            catch { return 0; }
        }

        public void Dispose()
        {
            fin = true;
            lock (Vivos) Vivos.Remove(this);
            IntPtr rc = render; render = IntPtr.Zero;
            if (rc != IntPtr.Zero) { EstadoDeFabrica(Math.Max(1, texW), Math.Max(1, texH)); Mpv.mpv_render_context_free(rc); D3D9.EnGl.EstadoTocado(); }   // (en el hilo de la pantalla: el AxHost se suelta ahi)
            if (propio.IsAllocated) propio.Free();
            IntPtr m = mpv;
            if (m != IntPtr.Zero)
            {
                Mpv.mpv_wakeup(m);
                if (eventos != null) eventos.Join(2000);   // el hilo de los avisos sale antes de soltar mpv
                mpv = IntPtr.Zero;
                Mpv.mpv_terminate_destroy(m);
            }
            if (tex != 0) { uint t = tex, f = fbo; GL.DeleteTextures(1, &t); GlMas.DeleteFramebuffers(1, &f); tex = fbo = 0; D3D9.EnGl.EstadoTocado(); }
            if (host != null) host.TexturaGl = 0;
        }
    }
}

// LA PANTALLA POR KMS (Rocola OS): sin servidor grafico. Se abre la tarjeta (/dev/dri/card0), se elige la salida conectada con su modo
// preferido, se dibuja con OpenGL ES en una superficie de GBM y cada cuadro se presenta con un cambio de pagina (page flip) sincronizado
// con el barrido (sin cortes). Al cerrar se deja la consola como estaba. Lo mismo que hace kmscube, que ya funciono en la etapa 1.
using System;
using System.Runtime.InteropServices;

namespace Rocola.Pantalla
{
    internal sealed unsafe class Kms : ISuperficie
    {
        private const string Drm = "libdrm.so.2", Gbm = "libgbm.so.1", C = "libc";
        [StructLayout(LayoutKind.Sequential)] private struct Res { public int count_fbs; public uint* fbs; public int count_crtcs; public uint* crtcs; public int count_connectors; public uint* connectors; public int count_encoders; public uint* encoders; public uint min_width, max_width, min_height, max_height; }
        [StructLayout(LayoutKind.Sequential)] private struct Modo { public uint clock; public ushort hdisplay, hsync_start, hsync_end, htotal, hskew, vdisplay, vsync_start, vsync_end, vtotal, vscan; public uint vrefresh, flags, type; public fixed byte name[32]; }
        [StructLayout(LayoutKind.Sequential)] private struct Conector { public uint connector_id, encoder_id, connector_type, connector_type_id; public int connection; public uint mmWidth, mmHeight; public int subpixel; public int count_modes; public Modo* modes; public int count_props; public uint* props; public ulong* prop_values; public int count_encoders; public uint* encoders; }
        [StructLayout(LayoutKind.Sequential)] private struct Codificador { public uint encoder_id, encoder_type, crtc_id, possible_crtcs, possible_clones; }
        [StructLayout(LayoutKind.Sequential)] private struct Crtc { public uint crtc_id, buffer_id, x, y, width, height; public int mode_valid; public Modo mode; public int gamma_size; }
        [DllImport(Drm)] private static extern Res* drmModeGetResources(int fd);
        [DllImport(Drm)] private static extern void drmModeFreeResources(Res* r);
        [DllImport(Drm)] private static extern Conector* drmModeGetConnector(int fd, uint id);
        [DllImport(Drm)] private static extern void drmModeFreeConnector(Conector* c);
        [DllImport(Drm)] private static extern Codificador* drmModeGetEncoder(int fd, uint id);
        [DllImport(Drm)] private static extern void drmModeFreeEncoder(Codificador* e);
        [DllImport(Drm)] private static extern Crtc* drmModeGetCrtc(int fd, uint id);
        [DllImport(Drm)] private static extern void drmModeFreeCrtc(Crtc* c);
        [DllImport(Drm)] private static extern int drmModeSetCrtc(int fd, uint crtc, uint fb, uint x, uint y, uint* conectores, int n, Modo* modo);
        [DllImport(Drm)] private static extern int drmModeAddFB(int fd, uint w, uint h, byte prof, byte bpp, uint paso, uint asa, uint* fb);
        [DllImport(Drm)] private static extern int drmModeRmFB(int fd, uint fb);
        [DllImport(Drm)] private static extern int drmModePageFlip(int fd, uint crtc, uint fb, uint banderas, IntPtr dato);
        [DllImport(Drm)] private static extern int drmDropMaster(int fd);
        [DllImport(Drm)] private static extern int drmSetMaster(int fd);
        [DllImport(Gbm)] private static extern IntPtr gbm_create_device(int fd);
        [DllImport(Gbm)] private static extern void gbm_device_destroy(IntPtr d);
        [DllImport(Gbm)] private static extern IntPtr gbm_surface_create(IntPtr d, uint w, uint h, uint formato, uint banderas);
        [DllImport(Gbm)] private static extern void gbm_surface_destroy(IntPtr s);
        [DllImport(Gbm)] private static extern IntPtr gbm_surface_lock_front_buffer(IntPtr s);
        [DllImport(Gbm)] private static extern void gbm_surface_release_buffer(IntPtr s, IntPtr bo);
        [DllImport(Gbm)] private static extern ulong gbm_bo_get_handle(IntPtr bo);
        [DllImport(Gbm)] private static extern uint gbm_bo_get_stride(IntPtr bo);
        [DllImport(C, SetLastError = true)] private static extern int open(string ruta, int banderas);
        [DllImport(C)] private static extern int close(int fd);
        [DllImport(C)] private static extern IntPtr read(int fd, byte* b, IntPtr n);
        private const uint XRGB8888 = 0x34325258, USO_PANTALLA = 1, USO_DIBUJO = 4, PAGE_FLIP_EVENT = 1;

        private readonly int fd;
        private readonly IntPtr gbm, superficie, egl, ctx, eglSup;
        private readonly uint crtc, conector;
        private Modo modo;
        private readonly Crtc* antes;
        private IntPtr boActual; private uint fbActual;
        private bool puesto;
        public int Ancho { get; private set; }
        public int Alto { get; private set; }
        public string Descripcion { get; private set; }

        internal Kms(string tarjeta = "/dev/dri/card0")
        {
            fd = open(tarjeta, 2 /* O_RDWR */ | 0x80000 /* O_CLOEXEC */);
            if (fd < 0) throw new InvalidOperationException("no se pudo abrir " + tarjeta + " (" + Marshal.GetLastWin32Error() + ")");
            Res* r = drmModeGetResources(fd);
            if (r == null) throw new InvalidOperationException(tarjeta + " no es una tarjeta con KMS");
            Conector* con = null;
            for (int i = 0; i < r->count_connectors && con == null; i++)
            {
                Conector* c = drmModeGetConnector(fd, r->connectors[i]);
                if (c != null && c->connection == 1 /* conectado */ && c->count_modes > 0) con = c; else if (c != null) drmModeFreeConnector(c);
            }
            if (con == null) { drmModeFreeResources(r); throw new InvalidOperationException("ninguna pantalla conectada"); }
            int elegido = 0;
            for (int i = 0; i < con->count_modes; i++) if ((con->modes[i].type & 8 /* PREFERRED */) != 0) { elegido = i; break; }
            modo = con->modes[elegido];
            conector = con->connector_id;
            uint crtcId = 0;
            if (con->encoder_id != 0) { Codificador* e = drmModeGetEncoder(fd, con->encoder_id); if (e != null) { crtcId = e->crtc_id; drmModeFreeEncoder(e); } }
            for (int i = 0; i < con->count_encoders && crtcId == 0; i++)
            {
                Codificador* e = drmModeGetEncoder(fd, con->encoders[i]); if (e == null) continue;
                for (int j = 0; j < r->count_crtcs; j++) if ((e->possible_crtcs & (1u << j)) != 0) { crtcId = r->crtcs[j]; break; }
                drmModeFreeEncoder(e);
            }
            drmModeFreeConnector(con); drmModeFreeResources(r);
            if (crtcId == 0) throw new InvalidOperationException("sin controlador de pantalla (crtc) para la salida");
            crtc = crtcId;
            antes = drmModeGetCrtc(fd, crtc);
            Ancho = modo.hdisplay; Alto = modo.vdisplay;
            gbm = gbm_create_device(fd);
            superficie = gbm_surface_create(gbm, (uint)Ancho, (uint)Alto, XRGB8888, USO_PANTALLA | USO_DIBUJO);
            if (superficie == IntPtr.Zero) throw new InvalidOperationException("GBM: sin superficie");
            IntPtr cfg;
            EGL.Contexto(EGL.PLATFORM_GBM_KHR, gbm, (int)XRGB8888, true, out egl, out cfg, out ctx);
            eglSup = EGL.CreateWindowSurface(egl, cfg, superficie, null);
            if (eglSup == IntPtr.Zero) throw new InvalidOperationException("EGL: sin superficie de ventana (" + EGL.GetError().ToString("x") + ")");
            EGL.MakeCurrent(egl, eglSup, eglSup, ctx);
            Descripcion = Ancho + "x" + Alto + " a " + modo.vrefresh + " Hz, " + GL.Texto(GL.RENDERER);
        }

        // Presenta lo dibujado: el cambio de pagina espera al barrido (no hay cortes) y se suelta el buffer anterior.
        public void Presenta()
        {
            EGL.SwapBuffers(egl, eglSup);
            IntPtr bo = gbm_surface_lock_front_buffer(superficie);
            uint fb;
            drmModeAddFB(fd, (uint)Ancho, (uint)Alto, 24, 32, gbm_bo_get_stride(bo), (uint)gbm_bo_get_handle(bo), &fb);
            if (!puesto)
            {
                uint con = conector; Modo m = modo;
                drmModeSetCrtc(fd, crtc, fb, 0, 0, &con, 1, &m);
                puesto = true;
            }
            else if (drmModePageFlip(fd, crtc, fb, PAGE_FLIP_EVENT, IntPtr.Zero) == 0)
            {
                byte* ev = stackalloc byte[1024];
                read(fd, ev, (IntPtr)1024);   // espera el aviso del cambio de pagina
            }
            if (boActual != IntPtr.Zero) { drmModeRmFB(fd, fbActual); gbm_surface_release_buffer(superficie, boActual); }
            boActual = bo; fbActual = fb;
        }

        // CEDER LA PANTALLA (a mpv, para un video) y recuperarla: se suelta el mando de la tarjeta (DRM master) y, al volver, se toma
        // otra vez y el siguiente cuadro vuelve a poner el modo.
        public void Suelta() { drmDropMaster(fd); }
        public void Recupera() { drmSetMaster(fd); puesto = false; }

        public void Dispose()
        {
            if (antes != null)
            {
                uint con = conector; Modo m = antes->mode;
                drmModeSetCrtc(fd, antes->crtc_id, antes->buffer_id, antes->x, antes->y, &con, 1, &m);   // la consola como estaba
                drmModeFreeCrtc(antes);
            }
            if (boActual != IntPtr.Zero) { drmModeRmFB(fd, fbActual); gbm_surface_release_buffer(superficie, boActual); }
            EGL.MakeCurrent(egl, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            EGL.DestroySurface(egl, eglSup); EGL.DestroyContext(egl, ctx); EGL.Terminate(egl);
            gbm_surface_destroy(superficie); gbm_device_destroy(gbm);
            close(fd);
        }
    }

    // Lo que necesita el dibujo: medida y presentar el cuadro.
    internal interface ISuperficie : IDisposable
    {
        int Ancho { get; }
        int Alto { get; }
        string Descripcion { get; }
        void Presenta();
        void Suelta();     // ceder la pantalla (un video)
        void Recupera();   // y recuperarla
    }

    // FUERA DE PANTALLA (para probar sin tarjeta ni monitor): Mesa sin superficie y un framebuffer de OpenGL; cada cuadro se puede
    // guardar como imagen (Captura).
    internal sealed unsafe class FueraDePantalla : ISuperficie
    {
        private readonly IntPtr egl, ctx;
        private readonly uint fbo, tex;
        public int Ancho { get; private set; }
        public int Alto { get; private set; }
        public string Descripcion { get; private set; }
        internal FueraDePantalla(int ancho, int alto)
        {
            Ancho = ancho; Alto = alto;
            IntPtr cfg;
            EGL.Contexto(EGL.PLATFORM_SURFACELESS_MESA, IntPtr.Zero, 0, false, out egl, out cfg, out ctx);
            if (EGL.MakeCurrent(egl, IntPtr.Zero, IntPtr.Zero, ctx) == 0) throw new InvalidOperationException("EGL: sin contexto actual");
            uint t, f;
            GL.GenTextures(1, &t); GL.BindTexture(GL.TEXTURE_2D, t);
            GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, ancho, alto, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR);
            GL.GenFramebuffers(1, &f); GL.BindFramebuffer(GL.FRAMEBUFFER, f);
            GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, t, 0);
            fbo = f; tex = t;
            Descripcion = ancho + "x" + alto + " fuera de pantalla, " + GL.Texto(GL.RENDERER);
        }
        public void Presenta() { GL.Finish(); }
        public void Suelta() { }
        public void Recupera() { }
        // El cuadro, en PPM (sin compresion; para mirarlo).
        internal void Captura(string ruta)
        {
            byte[] p = new byte[Ancho * Alto * 4];
            GL.BindFramebuffer(GL.FRAMEBUFFER, fbo);
            GL.PixelStorei(GL.PACK_ALIGNMENT, 1);
            fixed (byte* q = p) GL.ReadPixels(0, 0, Ancho, Alto, GL.RGBA, GL.UNSIGNED_BYTE, q);
            using (System.IO.FileStream f = System.IO.File.Create(ruta))
            {
                byte[] cab = System.Text.Encoding.ASCII.GetBytes("P6\n" + Ancho + " " + Alto + "\n255\n");
                f.Write(cab, 0, cab.Length);
                byte[] fila = new byte[Ancho * 3];
                for (int y = Alto - 1; y >= 0; y--)   // OpenGL lee de abajo arriba
                {
                    for (int x = 0; x < Ancho; x++) { int o = (y * Ancho + x) * 4; fila[3 * x] = p[o]; fila[3 * x + 1] = p[o + 1]; fila[3 * x + 2] = p[o + 2]; }
                    f.Write(fila, 0, fila.Length);
                }
            }
        }
        public void Dispose() { EGL.MakeCurrent(egl, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero); EGL.DestroyContext(egl, ctx); EGL.Terminate(egl); }
    }
}

// LA PANTALLA POR KMS (Rocola OS): sin servidor grafico. Se abre la tarjeta (/dev/dri/card0), se elige la salida conectada con su modo
// preferido, se dibuja con OpenGL ES en una superficie de GBM y cada cuadro se presenta con un cambio de pagina (page flip) sincronizado
// con el barrido (sin cortes). Al cerrar se deja la consola como estaba. Lo mismo que hace kmscube, que ya funciono en la etapa 1.
using System;
using System.Runtime.InteropServices;

namespace Rocola.Pantalla
{
    internal sealed unsafe class Kms : ISuperficie, IModosPantalla
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
        // (Rocola OS, fase 11, la hoja Pantalla de Ctrl+F4: la tabla de gamma, las propiedades del controlador y el nombre del controlador)
        [DllImport(Drm)] private static extern int drmModeCrtcSetGamma(int fd, uint crtc, uint tam, ushort* r, ushort* g, ushort* b);
        [StructLayout(LayoutKind.Sequential)] private struct Propiedades { public uint count_props; public uint* props; public ulong* prop_values; }
        [StructLayout(LayoutKind.Sequential)] private struct Propiedad { public uint prop_id, flags; public fixed byte name[32]; public int count_values; public ulong* values; public int count_enums; public IntPtr enums; public int count_blobs; public uint* blob_ids; }
        [DllImport(Drm)] private static extern Propiedades* drmModeObjectGetProperties(int fd, uint objeto, uint tipo);
        [DllImport(Drm)] private static extern void drmModeFreeObjectProperties(Propiedades* p);
        [DllImport(Drm)] private static extern Propiedad* drmModeGetProperty(int fd, uint id);
        [DllImport(Drm)] private static extern void drmModeFreeProperty(Propiedad* p);
        [DllImport(Drm)] private static extern int drmModeObjectSetProperty(int fd, uint objeto, uint tipo, uint propiedad, ulong valor);
        [DllImport(Drm)] private static extern int drmModeCreatePropertyBlob(int fd, void* datos, UIntPtr tam, uint* id);
        [DllImport(Drm)] private static extern int drmModeDestroyPropertyBlob(int fd, uint id);
        [StructLayout(LayoutKind.Sequential)] private struct Version { public int major, minor, patch; public int name_len; public byte* name; public int date_len; public byte* date; public int desc_len; public byte* desc; }
        [DllImport(Drm)] private static extern Version* drmGetVersion(int fd);
        [DllImport(Drm)] private static extern void drmFreeVersion(Version* v);
        private const uint OBJETO_CRTC = 0xcccccccc;
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
        [StructLayout(LayoutKind.Sequential)] private struct PollFd { public int fd; public short events, revents; }
        [DllImport(C)] private static extern int poll(PollFd* f, ulong n, int ms);
        private const uint XRGB8888 = 0x34325258, USO_PANTALLA = 1, USO_DIBUJO = 4, PAGE_FLIP_EVENT = 1;

        private readonly int fd;
        private readonly IntPtr gbm, egl, ctx, cfg;
        private IntPtr superficie, eglSup;   // (Rocola OS: se rehacen al cambiar de modo, CambiaModo)
        private readonly uint crtc, conector;
        private Modo modo;
        private readonly Crtc* antes;
        private IntPtr boActual;
        private bool puesto;
        public int Ancho { get; private set; }
        public int Alto { get; private set; }
        public string Descripcion { get; private set; }

        // 1024x768 a 60 Hz (VESA DMT): 65 MHz, sincronias negativas
        private static Modo Vesa1024x768()
        {
            Modo m = new Modo { clock = 65000, hdisplay = 1024, hsync_start = 1048, hsync_end = 1184, htotal = 1344, vdisplay = 768, vsync_start = 771, vsync_end = 777, vtotal = 806, vrefresh = 60, flags = 0x2 | 0x8 /* -hsync -vsync */, type = 0x40 /* DRIVER */ };
            byte[] n = System.Text.Encoding.ASCII.GetBytes("1024x768");
            for (int i = 0; i < n.Length; i++) m.name[i] = n[i];
            return m;
        }
        internal bool ACiegas { get; private set; }

        internal Kms(string tarjeta = "/dev/dri/card0")
        {
            fd = open(tarjeta, 2 /* O_RDWR */ | 0x80000 /* O_CLOEXEC */);
            if (fd < 0) throw new InvalidOperationException("no se pudo abrir " + tarjeta + " (" + Marshal.GetLastWin32Error() + ")");
            Res* r = drmModeGetResources(fd);
            if (r == null) throw new InvalidOperationException(tarjeta + " no es una tarjeta con KMS");
            // SIN MONITOR AL PRENDER (4-11-2026): con el monitor o la tele apagados (o en otra entrada) el controlador puede no ver ninguna
            // pantalla «conectada», y la rocola se quedaba en modo texto toda la noche aunque despues prendieran el monitor. Como XP (que
            // saca imagen siempre): si no hay ninguna conectada, la primera salida que exista, con su modo si lo dio o 1024x768 a 60 Hz
            // (VESA, lo acepta cualquier monitor); al prender el monitor ya hay imagen.
            Conector* con = null, otra = null;
            for (int i = 0; i < r->count_connectors && con == null; i++)
            {
                Conector* c = drmModeGetConnector(fd, r->connectors[i]);
                if (c == null) continue;
                if (c->connection == 1 /* conectado */ && c->count_modes > 0) con = c;
                else if (otra == null && c->count_encoders > 0) otra = c;
                else drmModeFreeConnector(c);
            }
            bool aCiegas = false;
            if (con == null && otra != null) { con = otra; otra = null; aCiegas = true; }
            if (otra != null) drmModeFreeConnector(otra);
            if (con == null) { drmModeFreeResources(r); throw new InvalidOperationException("ninguna salida de pantalla"); }
            int elegido = 0;
            if (con->count_modes == 0)
            {
                modo = Vesa1024x768();
                modos = new Modo[] { modo }; automatico = 0;
                goto modoHecho;
            }
            modos = new Modo[con->count_modes];
            for (int i = 0; i < con->count_modes; i++) modos[i] = con->modes[i];
            // LO AUTOMATICO (ModoAutomatico, abajo; lo usa tambien la pantalla simulada de las pruebas de Rocola OS): el preferido del
            // monitor, a esa resolucion el refresco mas alto (75 Hz en vez de 60, si lo tiene: mas cuadros, movimiento mas liso) y, con V30
            // apagado, 1024x768 si el monitor la ofrece (la de XP), la de mas refresco
            elegido = automatico = ModoAutomatico(Vista(modos), Form1.ResolucionNativaGuardada());
            // ROCOLA OS (fase 11, la hoja Pantalla de Ctrl+F4): el modo guardado por el dueno, si el monitor lo da (EligeModo; sin poner, lo
            // automatico como siempre)
            Func<ModoPantalla[], int, int> elige = EligeModo;
            if (elige != null && !aCiegas) { int e = elige(Vista(modos), automatico); if (e >= 0 && e < modos.Length) elegido = e; }
            modo = con->modes[elegido];
        modoHecho:
            ACiegas = aCiegas;
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
            EGL.Contexto(EGL.PLATFORM_GBM_KHR, gbm, (int)XRGB8888, true, out egl, out cfg, out ctx);
            eglSup = EGL.CreateWindowSurface(egl, cfg, superficie, null);
            if (eglSup == IntPtr.Zero) throw new InvalidOperationException("EGL: sin superficie de ventana (" + EGL.GetError().ToString("x") + ")");
            EGL.MakeCurrent(egl, eglSup, eglSup, ctx);
            Descripcion = Ancho + "x" + Alto + " a " + modo.vrefresh + " Hz, " + GL.Texto(GL.RENDERER) + (ACiegas ? " (sin monitor detectado al prender: salida a ciegas)" : "");
            Controlador = NombreDelControlador();
            // ROCOLA OS: un modo que no es el automatico se pone YA, con un cuadro negro, antes de que la rocola pinte nada; si el
            // controlador no lo acepta, lo automatico (y FalloAlArrancar lo dice). Sin EligeModo (o eligiendo lo automatico), nada cambia.
            if (elegido != automatico && modos != null)
            {
                string error;
                if (!PonYa(out error))
                {
                    FalloAlArrancar = Nombre(modos[elegido]) + ": " + error;
                    CambiaSuperficie(modos[automatico], true);
                    if (!PonYa(out error)) FalloAlArrancar += "; y lo automatico tampoco (" + error + ")";
                }
            }
        }

        // Presenta lo dibujado: el cambio de pagina espera al barrido (no hay cortes). SIN ESPERAR AQUI (3-11-2026): se pide el cambio y
        // se vuelve a dibujar el siguiente mientras la tarjeta termina este y llega el barrido; la espera es al presentar el siguiente
        // (si el anterior aun no cambio). Asi la preparacion de un cuadro y el dibujo del anterior van a la vez, no por turnos. GBM tiene
        // buffers de sobra para eso (en pantalla, el que espera el barrido y el que se dibuja). El framebuffer de DRM de cada buffer se
        // hace una vez y se reusa (antes se hacia y se borraba en cada cuadro).
        // ROCOLA_FLIP_ESPERA=1: como antes (esperar el cambio en el mismo cuadro), por si una tarjeta no lo llevara bien.
        private static readonly bool esperaAlMomento = Environment.GetEnvironmentVariable("ROCOLA_FLIP_ESPERA") != null;
        private System.Collections.Generic.Dictionary<IntPtr, uint> fbs = new System.Collections.Generic.Dictionary<IntPtr, uint>();   // (Rocola OS: otro al cambiar de medida)
        private IntPtr boPendiente; private bool pendiente;
        private uint FbDe(IntPtr bo)
        {
            uint fb;
            if (fbs.TryGetValue(bo, out fb)) return fb;
            drmModeAddFB(fd, (uint)Ancho, (uint)Alto, 24, 32, gbm_bo_get_stride(bo), (uint)gbm_bo_get_handle(bo), &fb);
            fbs[bo] = fb;
            return fb;
        }
        // Espera el aviso del cambio pedido: lo que estaba en pantalla se suelta y el pendiente pasa a estar en pantalla.
        // CON LIMITE (4-11-2026): antes se esperaba el aviso sin tope; si no llegaba (la tarjeta colgada, el monitor o la tele que se
        // desconectan y el controlador que deja de avisar), el bucle de la rocola se quedaba parado para siempre, y con el no entraba la
        // siguiente cancion ni respondian los botones. Ahora, a los CambioMaxMs se sigue como si hubiera llegado (se apunta una vez).
        internal const int CambioMaxMs = 500;
        internal static long CambiosSinAviso;
        private void EsperaCambio()
        {
            if (!pendiente) return;
            byte* ev = stackalloc byte[1024];
            PollFd pf = new PollFd { fd = fd, events = 1 /* POLLIN */ };
            int r = poll(&pf, 1, CambioMaxMs);
            if (r > 0) read(fd, ev, (IntPtr)1024);
            else if (System.Threading.Interlocked.Increment(ref CambiosSinAviso) == 1)
                try { Console.Error.WriteLine("Pantalla: la tarjeta no aviso del cambio de cuadro en " + CambioMaxMs + " ms; se sigue sin esperar"); } catch { }
            pendiente = false;
            if (boActual != IntPtr.Zero) gbm_surface_release_buffer(superficie, boActual);
            boActual = boPendiente; boPendiente = IntPtr.Zero;
        }
        private void PonEnPantalla(IntPtr bo)
        {
            if (boActual != IntPtr.Zero && boActual != bo) gbm_surface_release_buffer(superficie, boActual);
            boActual = bo;
        }
        public void Presenta()
        {
            EGL.SwapBuffers(egl, eglSup);
            IntPtr bo = gbm_surface_lock_front_buffer(superficie);
            if (bo == IntPtr.Zero) return;
            uint fb = FbDe(bo);
            EsperaCambio();   // el cambio del cuadro anterior (si aun no llego el barrido)
            if (!puesto)
            {
                uint con = conector; Modo m = modo;
                int r = drmModeSetCrtc(fd, crtc, fb, 0, 0, &con, 1, &m);
                if (r != 0) UltimoFallo = "drmModeSetCrtc " + r;   // (Rocola OS: lo mira la hoja Pantalla; antes no se miraba)
                puesto = true;
                PonEnPantalla(bo);
            }
            else if (drmModePageFlip(fd, crtc, fb, PAGE_FLIP_EVENT, IntPtr.Zero) == 0)
            {
                pendiente = true; boPendiente = bo;
                if (esperaAlMomento) EsperaCambio();
            }
            else PonEnPantalla(bo);   // no se pudo pedir el cambio (como antes: se da por puesto y se suelta el anterior)
        }

        // ======== ROCOLA OS (fase 11, la hoja Pantalla de Ctrl+F4, RocolaOs/RocolaOs.Pantalla.cs): los modos del monitor, cambiar de modo y
        // los colores del monitor por la tabla de gamma (y la matriz de color, si la hay) del controlador de pantalla (CRTC). Sin que nadie
        // lo pida (EligeModo sin poner, sin llamar a CambiaModo ni a PonGamma), la pantalla es la de siempre. ========
        private Modo[] modos;        // los del monitor (los del conector, con su EDID), en el orden del controlador
        private int automatico;      // el que se pone solo (ModoAutomatico)
        // A QUIEN ABRE LA PANTALLA: que modo poner al abrirla (los modos y el automatico; devuelve el indice). Sin poner, lo automatico.
        internal static Func<ModoPantalla[], int, int> EligeModo;
        // si el modo elegido no se pudo poner al abrir (y se puso lo automatico): que paso; null si no paso nada
        public string FalloAlArrancar { get; private set; }
        // el ultimo fallo de drmModeSetCrtc al presentar (antes no se miraba)
        public string UltimoFallo { get; private set; }
        // el nombre del controlador del nucleo (radeon, amdgpu, i915, bochs-drm...)
        public string Controlador { get; private set; }

        private static ModoPantalla[] Vista(Modo[] m)
        {
            ModoPantalla[] r = new ModoPantalla[m.Length];
            for (int i = 0; i < m.Length; i++)
            {
                Modo x = m[i];
                double hz = x.htotal > 0 && x.vtotal > 0 ? x.clock * 1000.0 / ((double)x.htotal * x.vtotal) : x.vrefresh;
                if ((x.flags & 0x10) != 0) hz *= 2;   // entrelazado: dos campos por cuadro
                r[i] = new ModoPantalla(i, x.hdisplay, x.vdisplay, (int)x.vrefresh, hz, (x.flags & 0x10) != 0, (x.type & 8) != 0);
            }
            return r;
        }
        private static string Nombre(Modo m) { return m.hdisplay + "x" + m.vdisplay + "@" + m.vrefresh + ((m.flags & 0x10) != 0 ? "i" : ""); }

        // LO AUTOMATICO (lo de siempre, sacado aqui sin cambiarlo para que lo use tambien la pantalla simulada de las pruebas): el preferido
        // del monitor; a esa resolucion, el refresco mas alto que no sea entrelazado; y sin la resolucion nativa (V30 apagado, lo de XP),
        // 1024x768 si el monitor la ofrece, la de mas refresco.
        internal static int ModoAutomatico(ModoPantalla[] m, bool nativa)
        {
            int elegido = 0;
            for (int i = 0; i < m.Length; i++) if (m[i].Preferido) { elegido = i; break; }
            for (int i = 0; i < m.Length; i++)
                if (m[i].Ancho == m[elegido].Ancho && m[i].Alto == m[elegido].Alto && !m[i].Entrelazado && m[i].Hz > m[elegido].Hz) elegido = i;
            if (!nativa)
                for (int i = 0, mejor = -1; i < m.Length; i++)
                    if (m[i].Ancho == 1024 && m[i].Alto == 768 && (mejor < 0 || m[i].Hz > m[mejor].Hz)) { mejor = i; elegido = i; }
            return elegido;
        }

        public ModoPantalla[] Modos { get { return modos == null ? new ModoPantalla[0] : Vista(modos); } }
        public int Automatico { get { return automatico; } }
        public ModoPantalla Actual
        {
            get
            {
                ModoPantalla[] v = Modos;
                for (int i = 0; i < v.Length; i++) if (modos[i].hdisplay == modo.hdisplay && modos[i].vdisplay == modo.vdisplay && modos[i].vrefresh == modo.vrefresh && modos[i].clock == modo.clock && modos[i].flags == modo.flags) return v[i];
                return Vista(new Modo[] { modo })[0];
            }
        }

        // PONER EL MODO YA: un cuadro negro de la medida del modo y drmModeSetCrtc con el. Lo que pasa en la pantalla se sabe al momento.
        private bool PonYa(out string error)
        {
            error = null;
            GL.BindFramebuffer(GL.FRAMEBUFFER, 0);
            GL.Disable(GL.SCISSOR_TEST);
            GL.Viewport(0, 0, Ancho, Alto);
            GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
            EGL.SwapBuffers(egl, eglSup);
            IntPtr bo = gbm_surface_lock_front_buffer(superficie);
            if (bo == IntPtr.Zero) { error = "GBM: sin buffer"; return false; }
            uint fb = FbDe(bo);
            uint con = conector; Modo m = modo;
            int r = drmModeSetCrtc(fd, crtc, fb, 0, 0, &con, 1, &m);
            if (r != 0) { error = "el controlador no lo acepta (drmModeSetCrtc " + r + ")"; gbm_surface_release_buffer(superficie, bo); return false; }
            puesto = true;
            PonEnPantalla(bo);
            return true;
        }
        // La superficie de otra medida (GBM y EGL; el contexto de OpenGL es el mismo: las texturas y los programas siguen). Lo de la medida
        // de antes se suelta (con suelta) o se devuelve (para volver atras).
        private struct Superficies { public IntPtr Gbm, Egl, Bo; public System.Collections.Generic.Dictionary<IntPtr, uint> Fbs; public Modo Modo; }
        private Superficies Guarda() { return new Superficies { Gbm = superficie, Egl = eglSup, Bo = boActual, Fbs = fbs, Modo = modo }; }
        private void CambiaSuperficie(Modo nuevo, bool sueltaLaVieja)
        {
            EsperaCambio();
            Superficies v = Guarda();
            if (nuevo.hdisplay != modo.hdisplay || nuevo.vdisplay != modo.vdisplay)
            {
                IntPtr s = gbm_surface_create(gbm, nuevo.hdisplay, nuevo.vdisplay, XRGB8888, USO_PANTALLA | USO_DIBUJO);
                if (s == IntPtr.Zero) throw new InvalidOperationException("GBM: sin superficie de " + Nombre(nuevo));
                IntPtr e = EGL.CreateWindowSurface(egl, cfg, s, null);
                if (e == IntPtr.Zero) { gbm_surface_destroy(s); throw new InvalidOperationException("EGL: sin superficie de " + Nombre(nuevo) + " (" + EGL.GetError().ToString("x") + ")"); }
                EGL.MakeCurrent(egl, e, e, ctx);
                superficie = s; eglSup = e; boActual = IntPtr.Zero; fbs = new System.Collections.Generic.Dictionary<IntPtr, uint>();
                if (sueltaLaVieja) Suelta(v);
            }
            modo = nuevo; Ancho = modo.hdisplay; Alto = modo.vdisplay; puesto = false;
        }
        private void Suelta(Superficies v)
        {
            if (v.Bo != IntPtr.Zero) gbm_surface_release_buffer(v.Gbm, v.Bo);
            foreach (uint f in v.Fbs.Values) drmModeRmFB(fd, f);
            EGL.DestroySurface(egl, v.Egl); gbm_surface_destroy(v.Gbm);
        }
        // CAMBIAR DE MODO con la rocola andando (en el hilo de la pantalla): la superficie nueva, un cuadro negro y drmModeSetCrtc; si el
        // controlador no lo acepta, todo como estaba (false y por que). La rocola se acomoda a la medida nueva despues (quien llama).
        public bool CambiaModo(int indice, out string error)
        {
            error = null;
            if (modos == null || indice < 0 || indice >= modos.Length) { error = "ese modo no es del monitor"; return false; }
            EsperaCambio();
            Superficies v = Guarda();
            bool otraMedida = modos[indice].hdisplay != modo.hdisplay || modos[indice].vdisplay != modo.vdisplay;
            try { CambiaSuperficie(modos[indice], false); }
            catch (Exception ex) { error = ex.Message; return false; }
            if (PonYa(out error))
            {
                if (otraMedida) Suelta(v);
                Descripcion = Ancho + "x" + Alto + " a " + modo.vrefresh + " Hz, " + GL.Texto(GL.RENDERER);
                UltimoFallo = null;
                return true;
            }
            // no: como estaba (el controlador no cambio nada: sigue en pantalla lo de antes)
            if (otraMedida)
            {
                Superficies nueva = Guarda();
                superficie = v.Gbm; eglSup = v.Egl; boActual = v.Bo; fbs = v.Fbs;
                EGL.MakeCurrent(egl, eglSup, eglSup, ctx);   // (primero la de antes al frente; despues se suelta la que no sirvio)
                Suelta(nueva);
            }
            modo = v.Modo; Ancho = modo.hdisplay; Alto = modo.vdisplay; puesto = true;
            return false;
        }

        // ---- los colores: la tabla de gamma del CRTC (GAMMA_LUT por la propiedad atomica si el controlador la tiene; si no, la llamada de
        // siempre, drmModeCrtcSetGamma) y la matriz de color (CTM), solo si la tiene ----
        private bool propsLeidas, gammaTocada;
        private int gammaTam, tamGammaLut;
        private uint propGammaLut, propCtm;
        private void LeePropiedades()
        {
            if (propsLeidas) return;
            propsLeidas = true;
            Crtc* c = drmModeGetCrtc(fd, crtc);
            if (c != null) { gammaTam = c->gamma_size; drmModeFreeCrtc(c); }
            Propiedades* p = drmModeObjectGetProperties(fd, crtc, OBJETO_CRTC);
            if (p == null) return;
            for (uint i = 0; i < p->count_props; i++)
            {
                Propiedad* q = drmModeGetProperty(fd, p->props[i]);
                if (q == null) continue;
                string n = new string((sbyte*)q->name);
                if (n == "GAMMA_LUT") propGammaLut = q->prop_id;
                else if (n == "GAMMA_LUT_SIZE") tamGammaLut = (int)p->prop_values[i];
                else if (n == "CTM") propCtm = q->prop_id;
                drmModeFreeProperty(q);
            }
            drmModeFreeObjectProperties(p);
        }
        private bool PorLut { get { LeePropiedades(); return propGammaLut != 0 && tamGammaLut > 0; } }
        public int TamanoGamma { get { LeePropiedades(); return PorLut ? tamGammaLut : gammaTam > 0 ? gammaTam : 0; } }
        public string ViaGamma { get { return TamanoGamma == 0 ? null : PorLut ? "GAMMA_LUT" : "drmModeCrtcSetGamma"; } }
        public bool ConCtm { get { LeePropiedades(); return propCtm != 0; } }
        public bool PonGamma(ushort[] r, ushort[] g, ushort[] b, out string error)
        {
            error = null;
            int n = TamanoGamma;
            if (n == 0) { error = "el controlador no tiene tabla de gamma"; return false; }
            if (r == null || r.Length != n || g.Length != n || b.Length != n) { error = "la tabla no mide " + n; return false; }
            gammaTocada = true;
            if (PorLut)
            {
                ushort[] lut = new ushort[n * 4];
                for (int i = 0; i < n; i++) { lut[4 * i] = r[i]; lut[4 * i + 1] = g[i]; lut[4 * i + 2] = b[i]; }
                return PonBlob(propGammaLut, lut, n * 8, out error);
            }
            int k;
            fixed (ushort* pr = r, pg = g, pb = b) k = drmModeCrtcSetGamma(fd, crtc, (uint)n, pr, pg, pb);
            if (k != 0) { error = "drmModeCrtcSetGamma " + k; return false; }
            return true;
        }
        // la matriz de color, 3x3 por filas (null: ninguna)
        public bool PonCtm(double[] m, out string error)
        {
            error = null;
            if (!ConCtm) { error = "el controlador no tiene matriz de color (CTM)"; return false; }
            gammaTocada = true;
            if (m == null) { int k = drmModeObjectSetProperty(fd, crtc, OBJETO_CRTC, propCtm, 0); if (k != 0) error = "CTM " + k; return k == 0; }
            ulong[] v = new ulong[9];
            for (int i = 0; i < 9; i++) v[i] = S31_32(m[i]);
            return PonBlob(propCtm, v, 72, out error);
        }
        internal static ulong S31_32(double x)   // signo y magnitud (drm_color_ctm)
        {
            ulong mag = (ulong)Math.Round(Math.Abs(x) * 4294967296.0);
            return x < 0 ? (mag | 0x8000000000000000UL) : mag;
        }
        private bool PonBlob<T>(uint prop, T[] datos, int bytes, out string error) where T : unmanaged
        {
            error = null;
            uint id;
            fixed (T* d = datos) { int k = drmModeCreatePropertyBlob(fd, d, (UIntPtr)bytes, &id); if (k != 0) { error = "blob " + k; return false; } }
            int r = drmModeObjectSetProperty(fd, crtc, OBJETO_CRTC, prop, id);
            drmModeDestroyPropertyBlob(fd, id);   // (la propiedad se queda con su copia)
            if (r != 0) { error = "propiedad " + r; return false; }
            return true;
        }
        // LOS COLORES DE FABRICA: sin tabla (GAMMA_LUT) o la tabla con que el nucleo arranca (drm_mode_crtc_set_gamma_size: i << 8 con 256),
        // y sin matriz
        public bool QuitaColores(out string error)
        {
            error = null;
            bool ok = true;
            if (PorLut) { int k = drmModeObjectSetProperty(fd, crtc, OBJETO_CRTC, propGammaLut, 0); if (k != 0) { ok = false; error = "GAMMA_LUT " + k; } }
            else if (TamanoGamma > 0)
            {
                int n = TamanoGamma; ushort[] t = TablaDeFabrica(n);
                int k; fixed (ushort* p = t) k = drmModeCrtcSetGamma(fd, crtc, (uint)n, p, p, p);
                if (k != 0) { ok = false; error = "drmModeCrtcSetGamma " + k; }
            }
            if (ConCtm) { int k = drmModeObjectSetProperty(fd, crtc, OBJETO_CRTC, propCtm, 0); if (k != 0) { ok = false; error = (error == null ? "" : error + "; ") + "CTM " + k; } }
            if (ok) gammaTocada = false;
            return ok;
        }
        internal static ushort[] TablaDeFabrica(int n)
        {
            ushort[] t = new ushort[n];
            for (int i = 0; i < n; i++) t[i] = (ushort)Math.Min(65535L, (long)i * 65536 / n);
            return t;
        }
        private string NombreDelControlador()
        {
            Version* v = drmGetVersion(fd);
            if (v == null) return "";
            string n = v->name != null ? new string((sbyte*)v->name, 0, v->name_len) : "";
            drmFreeVersion(v);
            return n;
        }

        // CEDER LA PANTALLA (a mpv, para un video) y recuperarla: se suelta el mando de la tarjeta (DRM master) y, al volver, se toma
        // otra vez y el siguiente cuadro vuelve a poner el modo.
        public void Suelta() { EsperaCambio(); drmDropMaster(fd); }
        public void Recupera() { drmSetMaster(fd); puesto = false; }

        public void Dispose()
        {
            EsperaCambio();
            if (gammaTocada) try { string e; QuitaColores(out e); } catch { }   // (Rocola OS) la consola, con los colores de fabrica
            if (antes != null)
            {
                uint con = conector; Modo m = antes->mode;
                drmModeSetCrtc(fd, antes->crtc_id, antes->buffer_id, antes->x, antes->y, &con, 1, &m);   // la consola como estaba
                drmModeFreeCrtc(antes);
            }
            if (boActual != IntPtr.Zero) gbm_surface_release_buffer(superficie, boActual);
            foreach (uint f in fbs.Values) drmModeRmFB(fd, f);
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

    // (Rocola OS, fase 11, la hoja Pantalla de Ctrl+F4) UN MODO DEL MONITOR: su medida, sus hercios (los redondos del controlador y los
    // de verdad), si es entrelazado y si es el preferido del monitor; Indice es su lugar en la lista del controlador.
    internal readonly struct ModoPantalla
    {
        public readonly int Indice, Ancho, Alto, Hz;
        public readonly double HzExactos;
        public readonly bool Entrelazado, Preferido;
        public ModoPantalla(int indice, int ancho, int alto, int hz, double exactos, bool entrelazado, bool preferido)
        { Indice = indice; Ancho = ancho; Alto = alto; Hz = hz; HzExactos = exactos; Entrelazado = entrelazado; Preferido = preferido; }
        // como se guarda: «1920x1080@60» («i» al final si es entrelazado)
        public string Clave { get { return Ancho + "x" + Alto + "@" + Hz + (Entrelazado ? "i" : ""); } }
        public override string ToString() { return Clave; }
    }
    // (Rocola OS) LA PANTALLA QUE SE PUEDE CAMBIAR: la de KMS (Kms) y la simulada de las pruebas (RocolaOs/RocolaOs.PantallaSimulada.cs)
    internal interface IModosPantalla
    {
        ModoPantalla[] Modos { get; }
        int Automatico { get; }
        ModoPantalla Actual { get; }
        string FalloAlArrancar { get; }
        string UltimoFallo { get; }
        string Controlador { get; }
        bool CambiaModo(int indice, out string error);
        int TamanoGamma { get; }
        string ViaGamma { get; }
        bool ConCtm { get; }
        bool PonGamma(ushort[] r, ushort[] g, ushort[] b, out string error);
        bool PonCtm(double[] m, out string error);
        bool QuitaColores(out string error);
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
        public void Presenta() { if (Environment.GetEnvironmentVariable("ROCOLA_BANCO_SIN_FIN") == null) GL.Finish(); }   // el banco puede medir solo lo del programa
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

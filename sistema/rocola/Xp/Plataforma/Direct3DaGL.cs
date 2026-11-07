// DIRECT3D 9 DE LA ROCOLA DE XP, EN OPENGL ES 2 (traduccion de XP, fase 1; decision del dueno del 7-11-2026).
//
// En XP, D3D9Star.cs y los demas archivos de la tarjeta llaman a Direct3D 9 por su tabla de funciones (D3D9.Fn<T>(objeto, hueco)). En la
// traduccion, cada una de esas llamadas se cambia por la funcion de aqui con el MISMO nombre que tiene en Direct3D y los mismos
// parametros (D3D9.EnGl.Present en vez de D3D9.Fn<D3D9.PresentDel>(dev, 17)), que hace lo mismo con OpenGL ES 2 (Mesa; en el A4, r600
// por KMS). Todo lo demas de XP queda igual.
//
// COMO SE HACE CADA COSA
//  - Los objetos (Direct3D, el dispositivo, texturas, superficies, vertices) son objetos de aqui con su cuenta de referencias, como en
//    COM. El IntPtr que ve el codigo de XP es su numero (no se repite nunca).
//  - Los colores van en el orden de Direct3D (A8R8G8B8: en memoria B, G, R, A) de punta a punta: las texturas se suben tal cual, el
//    color del vertice se lee tal cual y lo pintado queda igual en la textura del fondo. Las cuentas de las etapas y de la mezcla son las
//    mismas en cada canal de color, asi que no hace falta cambiarlos de orden; solo al ponerlo en la pantalla (el anfitrion) se leen en
//    el orden de OpenGL.
//  - Las coordenadas de Direct3D 9 son pixeles con el centro en el entero; en OpenGL el centro esta en el medio: x de OpenGL = x + 0.5.
//    Todo se pinta en texturas (el fondo del dispositivo y las imagenes intermedias) sin dar la vuelta: la fila 0 de Direct3D es la
//    fila 0 de la textura, como al subir una imagen. El anfitrion la pone derecha en la pantalla.
//  - Lo que Direct3D 9 hace sin programa (las etapas de textura, SetTextureStageState) se convierte en un programa de OpenGL por cada
//    combinacion que se usa (se hace la primera vez y se guarda). La mezcla (SetRenderState) es la de OpenGL.
//  - Bloquear una textura da una memoria aparte del tamano de lo bloqueado; al soltarla se sube a la tarjeta (glTexSubImage2D).
//  - Present pasa la textura del fondo al escritorio (Escritorio.PresentaNativa), que compone las ventanas con ella y la pone en pantalla.
//  - Los vertices ya transformados (XYZRHW) se interpolan como en Direct3D (con su RHW; XP siempre pone 1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Rocola.Pantalla;

namespace Rocola
{
    // Lo de OpenGL ES 2 que hace falta aqui y no esta en Gl.cs (que es de la rocola de Linux de ahora y no se toca).
    internal static unsafe class GlMas
    {
        private const string L = "libGLESv2.so.2";
        internal const uint ZERO = 0, SRC_COLOR = 0x0300, ONE_MINUS_SRC_COLOR = 0x0301, DST_ALPHA = 0x0304, ONE_MINUS_DST_ALPHA = 0x0305,
            DST_COLOR = 0x0306, ONE_MINUS_DST_COLOR = 0x0307, SRC_ALPHA_SATURATE = 0x0308, NEAREST = 0x2600, REPEAT = 0x2901,
            MIRRORED_REPEAT = 0x8370, CULL_FACE = 0x0B44, DYNAMIC_DRAW = 0x88E8, MAX_TEXTURE_SIZE = 0x0D33, POINTS = 0, LINES = 1,
            LINE_STRIP = 3, TRIANGLE_STRIP = 5, TRIANGLE_FAN = 6, OUT_OF_MEMORY = 0x0505, INFO_LOG_LENGTH = 0x8B84, DEPTH_TEST = 0x0B71,
            DITHER = 0x0BD0;
        [DllImport(L, EntryPoint = "glDisableVertexAttribArray")] internal static extern void DisableVertexAttribArray(uint i);
        [DllImport(L, EntryPoint = "glVertexAttrib4f")] internal static extern void VertexAttrib4f(uint i, float x, float y, float z, float w);
        [DllImport(L, EntryPoint = "glBufferSubData")] internal static extern void BufferSubData(uint d, IntPtr desp, IntPtr tam, void* datos);
        [DllImport(L, EntryPoint = "glDeleteBuffers")] internal static extern void DeleteBuffers(int n, uint* b);
        [DllImport(L, EntryPoint = "glDeleteFramebuffers")] internal static extern void DeleteFramebuffers(int n, uint* f);
        [DllImport(L, EntryPoint = "glDeleteShader")] internal static extern void DeleteShader(uint s);
        [DllImport(L, EntryPoint = "glGetProgramInfoLog")] internal static extern void GetProgramInfoLog(uint p, int max, int* largo, byte* texto);
        [DllImport(L, EntryPoint = "glGetError")] internal static extern uint GetError();
        [DllImport(L, EntryPoint = "glColorMask")] internal static extern void ColorMask(byte r, byte g, byte b, byte a);
        [DllImport(L, EntryPoint = "glFlush")] internal static extern void Flush();
    }

    internal static unsafe partial class D3D9
    {
        // Lo que dice Direct3D 9 cuando algo no se puede (los mismos numeros)
        internal const int D3D_OK = 0, D3DERR_INVALIDCALL = unchecked((int)0x8876086C), D3DERR_OUTOFVIDEOMEMORY = unchecked((int)0x8876017C),
            E_OUTOFMEMORY = unchecked((int)0x8007000E), D3DERR_NOTAVAILABLE = unchecked((int)0x8876086A);

        // ---------------- los objetos ----------------
        internal abstract class ObjetoGl { internal int Refs = 1; internal IntPtr Id; internal abstract void Libera(); }
        internal sealed class Direct3DGl : ObjetoGl { internal override void Libera() { } }

        internal sealed class TexturaGl : ObjetoGl
        {
            internal uint Nombre;              // la textura de OpenGL
            internal int W, H;
            internal uint Formato, Uso, Pool;
            internal bool SinAlfa;             // X8R8G8B8: el alfa vale 1 (al pintar en ella no se toca)
            internal SuperficieGl Nivel0;      // GetSurfaceLevel(0)
            internal IntPtr Bloqueo; internal int BX, BY, BW, BH; internal bool Bloqueada, BloqueoPropio;
            internal uint PuestoS, PuestoT, PuestoMin, PuestoMag;   // lo que tiene puesto en OpenGL (para no repetirlo)
            internal long Bytes { get { return (long)W * H * 4; } }
            internal override void Libera()
            {
                if (Bloqueada) { Bloqueada = false; SueltaMemoriaBloqueo(this); }
                if (Nivel0 != null) { Nivel0.Textura = null; Nivel0 = null; }
                uint n = Nombre; Nombre = 0;
                if (n != 0) EnGl.BorraTextura(n, Bytes);
            }
        }

        internal sealed class SuperficieGl : ObjetoGl
        {
            internal TexturaGl Textura;        // la textura donde se pinta (el fondo del dispositivo tambien es una)
            internal uint Fbo;
            internal bool DelFondo;            // el fondo del dispositivo (no se suelta con la textura)
            internal int W { get { return Textura != null ? Textura.W : 0; } }
            internal int H { get { return Textura != null ? Textura.H : 0; } }
            internal override void Libera()
            {
                uint f = Fbo; Fbo = 0;
                if (f != 0) EnGl.BorraFbo(f);
                TexturaGl t = Textura; Textura = null;
                if (t != null)
                {
                    if (t.Nivel0 == this) t.Nivel0 = null;
                    if (DelFondo) t.Libera();      // la textura del fondo es solo suya
                    else ReleaseObjeto(t);          // la superficie de una textura la tiene viva (como en Direct3D)
                }
            }
        }

        internal sealed class VerticesGl : ObjetoGl
        {
            internal uint Vbo; internal int Bytes;
            internal IntPtr Copia;             // lo bloqueado se escribe aqui y se sube al soltar
            internal int BDesde, BBytes; internal bool Bloqueado;
            internal override void Libera()
            {
                if (Copia != IntPtr.Zero) { NativeMemory.Free((void*)Copia); Copia = IntPtr.Zero; }
                uint b = Vbo; Vbo = 0;
                if (b != 0) EnGl.BorraVbo(b);
            }
        }

        // Una etapa de textura (D3DTSS_*) y su muestreo (D3DSAMP_*), con los valores de Direct3D 9 al empezar.
        internal struct Etapa
        {
            internal uint ColorOp, ColorArg1, ColorArg2, AlphaOp, AlphaArg1, AlphaArg2, IndiceCoordenadas;
            internal uint DireccionU, DireccionV, FiltroMag, FiltroMin;
            internal IntPtr Textura;
            internal static Etapa DeFabrica(int i)
            {
                Etapa e = new Etapa();
                e.ColorOp = i == 0 ? TOP_MODULATE : TOP_DISABLE; e.ColorArg1 = TA_TEXTURE; e.ColorArg2 = TA_CURRENT;
                e.AlphaOp = i == 0 ? TOP_SELECTARG1 : TOP_DISABLE; e.AlphaArg1 = TA_TEXTURE; e.AlphaArg2 = TA_CURRENT;
                e.IndiceCoordenadas = (uint)i;
                e.DireccionU = ADDRESS_WRAP; e.DireccionV = ADDRESS_WRAP; e.FiltroMag = 1; e.FiltroMin = 1;   // D3DTEXF_POINT
                return e;
            }
        }
        internal const int EtapasGl = 2;   // las que usa XP (la 0 y la 1, en la pasada de dos texturas)

        internal sealed class DispositivoGl : ObjetoGl
        {
            internal IntPtr Ventana;            // el control de la rocola donde se presenta (en XP, la ventana)
            internal PresentParams Pp;
            internal SuperficieGl Fondo;        // el fondo (back buffer): una textura de OpenGL con su FBO
            internal SuperficieGl Destino;      // donde se pinta ahora (SetRenderTarget)
            internal bool EnEscena;
            internal int Hilo;                  // el hilo de OpenGL (el de la pantalla)
            // el estado de Direct3D (lo que pidio el programa)
            internal bool Mezcla; internal uint Origen = 2 /* ONE */, Destino2 = 1 /* ZERO */;
            internal uint Recorte = 3;          // D3DCULL_CCW de fabrica (XP pone NONE)
            internal uint Fvf;
            internal Etapa[] Etapas = new Etapa[8];
            internal IntPtr Flujo; internal uint FlujoDesp, FlujoPaso;   // SetStreamSource
            // lo que esta puesto en OpenGL
            internal bool EstadoSucio = true, ProgramaSucio = true;
            internal ProgramaGl Programa;
            internal uint FboPuesto = uint.MaxValue; internal int VistaW = -1, VistaH = -1; internal bool MascaraSinAlfa;
            internal int MezclaPuesta = -1; internal uint OrigenPuesto, DestinoPuesto;
            internal uint[] TexturaPuesta = new uint[EtapasGl];
            internal uint TexturaNegra;         // la de una etapa sin textura: (0, 0, 0, 1)
            internal int Presentaciones;
            internal DispositivoGl() { for (int i = 0; i < Etapas.Length; i++) Etapas[i] = Etapa.DeFabrica(i); }
            internal override void Libera()
            {
                SuperficieGl f = Fondo; Fondo = null;
                SuperficieGl d = Destino; Destino = null;
                if (d != null) ReleaseObjeto(d);   // la referencia del destino (aunque sea el fondo)
                if (f != null) ReleaseObjeto(f);
                for (int i = 0; i < Etapas.Length; i++) { IntPtr t = Etapas[i].Textura; Etapas[i].Textura = IntPtr.Zero; if (t != IntPtr.Zero) ReleaseId(t); }
                if (Flujo != IntPtr.Zero) { ReleaseId(Flujo); Flujo = IntPtr.Zero; }
                if (TexturaNegra != 0) { EnGl.BorraTextura(TexturaNegra, 4); TexturaNegra = 0; }
                Control c = Control.FromHandle(Ventana);
                if (c != null) c.TexturaGl = 0;
            }
        }

        internal sealed class ProgramaGl { internal uint Nombre; internal int LugarM; internal int W = -1, H = -1; internal bool[] Muestrea = new bool[EtapasGl]; }

        // ---------------- la tabla de objetos ----------------
        private static readonly object candadoGl = new object();
        private static readonly Dictionary<IntPtr, ObjetoGl> objetosGl = new Dictionary<IntPtr, ObjetoGl>();
        private static long siguienteIdGl = 0x10000;
        internal static int ObjetosGlVivos { get { lock (candadoGl) return objetosGl.Count; } }
        private static IntPtr RegistraGl(ObjetoGl o)
        {
            lock (candadoGl) { siguienteIdGl += 16; o.Id = new IntPtr(siguienteIdGl); objetosGl[o.Id] = o; }
            return o.Id;
        }
        internal static T ObjetoDe<T>(IntPtr id) where T : ObjetoGl
        {
            if (id == IntPtr.Zero) return null;
            lock (candadoGl) { ObjetoGl o; return objetosGl.TryGetValue(id, out o) ? o as T : null; }
        }
        private static uint ReleaseId(IntPtr id)
        {
            ObjetoGl o;
            lock (candadoGl) { if (!objetosGl.TryGetValue(id, out o)) return 0; }
            return ReleaseObjeto(o);
        }
        private static uint ReleaseObjeto(ObjetoGl o)
        {
            int r;
            lock (candadoGl)
            {
                r = --o.Refs;
                if (r > 0) return (uint)r;
                objetosGl.Remove(o.Id);
            }
            o.Libera();
            return 0;
        }
        private static void AddRefObjeto(ObjetoGl o) { lock (candadoGl) o.Refs++; }

        // La memoria de bloquear una textura: una que se reusa (las portadas se suben de una en una) y, si esta ocupada, otra.
        private static IntPtr memoriaBloqueo; private static long memoriaBloqueoBytes; private static bool memoriaBloqueoOcupada;
        private static IntPtr TomaMemoriaBloqueo(TexturaGl t, long bytes)
        {
            lock (candadoGl)
            {
                if (!memoriaBloqueoOcupada)
                {
                    if (memoriaBloqueoBytes < bytes)
                    {
                        if (memoriaBloqueo != IntPtr.Zero) NativeMemory.Free((void*)memoriaBloqueo);
                        memoriaBloqueo = (IntPtr)NativeMemory.Alloc((nuint)bytes); memoriaBloqueoBytes = bytes;
                    }
                    memoriaBloqueoOcupada = true; t.BloqueoPropio = false;
                    return memoriaBloqueo;
                }
            }
            t.BloqueoPropio = true;
            return (IntPtr)NativeMemory.Alloc((nuint)bytes);
        }
        private static void SueltaMemoriaBloqueo(TexturaGl t)
        {
            if (t.BloqueoPropio) { if (t.Bloqueo != IntPtr.Zero) NativeMemory.Free((void*)t.Bloqueo); }
            else lock (candadoGl) memoriaBloqueoOcupada = false;
            t.Bloqueo = IntPtr.Zero; t.BloqueoPropio = false;
        }

        // ======================================================================================================================
        // LAS FUNCIONES DE DIRECT3D 9, EN OPENGL ES 2 (el nombre de cada una es el de Direct3D; entre corchetes, su hueco en la tabla)
        // ======================================================================================================================
        internal static class EnGl
        {
            // ---- lo que se borra fuera del hilo de OpenGL: se borra en el siguiente cuadro ----
            private static int hiloGl = -1;
            private static readonly List<uint> texturasPorBorrar = new List<uint>(), fbosPorBorrar = new List<uint>(), vbosPorBorrar = new List<uint>();
            private static long bytesTexturas;   // lo que ocupan las texturas de la rocola en la tarjeta (GetAvailableTextureMem lo descuenta)
            internal static long BytesTexturas { get { return Interlocked.Read(ref bytesTexturas); } }
            private static bool EnHiloGl { get { return hiloGl < 0 || Thread.CurrentThread.ManagedThreadId == hiloGl; } }
            internal static void BorraTextura(uint n, long bytes)
            {
                Interlocked.Add(ref bytesTexturas, -bytes);
                if (!EnHiloGl) { lock (texturasPorBorrar) texturasPorBorrar.Add(n); return; }
                GL.DeleteTextures(1, &n);
                OlvidaTextura(n);
            }
            internal static void BorraFbo(uint f)
            {
                if (!EnHiloGl) { lock (fbosPorBorrar) fbosPorBorrar.Add(f); return; }
                GlMas.DeleteFramebuffers(1, &f);
                foreach (DispositivoGl d in dispositivos) if (d.FboPuesto == f) d.FboPuesto = uint.MaxValue;
            }
            internal static void BorraVbo(uint b)
            {
                if (!EnHiloGl) { lock (vbosPorBorrar) vbosPorBorrar.Add(b); return; }
                GlMas.DeleteBuffers(1, &b);
            }
            private static void BorraPendientes()
            {
                lock (texturasPorBorrar) { foreach (uint n0 in texturasPorBorrar) { uint n = n0; GL.DeleteTextures(1, &n); OlvidaTextura(n); } texturasPorBorrar.Clear(); }
                lock (fbosPorBorrar) { foreach (uint f0 in fbosPorBorrar) { uint f = f0; GlMas.DeleteFramebuffers(1, &f); } if (fbosPorBorrar.Count > 0) foreach (DispositivoGl d in dispositivos) d.FboPuesto = uint.MaxValue; fbosPorBorrar.Clear(); }
                lock (vbosPorBorrar) { foreach (uint b0 in vbosPorBorrar) { uint b = b0; GlMas.DeleteBuffers(1, &b); } vbosPorBorrar.Clear(); }
            }
            // una textura borrada que seguia puesta en una etapa: OpenGL puede dar su nombre a otra
            private static void OlvidaTextura(uint n)
            {
                foreach (DispositivoGl d in dispositivos)
                    for (int i = 0; i < EtapasGl; i++) if (d.TexturaPuesta[i] == n) d.TexturaPuesta[i] = uint.MaxValue;
            }
            // Algo de fuera (el anfitrion al componer, subir una textura) cambio lo puesto en OpenGL: se vuelve a poner todo en el siguiente dibujo.
            internal static void EstadoTocado()
            {
                foreach (DispositivoGl d in dispositivos)
                {
                    d.EstadoSucio = true; d.FboPuesto = uint.MaxValue; d.VistaW = d.VistaH = -1; d.MezclaPuesta = -1;
                    d.OrigenPuesto = d.DestinoPuesto = uint.MaxValue;   // (el anfitrion compone con su propia mezcla)
                    for (int i = 0; i < EtapasGl; i++) d.TexturaPuesta[i] = uint.MaxValue;
                    d.Programa = null; d.ProgramaSucio = true;
                }
            }
            private static readonly List<DispositivoGl> dispositivos = new List<DispositivoGl>();

            // ---- Direct3D ----
            internal static IntPtr Direct3DCreate9(uint version)
            {
                if (version != 32) return IntPtr.Zero;   // D3D_SDK_VERSION de Direct3D 9.0c
                return RegistraGl(new Direct3DGl());
            }

            // IDirect3D9::CreateDevice [16]. Hace falta un contexto de OpenGL ES 2 en este hilo (lo abre el anfitrion al empezar).
            internal static int CreateDevice(IntPtr self, uint adaptador, uint tipo, IntPtr ventana, uint banderas, ref PresentParams pp, out IntPtr dispositivo)
            {
                dispositivo = IntPtr.Zero;
                if (ObjetoDe<Direct3DGl>(self) == null || adaptador != 0 || tipo != DEVTYPE_HAL) return D3DERR_INVALIDCALL;
                if (GL.GetString(GL.VERSION) == IntPtr.Zero) return D3DERR_NOTAVAILABLE;   // sin OpenGL en este hilo
                hiloGl = Thread.CurrentThread.ManagedThreadId;
                DispositivoGl d = new DispositivoGl();
                d.Ventana = ventana; d.Hilo = hiloGl;
                PresentParams p = pp;
                if (p.BackBufferWidth == 0 || p.BackBufferHeight == 0) { p.BackBufferWidth = (uint)Escritorio.Pantalla.Width; p.BackBufferHeight = (uint)Escritorio.Pantalla.Height; }
                if (p.BackBufferFormat == FMT_UNKNOWN) p.BackBufferFormat = FMT_X8R8G8B8;
                d.Pp = p; pp = p;
                if (!HazFondo(d)) return D3DERR_OUTOFVIDEOMEMORY;
                uint negra; GL.GenTextures(1, &negra); GL.BindTexture(GL.TEXTURE_2D, negra);
                uint px = 0xFF000000u;   // (0, 0, 0, 1) en cualquier orden
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, 1, 1, 0, GL.RGBA, GL.UNSIGNED_BYTE, &px);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                d.TexturaNegra = negra;
                Interlocked.Add(ref bytesTexturas, 4);
                lock (dispositivos) dispositivos.Add(d);
                dispositivo = RegistraGl(d);
                EstadoTocado();
                return D3D_OK;
            }
            private static bool HazFondo(DispositivoGl d)
            {
                TexturaGl t = NuevaTextura((int)d.Pp.BackBufferWidth, (int)d.Pp.BackBufferHeight, FMT_X8R8G8B8, USAGE_RENDERTARGET, POOL_DEFAULT);
                if (t == null) return false;
                SuperficieGl s = new SuperficieGl(); s.Textura = t; s.DelFondo = true;
                t.Nivel0 = s;
                RegistraGl(s);
                d.Fondo = s;
                AddRefObjeto(s); d.Destino = s;   // el destino tiene su referencia (como SetRenderTarget)
                return true;
            }
            private static DispositivoGl Disp(IntPtr self) { return ObjetoDe<DispositivoGl>(self); }

            // IUnknown::AddRef [1] y Release [2]
            internal static uint AddRef(IntPtr self)
            {
                lock (candadoGl) { ObjetoGl o; if (!objetosGl.TryGetValue(self, out o)) return 0; return (uint)++o.Refs; }
            }
            internal static uint Release(IntPtr self)
            {
                ObjetoGl o;
                lock (candadoGl) { if (!objetosGl.TryGetValue(self, out o)) return 0; }
                DispositivoGl d = o as DispositivoGl;
                if (d != null && o.Refs == 1) lock (dispositivos) dispositivos.Remove(d);
                return ReleaseObjeto(o);
            }

            // IDirect3DDevice9::TestCooperativeLevel [3]: en Rocola OS la tarjeta no se pierde (la pantalla la cede el anfitrion a un video, y
            // entonces lo dice: D3DERR_DEVICELOST, y al volver D3DERR_DEVICENOTRESET, como Windows al volver de otra ventana a pantalla completa).
            internal static int TestCooperativeLevel(IntPtr self)
            {
                if (Disp(self) == null) return D3DERR_INVALIDCALL;
                return D3D_OK;
            }
            // [4] lo que queda para texturas: lo de la tarjeta (VRAM + GTT del controlador radeon; si no se sabe, 512 MB) menos lo que ya ocupan
            // las de la rocola (Direct3D tambien lo descuenta). Redondeado a MB como el de XP.
            internal static uint GetAvailableTextureMem(IntPtr self)
            {
                if (Disp(self) == null) return 0;
                long total = MemoriaTarjeta(), libre = total - BytesTexturas;
                if (libre < 0) libre = 0;
                libre &= ~0xFFFFFL;
                return (uint)Math.Min(libre, 0xFFF00000L);
            }
            private static long memoriaTarjeta = -1;
            private static long MemoriaTarjeta()
            {
                if (memoriaTarjeta >= 0) return memoriaTarjeta;
                long m = MemoriaRadeon();
                if (m <= 0) m = 512L << 20;
                memoriaTarjeta = m;
                return m;
            }
            // DRM_IOCTL_RADEON_GEM_INFO: gart_size, vram_size, vram_visible (lo que el controlador radeon le da a las texturas)
            [DllImport("libc", SetLastError = true)] private static extern int open(string ruta, int banderas);
            [DllImport("libc")] private static extern int close(int fd);
            [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, ulong peticion, void* datos);
            private static long MemoriaRadeon()
            {
                // solo con el controlador radeon (el del A4): el mismo numero de llamada es otra cosa en otros controladores
                try
                {
                    FileSystemInfo c = Directory.ResolveLinkTarget("/sys/class/drm/card0/device/driver", true);
                    if (c == null || c.Name != "radeon") return 0;
                }
                catch { return 0; }
                foreach (string r in new[] { "/dev/dri/renderD128", "/dev/dri/card0" })
                {
                    int fd = -1;
                    try
                    {
                        fd = open(r, 2 | 0x80000);
                        if (fd < 0) continue;
                        ulong* info = stackalloc ulong[3];
                        if (ioctl(fd, 0xC018645CUL, info) == 0 && info[1] > 0) return (long)(info[0] + info[1]);
                    }
                    catch { }
                    finally { if (fd >= 0) close(fd); }
                }
                return 0;
            }
            // [5] EvictManagedResources: en OpenGL no hay copia de cada textura en la memoria del equipo que sacar; no hace nada.
            internal static int EvictManagedResources(IntPtr self) { return Disp(self) != null ? D3D_OK : D3DERR_INVALIDCALL; }

            // [16] Reset: el fondo a la medida nueva y todo el estado como al crear el dispositivo (como Direct3D 9)
            internal static int Reset(IntPtr self, ref PresentParams pp)
            {
                DispositivoGl d = Disp(self);
                if (d == null || d.EnEscena) return D3DERR_INVALIDCALL;
                PresentParams p = pp;
                if (p.BackBufferWidth == 0 || p.BackBufferHeight == 0) { p.BackBufferWidth = d.Pp.BackBufferWidth; p.BackBufferHeight = d.Pp.BackBufferHeight; }
                if (p.BackBufferFormat == FMT_UNKNOWN) p.BackBufferFormat = FMT_X8R8G8B8;
                bool otraMedida = p.BackBufferWidth != d.Pp.BackBufferWidth || p.BackBufferHeight != d.Pp.BackBufferHeight;
                d.Pp = p;
                pp = p;   // (Direct3D 9 pone a cero la medida al volver si falla; aqui no falla)
                if (d.Destino != null && d.Destino != d.Fondo) { ReleaseObjeto(d.Destino); d.Destino = d.Fondo; AddRefObjeto(d.Fondo); }
                if (otraMedida)
                {
                    SuperficieGl viejo = d.Fondo;
                    if (d.Destino == viejo) { d.Destino = null; ReleaseObjeto(viejo); }
                    d.Fondo = null;
                    ReleaseObjeto(viejo);
                    if (!HazFondo(d)) return D3DERR_OUTOFVIDEOMEMORY;
                }
                for (int i = 0; i < d.Etapas.Length; i++) { IntPtr t = d.Etapas[i].Textura; d.Etapas[i] = Etapa.DeFabrica(i); if (t != IntPtr.Zero) ReleaseId(t); }
                if (d.Flujo != IntPtr.Zero) { ReleaseId(d.Flujo); d.Flujo = IntPtr.Zero; }
                d.Mezcla = false; d.Origen = 2; d.Destino2 = 1; d.Recorte = 3; d.Fvf = 0;
                EstadoTocado();
                return D3D_OK;
            }

            // [17] Present: el fondo, al escritorio, que compone y lo pone en la pantalla.
            internal static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr e)
            {
                DispositivoGl d = Disp(self);
                if (d == null || d.EnEscena || d.Fondo == null || d.Fondo.Textura == null) return D3DERR_INVALIDCALL;
                d.Presentaciones++;
                trazando = false;
                Control control = Control.FromHandle(d.Ventana);
                if (control != null)
                {
                    control.TexturaGl = d.Fondo.Textura.Nombre; control.AnchoGl = d.Fondo.W; control.AltoGl = d.Fondo.H;
                    Escritorio.SinEspera = d.Pp.PresentationInterval == PRESENT_INTERVAL_IMMEDIATE;
                    Escritorio.PresentaNativa(control);
                    EstadoTocado();
                }
                else GlMas.Flush();
                BorraPendientes();
                return D3D_OK;
            }

            // [23] CreateTexture (un nivel; A8R8G8B8 o X8R8G8B8)
            internal static int CreateTexture(IntPtr self, uint w, uint h, uint niveles, uint uso, uint formato, uint pool, out IntPtr textura, IntPtr compartida)
            {
                textura = IntPtr.Zero;
                if (Disp(self) == null || w == 0 || h == 0 || niveles > 1 || (formato != FMT_A8R8G8B8 && formato != FMT_X8R8G8B8)) return D3DERR_INVALIDCALL;
                int max = MaxTextura();
                if (w > max || h > max) return D3DERR_INVALIDCALL;
                TexturaGl t = NuevaTextura((int)w, (int)h, formato, uso, pool);
                if (t == null) return D3DERR_OUTOFVIDEOMEMORY;
                textura = RegistraGl(t);
                return D3D_OK;
            }
            private static int maxTextura;
            internal static int MaxTextura()
            {
                if (maxTextura > 0) return maxTextura;
                int v = 0; GL.GetIntegerv(GlMas.MAX_TEXTURE_SIZE, &v);
                maxTextura = v > 0 ? v : 2048;
                return maxTextura;
            }
            private static TexturaGl NuevaTextura(int w, int h, uint formato, uint uso, uint pool)
            {
                while (GlMas.GetError() != 0) { }
                uint n; GL.GenTextures(1, &n);
                GL.BindTexture(GL.TEXTURE_2D, n);
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, w, h, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
                if (GlMas.GetError() == GlMas.OUT_OF_MEMORY) { GL.DeleteTextures(1, &n); EstadoTocado(); return null; }
                TexturaGl t = new TexturaGl();
                t.Nombre = n; t.W = w; t.H = h; t.Formato = formato; t.Uso = uso; t.Pool = pool; t.SinAlfa = formato == FMT_X8R8G8B8;
                t.PuestoS = t.PuestoT = t.PuestoMin = t.PuestoMag = uint.MaxValue;
                Interlocked.Add(ref bytesTexturas, t.Bytes);
                // lo de dentro de una textura nueva no esta definido en Direct3D; aqui, negro (y el alfa en 1 si no tiene alfa)
                EstadoTocado();
                return t;
            }

            // IDirect3DResource9::PreLoad [9]: OpenGL ya la subio al soltarla; no hace nada
            internal static void PreLoad(IntPtr self) { }

            // IDirect3DTexture9::GetLevelDesc [17]
            internal static int GetLevelDesc(IntPtr self, uint nivel, ref SurfaceDesc desc)
            {
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null || nivel != 0) return D3DERR_INVALIDCALL;
                desc.Width = (uint)t.W; desc.Height = (uint)t.H;
                return D3D_OK;
            }
            // IDirect3DTexture9::GetSurfaceLevel [18]: la superficie de la textura (para pintar en ella), con su referencia
            internal static int GetSurfaceLevel(IntPtr self, uint nivel, out IntPtr superficie)
            {
                superficie = IntPtr.Zero;
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null || nivel != 0) return D3DERR_INVALIDCALL;
                if (t.Nivel0 == null)
                {
                    SuperficieGl s = new SuperficieGl(); s.Textura = t;
                    AddRefObjeto(t);   // la superficie tiene viva su textura
                    t.Nivel0 = s;
                    superficie = RegistraGl(s);
                    return D3D_OK;
                }
                AddRefObjeto(t.Nivel0);
                superficie = t.Nivel0.Id;
                return D3D_OK;
            }
            // IDirect3DTexture9::LockRect [19] (toda la textura, o el rectangulo de 'rect')
            internal static int LockRect(IntPtr self, uint nivel, out LockedRect lr, IntPtr rect, uint banderas)
            {
                lr = new LockedRect();
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null) return D3DERR_INVALIDCALL;
                if (rect == IntPtr.Zero) return Bloquea(t, nivel, 0, 0, t.W, t.H, out lr);
                RectI r = *(RectI*)rect;
                return Bloquea(t, nivel, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, out lr);
            }
            internal static int LockRect(IntPtr self, uint nivel, out LockedRect lr, ref RectI rect, uint banderas)
            {
                lr = new LockedRect();
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null) return D3DERR_INVALIDCALL;
                return Bloquea(t, nivel, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, out lr);
            }
            private static int Bloquea(TexturaGl t, uint nivel, int x, int y, int w, int h, out LockedRect lr)
            {
                lr = new LockedRect();
                if (nivel != 0 || t.Bloqueada || t.Pool == POOL_DEFAULT || x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > t.W || y + h > t.H) return D3DERR_INVALIDCALL;
                long bytes = (long)w * h * 4;
                IntPtr m = TomaMemoriaBloqueo(t, bytes);
                t.Bloqueo = m; t.BX = x; t.BY = y; t.BW = w; t.BH = h; t.Bloqueada = true;
                lr.Pitch = w * 4; lr.Bits = m;
                return D3D_OK;
            }
            // IDirect3DTexture9::UnlockRect [20]: lo escrito, a la tarjeta (tal cual: B, G, R, A)
            internal static int UnlockRect(IntPtr self, uint nivel)
            {
                TexturaGl t = ObjetoDe<TexturaGl>(self);
                if (t == null || nivel != 0 || !t.Bloqueada) return D3DERR_INVALIDCALL;
                GL.BindTexture(GL.TEXTURE_2D, t.Nombre);
                GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
                GL.TexSubImage2D(GL.TEXTURE_2D, 0, t.BX, t.BY, t.BW, t.BH, GL.RGBA, GL.UNSIGNED_BYTE, (void*)t.Bloqueo);
                if (volcado != null && t.BW >= 64 && t.BH >= 64) Vuelca(t);
                t.Bloqueada = false;
                SueltaMemoriaBloqueo(t);
                EstadoTocado();
                return D3D_OK;
            }

            // (pruebas) ROCOLA_TRAZA_DIBUJOS=S: cada dibujo del cuadro que empieza a los S segundos (estado, textura y lo que ocupa)
            private static readonly double traza = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_DIBUJOS") != null ? double.Parse(Environment.GetEnvironmentVariable("ROCOLA_TRAZA_DIBUJOS"), System.Globalization.CultureInfo.InvariantCulture) * 1000.0 : 0;
            private static bool trazando, trazado;
            // (pruebas) ROCOLA_SALTA_DIBUJOS=k1,k2: en cada cuadro no se hacen esos dibujos (para buscar cual pinta algo mal)
            private static int dibujoEnCuadro;
            private static readonly int[] salta = Environment.GetEnvironmentVariable("ROCOLA_SALTA_DIBUJOS") == null ? null : Array.ConvertAll(Environment.GetEnvironmentVariable("ROCOLA_SALTA_DIBUJOS").Split(','), int.Parse);
            private static void Traza(DispositivoGl d, byte* v, int paso, int n)
            {
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue; uint c0 = 0;
                for (int i = 0; i < n; i++) { float* f = (float*)(v + (long)i * paso); x0 = Math.Min(x0, f[0]); y0 = Math.Min(y0, f[1]); x1 = Math.Max(x1, f[0]); y1 = Math.Max(y1, f[1]); if (i == 0) c0 = *(uint*)(v + 16); }
                TexturaGl t = ObjetoDe<TexturaGl>(d.Etapas[0].Textura);
                Console.WriteLine("dibujo n=" + n + " caja=(" + x0.ToString("0") + "," + y0.ToString("0") + ")-(" + x1.ToString("0") + "," + y1.ToString("0") + ") color=" + c0.ToString("X8") +
                    " mezcla=" + (d.Mezcla ? d.Origen + "/" + d.Destino2 : "no") + " etapas=" + Clave(d.Etapas) + " tex=" + (t != null ? t.W + "x" + t.H + "#" + t.Nombre : "-") + " filtro=" + d.Etapas[0].FiltroMin + "," + d.Etapas[0].FiltroMag +
                    " destino=" + (d.Destino == d.Fondo ? "fondo" : d.Destino.W + "x" + d.Destino.H));
            }

            // (pruebas) ROCOLA_VOLCADO_TEXTURAS=carpeta: cada textura grande que se sube, en PNG (como la ve Direct3D: sin premultiplicar)
            private static readonly string volcado = Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_TEXTURAS");
            private static int volcadas;
            private static void Vuelca(TexturaGl t)
            {
                try
                {
                    using (SkiaSharp.SKBitmap b = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(t.BW, t.BH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul)))
                    {
                        Buffer.MemoryCopy((void*)t.Bloqueo, (void*)b.GetPixels(), (long)t.BW * t.BH * 4, (long)t.BW * t.BH * 4);
                        using (SkiaSharp.SKData d = b.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                        using (FileStream f = File.Create(Path.Combine(volcado, (++volcadas).ToString("0000") + "_" + (Escritorio.AhoraMs / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s_" + t.W + "x" + t.H + "_n" + t.Nombre + "_en" + t.BX + "," + t.BY + "_" + t.BW + "x" + t.BH + ".png")))
                            d.SaveTo(f);
                    }
                }
                catch { }
            }

            // [26] CreateVertexBuffer, IDirect3DVertexBuffer9::Lock [11] y Unlock [12]
            internal static int CreateVertexBuffer(IntPtr self, uint largo, uint uso, uint fvf, uint pool, out IntPtr vertices, IntPtr compartido)
            {
                vertices = IntPtr.Zero;
                if (Disp(self) == null || largo == 0) return D3DERR_INVALIDCALL;
                VerticesGl v = new VerticesGl(); v.Bytes = (int)largo;
                uint b; GlMas_GenBuffer(&b);
                GL.BindBuffer(GL.ARRAY_BUFFER, b);
                GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)largo, null, GlMas.DYNAMIC_DRAW);
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                v.Vbo = b;
                v.Copia = (IntPtr)NativeMemory.Alloc((nuint)largo);
                vertices = RegistraGl(v);
                return D3D_OK;
            }
            private static void GlMas_GenBuffer(uint* b) { GL.GenBuffers(1, b); }
            internal static int LockVB(IntPtr self, uint desp, uint tam, out IntPtr datos, uint banderas)
            {
                datos = IntPtr.Zero;
                VerticesGl v = ObjetoDe<VerticesGl>(self);
                if (v == null || v.Bloqueado) return D3DERR_INVALIDCALL;
                if (tam == 0) { desp = 0; tam = (uint)v.Bytes; }
                if (desp + tam > (uint)v.Bytes) return D3DERR_INVALIDCALL;
                v.BDesde = (int)desp; v.BBytes = (int)tam; v.Bloqueado = true;
                datos = new IntPtr(v.Copia.ToInt64() + desp);
                return D3D_OK;
            }
            internal static int UnlockVB(IntPtr self)
            {
                VerticesGl v = ObjetoDe<VerticesGl>(self);
                if (v == null || !v.Bloqueado) return D3DERR_INVALIDCALL;
                GL.BindBuffer(GL.ARRAY_BUFFER, v.Vbo);
                GlMas.BufferSubData(GL.ARRAY_BUFFER, (IntPtr)v.BDesde, (IntPtr)v.BBytes, (void*)(v.Copia.ToInt64() + v.BDesde));
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                v.Bloqueado = false;
                return D3D_OK;
            }
            // [100] SetStreamSource (con su referencia, como Direct3D)
            internal static int SetStreamSource(IntPtr self, uint flujo, IntPtr vertices, uint desp, uint paso)
            {
                DispositivoGl d = Disp(self);
                if (d == null || flujo != 0) return D3DERR_INVALIDCALL;
                if (vertices != IntPtr.Zero) { VerticesGl v = ObjetoDe<VerticesGl>(vertices); if (v == null) return D3DERR_INVALIDCALL; AddRefObjeto(v); }
                if (d.Flujo != IntPtr.Zero) ReleaseId(d.Flujo);
                d.Flujo = vertices; d.FlujoDesp = desp; d.FlujoPaso = paso;
                return D3D_OK;
            }

            // [37] SetRenderTarget y [38] GetRenderTarget (con sus referencias)
            internal static int SetRenderTarget(IntPtr self, uint indice, IntPtr superficie)
            {
                DispositivoGl d = Disp(self);
                SuperficieGl s = ObjetoDe<SuperficieGl>(superficie);
                if (d == null || indice != 0 || s == null || s.Textura == null) return D3DERR_INVALIDCALL;
                if (s.Fbo == 0)
                {
                    uint f; GL.GenFramebuffers(1, &f);
                    GL.BindFramebuffer(GL.FRAMEBUFFER, f);
                    GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, s.Textura.Nombre, 0);
                    uint estado = GL.CheckFramebufferStatus(GL.FRAMEBUFFER);
                    if (estado != GL.FRAMEBUFFER_COMPLETE) { GlMas.DeleteFramebuffers(1, &f); EstadoTocado(); return D3DERR_INVALIDCALL; }
                    s.Fbo = f;
                    if (s.Textura.SinAlfa) { GlMas.ColorMask(1, 1, 1, 1); GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT); }   // el alfa en 1
                    EstadoTocado();
                }
                AddRefObjeto(s);
                if (d.Destino != null) ReleaseObjeto(d.Destino);
                d.Destino = s;
                d.FboPuesto = uint.MaxValue;   // y la vista a la medida del destino nuevo (como Direct3D)
                return D3D_OK;
            }
            internal static int GetRenderTarget(IntPtr self, uint indice, out IntPtr superficie)
            {
                superficie = IntPtr.Zero;
                DispositivoGl d = Disp(self);
                if (d == null || indice != 0 || d.Destino == null) return D3DERR_INVALIDCALL;
                AddRefObjeto(d.Destino);
                superficie = d.Destino.Id;
                return D3D_OK;
            }

            // [41] BeginScene y [42] EndScene
            internal static int BeginScene(IntPtr self)
            {
                DispositivoGl d = Disp(self);
                if (d == null || d.EnEscena) return D3DERR_INVALIDCALL;
                BorraPendientes();
                d.EnEscena = true;
                dibujoEnCuadro = 0;
                if (traza > 0 && !trazado && Escritorio.AhoraMs >= traza) { trazando = true; trazado = true; Console.WriteLine("---- cuadro a los " + (Escritorio.AhoraMs / 1000).ToString("0.00") + " s"); }
                return D3D_OK;
            }
            internal static int EndScene(IntPtr self)
            {
                DispositivoGl d = Disp(self);
                if (d == null || !d.EnEscena) return D3DERR_INVALIDCALL;
                d.EnEscena = false;
                return D3D_OK;
            }

            // [57] SetRenderState (lo que usa XP: la mezcla y el recorte; la profundidad y la luz no se usan con vertices ya transformados)
            internal static int SetRenderState(IntPtr self, uint estado, uint valor)
            {
                DispositivoGl d = Disp(self);
                if (d == null) return D3DERR_INVALIDCALL;
                switch (estado)
                {
                    case RS_ALPHABLENDENABLE: d.Mezcla = valor != 0; break;
                    case RS_SRCBLEND: d.Origen = valor; break;
                    case RS_DESTBLEND: d.Destino2 = valor; break;
                    case RS_CULLMODE: d.Recorte = valor; break;
                    default: break;   // ZENABLE (sin profundidad), LIGHTING (sin luz), y lo demas: nada que hacer aqui
                }
                return D3D_OK;
            }
            // [65] SetTexture (con su referencia, como Direct3D: una textura puesta no se borra hasta quitarla)
            internal static int SetTexture(IntPtr self, uint etapa, IntPtr textura)
            {
                DispositivoGl d = Disp(self);
                if (d == null || etapa >= d.Etapas.Length) return D3DERR_INVALIDCALL;
                if (textura != IntPtr.Zero)
                {
                    TexturaGl t = ObjetoDe<TexturaGl>(textura);
                    if (t == null) return D3DERR_INVALIDCALL;
                    AddRefObjeto(t);
                }
                IntPtr antes = d.Etapas[etapa].Textura;
                d.Etapas[etapa].Textura = textura;
                if (antes != IntPtr.Zero) ReleaseId(antes);
                if (etapa < EtapasGl) d.TexturaPuesta[etapa] = uint.MaxValue;
                return D3D_OK;
            }
            // [67] SetTextureStageState
            internal static int SetTextureStageState(IntPtr self, uint etapa, uint tipo, uint valor)
            {
                DispositivoGl d = Disp(self);
                if (d == null || etapa >= d.Etapas.Length) return D3DERR_INVALIDCALL;
                switch (tipo)
                {
                    case TSS_COLOROP: d.Etapas[etapa].ColorOp = valor; break;
                    case TSS_COLORARG1: d.Etapas[etapa].ColorArg1 = valor; break;
                    case TSS_COLORARG2: d.Etapas[etapa].ColorArg2 = valor; break;
                    case TSS_ALPHAOP: d.Etapas[etapa].AlphaOp = valor; break;
                    case TSS_ALPHAARG1: d.Etapas[etapa].AlphaArg1 = valor; break;
                    case TSS_ALPHAARG2: d.Etapas[etapa].AlphaArg2 = valor; break;
                    case TSS_TEXCOORDINDEX: d.Etapas[etapa].IndiceCoordenadas = valor & 0xFFFF; break;
                    default: return D3D_OK;
                }
                d.ProgramaSucio = true;
                return D3D_OK;
            }
            // [69] SetSamplerState
            internal static int SetSamplerState(IntPtr self, uint etapa, uint tipo, uint valor)
            {
                DispositivoGl d = Disp(self);
                if (d == null || etapa >= d.Etapas.Length) return D3DERR_INVALIDCALL;
                switch (tipo)
                {
                    case SAMP_ADDRESSU: d.Etapas[etapa].DireccionU = valor; break;
                    case SAMP_ADDRESSV: d.Etapas[etapa].DireccionV = valor; break;
                    case SAMP_MAGFILTER: d.Etapas[etapa].FiltroMag = valor; break;
                    case SAMP_MINFILTER: d.Etapas[etapa].FiltroMin = valor; break;
                    default: break;
                }
                return D3D_OK;
            }
            // [70] ValidateDevice: lo que pidan las etapas lo hace un programa, en una pasada
            internal static int ValidateDevice(IntPtr self, out uint pasadas)
            {
                pasadas = 1;
                return Disp(self) != null ? D3D_OK : D3DERR_INVALIDCALL;
            }
            // [89] SetFVF
            internal static int SetFVF(IntPtr self, uint fvf)
            {
                DispositivoGl d = Disp(self);
                if (d == null) return D3DERR_INVALIDCALL;
                d.Fvf = fvf;
                return D3D_OK;
            }

            // [83] DrawPrimitiveUP: los vertices del programa, tal cual (OpenGL los copia al dibujar)
            internal static int DrawPrimitiveUP(IntPtr self, uint tipo, uint cuantos, IntPtr datos, uint paso)
            {
                DispositivoGl d = Disp(self);
                if (d == null || !d.EnEscena || datos == IntPtr.Zero || cuantos == 0) return D3DERR_INVALIDCALL;
                uint modo; int n;
                if (!Primitiva(tipo, cuantos, out modo, out n)) return D3DERR_INVALIDCALL;
                if (!Prepara(d)) return D3DERR_INVALIDCALL;
                if (trazando) Traza(d, (byte*)datos, (int)paso, n);
                dibujoEnCuadro++;
                if (salta != null && Array.IndexOf(salta, dibujoEnCuadro) >= 0) return D3D_OK;
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                Atributos(d, (byte*)datos, (int)paso);
                GL.DrawArrays(modo, 0, n);
                return D3D_OK;
            }
            // [81] DrawPrimitive: de los vertices puestos con SetStreamSource, desde el vertice 'desde'
            internal static int DrawPrimitive(IntPtr self, uint tipo, uint desde, uint cuantos)
            {
                DispositivoGl d = Disp(self);
                VerticesGl v = d != null ? ObjetoDe<VerticesGl>(d.Flujo) : null;
                if (d == null || v == null || !d.EnEscena || cuantos == 0 || d.FlujoPaso == 0) return D3DERR_INVALIDCALL;
                uint modo; int n;
                if (!Primitiva(tipo, cuantos, out modo, out n)) return D3DERR_INVALIDCALL;
                if (!Prepara(d)) return D3DERR_INVALIDCALL;
                GL.BindBuffer(GL.ARRAY_BUFFER, v.Vbo);
                Atributos(d, (byte*)0 + d.FlujoDesp + (long)desde * d.FlujoPaso, (int)d.FlujoPaso);
                GL.DrawArrays(modo, 0, n);
                GL.BindBuffer(GL.ARRAY_BUFFER, 0);
                return D3D_OK;
            }
            private static bool Primitiva(uint tipo, uint cuantos, out uint modo, out int n)
            {
                modo = 0; n = 0;
                switch (tipo)
                {
                    case 1: modo = GlMas.POINTS; n = (int)cuantos; return true;
                    case 2: modo = GlMas.LINES; n = (int)cuantos * 2; return true;
                    case 3: modo = GlMas.LINE_STRIP; n = (int)cuantos + 1; return true;
                    case 4: modo = GL.TRIANGLES; n = (int)cuantos * 3; return true;
                    case 5: modo = GlMas.TRIANGLE_STRIP; n = (int)cuantos + 2; return true;
                    case 6: modo = GlMas.TRIANGLE_FAN; n = (int)cuantos + 2; return true;
                    default: return false;
                }
            }

            // ---- poner en OpenGL lo que pidio el programa, antes de cada dibujo (solo lo que cambio) ----
            private static bool Prepara(DispositivoGl d)
            {
                SuperficieGl s = d.Destino;
                if (s == null || s.Textura == null) return false;
                // el destino (y la vista: toda la superficie, como Direct3D al cambiar de destino)
                if (s.Fbo == 0)
                {
                    uint f; GL.GenFramebuffers(1, &f);
                    GL.BindFramebuffer(GL.FRAMEBUFFER, f);
                    GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, s.Textura.Nombre, 0);
                    s.Fbo = f;
                    GlMas.ColorMask(1, 1, 1, 1); GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
                    d.FboPuesto = f; d.VistaW = d.VistaH = -1; d.MascaraSinAlfa = false;
                }
                if (d.FboPuesto != s.Fbo) { GL.BindFramebuffer(GL.FRAMEBUFFER, s.Fbo); d.FboPuesto = s.Fbo; d.VistaW = d.VistaH = -1; }
                if (d.VistaW != s.W || d.VistaH != s.H)
                {
                    GL.Viewport(0, 0, s.W, s.H); d.VistaW = s.W; d.VistaH = s.H;
                    GlMas.ColorMask(1, 1, 1, (byte)(s.Textura.SinAlfa ? 0 : 1)); d.MascaraSinAlfa = s.Textura.SinAlfa;
                }
                if (d.EstadoSucio)
                {
                    GL.Disable(GL.SCISSOR_TEST); GL.Disable(GlMas.DEPTH_TEST); GL.Disable(GlMas.CULL_FACE); GL.Disable(GlMas.DITHER);
                    GlMas.ColorMask(1, 1, 1, (byte)(s.Textura.SinAlfa ? 0 : 1)); d.MascaraSinAlfa = s.Textura.SinAlfa;
                    d.EstadoSucio = false;
                }
                // la mezcla
                int mezcla = d.Mezcla ? 1 : 0;
                if (d.MezclaPuesta != mezcla) { if (d.Mezcla) GL.Enable(GL.BLEND); else GL.Disable(GL.BLEND); d.MezclaPuesta = mezcla; }
                if (d.Mezcla && (d.OrigenPuesto != d.Origen || d.DestinoPuesto != d.Destino2))
                { GL.BlendFunc(FactorMezcla(d.Origen), FactorMezcla(d.Destino2)); d.OrigenPuesto = d.Origen; d.DestinoPuesto = d.Destino2; }
                // el programa de las etapas
                if (d.ProgramaSucio || d.Programa == null)
                {
                    ProgramaGl p = ProgramaDe(d.Etapas);
                    if (p == null) return false;
                    if (d.Programa != p) { GL.UseProgram(p.Nombre); d.Programa = p; }
                    d.ProgramaSucio = false;
                }
                ProgramaGl pr = d.Programa;
                if (pr.W != s.W || pr.H != s.H) { GL.Uniform2f(pr.LugarM, 2f / s.W, 2f / s.H); pr.W = s.W; pr.H = s.H; }
                // las texturas de las etapas que muestrean, con el muestreo de su etapa
                for (int i = 0; i < EtapasGl; i++)
                {
                    if (!pr.Muestrea[i]) continue;
                    TexturaGl t = ObjetoDe<TexturaGl>(d.Etapas[i].Textura);
                    uint n = t != null ? t.Nombre : d.TexturaNegra;
                    GL.ActiveTexture(GL.TEXTURE0 + (uint)i);
                    if (d.TexturaPuesta[i] != n) { GL.BindTexture(GL.TEXTURE_2D, n); d.TexturaPuesta[i] = n; }
                    if (t != null) Muestreo(t, ref d.Etapas[i]);
                }
                GL.ActiveTexture(GL.TEXTURE0);
                return true;
            }
            private static void Muestreo(TexturaGl t, ref Etapa e)
            {
                uint s = Direccion(e.DireccionU), tt = Direccion(e.DireccionV);
                uint mn = e.FiltroMin == 1 ? GlMas.NEAREST : GL.LINEAR, mg = e.FiltroMag == 1 ? GlMas.NEAREST : GL.LINEAR;
                if (t.PuestoS != s) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)s); t.PuestoS = s; }
                if (t.PuestoT != tt) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)tt); t.PuestoT = tt; }
                if (t.PuestoMin != mn) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)mn); t.PuestoMin = mn; }
                if (t.PuestoMag != mg) { GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)mg); t.PuestoMag = mg; }
            }
            private static uint Direccion(uint d)
            {
                switch (d) { case ADDRESS_WRAP: return GlMas.REPEAT; case 2: return GlMas.MIRRORED_REPEAT; default: return GL.CLAMP_TO_EDGE; }   // CLAMP (3) y BORDER (4)
            }
            private static uint FactorMezcla(uint b)
            {
                switch (b)
                {
                    case 1: return GlMas.ZERO; case 2: return GL.ONE; case 3: return GlMas.SRC_COLOR; case 4: return GlMas.ONE_MINUS_SRC_COLOR;
                    case 5: return GL.SRC_ALPHA; case 6: return GL.ONE_MINUS_SRC_ALPHA; case 7: return GlMas.DST_ALPHA; case 8: return GlMas.ONE_MINUS_DST_ALPHA;
                    case 9: return GlMas.DST_COLOR; case 10: return GlMas.ONE_MINUS_DST_COLOR; case 11: return GlMas.SRC_ALPHA_SATURATE;
                    default: return GL.ONE;
                }
            }
            // Los vertices: XYZRHW (4 float), el color (4 bytes), y las coordenadas de textura que diga el FVF (2 float cada una).
            private static void Atributos(DispositivoGl d, byte* base0, int paso)
            {
                uint fvf = d.Fvf != 0 ? d.Fvf : FVF;
                int o = 0;
                if ((fvf & 0x00E) == 0x004) { GL.VertexAttribPointer(0, 4, GL.FLOAT, 0, paso, (IntPtr)(base0 + o)); o += 16; }
                else { GL.VertexAttribPointer(0, 3, GL.FLOAT, 0, paso, (IntPtr)(base0 + o)); o += 12; }   // (XYZ: no lo usa XP)
                GL.EnableVertexAttribArray(0);
                if ((fvf & 0x040) != 0) { GL.VertexAttribPointer(1, 4, GL.UNSIGNED_BYTE, 1, paso, (IntPtr)(base0 + o)); GL.EnableVertexAttribArray(1); o += 4; }
                else { GlMas.DisableVertexAttribArray(1); GlMas.VertexAttrib4f(1, 1, 1, 1, 1); }   // sin color: blanco (como Direct3D)
                if ((fvf & 0x080) != 0) o += 4;   // (especular: no lo usa XP)
                int coords = (int)((fvf >> 8) & 0xF);
                for (int i = 0; i < 2; i++)
                {
                    uint a = (uint)(2 + i);
                    if (i < coords) { GL.VertexAttribPointer(a, 2, GL.FLOAT, 0, paso, (IntPtr)(base0 + o + 8 * i)); GL.EnableVertexAttribArray(a); }
                    else { GlMas.DisableVertexAttribArray(a); GlMas.VertexAttrib4f(a, 0, 0, 0, 1); }
                }
            }

            // ---- los programas: uno por combinacion de etapas ----
            private static readonly Dictionary<string, ProgramaGl> programas = new Dictionary<string, ProgramaGl>();
            private const string FuenteVertices =
                "attribute vec4 p; attribute vec4 c; attribute vec2 t0; attribute vec2 t1;\n" +
                "uniform vec2 m;\n" +
                "varying vec4 vc; varying vec2 vt0; varying vec2 vt1;\n" +
                "void main() {\n" +
                "  float w = 1.0 / p.w;\n" +   // XYZRHW: rhw = 1/w; OpenGL interpola dividiendo por w, como Direct3D
                "  gl_Position = vec4(((p.xy + 0.5) * m - 1.0) * w, 0.0, w);\n" +
                "  vc = c; vt0 = t0; vt1 = t1;\n" +
                "}\n";
            private static string Clave(Etapa[] e)
            {
                StringBuilder sb = new StringBuilder(64);
                for (int i = 0; i < EtapasGl; i++)
                {
                    sb.Append(e[i].ColorOp).Append(',').Append(e[i].ColorArg1).Append(',').Append(e[i].ColorArg2).Append(',')
                      .Append(e[i].AlphaOp).Append(',').Append(e[i].AlphaArg1).Append(',').Append(e[i].AlphaArg2).Append(',')
                      .Append(e[i].IndiceCoordenadas).Append(';');
                    if (e[i].ColorOp == TOP_DISABLE) break;
                }
                return sb.ToString();
            }
            private static ProgramaGl ProgramaDe(Etapa[] e)
            {
                string k = Clave(e);
                ProgramaGl p;
                if (programas.TryGetValue(k, out p)) return p;
                bool[] muestrea = new bool[EtapasGl];
                string fuente = FuenteEtapas(e, muestrea);
                p = Compila(fuente);
                if (p == null) return null;
                p.Muestrea = muestrea;
                programas[k] = p;
                return p;
            }
            // Las etapas de Direct3D 9 en GLSL: cada etapa toma sus dos argumentos (el color del vertice, lo de la etapa anterior o su
            // textura, con «1 - x» y «el alfa en todos» si se piden), hace su operacion con el color y con el alfa por separado y lo deja
            // entre 0 y 1. La primera etapa con COLOROP = DISABLE acaba (y las de despues no cuentan).
            private static string FuenteEtapas(Etapa[] e, bool[] muestrea)
            {
                StringBuilder sb = new StringBuilder(1024);
                sb.Append("#ifdef GL_FRAGMENT_PRECISION_HIGH\nprecision highp float;\n#else\nprecision mediump float;\n#endif\n");
                sb.Append("varying vec4 vc; varying vec2 vt0; varying vec2 vt1;\nuniform sampler2D s0; uniform sampler2D s1;\n");
                sb.Append("void main() {\n  vec4 cur = vc;\n");
                for (int i = 0; i < EtapasGl; i++)
                {
                    Etapa st = e[i];
                    if (st.ColorOp == TOP_DISABLE) break;
                    bool usa = UsaTextura(st);
                    string tex = "tx" + i;
                    if (usa)
                    {
                        muestrea[i] = true;
                        sb.Append("  vec4 ").Append(tex).Append(" = texture2D(s").Append(i).Append(", ").Append((st.IndiceCoordenadas & 1) == 0 ? "vt0" : "vt1").Append(");\n");
                    }
                    string c1 = Arg(st.ColorArg1, tex), c2 = Arg(st.ColorArg2, tex), a1 = Arg(st.AlphaArg1, tex), a2 = Arg(st.AlphaArg2, tex);
                    string col = Op(st.ColorOp, "(" + c1 + ").rgb", "(" + c2 + ").rgb", tex, "cur.rgb");
                    string alf = st.AlphaOp == TOP_DISABLE ? "cur.a" : Op(st.AlphaOp, "(" + a1 + ").a", "(" + a2 + ").a", tex, "cur.a");
                    sb.Append("  cur = clamp(vec4(").Append(col).Append(", ").Append(alf).Append("), 0.0, 1.0);\n");
                }
                sb.Append("  gl_FragColor = cur;\n}\n");
                return sb.ToString();
            }
            private static bool UsaTextura(Etapa st)
            {
                bool c = (st.ColorArg1 & 0xF) == TA_TEXTURE || (st.ColorArg2 & 0xF) == TA_TEXTURE || st.ColorOp == 13 || st.ColorOp == 15;
                bool a = st.AlphaOp != TOP_DISABLE && ((st.AlphaArg1 & 0xF) == TA_TEXTURE || (st.AlphaArg2 & 0xF) == TA_TEXTURE || st.AlphaOp == 13 || st.AlphaOp == 15);
                return c || a;
            }
            private static string Arg(uint a, string tex)
            {
                string b;
                switch (a & 0xF)
                {
                    case TA_DIFFUSE: b = "vc"; break;
                    case TA_CURRENT: b = "cur"; break;   // (en la etapa 0, «cur» es el color del vertice)
                    case TA_TEXTURE: b = tex; break;
                    case 3: b = "vec4(1.0)"; break;      // D3DTA_TFACTOR (de fabrica 0xFFFFFFFF; XP no lo cambia)
                    default: b = "vec4(0.0)"; break;     // especular, temporal, constante: XP no los usa
                }
                if ((a & TA_COMPLEMENT) != 0) b = "(1.0 - " + b + ")";
                if ((a & TA_ALPHAREPLICATE) != 0) b = "vec4((" + b + ").a)";
                return b;
            }
            private static string Op(uint op, string x, string y, string tex, string cur)
            {
                switch (op)
                {
                    case TOP_SELECTARG1: return x;
                    case 3: return y;                                            // SELECTARG2
                    case TOP_MODULATE: return x + " * " + y;
                    case 5: return "2.0 * " + x + " * " + y;                     // MODULATE2X
                    case 6: return "4.0 * " + x + " * " + y;                     // MODULATE4X
                    case 7: return x + " + " + y;                                // ADD
                    case 8: return x + " + " + y + " - 0.5";                     // ADDSIGNED
                    case 9: return "2.0 * (" + x + " + " + y + " - 0.5)";        // ADDSIGNED2X
                    case 10: return x + " - " + y;                               // SUBTRACT
                    case TOP_ADDSMOOTH: return x + " + " + y + " - " + x + " * " + y;
                    case 12: return "mix(" + y + ", " + x + ", vc.a)";           // BLENDDIFFUSEALPHA
                    case 13: return "mix(" + y + ", " + x + ", " + tex + ".a)";  // BLENDTEXTUREALPHA
                    case 15: return x + " + " + y + " * (1.0 - " + tex + ".a)";  // BLENDTEXTUREALPHAPM
                    case 16: return "mix(" + y + ", " + x + ", cur.a)";          // BLENDCURRENTALPHA
                    default: return x;
                }
            }
            private static uint sombreadorVertices;
            private static ProgramaGl Compila(string fuenteFragmentos)
            {
                if (sombreadorVertices == 0) sombreadorVertices = Sombreador(GL.VERTEX_SHADER, FuenteVertices);
                uint f = Sombreador(GL.FRAGMENT_SHADER, fuenteFragmentos);
                if (sombreadorVertices == 0 || f == 0) return null;
                uint p = GL.CreateProgram();
                GL.AttachShader(p, sombreadorVertices); GL.AttachShader(p, f);
                GL.BindAttribLocation(p, 0, "p"); GL.BindAttribLocation(p, 1, "c"); GL.BindAttribLocation(p, 2, "t0"); GL.BindAttribLocation(p, 3, "t1");
                GL.LinkProgram(p);
                int ok; GL.GetProgramiv(p, GL.LINK_STATUS, &ok);
                GlMas.DeleteShader(f);
                if (ok == 0)
                {
                    byte* log = stackalloc byte[1024]; int largo;
                    GlMas.GetProgramInfoLog(p, 1024, &largo, log);
                    Console.Error.WriteLine("Direct3DaGL: el programa no enlaza: " + Marshal.PtrToStringAnsi((IntPtr)log, Math.Max(0, largo)));
                    return null;
                }
                ProgramaGl r = new ProgramaGl();
                r.Nombre = p;
                GL.UseProgram(p);
                r.LugarM = GL.GetUniformLocation(p, "m");
                GL.Uniform1i(GL.GetUniformLocation(p, "s0"), 0); GL.Uniform1i(GL.GetUniformLocation(p, "s1"), 1);
                EstadoTocado();   // (el programa en uso cambio)
                return r;
            }
            private static uint Sombreador(uint tipo, string fuente)
            {
                uint s = GL.CreateShader(tipo);
                byte[] b = Encoding.ASCII.GetBytes(fuente);
                fixed (byte* pb = b)
                {
                    byte* f = pb; int largo = b.Length;
                    GL.ShaderSource(s, 1, &f, &largo);
                }
                GL.CompileShader(s);
                int ok; GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
                if (ok == 0)
                {
                    byte* log = stackalloc byte[1024]; int largo;
                    GL.GetShaderInfoLog(s, 1024, &largo, log);
                    Console.Error.WriteLine("Direct3DaGL: el sombreador no compila: " + Marshal.PtrToStringAnsi((IntPtr)log, Math.Max(0, largo)) + "\n" + fuente);
                    return 0;
                }
                return s;
            }

            // Para las pruebas y el diagnostico: lo pintado en el fondo, en el orden de Direct3D (B, G, R, A por pixel, fila 0 arriba).
            internal static byte[] LeeFondo(IntPtr self, out int w, out int h)
            {
                w = h = 0;
                DispositivoGl d = Disp(self);
                if (d == null || d.Fondo == null || d.Fondo.Textura == null) return null;
                SuperficieGl s = d.Fondo;
                if (s.Fbo == 0) return null;
                w = s.W; h = s.H;
                byte[] px = new byte[w * h * 4];
                GL.BindFramebuffer(GL.FRAMEBUFFER, s.Fbo);
                GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
                fixed (byte* p = px) GL.ReadPixels(0, 0, w, h, GL.RGBA, GL.UNSIGNED_BYTE, p);
                EstadoTocado();
                return px;
            }
        }
    }
}

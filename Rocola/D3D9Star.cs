// ====================================================================================================
//  RENDER POR GPU (Direct3D 9), por interop a d3d9.dll (sin SlimDX). Cielo + estrellas con glow/destello.
// ----------------------------------------------------------------------------------------------------
//  - D3D9 / D3D9Device : interop crudo y un dispositivo que dibuja sky + estrellas texturizadas.
//  - GpuStarfield      : campo estrellado reusable (titileo + latido), teñible por genero.
//  - GpuTestWindow     : ventana de prueba (F12) que lo muestra con FPS.
//  - GpuPanel          : control D3D9 para usar el campo estrellado COMO FONDO de la vista de discos.
//  Todo dentro de #if ROCOLA_GPU. Con el simbolo apagado, el archivo compila vacio.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rocola
{
    // -------- Interop crudo con Direct3D 9 (sin SlimDX) --------
    internal static class D3D9
    {
        [DllImport("d3d9.dll")] public static extern IntPtr Direct3DCreate9(uint sdkVersion);
        // Copia bloque a bloque entre punteros sin pasar por un arreglo administrado (mitad de copias que Marshal.Copy).
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] public static extern void CopyMemory(IntPtr dst, IntPtr src, UIntPtr len);

        [StructLayout(LayoutKind.Sequential)]
        public struct PresentParams
        {
            public uint BackBufferWidth, BackBufferHeight, BackBufferFormat, BackBufferCount;
            public uint MultiSampleType, MultiSampleQuality, SwapEffect;
            public IntPtr DeviceWindow;
            public int Windowed, EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat, Flags, FullScreenRefreshRateInHz, PresentationInterval;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LockedRect { public int Pitch; public IntPtr Bits; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RectI { public int Left, Top, Right, Bottom; }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int LockRegionDel(IntPtr self, uint level, out LockedRect lr, ref RectI rect, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct Vtx { public float X, Y, Z, Rhw; public uint Color; public float U, V; }
        public const uint FVF = 0x004 | 0x040 | 0x100;   // XYZRHW | DIFFUSE | TEX1
        public const int VtxSize = 28;

        public const uint DEVTYPE_HAL = 1;
        public const uint CREATE_HARDWARE_VP = 0x40, CREATE_SOFTWARE_VP = 0x20, CREATE_FPU_PRESERVE = 0x2;
        public const uint FMT_UNKNOWN = 0, FMT_A8R8G8B8 = 21;
        public const uint POOL_MANAGED = 1;
        public const uint SWAPEFFECT_DISCARD = 1;
        public const uint CLEAR_TARGET = 1;
        public const uint PT_TRIANGLELIST = 4;
        public const uint RS_ZENABLE = 7, RS_CULLMODE = 22, RS_LIGHTING = 137;
        public const uint RS_ALPHABLENDENABLE = 27, RS_SRCBLEND = 19, RS_DESTBLEND = 20;
        public const uint BLEND_ONE = 2, BLEND_SRCALPHA = 5, BLEND_INVSRCALPHA = 6;
        public const uint CULL_NONE = 1;
        public const uint TSS_COLOROP = 1, TSS_COLORARG1 = 2, TSS_COLORARG2 = 3, TSS_ALPHAOP = 4, TSS_ALPHAARG1 = 5, TSS_ALPHAARG2 = 6;
        public const uint TOP_SELECTARG1 = 2, TOP_MODULATE = 4, TA_DIFFUSE = 0, TA_TEXTURE = 2;
        public const uint SAMP_MAGFILTER = 5, SAMP_MINFILTER = 6, TEXF_LINEAR = 2;
        public const int E_DEVICELOST = unchecked((int)0x88760868);
        public const int E_DEVICENOTRESET = unchecked((int)0x88760869);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateDeviceDel(IntPtr self, uint adapter, uint devType, IntPtr hwnd, uint behaviorFlags, ref PresentParams pp, out IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int HrSelf(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint UintSelf(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ResetDel(IntPtr self, ref PresentParams pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int PresentDel(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ClearDel(IntPtr self, uint count, IntPtr rects, uint flags, uint color, float z, uint stencil);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetU2Del(IntPtr self, uint a, uint b);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetU1Del(IntPtr self, uint a);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetU3Del(IntPtr self, uint a, uint b, uint c);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetTexDel(IntPtr self, uint stage, IntPtr tex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DrawUpDel(IntPtr self, uint primType, uint primCount, IntPtr data, uint stride);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateVBDel(IntPtr self, uint length, uint usage, uint fvf, uint pool, out IntPtr vb, IntPtr shared);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int LockVBDel(IntPtr self, uint offset, uint size, out IntPtr data, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int StreamDel(IntPtr self, uint stream, IntPtr vb, uint offset, uint stride);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTexDel(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int LockRectDel(IntPtr self, uint level, out LockedRect lr, IntPtr rect, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int UnlockRectDel(IntPtr self, uint level);

        public static T Fn<T>(IntPtr punk, int slot) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(punk);
            IntPtr fn = Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
        }
        public static void Release(IntPtr punk) { try { Fn<UintSelf>(punk, 2)(punk); } catch { } }
    }

    // -------- Dispositivo D3D9 sobre una ventana --------
    internal sealed class D3D9Device
    {
        public uint CubeBaseColor = 0x003C466Eu;
        private IntPtr d3d, dev;
        private D3D9.Vtx[] verticesFijados;
        private GCHandle pinVertices;
        private IntPtr direccionVertices;
        // Solo el ultimo array, durante una escena. Sin busquedas/listas ni arrays clavados entre cuadros.
        private IntPtr FijaVertices(D3D9.Vtx[] datos, out GCHandle temporal)
        {
            temporal = new GCHandle();
            if (inScene)
            {
                if (object.ReferenceEquals(verticesFijados, datos)) return direccionVertices;
                SueltaPinesVertices();
                long bytes = datos.LongLength * D3D9.VtxSize;
                if (bytes <= 6 * 1024 * 1024)
                {
                    pinVertices = GCHandle.Alloc(datos, GCHandleType.Pinned);
                    direccionVertices = pinVertices.AddrOfPinnedObject();
                    verticesFijados = datos; return direccionVertices;
                }
            }
            temporal = GCHandle.Alloc(datos, GCHandleType.Pinned);
            return temporal.AddrOfPinnedObject();
        }
        private void SueltaPinesVertices()
        {
            if (pinVertices.IsAllocated) pinVertices.Free();
            verticesFijados = null; direccionVertices = IntPtr.Zero;
        }
        // Anillo dinamico acotado. NOOVERWRITE nunca pisa vertices aun usados por la GPU.
        private IntPtr verticesGpu;
        private int verticesCap, verticesPos;
        private bool verticesSinSoporte;
        private bool verticesSoftware;
        // Calibracion sobre dibujos reales, sin dibujos extra ni limite de FPS/CPU.
        private int verticesModo, verticesPruebaTris, verticesPruebas;
        private long verticesUpTicks, verticesVbTicks;
        private D3D9.CreateVBDel creaVertices;
        private D3D9.LockVBDel bloqueaVertices;
        private D3D9.HrSelf sueltaVertices;
        private D3D9.StreamDel flujoVertices;
        private D3D9.SetU3Del dibujaVertices;
        private D3D9.PresentParams pp;
        private bool lost, resetPending, inScene;

        private D3D9.PresentDel present; private D3D9.HrSelf begin, end;
        private D3D9.SetU2Del setRS; private D3D9.SetU1Del setFVF; private D3D9.SetU3Del setTSS, setSamp;
        private D3D9.SetTexDel setTex; private D3D9.DrawUpDel drawUp; private D3D9.HrSelf testCoop;
        private D3D9.ResetDel reset; private D3D9.CreateTexDel createTex;

        public bool Ok { get { return dev != IntPtr.Zero; } }
        public uint EstimatedTextureMemory
        {
            get
            {
                if (dev == IntPtr.Zero) return 0;
                try { return D3D9.Fn<D3D9.UintSelf>(dev, 4)(dev); }
                catch { return 0; }
            }
        }

        public D3D9Device(IntPtr hwnd, int w, int h)
        {
            try
            {
                d3d = D3D9.Direct3DCreate9(32);
                if (d3d == IntPtr.Zero) return;
                pp = MakePP(hwnd, w, h);
                D3D9.CreateDeviceDel create = D3D9.Fn<D3D9.CreateDeviceDel>(d3d, 16);
                IntPtr dv = IntPtr.Zero;
                int hr = create(d3d, 0, D3D9.DEVTYPE_HAL, hwnd, D3D9.CREATE_HARDWARE_VP | D3D9.CREATE_FPU_PRESERVE, ref pp, out dv);
                if (hr < 0)
                {
                    // Aunque un driver correcto devuelve NULL al fallar, no se deja escapar una referencia
                    // defectuosa antes de intentar el camino por software.
                    if (dv != IntPtr.Zero) D3D9.Release(dv);
                    dv = IntPtr.Zero;
                    verticesSoftware = true;
                    hr = create(d3d, 0, D3D9.DEVTYPE_HAL, hwnd, D3D9.CREATE_SOFTWARE_VP | D3D9.CREATE_FPU_PRESERVE, ref pp, out dv);
                }
                if (hr < 0 || dv == IntPtr.Zero)
                {
                    if (dv != IntPtr.Zero) D3D9.Release(dv);
                    D3D9.Release(d3d); d3d = IntPtr.Zero;
                    return;
                }
                dev = dv;
                present = D3D9.Fn<D3D9.PresentDel>(dev, 17);
                begin = D3D9.Fn<D3D9.HrSelf>(dev, 41);
                end = D3D9.Fn<D3D9.HrSelf>(dev, 42);
                setRS = D3D9.Fn<D3D9.SetU2Del>(dev, 57);
                setFVF = D3D9.Fn<D3D9.SetU1Del>(dev, 89);
                setTSS = D3D9.Fn<D3D9.SetU3Del>(dev, 67);
                setSamp = D3D9.Fn<D3D9.SetU3Del>(dev, 69);
                setTex = D3D9.Fn<D3D9.SetTexDel>(dev, 65);
                drawUp = D3D9.Fn<D3D9.DrawUpDel>(dev, 83);
                testCoop = D3D9.Fn<D3D9.HrSelf>(dev, 3);
                reset = D3D9.Fn<D3D9.ResetDel>(dev, 16);
                createTex = D3D9.Fn<D3D9.CreateTexDel>(dev, 23);
                if (!InitStates()) Dispose();
            }
            catch
            {
                // Direct3D puede no estar instalado o un controlador puede exponer una tabla COM incompleta.
                // El fondo por CPU se encargara del dibujo; las referencias creadas hasta aqui se liberan.
                Dispose();
            }
        }

        public int BufAncho { get { return (int)pp.BackBufferWidth; } }
        public int BufAlto { get { return (int)pp.BackBufferHeight; } }
        // Quien esta dibujando, para el diagnostico y para las pruebas. El gemelo de Linux contesta aqui el
        // nombre de la tarjeta y la version de OpenGL, que alli hace falta para saber si esta dibujando la
        // tarjeta de verdad o el procesador haciendose pasar por ella.
        public string Quien { get { return "Direct3D 9"; } }

        // REHACE EL BUFFER DE DIBUJO al tamano nuevo de la ventana.
        //
        // Sin esto, al cambiar la resolucion de Windows con la rocola ABIERTA el buffer se quedaba del
        // tamano VIEJO. Dos consecuencias, y las dos se veian:
        //   - Cada cuadro habia que ESTIRARLO del tamano viejo al de la ventana, que es el camino lento del
        //     driver: de ahi los tirones al hojear y el congelon al dejar pulsado el boton de pagina.
        //   - Y todo lo que caia fuera del tamano viejo se RECORTABA, asi que a resoluciones altas se perdian
        //     trozos de la pantalla por la derecha y por abajo.
        // Arrancando ya en esa resolucion no pasaba nada, porque entonces el buffer se creaba del tamano bueno.
        //
        // Las texturas viven en el monton MANEJADO (POOL_MANAGED): sobreviven al reseteo solas, no hay que
        // volver a subir ni una. Lo que SI se pierde son los estados del dispositivo, y por eso se rehacen.
        public bool Redimensiona(int w, int h)
        {
            if (dev == IntPtr.Zero || w < 8 || h < 8) return false;
            bool mismo = pp.BackBufferWidth == (uint)w && pp.BackBufferHeight == (uint)h;
            if (mismo && !resetPending) return true;
            if (!mismo) { pp.BackBufferWidth = (uint)w; pp.BackBufferHeight = (uint)h; }
            resetPending = true;
            if (inScene) { lost = true; return false; }
            try
            {
                SueltaPinesVertices();
                LiberaVertices();
                if (reset(dev, ref pp) < 0)
                {
                    // Se conserva el tamano PEDIDO. BeginFrame lo reintentara cuando el dispositivo vuelva
                    // a estar listo; restaurar el tamano anterior recreaba despues el buffer equivocado.
                    lost = true;
                    return false;
                }
            }
            catch { lost = true; return false; }
            if (!InitStates()) { lost = true; return false; }
            resetPending = false;
            lost = false;
            return true;
        }

        private static D3D9.PresentParams MakePP(IntPtr hwnd, int w, int h)
        {
            D3D9.PresentParams p = new D3D9.PresentParams();
            p.BackBufferWidth = (uint)Math.Max(1, w);
            p.BackBufferHeight = (uint)Math.Max(1, h);
            p.BackBufferFormat = D3D9.FMT_UNKNOWN;
            p.BackBufferCount = 1;
            p.SwapEffect = D3D9.SWAPEFFECT_DISCARD;
            p.DeviceWindow = hwnd;
            p.Windowed = 1;
            p.PresentationInterval = 0;
            return p;
        }

        // ESTADO ACTUAL del dispositivo. Cambiar un estado en Direct3D 9 es una llamada al driver, y una pantalla
        // de la rocola son decenas de dibujos SEGUIDOS con el mismo estado (toda la rejilla de discos se pinta
        // igual: textura modulada + mezcla normal). Sin esto, cada quad repetia 9 cambios identicos. Anotando lo
        // que ya esta puesto, solo se manda al driver lo que de verdad cambia.
        private int stTss, stBlend;                  // 0 = sin fijar, 1 = diffuse / 2 = modulate ; 1 = sin mezcla, 2 = aditiva, 3 = normal
        private IntPtr stTex = new IntPtr(-1);       // -1 = sin fijar (IntPtr.Zero SI es un valor valido: sin textura)

        private bool InitStates()
        {
            stTss = 0; stBlend = 0; stTex = new IntPtr(-1);   // tras crear o RESETEAR el dispositivo todo vuelve a su valor por defecto
            if (dev == IntPtr.Zero) return false;
            try
            {
                if (setRS(dev, D3D9.RS_ZENABLE, 0) < 0) return false;
                if (setRS(dev, D3D9.RS_LIGHTING, 0) < 0) return false;
                if (setRS(dev, D3D9.RS_CULLMODE, D3D9.CULL_NONE) < 0) return false;
                if (setFVF(dev, D3D9.FVF) < 0) return false;
                if (setSamp(dev, 0, D3D9.SAMP_MAGFILTER, D3D9.TEXF_LINEAR) < 0) return false;
                if (setSamp(dev, 0, D3D9.SAMP_MINFILTER, D3D9.TEXF_LINEAR) < 0) return false;
                return true;
            }
            catch { return false; }
        }

        private void EstadoFallido()
        {
            lost = true;
            stTss = 0; stBlend = 0; stTex = new IntPtr(-1);
        }

        private void Tex(IntPtr t)
        {
            if (dev == IntPtr.Zero || lost || stTex == t) return;
            try
            {
                if (setTex(dev, 0, t) < 0) { EstadoFallido(); return; }
                stTex = t;
            }
            catch { EstadoFallido(); }
        }
        private void Blend(int modo)   // 1 = sin mezcla, 2 = aditiva (brillos), 3 = normal (encima)
        {
            if (dev == IntPtr.Zero || lost || stBlend == modo) return;
            try
            {
                if (modo == 1)
                {
                    if (setRS(dev, D3D9.RS_ALPHABLENDENABLE, 0) < 0) { EstadoFallido(); return; }
                }
                else
                {
                    if (stBlend <= 1 && setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1) < 0) { EstadoFallido(); return; }
                    if (setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_SRCALPHA) < 0) { EstadoFallido(); return; }
                    if (setRS(dev, D3D9.RS_DESTBLEND, modo == 2 ? D3D9.BLEND_ONE : D3D9.BLEND_INVSRCALPHA) < 0) { EstadoFallido(); return; }
                }
                stBlend = modo;
            }
            catch { EstadoFallido(); }
        }

        public IntPtr CreateTextureFromBitmap(Bitmap bmp)
        {
            if (dev == IntPtr.Zero || bmp == null) return IntPtr.Zero;
            IntPtr tex = IntPtr.Zero;
            try
            {
                int w = bmp.Width, h = bmp.Height;
                if (w < 1 || h < 1) return IntPtr.Zero;
                if (createTex(dev, (uint)w, (uint)h, 1, 0, D3D9.FMT_A8R8G8B8, D3D9.POOL_MANAGED, out tex, IntPtr.Zero) < 0 || tex == IntPtr.Zero)
                {
                    if (tex != IntPtr.Zero) D3D9.Release(tex);
                    return IntPtr.Zero;
                }
                if (!SubeBitmap(tex, bmp, w, h))
                {
                    D3D9.Release(tex);
                    return IntPtr.Zero;
                }
                return tex;
            }
            catch
            {
                if (tex != IntPtr.Zero) D3D9.Release(tex);
                return IntPtr.Zero;
            }
        }

        // Bloquea y rellena una textura garantizando que tanto GDI+ como Direct3D quedan desbloqueados aun
        // cuando el bitmap este corrupto, el driver rechace el recurso o falle una copia nativa.
        private static bool SubeBitmap(IntPtr tex, Bitmap bmp, int w, int h)
        {
            D3D9.UnlockRectDel unlockR = null;
            D3D9.LockedRect lr = new D3D9.LockedRect();
            BitmapData bd = null;
            bool texLocked = false, ok = false;
            try
            {
                D3D9.LockRectDel lockR = D3D9.Fn<D3D9.LockRectDel>(tex, 19);
                unlockR = D3D9.Fn<D3D9.UnlockRectDel>(tex, 20);
                if (lockR(tex, 0, out lr, IntPtr.Zero, 0) < 0) return false;
                texLocked = true;

                bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int rowBytes = checked(w * 4);
                if (lr.Bits == IntPtr.Zero || lr.Pitch < rowBytes || bd.Scan0 == IntPtr.Zero || Math.Abs((long)bd.Stride) < rowBytes)
                    return false;
                // Agrupa filas contiguas sin padding en bloques de hasta 64 KiB. Una copia gigante
                // resulto mas lenta en la medicion; los bloques reducen interop sin ese retroceso.
                // Stride negativo o pitch distinto conservan la copia exacta por filas.
                if (lr.Pitch == rowBytes && bd.Stride == rowBytes)
                {
                    long total = (long)rowBytes * h;
                    for (long offset = 0; offset < total; )
                    {
                        uint bytes = (uint)Math.Min(65536L, total - offset);
                        D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + offset), new IntPtr(bd.Scan0.ToInt64() + offset), (UIntPtr)bytes);
                        offset += bytes;
                    }
                }
                else
                    for (int y = 0; y < h; y++)
                        D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), (UIntPtr)(uint)rowBytes);
                ok = true;
            }
            catch { ok = false; }
            finally
            {
                if (bd != null)
                {
                    try { bmp.UnlockBits(bd); }
                    catch { ok = false; }
                }
                if (texLocked && unlockR != null)
                {
                    try { if (unlockR(tex, 0) < 0) ok = false; }
                    catch { ok = false; }
                }
            }
            return ok;
        }

        // Inicia el cuadro (maneja perdida de dispositivo). Devuelve true si se puede dibujar.
        public bool BeginFrame()
        {
            if (!inScene) SueltaPinesVertices(); // tambien un cuadro anterior abandonado por perdida
            if (dev == IntPtr.Zero) return false;
            try
            {
                if (lost || resetPending)
                {
                    int tc = testCoop(dev);
                    if (tc == D3D9.E_DEVICELOST) return false;
                    if (tc < 0 && tc != D3D9.E_DEVICENOTRESET) return false;
                    if (tc == D3D9.E_DEVICENOTRESET || resetPending)
                    {
                        LiberaVertices();
                        if (reset(dev, ref pp) < 0 || !InitStates()) return false;
                        resetPending = false;
                        lost = false;
                    }
                    else
                    {
                        // Algunos controladores saltan directamente de DEVICELOST a S_OK. Se rehacen los
                        // estados porque no sabemos cuanto de ellos conservaron durante la perdida.
                        if (!InitStates()) return false;
                        lost = false;
                    }
                }
                // La textura apuntada se olvida en CADA cuadro: entre cuadro y cuadro se recrean texturas (portadas,
                // UI) y una nueva puede caer en la MISMA direccion que una recien liberada. Los otros estados son
                // numeros, no punteros, asi que esos si se conservan. Cuesta un cambio de textura por cuadro.
                stTex = new IntPtr(-1);
                // SIN BORRAR LA PANTALLA (rendimiento, 24-09-2026). Lo primero que se dibuja en cada cuadro es el
                // cielo (GpuStarfield.Begin): dos cuadrilateros opacos, sin mezcla, que tapan todos los pixeles. Borrar
                // antes era escribir la pantalla entera para nada, una vez por cuadro. El buffer se descarta al
                // presentar (SWAPEFFECT_DISCARD): no hay nada viejo que respetar.
                if (begin(dev) < 0) { lost = true; return false; }
                inScene = true;
                return true;
            }
            catch { lost = true; return false; }
        }

        public bool EndFrame()
        {
            if (dev == IntPtr.Zero || !inScene) { SueltaPinesVertices(); return false; }
            inScene = false;
            try
            {
                if (end(dev) < 0) { lost = true; return false; }
                // No se cuenta una escena incompleta ni una presentacion rechazada.
                if (lost) return false;
                if (present(dev, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) < 0) { lost = true; return false; }
                return true;
            }
            catch { lost = true; return false; }
            finally { SueltaPinesVertices(); }
        }

        private void TssDiffuse()   // usa el color del vertice (sin textura)
        {
            if (dev == IntPtr.Zero || lost || stTss == 1) return;
            try
            {
                if (setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_SELECTARG1) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_DIFFUSE) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_SELECTARG1) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_DIFFUSE) < 0)
                { EstadoFallido(); return; }
                stTss = 1;
            }
            catch { EstadoFallido(); }
        }
        private void TssModulate()  // textura * color del vertice
        {
            if (dev == IntPtr.Zero || lost || stTss == 2) return;
            try
            {
                if (setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_MODULATE) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_TEXTURE) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_COLORARG2, D3D9.TA_DIFFUSE) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_MODULATE) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_TEXTURE) < 0 ||
                    setTSS(dev, 0, D3D9.TSS_ALPHAARG2, D3D9.TA_DIFFUSE) < 0)
                { EstadoFallido(); return; }
                stTss = 2;
            }
            catch { EstadoFallido(); }
        }

        // Cielo (color del vertice, sin mezcla) + estrellas (textura de glow, aditivo).
        // Cielo (color del vertice, sin mezcla). 2 quads apilados = degradado de 3 paradas (4 triangulos).
        public void DrawSky(D3D9.Vtx[] sky)
        {
            if (dev == IntPtr.Zero || lost) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(1);
            Draw(sky, 4);
        }
        // Estrellas (textura de glow, aditivo). Se puede llamar 2 veces (patron anterior + nuevo) en el entrecruzado.
        public void DrawStars(D3D9.Vtx[] stars, int starTris, IntPtr glowTex)
        {
            if (dev == IntPtr.Zero || lost || starTris <= 0 || glowTex == IntPtr.Zero) return;
            Tex(glowTex); TssModulate(); Blend(2);
            Draw(stars, starTris);
        }

        // Estelas de estrellas fugaces: cuadrangulos BLANCOS con mezcla ADITIVA y SIN textura (color del vertice).
        public void DrawShootStreaks(D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || lost || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(2);
            Draw(v, tris);
        }

        // Malla PLANA: triangulos SIN textura, color en los vertices y mezcla NORMAL (encima, no sumada).
        // Es lo que pinta el fondo reactivo: un relleno de color que la tarjeta degrada sola entre vertice y
        // vertice. Al no sumar luz nunca se lava hacia el blanco, que es lo que manchaba con los halos.
        public void DrawMesh(D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || lost || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(3);
            Draw(v, tris);
        }

        // La misma malla plana pero SUMANDO luz en vez de tapar. Un filo encendido se lee como neon
        // justamente por esto: lo que hay debajo se sigue viendo y encima se le suma el brillo, asi que
        // el nucleo satura a claro y el resplandor de alrededor tine sin borrar. Tapando no hay neon.
        public void DrawMeshAdd(D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || lost || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(2);
            Draw(v, tris);
        }

        // Un cuadro texturizado (2 triangulos) con mezcla NORMAL (encima): para los cubos, etiquetas, etc.
        public void DrawTexQuad(IntPtr tex, D3D9.Vtx[] quad)
        {
            if (dev == IntPtr.Zero || lost || tex == IntPtr.Zero) return;
            Tex(tex); TssModulate(); Blend(3);
            Draw(quad, 2);
        }

        // Una tanda de triangulos con mezcla NORMAL (encima), con textura o sin ella. Es la pieza con la
        // que el archivo del dibujo arma los cubos: le pasa cara por cara y el cache de estados hace que
        // repetir la peticion no cueste nada.
        public void DrawQuads(IntPtr tex, D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || lost || tris <= 0) return;
            if (tex != IntPtr.Zero) { Tex(tex); TssModulate(); } else { Tex(IntPtr.Zero); TssDiffuse(); }
            Blend(3);
            Draw(v, tris);
        }

        // Buffers reutilizables del cubo (evita asignar por cuadro). Solo se usan desde el hilo de render.
        private readonly D3D9.Vtx[] cubeFaceV = new D3D9.Vtx[6];
        private readonly D3D9.Vtx[] cubeBatchV = new D3D9.Vtx[24];
        private readonly double[] cbx = new double[4];
        private readonly double[] cbz = new double[4];
        private readonly int[] cbo = new int[4];

        // Dibuja un cubo GIRANDO (mismo aspecto que Cube.Draw del render por CPU) directo en GPU desde la
        // textura de la portada: el giro es solo geometria (4 caras como cuadrangulos, ordenadas por
        // profundidad y oscurecidas igual). Si tex es nulo usa un relleno azulado (portada aun cargando).
        // alpha < 1 permite FUNDIR una portada sobre otra (discos TOP) en dos pasadas.
        public void DrawCube(IntPtr tex, float uMax, float vMax, float ax, float ay, float aw, float ah, double angle, float alpha, double tilt)
        {
            uint BlankCubeColor = CubeBaseColor;
            if (dev == IntPtr.Zero || alpha <= 0.001f || aw < 1f || ah < 1f) return;
            double cx = ax + aw / 2.0; float y0 = ay; float Hh = ah; double R = aw / 2.0; double DEG = Math.PI / 180.0;
            double cyc = y0 + Hh / 2.0, ct = Math.Cos(tilt), stt = Math.Sin(tilt);   // bamboleo: rota el cubo entero alrededor de su centro (igual que RotateTransform en CPU)
            for (int k = 0; k < 4; k++) cbx[k] = cx + R * Math.Cos(angle + 45 * DEG + k * 90 * DEG);
            for (int k = 0; k < 4; k++) cbz[k] = Math.Sin(angle + 90 * DEG + k * 90 * DEG);
            cbo[0] = 0; cbo[1] = 1; cbo[2] = 2; cbo[3] = 3;
            for (int i = 0; i < 4; i++)                          // ordena las caras de atras hacia adelante
                for (int j = 0; j < 3 - i; j++)
                    if (cbz[cbo[j]] > cbz[cbo[j + 1]]) { int t = cbo[j]; cbo[j] = cbo[j + 1]; cbo[j + 1] = t; }
            bool textured = tex != IntPtr.Zero;
            if (textured) { Tex(tex); TssModulate(); } else { Tex(IntPtr.Zero); TssDiffuse(); }
            Blend(3);
            uint av = (uint)(alpha * 255f + 0.5f); if (av > 255) av = 255;
            int cubeCount = 0;
            for (int oi = 0; oi < 4; oi++)
            {
                int k = cbo[oi];
                if (cbz[k] <= 0.0) continue;                     // cara trasera: no se ve
                float left = (float)Math.Round(Math.Min(cbx[k], cbx[(k + 1) % 4]));
                float right = (float)Math.Round(Math.Max(cbx[k], cbx[(k + 1) % 4]));
                if (right - left < 1f) continue;
                int darken = (int)((1.0 - cbz[k]) * 130); if (darken < 0) darken = 0; if (darken > 255) darken = 255;
                uint g = (uint)(255 - darken);
                uint col = textured
                    ? (av << 24) | (g << 16) | (g << 8) | g                                   // textura * gris (oscurecido por profundidad)
                    : (av << 24) | ((((BlankCubeColor >> 16) & 255u) * g / 255u) << 16) | ((((BlankCubeColor >> 8) & 255u) * g / 255u) << 8) | ((BlankCubeColor & 255u) * g / 255u);
                CubeFace(cubeFaceV, left, y0, right, y0 + Hh, cx, cyc, ct, stt, uMax, vMax, col);
                Array.Copy(cubeFaceV, 0, cubeBatchV, cubeCount, 6);
                cubeCount += 6;
            }
            // Misma secuencia de triangulos y transparencias, una sola llamada por cubo.
            if (cubeCount > 0) Draw(cubeBatchV, cubeCount / 3);
        }
        // Construye la cara (2 triangulos) con sus 4 esquinas ROTADAS el bamboleo alrededor de (cx,cy).
        private static void CubeFace(D3D9.Vtx[] v, float x0, float y0, float x1, float y1,
            double cx, double cy, double ct, double st, float uM, float vM, uint c)
        {
            float tlx, tly, trx, tryy, brx, bry, blx, bly;
            RotP(x0, y0, cx, cy, ct, st, out tlx, out tly); RotP(x1, y0, cx, cy, ct, st, out trx, out tryy);
            RotP(x1, y1, cx, cy, ct, st, out brx, out bry); RotP(x0, y1, cx, cy, ct, st, out blx, out bly);
            CFV(ref v[0], tlx, tly, c, 0, 0); CFV(ref v[1], trx, tryy, c, uM, 0); CFV(ref v[2], brx, bry, c, uM, vM);
            CFV(ref v[3], tlx, tly, c, 0, 0); CFV(ref v[4], brx, bry, c, uM, vM); CFV(ref v[5], blx, bly, c, 0, vM);
        }
        private static void RotP(float x, float y, double cx, double cy, double ct, double st, out float ox, out float oy)
        {
            double dx = x - cx, dy = y - cy;
            ox = (float)(cx + dx * ct - dy * st); oy = (float)(cy + dx * st + dy * ct);
        }
        private static void CFV(ref D3D9.Vtx v, float x, float y, uint c, float u, float vv) { v.X = x - 0.5f; v.Y = y - 0.5f; v.Z = 0f; v.Rhw = 1f; v.Color = c; v.U = u; v.V = vv; }

        private static int Pow2(int n)
        {
            if (n < 1 || n > 0x40000000) return 0;
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        // Crea una textura de tamano POTENCIA DE DOS (compatible con GPU vieja) y sube el bitmap en su
        // esquina superior izquierda. Devuelve las coordenadas de textura maximas usadas (uMax, vMax).
        public IntPtr CreateTexturePow2(Bitmap bmp, out float uMax, out float vMax)
        {
            uMax = 1f; vMax = 1f;
            if (dev == IntPtr.Zero || bmp == null) return IntPtr.Zero;
            IntPtr tex = IntPtr.Zero;
            try
            {
                int w = bmp.Width, h = bmp.Height, tw = Pow2(w), th = Pow2(h);
                if (tw == 0 || th == 0) return IntPtr.Zero;
                if (createTex(dev, (uint)tw, (uint)th, 1, 0, D3D9.FMT_A8R8G8B8, D3D9.POOL_MANAGED, out tex, IntPtr.Zero) < 0 || tex == IntPtr.Zero)
                {
                    if (tex != IntPtr.Zero) D3D9.Release(tex);
                    return IntPtr.Zero;
                }
                if (!SubeBitmap(tex, bmp, w, h))
                {
                    D3D9.Release(tex);
                    return IntPtr.Zero;
                }
                uMax = (float)w / tw; vMax = (float)h / th;
                return tex;
            }
            catch
            {
                if (tex != IntPtr.Zero) D3D9.Release(tex);
                return IntPtr.Zero;
            }
        }

        // Re-sube el contenido a una textura pow2 YA existente (mismo tamano de bitmap) en vez de crear una
        // nueva: evita el churn de crear/destruir una textura a pantalla completa en cada reconstruccion de UI.
        // El llamador garantiza el mismo tamano. Devuelve false si no pudo bloquearla (entonces se recrea).
        // Actualiza solo la banda modificada. El rectangulo D3D marca tambien
        // la region sucia de la textura MANAGED, sin resubir todo el panel.
        public bool UpdateTextureRegion(IntPtr tex, Bitmap bmp, Rectangle area)
        {
            if (tex == IntPtr.Zero || bmp == null || area.Width <= 0 || area.Height <= 0 ||
                !new Rectangle(0, 0, bmp.Width, bmp.Height).Contains(area)) return false;
            BitmapData data = null; bool locked = false, ok = false;
            D3D9.UnlockRectDel unlock = null;
            try
            {
                D3D9.RectI rect = new D3D9.RectI();
                rect.Left = area.Left; rect.Top = area.Top; rect.Right = area.Right; rect.Bottom = area.Bottom;
                D3D9.LockedRect dest;
                D3D9.LockRegionDel take = D3D9.Fn<D3D9.LockRegionDel>(tex, 19);
                unlock = D3D9.Fn<D3D9.UnlockRectDel>(tex, 20);
                if (take(tex, 0, out dest, ref rect, 0) < 0) return false;
                locked = true;
                data = bmp.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int bytes = checked(area.Width * 4);
                if (dest.Bits == IntPtr.Zero || dest.Pitch < bytes || Math.Abs((long)data.Stride) < bytes) return false;
                for (int y = 0; y < area.Height; y++)
                    D3D9.CopyMemory(new IntPtr(dest.Bits.ToInt64() + (long)y * dest.Pitch),
                        new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), (UIntPtr)(uint)bytes);
                ok = true;
            }
            catch { ok = false; }
            finally
            {
                if (data != null) { try { bmp.UnlockBits(data); } catch { ok = false; } }
                if (locked && unlock != null) { try { if (unlock(tex, 0) < 0) ok = false; } catch { ok = false; } }
            }
            return ok;
        }
        public bool UpdateTexturePow2(IntPtr tex, Bitmap bmp, out float uMax, out float vMax)
        {
            uMax = 1f; vMax = 1f;
            if (dev == IntPtr.Zero || tex == IntPtr.Zero || bmp == null) return false;
            try
            {
                int w = bmp.Width, h = bmp.Height, tw = Pow2(w), th = Pow2(h);
                if (tw == 0 || th == 0 || !SubeBitmap(tex, bmp, w, h)) return false;
                uMax = (float)w / tw; vMax = (float)h / th;
                return true;
            }
            catch { return false; }
        }

        private void Draw(D3D9.Vtx[] verts, int tris)
        {
            if (dev == IntPtr.Zero || lost || verts == null || tris <= 0 || tris > verts.Length / 3) return;
            GCHandle gh;
            IntPtr datos = FijaVertices(verts, out gh);
            bool prueba = false, usaBuffer = false;
            long inicio = 0;
            try
            {
                // Los dibujos pequenos conservan UP: para un quad es mas barato que tres llamadas COM.
                if (tris >= 64 && !verticesSinSoporte)
                {
                    if (verticesPruebaTris == 0) verticesPruebaTris = tris;
                    prueba = verticesModo == 0 && tris == verticesPruebaTris;
                    usaBuffer = verticesModo == 2 || (prueba && (verticesPruebas & 1) != 0);
                    if (prueba) inicio = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (usaBuffer && DibujaVertices(tris, datos)) return;
                }
                if (drawUp(dev, D3D9.PT_TRIANGLELIST, (uint)tris, datos, D3D9.VtxSize) < 0)
                    EstadoFallido();
            }
            catch { EstadoFallido(); }
            finally
            {
                if (prueba && !lost && !verticesSinSoporte)
                {
                    long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - inicio;
                    // Las primeras parejas ceban recursos/JIT; no se comparan.
                    if (verticesPruebas >= 4) { if (usaBuffer) verticesVbTicks += ticks; else verticesUpTicks += ticks; }
                    if (++verticesPruebas >= 44)
                    { verticesModo = verticesVbTicks < verticesUpTicks * .95 ? 2 : 1;
                      if (verticesModo == 1) LiberaVertices(); }
                }
                if (gh.IsAllocated) gh.Free();
            }
        }
        private bool DibujaVertices(int tris, IntPtr datos)
        {
            int n = tris * 3;
            if (verticesSinSoporte || n > 196608) return false;
            try
            {
                if (verticesGpu == IntPtr.Zero || n > verticesCap)
                {
                    LiberaVertices();
                    if (creaVertices == null)
                    {
                        creaVertices = D3D9.Fn<D3D9.CreateVBDel>(dev, 26);
                        flujoVertices = D3D9.Fn<D3D9.StreamDel>(dev, 100);
                        dibujaVertices = D3D9.Fn<D3D9.SetU3Del>(dev, 81);
                    }
                    verticesCap = Math.Max(49152, n);
                    // DYNAMIC | WRITEONLY, POOL_DEFAULT; liberado ANTES de cada Reset.
                    if (creaVertices(dev, (uint)(verticesCap * D3D9.VtxSize), verticesSoftware ? 0x218u : 0x208u, D3D9.FVF, 0, out verticesGpu, IntPtr.Zero) < 0
                        || verticesGpu == IntPtr.Zero)
                    { LiberaVertices(); verticesSinSoporte = true; return false; }
                    bloqueaVertices = D3D9.Fn<D3D9.LockVBDel>(verticesGpu, 11);
                    sueltaVertices = D3D9.Fn<D3D9.HrSelf>(verticesGpu, 12);
                }
                if (verticesPos + n > verticesCap) verticesPos = 0;
                IntPtr destino;
                if (bloqueaVertices(verticesGpu, (uint)(verticesPos * D3D9.VtxSize), (uint)(n * D3D9.VtxSize),
                    out destino, verticesPos == 0 ? 0x2000u : 0x1000u) < 0)
                { LiberaVertices(); verticesSinSoporte = true; return false; }
                if (destino == IntPtr.Zero)
                { sueltaVertices(verticesGpu); LiberaVertices(); verticesSinSoporte = true; return false; }
                bool copiado = false;
                try { D3D9.CopyMemory(destino, datos, new UIntPtr((uint)(n * D3D9.VtxSize))); copiado = true; }
                finally { if (sueltaVertices(verticesGpu) < 0) copiado = false; }
                if (!copiado) { EstadoFallido(); return true; }
                if (flujoVertices(dev, 0, verticesGpu, 0, D3D9.VtxSize) < 0 ||
                    dibujaVertices(dev, D3D9.PT_TRIANGLELIST, (uint)verticesPos, (uint)tris) < 0)
                { EstadoFallido(); return true; }
                verticesPos += n;
                return true;
            }
            catch { LiberaVertices(); verticesSinSoporte = true; EstadoFallido(); return true; }
        }
        private void LiberaVertices()
        {
            IntPtr v = verticesGpu; verticesGpu = IntPtr.Zero; verticesPos = verticesCap = 0;
            if (v != IntPtr.Zero)
            {
                try { if (flujoVertices != null && dev != IntPtr.Zero) flujoVertices(dev, 0, IntPtr.Zero, 0, D3D9.VtxSize); } catch { }
                D3D9.Release(v);
            }
            bloqueaVertices = null; sueltaVertices = null;
        }

        public void Dispose()
        {
            SueltaPinesVertices();
            LiberaVertices();
            lost = true;
            resetPending = false;
            inScene = false;
            IntPtr dv = dev; dev = IntPtr.Zero;
            IntPtr d = d3d; d3d = IntPtr.Zero;
            if (dv != IntPtr.Zero) D3D9.Release(dv);
            if (d != IntPtr.Zero) D3D9.Release(d);
        }
    }

    // -------- Campo estrellado reusable (titileo + latido), teñible por genero --------
    internal sealed partial class GpuStarfield
    {
        public bool FondoNatural;
        private bool glowNatural;
        private bool atlasRequested;
        private double atlasRetryMs;
        private Bitmap naturalStarAtlas, legacyStarAtlas;
        internal Func<Bitmap, IntPtr> CreateStarAtlasForTest = null;
        private D3D9Device gpu; private IntPtr glowTex; private IntPtr uiGlowTex;   // glow de estrella (con chispa) / glow radial LISO para el destello de UI
        private float[] sx, sy, sphase, sspeed, ssize; private int[] skind, sforma; private uint[] scol; private int nStars;
        private D3D9.Vtx[] skyV = new D3D9.Vtx[12]; private D3D9.Vtx[] starV;   // cielo = 2 quads apilados (3 paradas de color)
        private uint skyTop = 0xFF06081A, skyMid = 0xFF101C3A, skyBot = 0xFF1A2C5A;
        // RELOJ DEL CAMPO DE ESTRELLAS. Igual que el de la aurora: en un bar la rocola no se apaga nunca, y
        // "los segundos que llevo encendida" guardados en un float se quedan sin decimales a los pocos dias
        // (el titileo empezaria a ir a saltos). Aqui el tiempo se lleva sumado en un double, que no se queda
        // corto ni en anos, y el paso de cada cuadro se saca por RESTA del RELOJ FINO (ver GpuPanel.AhoraMs):
        // el reloj de Windows solo contesta de 15.6 en 15.6 ms, y con el tope de cuadros puesto -un cuadro cada
        // ~15 ms- el titileo avanzaria a tirones, un cuadro si y el siguiente no.
        private double campoT; private double campoFinoAnt; private bool campoRelojOk;
        private int seed0;                                   // semilla base (el patron de cada minuto = seed0 + minuto*paso)
        private int curMinute = int.MinValue;                // minuto del patron actual (cambia cada 60s)
        // ENTRECRUZADO del patron: al cambiar de minuto se conserva el patron anterior y se desvanece mientras
        // el nuevo aparece, en vez de atenuar TODO el cielo. Asi el fondo nunca se queda sin estrellas.
        private float[] px, py, pphase, pspeed, psize; private int[] pkind, pforma; private uint[] pcol; private int pnStars;
        private D3D9.Vtx[] starV2;                            // vertices del patron ANTERIOR durante el entrecruzado
        private int transStart; private bool transing;
        private const int TransMs = 1300;                    // duracion del entrecruzado
        // POR DEBAJO DE ESTO NO SE VE NADA. El brillo de una estrella acaba siendo un numero de 0 a 255, asi
        // que un brillo de dos milesimas sale redondeado a cero: transparente del todo. Armar y mandar a la
        // tarjeta un juego entero de estrellas para que dibuje nada es trabajo tirado.
        private const float FadeMinimo = 0.002f;             // medio nivel de 255
        // Estrellas FUGACES: en CPU se podian pero costaban; en GPU son un par de cuadrangulos aditivos.
        // Cada una con tamaño, color, velocidad y largo de estela distintos.
        private struct Shoot { public bool On; public float X, Y, VX, VY, Life, MaxLife, Wdt, Tail; public uint Col; }
        private readonly Shoot[] shoots = new Shoot[2];
        private D3D9.Vtx[] shootV = new D3D9.Vtx[2 * 12];    // 2 fugaces x (estela 6 + cabeza 6) vertices
        private int nextShoot; private double shootFinoAnt;   // la CITA de la proxima fugaz en ms de Windows (son segundos); su avance, con el reloj fino
        private readonly Random srnd = new Random();
        private static readonly uint[] shootPal = { 0x00FFFFFF, 0x00FFFFFF, 0x00CFE6FF, 0x00FFE6A0, 0x00A0FFE0, 0x00FFC0E0 };   // blanco (x2), azul, dorado, aqua, rosa
        // DERIVA con la musica: al sonar, el campo entero se desliza LENTO en diagonal (abajo-derecha, como las
        // fugaces) y se envuelve por los bordes (bucle infinito). Es baratisimo: solo un desplazamiento + modulo
        // por estrella dentro del bucle que ya existe; ni asignaciones ni draws extra.
        private double driftFinoAnt;                         // ultimo Begin (reloj fino) para medir dt de la deriva
        private float driftEnv;                              // envolvente 0..1: sube al sonar, baja al parar (arranque/paro suave)
        private const float DriftVX = 18f, DriftVY = 9f;     // px/seg (derecha, abajo): lento, diagonal suave 2:1

        // ...Y NO TODAS A LA VEZ: DE AQUI SALE LA PROFUNDIDAD.
        //
        // Hasta ahora las mil cuatrocientas estrellas se deslizaban con el MISMO desplazamiento, y por eso el
        // cielo se movia como un papel pintado que alguien arrastra y no como un espacio con fondo. Lo que el
        // ojo lee como distancia -sin que nadie se lo explique- es que lo lejano se mueva MENOS que lo cercano.
        //
        // Cada estrella ya lleva apuntado su tamano, asi que el polvo -las mas pequenas, las que hacen de
        // fondo- deriva a un tercio de lo que derivan las grandes, y las medianas a poco mas de la mitad.
        //
        // Y SON TRES CUENTAS ACUMULADAS, no una multiplicada por tres factores. Tiene que ser asi: el
        // desplazamiento se guarda ya envuelto entre cero y el ancho de la pantalla, o sea que al dar la
        // vuelta salta del ancho a cero. Multiplicar DESPUES ese salto por un tercio movería el polvo de
        // golpe un tercio de pantalla cada vez que la cuenta grande da la vuelta: un tiron cada dos minutos
        // de musica. Acumulando cada tamano por su lado, la vuelta de cada uno es exacta y sus estrellas
        // reaparecen por el otro borde sin que se note nada.
        private static readonly float[] DerivaKind = { 0.33f, 0.58f, 1f };       // polvo, mediana, grande
        private readonly float[] driftX = new float[3], driftY = new float[3];   // acumulado (px) de cada tamano, envuelto con w/h

        public bool Ok { get { return gpu != null && gpu.Ok; } }
        public uint EstimatedTextureMemory { get { return gpu != null ? gpu.EstimatedTextureMemory : 0; } }
        public int Count { get { return nStars; } }
        private float dim = 1f;   // atenuacion del campo (estrellas + fugaces) mientras se navega con musica
        public void SetDim(float d) { dim = d < 0f ? 0f : (d > 1f ? 1f : d); }

        // ===================== LAS PERILLAS DEL CIELO (Ctrl+F3 -> Imagen y rendimiento) =====================
        // Tres cosas del cielo se pueden bajar cuando un equipo no da para todo. Estan juntas y aqui arriba a
        // proposito: repartidas por el archivo, nadie sabria de un vistazo que se puede aflojar.
        //
        // Y van por NIVEL, no por si/no: entre "fugaces si" y "fugaces no" hay sitio para "menos fugaces", y
        // eso es media rocola que no hay que afear para ganar los mismos cuadros.
        //
        //   densidad  cuantas estrellas hay, de 0.1 a 1 (1 = las de siempre)
        //   fugaces   2 = dos a la vez cada 7-16 s;  1 = una cada 15-30 s;  0 = ninguna
        //   relevo    2 = patron nuevo cada minuto;  1 = cada tres minutos;  0 = nunca
        //
        // Con las tres en alto (1, 2, 2) el cielo sale EXACTAMENTE como salia antes de que existieran:
        // ninguna cuenta cambia. Solo al bajarlas se nota, que es de lo que se trata.
        internal const int Alto = 2, Medio = 1, Bajo = 0;
        private float densidad = 1f; private int fugaces = Alto, relevo = Alto;
        public void SetEfectos(float dens, int fug, int rel, int w, int h)
        {
            if (dens < 0.1f) dens = 0.1f; else if (dens > 1f) dens = 1f;
            fugaces = fug; relevo = rel;
            if (Math.Abs(dens - densidad) < 0.001f) return;   // lo de siempre: esto se llama en CADA cuadro
            densidad = dens;
            if (!Ok || w < 8 || h < 8) return;
            // Cambiar cuantas estrellas hay obliga a rehacer el campo: viven en arreglos de ese tamano.
            int minuto = (curMinute != int.MinValue) ? curMinute : 0;
            BuildStars(w, h, unchecked(seed0 + minuto * 40503));
            transing = false; px = null;   // el entrecruzado en curso era del campo viejo: se corta
        }

        public GpuStarfield(IntPtr hwnd, int w, int h, int seed)
        {
            gpu = new D3D9Device(hwnd, w, h);
            if (!gpu.Ok) return;
            seed0 = seed;
            using (Bitmap tex = BakeStarTexture(64)) glowTex = gpu.CreateTextureFromBitmap(tex);
            using (Bitmap g2 = BakeRadialGlow(64)) uiGlowTex = gpu.CreateTextureFromBitmap(g2);   // glow LISO (sin chispa) para el destello del boton
            BuildStars(w, h, seed);
        }

        // Ajusta el dispositivo al tamano nuevo de la ventana y REHACE el campo de estrellas, que vive en
        // pixeles: sin rehacerlo se quedaria encogido en una esquina de la pantalla nueva.
        public void Redimensiona(int w, int h)
        {
            if (!Ok || w < 8 || h < 8) return;
            if (gpu.BufAncho == w && gpu.BufAlto == h) return;
            if (!gpu.Redimensiona(w, h)) return;
            int minuto = (curMinute != int.MinValue) ? curMinute : 0;
            BuildStars(w, h, unchecked(seed0 + minuto * 40503));
            transing = false; px = null;   // el entrecruzado en curso era del tamano viejo: se corta
        }

        public void SetSky(uint top, uint mid, uint bot) { skyTop = top; skyMid = mid; skyBot = bot; }
        public IntPtr CreateOverlay(Bitmap bmp, out float uMax, out float vMax) { return gpu.CreateTexturePow2(bmp, out uMax, out vMax); }
        public bool UpdateOverlay(IntPtr tex, Bitmap bmp, out float uMax, out float vMax) { return gpu.UpdateTexturePow2(tex, bmp, out uMax, out vMax); }
        public bool UpdateOverlayRegion(IntPtr tex, Bitmap bmp, Rectangle area) { return gpu.UpdateTextureRegion(tex, bmp, area); }
        public void DrawCube(IntPtr tex, float uMax, float vMax, float x, float y, float w, float h, double angle, float alpha, double tilt)
        { if (Ok) gpu.DrawCube(tex, uMax, vMax, x, y, w, h, angle, alpha, tilt); }

        // Un halo (glow radial ADITIVO) centrado en (cx,cy) de radio r, teñido/atenuado por 'argb'. Reusa la textura
        // de glow de las estrellas. Sirve para el DESTELLO del boton pulsado del buscador: lo anima la GPU (alfa que
        // baja) sin re-hornear ni re-subir la textura de UI -> el destello se desvanece suave y el tecleo va fluido.
        // Un halo es un circulo de luz que se apaga hacia afuera, y su textura vale CERO fuera de ese circulo.
        // Dibujarlo en un cuadrado significa pintar cuatro esquinas que no aportan NADA (el 17% de los pixeles).
        // Se dibuja entonces en un OCTAGONO: el mismo cuadrado con las esquinas cortadas justo donde la luz ya
        // vale cero. El resultado en pantalla es identico al pixel, y se pinta 17% menos. El octagono cabe
        // entero dentro del cuadrado, asi que las coordenadas de textura siguen dentro de 0..1.
        private const int HaloVtx = 18;                 // 6 triangulos en abanico
        private const float HaloC = 0.41421356f;        // tan(22.5): donde se corta cada esquina
        private static readonly float[] HaloX = { 1f, HaloC, -HaloC, -1f, -1f, -HaloC, HaloC, 1f };
        private static readonly float[] HaloY = { -HaloC, -1f, -1f, -HaloC, HaloC, 1f, 1f, HaloC };
        private readonly D3D9.Vtx[] glowV = new D3D9.Vtx[HaloVtx];
        public void DrawGlow(float cx, float cy, float r, uint argb)
        {
            // El halo de la interfaz tiene su propia textura LISA. Antes, si esa no se creaba, se caia a la de
            // las estrellas; ahora esa es un ATLAS de cuatro formas y caer ahi pintaria un cuarto de atlas con
            // tres siluetas dentro. Si no hay textura lisa, no se dibuja el halo y ya: se pierde un adorno, no
            // se ensucia la pantalla. (Las dos se crean con el mismo codigo seguido: si una sale, la otra sale.)
            IntPtr t = uiGlowTex;
            if (!Ok || t == IntPtr.Zero || r < 1f) return;
            Halo(glowV, 0, cx, cy, r, argb);
            gpu.DrawStars(glowV, HaloVtx / 3, t);   // mismo camino aditivo que las estrellas
        }
        // Acabado natural: luz proporcional a la portada, no un disco enorme
        // detras de ella. La misma textura lisa, 18 vertices y un unico envio.
        public void DrawGlowElipse(float cx, float cy, float rx, float ry, uint argb)
        {
            IntPtr t = uiGlowTex;
            if (!Ok || t == IntPtr.Zero || !(rx >= 1f) || !(ry >= 1f) || (argb >> 24) == 0) return;
            Halo(glowV, 0, cx, cy, rx, ry, argb);
            gpu.DrawStars(glowV, HaloVtx / 3, t);
        }
        // Escribe UN halo (6 triangulos en abanico) a partir del hueco i del arreglo que se le pase.
        private static void Halo(D3D9.Vtx[] a, int i, float cx, float cy, float r, uint c) { Halo(a, i, cx, cy, r, r, c); }
        // Con radios distintos a lo ancho y a lo alto sale una ELIPSE. Una luz de horizonte tiene que ser ancha
        // y baja: si se dibuja redonda, sube tanto como se extiende y acaba brillando a media pantalla.
        private static void Halo(D3D9.Vtx[] a, int i, float cx, float cy, float rx, float ry, uint c)
        {
            for (int k = 0; k < 6; k++)                  // abanico desde el vertice 0: (0,k+1,k+2)
            {
                Punto(a, i + k * 3 + 0, cx, cy, rx, ry, 0, c);
                Punto(a, i + k * 3 + 1, cx, cy, rx, ry, k + 1, c);
                Punto(a, i + k * 3 + 2, cx, cy, rx, ry, k + 2, c);
            }
        }
        private static void Punto(D3D9.Vtx[] a, int i, float cx, float cy, float rx, float ry, int k, uint c)
        {
            float dx = HaloX[k], dy = HaloY[k];
            GV(a, i, cx + dx * rx, cy + dy * ry, c, 0.5f + 0.5f * dx, 0.5f + 0.5f * dy);
        }
        private static void GV(D3D9.Vtx[] a, int i, float x, float y, uint c, float u, float v)
        { a[i].X = x - 0.5f; a[i].Y = y - 0.5f; a[i].Z = 0f; a[i].Rhw = 1f; a[i].Color = c; a[i].U = u; a[i].V = v; }

        // MUCHOS halos de UNA sola llamada de dibujo. La aurora son decenas de puntos por cuadro y en Direct3D 9
        // lo caro no es el relleno sino la LLAMADA: uno por uno serian decenas de DrawPrimitive, cada una con su
        // cambio de textura y de estado de mezcla (~10 llamadas al driver por halo). Aqui se acumulan todos en el
        // mismo arreglo y se mandan juntos: mismo dibujo exacto, una sola llamada.
        private D3D9.Vtx[] glowBatch;
        private int glowBatchN;
        public void GlowBatchBegin(int maxHalos)
        {
            if (glowBatch == null || glowBatch.Length < maxHalos * HaloVtx) glowBatch = new D3D9.Vtx[maxHalos * HaloVtx];
            glowBatchN = 0;
        }
        public void GlowBatchAdd(float cx, float cy, float r, uint argb) { GlowBatchAdd(cx, cy, r, r, argb); }
        public void GlowBatchAdd(float cx, float cy, float rx, float ry, uint argb)
        {
            if (rx < 1f || ry < 1f || glowBatch == null || (glowBatchN + 1) * HaloVtx > glowBatch.Length) return;
            Halo(glowBatch, glowBatchN * HaloVtx, cx, cy, rx, ry, argb);
            glowBatchN++;
        }
        public void GlowBatchEnd()
        {
            IntPtr t = uiGlowTex;   // ver DrawGlow: sin textura lisa no se dibuja halo (no se cae al atlas)
            if (Ok && t != IntPtr.Zero && glowBatchN > 0) gpu.DrawStars(glowBatch, glowBatchN * (HaloVtx / 3), t);
            glowBatchN = 0;
        }

        public void Render(int w, int h, float pulse) { if (Begin(w, h, pulse, pulse)) End(); }   // solo cielo+estrellas (ventana de prueba)

        // Lo que se dibuja ENTRE el cielo y las estrellas (la aurora). Asi las estrellas quedan SIEMPRE encima:
        // con la aurora tan presente como esta ahora, dibujarla despues las tapaba en media pantalla y parecia
        // que el campo estrellado se hubiera apagado. Por detras de una aurora de verdad tambien se ven.
        internal delegate void Dibuja();
        public Dibuja TrasCielo;
        public Dibuja TrasEstrellas;   // lo que va DELANTE de las estrellas y detras de las fugaces (la luna)

        // Inicia el cuadro: dibuja el cielo + estrellas. Luego se pueden apilar texturas con DrawQuad y cerrar
        // con End. 'pulse' ya viene atenuado (es brillo); 'pulseCrudo' NO, porque la deriva del campo es
        // MOVIMIENTO: atenuar la pantalla no puede congelarlo.
        public bool Begin(int w, int h, float pulse, float pulseCrudo)
        {
            if (!Ok) return false;
            ActualizaAtlasNatural();   // solo al alternar estilo, nunca una subida por cuadro
            if (!gpu.BeginFrame()) return false;
            int nowMs = Environment.TickCount;      // para las esperas LARGAS (entrecruzado de patron, proxima fugaz)
            double finoMs = GpuPanel.AhoraMs();     // y el reloj FINO para el paso de cada cuadro (ver mas abajo)
            if (campoRelojOk)
            {
                double dtc = (finoMs - campoFinoAnt) / 1000.0;
                if (!(dtc > 0.0)) dtc = 0.0; else if (dtc > 0.25) dtc = 0.25;   // acota atascos (y atrapa NaN)
                campoT += dtc;
            }
            campoFinoAnt = finoMs; campoRelojOk = true;
            double t = campoT;

            // DERIVA DIAGONAL con la musica: al sonar (pulse>0) el campo entero se desliza LENTO abajo-derecha
            // y se envuelve por los bordes. Envolvente suave para arrancar/parar sin saltos. Coste ~nulo.
            float ddt = (driftFinoAnt <= 0.0) ? 0f : (float)((finoMs - driftFinoAnt) / 1000.0);
            driftFinoAnt = finoMs;
            if (ddt < 0f) ddt = 0f; else if (ddt > 0.1f) ddt = 0.1f;   // acota saltos (p.ej. tras un atasco)
            float dTgt = (pulseCrudo > 0.02f) ? 1f : 0f;
            driftEnv += (dTgt - driftEnv) * (dTgt > driftEnv ? Math.Min(1f, ddt * 2.5f) : Math.Min(1f, ddt * 0.6f));   // ataque medio, caida lenta
            float avX = driftEnv * DriftVX * ddt, avY = driftEnv * DriftVY * ddt;   // env>=0 -> solo crece
            for (int k = 0; k < 3; k++)
            {
                driftX[k] += avX * DerivaKind[k]; if (w > 0) driftX[k] %= w;   // el modulo lo deja en [0,w)
                driftY[k] += avY * DerivaKind[k]; if (h > 0) driftY[k] %= h;
            }

            // PATRON que cambia cada minuto, con ENTRECRUZADO: se conserva el patron anterior y se desvanece
            // mientras el nuevo aparece (el cielo nunca se queda sin estrellas). Por GPU re-sortear es barato.
            // PERILLA DE Ctrl+F3: cada cuanto se renueva el patron de estrellas. Lo caro no es sortearlas, es
            // el segundo y pico de entrecruzado en que se dibujan DOS campos a la vez; espaciarlo a tres
            // minutos deja ese gasto en la tercera parte sin que el cielo se quede clavado para siempre.
            double cadaSeg = (relevo == Alto) ? 60.0 : 180.0;
            int minute = (relevo == Bajo) ? curMinute : (int)(t / cadaSeg);
            if (minute != curMinute)
            {
                if (curMinute != int.MinValue)
                {
                    px = sx; py = sy; pphase = sphase; pspeed = sspeed; psize = ssize; pkind = skind; pforma = sforma; pcol = scol; pnStars = nStars;   // guarda el actual como ANTERIOR
                    BuildStars(w, h, unchecked(seed0 + minute * 40503));     // construye el NUEVO (asigna arreglos nuevos; no pisa los anteriores)
                    if (starV2 == null || starV2.Length < pnStars * 6) starV2 = new D3D9.Vtx[pnStars * 6];
                    transing = true; transStart = nowMs;
                }
                curMinute = minute;
            }
            float tr = transing ? (nowMs - transStart) / (float)TransMs : 1f;
            if (tr >= 1f) { transing = false; tr = 1f; }
            if (FondoNatural) tr = RelevoNatural(tr);

            // Cielo de 3 PARADAS como en CPU: negro arriba -> azul noche -> color del genero abajo (antes 2
            // paradas: el genero teñia casi toda la pantalla). Dos quads apilados en la parada media.
            //
            // DONDE esta esa parada lo dice la perilla "cuanto negro" de Ctrl+F3, y el numero vive en Form1
            // -fuera de este archivo- porque la pared horneada tiene que usar exactamente el mismo: si cada
            // una llevara el suyo, la maquina del bar y la de repuesto pintarian cielos distintos.
            float midY = h * Form1.CorteDelCielo;
            SetQuad(skyV, 0, 0, 0, w, midY, skyTop, skyTop, skyMid, skyMid);
            SetQuad(skyV, 6, 0, midY, w, h, skyMid, skyMid, skyBot, skyBot);
            // Cielo azul/negro original; la paleta movil pertenece a la aurora.
            gpu.DrawSky(skyV);
            if (TrasCielo != null) TrasCielo();   // la aurora va aqui: entre el cielo y las estrellas
            // EL PATRON ANTERIOR, DESVANECIENDOSE. Mientras dura el cruce -1.3 segundos, una vez por
            // minuto- se arman y se dibujan DOS juegos de estrellas en vez de uno, o sea el doble de trabajo:
            // hasta 2800 estrellas por cuadro.
            //
            // Y la ultima parte de ese segundo y pico no se ve: el brillo de las estrellas es un numero de 0
            // a 255, asi que en cuanto lo que queda del patron viejo baja de medio nivel, todas sus estrellas
            // salen ya transparentes del todo. Armarlas y mandarlas a la tarjeta para que dibuje nada es
            // trabajo tirado, y justo del caro. Se corta ahi: no se ve, porque no habia nada que ver.
            float fadeAnt = (1f - tr) * dim;
            if (transing && px != null && fadeAnt > FadeMinimo)
            {
                BuildStarVerts(starV2, px, py, pphase, pspeed, psize, pkind, pforma, pcol, pnStars, t, fadeAnt, pulse, driftX, driftY, w, h);
                gpu.DrawStars(starV2, pnStars * 2, glowTex);
            }
            BuildStarVerts(starV, sx, sy, sphase, sspeed, ssize, skind, sforma, scol, nStars, t, (transing ? tr : 1f) * dim, pulse, driftX, driftY, w, h);   // patron ACTUAL (x atenuacion al navegar)
            gpu.DrawStars(starV, nStars * 2, glowTex);
            if (TrasEstrellas != null) TrasEstrellas();   // la luna va aqui: esta muchisimo mas cerca que las estrellas
            UpdateAndDrawShoots(w, h, nowMs, finoMs);
            return true;
        }

        // Construye los vertices de un campo de estrellas (titileo + latido) con un factor de brillo global
        // (para el entrecruzado del patron). Reutilizable por el patron actual y por el anterior.
        // EL TITILEO DE UNA ESTRELLA, POR TABLA Y NO POR SENO.
        //
        // Esto se hace una vez POR ESTRELLA Y POR CUADRO: hasta mil cuatrocientas estrellas a ciento ochenta
        // cuadros por segundo son 252.000 senos por segundo, y el DOBLE durante el segundo en que se cruzan
        // los dos patrones de estrellas, una vez por minuto. Medido: 24 milisegundos de cada mil.
        //
        // Un seno no se puede calcular; se aproxima. La tabla es otra aproximacion, y aqui la pregunta buena
        // no es cual es "mas exacta" sino si la diferencia SE VE: lo que sale de aqui acaba siendo el alfa de
        // un pixel, o sea un numero de 0 a 255. Con 1024 entradas y mezclando las dos vecinas, el error queda
        // por debajo de una centesima de nivel. La bateria 'fondocoste' lo comprueba en 20.000 puntos.
        private const int TitN = 1024;
        private static readonly float[] TitTab = HazTitTab();
        private static float[] HazTitTab()
        {
            float[] t = new float[TitN + 1];
            for (int i = 0; i <= TitN; i++)
                t[i] = (float)(0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(2.0 * Math.PI * i / TitN)));
            return t;   // la ultima repite la primera: asi el mezclado del final no se sale de la tabla
        }
        // RECIBE UN DOUBLE, Y NO ES UN CAPRICHO. Justo aqui arriba esta escrito por que el reloj del campo
        // de estrellas se lleva en double: en un bar la rocola no se apaga nunca, y "los segundos que llevo
        // encendida" guardados en un float se quedan sin decimales a los pocos dias -a los 25 dias, un float
        // ya solo distingue de 0.25 en 0.25 radianes- y el titileo empezaria a ir a saltos.
        //
        // La primera version de esta tabla recibia un float, o sea que tiraba el double a la basura JUSTO
        // ANTES de usarlo y volvia a meter el fallo que ese comentario avisa de no meter. La vuelta a la
        // rueda se hace aqui en double, y lo que se pasa a float es el resto, que siempre esta entre 0 y 1.
        internal static float Titileo(double x)
        {
            double u = x * (1.0 / 6.28318530717958648);
            u -= Math.Floor(u);
            float f = (float)(u * TitN);
            int i = (int)f;
            if (i >= TitN) i = TitN - 1;
            float r = f - i;
            return TitTab[i] + (TitTab[i + 1] - TitTab[i]) * r;   // entre las dos vecinas: sin escalones
        }

        // EL AIRE QUE HAY DELANTE DE CADA ESTRELLA, TAMBIEN POR TABLA.
        //
        // Cuanto se apaga una estrella, cuanto enrojece y cuanto mas titila segun a que altura de la pantalla
        // cae. La CURVA vive en Form1 -la comparten las dos paredes, la de tarjeta grafica y la horneada- y
        // aqui se guarda ya masticada en bandas de altura, por el mismo motivo que el titileo va por tabla:
        // esto se hace mil cuatrocientas veces por cuadro, y el doble durante el cruce de patrones.
        //
        // Los dos factores del COLOR van en enteros de 0 a 256 para que apagar y enrojecer salga con una
        // multiplicacion y un desplazamiento, que es exactamente lo que ya costaba empaquetar el color.
        //
        // Enrojecer no es sumarle rojo: es QUITARLE azul, y algo menos de verde. Es lo que hace el aire de
        // verdad -se traga el azul antes que el rojo, por eso el sol se pone naranja- y ademas asi una
        // estrella nunca sale mas brillante de lo que era.
        //
        // La tabla NO depende del alto de la pantalla: la banda se pide con la altura ya en tanto por uno.
        // Y VIENE YA MASTICADA DEL TODO. Apagarse y titilar mas son dos cuentas que acaban en un solo
        // numero, asi que en vez de guardar las dos por separado y rehacer la mezcla mil cuatrocientas veces
        // por cuadro, se guarda la mezcla ya hecha: el brillo de una estrella sale de "algo fijo mas el
        // titileo por algo", que son una multiplicacion y una suma. El verde y el azul viajan juntos en un
        // solo numero por la misma razon: una lectura de tabla en vez de dos.
        private const int AireN = 128;
        private const float TitMedia = 0.725f;   // el centro de TitTab (va de 0.45 a 1.0): el titileo se agranda ALREDEDOR de el
        // Y SE REHACE AL TOCAR LA PERILLA, no al arrancar y nunca mas. La fuerza del aire se elige en
        // Ctrl+F3 mirando la pared, asi que la tabla tiene que ponerse al dia sola en cuanto cambia. Se
        // mira el paso puesto en cada cuadro -una comparacion de dos numeros- y se rehace solo si cambio:
        // rellenar 128 bandas cuesta lo mismo que un pestaneo y pasa una vez, no cada cuadro.
        private static readonly float[] AireP = new float[AireN];   // el trozo FIJO del brillo
        private static readonly float[] AireQ = new float[AireN];   // y lo que multiplica al titileo
        private static readonly uint[] AireGB = new uint[AireN];    // verde<<16 | azul, de 0 a 256 cada uno
        private static int aireDe = -1;                             // con que paso de la perilla se lleno la tabla
        internal static void HazAire()
        {
            int paso = Form1.AirePaso;
            if (aireDe == paso) return;
            for (int i = 0; i < AireN; i++)
            {
                float aire = Form1.AireDelCielo(i + 0.5f, AireN);   // 0 arriba del todo, 1 abajo del todo
                float br = 1f - Form1.AireApaga * aire;             // el brillo que le queda
                float ti = 1f + Form1.AireCentelleo * aire;         // cuanto se le agranda el titileo
                // brillo = (TitMedia + (titileo - TitMedia) * ti) * br, desarmado en fijo + titileo * factor
                AireP[i] = TitMedia * (1f - ti) * br;
                AireQ[i] = ti * br;
                float c = Form1.AireCalor * aire;
                uint g = (uint)(256f * (1f - 0.35f * c) + 0.5f);
                uint az = (uint)(256f * (1f - 0.75f * c) + 0.5f);
                AireGB[i] = (g << 16) | az;
            }
            aireDe = paso;
        }

        public bool EstrellasFinas = true;
        private void BuildStarVerts(D3D9.Vtx[] buf, float[] xs, float[] ys, float[] ph, float[] spd, float[] sz, int[] kd, int[] fm, uint[] col, int n, double t, float fade, float pulse, float[] dx, float[] dy, float w, float h)
        {
            int vi = 0;
            HazAire();   // se rehace SOLO si se toco la perilla; si no, es comparar dos numeros
            float aireEsc = (h > 0f) ? (AireN / h) : 0f;   // de altura en pixeles a banda de la tabla del aire
            float f255 = fade * 255f;                      // el entrecruzado y el paso a niveles, de una vez
            for (int i = 0; i < n; i++)
            {
                int k = kd[i];   // 0 polvo, 1 mediana, 2 grande: manda la deriva Y el latido
                // LA POSICION VA PRIMERO, y no es por gusto. Lo que se apaga, lo que enrojece y lo que titila
                // depende de a que ALTURA acaba la estrella, o sea de DESPUES de la deriva. Calcular el color
                // antes -como estaba- lo dejaria atado a donde nacio y no a donde se la ve.
                float x = xs[i] + dx[k]; if (x >= w) x -= w;   // deriva la de SU tamano + envuelve (xs,dx en [0,w) -> x en [0,2w) -> 1 resta basta)
                float y = ys[i] + dy[k]; if (y >= h) y -= h;
                int b = (int)(y * aireEsc); if (b < 0) b = 0; else if (b >= AireN) b = AireN - 1;

                // EL TITILEO, MAS FUERTE ABAJO. Se agranda alrededor de su punto medio, no desde cero: asi la
                // estrella centellea mas -sube y baja mas- sin quedarse de media ni mas clara ni mas oscura de
                // lo que le toca. Lo de apagarse es el otro factor, y ese si le baja el brillo entero. Los dos
                // vienen ya mezclados de la tabla (ver AireP/AireQ).
                //
                // El titileo se recoge en float y no en double a proposito: la tabla YA devuelve un float, asi
                // que guardarlo en double no gana ni un decimal y arrastra toda la cuenta a double para nada.
                // El double que importa es el del RELOJ, que es lo que entra ahi dentro.
                float tw = Titileo(t * spd[i] + ph[i]);
                if (FondoNatural) tw = TitMedia + (tw - TitMedia) * (k == 0 ? .55f : .72f);
                float br = (AireP[b] + tw * AireQ[b]) * f255;
                if (!(br > 0f)) br = 0f;   // con el titileo muy agrandado el valle se pasaria de rosca; y el "!" atrapa el NaN
                uint a = (uint)(int)br; if (a > 255) a = 255;   // por entero: el mismo numero sin la conversion lenta
                uint rgb = col[i], gb = AireGB[b];
                uint c = (a << 24) | (rgb & 0x00FF0000u)                     // alfa = brillo; el rojo, entero
                       | ((((rgb >> 8) & 0xFFu) * (gb >> 16) >> 8) << 8)      // el verde, algo comido
                       | ((rgb & 0xFFu) * (gb & 0xFFFFu) >> 8);               // y el azul, comido del todo
                float s = sz[i];
                if (k != 0 && pulse > 0.02f) s += pulse * (FondoNatural ? (k == 2 ? 2.5f : 1f) : (k == 2 ? 9f : 5f));   // latido: medianas/grandes crecen con la musica
                // No cambiar el patron ni reconstruir el atlas al alternar: solo
                // reducir las puntas grandes, dejando algunas estrellas protagonistas.
                if (EstrellasFinas) s *= k == 0 ? 0.85f : (k == 1 ? 0.66f : (i % 5 == 0 ? 0.78f : 0.42f));
                int f = (fm != null) ? fm[i] : FormaEstrella;
                if (FondoNatural)
                {
                    // Misma distribucion, formas y jerarquia que Legacy. La cobertura
                    // antialias del polvo no puede multiplicar su luz: compensar el
                    // area del quad conserva su presencia sin recuperar el parpadeo.
                    float light = k == 2 ? .94f : .52f;
                    if (k == 0) {
                        float coverage = 1.25f + s * .65f;
                        light = .94f * s * s / (coverage * coverage);
                        s = coverage;
                    }
                    uint naturalAlpha = (uint)Math.Min(255f, br * light);
                    c = (naturalAlpha << 24) | (c & 0xFFFFFFu);
                }
                SetQuadUV(buf, vi, x - s, y - s, x + s, y + s, c, AtlasU0[f], AtlasV0[f], AtlasU1[f], AtlasV1[f]);
                vi += 6;
            }
        }

        // Estrellas FUGACES: una cada 7-16s, desde arriba-izquierda hacia abajo-derecha, con estela que se
        // apaga. Igual logica que el render por CPU (DrawShoots), pero dibujada como cuadrangulos aditivos.
        private void UpdateAndDrawShoots(int w, int h, int nowMs, double finoMs)
        {
            // PERILLA DE Ctrl+F3. Se sale ANTES de tocar el reloj a proposito: al volver a encenderlas, el
            // primer cuadro las agenda de nuevo desde cero en vez de creerse que han pasado diez minutos y
            // soltar las dos de golpe.
            if (fugaces == Bajo) { shootFinoAnt = 0.0; for (int i = 0; i < shoots.Length; i++) shoots[i].On = false; return; }
            // En MEDIO va una sola a la vez (la segunda ranura no se usa) y con el doble de espera entre una
            // y otra: sigue habiendo fugaces -que es lo que le da vida al cielo- pero cuestan la mitad.
            int ranuras = (fugaces == Alto) ? shoots.Length : 1;
            int esperaBase = (fugaces == Alto) ? 7000 : 15000, esperaAzar = (fugaces == Alto) ? 9000 : 15000;
            if (shootFinoAnt <= 0.0) { shootFinoAnt = finoMs; nextShoot = nowMs + 4000 + srnd.Next(8000); return; }   // 1er cuadro: agenda la 1a, sin saltos
            float dt = (float)((finoMs - shootFinoAnt) / 1000.0);
            if (dt < 0f) dt = 0f; if (dt > 0.12f) dt = 0.12f;   // acota saltos
            shootFinoAnt = finoMs;

            if (nowMs - nextShoot >= 0)   // RESTA, no >=: sobrevive el desbordamiento de TickCount (~25 dias encendida)
            {
                nextShoot = nowMs + esperaBase + srnd.Next(esperaAzar);   // alto: una cada 7-16s; medio: cada 15-30s
                for (int i = 0; i < ranuras; i++)
                    if (!shoots[i].On)
                    {
                        shoots[i].On = true;
                        // De donde sale y hacia donde va lo dice Form1, fuera de este archivo, para que las
                        // dos paredes tiren las MISMAS fugaces (ver TiraFugaz).
                        bool gorda;
                        Form1.TiraFugaz(srnd, w, h, out shoots[i].X, out shoots[i].Y,
                                        out shoots[i].VX, out shoots[i].VY,
                                        out shoots[i].MaxLife, out shoots[i].Wdt, out shoots[i].Tail, out gorda);
                        shoots[i].Life = shoots[i].MaxLife;
                        // Las gordas salen blancas o doradas: una bola de fuego no es rosa. Las demas, de la
                        // paleta de siempre.
                        shoots[i].Col = gorda ? ((srnd.Next(2) == 0) ? 0x00FFFFFFu : 0x00FFE6A0u)
                                              : shootPal[srnd.Next(shootPal.Length)];
                        break;
                    }
            }
            int vi = 0;
            for (int i = 0; i < shoots.Length; i++)
            {
                if (!shoots[i].On) continue;
                Shoot s = shoots[i];
                if (FondoNatural)
                {
                    NaturalShootVertices(shootV, vi, s.X, s.Y, s.VX, s.VY, s.Wdt, s.Tail, s.Life / s.MaxLife, dim, s.Col);
                    vi += 12;
                }
                else
                {
                float k = s.Life / s.MaxLife; if (k < 0f) k = 0f;
                uint la = (uint)(230 * k * dim); if (la > 255) la = 255;   // fugaz atenuada mientras se navega con musica
                float tailX = s.X - s.VX * s.Tail, tailY = s.Y - s.VY * s.Tail;   // estela de largo variado
                float len = (float)Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
                float nx = (len > 0.001f) ? s.VX / len : 1f, ny = (len > 0.001f) ? s.VY / len : 0f;
                float ox = -ny * s.Wdt, oy = nx * s.Wdt;   // perpendicular (medio ancho variado)
                uint rgb = s.Col & 0x00FFFFFF;
                uint headC = (la << 24) | rgb, tailC = rgb;   // estela: brillante en cabeza -> transparente en cola
                VShoot(shootV, vi + 0, s.X + ox, s.Y + oy, headC); VShoot(shootV, vi + 1, s.X - ox, s.Y - oy, headC); VShoot(shootV, vi + 2, tailX - ox, tailY - oy, tailC);
                VShoot(shootV, vi + 3, s.X + ox, s.Y + oy, headC); VShoot(shootV, vi + 4, tailX - ox, tailY - oy, tailC); VShoot(shootV, vi + 5, tailX + ox, tailY + oy, tailC);
                vi += 6;
                uint hc = (Math.Min(255u, la + 40u) << 24) | rgb; float hs = 0.9f + s.Wdt;   // cabeza brillante, tamaño segun grosor
                VShoot(shootV, vi + 0, s.X - hs, s.Y - hs, hc); VShoot(shootV, vi + 1, s.X + hs, s.Y - hs, hc); VShoot(shootV, vi + 2, s.X + hs, s.Y + hs, hc);
                VShoot(shootV, vi + 3, s.X - hs, s.Y - hs, hc); VShoot(shootV, vi + 4, s.X + hs, s.Y + hs, hc); VShoot(shootV, vi + 5, s.X - hs, s.Y + hs, hc);
                vi += 6;
                }
                s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt;
                // POR LOS CUATRO LADOS, y no es un detalle: antes solo podian irse por la derecha o por
                // abajo porque solo podian ir hacia alla. Con la direccion suelta, una que se vaya por la
                // izquierda seguiria viva, invisible y ocupando una de las dos ranuras hasta que se le
                // acabara la vida, y mientras tanto no saldria ninguna otra.
                if (s.Life <= 0f || s.X > w + Form1.FugazBorra || s.X < -Form1.FugazBorra
                                 || s.Y > h + Form1.FugazBorra || s.Y < -Form1.FugazBorra) s.On = false;
                shoots[i] = s;
            }
            if (vi > 0) gpu.DrawShootStreaks(shootV, vi / 3);
        }
        private static void VShoot(D3D9.Vtx[] v, int o, float x, float y, uint c) { v[o].X = x; v[o].Y = y; v[o].Z = 0f; v[o].Rhw = 1f; v[o].Color = c; v[o].U = 0f; v[o].V = 0f; }
        public void DrawQuad(IntPtr tex, D3D9.Vtx[] quad) { if (Ok) gpu.DrawTexQuad(tex, quad); }
        public void DrawTexturedBatch(IntPtr tex, D3D9.Vtx[] vertices, int triangles)
        { if (Ok && tex != IntPtr.Zero) gpu.DrawQuads(tex, vertices, triangles); }
        public void DrawColorQuad(D3D9.Vtx[] quad) { if (Ok) gpu.DrawQuads(IntPtr.Zero, quad, 2); }
        public uint BlankCubeColor { set { if (gpu != null) gpu.CubeBaseColor = value; } }
        public void DrawMesh(D3D9.Vtx[] v, int tris) { if (Ok) gpu.DrawMesh(v, tris); }   // malla plana (fondo reactivo)
        public void DrawMeshAdd(D3D9.Vtx[] v, int tris) { if (Ok) gpu.DrawMeshAdd(v, tris); }   // malla plana SUMANDO luz (el neon)
        public bool End() { return Ok && gpu.EndFrame(); }

        public void Dispose()
        {
            if (glowTex != IntPtr.Zero) { D3D9.Release(glowTex); glowTex = IntPtr.Zero; }
            if (uiGlowTex != IntPtr.Zero) { D3D9.Release(uiGlowTex); uiGlowTex = IntPtr.Zero; }
            if (naturalStarAtlas != null) { naturalStarAtlas.Dispose(); naturalStarAtlas = null; }
            if (legacyStarAtlas != null) { legacyStarAtlas.Dispose(); legacyStarAtlas = null; }
            if (gpu != null) { gpu.Dispose(); gpu = null; }
        }

        // Un numero al azar con forma de campana (Box-Muller): sirve para que las estrellas caigan ALREDEDOR
        // de un centro, apinandose cerca y ralas lejos, que es como se agrupan de verdad.
        private static float Gauss(Random r)
        {
            double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        private void BuildStars(int w, int h, int seed)
        {
            Random rnd = new Random(seed);
            // MUCHAS MAS Y MUCHO MAS TENUES, igual que por CPU: un cielo de verdad tiene un punado de
            // estrellas fuertes y cientos que apenas se adivinan, y eso es lo que da sensacion de fondo.
            nStars = Math.Max(200, w * h / 1700);
            if (nStars > 1400) nStars = 1400;
            // Y la perilla de Ctrl+F3 lo recorta. El suelo de 200 se recorta CON ella: dejarlo fijo haria
            // que pedir "un cielo al 20%" en una pantalla pequena no quitara ni una estrella.
            if (densidad < 0.999f) { nStars = (int)(nStars * densidad); if (nStars < 24) nStars = 24; }
            sx = new float[nStars]; sy = new float[nStars]; sphase = new float[nStars];
            sspeed = new float[nStars]; ssize = new float[nStars]; skind = new int[nStars]; sforma = new int[nStars]; scol = new uint[nStars];
            // DE QUE COLOR ES UNA ESTRELLA. Las de verdad van de blanco-azulado a blanco, amarillo y naranja:
            // no hay estrellas verdes ni violetas, y la inmensa mayoria de las que se ven son casi blancas.
            // Con la paleta de rotulador de antes el cielo parecia confeti. (La misma que usa la version por
            // CPU, para que el cielo se vea igual se juegue por donde se juegue.)
            uint[] palTemp = { 0xFF9BB0FF, 0xFFAABFFF,   // 0-1  azul-blanco (las mas calientes)
                               0xFFCAD7FF, 0xFFE4EBFF,   // 2-3  blanco-azul
                               0xFFF8F7FF, 0xFFFFFFFA,   // 4-5  blancas
                               0xFFFFF4EA, 0xFFFFEDCD,   // 6-7  blanco-amarillo (como el Sol)
                               0xFFFFDAA5, 0xFFFFC78E,   // 8-9  naranjas
                               0xFFFFB280 };             // 10   naranja hondo
            // DONDE SE PONEN. La MISMA receta que por CPU, para que el cielo se vea igual se juegue por
            // donde se juegue. Un cielo de verdad tiene grumos, parejas y zonas mas vacias, asi que una
            // CUARTA PARTE cae alrededor de unos centros y el resto suelta por cualquier sitio. Dos cosas
            // estaban mal y las dos se veian, sobre todo en pantalla ancha:
            //
            // 1) LAS HILERAS DE LOS BORDES. La que caia fuera de la pantalla se PEGABA al borde en vez de
            //    volver a tirarse. Como los grumos son campanas, muchas caian fuera... y acababan todas en
            //    la misma linea: medido, el 15% del cielo formaba cuatro hileras pegadas a los cantos.
            // 2) LOS GRUMOS ESTIRADOS. Salian mas anchos que altos (el 0.7 de la altura), o sea bandas
            //    horizontales en vez de grumos. Ahora el radio es el mismo en los dos ejes.
            int nc = 10 + rnd.Next(5);
            float[] ccx = new float[nc], ccy = new float[nc], ccr = new float[nc];
            for (int c = 0; c < nc; c++)
            {
                // Los centros no se pegan al canto: uno con el centro en el borde deja media campana fuera
                // y vuelve a amontonar todo en esa orilla.
                ccx[c] = w * (0.10f + (float)rnd.NextDouble() * 0.80f);
                ccy[c] = h * (0.10f + (float)rnd.NextDouble() * 0.80f);
                ccr[c] = h * (0.10f + (float)rnd.NextDouble() * 0.14f);
            }
            for (int i = 0; i < nStars; i++)
            {
                float bx, by;
                for (int intento = 0; ; intento++)
                {
                    if (intento < 6 && rnd.Next(100) < 25)
                    {
                        int c = rnd.Next(nc);
                        bx = ccx[c] + Gauss(rnd) * ccr[c]; by = ccy[c] + Gauss(rnd) * ccr[c];
                    }
                    else { bx = (float)(rnd.NextDouble() * w); by = (float)(rnd.NextDouble() * h); }
                    if (bx >= 2f && bx <= w - 2f && by >= 2f && by <= h - 2f) break;
                }
                sx[i] = bx; sy[i] = by;
                sphase[i] = (float)(rnd.NextDouble() * Math.PI * 2);
                sspeed[i] = 0.6f + (float)rnd.NextDouble() * 1.8f;
                // Lo brillante que es, con una curva bien inclinada: casi todas debilisimas, unas pocas
                // fuertes. De aqui salen el tamano, el color y a que grupo va.
                double m = Math.Pow(rnd.NextDouble(), 4.6);
                // DE QUE GRUPO ES. El corte de las medianas sube de 0.34 a 0.55: antes una de cada seis era
                // "mediana" -unas noventa en pantalla- y eran justo las que se veian como pompas. Ahora son
                // la mitad, y las que dejan de serlo pasan a ser polvo, que es como se ve de verdad la
                // inmensa mayoria del cielo. (El mismo corte que por CPU.)
                skind[i] = (m > 0.80) ? 2 : (m > 0.55) ? 1 : 0;
                // Y el POLVO encoge de golpe: medía tres o cuatro de medio lado, o sea seis a nueve pixeles
                // en pantalla, y con su halo salia una pompa. Un punto de luz sin forma solo pasa por
                // estrella si es diminuto; de ahi para arriba tiene que tener silueta. Por CPU el polvo es
                // un pixel clavado, asi que aqui se queda en dos o tres.
                // Los tamanos salen de igualar el cielo por CPU midiendo los dos dibujados: alli la grande
                // ocupa unos 34 px de halo con las puntas llegando a otros tantos -unos 67 px de punta a
                // punta- y la mediana unos 18. Como aqui el cromo entero cabe en el cuadrangulo y este mide
                // el doble de ssize, salen estos numeros. Antes la grande media 34 px en total: la mitad de
                // lo que mide por CPU, y por eso los dos cielos no se parecian.
                ssize[i] = (skind[i] == 2) ? (float)(20 + m * 14) : (skind[i] == 1) ? (float)(8 + m * 2) : (float)(1.0 + m * 1.2);
                // TODA la que se vea lleva rayitas: la grande las largas y la mediana las cortas y tenues
                // (son dos casillas distintas del atlas, asi que sigue costando UNA sola llamada de dibujo).
                sforma[i] = (skind[i] == 0) ? FormaPunto : (skind[i] == 2) ? FormaRayos : FormaEstrella;
                int q = rnd.Next(100);
                int idx = (q < 14) ? rnd.Next(2) : (q < 34) ? 2 + rnd.Next(2) : (q < 68) ? 4 + rnd.Next(2)
                        : (q < 88) ? 6 + rnd.Next(2) : (q < 97) ? 8 + rnd.Next(2) : 10;
                uint col = palTemp[idx];
                // El brillo entra en el ALFA del vertice: asi la de mas atras se ve como lo que es, un punto
                // que apenas se adivina, sin tener que dibujarla distinta.
                uint al = (uint)(255.0 * (0.26 + m * 0.74)); if (al > 255) al = 255;
                scol[i] = (al << 24) | (col & 0x00FFFFFFu);
            }
            starV = new D3D9.Vtx[nStars * 6];
        }

        // LA BANDA DE LA VIA LACTEA, horneada a un cuarto de tamano. Es la MISMA receta que por CPU -las
        // mismas manchas, el mismo desvanecido hacia abajo, las mismas vetas de polvo- para que el cielo se
        // vea igual se juegue por donde se juegue.
        internal static Bitmap BakeViaLactea(int mw, int mh)
        {
            Bitmap bmp = new Bitmap(mw, mh, PixelFormat.Format32bppArgb);
            Random r = new Random(20260620);   // fijo: la misma via lactea siempre, no una distinta por arranque
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float x0 = -mw * 0.08f, y0 = mh * 0.86f, x1 = mw * 1.08f, y1 = mh * 0.06f;
                for (int i = 0; i < 240; i++)
                {
                    float t = (float)r.NextDouble();
                    float ancho = mh * (0.075f + 0.045f * (float)Math.Sin(t * 3.1));
                    float x = x0 + (x1 - x0) * t + Gauss(r) * ancho * 0.55f;
                    float y = y0 + (y1 - y0) * t + Gauss(r) * ancho;
                    // Se apaga hacia abajo: ahi el cielo lleva el resplandor del horizonte, y una neblina
                    // blanca encima se lee como una mancha gris. Ademas es lo que pasa de verdad.
                    float fy = 1f - y / mh; if (fy < 0f) fy = 0f; if (fy > 1f) fy = 1f;
                    int al = (int)(6f * (0.10f + 0.90f * fy * fy)); if (al < 1) continue;
                    Mancha(g, x, y, mh * (0.05f + (float)r.NextDouble() * 0.10f), Color.FromArgb(214, 226, 255), al);
                }
                for (int i = 0; i < 24; i++)   // las vetas de polvo que la cruzan
                {
                    float t = (float)r.NextDouble();
                    float x = x0 + (x1 - x0) * t + Gauss(r) * mh * 0.05f;
                    float y = y0 + (y1 - y0) * t + Gauss(r) * mh * 0.055f;
                    Mancha(g, x, y, mh * (0.04f + (float)r.NextDouble() * 0.085f), Color.FromArgb(8, 10, 26), 34);
                }
            }
            return bmp;
        }

        // LAS NEBULOSAS, en blanco: el color se lo pone la tarjeta al dibujarlas (el del genero que suena).
        // Van en la mitad de arriba del cielo a proposito: abajo el degradado ya lleva el resplandor del
        // genero, y una nebulosa del mismo color encima de el se lee como suciedad.
        internal static Bitmap BakeNebulosas(int mw, int mh)
        {
            Bitmap bmp = new Bitmap(mw, mh, PixelFormat.Format32bppArgb);
            Random r = new Random(20260621);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                for (int k = 0; k < 3; k++)
                {
                    float cx = mw * (0.14f + (float)r.NextDouble() * 0.72f);
                    float cy = mh * (0.10f + (float)r.NextDouble() * 0.42f);
                    for (int i = 0; i < 20; i++)
                        Mancha(g, cx + Gauss(r) * mw * 0.075f, cy + Gauss(r) * mh * 0.08f,
                               mh * (0.07f + (float)r.NextDouble() * 0.14f), Color.White, AlfaNeb(4));
                    Mancha(g, cx, cy, mh * 0.07f, Color.White, AlfaNeb(7));   // el corazon, un pelin mas encendido
                }
            }
            return bmp;
        }

        // El alfa de una mancha de nebulosa a la fuerza elegida. El numero vive en Form1 -fuera del trozo de
        // tarjeta grafica- porque esta pared y la horneada tienen que usar la MISMA: con dos numeros, el dia
        // que alguien toque uno, la maquina del bar y la de repuesto pintarian fondos distintos.
        private static int AlfaNeb(int alfa)
        {
            int a = (int)(alfa * Form1.NebFuerza + 0.5f);
            return (a > 255) ? 255 : a;
        }

        // Una mancha de luz redonda que se desvanece hacia fuera: la pieza con la que estan hechas las dos.
        private static void Mancha(Graphics g, float cx, float cy, float rad, Color c, int alfaCentro)
        {
            if (rad < 1f) rad = 1f;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
                using (PathGradientBrush pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterPoint = new PointF(cx, cy);
                    pgb.CenterColor = Color.FromArgb(alfaCentro, c);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillEllipse(pgb, cx - rad, cy - rad, rad * 2, rad * 2);
                }
            }
        }

        // Glow radial LISO (sin la chispa de 4 puntas de las estrellas): para el destello del boton pulsado del
        // buscador, que debe verse como un halo suave (no como una estrellita).
        private static Bitmap BakeRadialGlow(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(0, 0, size, size);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterPoint = new PointF(size / 2f, size / 2f);
                        pgb.CenterColor = Color.FromArgb(220, 255, 255, 255);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                        g.FillEllipse(pgb, 0, 0, size, size);
                    }
                }
            }
            return bmp;
        }

        // LAS CUATRO FORMAS DE ESTRELLA, EN UNA SOLA TEXTURA.
        //
        // Antes todas las estrellas eran la misma figura -un destello de cuatro puntas- y solo cambiaban de
        // tamano y de color, asi que el cielo se veia siempre igual. Ahora hay cuatro siluetas distintas.
        //
        // Van las cuatro en UNA textura (un atlas de 2x2) y cada estrella escoge su trozo con las
        // coordenadas de textura. Eso importa: si cada forma fuera su propia textura habria que cambiar de
        // textura entre grupos y el campo entero dejaria de dibujarse de una sola llamada. Asi sigue
        // costando exactamente lo mismo que antes: una llamada para todas las estrellas.
        internal const int FormaEstrella = 0, FormaRayos = 1, FormaPunto = 2;
        internal const int NFormas = 3;   // el atlas es de 2x2: la cuarta casilla se queda vacia

        // Coordenadas del trozo de cada forma dentro del atlas, ya metidas medio texel hacia dentro para que
        // el filtrado no traiga pixeles de la casilla de al lado.
        private static readonly float[] AtlasU0 = new float[NFormas], AtlasV0 = new float[NFormas],
                                        AtlasU1 = new float[NFormas], AtlasV1 = new float[NFormas];
        static GpuStarfield() { CalculaAtlas(64); }   // por si alguien pide vertices antes de hornear la textura
        private static void CalculaAtlas(int celda)
        {
            float lado = celda * 2f, medio = 0.5f / lado;
            for (int i = 0; i < NFormas; i++)
            {
                float cx = (i % 2) * 0.5f, cy = (i / 2) * 0.5f;
                AtlasU0[i] = cx + medio; AtlasV0[i] = cy + medio;
                AtlasU1[i] = cx + 0.5f - medio; AtlasV1[i] = cy + 0.5f - medio;
            }
        }

        private static Bitmap BakeStarTexture(int celda)
        {
            CalculaAtlas(celda);
            Bitmap bmp = new Bitmap(celda * 2, celda * 2, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int forma = 0; forma < NFormas; forma++)
                {
                    int ox = (forma % 2) * celda, oy = (forma / 2) * celda;
                    // El halo de fondo. Cada forma lleva el suyo: el punto uno apretado y brillante (es polvo
                    // de estrellas, tiene que leerse aunque mida tres pixeles) y las figuras uno mas suave,
                    // porque su silueta ya se ve sola y un halo fuerte la emborronaria.
                    // El halo de fondo, que en un punto de luz es CASI TODO lo que se ve: el nucleo es
                    // diminuto y lo que da la sensacion de estrella es el resplandor de alrededor.
                    // CUANTO SITIO DE LA CASILLA OCUPA EL HALO, y es lo que decidia si la estrella se veia
                    // como estrella o como pompa. Antes el halo llenaba la casilla ENTERA y las rayitas solo
                    // llegaban a 0.46 de ella: o sea que las puntas ni asomaban del borron y todas las
                    // estrellas salian redondas. Ahora el halo es un nucleo apretado y las rayitas salen muy
                    // por fuera, que es exactamente lo que hace el cielo por CPU (halo de 34 px con puntas de
                    // 34 px en la grande). El polvo si llena su casilla: ahi el halo ES la estrella.
                    int centro = (forma == FormaPunto) ? 210 : (forma == FormaRayos) ? 190 : 150;
                    float halo = (forma == FormaPunto) ? 1.00f : (forma == FormaRayos) ? 0.52f : 0.34f;
                    float hd = celda * halo, hx = ox + (celda - hd) * 0.5f, hy = oy + (celda - hd) * 0.5f;
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddEllipse(hx, hy, hd, hd);
                        using (PathGradientBrush pgb = new PathGradientBrush(gp))
                        {
                            pgb.CenterPoint = new PointF(ox + celda / 2f, oy + celda / 2f);
                            pgb.CenterColor = Color.FromArgb(centro, 255, 255, 255);
                            pgb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                            g.FillEllipse(pgb, hx, hy, hd, hd);
                        }
                    }
                    float c = celda / 2f, cx2 = ox + c, cy2 = oy + c;
                    using (SolidBrush b = new SolidBrush(Color.White))
                    {
                        if (forma == FormaEstrella || forma == FormaRayos)
                        {
                            // UN PUNTO DE LUZ, que es lo que es una estrella: un nucleo diminuto y muy
                            // brillante, el resplandor que ya puso el halo de arriba, y las cuatro rayitas
                            // cruzadas. Las rayitas no las hace la estrella: las hace el diafragma de la
                            // camara, y son justo lo que hace que una foto del cielo parezca una foto.
                            //
                            // Las lleva TODA la que se ve, pero la mediana las lleva mas cortas y bastante
                            // mas tenues que la grande: esa diferencia es la que evita el efecto purpurina,
                            // porque si todas las llevaran igual de marcadas serian sesenta chispas y no un
                            // cielo. (Los mismos numeros que DrawRayos por CPU.)
                            bool grande = (forma == FormaRayos);
                            float largo = celda * (grande ? 0.47f : 0.40f), ancho = celda * 0.030f;
                            int tope = grande ? 120 : 62;
                            for (int paso = 0; paso < 3; paso++)
                            {
                                float L = largo * (1f - paso * 0.30f), A = ancho * (1f + paso * 1.1f);
                                int alfa = Math.Min(tope, 34 + paso * 26);
                                using (SolidBrush rb = new SolidBrush(Color.FromArgb(alfa, 255, 255, 255)))
                                {
                                    g.FillPolygon(rb, new PointF[] { new PointF(cx2 - L, cy2), new PointF(cx2, cy2 - A), new PointF(cx2 + L, cy2), new PointF(cx2, cy2 + A) });
                                    g.FillPolygon(rb, new PointF[] { new PointF(cx2, cy2 - L), new PointF(cx2 + A, cy2), new PointF(cx2, cy2 + L), new PointF(cx2 - A, cy2) });
                                }
                            }
                            float nn = celda * (grande ? 0.075f : 0.055f);
                            g.FillEllipse(b, cx2 - nn, cy2 - nn, nn * 2, nn * 2);
                        }
                        else
                        {
                            // Punto: solo un nucleo redondo. El halo de arriba hace el resto.
                            g.FillEllipse(b, cx2 - celda * 0.085f, cy2 - celda * 0.085f, celda * 0.17f, celda * 0.17f);
                        }
                    }
                }
            }
            return bmp;
        }

        private void ActualizaAtlasNatural()
        {
            // Seleccionar otro estilo cancela la espera pendiente, incluso al volver al atlas ya activo.
            if (atlasRequested != FondoNatural) { atlasRequested = FondoNatural; atlasRetryMs = 0; }
            if (glowNatural == FondoNatural) return;
            double now = GpuPanel.AhoraMs();
            if (now < atlasRetryMs) return;
            // Mismo atlas 2x2 de 128 px y una sola textura GPU. Cache RAM perezosa (maximo 128 KiB):
            // un driver temporalmente perdido se recupera sin rehornear ni saturar el hilo de dibujo.
            Bitmap bitmap;
            if (FondoNatural)
            {
                if (naturalStarAtlas == null) naturalStarAtlas = BakeNaturalStarTexture(64);
                bitmap = naturalStarAtlas;
            }
            else
            {
                if (legacyStarAtlas == null) legacyStarAtlas = BakeStarTexture(64);
                bitmap = legacyStarAtlas;
            }
            IntPtr next = CreateStarAtlasForTest != null ? CreateStarAtlasForTest(bitmap) : gpu.CreateTextureFromBitmap(bitmap);
            if (next == IntPtr.Zero) { atlasRetryMs = now + 1000; return; }
            if (glowTex != IntPtr.Zero) D3D9.Release(glowTex);
            glowTex = next;
            glowNatural = FondoNatural;
            atlasRetryMs = 0;
        }

        internal static Bitmap BakeNaturalStarTexture(int celda)
        {
            if (celda < 8 || celda > 256) throw new ArgumentOutOfRangeException("celda");
            CalculaAtlas(celda);
            int side = celda * 2;
            int[] pixels = new int[side * side];
            for (int forma = 0; forma < NFormas; forma++)
            {
                int ox = forma % 2 * celda, oy = forma / 2 * celda;
                for (int y = 0; y < celda; y++)
                    for (int x = 0; x < celda; x++)
                    {
                        double dx = (x + .5 - celda * .5) / (celda * .5), dy = (y + .5 - celda * .5) / (celda * .5);
                        double rr = dx * dx + dy * dy;
                        bool dust = forma == FormaPunto;
                        // Dos escalas en el mismo atlas: el polvo se reduce a 3-5 px,
                        // no puede compartir el nucleo subpixel de una luz grande.
                        // Horneado unico; sin mipmaps, nuevas texturas ni coste por cuadro.
                        double core = dust ? .88 * Math.Exp(-rr * 2.8) : Math.Exp(-rr * 27);
                        double halo = dust ? .12 * Math.Exp(-rr * 8) : .21 * Math.Exp(-rr * 14) + .018 * Math.Exp(-rr * 3);
                        double rays = 0;
                        if (!dust)
                        {
                            double strength = forma == FormaRayos ? .94 : .76;
                            double rx = Math.Max(0, 1 - Math.Abs(dx) / .96), ry = Math.Max(0, 1 - Math.Abs(dy) / .96);
                            rays = strength * (rx * rx * Math.Exp(-dy * dy * (50 + Math.Abs(dx) * 120))
                                + ry * ry * Math.Exp(-dx * dx * (50 + Math.Abs(dy) * 120)));
                        }
                        double edge = Math.Max(0, 1 - rr); edge = Math.Min(1, edge * 3);
                        int alpha = (int)(255 * Math.Min(1, (core + halo + rays) * edge) + .5);
                        pixels[(oy + y) * side + ox + x] = (alpha << 24) | 0xFFFFFF;
                    }
            }
            Bitmap result = new Bitmap(side, side, PixelFormat.Format32bppArgb);
            BitmapData data = result.LockBits(new Rectangle(0, 0, side, side), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < side; y++) Marshal.Copy(pixels, y * side, new IntPtr(data.Scan0.ToInt64() + y * data.Stride), side); }
            finally { result.UnlockBits(data); }
            return result;
        }

        // Dos triangulos de cola afilada y dos de cabeza romboidal, los mismos cuatro del estilo Legacy.
        // No hay enjambres, azar por cuadro, texturas adicionales ni un segundo envio de geometria.
        internal static void NaturalShootVertices(D3D9.Vtx[] v, int o, float x, float y, float vx, float vy,
            float width, float tail, float life, float visibility, uint color)
        {
            float len = (float)Math.Sqrt(vx * vx + vy * vy), nx = len > .001f ? vx / len : 1, ny = len > .001f ? vy / len : 0;
            float k = Form1Clamp(life), dimmer = Form1Clamp(visibility), px = -ny * width, py = nx * width;
            uint alpha = (uint)(230 * k * dimmer);
            uint rgb = width > 2.6f ? 0xFFE6B6u : (Math.Max(192u, (color >> 16) & 255u) << 16) | (Math.Max(200u, (color >> 8) & 255u) << 8) | Math.Max(216u, color & 255u);
            uint head = (alpha << 24) | rgb, far = rgb;
            float tx = x - vx * tail, ty = y - vy * tail;
            VShoot(v, o, x + px, y + py, head); VShoot(v, o + 1, x - px, y - py, head); VShoot(v, o + 2, tx - px * .04f, ty - py * .04f, far);
            VShoot(v, o + 3, x + px, y + py, head); VShoot(v, o + 4, tx - px * .04f, ty - py * .04f, far); VShoot(v, o + 5, tx + px * .04f, ty + py * .04f, far);
            float h = .8f + width, along = h * 1.65f;
            uint core = (Math.Min(255u, (uint)(255 * k * dimmer)) << 24) | 0xFFF9EEu;
            VShoot(v, o + 6, x + nx * along, y + ny * along, core); VShoot(v, o + 7, x + px, y + py, head); VShoot(v, o + 8, x - nx * along * .65f, y - ny * along * .65f, far);
            VShoot(v, o + 9, x + nx * along, y + ny * along, core); VShoot(v, o + 10, x - nx * along * .65f, y - ny * along * .65f, far); VShoot(v, o + 11, x - px, y - py, head);
        }
        private static float Form1Clamp(float v) { return !(v > 0) ? 0 : v >= 1 ? 1 : v; }

        private static void SetQuad(D3D9.Vtx[] v, int o, float x0, float y0, float x1, float y1, uint cTL, uint cTR, uint cBR, uint cBL)
        {
            V(ref v[o + 0], x0, y0, cTL, 0, 0); V(ref v[o + 1], x1, y0, cTR, 1, 0); V(ref v[o + 2], x1, y1, cBR, 1, 1);
            V(ref v[o + 3], x0, y0, cTL, 0, 0); V(ref v[o + 4], x1, y1, cBR, 1, 1); V(ref v[o + 5], x0, y1, cBL, 0, 1);
        }
        // El mismo quad pero escogiendo QUE TROZO de la textura se usa: asi las cuatro formas de estrella
        // salen de una sola textura y de una sola llamada de dibujo.
        private static void SetQuadUV(D3D9.Vtx[] v, int o, float x0, float y0, float x1, float y1, uint c,
                                     float u0, float v0, float u1, float v1)
        {
            V(ref v[o + 0], x0, y0, c, u0, v0); V(ref v[o + 1], x1, y0, c, u1, v0); V(ref v[o + 2], x1, y1, c, u1, v1);
            V(ref v[o + 3], x0, y0, c, u0, v0); V(ref v[o + 4], x1, y1, c, u1, v1); V(ref v[o + 5], x0, y1, c, u0, v1);
        }
        private static void V(ref D3D9.Vtx v, float x, float y, uint c, float u, float vv) { v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.Color = c; v.U = u; v.V = vv; }
    }

    // -------- Ventana de prueba (F12): muestra el campo estrellado por GPU con FPS --------
    internal sealed class GpuTestWindow : Form
    {
        private static GpuTestWindow instance;
        public static void Toggle()
        {
            if (instance != null) { instance.Close(); return; }
            instance = new GpuTestWindow();
            instance.Show();
        }

        private GpuStarfield stars;
        private readonly Timer timer = new Timer();
        private int frames, fpsTick, fps;
        private const string BaseTitle = "Rocola - Prueba de render por GPU (Direct3D 9) - F12 o Esc para cerrar";

        public GpuTestWindow()
        {
            Text = BaseTitle;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1024, 700);
            BackColor = Color.Black;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            stars = new GpuStarfield(this.Handle, ClientSize.Width, ClientSize.Height, 1234);
            if (!stars.Ok) { MessageBox.Show("No se pudo crear el dispositivo D3D9 para la ventana.", "GPU"); Close(); return; }
            timer.Interval = 15;
            timer.Tick += delegate { Frame(); };
            timer.Start();
        }

        private void Frame()
        {
            if (stars == null || !stars.Ok) return;
            stars.Render(ClientSize.Width, ClientSize.Height, 0f);
            frames++;
            if (Environment.TickCount - fpsTick >= 1000)
            {
                fps = frames; frames = 0; fpsTick = Environment.TickCount;
                Text = BaseTitle + "   [" + fps + " FPS, " + stars.Count + " estrellas]";
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == Keys.F12) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            if (stars != null) { stars.Dispose(); stars = null; }
            instance = null;
            base.OnFormClosed(e);
        }
    }

    // Datos de un cubo que GIRA para dibujarlo por GPU: celda, angulo y portada(s). Para discos TOP,
    // CoverB + Fade hacen el fundido suave entre la portada actual y la siguiente.
    internal struct GpuCubeInfo
    {
        public object Key;
        public bool Has;
        public float X, Y, W, H;       // celda donde va el cubo (coords del panel)
        public double Angle;           // giro actual (radianes)
        public Bitmap CoverA;          // portada principal (null si aun no cargo -> relleno azulado)
        public Bitmap CoverB;          // portada siguiente para el fundido TOP (null si no aplica)
        public float Fade;             // 0..1 mezcla A->B
        public float Tilt;             // bamboleo del cubo entero (radianes); los CubeBox grande/mini lo usan
    }
    internal struct GpuCubeState { public GpuCubeInfo Sel, Relay, Mini; public bool Pared; }   // album: seleccionado (gira) + relevo (decae); Mini = cubo de la barra inferior; Pared = Sel es el de la pared de discos

    // Una CELDA de la rejilla de discos para dibujarla como su PROPIO quad con textura (en vez de recomponer una
    // textura a pantalla completa por pagina). Bmp = atlas o celda entera (null si aun no listo).
    // Key = Folder (estable) para cachear su textura entre paginas; null = no cachear (discos TOP / sin carpeta).
    internal struct GpuCellLayout { public Rectangle Face, FaceAt, Text1, Text1At, Text2, Text2At, Top, TopAt, Bottom, BottomAt, Left, LeftAt, Right, RightAt; public int Width, Height; public bool Separate, HideBlank; }
    internal struct GpuGridCell { public string Key; public Bitmap Bmp, Preview, FullCover; public bool Direct, CoverReady; public GpuCellLayout Layout; public int Slot; public float X, Y, W, H; public float A; public bool Placeholder, HasCover, NeedsCover; public RectangleF CoverRect; public bool Sel; }   // Preview nunca sustituye la imagen final ni su paleta.
    internal struct GpuProgress { public bool Has, Neon; public Rectangle Rect; public double Value; public uint Color; public object Key; }
    // TRANSICION entre dos CUBOS de la rocola (celda de la rejilla, cubo grande del disco, cubo gigante de la
    // atraccion, cubo del buscador): la portada viaja de uno al otro dibujada ENCIMA de todo, girando, con
    // bamboleo y un destello detras, y se funde al llegar. Se dibuja con el MISMO DrawCube que los demas cubos,
    // asi que aterriza calcada sobre el cubo de destino. X,Y,W,H = celda del cubo (igual que GpuCubeInfo).
    internal struct GpuTrans
    {
        public int Key;
        public bool Has; public Bitmap Bmp;
        public float X, Y, W, H;   // celda donde va el cubo (coords de pantalla)
        public float Alpha;        // 1 = opaca; baja al final para fundirse con el cubo de destino
        public double Angle;       // giro (radianes)
        public float Tilt;         // bamboleo del cubo entero (radianes)
        public float Glow;         // 0..1 destello aditivo detras (0 = ninguno)
    }
    // VENTANA (el buscador) abriendo/cerrando, al estilo de una ventana de macOS: la ventana entera crece o
    // se encoge desde SU CENTRO, sube/baja un poco y se funde. Bmp = null significa "anima la PROPIA textura
    // de UI" (al abrir, esa textura YA es el buscador: no hay que copiar ni subir nada). HideUi = no dibujarla
    // ademas a tamano normal. Al cerrar, la textura de UI ya es la de navegacion y se anima una instantanea.
    // Robada = cerrando, con la textura que tenia la UI (sin copia: BuscadorLigeroGpu.cs); entonces Bmp es null.
    internal struct GpuWin { public bool Has; public bool HideUi; public Bitmap Bmp; public bool Robada; public float Scale, ScaleY, Dy, Alpha; }
    // AURORA DEL HORIZONTE: el visualizador de espectro, dibujado como parte del CIELO y no como un aparato
    // encima. B = 8 bandas ya suavizadas (0..1, de graves a agudos); se interpolan a lo ancho y se pintan con
    // halos aditivos MUY solapados, que es lo que convierte ocho valores sueltos en una franja continua de luz.
    // DimFilo atenua SOLO lo afilado -el filo de neon y la raya del horizonte-, aparte del relleno. Bajarlo
    // deja la aurora como una mancha de color suave, sin lineas duras: eso es lo que se lee como "difuminada".
    internal struct GpuAurora { public bool Has; public float[] B; public float BaseY, Alto; public uint Rgb, Rgb2, RgbA, Rgb2A; public float Dim, DimFilo, Calma; }   // Rgb/Rgb2 = pie de la cortina (grave/agudo), RgbA/Rgb2A = su punta; Calma = cuanto hay de REPOSO (sin musica)
    // AVISO ("moneda insertada", "cancion agregada"...). Antes era una etiqueta de Windows puesta ENCIMA del
    // panel: cada vez que aparecia o se iba, Windows tenia que recortar el panel a su alrededor y repintar,
    // y ese era un tiron justo en el momento en que el cliente mete la moneda. Ahora es un cuadro mas de la
    // GPU: entra deslizandose desde arriba y se va desvaneciendo, y no interrumpe nada. Ver = version del
    // texto (cambia al escribir uno nuevo); asi la textura se re-sube SOLO cuando de verdad cambio.
    internal struct GpuToast { public bool Has; public Bitmap Bmp; public int Ver; public float X, Y, Alpha; }
    internal delegate int GpuGridFetch(GpuGridCell[] buf);   // llena buf con las celdas de la pagina actual; devuelve cuantas (<= buf.Length)

    // -------- Control D3D9 para usar el campo estrellado como FONDO de la vista de discos --------
    //  Es un control aparte (su propia ventana) con estilos aptos para D3D9 (sin doble-buffer GDI).
    //  Se muestra ENCIMA de la vista de discos al activar el modo GPU; el render lo maneja D3D9.
    internal sealed partial class GpuPanel : Control
    {
        private GpuStarfield stars;
        private bool idleHooked;
        private int frames, fpsTick;
        private int lastHeavyMs;                          // ultima reconstruccion PESADA de la UI (barras); limita su ritmo
        private int heavyGapMs = 40;                      // espera minima entre reconstrucciones pesadas; SE AUTOAJUSTA al coste real (ver NoteHeavy)
        // REJILLA DE DISCOS POR CELDAS: cada disco es su PROPIO quad con su textura (disco estatico + etiqueta),
        // subida UNA vez y cacheada por Folder. Hojear = elegir texturas + posiciones y dibujar quads: instantaneo
        // al revisitar, sin reconstruir ni resubir una textura a pantalla completa por pagina. Las subidas nuevas
        // se reparten por cuadro (presupuesto) para que un barrido de paginas nuevas no baje los fps.
        private const int MaxCells = 36;                  // dos paginas: pasando de pagina se ven la que sale y la que entra (PantallaVivaGpu.cs)
        private const int GridUploadBudget = 6;           // texturas de celda NUEVAS subidas por cuadro (barrer sin bajon)
        private sealed class CellTex { public IntPtr Tex; public int Bw, Bh; public float U = 1f, V = 1f; public Bitmap Bmp; public int Frame; public GpuCellLayout Layout; public bool HasCover; }
        private readonly Dictionary<string, CellTex> cellTexCache = new Dictionary<string, CellTex>();
        private readonly CellTex[] slotTex = new CellTex[MaxCells];   // celdas sin Folder (TOP): textura por slot, no se cachea entre paginas
        private readonly GpuGridCell[] gridBuf = new GpuGridCell[MaxCells];
        private readonly IntPtr[] gridTex = new IntPtr[MaxCells];     // textura resuelta por celda (subida ANTES de Begin, dibujada despues)
        private readonly GpuCellLayout[] gridLayouts = new GpuCellLayout[MaxCells];
        private readonly D3D9.Vtx[] atlasQuad = new D3D9.Vtx[42];
        public Func<GpuTrans> GetTrans;                   // portada de la transicion entrar/salir de disco (null = sin animacion)
        private IntPtr transTex; private Bitmap transLastBmp; private int transBw, transBh; private float transU = 1f, transV = 1f;
        public Func<GpuAurora> GetAurora;                 // aurora del horizonte (espectro); null o Has=false = nada
        public Func<GpuToast> GetToast;                  // aviso flotante (moneda, cancion agregada...)
        public Func<GpuWin> GetWin;                       // ventana del buscador abriendo/cerrando (null = sin animacion)
        private IntPtr winTex; private Bitmap winLastBmp; private int winBw, winBh; private float winU = 1f, winV = 1f;
        private IntPtr toastTex; private int toastVer = -1, toastBw, toastBh; private float toastU = 1f, toastV = 1f;
        private readonly D3D9.Vtx[] toastQuad = new D3D9.Vtx[6];
        private readonly D3D9.Vtx[] winQuad = new D3D9.Vtx[6];
        private readonly float[] gridU = new float[MaxCells], gridV = new float[MaxCells];
        private int gridN, cellFrame;
        public GpuGridFetch GetGridAhead;
        private readonly GpuGridCell[] aheadGrid = new GpuGridCell[36];
        private long gridUploadCost;
        private double lastAheadMs, lastFrameWork;
        private long GridWorkLimit { get { return CoverControl != null ? CoverControl.RemainingTicks : CargaTrabajo.PresupuestoGpu(CargaTrabajo.Procesadores, TopeFpsReal, lastFrameWork); } }
        private readonly D3D9.Vtx[] cellQuad = new D3D9.Vtx[6];
        private IntPtr contourTex; private Bitmap lastContour; private float contU = 1f, contV = 1f;
        private readonly D3D9.Vtx[] contourQuad = new D3D9.Vtx[6];
        // Cubos que GIRAN (seleccionado + relevo): se dibujan como GEOMETRIA en GPU desde la textura de la
        // portada. La portada se sube UNA vez (al cambiar), no en cada cuadro -> casi cero CPU por cubo.
        private IntPtr selTexA, selTexB, relTex, miniTex;          // texturas de portada: seleccionada A, fundido B (TOP), relevo, mini-cubo
        private Bitmap selImgA, selImgB, relImg, miniImg;          // ultima portada subida en cada ranura (para re-subir solo al cambiar)
        private float selUA = 1f, selVA = 1f, selUB = 1f, selVB = 1f, relU = 1f, relV = 1f, miniU = 1f, miniV = 1f;
        private int selBwA, selBhA, selBwB, selBhB, relBw, relBh, miniBw, miniBh;   // tamano ya subido en cada ranura (para reusar la textura)
        // Textura PRINCIPAL de la UI: su alto depende del modo (en navegacion es solo la franja superior,
        // ~7x mas barata de rehacer/subir que la pantalla completa). Se dibuja anclada ARRIBA, a su tamano real.
        private IntPtr uiTex; private int uiVer = -1; private float uiU = 1f, uiV = 1f; private int uiBmpW = -1, uiBmpH = -1;
        private readonly D3D9.Vtx[] uiQuad = new D3D9.Vtx[6];
        // Franja de ESTADO de abajo (sonando/tiempo/creditos/progreso) en SU PROPIA textura chica: el tic del
        // reloj (~2 cambios/seg con musica) ya no rehace ni re-sube la textura principal. Anclada ABAJO.
        private IntPtr uiBotTex; private int uiBotVer = -1; private float uiBotU = 1f, uiBotV = 1f; private int uiBotW = -1, uiBotH = -1;
        private readonly D3D9.Vtx[] uiBotQuad = new D3D9.Vtx[6];
        // LAS PIEZAS VIVAS: trozos chicos que van ENCIMA de la textura de UI y cambian por su cuenta (hoy, la
        // tarjeta y la tecla elegidas del buscador). Antes iban horneados dentro de la textura grande, asi que
        // mover el recuadro obligaba a rehacerla y re-subirla entera -7 MB a 1080p por pulsacion-. Cada pieza
        // tiene su textura chica y solo se re-sube cuando de verdad cambia.
        private const int MaxPiezas = 5;   // la 4: las tarjetas del buscador con portadas (BuscadorPortadasGpu.cs)
        private readonly IntPtr[] piezaTex = new IntPtr[MaxPiezas];
        private readonly int[] piezaVer = new int[MaxPiezas];
        private readonly int[] piezaW = new int[MaxPiezas], piezaH = new int[MaxPiezas];
        private readonly float[] piezaU = new float[MaxPiezas], piezaV = new float[MaxPiezas];
        private readonly D3D9.Vtx[] piezaQuad = new D3D9.Vtx[6];
        public Func<Bitmap> GetUiBot;                     // bitmap de la franja de estado (lo provee el form)
        public Func<int> GetUiBotVersion;                 // cambia cuando algo de la franja cambia (re-subir textura)
        // Contador de FPS (diagnostico) en su PROPIA textura pequeña: si fuera parte de la textura de UI
        // obligaria a rehacerla (pantalla completa) 1 vez/seg -> el buscador oscilaba. Aparte cuesta nada.
        private IntPtr fpsTex; private int fpsShown = int.MinValue; private float fpsU = 1f, fpsV = 1f; private int fpsTW, fpsTH;
        private readonly D3D9.Vtx[] fpsQuad = new D3D9.Vtx[6];
        public Rectangle AlbumRect;                       // donde van los cubos del album (coords del panel = pantalla); el form la fija
        public bool ShowAlbum = true;                     // navegacion: dibuja la rejilla de cubos + contorno. Buscador: false (lo cubre el buscador)
        public bool UiUrgent;                             // el form lo pone al cambiar de modo: rehacer la UI YA (sin esperar franja) para que la transicion sea atomica
        public Func<Bitmap> GetUi;                        // bitmap transparente con las barras (lo provee el form)
        public Func<int> GetUiVersion;                    // cambia cuando alguna barra cambia (re-subir textura)
        // DESTELLOS del boton pulsado (buscador): halos que se desvanecen. La GPU los dibuja como quads propios (no
        // en la textura de UI), asi el desvanecido NO obliga a re-subir la textura -> tecleo fluido y brillo suave.
        public struct UiGlow { public float Cx, Cy, R; public uint Argb; }
        public Func<UiGlow[], int> GetUiGlows;            // el form llena el buffer con los destellos activos y devuelve cuantos
        private readonly UiGlow[] uiGlowBuf = new UiGlow[10];   // 4 destellos de botones + 2 de letras + 2 del letrero MEJORADA, con holgura
        public int Fps;                                   // cuadros por segundo (diagnostico)
        public bool ShowFps;                              // dibujar el contador de FPS (oculto por defecto; el form lo alterna con Ctrl+F1)

        // ================= LAS PERILLAS DE LOS EFECTOS (Ctrl+F3 -> Imagen y rendimiento) =================
        // Cuatro cosas que se pueden bajar o apagar cuando un equipo no da para todo. El form las pone y este
        // panel se las pasa al cielo en cada cuadro. De fabrica estan todas puestas y el dibujo sale
        // exactamente igual que antes de que existieran.
        // Van por NIVEL (2 alto, 1 medio, 0 bajo), no por si/no: un si/no no sabe decir "menos nieblas".
        public int Nieblas = GpuStarfield.Alto;           // 2 = nebulosa + velo;  1 = solo la nebulosa;  0 = nada
        public int Fugaces = GpuStarfield.Alto;           // 2 = dos cada 7-16 s;  1 = una cada 15-30 s;  0 = ninguna
        public int RelevoPatron = GpuStarfield.Alto;      // 2 = patron nuevo cada minuto;  1 = cada tres;  0 = nunca
        public float Estrellas = 1f;                      // cuantas estrellas, de 0.1 a 1 (1 = las de siempre)

        // ================= LO QUE CUESTA ARMAR UN CUADRO, APARTE DE ESPERAR AL MONITOR =================
        // El contador de arriba (Fps) NO dice si a un equipo le sobra. El bucle se sincroniza con el refresco
        // de la pantalla, asi que en un equipo sobrado marca 60 tanto si el cuadro costo 2 ms como si costo
        // 15: lo unico que cambia es cuanto se espera en Present. Por eso se cronometra APARTE el TRABAJO -de
        // empezar el cuadro hasta justo ANTES de stars.End(), que es donde se espera al monitor-. Los dos
        // numeros juntos distinguen "va a 30 porque el monitor va a 30" de "va a 30 porque no da para mas",
        // que es lo unico que decide si hay que apagar algo.
        //
        // Es un promedio EXACTO (suma / cuadros) desde la ultima puesta a cero, no una media que arrastra:
        // asi el medidor pide cero, espera un segundo y lee lo de ese segundo, sin restos del anterior.
        private double msTrabSuma; private int msTrabN;
        public double MsTrabajo { get { return (msTrabN > 0) ? (msTrabSuma / msTrabN) : 0.0; } }
        public int CuadrosMedidos { get { return msTrabN; } }
        public int FallosMedidos { get; private set; }
        public void ReiniciaMedida() { msTrabSuma = 0.0; msTrabN = 0; FallosMedidos = 0; Fps = 0; frames = 0; fpsTick = Environment.TickCount; if (CoverControl != null) CoverControl.ResetMeasure(); }
        public float Dim = 1f;                            // atenuacion del starfield mientras se navega con musica (el form la anima; 1 = normal)
        public float WallOpacity = 1f;
        public bool ProgresoFluido = true;
        public Func<GpuProgress> GetProgress;
        public bool IdleExpandTarget;
        public Func<Bitmap> GetIdlePanel;
        public int IdlePanelVersion;
        public Rectangle IdlePanelDirty;
        private int idleUploadedVersion = -1;
        private float idleBlend;
        private double idleClock;
        private IntPtr idleTex;
        private Bitmap idleLastBmp;
        private int idleW, idleH;
        private float idleU, idleV;
        private float IdleRise { get { return Math.Max(0, idleH - uiBotH) * idleBlend; } }
        internal static int IdlePanelHeight(int screenH) { return Math.Min(screenH, Math.Max(220, Math.Min(340, (int)(screenH * .32f)))); }
        // Coordenada calculada al hornear el texto; no medir fuentes por cuadro.
        public float IdleProgressStart;
        internal static Rectangle IdleBadgeRect(int w, int h)
        {
            int x = IdleCoverRect(w, h).Right + 20;
            return new Rectangle(x, h - 73, Math.Max(30, Math.Min(156, (int)(w * .52f) - x - 24)), 30);
        }
        internal RectangleF IdleBadgeGlow(RectangleF normal)
        {
            Rectangle badge = IdleBadgeRectCorrido(Width, idleH, IdleCorre);   // corrida si asoma el vinilo (SuenaVivoGpu.cs)
            float y = StatusTopY + badge.Y;
            return new RectangleF(normal.X + (badge.X - normal.X) * idleBlend,
                normal.Y + (y - normal.Y) * idleBlend,
                normal.Width + (badge.Width - normal.Width) * idleBlend,
                normal.Height + (badge.Height - normal.Height) * idleBlend);
        }
        internal static Rectangle IdleCoverRect(int w, int h)
        {
            int side = Math.Max(32, Math.Min(h - 76, (int)(w * .22f)));
            return new Rectangle(24, h - side - 8, side, side);
        }
        internal static float StepIdle(float actual, bool target, double dt)
        {
            dt = Math.Max(0, Math.Min(.1, dt));
            float end = target ? 1f : 0f;
            float next = actual + (end - actual) * (float)(1 - Math.Exp(-dt * (target ? 3.5 : 9)));
            return Math.Abs(next - end) < .001f ? end : next;
        }
        private object progressKey;
        private double progressDraw = -1, progressClock;
        private readonly D3D9.Vtx[] progressQuad = new D3D9.Vtx[6];
        public bool VinetaSuave = true, HaloSeleccion = true, EstrellasFinas = true, OndasSuaves = true;
        private IntPtr vinetaTex;
        private float vinetaU, vinetaV;
        private int vinetaRetry;
        private readonly D3D9.Vtx[] adornoQuad = new D3D9.Vtx[6];
        public Func<float> GetPulse;                      // el form provee el nivel de audio (0..1)
        public GpuGridFetch GetGrid;                      // el form llena el buffer con las celdas de la pagina actual (rejilla por quads)
        public int PiezasN;                               // cuantas piezas vivas hay ahora (0 = ninguna)
        public Func<int, Bitmap> GetPieza;                // la imagen chica de cada pieza viva
        public Func<int, Rectangle> GetPiezaRect;         // donde va, en coords de pantalla
        public Func<int, int> GetPiezaVersion;            // sube cuando su imagen cambia (la prepara por dentro)
        public Func<Bitmap> GetContour;                   // sprite del contorno de seleccion
        public Func<Rectangle> GetContourRect;            // donde va el contorno (celda seleccionada), en coords del panel
        public Func<GpuCubeState> GetCubeState;           // celdas/angulos/portadas de los cubos que giran (seleccionado + relevo)
        public int ThrottleMs;                            // >0 = a lo mas un cuadro cada tantos ms (con video sonando: ~30 fps)
        // TOPE DE CUADROS POR SEGUNDO. Sin tope, este bucle dibujaba TAN RAPIDO COMO PUDIERA: en un equipo con
        // tarjeta decente eso son mil y pico cuadros por segundo... de los que la pantalla solo ENSEÑA sesenta.
        // Los otros mil se calculaban y se tiraban a la basura, y mientras se tiraban un nucleo entero se
        // quedaba clavado al 100%: sin aire para el sonido, para el buscador ni para los repasos de fondo. De
        // ahi los tirones. Y de ahi tambien lo que se veia raro al medirlo: al encender mas efectos BAJABAN los
        // cuadros por segundo y la GRAFICA no subia su uso, porque la grafica nunca fue el cuello -iba sobrada,
        // esperando- y el que se ahogaba era el procesador armando cuadros que nadie iba a ver nunca.
        //
        // Ahora se dibuja un 10% POR ENCIMA de lo que refresca la pantalla. Ese 10% de mas no es capricho: si se
        // pidiera justo lo mismo que refresca, de vez en cuando un cuadro llegaria un pelo tarde y ese barrido
        // de pantalla tendria que repetir el anterior (un salto que SE VE). Con un poco de sobra, cada barrido
        // tiene siempre un cuadro nuevo esperando. Se ve exactamente igual que antes y sobra procesador.
        //
        // (23-09-2026) Ese 10 % de mas tenia su propio salto: con 66 cuadros para 60 refrescos, cada ~10 refrescos uno
        // no llega a verse y todo aparece avanzado el doble. Con el compositor de Windows el bucle ya no va por su
        // cuenta: dibuja uno por refresco, empezado justo despues de cada uno (OnIdle y RelojPantallaGpu.cs), y el tope
        // queda de techo. Sin compositor (XP, o el tema basico) sigue valiendo lo de arriba.
        public int TopeFps;                               // 0 = automatico (segun la pantalla); >0 = lo manda el config
        private int topeMs = -1;                          // ms minimos entre cuadros, ya resueltos (-1 = aun sin resolver)
        private const int VREFRESH = 116;                 // GetDeviceCaps: veces por segundo que refresca la pantalla
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);

        // Cada cuantos ms toca un cuadro, como poco. Windows contesta 0 o 1 al preguntarle el refresco cuando
        // no lo sabe (pantalla vieja, escritorio remoto, maquina virtual): ahi se toman 60, que es lo de siempre.
        private int TopeMs()
        {
            if (topeMs >= 0) return topeMs;
            int fps = TopeFps;
            if (fps <= 0)
            {
                int hz = 0;
                try
                {
                    IntPtr dc = GetDC(IntPtr.Zero);
                    if (dc != IntPtr.Zero)
                    {
                        try { hz = GetDeviceCaps(dc, VREFRESH); }
                        finally { ReleaseDC(IntPtr.Zero, dc); }
                    }
                }
                catch { }
                if (hz < 24 || hz > 400) hz = 60;
                fps = hz + hz / 10;   // el 10% de sobra
            }
            if (fps < 24) fps = 24; else if (fps > 480) fps = 480;
            topeMs = 1000 / fps; if (topeMs < 1) topeMs = 1;
            return topeMs;
        }
        public int TopeFpsReal { get { int m = TopeMs(); return (m > 0) ? 1000 / m : 0; } }   // lo que quedo, para el contador
        public void OlvidaTope() { topeMs = -1; }   // el tope se resuelve una vez y se guarda: al cambiarlo a mano hay que volver a resolverlo
        // RELOJ FINO para el limitador. Environment.TickCount avanza a SALTOS de ~15.6 ms en Windows, asi
        // que pidiendole "un cuadro cada 33 ms" solo podia contestar 46.8 (tres saltos): 21 cuadros por
        // segundo en vez de 30, que es lo que se veia. Este reloj mide de verdad, en microsegundos.
        private static readonly System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        internal static double AhoraMs() { return reloj.Elapsed.TotalMilliseconds; }
        private double proxCuadroMs;                      // cuando TOCA el proximo cuadro (ms del reloj fino)
        private double vistoMs = -1;                      // cuando se VERA el que se esta dibujando (-1 = no se sabe)
        public uint SkyTop = 0xFF06081A, SkyMid = 0xFF101C3A, SkyBot = 0xFF1A2C5A;   // el form los setea por genero (3 paradas)

        // Bucle de render por Application.Idle (patron estandar para D3D en WinForms): dibuja tan rapido como
        // permita el vsync (60 FPS) mientras no haya mensajes pendientes; al llegar un mensaje (tecla, timer)
        // sale, lo procesa y vuelve. El Timer de WinForms era impreciso (se quedaba en ~40 fps).
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage { public IntPtr Hwnd; public uint Msg; public IntPtr W; public IntPtr L; public uint Time; public Point Pt; }
        [DllImport("user32.dll")] private static extern int PeekMessage(out NativeMessage msg, IntPtr hWnd, uint min, uint max, uint remove);
        private static bool AppIdle() { NativeMessage m; return PeekMessage(out m, IntPtr.Zero, 0, 0, 0) == 0; }   // 0 = PM_NOREMOVE

        public GpuPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);   // D3D9 maneja sus pixeles: nada de doble-buffer GDI
            trasCielo = delegate { DibujaNieblas(); if (auCuadro.Has) DrawAurora(auCuadro); DibujaRuido(); };
            trasEstrellas = delegate { DibujaLuna(); };
        }

        private static int MilisegundosEspera(double restante)
        {
            if (!(restante > 0.6)) return 0;
            if (restante >= 8.6) return 8;
            return Math.Max(1, (int)(restante - 0.6));
        }
        // La espera no consume mensajes ni bombea eventos de forma reentrante.
        [DllImport("user32.dll")]
        private static extern uint MsgWaitForMultipleObjectsEx(uint count, IntPtr handles, uint ms, uint mask, uint flags);
        internal static bool EsperaMensajes(int ms)
        {
            ms = Math.Max(1, Math.Min(8, ms));
            try
            {
                // QS_ALLINPUT / MWMO_INPUTAVAILABLE: despierta incluso si PeekMessage ya vio la entrada.
                uint r = MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, (uint)ms, 0x04FF, 0x0004);
                if (r != 0xFFFFFFFF) return r == 0;
            }
            catch { }
            System.Threading.Thread.Sleep(1);
            return false;
        }

        private void OnIdle(object sender, EventArgs e)
        {
            // SIEMPRE hay tope (ver TopeMs): lo que refresca la pantalla y un poco mas. Con video A LA VISTA
            // el panel se puede limitar TODAVIA MAS (ThrottleMs ~33 = 30 fps) para dejarle equipo al video.
            // La espera se interrumpe al llegar mensajes; al despertar, AppIdle entrega el control a WinForms.
            // El plazo es acotado y no promete precision de 1 ms: la planificacion depende de Windows.
            //
            // DOS COSAS, y las dos hacen falta:
            //  - Se mide con el RELOJ FINO. Con TickCount, que salta de ~15.6 en ~15.6 ms, pedir un cuadro
            //    cada 33 ms daba uno cada 46.8: 21 cuadros por segundo en vez de 30. Eso era el fallo.
            //  - Y el proximo cuadro se agenda sobre el OBJETIVO anterior, no sobre el momento en que se
            //    dibujo. Windows no despierta cuando se le pide, despierta cuando puede; agendando sobre el
            //    objetivo, un cuadro que llega tarde NO arrastra su retraso al siguiente y el promedio sale
            //    el que se pidio, aunque el sueno de 1 ms se pase. Si se acumula mas de una vuelta de
            //    retraso -la maquina no da mas de si- se resincroniza en vez de intentar recuperarla.
            while (stars != null && stars.Ok && Visible && AppIdle())
            {
                int th = TopeMs();                        // el tope de la pantalla
                if (ThrottleMs > th) th = ThrottleMs;      // con video a la vista se pide ir MAS lento todavia
                double ahora = AhoraMs();
                if (ahora + 0.6 < proxCuadroMs) { EsperaMensajes(MilisegundosEspera(proxCuadroMs - ahora)); continue; }
                AgendaCuadro(ahora, th);
                RenderFrame();
                vistoMs = -1;   // un cuadro pedido fuera de este bucle (WM_PAINT, el medidor) no sabe cuando se vera
            }
        }

        // Cuando toca el cuadro siguiente y cuando se vera el que empieza ahora. Con la rejilla de refrescos de Windows:
        // un cuadro por refresco, empezado justo despues de uno, y la estrella colocada para cuando se vera
        // (RelojPantallaGpu.cs). Sin ella, como siempre: 'th' despues del objetivo anterior, y sin saber cuando se vera.
        private void AgendaCuadro(double ahora, int th)
        {
            double refresco, periodo, sig = double.NaN;
            if (RelojPantalla.Rejilla(ahora, out refresco, out periodo)) sig = RelojPantalla.Siguiente(ahora, th, refresco, periodo);
            vistoMs = -1;
            if (!double.IsNaN(sig)) { proxCuadroMs = sig; vistoMs = RelojPantalla.Despues(ahora, refresco, periodo); return; }
            proxCuadroMs += th;
            if (proxCuadroMs < ahora) proxCuadroMs = ahora + th;
        }

        public bool StartGpu()
        {
            if (stars == null || !stars.Ok)
            {
                if (stars != null) stars.Dispose();
                stars = new GpuStarfield(this.Handle, Width, Height, 4321);
                if (!stars.Ok) { stars.Dispose(); stars = null; return false; }
                cellTextureBudget = AdaptiveCellBudget(stars.EstimatedTextureMemory, Form1.CacheCeldasMb(), IntPtr.Size);
            }
            if (!idleHooked) { Application.Idle += OnIdle; idleHooked = true; }
            return true;
        }

        public void StopGpu()
        {
            CancelaPreparacionNatural();
            TrimLod(true);
            SueltaCintasGpu();
            ClearPaletteFade(); paletteKey = 0;
            TrimRecentCovers(true);
            if (idleHooked) { Application.Idle -= OnIdle; idleHooked = false; }
            ReleaseCellTex();   // libera todas las texturas de celda (cache por Folder + slots TOP)
            if (contourTex != IntPtr.Zero) { D3D9.Release(contourTex); contourTex = IntPtr.Zero; }
            if (selTexA != IntPtr.Zero) { D3D9.Release(selTexA); selTexA = IntPtr.Zero; }
            if (selTexB != IntPtr.Zero) { D3D9.Release(selTexB); selTexB = IntPtr.Zero; }
            if (relTex != IntPtr.Zero) { D3D9.Release(relTex); relTex = IntPtr.Zero; }
            if (miniTex != IntPtr.Zero) { D3D9.Release(miniTex); miniTex = IntPtr.Zero; }
            if (transTex != IntPtr.Zero) { D3D9.Release(transTex); transTex = IntPtr.Zero; transLastBmp = null; }
            if (winTex != IntPtr.Zero) { D3D9.Release(winTex); winTex = IntPtr.Zero; winLastBmp = null; }
            SueltaPiezasVentana();   // las piezas que se llevo la ventana del buscador con portadas (BuscadorPortadasGpu.cs)
            if (toastTex != IntPtr.Zero) { D3D9.Release(toastTex); toastTex = IntPtr.Zero; toastVer = -1; }
            SueltaPantallaViva();   // el letrero de la elegida y la ficha rapida
            SueltaDiscoVivo();      // el vinilo y el boleto
            SueltaSuenaVivo();      // los avisos, la invitacion, la moneda y lo del panel de reposo
            SueltaAtraccion();      // las fichas del escaparate, sus mensajes y la lista de la noche
            if (uiTex != IntPtr.Zero) { D3D9.Release(uiTex); uiTex = IntPtr.Zero; }
            if (hazTex != IntPtr.Zero) { D3D9.Release(hazTex); hazTex = IntPtr.Zero; }
            if (nebTex != IntPtr.Zero) { D3D9.Release(nebTex); nebTex = IntPtr.Zero; }
            if (vinetaTex != IntPtr.Zero) { D3D9.Release(vinetaTex); vinetaTex = IntPtr.Zero; }
            if (idleTex != IntPtr.Zero) { D3D9.Release(idleTex); idleTex = IntPtr.Zero; }
            idleLastBmp = null; idleBlend = 0; idleClock = 0; idleUploadedVersion = -1;
            idleNaturalVelocity = 0; naturalLightReady = false;
            vinetaRetry = 0;
            if (ruidoTex != IntPtr.Zero) { D3D9.Release(ruidoTex); ruidoTex = IntPtr.Zero; ruTW = ruTH = 0; }
            ResetStarEchoes();
            if (lunaTex != IntPtr.Zero) { D3D9.Release(lunaTex); lunaTex = IntPtr.Zero; luTW = luTH = 0; lunaDe = -1; }
            if (uiBotTex != IntPtr.Zero) { D3D9.Release(uiBotTex); uiBotTex = IntPtr.Zero; }
            for (int i = 0; i < MaxPiezas; i++)
            {
                if (piezaTex[i] != IntPtr.Zero) { D3D9.Release(piezaTex[i]); piezaTex[i] = IntPtr.Zero; }
                piezaVer[i] = -1; piezaW[i] = piezaH[i] = -1;
            }
            if (fpsTex != IntPtr.Zero) { D3D9.Release(fpsTex); fpsTex = IntPtr.Zero; }
            lastContour = null; selImgA = selImgB = relImg = miniImg = null; uiVer = -1; uiBotVer = -1; fpsShown = int.MinValue;
            selBwA = selBhA = selBwB = selBhB = relBw = relBh = miniBw = miniBh = transBw = transBh = winBw = winBh = 0;   // sin textura no hay tamano subido
            uiBmpW = uiBmpH = -1; uiBotW = uiBotH = -1;   // fuerza recrear las texturas reutilizables tras reiniciar el device
            if (stars != null) { stars.Dispose(); stars = null; }
        }

        // Libera y olvida todas las texturas de celda (cache por Folder + fallback por slot). Al reiniciar el
        // device o apagar la GPU: sus IntPtr dejan de ser validos.
        private void ReleaseCellTex()
        {
            foreach (CellTex ct in cellTexCache.Values) if (ct.Tex != IntPtr.Zero) D3D9.Release(ct.Tex);
            cellTexCache.Clear();
            ReleaseNativeCovers(); protectedAhead.Clear(); cellBudgetChecked = -2000;
            cellBytesDirty = true;
            for (int i = 0; i < slotTex.Length; i++) { CellTex ct = slotTex[i]; if (ct != null && ct.Tex != IntPtr.Zero) D3D9.Release(ct.Tex); slotTex[i] = null; }
            for (int i = 0; i < gridTex.Length; i++) gridTex[i] = IntPtr.Zero;
            gridN = 0;
        }

        // Sube la portada como textura SOLO si cambio respecto a la ranura (referencia distinta). Asi el
        // cubo girando no sube nada en la mayoria de cuadros: solo al navegar o al ciclar un disco TOP.
        // Sube la portada de una ANIMACION a su textura. Antes DESTRUIA la textura y CREABA una nueva cada vez
        // que cambiaba la portada, y crear una textura es una peticion al driver que no siempre contesta rapido:
        // ESE era el tiron de la primera vez que se veia cada animacion (y de cada cambio de portada despues).
        // Ahora, si la portada nueva mide lo mismo que la anterior -que es SIEMPRE, todas se hornean del mismo
        // tamano- se reaprovecha la textura y solo se re-copian los pixeles. La rejilla de discos ya lo hacia asi;
        // las animaciones se habian quedado atras.
        private void EnsureCover(ref IntPtr tex, ref Bitmap last, ref int bw, ref int bh, ref float u, ref float v, Bitmap cur)
        {
            if (ReferenceEquals(cur, last) && (cur == null || tex != IntPtr.Zero)) return;
            if (cur == null)
            {
                if (tex != IntPtr.Zero) { D3D9.Release(tex); tex = IntPtr.Zero; }
                bw = bh = 0; last = null; return;
            }
            tex = Upload(tex, ref bw, ref bh, cur, out u, out v);   // mismo tamano: reusa. Distinto: recrea (raro).
            last = cur;
        }

        private bool NoPresentado() { FallosMedidos++; Fps = 0; frames = 0; return false; }
        private bool RenderFrame()
        {
            VigiaPantalla.Hace("un cuadro de la tarjeta");
            if (stars == null || !stars.Ok) return NoPresentado();
            double msTrab0 = AhoraMs();   // arranca el cronometro del TRABAJO de este cuadro (ver MsTrabajo)
            stars.SetEfectos(Estrellas, Fugaces, RelevoPatron, Width, Height);   // las perillas de Ctrl+F3
            // Si la ventana cambio de tamano (resolucion de Windows, o el panel), el buffer de dibujo tiene
            // que cambiar con ella. Se hace AQUI, antes de abrir el cuadro: resetear el dispositivo con la
            // escena ya abierta no es valido en Direct3D 9.
            stars.Redimensiona(Width, Height);
            stars.FondoNatural = FondoNatural;
            stars.SetSky(CieloEstilo(SkyTop, 0, FondoNatural), CieloEstilo(SkyMid, 1, FondoNatural), CieloEstilo(SkyBot, 2, FondoNatural));
            // TRABAJO PESADO acotado: rehacer las barras (UI, bitmap a pantalla completa) cuesta; se limita a UNA
            // por franja (heavyGapMs, que SE AUTOAJUSTA al coste real). La rejilla de discos ya NO es trabajo
            // pesado: cada disco es su propio quad con textura cacheada (rejilla por celdas, mas abajo).
            int nowMs = Environment.TickCount;
            bool canHeavy = (nowMs - lastHeavyMs) >= heavyGapMs;
            // REJILLA POR CELDAS: pedimos las celdas de la pagina y aseguramos (subimos si falta) su textura ANTES
            // de Begin, repartiendo las subidas NUEVAS por cuadro (presupuesto) para no bajar los fps al barrer.
            gridN = 0;
            if (CoverControl != null) CoverControl.Begin(AhoraMs(), TopeFpsReal);
            if (ShowAlbum && GetGrid != null)
            {
                gridN = GetGrid(gridBuf);
                cellFrame++;
                RefreshCellBudget();
                if (CoverControl != null) CoverControl.PendingUpload = false;
                int upBudget = CoverControl != null ? 32 : GridUploadBudget;
                long uploadStart = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < gridN; i++)
                {
                    if (CoverControl != null && gridBuf[i].Slot < 18 && gridBuf[i].NeedsCover)
                        CoverControl.VisitCover(gridBuf[i].Key, PreparedCover(gridBuf[i]));
                    nativeGridTex[i] = PrepareNativeCover(gridBuf[i], ref upBudget, true, out nativeGridU[i], out nativeGridV[i]);
                    IntPtr tex; float u, v;
                    if (CoverControl != null && !CoverControl.CanStart(gridUploadCost, true, false)) upBudget = 0;
                    int beforeBudget = upBudget;
                    long oneStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (gridBuf[i].Bmp != null && EnsureCellTex(gridBuf[i], ref upBudget, out tex, out u, out v))
                    { gridTex[i] = tex; gridU[i] = u; gridV[i] = v; gridLayouts[i] = UploadedCellLayout(gridBuf[i]); }
                    else gridTex[i] = IntPtr.Zero;   // aun sin hornear o sin presupuesto de subida: se salta este cuadro (aparece luego)
                    if (upBudget < beforeBudget)
                    {
                        long spent = System.Diagnostics.Stopwatch.GetTimestamp() - oneStart;
                        gridUploadCost = CargaTrabajo.AprendeCoste(gridUploadCost, spent);
                        if (CoverControl != null && beforeBudget - upBudget == 1) CoverControl.Spend(spent);
                    }
                    PrepareLod(i, gridBuf[i].Direct ? NativeCoverReady(gridBuf[i]) : gridTex[i] != IntPtr.Zero && UploadedCover(gridBuf[i]));
                    if (CoverControl != null && gridBuf[i].Direct && gridBuf[i].FullCover != null && !NativeCoverReady(gridBuf[i])) CoverControl.PendingUpload = true;
                    if (CoverControl != null && gridBuf[i].Bmp != null)
                    {
                        CellTex pending;
                        if (gridBuf[i].Key != null && (!cellTexCache.TryGetValue(gridBuf[i].Key, out pending)
                            || pending.Tex == IntPtr.Zero || pending.Bmp != gridBuf[i].Bmp)) CoverControl.PendingUpload = true;
                    }
                    if (CoverControl == null && upBudget < GridUploadBudget && upBudget > 0 &&
                        !CargaTrabajo.PuedeEmpezar(System.Diagnostics.Stopwatch.GetTimestamp() - uploadStart, gridUploadCost, GridWorkLimit, false))
                        upBudget = 0; // las texturas cacheadas siguen disponibles con presupuesto cero
                }
                EvictCellTex();
                TrimLod(false);
                if (CoverControl != null && gridN > 0)
                {
                    bool ready = true;
                    for (int i = 0; i < gridN && gridBuf[i].Slot < 18; i++)
                        if (gridTex[i] == IntPtr.Zero || (gridBuf[i].NeedsCover && !UploadedCover(gridBuf[i]))) ready = false;
                    CoverControl.FinishPage(ready, AhoraMs());
                }
            }
            // Contorno de seleccion: sprite estatico (se sube cuando cambia el bitmap, p.ej. al cambiar tamano).
            if (GetContour != null)
            {
                Bitmap cb = GetContour();
                if (cb != lastContour || (cb != null && contourTex == IntPtr.Zero))
                {
                    if (contourTex != IntPtr.Zero) { D3D9.Release(contourTex); contourTex = IntPtr.Zero; }
                    if (cb != null) contourTex = stars.CreateOverlay(cb, out contU, out contV);
                    lastContour = cb;
                }
            }
            // Cubos que GIRAN (seleccionado + relevo): suben la PORTADA como textura SOLO cuando cambia
            // (al navegar o al ciclar un disco TOP), no en cada cuadro. El giro luego es solo geometria.
            BeginCubeFrame();
            GpuCubeState cs = (GetCubeState != null) ? GetCubeState() : new GpuCubeState();
            SampleCubePose(ref cs.Sel, 0); SampleCubePose(ref cs.Relay, 1); SampleCubePose(ref cs.Mini, 2);
            if (ShowAlbum && cs.Sel.CoverA == null)
                for (int i = 0; i < gridN; i++) if (gridBuf[i].Sel && gridBuf[i].Preview != null) { cs.Sel.CoverA = gridBuf[i].Preview; break; }
            EnsureRecentCover(ref selTexA, ref selImgA, ref selBwA, ref selBhA, ref selUA, ref selVA, cs.Sel.CoverA);
            EnsureRecentCover(ref selTexB, ref selImgB, ref selBwB, ref selBhB, ref selUB, ref selVB, cs.Sel.CoverB);
            EnsureRecentCover(ref relTex, ref relImg, ref relBw, ref relBh, ref relU, ref relV, cs.Relay.CoverA);
            EnsureCover(ref miniTex, ref miniImg, ref miniBw, ref miniBh, ref miniU, ref miniV, cs.Mini.CoverA);   // mini-cubo de la barra inferior
            AplicaEnfoque(ref cs);         // portada enfocada: agranda el cubo elegido y apaga un poco las demas (Ctrl+F3 -> Pantalla)
            double idleNow = AhoraMs();
            double idleDt = idleClock == 0 ? 0 : (idleNow - idleClock) / 1000.0; idleClock = idleNow;
            if ((IdleExpandTarget || idleBlend > 0) && GetIdlePanel != null)
            {
                Bitmap ib = GetIdlePanel();
                if (ib != null)
                {
                    if (ReferenceEquals(ib, idleLastBmp) && idleTex != IntPtr.Zero && IdlePanelVersion != idleUploadedVersion)
                    {
                        if (IdlePanelDirty.IsEmpty || !stars.UpdateOverlayRegion(idleTex, ib, IdlePanelDirty))
                            idleLastBmp = null; // fallo regional: reintentar la subida completa habitual
                    }
                    EnsureCover(ref idleTex, ref idleLastBmp, ref idleW, ref idleH, ref idleU, ref idleV, ib);
                    if (idleTex != IntPtr.Zero) idleUploadedVersion = IdlePanelVersion;
                }
            }
            if (FondoNatural)
                idleBlend = PasoReposoNatural(idleBlend, IdleExpandTarget && idleTex != IntPtr.Zero, ref idleNaturalVelocity, idleDt);
            else { idleNaturalVelocity = 0; idleBlend = StepIdle(idleBlend, IdleExpandTarget && idleTex != IntPtr.Zero, idleDt); }
            // ANIMACIONES: sus texturas TAMBIEN se aseguran AQUI, antes de Begin. Crear o bloquear una textura
            // con la escena ya abierta no es valido en Direct3D 9: fallaba en silencio y la portada que viajaba
            // entre cubos no llegaba a dibujarse NUNCA (la del buscador al abrir si, porque esa reusa la textura
            // de la UI, que ya venia subida de antes). Aqui se resuelven; abajo solo se dibujan.
            GpuTrans tr = (GetTrans != null) ? GetTrans() : new GpuTrans();
            SampleCubeFlight(ref tr);
            if (tr.Has && tr.Bmp != null) EnsureCover(ref transTex, ref transLastBmp, ref transBw, ref transBh, ref transU, ref transV, tr.Bmp);
            GpuWin gw = (GetWin != null) ? GetWin() : new GpuWin();
            if (gw.Has && gw.Bmp != null) EnsureCover(ref winTex, ref winLastBmp, ref winBw, ref winBh, ref winU, ref winV, gw.Bmp);
            GpuToast tt = (GetToast != null) ? GetToast() : new GpuToast();
            if (tt.Has && tt.Bmp != null && (tt.Ver != toastVer || toastTex == IntPtr.Zero))   // el bitmap se REUSA (mismo objeto), por eso manda la version
            { toastTex = Upload(toastTex, ref toastBw, ref toastBh, tt.Bmp, out toastU, out toastV); toastVer = tt.Ver; }
            PreparaPantallaViva();         // el letrero de la elegida, la ficha rapida y el empujon del cielo al pasar de pagina
            PreparaDiscoVivo();            // dentro del disco: el vinilo, las barritas de la que suena y el boleto que vuela
            PreparaSuenaVivo();            // los avisos, la invitacion, la moneda y el panel de reposo en grande
            PreparaAtraccion();            // el escaparate y lo mas pedido esta noche (AtraccionGpu.cs)
            // Barras (UI) a textura: se re-sube solo cuando cambia algun texto/estado (firma del form), limitada a
            // UNA por franja (heavyGapMs). UiUrgent: al CAMBIAR DE MODO (p.ej. cerrar el buscador) hay que rehacerla
            // en ESTE mismo cuadro, sin esperar franja: si no, se veria la rejilla por los huecos del teclado un instante.
            PreparePalette();
            bool uiUrgent = UiUrgent; UiUrgent = false;
            if (GetUi != null && GetUiVersion != null && (uiTex == IntPtr.Zero || uiUrgent || canHeavy))
            {
                int t0 = Environment.TickCount;
                int uv = GetUiVersion();   // puede rehacer el bitmap de la UI por dentro
                if (uv != uiVer || uiTex == IntPtr.Zero)
                {
                    Bitmap ub = GetUi();
                    SubeUi(ub, gw);   // reutiliza la textura, y en el buscador sube solo lo que cambio (BuscadorLigeroGpu.cs)
                    uiVer = uv;
                    NoteHeavy(t0);   // mide el coste real y ajusta la espera (la UI cacheada es barata -> franja corta)
                    uiTeselas = null; uiSubidaMs = AhoraMs();   // imagen nueva: sus teselas, cuando este quieta (DibujoLigeroGpu.cs)
                }
                else if (uiTeselas == null) uiTeselas = TeselasQuietas(GetUi(), uiBmpW, uiBmpH, uiSubidaMs);
            }
            // Franja de ESTADO de abajo: textura chica aparte. El tic del reloj/progreso (~2/seg con musica)
            // rehace y sube SOLO esta tira (~400KB), no la textura principal. Misma franja de trabajo pesado.
            if (GetUiBot != null && GetUiBotVersion != null && (uiBotTex == IntPtr.Zero || uiUrgent || canHeavy))
            {
                int bv = GetUiBotVersion();   // puede rehacer el bitmap de la tira por dentro
                if (bv != uiBotVer || uiBotTex == IntPtr.Zero)
                {
                    Bitmap bb = GetUiBot();
                    if (bb != null) SubeUiBot(bb);   // el trozo que cambio, o entera (FranjaLigeraGpu.cs)
                    uiBotVer = bv;
                    uiBotTeselas = null; uiBotSubidaMs = AhoraMs();   // imagen nueva: sus teselas, cuando este quieta (DibujoLigeroGpu.cs)
                }
                else if (uiBotTeselas == null) uiBotTeselas = TeselasQuietas(GetUiBot(), uiBotW, uiBotH, uiBotSubidaMs);
            }
            // Las piezas vivas. Se preguntan SIEMPRE (no solo cuando toca trabajo pesado): son diminutas, y
            // son justo lo que tiene que responder al instante cuando alguien se mueve entre casillas.
            if (GetPiezaVersion != null && GetPieza != null)
                for (int i = 0; i < PiezasN && i < MaxPiezas; i++)
                {
                    int pv = GetPiezaVersion(i);   // prepara la imagen por dentro
                    if (pv == piezaVer[i] && (piezaTex[i] != IntPtr.Zero || piezaW[i] == -1)) continue;
                    Bitmap pb = GetPieza(i);
                    if (pb != null) SubePieza(i, pb);
                    else { if (piezaTex[i] != IntPtr.Zero) D3D9.Release(piezaTex[i]); piezaTex[i] = IntPtr.Zero; piezaW[i] = piezaH[i] = -1; }
                    piezaVer[i] = pv;
                }
            PreparaCintasTexto();
            EnsureNieblas();               // la via lactea y las nebulosas (una vez por tamano de pantalla)
            EnsureVineta();
            EnsureLuna(); // ninguna reserva/subida de la luna dentro de Begin
            stars.EstrellasFinas = EstrellasFinas;
            if (ShowFps) EnsureFpsTex();   // crea/actualiza la textura del contador ANTES de Begin (solo si se pide, Ctrl+F1)
            CapturaLuzNatural(idleDt);
            stars.SetDim(SceneDim);       // luz interpolada solo en Natural; el cielo conserva su perfil
            float pulseCrudo = (GetPulse != null) ? GetPulse() : 0f;
            float pulse = pulseCrudo * SceneDim;   // el beat tambien baja mientras esta atenuado (la deriva NO: ver Begin)
            // La aurora se dibuja DENTRO de Begin, justo despues del cielo y antes de las estrellas.
            auCuadro = (GetAurora != null) ? GetAurora() : new GpuAurora();
            // Se engancha SIEMPRE: aqui van la via lactea y las nebulosas, haya aurora o no.
            auCuadro.Has = auCuadro.Has && auCuadro.B != null && auCuadro.B.Length >= 8;
            auCuadro.BaseY = OndasUnidas && uiBotH > 0
                ? AuroraBaseAtStatus(Height, uiBotH, idleH, idleBlend)
                : auCuadro.BaseY - IdleRise;
            UpdateMoonMusic(pulseCrudo);
            AdvanceCoverPalette(AhoraMs());
            stars.TrasCielo = trasCielo;
            stars.TrasEstrellas = trasEstrellas;
            // Dispositivo perdido (sesion bloqueada, cambio de pantalla...): sin este respiro, el bucle de
            // Application.Idle reintentaria a TODA MAQUINA y clavaria un nucleo entero hasta recuperarlo.
            double aheadNow = AhoraMs();
            double uploadMs = gridUploadCost * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (ShowAlbum && GetGridAhead != null && !UiUrgent &&
                aheadNow - lastAheadMs >= (CargaTrabajo.Procesadores <= 2 ? 40 : 20) &&
                (CoverControl != null
                    ? CoverControl.CanStart(gridUploadCost, false, false) && aheadNow - msTrab0 < 28
                    : aheadNow - msTrab0 + uploadMs < GridWorkLimit * 1000.0 / System.Diagnostics.Stopwatch.Frequency &&
                      lastFrameWork + uploadMs < 850.0 / Math.Max(1, TopeFpsReal)))
            {
                lastAheadMs = aheadNow;
                bool ready = gridN > 0;
                for (int i = 0; i < gridN; i++) if (gridTex[i] == IntPtr.Zero) ready = false;
                if (ready)
                {
                    double margin = CoverControl != null ? GridWorkLimit * 1000.0 / System.Diagnostics.Stopwatch.Frequency
                        : Math.Min(GridWorkLimit * 1000.0 / System.Diagnostics.Stopwatch.Frequency - (AhoraMs()-msTrab0),
                          850.0 / Math.Max(1, TopeFpsReal) - lastFrameWork);
                    aheadAvailableTicks = Math.Max(1L, (long)(margin * System.Diagnostics.Stopwatch.Frequency / 1000));
                    WarmAheadCells();
                }
            }
            if (!stars.Begin(Width, Height, pulse, pulseCrudo)) { System.Threading.Thread.Sleep(5); return NoPresentado(); }
            // Adornos solo del fondo: antes del muro y de toda la interfaz.
            if (VinetaSuave && vinetaTex != IntPtr.Zero)
            {
                DibujaTeselas(vinetaTex, vinetaTeselas, 0, 0, Width, Height, vinetaU, vinetaV, 0xFFFFFFFFu, adornoQuad);   // sin pintar su centro transparente (DibujoLigeroGpu.cs)
            }
            if (HaloSeleccion && ShowAlbum && GetContourRect != null)
            {
                Rectangle hr = GetContourRect();
                if (hr.Width > 0 && hr.Height > 0)
                {
                    // Con la portada enfocada el halo crece con ella y toma su color (HaloArgb); si no, el de siempre.
                    float hx = hr.X, hy = hr.Y, hw = hr.Width, hh = hr.Height;
                    EnfocaCaja(ref hx, ref hy, ref hw, ref hh);
                    if (FondoNatural)
                    {
                        float rx, ry; uint tint;
                        HaloPortadaNatural(hw, hh, HaloArgb(), out rx, out ry, out tint);
                        stars.DrawGlowElipse(hx + hw * 0.5f, hy + hh * 0.5f, rx, ry, tint);
                    }
                    else stars.DrawGlow(hx + hw * 0.5f, hy + hh * 0.5f, Math.Max(hw, hh) * 0.85f, HaloArgb());
                }
            }
            // Rejilla de discos: un quad por celda (textura ya resuelta arriba). Las celdas aun sin textura (recien
            // horneadas o sin presupuesto de subida este cuadro) se saltan y aparecen en los cuadros siguientes.
            if (ShowAlbum) DrawLod(true); // previews con marca: detras de la marca horneada
            if (ShowAlbum)
                for (int i = 0; i < gridN; i++)
                    if (gridTex[i] != IntPtr.Zero || (gridBuf[i].Direct && nativeGridTex[i] != IntPtr.Zero))
                    {
                        GpuGridCell c = gridBuf[i];
                        if (gridTex[i] != IntPtr.Zero) c.Layout = gridLayouts[i]; // puede ser una etiqueta anterior
                        c.CoverReady = nativeGridTex[i] != IntPtr.Zero;
                        if (c.X + c.W <= 0f || c.X >= Width) continue;   // hojeando: la casilla ya salio de la pantalla, no se pinta
                        int ca = (int)(Math.Max(0f, Math.Min(1f, c.A * WallOpacity)) * 255f + 0.5f);
                        if (ca <= 0) continue;
                        float apagada = atenua[i];   // portada enfocada: las demas, un poco mas oscuras (0 = como siempre)
                        if (c.Direct) DrawNativeCover(i, c, ColorCelda(ca, apagada));
                        if (gridTex[i] == IntPtr.Zero) continue;
                        if (c.Layout.Width > 0)
                        { DrawAtlasCell(gridTex[i], c, gridU[i], gridV[i], ColorCelda(ca, apagada)); continue; }
                        if (ca >= 255 && !(apagada > 0f)) SetTexQuad(cellQuad, c.X, c.Y, c.X + c.W, c.Y + c.H, gridU[i], gridV[i]);
                        else SetTexQuadA(cellQuad, c.X, c.Y, c.X + c.W, c.Y + c.H, gridU[i], gridV[i], ColorCelda(ca, apagada));
                        stars.DrawQuad(gridTex[i], cellQuad);
                    }
                    else if (gridBuf[i].Placeholder)
                    {
                        GpuGridCell c = gridBuf[i];
                        RectangleF r = c.CoverRect;
                        if (r.Right <= 0 || r.Left >= Width || r.Width <= 0 || r.Height <= 0) continue;
                        stars.BlankCubeColor = SkyMid & 0x00FFFFFFu;
                        stars.DrawCube(IntPtr.Zero, 1, 1, r.X, r.Y, r.Width, r.Height, 0.22,
                            Math.Max(0f, Math.Min(1f, c.A * WallOpacity)) * 0.35f, 0);
                        stars.BlankCubeColor = 0x003C466Eu;
                    }
            if (ShowAlbum) DrawLod(false);
            if (ShowAlbum && contourTex != IntPtr.Zero && GetContourRect != null)
            {
                Rectangle r = GetContourRect();
                if (r.Width > 0 && r.Height > 0)
                {
                    float rx = r.X, ry = r.Y, rw = r.Width, rh = r.Height;
                    EnfocaCaja(ref rx, ref ry, ref rw, ref rh);   // el contorno crece con la portada enfocada
                    SetTexQuadA(contourQuad, rx, ry, rx + rw, ry + rh, contU, contV, ((uint)(Math.Max(0f, Math.Min(1f, WallOpacity)) * 255f) << 24) | 0x00FFFFFFu);
                    stars.DrawQuad(contourTex, contourQuad);
                }
            }
            // Cubos girando ENCIMA del contorno (igual orden que en CPU): primero el relevo, luego el seleccionado.
            if (cs.Relay.Has)
                stars.DrawCube(relTex, relU, relV, cs.Relay.X, cs.Relay.Y, cs.Relay.W, cs.Relay.H, cs.Relay.Angle, WallOpacity, cs.Relay.Tilt);
            if (cs.Sel.Has && sceneMode == 3) DibujaHaloGenero(cs.Sel.X, cs.Sel.Y, cs.Sel.W, cs.Sel.H, ShowAlbum ? WallOpacity : 1f);   // dentro del disco, el color del genero detras, con el mismo alfa que el cubo (EstiloGpu.cs)
            // Sin vinilo ni desplazamiento lateral de la portada.
            if (cs.Sel.Has)
            {
                stars.DrawCube(selTexA, selUA, selVA, cs.Sel.X, cs.Sel.Y, cs.Sel.W, cs.Sel.H, cs.Sel.Angle, ShowAlbum ? WallOpacity : 1f, cs.Sel.Tilt);
                if (cs.Sel.CoverB != null && cs.Sel.Fade > 0.002f)   // disco TOP: 2a portada encima con alfa = fundido
                    stars.DrawCube(selTexB, selUB, selVB, cs.Sel.X, cs.Sel.Y, cs.Sel.W, cs.Sel.H, cs.Sel.Angle, cs.Sel.Fade * (ShowAlbum ? WallOpacity : 1f), cs.Sel.Tilt);
                DibujaFoco(cs);   // "12 canciones · 38 min" sobre el borde de abajo de la portada enfocada
            }
            // UI ENCIMA del fondo y los cubos: la textura principal anclada ARRIBA (a su tamano real: en
            // navegacion es solo la franja superior) y la franja de estado anclada ABAJO.
            bool uiPropia = gw.Has && gw.Bmp == null && !gw.Robada;   // se anima la PROPIA textura de UI (abriendo el buscador)
            if (!(gw.Has && gw.HideUi)) DrawPaletteOld();
            if (uiTex != IntPtr.Zero && uiBmpW > 0 && uiBmpH > 0 && !(gw.Has && gw.HideUi))
            {
                uint uiAlfa = (uint)(SceneAlpha() * PaletteAlpha() * ControlsOpacity * AtrUiAlfa * 255);
                if (uiAlfa > 0) DibujaTeselas(uiTex, uiTeselas, 0, 0, uiBmpW, uiBmpH, uiU, uiV, (uiAlfa << 24) | 0xFFFFFFu, uiQuad);   // sin lo transparente ni lo invisible (DibujoLigeroGpu.cs)
            }
            DibujaAtraccion();   // el escaparate y lo mas pedido esta noche (AtraccionGpu.cs); fuera de la atraccion, nada
            // Y ENCIMA las piezas vivas (la tarjeta y la tecla elegidas). Van despues de la textura de UI a
            // proposito: en ella estan las versiones SIN elegir, y estas las tapan. Se rellenan opacas, asi que
            // el resultado es identico a tenerlas horneadas dentro (comprobado pixel a pixel: pruebas/mover.cs).
            if (GlassPieces) CaptureSongSlide();
            if (GetPiezaRect != null && !(gw.Has && gw.HideUi))
            {
                DibujaCartasPrimero(0f, PaletteAlpha(), ControlsOpacity);   // las tarjetas del buscador con portadas, debajo de la elegida (BuscadorPortadasGpu.cs)
                for (int i = 0; i < PiezasN && i < MaxPiezas; i++)
                {
                    if (i == PiezaCartas || piezaTex[i] == IntPtr.Zero) continue;
                    Rectangle pr = GetPiezaRect(i);
                    if (pr.Width < 1 || pr.Height < 1) continue;
                    uint ink = GlassPieces ? (i == 0 ? GlassTint : 0xFFFFFFFFu) :
                        ((uint)(FocusAlpha(i, pr) * PaletteAlpha() * (i == 2 ? CartaElegidaAlfa : 1f) * 255) << 24) | 0xFFFFFFu;
                    if (GlassPieces) ink = (ink & 0xFFFFFFu) | ((uint)(SceneAlpha() * 255) << 24);
                    ink = FadeArgb(ink, ControlsOpacity);
                    if (GlassPieces && i == 1) { DrawSongPage(pr, GetPiezaRect(2), i, ink); continue; }
                    if (GlassPieces && i > 1) { DrawPieceBand(pr, i, pr.Top, pr.Bottom, ink); continue; }
                    SetTexQuadA(piezaQuad, pr.X, pr.Y, pr.X + pr.Width, pr.Y + pr.Height, piezaU[i], piezaV[i],
                        ink);
                    stars.DrawQuad(piezaTex[i], piezaQuad);
                }
            }
            if (!(gw.Has && gw.HideUi)) DibujaCintasTexto(true);
            if (!(gw.Has && gw.HideUi)) DibujaEq();   // las barritas de la que suena, encima de la lista y de su fila elegida
            // VENTANA del buscador abriendo/cerrando: el mismo quad de la UI, escalado desde su centro y fundido.
            if (gw.Has && gw.Alpha > 0.004f && gw.Scale > 0.01f)
            {
                IntPtr wt; float wu, wv, bw, bh;
                if (uiPropia) { wt = uiTex; wu = uiU; wv = uiV; bw = uiBmpW; bh = uiBmpH; }
                else if (gw.Robada) { wt = winTex; wu = winU; wv = winV; bw = winBw; bh = winBh; }   // cerrando: la textura que tenia la UI
                else { wt = winTex; wu = winU; wv = winV; bw = gw.Bmp.Width; bh = gw.Bmp.Height; }
                if (wt != IntPtr.Zero && bw > 1f && bh > 1f)
                {
                    float hw = bw * 0.5f, hh = bh * 0.5f;   // crece/encoge desde SU CENTRO (no desde una esquina)
                    int wa = (int)(gw.Alpha * 255f + 0.5f); if (wa > 255) wa = 255; else if (wa < 0) wa = 0;
                    SetTexQuadA(winQuad, hw - hw * gw.Scale, hh - hh * gw.ScaleY + gw.Dy,
                                         hw + hw * gw.Scale, hh + hh * gw.ScaleY + gw.Dy,
                                wu, wv, ((uint)wa << 24) | 0x00FFFFFFu);
                    stars.DrawQuad(wt, winQuad);
                }
            }
            PiezasConLaVentana(gw, uiPropia);   // el buscador con portadas: su casilla, lo elegido y sus tarjetas van con la ventana (BuscadorPortadasGpu.cs)
            if (uiBotTex != IntPtr.Zero && uiBotW > 0 && uiBotH > 0)
            {
                uint botAlfa = (uint)((1-idleBlend)*255);   // con el reposo del todo abierto va a opacidad cero: no se manda
                if (botAlfa > 0) DibujaTeselas(uiBotTex, uiBotTeselas, 0, Height - uiBotH, uiBotW, Height, uiBotU, uiBotV, (botAlfa << 24) | 0x00FFFFFFu, uiBotQuad);   // (DibujoLigeroGpu.cs)
            }
            if (idleBlend > 0 && idleTex != IntPtr.Zero)
            {
                float py = StatusTopY;
                SetTexQuadA(adornoQuad, 0, py, Width, py + idleH, idleU, idleV, ((uint)(idleBlend*255) << 24) | 0x00FFFFFFu);
                stars.DrawQuad(idleTex, adornoQuad);
                DibujaLatidoGenero(py);   // lo que suena, en grande: la pastilla del genero late con los graves
                DibujaTituloReposo(py);   // y el titulo que no cabe se desliza
            }
            DibujaCintasTexto(false);
            DrawSmoothProgress();
            // DESTELLOS del boton pulsado (buscador): halos aditivos ENCIMA de la UI, animados por la GPU (sin re-subir textura).
            if (GetUiGlows != null)
            {
                int ng = GetUiGlows(uiGlowBuf);
                if (ng > 0)
                {
                    stars.GlowBatchBegin(uiGlowBuf.Length);   // los destellos tambien salen de una sola llamada
                    for (int i = 0; i < ng && i < uiGlowBuf.Length; i++) { UiGlow ug = uiGlowBuf[i]; stars.GlowBatchAdd(ug.Cx, ug.Cy, ug.R, ug.Argb); }
                    stars.GlowBatchEnd();
                }
            }
            // Mini-cubo de la barra inferior (gira): cubo GPU, encima de la barra.
            if (cs.Mini.Has)
            {
                if (idleBlend > 0)
                {
                    Rectangle large = IdleCoverRect(Width, idleH);
                    cs.Mini.X += (large.X - cs.Mini.X) * idleBlend;
                    cs.Mini.Y += (Height - idleH + large.Y - cs.Mini.Y) * idleBlend;
                    cs.Mini.W += (large.Width - cs.Mini.W) * idleBlend;
                    cs.Mini.H += (large.Height - cs.Mini.H) * idleBlend;
                    cs.Mini.Tilt *= 1 - idleBlend;
                }
                DibujaHaloGenero(cs.Mini.X, cs.Mini.Y, cs.Mini.W, cs.Mini.H, 1f);   // el color del genero detras de la portada (EstiloGpu.cs)
                stars.DrawCube(miniTex, miniU, miniV, cs.Mini.X, cs.Mini.Y, cs.Mini.W, cs.Mini.H, cs.Mini.Angle, 1f, cs.Mini.Tilt);
            }
            DibujaFicha();   // la ficha rapida del disco: encima de la pared y las barras, debajo del viaje y de los avisos
            // TRANSICION entre cubos (viaje + giro + bamboleo + destello) ENCIMA de todo. Solo mientras vive.
            if (tr.Has && tr.Bmp != null && transTex != IntPtr.Zero && tr.Alpha > 0.004f && tr.W > 1f && tr.H > 1f)
            {
                if (tr.Glow > 0.004f)   // DESTELLO detras: halo aditivo (el mismo camino que los brillos del buscador)
                {
                    int ga = (int)(tr.Glow * tr.Alpha * 104f + 0.5f); if (ga > 255) ga = 255;
                    if (ga > 0) stars.DrawGlow(tr.X + tr.W * 0.5f, tr.Y + tr.H * 0.5f, tr.W * 0.66f, ((uint)ga << 24) | 0x00FFE2A8u);
                }
                stars.DrawCube(transTex, transU, transV, tr.X, tr.Y, tr.W, tr.H, tr.Angle, tr.Alpha, tr.Tilt);
            }
            DibujaBoleto();   // la cancion pedida volando de su fila a la cola: tambien encima de todo, menos los avisos
            DibujaMoneda();   // la moneda que cae en la capsula de creditos y su "+4 creditos"
            DibujaInvita();   // sin creditos: la tarjeta que invita a meter una moneda
            // AVISO flotante (moneda, cancion agregada...) por ENCIMA de todo menos el contador de FPS.
            if (tt.Has && toastTex != IntPtr.Zero && tt.Alpha > 0.004f && tt.Bmp != null)
            {
                int ta = (int)(tt.Alpha * 255f + 0.5f); if (ta > 255) ta = 255;
                SetTexQuadA(toastQuad, tt.X, tt.Y, tt.X + tt.Bmp.Width, tt.Y + tt.Bmp.Height,
                            toastU, toastV, ((uint)ta << 24) | 0x00FFFFFFu);
                stars.DrawQuad(toastTex, toastQuad);
            }
            DibujaAviso();   // el aviso claro (SuenaVivoGpu.cs): en el sitio del de siempre, encima de todo
            // Contador de FPS (diagnostico) encima de todo, en su textura pequeña. Oculto por defecto (Ctrl+F1).
            if (ShowFps && fpsTex != IntPtr.Zero) { SetTexQuadA(fpsQuad, Width - fpsTW - 10, 6, Width - 10, 6 + fpsTH, fpsU, fpsV, FadeArgb(0xFFFFFFFFu, ControlsOpacity)); stars.DrawQuad(fpsTex, fpsQuad); }
            // El cronometro se para AQUI, justo antes de End: dentro de End esta Present, que es donde se
            // espera al monitor. Un cuadro que se pasa media vuelta esperando no costo media vuelta.
            double trabajo = AhoraMs() - msTrab0;
            lastFrameWork = trabajo;
            if (!stars.End()) return NoPresentado();
            if (CoverControl != null) CoverControl.Observe(AhoraMs(), trabajo, TopeFpsReal);
            msTrabSuma += trabajo; msTrabN++;
            frames++;
            if (Environment.TickCount - fpsTick >= 1000) { Fps = frames; frames = 0; fpsTick = Environment.TickCount; }
            return true;
        }

        // Asegura (subiendo si hace falta) la textura de una celda y devuelve tex/u/v. Cachea por Key (Folder; los
        // discos TOP llevan Folder+portada) para que revisitar una pagina no resuba NADA (instantaneo). Celdas SIN
        // Key (raras: album sin carpeta) usan una textura por SLOT (no cruza paginas). Respeta el presupuesto de
        // subidas por cuadro: si se agota y no hay textura previa, devuelve false (la celda aparece al siguiente cuadro).
        private bool EnsureCellTex(GpuGridCell c, ref int upBudget, out IntPtr tex, out float u, out float v)
        {
            tex = IntPtr.Zero; u = 1f; v = 1f;
            if (c.Key != null)
            {
                CellTex ct;
                if (cellTexCache.TryGetValue(c.Key, out ct))
                {
                    ct.Frame = cellFrame;
                    if (ReferenceEquals(ct.Bmp, c.Bmp) && ct.Tex != IntPtr.Zero) { tex = ct.Tex; u = ct.U; v = ct.V; return true; }   // ya subida y sin cambios
                    if (upBudget <= 0) { if (ct.Tex == IntPtr.Zero) return false; tex = ct.Tex; u = ct.U; v = ct.V; return true; }   // sin presupuesto: usa la vieja (p.ej. placeholder) hasta que toque
                    upBudget--;
                    cellBytesDirty = true;
                    ct.Tex = Upload(ct.Tex, ref ct.Bw, ref ct.Bh, c.Bmp, out ct.U, out ct.V);   // portada llego / re-horneada: re-sube (mismo tamano -> reutiliza)
                    ct.Bmp = c.Bmp; ct.Layout = c.Layout; ct.HasCover = c.HasCover;
                    if (ct.Tex == IntPtr.Zero) return false;
                    tex = ct.Tex; u = ct.U; v = ct.V; return true;
                }
                if (upBudget <= 0) return false;   // celda nueva sin presupuesto: se salta este cuadro
                if (cellTexCache.Count >= cellTextureCapacity + MaxCells) return false;
                upBudget--;
                ct = new CellTex();
                cellBytesDirty = true;
                ct.Tex = Upload(IntPtr.Zero, ref ct.Bw, ref ct.Bh, c.Bmp, out ct.U, out ct.V);
                ct.Bmp = c.Bmp; ct.Layout = c.Layout; ct.Frame = cellFrame; ct.HasCover = c.HasCover;
                cellTexCache[c.Key] = ct;
                if (ct.Tex == IntPtr.Zero) return false;
                tex = ct.Tex; u = ct.U; v = ct.V; return true;
            }
            int s = c.Slot; if (s < 0 || s >= slotTex.Length) return false;   // celda sin Folder (TOP): textura por slot
            CellTex st = slotTex[s]; if (st == null) { st = new CellTex(); slotTex[s] = st; }
            st.Frame = cellFrame;
            if (!ReferenceEquals(st.Bmp, c.Bmp) || st.Tex == IntPtr.Zero)
            {
                if (upBudget <= 0) { if (st.Tex == IntPtr.Zero) return false; tex = st.Tex; u = st.U; v = st.V; return true; }
                upBudget--;
                cellBytesDirty = true;
                st.Tex = Upload(st.Tex, ref st.Bw, ref st.Bh, c.Bmp, out st.U, out st.V);
                st.Bmp = c.Bmp; st.Layout = c.Layout; st.HasCover = c.HasCover;
                if (st.Tex == IntPtr.Zero) return false;
            }
            tex = st.Tex; u = st.U; v = st.V; return true;
        }
        private bool PreparedCover(GpuGridCell c)
        {
            CellTex cached;
            return c.Bmp != null && c.HasCover && c.Key != null
                && cellTexCache.TryGetValue(c.Key, out cached) && cached.Tex != IntPtr.Zero
                && (c.Direct ? NativeCoverReady(c) : cached.HasCover) && ReferenceEquals(cached.Bmp, c.Bmp);
        }
        private GpuCellLayout UploadedCellLayout(GpuGridCell c)
        {
            CellTex ct = null;
            if (c.Key != null) cellTexCache.TryGetValue(c.Key, out ct);
            else if (c.Slot >= 0 && c.Slot < slotTex.Length) ct = slotTex[c.Slot];
            return ct != null ? ct.Layout : new GpuCellLayout();
        }
        private void DrawAtlasCell(IntPtr tex, GpuGridCell c, float u, float v, uint color)
        {
            AtlasCellVertices(c, u, v, color, atlasQuad);
            stars.DrawTexturedBatch(tex, atlasQuad, c.Layout.Separate ? 14 : 6); // etiquetas/contorno, un envio
        }
        internal static void AtlasCellVertices(GpuGridCell c, float u, float v, uint color, D3D9.Vtx[] result)
        {
            GpuCellLayout p = c.Layout;
            if (p.Separate) { SeparatedVertices(c, u, v, color, result); return; }
            float sx = c.W / (p.Text1At.Width + p.Text2At.Width);
            float sy = c.H / p.Text2At.Bottom;
            AtlasPart(result, 0, c.X, c.Y, sx, sy, p.Face, p.FaceAt, u / p.Width, v / p.Height, color);
            AtlasPart(result, 6, c.X, c.Y, sx, sy, p.Text1, p.Text1At, u / p.Width, v / p.Height, color);
            AtlasPart(result, 12, c.X, c.Y, sx, sy, p.Text2, p.Text2At, u / p.Width, v / p.Height, color);
        }
        private static void AtlasPart(D3D9.Vtx[] r, int o, float x, float y, float sx, float sy,
            Rectangle src, Rectangle at, float u, float v, uint color)
        {
            float x0 = x + at.X * sx, x1 = x + at.Right * sx, y0 = y + at.Y * sy, y1 = y + at.Bottom * sy;
            float u0 = src.X * u, u1 = src.Right * u, v0 = src.Y * v, v1 = src.Bottom * v;
            SetV(ref r[o], x0, y0, u0, v0); SetV(ref r[o + 1], x1, y0, u1, v0);
            SetV(ref r[o + 2], x1, y1, u1, v1); SetV(ref r[o + 3], x0, y0, u0, v0);
            SetV(ref r[o + 4], x1, y1, u1, v1); SetV(ref r[o + 5], x0, y1, u0, v1);
            for (int i = 0; i < 6; i++) r[o + i].Color = color;
        }

        private bool AheadContains(string key, int count)
        {
            for (int i = 0; i < count; i++) if (aheadGrid[i].Key == key) return true;
            return false;
        }

        // Nunca expulsar visibles ni las vecinas ya preparadas: evita un ciclo de subidas/expulsiones
        // cuando a resolucion alta no caben las tres paginas en el presupuesto.
        private bool ReserveAheadSpace(GpuGridCell cell, int aheadCount)
        {
            long w = 1, h = 1;
            while (w < cell.Bmp.Width) w <<= 1;
            while (h < cell.Bmp.Height) h <<= 1;
            long incoming = w * h * 4;
            if (cell.Direct && cell.FullCover != null && !NativeCoverReady(cell)) {
                long nw = 1, nh = 1;
                while (nw < cell.FullCover.Width) nw <<= 1;
                while (nh < cell.FullCover.Height) nh <<= 1;
                incoming += nw * nh * 4;
            }
            if (incoming > cellTextureBudget) return false;
            CellTex old; cellTexCache.TryGetValue(cell.Key, out old);
            int extra = old == null ? 1 : 0;
            while (CachedCellBytes() - CellBytes(old) + incoming > cellTextureBudget ||
                cellTexCache.Count + extra > cellTextureCapacity)
            {
                string victim = null; int oldest = 0;
                foreach (KeyValuePair<string, CellTex> kv in cellTexCache)
                {
                    int age = unchecked(cellFrame - kv.Value.Frame);
                    if (age > oldest && !AheadContains(kv.Key, aheadCount))
                    { oldest = age; victim = kv.Key; }
                }
                if (victim == null) { if (DiscardUnusedNative(false)) continue; return false; }
                CellTex removed = cellTexCache[victim];
                if (!ReleaseCoverTexture(removed.Tex)) return false;
                cellTexCache.Remove(victim); cellBytesDirty = true;
            }
            return true;
        }

        private void WarmAheadCells()
        {
            if (GetGridAhead == null) return;
            int count = Math.Max(0, Math.Min(aheadGrid.Length, GetGridAhead(aheadGrid)));
            protectedAhead.Clear(); protectedNativeAhead.Clear();
            for (int i = 0; i < count; i++) {
                if (aheadGrid[i].Key != null) protectedAhead.Add(aheadGrid[i].Key);
                if (aheadGrid[i].FullCover != null) protectedNativeAhead.Add(aheadGrid[i].FullCover);
            }
            int remaining = CoverControl != null ? 32 : AheadUploadAllowance(CargaTrabajo.Procesadores);
            int initial = remaining;
            long begin = System.Diagnostics.Stopwatch.GetTimestamp();
            long available = aheadAvailableTicks > 0 ? Math.Min(GridWorkLimit, aheadAvailableTicks) : GridWorkLimit;
            for (int i = 0; i < count; i++)
            {
                if (remaining <= 0 || (CoverControl == null && !CargaTrabajo.PuedeEmpezar(System.Diagnostics.Stopwatch.GetTimestamp()-begin,
                    gridUploadCost, available, remaining == initial))) break;
                GpuGridCell cell = aheadGrid[i];
                if (cell.Key == null || cell.Bmp == null) continue;
                CellTex old;
                if (cellTexCache.TryGetValue(cell.Key, out old) &&
                    (old.Frame == cellFrame || (old.Tex != IntPtr.Zero && ReferenceEquals(old.Bmp, cell.Bmp) && (!cell.Direct || cell.FullCover == null || NativeCoverReady(cell))))) continue;
                if (!ReserveAheadSpace(cell, count)) continue;
                if (CoverControl != null && !CoverControl.CanStart(gridUploadCost, false, false)) break;
                int budget = remaining; float nu, nv;
                PrepareNativeCover(cell, ref budget, false, out nu, out nv);
                remaining = budget; if (remaining <= 0) break;
                budget = 1; IntPtr tex; float u, v;
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                EnsureCellTex(cell, ref budget, out tex, out u, out v);
                long spent = System.Diagnostics.Stopwatch.GetTimestamp() - start;
                gridUploadCost = CargaTrabajo.AprendeCoste(gridUploadCost, spent);
                if (CoverControl != null && budget == 0) CoverControl.Spend(spent);
                if (cellTexCache.TryGetValue(cell.Key, out old)) old.Frame = unchecked(cellFrame - 1);
                remaining--; // presupuesto compartido; sin regulador, conservar 1/2/4
            }
            Array.Clear(aheadGrid, 0, aheadGrid.Length); // no retener bitmaps expulsados por la cache de composicion
        }

        // Expulsa las texturas de celda mas VIEJAS (no usadas este cuadro) cuando se pasa del tope, para acotar VRAM.
        // La "edad" se calcula por RESTA (cellFrame - Frame): sobrevive el desbordamiento del contador de cuadros
        // (int a 60fps se desborda a los ~414 dias encendida; misma familia que el TickCount de las fugaces).
        // Presupuesto de texeles ARGB (incluye redondeo POT), no una lectura de VRAM del driver.
        // Las visibles se conservan incluso si una pagina por si sola supera el presupuesto.
        private const long CellTexBytesCap = 32L * 1024 * 1024;
        private long cellTextureBudget = CellTexBytesCap;
        private long cellBytesTotal;
        private bool cellBytesDirty = true;
        private static long CellBytes(CellTex ct)
        {
            if (ct == null || ct.Tex == IntPtr.Zero || ct.Bw < 1 || ct.Bh < 1) return 0;
            long w = 1, h = 1;
            while (w < ct.Bw) w <<= 1;
            while (h < ct.Bh) h <<= 1;
            // Dimensiones imposibles no deben desbordar el total ni desactivar la poda.
            if (w > 0x40000000L || h > 0x40000000L) return long.MaxValue / 128;
            return Math.Min(w * h * 4L, long.MaxValue / 128);
        }
        private long CachedCellBytes()
        {
            if (!cellBytesDirty) return cellBytesTotal;
            long bytes = 0;
            foreach (CellTex ct in cellTexCache.Values) bytes += CellBytes(ct);
            foreach (CellTex ct in nativeCovers.Values) bytes += CellBytes(ct);
            for (int i = 0; i < slotTex.Length; i++) bytes += CellBytes(slotTex[i]);
            cellBytesTotal = bytes; cellBytesDirty = false;
            return bytes;
        }
        private void EvictCellTex()
        {
            EvictNativeCovers();
            long bytes = CachedCellBytes();
            while (cellTexCache.Count > cellTextureCapacity || bytes > cellTextureBudget)
            {
                string oldestKey = null; int oldestAge = 0, oldestSlot = -1;
                foreach (KeyValuePair<string, CellTex> kv in cellTexCache)
                { int age = unchecked(cellFrame - kv.Value.Frame); if (age > oldestAge && !protectedAhead.Contains(kv.Key)) { oldestAge = age; oldestKey = kv.Key; } }
                if (oldestKey == null)
                    foreach (KeyValuePair<string, CellTex> kv in cellTexCache) {
                        int age = unchecked(cellFrame - kv.Value.Frame);
                        if (age > oldestAge) { oldestAge = age; oldestKey = kv.Key; }
                    }
                // Los slots sin carpeta tambien ocupan memoria. Solo compiten si se exceden los bytes.
                if (bytes > cellTextureBudget)
                    for (int i = 0; i < slotTex.Length; i++)
                    {
                        CellTex st = slotTex[i]; if (st == null) continue;
                        int age = unchecked(cellFrame - st.Frame);
                        if (age > oldestAge) { oldestAge = age; oldestKey = null; oldestSlot = i; }
                    }
                if (oldestKey == null && oldestSlot < 0) break;
                CellTex ct = oldestSlot >= 0 ? slotTex[oldestSlot] : cellTexCache[oldestKey];
                if (!ReleaseCoverTexture(ct.Tex)) break;
                bytes -= CellBytes(ct);
                cellBytesTotal = bytes;
                if (oldestSlot >= 0) slotTex[oldestSlot] = null;
                else cellTexCache.Remove(oldestKey);
            }
        }

        // Rehace la textura del contador SOLO cuando cambia el numero (~1/seg). Es diminuta: coste despreciable.
        private void EnsureFpsTex()
        {
            if (fpsTex != IntPtr.Zero && fpsShown == Fps) return;
            fpsShown = Fps;
            fpsTW = 300; fpsTH = 26;
            using (Bitmap bmp = new Bitmap(fpsTW, fpsTH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (Font f = new Font(FontFamily.GenericMonospace, 9.5f, FontStyle.Bold))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
                using (SolidBrush yb = new SolidBrush(Color.FromArgb(235, 245, 225, 60)))
                {
                    g.Clear(Color.Transparent);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.FillRectangle(bg, 0, 0, fpsTW, fpsTH);
                    g.DrawString("GPU D3D9: " + Fps + " FPS  (tope " + TopeFpsReal + ")", f, yb, 6, 4);
                }
                if (fpsTex != IntPtr.Zero) { D3D9.Release(fpsTex); fpsTex = IntPtr.Zero; }
                fpsTex = stars.CreateOverlay(bmp, out fpsU, out fpsV);
            }
        }

        // Ajusta la espera entre reconstrucciones pesadas al COSTE REAL de la ultima: la espera es ~1.5x el
        // coste, asi el trabajo pesado ocupa como mucho ~40% del tiempo y el fondo siempre respira.
        // Reconstruccion barata (UI cacheada del buscador) -> espera corta -> fluido; rejilla cara (paginar
        // discos) -> espera larga -> el fondo no se atasca. Se auto-ajusta al equipo (banco potente o rocola vieja).
        private void NoteHeavy(int t0)
        {
            lastHeavyMs = Environment.TickCount;
            int cost = lastHeavyMs - t0; if (cost < 0) cost = 0;
            int g = cost + cost / 2;   // ~1.5x el coste
            heavyGapMs = g < 16 ? 16 : (g > 250 ? 250 : g);
        }

        // Sube 'bmp' a 'tex' REUTILIZANDO la textura si el bitmap tiene el mismo tamano (barato: solo re-copia
        // el contenido); si cambio de tamano o no habia, la recrea. Evita crear/destruir una textura a pantalla
        // completa en cada reconstruccion (lo caro), p.ej. la UI ~13fps por el brillo de la letra en navegacion.
        private IntPtr Upload(IntPtr tex, ref int bw, ref int bh, Bitmap bmp, out float u, out float v)
        {
            if (tex != IntPtr.Zero && bw == bmp.Width && bh == bmp.Height && stars.UpdateOverlay(tex, bmp, out u, out v))
                return tex;   // actualizada in situ (misma textura)
            if (tex != IntPtr.Zero) D3D9.Release(tex);
            tex = stars.CreateOverlay(bmp, out u, out v);
            bw = bmp.Width; bh = bmp.Height;
            return tex;
        }

        private static void SetTexQuad(D3D9.Vtx[] v, float x0, float y0, float x1, float y1, float uMax, float vMax)
        {
            SetV(ref v[0], x0, y0, 0, 0); SetV(ref v[1], x1, y0, uMax, 0); SetV(ref v[2], x1, y1, uMax, vMax);
            SetV(ref v[3], x0, y0, 0, 0); SetV(ref v[4], x1, y1, uMax, vMax); SetV(ref v[5], x0, y1, 0, vMax);
        }
        // Igual que SetTexQuad pero con COLOR (el alfa del vertice modula la textura): asi el desvanecido
        // de la transicion no cuesta nada, lo hace la GPU al mezclar.
        private static void SetTexQuadA(D3D9.Vtx[] v, float x0, float y0, float x1, float y1, float uMax, float vMax, uint argb)
        {
            SetTexQuad(v, x0, y0, x1, y1, uMax, vMax);
            for (int i = 0; i < 6; i++) v[i].Color = argb;
        }
        // -0.5 px: regla de D3D9 (el centro del pixel esta en x+0.5). Sin este ajuste, una textura mapeada
        // 1:1 (los cubos y sobre todo el TEXTO de las etiquetas) se muestrea entre texeles y se ve borrosa.
        private static void SetV(ref D3D9.Vtx vx, float x, float y, float u, float vv) { vx.X = x - 0.5f; vx.Y = y - 0.5f; vx.Z = 0; vx.Rhw = 1; vx.Color = 0xFFFFFFFF; vx.U = u; vx.V = vv; }

        // FONDO REACTIVO: una AURORA BOREAL. Se dibuja como una malla de columnas (dos triangulos por trozo)
        // sin textura y con mezcla normal: el color va en los vertices y la tarjeta lo degrada sola. Nada se
        // suma, asi que nunca se lava hacia el blanco -eso era lo que manchaba con los halos- y todo cuesta
        // UNA sola llamada de dibujo.
        //
        // Lo que la hace parecer una aurora de verdad y no una barra de niveles:
        //   1) CORTINAS. Encima de la forma que dictan las bandas va un ondeo de rayos verticales que se
        //      desliza despacio (AurRayo). Es lo que da las vetas de luz de una aurora; no depende de la
        //      musica, vive por su cuenta, y por eso el fondo respira aunque la cancion este tranquila.
        //   2) DOS COLORES A LO ALTO. El pie de la cortina lleva el color de la cancion y la punta el mismo
        //      color girado en la rueda hacia otro tono, como el verde que se vuelve morado en las de verdad.
        //   3) HORIZONTE. Por debajo de donde nace la cortina el color se HUNDE -de tono y de presencia- hasta
        //      fundirse con el cielo en el borde de la pantalla: es el suelo oscuro de las fotos de auroras y,
        //      de paso, deja leerse el texto de la franja de estado. Es un degradado que muere en el mismo
        //      color del cielo, asi que no hay ningun corte: todo sigue siendo una sola pieza.
        private const int AurCols = 144;      // trozos a lo ancho (145 puntos): hacen falta para las vetas finas
        private const int AurFilas = 6;       // franjas apiladas a lo alto: cuantas mas, mas suave el degradado
        private const float AurPiso = 0.20f;  // suelo: aunque la cancion afloje, la cortina no desaparece
        private const float AurAlfaBase = 0.46f, AurAlfaPico = 0.48f;   // opacidad del relleno (flojo -> fuerte)
        private const int AurSuelo = 4;       // franjas por DEBAJO del horizonte (el suelo)
        private const float AurHondo = 0.10f; // que tan hundido queda el tono al pie de la pantalla (0 = lo mas hondo)
        // Ancho del LOBULO de cada banda, medido en bandas. Al ser MENOR que 1, el lobulo de una banda se
        // apaga ANTES de llegar al centro de su vecina: asi una banda fuerte no se traga a la de al lado y se
        // ven los ocho bultos separados por su valle. Esto es lo que hace que se distingan las frecuencias.
        private const float AurLobulo = 0.72f;

        // ---------------- EL FILO DE NEON ----------------
        // El relleno se apaga hacia arriba sin dejar borde: la forma se intuye, pero no se ve DIBUJADA.
        // Encima de esa silueta va ahora un filo encendido -un nucleo fino y clarisimo y un resplandor
        // del mismo color a los dos lados- que se SUMA a lo que ya hay en vez de taparlo. Asi es como se
        // lee un tubo de neon: el centro casi quema y el color se derrama alrededor.
        private const float AurNeonNucleo = 1.6f;    // medio grosor del filo encendido, en pixeles
        private const float AurNeonFuera = 15f;      // hasta donde llega el resplandor por ENCIMA de la silueta
        private const float AurNeonDentro = 11f;     // y hacia dentro de la cortina
        private const float AurNeonQuiebre = 0.36f;  // donde se parte el desvanecido de fuera (para que caiga en curva, no en linea)
        private const float AurNeonBase = 0.30f, AurNeonPico = 0.52f;   // brillo del filo (la cancion floja -> el pico)
        private const float AurNeonBlanco = 0.45f;   // cuanto se aclara el NUCLEO respecto a su color (0 = el mismo)
        private const float AurNeonHalo = 0.34f;     // brillo del resplandor respecto al del nucleo

        // ---------------- LA LINEA DEL HORIZONTE ----------------
        // El filo sobre el que la aurora esta PARADA, de lado a lado de la pantalla: una raya fina y
        // clarisima con su resplandor desvaneciendose, como el anillo que cruza el agujero negro.
        //
        // DONDE VA. El relleno nace en BaseY, que cae 2 pixeles DENTRO de la primera linea del titulo de
        // la cancion (esa linea suele ir vacia, pero se usa cuando el titulo no cabe en una sola). Poner
        // ahi la raya encendida seria ponerla encima de ese texto. Asi que la raya se levanta un poco
        // (AurLineaAlza) y su resplandor hacia abajo se acaba JUSTO antes de donde el texto empieza.
        // Los pixeles de relleno que quedan por debajo de la raya no sobran: hacen de brillo por dentro,
        // que es justo como se ve la linea que cruza el agujero negro (luz por encima y por debajo).
        private const float AurLineaNucleo = 1.4f;
        private const float AurLineaAlza = 14f;
        private const float AurLineaArriba = 34f, AurLineaAbajo = 11f;
        private const float AurLineaQuiebre = 0.34f;
        private const float AurLineaBase = 0.50f, AurLineaPico = 0.50f;
        private const float AurLineaHalo = 0.30f;
        // La raya es PLANA: su forma no cambia a lo ancho, solo su brillo, y eso va cambiando muy despacio.
        // Asi que no le hacen falta las 144 columnas de la cortina: con 36 (una de cada cuatro) sale igual y
        // se ahorran 3.240 vertices por cuadro, que en la maquina modesta es lo que hay que cuidar.
        private const int AurLineaCols = 36;
        // El CONTORNO, al reves, va al DOBLE de puntos que la cortina y por una CURVA. El relleno se apaga
        // hacia arriba y su borde no llega a verse, asi que da igual que lo una con rectas; pero el filo es
        // una raya fina y ahi cualquier esquina canta -en una pantalla ancha, cada tramo mide 14 pixeles y
        // las cimas salian en punta, como un triangulo-. Con el doble de puntos y pasando por una curva que
        // toca los mismos (Catmull-Rom), las cimas salen redondas y los valles tambien.
        private const int AurContCols = AurCols * 2;
        private const int AurBrilloVtx = (4 * AurContCols + 5 * AurLineaCols) * 6;   // 4 franjas el contorno + 5 la raya

        private D3D9.Vtx[] aurV;                               // vertices de la malla (se reusan, no se asigna por cuadro)
        private D3D9.Vtx[] aurVb;                              // y los del brillo (el neon, que va sumado aparte)
        private float[] aurH, aurHs, aurA, aurR;             // alto (crudo y ya suavizado), opacidad y ondeo de cada columna
        private float[] aurNi, aurLi;                        // brillo del filo y de la linea, columna a columna
        private uint[] aurNc, aurLc;                         // y sus colores (el de la punta y el del pie)
        private uint[] aurCortina, aurSueloC;                // colores por franja: la cortina y el suelo
        private uint[] aurRampPie, aurRampAgu;               // el degradado pie->punta de cada extremo (una vez por cuadro)
        // RELOJ DE LA AURORA. Antes era "los segundos que llevo encendida", sacados de Environment.TickCount.
        // En un bar la rocola no se apaga NUNCA, y eso trae dos problemas de los que solo se ven despues de
        // semanas: a los 24 dias el reloj de Windows da la vuelta, y mucho antes -a los pocos dias- ese numero
        // se hace tan grande que un float ya no puede guardar decimales y el tiempo empieza a avanzar A SALTOS
        // de un cuarto de segundo: la aurora se veria ir a tirones sin motivo aparente.
        //
        // Aqui se lleva el tiempo a mano, sumando el paso de cada cuadro (la resta de TickCount da bien el paso
        // aunque el reloj de la vuelta), y se le da la vuelta cada 628.3185 segundos. Ese numero no es
        // cualquiera: es 2*pi/0.01, y TODAS las ondas del fondo giran a un multiplo exacto de 0.01, asi que al
        // dar la vuelta cada una vuelve EXACTAMENTE a donde estaba. El salto no existe: es el mismo dibujo.
        private const double AurVuelta = 628.3185307179586;   // 2*pi/0.01
        private double aurT;                                  // segundos de animacion, ya dada la vuelta
        private double aurFinoAnt; private bool aurRelojOk;   // para medir el paso de cada cuadro (reloj FINO: ver AhoraMs)
        private GpuAurora auCuadro;                          // la aurora de ESTE cuadro (la pide el render y la dibuja TrasCielo)
        private readonly GpuStarfield.Dibuja trasCielo;      // delegado creado UNA vez (nada de basura por cuadro)
        private readonly GpuStarfield.Dibuja trasEstrellas; // idem: la luna, delante de las estrellas

        // LA VIA LACTEA Y LAS NEBULOSAS. Son manchas grandes y muy suaves, o sea justo lo que se puede
        // dibujar CHIQUITO y estirar sin que se note: se hornean a un CUARTO de tamano y la tarjeta las
        // estira al dibujarlas. Dos cuadrangulos a pantalla completa por cuadro, que para una tarjeta es
        // nada, y ni una mancha que recalcular.
        //
        // Van en DOS texturas y no en una porque las nebulosas se tinen con el color del genero que suena y
        // la neblina no: la neblina es del blanco azulado del que esta hecha de verdad.
        private IntPtr nebTex, hazTex;
        private int nebTW = -1, nebTH = -1, hazTW = -1, hazTH = -1;
        private float nebU = 1f, nebV = 1f, hazU = 1f, hazV = 1f;
        private int nieblaW = -1, nieblaH = -1;
        private int nieblaRetryMs; private bool nieblaRetryPending;
        private readonly D3D9.Vtx[] nieblaQuad = new D3D9.Vtx[6];
        // LAS DOS NEBULOSAS. El form las pone: una con el color del GENERO y otra con el de la CARATULA
        // (ver ColoresNebulosa). NebFase es la misma cuenta lenta que las hace ir a la deriva.
        public uint NebColor = 0xFF8C78FF, NebColor2 = 0xFF8C78FF;
        public float HazFase = 0f;   // el reloj LENTO de la via lactea (una vuelta cada ~3 min); lo lleva Form1
        public bool ConLuna = true;  // perilla de Ctrl+F3 (pestana DETALLES)
        public float NebFase = 0f;
        // CUANTO SE SALEN DE LA PANTALLA. Las dos capas van a la deriva, y para que al moverse no asome el
        // borde se dibujan un poco mas grandes que la pantalla y se mueven DENTRO de ese margen. Por eso no
        // hace falta que la textura se repita: nunca se ve donde acaba.
        private const float NebDeriva = 0.06f;
        // CUANTO SE MUEVE LA VIA LACTEA, en parte de pantalla. La mitad que las nebulosas y con la vuelta
        // seis veces mas larga: es lo que esta mas lejos de todo.
        //
        // Y SE MUEVE EN CIRCULO ACOTADO, no dando la vuelta por el borde como hacen las estrellas. Tiene que
        // ser asi: una estrella que sale por la derecha reaparece por la izquierda y nadie lo nota porque es
        // un punto, pero la via lactea es una BANDA que cruza la pantalla entera y su dibujo llega hasta los
        // dos cantos. Envolverla partiria la banda por la mitad con una costura recta.
        //
        // El seno y el coseno se cierran EXACTOS en la vuelta del reloj, asi que no hay salto ni tiron al
        // completarse: es el mismo truco que ya usan las nebulosas.
        private const float HazDeriva = 0.035f;
        // Las dos capas van a TINTA ENTERA: cuanto se notan se decide en el dibujo (ver Form1.NebFuerza), no
        // aqui. Poner la fuerza en el alfa del vertice no valdria: ese alfa es un byte y no puede pasar de
        // uno, y lo que se eligio mirando es cuatro veces lo de antes.
        private const float NebAlfa = 1f;

        // CUANTO ESPERA EL MAPA ANTES DE REHORNEARSE POR EL DESLIZADOR.
        //
        // El interruptor del detalle natural es un clic: rehornea al momento y se acabo. El DESLIZADOR
        // no, y esa es toda la diferencia. Arrastrarlo de punta a punta suelta un aviso POR CADA UNIDAD
        // -cien-, y hornear el mapa cuesta 23 ms en la pantalla del bar, medido. Un cuadro a sesenta por
        // segundo dura 16,7: un solo horneado ya se pasa. Cien seguidos son dos segundos y pico de
        // tirones, mas cien veces destruir y crear la textura, que es lo que peor le sienta al
        // controlador de la tarjeta.
        //
        // Asi que la fuerza se apunta al vuelo -el numero sube mientras arrastras- pero el mapa se
        // hornea como mucho una vez cada NebEspera. Al soltar, el ultimo valor entra en la ronda
        // siguiente: nunca se queda sin hornear, y nunca tarda mas de esta espera en verse.
        internal const int NebEspera = 150;
        private int nebFuerzaMs;
        // LA DECISION VA APARTE Y ES PURA: recibe si cambio el interruptor, si cambio la fuerza, el reloj
        // de ahora y cuando se horneo la ultima vez, y dice si toca. Asi se puede probar sin tarjeta
        // grafica, que aqui no hay ninguna. Es el mismo apaño que PasoMuro, StepIdle o SmoothProgress.
        internal static bool TocaRehornear(bool cambioMando, bool cambioFuerza, int ahora, int ultimo)
        {
            if (cambioMando) return true;        // el interruptor es un clic: al momento
            if (!cambioFuerza) return false;
            return unchecked(ahora - ultimo) >= NebEspera;   // el deslizador se junta
        }
        private void EnsureNieblas()
        {
            if (FondoNatural) { EnsureNieblasPreparadas(); return; }
            bool cambioMando = nebNaturalDe != CieloProfundo || nebEstiloDe != FondoNatural;
            bool cambioFuerza = nebFuerzaDe != Form1.nebNaturalFuerza;
            int ahoraNeb = Environment.TickCount;
            if (TocaRehornear(cambioMando, cambioFuerza, ahoraNeb, nebFuerzaMs))
            { if (nebTex != IntPtr.Zero) { D3D9.Release(nebTex); nebTex = IntPtr.Zero; } nieblaRetryPending = false; }
            if (hazEstiloDe != FondoNatural) {
                if (hazTex != IntPtr.Zero) { D3D9.Release(hazTex); hazTex = IntPtr.Zero; }
                nieblaRetryPending = false;
            }
            bool nuevoTamano = nieblaW != Width || nieblaH != Height;
            if (!nuevoTamano && hazTex != IntPtr.Zero && nebTex != IntPtr.Zero && ruidoTex != IntPtr.Zero) return;
            int now = Environment.TickCount;
            if (!nuevoTamano && nieblaRetryPending && unchecked(now - nieblaRetryMs) < 250) return;
            nieblaW = Width; nieblaH = Height;
            int mw = Math.Max(8, Width / 4), mh = Math.Max(8, Height / 4);
            // Un fallo parcial solo reintenta la capa pendiente; no vuelve a hornear las que ya llegaron.
            if (nuevoTamano || hazTex == IntPtr.Zero)
                {
                    Bitmap hz = MapaViaEstilo(mw, mh);
                    hazTex = Upload(hazTex, ref hazTW, ref hazTH, hz, out hazU, out hazV);
                    hazTeselas = TeselasVisibles.De(hz, TeselasVisibles.LadoTesela, true);   // lo que tiene algo que pintar (DibujoLigeroGpu.cs)
                    hazEstiloDe = FondoNatural;
                }
            if (nuevoTamano || nebTex == IntPtr.Zero)
            {
                // CON QUE se horneo se apunta AQUI, no arriba al soltar la textura: si se apuntara antes
                // de saber que el horneado ocurre, un mapa que no llego a hacerse quedaria dado por bueno
                // y el mando no se veria hasta el siguiente cambio.
                nebNaturalDe = CieloProfundo; nebFuerzaDe = Form1.nebNaturalFuerza; nebFuerzaMs = ahoraNeb; nebEstiloDe = FondoNatural;
                Bitmap nb = MapaNebEstilo(mw, mh);
                nebTex = Upload(nebTex, ref nebTW, ref nebTH, nb, out nebU, out nebV);
            }
            if (ruidoTex == IntPtr.Zero)
                using (Bitmap rd = Form1.BakeRuidoGrande(RuidoTeja)) ruidoTex = Upload(ruidoTex, ref ruTW, ref ruTH, rd, out ruU, out ruV);
            nieblaRetryPending = hazTex == IntPtr.Zero || nebTex == IntPtr.Zero || ruidoTex == IntPtr.Zero;
            nieblaRetryMs = Environment.TickCount;   // un fallo persistente no rehornea las capas en cada cuadro
        }

        // EL GRANO QUE DESHACE LAS FRANJAS DEL CIELO. El por que esta en Form1, junto al dibujito: el cielo
        // se pintaba en unas treinta franjas planas porque el canal que menos se mueve cambia un nivel de 255
        // cada treinta pixeles, y el ojo engancha ese canto recto. Un grano de un nivel se lo quita.
        //
        // Va DESPUES del cielo, de las nieblas y de la aurora -que son las tres cosas que hacen bandas- y
        // ANTES de las estrellas, que no las hacen y a las que no tiene nada que aportarles.
        //
        // Y va en trozos porque las texturas de aqui estan en modo "pegado al borde" y no se repiten solas:
        // una teja de 1024 cubre una pantalla de bar en cuatro estampados, y una de 1024x768 en uno.
        private const int RuidoTeja = 1024;
        private IntPtr ruidoTex; private int ruTW, ruTH; private float ruU = 1f, ruV = 1f;

        // ESTRELLA GPU: superficie neutra preparada una vez por estilo (detallado/sencillo).
        // El color musical y el movimiento van en los vertices, sin rehornear ni subir
        // texturas al cambiar de cancion. Se conservan las claves de configuracion lunar.
        // La pared CPU mantiene su luna original, sin cambios.
        private IntPtr lunaTex; private int luTW, luTH; private float luU = 1f, luV = 1f;
        private int lunaDe = -1;
        private bool lunaEstiloDe;
        private D3D9.Vtx[] lunaQuad = new D3D9.Vtx[6];
        private void EnsureLuna()
        {
            if (!ConLuna || Height < 64) return;
            int moonKey = MoonTextureSize;
            if (lunaTex == IntPtr.Zero || lunaDe != moonKey || lunaDetalleDe != LunaRealista || lunaEstiloDe != FondoNatural)
            {
                using (Bitmap lb = FondoNatural ? BakeStarNatural(LunaRealista) : BakeStar(LunaRealista))
                {
                    lunaTex = Upload(lunaTex, ref luTW, ref luTH, lb, out luU, out luV);
                }
                if (lunaTex != IntPtr.Zero) {
                    lunaDe = moonKey; lunaDetalleDe = LunaRealista; lunaEstiloDe = FondoNatural;
                }
            }
        }
        private void DibujaLuna()
        {
            if (!ConLuna || Height < 64) return;
            if (lunaTex == IntPtr.Zero) return;
            DrawDetailedMoon();
        }
        private void DibujaRuido()
        {
            if (ruidoTex == IntPtr.Zero) return;
            for (int y = 0; y < Height; y += RuidoTeja)
                for (int x = 0; x < Width; x += RuidoTeja)
                {
                    float aw = Math.Min(RuidoTeja, Width - x), ah = Math.Min(RuidoTeja, Height - y);
                    SetTexQuad(nieblaQuad, x, y, x + aw, y + ah, ruU * (aw / RuidoTeja), ruV * (ah / RuidoTeja));
                    // El dither sigue evitando bandas, pero Natural usa un grano
                    // mas delicado. Misma teja estatica: nada de puntos nuevos por cuadro.
                    if (FondoNatural)
                        for (int i = 0; i < 6; i++) nieblaQuad[i].Color = 0xD0FFFFFFu;
                    stars.DrawQuad(ruidoTex, nieblaQuad);
                }
        }

        // Aqui hay DOS capas a pantalla completa, y ahi esta el escalon de en medio: la nebulosa lleva el
        // color del genero -es la que se ve y la que ata el fondo con la cancion- y la neblina es un velo
        // encima que casi no se nota por si solo. En MEDIO se queda la nebulosa y se va el velo: la mitad de
        // pantalla pintada, y de lejos el fondo sigue siendo el mismo.
        public bool NebulosasCine = true;
        public bool NeonIntenso = true, CieloProfundo = true, AuroraBandas = true;
        private bool nebNaturalDe;
        private float nebFuerzaDe = -1f;   // con que fuerza se horneo el mapa que hay puesto

        internal static double SmoothProgress(double current, double target, double seconds)
        {
            if (double.IsNaN(target) || double.IsInfinity(target)) target = 0;
            target = Math.Max(0, Math.Min(1, target));
            if (current < 0 || Math.Abs(target - current) > 0.02 || target == 0 || target == 1) return target;
            seconds = Math.Max(0, Math.Min(0.25, seconds));
            return current + (target - current) * (1 - Math.Exp(-seconds * 18));
        }
        private void DrawSmoothProgress()
        {
            if (GetProgress == null) { progressDraw = -1; progressKey = null; return; }
            GpuProgress p = GetProgress();
            if (!ProgresoFluido && !(NeonIntenso && p.Neon) && idleBlend <= 0) { progressDraw = -1; return; }
            if (!p.Has || p.Rect.Width < 4 || p.Rect.Height < 4) { progressDraw = -1; progressKey = null; return; }
            double now = AhoraMs();
            if (!ReferenceEquals(progressKey, p.Key)) progressDraw = -1;
            progressDraw = SmoothProgress(progressDraw, p.Value, (now - progressClock) / 1000.0);
            if (!ProgresoFluido) progressDraw = Math.Max(0, Math.Min(1, p.Value));
            progressClock = now; progressKey = p.Key;
            float h = p.Rect.Height, x = p.Rect.X + Math.Min(4f, (h - 1) * 0.5f);
            float y = p.Rect.Y + (h - 1) * 0.5f;
            float width = Math.Max(0, p.Rect.Right - 1 - (x - p.Rect.X) - x);
            if (idleBlend > 0)
            {
                float expandedX = IdleProgressStart > 0 ? IdleProgressStart : IdleCoverRect(Width, idleH).Right + 180;
                x += (expandedX - x) * idleBlend;
                y += (Height - 18 - y) * idleBlend;
                width += (Math.Max(0, Width - 24 - expandedX) - width) * idleBlend;
            }
            ProgressBand(x, y, width, Math.Max(1, h - 2), 0x87080F23u);
            float length = (float)(width * progressDraw);
            if (!(length > 0)) return;
            uint tint = ProgressLight(p.Color, 0.35f);
            if (p.Neon && NeonIntenso) ProgressBand(x, y, length, h * 2.1f, 0x32000000u | tint);
            if (p.Neon) ProgressBand(x, y, length, Math.Max(1, h - 1), (NeonIntenso ? 0x78000000u : 0x2D000000u) | tint);
            ProgressBand(x, y, length, Math.Max(1, h * (NeonIntenso && p.Neon ? 0.65f : 0.48f)), (NeonIntenso && p.Neon ? 0xE0000000u : 0xA0000000u) | tint);
            if (p.Neon) ProgressBand(x, y, length, Math.Max(1, h * (NeonIntenso ? 0.26f : 0.16f)), 0xFF000000u | ProgressLight(p.Color, NeonIntenso ? 0.96f : 0.88f));
        }
        private static uint ProgressLight(uint c, float f)
        {
            uint r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            return ((uint)(r + (255-r)*f) << 16) | ((uint)(g + (255-g)*f) << 8) | (uint)(b + (255-b)*f);
        }
        // Coordenadas fraccionarias y borde de un pixel interpolado por GPU:
        // no cuantizar ni rehornear toda la barra de estado al avanzar.
        private void ProgressBand(float x, float y, float w, float h, uint color)
        {
            float edge = Math.Min(1f, w * 0.5f);
            float top = y - h * 0.5f, bottom = y + h * 0.5f;
            SetTexQuadA(progressQuad, x, top, x + w - edge, bottom, 1, 1, color);
            stars.DrawColorQuad(progressQuad);
            SetTexQuadA(progressQuad, x + w - edge, top, x + w + edge, bottom, 1, 1, color);
            progressQuad[1].Color &= 0x00FFFFFFu;
            progressQuad[2].Color &= 0x00FFFFFFu;
            progressQuad[4].Color &= 0x00FFFFFFu;
            stars.DrawColorQuad(progressQuad);
        }

        // MISMOS PIXELES QUE ANTES, escritos de otra manera. SetPixel es una llamada a GDI+ por
        // pixel -16 384 aqui- y esto son 2 ms que se pagan en cada reinicio del dispositivo. La
        // hermana de al lado, BakeNatural, ya lo hacia con LockBits y una sola copia por fila.
        internal static Bitmap BakeVineta(int n)
        {
            if (n < 2) throw new ArgumentOutOfRangeException("n");
            Bitmap b = new Bitmap(n, n, PixelFormat.Format32bppArgb);
            int[] pixels = new int[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double dx = 2.0 * x / (n - 1) - 1.0, dy = 2.0 * y / (n - 1) - 1.0;
                    double f = Math.Max(0.0, Math.Min(1.0, (dx * dx + dy * dy - 0.18) / 1.82));
                    f = f * f * (3.0 - 2.0 * f);
                    pixels[y * n + x] = unchecked((int)((uint)(110.0 * f + 0.5) << 24));   // negro; solo cambia el alfa
                }
            BitmapData data = b.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < n; y++) Marshal.Copy(pixels, y * n, new IntPtr(data.Scan0.ToInt64() + y * (long)data.Stride), n); }
            finally { b.UnlockBits(data); }
            return b;
        }
        // EL MAPA SE HORNEA UNA VEZ Y SE GUARDA. Lo que puede fallar en el reintento es la SUBIDA a la
        // tarjeta -al arrancar, o cuando Windows se lleva el dispositivo al bloquear la sesion-, nunca
        // el horneado. Antes se rehorneaba entero en cada reintento, o sea cada segundo mientras durara
        // el fallo. Es la misma precaucion que ya tenia EnsureNieblas dos funciones mas arriba.
        //
        // Y al APAGAR el interruptor se suelta todo: antes la textura se quedaba puesta en la tarjeta
        // hasta cerrar la rocola, aunque ya no se dibujara.
        private Bitmap vinetaMapa;
        private void EnsureVineta()
        {
            if (vinetaEstiloDe != FondoNatural) {
                if (vinetaTex != IntPtr.Zero) { D3D9.Release(vinetaTex); vinetaTex = IntPtr.Zero; }
                if (vinetaMapa != null) { vinetaMapa.Dispose(); vinetaMapa = null; }
                vinetaTeselas = null; vinetaRetry = 0; vinetaEstiloDe = FondoNatural;
            }
            if (!VinetaSuave)
            {
                if (vinetaTex != IntPtr.Zero) { D3D9.Release(vinetaTex); vinetaTex = IntPtr.Zero; }
                if (vinetaMapa != null) { vinetaMapa.Dispose(); vinetaMapa = null; }
                vinetaTeselas = null;
                vinetaRetry = 0;
                return;
            }
            if (vinetaTex != IntPtr.Zero) return;
            int now = Environment.TickCount;
            if (vinetaRetry != 0 && unchecked(now - vinetaRetry) < 1000) return;
            vinetaRetry = now;
            if (vinetaMapa == null) vinetaMapa = FondoNatural ? BakeVinetaNatural(128) : BakeVineta(128);
            if (vinetaTeselas == null) vinetaTeselas = TeselasVisibles.De(vinetaMapa, TeselasVisibles.LadoTesela, true);   // su centro es transparente (DibujoLigeroGpu.cs)
            vinetaTex = stars.CreateOverlay(vinetaMapa, out vinetaU, out vinetaV);
        }

        // Solo tinte de los vertices GPU: mismas texturas y dos pasadas, sin buffers
        // ni asignaciones nuevas por cuadro. Conserva parte del acento musical.
        internal static uint TinteNebulosa(uint original, bool segunda, bool activo)
        {
            if (!activo) return original & 0x00FFFFFFu;
            uint baseColor = segunda ? 0x009C70E8u : 0x003899D8u;
            uint r = ((((original >> 16) & 255) * 35 + ((baseColor >> 16) & 255) * 65 + 50) / 100);
            uint g = ((((original >> 8) & 255) * 35 + ((baseColor >> 8) & 255) * 65 + 50) / 100);
            uint b = (((original & 255) * 35 + (baseColor & 255) * 65 + 50) / 100);
            return (r << 16) | (g << 8) | b;
        }

        private void DibujaNieblas()
        {
            if (Nieblas == GpuStarfield.Bajo) return;   // perilla de Ctrl+F3
            if (hazTex == IntPtr.Zero && nebTex == IntPtr.Zero) return;
            if (nebTex != IntPtr.Zero)
            {
                // El color va en el VERTICE: la tarjeta multiplica la textura por el y las nebulosas cambian
                // de color con la cancion sin volver a subir nada.
                //
                // Y AQUI ESTA EL MOVIMIENTO. La misma capa se dibuja DOS VECES -una del color del genero y
                // otra del de la caratula- yendo a la deriva en sentidos contrarios. Donde se cruzan sale la
                // mezcla de los dos colores, y como las dos se mueven, la mezcla se mueve. Son dos
                // cuadrilateros a pantalla completa en vez de uno: para la tarjeta no es nada.
                uint a = (uint)(NebAlfa * 255f + 0.5f); if (a > 255) a = 255;
                float mx = Width * NebDeriva, my = Height * NebDeriva;
                float c = (float)Math.Cos(NebFase), sn = (float)Math.Sin(NebFase);
                NieblaDeriva(mx, my, c * mx, sn * my, (a << 24) | TinteNebulosaEstilo(NebColor, false, NebulosasCine, FondoNatural));
                NieblaDeriva(mx, my, -c * mx, -sn * my, (a << 24) | TinteNebulosaEstilo(NebColor2, true, NebulosasCine, FondoNatural));
            }
            if (hazTex != IntPtr.Zero && Nieblas == GpuStarfield.Alto)
            {
                // El cuadrilatero se sale de la pantalla lo que dice el margen por los cuatro lados, asi que
                // por mucho que se mueva su borde nunca asoma. Es lo mismo que hace NieblaDeriva.
                float mx = Width * HazDeriva, my = Height * HazDeriva;
                float ox = (float)Math.Sin(HazFase) * mx, oy = (float)Math.Cos(HazFase) * my;
                DibujaTeselas(hazTex, hazTeselas, -mx + ox, -my + oy, Width + mx + ox, Height + my + oy, hazU, hazV, 0xFFFFFFFFu, nieblaQuad);   // solo lo que no es transparente (DibujoLigeroGpu.cs)
            }
        }

        // Una pasada de la nebulosa, corrida (dx, dy) de su sitio. El cuadrilatero se sale de la pantalla por
        // los cuatro lados lo que dice el margen, asi que por mucho que se mueva nunca asoma su borde.
        private void NieblaDeriva(float mx, float my, float dx, float dy, uint argb)
        {
            SetTexQuadA(nieblaQuad, -mx + dx, -my + dy, Width + mx + dx, Height + my + dy, nebU, nebV, argb);
            stars.DrawQuad(nebTex, nieblaQuad);
        }

        // ESCALERA DE TONOS: aqui esta lo que hace que se lea la intensidad. NO se dibuja subiendo el brillo
        // hacia el BLANCO -eso lava el color y es lo que se veia feo- sino recorriendo TONOS DE LA MISMA
        // FAMILIA. Si el genero es morado: lo flojo sale morado oscuro y profundo, lo medio sale del morado
        // del genero, y lo fuerte sale de un morado claro, casi lila. Nunca se sale de su color.
        //
        // La cuenta: al subir la intensidad sube el BRILLO del tono y baja su SATURACION, que es justo lo que
        // distingue un tono oscuro de uno claro dentro de una misma familia. La saturacion nunca baja del 45%,
        // que es el seguro para que jamas llegue a blanco.
        private static void TonoPorNivel(ref float r, ref float g, ref float b, float v)
        {
            if (!(v > 0f)) v = 0f; else if (v > 1f) v = 1f;   // "no es mayor que 0" tambien atrapa NaN
            float mx = r; if (g > mx) mx = g; if (b > mx) mx = b;
            if (mx < 1f) return;                                   // practicamente negro: nada que matizar
            float mn = r; if (g < mn) mn = g; if (b < mn) mn = b;
            float sat = (mx - mn) / mx;                            // 0 = gris, 1 = color puro
            float val = mx * (0.50f + 0.50f * v);                  // hundido cuando esta flojo, claro cuando pega
            if (val > 255f) val = 255f;
            float satNueva = sat * (1f - 0.09f * v);               // al aclararse pierde algo de saturacion, como un tono claro real
            // Se reconstruye conservando el TONO: cada canal guarda su posicion relativa dentro de la saturacion,
            // y esa posicion se vuelve a estirar contra la saturacion nueva.
            float piso = 1f - sat;
            float inv = (sat > 0.0001f) ? (1f / sat) : 0f;
            float tr = (sat > 0.0001f) ? ((r / mx) - piso) * inv : 1f;
            float tg = (sat > 0.0001f) ? ((g / mx) - piso) * inv : 1f;
            float tb = (sat > 0.0001f) ? ((b / mx) - piso) * inv : 1f;
            float pisoNuevo = 1f - satNueva;
            r = val * (pisoNuevo + satNueva * tr);
            g = val * (pisoNuevo + satNueva * tg);
            b = val * (pisoNuevo + satNueva * tb);
            if (r < 0f) r = 0f; else if (r > 255f) r = 255f;
            if (g < 0f) g = 0f; else if (g > 255f) g = 255f;
            if (b < 0f) b = 0f; else if (b > 255f) b = 255f;
        }

        // SILUETA. Devuelve, para una posicion de 0 (izquierda) a 1 (derecha) de la pantalla, que tan alto
        // llega el color ahi. Cada banda pone un LOBULO redondeado centrado en su sitio -la primera pegada al
        // borde izquierdo, la ultima al derecho- y se toma el MAS ALTO de los que alcanzan ese punto. Tomando
        // el mas alto (en vez de sumarlos) cada banda conserva su propio bulto y entre bulto y bulto queda un
        // valle: por eso se distinguen las frecuencias, en vez de salir una loma sin forma.
        //
        // El "mas alto" se toma SUAVE (la raiz cuarta de la suma de las cuartas potencias) y no a secas. El
        // maximo a secas deja un PICO en el cruce de dos lobulos, y la malla, que une los puntos con rectas,
        // corta ese pico y deja una esquina a la vista. Asi la silueta no tiene ni un solo pico: la banda que
        // manda sigue mandando igual, pero el cruce con la vecina pasa redondo.
        internal static float AurPerfil(float[] B, float fx)
        {
            int n = (B != null) ? B.Length : 0;
            if (n < 2) return 0f;
            float t = fx * (n - 1);
            int b0 = (int)t - 1; if (b0 < 0) b0 = 0;
            double suma = 0.0;
            for (int b = b0; b < n && b <= b0 + 3; b++)
            {
                float d = t - b; if (d < 0f) d = -d;
                d /= AurLobulo; if (d >= 1f) continue;
                float k = 1f - d * d * (3f - 2f * d);   // 1 en el centro de la banda, 0 en su borde, sin picos
                double v = B[b] * k; v *= v;            // al cuadrado
                suma += v * v;                          // ...y otra vez: la cuarta potencia
            }
            if (suma <= 0.0) return 0f;
            return (float)Math.Sqrt(Math.Sqrt(suma));
        }

        // CORTINAS. El multiplicador de altura de la columna que esta en fx (0..1) en el segundo t. Son tres
        // ondas de periodos que no encajan entre si, cada una deslizandose a su ritmo: por eso el dibujo NUNCA
        // se repite igual y no se ve un patron. Las de periodo corto hacen las vetas finas de luz y la larga
        // mece la cortina entera de un lado a otro. Va acotado para que ninguna columna se dispare ni se apague.
        internal static float AurRayo(float fx, float t)
        {
            double x = fx * 6.2831853;
            float a = (float)Math.Sin(x * 17.9 + t * 0.44);   // vetas finas (las que dan el aire de cortina)
            float b = (float)Math.Sin(x * 9.3 - t * 0.31);    // vetas anchas
            float c = (float)Math.Sin(x * 3.7 + t * 0.55);    // ondas medianas
            float d = (float)Math.Sin(x * 1.3 + t * 0.13);    // el meneo lento de toda la cortina
            float v = 1f + 0.09f * a + 0.11f * b + 0.09f * c + 0.07f * d;
            if (!(v > 0.64f)) v = 0.64f; else if (v > 1.36f) v = 1.36f;   // el "no es mayor que" atrapa tambien NaN
            return v;
        }

        // SUAVIZADO DE LA SILUETA. Donde se cruzan dos bandas vecinas queda un valle estrecho -cuatro
        // columnas- y el fondo de ese valle es una punta con el radio de una aguja: ocho pixeles. En el
        // relleno no se veia, porque su borde se desvanece; el filo de neon lo dibuja, y ahi se ve como un
        // diente de sierra. Por muchos puntos que se le echen encima, una punta de ocho pixeles se sigue
        // viendo en punta: hay que redondear la forma, no dibujarla mas fino.
        //
        // Se le pasa una media pesada de cinco columnas (1-4-6-4-1). Las lomas y los valles se quedan donde
        // estaban y casi con la misma hondura -son mucho mas anchos que la media-, pero ninguna punta queda
        // ya con radio de aguja. Cuesta 145 sumas por cuadro.
        internal static void AurSuaviza(float[] src, float[] dst)
        {
            int n = AurCols;
            for (int i = 0; i <= n; i++)
            {
                int a = i - 2; if (a < 0) a = 0;
                int b = i - 1; if (b < 0) b = 0;
                int c = i + 1; if (c > n) c = n;
                int d = i + 2; if (d > n) d = n;
                dst[i] = (src[a] + 4f * src[b] + 6f * src[i] + 4f * src[c] + src[d]) * 0.0625f;
            }
        }
        // LA FORMA EN REPOSO. Cuando no suena nada no hay bandas que seguir y la cortina se queda PLANA,
        // y una raya recta no parece una aurora: parece una neblina. Esto le pone unas lomas anchas que se
        // van corriendo muy despacio de un lado a otro -media vuelta tarda casi un minuto-, que es lo unico
        // que se mueve con la rocola callada. Por eso va lenta: tiene que estar viva sin llamar la atencion.
        //
        // Se toma la MAS ALTA entre esto y lo que traen las bandas, no la suma: en cuanto vuelve la musica,
        // la musica manda y esto desaparece por debajo sin que se note el relevo.
        internal static float AurCalma(float fx, float t)
        {
            double x = fx * 6.2831853;
            float a = (float)Math.Sin(x * 1.7 + t * 0.11);   // las lomas grandes
            float b = (float)Math.Sin(x * 2.9 - t * 0.07);   // otras que no encajan con las primeras
            float v = 0.30f + 0.13f * a + 0.09f * b;
            if (!(v > 0.05f)) v = 0.05f; else if (v > 0.55f) v = 0.55f;   // acotada como las demas (y atrapa NaN)
            return v;
        }
        // LO QUE ONDULA LA ALTURA de la cortina. Antes era el mismo AurRayo que pone las vetas, y ahi
        // estaba el problema: la veta fina va y viene casi dieciocho veces de lado a lado de la pantalla
        // -un diente cada 39 pixeles- y, metida en la ALTURA, dejaba la silueta hecha un serrucho. Un
        // diente asi tiene el radio de una aguja: por muchos puntos que se le echen encima, en el filo
        // se ve como una sierra. Mientras el relleno se desvanecia arriba sin dejar borde no se notaba;
        // con el filo de neon dibujando la silueta, se veia entero.
        //
        // Asi que la altura se queda solo con las ondas LARGAS, las que mecen la cortina de un lado a
        // otro, y las vetas finas se quedan donde de verdad se ven: en el BRILLO (ver AurRayo). Que es
        // ademas como es una aurora de verdad: el borde va limpio y lo que titila es la luz.
        internal static float AurOndaAlto(float fx, float t)
        {
            double x = fx * 6.2831853;
            float a = (float)Math.Sin(x * 3.7 + t * 0.55);    // el ondeo mediano
            float b = (float)Math.Sin(x * 2.3 - t * 0.29);    // otro que no encaja con el anterior
            float c = (float)Math.Sin(x * 1.3 + t * 0.13);    // el meneo lento de toda la cortina
            float v = 1f + 0.11f * a + 0.08f * b + 0.09f * c;
            if (!(v > 0.72f)) v = 0.72f; else if (v > 1.28f) v = 1.28f;   // idem
            return v;
        }
        // DESVANECIDO a lo alto: cuanta opacidad le queda al color a la altura f (0 = donde nace, 1 = la
        // punta).
        //
        // Antes era una S que a media altura ya iba por la mitad y a tres cuartos no quedaba casi nada: la
        // cortina tenia color pegado abajo y de ahi para arriba era una neblina. Con el filo de neon
        // dibujando la silueta, ese hueco entre el filo y el color se notaba muchisimo -se veian las rayas
        // y poco mas-, asi que ahora el color LLENA la cortina: a media altura conserva cuatro quintos y a
        // tres cuartos, la mitad larga. El ultimo tramo si se apaga deprisa y llega a cero con la pendiente
        // tambien a cero, para que el relleno muera sin dejar canto por debajo del filo.
        internal static float AurBaja(float f)
        {
            if (!(f > 0f)) return 1f;   // tambien atrapa NaN
            if (f >= 1f) return 0f;
            float cuerpo = 1f - (float)Math.Pow(f, 2.2);      // el cuerpo, que ahora aguanta arriba
            float f4 = f * f; f4 *= f4;
            return cuerpo * (1f - f4 * f4);                   // y el remate: se acaba en la punta, sin canto
        }

        // Cuanto del ONDEO se deja ver a la altura f. En el horizonte, NADA (ahi va el texto de la franja y no
        // se le puede estar encendiendo y apagando el fondo debajo); de media cortina para arriba, TODO. Sin
        // esto el ondeo solo movia la altura de la columna, y como el borde de arriba se desvanece sin filo, el
        // ojo no lo veia: la aurora salia como una neblina lisa. La veta se ve porque cambia el BRILLO.
        internal static float AurRampa(float f)
        {
            float r = f * 2f;
            if (!(r > 0f)) r = 0f; else if (r > 1f) r = 1f;
            return r;
        }

        // EL SUELO, por debajo del horizonte (fs: 0 = el horizonte, 1 = el borde de abajo de la pantalla).
        // El color se hunde a un tono profundo de la misma familia Y ademas pierde presencia, hasta quedarse
        // exactamente en el cielo al llegar al borde. Asi el fondo no se corta en ningun sitio -abajo del todo
        // simplemente ya es el cielo- y el texto de la franja de estado queda sobre oscuro, que es donde se lee.
        // Las dos caen DEPRISA nada mas pasar el horizonte: asi la cortina puede ser mucho mas presente
        // arriba sin comerse el texto de la franja, que empieza a solo 22 pixeles por debajo.
        internal static float AurSueloTono(float fs)
        {
            float k = 1f - fs; if (!(k > 0f)) k = 0f; else if (k > 1f) k = 1f;
            return AurHondo + (1f - AurHondo) * k * k * k;
        }
        internal static float AurSueloAlfa(float fs)
        {
            float k = 1f - fs; if (!(k > 0f)) k = 0f; else if (k > 1f) k = 1f;
            k *= k;
            return k * k;
        }

        // MEZCLA DE TONOS. Entre el color del pie y el de la punta hay bastantes grados de rueda, y mezclarlos
        // "a lo bruto" (canal a canal) atraviesa el centro de la rueda: a media altura el color se ensucia y
        // pierde fuerza, que es lo que hacia que los picos no se vieran parejos. Aqui la mezcla se hace por el
        // BORDE de la rueda -girando el tono por el camino corto- y ademas se mezclan aparte la viveza y el
        // brillo, asi que el degradado sale limpio de arriba abajo.
        private static uint AurTonoMezcla(uint c0, uint c1, float f)
        {
            if (f <= 0f) return c0;
            if (f >= 1f) return c1;
            float h0, s0, v0, h1, s1, v1;
            AurHsv(c0, out h0, out s0, out v0);
            AurHsv(c1, out h1, out s1, out v1);
            float d = h1 - h0;
            while (d > 180f) d -= 360f; while (d < -180f) d += 360f;   // siempre por el camino corto
            float h = h0 + d * f;
            while (h < 0f) h += 360f; while (h >= 360f) h -= 360f;
            return AurRgb(h, s0 + (s1 - s0) * f, v0 + (v1 - v0) * f);
        }
        private static void AurHsv(uint c, out float h, out float s, out float v)
        {
            float r = ((c >> 16) & 0xFF) / 255f, g = ((c >> 8) & 0xFF) / 255f, b = (c & 0xFF) / 255f;
            float mx = r; if (g > mx) mx = g; if (b > mx) mx = b;
            float mn = r; if (g < mn) mn = g; if (b < mn) mn = b;
            v = mx; float d = mx - mn;
            s = (mx <= 0.0001f) ? 0f : d / mx;
            if (d <= 0.0001f) { h = 0f; return; }
            if (mx == r) h = 60f * (((g - b) / d) % 6f);
            else if (mx == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            while (h < 0f) h += 360f; while (h >= 360f) h -= 360f;
        }
        private static uint AurRgb(float h, float s, float v)
        {
            if (s < 0f) s = 0f; else if (s > 1f) s = 1f;
            if (v < 0f) v = 0f; else if (v > 1f) v = 1f;
            float c = v * s, x = c * (1f - Math.Abs((h / 60f) % 2f - 1f)), m = v - c;
            float r, g, b;
            if (h < 60f) { r = c; g = x; b = 0f; }
            else if (h < 120f) { r = x; g = c; b = 0f; }
            else if (h < 180f) { r = 0f; g = c; b = x; }
            else if (h < 240f) { r = 0f; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }
            uint ri = (uint)((r + m) * 255f + 0.5f), gi = (uint)((g + m) * 255f + 0.5f), bi = (uint)((b + m) * 255f + 0.5f);
            if (ri > 255) ri = 255; if (gi > 255) gi = 255; if (bi > 255) bi = 255;
            return (ri << 16) | (gi << 8) | bi;
        }
        // Mezcla canal a canal: solo para lo ANCHO, donde los dos tonos estan a 32 grados y no hay nada que ensuciar.
        // Por entero y no por "sin signo": son los mismos numeros (de 0 a 255, con f entre 0 y 1) y se ahorra la
        // conversion lenta entre decimales y enteros sin signo, que aqui se hace miles de veces por cuadro.
        private static uint AurLerpRgb(uint c0, uint c1, float f)
        {
            int r0 = (int)((c0 >> 16) & 0xFF), g0 = (int)((c0 >> 8) & 0xFF), b0 = (int)(c0 & 0xFF);
            uint r = (uint)(int)(r0 + (((int)((c1 >> 16) & 0xFF) - r0) * f));
            uint g = (uint)(int)(g0 + (((int)((c1 >> 8) & 0xFF) - g0) * f));
            uint b = (uint)(int)(b0 + (((int)(c1 & 0xFF) - b0) * f));
            return (r << 16) | (g << 8) | b;
        }

        private static uint AurArgb(uint rgb, float a)
        {
            int v = (int)(a * 255f + 0.5f);
            if (v < 0) v = 0; else if (v > 255) v = 255;
            return ((uint)v << 24) | rgb;
        }
        // Por referencia: una comprobacion de limites por vertice en vez de siete (se escriben miles por cuadro).
        private static void AurV3(ref D3D9.Vtx a, float x, float y, uint c)
        { a.X = x - 0.5f; a.Y = y - 0.5f; a.Z = 0f; a.Rhw = 1f; a.Color = c; a.U = 0f; a.V = 0f; }

        // Arma la malla entera y devuelve cuantos vertices escribio. Aparte del dibujo para poder comprobarla
        // sin tarjeta. 'alturaPanel' es el borde de abajo de la PANTALLA: el color baja hasta ahi (por detras
        // de la franja de estado) hundiendose al tono profundo, para que no quede ninguna raya donde se acaba.
        internal static int AurMalla(D3D9.Vtx[] v, float[] hCol, uint[] cCortina, uint[] cSuelo, float[] aCol, float[] rCol,
                                     float ancho, float baseY, float alturaPanel)
        { return AurMallaEstilo(v, hCol, cCortina, cSuelo, aCol, rCol, ancho, baseY, alturaPanel, false); }

        private static int AurMallaEstilo(D3D9.Vtx[] v, float[] hCol, uint[] cCortina, uint[] cSuelo, float[] aCol, float[] rCol,
                                         float ancho, float baseY, float alturaPanel, bool natural)
        {
            int puntos = AurCols + 1;
            if (v == null || hCol == null || cCortina == null || cSuelo == null || aCol == null || rCol == null) return 0;
            if (v.Length < (AurFilas + AurSuelo) * AurCols * 6) return 0;
            if (hCol.Length < puntos || aCol.Length < puntos || rCol.Length < puntos) return 0;
            if (cCortina.Length < (AurFilas + 1) * puntos) return 0;
            if (cSuelo.Length < (AurSuelo + 1) * puntos) return 0;
            float borde = (alturaPanel > baseY) ? alturaPanel : baseY;   // el borde de abajo de la pantalla
            float hondo = borde - baseY;
            float paso = ancho / AurCols;
            int n = 0;
            // Primero EL SUELO (del horizonte al borde de abajo) y luego la cortina. Las dos partes comparten
            // el color y la opacidad exactos del horizonte, asi que ahi no hay ni un salto: es la misma pieza.
            for (int j = 0; j < AurSuelo; j++)
            {
                float s0 = (float)j / AurSuelo, s1 = (float)(j + 1) / AurSuelo;
                float m0 = AurSueloAlfa(s0), m1 = AurSueloAlfa(s1);
                int k0 = j * puntos, k1 = (j + 1) * puntos;
                float y0 = baseY + hondo * s0, y1 = baseY + hondo * s1;
                // El borde DERECHO de una celda es el IZQUIERDO de la de al lado: mismo color, misma opacidad,
                // misma cuenta. Se arrastra en vez de calcularlo dos veces.
                uint cL0 = AurArgb(cSuelo[k0], aCol[0] * m0), cL1 = AurArgb(cSuelo[k1], aCol[0] * m1);
                for (int i = 0; i < AurCols; i++)
                {
                    float x0 = i * paso, x1 = x0 + paso;
                    uint cR0 = AurArgb(cSuelo[k0 + i + 1], aCol[i + 1] * m0), cR1 = AurArgb(cSuelo[k1 + i + 1], aCol[i + 1] * m1);
                    AurV3(ref v[n + 0], x0, y0, cL0); AurV3(ref v[n + 1], x1, y0, cR0); AurV3(ref v[n + 2], x0, y1, cL1);
                    AurV3(ref v[n + 3], x1, y0, cR0); AurV3(ref v[n + 4], x1, y1, cR1); AurV3(ref v[n + 5], x0, y1, cL1);
                    n += 6;
                    cL0 = cR0; cL1 = cR1;
                }
            }
            for (int j = 0; j < AurFilas; j++)
            {
                float f0 = natural ? AuroraNaturalNivel(j) : (float)j / AurFilas;
                float f1 = natural ? AuroraNaturalNivel(j + 1) : (float)(j + 1) / AurFilas;
                float m0 = natural ? BajaNatural(f0) : AurBaja(f0), m1 = natural ? BajaNatural(f1) : AurBaja(f1);
                float g0 = AurRampa(f0), g1 = AurRampa(f1);   // cuanto ondeo se deja ver a esa altura
                int k0 = j * puntos, k1 = (j + 1) * puntos;
                // Lo mismo que en el suelo: el lado derecho de una celda es el izquierdo de la siguiente, y eso
                // son dos alturas, dos pesos y dos colores por celda que se estaban calculando dos veces. El
                // ondeo enciende y apaga el BRILLO de la veta, pero NO le cambia el color: asi todas las
                // cortinas llevan el mismo degradado y los picos se ven parejos entre si.
                float yL0 = baseY - hCol[0] * f0, yL1 = baseY - hCol[0] * f1;
                float wL0 = aCol[0] * m0 * (1f + (rCol[0] - 1f) * g0), wL1 = aCol[0] * m1 * (1f + (rCol[0] - 1f) * g1);
                uint cL0 = natural ? AurArgb(cCortina[k0] & 0xFFFFFFu, wL0 * AuroraNaturalPeso(cCortina[k0])) : AurArgb(cCortina[k0], wL0);
                uint cL1 = natural ? AurArgb(cCortina[k1] & 0xFFFFFFu, wL1 * AuroraNaturalPeso(cCortina[k1])) : AurArgb(cCortina[k1], wL1);
                for (int i = 0; i < AurCols; i++)
                {
                    float x0 = i * paso, x1 = x0 + paso;
                    float hR = hCol[i + 1];
                    float yR0 = baseY - hR * f0, yR1 = baseY - hR * f1;
                    float wR0 = aCol[i + 1] * m0 * (1f + (rCol[i + 1] - 1f) * g0), wR1 = aCol[i + 1] * m1 * (1f + (rCol[i + 1] - 1f) * g1);
                    uint rgb0 = cCortina[k0 + i + 1], rgb1 = cCortina[k1 + i + 1];
                    uint cR0 = natural ? AurArgb(rgb0 & 0xFFFFFFu, wR0 * AuroraNaturalPeso(rgb0)) : AurArgb(rgb0, wR0);
                    uint cR1 = natural ? AurArgb(rgb1 & 0xFFFFFFu, wR1 * AuroraNaturalPeso(rgb1)) : AurArgb(rgb1, wR1);
                    AurV3(ref v[n + 0], x0, yL1, cL1); AurV3(ref v[n + 1], x1, yR1, cR1); AurV3(ref v[n + 2], x0, yL0, cL0);
                    AurV3(ref v[n + 3], x1, yR1, cR1); AurV3(ref v[n + 4], x1, yR0, cR0); AurV3(ref v[n + 5], x0, yL0, cL0);
                    n += 6;
                    yL0 = yR0; yL1 = yR1; cL0 = cR0; cL1 = cR1;
                }
            }
            return n;
        }

        // ACLARA un color hacia el blanco sin cambiarle el tono: k=0 lo deja igual, k=1 lo deja blanco. Es
        // lo que hace que el NUCLEO del filo se lea como neon encendido y no como una raya de color mas.
        private static uint AurNeon(uint rgb, float k)
        {
            if (k <= 0f) return rgb;
            if (k > 1f) k = 1f;
            uint r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            r = (uint)(r + (255f - r) * k); g = (uint)(g + (255f - g) * k); b = (uint)(b + (255f - b) * k);
            return (r << 16) | (g << 8) | b;
        }

        // ALTURA DE LA SILUETA EN MEDIO DE DOS PUNTOS, por una curva que pasa por todos ellos. Es la de
        // siempre para esto (Catmull-Rom): mira tambien al punto de antes y al de despues, asi que en una
        // cima no corta en recta sino que la redondea.
        //
        // En los DOS EXTREMOS de la pantalla no hay punto de antes ni de despues. Repetir el del borde
        // -que es lo primero que uno hace- le miente a la curva diciendole que ahi la silueta se aplana, y
        // el resultado era un codo justo en el borde izquierdo, el peor de toda la pantalla. Se prolonga la
        // recta que traen los dos primeros (o los dos ultimos), que es como si la silueta siguiera igual.
        internal static float AurCurvaH(float[] h, int i, float u)
        {
            if (u <= 0f) return !(h[i] > 0f) ? 0f : h[i];   // en los puntos, la altura tal cual (nunca negativa)
            float p1 = h[i];
            float p2 = h[i < AurCols ? i + 1 : AurCols];
            float p0 = (i > 0) ? h[i - 1] : (2f * p1 - p2);
            float p3 = (i + 2 <= AurCols) ? h[i + 2] : (2f * p2 - p1);
            float c1 = p2 - p0;
            float c2 = 2f * p0 - 5f * p1 + 4f * p2 - p3;
            float c3 = 3f * p1 - 3f * p2 + p3 - p0;
            float v = 0.5f * (2f * p1 + (c1 + (c2 + c3 * u) * u) * u);
            return !(v > 0f) ? 0f : v;   // tambien atrapa NaN
        }

        // Una franja horizontal de la malla: dos alturas (la de arriba y la de abajo) por cada lado, con su
        // color y su brillo ya resueltos. Todo el neon se arma llamando a esto unas cuantas veces.
        private static int AurBanda(D3D9.Vtx[] v, int n, float x0, float x1,
                                    float yL0, float yR0, float yL1, float yR1,
                                    uint cL0, uint cR0, uint cL1, uint cR1)
        {
            AurV3(ref v[n + 0], x0, yL0, cL0); AurV3(ref v[n + 1], x1, yR0, cR0); AurV3(ref v[n + 2], x0, yL1, cL1);
            AurV3(ref v[n + 3], x1, yR0, cR0); AurV3(ref v[n + 4], x1, yR1, cR1); AurV3(ref v[n + 5], x0, yL1, cL1);
            return n + 6;
        }

        // EL BRILLO: el contorno de neon que sigue la silueta de la cortina y la linea encendida del
        // horizonte sobre la que la aurora esta parada. Va en su propia malla porque se dibuja SUMANDO luz,
        // no tapando (ver DrawMeshAdd). Aparte del dibujo, como AurMalla, para poder comprobarla sin tarjeta.
        //   hCol  = altura de la cortina en cada punto (el filo la sigue)
        //   cNeon = color del filo en cada punto (el de la PUNTA de esa columna)
        //   nCol  = brillo del filo en cada punto (0..1)
        //   cLinea/gCol = lo mismo para la linea del horizonte (color del PIE y su brillo)
        internal static int AurBrillo(D3D9.Vtx[] v, float[] hCol, uint[] cNeon, float[] nCol,
                                      uint[] cLinea, float[] gCol, float ancho, float baseY)
        { return AurBrilloEstilo(v, hCol, cNeon, nCol, cLinea, gCol, ancho, baseY, false); }

        private static int AurBrilloEstilo(D3D9.Vtx[] v, float[] hCol, uint[] cNeon, float[] nCol,
                                         uint[] cLinea, float[] gCol, float ancho, float baseY, bool natural)
        {
            int puntos = AurCols + 1;
            if (v == null || hCol == null || cNeon == null || nCol == null || cLinea == null || gCol == null) return 0;
            if (v.Length < AurBrilloVtx) return 0;
            if (hCol.Length < puntos || nCol.Length < puntos || gCol.Length < puntos) return 0;
            if (cNeon.Length < puntos || cLinea.Length < puntos) return 0;
            int n = 0;
            // ---- LA LINEA DEL HORIZONTE (primero: el contorno va encima) ----
            float lineAbove = natural ? 24f : AurLineaArriba, lineBelow = natural ? 8f : AurLineaAbajo;
            float lineCore = natural ? 1f : AurLineaNucleo, white = natural ? .27f : AurNeonBlanco;
            float glowOutside = natural ? 11f : AurNeonFuera, glowInside = natural ? 18f : AurNeonDentro;
            float glowCore = natural ? .85f : AurNeonNucleo;
            float lqA = lineAbove * AurLineaQuiebre, lqB = lineBelow * AurLineaQuiebre;
            float ejeY = baseY - AurLineaAlza;   // la raya va un poco por encima del pie (ver AurLineaAlza)
            float yFa = ejeY - lineAbove, yMa = ejeY - lqA, yNa = ejeY - lineCore;
            float yNb = ejeY + lineCore, yMb = ejeY + lqB, yFb = ejeY + lineBelow;
            int salto = AurCols / AurLineaCols;
            float pasoL = ancho / AurLineaCols;
            // El borde DERECHO de un tramo es exactamente el IZQUIERDO del siguiente: mismo color y misma
            // opacidad. Se arrastran en vez de volver a calcularlos -eran cuatro cuentas de bits por tramo
            // hechas dos veces- y de paso desaparece el par mLb/mRb, que era literalmente mLa/mRa otra vez.
            uint pL = cLinea[0];
            uint kL = AurNeon(pL, white);
            float aL = gCol[0];
            uint fL = AurArgb(pL, 0f);                                                 // el borde del resplandor: apagado
            uint mL = AurArgb(pL, aL * AurLineaHalo);
            uint nLc = AurArgb(kL, aL);                                                // el nucleo, aclarado
            for (int i = 0; i < AurLineaCols; i++)
            {
                float x0 = i * pasoL, x1 = x0 + pasoL;
                int j1 = (i + 1) * salto;
                uint pR = cLinea[j1];
                uint kR = AurNeon(pR, white);
                float aR = gCol[j1];
                uint fR = AurArgb(pR, 0f);
                uint mR = AurArgb(pR, aR * AurLineaHalo);
                uint nRc = AurArgb(kR, aR);
                n = AurBanda(v, n, x0, x1, yFa, yFa, yMa, yMa, fL, fR, mL, mR);
                n = AurBanda(v, n, x0, x1, yMa, yMa, yNa, yNa, mL, mR, nLc, nRc);
                n = AurBanda(v, n, x0, x1, yNa, yNa, yNb, yNb, nLc, nRc, nLc, nRc);
                n = AurBanda(v, n, x0, x1, yNb, yNb, yMb, yMb, nLc, nRc, mL, mR);
                n = AurBanda(v, n, x0, x1, yMb, yMb, yFb, yFb, mL, mR, fL, fR);
                fL = fR; mL = mR; nLc = nRc;
            }
            // ---- EL CONTORNO, siguiendo la silueta por la curva (ver AurContCols) ----
            float qf = glowOutside * AurNeonQuiebre;
            int sub = AurContCols / AurCols;
            float pasoC = ancho / AurContCols;
            float tL = baseY - hCol[0];
            uint cIz = cNeon[0]; float aIz = nCol[0];
            // Igual que en la raya: el extremo derecho de una columna ES el izquierdo de la siguiente. Antes se
            // recalculaban los cuatro colores en cada vuelta -288 vueltas por cuadro, cuatro cuentas de bits
            // cada una, todas repetidas-; ahora se arrastran.
            uint kIz = AurNeon(cIz, white);
            uint cfL = AurArgb(cIz, 0f);
            uint cmL = AurArgb(cIz, aIz * AurNeonHalo);
            uint cnL = AurArgb(kIz, aIz);
            for (int q = 0; q < AurContCols; q++)
            {
                int k = q + 1;
                int i = k / sub; float u = (k - i * sub) / (float)sub;
                if (i > AurCols) { i = AurCols; u = 0f; }
                int i2 = (i < AurCols) ? i + 1 : AurCols;
                float tR = baseY - AurCurvaH(hCol, i, u);              // la cresta al final de este tramo
                uint cDe = (u <= 0f) ? cNeon[i] : AurLerpRgb(cNeon[i], cNeon[i2], u);
                float aDe = nCol[i] + (nCol[i2] - nCol[i]) * u;
                float x0 = q * pasoC, x1 = x0 + pasoC;
                uint kR = AurNeon(cDe, white);
                uint cfR = AurArgb(cDe, 0f);
                uint cmR = AurArgb(cDe, aDe * AurNeonHalo);
                uint cnR = AurArgb(kR, aDe);
                n = AurBanda(v, n, x0, x1, tL - glowOutside, tR - glowOutside, tL - qf, tR - qf, cfL, cfR, cmL, cmR);
                n = AurBanda(v, n, x0, x1, tL - qf, tR - qf, tL - glowCore, tR - glowCore, cmL, cmR, cnL, cnR);
                n = AurBanda(v, n, x0, x1, tL - glowCore, tR - glowCore, tL + glowCore, tR + glowCore, cnL, cnR, cnL, cnR);
                n = AurBanda(v, n, x0, x1, tL + glowCore, tR + glowCore, tL + glowInside, tR + glowInside, cnL, cnR, cfL, cfR);
                tL = tR; aIz = aDe;
                cfL = cfR; cmL = cmR; cnL = cnR;
            }
            return n;
        }

        private void DrawAurora(GpuAurora au)
        {
            bool updateWave = SampleWave(ref au);
            // au es una copia: no modificar el espectro ni el renderizador CPU.
            if (OndasSuaves) { au.Alto *= 0.78f; au.Dim *= 0.85f; au.DimFilo *= 0.65f; }
            if (au.Alto < 2f || au.Dim <= 0.02f) return;
            int puntos = AurCols + 1;
            if (aurH == null)
            {
                aurH = new float[puntos]; aurHs = new float[puntos]; aurA = new float[puntos]; aurR = new float[puntos];
                aurNi = new float[puntos]; aurLi = new float[puntos];
                aurNc = new uint[puntos]; aurLc = new uint[puntos];
                aurCortina = new uint[(AurFilas + 1) * puntos]; aurSueloC = new uint[(AurSuelo + 1) * puntos];
                aurRampPie = new uint[AurFilas + 1]; aurRampAgu = new uint[AurFilas + 1];
            }
            int nv = (AurFilas + AurSuelo) * AurCols * 6;
            if (aurV == null || aurV.Length < nv) aurV = new D3D9.Vtx[nv];
            int nb = AurBrilloVtx;
            if (aurVb == null || aurVb.Length < nb) aurVb = new D3D9.Vtx[nb];
            double tkAur = waveCadence.SampleMs;   // reloj independiente de 24/50/60, sin frenar el resto del panel
                                        // Windows -que salta de 15.6 en 15.6- haria avanzar la aurora a tirones
            if (aurRelojOk)
            {
                double dt = (tkAur - aurFinoAnt) / 1000.0;
                if (!(dt > 0.0)) dt = 0.0; else if (dt > 0.25) dt = 0.25;   // acota atascos (y atrapa NaN)
                aurT += dt;
                if (aurT >= AurVuelta) aurT -= AurVuelta;
            }
            aurFinoAnt = tkAur; aurRelojOk = true;
            float t = (float)aurT;
            if (updateWave) {
            // DEGRADADO del pie a la punta de los dos extremos (grave y agudo), girando el tono por el BORDE de la
            // rueda (ver AurTonoMezcla): asi a media altura el color sigue siendo un color y no una mezcla sucia. Con
            // el ancho y la paleta de la portada da el color de partida de cada franja; se guarda y solo se rehace
            // cuando cambia algo de eso (AurBases, en DibujoLigeroGpu.cs).
            AurBases(au, puntos);
            for (int i = 0; i < puntos; i++)
            {
                float fx = (float)i / AurCols;
                float v = AuroraBandas ? PerfilDefinido(au.B, fx) : AurPerfil(au.B, fx);
                if (au.Calma > 0f) { float vc = AurCalma(fx, t) * au.Calma; if (vc > v) v = vc; }   // en reposo manda la forma lenta
                if (v > 1f) v = 1f;
                float alt = AurPiso + (1f - AurPiso) * v;
                float rayo = AurRayo(fx, t);                       // el titileo: solo brillo
                if (FondoNatural) rayo = RayoNatural(rayo);
                aurR[i] = rayo;
                aurH[i] = au.Alto * alt * AurOndaAlto(fx, t);      // la forma la pone la musica; el ondeo largo la mece
                // COLOR de la columna, franja por franja. A lo ancho se funde el tono del extremo grave con el
                // del agudo (estan a 32 grados: se mezclan sin ensuciarse) y, segun la INTENSIDAD, se elige un
                // tono de esa misma familia (ver TonoPorNivel). El ondeo NO entra aqui: solo cambia el brillo,
                // para que todas las cortinas lleven el MISMO degradado y los picos salgan parejos entre si.
                for (int k = 0; k <= AurFilas; k++)
                {
                    float yColor = FondoNatural ? au.BaseY - aurH[i] * AuroraNaturalNivel(k) : au.BaseY - aurH[i] * k / AurFilas;
                    uint c = FlowColor(aurBase[k * puntos + i],fx,yColor/Math.Max(1,Height));   // la base: AurBases
                    // Por entero y no por "sin signo": el mismo numero (van de 0 a 255) sin la conversion lenta.
                    float rr = (int)((c >> 16) & 0xFF), gg = (int)((c >> 8) & 0xFF), bb = (int)(c & 0xFF);
                    TonoPorNivel(ref rr, ref gg, ref bb, v);
                    uint color = ((uint)(int)rr << 16) | ((uint)(int)gg << 8) | (uint)(int)bb;
                    aurCortina[k * puntos + i] = FondoNatural ? AuroraNaturalVelo(color, k, fx, t, rayo) : color;
                }
                // EL FILO lleva el color de la PUNTA de su columna (el mismo de la cortina ahi arriba, no
                // otro): asi el contorno es del color de la aurora, encendido, y no una raya postiza.
                aurNc[i] = FondoNatural ? aurCortina[AurFilas * puntos + i] & 0xFFFFFFu : aurCortina[AurFilas * puntos + i];
                uint cPie = FlowColor(aurBasePie[i],fx,au.BaseY/Math.Max(1,Height));   // la base: AurBases
                float r0 = (int)((cPie >> 16) & 0xFF), g0 = (int)((cPie >> 8) & 0xFF), b0 = (int)(cPie & 0xFF);
                for (int k = 0; k <= AurSuelo; k++)                   // el suelo: el mismo color, hundiendose
                {
                    float rd = r0, gd = g0, bd = b0;
                    TonoPorNivel(ref rd, ref gd, ref bd, v * AurSueloTono((float)k / AurSuelo));
                    aurSueloC[k * puntos + i] = ((uint)(int)rd << 16) | ((uint)(int)gd << 8) | (uint)(int)bd;
                }
                float al = (AurAlfaBase + AurAlfaPico * v) * au.Dim;
                if (FondoNatural) al *= 1.04f;   // más cuerpo; las vetas abren huecos de luz en el velo
                if (al < 0f) al = 0f; else if (al > 1f) al = 1f;
                aurA[i] = al;
                // Brillo del FILO: sube con la banda y ademas con el ondeo, para que la veta se vea
                // recorriendo el contorno igual que recorre la cortina.
                float ne = (AurNeonBase + AurNeonPico * v) * rayo * au.Dim * au.DimFilo;
                if (FondoNatural) ne *= .82f;
                if (ne < 0f) ne = 0f; else if (ne > 1f) ne = 1f;
                aurNi[i] = ne;
                // La LINEA del horizonte lleva el color del pie sin matizar (limpio de lado a lado) y su
                // brillo late con la musica, pero nunca se apaga: es el suelo sobre el que todo se apoya.
                aurLc[i] = cPie;
                float li = (AurLineaBase + AurLineaPico * v) * au.Dim * au.DimFilo;
                if (li < 0f) li = 0f; else if (li > 1f) li = 1f;
                aurLi[i] = li;
            }
            // TRES pasadas, de ida y vuelta entre los dos arreglos: una sola no basta para el fondo de los
            // valles, que es lo mas puntiagudo que hay en la silueta. Se dibuja el resultado -el mismo para el
            // relleno y para el filo, o se despegarian.
            AurSuaviza(aurH, aurHs);
            if (!AuroraBandas) { AurSuaviza(aurHs, aurH); AurSuaviza(aurH, aurHs); }
            }
            int n = FondoNatural ? AurMallaNatural(aurV, aurHs, aurCortina, aurSueloC, aurA, aurR, Width, au.BaseY, Height) :
                AurMalla(aurV, aurHs, aurCortina, aurSueloC, aurA, aurR, Width, au.BaseY, Height);
            if (n >= 3) stars.DrawMesh(aurV, n / 3);   // TODO el relleno en UNA sola llamada de dibujo
            // Y encima el BRILLO -el filo de neon y la linea del horizonte- en otra sola llamada, esta
            // sumando luz en vez de tapar. Dos llamadas de dibujo para toda la aurora.
            int nb2 = FondoNatural ? AurBrilloNatural(aurVb, aurHs, aurNc, aurNi, aurLc, aurLi, Width, au.BaseY) :
                AurBrillo(aurVb, aurHs, aurNc, aurNi, aurLc, aurLi, Width, au.BaseY);
            if (nb2 >= 3) stars.DrawMeshAdd(aurVb, nb2 / 3);
        }

        // PRE-CALENTADO. Crear una textura es una peticion al driver y no siempre contesta rapido: si la
        // primera se pide justo cuando arranca una animacion, esa animacion se ve trabada. Aqui se crean de
        // una vez, mientras la rocola esta arrancando y nadie mira, con el tamano en que SIEMPRE vienen las
        // portadas y los avisos. De ahi en adelante todas se reaprovechan y ninguna animacion vuelve a pagar.
        public void PrecalientaAnimaciones(int coverPx, int toastW, int toastH)
        {
            if (stars == null || !stars.Ok) return;
            if (coverPx >= 8 && transTex == IntPtr.Zero)
            {
                using (Bitmap b = new Bitmap(coverPx, coverPx, PixelFormat.Format32bppArgb))
                    transTex = Upload(transTex, ref transBw, ref transBh, b, out transU, out transV);
                transLastBmp = null;   // no es una portada de verdad: la primera real se sube encima y la reusa
            }
            if (toastW >= 8 && toastH >= 8 && toastTex == IntPtr.Zero)
            {
                using (Bitmap b = new Bitmap(toastW, toastH, PixelFormat.Format32bppArgb))
                    toastTex = Upload(toastTex, ref toastBw, ref toastBh, b, out toastU, out toastV);
                toastVer = -1;
            }
        }

        protected override void OnPaint(PaintEventArgs e) { RenderFrame(); }   // WM_PAINT -> dibuja por D3D9

        // UN CUADRO, PEDIDO A MANO. Lo usa el medidor de Ctrl+F3: medir ocurre dentro de una pulsacion, o sea
        // con el bucle de mensajes parado, asi que esperar a que el bucle de siempre (Application.Idle) dibuje
        // seria esperar sentado. Es el MISMO cuadro que dibuja la rocola encendida, por el mismo camino: si
        // aqui se midiera otra cosa, la medida no valdria para nada.
        public bool DibujaUnCuadro() { return RenderFrame(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }        // no borra (D3D9 es dueño de los pixeles)

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopGpu();
                LiberaMapasEstilo();
                if (vinetaMapa != null) { vinetaMapa.Dispose(); vinetaMapa = null; }   // el mapa guardado de la vineta
            }
            base.Dispose(disposing);
        }
    }
}
#endif

// ====================================================================================================
//  EL DISPOSITIVO DE WINDOWS: Direct3D 9, por interop a d3d9.dll (sin SlimDX).
// ----------------------------------------------------------------------------------------------------
//  Esta es la capa fina que habla con la tarjeta grafica. Todo el dibujo de verdad -el cielo, las
//  estrellas, la aurora, los cubos, el buscador- esta en Rocola/D3D9Star.cs y no sabe nada de Direct3D:
//  solo pide "pintame estos triangulos con este color y esta textura".
//
//  El gemelo de este archivo para Linux es Rocola/GLDevice.cs, con OpenGL. Se compila UNO de los dos.
//
//  Todo dentro de #if ROCOLA_GPU. Con el simbolo apagado, el archivo compila vacio.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Rocola
{
    // -------- Interop crudo con Direct3D 9 (sin SlimDX) --------
    internal static partial class D3D9
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
        private IntPtr d3d, dev;
        private D3D9.PresentParams pp;
        private bool lost;

        private D3D9.PresentDel present; private D3D9.ClearDel clear; private D3D9.HrSelf begin, end;
        private D3D9.SetU2Del setRS; private D3D9.SetU1Del setFVF; private D3D9.SetU3Del setTSS, setSamp;
        private D3D9.SetTexDel setTex; private D3D9.DrawUpDel drawUp; private D3D9.HrSelf testCoop;
        private D3D9.ResetDel reset; private D3D9.CreateTexDel createTex;

        public bool Ok { get { return dev != IntPtr.Zero; } }

        public D3D9Device(IntPtr hwnd, int w, int h)
        {
            d3d = D3D9.Direct3DCreate9(32);
            if (d3d == IntPtr.Zero) return;
            pp = MakePP(hwnd, w, h);
            D3D9.CreateDeviceDel create = D3D9.Fn<D3D9.CreateDeviceDel>(d3d, 16);
            IntPtr dv;
            int hr = create(d3d, 0, D3D9.DEVTYPE_HAL, hwnd, D3D9.CREATE_HARDWARE_VP | D3D9.CREATE_FPU_PRESERVE, ref pp, out dv);
            if (hr < 0) hr = create(d3d, 0, D3D9.DEVTYPE_HAL, hwnd, D3D9.CREATE_SOFTWARE_VP | D3D9.CREATE_FPU_PRESERVE, ref pp, out dv);
            if (hr < 0) { D3D9.Release(d3d); d3d = IntPtr.Zero; return; }
            dev = dv;
            present = D3D9.Fn<D3D9.PresentDel>(dev, 17);
            clear = D3D9.Fn<D3D9.ClearDel>(dev, 43);
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
            InitStates();
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
            if (pp.BackBufferWidth == (uint)w && pp.BackBufferHeight == (uint)h) return true;
            uint aw = pp.BackBufferWidth, ah = pp.BackBufferHeight;
            pp.BackBufferWidth = (uint)w; pp.BackBufferHeight = (uint)h;
            if (reset(dev, ref pp) < 0)
            {
                // No se pudo ahora (p.ej. el dispositivo esta perdido en ese instante): se deja apuntado el
                // tamano nuevo y se reintenta en el proximo cuadro, por el mismo camino que la perdida.
                pp.BackBufferWidth = aw; pp.BackBufferHeight = ah;
                lost = true;
                return false;
            }
            InitStates();
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

        private void InitStates()
        {
            stTss = 0; stBlend = 0; stTex = new IntPtr(-1);   // tras crear o RESETEAR el dispositivo todo vuelve a su valor por defecto
            setRS(dev, D3D9.RS_ZENABLE, 0);
            setRS(dev, D3D9.RS_LIGHTING, 0);
            setRS(dev, D3D9.RS_CULLMODE, D3D9.CULL_NONE);
            setFVF(dev, D3D9.FVF);
            setSamp(dev, 0, D3D9.SAMP_MAGFILTER, D3D9.TEXF_LINEAR);
            setSamp(dev, 0, D3D9.SAMP_MINFILTER, D3D9.TEXF_LINEAR);
        }

        private void Tex(IntPtr t) { if (stTex != t) { setTex(dev, 0, t); stTex = t; } }
        private void Blend(int modo)   // 1 = sin mezcla, 2 = aditiva (brillos), 3 = normal (encima)
        {
            if (stBlend == modo) return;
            if (modo == 1) setRS(dev, D3D9.RS_ALPHABLENDENABLE, 0);
            else
            {
                if (stBlend <= 1) setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1);
                setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_SRCALPHA);
                setRS(dev, D3D9.RS_DESTBLEND, modo == 2 ? D3D9.BLEND_ONE : D3D9.BLEND_INVSRCALPHA);
            }
            stBlend = modo;
        }

        public IntPtr CreateTextureFromBitmap(Bitmap bmp)
        {
            if (dev == IntPtr.Zero) return IntPtr.Zero;
            int w = bmp.Width, h = bmp.Height;
            IntPtr tex;
            if (createTex(dev, (uint)w, (uint)h, 1, 0, D3D9.FMT_A8R8G8B8, D3D9.POOL_MANAGED, out tex, IntPtr.Zero) < 0) return IntPtr.Zero;
            D3D9.LockRectDel lockR = D3D9.Fn<D3D9.LockRectDel>(tex, 19);
            D3D9.UnlockRectDel unlockR = D3D9.Fn<D3D9.UnlockRectDel>(tex, 20);
            D3D9.LockedRect lr;
            if (lockR(tex, 0, out lr, IntPtr.Zero, 0) < 0) return tex;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int rowBytes = w * 4;
            for (int y = 0; y < h; y++)
                D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), (UIntPtr)(uint)rowBytes);
            bmp.UnlockBits(bd);
            unlockR(tex, 0);
            return tex;
        }

        // Inicia el cuadro (maneja perdida de dispositivo). Devuelve true si se puede dibujar.
        public bool BeginFrame()
        {
            if (dev == IntPtr.Zero) return false;
            if (lost)
            {
                int tc = testCoop(dev);
                if (tc == D3D9.E_DEVICENOTRESET) { if (reset(dev, ref pp) >= 0) { lost = false; InitStates(); } else return false; }
                else return false;
            }
            // La textura apuntada se olvida en CADA cuadro: entre cuadro y cuadro se recrean texturas (portadas,
            // UI) y una nueva puede caer en la MISMA direccion que una recien liberada. Los otros estados son
            // numeros, no punteros, asi que esos si se conservan. Cuesta un cambio de textura por cuadro.
            stTex = new IntPtr(-1);
            clear(dev, 0, IntPtr.Zero, D3D9.CLEAR_TARGET, 0xFF05060F, 1f, 0);
            return begin(dev) >= 0;
        }

        public void EndFrame()
        {
            end(dev);
            int hr = present(dev, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hr == D3D9.E_DEVICELOST) lost = true;
        }

        private void TssDiffuse()   // usa el color del vertice (sin textura)
        {
            if (stTss == 1) return;
            setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_SELECTARG1); setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_DIFFUSE);
            setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_SELECTARG1); setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_DIFFUSE);
            stTss = 1;
        }
        private void TssModulate()  // textura * color del vertice
        {
            if (stTss == 2) return;
            setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_MODULATE); setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_TEXTURE); setTSS(dev, 0, D3D9.TSS_COLORARG2, D3D9.TA_DIFFUSE);
            setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_MODULATE); setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_TEXTURE); setTSS(dev, 0, D3D9.TSS_ALPHAARG2, D3D9.TA_DIFFUSE);
            stTss = 2;
        }

        // Cielo (color del vertice, sin mezcla) + estrellas (textura de glow, aditivo).
        // Cielo (color del vertice, sin mezcla). 2 quads apilados = degradado de 3 paradas (4 triangulos).
        public void DrawSky(D3D9.Vtx[] sky)
        {
            Tex(IntPtr.Zero); TssDiffuse(); Blend(1);
            Draw(sky, 4);
        }
        // Estrellas (textura de glow, aditivo). Se puede llamar 2 veces (patron anterior + nuevo) en el entrecruzado.
        public void DrawStars(D3D9.Vtx[] stars, int starTris, IntPtr glowTex)
        {
            if (dev == IntPtr.Zero || starTris <= 0 || glowTex == IntPtr.Zero) return;
            Tex(glowTex); TssModulate(); Blend(2);
            Draw(stars, starTris);
        }

        // Estelas de estrellas fugaces: cuadrangulos BLANCOS con mezcla ADITIVA y SIN textura (color del vertice).
        public void DrawShootStreaks(D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(2);
            Draw(v, tris);
        }

        // Malla PLANA: triangulos SIN textura, color en los vertices y mezcla NORMAL (encima, no sumada).
        // Es lo que pinta el fondo reactivo: un relleno de color que la tarjeta degrada sola entre vertice y
        // vertice. Al no sumar luz nunca se lava hacia el blanco, que es lo que manchaba con los halos.
        public void DrawMesh(D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(3);
            Draw(v, tris);
        }

        // La misma malla plana pero SUMANDO luz en vez de tapar. Un filo encendido se lee como neon
        // justamente por esto: lo que hay debajo se sigue viendo y encima se le suma el brillo, asi que
        // el nucleo satura a claro y el resplandor de alrededor tine sin borrar. Tapando no hay neon.
        public void DrawMeshAdd(D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || tris <= 0) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(2);
            Draw(v, tris);
        }

        // Un cuadro texturizado (2 triangulos) con mezcla NORMAL (encima): para los cubos, etiquetas, etc.
        public void DrawTexQuad(IntPtr tex, D3D9.Vtx[] quad)
        {
            if (tex == IntPtr.Zero) return;
            Tex(tex); TssModulate(); Blend(3);
            Draw(quad, 2);
        }

        // Una tanda de triangulos con mezcla NORMAL (encima), con textura o sin ella. Es la pieza con la
        // que el archivo del dibujo arma los cubos: le pasa cara por cara y el cache de estados hace que
        // repetir la peticion no cueste nada.
        public void DrawQuads(IntPtr tex, D3D9.Vtx[] v, int tris)
        {
            if (dev == IntPtr.Zero || tris <= 0) return;
            if (tex != IntPtr.Zero) { Tex(tex); TssModulate(); } else { Tex(IntPtr.Zero); TssDiffuse(); }
            Blend(3);
            Draw(v, tris);
        }

        private static int Pow2(int n) { int p = 1; while (p < n) p <<= 1; return p; }

        // Crea una textura de tamano POTENCIA DE DOS (compatible con GPU vieja) y sube el bitmap en su
        // esquina superior izquierda. Devuelve las coordenadas de textura maximas usadas (uMax, vMax).
        public IntPtr CreateTexturePow2(Bitmap bmp, out float uMax, out float vMax)
        {
            uMax = 1f; vMax = 1f;
            if (dev == IntPtr.Zero) return IntPtr.Zero;
            int w = bmp.Width, h = bmp.Height, tw = Pow2(w), th = Pow2(h);
            IntPtr tex;
            if (createTex(dev, (uint)tw, (uint)th, 1, 0, D3D9.FMT_A8R8G8B8, D3D9.POOL_MANAGED, out tex, IntPtr.Zero) < 0) return IntPtr.Zero;
            D3D9.LockRectDel lockR = D3D9.Fn<D3D9.LockRectDel>(tex, 19);
            D3D9.UnlockRectDel unlockR = D3D9.Fn<D3D9.UnlockRectDel>(tex, 20);
            D3D9.LockedRect lr;
            if (lockR(tex, 0, out lr, IntPtr.Zero, 0) >= 0)
            {
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                    D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), (UIntPtr)(uint)rowBytes);
                bmp.UnlockBits(bd);
                unlockR(tex, 0);
            }
            uMax = (float)w / tw; vMax = (float)h / th;
            return tex;
        }

        // Re-sube el contenido a una textura pow2 YA existente (mismo tamano de bitmap) en vez de crear una
        // nueva: evita el churn de crear/destruir una textura a pantalla completa en cada reconstruccion de UI.
        // El llamador garantiza el mismo tamano. Devuelve false si no pudo bloquearla (entonces se recrea).
        public bool UpdateTexturePow2(IntPtr tex, Bitmap bmp, out float uMax, out float vMax)
        {
            uMax = 1f; vMax = 1f;
            if (dev == IntPtr.Zero || tex == IntPtr.Zero) return false;
            int w = bmp.Width, h = bmp.Height, tw = Pow2(w), th = Pow2(h);
            D3D9.LockRectDel lockR = D3D9.Fn<D3D9.LockRectDel>(tex, 19);
            D3D9.UnlockRectDel unlockR = D3D9.Fn<D3D9.UnlockRectDel>(tex, 20);
            D3D9.LockedRect lr;
            if (lockR(tex, 0, out lr, IntPtr.Zero, 0) < 0) return false;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int rowBytes = w * 4;
            for (int y = 0; y < h; y++)
                D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), (UIntPtr)(uint)rowBytes);
            bmp.UnlockBits(bd);
            unlockR(tex, 0);
            uMax = (float)w / tw; vMax = (float)h / th;
            return true;
        }

        private void Draw(D3D9.Vtx[] verts, int tris)
        {
            GCHandle gh = GCHandle.Alloc(verts, GCHandleType.Pinned);
            try { drawUp(dev, D3D9.PT_TRIANGLELIST, (uint)tris, gh.AddrOfPinnedObject(), D3D9.VtxSize); }
            finally { gh.Free(); }
        }

        public void Dispose()
        {
            if (dev != IntPtr.Zero) { D3D9.Release(dev); dev = IntPtr.Zero; }
            if (d3d != IntPtr.Zero) { D3D9.Release(d3d); d3d = IntPtr.Zero; }
        }
    }
}
#endif

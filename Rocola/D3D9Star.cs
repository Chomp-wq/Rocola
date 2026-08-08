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

        // Un cuadro texturizado (2 triangulos) con mezcla NORMAL (encima): para los cubos, etiquetas, etc.
        public void DrawTexQuad(IntPtr tex, D3D9.Vtx[] quad)
        {
            if (tex == IntPtr.Zero) return;
            Tex(tex); TssModulate(); Blend(3);
            Draw(quad, 2);
        }

        // Buffers reutilizables del cubo (evita asignar por cuadro). Solo se usan desde el hilo de render.
        private readonly D3D9.Vtx[] cubeFaceV = new D3D9.Vtx[6];
        private readonly double[] cbx = new double[4];
        private readonly double[] cbz = new double[4];
        private readonly int[] cbo = new int[4];

        // Dibuja un cubo GIRANDO (mismo aspecto que Cube.Draw del render por CPU) directo en GPU desde la
        // textura de la portada: el giro es solo geometria (4 caras como cuadrangulos, ordenadas por
        // profundidad y oscurecidas igual). Si tex es nulo usa un relleno azulado (portada aun cargando).
        // alpha < 1 permite FUNDIR una portada sobre otra (discos TOP) en dos pasadas.
        public void DrawCube(IntPtr tex, float uMax, float vMax, float ax, float ay, float aw, float ah, double angle, float alpha, double tilt)
        {
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
                    : (av << 24) | ((60u * g / 255u) << 16) | ((70u * g / 255u) << 8) | (110u * g / 255u);  // relleno azulado (sin portada)
                CubeFace(cubeFaceV, left, y0, right, y0 + Hh, cx, cyc, ct, stt, uMax, vMax, col);
                Draw(cubeFaceV, 2);
            }
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

    // -------- Campo estrellado reusable (titileo + latido), teñible por genero --------
    internal sealed class GpuStarfield
    {
        private D3D9Device gpu; private IntPtr glowTex; private IntPtr uiGlowTex;   // glow de estrella (con chispa) / glow radial LISO para el destello de UI
        private float[] sx, sy, sphase, sspeed, ssize; private int[] skind; private uint[] scol; private int nStars;
        private D3D9.Vtx[] skyV = new D3D9.Vtx[12]; private D3D9.Vtx[] starV;   // cielo = 2 quads apilados (3 paradas de color)
        private uint skyTop = 0xFF06081A, skyMid = 0xFF101C3A, skyBot = 0xFF1A2C5A;
        private readonly int startTick = Environment.TickCount;
        private int seed0;                                   // semilla base (el patron de cada minuto = seed0 + minuto*paso)
        private int curMinute = int.MinValue;                // minuto del patron actual (cambia cada 60s)
        // ENTRECRUZADO del patron: al cambiar de minuto se conserva el patron anterior y se desvanece mientras
        // el nuevo aparece, en vez de atenuar TODO el cielo. Asi el fondo nunca se queda sin estrellas.
        private float[] px, py, pphase, pspeed, psize; private int[] pkind; private uint[] pcol; private int pnStars;
        private D3D9.Vtx[] starV2;                            // vertices del patron ANTERIOR durante el entrecruzado
        private int transStart; private bool transing;
        private const int TransMs = 1300;                    // duracion del entrecruzado
        // Estrellas FUGACES: en CPU se podian pero costaban; en GPU son un par de cuadrangulos aditivos.
        // Cada una con tamaño, color, velocidad y largo de estela distintos.
        private struct Shoot { public bool On; public float X, Y, VX, VY, Life, MaxLife, Wdt, Tail; public uint Col; }
        private readonly Shoot[] shoots = new Shoot[2];
        private D3D9.Vtx[] shootV = new D3D9.Vtx[2 * 12];    // 2 fugaces x (estela 6 + cabeza 6) vertices
        private int nextShoot, lastShootTick;
        private readonly Random srnd = new Random();
        private static readonly uint[] shootPal = { 0x00FFFFFF, 0x00FFFFFF, 0x00CFE6FF, 0x00FFE6A0, 0x00A0FFE0, 0x00FFC0E0 };   // blanco (x2), azul, dorado, aqua, rosa
        // DERIVA con la musica: al sonar, el campo entero se desliza LENTO en diagonal (abajo-derecha, como las
        // fugaces) y se envuelve por los bordes (bucle infinito). Es baratisimo: solo un desplazamiento + modulo
        // por estrella dentro del bucle que ya existe; ni asignaciones ni draws extra.
        private int driftLastMs;                             // ultimo Begin (ms) para medir dt de la deriva
        private float driftEnv;                              // envolvente 0..1: sube al sonar, baja al parar (arranque/paro suave)
        private float driftX, driftY;                        // desplazamiento acumulado (px), envuelto con w/h
        private const float DriftVX = 18f, DriftVY = 9f;     // px/seg (derecha, abajo): lento, diagonal suave 2:1

        public bool Ok { get { return gpu != null && gpu.Ok; } }
        public int Count { get { return nStars; } }
        private float dim = 1f;   // atenuacion del campo (estrellas + fugaces) mientras se navega con musica
        public void SetDim(float d) { dim = d < 0f ? 0f : (d > 1f ? 1f : d); }

        public GpuStarfield(IntPtr hwnd, int w, int h, int seed)
        {
            gpu = new D3D9Device(hwnd, w, h);
            if (!gpu.Ok) return;
            seed0 = seed;
            using (Bitmap tex = BakeStarTexture(64)) glowTex = gpu.CreateTextureFromBitmap(tex);
            using (Bitmap g2 = BakeRadialGlow(64)) uiGlowTex = gpu.CreateTextureFromBitmap(g2);   // glow LISO (sin chispa) para el destello del boton
            BuildStars(w, h, seed);
        }

        public void SetSky(uint top, uint mid, uint bot) { skyTop = top; skyMid = mid; skyBot = bot; }
        public IntPtr CreateOverlay(Bitmap bmp, out float uMax, out float vMax) { return gpu.CreateTexturePow2(bmp, out uMax, out vMax); }
        public bool UpdateOverlay(IntPtr tex, Bitmap bmp, out float uMax, out float vMax) { return gpu.UpdateTexturePow2(tex, bmp, out uMax, out vMax); }
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
            IntPtr t = uiGlowTex != IntPtr.Zero ? uiGlowTex : glowTex;
            if (!Ok || t == IntPtr.Zero || r < 1f) return;
            Halo(glowV, 0, cx, cy, r, argb);
            gpu.DrawStars(glowV, HaloVtx / 3, t);   // mismo camino aditivo que las estrellas
        }
        // Escribe UN halo (6 triangulos en abanico) a partir del hueco i del arreglo que se le pase.
        private static void Halo(D3D9.Vtx[] a, int i, float cx, float cy, float r, uint c)
        {
            for (int k = 0; k < 6; k++)                  // abanico desde el vertice 0: (0,k+1,k+2)
            {
                Punto(a, i + k * 3 + 0, cx, cy, r, 0, c);
                Punto(a, i + k * 3 + 1, cx, cy, r, k + 1, c);
                Punto(a, i + k * 3 + 2, cx, cy, r, k + 2, c);
            }
        }
        private static void Punto(D3D9.Vtx[] a, int i, float cx, float cy, float r, int k, uint c)
        {
            float dx = HaloX[k], dy = HaloY[k];
            GV(a, i, cx + dx * r, cy + dy * r, c, 0.5f + 0.5f * dx, 0.5f + 0.5f * dy);
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
        public void GlowBatchAdd(float cx, float cy, float r, uint argb)
        {
            if (r < 1f || glowBatch == null || (glowBatchN + 1) * HaloVtx > glowBatch.Length) return;
            Halo(glowBatch, glowBatchN * HaloVtx, cx, cy, r, argb);
            glowBatchN++;
        }
        public void GlowBatchEnd()
        {
            IntPtr t = uiGlowTex != IntPtr.Zero ? uiGlowTex : glowTex;
            if (Ok && t != IntPtr.Zero && glowBatchN > 0) gpu.DrawStars(glowBatch, glowBatchN * (HaloVtx / 3), t);
            glowBatchN = 0;
        }

        public void Render(int w, int h, float pulse) { if (Begin(w, h, pulse)) End(); }   // solo cielo+estrellas (ventana de prueba)

        // Inicia el cuadro: dibuja el cielo + estrellas. Luego se pueden apilar texturas con DrawQuad y cerrar con End.
        public bool Begin(int w, int h, float pulse)
        {
            if (!Ok) return false;
            if (!gpu.BeginFrame()) return false;
            int nowMs = Environment.TickCount;
            float t = (nowMs - startTick) / 1000f;

            // DERIVA DIAGONAL con la musica: al sonar (pulse>0) el campo entero se desliza LENTO abajo-derecha
            // y se envuelve por los bordes. Envolvente suave para arrancar/parar sin saltos. Coste ~nulo.
            float ddt = (driftLastMs == 0) ? 0f : (nowMs - driftLastMs) / 1000f;
            driftLastMs = nowMs;
            if (ddt < 0f) ddt = 0f; else if (ddt > 0.1f) ddt = 0.1f;   // acota saltos (p.ej. tras un atasco)
            float dTgt = (pulse > 0.02f) ? 1f : 0f;
            driftEnv += (dTgt - driftEnv) * (dTgt > driftEnv ? Math.Min(1f, ddt * 2.5f) : Math.Min(1f, ddt * 0.6f));   // ataque medio, caida lenta
            driftX += driftEnv * DriftVX * ddt; if (w > 0) driftX %= w;   // env>=0 -> solo crece; el modulo lo deja en [0,w)
            driftY += driftEnv * DriftVY * ddt; if (h > 0) driftY %= h;

            // PATRON que cambia cada minuto, con ENTRECRUZADO: se conserva el patron anterior y se desvanece
            // mientras el nuevo aparece (el cielo nunca se queda sin estrellas). Por GPU re-sortear es barato.
            int minute = (int)(t / 60f);
            if (minute != curMinute)
            {
                if (curMinute != int.MinValue)
                {
                    px = sx; py = sy; pphase = sphase; pspeed = sspeed; psize = ssize; pkind = skind; pcol = scol; pnStars = nStars;   // guarda el actual como ANTERIOR
                    BuildStars(w, h, unchecked(seed0 + minute * 40503));     // construye el NUEVO (asigna arreglos nuevos; no pisa los anteriores)
                    if (starV2 == null || starV2.Length < pnStars * 6) starV2 = new D3D9.Vtx[pnStars * 6];
                    transing = true; transStart = nowMs;
                }
                curMinute = minute;
            }
            float tr = transing ? (nowMs - transStart) / (float)TransMs : 1f;
            if (tr >= 1f) { transing = false; tr = 1f; }

            // Cielo de 3 PARADAS como en CPU: negro arriba -> azul noche al 42% -> color del genero abajo
            // (antes 2 paradas: el genero teñia casi toda la pantalla). Dos quads apilados en la parada media.
            float midY = h * 0.42f;
            SetQuad(skyV, 0, 0, 0, w, midY, skyTop, skyTop, skyMid, skyMid);
            SetQuad(skyV, 6, 0, midY, w, h, skyMid, skyMid, skyBot, skyBot);
            gpu.DrawSky(skyV);
            if (transing && px != null)   // patron ANTERIOR desvaneciendose
            {
                BuildStarVerts(starV2, px, py, pphase, pspeed, psize, pkind, pcol, pnStars, t, (1f - tr) * dim, pulse, driftX, driftY, w, h);
                gpu.DrawStars(starV2, pnStars * 2, glowTex);
            }
            BuildStarVerts(starV, sx, sy, sphase, sspeed, ssize, skind, scol, nStars, t, (transing ? tr : 1f) * dim, pulse, driftX, driftY, w, h);   // patron ACTUAL (x atenuacion al navegar)
            gpu.DrawStars(starV, nStars * 2, glowTex);
            UpdateAndDrawShoots(w, h, nowMs);
            return true;
        }

        // Construye los vertices de un campo de estrellas (titileo + latido) con un factor de brillo global
        // (para el entrecruzado del patron). Reutilizable por el patron actual y por el anterior.
        private static void BuildStarVerts(D3D9.Vtx[] buf, float[] xs, float[] ys, float[] ph, float[] spd, float[] sz, int[] kd, uint[] col, int n, float t, float fade, float pulse, float dx, float dy, float w, float h)
        {
            int vi = 0;
            for (int i = 0; i < n; i++)
            {
                double tw = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(t * spd[i] + ph[i]));
                uint a = (uint)(tw * fade * 255); if (a > 255) a = 255;
                uint c = (a << 24) | (col[i] & 0x00FFFFFF);             // alfa = brillo (titileo * entrecruzado); rgb = color
                float s = sz[i];
                if (kd[i] != 0 && pulse > 0.02f) s += pulse * (kd[i] == 2 ? 9f : 5f);   // latido: medianas/grandes crecen con la musica
                float x = xs[i] + dx; if (x >= w) x -= w;   // deriva + envuelve (xs,dx en [0,w) -> x en [0,2w) -> 1 resta basta)
                float y = ys[i] + dy; if (y >= h) y -= h;
                SetQuad(buf, vi, x - s, y - s, x + s, y + s, c, c, c, c);
                vi += 6;
            }
        }

        // Estrellas FUGACES: una cada 7-16s, desde arriba-izquierda hacia abajo-derecha, con estela que se
        // apaga. Igual logica que el render por CPU (DrawShoots), pero dibujada como cuadrangulos aditivos.
        private void UpdateAndDrawShoots(int w, int h, int nowMs)
        {
            if (lastShootTick == 0) { lastShootTick = nowMs; nextShoot = nowMs + 4000 + srnd.Next(8000); return; }   // 1er cuadro: agenda la 1a, sin saltos
            float dt = (nowMs - lastShootTick) / 1000f;
            if (dt < 0f) dt = 0f; if (dt > 0.12f) dt = 0.12f;   // acota saltos
            lastShootTick = nowMs;

            if (nowMs - nextShoot >= 0)   // RESTA, no >=: sobrevive el desbordamiento de TickCount (~25 dias encendida)
            {
                nextShoot = nowMs + 7000 + srnd.Next(9000);   // una cada 7-16s
                for (int i = 0; i < shoots.Length; i++)
                    if (!shoots[i].On)
                    {
                        shoots[i].On = true;
                        shoots[i].X = (float)(srnd.NextDouble() * w * 0.6);
                        shoots[i].Y = (float)(srnd.NextDouble() * h * 0.45);
                        float spd = (w + h) * (0.40f + (float)srnd.NextDouble() * 0.45f);   // VELOCIDAD variada
                        shoots[i].VX = spd; shoots[i].VY = spd * (0.30f + (float)srnd.NextDouble() * 0.40f);
                        shoots[i].MaxLife = 0.9f + (float)srnd.NextDouble() * 0.8f; shoots[i].Life = shoots[i].MaxLife;
                        shoots[i].Wdt = 0.9f + (float)srnd.NextDouble() * 1.5f;      // GROSOR variado
                        shoots[i].Tail = 0.07f + (float)srnd.NextDouble() * 0.10f;   // LARGO de estela variado
                        shoots[i].Col = shootPal[srnd.Next(shootPal.Length)];        // COLOR variado
                        break;
                    }
            }
            int vi = 0;
            for (int i = 0; i < shoots.Length; i++)
            {
                if (!shoots[i].On) continue;
                Shoot s = shoots[i];
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
                s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt;
                if (s.Life <= 0f || s.X > w + 60 || s.Y > h + 60) s.On = false;
                shoots[i] = s;
            }
            if (vi > 0) gpu.DrawShootStreaks(shootV, vi / 3);
        }
        private static void VShoot(D3D9.Vtx[] v, int o, float x, float y, uint c) { v[o].X = x; v[o].Y = y; v[o].Z = 0f; v[o].Rhw = 1f; v[o].Color = c; v[o].U = 0f; v[o].V = 0f; }
        public void DrawQuad(IntPtr tex, D3D9.Vtx[] quad) { if (Ok) gpu.DrawTexQuad(tex, quad); }
        public void End() { if (Ok) gpu.EndFrame(); }

        public void Dispose()
        {
            if (glowTex != IntPtr.Zero) { D3D9.Release(glowTex); glowTex = IntPtr.Zero; }
            if (uiGlowTex != IntPtr.Zero) { D3D9.Release(uiGlowTex); uiGlowTex = IntPtr.Zero; }
            if (gpu != null) { gpu.Dispose(); gpu = null; }
        }

        private void BuildStars(int w, int h, int seed)
        {
            Random rnd = new Random(seed);
            nStars = Math.Max(60, w * h / 6500);
            if (nStars > 260) nStars = 260;
            sx = new float[nStars]; sy = new float[nStars]; sphase = new float[nStars];
            sspeed = new float[nStars]; ssize = new float[nStars]; skind = new int[nStars]; scol = new uint[nStars];
            uint[] pal = { 0xFFFFFFFF, 0xFFFFFFFF, 0xFFB4CDFF, 0xFFCDA5FF, 0xFFFFAAE6, 0xFFFFDC8C, 0xFF96FFE1 };
            float minD2 = 30f * 30f;   // separacion minima entre estrellas: evita que se amontonen
            for (int i = 0; i < nStars; i++)
            {
                float bx = 0f, by = 0f;
                for (int attempt = 0; attempt < 10; attempt++)   // busca un hueco lejos de las ya puestas
                {
                    bx = (float)(rnd.NextDouble() * w);
                    by = (float)(rnd.NextDouble() * h);
                    bool clear = true;
                    for (int j = 0; j < i; j++)
                    {
                        float dx = bx - sx[j], dy = by - sy[j];
                        if (dx * dx + dy * dy < minD2) { clear = false; break; }
                    }
                    if (clear) break;
                }
                sx[i] = bx; sy[i] = by;
                sphase[i] = (float)(rnd.NextDouble() * Math.PI * 2);
                sspeed[i] = 0.6f + (float)rnd.NextDouble() * 1.8f;
                int kk = rnd.Next(100);
                skind[i] = (kk < 7) ? 2 : (kk < 26) ? 1 : 0;   // MENOS grandes: 7% grandes (antes 12%), 19% medianas, resto puntos
                ssize[i] = (skind[i] == 2) ? (10 + rnd.Next(5)) : (skind[i] == 1) ? (6 + rnd.Next(4)) : (3 + rnd.Next(3));   // mas chicas (antes 16-27 las grandes)
                scol[i] = pal[rnd.Next(pal.Length)];
            }
            starV = new D3D9.Vtx[nStars * 6];
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

        private static Bitmap BakeStarTexture(int size)
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
                        pgb.CenterColor = Color.FromArgb(170, 255, 255, 255);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                        g.FillEllipse(pgb, 0, 0, size, size);
                    }
                }
                float c = size / 2f, R = size * 0.42f, r = R * 0.22f;
                PointF[] spk = {
                    new PointF(c, c - R), new PointF(c + r, c - r), new PointF(c + R, c), new PointF(c + r, c + r),
                    new PointF(c, c + R), new PointF(c - r, c + r), new PointF(c - R, c), new PointF(c - r, c - r)
                };
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 255, 255))) g.FillPolygon(b, spk);
            }
            return bmp;
        }

        private static void SetQuad(D3D9.Vtx[] v, int o, float x0, float y0, float x1, float y1, uint cTL, uint cTR, uint cBR, uint cBL)
        {
            V(ref v[o + 0], x0, y0, cTL, 0, 0); V(ref v[o + 1], x1, y0, cTR, 1, 0); V(ref v[o + 2], x1, y1, cBR, 1, 1);
            V(ref v[o + 3], x0, y0, cTL, 0, 0); V(ref v[o + 4], x1, y1, cBR, 1, 1); V(ref v[o + 5], x0, y1, cBL, 0, 1);
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
        public bool Has;
        public float X, Y, W, H;       // celda donde va el cubo (coords del panel)
        public double Angle;           // giro actual (radianes)
        public Bitmap CoverA;          // portada principal (null si aun no cargo -> relleno azulado)
        public Bitmap CoverB;          // portada siguiente para el fundido TOP (null si no aplica)
        public float Fade;             // 0..1 mezcla A->B
        public float Tilt;             // bamboleo del cubo entero (radianes); los CubeBox grande/mini lo usan
    }
    internal struct GpuCubeState { public GpuCubeInfo Sel, Relay, Mini; }   // album: seleccionado (gira) + relevo (decae); Mini = cubo de la barra inferior

    // Una CELDA de la rejilla de discos para dibujarla como su PROPIO quad con textura (en vez de recomponer una
    // textura a pantalla completa por pagina). Bmp = disco estatico + etiqueta ya horneado (null si aun no listo).
    // Key = Folder (estable) para cachear su textura entre paginas; null = no cachear (discos TOP / sin carpeta).
    internal struct GpuGridCell { public string Key; public Bitmap Bmp; public int Slot; public float X, Y, W, H; }
    // TRANSICION entre dos CUBOS de la rocola (celda de la rejilla, cubo grande del disco, cubo gigante de la
    // atraccion, cubo del buscador): la portada viaja de uno al otro dibujada ENCIMA de todo, girando, con
    // bamboleo y un destello detras, y se funde al llegar. Se dibuja con el MISMO DrawCube que los demas cubos,
    // asi que aterriza calcada sobre el cubo de destino. X,Y,W,H = celda del cubo (igual que GpuCubeInfo).
    internal struct GpuTrans
    {
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
    internal struct GpuWin { public bool Has; public bool HideUi; public Bitmap Bmp; public float Scale, ScaleY, Dy, Alpha; }
    // AURORA DEL HORIZONTE: el visualizador de espectro, dibujado como parte del CIELO y no como un aparato
    // encima. B = 8 bandas ya suavizadas (0..1, de graves a agudos); se interpolan a lo ancho y se pintan con
    // halos aditivos MUY solapados, que es lo que convierte ocho valores sueltos en una franja continua de luz.
    internal struct GpuAurora { public bool Has; public float[] B; public float BaseY, Alto; public uint Rgb; public float Dim; }
    internal delegate int GpuGridFetch(GpuGridCell[] buf);   // llena buf con las celdas de la pagina actual; devuelve cuantas (<= buf.Length)

    // -------- Control D3D9 para usar el campo estrellado como FONDO de la vista de discos --------
    //  Es un control aparte (su propia ventana) con estilos aptos para D3D9 (sin doble-buffer GDI).
    //  Se muestra ENCIMA de la vista de discos al activar el modo GPU; el render lo maneja D3D9.
    internal sealed class GpuPanel : Control
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
        private const int MaxCells = 32;
        private const int CellTexCap = 3 * 18;            // ~3 paginas de texturas de celda en VRAM (acotado); se expulsa lo mas viejo
        private const int GridUploadBudget = 6;           // texturas de celda NUEVAS subidas por cuadro (barrer sin bajon)
        private sealed class CellTex { public IntPtr Tex; public int Bw, Bh; public float U = 1f, V = 1f; public Bitmap Bmp; public int Frame; }
        private readonly Dictionary<string, CellTex> cellTexCache = new Dictionary<string, CellTex>();
        private readonly CellTex[] slotTex = new CellTex[MaxCells];   // celdas sin Folder (TOP): textura por slot, no se cachea entre paginas
        private readonly GpuGridCell[] gridBuf = new GpuGridCell[MaxCells];
        private readonly IntPtr[] gridTex = new IntPtr[MaxCells];     // textura resuelta por celda (subida ANTES de Begin, dibujada despues)
        public Func<GpuTrans> GetTrans;                   // portada de la transicion entrar/salir de disco (null = sin animacion)
        private IntPtr transTex; private Bitmap transLastBmp; private float transU = 1f, transV = 1f;
        public Func<GpuAurora> GetAurora;                 // aurora del horizonte (espectro); null o Has=false = nada
        public Func<GpuWin> GetWin;                       // ventana del buscador abriendo/cerrando (null = sin animacion)
        private IntPtr winTex; private Bitmap winLastBmp; private float winU = 1f, winV = 1f;
        private readonly D3D9.Vtx[] winQuad = new D3D9.Vtx[6];
        private readonly float[] gridU = new float[MaxCells], gridV = new float[MaxCells];
        private int gridN, cellFrame;
        private readonly D3D9.Vtx[] cellQuad = new D3D9.Vtx[6];
        private IntPtr contourTex; private Bitmap lastContour; private float contU = 1f, contV = 1f;
        private readonly D3D9.Vtx[] contourQuad = new D3D9.Vtx[6];
        // Cubos que GIRAN (seleccionado + relevo): se dibujan como GEOMETRIA en GPU desde la textura de la
        // portada. La portada se sube UNA vez (al cambiar), no en cada cuadro -> casi cero CPU por cubo.
        private IntPtr selTexA, selTexB, relTex, miniTex;          // texturas de portada: seleccionada A, fundido B (TOP), relevo, mini-cubo
        private Bitmap selImgA, selImgB, relImg, miniImg;          // ultima portada subida en cada ranura (para re-subir solo al cambiar)
        private float selUA = 1f, selVA = 1f, selUB = 1f, selVB = 1f, relU = 1f, relV = 1f, miniU = 1f, miniV = 1f;
        // Textura PRINCIPAL de la UI: su alto depende del modo (en navegacion es solo la franja superior,
        // ~7x mas barata de rehacer/subir que la pantalla completa). Se dibuja anclada ARRIBA, a su tamano real.
        private IntPtr uiTex; private int uiVer = -1; private float uiU = 1f, uiV = 1f; private int uiBmpW = -1, uiBmpH = -1;
        private readonly D3D9.Vtx[] uiQuad = new D3D9.Vtx[6];
        // Franja de ESTADO de abajo (sonando/tiempo/creditos/progreso) en SU PROPIA textura chica: el tic del
        // reloj (~2 cambios/seg con musica) ya no rehace ni re-sube la textura principal. Anclada ABAJO.
        private IntPtr uiBotTex; private int uiBotVer = -1; private float uiBotU = 1f, uiBotV = 1f; private int uiBotW = -1, uiBotH = -1;
        private readonly D3D9.Vtx[] uiBotQuad = new D3D9.Vtx[6];
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
        public float Dim = 1f;                            // atenuacion del starfield mientras se navega con musica (el form la anima; 1 = normal)
        public Func<float> GetPulse;                      // el form provee el nivel de audio (0..1)
        public GpuGridFetch GetGrid;                      // el form llena el buffer con las celdas de la pagina actual (rejilla por quads)
        public Func<Bitmap> GetContour;                   // sprite del contorno de seleccion
        public Func<Rectangle> GetContourRect;            // donde va el contorno (celda seleccionada), en coords del panel
        public Func<GpuCubeState> GetCubeState;           // celdas/angulos/portadas de los cubos que giran (seleccionado + relevo)
        public int ThrottleMs;                            // >0 = a lo mas un cuadro cada tantos ms (con video sonando: ~30 fps)
        private int lastFrameMs;                          // TickCount del ultimo cuadro dibujado
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
        }

        private void OnIdle(object sender, EventArgs e)
        {
            // Con video sonando el panel se LIMITA (ThrottleMs ~33 = 30 fps): el resto del equipo es para el
            // video. El sueno de 1 ms no atora nada: en cuanto llega un mensaje (tecla, timer), AppIdle lo ve
            // y el bucle sale a atenderlo.
            while (stars != null && stars.Ok && Visible && AppIdle())
            {
                int th = ThrottleMs;
                if (th > 0)
                {
                    int since = Environment.TickCount - lastFrameMs;
                    if (since >= 0 && since < th) { System.Threading.Thread.Sleep(1); continue; }
                }
                RenderFrame();
            }
        }

        public bool StartGpu()
        {
            if (stars == null || !stars.Ok)
            {
                if (stars != null) stars.Dispose();
                stars = new GpuStarfield(this.Handle, Width, Height, 4321);
                if (!stars.Ok) { stars.Dispose(); stars = null; return false; }
            }
            if (!idleHooked) { Application.Idle += OnIdle; idleHooked = true; }
            return true;
        }

        public void StopGpu()
        {
            if (idleHooked) { Application.Idle -= OnIdle; idleHooked = false; }
            ReleaseCellTex();   // libera todas las texturas de celda (cache por Folder + slots TOP)
            if (contourTex != IntPtr.Zero) { D3D9.Release(contourTex); contourTex = IntPtr.Zero; }
            if (selTexA != IntPtr.Zero) { D3D9.Release(selTexA); selTexA = IntPtr.Zero; }
            if (selTexB != IntPtr.Zero) { D3D9.Release(selTexB); selTexB = IntPtr.Zero; }
            if (relTex != IntPtr.Zero) { D3D9.Release(relTex); relTex = IntPtr.Zero; }
            if (miniTex != IntPtr.Zero) { D3D9.Release(miniTex); miniTex = IntPtr.Zero; }
            if (transTex != IntPtr.Zero) { D3D9.Release(transTex); transTex = IntPtr.Zero; transLastBmp = null; }
            if (winTex != IntPtr.Zero) { D3D9.Release(winTex); winTex = IntPtr.Zero; winLastBmp = null; }
            if (uiTex != IntPtr.Zero) { D3D9.Release(uiTex); uiTex = IntPtr.Zero; }
            if (uiBotTex != IntPtr.Zero) { D3D9.Release(uiBotTex); uiBotTex = IntPtr.Zero; }
            if (fpsTex != IntPtr.Zero) { D3D9.Release(fpsTex); fpsTex = IntPtr.Zero; }
            lastContour = null; selImgA = selImgB = relImg = miniImg = null; uiVer = -1; uiBotVer = -1; fpsShown = int.MinValue;
            uiBmpW = uiBmpH = -1; uiBotW = uiBotH = -1;   // fuerza recrear las texturas reutilizables tras reiniciar el device
            if (stars != null) { stars.Dispose(); stars = null; }
        }

        // Libera y olvida todas las texturas de celda (cache por Folder + fallback por slot). Al reiniciar el
        // device o apagar la GPU: sus IntPtr dejan de ser validos.
        private void ReleaseCellTex()
        {
            foreach (CellTex ct in cellTexCache.Values) if (ct.Tex != IntPtr.Zero) D3D9.Release(ct.Tex);
            cellTexCache.Clear();
            for (int i = 0; i < slotTex.Length; i++) { CellTex ct = slotTex[i]; if (ct != null && ct.Tex != IntPtr.Zero) D3D9.Release(ct.Tex); slotTex[i] = null; }
            for (int i = 0; i < gridTex.Length; i++) gridTex[i] = IntPtr.Zero;
            gridN = 0;
        }

        // Sube la portada como textura SOLO si cambio respecto a la ranura (referencia distinta). Asi el
        // cubo girando no sube nada en la mayoria de cuadros: solo al navegar o al ciclar un disco TOP.
        private void EnsureCover(ref IntPtr tex, ref Bitmap last, ref float u, ref float v, Bitmap cur)
        {
            if (ReferenceEquals(cur, last)) return;
            if (tex != IntPtr.Zero) { D3D9.Release(tex); tex = IntPtr.Zero; }
            if (cur != null) { float uu, vv; tex = stars.CreateOverlay(cur, out uu, out vv); u = uu; v = vv; }
            last = cur;
        }

        private void RenderFrame()
        {
            if (stars == null || !stars.Ok) return;
            lastFrameMs = Environment.TickCount;
            stars.SetSky(SkyTop, SkyMid, SkyBot);
            // TRABAJO PESADO acotado: rehacer las barras (UI, bitmap a pantalla completa) cuesta; se limita a UNA
            // por franja (heavyGapMs, que SE AUTOAJUSTA al coste real). La rejilla de discos ya NO es trabajo
            // pesado: cada disco es su propio quad con textura cacheada (rejilla por celdas, mas abajo).
            int nowMs = Environment.TickCount;
            bool canHeavy = (nowMs - lastHeavyMs) >= heavyGapMs;
            // REJILLA POR CELDAS: pedimos las celdas de la pagina y aseguramos (subimos si falta) su textura ANTES
            // de Begin, repartiendo las subidas NUEVAS por cuadro (presupuesto) para no bajar los fps al barrer.
            gridN = 0;
            if (ShowAlbum && GetGrid != null)
            {
                gridN = GetGrid(gridBuf);
                cellFrame++;
                int upBudget = GridUploadBudget;
                for (int i = 0; i < gridN; i++)
                {
                    IntPtr tex; float u, v;
                    if (gridBuf[i].Bmp != null && EnsureCellTex(gridBuf[i], ref upBudget, out tex, out u, out v))
                    { gridTex[i] = tex; gridU[i] = u; gridV[i] = v; }
                    else gridTex[i] = IntPtr.Zero;   // aun sin hornear o sin presupuesto de subida: se salta este cuadro (aparece luego)
                }
                EvictCellTex();
            }
            // Contorno de seleccion: sprite estatico (se sube cuando cambia el bitmap, p.ej. al cambiar tamano).
            if (GetContour != null)
            {
                Bitmap cb = GetContour();
                if (cb != lastContour)
                {
                    if (contourTex != IntPtr.Zero) { D3D9.Release(contourTex); contourTex = IntPtr.Zero; }
                    if (cb != null) contourTex = stars.CreateOverlay(cb, out contU, out contV);
                    lastContour = cb;
                }
            }
            // Cubos que GIRAN (seleccionado + relevo): suben la PORTADA como textura SOLO cuando cambia
            // (al navegar o al ciclar un disco TOP), no en cada cuadro. El giro luego es solo geometria.
            GpuCubeState cs = (GetCubeState != null) ? GetCubeState() : new GpuCubeState();
            EnsureCover(ref selTexA, ref selImgA, ref selUA, ref selVA, cs.Sel.CoverA);
            EnsureCover(ref selTexB, ref selImgB, ref selUB, ref selVB, cs.Sel.CoverB);
            EnsureCover(ref relTex, ref relImg, ref relU, ref relV, cs.Relay.CoverA);
            EnsureCover(ref miniTex, ref miniImg, ref miniU, ref miniV, cs.Mini.CoverA);   // mini-cubo de la barra inferior
            // ANIMACIONES: sus texturas TAMBIEN se aseguran AQUI, antes de Begin. Crear o bloquear una textura
            // con la escena ya abierta no es valido en Direct3D 9: fallaba en silencio y la portada que viajaba
            // entre cubos no llegaba a dibujarse NUNCA (la del buscador al abrir si, porque esa reusa la textura
            // de la UI, que ya venia subida de antes). Aqui se resuelven; abajo solo se dibujan.
            GpuTrans tr = (GetTrans != null) ? GetTrans() : new GpuTrans();
            if (tr.Has && tr.Bmp != null) EnsureCover(ref transTex, ref transLastBmp, ref transU, ref transV, tr.Bmp);
            GpuWin gw = (GetWin != null) ? GetWin() : new GpuWin();
            if (gw.Has && gw.Bmp != null) EnsureCover(ref winTex, ref winLastBmp, ref winU, ref winV, gw.Bmp);
            // Barras (UI) a textura: se re-sube solo cuando cambia algun texto/estado (firma del form), limitada a
            // UNA por franja (heavyGapMs). UiUrgent: al CAMBIAR DE MODO (p.ej. cerrar el buscador) hay que rehacerla
            // en ESTE mismo cuadro, sin esperar franja: si no, se veria la rejilla por los huecos del teclado un instante.
            bool uiUrgent = UiUrgent; UiUrgent = false;
            if (GetUi != null && GetUiVersion != null && (uiTex == IntPtr.Zero || uiUrgent || canHeavy))
            {
                int t0 = Environment.TickCount;
                int uv = GetUiVersion();   // puede rehacer el bitmap de la UI por dentro
                if (uv != uiVer)
                {
                    Bitmap ub = GetUi();
                    if (ub != null) uiTex = Upload(uiTex, ref uiBmpW, ref uiBmpH, ub, out uiU, out uiV);   // reutiliza la textura (no re-crear a pantalla completa cada vez)
                    uiVer = uv;
                    NoteHeavy(t0);   // mide el coste real y ajusta la espera (la UI cacheada es barata -> franja corta)
                }
            }
            // Franja de ESTADO de abajo: textura chica aparte. El tic del reloj/progreso (~2/seg con musica)
            // rehace y sube SOLO esta tira (~400KB), no la textura principal. Misma franja de trabajo pesado.
            if (GetUiBot != null && GetUiBotVersion != null && (uiBotTex == IntPtr.Zero || uiUrgent || canHeavy))
            {
                int bv = GetUiBotVersion();   // puede rehacer el bitmap de la tira por dentro
                if (bv != uiBotVer)
                {
                    Bitmap bb = GetUiBot();
                    if (bb != null) uiBotTex = Upload(uiBotTex, ref uiBotW, ref uiBotH, bb, out uiBotU, out uiBotV);
                    uiBotVer = bv;
                }
            }
            if (ShowFps) EnsureFpsTex();   // crea/actualiza la textura del contador ANTES de Begin (solo si se pide, Ctrl+F1)
            stars.SetDim(Dim);             // atenuacion mientras se navega con musica (estrellas/brillos; el cielo queda igual)
            float pulse = ((GetPulse != null) ? GetPulse() : 0f) * Dim;   // el beat tambien baja mientras esta atenuado
            // Dispositivo perdido (sesion bloqueada, cambio de pantalla...): sin este respiro, el bucle de
            // Application.Idle reintentaria a TODA MAQUINA y clavaria un nucleo entero hasta recuperarlo.
            if (!stars.Begin(Width, Height, pulse)) { System.Threading.Thread.Sleep(5); return; }
            // AURORA: va pegada al cielo, ANTES de la rejilla y los cubos. Asi es fondo de verdad: la musica se
            // ve respirar por detras y las portadas siguen siendo lo que manda en pantalla.
            if (GetAurora != null) { GpuAurora au = GetAurora(); if (au.Has && au.B != null && au.B.Length >= 8) DrawAurora(au); }
            // Rejilla de discos: un quad por celda (textura ya resuelta arriba). Las celdas aun sin textura (recien
            // horneadas o sin presupuesto de subida este cuadro) se saltan y aparecen en los cuadros siguientes.
            if (ShowAlbum)
                for (int i = 0; i < gridN; i++)
                    if (gridTex[i] != IntPtr.Zero)
                    { GpuGridCell c = gridBuf[i]; SetTexQuad(cellQuad, c.X, c.Y, c.X + c.W, c.Y + c.H, gridU[i], gridV[i]); stars.DrawQuad(gridTex[i], cellQuad); }
            if (ShowAlbum && contourTex != IntPtr.Zero && GetContourRect != null)
            {
                Rectangle r = GetContourRect();
                if (r.Width > 0 && r.Height > 0) { SetTexQuad(contourQuad, r.X, r.Y, r.X + r.Width, r.Y + r.Height, contU, contV); stars.DrawQuad(contourTex, contourQuad); }
            }
            // Cubos girando ENCIMA del contorno (igual orden que en CPU): primero el relevo, luego el seleccionado.
            if (cs.Relay.Has)
                stars.DrawCube(relTex, relU, relV, cs.Relay.X, cs.Relay.Y, cs.Relay.W, cs.Relay.H, cs.Relay.Angle, 1f, cs.Relay.Tilt);
            if (cs.Sel.Has)
            {
                stars.DrawCube(selTexA, selUA, selVA, cs.Sel.X, cs.Sel.Y, cs.Sel.W, cs.Sel.H, cs.Sel.Angle, 1f, cs.Sel.Tilt);
                if (cs.Sel.CoverB != null && cs.Sel.Fade > 0.002f)   // disco TOP: 2a portada encima con alfa = fundido
                    stars.DrawCube(selTexB, selUB, selVB, cs.Sel.X, cs.Sel.Y, cs.Sel.W, cs.Sel.H, cs.Sel.Angle, cs.Sel.Fade, cs.Sel.Tilt);
            }
            // UI ENCIMA del fondo y los cubos: la textura principal anclada ARRIBA (a su tamano real: en
            // navegacion es solo la franja superior) y la franja de estado anclada ABAJO.
            bool uiPropia = gw.Has && gw.Bmp == null;   // se anima la PROPIA textura de UI (abriendo el buscador)
            if (uiTex != IntPtr.Zero && uiBmpW > 0 && uiBmpH > 0 && !(gw.Has && gw.HideUi))
                { SetTexQuad(uiQuad, 0, 0, uiBmpW, uiBmpH, uiU, uiV); stars.DrawQuad(uiTex, uiQuad); }
            // VENTANA del buscador abriendo/cerrando: el mismo quad de la UI, escalado desde su centro y fundido.
            if (gw.Has && gw.Alpha > 0.004f && gw.Scale > 0.01f)
            {
                IntPtr wt; float wu, wv, bw, bh;
                if (uiPropia) { wt = uiTex; wu = uiU; wv = uiV; bw = uiBmpW; bh = uiBmpH; }
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
            if (uiBotTex != IntPtr.Zero && uiBotW > 0 && uiBotH > 0) { SetTexQuad(uiBotQuad, 0, Height - uiBotH, uiBotW, Height, uiBotU, uiBotV); stars.DrawQuad(uiBotTex, uiBotQuad); }
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
                stars.DrawCube(miniTex, miniU, miniV, cs.Mini.X, cs.Mini.Y, cs.Mini.W, cs.Mini.H, cs.Mini.Angle, 1f, cs.Mini.Tilt);
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
            // Contador de FPS (diagnostico) encima de todo, en su textura pequeña. Oculto por defecto (Ctrl+F1).
            if (ShowFps && fpsTex != IntPtr.Zero) { SetTexQuad(fpsQuad, Width - fpsTW - 10, 6, Width - 10, 6 + fpsTH, fpsU, fpsV); stars.DrawQuad(fpsTex, fpsQuad); }
            stars.End();
            frames++;
            if (Environment.TickCount - fpsTick >= 1000) { Fps = frames; frames = 0; fpsTick = Environment.TickCount; }
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
                    ct.Tex = Upload(ct.Tex, ref ct.Bw, ref ct.Bh, c.Bmp, out ct.U, out ct.V);   // portada llego / re-horneada: re-sube (mismo tamano -> reutiliza)
                    ct.Bmp = c.Bmp;
                    if (ct.Tex == IntPtr.Zero) return false;
                    tex = ct.Tex; u = ct.U; v = ct.V; return true;
                }
                if (upBudget <= 0) return false;   // celda nueva sin presupuesto: se salta este cuadro
                upBudget--;
                ct = new CellTex();
                ct.Tex = Upload(IntPtr.Zero, ref ct.Bw, ref ct.Bh, c.Bmp, out ct.U, out ct.V);
                ct.Bmp = c.Bmp; ct.Frame = cellFrame;
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
                st.Tex = Upload(st.Tex, ref st.Bw, ref st.Bh, c.Bmp, out st.U, out st.V);
                st.Bmp = c.Bmp;
                if (st.Tex == IntPtr.Zero) return false;
            }
            tex = st.Tex; u = st.U; v = st.V; return true;
        }

        // Expulsa las texturas de celda mas VIEJAS (no usadas este cuadro) cuando se pasa del tope, para acotar VRAM.
        // La "edad" se calcula por RESTA (cellFrame - Frame): sobrevive el desbordamiento del contador de cuadros
        // (int a 60fps se desborda a los ~414 dias encendida; misma familia que el TickCount de las fugaces).
        private void EvictCellTex()
        {
            while (cellTexCache.Count > CellTexCap)
            {
                string oldestKey = null; int oldestAge = -1;
                foreach (KeyValuePair<string, CellTex> kv in cellTexCache)
                { int age = cellFrame - kv.Value.Frame; if (age > oldestAge) { oldestAge = age; oldestKey = kv.Key; } }
                if (oldestKey == null || oldestAge <= 0) break;   // no expulsar lo que se dibujo este cuadro
                CellTex ct = cellTexCache[oldestKey];
                if (ct.Tex != IntPtr.Zero) D3D9.Release(ct.Tex);
                cellTexCache.Remove(oldestKey);
            }
        }

        // Rehace la textura del contador SOLO cuando cambia el numero (~1/seg). Es diminuta: coste despreciable.
        private void EnsureFpsTex()
        {
            if (fpsTex != IntPtr.Zero && fpsShown == Fps) return;
            fpsShown = Fps;
            fpsTW = 190; fpsTH = 26;
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
                    g.DrawString("GPU D3D9: " + Fps + " FPS", f, yb, 6, 4);
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

        // Convierte 8 bandas en una franja de luz continua. Dos pasadas: una BRUMA ancha y tenue que da el halo
        // (como el resplandor de una ciudad en el horizonte) y encima la luz principal, mas ajustada. Cada punto
        // es un halo aditivo, la misma tecnica de los destellos del buscador: sin bordes duros por diseno.
        //
        // Los dos numeros de puntos son distintos a proposito. La luz lleva 40 porque es la que dibuja la FORMA
        // (ahi si se nota el detalle). La bruma es un halo enorme y difuso donde cada mancha mide mas de tres
        // separaciones: con 20 puntos se ve igual (medido: 6 de 255 de diferencia como maximo, invisible) y se
        // pinta la MITAD de pixeles, que es lo caro de verdad en la maquina modesta. Como la luz se SUMA, la
        // mitad de manchas necesita el doble de alfa para que la bruma quede con el mismo brillo de siempre.
        private const int AurNBruma = 20, AurNLuz = 40;
        private void DrawAurora(GpuAurora au)
        {
            if (au.Alto < 2f || au.Dim <= 0.02f) return;
            float paso40 = (float)Width / (AurNLuz - 1);   // los radios van en pixeles, no dependen del numero de puntos
            stars.GlowBatchBegin(AurNBruma + AurNLuz);      // todo el horizonte en UNA sola llamada de dibujo
            for (int pasada = 0; pasada < 2; pasada++)
            {
                bool bruma = (pasada == 0);
                int N = bruma ? AurNBruma : AurNLuz;
                float paso = (float)Width / (N - 1);
                float k = bruma ? 42f * (AurNLuz - 1) / (float)(AurNBruma - 1) : 116f;   // compensa la densidad
                for (int i = 0; i < N; i++)
                {
                    // posicion continua dentro de las 8 bandas + suavizado: sale una CURVA, no dientes de sierra
                    float t = (float)i / (N - 1) * (au.B.Length - 1);
                    int b0 = (int)t; if (b0 > au.B.Length - 2) b0 = au.B.Length - 2;
                    float f = t - b0; f = f * f * (3f - 2f * f);
                    float v = au.B[b0] + (au.B[b0 + 1] - au.B[b0]) * f;
                    if (v <= 0.006f) continue;
                    float h = v * au.Alto;
                    float r = bruma ? (paso40 * 3.2f + h * 1.15f) : (paso40 * 1.5f + h * 0.5f);
                    float cy = au.BaseY - h * (bruma ? 0.30f : 0.45f);
                    int a = (int)(v * au.Dim * k + 0.5f);
                    if (a < 2) continue; if (a > 255) a = 255;
                    // los picos tiran a blanco: el color del genero se mantiene, pero la cresta brilla
                    uint rr = (au.Rgb >> 16) & 0xFF, gg = (au.Rgb >> 8) & 0xFF, bb = au.Rgb & 0xFF;
                    float w = v * 0.55f;
                    rr = (uint)(rr + (255 - rr) * w); gg = (uint)(gg + (255 - gg) * w); bb = (uint)(bb + (255 - bb) * w);
                    stars.GlowBatchAdd(i * paso, cy, r, ((uint)a << 24) | (rr << 16) | (gg << 8) | bb);
                }
            }
            stars.GlowBatchEnd();
        }

        protected override void OnPaint(PaintEventArgs e) { RenderFrame(); }   // WM_PAINT -> dibuja por D3D9
        protected override void OnPaintBackground(PaintEventArgs e) { }        // no borra (D3D9 es dueño de los pixeles)

        protected override void Dispose(bool disposing)
        {
            if (disposing) StopGpu();
            base.Dispose(disposing);
        }
    }
}
#endif

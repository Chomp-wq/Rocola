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

        private void InitStates()
        {
            setRS(dev, D3D9.RS_ZENABLE, 0);
            setRS(dev, D3D9.RS_LIGHTING, 0);
            setRS(dev, D3D9.RS_CULLMODE, D3D9.CULL_NONE);
            setFVF(dev, D3D9.FVF);
            setSamp(dev, 0, D3D9.SAMP_MAGFILTER, D3D9.TEXF_LINEAR);
            setSamp(dev, 0, D3D9.SAMP_MINFILTER, D3D9.TEXF_LINEAR);
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
            setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_SELECTARG1); setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_DIFFUSE);
            setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_SELECTARG1); setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_DIFFUSE);
        }
        private void TssModulate()  // textura * color del vertice
        {
            setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_MODULATE); setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_TEXTURE); setTSS(dev, 0, D3D9.TSS_COLORARG2, D3D9.TA_DIFFUSE);
            setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_MODULATE); setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_TEXTURE); setTSS(dev, 0, D3D9.TSS_ALPHAARG2, D3D9.TA_DIFFUSE);
        }

        // Cielo (color del vertice, sin mezcla) + estrellas (textura de glow, aditivo).
        public void DrawSkyStars(D3D9.Vtx[] sky, D3D9.Vtx[] stars, int starTris, IntPtr glowTex)
        {
            setTex(dev, 0, IntPtr.Zero); TssDiffuse();
            setRS(dev, D3D9.RS_ALPHABLENDENABLE, 0);
            Draw(sky, 2);
            if (starTris > 0 && glowTex != IntPtr.Zero)
            {
                setTex(dev, 0, glowTex); TssModulate();
                setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1);
                setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_SRCALPHA); setRS(dev, D3D9.RS_DESTBLEND, D3D9.BLEND_ONE);
                Draw(stars, starTris);
            }
        }

        // Un cuadro texturizado (2 triangulos) con mezcla NORMAL (encima): para los cubos, etiquetas, etc.
        public void DrawTexQuad(IntPtr tex, D3D9.Vtx[] quad)
        {
            if (tex == IntPtr.Zero) return;
            setTex(dev, 0, tex); TssModulate();
            setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1);
            setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_SRCALPHA); setRS(dev, D3D9.RS_DESTBLEND, D3D9.BLEND_INVSRCALPHA);
            Draw(quad, 2);
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

        // Actualiza EN SITIO los pixeles de una textura pow2 ya creada (mismo tamaño de bitmap). Evita
        // recrear/liberar una textura ~24 veces/seg (el cubo girando), que en sesiones largas de rocola
        // podria fragmentar memoria de video. Solo bloquea, copia y desbloquea (el pool MANAGED lo re-sube).
        public bool UpdateTexturePow2(IntPtr tex, Bitmap bmp)
        {
            if (dev == IntPtr.Zero || tex == IntPtr.Zero) return false;
            int w = bmp.Width, h = bmp.Height;
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
        private D3D9Device gpu; private IntPtr glowTex;
        private float[] sx, sy, sphase, sspeed, ssize; private int[] skind; private uint[] scol; private int nStars;
        private D3D9.Vtx[] skyV = new D3D9.Vtx[6]; private D3D9.Vtx[] starV;
        private uint skyTop = 0xFF06081A, skyBot = 0xFF1A2C5A;
        private readonly int startTick = Environment.TickCount;

        public bool Ok { get { return gpu != null && gpu.Ok; } }
        public int Count { get { return nStars; } }

        public GpuStarfield(IntPtr hwnd, int w, int h, int seed)
        {
            gpu = new D3D9Device(hwnd, w, h);
            if (!gpu.Ok) return;
            using (Bitmap tex = BakeStarTexture(64)) glowTex = gpu.CreateTextureFromBitmap(tex);
            BuildStars(w, h, seed);
        }

        public void SetSky(uint top, uint bot) { skyTop = top; skyBot = bot; }
        public IntPtr CreateOverlay(Bitmap bmp, out float uMax, out float vMax) { return gpu.CreateTexturePow2(bmp, out uMax, out vMax); }
        public bool UpdateOverlay(IntPtr tex, Bitmap bmp) { return gpu.UpdateTexturePow2(tex, bmp); }   // re-sube pixeles sin recrear (mismo tamaño)

        public void Render(int w, int h, float pulse) { if (Begin(w, h, pulse)) End(); }   // solo cielo+estrellas (ventana de prueba)

        // Inicia el cuadro: dibuja el cielo + estrellas. Luego se pueden apilar texturas con DrawQuad y cerrar con End.
        public bool Begin(int w, int h, float pulse)
        {
            if (!Ok) return false;
            if (!gpu.BeginFrame()) return false;
            float t = (Environment.TickCount - startTick) / 1000f;
            SetQuad(skyV, 0, 0, 0, w, h, skyTop, skyTop, skyBot, skyBot);
            int vi = 0;
            for (int i = 0; i < nStars; i++)
            {
                double tw = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(t * sspeed[i] + sphase[i]));
                uint a = (uint)(tw * 255); if (a > 255) a = 255;
                uint c = (a << 24) | (scol[i] & 0x00FFFFFF);             // alfa = brillo (titileo); rgb = color
                float s = ssize[i];
                if (skind[i] != 0 && pulse > 0.02f) s += pulse * (skind[i] == 2 ? 9f : 5f);   // latido: medianas/grandes crecen con la musica
                SetQuad(starV, vi, sx[i] - s, sy[i] - s, sx[i] + s, sy[i] + s, c, c, c, c);
                vi += 6;
            }
            gpu.DrawSkyStars(skyV, starV, nStars * 2, glowTex);
            return true;
        }
        public void DrawQuad(IntPtr tex, D3D9.Vtx[] quad) { if (Ok) gpu.DrawTexQuad(tex, quad); }
        public void End() { if (Ok) gpu.EndFrame(); }

        public void Dispose()
        {
            if (glowTex != IntPtr.Zero) { D3D9.Release(glowTex); glowTex = IntPtr.Zero; }
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
            for (int i = 0; i < nStars; i++)
            {
                sx[i] = (float)(rnd.NextDouble() * w);
                sy[i] = (float)(rnd.NextDouble() * h);
                sphase[i] = (float)(rnd.NextDouble() * Math.PI * 2);
                sspeed[i] = 0.6f + (float)rnd.NextDouble() * 1.8f;
                int kk = rnd.Next(100);
                skind[i] = (kk < 12) ? 2 : (kk < 34) ? 1 : 0;
                ssize[i] = (skind[i] == 2) ? (16 + rnd.Next(12)) : (skind[i] == 1) ? (9 + rnd.Next(7)) : (4 + rnd.Next(4));
                scol[i] = pal[rnd.Next(pal.Length)];
            }
            starV = new D3D9.Vtx[nStars * 6];
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

    // -------- Control D3D9 para usar el campo estrellado como FONDO de la vista de discos --------
    //  Es un control aparte (su propia ventana) con estilos aptos para D3D9 (sin doble-buffer GDI).
    //  Se muestra ENCIMA de la vista de discos al activar el modo GPU; el render lo maneja D3D9.
    internal sealed class GpuPanel : Control
    {
        private GpuStarfield stars;
        private bool idleHooked;
        private int frames, fpsTick;
        private IntPtr cubeTex; private int cubeVer = -1; private float cubeU = 1f, cubeV = 1f;
        private readonly D3D9.Vtx[] cubeQuad = new D3D9.Vtx[6];
        private IntPtr contourTex; private Bitmap lastContour; private float contU = 1f, contV = 1f;
        private readonly D3D9.Vtx[] contourQuad = new D3D9.Vtx[6];
        private IntPtr spinTex; private int spinVer = -1; private float spinU = 1f, spinV = 1f;
        private int spinBmpW = -1, spinBmpH = -1;          // tamaño del bitmap subido (para actualizar en sitio si no cambia)
        private readonly D3D9.Vtx[] spinQuad = new D3D9.Vtx[6];
        public int Fps;                                   // cuadros por segundo (para mostrar mientras integramos)
        public Func<float> GetPulse;                      // el form provee el nivel de audio (0..1)
        public Func<Bitmap> GetCubes;                     // el form provee el bitmap de cubos+etiquetas (cubeCache)
        public Func<int> GetCubesVersion;                 // cambia cuando cubeCache se reconstruye (re-subir textura)
        public Func<Bitmap> GetContour;                   // sprite del contorno de seleccion
        public Func<Rectangle> GetContourRect;            // donde va el contorno (celda seleccionada), en coords del panel
        public Func<Bitmap> GetSpin;                      // hornea el cubo seleccionado girando (+ relevo) y lo devuelve
        public Func<int> GetSpinVersion;                  // sube ~24fps al avanzar el giro (re-subir textura)
        public Func<Rectangle> GetSpinRect;               // donde va el overlay del giro (coords del panel)
        public uint SkyTop = 0xFF06081A, SkyBot = 0xFF1A2C5A;   // el form los setea por genero

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
            while (stars != null && stars.Ok && Visible && AppIdle()) RenderFrame();
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
            if (cubeTex != IntPtr.Zero) { D3D9.Release(cubeTex); cubeTex = IntPtr.Zero; }
            if (contourTex != IntPtr.Zero) { D3D9.Release(contourTex); contourTex = IntPtr.Zero; }
            if (spinTex != IntPtr.Zero) { D3D9.Release(spinTex); spinTex = IntPtr.Zero; }
            cubeVer = -1; lastContour = null; spinVer = -1; spinBmpW = spinBmpH = -1;
            if (stars != null) { stars.Dispose(); stars = null; }
        }

        private void RenderFrame()
        {
            if (stars == null || !stars.Ok) return;
            stars.SetSky(SkyTop, SkyBot);
            // Cubos: textura que cambia al navegar / llegar caratula.
            if (GetCubes != null && GetCubesVersion != null)
            {
                int v = GetCubesVersion();
                if (v != cubeVer)
                {
                    if (cubeTex != IntPtr.Zero) { D3D9.Release(cubeTex); cubeTex = IntPtr.Zero; }
                    Bitmap bm = GetCubes();
                    if (bm != null) cubeTex = stars.CreateOverlay(bm, out cubeU, out cubeV);
                    cubeVer = v;
                }
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
            // Cubo seleccionado GIRANDO (+ relevo): se re-hornea al ritmo del giro (~24fps); entre medias se reusa.
            if (GetSpin != null && GetSpinVersion != null)
            {
                int sv = GetSpinVersion();
                if (sv != spinVer)
                {
                    Bitmap sb = GetSpin();
                    if (sb == null)
                    {
                        if (spinTex != IntPtr.Zero) { D3D9.Release(spinTex); spinTex = IntPtr.Zero; }
                        spinBmpW = spinBmpH = -1;
                    }
                    else if (spinTex != IntPtr.Zero && sb.Width == spinBmpW && sb.Height == spinBmpH)
                    {
                        stars.UpdateOverlay(spinTex, sb);   // mismo tamaño: actualiza pixeles EN SITIO (sin recrear)
                    }
                    else
                    {
                        if (spinTex != IntPtr.Zero) { D3D9.Release(spinTex); spinTex = IntPtr.Zero; }
                        spinTex = stars.CreateOverlay(sb, out spinU, out spinV);
                        spinBmpW = sb.Width; spinBmpH = sb.Height;
                    }
                    spinVer = sv;
                }
            }
            float pulse = (GetPulse != null) ? GetPulse() : 0f;
            if (!stars.Begin(Width, Height, pulse)) return;
            if (cubeTex != IntPtr.Zero) { SetTexQuad(cubeQuad, 0, 0, Width, Height, cubeU, cubeV); stars.DrawQuad(cubeTex, cubeQuad); }
            if (contourTex != IntPtr.Zero && GetContourRect != null)
            {
                Rectangle r = GetContourRect();
                if (r.Width > 0 && r.Height > 0) { SetTexQuad(contourQuad, r.X, r.Y, r.X + r.Width, r.Y + r.Height, contU, contV); stars.DrawQuad(contourTex, contourQuad); }
            }
            if (spinTex != IntPtr.Zero && GetSpinRect != null)   // el cubo girando va ENCIMA del contorno (igual que en CPU)
            {
                Rectangle rs = GetSpinRect();
                if (rs.Width > 0 && rs.Height > 0) { SetTexQuad(spinQuad, rs.X, rs.Y, rs.X + rs.Width, rs.Y + rs.Height, spinU, spinV); stars.DrawQuad(spinTex, spinQuad); }
            }
            stars.End();
            frames++;
            if (Environment.TickCount - fpsTick >= 1000) { Fps = frames; frames = 0; fpsTick = Environment.TickCount; }
        }

        private static void SetTexQuad(D3D9.Vtx[] v, float x0, float y0, float x1, float y1, float uMax, float vMax)
        {
            SetV(ref v[0], x0, y0, 0, 0); SetV(ref v[1], x1, y0, uMax, 0); SetV(ref v[2], x1, y1, uMax, vMax);
            SetV(ref v[3], x0, y0, 0, 0); SetV(ref v[4], x1, y1, uMax, vMax); SetV(ref v[5], x0, y1, 0, vMax);
        }
        // -0.5 px: regla de D3D9 (el centro del pixel esta en x+0.5). Sin este ajuste, una textura mapeada
        // 1:1 (los cubos y sobre todo el TEXTO de las etiquetas) se muestrea entre texeles y se ve borrosa.
        private static void SetV(ref D3D9.Vtx vx, float x, float y, float u, float vv) { vx.X = x - 0.5f; vx.Y = y - 0.5f; vx.Z = 0; vx.Rhw = 1; vx.Color = 0xFFFFFFFF; vx.U = u; vx.V = vv; }

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

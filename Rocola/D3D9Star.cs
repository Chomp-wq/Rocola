// ====================================================================================================
//  RENDER POR GPU (Direct3D 9) — PASO 2: cielo + estrellas con GLOW y DESTELLO (textura), por interop.
// ----------------------------------------------------------------------------------------------------
//  Sin dependencias (llama a d3d9.dll y a la vtable). Hornea UNA textura de destello+glow (en CPU, una
//  vez) y dibuja cada estrella como un cuadro texturizado teñido de su color y con brillo titilante
//  (mezcla aditiva). Una ventana de prueba (F12) lo corre y muestra los FPS. No toca la app.
//  Todo dentro de #if ROCOLA_GPU.
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

        // Vertice 2D ya transformado + color + coordenada de textura. FVF = XYZRHW | DIFFUSE | TEX1.
        [StructLayout(LayoutKind.Sequential)]
        public struct Vtx { public float X, Y, Z, Rhw; public uint Color; public float U, V; }
        public const uint FVF = 0x004 | 0x040 | 0x100;
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
        public const uint BLEND_ONE = 2, BLEND_SRCALPHA = 5;
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

        // Sube un Bitmap (32bpp ARGB recto) a una textura MANAGED (sobrevive a la perdida de dispositivo).
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
            byte[] buf = new byte[rowBytes];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), buf, 0, rowBytes);
                Marshal.Copy(buf, 0, new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), rowBytes);
            }
            bmp.UnlockBits(bd);
            unlockR(tex, 0);
            return tex;
        }

        // Dibuja un cuadro: cielo (sin textura, color del vertice) + estrellas (textura de glow, aditivo).
        public void Render(int w, int h, D3D9.Vtx[] sky, D3D9.Vtx[] stars, int starTris, IntPtr glowTex)
        {
            if (dev == IntPtr.Zero) return;
            if (lost)
            {
                int tc = testCoop(dev);
                if (tc == D3D9.E_DEVICENOTRESET) { if (reset(dev, ref pp) >= 0) { lost = false; InitStates(); } else return; }
                else return;
            }

            clear(dev, 0, IntPtr.Zero, D3D9.CLEAR_TARGET, 0xFF05060F, 1f, 0);
            if (begin(dev) < 0) return;
            try
            {
                // CIELO: sin textura, usa el color del vertice directamente, sin mezcla.
                setTex(dev, 0, IntPtr.Zero);
                setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_SELECTARG1); setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_DIFFUSE);
                setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_SELECTARG1); setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_DIFFUSE);
                setRS(dev, D3D9.RS_ALPHABLENDENABLE, 0);
                Draw(sky, 2);

                // ESTRELLAS: textura * color del vertice, mezcla ADITIVA pesada por el alfa de la textura.
                if (starTris > 0 && glowTex != IntPtr.Zero)
                {
                    setTex(dev, 0, glowTex);
                    setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_MODULATE); setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_TEXTURE); setTSS(dev, 0, D3D9.TSS_COLORARG2, D3D9.TA_DIFFUSE);
                    setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_MODULATE); setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_TEXTURE); setTSS(dev, 0, D3D9.TSS_ALPHAARG2, D3D9.TA_DIFFUSE);
                    setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1);
                    setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_SRCALPHA);
                    setRS(dev, D3D9.RS_DESTBLEND, D3D9.BLEND_ONE);
                    Draw(stars, starTris);
                }
            }
            finally { end(dev); }

            int hr = present(dev, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hr == D3D9.E_DEVICELOST) lost = true;
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

    // -------- Ventana de prueba: cielo + estrellas con glow/destello por GPU. F12 abre/cierra. --------
    internal sealed class GpuTestWindow : Form
    {
        private static GpuTestWindow instance;
        public static void Toggle()
        {
            if (instance != null) { instance.Close(); return; }
            instance = new GpuTestWindow();
            instance.Show();
        }

        private D3D9Device gpu;
        private IntPtr glowTex;
        private readonly Timer timer = new Timer();
        private readonly Random rnd = new Random(1234);
        private float[] sx, sy, sphase, sspeed, ssize; private uint[] scol; private int nStars;
        private D3D9.Vtx[] skyV = new D3D9.Vtx[6];
        private D3D9.Vtx[] starV;
        private int startTick, frames, fpsTick, fps;
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
            gpu = new D3D9Device(this.Handle, ClientSize.Width, ClientSize.Height);
            if (!gpu.Ok) { MessageBox.Show("No se pudo crear el dispositivo D3D9 para la ventana.", "GPU"); Close(); return; }
            using (Bitmap tex = BakeStarTexture(64)) glowTex = gpu.CreateTextureFromBitmap(tex);
            BuildStars(ClientSize.Width, ClientSize.Height);
            startTick = Environment.TickCount;
            timer.Interval = 15;
            timer.Tick += delegate { Frame(); };
            timer.Start();
        }

        // Textura blanca: glow radial suave + destello de 4 puntas. Se tiñe con el color de cada estrella.
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

        private void BuildStars(int w, int h)
        {
            nStars = Math.Max(60, w * h / 6500);
            if (nStars > 260) nStars = 260;
            sx = new float[nStars]; sy = new float[nStars]; sphase = new float[nStars];
            sspeed = new float[nStars]; ssize = new float[nStars]; scol = new uint[nStars];
            uint[] pal = { 0xFFFFFFFF, 0xFFFFFFFF, 0xFFB4CDFF, 0xFFCDA5FF, 0xFFFFAAE6, 0xFFFFDC8C, 0xFF96FFE1 };
            for (int i = 0; i < nStars; i++)
            {
                sx[i] = (float)(rnd.NextDouble() * w);
                sy[i] = (float)(rnd.NextDouble() * h);
                sphase[i] = (float)(rnd.NextDouble() * Math.PI * 2);
                sspeed[i] = 0.6f + (float)rnd.NextDouble() * 1.8f;
                int kk = rnd.Next(100);
                ssize[i] = (kk < 12) ? (16 + rnd.Next(12)) : (kk < 34) ? (9 + rnd.Next(7)) : (4 + rnd.Next(4));
                scol[i] = pal[rnd.Next(pal.Length)];
            }
            starV = new D3D9.Vtx[nStars * 6];
        }

        private void Frame()
        {
            if (gpu == null || !gpu.Ok) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            float t = (Environment.TickCount - startTick) / 1000f;

            uint top = 0xFF06081A, bot = 0xFF1A2C5A;
            SetQuad(skyV, 0, 0, 0, w, h, top, top, bot, bot);

            int vi = 0;
            for (int i = 0; i < nStars; i++)
            {
                double tw = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(t * sspeed[i] + sphase[i]));
                uint a = (uint)(tw * 255); if (a > 255) a = 255;
                uint c = (a << 24) | (scol[i] & 0x00FFFFFF);   // alfa = brillo (titileo); rgb = color de la estrella
                float s = ssize[i];
                SetQuad(starV, vi, sx[i] - s, sy[i] - s, sx[i] + s, sy[i] + s, c, c, c, c);
                vi += 6;
            }
            gpu.Render(w, h, skyV, starV, nStars * 2, glowTex);

            frames++;
            if (Environment.TickCount - fpsTick >= 1000)
            {
                fps = frames; frames = 0; fpsTick = Environment.TickCount;
                Text = BaseTitle + "   [" + fps + " FPS, " + nStars + " estrellas]";
            }
        }

        // 2 triangulos (6 vertices) de un rectangulo con color por esquina y coordenadas de textura 0..1.
        private static void SetQuad(D3D9.Vtx[] v, int o, float x0, float y0, float x1, float y1, uint cTL, uint cTR, uint cBR, uint cBL)
        {
            V(ref v[o + 0], x0, y0, cTL, 0, 0); V(ref v[o + 1], x1, y0, cTR, 1, 0); V(ref v[o + 2], x1, y1, cBR, 1, 1);
            V(ref v[o + 3], x0, y0, cTL, 0, 0); V(ref v[o + 4], x1, y1, cBR, 1, 1); V(ref v[o + 5], x0, y1, cBL, 0, 1);
        }
        private static void V(ref D3D9.Vtx v, float x, float y, uint c, float u, float vv) { v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.Color = c; v.U = u; v.V = vv; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == Keys.F12) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            if (glowTex != IntPtr.Zero) { D3D9.Release(glowTex); glowTex = IntPtr.Zero; }
            if (gpu != null) { gpu.Dispose(); gpu = null; }
            instance = null;
            base.OnFormClosed(e);
        }
    }
}
#endif

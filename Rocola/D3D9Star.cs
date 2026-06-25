// ====================================================================================================
//  RENDER POR GPU (Direct3D 9) — PASO 1: ventana de prueba con cielo + estrellas, SIN dependencias.
// ----------------------------------------------------------------------------------------------------
//  Extiende la tecnica que la prueba ya valido en la rocola: se llama a d3d9.dll por interop y a los
//  metodos del dispositivo por su lugar en la vtable. Aqui ya se DIBUJA: un cielo en degradado y estrellas
//  titilantes, aceleradas por GPU, en una ventana aparte que se abre/cierra con F12. No toca la app.
//
//  Todo dentro de #if ROCOLA_GPU. Con el simbolo apagado, el archivo compila vacio.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Drawing;
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

        // Vertice 2D ya transformado (coordenadas de pantalla) + color. FVF = XYZRHW | DIFFUSE.
        [StructLayout(LayoutKind.Sequential)]
        public struct Vtx { public float X, Y, Z, Rhw; public uint Color; }
        public const uint FVF_XYZRHW_DIFFUSE = 0x004 | 0x040;
        public const int VtxSize = 20;

        // Constantes D3D9 que usamos
        public const uint DEVTYPE_HAL = 1;
        public const uint CREATE_HARDWARE_VP = 0x40, CREATE_SOFTWARE_VP = 0x20, CREATE_FPU_PRESERVE = 0x2;
        public const uint FMT_UNKNOWN = 0;
        public const uint SWAPEFFECT_DISCARD = 1;
        public const uint CLEAR_TARGET = 1;
        public const uint PT_TRIANGLELIST = 4;
        // Render states
        public const uint RS_ZENABLE = 7, RS_CULLMODE = 22, RS_LIGHTING = 137;
        public const uint RS_ALPHABLENDENABLE = 27, RS_SRCBLEND = 19, RS_DESTBLEND = 20;
        public const uint BLEND_ONE = 2;
        public const uint CULL_NONE = 1;
        // Texture stage states (para usar el color del vertice SIN textura)
        public const uint TSS_COLOROP = 1, TSS_COLORARG1 = 2, TSS_ALPHAOP = 4, TSS_ALPHAARG1 = 5;
        public const uint TOP_SELECTARG1 = 2, TA_DIFFUSE = 0;
        // HRESULTs de dispositivo perdido
        public const int E_DEVICELOST = unchecked((int)0x88760868);
        public const int E_DEVICENOTRESET = unchecked((int)0x88760869);

        // Delegados (convencion stdcall, 'this' como primer argumento) para los metodos de la vtable.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateDeviceDel(IntPtr self, uint adapter, uint devType, IntPtr hwnd, uint behaviorFlags, ref PresentParams pp, out IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int HrSelf(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint UintSelf(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ResetDel(IntPtr self, ref PresentParams pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int PresentDel(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ClearDel(IntPtr self, uint count, IntPtr rects, uint flags, uint color, float z, uint stencil);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetU2Del(IntPtr self, uint a, uint b);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetU1Del(IntPtr self, uint a);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetTssDel(IntPtr self, uint stage, uint type, uint value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DrawUpDel(IntPtr self, uint primType, uint primCount, IntPtr data, uint stride);

        public static T Fn<T>(IntPtr punk, int slot) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(punk);
            IntPtr fn = Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
        }
        public static void Release(IntPtr punk) { try { Fn<UintSelf>(punk, 2)(punk); } catch { } }
    }

    // -------- Dispositivo D3D9 sobre una ventana, con dibujo de cielo + estrellas --------
    internal sealed class D3D9Device
    {
        private IntPtr d3d, dev;
        private D3D9.PresentParams pp;
        private bool lost;

        private D3D9.PresentDel present; private D3D9.ClearDel clear; private D3D9.HrSelf begin, end;
        private D3D9.SetU2Del setRS; private D3D9.SetU1Del setFVF; private D3D9.SetTssDel setTSS;
        private D3D9.DrawUpDel drawUp; private D3D9.HrSelf testCoop; private D3D9.ResetDel reset;

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
            setTSS = D3D9.Fn<D3D9.SetTssDel>(dev, 67);
            drawUp = D3D9.Fn<D3D9.DrawUpDel>(dev, 83);
            testCoop = D3D9.Fn<D3D9.HrSelf>(dev, 3);
            reset = D3D9.Fn<D3D9.ResetDel>(dev, 16);
            InitStates();
        }

        private static D3D9.PresentParams MakePP(IntPtr hwnd, int w, int h)
        {
            D3D9.PresentParams p = new D3D9.PresentParams();
            p.BackBufferWidth = (uint)Math.Max(1, w);
            p.BackBufferHeight = (uint)Math.Max(1, h);
            p.BackBufferFormat = D3D9.FMT_UNKNOWN;   // en ventana: usa el formato del escritorio
            p.BackBufferCount = 1;
            p.SwapEffect = D3D9.SWAPEFFECT_DISCARD;
            p.DeviceWindow = hwnd;
            p.Windowed = 1;
            p.PresentationInterval = 0;              // vsync por defecto
            return p;
        }

        private void InitStates()
        {
            setRS(dev, D3D9.RS_ZENABLE, 0);
            setRS(dev, D3D9.RS_LIGHTING, 0);
            setRS(dev, D3D9.RS_CULLMODE, D3D9.CULL_NONE);
            setFVF(dev, D3D9.FVF_XYZRHW_DIFFUSE);
            // usar el color del vertice directamente (sin textura) en la etapa 0
            setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_SELECTARG1);
            setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_DIFFUSE);
            setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_SELECTARG1);
            setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_DIFFUSE);
        }

        // Dibuja un cuadro: cielo en degradado (sin mezcla) + estrellas (mezcla aditiva).
        public void Render(int w, int h, D3D9.Vtx[] sky, D3D9.Vtx[] stars, int starTris)
        {
            if (dev == IntPtr.Zero) return;
            if (lost)
            {
                int tc = testCoop(dev);
                if (tc == D3D9.E_DEVICENOTRESET) { if (reset(dev, ref pp) >= 0) { lost = false; InitStates(); } else return; }
                else return;   // sigue perdido
            }

            clear(dev, 0, IntPtr.Zero, D3D9.CLEAR_TARGET, 0xFF05060F, 1f, 0);
            if (begin(dev) < 0) return;
            try
            {
                setRS(dev, D3D9.RS_ALPHABLENDENABLE, 0);
                Draw(sky, 2);
                setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1);
                setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_ONE);
                setRS(dev, D3D9.RS_DESTBLEND, D3D9.BLEND_ONE);
                if (starTris > 0) Draw(stars, starTris);
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

    // -------- Ventana de prueba: cielo + estrellas titilantes por GPU. F12 abre/cierra. --------
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
        private readonly Timer timer = new Timer();
        private readonly Random rnd = new Random(1234);
        private float[] sx, sy, sphase, sspeed, ssize; private uint[] scol; private int nStars;
        private D3D9.Vtx[] skyV = new D3D9.Vtx[6];
        private D3D9.Vtx[] starV;
        private int startTick;
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
            gpu = new D3D9Device(this.Handle, ClientSize.Width, ClientSize.Height);
            if (!gpu.Ok)
            {
                MessageBox.Show("No se pudo crear el dispositivo D3D9 para la ventana.", "GPU");
                Close();
                return;
            }
            BuildStars(ClientSize.Width, ClientSize.Height);
            startTick = Environment.TickCount;
            timer.Interval = 15;   // ~66 fps
            timer.Tick += delegate { Frame(); };
            timer.Start();
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
                ssize[i] = (kk < 12) ? (5 + rnd.Next(5)) : (kk < 34) ? (3 + rnd.Next(3)) : (1 + rnd.Next(2));
                scol[i] = pal[rnd.Next(pal.Length)];
            }
            starV = new D3D9.Vtx[nStars * 6];
        }

        private void Frame()
        {
            if (gpu == null || !gpu.Ok) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            float t = (Environment.TickCount - startTick) / 1000f;

            // cielo en degradado (arriba oscuro -> abajo azul noche)
            uint top = 0xFF06081A, bot = 0xFF1A2C5A;
            SetQuad(skyV, 0, 0, 0, w, h, top, top, bot, bot);

            // estrellas (cuadritos), brillo titilando; aditivo -> brillan
            int vi = 0;
            for (int i = 0; i < nStars; i++)
            {
                double tw = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(t * sspeed[i] + sphase[i]));
                uint c = Scale(scol[i], tw);
                float s = ssize[i];
                SetQuad(starV, vi, sx[i] - s, sy[i] - s, sx[i] + s, sy[i] + s, c, c, c, c);
                vi += 6;
            }
            gpu.Render(w, h, skyV, starV, nStars * 2);

            frames++;
            if (Environment.TickCount - fpsTick >= 1000)
            {
                fps = frames; frames = 0; fpsTick = Environment.TickCount;
                Text = BaseTitle + "   [" + fps + " FPS, " + nStars + " estrellas]";
            }
        }

        // Escala el RGB de un color ARGB por f (0..1+); para el brillo aditivo.
        private static uint Scale(uint argb, double f)
        {
            int r = (int)(((argb >> 16) & 0xFF) * f); if (r > 255) r = 255;
            int g = (int)(((argb >> 8) & 0xFF) * f); if (g > 255) g = 255;
            int b = (int)(((argb) & 0xFF) * f); if (b > 255) b = 255;
            return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }

        // Escribe 2 triangulos (6 vertices) de un rectangulo con colores por esquina, desde el indice 'o'.
        private static void SetQuad(D3D9.Vtx[] v, int o, float x0, float y0, float x1, float y1, uint cTL, uint cTR, uint cBR, uint cBL)
        {
            V(ref v[o + 0], x0, y0, cTL); V(ref v[o + 1], x1, y0, cTR); V(ref v[o + 2], x1, y1, cBR);
            V(ref v[o + 3], x0, y0, cTL); V(ref v[o + 4], x1, y1, cBR); V(ref v[o + 5], x0, y1, cBL);
        }
        private static void V(ref D3D9.Vtx v, float x, float y, uint c) { v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.Color = c; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == Keys.F12) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            if (gpu != null) { gpu.Dispose(); gpu = null; }
            instance = null;
            base.OnFormClosed(e);
        }
    }
}
#endif

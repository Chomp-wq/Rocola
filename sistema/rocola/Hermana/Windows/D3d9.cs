// EL DIRECT3D 9 DE ROCOLA OS. La rocola de XP dibuja con Direct3D 9 llamando a sus tablas COM (D3D9Star.cs: D3D9.Fn(objeto, ranura)).
// Aqui esas tablas existen: Direct3DCreate9 (exportada por el programa, ver Enlaces.cs) devuelve objetos con sus tablas de funciones,
// y cada funcion hace lo mismo con OpenGL ES 2. Solo lo que usa la rocola: vertices ya transformados (XYZRHW) con color y una o dos
// coordenadas de textura, dos etapas de textura de funcion fija (elegir, modular, suma suave; complemento y alfa replicado), mezcla
// normal / suma / uno-inverso, texturas A8R8G8B8 y X8R8G8B8 que se bloquean y se escriben desde el procesador, imagenes intermedias
// (render targets) y un buffer de vertices. La regla de los pixeles es la de Direct3D 9 (centros en los enteros); el muestreo, el de
// la tarjeta (lineal o punto, repetir o recortar). Lo que no esta, contesta E_NOTIMPL, y la rocola ya sabe vivir sin ello.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Rocola.Linux
{
    internal static unsafe class D3d9
    {
        internal const int S_OK = 0, E_FAIL = unchecked((int)0x80004005), E_NOTIMPL = unchecked((int)0x80004001), E_NOINTERFACE = unchecked((int)0x80004002),
            D3DERR_INVALIDCALL = unchecked((int)0x8876086C), E_OUTOFMEMORY = unchecked((int)0x8007000E);

        // ---------------- los objetos COM ----------------
        internal class Objeto { public int Refs = 1; public IntPtr Ptr; }
        internal sealed class Direct3D : Objeto { }
        internal sealed class Superficie : Objeto { public Textura Tex; }
        internal sealed class Vertices : Objeto { public IntPtr Mem; public int Largo; }
        internal sealed class Textura : Objeto
        {
            public int W, H; public bool Rt, Xrgb; public IntPtr Mem; public uint Gl, Fbo; public int SuciaY0 = int.MaxValue, SuciaY1 = -1;
            public uint DirU = uint.MaxValue, DirV = uint.MaxValue, Mag = uint.MaxValue, Min = uint.MaxValue;   // lo puesto en OpenGL
            public Superficie Sup; public bool Suelta;
        }
        private static readonly Dictionary<IntPtr, Objeto> objetos = new Dictionary<IntPtr, Objeto>();
        private static IntPtr Nuevo(IntPtr vt, Objeto o)
        {
            IntPtr p = (IntPtr)NativeMemory.AllocZeroed(16);
            *(IntPtr*)p = vt; o.Ptr = p;
            lock (objetos) objetos[p] = o;
            return p;
        }
        private static T De<T>(IntPtr p) where T : Objeto { lock (objetos) { Objeto o; return objetos.TryGetValue(p, out o) ? o as T : null; } }
        private static void Olvida(Objeto o) { lock (objetos) objetos.Remove(o.Ptr); NativeMemory.Free((void*)o.Ptr); o.Ptr = IntPtr.Zero; }

        // ---------------- las tablas ----------------
        private static readonly IntPtr vtD3d, vtDev, vtTex, vtSup, vtVb;
        static D3d9()
        {
            vtD3d = Tabla(20); vtDev = Tabla(120); vtTex = Tabla(24); vtSup = Tabla(20); vtVb = Tabla(16);
            foreach (IntPtr vt in new IntPtr[] { vtD3d, vtDev, vtTex, vtSup, vtVb })
            {
                Pon(vt, 0, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr*, int>)&QueryInterface);
                Pon(vt, 1, (IntPtr)(delegate* unmanaged<IntPtr, uint>)&AddRef);
                Pon(vt, 2, (IntPtr)(delegate* unmanaged<IntPtr, uint>)&Release);
            }
            Pon(vtD3d, 16, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, IntPtr, uint, PresentParams*, IntPtr*, int>)&CreateDevice);
            Pon(vtDev, 3, (IntPtr)(delegate* unmanaged<IntPtr, int>)&Bien);
            Pon(vtDev, 4, (IntPtr)(delegate* unmanaged<IntPtr, uint>)&MemoriaTexturas);
            Pon(vtDev, 5, (IntPtr)(delegate* unmanaged<IntPtr, int>)&Bien);
            Pon(vtDev, 7, (IntPtr)(delegate* unmanaged<IntPtr, byte*, int>)&GetDeviceCaps);
            Pon(vtDev, 16, (IntPtr)(delegate* unmanaged<IntPtr, PresentParams*, int>)&Reset);
            Pon(vtDev, 17, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int>)&Present);
            Pon(vtDev, 23, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, uint, uint, uint, uint, IntPtr*, IntPtr, int>)&CreateTexture);
            Pon(vtDev, 26, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, uint, uint, IntPtr*, IntPtr, int>)&CreateVertexBuffer);
            Pon(vtDev, 37, (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, int>)&SetRenderTarget);
            Pon(vtDev, 38, (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr*, int>)&GetRenderTarget);
            Pon(vtDev, 41, (IntPtr)(delegate* unmanaged<IntPtr, int>)&Bien);
            Pon(vtDev, 42, (IntPtr)(delegate* unmanaged<IntPtr, int>)&Bien);
            Pon(vtDev, 43, (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, uint, uint, float, uint, int>)&Clear);
            Pon(vtDev, 57, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, int>)&SetRenderState);
            Pon(vtDev, 65, (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, int>)&SetTexture);
            Pon(vtDev, 67, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, uint, int>)&SetTextureStageState);
            Pon(vtDev, 69, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, uint, int>)&SetSamplerState);
            Pon(vtDev, 70, (IntPtr)(delegate* unmanaged<IntPtr, uint*, int>)&ValidateDevice);
            Pon(vtDev, 81, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, uint, int>)&DrawPrimitive);
            Pon(vtDev, 83, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, IntPtr, uint, int>)&DrawPrimitiveUP);
            Pon(vtDev, 89, (IntPtr)(delegate* unmanaged<IntPtr, uint, int>)&SetFVF);
            Pon(vtDev, 100, (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, uint, uint, int>)&SetStreamSource);
            Pon(vtTex, 9, (IntPtr)(delegate* unmanaged<IntPtr, void>)&PreLoad);
            Pon(vtTex, 17, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint*, int>)&GetLevelDesc);
            Pon(vtTex, 18, (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr*, int>)&GetSurfaceLevel);
            Pon(vtTex, 19, (IntPtr)(delegate* unmanaged<IntPtr, uint, LockedRect*, int*, uint, int>)&LockRect);
            Pon(vtTex, 20, (IntPtr)(delegate* unmanaged<IntPtr, uint, int>)&UnlockRect);
            Pon(vtVb, 11, (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, IntPtr*, uint, int>)&LockVb);
            Pon(vtVb, 12, (IntPtr)(delegate* unmanaged<IntPtr, int>)&Bien);
        }
        private static IntPtr Tabla(int n)
        {
            IntPtr t = (IntPtr)NativeMemory.Alloc((nuint)(n * IntPtr.Size));
            for (int i = 0; i < n; i++) Pon(t, i, (IntPtr)(delegate* unmanaged<int>)&NoHecho);
            return t;
        }
        private static void Pon(IntPtr t, int i, IntPtr f) { ((IntPtr*)t)[i] = f; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PresentParams
        {
            public uint BackBufferWidth, BackBufferHeight, BackBufferFormat, BackBufferCount;
            public uint MultiSampleType, MultiSampleQuality, SwapEffect;
            public IntPtr DeviceWindow;
            public int Windowed, EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat, Flags, FullScreenRefreshRateInHz, PresentationInterval;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct LockedRect { public int Pitch; public IntPtr Bits; }

        // ---------------- lo comun ----------------
        [UnmanagedCallersOnly] private static int NoHecho() { return E_NOTIMPL; }
        [UnmanagedCallersOnly] private static int Bien(IntPtr self) { return S_OK; }
        [UnmanagedCallersOnly] private static int QueryInterface(IntPtr self, IntPtr riid, IntPtr* salida) { if (salida != null) *salida = IntPtr.Zero; return E_NOINTERFACE; }
        [UnmanagedCallersOnly] private static uint AddRef(IntPtr self) { Objeto o = De<Objeto>(self); return o == null ? 0u : (uint)++o.Refs; }
        [UnmanagedCallersOnly]
        private static uint Release(IntPtr self)
        {
            Objeto o = De<Objeto>(self); if (o == null) return 0;
            int r = --o.Refs;
            if (r > 0) return (uint)r;
            try { Destruye(o); } catch (Exception ex) { Apunta(ex); }
            return 0;
        }
        private static void Destruye(Objeto o)
        {
            Textura t = o as Textura;
            if (t != null)
            {
                Dispositivo.Suelta(t);
                if (t.Mem != IntPtr.Zero) { NativeMemory.Free((void*)t.Mem); t.Mem = IntPtr.Zero; }
                if (t.Sup != null && t.Sup.Ptr != IntPtr.Zero && t.Sup.Refs <= 0) Olvida(t.Sup);
            }
            Superficie s = o as Superficie;
            if (s != null && s.Tex != null && !s.Tex.Rt) { }
            Vertices v = o as Vertices;
            if (v != null && v.Mem != IntPtr.Zero) { NativeMemory.Free((void*)v.Mem); v.Mem = IntPtr.Zero; }
            Dispositivo d = o as Dispositivo;
            if (d != null) d.Cierra();
            if (s != null && s.Tex != null && s.Tex.Ptr != IntPtr.Zero) return;   // la superficie vive mientras viva su textura
            Olvida(o);
        }
        internal static void Apunta(Exception ex) { try { Console.Error.WriteLine("[Direct3D de Rocola OS] " + ex); } catch { } }

        // ---------------- Direct3D ----------------
        [UnmanagedCallersOnly(EntryPoint = "Direct3DCreate9")]
        internal static IntPtr Direct3DCreate9(uint version)
        {
            try { if (!Pantalla.Lista) return IntPtr.Zero; return Nuevo(vtD3d, new Direct3D()); }
            catch (Exception ex) { Apunta(ex); return IntPtr.Zero; }
        }
        [UnmanagedCallersOnly]
        private static int CreateDevice(IntPtr self, uint adapter, uint tipo, IntPtr hwnd, uint banderas, PresentParams* pp, IntPtr* salida)
        {
            *salida = IntPtr.Zero;
            try
            {
                Dispositivo d = new Dispositivo(hwnd, pp);
                *salida = Nuevo(vtDev, d);
                d.Crea();
                return S_OK;
            }
            catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }

        // ---------------- el dispositivo ----------------
        internal sealed class Dispositivo : Objeto
        {
            internal static readonly List<Dispositivo> Vivos = new List<Dispositivo>();
            public readonly IntPtr Hwnd;
            public PresentParams Pp;
            public Textura Fondo;          // el buffer de atras (lo que se presenta)
            public Superficie Destino;     // donde se dibuja ahora
            public bool Mezcla; public uint Origen = 2, DestinoMezcla = 1;
            public uint Fvf; public Vertices Flujo; public uint FlujoDesde, FlujoPaso;
            public readonly Etapa[] Etapas = { new Etapa(0), new Etapa(1) };
            public Dispositivo(IntPtr hwnd, PresentParams* pp) { Hwnd = hwnd; Pp = *pp; }
            public void Crea()
            {
                int w = (int)Pp.BackBufferWidth, h = (int)Pp.BackBufferHeight;
                System.Drawing.Rectangle r = System.Windows.Forms.Escritorio.RectEnPantalla(Hwnd);
                if (w <= 0) w = Math.Max(1, r.Width); if (h <= 0) h = Math.Max(1, r.Height);
                Pp.BackBufferWidth = (uint)w; Pp.BackBufferHeight = (uint)h;
                Fondo = NuevaTextura(w, h, true, true);
                Fondo.Sup = new Superficie(); Fondo.Sup.Tex = Fondo; Nuevo(vtSup, Fondo.Sup);
                Destino = Fondo.Sup;
                lock (Vivos) Vivos.Add(this);
                System.Windows.Forms.Escritorio.MarcaNativa(Hwnd, true);
            }
            public void Cierra()
            {
                lock (Vivos) Vivos.Remove(this);
                System.Windows.Forms.Escritorio.MarcaNativa(Hwnd, false);
                if (Fondo != null) { BorraGl(Fondo); if (Fondo.Sup != null && Fondo.Sup.Ptr != IntPtr.Zero) Olvida(Fondo.Sup); Fondo = null; }
            }
            public static void Suelta(Textura t)
            {
                t.Suelta = true;
                lock (Vivos) foreach (Dispositivo d in Vivos)
                    {
                        foreach (Etapa e in d.Etapas) if (e.Tex == t) e.Tex = null;
                        if (d.Destino != null && d.Destino.Tex == t) d.Destino = d.Fondo.Sup;
                    }
                BorraGl(t);
            }
        }
        internal sealed class Etapa
        {
            public uint ColorOp, ColorA1 = 2, ColorA2 = 1, AlphaOp, AlphaA1 = 2, AlphaA2 = 1, Indice;
            public uint DirU = 1, DirV = 1, Mag = 1, Min = 1;
            public Textura Tex;
            public Etapa(int i) { Indice = (uint)i; ColorOp = i == 0 ? 4u : 1u; AlphaOp = i == 0 ? 2u : 1u; }
        }

        [UnmanagedCallersOnly] private static uint MemoriaTexturas(IntPtr self) { return 256u * 1024 * 1024; }
        [UnmanagedCallersOnly]
        private static int GetDeviceCaps(IntPtr self, byte* caps)
        {
            new Span<byte>(caps, 304).Clear();
            *(uint*)(caps + 20) = 0x80000001u;   // intervalos de presentacion: inmediato y uno
            *(uint*)(caps + 44) = 0x1FFF;        // mezclas de origen
            int max = Gl.MaxTextura;
            *(uint*)(caps + 88) = (uint)max; *(uint*)(caps + 92) = (uint)max;
            *(uint*)(caps + 140) = 8;            // coordenadas de textura
            *(uint*)(caps + 144) = 0x00FFFFFF;   // operaciones de textura
            *(uint*)(caps + 152) = 2;            // texturas a la vez
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int Reset(IntPtr self, PresentParams* pp)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            try
            {
                int w = (int)pp->BackBufferWidth, h = (int)pp->BackBufferHeight;
                System.Drawing.Rectangle r = System.Windows.Forms.Escritorio.RectEnPantalla(d.Hwnd);
                if (w <= 0) w = Math.Max(1, r.Width); if (h <= 0) h = Math.Max(1, r.Height);
                d.Pp = *pp; d.Pp.BackBufferWidth = (uint)w; d.Pp.BackBufferHeight = (uint)h;
                if (d.Fondo.W != w || d.Fondo.H != h) { BorraGl(d.Fondo); d.Fondo.W = w; d.Fondo.H = h; }
                d.Destino = d.Fondo.Sup;
                return S_OK;
            }
            catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }
        [UnmanagedCallersOnly]
        private static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr e)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            try { Pantalla.Presenta(d); return S_OK; }
            catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }
        [UnmanagedCallersOnly]
        private static int CreateTexture(IntPtr self, uint w, uint h, uint niveles, uint uso, uint formato, uint pool, IntPtr* salida, IntPtr compartida)
        {
            *salida = IntPtr.Zero;
            if (w == 0 || h == 0 || w > (uint)Gl.MaxTextura || h > (uint)Gl.MaxTextura) return D3DERR_INVALIDCALL;
            if (formato != 21 && formato != 22) return D3DERR_INVALIDCALL;
            try
            {
                Textura t = NuevaTextura((int)w, (int)h, (uso & 1) != 0, formato == 22);
                *salida = Nuevo(vtTex, t);
                return S_OK;
            }
            catch (OutOfMemoryException) { return E_OUTOFMEMORY; }
            catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }
        private static Textura NuevaTextura(int w, int h, bool rt, bool xrgb)
        {
            Textura t = new Textura(); t.W = w; t.H = h; t.Rt = rt; t.Xrgb = xrgb;
            if (!rt) t.Mem = (IntPtr)NativeMemory.AllocZeroed((nuint)((long)w * h * 4));
            return t;
        }
        [UnmanagedCallersOnly]
        private static int CreateVertexBuffer(IntPtr self, uint largo, uint uso, uint fvf, uint pool, IntPtr* salida, IntPtr compartida)
        {
            Vertices v = new Vertices(); v.Largo = (int)largo; v.Mem = (IntPtr)NativeMemory.AllocZeroed(largo);
            *salida = Nuevo(vtVb, v);
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int SetRenderTarget(IntPtr self, uint i, IntPtr sup)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null || i != 0) return D3DERR_INVALIDCALL;
            Superficie s = sup == IntPtr.Zero ? null : De<Superficie>(sup);
            if (s == null || s.Tex == null || !s.Tex.Rt) return D3DERR_INVALIDCALL;
            d.Destino = s; return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int GetRenderTarget(IntPtr self, uint i, IntPtr* salida)
        {
            Dispositivo d = De<Dispositivo>(self); *salida = IntPtr.Zero;
            if (d == null || i != 0 || d.Destino == null) return D3DERR_INVALIDCALL;
            d.Destino.Refs++; *salida = d.Destino.Ptr; return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int Clear(IntPtr self, uint n, IntPtr rects, uint banderas, uint color, float z, uint stencil)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            if ((banderas & 1) == 0) return S_OK;
            try { Gl.Borra(d, color); return S_OK; } catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }
        [UnmanagedCallersOnly]
        private static int SetRenderState(IntPtr self, uint estado, uint valor)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            if (estado == 27) d.Mezcla = valor != 0; else if (estado == 19) d.Origen = valor; else if (estado == 20) d.DestinoMezcla = valor;
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int SetTexture(IntPtr self, uint etapa, IntPtr tex)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            if (etapa > 1) return S_OK;
            d.Etapas[etapa].Tex = tex == IntPtr.Zero ? null : De<Textura>(tex);
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int SetTextureStageState(IntPtr self, uint etapa, uint tipo, uint v)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            if (etapa > 1) return S_OK;
            Etapa e = d.Etapas[etapa];
            switch (tipo) { case 1: e.ColorOp = v; break; case 2: e.ColorA1 = v; break; case 3: e.ColorA2 = v; break; case 4: e.AlphaOp = v; break; case 5: e.AlphaA1 = v; break; case 6: e.AlphaA2 = v; break; case 11: e.Indice = v & 0xFFFF; break; }
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int SetSamplerState(IntPtr self, uint etapa, uint tipo, uint v)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL;
            if (etapa > 1) return S_OK;
            Etapa e = d.Etapas[etapa];
            switch (tipo) { case 1: e.DirU = v; break; case 2: e.DirV = v; break; case 5: e.Mag = v; break; case 6: e.Min = v; break; }
            return S_OK;
        }
        [UnmanagedCallersOnly] private static int ValidateDevice(IntPtr self, uint* pasadas) { *pasadas = 1; return S_OK; }
        [UnmanagedCallersOnly] private static int SetFVF(IntPtr self, uint fvf) { Dispositivo d = De<Dispositivo>(self); if (d == null) return D3DERR_INVALIDCALL; d.Fvf = fvf; return S_OK; }
        [UnmanagedCallersOnly]
        private static int SetStreamSource(IntPtr self, uint flujo, IntPtr vb, uint desde, uint paso)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null || flujo != 0) return D3DERR_INVALIDCALL;
            d.Flujo = vb == IntPtr.Zero ? null : De<Vertices>(vb); d.FlujoDesde = desde; d.FlujoPaso = paso;
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int DrawPrimitive(IntPtr self, uint tipo, uint primero, uint cuantos)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null || d.Flujo == null || d.FlujoPaso == 0) return D3DERR_INVALIDCALL;
            try
            {
                long desde = d.FlujoDesde + (long)primero * d.FlujoPaso;
                int n = Gl.Vertices(tipo, (int)cuantos);
                if (desde + (long)n * d.FlujoPaso > d.Flujo.Largo) return D3DERR_INVALIDCALL;
                Gl.Dibuja(d, tipo, (int)cuantos, (byte*)d.Flujo.Mem + desde, (int)d.FlujoPaso);
                return S_OK;
            }
            catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }
        [UnmanagedCallersOnly]
        private static int DrawPrimitiveUP(IntPtr self, uint tipo, uint cuantos, IntPtr datos, uint paso)
        {
            Dispositivo d = De<Dispositivo>(self); if (d == null || datos == IntPtr.Zero) return D3DERR_INVALIDCALL;
            try { Gl.Dibuja(d, tipo, (int)cuantos, (byte*)datos, (int)paso); return S_OK; }
            catch (Exception ex) { Apunta(ex); return E_FAIL; }
        }

        // ---------------- texturas, superficies y vertices ----------------
        [UnmanagedCallersOnly] private static void PreLoad(IntPtr self) { Textura t = De<Textura>(self); if (t != null) try { Gl.Sube(t); } catch (Exception ex) { Apunta(ex); } }
        [UnmanagedCallersOnly]
        private static int GetLevelDesc(IntPtr self, uint nivel, uint* desc)
        {
            Textura t = De<Textura>(self); if (t == null || nivel != 0) return D3DERR_INVALIDCALL;
            desc[0] = t.Xrgb ? 22u : 21u; desc[1] = 3; desc[2] = t.Rt ? 1u : 0u; desc[3] = t.Rt ? 0u : 1u; desc[4] = 0; desc[5] = 0;
            desc[6] = (uint)t.W; desc[7] = (uint)t.H;
            return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int GetSurfaceLevel(IntPtr self, uint nivel, IntPtr* salida)
        {
            Textura t = De<Textura>(self); *salida = IntPtr.Zero;
            if (t == null || nivel != 0) return D3DERR_INVALIDCALL;
            if (t.Sup == null || t.Sup.Ptr == IntPtr.Zero) { t.Sup = new Superficie(); t.Sup.Tex = t; Nuevo(vtSup, t.Sup); }
            else t.Sup.Refs++;
            *salida = t.Sup.Ptr; return S_OK;
        }
        [UnmanagedCallersOnly]
        private static int LockRect(IntPtr self, uint nivel, LockedRect* lr, int* rect, uint banderas)
        {
            Textura t = De<Textura>(self);
            if (t == null || nivel != 0 || t.Mem == IntPtr.Zero) return D3DERR_INVALIDCALL;
            int x0 = 0, y0 = 0, x1 = t.W, y1 = t.H;
            if (rect != null) { x0 = rect[0]; y0 = rect[1]; x1 = rect[2]; y1 = rect[3]; }
            if (x0 < 0 || y0 < 0 || x1 > t.W || y1 > t.H || x0 >= x1 || y0 >= y1) return D3DERR_INVALIDCALL;
            lr->Pitch = t.W * 4;
            lr->Bits = t.Mem + (nint)(((long)y0 * t.W + x0) * 4);
            if ((banderas & 0x10) == 0)   // D3DLOCK_READONLY no ensucia
            {
                if (y0 < t.SuciaY0) t.SuciaY0 = y0;
                if (y1 > t.SuciaY1) t.SuciaY1 = y1;
            }
            return S_OK;
        }
        [UnmanagedCallersOnly] private static int UnlockRect(IntPtr self, uint nivel) { return S_OK; }
        [UnmanagedCallersOnly]
        private static int LockVb(IntPtr self, uint desde, uint tam, IntPtr* datos, uint banderas)
        {
            Vertices v = De<Vertices>(self); if (v == null || desde > (uint)v.Largo) return D3DERR_INVALIDCALL;
            *datos = v.Mem + (int)desde; return S_OK;
        }

        private static void BorraGl(Textura t)
        {
            if (t.Gl == 0 && t.Fbo == 0) return;
            Pantalla.BorraLuego(t.Gl, t.Fbo);
            t.Gl = 0; t.Fbo = 0; t.DirU = t.DirV = t.Mag = t.Min = uint.MaxValue;
            t.SuciaY0 = 0; t.SuciaY1 = t.H;
        }
    }
}

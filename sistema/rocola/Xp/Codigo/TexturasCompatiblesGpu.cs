#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Rocola
{
    // Un panel que supera el limite del driver mantiene TODOS sus pixeles. Las teselas
    // tienen un texel vecino de margen: el filtrado bilineal no deja costuras.
    internal static class TexturasDivididasGpu
    {
        internal sealed class Tile
        { public Rectangle Core, Source; public IntPtr Tex; public int W, H; }
        internal sealed class Surface
        { public D3D9Device Owner; public int W, H; public Tile[] Tiles; }
        private static readonly object candado = new object();
        private static readonly Dictionary<IntPtr, Surface> superficies = new Dictionary<IntPtr, Surface>();
        private static volatile int cantidad;
        internal static Surface Busca(IntPtr textura)
        {
            if (textura == IntPtr.Zero || cantidad == 0) return null;
            lock (candado) { Surface s; return superficies.TryGetValue(textura, out s) ? s : null; }
        }
        internal static IntPtr Registra(Surface s)
        {
            IntPtr id = Marshal.AllocHGlobal(1);
            try { lock (candado) { superficies.Add(id, s); cantidad = superficies.Count; } return id; }
            catch { Marshal.FreeHGlobal(id); throw; }
        }
        internal static bool Libera(IntPtr textura)
        {
            if (textura == IntPtr.Zero || cantidad == 0) return false;
            Surface s;
            lock (candado)
            {
                if (!superficies.TryGetValue(textura, out s)) return false;
                superficies.Remove(textura); cantidad = superficies.Count;
            }
            Marshal.FreeHGlobal(textura);
            foreach (Tile t in s.Tiles) if (t != null && t.Tex != IntPtr.Zero) D3D9.Release(t.Tex);
            return true;
        }
    }

    // No posee una referencia extra ni reserva VRAM: solo comparte las referencias
    // que ya necesitan la pared y los cubos. Nunca recibe bitmaps de interfaz mutables.
    internal static class PortadasCompartidasGpu
    {
        private sealed class Entry
        { public D3D9Device Owner; public Bitmap Bmp; public IntPtr Tex; public float U, V; public int Owners; }
        private static readonly object candado = new object();
        private static readonly Dictionary<IntPtr, Entry> referencias = new Dictionary<IntPtr, Entry>();
        private static readonly Dictionary<D3D9Device, Dictionary<Bitmap, Entry>> dispositivos = new Dictionary<D3D9Device, Dictionary<Bitmap, Entry>>();
        private static volatile int cantidad;
        internal static IntPtr Toma(D3D9Device owner, Bitmap bmp, out float u, out float v)
        {
            u = v = 1;
            if (bmp == null) return IntPtr.Zero;
            lock (candado)
            {
                Dictionary<Bitmap, Entry> mapa;
                Entry e;
                if (dispositivos.TryGetValue(owner, out mapa) && mapa.TryGetValue(bmp, out e))
                {
                    // Una textura dividida contiene referencias normales, pero su identificador
                    // logico no es COM. Las portadas normales caben; si no, no se comparten.
                    try { D3D9.EnGl.AddRef(e.Tex); }
                    catch { return IntPtr.Zero; }
                    e.Owners++; u = e.U; v = e.V; return e.Tex;
                }
                IntPtr tex = owner.CreateTexturePow2(bmp, out u, out v);
                if (tex == IntPtr.Zero || TexturasDivididasGpu.Busca(tex) != null) return tex;
                e = new Entry { Owner = owner, Bmp = bmp, Tex = tex, U = u, V = v, Owners = 1 };
                if (mapa == null) { mapa = new Dictionary<Bitmap, Entry>(); dispositivos.Add(owner, mapa); }
                mapa.Add(bmp, e); referencias.Add(tex, e); cantidad = referencias.Count;
                return tex;
            }
        }
        internal static void AlSoltar(IntPtr tex)
        {
            if (cantidad == 0) return;
            lock (candado)
            {
                Entry e;
                if (!referencias.TryGetValue(tex, out e) || --e.Owners > 0) return;
                referencias.Remove(tex);
                Dictionary<Bitmap, Entry> mapa = dispositivos[e.Owner]; mapa.Remove(e.Bmp);
                if (mapa.Count == 0) dispositivos.Remove(e.Owner);
                cantidad = referencias.Count;
            }
        }
    }

    internal sealed partial class D3D9Device
    {
        // D3DCAPS9: prefijo documentado, dos DWORD en 88/92. El buffer completo
        // tiene holgura para que GetDeviceCaps escriba el resto de la estructura.
        [StructLayout(LayoutKind.Explicit, Size = 512)]
        private struct CapsTextura
        {
            [FieldOffset(88)] public uint W; [FieldOffset(92)] public uint H;
            // Lo que piden los ahorros de Performance (PerformanceGpu.cs), con los desplazamientos de D3DCAPS9.
            [FieldOffset(20)] public uint Intervalos; [FieldOffset(44)] public uint MezclaOrigen;
            [FieldOffset(140)] public uint Fvf; [FieldOffset(144)] public uint OperacionesTextura;
            [FieldOffset(152)] public uint TexturasALaVez;
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CapsDel(IntPtr self, ref CapsTextura caps);
        private int limiteTexturaW, limiteTexturaH;
        private int LimiteTexturaW { get { return limiteTexturaW > 2 ? limiteTexturaW : 2048; } }
        private int LimiteTexturaH { get { return limiteTexturaH > 2 ? limiteTexturaH : 2048; } }
        private IntPtr texturaLogica;
        private D3D9.Vtx[] recortaA, recortaB, recortaDibujo;
        private void LeeLimitesTexturas()
        {
            try
            {
                CapsTextura caps = new CapsTextura();
                if (GetDeviceCaps(dev, ref caps) >= 0)   // Linux: D3D9Device.Gl.cs
                {
                    limiteTexturaW = PotenciaInferior(caps.W); limiteTexturaH = PotenciaInferior(caps.H);
                    LeeCapacidades(caps.Intervalos, caps.MezclaOrigen, caps.Fvf, caps.OperacionesTextura, caps.TexturasALaVez);
                }
            }
            catch { } // limite conservador si el driver no expone las capacidades
        }
        private static int PotenciaInferior(uint n)
        { int p = 1; while ((uint)p <= n / 2 && p < 0x40000000) p <<= 1; return n >= 4 ? p : 0; }
        internal IntPtr CreaPortadaCompartida(Bitmap bmp, out float u, out float v)
        { if (CintaTarjeta.Graba && CintaTarjeta.Fuera) return CintaTarjeta.CreaPortadaCompartida(this, bmp, out u, out v); return PortadasCompartidasGpu.Toma(this, bmp, out u, out v); }   // (pruebas: ROCOLA_CINTA, Nativo/CintaTarjeta.cs)

        private static void RellenaBorde(D3D9.LockedRect lr, int w, int h, int tw, int th)
        {
            if (tw > w)
                for (int y = 0; y < h; y++)
                {
                    IntPtr inicio = new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch + (long)(w - 1) * 4);
                    int hecho = 1, faltan = tw - w;
                    while (faltan > 0)
                    {
                        int n = Math.Min(hecho, faltan);
                        D3D9.CopyMemory(new IntPtr(inicio.ToInt64() + (long)hecho * 4), inicio, (UIntPtr)(uint)(n * 4));
                        hecho += n; faltan -= n;
                    }
                }
            IntPtr ultima = new IntPtr(lr.Bits.ToInt64() + (long)(h - 1) * lr.Pitch);
            for (int y = h; y < th; y++)
                D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), ultima, (UIntPtr)(uint)(tw * 4));
        }
        private static bool ActualizaRegionBitmap(IntPtr tex, Bitmap bmp, Rectangle origen, Point destino, int w, int h)
        {
            D3D9.UnlockRectDel unlock = null;
            BitmapData bd = null; bool locked = false, ok = false;
            try
            {
                int right = destino.X + origen.Width, bottom = destino.Y + origen.Height;
                int r = right == w ? Pow2(w) : right, b = bottom == h ? Pow2(h) : bottom;
                D3D9.RectI region = new D3D9.RectI { Left = destino.X, Top = destino.Y, Right = r, Bottom = b };
                D3D9.LockedRect lr;
                unlock = D3D9.EnGl.UnlockRect;
                if (D3D9.EnGl.LockRect(tex, 0, out lr, ref region, 0) < 0) return false;
                locked = true;
                bd = bmp.LockBits(origen, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int bytes = checked(origen.Width * 4);
                if (lr.Bits == IntPtr.Zero || lr.Pitch < (r - destino.X) * 4 || bd.Scan0 == IntPtr.Zero || Math.Abs((long)bd.Stride) < bytes) return false;
                for (int y = 0; y < origen.Height; y++)
                    D3D9.CopyMemory(new IntPtr(lr.Bits.ToInt64() + (long)y * lr.Pitch), new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), (UIntPtr)(uint)bytes);
                RellenaBorde(lr, origen.Width, origen.Height, r - destino.X, b - destino.Y);
                ok = true;
            }
            catch { ok = false; }
            finally
            {
                if (bd != null) try { bmp.UnlockBits(bd); } catch { ok = false; }
                if (locked && unlock != null) try { if (unlock(tex, 0) < 0) ok = false; } catch { ok = false; }
            }
            return ok;
        }
        private IntPtr CreaTexturaDividida(Bitmap bmp, out float u, out float v)
        {
            u = v = 1;
            int pasoX = LimiteTexturaW - 2, pasoY = LimiteTexturaH - 2;
            int w = bmp.Width, h = bmp.Height;
            long nx = ((long)w + pasoX - 1) / pasoX, ny = ((long)h + pasoY - 1) / pasoY;
            if (nx * ny > 256) return IntPtr.Zero; // no transformar una imagen arbitraria en recursos sin limite
            TexturasDivididasGpu.Surface s = new TexturasDivididasGpu.Surface { Owner = this, W = w, H = h,
                Tiles = new TexturasDivididasGpu.Tile[(int)(nx * ny)] };
            bool ok = false;
            try
            {
                int i = 0;
                for (int y = 0; y < h; y += pasoY) for (int x = 0; x < w; x += pasoX)
                {
                    Rectangle core = new Rectangle(x, y, Math.Min(pasoX, w - x), Math.Min(pasoY, h - y));
                    Rectangle source = core; source.Inflate(1, 1); source.Intersect(new Rectangle(0, 0, w, h));
                    TexturasDivididasGpu.Tile t = new TexturasDivididasGpu.Tile { Core = core, Source = source, W = Pow2(source.Width), H = Pow2(source.Height) };
                    s.Tiles[i++] = t;
                    if (createTex(dev, (uint)t.W, (uint)t.H, 1, 0, D3D9.FMT_A8R8G8B8, D3D9.POOL_MANAGED, out t.Tex, IntPtr.Zero) < 0 || t.Tex == IntPtr.Zero ||
                        !ActualizaRegionBitmap(t.Tex, bmp, source, Point.Empty, source.Width, source.Height)) return IntPtr.Zero;
                }
                IntPtr id = TexturasDivididasGpu.Registra(s); ok = true; return id;
            }
            catch { return IntPtr.Zero; }
            finally { if (!ok) foreach (TexturasDivididasGpu.Tile t in s.Tiles) if (t != null && t.Tex != IntPtr.Zero) D3D9.Release(t.Tex); }
        }
        private bool ActualizaDividida(TexturasDivididasGpu.Surface s, Bitmap bmp, Rectangle area)
        {
            if (s.Owner != this || bmp.Width != s.W || bmp.Height != s.H) return false;
            foreach (TexturasDivididasGpu.Tile t in s.Tiles)
            {
                Rectangle region = Rectangle.Intersect(area, t.Source);
                if (region.Width > 0 && region.Height > 0 && !ActualizaRegionBitmap(t.Tex, bmp, region,
                    new Point(region.X - t.Source.X, region.Y - t.Source.Y), t.Source.Width, t.Source.Height)) return false;
            }
            return true;
        }

        // Recorte de triangulos por UV, sin asignaciones por cuadro. El quad mantiene
        // posicion, opacidad y colores originales incluso mientras se anima.
        private void DibujaDividida(TexturasDivididasGpu.Surface s, D3D9.Vtx[] datos, int tris)
        {
            if (s.Owner != this || dev == IntPtr.Zero || lost || datos == null || tris <= 0 || tris > datos.Length / 3) return;
            if (recortaA == null) { recortaA = new D3D9.Vtx[8]; recortaB = new D3D9.Vtx[8]; recortaDibujo = new D3D9.Vtx[18]; }
            for (int tr = 0; tr < tris; tr++) foreach (TexturasDivididasGpu.Tile tile in s.Tiles)
            {
                recortaA[0] = datos[tr * 3]; recortaA[1] = datos[tr * 3 + 1]; recortaA[2] = datos[tr * 3 + 2];
                int n = Recorta(recortaA, 3, recortaB, true, (float)tile.Core.Left / s.W, true);
                n = Recorta(recortaB, n, recortaA, true, (float)tile.Core.Right / s.W, false);
                n = Recorta(recortaA, n, recortaB, false, (float)tile.Core.Top / s.H, true);
                n = Recorta(recortaB, n, recortaA, false, (float)tile.Core.Bottom / s.H, false);
                if (n < 3) continue;
                int vtx = 0;
                for (int k = 1; k < n - 1; k++)
                { recortaDibujo[vtx++] = UvTesela(recortaA[0], s, tile); recortaDibujo[vtx++] = UvTesela(recortaA[k], s, tile); recortaDibujo[vtx++] = UvTesela(recortaA[k + 1], s, tile); }
                PonTexturaFisica(tile.Tex); DrawFisico(recortaDibujo, vtx / 3);
            }
        }
        private static D3D9.Vtx UvTesela(D3D9.Vtx q, TexturasDivididasGpu.Surface s, TexturasDivididasGpu.Tile t)
        { q.U = (q.U * s.W - t.Source.X) / t.W; q.V = (q.V * s.H - t.Source.Y) / t.H; return q; }
        private static int Recorta(D3D9.Vtx[] from, int n, D3D9.Vtx[] to, bool horizontal, float edge, bool greater)
        {
            if (n == 0) return 0;
            int count = 0; D3D9.Vtx prev = from[n - 1];
            float p = horizontal ? prev.U : prev.V; bool inside = greater ? p >= edge : p <= edge;
            for (int i = 0; i < n; i++)
            {
                D3D9.Vtx cur = from[i]; float c = horizontal ? cur.U : cur.V; bool ci = greater ? c >= edge : c <= edge;
                if (inside != ci)
                {
                    D3D9.Vtx cut = Entre(prev, cur, (edge - p) / (c - p));
                    if (horizontal) cut.U = edge; else cut.V = edge;
                    to[count++] = cut;
                }
                if (ci) to[count++] = cur;
                prev = cur; p = c; inside = ci;
            }
            return count;
        }
        private static D3D9.Vtx Entre(D3D9.Vtx a, D3D9.Vtx b, float t)
        {
            D3D9.Vtx q = new D3D9.Vtx { X = a.X + (b.X - a.X) * t, Y = a.Y + (b.Y - a.Y) * t,
                Z = a.Z + (b.Z - a.Z) * t, Rhw = a.Rhw + (b.Rhw - a.Rhw) * t, U = a.U + (b.U - a.U) * t, V = a.V + (b.V - a.V) * t };
            uint color = 0;
            for (int shift = 0; shift < 32; shift += 8)
            { int x = (int)((a.Color >> shift) & 255), y = (int)((b.Color >> shift) & 255); color |= (uint)Math.Max(0, Math.Min(255, (int)Math.Round(x + (y - x) * t))) << shift; }
            q.Color = color; return q;
        }
    }
}
#endif

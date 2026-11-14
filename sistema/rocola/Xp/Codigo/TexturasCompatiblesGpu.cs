#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la subida a la tarjeta lee los puntos de un SKBitmap (Nativo/Pixeles.cs), no de un Bitmap de GDI+
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
        { public DispositivoTarjeta Owner; public int W, H; public Tile[] Tiles; }
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
            foreach (Tile t in s.Tiles) if (t != null && t.Tex != IntPtr.Zero) Tarjeta.Suelta(t.Tex);
            return true;
        }
    }

    // No posee una referencia extra ni reserva VRAM: solo comparte las referencias
    // que ya necesitan la pared y los cubos. Nunca recibe bitmaps de interfaz mutables.
    internal static class PortadasCompartidasGpu
    {
        private sealed class Entry
        { public DispositivoTarjeta Owner; public SKBitmap Bmp; public IntPtr Tex; public float U, V; public int Owners; }   // EN LINUX: la portada es un SKBitmap (la misma identidad: un mapa por portada)
        private static readonly object candado = new object();
        private static readonly Dictionary<IntPtr, Entry> referencias = new Dictionary<IntPtr, Entry>();
        private static readonly Dictionary<DispositivoTarjeta, Dictionary<SKBitmap, Entry>> dispositivos = new Dictionary<DispositivoTarjeta, Dictionary<SKBitmap, Entry>>();   // EN LINUX: por SKBitmap (por identidad, como el Bitmap en XP)
        private static volatile int cantidad;
        internal static IntPtr Toma(DispositivoTarjeta owner, SKBitmap bmp, out float u, out float v)   // EN LINUX: la portada es un SKBitmap
        {
            u = v = 1;
            if (bmp == null) return IntPtr.Zero;
            lock (candado)
            {
                Dictionary<SKBitmap, Entry> mapa;   // EN LINUX: por SKBitmap
                Entry e;
                if (dispositivos.TryGetValue(owner, out mapa) && mapa.TryGetValue(bmp, out e))
                {
                    // Una textura dividida contiene referencias normales, pero su identificador
                    // logico no es COM. Las portadas normales caben; si no, no se comparten.
                    // EN LINUX: sin AddRef: la textura vive mientras la entrada tenga duenos (AlSoltar, desde Tarjeta.Suelta)
                    e.Owners++; u = e.U; v = e.V; return e.Tex;
                }
                IntPtr tex = owner.CreateTexturePow2(bmp, out u, out v);
                if (tex == IntPtr.Zero || TexturasDivididasGpu.Busca(tex) != null) return tex;
                e = new Entry { Owner = owner, Bmp = bmp, Tex = tex, U = u, V = v, Owners = 1 };
                if (mapa == null) { mapa = new Dictionary<SKBitmap, Entry>(); dispositivos.Add(owner, mapa); }   // EN LINUX: por SKBitmap
                mapa.Add(bmp, e); referencias.Add(tex, e); cantidad = referencias.Count;
                return tex;
            }
        }
        internal static bool AlSoltar(IntPtr tex)   // EN LINUX: dice si aun le quedan duenos (la cuenta de referencias la llevaba Direct3D)
        {
            if (cantidad == 0) return false;   // EN LINUX
            lock (candado)
            {
                Entry e;
                if (!referencias.TryGetValue(tex, out e)) return false;   // EN LINUX
                if (--e.Owners > 0) return true;   // EN LINUX
                referencias.Remove(tex);
                Dictionary<SKBitmap, Entry> mapa = dispositivos[e.Owner]; mapa.Remove(e.Bmp);   // EN LINUX: por SKBitmap
                if (mapa.Count == 0) dispositivos.Remove(e.Owner);
                cantidad = referencias.Count;
            }
            return false;   // EN LINUX
        }
    }

    internal sealed partial class DispositivoTarjeta
    {
        // EN LINUX: (sin D3DCAPS9 ni GetDeviceCaps: lo que se preguntaba se lee de OpenGL en LeeLimitesTexturas)
        private int limiteTexturaW, limiteTexturaH;
        private int LimiteTexturaW { get { return limiteTexturaW > 2 ? limiteTexturaW : 2048; } }
        private int LimiteTexturaH { get { return limiteTexturaH > 2 ? limiteTexturaH : 2048; } }
        private IntPtr texturaLogica;
        private Tarjeta.Vtx[] recortaA, recortaB, recortaDibujo;
        private void LeeLimitesTexturas()
        {
            try
            {
                uint max = (uint)TexturasGl.Maxima();   // EN LINUX: GetDeviceCaps: MaxTextureWidth y MaxTextureHeight = GL_MAX_TEXTURE_SIZE
                if (max > 0)   // EN LINUX
                {
                    limiteTexturaW = PotenciaInferior(max); limiteTexturaH = PotenciaInferior(max);   // EN LINUX
                    // EN LINUX: lo que pedian los ahorros (LeeCapacidades, con los bits de D3DCAPS9): presentar sin esperar al barrido (el cambio
                    // de pagina de KMS sin esperar) y dos texturas en una pasada (OpenGL ES 2 tiene al menos 8 unidades y el programa fijo
                    // «dos texturas» hace la combinacion: Nativo/SombreadoresTarjeta.cs)
                    CapsSinEspera = true; CapsDosTexturas = true;
                }
            }
            catch { } // limite conservador si el driver no expone las capacidades
        }
        private static int PotenciaInferior(uint n)
        { int p = 1; while ((uint)p <= n / 2 && p < 0x40000000) p <<= 1; return n >= 4 ? p : 0; }
        internal IntPtr CreaPortadaCompartida(SKBitmap bmp, out float u, out float v)   // EN LINUX: la portada es un SKBitmap
        { if (CintaTarjeta.Graba && CintaTarjeta.Fuera) return CintaTarjeta.CreaPortadaCompartida(this, bmp, out u, out v); return PortadasCompartidasGpu.Toma(this, bmp, out u, out v); }   // (pruebas: ROCOLA_CINTA, Nativo/CintaTarjeta.cs)

        private static unsafe void RellenaBorde(IntPtr bits, int pitch, int w, int h, int tw, int th)   // EN LINUX: lo bloqueado (antes D3DLOCKED_RECT); unsafe (las copias)
        {
            if (tw > w)
                for (int y = 0; y < h; y++)
                {
                    IntPtr inicio = new IntPtr(bits.ToInt64() + (long)y * pitch + (long)(w - 1) * 4);   // EN LINUX: (lr.Bits, lr.Pitch)
                    int hecho = 1, faltan = tw - w;
                    while (faltan > 0)
                    {
                        int n = Math.Min(hecho, faltan);
                        Buffer.MemoryCopy((void*)inicio, (void*)(inicio.ToInt64() + (long)hecho * 4), n * 4, n * 4);   // EN LINUX: CopyMemory (RtlMoveMemory) = Buffer.MemoryCopy
                        hecho += n; faltan -= n;
                    }
                }
            IntPtr ultima = new IntPtr(bits.ToInt64() + (long)(h - 1) * pitch);   // EN LINUX: (lr.Bits, lr.Pitch)
            for (int y = h; y < th; y++)
                Buffer.MemoryCopy((void*)ultima, (void*)(bits.ToInt64() + (long)y * pitch), tw * 4, tw * 4);   // EN LINUX: CopyMemory
        }
        private static unsafe bool ActualizaRegionBitmap(IntPtr tex, SKBitmap bmp, Rectangle origen, Point destino, int w, int h)   // EN LINUX: unsafe (las copias); el mapa es un SKBitmap
        {
            IntPtr bits = IntPtr.Zero; int pitch = 0, lx = 0, ly = 0, lw = 0, lh = 0, tw0, th0; bool propia = false;   // EN LINUX: lo bloqueado (antes D3DLOCKED_RECT)
            bool locked = false, ok = false;   // EN LINUX: (sin BitmapData: los puntos del trozo se leen del SKBitmap directo a lo bloqueado, abajo)
            try
            {
                int right = destino.X + origen.Width, bottom = destino.Y + origen.Height;
                int r = right == w ? Pow2(w) : right, b = bottom == h ? Pow2(h) : bottom;
                lx = destino.X; ly = destino.Y; lw = r - destino.X; lh = b - destino.Y;   // EN LINUX: la zona (antes D3DRECT)
                // EN LINUX: LockRect de la zona = una memoria aparte de su medida (Nativo/TexturasGl.cs), que se sube al soltarla; como Direct3D,
                // no si la zona no cabe en la textura
                if (!TexturasGl.Medida((uint)tex, out tw0, out th0) || lx < 0 || ly < 0 || lw <= 0 || lh <= 0 || lx + lw > tw0 || ly + lh > th0) return false;
                bits = TexturasGl.Memoria((long)lw * lh * 4, out propia); pitch = lw * 4;   // EN LINUX
                locked = true;
                // EN LINUX: bd = bmp.LockBits(origen, ReadOnly, Format32bppArgb): la lectura va abajo, con la copia (Pixeles.CopiaSinPremultiplicar)
                int bytes = checked(origen.Width * 4);
                if (bits == IntPtr.Zero || pitch < (r - destino.X) * 4 || bmp.GetPixels() == IntPtr.Zero || Math.Abs((long)bmp.RowBytes) < bytes) return false;   // EN LINUX: (lr.Bits, lr.Pitch); bd.Scan0 y bd.Stride = los puntos del SKBitmap y su paso
                Pixeles.CopiaSinPremultiplicar(bmp, origen, bits, pitch);   // EN LINUX: LockBits(origen, ReadOnly, Format32bppArgb) y la copia fila a fila a lo bloqueado, en una pasada: los puntos del trozo sin premultiplicar (la tabla truncada de la capa de la referencia, Nativo/Pixeles.cs) directo a lo bloqueado, con su paso; los mismos bytes, sin la copia aparte de GDI+
                // EN LINUX: (en XP: for (int y = 0; y < origen.Height; y++) D3D9.CopyMemory(lr.Bits + y * lr.Pitch, bd.Scan0 + y * bd.Stride, bytes))
                RellenaBorde(bits, pitch, origen.Width, origen.Height, r - destino.X, b - destino.Y);   // EN LINUX: (lr)
                ok = true;
            }
            catch { ok = false; }
            finally
            {
                // EN LINUX: (sin UnlockBits: los puntos se leyeron directo del SKBitmap y no queda nada de GDI+ que soltar; en XP: if (bd != null) try { bmp.UnlockBits(bd); } catch { ok = false; })
                if (locked) { try { if (!TexturasGl.Sube((uint)tex, lx, ly, lw, lh, bits)) ok = false; else if (VolcadoTexturas.Vuelca) VolcadoTexturas.Apunta((uint)tex, lx, ly, lw, lh, bits); } catch { ok = false; } TexturasGl.SueltaMemoria(bits, propia); }   // EN LINUX: UnlockRect = subir la zona (glTexSubImage2D) y soltar la memoria   (pruebas: ROCOLA_VOLCADO_TEXTURAS, Nativo/VolcadoTexturas.cs)
            }
            return ok;
        }
        private IntPtr CreaTexturaDividida(SKBitmap bmp, out float u, out float v)   // EN LINUX: el mapa es un SKBitmap
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
                    if ((t.Tex = (IntPtr)TexturasGl.Nueva(t.W, t.H)) == IntPtr.Zero ||   // EN LINUX: CreateTexture (A8R8G8B8, MANAGED) = glTexImage2D
                        !ActualizaRegionBitmap(t.Tex, bmp, source, Point.Empty, source.Width, source.Height)) return IntPtr.Zero;
                }
                IntPtr id = TexturasDivididasGpu.Registra(s); ok = true; return id;
            }
            catch { return IntPtr.Zero; }
            finally { if (!ok) foreach (TexturasDivididasGpu.Tile t in s.Tiles) if (t != null && t.Tex != IntPtr.Zero) Tarjeta.Suelta(t.Tex); }
        }
        private bool ActualizaDividida(TexturasDivididasGpu.Surface s, SKBitmap bmp, Rectangle area)   // EN LINUX: el mapa es un SKBitmap
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
        private void DibujaDividida(TexturasDivididasGpu.Surface s, Tarjeta.Vtx[] datos, int tris)
        {
            if (s.Owner != this || dev == IntPtr.Zero || lost || datos == null || tris <= 0 || tris > datos.Length / 3) return;
            if (recortaA == null) { recortaA = new Tarjeta.Vtx[8]; recortaB = new Tarjeta.Vtx[8]; recortaDibujo = new Tarjeta.Vtx[18]; }
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
        private static Tarjeta.Vtx UvTesela(Tarjeta.Vtx q, TexturasDivididasGpu.Surface s, TexturasDivididasGpu.Tile t)
        { q.U = (q.U * s.W - t.Source.X) / t.W; q.V = (q.V * s.H - t.Source.Y) / t.H; return q; }
        private static int Recorta(Tarjeta.Vtx[] from, int n, Tarjeta.Vtx[] to, bool horizontal, float edge, bool greater)
        {
            if (n == 0) return 0;
            int count = 0; Tarjeta.Vtx prev = from[n - 1];
            float p = horizontal ? prev.U : prev.V; bool inside = greater ? p >= edge : p <= edge;
            for (int i = 0; i < n; i++)
            {
                Tarjeta.Vtx cur = from[i]; float c = horizontal ? cur.U : cur.V; bool ci = greater ? c >= edge : c <= edge;
                if (inside != ci)
                {
                    Tarjeta.Vtx cut = Entre(prev, cur, (edge - p) / (c - p));
                    if (horizontal) cut.U = edge; else cut.V = edge;
                    to[count++] = cut;
                }
                if (ci) to[count++] = cur;
                prev = cur; p = c; inside = ci;
            }
            return count;
        }
        private static Tarjeta.Vtx Entre(Tarjeta.Vtx a, Tarjeta.Vtx b, float t)
        {
            Tarjeta.Vtx q = new Tarjeta.Vtx { X = a.X + (b.X - a.X) * t, Y = a.Y + (b.Y - a.Y) * t,
                Z = a.Z + (b.Z - a.Z) * t, Rhw = a.Rhw + (b.Rhw - a.Rhw) * t, U = a.U + (b.U - a.U) * t, V = a.V + (b.V - a.V) * t };
            uint color = 0;
            for (int shift = 0; shift < 32; shift += 8)
            { int x = (int)((a.Color >> shift) & 255), y = (int)((b.Color >> shift) & 255); color |= (uint)Math.Max(0, Math.Min(255, (int)Math.Round(x + (y - x) * t))) << shift; }
            q.Color = color; return q;
        }
    }
}
#endif

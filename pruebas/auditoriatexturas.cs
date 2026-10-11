#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Rocola
{
    // Driver artificial con memoria inicial sucia, limites pequenos y referencias COM.
    // Se compila junto al codigo real; no necesita HAL ni modifica la instalacion.
    internal static class AuditoriaTexturas
    {
        private sealed class Texture
        { internal int W, H, Refs = 1; internal IntPtr Mem, Table; }
        private static readonly Dictionary<IntPtr, Texture> textures = new Dictionary<IntPtr, Texture>();
        private static readonly List<Delegate> callbacks = new List<Delegate>();
        private static readonly List<IntPtr> allocations = new List<IntPtr>();
        private static IntPtr bound;
        private static uint addressU, addressV;
        private static bool regional, expectWrap;
        private static int n, creates, failAt;
        private static int[] coverage;
        private static uint[] canvas;
        private static int canvasW, canvasH;
        private static void Check(bool ok, string label) { n++; if (!ok) throw new Exception(label); }
        private static void Put(object o, string name, object value) { o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value); }
        private static IntPtr Pointer(Delegate d) { callbacks.Add(d); return Marshal.GetFunctionPointerForDelegate(d); }
        private static void Slot(IntPtr table, int index, Delegate d) { Marshal.WriteIntPtr(table, index * IntPtr.Size, Pointer(d)); }
        private static uint Nothing(IntPtr self) { return 0; }
        private static uint AddRef(IntPtr self) { return (uint)++textures[self].Refs; }
        private static uint Release(IntPtr self)
        {
            Texture t = textures[self];
            if (--t.Refs == 0) { textures.Remove(self); Marshal.FreeHGlobal(t.Mem); return 0; }
            return (uint)t.Refs;
        }
        private static int Create(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
        {
            creates++;
            IntPtr table = Marshal.AllocHGlobal(64 * IntPtr.Size), obj = Marshal.AllocHGlobal(IntPtr.Size);
            allocations.Add(table); allocations.Add(obj);
            for (int i = 0; i < 64; i++) Slot(table, i, new D3D9.UintSelf(Nothing));
            Slot(table, 1, new D3D9.UintSelf(AddRef)); Slot(table, 2, new D3D9.UintSelf(Release));
            if (regional) Slot(table, 19, new D3D9.LockRegionDel(LockRegion)); else Slot(table, 19, new D3D9.LockRectDel(Lock));
            Slot(table, 20, new D3D9.UnlockRectDel(Unlock)); Marshal.WriteIntPtr(obj, table);
            Texture t = new Texture { W = (int)w, H = (int)h, Mem = Marshal.AllocHGlobal(checked((int)(w * h * 4))), Table = table };
            for (int i = 0; i < t.W * t.H; i++) Marshal.WriteInt32(t.Mem, i * 4, unchecked((int)0xFFFF00FF));
            textures.Add(obj, t); tex = obj;
            return failAt == creates ? -1 : 0; // incluso un driver que devuelve recurso al fallar se limpia
        }
        private static int Lock(IntPtr self, uint level, out D3D9.LockedRect result, IntPtr rect, uint flags)
        { Texture t = textures[self]; result = new D3D9.LockedRect { Pitch = t.W * 4, Bits = t.Mem }; return 0; }
        private static int LockRegion(IntPtr self, uint level, out D3D9.LockedRect result, ref D3D9.RectI r, uint flags)
        {
            Texture t = textures[self];
            Check(r.Left >= 0 && r.Top >= 0 && r.Right <= t.W && r.Bottom <= t.H, "bloqueo sale de la textura");
            result = new D3D9.LockedRect { Pitch = t.W * 4, Bits = new IntPtr(t.Mem.ToInt64() + (r.Top * t.W + r.Left) * 4) }; return 0;
        }
        private static int Unlock(IntPtr self, uint level) { return 0; }
        private static int Set2(IntPtr self, uint a, uint b) { return 0; }
        private static int Set3(IntPtr self, uint a, uint b, uint c) { return 0; }
        private static int Sampler(IntPtr self, uint stage, uint type, uint value)
        { if (type == D3D9.SAMP_ADDRESSU) addressU = value; if (type == D3D9.SAMP_ADDRESSV) addressV = value; return 0; }
        private static int Bind(IntPtr self, uint stage, IntPtr tex)
        { Check(tex == IntPtr.Zero || textures.ContainsKey(tex), "identificador virtual enviado al driver"); bound = tex; return 0; }
        private static int Draw(IntPtr self, uint type, uint triangles, IntPtr data, uint stride)
        {
            uint expected = expectWrap ? D3D9.ADDRESS_WRAP : D3D9.ADDRESS_CLAMP;
            Check(addressU == expected && addressV == expected, "direccionamiento incorrecto de portada/ruido");
            if (canvas == null) return 0;
            for (int tr = 0; tr < triangles; tr++)
            {
                D3D9.Vtx a = Vertex(data, tr * 3), b = Vertex(data, tr * 3 + 1), c = Vertex(data, tr * 3 + 2);
                double den = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
                if (Math.Abs(den) < 1e-9) continue;
                for (int y = 0; y < canvasH; y++) for (int x = 0; x < canvasW; x++)
                {
                    double px = x + .5, py = y + .5;
                    double wa = ((b.Y - c.Y) * (px - c.X) + (c.X - b.X) * (py - c.Y)) / den;
                    double wb = ((c.Y - a.Y) * (px - c.X) + (a.X - c.X) * (py - c.Y)) / den, wc = 1 - wa - wb;
                    if (wa < -1e-8 || wb < -1e-8 || wc < -1e-8) continue;
                    int pos = y * canvasW + x; coverage[pos]++;
                    canvas[pos] = Sample(textures[bound], wa * a.U + wb * b.U + wc * c.U, wa * a.V + wb * b.V + wc * c.V);
                }
            }
            return 0;
        }
        private static D3D9.Vtx Vertex(IntPtr data, int index)
        { return (D3D9.Vtx)Marshal.PtrToStructure(new IntPtr(data.ToInt64() + index * D3D9.VtxSize), typeof(D3D9.Vtx)); }
        private static uint Pixel(Texture t, int x, int y)
        { x = Math.Max(0, Math.Min(t.W - 1, x)); y = Math.Max(0, Math.Min(t.H - 1, y)); return unchecked((uint)Marshal.ReadInt32(t.Mem, (y * t.W + x) * 4)); }
        private static uint Sample(Texture t, double u, double v)
        {
            double px = u * t.W - .5, py = v * t.H - .5; int x = (int)Math.Floor(px), y = (int)Math.Floor(py);
            double fx = px - x, fy = py - y; uint q = 0;
            for (int shift = 0; shift < 32; shift += 8)
            {
                double top = ((Pixel(t, x, y) >> shift) & 255) * (1 - fx) + ((Pixel(t, x + 1, y) >> shift) & 255) * fx;
                double bottom = ((Pixel(t, x, y + 1) >> shift) & 255) * (1 - fx) + ((Pixel(t, x + 1, y + 1) >> shift) & 255) * fx;
                q |= (uint)Math.Round(top * (1 - fy) + bottom * fy) << shift;
            }
            return q;
        }
        private static D3D9Device Device(int limit)
        {
            D3D9Device d = (D3D9Device)FormatterServices.GetUninitializedObject(typeof(D3D9Device));
            IntPtr obj = Marshal.AllocHGlobal(IntPtr.Size), table = Marshal.AllocHGlobal(128 * IntPtr.Size); allocations.Add(obj); allocations.Add(table);
            for (int i = 0; i < 128; i++) Slot(table, i, new D3D9.UintSelf(Nothing)); Marshal.WriteIntPtr(obj, table);
            Put(d, "dev", obj); Put(d, "limiteTexturaW", limit); Put(d, "limiteTexturaH", limit); Put(d, "stTex", new IntPtr(-1));
            Put(d, "createTex", new D3D9.CreateTexDel(Create)); Put(d, "setRS", new D3D9.SetU2Del(Set2));
            Put(d, "setTSS", new D3D9.SetU3Del(Set3)); Put(d, "setSamp", new D3D9.SetU3Del(Sampler));
            Put(d, "setTex", new D3D9.SetTexDel(Bind)); Put(d, "drawUp", new D3D9.DrawUpDel(Draw)); Put(d, "verticesSinSoporte", true);
            return d;
        }
        private static D3D9.Vtx V(float x, float y, float u, float v)
        { return new D3D9.Vtx { X = x, Y = y, Rhw = 1, Color = 0xFFFFFFFFu, U = u, V = v }; }
        private static D3D9.Vtx[] Quad(float x, float y, float w, float h, float u, float v)
        { return new D3D9.Vtx[] { V(x,y,0,0),V(x+w,y,u,0),V(x+w,y+h,u,v),V(x,y,0,0),V(x+w,y+h,u,v),V(x,y+h,0,v) }; }
        private static void Edges(D3D9Device d, Bitmap bmp)
        {
            regional = false; float u, v; IntPtr tex = d.CreateTexturePow2(bmp, out u, out v); Check(tex != IntPtr.Zero, "crear POT normal");
            Texture t = textures[tex];
            for (int y = 0; y < t.H; y++) for (int x = 0; x < t.W; x++)
                Check(Pixel(t, x, y) == unchecked((uint)bmp.GetPixel(Math.Min(x,bmp.Width-1), Math.Min(y,bmp.Height-1)).ToArgb()), "padding sin inicializar");
            Slot(t.Table, 19, new D3D9.LockRegionDel(LockRegion)); bmp.SetPixel(bmp.Width-1, bmp.Height-1, Color.Cyan);
            Check(d.UpdateTextureRegion(tex, bmp, new Rectangle(bmp.Width-1,bmp.Height-1,1,1)), "actualizar esquina POT");
            for (int y = bmp.Height-1; y < t.H; y++) for (int x = bmp.Width-1; x < t.W; x++)
                Check(Pixel(t,x,y) == unchecked((uint)Color.Cyan.ToArgb()), "padding no sigue una actualizacion parcial");
            expectWrap=true; d.DrawQuadsWrap(tex,Quad(.3f,.7f,30,40,u,v),2); expectWrap=false;
            d.DrawQuads(tex, Quad(.3f,.7f,30,40,u,v), 2); D3D9.Release(tex);
        }
        private static void Shared(D3D9Device d, Bitmap b)
        {
            regional = false; float u, v; int antes = creates;
            IntPtr first = d.CreaPortadaCompartida(b, out u, out v), second = d.CreaPortadaCompartida(b, out u, out v);
            Check(first != IntPtr.Zero && first == second && creates == antes+1, "misma portada sube dos veces");
            Check(textures[first].Refs == 2, "segunda portada no tiene referencia propia");
            D3D9.Release(first); Check(textures.ContainsKey(second) && textures[second].Refs == 1, "soltar pared rompe el cubo");
            D3D9.Release(second); Check(!textures.ContainsKey(second), "ultima referencia no libera VRAM");
            Check((int)typeof(PortadasCompartidasGpu).GetField("cantidad", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null) == 0, "registro retiene el bitmap sin consumidores");
            D3D9Device other = Device(64); first = d.CreaPortadaCompartida(b, out u, out v); second = other.CreaPortadaCompartida(b, out u, out v);
            Check(first != second, "comparte textura entre dispositivos incompatibles"); D3D9.Release(first); D3D9.Release(second);
        }
        private static void Consumers(Bitmap a)
        {
            D3D9Device d=Device(64); regional=false;
            GpuStarfield stars=(GpuStarfield)FormatterServices.GetUninitializedObject(typeof(GpuStarfield)); Put(stars,"gpu",d);
            using(GpuPanel panel=new GpuPanel()) using(Bitmap b=new Bitmap(a.Width,a.Height,PixelFormat.Format32bppArgb))
            {
                Put(panel,"stars",stars);
                using(Graphics g=Graphics.FromImage(b)) g.Clear(Color.Lime);
                BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                MethodInfo prepare=typeof(GpuPanel).GetMethod("PrepareNativeCover",flags), cube=typeof(GpuPanel).GetMethod("EnsureImmutableCover",flags);
                object[] args={new GpuGridCell { Direct=true, FullCover=a },10,true,0f,0f}; int antes=creates;
                IntPtr wall=(IntPtr)prepare.Invoke(panel,args);
                object[] selected={IntPtr.Zero,null,0,0,1f,1f,a}; cube.Invoke(panel,selected);
                object[] mini={IntPtr.Zero,null,0,0,1f,1f,a}; cube.Invoke(panel,mini);
                Check(wall != IntPtr.Zero && wall == (IntPtr)selected[0] && wall == (IntPtr)mini[0] && creates == antes+1,
                    "pared, cubo elegido y mini-cubo no reutilizan la misma subida real");
                Check(textures[wall].Refs == 3,"los tres consumidores no tienen propiedad independiente");
                selected[6]=b; cube.Invoke(panel,selected);
                Check((IntPtr)selected[0] != wall && textures[wall].Refs == 2,"otra portada muta la textura de la pared");
                Check(Pixel(textures[wall],4,5) == unchecked((uint)a.GetPixel(4,5).ToArgb()),"cambio de cubo altera portada compartida");
                Put(panel,"selTexA",selected[0]); Put(panel,"miniTex",mini[0]);
                // Dispose real del panel tiene que soltar sus tres referencias, tambien la pared.
            }
            Check(textures.Count == 0,"cerrar panel deja referencias compartidas vivas");
        }
        private static void Tiles(Bitmap bmp)
        {
            D3D9Device d = Device(16); regional = true; float u,v;
            IntPtr tex = d.CreateTexturePow2(bmp, out u, out v); TexturasDivididasGpu.Surface s = TexturasDivididasGpu.Busca(tex);
            Check(tex != IntPtr.Zero && s != null && u == 1 && v == 1, "panel grande desaparece en GPU limitada");
            foreach (TexturasDivididasGpu.Tile t in s.Tiles)
            {
                Texture raw = textures[t.Tex]; Check(raw.W <= 16 && raw.H <= 16, "tesela supera capacidades");
                for (int y=0; y<raw.H; y++) for (int x=0; x<raw.W; x++)
                    Check(Pixel(raw,x,y) == unchecked((uint)bmp.GetPixel(t.Source.X+Math.Min(x,t.Source.Width-1),t.Source.Y+Math.Min(y,t.Source.Height-1)).ToArgb()), "tesela cambia pixel o margen");
            }
            // Cambiar el pixel de una union debe actualizar tanto el nucleo como el margen de la vecina.
            bmp.SetPixel(14,14,Color.Gold); Check(d.UpdateTextureRegion(tex,bmp,new Rectangle(14,14,1,1)), "actualizar union de teselas");
            foreach (TexturasDivididasGpu.Tile t in s.Tiles) if(t.Source.Contains(14,14))
                Check(Pixel(textures[t.Tex],14-t.Source.X,14-t.Source.Y) == unchecked((uint)Color.Gold.ToArgb()), "vecina conserva margen viejo");
            Check(!Device(16).UpdateTexturePow2(tex,bmp,out u,out v), "otro dispositivo actualiza un identificador ajeno");
            Raster(d,tex,bmp);
            int vivos = textures.Count; D3D9.Release(tex);
            Check(TexturasDivididasGpu.Busca(tex) == null && textures.Count == vivos-s.Tiles.Length, "no libera todas las teselas");
            int antes = textures.Count; failAt = creates+2;
            Check(d.CreateTexturePow2(bmp,out u,out v) == IntPtr.Zero, "fallo del driver publica panel incompleto");
            Check(textures.Count == antes, "fallo de creacion pierde texturas"); failAt=0;
        }
        private static void Raster(D3D9Device d, IntPtr tex, Bitmap bmp)
        {
            // Mismo panel animado a coordenadas fraccionarias, con y sin teselas.
            D3D9Device grande = Device(64); regional=false; float u,v;
            IntPtr original = grande.CreateTexturePow2(bmp,out u,out v);
            canvasW=60; canvasH=76; canvas=new uint[canvasW*canvasH]; coverage=new int[canvas.Length];
            grande.DrawQuads(original,Quad(2.37f,3.71f,51.73f,64.37f,u,v),2);
            uint[] refPixels=canvas; int[] refHits=coverage; canvas=new uint[canvas.Length]; coverage=new int[canvas.Length];
            d.DrawQuads(tex,Quad(2.37f,3.71f,51.73f,64.37f,1,1),2);
            for (int i=0; i<canvas.Length; i++)
            {
                Check(coverage[i] == refHits[i] && coverage[i] <= 1, "hueco o doble mezcla en costura");
                for (int shift=0; shift<32; shift+=8)
                    Check(Math.Abs((int)((canvas[i]>>shift)&255)-(int)((refPixels[i]>>shift)&255)) <= 1, "filtrado bilineal cambia aspecto al dividir");
            }
            canvas=null; coverage=null; D3D9.Release(original);
        }
        private static int Main()
        {
            try
            {
                using (Bitmap b=new Bitmap(29,37,PixelFormat.Format32bppArgb))
                {
                    for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)b.SetPixel(x,y,Color.FromArgb((x*11+y*7)%256,(x*3)%256,(y*5)%256,(x+y)*3));
                    D3D9Device d=Device(64); Edges(d,b); Shared(d,b); Consumers(b); Tiles(b);
                }
                Check(textures.Count == 0,"quedan texturas artificiales vivas");
                Console.WriteLine("PASS texturas GPU: "+n+" comprobaciones; padding, alfa, clamp, referencias, limites, uniones y fallos. Driver artificial; no FPS."); return 0;
            }
            catch(Exception ex){ Console.WriteLine("FALLO "+ex); return 1; }
            finally
            {
                foreach(Texture t in textures.Values) Marshal.FreeHGlobal(t.Mem); textures.Clear();
                foreach(IntPtr p in allocations)Marshal.FreeHGlobal(p);
                GC.KeepAlive(callbacks);
            }
        }
    }
}
#endif

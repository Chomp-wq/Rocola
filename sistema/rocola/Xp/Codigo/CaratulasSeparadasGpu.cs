#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Dibujo2D;   // EN LINUX: GDI+ de la transicion (Plataforma/Dibujo2D.cs) hasta que este archivo se traduzca a SkiaSharp directo
using System.Collections.Generic;
using System.Diagnostics;
namespace Rocola
{
    public partial class Form1
    {
        private partial class AlbumView
        {
            // Portada nativa por separado. Solo se rasterizan texto, marcas y cuatro tiras
            // del contorno: no se crea una superficie del tamaño de la portada ampliada.
            private Bitmap BakeSeparatedCell(Album a, int cw, int side, int labelH, int gap, out GpuCellLayout layout)
            {
                int split = (cw + 1) / 2, left = (int)Math.Round((cw - side) / 2 + side * .5 - side * .5 / Math.Sqrt(2));
                int right = left + (int)Math.Round(side / Math.Sqrt(2)); // Cube.Draw redondea el ancho del contorno, no sus extremos
                bool queued = EnCola != null && EnCola(a);
                Rectangle mark = Rectangle.Empty;
                if (queued) {
                    float d = Math.Max(13f, side * .26f), x = (cw - side) / 2 + side - d * .92f;
                    float margin = Math.Max(1f, d * .07f) * .5f + 2f;
                    int mx = Math.Max(0, (int)Math.Floor(x - margin));
                    mark = new Rectangle(mx, 0, Math.Min(cw, (int)Math.Ceiling(x + d + margin)) - mx,
                        Math.Min(side + 2, (int)Math.Ceiling(d * .92f + margin)));
                }
                layout = new GpuCellLayout(); layout.Separate = true; layout.HideBlank = PortadasLod;
                layout.Width = Math.Max(split + 2, Math.Max(right - left + 5, mark.Width + 2));
                int y = 1;
                layout.TopAt = new Rectangle(left - 1, 0, right - left + 3, 2);
                layout.Top = new Rectangle(1, y, layout.TopAt.Width, 2); y += 4;
                layout.BottomAt = new Rectangle(left - 1, side - 2, right - left + 3, 4);
                layout.Bottom = new Rectangle(1, y, layout.BottomAt.Width, 4); y += 6;
                layout.LeftAt = new Rectangle(left - 1, 2, 3, Math.Max(0, side - 4));
                layout.Left = new Rectangle(1, y, 3, 1); y += 3;
                layout.RightAt = new Rectangle(right - 1, 2, 3, Math.Max(0, side - 4));
                layout.Right = new Rectangle(1, y, 3, 1); y += 3;
                layout.FaceAt = mark; layout.Face = new Rectangle(1, y, mark.Width, mark.Height);
                if (queued) y += mark.Height + 2;
                layout.Text1At = new Rectangle(0, side + gap, split, labelH);
                layout.Text1 = new Rectangle(1, y, split, labelH); y += labelH + 2;
                layout.Text2At = new Rectangle(split, side + gap, cw - split, labelH);
                layout.Text2 = new Rectangle(1, y, cw - split, labelH);
                layout.Height = y + labelH + 2;
                Bitmap result = new Bitmap(layout.Width, layout.Height, PixelFormat.Format32bppPArgb);
                try {
                    using (Graphics g = Graphics.FromImage(result)) g.Clear(Color.Transparent);
                    PegaBordes(result, layout, cw, side, left, right);
                    if (queued)
                        using (Bitmap stamp = new Bitmap(mark.Width, mark.Height, PixelFormat.Format32bppPArgb)) {
                            using (Graphics g = Graphics.FromImage(stamp)) {
                                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.TranslateTransform(-mark.X, 0); Palomito(g, new Rectangle((cw - side) / 2, 0, side, side));
                            }
                            using (Graphics g = Graphics.FromImage(result)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(stamp, layout.Face.Location); }
                        }
                    using (Bitmap label = new Bitmap(cw, labelH + 2, PixelFormat.Format32bppPArgb)) {
                        using (Graphics g = Graphics.FromImage(label)) {
                            g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias; EnsureLabelRes();
                            g.DrawString(AlbumLabel(a), lblFont, lblBrush, new Rectangle(2, 1, cw - 4, labelH), lblSF);
                        }
                        CopiaEtiquetaGpu(label, result, layout.Text1At, layout.Text1);
                        CopiaEtiquetaGpu(label, result, layout.Text2At, layout.Text2);
                    }
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
            // LOS CUATRO BORDES SON IGUALES EN TODAS LAS CELDAS (20-10-2026). Su dibujo depende solo de (cw, side): cuatro tiras con su Graphics
            // y su lapiz por celda, para sacar cada vez los mismos pixeles. Se dibujan UNA vez, con el mismo PackEdge, en un mapa de bits
            // chico transparente y cada celda copia sus filas (el resultado antes de los bordes es todo transparente y SourceCopy copia
            // los pixeles tal cual: los bytes son los mismos, comprobado contra el camino de antes en tironescambio). Una sola entrada:
            // al cambiar la resolucion o el lado se rehace.
            private Bitmap bordesHechos; private byte[] bordesFila; private int bordesCw = -1, bordesSide = -1;
            private readonly object bordesCerrojo = new object();
            internal int BordesDibujados, BordesCopiados;
            private void PegaBordes(Bitmap result, GpuCellLayout layout, int cw, int side, int left, int right)
            {
                int ancho = layout.TopAt.Width + 2, alto = layout.Right.Bottom;   // las tiras empiezan en x = 1 y acaban en la fila de Right
                if (ancho < 1 || alto < 1 || ancho > result.Width || alto > result.Height) {   // geometria rara: como siempre
                    PackEdge(result, layout.TopAt, layout.Top, left, right, side); PackEdge(result, layout.BottomAt, layout.Bottom, left, right, side);
                    PackEdge(result, layout.LeftAt, layout.Left, left, right, side); PackEdge(result, layout.RightAt, layout.Right, left, right, side);
                    return;
                }
                lock (bordesCerrojo) {
                    if (bordesHechos == null || bordesCw != cw || bordesSide != side || bordesHechos.Width != ancho || bordesHechos.Height != alto) {
                        Bitmap nuevo = new Bitmap(ancho, alto, PixelFormat.Format32bppPArgb);
                        try {
                            using (Graphics g = Graphics.FromImage(nuevo)) g.Clear(Color.Transparent);
                            PackEdge(nuevo, layout.TopAt, layout.Top, left, right, side); PackEdge(nuevo, layout.BottomAt, layout.Bottom, left, right, side);
                            PackEdge(nuevo, layout.LeftAt, layout.Left, left, right, side); PackEdge(nuevo, layout.RightAt, layout.Right, left, right, side);
                        }
                        catch { nuevo.Dispose(); throw; }
                        if (bordesHechos != null) bordesHechos.Dispose();
                        bordesHechos = nuevo; bordesCw = cw; bordesSide = side; bordesFila = new byte[ancho * 4]; BordesDibujados++;
                    }
                    else BordesCopiados++;
                    Rectangle area = new Rectangle(0, 0, ancho, alto);
                    BitmapData src = bordesHechos.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    try {
                        BitmapData dst = result.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                        try {
                            for (int y = 0; y < alto; y++) {
                                System.Runtime.InteropServices.Marshal.Copy(new IntPtr(src.Scan0.ToInt64() + (long)y * src.Stride), bordesFila, 0, bordesFila.Length);
                                System.Runtime.InteropServices.Marshal.Copy(bordesFila, 0, new IntPtr(dst.Scan0.ToInt64() + (long)y * dst.Stride), bordesFila.Length);
                            }
                        }
                        finally { result.UnlockBits(dst); }
                    }
                    finally { bordesHechos.UnlockBits(src); }
                }
            }
            private static void PackEdge(Bitmap target, Rectangle at, Rectangle source, int left, int right, int height)
            {
                if (at.Width < 1 || at.Height < 1) return;
                // El tramo vertical recto tiene exactamente los mismos tres pixeles en cada fila.
                using (Bitmap strip = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb)) {
                    using (Graphics g = Graphics.FromImage(strip)) using (Pen edge = new Pen(Color.FromArgb(120, 0, 0, 0), 1)) {
                        g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TranslateTransform(-at.X, -at.Y); g.DrawRectangle(edge, left, 0, right - left, height - 1);
                    }
                    using (Graphics g = Graphics.FromImage(target)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(strip, source.Location); }
                }
            }
        }
    }
    internal sealed partial class GpuPanel
    {
        private readonly Dictionary<Bitmap, CellTex> nativeCovers = new Dictionary<Bitmap, CellTex>();
        private readonly HashSet<Bitmap> protectedNativeAhead = new HashSet<Bitmap>();
        private readonly IntPtr[] nativeGridTex = new IntPtr[MaxCells];
        private readonly float[] nativeGridU = new float[MaxCells], nativeGridV = new float[MaxCells];
        private sealed class CubiertaGuardada
        {
            internal readonly D3D9.Vtx[] Vertices = new D3D9.Vtx[54];
            internal bool Lista; internal float X, Y, W, H, U, V; internal int Bw, Bh; internal uint Color;
        }
        private readonly CubiertaGuardada[] cubiertas = new CubiertaGuardada[MaxCells];
        internal int CubiertasCalculadas, CubiertasReusadas;
        private readonly D3D9.Vtx[] nativeQuad = new D3D9.Vtx[6];
        private readonly D3D9.Vtx[] nativeSoftQuad = new D3D9.Vtx[54];
        private readonly D3D9.Vtx[] nativeCorners = new D3D9.Vtx[16];
        private readonly float[] nativeXs = new float[4], nativeYs = new float[4], nativeUs = new float[4], nativeVs = new float[4];
        private long nativeUploadCost;
        private int maintenanceFrame = -1, maintenanceCount;
        private long maintenanceStart, maintenanceCost;
        // Las liberaciones del driver tambien son trabajo: un barrido completo no puede
        // quedar fuera del regulador. Al menos una por cuadro garantiza progreso.
        private bool ReleaseCoverTexture(IntPtr texture)
        {
            if (texture == IntPtr.Zero) return true;
            long now = Stopwatch.GetTimestamp();
            if (maintenanceFrame != cellFrame) {
                maintenanceFrame = cellFrame; maintenanceCount = 0; maintenanceStart = now;
            }
            if (maintenanceCount > 0 && (maintenanceCount >= 4 ||
                now - maintenanceStart + maintenanceCost >= Stopwatch.Frequency * .00075 ||
                (CoverControl != null && !CoverControl.CanStart(maintenanceCost, false, false)))) return false;
            D3D9.Release(texture); maintenanceCount++;
            long spent = Stopwatch.GetTimestamp() - now;
            maintenanceCost = CargaTrabajo.AprendeCoste(maintenanceCost, spent);
            if (CoverControl != null) CoverControl.Spend(spent);
            return true;
        }
        private IntPtr PrepareNativeCover(GpuGridCell cell, ref int budget, bool visible, out float u, out float v)
        {
            u = v = 1; if (!cell.Direct || cell.FullCover == null) return IntPtr.Zero;
            CellTex texture;
            if (!nativeCovers.TryGetValue(cell.FullCover, out texture)) texture = new CellTex();
            if (visible || texture.Frame != cellFrame) texture.Frame = visible ? cellFrame : unchecked(cellFrame - 1);
            if (texture.Tex != IntPtr.Zero) { u = texture.U; v = texture.V; return texture.Tex; }
            // La poda gradual puede tardar varios cuadros. No permitir crecimiento
            // ilimitado si el usuario encadena paginas mas rapido que su limpieza.
            if (nativeCovers.Count >= cellTextureCapacity + MaxCells) return IntPtr.Zero;
            if (budget <= 0 || (CoverControl != null && !CoverControl.CanStart(nativeUploadCost, visible, false))) return IntPtr.Zero;
            long start = Stopwatch.GetTimestamp(); budget--;
            texture.Tex = stars.CreateCoverShared(cell.FullCover, out texture.U, out texture.V);
            if (texture.Tex == IntPtr.Zero) sondeoMemoriaPedido = true;   // quiza falta memoria: volver a preguntar cuanta hay (PrecargaPaginaGpu.cs)
            texture.Bw = cell.FullCover.Width; texture.Bh = cell.FullCover.Height;
            texture.Opaca = texture.Tex != IntPtr.Zero && InteriorOpaco(cell.FullCover);   // el cielo no se pinta debajo (DibujoLigeroGpu.cs); dentro del coste medido
            long spent = Stopwatch.GetTimestamp() - start; nativeUploadCost = CargaTrabajo.AprendeCoste(nativeUploadCost, spent);
            if (CoverControl != null) CoverControl.Spend(spent);
            if (texture.Tex != IntPtr.Zero) { nativeCovers[cell.FullCover] = texture; cellBytesDirty = true; }
            u = texture.U; v = texture.V; return texture.Tex;
        }
        private bool NativeCoverReady(GpuGridCell cell)
        {
            CellTex texture;
            return cell.FullCover != null && nativeCovers.TryGetValue(cell.FullCover, out texture) && texture.Tex != IntPtr.Zero;
        }
        internal static Rectangle StaticFace(RectangleF bounds)
        {
            // Igual redondeo y lado frontal que Cube.Draw(angle=0), en coordenadas locales.
            int left = (int)Math.Round(bounds.X + bounds.Width * .5 - bounds.Width * .5 / Math.Sqrt(2));
            int right = (int)Math.Round(bounds.X + bounds.Width * .5 + bounds.Width * .5 / Math.Sqrt(2));
            return new Rectangle(left, (int)bounds.Y, Math.Max(0, right - left), (int)bounds.Height);
        }
        private void DrawNativeCover(int index, GpuGridCell cell, uint color)
        {
            RectangleF bounds = cell.CoverRect;
            // Redondear sin el desplazamiento animado para conservar la misma anchura en cada cuadro.
            Rectangle face = StaticFace(new RectangleF(0, 0, bounds.Width, bounds.Height));
            float x = bounds.X + face.X, y = bounds.Y;
            IntPtr texture = nativeGridTex[index];
            if (texture == IntPtr.Zero) {
                if (cell.Layout.HideBlank || cell.FullCover != null) return;
                uint alpha = color & 0xff000000u, shade = color & 255u;
                color = alpha | ((60u * shade / 255) << 16) | ((70u * shade / 255) << 8) | (110u * shade / 255);
                SetTexQuadA(nativeQuad, x, y, x + face.Width, y + face.Height, 1, 1, color);
                stars.DrawQuad(texture, nativeQuad); return;
            }
            CellTex source;
            if (!nativeCovers.TryGetValue(cell.FullCover, out source)) return;
            // La malla de una portada quieta es la misma en cada cuadro: se guarda por puesto de la rejilla (a lo mas MaxCells, 54 vertices
            // cada una, unos 54 KiB en total) y se vuelve a armar solo si cambia su sitio, su tamano, su textura o su opacidad.
            CubiertaGuardada g = index >= 0 && index < cubiertas.Length ? (cubiertas[index] ?? (cubiertas[index] = new CubiertaGuardada())) : null;
            if (g != null && g.Lista && g.X == x && g.Y == y && g.W == face.Width && g.H == face.Height && g.U == nativeGridU[index] && g.V == nativeGridV[index]
                && g.Bw == source.Bw && g.Bh == source.Bh && g.Color == color)
            { CubiertasReusadas++; stars.DrawTexturedBatch(texture, g.Vertices, 18); return; }
            // GDI interpolaba contra transparencia fuera del JPEG. Reproducir ese borde con
            // medio texel fuente, no una copia ampliada ni un contorno duro al deslizar.
            float px = face.Width / (2f * Math.Max(1, source.Bw)), py = face.Height / (2f * Math.Max(1, source.Bh));
            nativeXs[0] = x - px; nativeXs[1] = x + px; nativeXs[2] = x + face.Width - px; nativeXs[3] = x + face.Width + px;
            nativeYs[0] = y - py; nativeYs[1] = y + py; nativeYs[2] = y + face.Height - py; nativeYs[3] = y + face.Height + py;
            nativeUs[0] = 0; nativeUs[1] = nativeGridU[index] / (2f * Math.Max(1, source.Bw)); nativeUs[2] = nativeGridU[index] - nativeUs[1]; nativeUs[3] = nativeGridU[index];
            nativeVs[0] = 0; nativeVs[1] = nativeGridV[index] / (2f * Math.Max(1, source.Bh)); nativeVs[2] = nativeGridV[index] - nativeVs[1]; nativeVs[3] = nativeGridV[index];
            // La malla de nueve parches comparte dieciseis esquinas. Conservar sus
            // 54 vertices finales y el alfa del borde, calculando cada esquina una vez.
            for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++)
                NativeCorner(row * 4 + col, col, row, color);
            D3D9.Vtx[] malla = g != null ? g.Vertices : nativeSoftQuad;
            int n = 0;
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++) {
                int a = row * 4 + col;
                malla[n++] = nativeCorners[a]; malla[n++] = nativeCorners[a + 1]; malla[n++] = nativeCorners[a + 5];
                malla[n++] = nativeCorners[a]; malla[n++] = nativeCorners[a + 5]; malla[n++] = nativeCorners[a + 4];
            }
            if (g != null)
            {
                g.Lista = true; g.X = x; g.Y = y; g.W = face.Width; g.H = face.Height; g.U = nativeGridU[index]; g.V = nativeGridV[index];
                g.Bw = source.Bw; g.Bh = source.Bh; g.Color = color; CubiertasCalculadas++;
            }
            stars.DrawTexturedBatch(texture, malla, 18);
        }
        private void NativeCorner(int n, int x, int y, uint color)
        {
            SetV(ref nativeCorners[n], nativeXs[x], nativeYs[y], nativeUs[x], nativeVs[y]);
            nativeCorners[n].Color = x == 0 || x == 3 || y == 0 || y == 3 ? color & 0x00ffffffu : color;
        }
        private void ReleaseNativeCovers()
        {
            foreach (CellTex texture in nativeCovers.Values) if (texture.Tex != IntPtr.Zero) D3D9.Release(texture.Tex);
            nativeCovers.Clear(); protectedNativeAhead.Clear(); Array.Clear(nativeGridTex, 0, nativeGridTex.Length); cellBytesDirty = true;
        }
        private bool DiscardUnusedNative(bool allowAdjacent)
        {
            Bitmap victim = null; int oldest = 0;
            foreach (KeyValuePair<Bitmap, CellTex> row in nativeCovers) {
                int age = unchecked(cellFrame - row.Value.Frame);
                if (age > oldest && !protectedNativeAhead.Contains(row.Key)) { oldest = age; victim = row.Key; }
            }
            if (victim == null && allowAdjacent) foreach (KeyValuePair<Bitmap, CellTex> row in nativeCovers) {
                int age = unchecked(cellFrame - row.Value.Frame); if (age > oldest) { oldest = age; victim = row.Key; }
            }
            if (victim == null) return false;
            if (!ReleaseCoverTexture(nativeCovers[victim].Tex)) return false;
            nativeCovers.Remove(victim); cellBytesDirty = true; return true;
        }
        private void EvictNativeCovers()
        {
            while (nativeCovers.Count > cellTextureCapacity || CachedCellBytes() > cellTextureBudget)
                if (!DiscardUnusedNative(true)) break; // visibles siempre protegidas; vecinas solo ceden bajo presion real
        }
        private static void AtlasStraightEdge(D3D9.Vtx[] result, int offset, GpuGridCell cell, Rectangle src, Rectangle at, float u, float v, uint color)
        {
            float sx = cell.W / (cell.Layout.Text1At.Width + cell.Layout.Text2At.Width), sy = cell.H / cell.Layout.Text2At.Bottom;
            AtlasPart(result, offset, cell.X, cell.Y, sx, sy, src, at, u, v, color);
            // Centro de la fila unica: sin interpolar con el relleno transparente al estirar.
            for (int i = 0; i < 6; i++) result[offset + i].V = (src.Y + .5f) * v;
        }
        private static void SeparatedVertices(GpuGridCell cell, float uMax, float vMax, uint color, D3D9.Vtx[] result)
        {
            GpuCellLayout p = cell.Layout;
            float u = uMax / p.Width, v = vMax / p.Height;
            float sx = cell.W / (p.Text1At.Width + p.Text2At.Width), sy = cell.H / p.Text2At.Bottom;
            uint edge = p.HideBlank && !cell.CoverReady ? 0u : color;
            AtlasPart(result, 0, cell.X, cell.Y, sx, sy, p.Top, p.TopAt, u, v, edge);
            AtlasPart(result, 6, cell.X, cell.Y, sx, sy, p.Bottom, p.BottomAt, u, v, edge);
            AtlasStraightEdge(result, 12, cell, p.Left, p.LeftAt, u, v, edge);
            AtlasStraightEdge(result, 18, cell, p.Right, p.RightAt, u, v, edge);
            AtlasPart(result, 24, cell.X, cell.Y, sx, sy, p.Face, p.FaceAt, u, v, color);
            AtlasPart(result, 30, cell.X, cell.Y, sx, sy, p.Text1, p.Text1At, u, v, color);
            AtlasPart(result, 36, cell.X, cell.Y, sx, sy, p.Text2, p.Text2At, u, v, color);
        }
    }
}
#endif


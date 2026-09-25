#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Rocola
{
    internal sealed class CintaTexto : IDisposable
    {
        internal string Key, Identity;
        internal RectangleF Rect;
        internal Bitmap[] Tiles;
        internal int Width, Born;
        public void Dispose() { if (Tiles != null) foreach (Bitmap b in Tiles) b.Dispose(); }
    }
    internal sealed class CintasTexto : IDisposable
    {
        internal readonly List<CintaTexto> Items = new List<CintaTexto>();
        private int used, pixels;
        internal void Begin() { used = 0; pixels = 0; }
        internal void End()
        {
            while (Items.Count > used) { int i = Items.Count - 1; Items[i].Dispose(); Items.RemoveAt(i); }
        }
        internal bool Add(Graphics source, string text, Font font, Color color, RectangleF rect, StringFormat format)
        {
            if (string.IsNullOrEmpty(text) || rect.Width < 12 || rect.Height < 8 || used >= 48
                || !rect.IntersectsWith(source.VisibleClipBounds)) return false;
            using (StringFormat sf = (StringFormat)format.Clone())
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                sf.Trimming = StringTrimming.None; sf.Alignment = StringAlignment.Near;
                float measured = source.MeasureString(text, font, new SizeF(100000, rect.Height), sf).Width;
                if (measured <= rect.Width - 1) return false;
                int width = (int)Math.Ceiling(measured) + 4, height = (int)Math.Ceiling(rect.Height);
                // Limite de memoria ante metadatos corruptos; las tiras caben incluso en texturas de 1024.
                if (width > 16384 || height > 256 || width * height > 2097152 - pixels) return false;
                pixels += width * height;
                PointF[] points = { new PointF(rect.X, rect.Y) };
                using (Matrix m = source.Transform) m.TransformPoints(points);
                RectangleF target = new RectangleF(points[0].X, points[0].Y, rect.Width, rect.Height);
                string identity = text + "|" + font.Name + "|" + font.Size + "|" + (int)font.Style + "|" + (int)font.Unit
                    + "|" + rect.Width + "|" + height + "|" + (int)sf.LineAlignment;
                string key = identity + "|" + color.ToArgb();
                CintaTexto run = used < Items.Count ? Items[used] : null;
                if (run == null || run.Key != key)
                {
                    CintaTexto next = new CintaTexto { Key = key, Identity = identity, Width = width,
                        Born = run != null && run.Identity == identity ? run.Born : Environment.TickCount,
                        Tiles = new Bitmap[(width + 1023) / 1024] };
                    try
                    {
                        for (int i = 0; i < next.Tiles.Length; i++)
                        {
                            Bitmap b = new Bitmap(Math.Min(1024, width - i * 1024), height, PixelFormat.Format32bppPArgb);
                            next.Tiles[i] = b;
                            using (Graphics g = Graphics.FromImage(b))
                            using (SolidBrush brush = new SolidBrush(color))
                            {
                                g.Clear(Color.Transparent);
                                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                                g.TranslateTransform(-i * 1024, 0);
                                g.DrawString(text, font, brush, new RectangleF(0, 0, width, height), sf);
                            }
                        }
                    }
                    catch { foreach (Bitmap b in next.Tiles) if (b != null) b.Dispose(); throw; }
                    if (run != null) { run.Dispose(); Items[used] = next; }
                    else Items.Add(next);
                    run = next;
                }
                run.Rect = target; used++;
                return true;
            }
        }
        public void Dispose() { foreach (CintaTexto r in Items) r.Dispose(); Items.Clear(); used = 0; }
    }
    public partial class Form1
    {
        [ThreadStatic] private static CintasTexto capturandoCintas;
        private CintasTexto[] cintasTexto;
        private sealed class CapturaCintas : IDisposable
        {
            private CintasTexto before, current;
            internal CapturaCintas(CintasTexto group)
            { before = capturandoCintas; current = group; if (group != null) group.Begin(); capturandoCintas = group; }
            public void Dispose() { if (current != null) current.End(); capturandoCintas = before; }
        }
        private IDisposable CapturaTexto(int group)
        {
            if (cintasTexto == null) cintasTexto = new CintasTexto[5];
            if (cintasTexto[group] == null) cintasTexto[group] = new CintasTexto();
            if (!textosDeslizantes || !gpuEnabled) { cintasTexto[group].Dispose(); return new CapturaCintas(null); }
            return new CapturaCintas(cintasTexto[group]);
        }
        internal static bool CintaVistaPrevia(Graphics g, string text, Font font, Color color, Rectangle rect)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.FormatFlags = StringFormatFlags.NoWrap;
                return TextoConCinta(g, text, font, color, rect, sf);
            }
        }
        internal static void PintaTextoMovil(Graphics g, string text, Font f, Brush brush, RectangleF rect, StringFormat sf)
        {
            SolidBrush solid = brush as SolidBrush;
            if (solid == null || !TextoConCinta(g, text, f, solid.Color, rect, sf)) g.DrawString(text, f, brush, rect, sf);
        }
        private void PintaArtistaCompacto(Graphics g)
        {
            if (lblNow == null) return;
            Rectangle r = lblNow.Bounds;
            int right = r.Right;
            if (pastGenero != null && pastGenero.Visible) right = Math.Min(right, pastGenero.Left - 8);
            if (lblMejorada != null && lblMejorada.Visible) right = Math.Min(right, lblMejorada.Left - 8);
            Rectangle next = CajaSiguienteCompacto();
            if (!next.IsEmpty) right = Math.Min(right, next.Left - 16);
            r.Width = Math.Max(0, right - r.Left);
            using (StringFormat sf = new StringFormat())
            {
                sf.FormatFlags = StringFormatFlags.NoWrap;
                ContentAlignment align = lblNow.TextAlign;
                sf.LineAlignment = (align & (ContentAlignment.MiddleLeft | ContentAlignment.MiddleCenter | ContentAlignment.MiddleRight)) != 0
                    ? StringAlignment.Center : (align & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0
                    ? StringAlignment.Far : StringAlignment.Near;
                if (TextoConCinta(g, lblNow.Text, lblNow.Font, lblNow.ForeColor, r, sf)) return;
                GraphicsState state = g.Save();
                g.SetClip(r, CombineMode.Intersect); DrawLabelOverlay(g, lblNow); g.Restore(state);
            }
        }
        internal static bool TextoConCinta(Graphics g, string text, Font f, Color color, RectangleF rect, StringFormat sf)
        { return capturandoCintas != null && capturandoCintas.Add(g, text, f, color, rect, sf); }
        private void SueltaCintasTexto()
        {
            if (cintasTexto != null) foreach (CintasTexto group in cintasTexto) if (group != null) group.Dispose();
            cintasTexto = null;
        }
    }
    internal sealed partial class GpuPanel
    {
        internal Func<CintasTexto[]> GetCintasTexto;
        private sealed class CintaTile
        {
            internal IntPtr Tex;
            internal Bitmap Source;
            internal CintaTexto Run;
            internal int W, H, Group, Part;
            internal float U, V;
        }
        private readonly List<CintaTile> cintaTiles = new List<CintaTile>();
        private readonly D3D9.Vtx[] cintaQuad = new D3D9.Vtx[6];
        private void PreparaCintasTexto()
        {
            int count = 0;
            CintasTexto[] groups = GetCintasTexto != null ? GetCintasTexto() : null;
            if (groups != null)
                for (int group = 0; group < groups.Length; group++)
                {
                    if (groups[group] == null) continue;
                    foreach (CintaTexto run in groups[group].Items)
                        for (int part = 0; part < run.Tiles.Length; part++)
                        {
                            CintaTile tile;
                            if (count == cintaTiles.Count) { tile = new CintaTile(); cintaTiles.Add(tile); }
                            else tile = cintaTiles[count];
                            Bitmap source = run.Tiles[part];
                            if (!ReferenceEquals(source, tile.Source) || tile.Tex == IntPtr.Zero)
                            {
                                tile.Tex = Upload(tile.Tex, ref tile.W, ref tile.H, source, out tile.U, out tile.V);
                                tile.Source = source;
                            }
                            tile.Run = run; tile.Group = group; tile.Part = part; count++;
                        }
                }
            while (cintaTiles.Count > count)
            {
                int i = cintaTiles.Count - 1;
                if (cintaTiles[i].Tex != IntPtr.Zero) D3D9.Release(cintaTiles[i].Tex);
                cintaTiles.RemoveAt(i);
            }
        }
        private void DibujaCintasTexto(bool lista)
        {
            int now = Environment.TickCount;
            foreach (CintaTile tile in cintaTiles)
            {
                if ((tile.Group >= 2) != lista) continue;
                if (lista && !CintaDeLista(tile.Group)) continue;   // (BuscadorPortadasGpu.cs: lo de siempre, y con la ventana del buscador)
                float alpha = lista ? SceneAlpha() * ControlsOpacity : tile.Group == 0 ? 1 - idleBlend : idleBlend;
                if (alpha <= .003f || tile.Tex == IntPtr.Zero) continue;
                CintaTexto run = tile.Run; RectangleF r = run.Rect;
                float x = r.X, y = r.Y + (tile.Group == 0 ? Height - uiBotH : StatusTopY);
                RectangleF clip = new RectangleF(x, y, r.Width, r.Height);
                if (lista)
                {
                    Rectangle origin = tile.Group == 4 ? OrigenVistaPrevia() : GetPiezaRect(tile.Group == 2 ? 1 : 2);
                    if (origin.IsEmpty) continue;
                    if (tile.Group == 2)
                    {
                        Rectangle selected = GetPiezaRect(2);
                        float rowY = origin.Y + r.Y + r.Height * .5f;
                        if (!selected.IsEmpty && rowY >= selected.Top && rowY < selected.Bottom) continue;
                    }
                    x = origin.X + r.X + (tile.Group == 4 || songFrameVertical ? 0 : songFrameSlide);
                    y = origin.Y + r.Y + (tile.Group != 4 && songFrameVertical ? songFrameSlide : 0);
                    clip = RectangleF.Intersect(new RectangleF(x, y, r.Width, r.Height), tile.Group == 4 ? origin : GetPiezaRect(0));
                    if (tile.Group == 4) alpha = AlfaVistaPrevia(origin);
                    if (clip.IsEmpty) continue;
                }
                float off = CorreTitulo((uint)(now - run.Born), Math.Max(0, run.Width - r.Width));
                float start = tile.Part * 1024 - off;
                float left = Math.Max(clip.Left - x, start), right = Math.Min(clip.Right - x, start + tile.Source.Width);
                if (right <= left) continue;
                float u0 = (left - start) / tile.Source.Width * tile.U;
                float u1 = (right - start) / tile.Source.Width * tile.U;
                float v0 = (clip.Top - y) / r.Height * tile.V, v1 = (clip.Bottom - y) / r.Height * tile.V;
                SetTexQuadA(cintaQuad, x + left, clip.Top, x + right, clip.Bottom, tile.U, tile.V, ((uint)(alpha * 255) << 24) | 0xFFFFFFu);
                for (int k = 0; k < cintaQuad.Length; k++)
                {
                    cintaQuad[k].U = cintaQuad[k].U == 0 ? u0 : u1;
                    cintaQuad[k].V = cintaQuad[k].V == 0 ? v0 : v1;
                }
                stars.DrawQuad(tile.Tex, cintaQuad);
            }
        }
        private void SueltaCintasGpu()
        {
            foreach (CintaTile t in cintaTiles) if (t.Tex != IntPtr.Zero) D3D9.Release(t.Tex);
            cintaTiles.Clear();
        }
    }
}
#endif

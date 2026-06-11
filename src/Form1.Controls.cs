using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private class RoundButton : Button
        {
            public RoundButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                FlatAppearance.MouseOverBackColor = Color.Transparent;
                FlatAppearance.MouseDownBackColor = Color.Transparent;
                SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
                ForeColor = Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                int radius = Math.Min(Height / 2, 14);
                Color top = Enabled ? Color.FromArgb(40, 62, 135) : Color.FromArgb(28, 34, 60);
                Color bottom = Enabled ? Color.FromArgb(20, 30, 82) : Color.FromArgb(18, 22, 44);
                using (GraphicsPath path = RoundedPath(rect, radius))
                {
                    using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), top, bottom, LinearGradientMode.Vertical))
                    {
                        g.FillPath(lg, path);
                    }
                    g.DrawPath(Gdi.Pen(Color.FromArgb(Enabled ? 200 : 90, 110, 150, 230), 1.4f), path);
                }
                TextRenderer.DrawText(g, Text, Font, rect,
                    Enabled ? Color.White : Color.FromArgb(150, 160, 185),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static class Cube
        {
            // Arreglos de trabajo reutilizados entre frames (solo hilo de UI):
            // evita 3 asignaciones por cubo dibujado a 25 fps.
            private static readonly double[] xs = new double[4];
            private static readonly double[] depth = new double[4];
            private static readonly int[] order = new int[4];

            public static void Draw(Graphics g, Rectangle bounds, Image cover, double angle)
            {
                double cx = bounds.X + (double)bounds.Width / 2.0;
                int y = bounds.Y;
                int height = bounds.Height;
                double half = (double)bounds.Width / 2.0;
                double rad = Math.PI / 180.0;
                for (int i = 0; i < 4; i++)
                {
                    xs[i] = cx + half * Math.Cos(angle + 45.0 * rad + (i * 90) * rad);
                }
                for (int i = 0; i < 4; i++)
                {
                    depth[i] = Math.Sin(angle + 90.0 * rad + (i * 90) * rad);
                }
                order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 3 - i; j++)
                    {
                        if (depth[order[j]] > depth[order[j + 1]])
                        {
                            int tmp = order[j];
                            order[j] = order[j + 1];
                            order[j + 1] = tmp;
                        }
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    int f = order[i];
                    if (depth[f] <= 0.0)
                    {
                        continue;
                    }
                    int x0 = (int)Math.Round(Math.Min(xs[f], xs[(f + 1) % 4]));
                    int w = (int)Math.Round(Math.Max(xs[f], xs[(f + 1) % 4])) - x0;
                    if (w < 1)
                    {
                        continue;
                    }
                    Rectangle face = new Rectangle(x0, y, w, height);
                    if (cover != null)
                    {
                        g.DrawImage(cover, face);
                    }
                    else
                    {
                        g.FillRectangle(Gdi.Brush(Color.FromArgb(60, 70, 110)), face);
                    }
                    int shadow = (int)((1.0 - depth[f]) * 130.0);
                    if (shadow > 0)
                    {
                        g.FillRectangle(Gdi.Brush(Color.FromArgb(shadow, 0, 0, 0)), face);
                    }
                }
                double minX = xs[0];
                double maxX = xs[0];
                for (int i = 1; i < 4; i++)
                {
                    if (xs[i] < minX)
                    {
                        minX = xs[i];
                    }
                    if (xs[i] > maxX)
                    {
                        maxX = xs[i];
                    }
                }
                g.DrawRectangle(Gdi.Pen(Color.FromArgb(120, 0, 0, 0), 1f), (int)Math.Round(minX), y, (int)Math.Round(maxX - minX), height - 1);
            }
        }

        private class HelpBar : Control
        {
            public HelpBar()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                int cy = Height / 2;
                Color cText = Color.FromArgb(228, 234, 250);
                Color cRed = Color.FromArgb(228, 72, 72);
                Color cGreen = Color.FromArgb(72, 212, 112);
                int dot = Math.Min(20, Height - 8);
                if (dot < 12)
                {
                    dot = 12;
                }
                float gap = 7f;
                float sep = 20f;
                Font f = Gdi.SansBold95;
                float x = 2f;
                x = CtrlDot(g, x, cy, dot, cRed) + 4f;
                x = CtrlDot(g, x, cy, dot, cGreen);
                x = CtrlTxt(g, f, "Buscar", x + gap, cy, cText) + sep;
                x = CtrlPad(g, x, cy, dot + 4);
                x = CtrlTxt(g, f, "Mover", x + gap, cy, cText) + sep;
                x = CtrlDot(g, x, cy, dot, cGreen);
                x = CtrlTxt(g, f, "Entrar / Poner", x + gap, cy, cText) + sep;
                x = CtrlDot(g, x, cy, dot, cRed);
                CtrlTxt(g, f, "Volver", x + gap, cy, cText);
            }
        }

        private struct GlowP
        {
            public float bx;
            public float by;
            public float seed;
            public float size;
            public float sp;
        }

        private class BgPanel : Panel
        {
            public Color Accent = DefaultAccent;
            public double Phase;

            private Bitmap bgCache;
            private int cacheW = -1;
            private int cacheH = -1;
            private int cacheAccent;

            public BgPanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width;
                int h = Height;
                if (w < 4 || h < 4)
                {
                    return;
                }
                if (bgCache == null || cacheW != w || cacheH != h || cacheAccent != Accent.ToArgb())
                {
                    if (bgCache != null)
                    {
                        bgCache.Dispose();
                        bgCache = null;
                    }
                    bgCache = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    using (Graphics bg = Graphics.FromImage(bgCache))
                    using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Shade(Accent, 0.5), Shade(Accent, 0.86), LinearGradientMode.Vertical))
                    {
                        bg.FillRectangle(lg, 0, 0, w, h);
                    }
                    cacheW = w;
                    cacheH = h;
                    cacheAccent = Accent.ToArgb();
                }
                g.DrawImageUnscaled(bgCache, 0, 0);
                DrawGlows(g, w, h);
            }

            private void DrawGlows(Graphics g, int w, int h)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color glow1 = Lighten(Accent, 0.4);
                Color glow2 = Lighten(Accent, 0.7);
                float t = (float)Phase * 1.8f;
                float x1 = (0.4f + 0.16f * (float)Math.Sin(t * 0.12)) * w;
                float y1 = (0.42f + 0.12f * (float)Math.Cos(t * 0.1)) * h;
                float r1 = Math.Min(Math.Min(w, h) * 0.55f, 460f);
                g.FillEllipse(Gdi.Brush(Color.FromArgb(26, glow1)), x1 - r1, y1 - r1, r1 * 2f, r1 * 2f);
                float x2 = (0.64f + 0.14f * (float)Math.Cos(t * 0.09 + 1.5)) * w;
                float y2 = (0.55f + 0.13f * (float)Math.Sin(t * 0.11 + 0.7)) * h;
                float r2 = Math.Min(Math.Min(w, h) * 0.42f, 460f);
                g.FillEllipse(Gdi.Brush(Color.FromArgb(22, glow2)), x2 - r2, y2 - r2, r2 * 2f, r2 * 2f);
            }
        }

        private class AlbumView : Control
        {
            public List<Album> Page = new List<Album>();
            public int Selected;
            public double SelAngle;
            public double Phase;
            public Color BgAccent = DefaultAccent;
            public Func<Album, Image> GetCover;
            public Action<int> OnActivate;

            private Bitmap bgCache;
            private Bitmap cubeCache;
            private int cacheW = -1;
            private int cacheH = -1;
            private int cacheSel = -2;
            private string cacheSig;
            private Color cacheAccent = Color.Empty;
            private GlowP[] parts;
            private Pen cPenGlow;
            private Pen cPenMid;
            private Pen cPenLine;
            private int prevIdx = -1;
            private double prevAngle;
            private float prevSpin;
            private float spinDir = 1f;

            private Font labelFont;
            private StringFormat labelFormat;

            public void MoveSelectionTo(int newSel, int dir)
            {
                int count = (Page != null) ? Page.Count : 0;
                if (newSel != Selected && Selected >= 0 && Selected < count)
                {
                    prevIdx = Selected;
                    prevAngle = SelAngle;
                    prevSpin = 0.7f * ((dir >= 0) ? 1f : (-1f));
                }
                spinDir = (dir >= 0) ? 1f : (-1f);
                Selected = newSel;
                SelAngle = 0.0;
            }

            public void CancelHandoff()
            {
                prevIdx = -1;
                prevSpin = 0f;
            }

            public void StepSpin()
            {
                SelAngle += 0.06 * spinDir;
                if (prevIdx < 0)
                {
                    return;
                }
                if (Math.Abs(prevSpin) > 0.02f)
                {
                    prevAngle += prevSpin;
                    prevSpin *= 0.85f;
                    return;
                }
                double rest = Math.Round(prevAngle / (Math.PI * 2.0)) * (Math.PI * 2.0);
                prevAngle += (rest - prevAngle) * 0.25;
                if (Math.Abs(rest - prevAngle) < 0.03)
                {
                    prevIdx = -1;
                }
            }

            public AlbumView()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            public void InvalidateCubeCache()
            {
                cacheSig = null;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int cellW = Width / 6;
                int cellH = Height / 3;
                if (cellW > 0 && cellH > 0)
                {
                    int col = e.X / cellW;
                    int row = e.Y / cellH;
                    if (col >= 0 && col < 6 && row >= 0 && row < 3)
                    {
                        int idx = row * 6 + col;
                        if (Page != null && idx < Page.Count)
                        {
                            Selected = idx;
                            if (OnActivate != null)
                            {
                                OnActivate(idx);
                            }
                        }
                    }
                }
                base.OnMouseClick(e);
            }

            private void InitParts()
            {
                Random rnd = new Random(7321);
                int n = 8;
                parts = new GlowP[n];
                for (int i = 0; i < n; i++)
                {
                    parts[i].bx = (float)rnd.NextDouble();
                    parts[i].by = (float)rnd.NextDouble();
                    parts[i].seed = (float)(rnd.NextDouble() * 6.2832);
                    parts[i].size = 7f + (float)(rnd.NextDouble() * 16.0);
                    parts[i].sp = 0.6f + (float)(rnd.NextDouble() * 0.9);
                }
            }

            private string Sig()
            {
                if (Page == null || Page.Count == 0)
                {
                    return "0";
                }
                return Page.Count + "|" + (Page[0].Folder ?? "") + "|" + (Page[Page.Count - 1].Folder ?? "");
            }

            private void Geometry(int w, int h, int i, out Rectangle cubeArea, out Rectangle labelRect)
            {
                int cellW = w / 6;
                int cellH = h / 3;
                int col = i % 6;
                int row = i / 6;
                Rectangle cell = new Rectangle(col * cellW, row * cellH, cellW, cellH);
                int labelH = 38;
                int padX = 8;
                int gap = 3;
                int padY = 6;
                int side = Math.Min(cell.Width - 2 * padX, cell.Height - labelH - gap - 2 * padY);
                if (side < 10)
                {
                    side = 10;
                }
                int total = side + gap + labelH;
                int top = cell.Y + Math.Max(padY, (cell.Height - total) / 2);
                cubeArea = new Rectangle(cell.X + (cell.Width - side) / 2, top, side, side);
                labelRect = new Rectangle(cell.X + 2, cubeArea.Bottom + gap, cell.Width - 4, labelH);
            }

            private void EnsureLabelResources()
            {
                if (labelFont == null)
                {
                    labelFont = new Font(Font.FontFamily, 10f, FontStyle.Bold);
                }
                if (labelFormat == null)
                {
                    labelFormat = new StringFormat();
                    labelFormat.Alignment = StringAlignment.Center;
                    labelFormat.LineAlignment = StringAlignment.Center;
                    labelFormat.Trimming = StringTrimming.EllipsisCharacter;
                    labelFormat.FormatFlags = StringFormatFlags.LineLimit;
                }
            }

            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                if (labelFont != null)
                {
                    labelFont.Dispose();
                    labelFont = null;
                }
            }

            private void RebuildBg(int w, int h)
            {
                if (bgCache == null || bgCache.Width != w || bgCache.Height != h)
                {
                    if (bgCache != null)
                    {
                        bgCache.Dispose();
                    }
                    bgCache = new Bitmap(w, h);
                }
                using (Graphics g = Graphics.FromImage(bgCache))
                using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Shade(BgAccent, 0.5), Shade(BgAccent, 0.86), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(lg, 0, 0, w, h);
                }
            }

            private void RebuildCubes(int w, int h)
            {
                if (cubeCache == null || cubeCache.Width != w || cubeCache.Height != h)
                {
                    if (cubeCache != null)
                    {
                        cubeCache.Dispose();
                    }
                    cubeCache = new Bitmap(w, h);
                }
                using (Graphics g = Graphics.FromImage(cubeCache))
                {
                    g.Clear(Color.Transparent);
                    if (Page == null)
                    {
                        return;
                    }
                    int cellW = w / 6;
                    int cellH = h / 3;
                    if (cellW < 4 || cellH < 4)
                    {
                        return;
                    }
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    EnsureLabelResources();
                    for (int i = 0; i < Page.Count && i < 18; i++)
                    {
                        Rectangle cubeArea;
                        Rectangle labelRect;
                        Geometry(w, h, i, out cubeArea, out labelRect);
                        Cube.Draw(g, cubeArea, (GetCover != null) ? GetCover(Page[i]) : null, 0.0);
                        g.DrawString(AlbumLabel(Page[i]), labelFont, Gdi.Brush(Color.White), labelRect, labelFormat);
                    }
                }
            }

            public void RefreshCover(string coverPath)
            {
                if (Page == null || coverPath == null)
                {
                    return;
                }
                int w = Width;
                int h = Height;
                if (cubeCache == null || cubeCache.Width != w || cubeCache.Height != h)
                {
                    InvalidateCubeCache();
                    return;
                }
                int cellW = w / 6;
                int cellH = h / 3;
                if (cellW < 4 || cellH < 4)
                {
                    return;
                }
                bool painted = false;
                using (Graphics g = Graphics.FromImage(cubeCache))
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    EnsureLabelResources();
                    for (int i = 0; i < Page.Count && i < 18; i++)
                    {
                        if (Page[i] != null && !(Page[i].CoverPath != coverPath))
                        {
                            int col = i % 6;
                            int row = i / 6;
                            Rectangle cell = new Rectangle(col * cellW, row * cellH, cellW, cellH);
                            Rectangle cubeArea;
                            Rectangle labelRect;
                            Geometry(w, h, i, out cubeArea, out labelRect);
                            g.CompositingMode = CompositingMode.SourceCopy;
                            g.FillRectangle(Gdi.Brush(Color.Transparent), cell);
                            g.CompositingMode = CompositingMode.SourceOver;
                            Cube.Draw(g, cubeArea, (GetCover != null) ? GetCover(Page[i]) : null, 0.0);
                            g.DrawString(AlbumLabel(Page[i]), labelFont, Gdi.Brush(Color.White), labelRect, labelFormat);
                            painted = true;
                        }
                    }
                }
                if (painted)
                {
                    Invalidate();
                }
            }

            private void DrawParticles(Graphics g, int w, int h)
            {
                if (parts == null)
                {
                    InitParts();
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color glow1 = Lighten(BgAccent, 0.4);
                Color glow2 = Lighten(BgAccent, 0.7);
                float t = (float)Phase;
                float t2 = t * 1.8f;
                SolidBrush brush = Gdi.Brush(Color.Black);
                float x1 = (0.42f + 0.16f * (float)Math.Sin(t2 * 0.12)) * w;
                float y1 = (0.4f + 0.12f * (float)Math.Cos(t2 * 0.1)) * h;
                float r1 = Math.Min(Math.Min(w, h) * 0.5f, 460f);
                brush.Color = Color.FromArgb(30, glow1);
                g.FillEllipse(brush, x1 - r1, y1 - r1, r1 * 2f, r1 * 2f);
                float x2 = (0.62f + 0.14f * (float)Math.Cos(t2 * 0.09 + 1.5)) * w;
                float y2 = (0.55f + 0.13f * (float)Math.Sin(t2 * 0.11 + 0.7)) * h;
                float r2 = Math.Min(Math.Min(w, h) * 0.4f, 460f);
                brush.Color = Color.FromArgb(24, glow2);
                g.FillEllipse(brush, x2 - r2, y2 - r2, r2 * 2f, r2 * 2f);
                for (int i = 0; i < parts.Length; i++)
                {
                    GlowP p = parts[i];
                    float px = (p.bx + 0.045f * (float)Math.Sin(t * 0.3 * p.sp + p.seed)) * w;
                    float py = (p.by - t * 0.012f * p.sp) % 1f;
                    if (py < 0f)
                    {
                        py += 1f;
                    }
                    py *= h;
                    int alpha = (int)((0.5f + 0.5f * (float)Math.Sin(t * 1.6 * p.sp + p.seed)) * 150f);
                    if (alpha < 0)
                    {
                        alpha = 0;
                    }
                    if (alpha > 255)
                    {
                        alpha = 255;
                    }
                    float sz = p.size * (0.85f + 0.3f * (float)Math.Sin(t * 0.8 + p.seed));
                    brush.Color = Color.FromArgb(alpha / 2, glow1);
                    g.FillEllipse(brush, px - sz, py - sz, sz * 2f, sz * 2f);
                    brush.Color = Color.FromArgb(alpha, glow2);
                    g.FillEllipse(brush, px - sz * 0.4f, py - sz * 0.4f, sz * 0.8f, sz * 0.8f);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width;
                int h = Height;
                if (w < 8 || h < 8)
                {
                    g.Clear(Shade(BgAccent, 0.82));
                    return;
                }
                bool sizeChanged = cacheW != w || cacheH != h;
                string sig = Sig();
                if (sizeChanged || bgCache == null || cacheAccent != BgAccent)
                {
                    RebuildBg(w, h);
                }
                if (sizeChanged || cubeCache == null || sig != cacheSig)
                {
                    RebuildCubes(w, h);
                }
                cacheW = w;
                cacheH = h;
                cacheSig = sig;
                cacheSel = Selected;
                cacheAccent = BgAccent;
                g.DrawImageUnscaled(bgCache, 0, 0);
                DrawParticles(g, w, h);
                g.DrawImageUnscaled(cubeCache, 0, 0);
                if (Page != null && Selected >= 0 && Selected < Page.Count)
                {
                    int cellW = w / 6;
                    int cellH = h / 3;
                    if (cellW >= 4 && cellH >= 4)
                    {
                        Rectangle cubeArea;
                        Rectangle labelRect;
                        Geometry(w, h, Selected, out cubeArea, out labelRect);
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        Rectangle r = cubeArea;
                        r.Inflate(4, 4);
                        if (cPenGlow == null)
                        {
                            cPenGlow = new Pen(Color.FromArgb(20, 255, 255, 255), 11f);
                            cPenMid = new Pen(Color.FromArgb(45, 255, 255, 255), 6f);
                            cPenLine = new Pen(Color.FromArgb(235, 255, 255, 255), 2.4f);
                        }
                        using (GraphicsPath path = RoundRect(r, 10))
                        {
                            g.DrawPath(cPenGlow, path);
                            g.DrawPath(cPenMid, path);
                            g.DrawPath(cPenLine, path);
                        }
                        Cube.Draw(g, cubeArea, (GetCover != null) ? GetCover(Page[Selected]) : null, SelAngle);
                    }
                }
                if (prevIdx >= 0 && prevIdx != Selected && Page != null && prevIdx < Page.Count)
                {
                    int cellW = w / 6;
                    int cellH = h / 3;
                    if (cellW >= 4 && cellH >= 4)
                    {
                        Rectangle cubeArea;
                        Rectangle labelRect;
                        Geometry(w, h, prevIdx, out cubeArea, out labelRect);
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        Cube.Draw(g, cubeArea, (GetCover != null) ? GetCover(Page[prevIdx]) : null, prevAngle);
                    }
                }
            }
        }

        private class VideoHost : AxHost
        {
            public VideoHost()
                : base("6bf52a52-394a-11d3-b153-00c04f79faa6")
            {
            }

            public object GetOcxObj()
            {
                return GetOcx();
            }
        }

        private class CubeBox : Control
        {
            public Image Cover;
            public double Angle;
            public double Phase;

            public CubeBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.FromArgb(10, 15, 44);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                if (BackColor.A == byte.MaxValue)
                {
                    g.Clear(BackColor);
                }
                int w = Width;
                int h = Height;
                if (w >= 12 && h >= 12)
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    int side = (int)(Math.Min(w, h) * 0.78);
                    Rectangle bounds = new Rectangle((w - side) / 2, (h - side) / 2, side, side);
                    float cx = w / 2f;
                    float cy = h / 2f;
                    float tilt = (float)(10.0 * Math.Sin(Phase * 0.9));
                    GraphicsState state = g.Save();
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(tilt);
                    g.TranslateTransform(0f - cx, 0f - cy);
                    Cube.Draw(g, bounds, Cover, Angle);
                    g.Restore(state);
                }
            }
        }

        private class SongPagerBar : Control
        {
            public int Page;
            public int Pages = 1;

            public SongPagerBar()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width;
                int h = Height;
                g.FillRectangle(Gdi.Brush(BackColor), 0, 0, w, h);
                if (Pages <= 1 || w < 60 || h < 10)
                {
                    return;
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                int cy = h / 2;
                Color cMain = Color.FromArgb(228, 236, 252);
                Color cDim = Color.FromArgb(175, 188, 215);
                Color cArrow = Color.FromArgb(242, 206, 72);
                Font f = Gdi.SansBold95;
                string text = "Pagina " + (Page + 1) + " de " + Pages;
                SizeF size = g.MeasureString(text, f);
                g.DrawString(text, f, Gdi.Brush(cMain), (w - size.Width) / 2f, cy - size.Height / 2f);
                if (Page > 0)
                {
                    float x = 4f;
                    x = CtrlDirLeft(g, x, cy, 11, cArrow) + 5f;
                    CtrlTxt(g, f, "pagina " + Page, x, cy, cDim);
                }
                if (Page < Pages - 1)
                {
                    string next = "pagina " + (Page + 2);
                    float x = (w - 4 - 11 - 5) - g.MeasureString(next, f).Width;
                    x = CtrlTxt(g, f, next, x, cy, cDim) + 5f;
                    CtrlDirRight(g, x, cy, 11, cArrow);
                }
            }
        }

        private class LetterBar : Control
        {
            public string Letters = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
            public char Current = '#';
            public double Phase;
            public HashSet<char> Available;
            public Action<char> OnPick;

            private Font letterFont;
            private Font videoFont;
            private StringFormat centerFormat;

            public LetterBar()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
            }

            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                if (letterFont != null)
                {
                    letterFont.Dispose();
                    letterFont = null;
                }
                if (videoFont != null)
                {
                    videoFont.Dispose();
                    videoFont = null;
                }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int count = Letters.Length;
                if (count == 0 || Width <= 0)
                {
                    base.OnMouseClick(e);
                    return;
                }
                int cellW = Width / count;
                if (cellW <= 0)
                {
                    base.OnMouseClick(e);
                    return;
                }
                int idx = e.X / cellW;
                if (idx >= 0 && idx < count && OnPick != null)
                {
                    OnPick(Letters[idx]);
                }
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width;
                int h = Height;
                int count = Letters.Length;
                if (w < 4 || h < 4 || count == 0)
                {
                    return;
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cellW = (float)w / count;
                double pulse = 0.5 + 0.5 * Math.Sin(Phase * 2.2);
                if (letterFont == null)
                {
                    letterFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
                }
                if (videoFont == null)
                {
                    videoFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
                }
                if (centerFormat == null)
                {
                    centerFormat = new StringFormat();
                    centerFormat.Alignment = StringAlignment.Center;
                    centerFormat.LineAlignment = StringAlignment.Center;
                }
                for (int i = 0; i < count; i++)
                {
                    char c = Letters[i];
                    bool isVideo = c == '►';
                    string text = isVideo ? "VIDEO" : c.ToString();
                    Font font = isVideo ? videoFont : letterFont;
                    RectangleF cell = new RectangleF(i * cellW, 0f, cellW, h);
                    bool selected = c == Current;
                    bool available = Available == null || Available.Contains(c);
                    if (selected)
                    {
                        int pad = 3;
                        RectangleF r = new RectangleF(cell.X + pad, pad, cell.Width - 2 * pad, h - 2 * pad);
                        int alpha = 70 + (int)(pulse * 110.0);
                        using (GraphicsPath path = RoundRectF(r, 8))
                        {
                            g.FillPath(Gdi.Brush(Color.FromArgb(alpha, 90, 170, 255)), path);
                            g.DrawPath(Gdi.Pen(Color.FromArgb(220, 150, 205, 255), 2f), path);
                        }
                        g.DrawString(text, font, Gdi.Brush(Color.White), cell, centerFormat);
                    }
                    else
                    {
                        g.DrawString(text, font, Gdi.Brush(available ? Color.FromArgb(225, 232, 245) : Color.FromArgb(110, 120, 150)), cell, centerFormat);
                    }
                }
            }

            private static GraphicsPath RoundRectF(RectangleF r, int radius)
            {
                GraphicsPath path = new GraphicsPath();
                float d = radius * 2;
                if (d > r.Height)
                {
                    d = r.Height;
                }
                if (d > r.Width)
                {
                    d = r.Width;
                }
                if (d < 2f)
                {
                    d = 2f;
                }
                path.AddArc(r.X, r.Y, d, d, 180f, 90f);
                path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
                path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
                path.CloseFigure();
                return path;
            }
        }

        private class ProgressView : Control
        {
            private double _value;
            private int lastPx = -1;

            public double Value
            {
                get
                {
                    return _value;
                }
                set
                {
                    _value = (value < 0.0) ? 0.0 : ((value > 1.0) ? 1.0 : value);
                    // Solo repinta si la barra cambia de tamano en pixeles.
                    int px = (int)(_value * (Width - 2));
                    if (px != lastPx)
                    {
                        lastPx = px;
                        Invalidate();
                    }
                }
            }

            public ProgressView()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Width;
                int h = Height;
                if (w < 4 || h < 4)
                {
                    return;
                }
                using (GraphicsPath back = RoundedPath(new Rectangle(0, 0, w - 1, h - 1), h / 2))
                {
                    g.FillPath(Gdi.Brush(Color.FromArgb(60, 255, 255, 255)), back);
                }
                int px = (int)(_value * (w - 2));
                lastPx = px;
                if (px < 4)
                {
                    return;
                }
                Rectangle bar = new Rectangle(1, 1, px, h - 3);
                using (GraphicsPath fill = RoundedPath(bar, (h - 3) / 2))
                using (LinearGradientBrush lg = new LinearGradientBrush(bar, Color.FromArgb(235, 245, 255), Color.FromArgb(150, 190, 255), LinearGradientMode.Vertical))
                {
                    g.FillPath(lg, fill);
                }
            }
        }

        private enum KKind
        {
            Char,
            Space,
            Back,
            Choose
        }

        private class SearchKey
        {
            public KKind Kind;
            public char Ch;
            public string Label;
            public RectangleF R;
        }

        private class SearchItem
        {
            public int Kind;
            public string Display;
            public string Key;
            public int Score;
            public Album AlbumRef;
            public Track TrackRef;
            public List<Album> Albums;
        }
    }
}

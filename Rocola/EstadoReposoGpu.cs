#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        private Bitmap idlePanelBmp;
        private string idlePanelSig;
        private int idlePanelCheck;
        private int idlePanelProgressStart;
        private Bitmap BuildIdlePanel()
        {
            if (nowPlaying == null || videoOn) return null;
            if (gpuPanel != null && idlePanelBmp != null) gpuPanel.IdleProgressStart = idlePanelProgressStart;
            int w = ClientSize.Width, h = GpuPanel.IdlePanelHeight(ClientSize.Height);
            if (w < 480 || h < 140) return null;
            int now = Environment.TickCount;
            if (idlePanelBmp != null && idlePanelBmp.Width == w && idlePanelBmp.Height == h &&
                unchecked(now - idlePanelCheck) < 400) return idlePanelBmp;
            idlePanelCheck = now;
            string title, artist; nowPlaying.TituloArtista(out title, out artist);
            string[] qt = new string[Math.Min(colaDoce ? 12 : 3, queue.Count)], qa = new string[qt.Length];
            Image[] covers = new Image[qt.Length];
            StringBuilder sig = new StringBuilder();
            sig.Append(w).Append('|').Append(h).Append('|').Append(nowPlaying.PathKey()).Append('|')
                .Append(title).Append('|').Append(artist).Append('|').Append(queue.Count).Append('|').Append(bgAccent.ToArgb());
            string time = lblTime != null ? lblTime.Text : "";
            int iaBadgeKind = MejActivo() ? 2 : 0;
            sig.Append('|').Append(time).Append('|').Append(estadoTransparente).Append('|').Append(iaBadgeKind);
            for (int i = 0; i < qt.Length; i++)
            {
                queue[i].TituloArtista(out qt[i], out qa[i]);
                covers[i] = queue[i].Disc != null ? LoadCoverAsync(queue[i].Disc) : null;
                sig.Append('|').Append(queue[i].PathKey()).Append('|').Append(qt[i]).Append('|').Append(qa[i])
                    .Append('|').Append(covers[i] != null ? covers[i].GetHashCode() : 0);
            }
            sig.Append('|').Append(colaDoce);
            string key = sig.ToString();
            if (idlePanelBmp != null && key == idlePanelSig) return idlePanelBmp;
            int progressStart;
            Bitmap next = RenderIdlePanelLayout(w, h, title, artist, time, qt, qa, covers, queue.Count, bgAccent, estadoTransparente, iaBadgeKind, colaDoce, out progressStart);
            idlePanelProgressStart = progressStart;
            if (gpuPanel != null) gpuPanel.IdleProgressStart = progressStart;
            Bitmap old = idlePanelBmp; idlePanelBmp = next; idlePanelSig = key;
            if (old != null) old.Dispose();
            return idlePanelBmp;
        }

        // Textura exclusiva GPU: sus textos no se escalan durante la animacion.
        // El cubo actual se dibuja aparte como geometria y crece con el panel.
        internal static Bitmap RenderIdlePanel(int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent)
        { return RenderIdlePanelMode(w, h, title, artist, time, qt, qa, covers, count, accent, false); }

        internal static Bitmap RenderIdlePanelMode(int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent)
        { return RenderIdlePanelStyled(w, h, title, artist, time, qt, qa, covers, count, accent, transparent, 0); }

        internal static Bitmap RenderIdlePanelStyled(int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent, int badgeKind)
        { return RenderIdlePanelQueue(w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, false); }

        internal static Rectangle IdleQueueCell(int w, int h, int index, bool dozen)
        { return IdleQueueCellCount(w, h, index, dozen ? 12 : 3, dozen); }

        internal static Rectangle IdleQueueCellCount(int w, int h, int index, int count, bool dozen)
        {
            int split = (int)(w * .52f), visible = Math.Max(1, Math.Min(count, 12));
            int cols = !dozen ? 1 : w >= 1500 ? Math.Min(3, visible) :
                visible <= 3 ? 1 : visible <= 8 && h >= 260 ? 2 : visible <= 6 ? 2 : 3;
            int rows = dozen ? (visible + cols - 1) / cols : 3, gap = 8;
            int cw = (w - split - 24 - (cols - 1) * gap) / cols;
            int rh = (h - 78) / rows;
            return new Rectangle(split + (index % cols) * (cw + gap), 42 + (index / cols) * rh, cw, rh - 5);
        }

        internal static Rectangle IdleQueueThumb(Rectangle cell)
        {
            int side = Math.Max(12, Math.Min(Math.Min(cell.Height - 6, cell.Width / 3), 64));
            return new Rectangle(cell.X + 5, cell.Y + (cell.Height - side) / 2, side, side);
        }

        internal static Bitmap RenderIdlePanelQueue(int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen)
        {
            int progressStart;
            return RenderIdlePanelLayout(w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, dozen, out progressStart);
        }

        internal static Bitmap RenderIdlePanelLayout(int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen, out int progressStart)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                Color.FromArgb(transparent ? 0 : 232, 12, 19, 42), Color.FromArgb(transparent ? 0 : 248, 7, 12, 29), 90f))
            using (Pen top = new Pen(Color.FromArgb(155, Lighten(accent, .45)), 2))
            using (Font small = new Font("Trebuchet MS", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font normal = new Font("Trebuchet MS", 20 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font big = new Font("Trebuchet MS", Math.Max(22, Math.Min(36 * scale, w / 30f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font clock = new Font("Tahoma", 17 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(240, 245, 255)))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(157, 179, 209)))
            using (SolidBrush color = new SolidBrush(Lighten(accent, .5)))
            using (SolidBrush card = new SolidBrush(Color.FromArgb(transparent ? 65 : 160, 27, 40, 67)))
            using (StringFormat line = new StringFormat())
            using (StringFormat wrap = new StringFormat())
            using (StringFormat numberFormat = StringFormat.GenericTypographic)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                line.Trimming = StringTrimming.EllipsisCharacter; line.FormatFlags = StringFormatFlags.NoWrap;
                wrap.Trimming = StringTrimming.EllipsisWord; wrap.FormatFlags = StringFormatFlags.LineLimit;
                g.FillRectangle(bg, 0, 0, w, h); g.DrawLine(top, 0, 1, w, 1);
                int split = (int)(w * .52f);
                Rectangle cover = GpuPanel.IdleCoverRect(w, h);
                // Sin tarjeta bajo el cubo: se ve directamente el fondo del estado.
                int tx = cover.Right + 20, tw = Math.Max(20, split - tx - 24);
                g.DrawString("A H O R A   S U E N A", small, color, new RectangleF(24, 12, split - 40, 26 * scale), line);
                float titleH = Math.Min(90, h * .28f), artistY = 56 + titleH + 6;
                g.DrawString(title ?? "", big, white, new RectangleF(tx, 56, tw, titleH), wrap);
                g.DrawString(artist ?? "", normal, color, new RectangleF(tx, artistY, tw, Math.Max(22, h - 82 - artistY)), line);
                if (badgeKind != 0)
                {
                    Rectangle badge = GpuPanel.IdleBadgeRect(w, h);
                    Color metal = badgeKind == 2 ? Color.FromArgb(255, 221, 132) : Color.FromArgb(218, 237, 255);
                    using (SolidBrush tint = new SolidBrush(Color.FromArgb(42, metal)))
                    using (SolidBrush ink = new SolidBrush(metal))
                    using (Pen rim = new Pen(Color.FromArgb(145, metal)))
                    using (GraphicsPath shape = RoundedPath(badge, 8))
                    {
                        g.FillPath(tint, shape); g.DrawPath(rim, shape);
                        using (StringFormat centered = new StringFormat(line))
                        {
                            centered.Alignment = StringAlignment.Center; centered.LineAlignment = StringAlignment.Center;
                            g.DrawString("MEJORADA", small, ink, badge, centered);
                        }
                    }
                }
                using (StringFormat timeFormat = new StringFormat(line))
                {
                    timeFormat.Alignment = StringAlignment.Near;
                    timeFormat.LineAlignment = StringAlignment.Center;
                    int timeX = cover.Right + 16;
                    int timeWidth = (int)Math.Ceiling(g.MeasureString(time ?? "", clock, int.MaxValue, timeFormat).Width) + 2;
                    // Dejar al menos 80 px de progreso incluso en ventanas estrechas.
                    timeWidth = Math.Max(1, Math.Min(timeWidth, w - timeX - 112));
                    progressStart = timeX + timeWidth + 8;
                    g.DrawString(time ?? "", clock, white, new RectangleF(timeX, h - 32, timeWidth, 28), timeFormat);
                }
                string heading = "A CONTINUACION  /  " + count + (count == 1 ? " CANCION" : " CANCIONES");
                g.DrawString(heading, small, color, new RectangleF(split, 12, w - split - 24, 26 * scale), line);
                if (count == 0)
                    g.DrawString("La siguiente cancion la eliges tu", normal, muted, new RectangleF(split + 14, 62, w - split - 45, 64));
                Rectangle sample = IdleQueueCellCount(w, h, 0, qt.Length, dozen);
                using (Font queueTitle = new Font("Trebuchet MS", dozen ? Math.Max(9, Math.Min(21 * scale, (sample.Height - 8) * .52f)) : normal.Size, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font queueArtist = new Font("Trebuchet MS", dozen ? Math.Max(8, Math.Min(16 * scale, (sample.Height - 8) * .40f)) : small.Size, FontStyle.Bold, GraphicsUnit.Pixel))
                for (int i = 0; i < qt.Length && i < (dozen ? 12 : 3); i++)
                {
                    Rectangle r = IdleQueueCellCount(w, h, i, qt.Length, dozen);
                    using (GraphicsPath shape = RoundedPath(r, Math.Min(10, r.Height / 3))) g.FillPath(card, shape);
                    Rectangle thumb = IdleQueueThumb(r);
                    if (i < covers.Length && covers[i] != null) g.DrawImage(covers[i], thumb);
                    else g.FillRectangle(color, thumb);
                    int textX = thumb.Right + 5;
                    int numberWidth = (int)Math.Ceiling(g.MeasureString("12", queueArtist).Width) + 4;
                    bool compact = r.Width < 180;
                    int textW = Math.Max(12, r.Right - textX - (compact ? 5 : numberWidth + 8));
                    float titleHeight = queueTitle.GetHeight(g);
                    g.DrawString(qt[i] ?? "", queueTitle, white, new RectangleF(textX, r.Y + 2, textW, titleHeight), line);
                    g.DrawString(i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, new RectangleF(textX, r.Y + 2 + titleHeight, textW, r.Height - titleHeight - 2), line);
                    if (compact)
                    {
                        float numberHeight = (float)Math.Ceiling(queueArtist.GetHeight(g)) + 2;
                        RectangleF label = new RectangleF(thumb.Left, thumb.Bottom - numberHeight, numberWidth, numberHeight);
                        using (SolidBrush shade = new SolidBrush(Color.FromArgb(210, 8, 12, 24))) g.FillRectangle(shade, label);
                        g.DrawString((i + 1).ToString("00"), queueArtist, white, label.Location, numberFormat);
                    }
                    else g.DrawString((i + 1).ToString("00"), queueArtist, color, new RectangleF(r.Right - numberWidth - 3, r.Y + 3, numberWidth, r.Height - 4), line);
                }
            }
            return bitmap;
        }
    }
}
#endif

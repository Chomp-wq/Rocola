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
        private Bitmap BuildIdlePanel()
        {
            if (nowPlaying == null || videoOn) return null;
            int w = ClientSize.Width, h = GpuPanel.IdlePanelHeight(ClientSize.Height);
            if (w < 480 || h < 140) return null;
            int now = Environment.TickCount;
            if (idlePanelBmp != null && idlePanelBmp.Width == w && idlePanelBmp.Height == h &&
                unchecked(now - idlePanelCheck) < 400) return idlePanelBmp;
            idlePanelCheck = now;
            string title, artist; nowPlaying.TituloArtista(out title, out artist);
            string[] qt = new string[Math.Min(3, queue.Count)], qa = new string[qt.Length];
            Image[] covers = new Image[qt.Length];
            StringBuilder sig = new StringBuilder();
            sig.Append(w).Append('|').Append(h).Append('|').Append(nowPlaying.PathKey()).Append('|')
                .Append(title).Append('|').Append(artist).Append('|').Append(queue.Count).Append('|').Append(bgAccent.ToArgb());
            string time = lblTime != null ? lblTime.Text : "";
            sig.Append('|').Append(time).Append('|').Append(estadoTransparente).Append('|').Append(iaBadgeKind);
            for (int i = 0; i < qt.Length; i++)
            {
                queue[i].TituloArtista(out qt[i], out qa[i]);
                covers[i] = queue[i].Disc != null ? LoadCoverAsync(queue[i].Disc) : null;
                sig.Append('|').Append(queue[i].PathKey()).Append('|').Append(qt[i]).Append('|').Append(qa[i])
                    .Append('|').Append(covers[i] != null ? covers[i].GetHashCode() : 0);
            }
            string key = sig.ToString();
            if (idlePanelBmp != null && key == idlePanelSig) return idlePanelBmp;
            Bitmap next = RenderIdlePanelStyled(w, h, title, artist, time, qt, qa, covers, queue.Count, bgAccent, estadoTransparente, iaBadgeKind);
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
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                line.Trimming = StringTrimming.EllipsisCharacter; line.FormatFlags = StringFormatFlags.NoWrap;
                wrap.Trimming = StringTrimming.EllipsisWord; wrap.FormatFlags = StringFormatFlags.LineLimit;
                g.FillRectangle(bg, 0, 0, w, h); g.DrawLine(top, 0, 1, w, 1);
                int split = (int)(w * .52f);
                Rectangle cover = GpuPanel.IdleCoverRect(w, h);
                using (GraphicsPath shape = RoundedPath(cover, 12)) g.FillPath(card, shape);
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
                g.DrawString(time ?? "", clock, white, new RectangleF(24, h - 32, GpuPanel.IdleProgressLeft(h) - 36, 28), line);
                string heading = "A CONTINUACION  /  " + count + (count == 1 ? " CANCION" : " CANCIONES");
                g.DrawString(heading, small, color, new RectangleF(split, 12, w - split - 24, 26 * scale), line);
                if (count == 0)
                    g.DrawString("La siguiente cancion la eliges tu", normal, muted, new RectangleF(split + 14, 62, w - split - 45, 64));
                int rh = (h - 70) / 3;
                for (int i = 0; i < qt.Length && i < 3; i++)
                {
                    Rectangle r = new Rectangle(split, 42 + i * rh, w - split - 24, rh - 7);
                    using (GraphicsPath shape = RoundedPath(r, 10)) g.FillPath(card, shape);
                    int side = Math.Max(16, r.Height - 16);
                    Rectangle thumb = new Rectangle(r.X + 12, r.Y + 8, side, side);
                    if (covers[i] != null) g.DrawImage(covers[i], thumb);
                    else g.FillRectangle(color, thumb);
                    int textX = thumb.Right + 12, textW = Math.Max(12, r.Right - textX - 42);
                    float rowTitleH = 25 * scale;
                    g.DrawString(qt[i] ?? "", normal, white, new RectangleF(textX, r.Y + 3, textW, rowTitleH), line);
                    g.DrawString(qa[i] ?? "", small, muted, new RectangleF(textX, r.Y + 3 + rowTitleH, textW, 21 * scale), line);
                    g.DrawString((i + 1).ToString("00"), small, color, new RectangleF(r.Right - 36, r.Y + 14, 34, 25 * scale), line);
                }
            }
            return bitmap;
        }
    }
}
#endif

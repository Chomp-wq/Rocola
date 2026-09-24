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
        private Bitmap idleClockUnderlay;
        private string idlePanelTime;
        private int idlePanelRevision;
        private int idlePanelProgressStart;

        // EL PANEL SE REHACE CUANDO ALGO CAMBIA, NO CADA 400 ms MIRANDO A VER.
        //
        // Antes se rearmaba la firma -titulo, artista, y para cada una de las doce de la cola su ruta, su
        // texto y su caratula- dos veces y media por segundo, para casi siempre descubrir que nada habia
        // cambiado. No costaba casi nada -0,005 ms, medido con doce en la cola-, pero tenia un precio
        // peor que el del procesador: un cambio tardaba hasta 400 ms en verse. Pones una cancion y el
        // panel va detras. Ese, y no el ahorro, es el motivo de este cambio.
        //
        // Ahora cada sitio que cambia algo del panel avisa, y la firma solo se rearma cuando hay aviso.
        // LA FIRMA SE QUEDA como segundo filtro, y eso es a proposito: un aviso de mas cuesta 0,005 ms y
        // no un redibujo de 1,70. O sea que avisar de sobra sale barato y lo unico caro es olvidarse, que
        // es justo al reves de como suele salir esto. Lo vigila la bateria 'panelreposo': cambia cada cosa
        // que el panel ensena, una por una, y exige que se entere.
        private int idlePanelDatos;
        private int idlePanelDatosDe = -1;
        private Color idlePanelAcento = Color.Empty;
        partial void CambioElPanelDeReposo() { unchecked { idlePanelDatos++; } }

        // EL COLOR DEL GENERO ES OTRA COSA: no cambia, SE DESLIZA. En cada latido -24 por segundo- se
        // acerca un 16% al color de la cancion nueva, y tarda unos 24 latidos en llegar. Avisar en cuanto
        // se mueve un punto seria rehacer el panel en los 24 seguidos: 41 ms, medido.
        //
        // Asi que avisa cuando se ha movido lo bastante para VERSE. Seis de 255 es un 2%: por debajo de
        // eso, dos paneles seguidos son el mismo panel para cualquiera que lo mire. Con el umbral la
        // transicion baja a 14 redibujos, 24 ms.
        //
        // Y AUN ASI ESTO SALE MAS CARO QUE ANTES, que hay que decirlo: con el reloj de los 400 ms la
        // misma transicion costaba unos 3 redibujos -5 ms-, porque la mayoria de los latidos ni se
        // miraban. Son 19 ms de mas UNA VEZ POR CANCION -cada tres o cuatro minutos- a cambio de que se
        // vea al momento en vez de hasta 400 ms tarde. El numero lo vuelve a sacar la bateria
        // 'panelreposo' cada vez que corre, para que no se pierda.
        private const int AcentoPaso = 6;
        partial void CambioElAcentoDelPanel(Color nuevo)
        {
            if (idlePanelAcento != Color.Empty
                && Math.Abs(nuevo.R - idlePanelAcento.R) < AcentoPaso
                && Math.Abs(nuevo.G - idlePanelAcento.G) < AcentoPaso
                && Math.Abs(nuevo.B - idlePanelAcento.B) < AcentoPaso) return;
            idlePanelAcento = nuevo;
            CambioElPanelDeReposo();
        }
        private Bitmap BuildIdlePanel()
        {
            if (nowPlaying == null || videoOn) return null;
            if (gpuPanel != null && idlePanelBmp != null) gpuPanel.IdleProgressStart = idlePanelProgressStart;
            int w = ClientSize.Width, h = GpuPanel.IdlePanelHeight(ClientSize.Height);
            if (w < 480 || h < 140) return null;
            if (idlePanelBmp != null && idlePanelBmp.Width == w && idlePanelBmp.Height == h &&
                idlePanelDatos == idlePanelDatosDe)
            {
                // Nada de lo que el panel ensena ha cambiado. Lo unico que corre solo es el reloj, y ese
                // se parchea aparte sin rehacer nada mas.
                ParcheaElReloj(w, h);
                return idlePanelBmp;
            }
            idlePanelDatosDe = idlePanelDatos;
            string title, artist; nowPlaying.TituloArtista(out title, out artist);
            string[] qt = new string[Math.Min(colaDoce ? 12 : 3, queue.Count)], qa = new string[qt.Length];
            Image[] covers = new Image[qt.Length];
            StringBuilder sig = new StringBuilder();
            sig.Append(w).Append('|').Append(h).Append('|').Append(nowPlaying.PathKey()).Append('|')
                .Append(title).Append('|').Append(artist).Append('|').Append(queue.Count).Append('|').Append(bgAccent.ToArgb());
            string time = TextoRelojReposo(reader);   // con "Lo que suena, en grande", tambien cuanto falta (SuenaVivoGpu.cs)
            sig.Append('|').Append(estadoTransparente).Append('|').Append(iaBadgeKind);
            for (int i = 0; i < qt.Length; i++)
            {
                queue[i].TituloArtista(out qt[i], out qa[i]);
                covers[i] = queue[i].Disc != null ? LoadCoverAsync(queue[i].Disc) : null;
                sig.Append('|').Append(queue[i].PathKey()).Append('|').Append(qt[i]).Append('|').Append(qa[i])
                    .Append('|').Append(covers[i] != null ? covers[i].GetHashCode() : 0);
            }
            sig.Append('|').Append(colaDoce);
            // Lo que suena, en grande (SuenaVivoGpu.cs): el genero y lo que falta para cada una de la cola.
            ReposoGrande extra = ExtraReposo(w, h, qt.Length);
            if (extra != null)
            {
                sig.Append("|G|").Append(extra.Genero).Append('|').Append(extra.GeneroColor.ToArgb());
                for (int i = 0; i < extra.Minutos.Length; i++) sig.Append('|').Append(extra.Minutos[i]);
            }
            string key = sig.ToString();
            if (idlePanelBmp != null && key == idlePanelSig) { ParcheaElReloj(w, h); return idlePanelBmp; }
            int progressStart;
            Bitmap next = RenderIdlePanelGrande(w, h, title, artist, "", qt, qa, covers, queue.Count, bgAccent, estadoTransparente, iaBadgeKind, colaDoce, extra, out progressStart);
            reposoUltimo = extra;
            if (gpuPanel != null) gpuPanel.IdleCorre = (extra != null) ? extra.Corre : 0;
            if (extra != null) { extra.Titulo = title; extra.W = w; extra.H = h; }   // el titulo que se desliza lo hornea la tarjeta al pedirlo
            if (idleClockUnderlay != null) { idleClockUnderlay.Dispose(); idleClockUnderlay = null; }
            idleClockUnderlay = next.Clone(new Rectangle(0, h - 34, w, 34), PixelFormat.Format32bppPArgb);
            progressStart = UpdateIdleClock(next, idleClockUnderlay, time);
            idlePanelTime = time; idlePanelRevision++;
            if (gpuPanel != null) { gpuPanel.IdlePanelVersion = idlePanelRevision; gpuPanel.IdlePanelDirty = Rectangle.Empty; }
            idlePanelProgressStart = progressStart;
            if (gpuPanel != null) gpuPanel.IdleProgressStart = progressStart;
            Bitmap old = idlePanelBmp; idlePanelBmp = next; idlePanelSig = key;
            if (old != null) old.Dispose();
            return idlePanelBmp;
        }

        internal static int DrawIdleClock(Graphics g, int w, int h, string time)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            using (Font clock = new Font("Tahoma", 17 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(240, 245, 255)))
            using (StringFormat format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.Alignment = StringAlignment.Near; format.LineAlignment = StringAlignment.Center;
                int x = GpuPanel.IdleCoverRect(w, h).Right + 16;
                int width = (int)Math.Ceiling(g.MeasureString(time ?? "", clock, int.MaxValue, format).Width) + 2;
                width = Math.Max(1, Math.Min(width, w - x - 112));
                g.DrawString(time ?? "", clock, white, new RectangleF(x, h - 32, width, 28), format);
                return x + width + 8;
            }
        }
        // SOLO EL RELOJ. Es lo unico del panel que cambia sin que nadie lo toque, asi que no entra en el
        // sistema de avisos: se mira su texto -una comparacion de cadenas- y si cambio se repinta la franja
        // de abajo y nada mas. Rehacer el panel entero por un digito costaria 1,70 ms cada segundo.
        private void ParcheaElReloj(int w, int h)
        {
            string time = TextoRelojReposo(reader);
            if (idleClockUnderlay == null || idlePanelBmp == null || idlePanelTime == time) return;
            idlePanelProgressStart = UpdateIdleClock(idlePanelBmp, idleClockUnderlay, time);
            idlePanelTime = time; idlePanelRevision++;
            if (gpuPanel != null)
            {
                gpuPanel.IdlePanelVersion = idlePanelRevision;
                gpuPanel.IdlePanelDirty = new Rectangle(0, h - 34, w, 34);
                gpuPanel.IdleProgressStart = idlePanelProgressStart;
            }
        }

        internal static int UpdateIdleClock(Bitmap panel, Bitmap underlay, string time)
        {
            using (Graphics g = Graphics.FromImage(panel))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(underlay, 0, panel.Height - underlay.Height);
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                return DrawIdleClock(g, panel.Width, panel.Height, time);
            }
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
            // Una cola corta no debe estirar las tarjetas hasta llenar la pantalla.
            // Mantener la rejilla compacta y anclada arriba; las pantallas pequenas
            // conservan el reparto que deja sitio para todas sus miniaturas.
            if (dozen && w >= 1500) { cw = Math.Min(cw, 520); rh = Math.Min(rh, 84); }
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
        { return RenderIdlePanelGrande(w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, dozen, null, out progressStart); }

        // La letra del titulo del panel (tambien la del titulo que se desliza, SuenaVivoGpu.cs).
        internal static Font FuenteTituloReposo(int w, int h)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            return new Font("Trebuchet MS", Math.Max(22, Math.Min(36 * scale, w / 30f)), FontStyle.Bold, GraphicsUnit.Pixel);
        }

        // 'extra' es lo de "Lo que suena, en grande" (SuenaVivoGpu.cs); con null, el panel de siempre.
        internal static Bitmap RenderIdlePanelGrande(int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen, ReposoGrande extra, out int progressStart)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                Color.FromArgb(transparent ? 0 : 232, 12, 19, 42), Color.FromArgb(transparent ? 0 : 248, 7, 12, 29), 90f))
            using (Pen top = new Pen(Color.FromArgb(155, Lighten(accent, .45)), 2))
            using (Font small = new Font("Trebuchet MS", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font normal = new Font("Trebuchet MS", 20 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font big = FuenteTituloReposo(w, h))
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
                g.FillRectangle(bg, 0, 0, w, h);
                g.DrawLine(top, 0, GpuPanel.StatusEdgeOffset, w, GpuPanel.StatusEdgeOffset);
                int split = (int)(w * .52f);
                Rectangle cover = GpuPanel.IdleCoverRect(w, h);
                // Sin tarjeta bajo el cubo: se ve directamente el fondo del estado.
                int tx = cover.Right + 20 + (extra != null ? extra.Corre : 0), tw = Math.Max(20, split - tx - 24);
                g.DrawString("A H O R A   S U E N A", small, color, new RectangleF(24, 12, split - 40, 26 * scale), line);
                float titleH = Math.Min(90, h * .28f), artistY = 56 + titleH + 6;
                if (extra == null)
                {
                    g.DrawString(title ?? "", big, white, new RectangleF(tx, 56, tw, titleH), wrap);
                    g.DrawString(artist ?? "", normal, color, new RectangleF(tx, artistY, tw, Math.Max(22, h - 82 - artistY)), line);
                }
                else PintaTituloGrande(g, extra, title, artist, big, normal, small, white, color, line, tx, tw);   // en una linea
                if (badgeKind != 0)
                {
                    Rectangle badge = GpuPanel.IdleBadgeRectCorrido(w, h, extra != null ? extra.Corre : 0);
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
                progressStart = DrawIdleClock(g, w, h, time);
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
                    float textY = dozen && w >= 1500 ? r.Y + Math.Max(2, (r.Height - titleHeight - queueArtist.GetHeight(g)) * .5f) : r.Y + 2;
                    g.DrawString(qt[i] ?? "", queueTitle, white, new RectangleF(textX, textY, textW, titleHeight), line);
                    string enMin = (extra != null) ? TextoMinutosCola(extra, i) : "";   // "en ~6 min" (SuenaVivoGpu.cs)
                    if (enMin.Length == 0)
                        g.DrawString(i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, new RectangleF(textX, textY + titleHeight, textW, r.Bottom - textY - titleHeight - 2), line);
                    else PintaMinutosCola(g, enMin, i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, compact, textX, textY + titleHeight, textW, r.Bottom - textY - titleHeight - 2, line);
                    if (compact)
                    {
                        float numberHeight = (float)Math.Ceiling(queueArtist.GetHeight(g)) + 2;
                        RectangleF label = new RectangleF(thumb.Left, thumb.Bottom - numberHeight, numberWidth, numberHeight);
                        using (SolidBrush shade = new SolidBrush(Color.FromArgb(210, 8, 12, 24))) g.FillRectangle(shade, label);
                        g.DrawString((i + 1).ToString("00"), queueArtist, white, label.Location, numberFormat);
                    }
                    else g.DrawString((i + 1).ToString("00"), queueArtist, color, new RectangleF(r.Right - numberWidth - 3, textY, numberWidth, r.Bottom - textY - 2), line);
                }
            }
            return bitmap;
        }
    }
}
#endif

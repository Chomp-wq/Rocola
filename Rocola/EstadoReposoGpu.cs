#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace Rocola
{
    // El texto solo cambia al pasar de segundo, aunque la fuente se sigue leyendo en cada cuadro.
    // Se conserva TextoFaltan como unica autoridad del formato y del limite de fin de cancion.
    internal sealed class CacheRelojReposo
    {
        // El lector activo ya pertenece a Form1. La cache no debe retener un lector cerrado
        // (ni su MP3 en memoria) si el panel deja de pedir el reloj al terminar la musica.
        private WeakReference fuente;
        private string texto;
        private string signoNegativo;
        private long segundoActual, segundoTotal;
        private int minutoActual, minutoTotal;

        internal void Vaciar() { if (fuente != null) fuente.Target = null; texto = null; }

        internal string Texto(object origen, TimeSpan actual, TimeSpan total, string siNo)
        {
            if (total.TotalSeconds <= 0 || actual.TotalSeconds < 0)
            { Vaciar(); return siNo; }
            if (actual > total) actual = total;
            long sa = actual.Ticks / TimeSpan.TicksPerSecond, st = total.Ticks / TimeSpan.TicksPerSecond;
            // Fmt usa TotalMinutes: conservar tambien esa cuenta, incluso para duraciones extremas
            // donde el double puede redondear cerca del siguiente minuto antes que los ticks.
            int ma = (int)actual.TotalMinutes, mt = (int)total.TotalMinutes;
            string signo = ma < 0 || mt < 0 ? System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NegativeSign : null;
            if (texto != null && fuente != null && ReferenceEquals(fuente.Target, origen) && sa == segundoActual && st == segundoTotal &&
                ma == minutoActual && mt == minutoTotal && signo == signoNegativo) return texto;
            string nuevo = Form1.TextoFaltan(actual, total, siNo);
            if (fuente == null) fuente = new WeakReference(origen); else fuente.Target = origen;
            segundoActual = sa; segundoTotal = st; minutoActual = ma; minutoTotal = mt; signoNegativo = signo; texto = nuevo;
            return nuevo;
        }
    }

    public partial class Form1
    {
        private Bitmap idlePanelBmp;
        private string idlePanelSig;
        private Bitmap idleClockUnderlay;
        private string idlePanelTime;
        private int idlePanelRevision;
        private int idlePanelProgressStart;
        private CacheRelojReposo idleClockTexto;

        private string TextoRelojReposoCache(IAudioSource r)
        {
            string corto = (lblTime != null) ? lblTime.Text : "";
            if (!suenaGrande || r == null || nowPlaying == null || nowPlaying.IsVideo)
            { if (idleClockTexto != null) idleClockTexto.Vaciar(); return corto; }
            TimeSpan cur, tot;
            try { cur = r.CurrentTime; tot = r.TotalTime; }
            catch { if (idleClockTexto != null) idleClockTexto.Vaciar(); return corto; }
            if (idleClockTexto == null) idleClockTexto = new CacheRelojReposo();
            return idleClockTexto.Texto(r, cur, tot, corto);
        }

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
            if (estadoColorDirecto) nuevo = bgAccentTarget;
            if (idlePanelAcento != Color.Empty
                && Math.Abs(nuevo.R - idlePanelAcento.R) < AcentoPaso
                && Math.Abs(nuevo.G - idlePanelAcento.G) < AcentoPaso
                && Math.Abs(nuevo.B - idlePanelAcento.B) < AcentoPaso) return;
            idlePanelAcento = nuevo;
            CambioElPanelDeReposo();
        }
        // Las precargas de otras paginas no cambian las portaditas del estado ni de su cola.
        private bool PortadasVisiblesEnCola(string[] keys, int maximum)
        {
            int count = Math.Min(maximum, queue.Count);
            for (int i = 0; i < count; i++)
            {
                Album disc = queue[i] != null ? queue[i].Disc : null;
                if (disc == null || string.IsNullOrEmpty(disc.CoverPath)) continue;
                for (int k = 0; k < keys.Length; k++)
                    if (string.Equals(keys[k], disc.CoverPath, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
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
            Color acento = estadoColorDirecto ? bgAccentTarget : bgAccent;
            string title, artist; nowPlaying.TituloArtista(out title, out artist);
            title = TituloSinPista(title);
            string[] qt = new string[Math.Min(colaDoce ? 12 : 3, queue.Count)], qa = new string[qt.Length];
            Image[] covers = new Image[qt.Length];
            StringBuilder sig = new StringBuilder();
            sig.Append(w).Append('|').Append(h).Append('|').Append(nowPlaying.PathKey()).Append('|')
                .Append(title).Append('|').Append(artist).Append('|').Append(queue.Count).Append('|').Append(acento.ToArgb());
            string time = TextoRelojReposoCache(reader);   // mismo formato de SuenaVivoGpu.cs, sin volver a formar cadenas por cuadro
            sig.Append('|').Append(estadoTransparente).Append('|').Append(EstadoOpacoActivo).Append('|').Append(iaBadgeKind);
            for (int i = 0; i < qt.Length; i++)
            {
                queue[i].TituloArtista(out qt[i], out qa[i]);
                qt[i] = TituloSinPista(qt[i]);
                covers[i] = queue[i].Disc != null ? LoadCoverAsync(queue[i].Disc) : null;
                sig.Append('|').Append(queue[i].PathKey()).Append('|').Append(qt[i]).Append('|').Append(qa[i])
                    .Append('|').Append(covers[i] != null ? covers[i].GetHashCode() : 0);
            }
            sig.Append('|').Append(colaDoce).Append('|').Append(textosDeslizantes).Append('|').Append(InterfazTramada);
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
            Bitmap next;
            using (CapturaTexto(1))
                next = RenderIdlePanelEn(idlePanelBmp, w, h, title, artist, "", qt, qa, covers, queue.Count, acento,
                    estadoTransparente && !EstadoOpacoActivo, iaBadgeKind, colaDoce, extra, EstadoOpacoActivo, out progressStart);
            reposoUltimo = extra;
            if (extra != null) { extra.Titulo = title; extra.W = w; extra.H = h; }   // el titulo que se desliza lo hornea la tarjeta al pedirlo
            if (idleClockUnderlay == null || idleClockUnderlay.Width != w)
            {
                if (idleClockUnderlay != null) idleClockUnderlay.Dispose();
                idleClockUnderlay = new Bitmap(w, 34, PixelFormat.Format32bppPArgb);
            }
            CopiaRegionReposo(next, new Rectangle(0, h - 34, w, 34), idleClockUnderlay, Point.Empty);
            progressStart = UpdateIdleClock(next, idleClockUnderlay, time);
            idlePanelTime = time; idlePanelRevision++;
            if (gpuPanel != null) { gpuPanel.IdlePanelVersion = idlePanelRevision; gpuPanel.IdlePanelDirty = Rectangle.Empty; }
            idlePanelProgressStart = progressStart;
            if (gpuPanel != null) gpuPanel.IdleProgressStart = progressStart;
            Bitmap old = idlePanelBmp; idlePanelBmp = next; idlePanelSig = key;
            if (old != null && !ReferenceEquals(old, next)) old.Dispose();
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
            string time = TextoRelojReposoCache(reader);
            if (idleClockUnderlay == null || idlePanelBmp == null || idlePanelTime == time) return;
            Rectangle zona;
            idlePanelProgressStart = UpdateIdleClockRegion(idlePanelBmp, idleClockUnderlay, time, idlePanelProgressStart, out zona);
            idlePanelTime = time; idlePanelRevision++;
            if (gpuPanel != null)
            {
                gpuPanel.IdlePanelVersion = idlePanelRevision;
                gpuPanel.IdlePanelDirty = zona;
                gpuPanel.IdleProgressStart = idlePanelProgressStart;
            }
        }

        // Solo los digitos, con margen para el antialias y para borrar un texto anterior mas largo.
        internal static int UpdateIdleClockRegion(Bitmap panel, Bitmap underlay, string time, int previousRight, out Rectangle dirty)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, panel.Height / 260f));
            int x = GpuPanel.IdleCoverRect(panel.Width, panel.Height).Right + 16;
            using (Graphics g = Graphics.FromImage(panel))
            using (Font font = new Font("Tahoma", 17 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                int width = (int)Math.Ceiling(g.MeasureString(time ?? "", font, int.MaxValue, sf).Width) + 2;
                width = Math.Max(1, Math.Min(width, panel.Width - x - 112));
                int right = Math.Max(previousRight, x + width + 8) + 3;
                dirty = Rectangle.Intersect(new Rectangle(0, 0, panel.Width, panel.Height),
                    new Rectangle(Math.Max(0, x - 3), panel.Height - 34, Math.Max(1, right - x + 3), 34));
            }
            CopiaRegionReposo(underlay, new Rectangle(dirty.X, dirty.Y - panel.Height + underlay.Height, dirty.Width, dirty.Height), panel, dirty.Location);
            using (Graphics g = Graphics.FromImage(panel))
            {
                g.SetClip(dirty);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                return DrawIdleClock(g, panel.Width, panel.Height, time);
            }
        }

        [ThreadStatic] private static byte[] filaReposo;
        internal static void CopiaRegionReposo(Bitmap source, Rectangle area, Bitmap target, Point position)
        {
            int bytes = checked(area.Width * 4);
            if (filaReposo == null || filaReposo.Length < bytes) filaReposo = new byte[bytes];
            BitmapData src = source.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                BitmapData dst = target.LockBits(new Rectangle(position, area.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int y = 0; y < area.Height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(new IntPtr(src.Scan0.ToInt64() + (long)y * src.Stride), filaReposo, 0, bytes);
                        System.Runtime.InteropServices.Marshal.Copy(filaReposo, 0, new IntPtr(dst.Scan0.ToInt64() + (long)y * dst.Stride), bytes);
                    }
                }
                finally { target.UnlockBits(dst); }
            }
            finally { source.UnlockBits(src); }
        }

        internal static int UpdateIdleClock(Bitmap panel, Bitmap underlay, string time)
        {
            CopiaRegionReposo(underlay, new Rectangle(0, 0, underlay.Width, underlay.Height), panel, new Point(0, panel.Height - underlay.Height));
            using (Graphics g = Graphics.FromImage(panel))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                return DrawIdleClock(g, panel.Width, panel.Height, time);
            }
        }

        // Textura exclusiva GPU: sus textos no se escalan durante la animacion.
        // El cubo actual se dibuja aparte como geometria y crece con el panel.

        internal static Rectangle IdleQueueCell(int w, int h, int index, bool dozen)
        { return IdleQueueCellCount(w, h, index, dozen ? 12 : 3, dozen); }

        internal static Rectangle IdleQueueCellCount(int w, int h, int index, int count, bool dozen)
        {
            int split = (int)(w * .52f), visible = Math.Max(1, Math.Min(count, 12));
            int cols = !dozen ? 1 : w >= 1500 ? Math.Min(3, visible) :
                visible <= 3 ? 1 : visible <= 8 && h >= 260 ? 2 : visible <= 6 ? 2 : h >= 280 ? 2 : 3;
            int rows = dozen ? (visible + cols - 1) / cols : 3, gap = 8;
            int cw = (w - split - 24 - (cols - 1) * gap) / cols;
            int rh = (h - 78) / rows;
            // Una cola corta no debe estirar las tarjetas hasta llenar la pantalla.
            // El limite vale tambien para una o dos canciones en pantallas pequenas:
            // estirar esa unica fila separaba la miniatura (centrada) del texto (arriba).
            // Solo se limita; nunca se agrandan las filas de una cola larga.
            if (dozen) { cw = Math.Min(cw, 520); rh = Math.Min(rh, 84); }
            return new Rectangle(split + (index % cols) * (cw + gap), 42 + (index / cols) * rh, cw, rh - 5);
        }

        internal static Rectangle IdleQueueThumb(Rectangle cell)
        {
            int side = Math.Max(12, Math.Min(Math.Min(cell.Height - 6, cell.Width / 3), 64));
            return new Rectangle(cell.X + 5, cell.Y + (cell.Height - side) / 2, side, side);
        }

        internal static RectangleF IdleQueueTextBlock(Rectangle cell, float titleHeight, float detailHeight, int numberWidth)
        {
            Rectangle thumb = IdleQueueThumb(cell);
            int x = thumb.Right + 5;
            int width = Math.Max(12, cell.Right - x - (cell.Width < 180 ? 5 : numberWidth + 8));
            float height = titleHeight + detailHeight;
            return new RectangleF(x, cell.Y + Math.Max(2f, (cell.Height - height) * .5f), width, height);
        }

        internal static RectangleF IdleQueueNumberRect(Rectangle cell, float numberHeight)
        {
            Rectangle thumb = IdleQueueThumb(cell);
            float height = Math.Min(thumb.Height, (float)Math.Ceiling(numberHeight) + 2f);
            return new RectangleF(thumb.Left, thumb.Bottom - height, thumb.Width, height);
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
        { return RenderIdlePanelEn(null, w, h, title, artist, time, qt, qa, covers, count, accent, transparent, badgeKind, dozen, extra, false, out progressStart); }

        internal static Bitmap RenderIdlePanelEn(Bitmap bitmap, int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen,
            ReposoGrande extra, bool opaque, out int progressStart)
        {
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            if (bitmap == null || bitmap.Width != w || bitmap.Height != h) bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Brush bg = DegradadoV(new Rectangle(0, 0, w, h),
                Color.FromArgb(opaque ? 255 : transparent ? 0 : 232, 12, 19, 42), Color.FromArgb(opaque ? 255 : transparent ? 0 : 248, 7, 12, 29)))   // C4 (PulidoGpu.cs)
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
                g.Clear(Color.Transparent); // el mapa se reutiliza: no acumular texto/alfa de la cancion anterior
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                line.Trimming = StringTrimming.EllipsisCharacter; line.FormatFlags = StringFormatFlags.NoWrap;
                wrap.Trimming = StringTrimming.EllipsisWord; wrap.FormatFlags = StringFormatFlags.LineLimit;
                g.FillRectangle(bg, 0, 0, w, h);
                g.DrawLine(top, 0, GpuPanel.StatusEdgeOffset, w, GpuPanel.StatusEdgeOffset);
                int split = (int)(w * .52f);
                Rectangle cover = GpuPanel.IdleCoverRect(w, h);
                // Sin tarjeta bajo el cubo: se ve directamente el fondo del estado.
                int tx = cover.Right + 20, tw = Math.Max(20, split - tx - 24);
                g.DrawString("A H O R A   S U E N A", small, color, new RectangleF(24, 12, split - 40, 26 * scale), line);
                float titleH = Math.Min(90, h * .28f), artistY = 56 + titleH + 6;
                if (extra == null)
                {
                    g.DrawString(title ?? "", big, white, new RectangleF(tx, 56, tw, titleH), wrap);
                    PintaTextoMovil(g, artist ?? "", normal, color, new RectangleF(tx, artistY, tw, Math.Max(22, h - 82 - artistY)), line);
                }
                else PintaTituloGrande(g, extra, title, artist, big, normal, small, white, color, line, tx, tw);   // en una linea
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
                progressStart = DrawIdleClock(g, w, h, time);
                string heading = "A CONTINUACION  /  " + count + (count == 1 ? " CANCION" : " CANCIONES");
                g.DrawString(heading, small, color, new RectangleF(split, 12, w - split - 24, 26 * scale), line);
                if (count == 0)
                    g.DrawString("La siguiente cancion la eliges tu", normal, muted, new RectangleF(split + 14, 62, w - split - 45, 64));
                Rectangle sample = IdleQueueCellCount(w, h, 0, qt.Length, dozen);
                using (Font queueTitle = new Font("Trebuchet MS", dozen ? Math.Max(9, Math.Min(21 * scale, (sample.Height - 8) * .52f)) : normal.Size, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font queueArtist = new Font("Trebuchet MS", dozen ? Math.Max(8, Math.Min(16 * scale, (sample.Height - 8) * .40f)) : small.Size, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font queueIndex = new Font("Trebuchet MS", Math.Min(queueArtist.Size, IdleQueueThumb(sample).Width / 1.6f), FontStyle.Bold, GraphicsUnit.Pixel))
                {
                float titleHeight = queueTitle.GetHeight(g), detailHeight = queueArtist.GetHeight(g) + 2;
                int numberWidth = (int)Math.Ceiling(g.MeasureString("12", queueArtist).Width) + 4;
                numberFormat.Alignment = StringAlignment.Center; numberFormat.LineAlignment = StringAlignment.Center;
                numberFormat.FormatFlags |= StringFormatFlags.NoWrap;
                for (int i = 0; i < qt.Length && i < (dozen ? 12 : 3); i++)
                {
                    Rectangle r = IdleQueueCellCount(w, h, i, qt.Length, dozen);
                    using (GraphicsPath shape = RoundedPath(r, Math.Min(10, r.Height / 3))) g.FillPath(card, shape);
                    Rectangle thumb = IdleQueueThumb(r);
                    if (i < covers.Length && covers[i] != null) g.DrawImage(covers[i], thumb);
                    else g.FillRectangle(color, thumb);
                    bool compact = r.Width < 180;
                    RectangleF text = IdleQueueTextBlock(r, titleHeight, detailHeight, numberWidth);
                    float textX = text.X, textW = text.Width, textY = text.Y;
                    PintaTextoMovil(g, qt[i] ?? "", queueTitle, white, new RectangleF(textX, textY, textW, titleHeight), line);
                    string enMin = (extra != null) ? TextoMinutosCola(extra, i) : "";   // "en ~6 min" (SuenaVivoGpu.cs)
                    if (enMin.Length == 0)
                        PintaTextoMovil(g, i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, new RectangleF(textX, textY + titleHeight, textW, detailHeight), line);
                    else PintaMinutosCola(g, enMin, i < qa.Length ? qa[i] ?? "" : "", queueArtist, muted, compact, textX, textY + titleHeight, textW, detailHeight, line);
                    if (compact)
                    {
                        RectangleF label = IdleQueueNumberRect(r, queueIndex.GetHeight(g));
                        using (SolidBrush shade = new SolidBrush(Color.FromArgb(210, 8, 12, 24))) g.FillRectangle(shade, label);
                        g.DrawString((i + 1).ToString("00"), queueIndex, white, label, numberFormat);
                    }
                    else g.DrawString((i + 1).ToString("00"), queueArtist, color, new RectangleF(r.Right - numberWidth - 3, textY, numberWidth, r.Bottom - textY - 2), line);
                }
                }
            }
            return bitmap;
        }
    }
}
#endif

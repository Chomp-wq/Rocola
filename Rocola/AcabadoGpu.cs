#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace Rocola
{
    public partial class Form1
    {
        // Solo presentacion: nunca cambia rutas, etiquetas, orden ni claves de busqueda.
        internal static string TituloSinPista(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            int i = 0;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i < title.Length && title[i] == '-') i++;
            int start = i;
            while (i < title.Length && title[i] >= '0' && title[i] <= '9') i++;
            int digits = i - start;
            if (digits < 1 || digits > 3) return title;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i >= title.Length) return title;
            char separator = title[i++];
            if (separator != '-' && separator != '.' && separator != '–' && separator != '—') return title;
            // Un numero decimal no es un numero de pista.
            if (separator == '.' && i < title.Length && char.IsDigit(title[i])) return title;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            return i < title.Length ? title.Substring(i) : title;
        }
        private bool songMoveVertical;
        private int songMoveDirection;
        private InstrFlash songPagerFlash;
        private void MoveSongInput(int delta, bool vertical)
        {
            if (!gpuEnabled) { MoveSong(delta); return; }
            songMoveVertical = vertical; songMoveDirection = Math.Sign(delta);
            if (!vertical)
            {
                if (songPagerFlash == null) songPagerFlash = new InstrFlash();
                songPagerFlash.Fire(delta < 0 ? 0 : 1, Color.FromArgb(242, 206, 72));
            }
            try
            {
                // Con una sola fila por pagina, izquierda/derecha tampoco deben ciclar.
                if (!vertical) delta = Math.Max(0, Math.Min(songItems.Count - 1, songSel + delta)) - songSel;
                MoveSong(delta);
            }
            finally { songMoveVertical = false; songMoveDirection = 0; }
        }
        private partial class SongListView
        {
            public bool GpuSlideVertical;
            public void StartGpuSlide(int direction, bool vertical)
            {
                StartSlide(direction); GpuSlideVertical = vertical;
                if (vertical) slideDesde = direction * (float)Height;
            }
            // El velo y la clave de color se hacian NUEVOS en cada pintada. Los dos son objetos de GDI+
            // con su manija nativa detras, y esta lista se repinta al desplazarse y al pasar de pagina.
            // Se guardan como hace el resto de la casa (ver EnsurePaintRes en el buscador) y se sueltan
            // en el Dispose de la lista, por SueltaAcabadoGpu.
            private SolidBrush veloGpu;
            private ImageAttributes claveGpu;
            private Color claveGpuDe;
            private Bitmap cristalGpu;
            private bool cristalNeutral;
            private ImageAttributes tinteCristalGpu;
            private Color tinteCristalColor;
            private int gpuSnapshotSel = -1;
            private Bitmap gpuSnapshotRow, gpuSelectedRaw;
            private ImageAttributes GpuColorKey()
            {
                if (claveGpu == null || claveGpuDe != BackColor)
                {
                    if (claveGpu == null) claveGpu = new ImageAttributes();
                    claveGpu.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap);
                    claveGpuDe = BackColor;
                }
                return claveGpu;
            }
            private Bitmap GpuRowBuffer(ref Bitmap buffer, int w, int h)
            {
                if (buffer == null || buffer.Width != w || buffer.Height != h)
                {
                    SueltaDibujo(ref buffer);
                    buffer = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                }
                return buffer;
            }
            // Restaurar una fila no debe volver a medir/dibujar todas las canciones de la pagina.
            // Guardamos sus pixeles originales: mismos bordes y suavizado, sin otro mapa de pagina.
            private void UpdateGpuSnapshot(Graphics snapshot, Bitmap selected, int index, int w)
            {
                GraphicsState state = snapshot.Save();
                try
                {
                    snapshot.CompositingMode = CompositingMode.SourceCopy;
                    if (gpuSnapshotSel >= PageStart && gpuSnapshotSel < PageStart + RowsPerPage &&
                        gpuSnapshotRow != null && gpuSnapshotRow.Width == w && gpuSnapshotRow.Height == RowH)
                        snapshot.DrawImageUnscaled(gpuSnapshotRow, 0, (gpuSnapshotSel - PageStart) * RowH);
                }
                finally { snapshot.Restore(state); }
                int y = (index - PageStart) * RowH;
                Bitmap original = GpuRowBuffer(ref gpuSnapshotRow, w, RowH);
                using (Graphics copy = Graphics.FromImage(original))
                {
                    copy.CompositingMode = CompositingMode.SourceCopy;
                    copy.Clear(Color.Transparent);
                    copy.DrawImage(hornoPag, new Rectangle(0, 0, w, RowH), 0, y, w, RowH, GraphicsUnit.Pixel);
                }
                snapshot.DrawImageUnscaled(selected, 0, y);
                gpuSnapshotSel = index;
            }
            // Capas inmutables: pagina sin seleccion y una sola fila elegida.
            // Se conserva el horneado anterior para el hojeo ya existente.
            public Bitmap BakeGpuListLayer(bool selected)
            {
                int w = ClientSize.Width, h = selected ? RowH : ClientSize.Height;
                pintandoGpu = true;   // con la tarjeta, la fila elegida queda dentro del cristal (DentroDelCristal)
                Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                int saved = Sel;
                try
                {
                    if (!selected)
                    {
                        Sel = -1; hornoSig = long.MinValue;
                        using (new CapturaCintas(null)) AseguraHorneada(w, h);
                        using (Graphics output = Graphics.FromImage(result))
                        {
                            ImageAttributes key = GpuColorKey();
                            if (capturandoCintas == null)
                                output.DrawImage(hornoPag, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, key);
                            else using (Bitmap pageRaw = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
                            {
                                using (Graphics g = Graphics.FromImage(pageRaw)) PintaPagina(g, PageStart);
                                output.DrawImage(pageRaw, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, key);
                            }
                        }
                        gpuSnapshotSel = -1;
                        return result;
                    }
                    Bitmap raw = GpuRowBuffer(ref gpuSelectedRaw, w, h);
                    using (Graphics g = Graphics.FromImage(raw))
                    using (Graphics output = Graphics.FromImage(result))
                    {
                        // La fila elegida se pinta sola, como si fuera la primera: se le dice donde esta de verdad, para
                        // que quede dentro de las esquinas del cristal (DentroDelCristal).
                        gpuFilaY = (saved - PageStart) * RowH;
                        // La instantanea anterior conserva el titulo completo estatico.
                        using (new CapturaCintas(null))
                        {
                            PintaPagina(g, saved);
                            if (hornoPag != null)
                            using (Graphics snapshot = Graphics.FromImage(hornoPag))
                                UpdateGpuSnapshot(snapshot, raw, saved, w);
                        }
                        if (capturandoCintas != null) PintaPagina(g, saved);
                        output.DrawImage(raw, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, GpuColorKey());
                    }
                    return result;
                }
                catch { result.Dispose(); throw; }
                finally { Sel = saved; gpuFilaY = 0; }
            }

            public long GpuLayerSignature()
            {
                pintandoGpu = true;
                int saved = Sel;
                try
                {
                    Sel = -1;
                    return Mix(Mix(FirmaPagina(Width, Height), Font.Name), (int)Font.Style);
                }
                finally { Sel = saved; }
            }
            public int GpuSlideToken { get { return slideTick; } }
            public float GpuSlideOffset { get { return DesplazaAhora(); } }
            public float GpuSlideTravel { get { return slideDesde; } }
            public bool GpuPreviousReady { get { return Sliding && hornoAnt != null && hornoAnt.Width == Width && hornoAnt.Height == Height; } }
            public Bitmap BakeGpuPrevious()
            {
                if (!GpuPreviousReady) return null;
                Bitmap result = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (Graphics g = Graphics.FromImage(result))
                    using (ImageAttributes key = new ImageAttributes())
                    {
                        key.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap);
                        g.DrawImage(hornoAnt, new Rectangle(0, 0, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, key);
                    }
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
            partial void SueltaAcabadoGpu()
            {
                SueltaDibujo(ref veloGpu); SueltaDibujo(ref claveGpu);
                SueltaDibujo(ref cristalGpu);
                SueltaDibujo(ref tinteCristalGpu);
                SueltaDibujo(ref gpuSnapshotRow); SueltaDibujo(ref gpuSelectedRaw);
            }
            public void RenderTransparentGpu(Graphics g)
            { RenderTransparentGpuMode(g, false); }
            public void RenderTransparentGpuMode(Graphics g, bool glass)
            { RenderTransparentGpuTint(g, glass, false, Color.White); }
            public void RenderTransparentGpuTint(Graphics g, bool glass, bool tinted, Color tint)
            {
                pintandoGpu = true;   // la fila elegida, dentro del cristal (Form1.Vistas.cs, DentroDelCristal)
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                AseguraHorneada(w, h);
                float dx = DesplazaAhora();
                // Velo mayormente opaco: deja ver el fondo sin competir con las letras.
                if (veloGpu == null) veloGpu = new SolidBrush(Color.FromArgb(210, 12, 16, 30));
                if (glass)
                {
                    if (cristalGpu == null || cristalGpu.Width != w || cristalGpu.Height != h || cristalNeutral != tinted)
                    {
                        SueltaDibujo(ref cristalGpu);
                        cristalGpu = BakeSongGlassTint(w, h, tinted); cristalNeutral = tinted;
                    }
                    if (tinted)
                    {
                        if (tinteCristalGpu == null || tinteCristalColor != tint)
                        {
                            SueltaDibujo(ref tinteCristalGpu); tinteCristalGpu = new ImageAttributes();
                            ColorMatrix matrix = new ColorMatrix();
                            matrix.Matrix00 = tint.R / 255f; matrix.Matrix11 = tint.G / 255f; matrix.Matrix22 = tint.B / 255f;
                            tinteCristalGpu.SetColorMatrix(matrix); tinteCristalColor = tint;
                        }
                        g.DrawImage(cristalGpu, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, tinteCristalGpu);
                    }
                    else g.DrawImageUnscaled(cristalGpu, 0, 0);
                }
                else g.FillRectangle(veloGpu, 0, 0, w, h);
                // Elimina el relleno exacto, no reduce el alfa del texto ni de
                // la seleccion. Conserva la cache, geometria y paginacion.
                // La clave se actualiza al cambiar el fondo, reutilizando su atributo nativo.
                GpuColorKey();
                if (dx != 0f && hornoAnt != null)
                    g.DrawImage(hornoAnt, GpuSlideVertical ? new Rectangle(0, (int)(dx-slideDesde), w, h) : new Rectangle((int)(dx-slideDesde), 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, claveGpu);
                g.DrawImage(hornoPag, GpuSlideVertical ? new Rectangle(0, (int)dx, w, h) : new Rectangle((int)dx, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, claveGpu);
            }
            public void RenderOpaqueGpu(Graphics g)
            {
                pintandoGpu = true;
                int w = Width, h = Height;
                AseguraHorneada(w, h);
                float d = DesplazaAhora();
                if (d != 0 && hornoAnt != null) g.DrawImageUnscaled(hornoAnt, GpuSlideVertical ? 0 : (int)(d-slideDesde), GpuSlideVertical ? (int)(d-slideDesde) : 0);
                g.DrawImageUnscaled(hornoPag, GpuSlideVertical ? 0 : (int)d, GpuSlideVertical ? (int)d : 0);
            }
        }
        private void DrawSongPagerGpu(Graphics g)
        {
            Rectangle r = songPager.Bounds;
            int pages = songPager.Pages, page = songPager.Page;
            if (pages <= 1 || r.Width < 60) return;
            float size = Math.Max(12f, Math.Min(20f, r.Height * .43f));
            using (Font font = new Font(FontFamily.GenericSansSerif, size, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush text = new SolidBrush(Color.FromArgb(236, 242, 255)))
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(180, 200, 230)))
            using (SolidBrush body = new SolidBrush(Color.FromArgb(125, 16, 24, 45)))
            using (Pen rim = new Pen(Color.FromArgb(110, Lighten(bgAccentTarget, .45))))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                int cw = Math.Min(r.Width, Math.Max(170, (int)(size * 13)));
                Rectangle center = new Rectangle(r.X + (r.Width - cw) / 2, r.Y + 2, cw, Math.Max(12, r.Height - 4));
                using (GraphicsPath p = RoundedPath(center, Math.Min(15, center.Height / 2)))
                { g.FillPath(body, p); g.DrawPath(rim, p); }
                g.DrawString("PÁGINA  " + (page + 1) + " / " + pages, font, text, center, sf);
                int side = Math.Max(0, (r.Width - cw) / 2 - 12);
                if (side >= 90)
                {
                    int cy = r.Top + r.Height / 2, arrow = (int)size;
                    Color yellow = Color.FromArgb(242, 206, 72);
                    CtrlDirLeft(g, r.Left + 8, cy, arrow, page > 0 ? yellow : Color.FromArgb(80, yellow));
                    CtrlDirRight(g, r.Right - 8 - arrow, cy, arrow, page + 1 < pages ? yellow : Color.FromArgb(80, yellow));
                    sf.Alignment = StringAlignment.Near;
                    if (page > 0) g.DrawString("ANTERIOR", font, dim, new Rectangle(r.Left + 16 + arrow, r.Top, side - arrow - 8, r.Height), sf);
                    sf.Alignment = StringAlignment.Far;
                    if (page + 1 < pages) g.DrawString("SIGUIENTE", font, dim, new Rectangle(r.Right - side - 8, r.Top, side - arrow - 8, r.Height), sf);
                }
            }
        }
        private bool FastSongGlass { get { return gpuPanel != null && gpuPanel.GlassPieces; } }
        private bool CanUseSongGlass
        {
            get { return glassCapas && cancionesCristal && cancionesTransparentes && !searchOpen &&
                panelSongs != null && panelSongs.Visible && songList != null &&
                songList.Width >= 4 && songList.Height >= 4; }
        }
        private int EnsureSongPiece(int index)
        {
            Rectangle rect = new Rectangle(panelSongs.Left + songList.Left, panelSongs.Top + songList.Top,
                songList.Width, songList.Height);
            bool empty = (index == 3 && !songList.GpuPreviousReady) || index >= 4;   // la 4 es del buscador con portadas
            if (index == 2)
            {
                empty = songList.Sel < songList.PageStart || songList.Sel >= songList.PageStart + songList.RowsPerPage ||
                    songList.Sel >= songItems.Count;
                rect.Y += (songList.Sel - songList.PageStart) * songList.RowH;
                rect.Height = songList.RowH;
            }
            if (empty)
            {
                if (uiPieza[index] != null) { uiPieza[index].Dispose(); uiPieza[index] = null; uiPiezaVer[index]++; }
                uiPiezaRect[index] = Rectangle.Empty; uiPiezaSig[index] = SigNada;
                return uiPiezaVer[index];
            }
            long sig = Mix(Mix(Mix(SigBase, 'G'), index), songList.Width);
            sig = Mix(sig, songList.Height); sig = Mix(sig, textosDeslizantes);
            if (index == 3)
            {
                sig = Mix(sig, songList.GpuSlideToken); sig = Mix(sig, songList.PageStart);
            }
            else if (index != 0)
            {
                sig = Mix(sig, songList.GpuLayerSignature()); sig = Mix(sig, colaVer); sig = Mix(sig, generoVer);
                if (index == 2) { Color accent = songList.Accent; sig = Mix(sig, songList.Sel); sig = Mix(sig, accent.ToArgb()); }
            }
            uiPiezaRect[index] = rect; // mover una capa no obliga a volver a subir sus pixeles
            if (uiPieza[index] != null && uiPiezaSig[index] == sig) return uiPiezaVer[index];
            Bitmap next;
            if (index == 0) next = BakeSongGlassTint(songList.Width, songList.Height, true);
            else if (index == 3) next = songList.BakeGpuPrevious();
            else using (CapturaTexto(index == 1 ? 2 : 3)) next = songList.BakeGpuListLayer(index == 2);
            if (uiPieza[index] != null) uiPieza[index].Dispose();
            uiPieza[index] = next; uiPiezaSig[index] = sig;
            return ++uiPiezaVer[index];
        }
        internal static Bitmap BakeSongGlass(int w, int h)
        { return BakeSongGlassTint(w, h, false); }
        internal static Bitmap BakeSongGlassTint(int w, int h, bool neutral)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            using (GraphicsPath edge = RoundedPath(new Rectangle(1, 1, w - 3, h - 3), Math.Min(28, Math.Min(w, h) / 4)))
            using (LinearGradientBrush body = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                neutral ? Color.FromArgb(205, 55, 55, 55) : Color.FromArgb(205, 26, 37, 59),
                neutral ? Color.FromArgb(218, 24, 24, 24) : Color.FromArgb(218, 9, 16, 31), 90f))
            using (LinearGradientBrush sheen = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                neutral ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(36, 210, 230, 255), Color.FromArgb(0, 210, 230, 255), 115f))
            using (Pen rim = new Pen(neutral ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(96, 193, 222, 255), 1.3f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(body, edge); g.FillPath(sheen, edge); g.DrawPath(rim, edge);
            }
            return b;
        }
        private partial class SearchOverlay
        {
            // LOS DOS NUMEROS SALEN DE UN SOLO SITIO. Antes esto rehacia a mano el reparto del
            // buscador -el 128 escrito tal cual en vez de M+70+28, y el margen otra vez como 30- en un
            // archivo distinto del que lo decide. Coincidian, pero nada los ataba: el dia que se toque
            // el margen o el alto de la cabecera se mueve todo menos este trozo, y no avisa nadie.
            private void ExtraPreviewGeometry(out Rectangle region, out Rectangle area)
            {
                region = RegionTeclado();
                area = CajaPortada(region);
            }
            public bool ExtraGpuRect(int kind, out Rectangle rect)
            {
                rect = Rectangle.Empty;
                if (!SeparateGpu || Results == null || Results.Count == 0) return false;
                int selected = FocusResults ? SelResult : 0;
                if (selected < 0 || selected >= Results.Count) return false;
                if (kind == 2)
                {
                    if (selected >= resultRects.Count) return false;
                    rect = Rectangle.Inflate(Rectangle.Round(resultRects[selected].Key), MargenTarj, MargenTarj);
                    return true;
                }
                if (kind == 3 && FocusResults && GetCover != null && Results[selected].Kind != 3)
                {
                    Rectangle region, area; ExtraPreviewGeometry(out region, out area);
                    int top = area.Bottom + 10;
                    rect = new Rectangle(region.X, top, region.Width, Math.Max(1, region.Bottom-top+4));
                    return region.Width > 0 && region.Height > 0;
                }
                return false;
            }
            public void DrawExtraGpu(Graphics g, int kind, int dx, int dy)
            {
                if (!SeparateGpu || Results == null || Results.Count == 0) return;
                if (kind == 2)
                {
                    int selected = FocusResults ? SelResult : 0;
                    if (selected < 0 || selected >= resultRects.Count) return;
                    Rectangle r = Rectangle.Round(resultRects[selected].Key); r.Offset(dx, dy);
                    DrawOneResultCard(g, selected, r, true, Normalize(Query ?? ""));
                }
                else if (kind == 3 && FocusResults)
                {
                    Rectangle region, area; ExtraPreviewGeometry(out region, out area);
                    region.Offset(dx, dy); area.Offset(dx, dy);
                    DrawPreviewInfo(g, region, area);
                }
            }
        }
    }
}
#endif

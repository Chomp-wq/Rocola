#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace Rocola
{
    public partial class Form1
    {
        private partial class SongListView
        {
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
            // Capas inmutables: pagina sin seleccion y una sola fila elegida.
            // Se conserva el horneado anterior para el hojeo ya existente.
            public Bitmap BakeGpuListLayer(bool selected)
            {
                int w = ClientSize.Width, h = selected ? RowH : ClientSize.Height;
                Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                int saved = Sel;
                try
                {
                    using (Bitmap raw = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
                    using (Graphics g = Graphics.FromImage(raw))
                    using (Graphics output = Graphics.FromImage(result))
                    using (ImageAttributes key = new ImageAttributes())
                    {
                        if (!selected)
                        {
                            Sel = -1; hornoSig = long.MinValue; AseguraHorneada(w, h);
                            g.DrawImageUnscaled(hornoPag, 0, 0); gpuSnapshotSel = -1;
                        }
                        else
                        {
                            PintaPagina(g, saved);
                            if (hornoPag != null)
                            using (Graphics snapshot = Graphics.FromImage(hornoPag))
                            {
                                if (gpuSnapshotSel >= PageStart && gpuSnapshotSel < PageStart + RowsPerPage)
                                {
                                    snapshot.SetClip(new Rectangle(0, (gpuSnapshotSel - PageStart) * RowH, w, RowH));
                                    Sel = -1; PintaPagina(snapshot, PageStart); Sel = saved;
                                }
                                snapshot.ResetClip();
                                snapshot.DrawImageUnscaled(raw, 0, (saved - PageStart) * RowH);
                                gpuSnapshotSel = saved;
                            }
                        }
                        key.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap);
                        output.DrawImage(raw, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, key);
                    }
                    return result;
                }
                catch { result.Dispose(); throw; }
                finally { Sel = saved; }
            }

            public long GpuLayerSignature()
            {
                int saved = Sel;
                try
                {
                    Sel = -1;
                    return Mix(Mix(FirmaPagina(Width, Height), Font.Name), (int)Font.Style);
                }
                finally { Sel = saved; }
            }
            partial void SueltaAcabadoGpu()
            {
                SueltaDibujo(ref veloGpu); SueltaDibujo(ref claveGpu);
                SueltaDibujo(ref cristalGpu);
                SueltaDibujo(ref tinteCristalGpu);
            }
            public void RenderTransparentGpu(Graphics g)
            { RenderTransparentGpuMode(g, false); }
            public void RenderTransparentGpuMode(Graphics g, bool glass)
            { RenderTransparentGpuTint(g, glass, false, Color.White); }
            public void RenderTransparentGpuTint(Graphics g, bool glass, bool tinted, Color tint)
            {
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
                // Se rehace solo si cambio el color de fondo, que es de donde sale la clave.
                if (claveGpu == null || claveGpuDe != BackColor)
                {
                    SueltaDibujo(ref claveGpu);
                    claveGpu = new ImageAttributes();
                    claveGpu.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap);
                    claveGpuDe = BackColor;
                }
                if (dx != 0f && hornoAnt != null)
                    g.DrawImage(hornoAnt, new Rectangle((int)(dx-slideDesde), 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, claveGpu);
                g.DrawImage(hornoPag, new Rectangle((int)dx, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, claveGpu);
            }
        }
        private bool FastSongGlass { get { return gpuPanel != null && gpuPanel.GlassPieces; } }
        private bool CanUseSongGlass
        {
            get { return glassCapas && cancionesCristal && cancionesTransparentes && !searchOpen &&
                panelSongs != null && panelSongs.Visible && songList != null &&
                songList.Width >= 4 && songList.Height >= 4 && !songList.Sliding; }
        }
        private int EnsureSongPiece(int index)
        {
            Rectangle rect = new Rectangle(panelSongs.Left + songList.Left, panelSongs.Top + songList.Top,
                songList.Width, songList.Height);
            bool empty = index == 3;
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
            sig = Mix(sig, songList.Height);
            if (index != 0)
            {
                sig = Mix(sig, songList.GpuLayerSignature()); sig = Mix(sig, colaVer); sig = Mix(sig, generoVer);
                if (index == 2) sig = Mix(sig, songList.Sel);
            }
            uiPiezaRect[index] = rect; // mover una capa no obliga a volver a subir sus pixeles
            if (uiPieza[index] != null && uiPiezaSig[index] == sig) return uiPiezaVer[index];
            Bitmap next = index == 0 ? BakeSongGlassTint(songList.Width, songList.Height, true) : songList.BakeGpuListLayer(index == 2);
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
                if (kind == 3 && FocusResults && GetCover != null)
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

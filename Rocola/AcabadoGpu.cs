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
            public void RenderTransparentGpu(Graphics g)
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                AseguraHorneada(w, h);
                float dx = DesplazaAhora();
                // Velo mayormente opaco: deja ver el fondo sin competir con las letras.
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(210, 12, 16, 30)))
                    g.FillRectangle(veil, 0, 0, w, h);
                using (ImageAttributes attr = new ImageAttributes())
                {
                    // Elimina el relleno exacto, no reduce el alfa del texto ni de
                    // la seleccion. Conserva la cache, geometria y paginacion.
                    attr.SetColorKey(BackColor, BackColor, ColorAdjustType.Bitmap);
                    if (dx != 0f && hornoAnt != null)
                        g.DrawImage(hornoAnt, new Rectangle((int)(dx-slideDesde), 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attr);
                    g.DrawImage(hornoPag, new Rectangle((int)dx, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attr);
                }
            }
        }
        private partial class SearchOverlay
        {
            private void ExtraPreviewGeometry(out Rectangle region, out Rectangle area)
            {
                int contentY = 128, contentH = Height - contentY - 30;
                region = new Rectangle(30, contentY, (int)((Width-60)*.52), contentH);
                int side = Math.Max(40, (int)(Math.Min(region.Width, region.Height)*.62));
                int top = Math.Max(region.Y+6, region.Y+(region.Height-side)/2-24);
                area = new Rectangle(region.X+region.Width/2-side/2, top, side, side);
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

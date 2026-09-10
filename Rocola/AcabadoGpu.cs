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
            partial void SueltaAcabadoGpu()
            {
                SueltaDibujo(ref veloGpu); SueltaDibujo(ref claveGpu);
            }
            public void RenderTransparentGpu(Graphics g)
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                AseguraHorneada(w, h);
                float dx = DesplazaAhora();
                // Velo mayormente opaco: deja ver el fondo sin competir con las letras.
                if (veloGpu == null) veloGpu = new SolidBrush(Color.FromArgb(210, 12, 16, 30));
                g.FillRectangle(veloGpu, 0, 0, w, h);
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

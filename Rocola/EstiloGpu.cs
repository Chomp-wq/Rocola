// EL ESTILO GENERAL DE LA PANTALLA VIVA (tanda 5, 24-09-2026): letra para leer de lejos y el color del genero en todo.
// Solo con tarjeta grafica.
//
//  - LETRA PARA LEER DE LEJOS (letra_lejos=). La tarjeta de lo que suena, abajo, es lo que se lee de pie a dos o tres
//    metros de la rocola. Con esta idea, en la imagen que hornea la tarjeta:
//      * el TITULO crece un 45 % (si no cabe, se achica hasta un 15 % mas grande de lo de siempre y, si aun asi no,
//        termina en puntos suspensivos). Se queda con casi todo el alto del renglon de lo que sigue;
//      * el ARTISTA, con su pastilla del genero y el letrero MEJORADA, baja casi ese renglon, lo que deja la caja del
//        reloj (y el brillo del letrero va con el);
//      * LO QUE SIGUE se va a ese mismo renglon del artista, a la derecha y hasta el borde de las capsulas, en un gris
//        mucho mas claro. Si no cabe, termina en puntos suspensivos.
//    La franja mide lo mismo y nada mas se mueve: la pared de discos, las capsulas, la barra y el reloj siguen donde
//    estaban. Las etiquetas de verdad no se tocan: si alguna vez las pinta el procesador, salen como siempre.
//  - EL COLOR DEL GENERO EN TODO (color_genero=). El color del genero de lo que suena ya tenia el cielo, la aurora, la
//    barra de avance y la cancion elegida de la lista. Ahora tambien:
//      * la portada de lo que suena (la chica de abajo, y la grande del reposo cuando crece) lleva detras un resplandor
//        de ese color que respira despacio;
//      * la portada grande de dentro de un disco, tambien;
//      * el pulso de la letra elegida en la barra de letras toma el color del genero de esa letra (el mismo que luce la
//        seccion), en vez del azul de siempre.
//    Respira con su propio reloj (5 s), no con la musica: nada da saltos.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // Existen en los dos perfiles porque la configuracion es la misma; sin tarjeta grafica no se usan.
        private bool letraLejos = true;    // "letra_lejos="
        private bool colorGenero = true;   // "color_genero="
    }

#if ROCOLA_GPU
    public partial class Form1
    {
        // ======================================================================================================
        //  LETRA PARA LEER DE LEJOS
        // ======================================================================================================
        internal const float LejosTitulo = 1.45f, LejosTituloMin = 1.15f;
        internal static readonly Color LejosSigue = Color.FromArgb(214, 224, 246);   // lo que sigue (antes 150,165,200)

        private bool LetraLejosActiva()
        {
            return letraLejos && gpuEnabled && gpuPanel != null && lblNowTop != null && lblNow != null && lblNext != null
                && pastCred != null && pastCola != null && lblTime != null;
        }
        // Cuanto baja el renglon del artista (con su pastilla y el letrero): un renglon de lo que sigue, o lo que quepa
        // sin tocar la caja del reloj (lo que sigue va a ir en ese renglon, a la derecha, encima del reloj).
        private int BajaArtistaLejos() { return LetraLejosActiva() ? BajaLejos(lblNow.Bounds, lblNext.Height, lblTime.Bounds) : 0; }
        internal static int BajaLejos(Rectangle artista, int renglon, Rectangle reloj)
        {
            return Math.Max(0, Math.Min(renglon, reloj.Top - 1 - artista.Bottom));
        }
        // Hasta donde llega lo que lleva el renglon del artista: el artista, su pastilla del genero y el letrero.
        private int FinRenglonArtista()
        {
            int fin = lblNow.Left + Math.Min(lblNow.Width, TextRenderer.MeasureText(lblNow.Text ?? "", lblNow.Font).Width);
            if (pastGenero != null && pastGenero.Visible) fin = Math.Max(fin, pastGenero.Right);
            if (lblMejorada != null && lblMejorada.Visible) fin = Math.Max(fin, lblMejorada.Right);
            return fin;
        }
        // Donde va "Sigue:" de lejos: en el renglon del artista (ya bajado), detras de lo que lleva y hasta el borde derecho
        // de las capsulas.
        internal static Rectangle CajaSigueLejos(Rectangle renglon, int finContenido, int derecha)
        {
            int x0 = finContenido + 24;
            return new Rectangle(x0, renglon.Top, Math.Max(0, derecha - x0), renglon.Height);
        }
        // Cuanto crece el titulo: lo mas que quepa entre 1,45 y 1,15; si ni asi cabe, 1,15 y puntos suspensivos.
        internal static float EscalaTituloLejos(Graphics g, string texto, Font f, float ancho, float alto)
        {
            if (string.IsNullOrEmpty(texto) || f == null) return LejosTituloMin;
            float tope = Math.Min(LejosTitulo, alto / Math.Max(1f, f.GetHeight(g)));
            if (tope < LejosTituloMin) return Math.Max(1f, tope);
            float w = g.MeasureString(texto, f, PointF.Empty, StringFormat.GenericTypographic).Width;
            for (float e = tope; e > LejosTituloMin; e -= .05f)
                if (w * e <= ancho) return e;
            return LejosTituloMin;
        }

        // La tarjeta de lo que suena, de lejos, en la imagen de la franja (EnsureUiBotOverlay; 'g' ya va trasladado a
        // coordenadas del form). Devuelve false si la idea esta apagada: entonces se pinta la de siempre.
        private bool FranjaLejos(Graphics g)
        {
            if (!LetraLejosActiva()) return false;
            int baja = BajaArtistaLejos();
            PintaTituloLejos(g, lblNowTop, baja);
            GraphicsState st = g.Save();
            g.TranslateTransform(0, baja);   // el renglon del artista baja, con su pastilla y su letrero
            DrawLabelOverlay(g, lblNow);
            if (lblMejorada != null && lblMejorada.Visible) DrawLabelOverlay(g, lblMejorada);
            if (pastGenero != null && pastGenero.Visible) DrawCtrlOverlay(g, pastGenero);
            g.Restore(st);
            Rectangle renglon = lblNow.Bounds; renglon.Offset(0, baja);
            PintaSigueLejos(g, lblNext, CajaSigueLejos(renglon, FinRenglonArtista(), Math.Max(pastCola.Right, pastCred.Right)));
            return true;
        }
        internal static void PintaTituloLejos(Graphics g, Label titulo, int extra)
        {
            if (titulo == null || string.IsNullOrEmpty(titulo.Text)) return;
            RectangleF r = new RectangleF(titulo.Left, titulo.Top, titulo.Width, titulo.Height + extra);
            float e = EscalaTituloLejos(g, titulo.Text, titulo.Font, r.Width, r.Height);
            using (Font f = new Font(titulo.Font.FontFamily, titulo.Font.Size * e, titulo.Font.Style, titulo.Font.Unit))
                PintaRenglonLejos(g, titulo.Text, f, titulo.ForeColor, r, StringAlignment.Near);
        }
        internal static void PintaSigueLejos(Graphics g, Label sigue, Rectangle caja)
        {
            if (sigue == null || string.IsNullOrEmpty(sigue.Text) || caja.Width < 20 || caja.Height < 6) return;
            PintaRenglonLejos(g, sigue.Text, sigue.Font, LejosSigue, caja, StringAlignment.Far);
        }
        // Un renglon de la franja de lejos. Si no cabe, se recorta aqui y termina en puntos suspensivos (U+2026): asi sale
        // igual con cualquier GDI+. Alineado a la derecha, el recorte propio de GDI+ no se porta igual en todas partes
        // (en alguno se come el principio en vez del final).
        private static void PintaRenglonLejos(Graphics g, string texto, Font f, Color c, RectangleF r, StringAlignment al)
        {
            using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (SolidBrush b = new SolidBrush(c))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.None;
                sf.Alignment = al; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(RecortaLejos(g, texto, f, r.Width - 1f), f, b, r, sf);
            }
        }
        // El texto tal cual si cabe; si no, lo mas largo que quepa con los puntos suspensivos detras.
        internal static string RecortaLejos(Graphics g, string texto, Font f, float ancho)
        {
            if (string.IsNullOrEmpty(texto) || AnchoLejos(g, texto, f) <= ancho) return texto;
            int lo = 0, hi = texto.Length - 1;   // cuantas letras caben, contando los puntos
            while (lo < hi)
            {
                int m = (lo + hi + 1) / 2;
                if (AnchoLejos(g, texto.Substring(0, m).TrimEnd() + "\u2026", f) <= ancho) lo = m; else hi = m - 1;
            }
            if (lo > 0 && char.IsHighSurrogate(texto[lo - 1])) lo--;   // sin partir un caracter doble
            return lo > 0 ? texto.Substring(0, lo).TrimEnd() + "\u2026" : "";
        }
        private static float AnchoLejos(Graphics g, string s, Font f)
        {
            return g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic).Width;
        }
        // El letrero MEJORADA, donde se ve (su brillo lo pone la tarjeta y tiene que ir con el).
        private Rectangle RectMejorada()
        {
            Rectangle r = lblMejorada.Bounds;
            r.Offset(0, BajaArtistaLejos());
            return r;
        }

        // ======================================================================================================
        //  EL COLOR DEL GENERO EN TODO
        // ======================================================================================================
        // El pulso de la letra elegida: el color del genero de esa letra (el de la seccion), o el azul de siempre.
        private Color ColorPulsoLetra()
        {
            if (!colorGenero || !gpuEnabled) return Color.FromArgb(110, 180, 255);
            return AcentoSeccion();
        }
    }

    internal sealed partial class GpuPanel
    {
        internal bool GeneroTodo;        // el color del genero en todo (Ctrl+F3 -> Pantalla)
        internal uint GeneroArgb = 0xFF6E78A0u;   // el color del genero de lo que suena (lo pone Form1 en cada latido)
        internal const int HaloGeneroMs = 5000;   // lo que tarda en respirar una vez

        // El resplandor del genero detras de una portada: centrado con ella, un poco mas grande, y respirando despacio con
        // su propio reloj (no con la musica).
        internal static void HaloGenero(float x, float y, float w, float h, uint genero, double ms, out float cx, out float cy, out float r, out uint argb)
        {
            cx = x + w * .5f; cy = y + h * .5f; r = Math.Max(w, h) * .82f;
            double p = .5 + .5 * Math.Sin(ms * 2.0 * Math.PI / HaloGeneroMs);
            int a = (int)(70 + 40 * p);
            argb = ((uint)a << 24) | (genero & 0x00FFFFFFu);
        }
        private void DibujaHaloGenero(float x, float y, float w, float h, float alfa)
        {
            if (!GeneroTodo || w < 4f || h < 4f || alfa <= .003f) return;
            float cx, cy, r; uint argb;
            HaloGenero(x, y, w, h, GeneroArgb, AhoraMs(), out cx, out cy, out r, out argb);
            stars.DrawGlow(cx, cy, r, FadeArgb(argb, alfa));
        }
    }
#endif
}

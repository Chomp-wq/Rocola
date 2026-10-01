// EL ESTILO GENERAL DE LA PANTALLA VIVA (tanda 5, 24-09-2026): letra para leer de lejos y el color del genero en todo.
// Solo con tarjeta grafica.
//
//  - LETRA PARA LEER DE LEJOS (letra_lejos=). La tarjeta de lo que suena, abajo, es lo que se lee de pie a dos o tres
//    metros de la rocola. Con esta idea, en la imagen que hornea la tarjeta:
//      * el TITULO crece un 45 % (si no cabe, se achica hasta un 15 % mas grande de lo de siempre y, si aun asi no,
//        termina en puntos suspensivos). Se queda con casi todo el alto del renglon de lo que sigue;
//      * el ARTISTA, con su pastilla del genero y el letrero MEJORADA, baja casi ese renglon, lo que deja la caja del
//        reloj (y el brillo del letrero va con el);
//      * LO QUE SIGUE tiene una tarjeta lateral ("A CONTINUACION"): alta, del titulo a la barra, si el renglon del artista
//        le deja sitio; si no (el letrero MEJORADA, o las capsulas anchas de "Inserta una moneda"), baja, a la altura del
//        titulo, con el artista pasando por debajo. Solo en pantallas estrechas conserva su renglon bajo el artista.
//        El titulo reserva ese espacio y los textos largos terminan en puntos suspensivos.
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
        private bool textosDeslizantes = true; // "textos_deslizantes="
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
        private int BajaArtistaLejos()
        {
            // Si no cabe la tarjeta lateral, conservar un renglon propio para lo siguiente.
            return LetraLejosActiva() && (TextoSiguiente().Length == 0 || !CajaSiguienteCompacto().IsEmpty)
                ? BajaLejos(lblNow.Bounds, lblNext.Height, lblTime.Bounds) : 0;
        }
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
            PintaTituloCompacto(g, true);
            GraphicsState st = g.Save();
            g.TranslateTransform(0, baja);   // el renglon del artista baja, con su pastilla y su letrero
            PintaArtistaCompacto(g);
            if (lblMejorada != null && lblMejorada.Visible) DrawLabelOverlay(g, lblMejorada);
            if (pastGenero != null && pastGenero.Visible) DrawCtrlOverlay(g, pastGenero);
            g.Restore(st);
            PintaSiguienteCompacto(g);
            return true;
        }
        internal static void PintaTituloLejos(Graphics g, Label titulo, int extra)
        {
            if (titulo == null || string.IsNullOrEmpty(titulo.Text)) return;
            RectangleF r = new RectangleF(titulo.Left, titulo.Top, titulo.Width, titulo.Height + extra);
            PintaTituloEnCaja(g, titulo, r);
        }
        private static void PintaTituloEnCaja(Graphics g, Label titulo, RectangleF r)
        {
            float e = EscalaTituloLejos(g, titulo.Text, titulo.Font, r.Width, r.Height);
            using (Font f = new Font(titulo.Font.FontFamily, titulo.Font.Size * e, titulo.Font.Style, titulo.Font.Unit))
                PintaRenglonLejos(g, titulo.Text, f, titulo.ForeColor, r, StringAlignment.Near);
        }
        internal static void PintaSigueLejos(Graphics g, Label sigue, Rectangle caja)
        {
            if (sigue == null || string.IsNullOrEmpty(sigue.Text) || caja.Width < 20 || caja.Height < 6) return;
            PintaRenglonLejos(g, sigue.Text, sigue.Font, LejosSigue, caja, StringAlignment.Far);
        }
        private string TextoSiguiente()
        {
            if (lblNext == null) return "";
            string next = (lblNext.Text ?? "").Trim();
            if (next.StartsWith("Sigue:", StringComparison.OrdinalIgnoreCase)) next = next.Substring(6).Trim();
            return next == "-" ? "" : next;
        }
        // LA TARJETA DE LO QUE SIGUE, entre lo que suena y las capsulas: acaba 20 px antes de la de la cola y empieza en el
        // 43 % del ancho, o algo mas a la izquierda si asi no llega a sus 180 px (sin quitarle al titulo sus 240 px). Dos formas:
        //  - ALTA, del titulo a la barra: cuando el renglon del artista (con su pastilla del genero y el letrero MEJORADA)
        //    acaba 28 px antes. Es la de siempre, en el mismo sitio.
        //  - BAJA, a la altura del titulo: el renglon del artista, que baja un renglon con la letra de lejos, pasa por debajo.
        //    Hasta el 1-10-2026, con MEJORADA o con las capsulas anchas ("Inserta una moneda") a 1024 no cabia la alta y lo que
        //    sigue volvia a su renglon de reserva, "SIGUE . ...", como en las pantallas estrechas.
        // Si ni asi caben 180 px (pantallas estrechas), lo que sigue conserva su renglon bajo el artista.
        internal const int TarjetaSiguienteMin = 180;
        private Rectangle CajaSiguienteCompacto() { bool alta; return CajaSiguienteForma(out alta); }
        private Rectangle CajaSiguienteForma(out bool alta)
        {
            alta = false;
            if (lblNow == null || lblNowTop == null || progress == null || pastCola == null || TextoSiguiente().Length == 0) return Rectangle.Empty;
            int right = pastCola.Left - 20;
            int left = Math.Max(lblNowTop.Left + 240, Math.Min(ClientSize.Width * 43 / 100, right - TarjetaSiguienteMin));
            int top = lblNowTop.Top;
            int leftAlta = Math.Max(left, FinRenglonArtista() + 28);
            Rectangle box = new Rectangle(leftAlta, top, Math.Max(0, right - leftAlta), Math.Max(0, progress.Top - top - 4));
            if (box.Width >= TarjetaSiguienteMin && box.Height >= 28) { alta = true; return box; }
            // La baja llega hasta el renglon del artista ya bajado (sin la letra de lejos no baja, y no suele caber).
            int baja = LetraLejosActiva() ? BajaLejos(lblNow.Bounds, lblNext.Height, lblTime.Bounds) : 0;
            box = new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, lblNow.Top + baja - 4 - top));
            return box.Width >= TarjetaSiguienteMin && box.Height >= 28 ? box : Rectangle.Empty;
        }
        private void PintaTituloCompacto(Graphics g, bool grande)
        {
            if (lblNowTop == null) return;
            Rectangle box = CajaSiguienteCompacto();
            Rectangle r = lblNowTop.Bounds;
            if (!box.IsEmpty) r.Width = Math.Min(r.Width, Math.Max(0, box.Left - r.Left - 20));
            if (grande) { r.Height += BajaArtistaLejos(); PintaTituloEnCaja(g, lblNowTop, r); }
            else PintaRenglonLejos(g, lblNowTop.Text, lblNowTop.Font, lblNowTop.ForeColor, r, StringAlignment.Near);
        }
        private void PintaSiguienteCompacto(Graphics g)
        {
            string next = TextoSiguiente();
            if (next.Length == 0) return;
            Rectangle box = CajaSiguienteCompacto();
            if (box.IsEmpty)
            {
                PintaRenglonLejos(g, "SIGUE  ·  " + next, lblNext.Font, LejosSigue, lblNext.Bounds, StringAlignment.Near);
                return;
            }
            PintaTarjetaSiguiente(g, next, box);
        }
        internal static void PintaTarjetaSiguiente(Graphics g, string next, Rectangle box)
        {
            using (GraphicsPath p = RoundedPath(box, Math.Min(12, box.Height / 3)))
            using (SolidBrush body = new SolidBrush(Color.FromArgb(66, 12, 20, 40)))
            using (Pen edge = new Pen(Color.FromArgb(55, 180, 210, 255)))
            using (Font caption = new Font(FontFamily.GenericSansSerif, Math.Max(10f, Math.Min(13f, box.Height * .25f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font title = new Font(FontFamily.GenericSansSerif, Math.Max(12f, Math.Min(20f, box.Height * .40f)), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.FillPath(body, p); g.DrawPath(edge, p);
                int head = Math.Max(12, (int)(box.Height * .38f));
                PintaRenglonLejos(g, "A CONTINUACIÓN", caption, Color.FromArgb(169, 191, 224),
                    new Rectangle(box.X + 12, box.Y + 2, Math.Max(0, box.Width - 24), head), StringAlignment.Near);
                PintaRenglonLejos(g, next, title, LejosSigue,
                    new Rectangle(box.X + 12, box.Y + head, Math.Max(0, box.Width - 24), box.Height - head - 2), StringAlignment.Near);
            }
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
                // Sin LineLimit (viene en GenericTypographic): un renglon un pelo mas alto que su caja se descartaba ENTERO.
                // Le pasaba al titulo sin tarjeta al lado: su letra crece justo hasta llenar el renglon.
                sf.FormatFlags &= ~StringFormatFlags.LineLimit;
                sf.Alignment = al; sf.LineAlignment = StringAlignment.Center;
                if (!TextoConCinta(g, texto, f, c, r, sf))
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

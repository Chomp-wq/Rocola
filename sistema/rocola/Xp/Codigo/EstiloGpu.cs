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
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la franja de lejos dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; lo que le llega de archivos sin traducir (las letras de las etiquetas, TextoConCinta, PintaArtistaCompacto) pasa por el puente (Plataforma/Puente.cs y Puente.acabado-estilo.cs); RoundedPath (Form1.Fondo.cs, ya traducido) da su SKPath directo (ContornoRedondo)

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
            int fin = lblNow.Left + Math.Min(lblNow.Width, TextoGdi.Mide(lblNow.Text ?? "", PuenteDibujo.Letra(lblNow.Font)).Width);   // EN LINUX: TextRenderer.MeasureText(texto, letra): la medida de GDI (Nativo/TextoGdi.cs; sin caja ni banderas, Bottom, como WinForms); la letra de la etiqueta (WinForms, aun sin traducir), por el puente
            if (pastGenero != null && pastGenero.Visible) fin = Math.Max(fin, pastGenero.Right);
            if (lblMejorada != null && lblMejorada.Visible) fin = Math.Max(fin, lblMejorada.Right);
            return fin;
        }
        // A otro tamano las letras no miden exactamente lo proporcional (se ajustan a la rejilla de pixeles): un titulo que
        // por la cuenta cabia justo se pasaba un pelo con la letra de verdad y salia con puntos al final. Se comprueba
        // midiendolo con la letra de ese tamano (sexta tanda, 12-10-2026). Solo al hornear la franja, nunca por cuadro.
        private static bool CabeConLetra(AjusteLetra ajuste, string texto, Rocola.Nativo.Letra f, float e, float ancho)   // EN LINUX: del Graphics con que se media solo pesa su modo de letra (TextRenderingHint); la letra es la nativa (Nativo/Letras.cs; Form1 tiene un metodo Letra)
        {
            using (Rocola.Nativo.Letra mayor = f.UnidadPixel ? Rocola.Nativo.Letra.EnPixeles(f.Familia, f.Tamano * e, f.Negrita, f.Cursiva, f.Subrayada) : Rocola.Nativo.Letra.EnPuntos(f.Familia, f.Tamano * e, f.Negrita, f.Cursiva, f.Subrayada))   // EN LINUX: new Font(f.FontFamily, f.Size * e, f.Style, f.Unit): la misma familia y el mismo estilo a ese tamano, en la unidad de la de XP (pixeles o puntos)
                return TextoGdiPlus.Mide(texto, mayor, ajuste, SizeF.Empty, FormatoTexto.TipograficoCompartido).Width <= ancho;   // EN LINUX: g.MeasureString(texto, mayor, PointF.Empty, GenericTypographic): sin caja y con el tipografico COMPARTIDO (Nativo/TextoGdiPlus.cs)
        }
        // Cuanto crece el titulo: lo mas que quepa entre 1,45 y 1,15; si ni asi cabe, 1,15 y puntos suspensivos.
        internal static float EscalaTituloLejos(AjusteLetra ajuste, string texto, Rocola.Nativo.Letra f, float ancho, float alto)   // EN LINUX: del Graphics con que se mide solo pesa su modo de letra; la letra es la nativa
        {
            if (string.IsNullOrEmpty(texto) || f == null) return LejosTituloMin;
            float tope = Math.Min(LejosTitulo, alto / Math.Max(1f, f.AltoRenglon));   // EN LINUX: f.GetHeight(g): el alto del renglon en pixeles, a 96 puntos por pulgada (los de la pantalla de XP)
            if (tope < LejosTituloMin) return Math.Max(1f, tope);
            float w = TextoGdiPlus.Mide(texto, f, ajuste, SizeF.Empty, FormatoTexto.TipograficoCompartido).Width;   // EN LINUX: g.MeasureString(texto, f, PointF.Empty, GenericTypographic)
            for (float e = tope; e > LejosTituloMin; e -= .05f)
                if (w * e <= ancho && CabeConLetra(ajuste, texto, f, e, ancho)) return e;   // EN LINUX: con el modo de letra
            return LejosTituloMin;
        }

        // La tarjeta de lo que suena, de lejos, en la imagen de la franja (EnsureUiBotOverlay; 'g' ya va trasladado a
        // coordenadas del form). Devuelve false si la idea esta apagada: entonces se pinta la de siempre.
        private bool FranjaLejos(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio)   // EN LINUX: el lienzo de Skia y el estado del Graphics de la franja (suavizado, modo de letra, copia y medio pixel), como parametros; EnsureUiBotOverlay (Form1.Video.cs, aun sin traducir) la llama con su Graphics por el puente (Plataforma/Puente.acabado-estilo.cs)
        {
            if (!LetraLejosActiva()) return false;
            int baja = BajaArtistaLejos();
            PintaTituloCompacto(g, suave, ajuste, copia, medio, true);   // EN LINUX: con el lienzo y su estado
            int st = g.Save(); Rocola.Dibujo2D.Graphics gg = PuenteDibujo.GraphicsAcabado(g, suave, ajuste, copia, medio);   // EN LINUX: GraphicsState st = g.Save(): la matriz y el recorte los guarda el lienzo (el estado del dibujo va en variables y aqui no cambia); gg: el Graphics de XP para lo de abajo que aun no esta traducido, sobre el mismo lienzo y con el estado de la franja (Plataforma/Puente.acabado-estilo.cs)
            g.Translate(0, baja);   // el renglon del artista baja, con su pastilla y su letrero   // EN LINUX: g.TranslateTransform(0, baja) (gg pinta en este mismo lienzo: lo ve)
            PintaArtistaCompacto(gg);   // EN LINUX: PintaArtistaCompacto (TextoMovilGpu.cs, aun sin traducir) pinta con un Graphics: gg
            if (lblMejorada != null && lblMejorada.Visible) DrawLabelOverlay(g, ajuste, copia, medio, lblMejorada);   // EN LINUX: DrawLabelOverlay (Form1.Video.cs, ya traducida) con el lienzo y su estado (el que llevaba gg)
            if (pastGenero != null && pastGenero.Visible) DrawCtrlOverlay(g, suave, ajuste, copia, Filtro.Bilineal, medio, pastGenero);   // EN LINUX: DrawCtrlOverlay (Form1.Video.cs, ya traducida) con el lienzo y su estado (el que llevaba gg: el filtro de fabrica, Bilinear)
            g.RestoreToCount(st);   // EN LINUX: g.Restore(st)
            PintaSiguienteCompacto(g, suave, ajuste, copia, medio);   // EN LINUX: con el lienzo y su estado
            return true;
        }
        private static void PintaTituloEnCaja(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Label titulo, RectangleF r)   // EN LINUX: el lienzo de Skia y el estado del Graphics
        {
            Rocola.Nativo.Letra lt = PuenteDibujo.Letra(titulo.Font); float e = EscalaTituloLejos(ajuste, titulo.Text, lt, r.Width, r.Height);   // EN LINUX: con el modo de letra; lt: la letra de la etiqueta (WinForms, aun sin traducir), por el puente (la misma, no una copia)
            using (Rocola.Nativo.Letra f = lt.UnidadPixel ? Rocola.Nativo.Letra.EnPixeles(lt.Familia, lt.Tamano * e, lt.Negrita, lt.Cursiva, lt.Subrayada) : Rocola.Nativo.Letra.EnPuntos(lt.Familia, lt.Tamano * e, lt.Negrita, lt.Cursiva, lt.Subrayada))   // EN LINUX: new Font(familia, tamano * e, estilo, unidad) de la letra de la etiqueta: la misma familia y estilo a ese tamano, en su unidad (pixeles o puntos)
                PintaRenglonLejos(g, suave, ajuste, copia, medio, titulo.Text, f, titulo.ForeColor, r, Alinea.Cerca);   // EN LINUX: con el lienzo y su estado; StringAlignment.Near es Alinea.Cerca
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
        private void PintaTituloCompacto(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, bool grande)   // EN LINUX: el lienzo de Skia y el estado del Graphics de la franja; EnsureUiBotOverlay (Form1.Video.cs) la llama por el puente
        {
            if (lblNowTop == null) return;
            Rectangle box = CajaSiguienteCompacto();
            Rectangle r = lblNowTop.Bounds;
            if (!box.IsEmpty) r.Width = Math.Min(r.Width, Math.Max(0, box.Left - r.Left - 20));
            if (grande) { r.Height += BajaArtistaLejos(); PintaTituloEnCaja(g, suave, ajuste, copia, medio, lblNowTop, r); }   // EN LINUX: con el lienzo y su estado
            else PintaRenglonLejos(g, suave, ajuste, copia, medio, lblNowTop.Text, PuenteDibujo.Letra(lblNowTop.Font), lblNowTop.ForeColor, r, Alinea.Cerca);   // EN LINUX: con el lienzo y su estado; la letra de la etiqueta, por el puente; Near es Alinea.Cerca
        }
        private void PintaSiguienteCompacto(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio)   // EN LINUX: el lienzo de Skia y el estado del Graphics de la franja; EnsureUiBotOverlay (Form1.Video.cs) la llama por el puente
        {
            string next = TextoSiguiente();
            if (next.Length == 0) return;
            Rectangle box = CajaSiguienteCompacto();
            if (box.IsEmpty)
            {
                PintaRenglonLejos(g, suave, ajuste, copia, medio, "SIGUE  ·  " + next, PuenteDibujo.Letra(lblNext.Font), LejosSigue, lblNext.Bounds, Alinea.Cerca);   // EN LINUX: con el lienzo y su estado; la letra de la etiqueta, por el puente; Near es Alinea.Cerca
                return;
            }
            PintaTarjetaSiguiente(g, suave, ajuste, copia, medio, next, box);   // EN LINUX: con el lienzo y su estado
        }
        internal static void PintaTarjetaSiguiente(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, string next, Rectangle box)   // EN LINUX: el lienzo de Skia y el estado del Graphics
        {
            using (SKPath p = ContornoRedondo(box, Math.Min(12, box.Height / 3)))   // EN LINUX: RoundedPath devuelve un GraphicsPath: aqui su SKPath (ContornoRedondo, Form1.Fondo.cs: el mismo trazo)
            using (SKPaint body = new SKPaint { Color = Color.FromArgb(66, 12, 20, 40).Sk() })   // EN LINUX: SolidBrush: un SKPaint de ese color
            using (SKPaint edge = new SKPaint { Color = Color.FromArgb(55, 180, 210, 255).Sk(), StrokeWidth = 1f })   // EN LINUX: Pen(color): la pluma de 1 de ancho (Pixel.Traza le pone lo de GDI+)
            using (Rocola.Nativo.Letra caption = Rocola.Nativo.Letra.EnPixeles(Letras.SinRemates, Math.Max(10f, Math.Min(13f, box.Height * .25f)), true))   // EN LINUX: new Font(GenericSansSerif, px, Bold, Pixel): la generica sin remates de XP (Microsoft Sans Serif), en pixeles y en negrita
            using (Rocola.Nativo.Letra title = Rocola.Nativo.Letra.EnPixeles(Letras.SinRemates, Math.Max(12f, Math.Min(20f, box.Height * .40f)), true))   // EN LINUX: new Font(GenericSansSerif, px, Bold, Pixel)
            {
                Pixel.Rellena(g, p, body, suave, medio, copia); Pixel.Traza(g, p, edge, suave, medio, copia);   // EN LINUX: g.FillPath(body, p); g.DrawPath(edge, p) (Nativo/Pixel.cs)
                int head = Math.Max(12, (int)(box.Height * .38f));
                PintaRenglonLejos(g, suave, ajuste, copia, medio, "A CONTINUACIÓN", caption, Color.FromArgb(169, 191, 224),   // EN LINUX: con el lienzo y su estado
                    new Rectangle(box.X + 12, box.Y + 2, Math.Max(0, box.Width - 24), head), Alinea.Cerca);   // EN LINUX: StringAlignment.Near es Alinea.Cerca
                PintaRenglonLejos(g, suave, ajuste, copia, medio, next, title, LejosSigue,   // EN LINUX: con el lienzo y su estado
                    new Rectangle(box.X + 12, box.Y + head, Math.Max(0, box.Width - 24), box.Height - head - 2), Alinea.Cerca);   // EN LINUX: StringAlignment.Near es Alinea.Cerca
            }
        }
        // Un renglon de la franja de lejos. Si no cabe, se recorta aqui y termina en puntos suspensivos (U+2026): asi sale
        // igual con cualquier GDI+. Alineado a la derecha, el recorte propio de GDI+ no se porta igual en todas partes
        // (en alguno se come el principio en vez del final).
        private static void PintaRenglonLejos(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, string texto, Rocola.Nativo.Letra f, Color c, RectangleF r, Alinea al)   // EN LINUX: el lienzo de Skia y el estado del Graphics, la letra nativa y la alineacion nativa (StringAlignment); TextoMovilGpu.cs (aun sin traducir) la llama con su Graphics por el puente (Plataforma/Puente.acabado-estilo.cs)
        {
            // EN LINUX: using (StringFormat sf = GenericTypographic.Clone()): la copia es un FormatoTexto, que no se suelta (abajo, dentro)
            using (SKPaint b = new SKPaint { Color = c.Sk() })   // EN LINUX: SolidBrush(c)
            {
                FormatoTexto sf = new FormatoTexto(FormatoTexto.TipograficoCompartido); sf.Banderas |= BanderasTexto.SinPartir; sf.Corte = CorteTexto.Nada;   // EN LINUX: la copia del tipografico COMPARTIDO (con lo que otro le haya cambiado: Clone es new StringFormat(este)), FormatFlags |= NoWrap y Trimming = None
                // Sin LineLimit (viene en GenericTypographic): un renglon un pelo mas alto que su caja se descartaba ENTERO.
                // Le pasaba al titulo sin tarjeta al lado: su letra crece justo hasta llenar el renglon.
                sf.Banderas &= ~BanderasTexto.SoloEnteros;   // EN LINUX: FormatFlags &= ~LineLimit
                sf.Horizontal = al; sf.Vertical = Alinea.Centro;   // EN LINUX: Alignment = al; LineAlignment = Center
                if (!TextoConCintaAcabado(g, suave, ajuste, copia, medio, texto, f, c, r, sf))   // EN LINUX: TextoConCinta (TextoMovilGpu.cs, aun sin traducir) pide un Graphics, una Font y un StringFormat: por el puente (Plataforma/Puente.acabado-estilo.cs)
                    TextoGdiPlus.Escribe(g, RecortaLejos(ajuste, texto, f, r.Width - 1f), f, ajuste, b, r, sf, copia, medio);   // EN LINUX: g.DrawString(texto, letra, pincel, caja, formato) (Nativo/TextoGdiPlus.cs)
            }
        }
        // El texto tal cual si cabe; si no, lo mas largo que quepa con los puntos suspensivos detras.
        internal static string RecortaLejos(AjusteLetra ajuste, string texto, Rocola.Nativo.Letra f, float ancho)   // EN LINUX: del Graphics con que se mide solo pesa su modo de letra; la letra es la nativa
        {
            if (string.IsNullOrEmpty(texto) || AnchoLejos(ajuste, texto, f) <= ancho) return texto;   // EN LINUX: con el modo de letra
            int lo = 0, hi = texto.Length - 1;   // cuantas letras caben, contando los puntos
            while (lo < hi)
            {
                int m = (lo + hi + 1) / 2;
                if (AnchoLejos(ajuste, texto.Substring(0, m).TrimEnd() + "\u2026", f) <= ancho) lo = m; else hi = m - 1;   // EN LINUX: con el modo de letra
            }
            if (lo > 0 && char.IsHighSurrogate(texto[lo - 1])) lo--;   // sin partir un caracter doble
            return lo > 0 ? texto.Substring(0, lo).TrimEnd() + "\u2026" : "";
        }
        private static float AnchoLejos(AjusteLetra ajuste, string s, Rocola.Nativo.Letra f)   // EN LINUX: el modo de letra del Graphics con que se mide y la letra nativa
        {
            return TextoGdiPlus.Mide(s, f, ajuste, SizeF.Empty, FormatoTexto.TipograficoCompartido).Width;   // EN LINUX: g.MeasureString(s, f, PointF.Empty, GenericTypographic)
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

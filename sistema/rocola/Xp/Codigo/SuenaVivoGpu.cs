// LA PANTALLA VIVA, TANDA 3: LO QUE SUENA Y LOS CREDITOS. Las ideas del lienzo de bocetos para lo que suena, las
// monedas y los avisos (24-09-2026). Solo con tarjeta grafica.
//
// Cada idea tiene su interruptor en Ctrl+F3 -> Pantalla -> Lo que suena y los creditos y se guarda en rocola_config.txt.
// Sin tarjeta grafica no hacen nada: el dibujo por procesador esta congelado y sale exactamente como antes.
//
//  - LO QUE SUENA, EN GRANDE. El panel que crece cuando nadie toca la rocola lleva el titulo en una sola linea que se desliza si no cabe, cuanto falta ("1:47 · faltan 2:25"), la pastilla
//    del genero con un brillo que late con los graves, y en las siguientes cuanto falta para que suenen.
//  - LA MONEDA QUE CAE. Al meter una moneda cae una moneda dorada girando hasta la capsula de Creditos; la capsula da
//    un golpe con un destello verde, el numero rueda y sube "+4 creditos".
//  - SIN CREDITOS, INVITAR. Si alguien intenta poner algo sin creditos, en vez del letrero naranja sale una tarjeta
//    amable con la ranura y una moneda entrando: "Te falta un credito. Inserta una moneda para poner «...»". Se va sola.
//  - AVISOS QUE SE ENTIENDEN. Los avisos entran por la derecha, con su color y su icono segun lo que dicen (hecho,
//    cuidado, error, novedad), en dos renglones, y con una linea que va enseñando cuanto les queda.
using System;
using System.Collections.Generic;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: lo que suena en grande, la moneda, la invitacion y los avisos dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; lo que aun no esta traducido pasa por el puente (Plataforma/Puente.cs y Puente.suena.cs)

namespace Rocola
{
    public partial class Form1
    {
        // Existen en los dos perfiles porque la configuracion es la misma; sin tarjeta grafica no se usan.
        private bool suenaGrande = true;          // "suena_grande="
        private bool monedaCae = true;            // "moneda_cae="
        private bool sinCreditosInvita = true;    // "sin_creditos_invita="
        private bool avisosClaros = true;         // "avisos_claros="

        // LOS ENGANCHES, en los dos perfiles: sin tarjeta grafica no hacen nada y todo sigue exactamente como antes.
        //
        // Sin creditos: si sale la tarjeta que invita, el que llama ya no pone el letrero naranja.
        private bool InvitaSinCredito(PlayItem it)
        {
#if ROCOLA_GPU
            if (InvitaAPoner(it)) return true;
            if (AvisoClaro()) PreparaAvisoPropio(TipoAviso.Cuidado, "Te falta un crédito", "Inserta una moneda");
#endif
            return false;
        }
        // La cancion recien pedida: el aviso dice cual es y cuanto falta ("Pusiste «El Rey» · va la 3.ª en la cola").
        private void AvisoPedido()
        {
#if ROCOLA_GPU
            if (!AvisoClaro() || ultimoPedido == null) return;
            string tit, art; ultimoPedido.TituloArtista(out tit, out art); tit = TituloSinPista(tit);
            bool yaSuena = ReferenceEquals(ultimoPedido, nowPlaying);
            string t, d; TextoPedido(tit, turnoPuesto, turnoOn ? turnoSegs : -1, yaSuena, ultimoPedido.IsVideo, out t, out d);
            PreparaAvisoPropio(TipoAviso.Hecho, t, d);
#endif
        }
        // Lo que dura el aviso a la vista (para sumar monedas o canciones seguidas mientras se ve).
        private int VidaAviso()
        {
#if ROCOLA_GPU
            if (AvisoClaro()) return AvisoMs;
#endif
            return 1300;
        }
        // En el buscador, el aviso claro va encima de todo como los demas (el buscador no lo pinta en su imagen).
        private bool AvisoEnBuscador(string texto, bool bien)
        {
#if ROCOLA_GPU
            if (!AvisoClaro()) return false;
            HorneaAviso(texto, bien ? Color.FromArgb(40, 165, 90) : Color.FromArgb(200, 130, 35));
            return true;
#else
            return false;
#endif
        }
#if ROCOLA_GPU
        private PlayItem ultimoPedido;   // lo ultimo que se pidio (AddItem), para el aviso
#endif
    }

#if ROCOLA_GPU
    // Lo que el panel de la tarjeta necesita de cada idea. Los tiempos van en el reloj del panel (GpuPanel.AhoraMs):
    // asi cada cuadro coloca las cosas para cuando se VERA, igual que la estrella y el boleto.
    internal struct GpuAviso { public bool Has; public SKBitmap Bmp; public int Ver; public double Desde; public float X, Y; public uint Color; }   // EN LINUX: el mapa es un SKBitmap (Upload, aun sin traducir, lo recibe por el puente: PreparaSuenaVivo)
    internal struct GpuInvita { public bool Has; public SKBitmap Bmp; public int Ver; public double Desde, Hasta; public float X, Y, RanuraX, RanuraY, MonedaR; public uint Color; }   // EN LINUX: el mapa es un SKBitmap
    internal struct GpuMoneda { public bool Has; public double Desde; public float X0, Y0, X1, Y1, R, GolpeX, GolpeY, GolpeR; public SKBitmap Mas; public int MasVer; public float MasX, MasY; }   // EN LINUX: el mapa es un SKBitmap
    internal struct GpuReposo
    {
        public bool Has;
        public SKBitmap Titulo; public int TituloVer; public float TituloX, TituloY, TituloW, TituloH, TituloExceso;   // EN LINUX: el mapa es un SKBitmap
        public RectangleF Genero; public uint GeneroColor;
    }

    // Lo que el panel de reposo pinta de mas con "Lo que suena, en grande" (EstadoReposoGpu.cs). Sin esto (null), el de
    // siempre.
    internal sealed class ReposoGrande
    {
        public string Genero = ""; public Color GeneroColor;
        public int[] Minutos;           // cuanto falta para cada una de la cola (-1 = no se sabe)
        // Lo que devuelve el horneado: donde quedo el titulo (si no cupo, lo pinta la tarjeta deslizandose) y la pastilla.
        public bool TituloFuera; public RectangleF TituloCaja; public RectangleF GeneroCaja;
        public string Titulo = ""; public int W, H;   // para hornear el titulo que se desliza (al pedirlo la tarjeta)
    }

    public partial class Form1
    {
        // ======================================================================================================
        //  AVISOS QUE SE ENTIENDEN
        // ======================================================================================================
        internal enum TipoAviso { Hecho, Cuidado, Error, Novedad }
        internal const int AvisoMs = 2800, AvisoEntraMs = 220, AvisoSaleMs = 320;
        internal const float AvisoCorre = 64f;      // lo que entra desde la derecha
        internal const float AvisoAire = 2f;        // aire transparente alrededor (la tarjeta lo dibuja con filtro)
        internal const float AvisoArriba = 80f;     // a la misma altura que el aviso de siempre
        private SKBitmap avisoBmp; private int avisoVer; private GpuAviso aviso;   // EN LINUX: el mapa es un SKBitmap (Nativo/Mapas.cs)
        private Rocola.Nativo.Letra fAvisoTit, fAvisoDet;   // EN LINUX: las letras son Letra (Nativo/Letras.cs; con su espacio entero: Form1 tiene un metodo Letra)
        // Un titulo y un renglon a medida para el proximo aviso: lo pone quien sabe mas que el texto ("Pusiste «El Rey»").
        private bool avisoPropio; private TipoAviso avisoPropioTipo; private string avisoTitulo = "", avisoDetalle = "";

        private bool AvisoClaro() { return avisosClaros && gpuEnabled && gpuPanel != null; }

        // El tipo, por el color con que lo pide cada sitio (asi no hay que tocar los cuarenta sitios que avisan).
        internal static TipoAviso TipoDeColor(Color c, string texto)
        {
            string t = texto ?? "";
            if (t.StartsWith("Carpetas actualizadas") || t.StartsWith("Reanudando")) return TipoAviso.Novedad;
            int rgb = c.ToArgb() & 0xFFFFFF;
            if (rgb == 0x28A55A || rgb == 0x28825A) return TipoAviso.Hecho;       // verde de "hecho" y el de los ajustes
            if (rgb == 0xC88223 || rgb == 0x963C2D) return TipoAviso.Cuidado;     // naranja y el del repaso a fondo
            if (rgb == 0xC85046) return TipoAviso.Error;                          // rojo de "no se pudo"
            if (c.B > c.R && c.B > c.G) return TipoAviso.Novedad;
            if (c.G >= c.R) return TipoAviso.Hecho;
            if (c.R > c.G * 1.8f) return TipoAviso.Error;
            return TipoAviso.Cuidado;
        }
        // Colores del boceto: el del borde, el icono y la linea; y el fondo, del mismo tono pero oscuro.
        internal static Color ColorAviso(TipoAviso t)
        {
            switch (t)
            {
                case TipoAviso.Cuidado: return Color.FromArgb(242, 206, 72);
                case TipoAviso.Error: return Color.FromArgb(240, 100, 96);
                case TipoAviso.Novedad: return Color.FromArgb(150, 200, 255);
                default: return ColaVerde;
            }
        }
        internal static Color FondoAviso(TipoAviso t)
        {
            switch (t)
            {
                case TipoAviso.Cuidado: return Color.FromArgb(244, 52, 40, 8);
                case TipoAviso.Error: return Color.FromArgb(244, 62, 18, 18);
                case TipoAviso.Novedad: return Color.FromArgb(244, 14, 28, 60);
                default: return Color.FromArgb(244, 14, 52, 34);
            }
        }
        // "No se pudo reproducir: credito devuelto" -> titulo y renglon. Se parte por el primer ": " o por " - ".
        internal static void PartirAviso(string texto, out string titulo, out string detalle)
        {
            string t = (texto ?? "").Trim();
            titulo = t; detalle = "";
            int p = t.IndexOf(": ", StringComparison.Ordinal);
            if (p <= 0 || p >= t.Length - 2) p = -1;
            int q = t.IndexOf(" - ", StringComparison.Ordinal);
            if (p < 0 && q > 0 && q < t.Length - 3) { titulo = t.Substring(0, q); detalle = t.Substring(q + 3); return; }
            if (p > 0) { titulo = t.Substring(0, p); detalle = t.Substring(p + 2); }
        }
        // El aviso de una cancion recien pedida, en dos renglones. 'puesto' es el de la cola al pedirla (1 = la siguiente)
        // y 'segundos' lo que falta para que suene (-1 = no se sabe). 'yaSuena' si no habia nada y ya empezo.
        internal static void TextoPedido(string cancion, int puesto, double segundos, bool yaSuena, bool esVideo, out string titulo, out string detalle)
        {
            string que = string.IsNullOrEmpty(cancion) ? (esVideo ? "tu video" : "tu canción") : ("«" + cancion + "»");
            titulo = "Pusiste " + que;
            if (yaSuena) { detalle = "ya está sonando"; return; }
            if (puesto <= 0) { detalle = "va a la cola"; return; }
            detalle = (puesto == 1) ? "es la siguiente" : ("va la " + puesto + ".ª en la cola");
            if (segundos >= 0) detalle += (segundos < 60) ? " · en menos de 1 min" : (" · en ~" + (int)((segundos + 30) / 60) + " min");
        }
        private void PreparaAvisoPropio(TipoAviso tipo, string titulo, string detalle)
        { avisoPropio = true; avisoPropioTipo = tipo; avisoTitulo = titulo ?? ""; avisoDetalle = detalle ?? ""; }

        // Lo llama HorneaToast (Form1.Ajustes.cs) en vez de hornear el aviso de siempre.
        private void HorneaAviso(string texto, Color fondo)
        {
            TipoAviso tipo; string tit, det;
            if (avisoPropio) { tipo = avisoPropioTipo; tit = avisoTitulo; det = avisoDetalle; avisoPropio = false; }
            else { tipo = TipoDeColor(fondo, texto); PartirAviso(texto, out tit, out det); }
            if (fAvisoTit == null) fAvisoTit = Rocola.Nativo.Letra.EnPuntos(PuenteDibujo.Letra(lblToast.Font).Familia, 14f, true);   // EN LINUX: new Font(lblToast.Font.FontFamily, 14f, FontStyle.Bold): la familia de la etiqueta (aun sin traducir) por el puente; en puntos y negrita
            if (fAvisoDet == null) fAvisoDet = Rocola.Nativo.Letra.EnPuntos(PuenteDibujo.Letra(lblToast.Font).Familia, 11f, true);   // EN LINUX: new Font(lblToast.Font.FontFamily, 11f, FontStyle.Bold)
            int borde = BordeLibreAvisos();   // con video en ventana, a la izquierda de el (si no, quedaba detras)
            Size tam = TamanoAviso(Math.Min(ClientSize.Width, borde), tit, det, fAvisoTit, fAvisoDet);
            if (avisoBmp == null || avisoBmp.Width != tam.Width || avisoBmp.Height != tam.Height)
            {
                if (avisoBmp != null) avisoBmp.Dispose();
                avisoBmp = Mapas.Nuevo(tam.Width, tam.Height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            }
            PintaAviso(avisoBmp, tipo, tit, det, fAvisoTit, fAvisoDet);
            aviso.Has = true; aviso.Bmp = avisoBmp; aviso.Ver = ++avisoVer;
            aviso.Desde = GpuPanel.AhoraMs();
            aviso.X = Math.Max(8f, borde - 18f - tam.Width); aviso.Y = AvisoArriba;
            Color c = ColorAviso(tipo);
            aviso.Color = 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            toastTick = -1;   // el aviso de siempre no sale
        }
        private GpuAviso BuildAvisoState()
        {
            if (aviso.Has && GpuPanel.AhoraMs() - aviso.Desde > AvisoMs + 40) aviso.Has = false;
            return aviso;
        }
        // Ancho: un 40 % de la pantalla, entre 360 y 560 px, o menos si el texto es corto. Alto: lo que pidan los renglones.
        internal static Size TamanoAviso(int anchoPantalla, string titulo, string detalle, Rocola.Nativo.Letra fTit, Rocola.Nativo.Letra fDet)   // EN LINUX: las letras son Letra; quien aun pasa Font llega por el puente (Plataforma/Puente.suena.cs)
        {
            int hTit = fTit.AltoEntero, hDet = string.IsNullOrEmpty(detalle) ? 0 : fDet.AltoEntero;   // EN LINUX: Font.Height (el alto del renglon redondeado hacia arriba)
            int alto = Math.Max(56, 12 + hTit + (hDet > 0 ? 2 + hDet : 0) + 14);
            int icono = Math.Min(44, alto - 22);
            int texto = Math.Max(AnchoTextoSuena(titulo ?? "", fTit), hDet > 0 ? AnchoTextoSuena(detalle ?? "", fDet) : 0);   // EN LINUX: AnchoTexto (Form1.Ajustes.cs, aun sin traducir) mide con una Font: se la da el puente (Plataforma/Puente.suena.cs)
            int max = Math.Max(360, Math.Min(560, (int)(anchoPantalla * .40f)));
            int w = 16 + icono + 14 + texto + 22;
            if (w > max) w = max;
            if (w < 300) w = 300;
            return new Size(w + (int)(2 * AvisoAire), alto + (int)(2 * AvisoAire));
        }
        internal static float RadioAviso(int alto) { return Math.Min(16f, alto * .26f); }
        // El dibujo, aparte del estado para poder mirarlo sin encender nada.
        internal static void PintaAviso(SKBitmap bmp, TipoAviso tipo, string titulo, string detalle, Rocola.Nativo.Letra fTit, Rocola.Nativo.Letra fDet)   // EN LINUX: el mapa es un SKBitmap y las letras Letra; quien aun pasa Bitmap y Font llega por el puente (Plataforma/Puente.suena.cs)
        {
            int w = bmp.Width, h = bmp.Height;
            Color col = ColorAviso(tipo);
            using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto izq = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable (el medio pixel y la copia, los de fabrica)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // gris: ClearType deja flecos sobre transparente (EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable)
                RectangleF caja = new RectangleF(AvisoAire, AvisoAire, w - 2 * AvisoAire - .5f, h - 2 * AvisoAire - .5f);
                using (Rocola.Dibujo2D.GraphicsPath p = Redondo(caja, RadioAviso(h)))   // EN LINUX: el trazo redondo lo hace PantallaVivaGpu.cs (aun sin traducir) como GraphicsPath: se pinta su SKPath (PuenteDibujo.Ruta)
                {
                    using (SKPaint b = new SKPaint { Color = FondoAviso(tipo).Sk() }) Pixel.Rellena(g, PuenteDibujo.Ruta(p), b, suave);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pe = new SKPaint { Color = col.Sk(), StrokeWidth = 1.5f }) Pixel.Traza(g, PuenteDibujo.Ruta(p), pe, suave);   // EN LINUX: Pen(col, 1.5) y g.DrawPath
                }
                float alto = caja.Height;
                float lado = Math.Min(44f, alto - 22f);
                RectangleF ri = new RectangleF(caja.X + 16f, caja.Y + (alto - lado) * .5f - 1f, lado, lado);
                using (SKPaint aro = new SKPaint { Color = col.Sk(), StrokeWidth = 2f }) Pixel.TrazaOvalo(g, ri, aro, suave);   // EN LINUX: Pen(col, 2) y g.DrawEllipse
                PintaIcono(g, suave, tipo, ri, col);   // EN LINUX: el lienzo y su suavizado
                izq.Banderas = BanderasTexto.SinPartir; izq.Corte = CorteTexto.PuntosPorLetra;   // EN LINUX: FormatFlags = NoWrap (solo esa) y Trimming = EllipsisCharacter
                izq.Vertical = Alinea.Cerca;   // EN LINUX: LineAlignment = Near
                float xt = ri.Right + 14f, wt = Math.Max(20f, caja.Right - xt - 16f);
                bool dos = !string.IsNullOrEmpty(detalle);
                float bloque = fTit.AltoEntero + (dos ? 2 + fDet.AltoEntero : 0);   // EN LINUX: Font.Height
                float yt = caja.Y + (alto - bloque) * .5f - 2f;
                using (SKPaint b = new SKPaint { Color = Color.White.Sk() })   // EN LINUX: SolidBrush(Color.White)
                    TextoGdiPlus.Escribe(g, titulo ?? "", fTit, ajuste, b, new RectangleF(xt, yt, wt, fTit.AltoEntero + 2), izq);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs), con el modo de letra del lienzo; Font.Height
                if (dos)
                    using (SKPaint b = new SKPaint { Color = Color.FromArgb(214, 222, 245).Sk() })   // EN LINUX: SolidBrush
                        TextoGdiPlus.Escribe(g, detalle, fDet, ajuste, b, new RectangleF(xt, yt + fTit.AltoEntero + 2, wt, fDet.AltoEntero + 2), izq);   // EN LINUX: g.DrawString; Font.Height
            }
        }
        // Los iconos del boceto, trazados en una caja de 24x24 y escalados al circulo.
        private static void PintaIcono(SKCanvas g, bool suave, TipoAviso tipo, RectangleF r, Color col)   // EN LINUX: el lienzo de Skia y su suavizado (el medio pixel y la copia, los de fabrica: los del aviso)
        {
            float k = r.Width / 44f * 24f / 24f * (44f / 24f) * .52f;   // el icono ocupa algo mas de la mitad del circulo
            float ox = r.X + r.Width * .5f - 12f * k, oy = r.Y + r.Height * .5f - 12f * k;
            using (SKPaint pe = new SKPaint { Color = col.Sk(), StrokeWidth = Math.Max(1.6f, 2.6f * k) })   // EN LINUX: Pen(col, ancho)
            {
                pe.StrokeCap = SKStrokeCap.Round; pe.StrokeJoin = SKStrokeJoin.Round;   // EN LINUX: StartCap y EndCap = Round (en Skia, una punta para las dos) y LineJoin = Round
                switch (tipo)
                {
                    case TipoAviso.Hecho:
                        Pixel.Lineas(g, new PointF[] { P(ox, oy, k, 4f, 12.5f), P(ox, oy, k, 9.5f, 18f), P(ox, oy, k, 20f, 6f) }, pe, suave);   // EN LINUX: g.DrawLines (Nativo/Pixel.cs)
                        break;
                    case TipoAviso.Cuidado:
                        { PointF a = P(ox, oy, k, 12f, 6f), b = P(ox, oy, k, 12f, 13.5f); Pixel.Linea(g, a.X, a.Y, b.X, b.Y, pe, suave); }   // EN LINUX: g.DrawLine(pe, a, b)
                        using (SKPaint b = new SKPaint { Color = col.Sk() }) { PointF d = P(ox, oy, k, 12f, 18f); float rr = 1.7f * k; Pixel.RellenaOvalo(g, new RectangleF(d.X - rr, d.Y - rr, 2 * rr, 2 * rr), b, suave); }   // EN LINUX: SolidBrush y g.FillEllipse
                        break;
                    case TipoAviso.Error:
                        { PointF a = P(ox, oy, k, 7f, 7f), b = P(ox, oy, k, 17f, 17f); Pixel.Linea(g, a.X, a.Y, b.X, b.Y, pe, suave); }   // EN LINUX: g.DrawLine(pe, a, b)
                        { PointF a = P(ox, oy, k, 17f, 7f), b = P(ox, oy, k, 7f, 17f); Pixel.Linea(g, a.X, a.Y, b.X, b.Y, pe, suave); }   // EN LINUX: g.DrawLine(pe, a, b)
                        break;
                    default:   // una nota musical
                        Pixel.Lineas(g, new PointF[] { P(ox, oy, k, 9f, 18f), P(ox, oy, k, 9f, 6f), P(ox, oy, k, 20f, 4f), P(ox, oy, k, 20f, 16f) }, pe, suave);   // EN LINUX: g.DrawLines
                        Pixel.TrazaOvalo(g, new RectangleF(ox + (6.5f - 2.5f) * k, oy + (18f - 2.5f) * k, 5f * k, 5f * k), pe, suave);   // EN LINUX: g.DrawEllipse
                        Pixel.TrazaOvalo(g, new RectangleF(ox + (17.5f - 2.5f) * k, oy + (16f - 2.5f) * k, 5f * k, 5f * k), pe, suave);   // EN LINUX: g.DrawEllipse
                        break;
                }
            }
        }
        private static PointF P(float ox, float oy, float k, float x, float y) { return new PointF(ox + x * k, oy + y * k); }

        // ======================================================================================================
        //  SIN CREDITOS, INVITAR
        // ======================================================================================================
        internal const int InvitaMs = 5200, InvitaEntraMs = 200, InvitaSaleMs = 260, InvitaMonedaMs = 2600;
        private SKBitmap invitaBmp; private int invitaVer; private GpuInvita invita;   // EN LINUX: el mapa es un SKBitmap (Nativo/Mapas.cs)
        private Rocola.Nativo.Letra fInvTit, fInvTxt, fInvPie;   // EN LINUX: las letras son Letra (Nativo/Letras.cs)

        // Devuelve si salio (si no, el que llama pone el letrero de siempre). Solo con la tarjeta dibujando y la franja a
        // la vista: es donde vive la capsula de creditos a la que invita.
        private bool InvitaAPoner(PlayItem it)
        {
            if (!sinCreditosInvita || !gpuEnabled || gpuPanel == null || !gpuPanel.Visible || attractOn || videoFull) return false;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 400 || h < 300) return false;
            string que = "";
            if (it != null) { string tit, art; it.TituloArtista(out tit, out art); que = TituloSinPista(tit) ?? ""; }
            if (fInvTit == null) fInvTit = Rocola.Nativo.Letra.EnPuntos(PuenteDibujo.Letra(lblToast.Font).Familia, 17f, true);   // EN LINUX: new Font(lblToast.Font.FontFamily, 17f, FontStyle.Bold): la familia de la etiqueta (aun sin traducir) por el puente
            if (fInvTxt == null) fInvTxt = Rocola.Nativo.Letra.EnPuntos(PuenteDibujo.Letra(lblToast.Font).Familia, 12.5f, true);   // EN LINUX: new Font(lblToast.Font.FontFamily, 12.5f, FontStyle.Bold)
            if (fInvPie == null) fInvPie = Rocola.Nativo.Letra.EnPuntos(PuenteDibujo.Letra(lblToast.Font).Familia, 10.5f, true);   // EN LINUX: new Font(lblToast.Font.FontFamily, 10.5f, FontStyle.Bold)
            int libre = BordeLibreAvisos();   // con video en ventana, centrada en lo que se ve (si no, la tapaba a medias)
            Size tam = TamanoInvita(libre);
            if (invitaBmp == null || invitaBmp.Width != tam.Width || invitaBmp.Height != tam.Height)
            {
                if (invitaBmp != null) invitaBmp.Dispose();
                invitaBmp = Mapas.Nuevo(tam.Width, tam.Height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            }
            PointF ranura; float r;
            PintaInvita(invitaBmp, que, it != null && it.IsVideo, CreditsPerCoin, fInvTit, fInvTxt, fInvPie, out ranura, out r);
            double ahora = GpuPanel.AhoraMs();
            // Si ya estaba a la vista, no vuelve a entrar: solo se alarga (quien insiste ve que la tarjeta sigue ahi).
            bool yaEstaba = invita.Has && ahora < invita.Hasta - InvitaSaleMs;
            invita.Has = true; invita.Bmp = invitaBmp; invita.Ver = ++invitaVer;
            if (!yaEstaba) invita.Desde = ahora;
            invita.Hasta = ahora + InvitaMs;
            invita.X = Math.Max(0f, (libre - tam.Width) * .5f);
            invita.Y = Math.Max(90f, (BotStripTop(h) - tam.Height) * .5f);
            invita.RanuraX = ranura.X; invita.RanuraY = ranura.Y; invita.MonedaR = r;
            invita.Color = 0xFFF2CE48u;
            return true;
        }
        // Al entrar una moneda la tarjeta ya no hace falta: se va sin esperar a su tiempo.
        private void CierraInvita()
        {
            if (!invita.Has) return;
            double ahora = GpuPanel.AhoraMs();
            if (invita.Hasta > ahora + InvitaSaleMs) invita.Hasta = ahora + InvitaSaleMs;
        }
        private GpuInvita BuildInvitaState()
        {
            if (invita.Has && credits > 0) CierraInvita();   // ya hay creditos (una moneda, o los de servicio): se va
            if (invita.Has && GpuPanel.AhoraMs() > invita.Hasta + 40) invita.Has = false;
            return invita;
        }
        // Un 44 % de la pantalla de ancho (entre 420 y 620 px) y un 42 % de eso de alto.
        internal static Size TamanoInvita(int anchoPantalla)
        {
            int w = Math.Max(420, Math.Min(620, (int)(anchoPantalla * .44f)));
            if (w > anchoPantalla - 40) w = Math.Max(200, anchoPantalla - 40);
            int h = Math.Max(170, (int)(w * .42f));
            return new Size(w, h);
        }
        // 'ranura' y 'radio' devuelven donde queda la ranura (dentro de la imagen) y el tamaño de la moneda que entra.
        internal static void PintaInvita(SKBitmap bmp, string cancion, bool esVideo, int creditosPorMoneda, Rocola.Nativo.Letra fTit, Rocola.Nativo.Letra fTxt, Rocola.Nativo.Letra fPie, out PointF ranura, out float radio)   // EN LINUX: el mapa es un SKBitmap y las letras Letra; quien aun pasa Bitmap y Font llega por el puente (Plataforma/Puente.suena.cs)
        {
            int w = bmp.Width, h = bmp.Height;
            Color oro = Color.FromArgb(242, 206, 72);
            float pad = Math.Max(18f, h * .12f);
            float lado = Math.Min(h - 2 * pad - 10f, w * .24f);
            RectangleF caja = new RectangleF(2f, 2f, w - 4.5f, h - 4.5f);
            RectangleF hueco = new RectangleF(caja.X + pad, caja.Y + (caja.Height - lado) * .5f - 3f, lado, lado);
            ranura = new PointF(hueco.X + lado * .38f, hueco.Y + lado * .50f);
            radio = lado * .24f;
            using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto izq = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable (el medio pixel y la copia, los de fabrica)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                using (Rocola.Dibujo2D.GraphicsPath p = Redondo(caja, 18f))   // EN LINUX: el trazo redondo lo hace PantallaVivaGpu.cs (aun sin traducir) como GraphicsPath: se pinta su SKPath (PuenteDibujo.Ruta)
                {
                    using (SKPaint b = new SKPaint { Color = Color.FromArgb(240, 40, 30, 8).Sk() }) Pixel.Rellena(g, PuenteDibujo.Ruta(p), b, suave);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pe = new SKPaint { Color = Color.FromArgb(190, oro).Sk(), StrokeWidth = 2f }) Pixel.Traza(g, PuenteDibujo.Ruta(p), pe, suave);   // EN LINUX: Pen(color, 2) y g.DrawPath
                }
                // La caja de la ranura, con la ranura oscura: la moneda (la pone la tarjeta, animada) entra en ella.
                using (Rocola.Dibujo2D.GraphicsPath p = Redondo(hueco, lado * .14f))   // EN LINUX: el trazo de PantallaVivaGpu.cs, como arriba
                {
                    using (SKPaint b = new SKPaint { Color = Color.FromArgb(90, 0, 0, 0).Sk() }) Pixel.Rellena(g, PuenteDibujo.Ruta(p), b, suave);   // EN LINUX: SolidBrush y g.FillPath
                    using (SKPaint pe = new SKPaint { Color = Color.FromArgb(100, oro).Sk(), StrokeWidth = 1f }) Pixel.Traza(g, PuenteDibujo.Ruta(p), pe, suave);   // EN LINUX: Pen(color, 1) y g.DrawPath
                }
                RectangleF rr = new RectangleF(ranura.X - lado * .20f, ranura.Y - lado * .04f, lado * .40f, lado * .08f);
                using (Rocola.Dibujo2D.GraphicsPath p = Redondo(rr, lado * .04f))   // EN LINUX: el trazo de PantallaVivaGpu.cs, como arriba
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(255, 10, 10, 10).Sk() }) Pixel.Rellena(g, PuenteDibujo.Ruta(p), b, suave);   // EN LINUX: SolidBrush y g.FillPath
                float xt = hueco.Right + pad, wt = Math.Max(40f, caja.Right - xt - pad);
                izq.Corte = CorteTexto.PuntosPorPalabra; izq.Banderas = BanderasTexto.SoloEnteros;   // EN LINUX: Trimming = EllipsisWord y FormatFlags = LineLimit (solo esa)
                string que = string.IsNullOrEmpty(cancion) ? "" : (" «" + cancion + "»");
                string cuerpo = "Inserta una moneda para poner" + (que.Length > 0 ? que : (esVideo ? " tu video" : " tu canción")) + ".";
                string pie = "Una moneda = " + creditosPorMoneda + (esVideo ? (creditosPorMoneda == 1 ? " video" : " videos") : (creditosPorMoneda == 1 ? " canción" : " canciones"));
                float hTit = fTit.AltoEntero, hTxt = fTxt.AltoEntero * 2 + 2, hPie = fPie.AltoEntero;   // EN LINUX: Font.Height
                float bloque = hTit + 6 + hTxt + 8 + hPie;
                float y = caja.Y + (caja.Height - bloque) * .5f - 3f;
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(255, 232, 154).Sk() })   // EN LINUX: SolidBrush
                    TextoGdiPlus.Escribe(g, "Te falta un crédito", fTit, ajuste, b, new RectangleF(xt, y, wt, hTit + 2), izq);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs), con el modo de letra del lienzo
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(255, 243, 207).Sk() })   // EN LINUX: SolidBrush
                    TextoGdiPlus.Escribe(g, cuerpo, fTxt, ajuste, b, new RectangleF(xt, y + hTit + 6, wt, hTxt + 2), izq);   // EN LINUX: g.DrawString
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(217, 196, 138).Sk() })   // EN LINUX: SolidBrush
                    TextoGdiPlus.Escribe(g, pie, fPie, ajuste, b, new RectangleF(xt, y + hTit + 6 + hTxt + 8, wt, hPie + 2), izq);   // EN LINUX: g.DrawString
            }
        }

        // ======================================================================================================
        //  LA MONEDA QUE CAE
        // ======================================================================================================
        internal const float MonedaAltura = 3.6f;   // desde cuantas capsulas de alto cae
        private GpuMoneda moneda; private int monedaVista; private SKBitmap monedaMas; private int monedaMasVer;   // EN LINUX: el mapa es un SKBitmap (Nativo/Mapas.cs)
        private Rocola.Nativo.Letra fMonedaMas;   // EN LINUX: la letra es una Letra (Nativo/Letras.cs)
        // Si se va a ver: la tarjeta dibujando y la franja de abajo a la vista (en atraccion va limpia).
        private bool MonedaSeVe()
        {
            return monedaCae && gpuEnabled && gpuPanel != null && gpuPanel.Visible && !attractOn && !videoFull
                && pastCred != null && pastCred.Height >= 8;
        }
        // La capsula de creditos arranca su ceremonia (PastillaView.Moneda) al entrar una moneda o unos creditos de
        // servicio: se nota aqui, por su reloj, sin tocar a quien la llama.
        private GpuMoneda BuildMonedaState()
        {
            int t = (pastCred != null) ? pastCred.MonedaTick : 0;
            if (t != 0 && t != monedaVista)
            {
                monedaVista = t;
                if (MonedaSeVe()) SueltaMoneda();
            }
            if (moneda.Has && GpuPanel.AhoraMs() - moneda.Desde > MonedaTotalMs + 60) moneda.Has = false;
            return moneda;
        }
        internal const int MonedaMasMs = 1100;                                          // lo que dura el "+4 creditos"
        // Lo que tarda en caer: lo mismo que la monedita de la capsula (asi el numero rueda cuando aterriza).
        internal static int MonedaCaeMs { get { return PastillaView.MonedaMsGpu; } }
        internal static int MonedaTotalMs { get { return MonedaCaeMs + MonedaMasMs; } }
        private void SueltaMoneda()
        {
            Rectangle pc = pastCred.Bounds;
            float h = pc.Height, xn = pc.Left + pastCred.XNumero();
            moneda.Has = true; moneda.Desde = GpuPanel.AhoraMs();
            moneda.R = h * .55f;
            moneda.X1 = xn; moneda.Y1 = pc.Top + h * .5f;
            moneda.X0 = xn - h * .35f; moneda.Y0 = pc.Top - h * MonedaAltura;
            moneda.GolpeX = pc.Left + pc.Width * .5f; moneda.GolpeY = pc.Top + h * .5f; moneda.GolpeR = Math.Max(pc.Width * .62f, h * 1.6f);
            int de, a;
            int.TryParse(pastCred.ValorAntes ?? "", out de);
            int.TryParse(pastCred.Valor ?? "", out a);
            int mas = a - de; if (mas <= 0) mas = CreditsPerCoin;
            if (fMonedaMas == null) fMonedaMas = Rocola.Nativo.Letra.EnPixeles(PuenteDibujo.Letra(lblToast.Font).Familia, Math.Max(11f, h * .40f), true);   // EN LINUX: new Font(lblToast.Font.FontFamily, px, FontStyle.Bold, GraphicsUnit.Pixel): la familia de la etiqueta (aun sin traducir) por el puente; en pixeles y negrita
            string txt = "+" + mas + (mas == 1 ? " crédito" : " créditos");
            Size tm = TamanoMas(txt, fMonedaMas);
            if (monedaMas == null || monedaMas.Width != tm.Width || monedaMas.Height != tm.Height)
            {
                if (monedaMas != null) monedaMas.Dispose();
                monedaMas = Mapas.Nuevo(tm.Width, tm.Height);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            }
            PintaMas(monedaMas, txt, fMonedaMas);
            moneda.Mas = monedaMas; moneda.MasVer = ++monedaMasVer;
            moneda.MasX = Math.Min(ClientSize.Width - tm.Width - 6f, xn + h * .2f); moneda.MasY = pc.Top - tm.Height * .6f;
            CierraInvita();   // quien estaba mirando la invitacion ya tiene su moneda
        }
        internal static Size TamanoMas(string txt, Rocola.Nativo.Letra f) { return new Size(AnchoTextoSuena(txt, f) + 24, f.AltoEntero + 14); }   // EN LINUX: la letra es una Letra (quien aun pasa Font llega por el puente); AnchoTexto (Form1.Ajustes.cs, aun sin traducir) con la Font que le da el puente (Plataforma/Puente.suena.cs); Font.Height
        internal static void PintaMas(SKBitmap bmp, string txt, Rocola.Nativo.Letra f)   // EN LINUX: el mapa es un SKBitmap y la letra una Letra; quien aun pasa Bitmap y Font llega por el puente (Plataforma/Puente.suena.cs)
        {
            using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto sf = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                // EN LINUX: g.SmoothingMode = AntiAlias: aqui solo se escribe texto, que no lo mira (el texto se suaviza por el modo de letra)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                sf.Horizontal = Alinea.Centro; sf.Vertical = Alinea.Centro; sf.Banderas = BanderasTexto.SinPartir;   // EN LINUX: Alignment y LineAlignment = Center, FormatFlags = NoWrap (solo esa)
                RectangleF r = new RectangleF(0, 0, bmp.Width, bmp.Height);
                // Un reborde oscuro fino: sobre el cielo claro de la aurora se sigue leyendo.
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(150, 6, 30, 14).Sk() })   // EN LINUX: SolidBrush
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                        if (dx != 0 || dy != 0) TextoGdiPlus.Escribe(g, txt, f, ajuste, b, new RectangleF(dx * 1.2f, dy * 1.2f, bmp.Width, bmp.Height), sf);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                using (SKPaint b = new SKPaint { Color = Color.FromArgb(120, 240, 140).Sk() }) TextoGdiPlus.Escribe(g, txt, f, ajuste, b, r, sf);   // EN LINUX: SolidBrush y g.DrawString
            }
        }

        // ======================================================================================================
        //  LO QUE SUENA, EN GRANDE (el panel de reposo)
        // ======================================================================================================
        private SKBitmap tituloReposoBmp; private int tituloReposoVer; private string tituloReposoDe; private int tituloReposoAlto = -1;   // EN LINUX: el mapa es un SKBitmap (Nativo/Mapas.cs)
        private ReposoGrande reposoUltimo;
        private Rocola.Nativo.Letra fTituloReposo; private int fTituloW, fTituloH;   // EN LINUX: la letra es una Letra (Nativo/Letras.cs)
        private bool ReposoGrandeActivo() { return suenaGrande && EstadoExtendidoEfectivo; }
        // Lo que el panel de reposo pinta de mas; null si la idea esta apagada.
        private ReposoGrande ExtraReposo(int w, int h, int enCola)
        {
            if (!suenaGrande) return null;
            ReposoGrande x = new ReposoGrande();
            if (nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null)
            {
                string et = GeneroDeCancion(nowPlaying.Song, nowPlaying.Disc) ?? "";
                x.Genero = et; x.GeneroColor = (et.Length > 0) ? ColorDeEtiqueta(et) : DefaultAccent;
            }
            x.Minutos = new int[enCola];
            for (int i = 0; i < enCola; i++) x.Minutos[i] = EsperaDePuesto(i + 1);
            return x;
        }
        // El reloj del panel grande: transcurrido / duracion total. De lo que suena, no de la etiqueta de abajo, para que
        // los dos numeros salgan del mismo instante.
        // 'r' es la fuente de lo que suena: la pasan los dos caminos del reloj del panel (el parche de cada segundo y el
        // panel entero), que es donde se lee.
        private string TextoRelojReposo(IAudioSource r)
        {
            string corto = (lblTime != null) ? lblTime.Text : "";
            if (!suenaGrande || r == null || nowPlaying == null || nowPlaying.IsVideo) return corto;
            TimeSpan cur, tot;
            try { cur = r.CurrentTime; tot = r.TotalTime; } catch { return corto; }
            return TextoFaltan(cur, tot, corto);
        }
        internal static string TextoFaltan(TimeSpan cur, TimeSpan tot, string siNo)
        {
            if (tot.TotalSeconds <= 0 || cur.TotalSeconds < 0) return siNo;
            if (cur > tot) cur = tot;
            return Fmt(cur) + " / " + Fmt(tot);
        }
        // El titulo que no cabe, en su propia imagen: la tarjeta lo desliza dentro de su caja. Se hornea con el panel.
        private void HorneaTituloReposo(string titulo, Rocola.Nativo.Letra f)   // EN LINUX: la letra es una Letra (Nativo/Letras.cs)
        {
            if (tituloReposoBmp != null && titulo == tituloReposoDe && f.AltoEntero == tituloReposoAlto) return;   // EN LINUX: Font.Height
            float ancho;
            // EN LINUX: new Bitmap(1, 1, Format32bppPArgb) y Graphics.FromImage solo daban un Graphics con que medir: TextoGdiPlus.Mide no necesita
            // EN LINUX: lienzo (el modo de letra con que se mide, el que XP le ponia, va en una variable)
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto una = new FormatoTexto(FormatoTexto.NormalCompartido);   // EN LINUX: new StringFormat(StringFormat.GenericDefault): una copia del normal compartido (Nativo/TextoGdiPlus.cs)
                AjusteLetra ajuste1 = AjusteLetra.SuaveAjustada;   // EN LINUX: g1.TextRenderingHint = AntiAliasGridFit, en una variable
                una.Banderas = BanderasTexto.SinPartir;   // EN LINUX: FormatFlags = NoWrap (solo esa)
                ancho = TextoGdiPlus.Mide(titulo ?? "", f, ajuste1, SizeF.Empty, una).Width;   // EN LINUX: g1.MeasureString(..., PointF.Empty, una): desde un punto mide sin limite (Nativo/TextoGdiPlus.cs)
            }
            int wt = (int)Math.Ceiling(ancho) + 8, ht = (int)Math.Ceiling(f.AltoRenglon) + 6;   // EN LINUX: Font.GetHeight() (el alto del renglon)
            if (wt > 4096) wt = 4096;
            if (tituloReposoBmp == null || tituloReposoBmp.Width != wt || tituloReposoBmp.Height != ht)
            {
                if (tituloReposoBmp != null) tituloReposoBmp.Dispose();
                tituloReposoBmp = Mapas.Nuevo(wt, ht);   // EN LINUX: new Bitmap(wt, ht, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            }
            // Con la misma letra y el mismo suavizado que el panel, y en el mismo sitio de su caja: quieto al principio,
            // se ve exactamente donde iria si cupiera.
            using (SKCanvas g = new SKCanvas(tituloReposoBmp))   // EN LINUX: Graphics.FromImage
            using (SKPaint b = new SKPaint { Color = Color.FromArgb(240, 245, 255).Sk() })   // EN LINUX: SolidBrush
            {
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                // EN LINUX: g.SmoothingMode = AntiAlias: aqui solo se escribe texto, que no lo mira (el texto se suaviza por el modo de letra)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                TextoGdiPlus.Escribe(g, titulo ?? "", f, ajuste, b, new RectangleF(0f, 0f, 0, 0), null);   // EN LINUX: g.DrawString(..., 0f, 0f): desde un punto (una caja sin ancho ni alto) y sin formato, el de fabrica (Nativo/TextoGdiPlus.cs)
            }
            tituloReposoDe = titulo; tituloReposoAlto = f.AltoEntero; tituloReposoVer++;   // EN LINUX: Font.Height
        }
        private GpuReposo BuildReposoState()
        {
            GpuReposo r = new GpuReposo();
            if (!ReposoGrandeActivo() || gpuPanel == null || nowPlaying == null || nowPlaying.IsVideo) return r;
            ReposoGrande x = reposoUltimo;
            if (x == null) return r;
            r.Has = true;
            if (x.TituloFuera)
            {
                // El titulo que se desliza se hornea aqui, al pedirlo la tarjeta y una vez por titulo (no dentro del panel).
                if (fTituloReposo == null || fTituloW != x.W || fTituloH != x.H)
                {
                    if (fTituloReposo != null) fTituloReposo.Dispose();
                    fTituloReposo = PuenteDibujo.Letra(FuenteTituloReposo(x.W, x.H)); fTituloW = x.W; fTituloH = x.H;   // EN LINUX: FuenteTituloReposo (EstadoReposoGpu.cs, aun sin traducir) da una Font: su Letra, por el puente (soltar la Letra es lo que hacia soltar la Font)
                }
                HorneaTituloReposo(x.Titulo, fTituloReposo);
            }
            if (x.TituloFuera && tituloReposoBmp != null)
            {
                r.Titulo = tituloReposoBmp; r.TituloVer = tituloReposoVer;
                r.TituloX = x.TituloCaja.X; r.TituloY = x.TituloCaja.Y; r.TituloW = x.TituloCaja.Width; r.TituloH = x.TituloCaja.Height;
                r.TituloExceso = Math.Max(0f, tituloReposoBmp.Width - x.TituloCaja.Width);
            }
            if (x.GeneroCaja.Width > 0f)
            {
                r.Genero = x.GeneroCaja;
                Color c = x.GeneroColor;
                r.GeneroColor = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            }
            return r;
        }

        // EL TITULO EN UNA LINEA (EstadoReposoGpu.cs): si cabe, aqui; si no, lo pinta la tarjeta deslizandose y aqui solo
        // se apunta su caja. Debajo, el artista y la pastilla del genero.
        internal static void PintaTituloGrande(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, ReposoGrande x, string title, string artist, Rocola.Nativo.Letra big, Rocola.Nativo.Letra normal, Rocola.Nativo.Letra small,   // EN LINUX: el lienzo de Skia y el estado del dibujo del que llama (suavizado, modo de letra, copia y medio pixel); las letras son Letra; quien aun pasa un Graphics llega por el puente (Plataforma/Puente.suena.cs)
            SKPaint white, SKPaint color, FormatoTexto line, int tx, int tw)   // EN LINUX: los pinceles son SKPaint (su color) y el formato un FormatoTexto (Nativo/TextoGdiPlus.cs)
        {
            float hTit = big.AltoRenglon + 4f;   // EN LINUX: Font.GetHeight(g): el alto del renglon (a 96 puntos por pulgada, como todo Graphics de la rocola)
            RectangleF caja = new RectangleF(tx, 56, tw, hTit);
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto una = new FormatoTexto(FormatoTexto.NormalCompartido);   // EN LINUX: new StringFormat(StringFormat.GenericDefault): una copia del normal compartido
                una.Banderas = BanderasTexto.SinPartir;   // EN LINUX: FormatFlags = NoWrap (solo esa)
                float ancho = TextoGdiPlus.Mide(title ?? "", big, ajuste, SizeF.Empty, una).Width;   // EN LINUX: g.MeasureString(..., PointF.Empty, una): desde un punto mide sin limite, con el modo de letra del lienzo (Nativo/TextoGdiPlus.cs)
                x.TituloFuera = ancho > tw;
                x.TituloCaja = caja;
                if (!x.TituloFuera) TextoGdiPlus.Escribe(g, title ?? "", big, ajuste, white, caja, line, copia, medio);   // EN LINUX: g.DrawString
            }
            float artistY = caja.Bottom + 6f, hArt = normal.AltoRenglon + 2f;   // EN LINUX: Font.GetHeight(g)
            string gen = x.Genero ?? "";
            float gw = 0f, gh = 0f;
            if (gen.Length > 0) { gh = small.AltoRenglon + 8f; gw = TextoGdiPlus.Mide(gen, small, ajuste, SizeF.Empty, null).Width + gh * .9f; }   // EN LINUX: Font.GetHeight(g) y g.MeasureString(gen, small): sin caja ni formato (el de fabrica)
            // El artista manda: si la pastilla no le cabe al lado entero, va en el renglon de abajo.
            float anchoArt = TextoGdiPlus.Mide(artist ?? "", normal, ajuste, SizeF.Empty, null).Width;   // EN LINUX: g.MeasureString(artist, normal)
            bool alLado = gw <= 0f || anchoArt + 14f + gw <= tw;
            float artW = alLado ? tw : tw;
            PintaTextoMovilSuena(g, suave, ajuste, copia, medio, artist ?? "", normal, color, new RectangleF(tx, artistY, artW, hArt), line);   // EN LINUX: PintaTextoMovil (TextoMovilGpu.cs) aun pinta con un Graphics: el lienzo, su estado, la letra, el pincel y el formato le llegan por el puente (Plataforma/Puente.suena.cs)
            x.GeneroCaja = RectangleF.Empty;
            if (gw > 0f)
            {
                RectangleF pg = alLado ? new RectangleF(tx + anchoArt + 14f, artistY + (hArt - gh) * .5f, gw, gh)
                                       : new RectangleF(tx, artistY + hArt + 4f, Math.Min(gw, tw), gh);
                Color c = x.GeneroColor;
                using (Rocola.Dibujo2D.GraphicsPath p = Redondo(pg, gh * .5f))   // EN LINUX: el trazo redondo lo hace PantallaVivaGpu.cs (aun sin traducir) como GraphicsPath: se pinta su SKPath (PuenteDibujo.Ruta)
                {
                    using (SKPaint b = new SKPaint { Color = Color.FromArgb(46, c).Sk() }) Pixel.Rellena(g, PuenteDibujo.Ruta(p), b, suave, medio, copia);   // EN LINUX: SolidBrush y g.FillPath (Nativo/Pixel.cs)
                    using (SKPaint pe = new SKPaint { Color = Color.FromArgb(170, c).Sk(), StrokeWidth = 1.3f }) Pixel.Traza(g, PuenteDibujo.Ruta(p), pe, suave, medio, copia);   // EN LINUX: Pen(color, 1.3) y g.DrawPath
                }
                using (SKPaint b = new SKPaint { Color = Lighten(c, .45).Sk() })   // EN LINUX: SolidBrush(Lighten(c, .45)), que en XP iba en la linea de abajo (aqui iba el StringFormat)
                {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                    FormatoTexto cen = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                    cen.Horizontal = Alinea.Centro; cen.Vertical = Alinea.Centro; cen.Banderas = BanderasTexto.SinPartir;   // EN LINUX: Alignment y LineAlignment = Center, FormatFlags = NoWrap (solo esa)
                    TextoGdiPlus.Escribe(g, gen, small, ajuste, b, pg, cen, copia, medio);   // EN LINUX: g.DrawString
                }
                x.GeneroCaja = pg;
            }
        }
        // "en ~6 min" para el puesto i de la cola (0 = la siguiente); "" si no se sabe.
        internal static string TextoMinutosCola(ReposoGrande x, int i)
        {
            if (x == null || x.Minutos == null || i < 0 || i >= x.Minutos.Length) return "";
            int m = x.Minutos[i];
            if (m < 0) return "";
            return (m == 0) ? "ya casi" : ("en ~" + m + " min");
        }
        // En el renglon del artista: en una tarjeta estrecha van SOLO los minutos; en una ancha, el artista y a la derecha
        // los minutos.
        internal static void PintaMinutosCola(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, string minutos, string artista, Rocola.Nativo.Letra f, SKPaint tenue, bool estrecha,   // EN LINUX: el lienzo de Skia y el estado del dibujo del que llama; la letra es una Letra y el pincel un SKPaint; quien aun pasa un Graphics llega por el puente (Plataforma/Puente.suena.cs)
            float x, float y, float w, float h, FormatoTexto line)   // EN LINUX: el formato es un FormatoTexto
        {
            using (SKPaint azul = new SKPaint { Color = Color.FromArgb(150, 200, 255).Sk() })   // EN LINUX: SolidBrush
            {
                if (estrecha) { TextoGdiPlus.Escribe(g, minutos, f, ajuste, azul, new RectangleF(x, y, w, h), line, copia, medio); return; }   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                float wm = TextoGdiPlus.Mide(minutos, f, ajuste, SizeF.Empty, null).Width + 2f;   // EN LINUX: g.MeasureString(minutos, f): sin caja ni formato (el de fabrica)
                if (wm > w * .6f) { TextoGdiPlus.Escribe(g, minutos, f, ajuste, azul, new RectangleF(x, y, w, h), line, copia, medio); return; }   // EN LINUX: g.DrawString
                PintaTextoMovilSuena(g, suave, ajuste, copia, medio, artista ?? "", f, tenue, new RectangleF(x, y, Math.Max(10f, w - wm - 8f), h), line);   // EN LINUX: PintaTextoMovil (TextoMovilGpu.cs) aun pinta con un Graphics: todo le llega por el puente (Plataforma/Puente.suena.cs)
                TextoGdiPlus.Escribe(g, minutos, f, ajuste, azul, new RectangleF(x + w - wm, y, wm, h), line, copia, medio);   // EN LINUX: g.DrawString
            }
        }
    }

    public partial class Form1
    {
        private partial class PastillaView
        {
            // LA MONEDA GRANDE (solo con tarjeta grafica): mientras cae la de la tarjeta, la monedita de dentro de la capsula
            // no se pinta en su imagen, y al aterrizar la capsula da un golpe.
            internal const int MonedaGolpeMs = 300;
            internal const float MonedaGolpeAlto = .12f;
            internal static int MonedaMsGpu { get { return MonMs; } }
            public bool MonedaGrande;
            internal int MonedaTick { get { return monTick; } }
            internal string ValorAntes { get { return rodDe; } }
            // En la imagen de la tarjeta (nunca en la del procesador) y con la idea encendida.
            private bool MoneditaTapada() { return dibujaGpu && MonedaGrande; }
            private float SaltoMoneda(int t)
            {
                if (!MoneditaTapada() || monTick == 0) return 1f;
                int d = t - (monTick + MonMs);
                if (d < 0 || d >= MonedaGolpeMs) return 1f;
                return 1f + MonedaGolpeAlto * (float)Math.Sin(Math.PI * d / MonedaGolpeMs);
            }
            // Donde cae el numero (su centro), con la misma cuenta que RenderContent. Para que la moneda aterrice en el.
            // EN LINUX: (private static Bitmap medidor: el mapa de 1x1 solo daba un Graphics con que medir; TextoGdiPlus.Mide no necesita lienzo)
            internal float XNumero()
            {
                int h = Height, w = Width;
                if (h < 8 || w < 8) return w * .5f;
                EnsureRes(h);
                // EN LINUX: (aqui se hacia el mapa del medidor, una vez)
                AjusteLetra ajuste = AjusteLetra.DelSistema;   // EN LINUX: using (Graphics g = Graphics.FromImage(medidor)): se mide con el modo de letra de un Graphics nuevo (SystemDefault), en una variable
                {
                    string et = Etiqueta ?? "", va = Valor ?? "";
                    float we = TextoGdiPlus.Mide(et, PuenteDibujo.Letra(fEtq), ajuste, SizeF.Empty, null).Width;   // EN LINUX: g.MeasureString(et, fEtq): la letra de la capsula (Form1.Vistas.cs, aun sin traducir) por el puente; sin caja ni formato
                    float wv = (va.Length > 0) ? TextoGdiPlus.Mide(va, PuenteDibujo.Letra(fVal), ajuste, SizeF.Empty, null).Width : 0f;   // EN LINUX: g.MeasureString(va, fVal)
                    float hueco = (va.Length > 0) ? h * 0.18f : 0f;
                    float wPort = ConPortadas ? AnchoPortaditas(h) : 0f;
                    float x = (w - (we + wv + hueco + wPort)) / 2f;
                    if (x < h * 0.25f) x = h * 0.25f;
                    float xv = x + we + hueco;
                    return (va.Length > 0) ? xv + Math.Max(wv, 10f) * .5f : w * .5f;
                }
            }
        }
    }

    internal sealed partial class GpuPanel
    {
        public Func<GpuAviso> GetAviso;
        public Func<GpuInvita> GetInvita;
        public Func<GpuMoneda> GetMoneda;
        public Func<GpuReposo> GetReposo;
        private IntPtr avisoTex; private int avisoVer = -1, avisoBw, avisoBh; private float avisoU = 1f, avisoV = 1f;
        private IntPtr invitaTex; private int invitaVer = -1, invitaBw, invitaBh; private float invitaU = 1f, invitaV = 1f;
        private IntPtr monedaTex; private int monedaBw, monedaBh; private float monedaU = 1f, monedaV = 1f;
        private IntPtr masTex; private int masVer = -1, masBw, masBh; private float masU = 1f, masV = 1f;
        private IntPtr tituloRepTex; private int tituloRepVer = -1, tituloRepBw, tituloRepBh; private float tituloRepU = 1f, tituloRepV = 1f;
        private GpuAviso avisoCuadro; private GpuInvita invitaCuadro; private GpuMoneda monedaCuadro; private GpuReposo reposoCuadro;
        private Rocola.Dibujo2D.Bitmap monedaBmp;   // EN LINUX: el Bitmap que entrega BakeMoneda para Upload (D3D9Star.cs, aun sin traducir): costura con lo no traducido
        private double tituloRepDesde = -1; private int tituloRepDe = -1;
        private readonly Tarjeta.Vtx[] svQuad = new Tarjeta.Vtx[6], svTira = new Tarjeta.Vtx[18];

        // Texturas, ANTES de Begin como las demas: solo se suben cuando cambia su version.
        private void PreparaSuenaVivo()
        {
            avisoCuadro = (GetAviso != null) ? GetAviso() : new GpuAviso();
            if (avisoCuadro.Has && avisoCuadro.Bmp != null && (avisoCuadro.Ver != avisoVer || avisoTex == IntPtr.Zero))
            { avisoTex = Upload(avisoTex, ref avisoBw, ref avisoBh, PuenteDibujo.Bitmap(avisoCuadro.Bmp), out avisoU, out avisoV); avisoVer = avisoCuadro.Ver; }   // EN LINUX: Upload (D3D9Star.cs, aun sin traducir) pide un Bitmap: el mapa le llega prestado por el puente (Plataforma/Puente.cs; el mismo Bitmap para el mismo mapa)
            invitaCuadro = (GetInvita != null) ? GetInvita() : new GpuInvita();
            if (invitaCuadro.Has && invitaCuadro.Bmp != null && (invitaCuadro.Ver != invitaVer || invitaTex == IntPtr.Zero))
            { invitaTex = Upload(invitaTex, ref invitaBw, ref invitaBh, PuenteDibujo.Bitmap(invitaCuadro.Bmp), out invitaU, out invitaV); invitaVer = invitaCuadro.Ver; }   // EN LINUX: el mapa, prestado por el puente a Upload
            monedaCuadro = (GetMoneda != null) ? GetMoneda() : new GpuMoneda();
            if ((monedaCuadro.Has || invitaCuadro.Has) && monedaTex == IntPtr.Zero)
            {
                if (monedaBmp == null) monedaBmp = BakeMoneda(96);
                monedaTex = Upload(monedaTex, ref monedaBw, ref monedaBh, monedaBmp, out monedaU, out monedaV);
            }
            if (monedaCuadro.Has && monedaCuadro.Mas != null && (monedaCuadro.MasVer != masVer || masTex == IntPtr.Zero))
            { masTex = Upload(masTex, ref masBw, ref masBh, PuenteDibujo.Bitmap(monedaCuadro.Mas), out masU, out masV); masVer = monedaCuadro.MasVer; }   // EN LINUX: el mapa, prestado por el puente a Upload
            reposoCuadro = (GetReposo != null && (IdleExpandTarget || idleBlend > 0)) ? GetReposo() : new GpuReposo();
            if (reposoCuadro.Has && reposoCuadro.Titulo != null && (reposoCuadro.TituloVer != tituloRepVer || tituloRepTex == IntPtr.Zero))
            { tituloRepTex = Upload(tituloRepTex, ref tituloRepBw, ref tituloRepBh, PuenteDibujo.Bitmap(reposoCuadro.Titulo), out tituloRepU, out tituloRepV); tituloRepVer = reposoCuadro.TituloVer; }   // EN LINUX: el mapa, prestado por el puente a Upload
        }

        // ---------------- LA MONEDA ----------------
        // Una moneda dorada: degradado, canto, un "$" en relieve y aire transparente alrededor (se dibuja aplastada al
        // girar, con filtro).
        internal static Rocola.Dibujo2D.Bitmap BakeMoneda(int n)   // EN LINUX: lo suben Upload (D3D9Star.cs) y lo pide PrecargaPaginaGpu.cs, aun sin traducir, como Bitmap: se les entrega por el puente (Plataforma/Puente.cs)
        {
            SKBitmap b = Mapas.Nuevo(n, n);   // EN LINUX: new Bitmap(n, n, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            using (SKCanvas g = new SKCanvas(b))   // EN LINUX: Graphics.FromImage
            {
                g.Clear(Color.Transparent.Sk());   // EN LINUX: Graphics.Clear
                bool suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias, en una variable (el medio pixel y la copia, los de fabrica)
                AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAliasGridFit, en una variable
                float a = 3f, d = n - 2 * a;
                RectangleF r = new RectangleF(a, a, d, d);
                using (SKPath p = new SKPath { FillType = SKPathFillType.EvenOdd })   // EN LINUX: GraphicsPath: un SKPath (su relleno de fabrica, Alternate, es par-impar)
                {
                    p.AddOval(new SKRect(r.Left, r.Top, r.Right, r.Bottom));   // EN LINUX: p.AddEllipse(r)
                    RectangleF cajaPg = RectangleF.FromLTRB(p.Bounds.Left, p.Bounds.Top, p.Bounds.Right, p.Bounds.Bottom);   // EN LINUX: new PathGradientBrush(p): su caja es la del trazo (GraphicsPath.GetBounds); el pincel va abajo, en un SKPaint
                    {
                        PointF centroPg = new PointF(a + d * .35f, a + d * .30f);   // EN LINUX: pg.CenterPoint
                        Color colorCentroPg = Color.FromArgb(255, 255, 246, 200);   // EN LINUX: pg.CenterColor
                        Color[] alrededorPg = new Color[] { Color.FromArgb(255, 184, 134, 11) };   // EN LINUX: pg.SurroundColors (el de la orilla es el primero)
                        using (SKShader degradado = Degradado.DeTrazo(cajaPg, centroPg, colorCentroPg, alrededorPg[0])) using (SKPaint pg = new SKPaint { Shader = degradado }) Pixel.Rellena(g, p, pg, suave);   // EN LINUX: g.FillPath(pg, p): el degradado del trazo (Nativo/Degradado.cs, DeTrazo) en un SKPaint (Nativo/Pixel.cs)
                    }
                }
                using (SKPaint canto = new SKPaint { Color = Color.FromArgb(255, 217, 169, 44).Sk(), StrokeWidth = d * .06f }) Pixel.TrazaOvalo(g, new RectangleF(a + d * .03f, a + d * .03f, d * .94f, d * .94f), canto, suave);   // EN LINUX: Pen(color, ancho) y g.DrawEllipse (Nativo/Pixel.cs)
                using (SKPaint dentro = new SKPaint { Color = Color.FromArgb(150, 255, 236, 150).Sk(), StrokeWidth = 1.2f }) Pixel.TrazaOvalo(g, new RectangleF(a + d * .14f, a + d * .14f, d * .72f, d * .72f), dentro, suave);   // EN LINUX: Pen(color, 1.2) y g.DrawEllipse
                using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPixeles(Letras.SinRemates, d * .46f, true))   // EN LINUX: new Font(FontFamily.GenericSansSerif, d*.46, Bold, Pixel): la generica sin remates es Microsoft Sans Serif (Letras.SinRemates)
                using (SKPaint t = new SKPaint { Color = Color.FromArgb(255, 122, 82, 0).Sk() })   // EN LINUX: SolidBrush, que en XP iba en la linea de abajo (aqui iba el StringFormat)
                {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                    FormatoTexto sf = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                    sf.Horizontal = Alinea.Centro; sf.Vertical = Alinea.Centro;   // EN LINUX: Alignment y LineAlignment = Center
                    TextoGdiPlus.Escribe(g, "$", f, ajuste, t, new RectangleF(a, a + d * .02f, d, d), sf);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                }
            }
            return PuenteDibujo.Entrega(b, Rocola.Dibujo2D.PixelFormat.Format32bppPArgb);   // EN LINUX: el mapa, como el Bitmap Format32bppPArgb que hacia XP y que se hace su dueno (Plataforma/Puente.cs)
        }
        // Donde va la moneda en su caida: 'u' de 0 (arriba) a 1 (aterriza). Cae acelerando, como algo que pesa, y gira
        // (se ve aplastada a lo ancho); al final da un botecito sobre el numero.
        internal static void PasoMoneda(float u, float x0, float y0, float x1, float y1, out float x, out float y, out float ancho, out float alfa)
        {
            if (!(u > 0f)) u = 0f; else if (u > 1f) u = 1f;
            float c = u * u;                                  // cae acelerando
            x = x0 + (x1 - x0) * u; y = y0 + (y1 - y0) * c;
            ancho = Math.Max(.16f, Math.Abs((float)Math.Cos(u * 11.5)));   // dos vueltas y media
            alfa = Math.Min(1f, u * 8f);
        }
        // El bote y el hundirse en la capsula, despues de aterrizar: 'v' de 0 a 1 en MonedaHundeMs.
        internal const float MonedaHundeMs = 180f;
        private void DibujaMoneda()
        {
            if (!monedaCuadro.Has || monedaTex == IntPtr.Zero) return;
            double ms = MomentoDisco() - monedaCuadro.Desde;
            if (ms < 0) return;
            float caida = Form1.MonedaCaeMs;
            float bw = (monedaBmp != null) ? monedaBmp.Width : 96f;
            float u0 = monedaU / bw, v0 = monedaV / bw, u1 = monedaU * (bw - 1f) / bw, v1 = monedaV * (bw - 1f) / bw;
            float esc = monedaCuadro.R / Math.Max(1f, (bw - 6f) * .5f);   // la moneda (sin su aire) mide 2R
            float hw = (bw - 2f) * .5f * esc;
            if (ms < caida)
            {
                float x, y, ancho, alfa;
                PasoMoneda((float)(ms / caida), monedaCuadro.X0, monedaCuadro.Y0, monedaCuadro.X1, monedaCuadro.Y1, out x, out y, out ancho, out alfa);
                int a = (int)(alfa * 255f + .5f); if (a > 255) a = 255;
                SetTexQuadGiro(svQuad, x, y, hw * ancho, hw, 0f, u0, v0, u1, v1, ((uint)a << 24) | 0x00FFFFFFu);
                stars.DrawQuad(monedaTex, svQuad);
            }
            else if (ms < caida + MonedaHundeMs)
            {
                // Aterriza: un botecito y se hunde en la capsula encogiendose, mientras la capsula da su golpe.
                float v = (float)((ms - caida) / MonedaHundeMs);
                float y = monedaCuadro.Y1 - (float)Math.Sin(Math.PI * v) * monedaCuadro.R * .5f;
                float s = 1f - .55f * v;
                int a = (int)((1f - v) * 255f + .5f); if (a > 255) a = 255;
                if (a > 0)
                {
                    SetTexQuadGiro(svQuad, monedaCuadro.X1, y, hw * s, hw * s, 0f, u0, v0, u1, v1, ((uint)a << 24) | 0x00FFFFFFu);
                    stars.DrawQuad(monedaTex, svQuad);
                }
            }
            // EL GOLPE: un destello verde detras de la capsula, que se apaga en medio segundo.
            if (ms >= caida && ms < caida + 520)
            {
                float k = (float)((ms - caida) / 520.0);
                int ga = (int)(150f * (1f - k) * (1f - k) + .5f);
                if (ga > 0) stars.DrawGlow(monedaCuadro.GolpeX, monedaCuadro.GolpeY, monedaCuadro.GolpeR * (1f + .25f * k), ((uint)ga << 24) | 0x0078F08Cu);
            }
            // "+4 creditos": sube desde la capsula y se desvanece.
            if (masTex != IntPtr.Zero && monedaCuadro.Mas != null && ms >= caida)
            {
                float m = (float)((ms - caida) / Form1.MonedaMasMs);
                if (m < 1f)
                {
                    float sube = 46f * (1f - (1f - m) * (1f - m));
                    float al = (m < .08f) ? m / .08f : (m > .6f ? (1f - m) / .4f : 1f);
                    int a = (int)(al * 255f + .5f); if (a > 255) a = 255;
                    float mw = monedaCuadro.Mas.Width, mh = monedaCuadro.Mas.Height;
                    SetTexQuadA(svQuad, monedaCuadro.MasX, monedaCuadro.MasY - sube, monedaCuadro.MasX + mw, monedaCuadro.MasY - sube + mh, masU, masV, ((uint)a << 24) | 0x00FFFFFFu);
                    stars.DrawQuad(masTex, svQuad);
                }
            }
        }

        // ---------------- LA INVITACION ----------------
        private void DibujaInvita()
        {
            if (!invitaCuadro.Has || invitaTex == IntPtr.Zero || invitaCuadro.Bmp == null) return;
            double ahora = MomentoDisco();
            double dEntra = ahora - invitaCuadro.Desde, dSale = invitaCuadro.Hasta - ahora;
            if (dEntra < 0 || dSale <= 0) return;
            float alfa = 1f, esc = 1f;
            if (dEntra < Form1.InvitaEntraMs) { float q = (float)(dEntra / Form1.InvitaEntraMs); float e = 1f - (1f - q) * (1f - q) * (1f - q); alfa = e; esc = .96f + .04f * e; }
            if (dSale < Form1.InvitaSaleMs) alfa = Math.Min(alfa, (float)(dSale / Form1.InvitaSaleMs));
            float bw = invitaCuadro.Bmp.Width, bh = invitaCuadro.Bmp.Height;
            float cx = invitaCuadro.X + bw * .5f, cy = invitaCuadro.Y + bh * .5f;
            int a = (int)(alfa * 255f + .5f); if (a > 255) a = 255;
            if (a <= 0) return;
            SetTexQuadA(svQuad, cx - bw * .5f * esc, cy - bh * .5f * esc, cx + bw * .5f * esc, cy + bh * .5f * esc, invitaU, invitaV, ((uint)a << 24) | 0x00FFFFFFu);
            stars.DrawQuad(invitaTex, svQuad);
            // La moneda que entra en la ranura, una y otra vez: llega desde la derecha, se deja ver y se mete.
            if (monedaTex != IntPtr.Zero && monedaBmp != null)
            {
                float f; float mx, ma; FaseInvita(dEntra, out f, out mx, out ma);
                float r = invitaCuadro.MonedaR * esc;
                float x = cx + (invitaCuadro.X + invitaCuadro.RanuraX - cx) * esc + mx * r * 2.8f;
                float y = cy + (invitaCuadro.Y + invitaCuadro.RanuraY - r * .9f - cy) * esc;
                int am = (int)(ma * alfa * 255f + .5f); if (am > 255) am = 255;
                if (am > 0)
                {
                    float mbw = monedaBmp.Width;
                    float e2 = r / Math.Max(1f, (mbw - 6f) * .5f), hw = (mbw - 2f) * .5f * e2;
                    SetTexQuadGiro(svQuad, x, y, hw, hw, 0f, monedaU / mbw, monedaV / mbw, monedaU * (mbw - 1f) / mbw, monedaV * (mbw - 1f) / mbw, ((uint)am << 24) | 0x00FFFFFFu);
                    stars.DrawQuad(monedaTex, svQuad);
                }
            }
            // La linea de lo que le queda, abajo, dentro de la tarjeta.
            double vida = (invitaCuadro.Hasta - ahora) / Form1.InvitaMs;
            if (vida > 1) vida = 1;
            float lx = cx - (bw * .5f - 20f) * esc, lw = (float)((bw - 40f) * esc * vida), ly = cy + (bh * .5f - 9f) * esc;
            if (lw > 1f) Linea(lx, ly, lw, 4f, FadeArgb(invitaCuadro.Color, alfa));
        }
        // La moneda de la invitacion: un ciclo de 2,6 s como en el boceto. mx: de 1 (a la derecha) a 0 (en la ranura);
        // ma: su opacidad (aparece, se ve y se mete).
        internal static void FaseInvita(double ms, out float f, out float mx, out float ma)
        {
            f = (float)((ms % Form1.InvitaMonedaMs) / Form1.InvitaMonedaMs);
            if (f < .15f) { mx = 1f; ma = 0f; return; }
            if (f < .60f) { float q = (f - .15f) / .45f; float e = q * q * (3f - 2f * q); mx = 1f - e; ma = Math.Min(1f, (f - .15f) / .10f); return; }
            if (f < .75f) { mx = 0f; ma = 1f - (f - .60f) / .15f; return; }
            mx = 0f; ma = 0f;
        }
        private void Linea(float x, float y, float w, float h, uint argb)
        {
            SetTexQuadA(svQuad, x, y - h * .5f, x + w, y + h * .5f, 1, 1, argb);
            stars.DrawColorQuad(svQuad);
        }

        // ---------------- EL AVISO ----------------
        // Entra por la derecha frenando, se queda, y al final se va hacia la derecha desvaneciendose.
        internal static void CurvaAviso(double ms, out float alfa, out float dx)
        {
            alfa = 1f; dx = 0f;
            if (ms < 0 || ms >= Form1.AvisoMs) { alfa = 0f; dx = Form1.AvisoCorre; return; }
            if (ms < Form1.AvisoEntraMs)
            {
                float q = (float)(ms / Form1.AvisoEntraMs);
                float e = 1f - (1f - q) * (1f - q) * (1f - q);
                alfa = e; dx = Form1.AvisoCorre * (1f - e);
            }
            else if (ms > Form1.AvisoMs - Form1.AvisoSaleMs)
            {
                float q = (float)((ms - (Form1.AvisoMs - Form1.AvisoSaleMs)) / Form1.AvisoSaleMs);
                alfa = 1f - q; dx = Form1.AvisoCorre * q * q;
            }
        }
        private void DibujaAviso()
        {
            if (!avisoCuadro.Has || avisoTex == IntPtr.Zero || avisoCuadro.Bmp == null) return;
            double ms = MomentoDisco() - avisoCuadro.Desde;
            float alfa, dx; CurvaAviso(ms, out alfa, out dx);
            int a = (int)(alfa * 255f + .5f); if (a > 255) a = 255;
            if (a <= 0) return;
            float bw = avisoCuadro.Bmp.Width, bh = avisoCuadro.Bmp.Height, x = avisoCuadro.X + dx, y = avisoCuadro.Y;
            SetTexQuadA(svQuad, x, y, x + bw, y + bh, avisoU, avisoV, ((uint)a << 24) | 0x00FFFFFFu);
            stars.DrawQuad(avisoTex, svQuad);
            // La linea de lo que le queda: por dentro del borde de abajo, sin llegar a las esquinas redondas.
            float vida = 1f - (float)(ms / Form1.AvisoMs); if (vida < 0f) vida = 0f;
            float rad = Form1.RadioAviso((int)bh), ai = Form1.AvisoAire;
            float lw = (bw - 2f * ai - 2f * rad) * vida;
            if (lw > 1f) Linea(x + ai + rad, y + bh - ai - 4.5f, lw, 3f, FadeArgb(avisoCuadro.Color, alfa));
        }

        // ---------------- LO QUE SUENA, EN GRANDE ----------------
        // EL TITULO QUE NO CABE, deslizandose dentro de su caja: quieto al principio, va hasta el final, se para, y vuelve.
        // Los bordes se desvanecen (dos tiras con el alfa en degradado), asi no se corta de golpe.
        internal const float TituloVelIda = 42f, TituloVelVuelta = 140f, TituloPausaIni = 2200f, TituloPausaFin = 1400f;
        internal static float CorreTitulo(double ms, float exceso)
        {
            if (!(exceso > 0.5f) || ms < 0) return 0f;
            double ida = Math.Max(1200.0, exceso / TituloVelIda * 1000.0), vuelta = Math.Max(700.0, exceso / TituloVelVuelta * 1000.0);
            double ciclo = TituloPausaIni + ida + TituloPausaFin + vuelta;
            double t = ms % ciclo;
            if (t < TituloPausaIni) return 0f;
            t -= TituloPausaIni;
            if (t < ida) { double q = t / ida; return (float)(exceso * q * q * (3 - 2 * q)); }
            t -= ida;
            if (t < TituloPausaFin) return exceso;
            t -= TituloPausaFin;
            double v = t / vuelta; return (float)(exceso * (1 - v * v * (3 - 2 * v)));
        }
        private void DibujaTituloReposo(float py)
        {
            if (!reposoCuadro.Has || reposoCuadro.Titulo == null || tituloRepTex == IntPtr.Zero || idleBlend <= 0.001f) return;
            if (tituloRepDe != reposoCuadro.TituloVer || tituloRepDesde < 0) { tituloRepDe = reposoCuadro.TituloVer; tituloRepDesde = MomentoDisco(); }
            float off = CorreTitulo(MomentoDisco() - tituloRepDesde, reposoCuadro.TituloExceso);
            float bw = reposoCuadro.Titulo.Width, bh = reposoCuadro.Titulo.Height;
            float x0 = reposoCuadro.TituloX, y0 = py + reposoCuadro.TituloY, w = Math.Min(reposoCuadro.TituloW, bw), h = Math.Min(reposoCuadro.TituloH, bh);
            float fade = Math.Min(22f, w * .12f);
            uint a = (uint)(SafeMoonLevel(idleBlend) * 255f + .5f); if (a > 255) a = 255;
            // Tres tiras: la del medio opaca y las dos de los bordes con el alfa en degradado (solo si hay texto detras).
            float uu = tituloRepU / bw, vb = h / bh * tituloRepV;
            float s0 = off, s1 = off + w;
            float[] xs = { x0, x0 + fade, x0 + w - fade, x0 + w };
            float[] us = { s0, s0 + fade, s1 - fade, s1 };
            uint[] al = { off > .5f ? 0u : a, a, a, off < reposoCuadro.TituloExceso - .5f ? 0u : a };
            for (int k = 0; k < 3; k++)
            {
                int i = k * 6;
                SetV(ref svTira[i + 0], xs[k], y0, us[k] * uu, 0); SetV(ref svTira[i + 1], xs[k + 1], y0, us[k + 1] * uu, 0);
                SetV(ref svTira[i + 2], xs[k + 1], y0 + h, us[k + 1] * uu, vb);
                SetV(ref svTira[i + 3], xs[k], y0, us[k] * uu, 0); SetV(ref svTira[i + 4], xs[k + 1], y0 + h, us[k + 1] * uu, vb);
                SetV(ref svTira[i + 5], xs[k], y0 + h, us[k] * uu, vb);
                svTira[i + 0].Color = svTira[i + 3].Color = svTira[i + 5].Color = (al[k] << 24) | 0x00FFFFFFu;
                svTira[i + 1].Color = svTira[i + 2].Color = svTira[i + 4].Color = (al[k + 1] << 24) | 0x00FFFFFFu;
            }
            stars.DrawTexturedBatch(tituloRepTex, svTira, 6);
        }
        // LA PASTILLA DEL GENERO LATE CON LOS GRAVES: un brillo de su color detras, que sube con cada golpe y se apaga
        // solo. Es solo luz: la pastilla no se mueve ni cambia de tamaño.
        private void DibujaLatidoGenero(float py)
        {
            if (!reposoCuadro.Has || reposoCuadro.Genero.Width <= 0f || idleBlend <= 0.001f) return;
            float k = SafeMoonLevel(lunaBeat) * SafeMoonLevel(idleBlend);
            int a = (int)(18f + 70f * k + .5f); if (a > 255) a = 255;
            RectangleF g = reposoCuadro.Genero;
            float cy = py + g.Y + g.Height * .5f, r = g.Height * (1.0f + .35f * k);
            uint col = ((uint)a << 24) | (reposoCuadro.GeneroColor & 0xFFFFFFu);
            stars.GlowBatchBegin(3);
            stars.GlowBatchAdd(g.X + g.Height * .5f, cy, r, col);
            stars.GlowBatchAdd(g.X + g.Width * .5f, cy, Math.Max(r, g.Width * .45f), r, col);
            stars.GlowBatchAdd(g.Right - g.Height * .5f, cy, r, col);
            stars.GlowBatchEnd();
        }

        private void SueltaSuenaVivo()
        {
            if (avisoTex != IntPtr.Zero) { Tarjeta.Suelta(avisoTex); avisoTex = IntPtr.Zero; }
            if (invitaTex != IntPtr.Zero) { Tarjeta.Suelta(invitaTex); invitaTex = IntPtr.Zero; }
            if (monedaTex != IntPtr.Zero) { Tarjeta.Suelta(monedaTex); monedaTex = IntPtr.Zero; }
            if (masTex != IntPtr.Zero) { Tarjeta.Suelta(masTex); masTex = IntPtr.Zero; }
            if (tituloRepTex != IntPtr.Zero) { Tarjeta.Suelta(tituloRepTex); tituloRepTex = IntPtr.Zero; }
            avisoVer = invitaVer = masVer = tituloRepVer = -1;
            avisoBw = avisoBh = invitaBw = invitaBh = monedaBw = monedaBh = masBw = masBh = 0;
            tituloRepBw = tituloRepBh = 0;
        }
    }
#endif
}

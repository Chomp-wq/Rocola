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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

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
    internal struct GpuAviso { public bool Has; public Bitmap Bmp; public int Ver; public double Desde; public float X, Y; public uint Color; }
    internal struct GpuInvita { public bool Has; public Bitmap Bmp; public int Ver; public double Desde, Hasta; public float X, Y, RanuraX, RanuraY, MonedaR; public uint Color; }
    internal struct GpuMoneda { public bool Has; public double Desde; public float X0, Y0, X1, Y1, R, GolpeX, GolpeY, GolpeR; public Bitmap Mas; public int MasVer; public float MasX, MasY; }
    internal struct GpuReposo
    {
        public bool Has;
        public Bitmap Titulo; public int TituloVer; public float TituloX, TituloY, TituloW, TituloH, TituloExceso;
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
        private Bitmap avisoBmp; private int avisoVer; private GpuAviso aviso;
        private Font fAvisoTit, fAvisoDet;
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
            if (fAvisoTit == null) fAvisoTit = new Font(lblToast.Font.FontFamily, 14f, FontStyle.Bold);
            if (fAvisoDet == null) fAvisoDet = new Font(lblToast.Font.FontFamily, 11f, FontStyle.Bold);
            int borde = BordeLibreAvisos();   // con video en ventana, a la izquierda de el (si no, quedaba detras)
            Size tam = TamanoAviso(Math.Min(ClientSize.Width, borde), tit, det, fAvisoTit, fAvisoDet);
            if (avisoBmp == null || avisoBmp.Width != tam.Width || avisoBmp.Height != tam.Height)
            {
                if (avisoBmp != null) avisoBmp.Dispose();
                avisoBmp = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppPArgb);
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
        internal static Size TamanoAviso(int anchoPantalla, string titulo, string detalle, Font fTit, Font fDet)
        {
            int hTit = fTit.Height, hDet = string.IsNullOrEmpty(detalle) ? 0 : fDet.Height;
            int alto = Math.Max(56, 12 + hTit + (hDet > 0 ? 2 + hDet : 0) + 14);
            int icono = Math.Min(44, alto - 22);
            int texto = Math.Max(AnchoTexto(titulo ?? "", fTit), hDet > 0 ? AnchoTexto(detalle ?? "", fDet) : 0);
            int max = Math.Max(360, Math.Min(560, (int)(anchoPantalla * .40f)));
            int w = 16 + icono + 14 + texto + 22;
            if (w > max) w = max;
            if (w < 300) w = 300;
            return new Size(w + (int)(2 * AvisoAire), alto + (int)(2 * AvisoAire));
        }
        internal static float RadioAviso(int alto) { return Math.Min(16f, alto * .26f); }
        // El dibujo, aparte del estado para poder mirarlo sin encender nada.
        internal static void PintaAviso(Bitmap bmp, TipoAviso tipo, string titulo, string detalle, Font fTit, Font fDet)
        {
            int w = bmp.Width, h = bmp.Height;
            Color col = ColorAviso(tipo);
            using (Graphics g = Graphics.FromImage(bmp))
            using (StringFormat izq = new StringFormat())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;   // gris: ClearType deja flecos sobre transparente
                RectangleF caja = new RectangleF(AvisoAire, AvisoAire, w - 2 * AvisoAire - .5f, h - 2 * AvisoAire - .5f);
                using (GraphicsPath p = Redondo(caja, RadioAviso(h)))
                {
                    using (SolidBrush b = new SolidBrush(FondoAviso(tipo))) g.FillPath(b, p);
                    using (Pen pe = new Pen(col, 1.5f)) g.DrawPath(pe, p);
                }
                float alto = caja.Height;
                float lado = Math.Min(44f, alto - 22f);
                RectangleF ri = new RectangleF(caja.X + 16f, caja.Y + (alto - lado) * .5f - 1f, lado, lado);
                using (Pen aro = new Pen(col, 2f)) g.DrawEllipse(aro, ri);
                PintaIcono(g, tipo, ri, col);
                izq.FormatFlags = StringFormatFlags.NoWrap; izq.Trimming = StringTrimming.EllipsisCharacter;
                izq.LineAlignment = StringAlignment.Near;
                float xt = ri.Right + 14f, wt = Math.Max(20f, caja.Right - xt - 16f);
                bool dos = !string.IsNullOrEmpty(detalle);
                float bloque = fTit.Height + (dos ? 2 + fDet.Height : 0);
                float yt = caja.Y + (alto - bloque) * .5f - 2f;
                using (SolidBrush b = new SolidBrush(Color.White))
                    g.DrawString(titulo ?? "", fTit, b, new RectangleF(xt, yt, wt, fTit.Height + 2), izq);
                if (dos)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 222, 245)))
                        g.DrawString(detalle, fDet, b, new RectangleF(xt, yt + fTit.Height + 2, wt, fDet.Height + 2), izq);
            }
        }
        // Los iconos del boceto, trazados en una caja de 24x24 y escalados al circulo.
        private static void PintaIcono(Graphics g, TipoAviso tipo, RectangleF r, Color col)
        {
            float k = r.Width / 44f * 24f / 24f * (44f / 24f) * .52f;   // el icono ocupa algo mas de la mitad del circulo
            float ox = r.X + r.Width * .5f - 12f * k, oy = r.Y + r.Height * .5f - 12f * k;
            using (Pen pe = new Pen(col, Math.Max(1.6f, 2.6f * k)))
            {
                pe.StartCap = LineCap.Round; pe.EndCap = LineCap.Round; pe.LineJoin = LineJoin.Round;
                switch (tipo)
                {
                    case TipoAviso.Hecho:
                        g.DrawLines(pe, new PointF[] { P(ox, oy, k, 4f, 12.5f), P(ox, oy, k, 9.5f, 18f), P(ox, oy, k, 20f, 6f) });
                        break;
                    case TipoAviso.Cuidado:
                        g.DrawLine(pe, P(ox, oy, k, 12f, 6f), P(ox, oy, k, 12f, 13.5f));
                        using (SolidBrush b = new SolidBrush(col)) { PointF d = P(ox, oy, k, 12f, 18f); float rr = 1.7f * k; g.FillEllipse(b, d.X - rr, d.Y - rr, 2 * rr, 2 * rr); }
                        break;
                    case TipoAviso.Error:
                        g.DrawLine(pe, P(ox, oy, k, 7f, 7f), P(ox, oy, k, 17f, 17f));
                        g.DrawLine(pe, P(ox, oy, k, 17f, 7f), P(ox, oy, k, 7f, 17f));
                        break;
                    default:   // una nota musical
                        g.DrawLines(pe, new PointF[] { P(ox, oy, k, 9f, 18f), P(ox, oy, k, 9f, 6f), P(ox, oy, k, 20f, 4f), P(ox, oy, k, 20f, 16f) });
                        g.DrawEllipse(pe, ox + (6.5f - 2.5f) * k, oy + (18f - 2.5f) * k, 5f * k, 5f * k);
                        g.DrawEllipse(pe, ox + (17.5f - 2.5f) * k, oy + (16f - 2.5f) * k, 5f * k, 5f * k);
                        break;
                }
            }
        }
        private static PointF P(float ox, float oy, float k, float x, float y) { return new PointF(ox + x * k, oy + y * k); }

        // ======================================================================================================
        //  SIN CREDITOS, INVITAR
        // ======================================================================================================
        internal const int InvitaMs = 5200, InvitaEntraMs = 200, InvitaSaleMs = 260, InvitaMonedaMs = 2600;
        private Bitmap invitaBmp; private int invitaVer; private GpuInvita invita;
        private Font fInvTit, fInvTxt, fInvPie;

        // Devuelve si salio (si no, el que llama pone el letrero de siempre). Solo con la tarjeta dibujando y la franja a
        // la vista: es donde vive la capsula de creditos a la que invita.
        private bool InvitaAPoner(PlayItem it)
        {
            if (!sinCreditosInvita || !gpuEnabled || gpuPanel == null || !gpuPanel.Visible || attractOn || videoFull) return false;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 400 || h < 300) return false;
            string que = "";
            if (it != null) { string tit, art; it.TituloArtista(out tit, out art); que = TituloSinPista(tit) ?? ""; }
            if (fInvTit == null) fInvTit = new Font(lblToast.Font.FontFamily, 17f, FontStyle.Bold);
            if (fInvTxt == null) fInvTxt = new Font(lblToast.Font.FontFamily, 12.5f, FontStyle.Bold);
            if (fInvPie == null) fInvPie = new Font(lblToast.Font.FontFamily, 10.5f, FontStyle.Bold);
            int libre = BordeLibreAvisos();   // con video en ventana, centrada en lo que se ve (si no, la tapaba a medias)
            Size tam = TamanoInvita(libre);
            if (invitaBmp == null || invitaBmp.Width != tam.Width || invitaBmp.Height != tam.Height)
            {
                if (invitaBmp != null) invitaBmp.Dispose();
                invitaBmp = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppPArgb);
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
        internal static void PintaInvita(Bitmap bmp, string cancion, bool esVideo, int creditosPorMoneda, Font fTit, Font fTxt, Font fPie, out PointF ranura, out float radio)
        {
            int w = bmp.Width, h = bmp.Height;
            Color oro = Color.FromArgb(242, 206, 72);
            float pad = Math.Max(18f, h * .12f);
            float lado = Math.Min(h - 2 * pad - 10f, w * .24f);
            RectangleF caja = new RectangleF(2f, 2f, w - 4.5f, h - 4.5f);
            RectangleF hueco = new RectangleF(caja.X + pad, caja.Y + (caja.Height - lado) * .5f - 3f, lado, lado);
            ranura = new PointF(hueco.X + lado * .38f, hueco.Y + lado * .50f);
            radio = lado * .24f;
            using (Graphics g = Graphics.FromImage(bmp))
            using (StringFormat izq = new StringFormat())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (GraphicsPath p = Redondo(caja, 18f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 40, 30, 8))) g.FillPath(b, p);
                    using (Pen pe = new Pen(Color.FromArgb(190, oro), 2f)) g.DrawPath(pe, p);
                }
                // La caja de la ranura, con la ranura oscura: la moneda (la pone la tarjeta, animada) entra en ella.
                using (GraphicsPath p = Redondo(hueco, lado * .14f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(b, p);
                    using (Pen pe = new Pen(Color.FromArgb(100, oro), 1f)) g.DrawPath(pe, p);
                }
                RectangleF rr = new RectangleF(ranura.X - lado * .20f, ranura.Y - lado * .04f, lado * .40f, lado * .08f);
                using (GraphicsPath p = Redondo(rr, lado * .04f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 10, 10, 10))) g.FillPath(b, p);
                float xt = hueco.Right + pad, wt = Math.Max(40f, caja.Right - xt - pad);
                izq.Trimming = StringTrimming.EllipsisWord; izq.FormatFlags = StringFormatFlags.LineLimit;
                string que = string.IsNullOrEmpty(cancion) ? "" : (" «" + cancion + "»");
                string cuerpo = "Inserta una moneda para poner" + (que.Length > 0 ? que : (esVideo ? " tu video" : " tu canción")) + ".";
                string pie = "Una moneda = " + creditosPorMoneda + (esVideo ? (creditosPorMoneda == 1 ? " video" : " videos") : (creditosPorMoneda == 1 ? " canción" : " canciones"));
                float hTit = fTit.Height, hTxt = fTxt.Height * 2 + 2, hPie = fPie.Height;
                float bloque = hTit + 6 + hTxt + 8 + hPie;
                float y = caja.Y + (caja.Height - bloque) * .5f - 3f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 232, 154)))
                    g.DrawString("Te falta un crédito", fTit, b, new RectangleF(xt, y, wt, hTit + 2), izq);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 243, 207)))
                    g.DrawString(cuerpo, fTxt, b, new RectangleF(xt, y + hTit + 6, wt, hTxt + 2), izq);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(217, 196, 138)))
                    g.DrawString(pie, fPie, b, new RectangleF(xt, y + hTit + 6 + hTxt + 8, wt, hPie + 2), izq);
            }
        }

        // ======================================================================================================
        //  LA MONEDA QUE CAE
        // ======================================================================================================
        internal const float MonedaAltura = 3.6f;   // desde cuantas capsulas de alto cae
        private GpuMoneda moneda; private int monedaVista; private Bitmap monedaMas; private int monedaMasVer;
        private Font fMonedaMas;
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
            if (fMonedaMas == null) fMonedaMas = new Font(lblToast.Font.FontFamily, Math.Max(11f, h * .40f), FontStyle.Bold, GraphicsUnit.Pixel);
            string txt = "+" + mas + (mas == 1 ? " crédito" : " créditos");
            Size tm = TamanoMas(txt, fMonedaMas);
            if (monedaMas == null || monedaMas.Width != tm.Width || monedaMas.Height != tm.Height)
            {
                if (monedaMas != null) monedaMas.Dispose();
                monedaMas = new Bitmap(tm.Width, tm.Height, PixelFormat.Format32bppPArgb);
            }
            PintaMas(monedaMas, txt, fMonedaMas);
            moneda.Mas = monedaMas; moneda.MasVer = ++monedaMasVer;
            moneda.MasX = Math.Min(ClientSize.Width - tm.Width - 6f, xn + h * .2f); moneda.MasY = pc.Top - tm.Height * .6f;
            CierraInvita();   // quien estaba mirando la invitacion ya tiene su moneda
        }
        internal static Size TamanoMas(string txt, Font f) { return new Size(AnchoTexto(txt, f) + 24, f.Height + 14); }
        internal static void PintaMas(Bitmap bmp, string txt, Font f)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            using (StringFormat sf = new StringFormat())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; sf.FormatFlags = StringFormatFlags.NoWrap;
                RectangleF r = new RectangleF(0, 0, bmp.Width, bmp.Height);
                // Un reborde oscuro fino: sobre el cielo claro de la aurora se sigue leyendo.
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 6, 30, 14)))
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                        if (dx != 0 || dy != 0) g.DrawString(txt, f, b, new RectangleF(dx * 1.2f, dy * 1.2f, bmp.Width, bmp.Height), sf);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 240, 140))) g.DrawString(txt, f, b, r, sf);
            }
        }

        // ======================================================================================================
        //  LO QUE SUENA, EN GRANDE (el panel de reposo)
        // ======================================================================================================
        private Bitmap tituloReposoBmp; private int tituloReposoVer; private string tituloReposoDe; private int tituloReposoAlto = -1;
        private ReposoGrande reposoUltimo;
        private Font fTituloReposo; private int fTituloW, fTituloH;
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
        private void HorneaTituloReposo(string titulo, Font f)
        {
            if (tituloReposoBmp != null && titulo == tituloReposoDe && f.Height == tituloReposoAlto) return;
            float ancho;
            using (Bitmap b1 = new Bitmap(1, 1, PixelFormat.Format32bppPArgb))
            using (Graphics g1 = Graphics.FromImage(b1))
            using (StringFormat una = new StringFormat(StringFormat.GenericDefault))
            {
                g1.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                una.FormatFlags = StringFormatFlags.NoWrap;
                ancho = g1.MeasureString(titulo ?? "", f, PointF.Empty, una).Width;
            }
            int wt = (int)Math.Ceiling(ancho) + 8, ht = (int)Math.Ceiling(f.GetHeight()) + 6;
            if (wt > 4096) wt = 4096;
            if (tituloReposoBmp == null || tituloReposoBmp.Width != wt || tituloReposoBmp.Height != ht)
            {
                if (tituloReposoBmp != null) tituloReposoBmp.Dispose();
                tituloReposoBmp = new Bitmap(wt, ht, PixelFormat.Format32bppPArgb);
            }
            // Con la misma letra y el mismo suavizado que el panel, y en el mismo sitio de su caja: quieto al principio,
            // se ve exactamente donde iria si cupiera.
            using (Graphics g = Graphics.FromImage(tituloReposoBmp))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 245, 255)))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(titulo ?? "", f, b, 0f, 0f);
            }
            tituloReposoDe = titulo; tituloReposoAlto = f.Height; tituloReposoVer++;
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
                    fTituloReposo = FuenteTituloReposo(x.W, x.H); fTituloW = x.W; fTituloH = x.H;
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
        internal static void PintaTituloGrande(Graphics g, ReposoGrande x, string title, string artist, Font big, Font normal, Font small,
            Brush white, Brush color, StringFormat line, int tx, int tw)
        {
            float hTit = big.GetHeight(g) + 4f;
            RectangleF caja = new RectangleF(tx, 56, tw, hTit);
            using (StringFormat una = new StringFormat(StringFormat.GenericDefault))
            {
                una.FormatFlags = StringFormatFlags.NoWrap;
                float ancho = g.MeasureString(title ?? "", big, PointF.Empty, una).Width;
                x.TituloFuera = ancho > tw;
                x.TituloCaja = caja;
                if (!x.TituloFuera) g.DrawString(title ?? "", big, white, caja, line);
            }
            float artistY = caja.Bottom + 6f, hArt = normal.GetHeight(g) + 2f;
            string gen = x.Genero ?? "";
            float gw = 0f, gh = 0f;
            if (gen.Length > 0) { gh = small.GetHeight(g) + 8f; gw = g.MeasureString(gen, small).Width + gh * .9f; }
            // El artista manda: si la pastilla no le cabe al lado entero, va en el renglon de abajo.
            float anchoArt = g.MeasureString(artist ?? "", normal).Width;
            bool alLado = gw <= 0f || anchoArt + 14f + gw <= tw;
            float artW = alLado ? tw : tw;
            PintaTextoMovil(g, artist ?? "", normal, color, new RectangleF(tx, artistY, artW, hArt), line);
            x.GeneroCaja = RectangleF.Empty;
            if (gw > 0f)
            {
                RectangleF pg = alLado ? new RectangleF(tx + anchoArt + 14f, artistY + (hArt - gh) * .5f, gw, gh)
                                       : new RectangleF(tx, artistY + hArt + 4f, Math.Min(gw, tw), gh);
                Color c = x.GeneroColor;
                using (GraphicsPath p = Redondo(pg, gh * .5f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(46, c))) g.FillPath(b, p);
                    using (Pen pe = new Pen(Color.FromArgb(170, c), 1.3f)) g.DrawPath(pe, p);
                }
                using (StringFormat cen = new StringFormat())
                using (SolidBrush b = new SolidBrush(Lighten(c, .45)))
                {
                    cen.Alignment = StringAlignment.Center; cen.LineAlignment = StringAlignment.Center; cen.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(gen, small, b, pg, cen);
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
        internal static void PintaMinutosCola(Graphics g, string minutos, string artista, Font f, Brush tenue, bool estrecha,
            float x, float y, float w, float h, StringFormat line)
        {
            using (SolidBrush azul = new SolidBrush(Color.FromArgb(150, 200, 255)))
            {
                if (estrecha) { g.DrawString(minutos, f, azul, new RectangleF(x, y, w, h), line); return; }
                float wm = g.MeasureString(minutos, f).Width + 2f;
                if (wm > w * .6f) { g.DrawString(minutos, f, azul, new RectangleF(x, y, w, h), line); return; }
                PintaTextoMovil(g, artista ?? "", f, tenue, new RectangleF(x, y, Math.Max(10f, w - wm - 8f), h), line);
                g.DrawString(minutos, f, azul, new RectangleF(x + w - wm, y, wm, h), line);
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
            private static Bitmap medidor;
            internal float XNumero()
            {
                int h = Height, w = Width;
                if (h < 8 || w < 8) return w * .5f;
                EnsureRes(h);
                if (medidor == null) medidor = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(medidor))
                {
                    string et = Etiqueta ?? "", va = Valor ?? "";
                    float we = g.MeasureString(et, fEtq).Width;
                    float wv = (va.Length > 0) ? g.MeasureString(va, fVal).Width : 0f;
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
        private Bitmap monedaBmp;
        private double tituloRepDesde = -1; private int tituloRepDe = -1;
        private readonly D3D9.Vtx[] svQuad = new D3D9.Vtx[6], svTira = new D3D9.Vtx[18];

        // Texturas, ANTES de Begin como las demas: solo se suben cuando cambia su version.
        private void PreparaSuenaVivo()
        {
            avisoCuadro = (GetAviso != null) ? GetAviso() : new GpuAviso();
            if (avisoCuadro.Has && avisoCuadro.Bmp != null && (avisoCuadro.Ver != avisoVer || avisoTex == IntPtr.Zero))
            { avisoTex = Upload(avisoTex, ref avisoBw, ref avisoBh, avisoCuadro.Bmp, out avisoU, out avisoV); avisoVer = avisoCuadro.Ver; }
            invitaCuadro = (GetInvita != null) ? GetInvita() : new GpuInvita();
            if (invitaCuadro.Has && invitaCuadro.Bmp != null && (invitaCuadro.Ver != invitaVer || invitaTex == IntPtr.Zero))
            { invitaTex = Upload(invitaTex, ref invitaBw, ref invitaBh, invitaCuadro.Bmp, out invitaU, out invitaV); invitaVer = invitaCuadro.Ver; }
            monedaCuadro = (GetMoneda != null) ? GetMoneda() : new GpuMoneda();
            if ((monedaCuadro.Has || invitaCuadro.Has) && monedaTex == IntPtr.Zero)
            {
                if (monedaBmp == null) monedaBmp = BakeMoneda(96);
                monedaTex = Upload(monedaTex, ref monedaBw, ref monedaBh, monedaBmp, out monedaU, out monedaV);
            }
            if (monedaCuadro.Has && monedaCuadro.Mas != null && (monedaCuadro.MasVer != masVer || masTex == IntPtr.Zero))
            { masTex = Upload(masTex, ref masBw, ref masBh, monedaCuadro.Mas, out masU, out masV); masVer = monedaCuadro.MasVer; }
            reposoCuadro = (GetReposo != null && (IdleExpandTarget || idleBlend > 0)) ? GetReposo() : new GpuReposo();
            if (reposoCuadro.Has && reposoCuadro.Titulo != null && (reposoCuadro.TituloVer != tituloRepVer || tituloRepTex == IntPtr.Zero))
            { tituloRepTex = Upload(tituloRepTex, ref tituloRepBw, ref tituloRepBh, reposoCuadro.Titulo, out tituloRepU, out tituloRepV); tituloRepVer = reposoCuadro.TituloVer; }
        }

        // ---------------- LA MONEDA ----------------
        // Una moneda dorada: degradado, canto, un "$" en relieve y aire transparente alrededor (se dibuja aplastada al
        // girar, con filtro).
        internal static Bitmap BakeMoneda(int n)
        {
            Bitmap b = new Bitmap(n, n, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                float a = 3f, d = n - 2 * a;
                RectangleF r = new RectangleF(a, a, d, d);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(r);
                    using (PathGradientBrush pg = new PathGradientBrush(p))
                    {
                        pg.CenterPoint = new PointF(a + d * .35f, a + d * .30f);
                        pg.CenterColor = Color.FromArgb(255, 255, 246, 200);
                        pg.SurroundColors = new Color[] { Color.FromArgb(255, 184, 134, 11) };
                        g.FillPath(pg, p);
                    }
                }
                using (Pen canto = new Pen(Color.FromArgb(255, 217, 169, 44), d * .06f)) g.DrawEllipse(canto, a + d * .03f, a + d * .03f, d * .94f, d * .94f);
                using (Pen dentro = new Pen(Color.FromArgb(150, 255, 236, 150), 1.2f)) g.DrawEllipse(dentro, a + d * .14f, a + d * .14f, d * .72f, d * .72f);
                using (Font f = new Font(FontFamily.GenericSansSerif, d * .46f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                using (SolidBrush t = new SolidBrush(Color.FromArgb(255, 122, 82, 0)))
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("$", f, t, new RectangleF(a, a + d * .02f, d, d), sf);
                }
            }
            return b;
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
            if (avisoTex != IntPtr.Zero) { D3D9.Release(avisoTex); avisoTex = IntPtr.Zero; }
            if (invitaTex != IntPtr.Zero) { D3D9.Release(invitaTex); invitaTex = IntPtr.Zero; }
            if (monedaTex != IntPtr.Zero) { D3D9.Release(monedaTex); monedaTex = IntPtr.Zero; }
            if (masTex != IntPtr.Zero) { D3D9.Release(masTex); masTex = IntPtr.Zero; }
            if (tituloRepTex != IntPtr.Zero) { D3D9.Release(tituloRepTex); tituloRepTex = IntPtr.Zero; }
            avisoVer = invitaVer = masVer = tituloRepVer = -1;
            avisoBw = avisoBh = invitaBw = invitaBh = monedaBw = monedaBh = masBw = masBh = 0;
            tituloRepBw = tituloRepBh = 0;
        }
    }
#endif
}

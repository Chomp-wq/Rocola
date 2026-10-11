#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Rocola
{
    // LA CASILLA DE AYUDA (peticion del 11-10-2026). Va al lado de la de BUSCAR y se lee igual que ella:
    // un simbolo, la palabra y los DOS BOTONES que hay que apretar, dibujados tal cual son en la maquina:
    // el ROJO, un mas, y la flecha de ABAJO (roja, como en la cruceta). En vez de la lupa lleva un signo de
    // interrogacion DORADO que se mueve: lo anima la tarjeta grafica encima de la interfaz (bota, se balancea
    // y de vez en cuando da una vuelta de moneda), asi que la textura de la interfaz no se rehace por eso.
    // Tocarla con el dedo o el raton tambien abre la ayuda. Solo con tarjeta grafica: la ayuda solo existe ahi.
    public partial class Form1
    {
        private AyudaView ayudaView;

        private class AyudaView : Control
        {
            public bool Compacta;           // sin la palabra AYUDA cuando la fila va justa (1024x768)
            public bool Abierta;            // con la ayuda en pantalla la casilla queda encendida fija
            private Font fTxt; private float fAlto = -1f;

            public AyudaView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                TabStop = false;
            }
            protected override void Dispose(bool disposing)
            {
                try { if (disposing && fTxt != null) { fTxt.Dispose(); fTxt = null; } }
                finally { base.Dispose(disposing); }
            }
            private static int Sim(int alto) { int d = (int)(alto * 0.52f); if (d < 12) d = 12; if (d > 30) d = 30; return d; }

            // Lo que mide de ancho con ese alto: interrogacion + (AYUDA) + rojo + mas + flecha, con aire.
            public static int Ancho(int alto, bool compacta)
            {
                int d = Sim(alto);
                return 11 + (int)(d * 1.15f) + (compacta ? 6 : 8 + (int)(alto * 1.80f)) + 12 + d + 10 + d + 8 + (int)(d * 0.9f) + 14;
            }

            // Donde va el signo de interrogacion (coordenadas del control). Lo dibuja la tarjeta, animado.
            public RectangleF Icono()
            {
                int d = Sim(Height); float lado = d * 1.15f;
                return new RectangleF(11, (Height - lado) / 2f, lado, lado);
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics, false); }

            // forGpu: el signo NO va en la textura (lo pone la tarjeta, moviendose); sin tarjeta, fijo.
            public void RenderContent(Graphics g, bool forGpu)
            {
                int w = Width, h = Height;
                if (w < 30 || h < 12) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                if (fTxt == null || fAlto != h)
                { if (fTxt != null) fTxt.Dispose(); fTxt = new Font(FontFamily.GenericSansSerif, Math.Max(8f, h * 0.30f), FontStyle.Bold); fAlto = h; }
                Color oro = Color.FromArgb(255, 205, 90);
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (GraphicsPath gp = RoundedPath(r, Math.Min(14, h / 2)))
                {
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(Abierta ? 92 : 64, oro))) g.FillPath(sb, gp);
                    using (Pen pn = new Pen(Color.FromArgb(Abierta ? 255 : 220, oro), 2f)) g.DrawPath(pn, gp);
                }
                int cy = h / 2, d = Sim(h);
                RectangleF ic = Icono();
                if (!forGpu) PintaInterrogacion(g, ic, 1f);
                float x = ic.Right;
                Color tinta = Color.FromArgb(255, 255, 240, 205);
                if (!Compacta) x = CtrlTxt(g, fTxt, "AYUDA", x + 8, cy, tinta);
                x += Compacta ? 6 : 12;
                x = CtrlDot(g, x, cy, d, Color.FromArgb(228, 72, 72)) + 4;
                x = CtrlTxt(g, fTxt, "+", x, cy, tinta) + 6;
                FlechaAbajo(g, x, cy, (int)(d * 0.9f), Color.FromArgb(228, 72, 72));
            }

            // La flecha de ABAJO, en una tecla redondeada (la gemela de la de BUSCAR, que apunta arriba).
            internal static void FlechaAbajo(Graphics g, float x, int cy, int d, Color c)
            {
                Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
                using (GraphicsPath gp = RoundedPath(k, Math.Max(3, d / 4)))
                {
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, c))) g.FillPath(sb, gp);
                    using (Pen pn = new Pen(Color.FromArgb(215, c), 1.6f)) g.DrawPath(pn, gp);
                }
                float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
                CtrlTri(g, c, new PointF(cx, cy + t), new PointF(cx - an, cy - t * 0.62f), new PointF(cx + an, cy - t * 0.62f));
            }
        }

        // El signo dorado: degradado de oro, filo oscuro y un brillo arriba. Lo usan la casilla sin tarjeta y
        // la textura que anima la tarjeta (asi los dos se ven iguales).
        internal static void PintaInterrogacion(Graphics g, RectangleF r, float alfa)
        {
            if (r.Width < 4 || r.Height < 4) return;
            int a = (int)(255 * Math.Max(0f, Math.Min(1f, alfa)));
            using (GraphicsPath p = new GraphicsPath())
            using (FontFamily fam = new FontFamily(System.Drawing.Text.GenericFontFamilies.Serif))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                p.AddString("?", fam, (int)FontStyle.Bold, r.Height * 1.02f, new RectangleF(r.X - r.Width * .3f, r.Y + r.Height * .04f, r.Width * 1.6f, r.Height), sf);
                using (LinearGradientBrush oro = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, r.Height + 1),
                    Color.FromArgb(a, 255, 238, 150), Color.FromArgb(a, 214, 140, 24), 90f))
                using (Pen filo = new Pen(Color.FromArgb(a * 3 / 4, 70, 40, 6), Math.Max(1.2f, r.Height * .06f)))
                {
                    filo.LineJoin = LineJoin.Round;
                    g.DrawPath(filo, p);
                    g.FillPath(oro, p);
                }
            }
        }

        // Acomoda la casilla al lado de la de BUSCAR. Devuelve donde acaba (para el encabezado).
        private int AcomodaAyuda(int busX, int busW, int filaH, int barraDer, bool buscaVisible)
        {
            if (ayudaView == null) return busX + busW;
            int x = busX + busW + 10, libre = barraDer - x;
            // Primero con la palabra; si al encabezado no le quedan 200 px, sin ella; si ni asi, no se pone.
            bool compacta = libre - AyudaView.Ancho(filaH, false) < 200;
            int ancho = AyudaView.Ancho(filaH, compacta);
            bool cabe = buscaVisible && gpuEnabled && ancho <= libre - 90;
            ayudaView.Compacta = compacta;
            ayudaView.SetBounds(x, 8, ancho, filaH);
            ayudaView.Visible = cabe;
            return cabe ? x + ancho : busX + busW;
        }

        // El signo, en coordenadas de la pantalla, cuando se ve (si no, vacio).
        private RectangleF AyudaIconoRect()
        {
            if (ayudaView == null || !ayudaView.Visible || searchOpen || attractOn || videoFull || configMode) return RectangleF.Empty;
            RectangleF r = ayudaView.Icono();
            r.Offset(ayudaView.Left, ayudaView.Top);
            return r;
        }

        // Tocar la casilla abre la ayuda (pantalla tactil o raton).
        private bool AyudaTocada(Point p)
        {
            if (ayudaView == null || !ayudaView.Visible || !ayudaView.Bounds.Contains(p)) return false;
            // Con la ayuda abierta (preguntando o guiando), tocar la casilla la vuelve a guardar: es la gemela
            // tactil de ROJO + ABAJO, que tambien abre y cierra. El mismo toque que la abrio no la cierra.
            if (asistenteVisible)
            {
                if (unchecked(Environment.TickCount - asistenteInicioTick) >= 450) CierraAsistente();
                return true;
            }
            if (AsistenteVisible || !AsistentePermitido()) return false;   // aun regresando a su sitio
            AbreAsistente();
            return true;
        }
    }

    internal sealed partial class GpuPanel
    {
        // El signo de interrogacion dorado de la casilla de AYUDA, animado aqui encima de la interfaz.
        public Func<RectangleF> GetAyudaIcono;
        private IntPtr ayudaTex; private int ayudaTW, ayudaTH; private float ayudaU = 1f, ayudaV = 1f;
        private int ayudaLado = -1;
        private RectangleF ayudaRect;
        private readonly D3D9.Vtx[] ayudaQuad = new D3D9.Vtx[6];

        // Antes de Begin: se hornea una vez por tamano (nunca por cuadro).
        private void PreparaAyudaIcono()
        {
            ayudaRect = GetAyudaIcono != null ? GetAyudaIcono() : RectangleF.Empty;
            if (ayudaRect.Width < 4) return;
            int lado = Math.Max(16, Math.Min(256, (int)(ayudaRect.Height * 2f)));
            if (ayudaTex != IntPtr.Zero && lado == ayudaLado) return;
            using (Bitmap b = BakeInterrogacion(lado))
                ayudaTex = Upload(ayudaTex, ref ayudaTW, ref ayudaTH, b, out ayudaU, out ayudaV);
            ayudaLado = ayudaTex != IntPtr.Zero ? lado : -1;
        }
        internal static Bitmap BakeInterrogacion(int lado)
        {
            if (lado < 8 || lado > 1024) throw new ArgumentOutOfRangeException("lado");
            Bitmap b = new Bitmap(lado, lado, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Form1.PintaInterrogacion(g, new RectangleF(lado * .08f, lado * .06f, lado * .84f, lado * .84f), 1f);
            }
            return b;
        }

        // Bota, se balancea y cada cinco segundos da media vuelta de moneda. Con un brillo dorado que respira.
        internal static void AnimaInterrogacion(double t, out float bota, out float giro, out float anchoX, out float escala, out float brillo)
        {
            if (double.IsNaN(t) || double.IsInfinity(t)) t = 0;
            bota = (float)(-Math.Abs(Math.Sin(t * 2.6)) * .10);
            giro = (float)(Math.Sin(t * 1.7) * .16);
            double vuelta = t % 5.0;
            anchoX = vuelta < .7 ? (float)Math.Cos(vuelta / .7 * Math.PI * 2) : 1f;
            if (Math.Abs(anchoX) < .08f) anchoX = anchoX < 0 ? -.08f : .08f;
            escala = 1f + .05f * (float)Math.Sin(t * 5.2);
            brillo = .5f + .5f * (float)Math.Sin(t * 2.6 + 1.2);
        }

        private void DibujaInterrogacion(float alfa)
        {
            if (ayudaTex == IntPtr.Zero || ayudaRect.Width < 4 || !(alfa > .004f)) return;
            float bota, giro, anchoX, escala, brillo;
            AnimaInterrogacion(AhoraMs() / 1000.0, out bota, out giro, out anchoX, out escala, out brillo);
            float cx = ayudaRect.X + ayudaRect.Width * .5f, cy = ayudaRect.Y + ayudaRect.Height * .5f + bota * ayudaRect.Height;
            float r = ayudaRect.Height * .62f * escala;
            stars.GlowBatchBegin(1);
            stars.GlowBatchAdd(cx, cy, r * 1.7f, ((uint)((30 + 60 * brillo) * alfa) << 24) | 0xFFCC55u);
            stars.GlowBatchEnd();
            float rx = r * Math.Abs(anchoX);
            SetTexQuadA(ayudaQuad, cx - rx, cy - r, cx + rx, cy + r, ayudaU, ayudaV, ((uint)(255 * Math.Min(1f, alfa)) << 24) | 0xFFFFFFu);
            if (anchoX < 0)   // de espaldas: la textura se ve espejada, como una moneda que gira
                for (int i = 0; i < 6; i++) ayudaQuad[i].U = ayudaU - ayudaQuad[i].U;
            RotateStarQuad(ayudaQuad, cx, cy, giro);
            stars.DrawQuad(ayudaTex, ayudaQuad);
        }

        private void SueltaAyudaIcono()
        {
            if (ayudaTex != IntPtr.Zero) { D3D9.Release(ayudaTex); ayudaTex = IntPtr.Zero; }
            ayudaLado = -1;
        }
    }
}
#endif

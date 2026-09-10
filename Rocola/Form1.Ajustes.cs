// LA VENTANA DE AJUSTES (Ctrl+F3) y el asistente de botones
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {

        // Ctrl+F3: ajuste VISUAL del sonido. Tres perillas: "volumen objetivo" (que tan fuerte quedan todas),
        // "fuerza" (cuanto se suben las bajitas) y "realce de brillo" (exciter tipo DSEE: re-crea el "aire" que el
        // MP3 recorta). Filosofia del nivelado: subir las flojas, NO bajar las que ya suenan fuerte. Todo se oye
        // al instante en la cancion actual y aplica a las siguientes.
        // ======================= PIEZAS DE LA VENTANA DE SONIDO (Ctrl+F3) =======================
        // Windows XP dibuja sus barras, casillas y botones como en 1995: cuadrados, grises y con el marco
        // hundido. En una rocola que por fuera es un cielo estrellado eso desentona muchisimo, asi que estas
        // piezas se pintan solas. Por dentro son controles normales de Windows (raton, teclado y foco funcionan
        // igual); solo cambia el dibujo: esquinas redondeadas, un color de acento por ajuste y nada de marcos.

        // La familia de letra de la interfaz: se prueban por orden y se usa la primera que exista. En Windows XP
        // no hay Segoe UI, asi que ahi cae en Tahoma (la del sistema) y en equipos nuevos en Segoe UI.
        // Se busca UNA sola vez y se guarda. Antes se creaba una familia de letras nueva cada vez que se
        // abria la ventana de sonido, y una familia es un recurso del sistema que hay que devolver: como no
        // se devolvia, cada apertura dejaba uno colgado. Guardarla evita ademas la trampa contraria: la
        // familia de reserva (GenericSansSerif) es de Windows y NO se debe devolver nunca.
        private static FontFamily familiaUi;
        private static FontFamily FamiliaUi()
        {
            if (familiaUi != null) return familiaUi;
            string[] pref = { "Segoe UI", "Tahoma", "Verdana" };
            for (int i = 0; i < pref.Length; i++)
                try { familiaUi = new FontFamily(pref[i]); return familiaUi; } catch { }
            familiaUi = FontFamily.GenericSansSerif;
            return familiaUi;
        }

        // Arma una fila de ajuste dentro de una tarjeta. Es un delegado propio porque en .NET 3.5 los Action
        // de la biblioteca solo llegan a cuatro argumentos.
        private delegate void FilaAjuste(int k, Tarjeta card, int y, string titulo, Color ac, int min, int max, bool conAuto);

        // TARJETA: el recuadro redondeado que agrupa unos cuantos ajustes bajo un titulo.
        private sealed class Tarjeta : Panel
        {
            public string Titulo = "";
            public Color Acento = Color.FromArgb(120, 200, 255);
            private Font fTitulo;
            protected override void Dispose(bool d) { if (d && fTitulo != null) { fTitulo.Dispose(); fTitulo = null; } base.Dispose(d); }
            public Tarjeta()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath pa = RoundedPath(r, 12))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 28, 56)))
                using (Pen pn = new Pen(Color.FromArgb(46, 58, 104)))
                { g.FillPath(b, pa); g.DrawPath(pn, pa); }
                if (!string.IsNullOrEmpty(Titulo))
                {
                    using (SolidBrush ac = new SolidBrush(Acento))
                    using (GraphicsPath ba = RoundedPath(new Rectangle(16, 15, 4, 14), 2))
                        g.FillPath(ac, ba);
                    if (fTitulo == null) fTitulo = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);   // una vez, no en cada repintado
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(226, 234, 255)))
                        g.DrawString(Titulo, fTitulo, tb, 28, 12);
                }
            }
        }

        // DESLIZADOR: la barra de un ajuste. Riel redondeado, la parte ya recorrida en el color del ajuste y un
        // tirador redondo. Responde al raton (arrastrar o pulsar en cualquier punto) y al teclado (flechas de
        // una en una, AvPag/RePag de diez en diez, Inicio/Fin a los extremos).
        private sealed class Deslizador : Control
        {
            public int Minimum = 0, Maximum = 100;
            public Color Acento = Color.FromArgb(120, 200, 255);
            public event EventHandler Scroll;
            private int val;
            private bool arrastra;
            public Deslizador()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                TabStop = true;
                Height = 22;
                Cursor = Cursors.Hand;
            }
            public int Value
            {
                get { return val; }
                set { int v = Acota(value); if (v != val) { val = v; Invalidate(); } }
            }
            private int Acota(int v) { return v < Minimum ? Minimum : (v > Maximum ? Maximum : v); }
            private const int Radio = 9;
            private int DeX(int x)
            {
                int w = Width - Radio * 2; if (w < 1) w = 1;
                float f = (x - Radio) / (float)w;
                if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
                return Minimum + (int)(f * (Maximum - Minimum) + 0.5f);
            }
            private void Pon(int v)
            {
                v = Acota(v);
                if (v == val) return;
                val = v; Invalidate();
                if (Scroll != null) Scroll(this, EventArgs.Empty);
            }
            protected override void OnMouseDown(MouseEventArgs e)
            { if (Enabled) { Focus(); arrastra = true; Pon(DeX(e.X)); } base.OnMouseDown(e); }
            protected override void OnMouseMove(MouseEventArgs e)
            { if (arrastra && Enabled) Pon(DeX(e.X)); base.OnMouseMove(e); }
            protected override void OnMouseUp(MouseEventArgs e) { arrastra = false; base.OnMouseUp(e); }
            protected override bool IsInputKey(Keys k)
            {
                if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down
                 || k == Keys.PageUp || k == Keys.PageDown || k == Keys.Home || k == Keys.End) return true;
                return base.IsInputKey(k);
            }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (!Enabled) { base.OnKeyDown(e); return; }
                int salto = ((Maximum - Minimum) >= 40) ? 10 : 2;
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Pon(val - 1); e.Handled = true; }
                else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Pon(val + 1); e.Handled = true; }
                else if (e.KeyCode == Keys.PageDown) { Pon(val - salto); e.Handled = true; }
                else if (e.KeyCode == Keys.PageUp) { Pon(val + salto); e.Handled = true; }
                else if (e.KeyCode == Keys.Home) { Pon(Minimum); e.Handled = true; }
                else if (e.KeyCode == Keys.End) { Pon(Maximum); e.Handled = true; }
                base.OnKeyDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cy = Height / 2, alto = 6;
                Rectangle riel = new Rectangle(Radio, cy - alto / 2, Math.Max(1, Width - Radio * 2), alto);
                float f = (Maximum > Minimum) ? (val - Minimum) / (float)(Maximum - Minimum) : 0f;
                int cx = riel.X + (int)(riel.Width * f);
                Color ac = Enabled ? Acento : Color.FromArgb(92, 100, 124);
                using (GraphicsPath pa = RoundedPath(riel, alto / 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 46, 82)))
                    g.FillPath(b, pa);
                if (cx > riel.X + 1)
                    using (GraphicsPath pa = RoundedPath(new Rectangle(riel.X, riel.Y, cx - riel.X, riel.Height), alto / 2))
                    using (SolidBrush b = new SolidBrush(ac))
                        g.FillPath(b, pa);
                if (Focused)   // aro exterior: se ve cual esta enfocado al moverse con el teclado
                    using (Pen pn = new Pen(Color.FromArgb(130, ac.R, ac.G, ac.B), 2f))
                        g.DrawEllipse(pn, cx - Radio - 2, cy - Radio - 2, (Radio + 2) * 2, (Radio + 2) * 2);
                using (SolidBrush b = new SolidBrush(Enabled ? Color.FromArgb(238, 244, 255) : Color.FromArgb(130, 138, 160)))
                    g.FillEllipse(b, cx - Radio, cy - Radio, Radio * 2, Radio * 2);
                using (Pen pn = new Pen(ac, 2.2f))
                    g.DrawEllipse(pn, cx - Radio + 1, cy - Radio + 1, Radio * 2 - 2, Radio * 2 - 2);
            }
        }

        // INTERRUPTOR: la casilla de encender/apagar, con forma de pastilla. El texto va a su derecha y pulsar
        // en cualquier parte (pastilla o texto) lo cambia.
        private sealed class Interruptor : Control
        {
            public Color Acento = Color.FromArgb(110, 225, 160);
            public event EventHandler CheckedChanged;
            private bool marcado;
            public Interruptor()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                TabStop = true;
                Height = 26;
                Cursor = Cursors.Hand;
            }
            public bool Checked
            {
                get { return marcado; }
                set { if (marcado != value) { marcado = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
            }
            protected override void OnMouseDown(MouseEventArgs e) { if (Enabled) { Focus(); Checked = !Checked; } base.OnMouseDown(e); }
            protected override bool IsInputKey(Keys k) { return (k == Keys.Space) || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)) { Checked = !Checked; e.Handled = true; }
                base.OnKeyDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int h = 20, w = 40, y = (Height - h) / 2;
                Color ac = !Enabled ? Color.FromArgb(80, 88, 112) : (marcado ? Acento : Color.FromArgb(58, 68, 108));
                using (GraphicsPath pa = RoundedPath(new Rectangle(0, y, w, h), h / 2))
                using (SolidBrush b = new SolidBrush(ac))
                    g.FillPath(b, pa);
                if (Focused)
                    using (Pen pn = new Pen(Color.FromArgb(150, 190, 210, 255)))
                    using (GraphicsPath pa = RoundedPath(new Rectangle(0, y, w, h), h / 2))
                        g.DrawPath(pn, pa);
                int r = h - 6, kx = marcado ? (w - r - 3) : 3;
                using (SolidBrush b = new SolidBrush(Enabled ? Color.FromArgb(244, 248, 255) : Color.FromArgb(150, 156, 176)))
                    g.FillEllipse(b, kx, y + 3, r, r);
                if (!string.IsNullOrEmpty(Text))
                    using (SolidBrush tb = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(110, 118, 142)))
                        g.DrawString(Text, Font, tb, w + 10, (Height - Font.Height) / 2f - 1f);
            }
        }

        // BOTON plano y redondeado, con su color de relleno. Los de Windows en XP salen grises con degradado.
        private sealed class BotonPlano : Control
        {
            public Color Relleno = Color.FromArgb(40, 52, 98);
            public Color Borde = Color.FromArgb(72, 90, 150);
            private bool encima;
            public BotonPlano()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent; TabStop = true; Height = 38; Cursor = Cursors.Hand;
                ForeColor = Color.White;
            }
            protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { encima = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
            protected override bool IsInputKey(Keys k) { return (k == Keys.Space) || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
                base.OnKeyDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                Color rel = encima ? Aclara(Relleno, 0.16f) : Relleno;
                using (GraphicsPath pa = RoundedPath(r, 9))
                using (SolidBrush b = new SolidBrush(rel))
                using (Pen pn = new Pen(Focused ? Aclara(Borde, 0.35f) : Borde, Focused ? 2f : 1f))
                { g.FillPath(b, pa); g.DrawPath(pn, pa); }
                SizeF sz = g.MeasureString(Text, Font);
                using (SolidBrush tb = new SolidBrush(ForeColor))
                    g.DrawString(Text, Font, tb, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
            }
            private static Color Aclara(Color c, float t)
            { return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t)); }
        }

        // La ventana de ajustes SE REABRE SOLA una vez: al pulsar "Medir este equipo" hay que cerrarla -medir
        // necesita la pantalla libre y la rocola quieta, y con la ventana encima ni se veria ni se mediria lo
        // mismo-, y en cuanto acaba vuelve con los numeros puestos, ya en la pestana de Imagen. Por eso el
        // cuerpo de siempre esta en VentanaAjustes y aqui solo hay la vuelta de mas.
        private bool ajustesAbiertos;
        private void ShowVolumeManager()
        {
            // Los atajos reenviados al propietario no deben anidar otro ShowDialog:
            // dos copias aplicaban/restauraban preferencias y foco en distinto orden.
            if (ajustesAbiertos || IsDisposed || Disposing) return;
            ajustesAbiertos = true;
            try
            {
                while (true)
                {
                    VentanaAjustes();
                    if (!f3Medir) break;
                    f3Medir = false;
                    MideRendimiento();
                    f3Pagina = 1;
                }
            }
            finally { ajustesAbiertos = false; f3Pagina = 0; }
        }

        private void VentanaAjustes()
        {
            if (searchOpen) CloseSearch();
            bool wasMaint = maintMode;
            maintMode = true;   // el dialogo modal necesita quedarse al frente; se devuelve en el finally
            bool changed = false;
            float bkTarget = normTarget, bkStrength = normStrength, bkExc = excAmount, bkUp = masterDb, bkEq = eqAmount, bkBass = bassAmount; int bkVid = videoVol, bkDsk = perfilDisco; bool bkNoche = nocheOn, bkOn = normalizeOn, bkSbr = sbrOn, bkIa = iaVivo, bkEqA = eqAuto, bkBassA = bassAuto, bkVidA = videoAuto, bkIaDat = iaHarvestOn; string bkIaMez = iaMezcla, bkIaGpu = iaGpu;   // por si se Cancela
            try
            {
                Cursor.Show();
                FontFamily ff = FamiliaUi();
                using (Font fTit = new Font(ff, 15f, FontStyle.Bold))
                using (Font fSub = new Font(ff, 9.5f))
                using (Font fNom = new Font(ff, 10f, FontStyle.Bold))
                using (Font fVal = new Font(ff, 11f, FontStyle.Bold))
                using (Font fDes = new Font(ff, 8.5f))
                using (Font fBot = new Font(ff, 10f, FontStyle.Bold))
                using (Font fInt = new Font(ff, 9.5f, FontStyle.Bold))
                using (Form dlg = new Form())
                {
                    dlg.Text = "Rocola  -  Sonido";
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false; dlg.ShowIcon = false;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ClientSize = new Size(960, 766);   // sin la tarjeta del ULTRA cabe en una pantalla de 768
                    dlg.BackColor = Color.FromArgb(11, 14, 32);
                    dlg.Font = fSub;

                    Color cVol = Color.FromArgb(120, 230, 165);      // un color de acento por bloque
                    Color cFue = Color.FromArgb(140, 205, 255);
                    Color cSub = Color.FromArgb(255, 160, 140);
                    Color cVid = Color.FromArgb(140, 220, 232);
                    Color cCal = Color.FromArgb(178, 152, 255);
                    Color cGra = Color.FromArgb(255, 176, 122);
                    Color cBri = Color.FromArgb(255, 214, 140);
                    Color cBib = Color.FromArgb(255, 176, 102);
                    Color cIa = Color.FromArgb(255, 210, 130);
                    Color cTexto = Color.FromArgb(222, 230, 250);
                    Color cTenue = Color.FromArgb(138, 149, 182);    // texto de las explicaciones
                    Color cApagado = Color.FromArgb(96, 104, 128);

                    // El titulo encoge de 600 a 298 para dejarle sitio a las CUATRO pestañas. No mueve nada:
                    // los cuatro titulos miden 76, 217, 214 y 179 px, medidos con su letra, asi que el mas
                    // largo se queda a 81 px de tocar la primera pestaña (que empieza en 328).
                    Label lTit = new Label(); lTit.SetBounds(22, 10, 284, 28);   // hasta el 306: la primera pestaña empieza en 312 lTit.BackColor = Color.Transparent;
                    lTit.ForeColor = Color.FromArgb(232, 238, 255); lTit.Font = fTit; lTit.Text = "Sonido";
                    // LAS DOS PESTANAS VAN AQUI, EN EL RENGLON DEL TITULO, y no en una fila propia: esta ventana
                    // mide 960x732 a proposito -con su barra de titulo cabe justo en una pantalla de 1024x768- y
                    // esta ocupada del primer pixel al ultimo. Una fila de pestanas de 34 px de alto habria
                    // obligado a encoger algo de lo que ya funciona. Aqui no cuestan NADA de alto y la pagina de
                    // Sonido se queda exactamente donde estaba.
                    //
                    // Y AHORA SON CINCO, DE 120 Y NO DE 148. Con cuatro de 148 la tira iba de 328 a 944; una
                    // quinta habria empezado en 172 y se habria comido el titulo. A 120 las cinco van de 312
                    // a 944 -acaban en el mismo sitio- y al titulo le quedan 284 px para los 247 que gasta el
                    // mas largo. La ventana no crece: sigue cabiendo en la pantalla del bar. Lo comprueba la
                    // bateria 'ventana', que las mide de verdad.
                    BotonPlano bPagS = new BotonPlano(); bPagS.SetBounds(568, 8, 120, 30); bPagS.Font = fBot; bPagS.Text = "Sonido";
                    BotonPlano bPagI = new BotonPlano(); bPagI.SetBounds(696, 8, 120, 30); bPagI.Font = fBot; bPagI.Text = "Imagen";
                    // LA QUINTA: el fondo y sus adornos. Once interruptores que estaban en Imagen y no eran
                    // de ahi: ninguno le quita trabajo a la rocola, son de gusto. Y no cabian: con los
                    // dieciseis, la tarjeta de Imagen media 1026 px en una pagina de 636 y hubo que ponerle
                    // barra de desplazamiento.
                    BotonPlano bPagF = new BotonPlano(); bPagF.SetBounds(824, 8, 120, 30); bPagF.Font = fBot; bPagF.Text = "Fondo";
                    // Y LA TERCERA: los detalles que dan vida. No caben en Imagen -la tarjeta EFECTOS mide
                    // 568 px y su contenido llega al 562, medido- y ademas no son lo mismo: las de alli se
                    // bajan para ganar rendimiento a cambio de que se vea menos bonito, y el medidor las
                    // puede proponer. Estas no cuestan nada que se note: se encienden y se dejan.
                    BotonPlano bPagD = new BotonPlano(); bPagD.SetBounds(440, 8, 120, 30); bPagD.Font = fBot; bPagD.Text = "Detalles";
                    // Y LA CUARTA: los colores del fondo. Tampoco caben en Detalles -sus dos tarjetas ocupan
                    // de y=4 a y=428 de 636 y debajo ya estan el pie y el boton de fabrica-, y ademas no son
                    // lo mismo: las de alli son adornos que se encienden y se dejan; estas cambian de que
                    // color es la rocola entera.
                    BotonPlano bPagC = new BotonPlano(); bPagC.SetBounds(312, 8, 120, 30); bPagC.Font = fBot; bPagC.Text = "Colores";
                    // El subtitulo ya no ocupa el renglon entero: se queda con su mitad izquierda (su texto
                    // gasta 545 px medidos) y la derecha es del interruptor de aqui abajo. Antes reclamaba los
                    // 916 aunque no los usara, y asi los dos decian ser dueños del mismo sitio.
                    // El subtitulo ya no ocupa el renglon entero: se queda con su mitad izquierda (su texto
                    // gasta 545 px medidos) y la derecha es del interruptor de aqui abajo. Antes reclamaba los
                    // 916 aunque no los usara, y asi los dos decian ser dueños del mismo sitio.
                    Label lSub = new Label(); lSub.SetBounds(24, 42, 590, 18); lSub.BackColor = Color.Transparent;
                    lSub.ForeColor = cTenue; lSub.Font = fSub;
                    lSub.Text = "Todo lo que cambies aqui se oye al instante en la cancion que esta sonando.";
                    // Y DE CUANDO ES ESTA ROCOLA, abajo. Es lo primero que hay que mirar cuando algo "no
                    // funciona": si la fecha no es la del arreglo, no hay nada roto, es que no se copio.
                    //
                    // VA ENTRE LOS DOS BOTONES DE ABAJO, y no a la izquierda como estaba: alli se dibujaba
                    // ENCIMA del boton "Graficos como de fabrica" de la pagina de Imagen, que ocupa de la y
                    // 680 a la 718. Esta franja -de la 734 para abajo, entre el boton de fabrica (acaba en la
                    // x 236) y el de Listo (empieza en la 804)- es la unica que esta libre en las cuatro
                    // paginas. El texto mide 214 px, medido, y hay 540.
                    Label lVer = new Label(); lVer.SetBounds(250, 734, 540, 18); lVer.BackColor = Color.Transparent;
                    lVer.ForeColor = cApagado; lVer.Font = fSub;
                    string cuando = DeCuandoEs();
                    lVer.Text = (cuando.Length > 0) ? ("Esta rocola es del " + cuando) : "";
                    // MODO NOCHE. Va AQUI, en el renglon del subtitulo, y no dentro de una tarjeta: esta
                    // ventana mide 960x732 a proposito -con su barra de titulo cabe justo en 1024x768- y las
                    // tres tarjetas estan llenas hasta el ultimo pixel. Este renglon, en cambio, tiene 371 px
                    // libres MEDIDOS (el subtitulo gasta 545 de 916), asi que el interruptor entra sin mover
                    // ni un pixel de nada de lo que ya funciona.
                    //
                    // Y ademas es su sitio: no es una de las cuatro perillas de nivelado, es una regla que
                    // vale para toda la ventana -musica y videos por igual-, y por eso queda fuera y arriba.
                    // La hora sale de la configuracion, no escrita a mano: si el operador cambia noche_desde=
                    // en el archivo, el letrero lo dice.
                    Interruptor swNoche = new Interruptor();
                    swNoche.SetBounds(624, 40, 316, 22); swNoche.ForeColor = cTenue; swNoche.Font = fInt;
                    swNoche.Text = "Bajar el volumen de " + nocheDesde + " a " + nocheHasta + " h";
                    swNoche.Acento = Color.FromArgb(150, 205, 255); swNoche.Checked = nocheOn;
                    // QUE HAY DESPUES DEL CABLE. La rocola mide el ARCHIVO, y eso vale en cualquier equipo; lo
                    // que no puede saber por su cuenta es si Windows le esta metiendo sus propias mejoras
                    // encima. Se lee del registro de Windows y se dice aqui, en ambar si hay algo que arreglar.
                    bool avisoSal; string txtSal;
                    try { txtSal = TextoSalida(out avisoSal); } catch { txtSal = ""; avisoSal = false; }
                    Label lSal = new Label(); lSal.SetBounds(24, 62, 916, 34); lSal.BackColor = Color.Transparent;
                    lSal.ForeColor = avisoSal ? Color.FromArgb(255, 196, 96) : cTenue; lSal.Font = fDes;
                    lSal.Text = txtSal;

                    // Las explicaciones ocupan DOS renglones y su hueco tiene sitio para dos: antes media 28 px,
                    // que con la letra de Windows se queda a un pelo justo de dos, y el segundo salia cortado
                    // ("...NUNCA se" y ahi se acababa). Ahora 32, y ademas cada texto lleva escrito a mano donde
                    // parte, para que no dependa de lo ancha que sea la letra en cada equipo.
                    Tarjeta tVol = new Tarjeta(); tVol.SetBounds(16, 100, 456, 398); tVol.Titulo = "VOLUMEN PAREJO"; tVol.Acento = cVol;
                    Tarjeta tCal = new Tarjeta(); tCal.SetBounds(488, 100, 456, 398); tCal.Titulo = "CALIDAD DEL SONIDO"; tCal.Acento = cCal;
                    // La tarjeta del DSEE ULTRA se arma pero NO SE ENSENA: en Linux ese modo no existe (mira
                    // el comentario de iaVivo). Se sigue armando para no tocar el resto de la ventana, y la
                    // ventana entera se hace 242 px mas baja, con lo que ademas pasa a caber en una pantalla
                    // de 768 de alto, que antes no cabia.
                    Tarjeta tIa = new Tarjeta(); tIa.SetBounds(16, 508, 928, 232); tIa.Titulo = "IA EN VIVO  (DSEE ULTRA)"; tIa.Acento = cIa;
                    tIa.Visible = false;
                    Tarjeta tBib = new Tarjeta(); tBib.SetBounds(16, 508, 928, 194); tBib.Titulo = "TU BIBLIOTECA"; tBib.Acento = cBib;

                    // --- Una FILA de ajuste: nombre arriba, valor a la derecha, explicacion debajo y la barra abajo ---
                    Label[] nom = new Label[7], val = new Label[7], des = new Label[7];
                    Deslizador[] bar = new Deslizador[7];
                    Interruptor[] au = new Interruptor[7];
                    const int anchoFila = 424;
                    FilaAjuste Fila = delegate (int k, Tarjeta card, int y, string titulo, Color ac, int min, int max, bool conAuto)
                    {
                        int wNom = conAuto ? (anchoFila - 210) : (anchoFila - 100);
                        nom[k] = new Label(); nom[k].SetBounds(16, y, wNom, 18); nom[k].BackColor = Color.Transparent;
                        nom[k].ForeColor = cTexto; nom[k].Font = fNom; nom[k].Text = titulo;
                        if (conAuto)
                        {
                            au[k] = new Interruptor(); au[k].SetBounds(16 + wNom + 6, y - 2, 104, 22);
                            au[k].ForeColor = cTenue; au[k].Font = fInt; au[k].Text = "Auto"; au[k].Acento = ac;
                            card.Controls.Add(au[k]);
                        }
                        val[k] = new Label(); val[k].SetBounds(16 + anchoFila - 94, y - 2, 94, 20); val[k].BackColor = Color.Transparent;
                        val[k].ForeColor = ac; val[k].Font = fVal; val[k].TextAlign = ContentAlignment.TopRight;
                        des[k] = new Label(); des[k].SetBounds(16, y + 21, anchoFila, 32); des[k].BackColor = Color.Transparent;
                        des[k].ForeColor = cTenue; des[k].Font = fDes;
                        bar[k] = new Deslizador(); bar[k].SetBounds(16, y + 55, anchoFila, 22);
                        bar[k].Minimum = min; bar[k].Maximum = max; bar[k].Acento = ac;
                        card.Controls.Add(nom[k]); card.Controls.Add(val[k]); card.Controls.Add(des[k]); card.Controls.Add(bar[k]);
                    };

                    // ---------------- VOLUMEN PAREJO ----------------
                    Interruptor swOn = new Interruptor();
                    swOn.SetBounds(16, 40, 424, 26); swOn.ForeColor = cTexto; swOn.Font = fNom;
                    swOn.Text = "Nivelar el volumen de todas las canciones"; swOn.Acento = cVol; swOn.Checked = normalizeOn;
                    tVol.Controls.Add(swOn);
                    Fila(0, tVol, 76, "Volumen objetivo", cVol, 0, 100, false);
                    Fila(1, tVol, 156, "Fuerza del nivelado", cFue, 0, 100, false);
                    Fila(2, tVol, 236, "Subir TODO el volumen", cSub, 0, 12, false);
                    Fila(3, tVol, 316, "Volumen de los videos", cVid, 0, 100, true);
                    bar[0].Value = TargetToPct(normTarget);
                    bar[1].Value = (int)Math.Round(normStrength * 100f);
                    bar[2].Value = (int)Math.Round(masterDb);
                    bar[3].Value = videoVol; au[3].Checked = videoAuto;

                    // ---------------- CALIDAD DEL SONIDO ----------------
                    Fila(4, tCal, 44, "EQ automatico", cCal, 0, 100, true);
                    Fila(5, tCal, 124, "Graves (bajo profundo)", cGra, 0, 100, true);
                    Fila(6, tCal, 204, "Realce de brillo", cBri, 0, 100, false);
                    bar[4].Value = (int)Math.Round(eqAmount * 100f); au[4].Checked = eqAuto;
                    bar[5].Value = (int)Math.Round(bassAmount * 100f); au[5].Checked = bassAuto;
                    bar[6].Value = (int)Math.Round(excAmount * 100f);

                    Interruptor swSbr = new Interruptor();
                    swSbr.SetBounds(16, 292, 424, 26); swSbr.ForeColor = cTexto; swSbr.Font = fNom;
                    swSbr.Text = "DSEE"; swSbr.Acento = Color.FromArgb(150, 205, 255);
                    swSbr.Checked = sbrOn && sbrAllowed; swSbr.Enabled = sbrAllowed;
                    Label lSbr = new Label(); lSbr.SetBounds(16, 322, 424, 68); lSbr.BackColor = Color.Transparent;
                    lSbr.ForeColor = sbrAllowed ? cTenue : Color.FromArgb(224, 158, 120); lSbr.Font = fDes;
                    // El DSEE hace DOS cosas y conviene que se entienda cual es cual: una la hace SIEMPRE (medir
                    // donde el archivo se queda sin agudos, que es de donde salen el EQ y los graves automaticos)
                    // y la otra solo si se enciende (volver a crear esos agudos). El interruptor manda sobre la
                    // segunda, no sobre la primera, y por eso la explicacion las separa.
                    lSbr.Text = sbrAllowed
                        ? ("Oye cada cancion y ve donde el MP3 le corto los agudos.\n"
                           + "Eso lo mide SIEMPRE: de ahi sacan el EQ automatico y los\n"
                           + "graves automaticos cuanto le falta a cada una.\n"
                           + "Encendido, ademas los recrea. Con DSEE ULTRA manda ese.")
                        : "No disponible en este equipo: " + sbrReason;
                    tCal.Controls.Add(swSbr); tCal.Controls.Add(lSbr);

                    // ---------------- IA EN VIVO (DSEE ULTRA) ----------------
                    // Es la restauracion GRANDE, la que corre en la tarjeta grafica con Apollo. No va junta con
                    // el DSEE ligero de la tarjeta de al lado: al encender esta, aquella se apaga sola.
                    Interruptor swUltra = new Interruptor();
                    swUltra.SetBounds(16, 40, 480, 26); swUltra.ForeColor = cTexto; swUltra.Font = fNom;
                    swUltra.Text = "DSEE ULTRA  -  restaurar con IA (maxima calidad)"; swUltra.Acento = cIa; swUltra.Checked = iaVivo;
                    Label lUltra = new Label(); lUltra.SetBounds(16, 68, 896, 18); lUltra.BackColor = Color.Transparent;
                    lUltra.ForeColor = cTenue; lUltra.Font = fDes;
                    lUltra.Text = "Restaura la cancion entera con IA usando la tarjeta grafica. Queda lista despues de oirla una vez.";
                    Interruptor swIaAuto = new Interruptor();
                    swIaAuto.SetBounds(16, 96, 480, 26); swIaAuto.ForeColor = cTexto; swIaAuto.Font = fNom;
                    swIaAuto.Text = "Decidir sola cuanta IA segun el dano"; swIaAuto.Acento = cIa;
                    int iaMixInit = 70; bool iaAutoInit = true;
                    if (iaMezcla != null && iaMezcla != "auto") { int tmp; if (int.TryParse(iaMezcla, out tmp)) { iaMixInit = tmp < 0 ? 0 : (tmp > 100 ? 100 : tmp); iaAutoInit = false; } }
                    swIaAuto.Checked = iaAutoInit;
                    Label lMixN = new Label(); lMixN.SetBounds(16, 132, 330, 18); lMixN.BackColor = Color.Transparent;
                    lMixN.ForeColor = cTexto; lMixN.Font = fNom; lMixN.Text = "Cuanta IA";
                    Label lMixV = new Label(); lMixV.SetBounds(346, 130, 94, 20); lMixV.BackColor = Color.Transparent;
                    lMixV.ForeColor = cIa; lMixV.Font = fVal; lMixV.TextAlign = ContentAlignment.TopRight;
                    Label lMixD = new Label(); lMixD.SetBounds(16, 152, 424, 32); lMixD.BackColor = Color.Transparent;
                    lMixD.ForeColor = cTenue; lMixD.Font = fDes;
                    lMixD.Text = "Cuanto de lo restaurado se mezcla con el original.\nMenos IA = mas natural; mas IA = mas limpio.";
                    Deslizador barMix = new Deslizador(); barMix.SetBounds(16, 186, 424, 22);
                    barMix.Minimum = 0; barMix.Maximum = 100; barMix.Acento = cIa; barMix.Value = iaMixInit;
                    Label lGpu = new Label(); lGpu.SetBounds(488, 132, 424, 18); lGpu.BackColor = Color.Transparent;
                    lGpu.ForeColor = cTexto; lGpu.Font = fNom; lGpu.Text = "Tarjeta grafica que hace los calculos";
                    ComboBox cbGpu = new ComboBox(); cbGpu.SetBounds(488, 156, 424, 26); cbGpu.DropDownStyle = ComboBoxStyle.DropDownList;
                    cbGpu.BackColor = Color.FromArgb(30, 38, 72); cbGpu.ForeColor = Color.White; cbGpu.FlatStyle = FlatStyle.Flat; cbGpu.Font = fSub;
                    cbGpu.Items.Add("Automatica");
                    string[] gpus = IaGpuList();
                    for (int gi = 0; gi < gpus.Length; gi++) cbGpu.Items.Add("GPU " + gi + " - " + gpus[gi]);
                    int selGpu = 0; { int gn; if (iaGpu != "auto" && int.TryParse(iaGpu, out gn) && gn >= 0 && gn + 1 < cbGpu.Items.Count) selGpu = gn + 1; }
                    cbGpu.SelectedIndex = selGpu;   // antes de enganchar el evento: sin reinicios fantasma
                    tIa.Controls.Add(swUltra); tIa.Controls.Add(lUltra); tIa.Controls.Add(swIaAuto);
                    tIa.Controls.Add(lMixN); tIa.Controls.Add(lMixV); tIa.Controls.Add(lMixD); tIa.Controls.Add(barMix);
                    tIa.Controls.Add(lGpu); tIa.Controls.Add(cbGpu);

                    // ---------------- TU BIBLIOTECA ----------------
                    Label lCal = new Label(); lCal.SetBounds(16, 40, 896, 20); lCal.BackColor = Color.Transparent;
                    lCal.ForeColor = Color.FromArgb(198, 214, 244); lCal.Font = fNom;
                    Label hint = new Label(); hint.SetBounds(16, 62, 896, 48); hint.BackColor = Color.Transparent;   // TRES renglones
                    hint.ForeColor = cTenue; hint.Font = fDes;
                    BotonPlano bTrain = new BotonPlano();
                    bTrain.SetBounds(16, 114, 300, 34); bTrain.Font = fBot;
                    Label lPct = new Label(); lPct.SetBounds(332, 112, 580, 18); lPct.BackColor = Color.Transparent;
                    lPct.ForeColor = cBib; lPct.Font = fDes; lPct.TextAlign = ContentAlignment.TopRight; lPct.Visible = false;
                    BarraAvance pbTrain = new BarraAvance();
                    pbTrain.SetBounds(332, 134, 580, 8); pbTrain.Acento = cBib; pbTrain.Visible = false;
                    // LIMPIEZA DE VIDEOS MUDOS. Comparte fila con el repaso a fondo, a la derecha del todo; la
                    // barra de avance del repaso se queda con lo de en medio.
                    BotonPlano bMudos = new BotonPlano();
                    bMudos.SetBounds(652, 114, 260, 34); bMudos.Font = fBot;
                    lPct.SetBounds(332, 112, 306, 18); pbTrain.SetBounds(332, 134, 306, 8);
                    // JUNTAR MATERIAL PARA LA IA: se puede apagar. Es lo unico de aqui que ESCRIBE UN ARCHIVO
                    // GORDO en el disco de la rocola -llega a 24 GB- y hay locales donde eso no interesa: un
                    // disco justo de espacio, o simplemente que el dueno no quiere juntar nada. Apagarlo NO
                    // toca ninguna otra cosa: la rocola sigue midiendo el volumen de cada cancion, aprendiendo
                    // el EQ y los graves de la biblioteca, buscando donde el MP3 corto los agudos y repasando
                    // a fondo igual que antes. Lo unico que deja de hacer es GUARDAR ejemplos para entrenar.
                    // Va en su propio renglon porque el de arriba ya lo llenan el repaso a fondo, su avance y
                    // la limpieza de videos mudos.
                    Interruptor swIa = new Interruptor();
                    swIa.SetBounds(16, 156, 286, 26); swIa.ForeColor = cTexto; swIa.Font = fNom;
                    swIa.Text = "Juntar material para la IA"; swIa.Acento = cBib; swIa.Checked = iaHarvestOn;
                    tBib.Controls.Add(lCal); tBib.Controls.Add(hint); tBib.Controls.Add(bTrain); tBib.Controls.Add(lPct); tBib.Controls.Add(pbTrain); tBib.Controls.Add(bMudos);
                    tBib.Controls.Add(swIa);
                    EscribeInformeCalidad();   // el detalle, cancion por cancion, queda en un archivo junto al programa

                    // ---------------- TIPO DE DISCO ----------------
                    // La rocola trabaja MUY distinto segun donde este la musica: un disco duro tiene un cabezal
                    // que hay que mover, asi que se le pide de uno en uno y se le deja respirar; un SSD no tiene
                    // nada que mover y rinde mas cuantas mas cosas se le pidan a la vez. Normalmente lo mide
                    // sola al arrancar y acierta. Esto esta aqui por si el operador quiere fijarlo -por ejemplo
                    // con la musica en un disco de red, donde medir no dice gran cosa-.
                    Color cDsk = Color.FromArgb(150, 205, 255);
                    Label lDskT = new Label(); lDskT.SetBounds(252, 712, 150, 18); lDskT.BackColor = Color.Transparent;
                    lDskT.ForeColor = cTexto; lDskT.Font = fNom; lDskT.Text = "Tipo de disco";
                    BotonPlano bDskA = new BotonPlano(); bDskA.SetBounds(408, 710, 122, 34); bDskA.Font = fBot; bDskA.Text = "Automatico";
                    BotonPlano bDskS = new BotonPlano(); bDskS.SetBounds(538, 710, 104, 34); bDskS.Font = fBot; bDskS.Text = "SSD";
                    BotonPlano bDskH = new BotonPlano(); bDskH.SetBounds(650, 710, 142, 34); bDskH.Font = fBot; bDskH.Text = "Disco duro";
                    Label lDskS = new Label(); lDskS.SetBounds(252, 746, 540, 18); lDskS.BackColor = Color.Transparent;
                    lDskS.ForeColor = cTenue; lDskS.Font = fDes;
                    Action pintaDsk = delegate
                    {
                        BotonPlano[] bb = { bDskA, bDskS, bDskH };
                        for (int k = 0; k < 3; k++)
                        {
                            bool act = (perfilDisco == k);
                            bb[k].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                            bb[k].Borde = act ? cDsk : Color.FromArgb(60, 72, 118);
                            bb[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                            bb[k].Invalidate();
                        }
                        string med = string.IsNullOrEmpty(perfilTexto) ? "aun sin medir" : perfilTexto;
                        // Con la musica repartida entre varios discos, aqui salen los dos: cada archivo se
                        // trata como merece el suyo, no todos por el mismo rasero.
                        lDskS.Text = (perfilDisco == 0)
                            ? ("Lo mide sola y se adapta a cada unidad.   Medido: " + med)
                            : ("Fijado a mano: " + (perfilDisco == 1 ? "SSD" : "disco duro") + ".   Medido: " + med);
                    };
                    EventHandler ponDsk = delegate (object so, EventArgs ea)
                    {
                        int q = (so == bDskS) ? 1 : (so == bDskH) ? 2 : 0;
                        if (q == perfilDisco) return;
                        perfilDisco = q; AplicaPerfilDisco(); changed = true;
                        lock (discoLock) discoLento.Clear();   // el forzado manda sobre lo medido de cada unidad
                        pintaDsk();
                    };
                    bDskA.Click += ponDsk; bDskS.Click += ponDsk; bDskH.Click += ponDsk;
                    pintaDsk();

                    // ---------------- PIE ----------------
                    BotonPlano bReset = new BotonPlano(); bReset.SetBounds(16, 714, 220, 38); bReset.Font = fBot;
                    bReset.Text = "Dejar como de fabrica";
                    BotonPlano bOk = new BotonPlano(); bOk.SetBounds(804, 714, 140, 38); bOk.Font = fBot;
                    bOk.Text = "Listo"; bOk.Relleno = Color.FromArgb(30, 96, 70); bOk.Borde = Color.FromArgb(72, 182, 132);

                    Action pintaMudos = delegate
                    {
                        List<float> dbm;
                        int nm = VideosMudos(out dbm).Count;
                        bMudos.Text = (nm > 0) ? ("Videos que no suenan: " + nm) : "No hay videos mudos";
                        bMudos.Enabled = nm > 0;
                        bMudos.Relleno = (nm > 0) ? Color.FromArgb(88, 40, 36) : Color.FromArgb(28, 34, 62);
                        bMudos.Borde = (nm > 0) ? Color.FromArgb(180, 92, 76) : Color.FromArgb(60, 72, 118);
                        bMudos.ForeColor = (nm > 0) ? Color.FromArgb(240, 214, 206) : cApagado;
                        bMudos.Invalidate();
                    };
                    bMudos.Click += delegate { ShowLimpiezaVideos(); pintaMudos(); };

                    Action pintaTrain = delegate
                    {
                        bool on = preScanTurbo;
                        bTrain.Text = on ? "Detener el repaso a fondo" : "Repasar la biblioteca a fondo";
                        bTrain.Relleno = on ? Color.FromArgb(126, 52, 42) : Color.FromArgb(40, 52, 98);
                        bTrain.Borde = on ? Color.FromArgb(196, 96, 76) : Color.FromArgb(72, 90, 150);
                        bTrain.Invalidate();
                        int tot = preScanTotal, fal = preScanLeft;
                        bool corriendo = on && tot > 0;
                        pbTrain.Visible = corriendo; lPct.Visible = corriendo;
                        if (corriendo)
                        {
                            int hechas = tot - fal; if (hechas < 0) hechas = 0; if (hechas > tot) hechas = tot;
                            pbTrain.Valor = hechas / (float)Math.Max(1, tot);
                            pbTrain.Invalidate();
                            lPct.Text = hechas.ToString("#,0") + " de " + tot.ToString("#,0") + " archivos   (" + (hechas * 100 / Math.Max(1, tot)) + "%)";
                        }
                    };
                    Action updateLabels = delegate
                    {
                        // ---------- VOLUMEN ----------
                        val[0].Text = bar[0].Value + "%";
                        des[0].Text = "Que tan fuerte queda el conjunto.\nMas alto = todas las canciones suenan mas fuerte.";
                        val[1].Text = bar[1].Value + "%";
                        des[1].Text = "Cuanto se suben las canciones bajitas.\nLas que ya suenan fuerte NUNCA se bajan.";
                        val[2].Text = "+" + bar[2].Value + " dB";
                        des[2].Text = "De mas para TODAS, ademas del nivelado.\nPara cuando el local se queda corto de volumen.";
                        // Los videos no pasan por la cadena de sonido: los pone el reproductor de Windows, que solo
                        // sabe BAJAR desde lo que el archivo trae. Por eso en automatico el programa elige el
                        // base mirando lo que miden todos: lo pone donde los flojos todavia alcanzan a los
                        // fuertes, que es la unica manera de que suenen todos igual.
                        int vMed = VideosMedidos();
                        val[3].Text = (au[3].Checked ? VideoBase() : bar[3].Value) + "%";
                        des[3].Text = "Los videos no pasan por la cadena de sonido: los pone Windows.\n"
                                    + (au[3].Checked
                                       ? (videoSinMedir
                                          ? "Solo: este equipo no puede medirlos; de momento manda la barra."
                                          : (vMed >= 8
                                             ? ("Solo: bajado hasta donde alcanzan los flojos (" + vMed.ToString("#,0") + " medidos).")
                                             : "Solo: aun no hay bastantes medidos; de momento manda la barra."))
                                       : "A mano: manda la barra, sin emparejar nada.");
                        // ---------- CALIDAD ---------- (en automatico se ensena lo que eligio el programa, no la barra)
                        int eqPct = (int)Math.Round((au[4].Checked ? EqFuerzaAuto() : bar[4].Value / 100f) * 100f);
                        val[4].Text = eqPct + "%";
                        des[4].Text = "Empareja el tono de cada cancion con el de tu biblioteca.\n"
                                    + (au[4].Checked
                                       ? (eqAvgSanas >= 10
                                          ? ("Solo: mas fuerza cuantas mas canciones medidas (van " + eqAvgSanas.ToString("#,0") + ").")
                                          : "Solo: aun no hay bastantes medidas; de momento no toca nada.")
                                       : "A mano: manda la barra.");
                        int bBase = (int)Math.Round((au[5].Checked ? bassAutoBase : bar[5].Value / 100f) * 100f);
                        int bEfe = (int)Math.Round(bBase * bassSongFactor);
                        val[5].Text = bBase + "%";
                        des[5].Text = "Devuelve el golpe de bajo que el archivo no trae.\n"
                                    + (au[5].Checked ? "Solo: a cada cancion se le da lo que le falta." : "A mano: manda la barra.")
                                    + ((nowPlaying != null && bEfe != bBase) ? ("   Esta: " + bEfe + "%.") : "");
                        val[6].Text = bar[6].Value + "%";
                        des[6].Text = "Devuelve el 'aire' que el MP3 apaga.\nSe le da mas a las canciones peor grabadas.";
                        // ---------- BIBLIOTECA ----------
                        {
                            int cb, cm, cp, cr, cs2;
                            CalidadResumen(out cb, out cm, out cp, out cr, out cs2);
                            lCal.Text = (cb + cm + cp > 0)
                                ? ("Calidad:   " + cb.ToString("#,0") + " completas     " + cm.ToString("#,0") + " aceptables     "
                                   + cp.ToString("#,0") + " pobres" + (cr > 0 ? ("     " + cr.ToString("#,0") + " con los picos recortados") : ""))
                                : "Calidad: aun sin medir. Deja que el repaso vaya haciendo su trabajo.";
                        }
                        string etapa = (nowPlaying != null && nowPlaying.Song != null) ? LoudTierName(nowPlaying.Song.Path) : "";
                        int tTot = preScanTotal, tFal = preScanLeft;
                        // TRES renglones, cada uno con lo suyo y con el corte escrito a mano: antes iba todo
                        // seguido en dos y con la biblioteca grande el ultimo se salia del hueco y no se veia.
                        // El renglon del material se dice SIEMPRE, tambien apagado: si desaparecia no habia
                        // forma de saber si se estaba juntando o no. Y apagado se aclara que lo demas sigue,
                        // que es justo la duda que le entra a cualquiera al ver un interruptor de IA.
                        string iaTxt = iaHarvestOn
                            ? ("Material para la IA: " + IaInfoText() + ".   ")
                            : "Material para la IA: apagado (lo demas se sigue midiendo igual).   ";
                        hint.Text = (preScanTurbo && tTot > 0)
                            ? ((tFal > 0 ? "Repasando la biblioteca a fondo ahora mismo." : "Repaso a fondo terminado: la biblioteca esta entera.")
                               + "\n" + iaTxt
                               + (etapa.Length > 0 ? ("La cancion que suena entra en la etapa " + etapa + ".") : "")
                               + "\nEl detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.")
                            : ("La biblioteca se mide sola mientras la rocola trabaja: "
                               + (tFal > 0 ? ("faltan " + tFal.ToString("#,0") + " archivos.") : "ya esta al dia.")
                               + (durScanLeft > 0 ? ("   Duraciones: faltan " + durScanLeft.ToString("#,0") + ".") : "")
                               + "\n" + iaTxt
                               + (etapa.Length > 0 ? ("La cancion que suena entra en la etapa " + etapa + ".") : "")
                               + "\nEl detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.");
                        pintaTrain();
                        pintaMudos();
                        // ---------- IA EN VIVO ----------
                        bool ultra = swUltra.Checked;
                        swSbr.Enabled = sbrAllowed && !ultra;      // ULTRA encendido -> el DSEE ligero se apaga solo
                        swIaAuto.Enabled = ultra;
                        barMix.Enabled = ultra && !swIaAuto.Checked;
                        cbGpu.Enabled = ultra;
                        lMixV.Text = swIaAuto.Checked ? "sola" : (barMix.Value + "%");
                        lUltra.ForeColor = ultra ? cTenue : cApagado;
                        lMixN.ForeColor = lGpu.ForeColor = ultra ? cTexto : cApagado;
                        lMixV.ForeColor = barMix.Enabled ? cIa : cApagado;
                        lMixD.ForeColor = ultra ? cTenue : cApagado;
                        // Lo que no manda nadie se apaga, para que se vea de un vistazo que no hay que tocarlo.
                        bool en = swOn.Checked;
                        bar[0].Enabled = en; bar[1].Enabled = en;
                        nom[0].ForeColor = nom[1].ForeColor = en ? cTexto : cApagado;
                        val[0].ForeColor = en ? cVol : cApagado;
                        val[1].ForeColor = en ? cFue : cApagado;
                        des[0].ForeColor = des[1].ForeColor = en ? cTenue : cApagado;
                        bar[3].Enabled = !au[3].Checked;
                        bar[4].Enabled = !au[4].Checked;
                        bar[5].Enabled = !au[5].Checked;
                    };
                    EventHandler apply = delegate
                    {
                        normalizeOn = swOn.Checked;
                        normTarget = TargetFromPct(bar[0].Value);
                        normStrength = bar[1].Value / 100f;
                        masterDb = bar[2].Value;
                        videoVol = bar[3].Value; videoAuto = au[3].Checked;
                        if (videoOn && nowPlaying != null) SetVideoVolume(VideoVolumeFor(nowPlaying.VideoPath));   // el video que suena cambia YA
                        eqAuto = au[4].Checked; bassAuto = au[5].Checked;
                        eqAmount = bar[4].Value / 100f;
                        bassAmount = bar[5].Value / 100f;
                        excAmount = bar[6].Value / 100f;
                        bool ultra2 = swUltra.Checked;
                        iaVivo = false;   // el DSEE ULTRA no existe en Linux: nunca se enciende
                        iaMezcla = swIaAuto.Checked ? "auto" : barMix.Value.ToString();
                        sbrOn = swSbr.Checked && sbrAllowed;   // SOLO la preferencia: encender ULTRA ya no te la borra
                        nocheOn = swNoche.Checked;   // al apagarlo de madrugada, la rampa lo devuelve solo (AvanzaModoNoche)
                        iaHarvestOn = swIa.Checked;
                        changed = true;
                        updateLabels();
                        ApplyNormNow();   // se oye el cambio al instante en la cancion que suena
                        UpdateNowPlayingUI();   // y el letrero MEJORADA aparece/desaparece al instante
                    };
                    for (int k = 0; k < bar.Length; k++) { bar[k].Scroll += apply; if (au[k] != null) au[k].CheckedChanged += apply; }
                    swOn.CheckedChanged += apply; swSbr.CheckedChanged += apply; swIa.CheckedChanged += apply;
                    swNoche.CheckedChanged += apply;
                    barMix.Scroll += apply; swIaAuto.CheckedChanged += apply;
                    swUltra.CheckedChanged += delegate { if (swUltra.Checked) { IaEnsureMotor(); IaRequestQueueJobs(); } apply(swUltra, EventArgs.Empty); };   // al encender ULTRA: motor oculto + preparar cola
                    cbGpu.SelectedIndexChanged += delegate
                    {
                        iaGpu = (cbGpu.SelectedIndex <= 0) ? "auto" : (cbGpu.SelectedIndex - 1).ToString();
                        changed = true;
                        IaRestartMotor();   // el motor renace OCULTO con la GPU elegida (Apollo recarga en ella)
                    };
                    bReset.Click += delegate
                    {
                        swOn.Checked = true;
                        bar[0].Value = TargetToPct(0.26f); bar[1].Value = 100; bar[2].Value = 0; bar[3].Value = 85;
                        bar[4].Value = 0; bar[5].Value = 30; bar[6].Value = 35;
                        au[3].Checked = true; au[4].Checked = true; au[5].Checked = true;   // videos, EQ y graves en automatico
                        swUltra.Checked = false; swIaAuto.Checked = true;    // la IA en vivo, apagada
                        if (swSbr.Enabled) swSbr.Checked = false;            // el espectral ligero, apagado
                        swIa.Checked = true;                                 // juntar material para la IA: encendido de fabrica
                        swNoche.Checked = true;                              // y bajar el volumen de madrugada, tambien
                        if (perfilDisco != 0)                                // el tipo de disco, otra vez a medirlo sola
                        { perfilDisco = 0; lock (discoLock) discoLento.Clear(); AplicaPerfilDisco(); pintaDsk(); }
                        apply(bReset, EventArgs.Empty);
                    };
                    bOk.Click += delegate { dlg.Close(); };
                    // El boton enciende/apaga el repaso a fondo. Al encenderlo se relanza el pre-analisis con TODA
                    // la biblioteca; al apagarlo, la tanda en curso se corta y vuelve el ritmo normal (que seguira
                    // solo con lo que falte). Lo medido hasta ese momento ya quedo guardado.
                    bTrain.Click += delegate
                    {
                        preScanTurbo = !preScanTurbo;
                        StartPreScan();
                        if (!preScanTurbo) { preScanTotal = 0; }
                        updateLabels();
                        ShowToast(preScanTurbo ? "Repaso a fondo: ENCENDIDO" : "Repaso a fondo: detenido",
                                  preScanTurbo ? Color.FromArgb(150, 60, 45) : Color.FromArgb(40, 130, 90));
                    };

                    // LA PAGINA DE IMAGEN Y RENDIMIENTO: el medidor de este equipo y, al lado, los efectos con
                    // su coste medido. Vive entera dentro de un Panel, asi cambiar de pestana es una linea y la
                    // pagina de Sonido ni se entera: se queda exactamente donde estaba, hasta el ultimo pixel.
                    Panel pImg = PaginaImagen(fNom, fVal, fDes, fBot, cTexto, cTenue, cApagado);
                    Panel pDet = PaginaDetalles(fNom, fDes, fBot, cTexto, cTenue, cApagado);
                    Panel pCol = PaginaColores(fNom, fDes, fBot, cTexto, cTenue, cApagado);
                    Panel pFon = PaginaFondo(fNom, fVal, fDes, fBot, cTexto, cTenue, cApagado);
                    string[] nomPag = { "Sonido", "Imagen y rendimiento", "Detalles que dan vida", "Colores del fondo", "El fondo y sus adornos" };
                    Action pintaPag = delegate
                    {
                        if (f3Pagina < 0 || f3Pagina > 4) f3Pagina = 0;
                        // Se ensena o se esconde TODO lo que hay en la ventana menos el marco (titulo,
                        // pestanas y el boton de Listo, que se ven en las tres) y las paginas propias.
                        // Se hace asi y no con una lista de nombres a proposito: una lista se olvida de una
                        // tarjeta al agregarla -y en la rama del equipo potente hay una mas- y esa tarjeta se
                        // quedaria colgada encima de la pagina nueva.
                        foreach (Control cc in dlg.Controls)
                            if (cc != lTit && cc != bPagS && cc != bPagI && cc != bPagD && cc != bPagC && cc != bOk && cc != pImg && cc != pDet && cc != pCol && cc != lVer)
                                cc.Visible = (f3Pagina == 0);
                        pImg.Visible = (f3Pagina == 1);
                        pDet.Visible = (f3Pagina == 2);
                        pCol.Visible = (f3Pagina == 3);
                        pFon.Visible = (f3Pagina == 4);
                        lTit.Text = nomPag[f3Pagina];
                        BotonPlano[] pb = { bPagS, bPagI, bPagD, bPagC, bPagF };
                        for (int k = 0; k < pb.Length; k++)
                        {
                            bool act = (k == f3Pagina);
                            pb[k].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                            pb[k].Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                            pb[k].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                            pb[k].Invalidate();
                        }
                    };
                    bPagS.Click += delegate { f3Pagina = 0; pintaPag(); };
                    bPagI.Click += delegate { f3Pagina = 1; pintaPag(); };
                    bPagD.Click += delegate { f3Pagina = 2; pintaPag(); };
                    bPagC.Click += delegate { f3Pagina = 3; pintaPag(); };
                    bPagF.Click += delegate { f3Pagina = 4; pintaPag(); };

                    dlg.Controls.Add(lTit); dlg.Controls.Add(bPagS); dlg.Controls.Add(bPagI); dlg.Controls.Add(bPagD); dlg.Controls.Add(bPagC); dlg.Controls.Add(bPagF); dlg.Controls.Add(lVer); dlg.Controls.Add(lSub); dlg.Controls.Add(swNoche); dlg.Controls.Add(lSal);
                    dlg.Controls.Add(tVol); dlg.Controls.Add(tCal); dlg.Controls.Add(tIa); dlg.Controls.Add(tBib);
                    dlg.Controls.Add(lDskT); dlg.Controls.Add(bDskA); dlg.Controls.Add(bDskS); dlg.Controls.Add(bDskH); dlg.Controls.Add(lDskS);
                    dlg.Controls.Add(bReset); dlg.Controls.Add(bOk); dlg.Controls.Add(pImg); dlg.Controls.Add(pDet); dlg.Controls.Add(pCol); dlg.Controls.Add(pFon);
                    pintaPag();
                    // Escape cierra, como toda la vida (los controles propios se comen las teclas, asi que la
                    // ventana las mira antes con KeyPreview).
                    dlg.KeyPreview = true;
                    dlg.KeyDown += delegate (object so, KeyEventArgs ke) { if (ke.KeyCode == Keys.Escape) dlg.Close(); };
                    // Refresco del avance mientras el dialogo esta abierto (el trabajo ocurre en otro hilo).
                    System.Windows.Forms.Timer tTrain = new System.Windows.Forms.Timer();
                    tTrain.Interval = 500;
                    tTrain.Tick += delegate { updateLabels(); };
                    tTrain.Start();
                    updateLabels();
                    dlg.ShowDialog(this);
                    tTrain.Stop(); tTrain.Dispose();
                }
                if (changed || f3CambioGraf) { SaveConfigPrefs(); ShowToast(changed ? "Sonido actualizado" : "Imagen actualizada", Color.FromArgb(40, 130, 90)); }
                f3CambioGraf = false;
            }
            catch { nocheOn = bkNoche; normTarget = bkTarget; normStrength = bkStrength; normalizeOn = bkOn; excAmount = bkExc; masterDb = bkUp; eqAmount = bkEq; bassAmount = bkBass; videoVol = bkVid; videoAuto = bkVidA; sbrOn = bkSbr; iaVivo = bkIa; eqAuto = bkEqA; bassAuto = bkBassA; iaMezcla = bkIaMez; iaGpu = bkIaGpu; perfilDisco = bkDsk; iaHarvestOn = bkIaDat; AplicaPerfilDisco(); }   // si algo truena, deja los valores como estaban
            finally { try { Cursor.Hide(); } catch { } maintMode = wasMaint; ForceFocus(); }
        }

        // Cuantos repintados de la ventana entera quedan pendientes (ver el final de LayoutControls).
        private int repintaPendientes = 0;

        // EL CIELO DE LA VENTANA, O UN TROZO DE EL.
        //
        // La ventana entera y cada control transparente piden lo mismo con distinto recorte: se dice en que
        // punto del cielo empieza el trozo y de que tamano es. El campo de estrellas ya sabe recortar asi
        // -es lo mismo que hacen la pared de discos y la barra de letras-, y por eso las estrellas caen
        // donde deben y no se nota ninguna costura.
        //
        // Hace falta porque en Linux el fondo "transparente" de un control no lo pinta el sistema: lo pinta
        // de NEGRO (ver Rocola/LinuxPintura.cs).
        void IFondoPintable.PintaCieloEn(Graphics g, int offX, int offY, int an, int al)
        {
            if (an < 2 || al < 2) return;
#if ROCOLA_GPU
            if (gpuPanel != null && gpuPanel.Visible) return;   // el panel GPU (pantalla completa) ya cubre todo
#endif
            formStars.SkyOffsetX = offX; formStars.SkyOffsetY = offY;
            formStars.SkyFullW = ClientSize.Width; formStars.SkyFullH = ClientSize.Height;
            formStars.SkyMid = EscalaBrillo(SkyColorMid(bgAccent), bgRespiro); formStars.Acento = bgAccent;
            // LAS DOS NEBULOSAS, AQUI QUIETAS. Y es a proposito.
            //
            // En esta maquina el cielo entero va horneado a un mapa de bits que se guarda, porque rehacerlo
            // cuesta de 6 a 27 ms segun la pantalla, y se rehornea cuando cambia alguno de sus colores. Se
            // probo pasarle los dos colores en movimiento -el cruce de la caratula y la respiracion- y esta
            // medido lo que costaba: los rehorneados pasaban de 73 a 151 en medio minuto, o sea 70 ms de
            // cada mil en una pantalla de 2560x1440. El doble de trabajo, en la maquina que se eligio
            // JUSTAMENTE por no tener tarjeta grafica.
            //
            // Asi que aqui se pasan los dos colores QUIETOS, y ninguno de los dos depende de la fase: el del
            // genero no depende de ella nunca, y el de la caratula es exactamente el que esta pared ya
            // pintaba antes de todo esto. Los dos cambian solo al cambiar de cancion, que es cuando el cielo
            // se rehornea de todas formas. Coste de esto, medido: CERO rehorneados mas.
            //
            // Lo que esta maquina se pierde es el MOVIMIENTO. Los dos colores estan -que era lo gordo, porque
            // antes habia uno solo-; lo que no hay es el cruce ni la deriva, que es lo que sale gratis con
            // tarjeta grafica y aqui costaria rehacer la pantalla entera.
            formStars.NebUno = nebA;
            formStars.NebDos = ColorNebulosa(bgAccent);
            formStars.HazFase = hazFase;   // el paseo lento de la luna: el mismo reloj que la via lactea
            formStars.ConLuna = lunaOn;
            formStars.Dim = bgDimResp;   // atenua TAMBIEN las franjas de arriba/abajo en sincronia con la rejilla (una sola pieza), respiracion incluida
            formStars.DrawCached(g, an, al, EscalaBrillo(SkyColor(bgAccent), bgRespiro), EscalaBrillo(SkyColor2(bgAccent), bgRespiro));
        }

        // =================== ASISTENTE DE CONFIGURACION DE BOTONES ===================
        private string KeysFile() { return RutaDatos("rocola_teclas.txt"); }

        // Carga el mapeo de botones. Devuelve true solo si estan los 8 completos.
        private bool LoadKeys()
        {
            try
            {
                string f = KeysFile();
                if (!File.Exists(f)) return false;
                Keys u = Keys.None, d = Keys.None, l = Keys.None, r = Keys.None, ok = Keys.None, bk = Keys.None, co = Keys.None, sk = Keys.None;
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = lines[i].Trim();
                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;
                    string name = s.Substring(0, eq).Trim().ToLower();
                    int val;
                    if (!int.TryParse(s.Substring(eq + 1).Trim(), out val)) continue;
                    Keys k = (Keys)val;
                    if (name == "arriba") u = k;
                    else if (name == "abajo") d = k;
                    else if (name == "izquierda") l = k;
                    else if (name == "derecha") r = k;
                    else if (name == "poner") ok = k;
                    else if (name == "regresar") bk = k;
                    else if (name == "moneda") co = k;
                    else if (name == "saltar") sk = k;
                }
                if (u == Keys.None || d == Keys.None || l == Keys.None || r == Keys.None ||
                    ok == Keys.None || bk == Keys.None || co == Keys.None || sk == Keys.None) return false;
                keyUp = u; keyDown = d; keyLeft = l; keyRight = r; keyOK = ok; keyBack = bk; keyCoin = co; keySkip = sk;
                return true;
            }
            catch { return false; }
        }

        private void SaveKeys()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("arriba=").Append((int)keyUp).Append("\r\n");
                sb.Append("abajo=").Append((int)keyDown).Append("\r\n");
                sb.Append("izquierda=").Append((int)keyLeft).Append("\r\n");
                sb.Append("derecha=").Append((int)keyRight).Append("\r\n");
                sb.Append("poner=").Append((int)keyOK).Append("\r\n");
                sb.Append("regresar=").Append((int)keyBack).Append("\r\n");
                sb.Append("moneda=").Append((int)keyCoin).Append("\r\n");
                sb.Append("saltar=").Append((int)keySkip).Append("\r\n");
                EscribeSeguro(KeysFile(), sb.ToString());
            }
            catch { }
        }

        // Inicia el asistente (primera vez, o cuando se escriben diez ceros en el buscador).
        private void StartConfig()
        {
            if (searchOpen) CloseSearch();
            configMode = true;
            configStep = 0;
            for (int i = 0; i < configCap.Length; i++) configCap[i] = Keys.None;
            if (lblConfig != null) { lblConfig.Visible = true; lblConfig.BringToFront(); }
            UpdateConfigPrompt(null);
        }

        private void UpdateConfigPrompt(string warn)
        {
            if (lblConfig == null) return;
            string t = "CONFIGURACION DE BOTONES DE LA ROCOLA\r\n\r\n";
            t += "Paso " + (configStep + 1) + " de " + ConfigSteps.Length + "\r\n\r\n";
            t += "Presiona en la rocola el boton para:\r\n\r\n";
            t += ">>>   " + ConfigSteps[configStep] + "   <<<";
            if (warn != null) t += "\r\n\r\n" + warn;
            t += "\r\n\r\n\r\n(cada boton debe ser distinto)";
            lblConfig.Text = t;
            lblConfig.BringToFront();
        }

        // Captura el boton presionado durante la configuracion.
        private void CaptureConfigKey(Keys keyData, Message msg)
        {
            if ((msg.LParam.ToInt64() & 0x40000000) != 0) return;   // ignora auto-repeticion (boton mantenido)
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.None || k == Keys.Menu || k == Keys.ControlKey) return;   // ignora teclas vacias/control sueltas
            for (int j = 0; j < configStep; j++)
                if (configCap[j] == k) { UpdateConfigPrompt("Ese boton ya esta usado. Presiona OTRO."); return; }
            configCap[configStep] = k;
            configStep++;
            if (configStep >= ConfigSteps.Length) FinishConfig();
            else UpdateConfigPrompt(null);
        }

        private void FinishConfig()
        {
            keyUp = configCap[0]; keyDown = configCap[1]; keyLeft = configCap[2]; keyRight = configCap[3];
            keyOK = configCap[4]; keyBack = configCap[5]; keyCoin = configCap[6]; keySkip = configCap[7];
            SaveKeys();
            configMode = false;
            needConfig = false;
            if (lblConfig != null) lblConfig.Visible = false;
            this.Focus();
            ShowToast("¡Botones configurados!");
        }

        // El aviso se dibuja SIEMPRE en un bitmap de este tamano (por eso la textura se reusa y no cuesta nada),
        // pero la pastilla de color se ajusta al texto y va centrada dentro. Antes era fija en 380 y los avisos
        // largos ("No se pudo abrir el video: credito devuelto") salian cortados.
        private const int ToastW = 620, ToastH = 48, ToastMinW = 260;
        // Ancho de la pastilla para ESE texto (acotado): lo usan las dos formas de dibujarla, por GPU y por CPU,
        // para que se vea igual en las dos.
        internal static int AnchoToast(string text, Font font)
        {
            int w = TextRenderer.MeasureText(text, font).Width + 44;
            return w < ToastMinW ? ToastMinW : (w > ToastW ? ToastW : w);
        }
#if ROCOLA_GPU
        private Bitmap toastBmp; private int toastVer, toastTick = -1;
        private const int ToastMs = 1300, ToastInMs = 190, ToastOutMs = 260;   // ToastMs = lo mismo que dura el temporizador

        // Dibuja el aviso en su propio bitmap (se REUSA siempre el mismo: cero basura para el recolector) y le
        // sube la version, que es lo que hace que la GPU vuelva a subirlo.
        private void HorneaToast(string text, Color back)
        {
            if (toastBmp == null) toastBmp = new Bitmap(ToastW, ToastH, PixelFormat.Format32bppPArgb);
            PintaToast(toastBmp, text, back, lblToast.Font);
            toastVer++; toastTick = Environment.TickCount;
        }

        // El dibujo en si, aparte del estado: asi se puede ver y comprobar sin arrancar la rocola entera.
        internal static void PintaToast(Bitmap bmp, string text, Color back, Font font)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // gris: ClearType deja flecos sobre transparente
                int tw = AnchoToast(text, font);   // el texto MANDA el ancho de la pastilla
                int x0 = (ToastW - tw) / 2;
                using (GraphicsPath gp = RoundRect(new Rectangle(x0, 0, tw, ToastH), 16))
                {
                    using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, gp);
                    using (Pen pe = new Pen(Color.FromArgb(70, 255, 255, 255), 1.5f)) g.DrawPath(pe, gp);   // filo claro: lo despega del fondo
                }
                using (SolidBrush b = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(text, font, b, new RectangleF(x0 + 10, 0, tw - 20, ToastH), sf);
                }
            }
        }

        // CURVA del aviso (funcion pura, para poder comprobarla): baja desde arriba mientras aparece, se queda
        // quieto, y al final sube un poco mientras se desvanece.
        internal static void CurvaToast(int dt, out float alpha, out float dy)
        {
            alpha = 1f; dy = 0f;
            if (dt < 0 || dt >= ToastMs) { alpha = 0f; return; }
            if (dt < ToastInMs)
            {
                float q = dt / (float)ToastInMs;
                float e = 1f - (1f - q) * (1f - q) * (1f - q);   // frenado suave al llegar
                alpha = e; dy = -30f * (1f - e);
            }
            else if (dt > ToastMs - ToastOutMs)
            {
                float q = (dt - (ToastMs - ToastOutMs)) / (float)ToastOutMs;
                alpha = 1f - q; dy = -16f * q;
            }
        }

        // Estado del aviso para la GPU: entra deslizandose desde arriba mientras aparece, se queda quieto, y
        // al final sube un poco mientras se desvanece. El avance va con el reloj, asi que se ve fluido a los
        // fps que de el equipo (no a los 24 del temporizador).
        private GpuToast BuildToastState()
        {
            GpuToast t = new GpuToast();
            if (toastTick < 0 || toastBmp == null) return t;
            int dt = Environment.TickCount - toastTick;
            if (dt < 0 || dt >= ToastMs) { toastTick = -1; return t; }
            float a, dy; CurvaToast(dt, out a, out dy);
            t.Has = true; t.Bmp = toastBmp; t.Ver = toastVer;
            t.X = (ClientSize.Width - ToastW) * 0.5f;
            t.Y = 80f + dy;
            t.Alpha = a;
            return t;
        }
#endif

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 1) d = 1;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---- Cola unificada (canciones + videos, en orden) + creditos ----
        private enum AddResult { Added, NoCredit, Repeat }

        private AddResult AddItem(PlayItem it)
        {
            if (it == null) return AddResult.Repeat;
            if (credits <= 0)
            {   // sin creditos no se puede poner nada, y ademas la capsula se SACUDE: a las dos de la
                // manana nadie lee el letrero, pero un movimiento si se ve.
                if (pastCred != null) { pastCred.Aviso(); pastCred.Invalidate(); }
                return AddResult.NoCredit;
            }
            // no repetir lo mismo dos veces seguidas (lo ultimo en sonar/encolar)
            PlayItem last = (queue.Count > 0) ? queue[queue.Count - 1] : nowPlaying;
            if (last != null && string.Equals(last.PathKey(), it.PathKey(), StringComparison.OrdinalIgnoreCase)) return AddResult.Repeat;
            queue.Add(it); RefrescaCola(); PrecalientaSiguiente();
            // EL TURNO SE APUNTA AQUI Y NO EN EL AVISO, a proposito: aqui la cola esta como quedo justo
            // al meterla, y unas lineas mas abajo PlayNext puede sacar la primera y correrlo todo.
            turnoPuesto = queue.Count;
            double[] antes = new double[queue.Count - 1];
            for (int i = 0; i < queue.Count - 1; i++) antes[i] = SegundosDe(queue[i].PathKey());
            turnoSegs = EsperaSegundos(antes, RestanDeLaQueSuena());
            VuelaALaCola(it.Disc);   // se VE: la portada sale de la pared y aterriza en la pastilla de la cola
            if (iaVivo && !it.IsVideo && it.Song != null) IaRequestJob(it.Song.Path);   // IA EN VIVO: prepara desde que se encola (lista para cuando le toque)
            RecordPlay(it);                                              // estadisticas: cuenta lo pedido
            credits--;                                                   // cada cosa cuesta 1 credito
            UpdateCredits();
            if (nowPlaying == null) PlayNext();   // nada sonando -> arranca (PlayNext guarda el estado); si algo suena, solo se encola
            else { UpdateNowPlayingUI(); SaveState(); }   // estado vivo: cola/creditos cambiaron
            return AddResult.Added;
        }

        // Aviso de cancion agregada, con acumulacion: si agregas varias rapido (mientras el aviso sigue),
        // dice "Cancion agregada", "Cancion agregada  X2", "X3"... para que quede claro cuantas van y no confunda.
        private void ShowSongAddedToast(bool search)
        {
            songTotal = SameToastActive(ToastKind.Song) ? songTotal + 1 : 1;
            // EL TURNO SUSTITUYE AL "X2". Antes, al pedir varias seguidas, el aviso contaba cuantas
            // llevabas; el turno dice algo mejor y lo dice solo, porque el puesto de la ultima YA sube
            // con cada una. Y si no se supiera el puesto -no deberia pasar- se vuelve al de siempre.
            string msg = TextoTurno(turnoPuesto, turnoSegs);
            if (msg.Length == 0) msg = songTotal > 1 ? "Cancion agregada  X" + songTotal : "Cancion agregada";
            if (search) ShowSearchToast(msg, true); else ShowToast(msg);
            MarkToast(ToastKind.Song);
        }

        // Suma creditos (la moneda) y actualiza el letrero
        private void AddCoin()
        {
            credits += CreditsPerCoin;
            // ANTES de UpdateCredits, a proposito: la pastilla se guarda el numero de AHORA para rodar
            // desde el, y quien lo cambia es la linea de mas abajo.
            if (pastCred != null) { pastCred.Moneda(); pastCred.Invalidate(); }
            statsCoins++; statsCreditsAdded += CreditsPerCoin; statsDirty = true;   // estadisticas
            UpdateCredits();
            SaveState();   // estado vivo: si se corta la luz, el dinero del cliente no se pierde
            // Monedas rapidas seguidas: el aviso SUMA (+4, +8, +12...) en vez de reiniciar; si ya paso su
            // tiempo (el usuario fue lento), arranca de nuevo en +4.
            coinTotal = SameToastActive(ToastKind.Coin) ? coinTotal + CreditsPerCoin : CreditsPerCoin;
            ShowToast("+" + coinTotal + " creditos", Color.FromArgb(40, 165, 90));
            MarkToast(ToastKind.Coin);
        }
    }
}

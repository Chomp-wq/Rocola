using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Rocola
{
    // Tiempo real, no numero de cuadros. Una combinacion consumida exige liberar
    // AMBOS botones antes de permitir otra; tampoco se reanuda al volver el foco.
    internal sealed class AsistenteCombinacion
    {
        private bool capturada, cancelada, activada;
        private int inicio;
        private float progreso;
        public bool Capturada { get { return capturada; } }
        public float Progreso { get { return progreso; } }
        public void Reinicia() { capturada = cancelada = activada = false; progreso = 0; }
        public bool Actualiza(int ahora, bool rojo, bool verde, bool permitido)
        {
            if (!rojo && !verde) { Reinicia(); return false; }
            if (!permitido)
            {
                if (capturada || (rojo && verde)) { capturada = cancelada = true; progreso = 0; }
                return false;
            }
            if (!capturada)
            {
                if (!rojo || !verde) return false;
                capturada = true; inicio = ahora; progreso = 0;
            }
            if (!rojo || !verde) { cancelada = true; progreso = 0; return false; }
            if (cancelada || activada) return false;
            int tiempo = unchecked(ahora - inicio);
            if (tiempo < 0) { cancelada = true; progreso = 0; return false; }
            progreso = Math.Min(1f, tiempo / 3000f);
            if (tiempo < 3000) return false;
            activada = true; return true;
        }
    }

    public partial class Form1
    {
        private bool asistenteAnimado = true;
#if ROCOLA_GPU
        private readonly AsistenteCombinacion asistenteCombo = new AsistenteCombinacion();
        private bool asistenteVisible, asistenteReenvio, asistentePendiente, asistenteRestaurarVideo;
        private Message asistenteMensaje;
        private Keys asistenteTecla;
        private int asistentePendienteTick, asistenteInicioTick, asistenteActividadTick;
        private int asistentePagina, asistenteLeccion, asistenteSeleccion;
        private int asistenteEvento;
        private AsistenteGestoGpu asistenteGesto;
        private Bitmap asistentePanel;
        private int asistentePanelVersion, asistentePanelPagina = -1, asistentePanelSeleccion = -1, asistentePanelLeccion = -1;
        private bool AsistenteVisible { get { return asistenteVisible || (gpuPanel != null && gpuPanel.AsistenteEnTransicion); } }

        private bool VerdeHeld()
        { return KeyHeld(keyOK) || (KeyHeld(Keys.Enter) && !IsConfiguredKey(Keys.Enter)); }

        private bool AsistentePermitido()
        {
            return gpuEnabled && gpuPanel != null && ContainsFocus && Enabled
                && !arranqueEnEspera && !startupClosing && !IsDisposed && !configMode
                && !maintMode && !libBuilding && keyOK != keyBack;
        }

        private void AbreAsistente()
        {
            if (asistenteVisible) return;
            asistentePendiente = false; asistenteVisible = true;
            asistentePagina = asistenteLeccion = asistenteSeleccion = 0;
            asistenteInicioTick = asistenteActividadTick = Environment.TickCount;
            AnimaAsistente(AsistenteGestoGpu.Saludar);
            if (videoFull) ExitVideoFull();
            if (attractOn) ExitAttract();
            if (video != null) video.Visible = false;
            if (gpuPanel != null) { gpuPanel.IdleExpandTarget = false; gpuPanel.TouchUi(); }
            SyncGpuFullScreen();
        }

        private void CierraAsistente()
        {
            AnimaAsistente(AsistenteGestoGpu.Despedir);
            asistenteVisible = false; asistentePendiente = false;
            lastInput = DateTime.Now; searchLastActivity = DateTime.Now;
            asistenteRestaurarVideo = true;
            if (video != null) video.Visible = videoOn && !searchOpen && !AsistenteVisible;
            if (!startupClosing && !IsDisposed) { SyncGpuFullScreen(); KeepVideoOnTop(); }
        }

        private void DescartaEntradaAsistente()
        {
            asistentePendiente = false;
            if (asistenteCombo != null) asistenteCombo.Actualiza(Environment.TickCount, BackHeld(), VerdeHeld(), false);
            if (asistenteVisible) CierraAsistente();
        }

        private void ReenviaEntradaAsistente()
        {
            if (!asistentePendiente) return;
            Message mensaje = asistenteMensaje; Keys tecla = asistenteTecla;
            asistentePendiente = false;
            if (!AsistentePermitido() || asistenteVisible || asistenteCombo.Capturada) return;
            asistenteReenvio = true;
            try { ProcessCmdKey(ref mensaje, tecla); }
            finally { asistenteReenvio = false; }
        }

        private bool AsistenteEntrada(Keys keyData, ref Message msg)
        {
            if (asistenteReenvio || asistenteCombo == null) return false;
            Keys kc = keyData & Keys.KeyCode;
            // Mantenimiento/salida siempre accesibles; no replay tardio al cerrar un dialogo.
            if ((keyData & (Keys.Control | Keys.Alt)) != 0 || kc == Keys.F5 || kc == Keys.F12
                || ((kc == Keys.F8 || kc == Keys.F10) && !IsConfiguredKey(kc)))
            { DescartaEntradaAsistente(); return false; }
            if (!AsistentePermitido()) { DescartaEntradaAsistente(); return false; }
            bool rojo = BackHeld() || IsBack(kc), verde = VerdeHeld() || IsOK(kc);
            if (asistenteCombo.Actualiza(Environment.TickCount, rojo, verde, true)) AbreAsistente();
            if (asistenteCombo.Capturada) { asistentePendiente = false; return true; }
            if (!asistenteVisible && AsistenteVisible) return true; // regreso animado: no activar debajo
            if (asistenteVisible)
            {
                asistenteActividadTick = Environment.TickCount;
                if (unchecked(Environment.TickCount - asistenteInicioTick) < 750) return true;
                // Pulsacion unica: mantener verde/rojo no recorre varios pasos.
                if ((msg.LParam.ToInt64() & 0x40000000L) != 0) return true;
                if (IsBack(kc)) { if (asistentePagina == 0) CierraAsistente(); else { asistentePagina = asistenteSeleccion = 0; AnimaAsistente(AsistenteGestoGpu.Volver); } }
                else if (IsLeft(kc) || IsUp(kc)) SeleccionaAsistente(0);
                else if (IsRight(kc) || IsDown(kc)) SeleccionaAsistente(1);
                else if (IsOK(kc)) EligeAsistente();
                return true;
            }
            if (IsOK(kc) || IsBack(kc))
            {
                // Breve ventana para reconocer ambos ordenes del acorde. La pista
                // responde ya; solo se aplaza la accion para no gastar/borrar antes.
                if (asistentePendiente) return true;
                if ((msg.LParam.ToInt64() & 0x40000000L) != 0) return false;
                asistenteMensaje = msg; asistenteTecla = keyData;
                asistentePendienteTick = Environment.TickCount; asistentePendiente = true;
                if (searchOpen && searchOverlay != null) { if (IsOK(kc)) searchOverlay.FlashOK(); }
                else if (helpBar != null) helpBar.Flash(IsOK(kc) ? HelpBar.FEntrar : HelpBar.FVolver);
                return true;
            }
            ReenviaEntradaAsistente();
            return false;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            // Atender el soltar real, no esperar hasta el siguiente timer: dos
            // toques rapidos de verde no se convierten en una sola pulsacion.
            if (asistenteCombo != null && (IsBack(e.KeyCode) || IsOK(e.KeyCode)))
            {
                bool permitido = AsistentePermitido();
                if (asistenteCombo.Actualiza(Environment.TickCount, BackHeld(), VerdeHeld(), permitido)) AbreAsistente();
                if (asistenteCombo.Capturada) asistentePendiente = false;
                else if (asistentePendiente && (asistenteTecla & Keys.KeyCode) == e.KeyCode)
                { if (permitido) ReenviaEntradaAsistente(); else asistentePendiente = false; }
            }
            base.OnKeyUp(e);
        }

        private void TickAsistenteGpu()
        {
            if (asistenteCombo == null) return; // propietarios aislados de pruebas sin constructor
            if (asistenteRestaurarVideo && !AsistenteVisible)
            {
                asistenteRestaurarVideo = false;
                if (video != null) video.Visible = videoOn && !searchOpen;
                SyncGpuFullScreen(); KeepVideoOnTop();
            }
            bool permitido = AsistentePermitido();
            bool rojo = BackHeld(), verde = VerdeHeld();
            if (asistenteCombo.Actualiza(Environment.TickCount, rojo, verde, permitido)) AbreAsistente();
            if (!permitido) { asistentePendiente = false; if (asistenteVisible) CierraAsistente(); return; }
            if (asistenteCombo.Capturada) asistentePendiente = false;
            else if (asistentePendiente && (unchecked(Environment.TickCount - asistentePendienteTick) >= 120
                || (!IsOK(asistenteTecla & Keys.KeyCode) ? !rojo : !verde))) ReenviaEntradaAsistente();
            if (asistenteVisible)
            {
                if (unchecked(Environment.TickCount - asistenteActividadTick) >= 90000) { CierraAsistente(); return; }
                // Solo evita decisiones de inactividad. El audio/FFT/color siguen en su latido normal.
                lastInput = DateTime.Now; searchLastActivity = lastInput;
                if (gpuPanel != null) gpuPanel.IdleExpandTarget = false;
            }
        }

        // Moneda y salto siguen disponibles en la ayuda, pero nunca reproducen
        // despues un verde pendiente ni completan un acorde interrumpido.
        private void InterrumpeComboAsistente(bool moneda)
        {
            asistentePendiente = false;
            if (asistenteCombo != null) asistenteCombo.Actualiza(Environment.TickCount, BackHeld(), VerdeHeld(), false);
            if (asistenteVisible) {
                asistenteActividadTick = Environment.TickCount;
                if (moneda) AnimaAsistente(AsistenteGestoGpu.Credito);
            }
        }

        private void AnimaAsistente(AsistenteGestoGpu gesto)
        { asistenteGesto = gesto; asistenteEvento = unchecked(asistenteEvento + 1); }

        private void SeleccionaAsistente(int seleccion)
        {
            seleccion = Math.Max(0, Math.Min(1, seleccion));
            if (asistenteSeleccion == seleccion) return;
            asistenteSeleccion = seleccion; AnimaAsistente(AsistenteGestoGpu.Senalar);
        }

        private void EligeAsistente()
        {
            if (asistentePagina == 0) {
                asistentePagina = asistenteSeleccion == 0 ? 1 : 2; asistenteLeccion = asistenteSeleccion = 0;
                AnimaAsistente(asistentePagina == 1 ? AsistenteGestoGpu.Explicar : AsistenteGestoGpu.Buscar);
            }
            else if (asistentePagina == 1)
            {
                if (asistenteSeleccion == 1) asistentePagina = asistenteSeleccion = 0;
                else if (asistenteLeccion < 2) asistenteLeccion++;
                else { asistentePagina = 2; asistenteSeleccion = 0; }
                AnimaAsistente(asistentePagina == 0 ? AsistenteGestoGpu.Volver : asistentePagina == 1 ? AsistenteGestoGpu.Explicar : AsistenteGestoGpu.Buscar);
            }
            else
            {
                bool buscar = asistenteSeleccion == 0;
                CierraAsistente();
                AnimaAsistente(AsistenteGestoGpu.Confirmar);
                if (buscar) { if (!searchOpen) OpenSearch(); else MarkSearchActivity(); }
                else { if (searchOpen) CloseSearch(); if (panelSongs != null && panelSongs.Visible) BackToAlbums(); }
            }
        }

        private void AsistenteClick(Point punto)
        {
            if (!asistenteVisible || asistenteCombo.Capturada || unchecked(Environment.TickCount - asistenteInicioTick) < 750) return;
            Rectangle r = GpuPanel.AsistentePanelRect(ClientSize.Width, ClientSize.Height);
            punto.Offset(-r.Left, -r.Top);
            for (int i = 0; i < 2; i++) if (AsistenteOpcionRect(r.Width, r.Height, i).Contains(punto))
            { asistenteSeleccion = i; asistenteActividadTick = Environment.TickCount; EligeAsistente(); return; }
        }

        private void AsistenteMueveRaton(Point punto)
        {
            if (!asistenteVisible || asistenteCombo.Capturada || unchecked(Environment.TickCount - asistenteInicioTick) < 750) return;
            asistenteActividadTick = Environment.TickCount;
            Rectangle r = GpuPanel.AsistentePanelRect(ClientSize.Width, ClientSize.Height);
            punto.Offset(-r.Left, -r.Top);
            for (int i = 0; i < 2; i++) if (AsistenteOpcionRect(r.Width, r.Height, i).Contains(punto))
            { SeleccionaAsistente(i); return; }
        }

        private AsistenteEstadoGpu BuildAsistenteState()
        {
            AsistenteEstadoGpu estado = new AsistenteEstadoGpu();
            estado.Visible = asistenteVisible; estado.Animada = asistenteAnimado;
            estado.Gesto = asistenteGesto; estado.Evento = asistenteEvento;
            estado.Pagina = asistentePagina; estado.Leccion = asistenteLeccion; estado.Seleccion = asistenteSeleccion;
            // Conservar bitmap durante el regreso animado, sin volver a hornearlo.
            if (asistenteVisible) EnsureAsistentePanel();
            estado.Panel = asistentePanel; estado.Version = asistentePanelVersion;
            return estado;
        }

        private void EnsureAsistentePanel()
        {
            Rectangle r = GpuPanel.AsistentePanelRect(ClientSize.Width, ClientSize.Height);
            if (asistentePanel != null && asistentePanel.Size == r.Size && asistentePanelPagina == asistentePagina
                && asistentePanelSeleccion == asistenteSeleccion && asistentePanelLeccion == asistenteLeccion) return;
            Bitmap nuevo = PintaPanelAsistente(r.Width, r.Height, asistentePagina, asistenteLeccion, asistenteSeleccion);
            Bitmap anterior = asistentePanel; asistentePanel = nuevo; asistentePanelVersion++;
            asistentePanelPagina = asistentePagina; asistentePanelSeleccion = asistenteSeleccion; asistentePanelLeccion = asistenteLeccion;
            if (anterior != null) anterior.Dispose();
        }

        internal static Rectangle AsistenteOpcionRect(int w, int h, int i)
        {
            int x = AsistenteGloboMargen(w), derecha = Math.Max(12, w / 20);
            return new Rectangle(x, (int)(h * (.61f + i * .16f)), Math.Max(1, w - x - derecha), Math.Max(24, (int)(h * .13f)));
        }

        internal static int AsistenteGloboMargen(int w)
        { return Math.Min(Math.Max(0, w - 1), Math.Max(12, w / 20) + Math.Min(32, Math.Max(18, w / 18))); }

        // Contorno unico: la cola del globo apunta a la estrella y no deja una
        // costura entre un rectangulo y un triangulo. Solo se compone al cambiar texto.
        internal static GraphicsPath AsistenteGloboCamino(int w, int h)
        {
            GraphicsPath p = new GraphicsPath();
            if (w < 64 || h < 64) { p.AddRectangle(new RectangleF(0, 0, Math.Max(1, w), Math.Max(1, h))); return p; }
            float x = Math.Min(32, Math.Max(18, w / 18)), y = 2, derecha = w - 3, abajo = h - 3;
            float r = Math.Min(36, Math.Min((derecha - x) * .18f, h * .10f));
            float cola = h * .46f, baseCola = Math.Min(30, h * .055f);
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(derecha - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(derecha - r * 2, abajo - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, abajo - r * 2, r * 2, r * 2, 90, 90);
            p.AddLine(x, abajo - r, x, cola + baseCola);
            p.AddBezier(x, cola + baseCola, x - 2, cola + baseCola * .3f, 4, cola + 4, 2, cola);
            p.AddBezier(2, cola, 6, cola - 2, x - 3, cola - baseCola * .8f, x, cola - baseCola);
            p.AddLine(x, cola - baseCola, x, y + r); p.CloseFigure(); return p;
        }

        internal static Bitmap PintaPanelAsistente(int w, int h, int pagina, int leccion, int seleccion)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font pequeno = new Font("Trebuchet MS", Math.Max(10, Math.Min(18, h * .034f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font titulo = new Font("Trebuchet MS", Math.Max(20, Math.Min(42, h * .085f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font cuerpo = new Font("Trebuchet MS", Math.Max(12, Math.Min(21, h * .043f)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font opcion = new Font("Trebuchet MS", Math.Max(14, Math.Min(27, h * .052f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush blanco = new SolidBrush(Color.FromArgb(246, 248, 255)))
            using (SolidBrush tenue = new SolidBrush(Color.FromArgb(185, 206, 234)))
            using (SolidBrush azul = new SolidBrush(Color.FromArgb(150, 215, 255)))
            using (StringFormat formato = new StringFormat())
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (GraphicsPath borde = AsistenteGloboCamino(w, h))
                using (LinearGradientBrush vidrio = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(252, 24, 42, 74), Color.FromArgb(248, 10, 18, 37), 90f))
                using (Pen halo = new Pen(Color.FromArgb(35, 101, 206, 255), 5f))
                using (Pen brillo = new Pen(Color.FromArgb(220, 142, 215, 255), 1.8f))
                { g.DrawPath(halo, borde); g.FillPath(vidrio, borde); g.DrawPath(brillo, borde); }
                int x = AsistenteGloboMargen(w), tw = Math.Max(1, w - x - Math.Max(12, w / 20));
                string saludo = pagina == 0 ? "¡HOLA! ESTOY PARA AYUDARTE" : pagina == 1 ? "VAMOS PASO A PASO" : "ENCONTREMOS TU CANCIÓN";
                g.DrawString(saludo, pequeno, azul, new RectangleF(x, h * .045f, tw, h * .06f));
                string cabecera = pagina == 0 ? "¿Qué necesitas?" : pagina == 1 ? "Cómo usar la Rocola" : "Vamos a poner una canción";
                g.DrawString(cabecera, titulo, blanco, new RectangleF(x, h * .12f, tw, h * .17f));
                string texto = pagina == 0 ? "Vamos paso a paso. Elige lo que necesitas; yo te acompaño y la música sigue sonando." : pagina == 2 ?
                    "Busca por título o artista. Escribe con el teclado, o elige letras con las flechas y verde. Después elige un resultado y pulsa verde para añadirlo a la cola." :
                    leccion == 0 ? "1. Muévete con las flechas entre los discos.\n2. Pulsa verde para ver sus canciones.\n3. Elige una canción y pulsa verde para añadirla a la cola." :
                    leccion == 1 ? "Mantén rojo y pulsa izquierda o derecha para cambiar de letra.\nPara abrir o cerrar el buscador, mantén rojo y pulsa arriba." :
                    "Cada canción cuesta 1 crédito. Inserta una moneda si no tienes.\nDentro de un disco, rojo vuelve a la pared. En el buscador, rojo borra.";
                g.DrawString(texto, cuerpo, tenue, new RectangleF(x, h * .31f, tw, h * .27f));
                formato.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < 2; i++)
                {
                    Rectangle r = AsistenteOpcionRect(w, h, i);
                    using (GraphicsPath camino = RoundedPath(r, Math.Min(14, r.Height / 3)))
                    using (SolidBrush relleno = new SolidBrush(i == seleccion ? Color.FromArgb(235, 42, 87, 130) : Color.FromArgb(150, 28, 42, 68)))
                    using (Pen contorno = new Pen(i == seleccion ? Color.FromArgb(150, 235, 188) : Color.FromArgb(90, 113, 150), i == seleccion ? 2f : 1f))
                    { g.FillPath(relleno, camino); g.DrawPath(contorno, camino); }
                    string nombre = pagina == 0 ? (i == 0 ? "Cómo usar la Rocola" : "Ayúdame a poner una canción") : pagina == 1 ?
                        (i == 1 ? "Volver al inicio" : leccion < 2 ? "Siguiente paso" : "Poner una canción") : (i == 0 ? "Abrir el buscador" : "Prefiero explorar los discos");
                    g.DrawString(nombre, opcion, blanco, new RectangleF(r.Left + 12, r.Top, Math.Max(1, r.Width - 24), r.Height), formato);
                }
                g.DrawString("FLECHAS: elegir · VERDE: aceptar · ROJO: volver", pequeno, azul, new RectangleF(x, h * .93f, tw, h * .065f));
            }
            return bmp;
        }

        private void SueltaAsistente()
        {
            asistentePendiente = asistenteVisible = false;
            if (asistentePanel != null) { asistentePanel.Dispose(); asistentePanel = null; }
        }
#endif
    }
}

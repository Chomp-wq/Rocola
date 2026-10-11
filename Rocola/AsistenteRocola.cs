using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private bool asistenteAnimado = true;
#if ROCOLA_GPU
        // LA AYUDA SE ABRE CON EL ROJO Y LA FLECHA DE ABAJO (peticion del 11-10-2026), la gemela de BUSCAR, que
        // es el rojo y la flecha de arriba. Las dos casillas estan una al lado de la otra y llevan sus dos botones
        // dibujados, asi que no hay nada que adivinar. Antes era mantener rojo y verde tres segundos: nadie lo
        // descubria, y obligaba a retrasar 120 ms cada toque de rojo o verde para reconocer el acorde. Eso se
        // acabo: los botones responden ya, y ROJO + ABAJO con la ayuda abierta la vuelve a guardar.
        //
        // Y NO DA INSTRUCCIONES ESCRITAS Y YA: primero pregunta que quieres hacer y despues TE LLEVA DE LA MANO.
        // Mira lo que de verdad esta pasando en la Rocola (donde estas, si tienes credito, si abriste el buscador,
        // si ya pusiste la cancion), te senala con la mano y una flecha dorada lo que toca ahora, y tu lo haces con
        // los botones de siempre: la ayuda no se queda con ellos. Al lograrlo celebra y vuelve sola al cielo.
        private bool asistenteVisible, asistenteRestaurarVideo;
        private int asistenteInicioTick, asistenteActividadTick;
        private int asistentePagina, asistenteLeccion, asistenteSeleccion;
        private int asistenteEvento;
        private AsistenteGestoGpu asistenteGesto;
        private Bitmap asistentePanel;
        private int asistentePanelVersion, asistentePanelPagina = -1, asistentePanelSeleccion = -1, asistentePanelLeccion = -1;
        private Size asistentePanelTam;
        private bool AsistenteVisible { get { return asistenteVisible || AsistenteRegresando; } }

        // EL REGRESO ANIMADO TIENE TOPE (aviso del 11-10-2026: "despues de usar el asistente una vez, los controles se
        // bloquean para siempre"). Mientras la estrella vuelve a su sitio (0,72 s) la rocola no deja que un boton active
        // lo de debajo. Pero ese "regresando" lo decide la tarjeta con lo que vio en su ULTIMO cuadro: si los cuadros se
        // paraban con la ayuda todavia abierta (el panel escondido por un video, la ventana minimizada, el dispositivo
        // perdido), la tarjeta seguia creyendo que la ayuda estaba abierta y el regreso no se acababa nunca: se tragaba
        // los botones, no salian las pistas del rojo y ROJO + ABAJO no la volvia a abrir. Ahora lo cuenta el formulario
        // con su propio reloj: pasado RegresoTopeMs desde que se guardo, el regreso ya no retiene nada, digan lo que
        // digan los cuadros (la animacion termina sola cuando vuelvan).
        internal const int RegresoTopeMs = 1500;
        private bool asistenteRegresando; private int asistenteCierreTick, regresoAtascadoApuntes;
        private bool AsistenteRegresando
        {
            get
            {
                if (!asistenteRegresando) return false;
                bool tarjeta = gpuPanel != null && gpuPanel.AsistenteEnTransicion;
                if (tarjeta && RegresoVigente(asistenteCierreTick, Environment.TickCount)) return true;
                asistenteRegresando = false;
                if (tarjeta && regresoAtascadoApuntes < 20)
                { regresoAtascadoApuntes++; LogError("Ayuda: el regreso de la estrella no termino a tiempo (sin cuadros); se sueltan los botones", null); }
                return false;
            }
        }
        // Pura: se prueba sin ventana ni tarjeta. Aguanta la vuelta del contador de Windows (49,7 dias).
        internal static bool RegresoVigente(int cierreTick, int ahoraTick)
        { int d = unchecked(ahoraTick - cierreTick); return d >= 0 && d < RegresoTopeMs; }

        // LA ROCOLA TIENE EL TECLADO: algo suyo tiene el foco, o es la ventana de delante aunque el foco se haya quedado
        // en nada o fuera de ella (el video, un control que se escondio). Antes se exigia el foco estricto, y con la
        // ventana delante pero sin foco la ayuda y las pistas del rojo quedaban negadas sin remedio: el vigilante del
        // foco solo actuaba si OTRA ventana se ponia delante (ahora tambien devuelve el foco, FocusTimer_Tick).
        private static bool sinUser32;
        private bool RocolaConTeclado()
        {
            if (ContainsFocus) return true;
            if (sinUser32 || !IsHandleCreated) return false;
            try { return GetForegroundWindow() == Handle; }
            catch { sinUser32 = true; return false; }   // sin Windows (pruebas en Linux): solo cuenta el foco
        }
        // La ayuda solo se queda con los botones mientras PREGUNTA (el menu) y durante su regreso animado.
        // Guiando, cada boton hace lo de siempre: es el cliente quien lo hace, la estrella solo senala.
        private bool AsistenteCapturaEntrada
        { get { return (asistenteVisible && asistenteMision == 0) || (!asistenteVisible && AsistenteVisible); } }


        private bool AsistentePermitido()
        {
            return gpuEnabled && gpuPanel != null && RocolaConTeclado() && Enabled
                && !arranqueEnEspera && !startupClosing && !IsDisposed && !configMode
                && !maintMode && !libBuilding && keyOK != keyBack;
        }

        private void AbreAsistente()
        {
            if (asistenteVisible) return;
            asistenteVisible = true; asistenteRegresando = false;   // si aun regresaba, la tarjeta da media vuelta desde donde iba
            asistentePagina = asistenteLeccion = asistenteSeleccion = 0;
            asistenteMision = 0; asistenteLogroTick = 0;
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
            asistenteVisible = false; asistenteMision = 0; asistenteLogroTick = 0;
            asistenteRegresando = true; asistenteCierreTick = Environment.TickCount;
            lastInput = DateTime.Now; searchLastActivity = DateTime.Now;
            asistenteRestaurarVideo = true;
            if (video != null) video.Visible = videoOn && !searchOpen && !AsistenteVisible;
            if (!startupClosing && !IsDisposed) { SyncGpuFullScreen(); KeepVideoOnTop(); }
        }

        // ROJO + ABAJO pulsado y la ayuda no se pudo abrir: se apunta por que (rocola_error.txt, tope por sesion).
        private int ayudaNoPermitidaApuntes;
        private void ApuntaAyudaNoPermitida()
        {
            if (ayudaNoPermitidaApuntes >= 20) return;
            string motivo = !gpuEnabled || gpuPanel == null ? "sin tarjeta grafica" : !RocolaConTeclado() ? "la rocola no tiene el foco del teclado"
                : !Enabled ? "ventana deshabilitada" : arranqueEnEspera ? "arrancando" : configMode ? "configurando botones"
                : maintMode ? "mantenimiento (F10)" : libBuilding ? "leyendo la biblioteca"
                : keyOK == keyBack ? "verde y rojo son la misma tecla" : "cerrando";
            ayudaNoPermitidaApuntes++;
            LogError("ROJO + ABAJO sin ayuda: " + motivo, null);
        }

        // ROJO + ABAJO, en una sola pulsacion (sin autorrepeticion). Pura: se prueba sin teclado.
        internal static bool EsAtajoAyuda(bool rojoMantenido, bool esAbajo, bool repeticion)
        { return rojoMantenido && esAbajo && !repeticion; }

        private bool AsistenteEntrada(Keys keyData, ref Message msg)
        {
            Keys kc = keyData & Keys.KeyCode;
            // Mantenimiento/salida siempre accesibles.
            if ((keyData & (Keys.Control | Keys.Alt)) != 0 || kc == Keys.F5 || kc == Keys.F12
                || ((kc == Keys.F8 || kc == Keys.F10) && !IsConfiguredKey(kc)))
            { if (asistenteVisible) CierraAsistente(); return false; }
            bool repeticion = (msg.LParam.ToInt64() & 0x40000000L) != 0;
            if (!AsistentePermitido())
            {
                if (asistenteVisible) CierraAsistente();
                else if (EsAtajoAyuda(BackHeld(), IsDown(kc), repeticion)) ApuntaAyudaNoPermitida();
                return false;
            }
            if (asistenteVisible) asistenteActividadTick = Environment.TickCount;
            if (EsAtajoAyuda(BackHeld(), IsDown(kc), repeticion))
            {
                // Tambien mientras la estrella regresa: antes ese medio segundo no hacia nada y parecia bloqueada.
                if (asistenteVisible) CierraAsistente(); else AbreAsistente();
                return true;
            }
            if (!AsistenteCapturaEntrada) return false;   // guiando: el boton hace lo suyo de siempre
            if (!asistenteVisible) return true;           // regreso animado: no activar nada debajo
            if (unchecked(Environment.TickCount - asistenteInicioTick) < 450) return true;
            if (repeticion) return true;                  // mantener un boton no recorre varias opciones
            if (IsBack(kc)) CierraAsistente();
            else if (IsLeft(kc) || IsUp(kc)) SeleccionaAsistente(asistenteSeleccion - 1);
            else if (IsRight(kc) || IsDown(kc)) SeleccionaAsistente(asistenteSeleccion + 1);
            else if (IsOK(kc)) EligeAsistente();
            return true;
        }

        private void TickAsistenteGpu()
        {
            VigilaRojoMantenido();   // si el rojo mantenido no da sus pistas, la bitacora dice por que (GuiaRojoGpu.cs)
            if (asistenteRestaurarVideo && !AsistenteVisible)
            {
                asistenteRestaurarVideo = false;
                if (video != null) video.Visible = videoOn && !searchOpen;
                SyncGpuFullScreen(); KeepVideoOnTop();
            }
            if (ayudaView != null && ayudaView.Abierta != asistenteVisible)
            {   // encendida mientras la ayuda esta abierta (la textura de la interfaz lo ve en su firma; el control, aqui)
                ayudaView.Abierta = asistenteVisible;
                ayudaView.Invalidate();
            }
            if (!asistenteVisible) return;
            if (!AsistentePermitido()) { CierraAsistente(); return; }
            if (unchecked(Environment.TickCount - asistenteActividadTick) >= 90000) { CierraAsistente(); return; }
            if (asistenteMision != 0) EvaluaGuia();
            if (!asistenteVisible) return;
            // Solo evita decisiones de inactividad. El audio/FFT/color siguen en su latido normal.
            lastInput = DateTime.Now; searchLastActivity = lastInput;
            if (gpuPanel != null) gpuPanel.IdleExpandTarget = false;
        }

        // Una moneda real: la estrella lo celebra (y si estaba esperando la moneda, la guia sigue sola).
        private void InterrumpeComboAsistente(bool moneda)
        {
            if (asistenteVisible) {
                asistenteActividadTick = Environment.TickCount;
                if (moneda) { AnimaAsistente(AsistenteGestoGpu.Credito); asistenteMonedaTick = Environment.TickCount; if (asistenteMonedaTick == 0) asistenteMonedaTick = 1; }
            }
        }

        private void AnimaAsistente(AsistenteGestoGpu gesto)
        { asistenteGesto = gesto; asistenteEvento = unchecked(asistenteEvento + 1); }

        internal const int AsistenteOpciones = 3;
        private void SeleccionaAsistente(int seleccion)
        {
            seleccion = Math.Max(0, Math.Min(AsistenteOpciones - 1, seleccion));
            if (asistenteSeleccion == seleccion) return;
            asistenteSeleccion = seleccion; AnimaAsistente(AsistenteGestoGpu.Senalar);
        }

        // Elegir en el menu empieza la mision: 1 poner una cancion (por los discos), 2 buscarla por su nombre,
        // 3 aprender a moverse. Desde ahi la guia va sola (AsistenteGuiaRocola.cs).
        private void EligeAsistente()
        {
            if (!asistenteVisible || asistenteMision != 0) return;
            EmpiezaMision(asistenteSeleccion + 1);
        }

        private void AsistenteClick(Point punto)
        {
            if (!asistenteVisible || asistenteMision != 0 || unchecked(Environment.TickCount - asistenteInicioTick) < 450) return;
            Rectangle r = GpuPanel.AsistentePanelRect(ClientSize.Width, ClientSize.Height);
            // Tocar fuera del globo guarda la pregunta (en una pantalla tactil no hay boton ROJO a mano). El toque no
            // sigue hacia la pared: quien lo dio queria cerrar, no elegir un disco a ciegas.
            if (!r.Contains(punto)) { CierraAsistente(); return; }
            punto.Offset(-r.Left, -r.Top);
            for (int i = 0; i < AsistenteOpciones; i++) if (AsistenteOpcionRect(r.Width, r.Height, i).Contains(punto))
            { asistenteSeleccion = i; asistenteActividadTick = Environment.TickCount; EligeAsistente(); return; }
        }

        private void AsistenteMueveRaton(Point punto)
        {
            if (!asistenteVisible || asistenteMision != 0 || unchecked(Environment.TickCount - asistenteInicioTick) < 450) return;
            asistenteActividadTick = Environment.TickCount;
            Rectangle r = GpuPanel.AsistentePanelRect(ClientSize.Width, ClientSize.Height);
            punto.Offset(-r.Left, -r.Top);
            for (int i = 0; i < AsistenteOpciones; i++) if (AsistenteOpcionRect(r.Width, r.Height, i).Contains(punto))
            { SeleccionaAsistente(i); return; }
        }

        private AsistenteEstadoGpu BuildAsistenteState()
        {
            AsistenteEstadoGpu estado = new AsistenteEstadoGpu();
            estado.Visible = asistenteVisible; estado.Animada = asistenteAnimado;
            estado.Gesto = asistenteGesto; estado.Evento = asistenteEvento;
            estado.Pagina = asistentePagina; estado.Leccion = asistenteLeccion; estado.Seleccion = asistenteSeleccion;
            if (asistenteVisible && asistenteMision != 0)
            {
                estado.Guia = true; estado.Logro = asistenteLogroTick != 0;
                estado.Objetivo = estado.Logro ? RectangleF.Empty : ObjetivoGuia(asistentePaso);
            }
            // Conservar bitmap durante el regreso animado, sin volver a hornearlo.
            if (asistenteVisible) EnsureAsistentePanel();
            estado.Panel = asistentePanel; estado.Version = asistentePanelVersion;
            return estado;
        }

        private void EnsureAsistentePanel()
        {
            bool guia = asistenteMision != 0;
            int w = ClientSize.Width, h = ClientSize.Height;
            Size tam = guia ? GpuPanel.AsistenteGuiaTam(w, h) : GpuPanel.AsistentePanelRect(w, h).Size;
            int pagina = guia ? 16 + asistenteMision : 0;
            int leccion = guia ? ClaveGuia() : 0;
            if (asistentePanel != null && asistentePanelTam == tam && asistentePanelPagina == pagina
                && asistentePanelSeleccion == asistenteSeleccion && asistentePanelLeccion == leccion) return;
            Bitmap nuevo = guia ? PintaGuiaActual(tam.Width, tam.Height)
                : PintaPanelAsistente(tam.Width, tam.Height, 0, 0, asistenteSeleccion);
            Bitmap anterior = asistentePanel; asistentePanel = nuevo; asistentePanelVersion++;
            asistentePanelTam = tam; asistentePanelPagina = pagina; asistentePanelSeleccion = asistenteSeleccion; asistentePanelLeccion = leccion;
            asistentePagina = pagina; asistenteLeccion = leccion;
            if (anterior != null) anterior.Dispose();
        }

        internal static Rectangle AsistenteOpcionRect(int w, int h, int i)
        {
            int x = AsistenteGloboMargen(w), derecha = Math.Max(12, w / 20);
            return new Rectangle(x, (int)(h * (.50f + i * .135f)), Math.Max(1, w - x - derecha), Math.Max(22, (int)(h * .115f)));
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
                g.DrawString("¡HOLA! TE ACOMPAÑO PASO A PASO", pequeno, azul, new RectangleF(x, h * .045f, tw, h * .06f));
                g.DrawString("¿Qué quieres hacer?", titulo, blanco, new RectangleF(x, h * .12f, tw, h * .17f));
                g.DrawString("Elige y yo te voy señalando qué botón tocar. Tú lo haces y la música sigue sonando.",
                    cuerpo, tenue, new RectangleF(x, h * .30f, tw, h * .19f));
                formato.LineAlignment = StringAlignment.Center;
                string[] nombres = { "Poner una canción", "Buscar una canción por su nombre", "Aprender a moverme" };
                for (int i = 0; i < AsistenteOpciones; i++)
                {
                    Rectangle r = AsistenteOpcionRect(w, h, i);
                    using (GraphicsPath camino = RoundedPath(r, Math.Min(14, r.Height / 3)))
                    using (SolidBrush relleno = new SolidBrush(i == seleccion ? Color.FromArgb(235, 42, 87, 130) : Color.FromArgb(150, 28, 42, 68)))
                    using (Pen contorno = new Pen(i == seleccion ? Color.FromArgb(150, 235, 188) : Color.FromArgb(90, 113, 150), i == seleccion ? 2f : 1f))
                    { g.FillPath(relleno, camino); g.DrawPath(contorno, camino); }
                    g.DrawString(nombres[i], opcion, blanco, new RectangleF(r.Left + 12, r.Top, Math.Max(1, r.Width - 24), r.Height), formato);
                }
                g.DrawString("FLECHAS: elegir · VERDE: aceptar · ROJO: salir", pequeno, azul, new RectangleF(x, h * .925f, tw, h * .065f));
            }
            return bmp;
        }

        private void SueltaAsistente()
        {
            asistenteVisible = false; asistenteMision = 0; asistenteLogroTick = 0;
            if (asistentePanel != null) { asistentePanel.Dispose(); asistentePanel = null; }
        }
#endif
    }
}

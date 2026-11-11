using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: los globos de la estrella (la pregunta y la despedida) dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+; lo que les llega de archivos sin traducir (RoundedPath, CtrlDot, CtrlTxt) pasa por el puente (Plataforma/Puente.cs y Puente.asistente.cs)

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
        // LA DESPEDIDA (15-10-2026: el usuario amplia de 15 a 45 segundos el tiempo sin uso).
        // Con 45 s sin que nadie toque nada, la estrella se despide (gesto y «¡Adios!» en su globo) y a los 2,2 s
        // se cierra. Si alguien toca algo mientras se despide, se queda. 0 = no se esta despidiendo.
        internal const int AdiosTrasMs = 45000, AdiosMs = 2200;
        private int asistenteAdiosTick;
        private bool AsistenteDespidiendo { get { return asistenteVisible && asistenteAdiosTick != 0; } }
        // Lo que hace el reloj de la ayuda con el tiempo quieto: 0 nada, 1 empezar a despedirse, 2 cerrar, 3 seguir (alguien
        // volvio a usarla mientras se despedia). Pura, para las pruebas.
        internal static int PasoDespedida(int ahora, int actividad, int adios)
        {
            if (adios != 0)
            {
                if (unchecked(actividad - adios) > 0) return 3;
                return unchecked(ahora - adios) >= AdiosMs ? 2 : 0;
            }
            return unchecked(ahora - actividad) >= AdiosTrasMs ? 1 : 0;
        }
        private int asistentePagina, asistenteLeccion, asistenteSeleccion;
        private int asistenteEvento;
        private AsistenteGestoGpu asistenteGesto;
        private SKBitmap asistentePanel;   // EN LINUX: un SKBitmap (la tarjeta lo sube tal cual)
        private int asistentePanelVersion, asistentePanelPagina = -1, asistentePanelSeleccion = -1, asistentePanelLeccion = -1;
        private Size asistentePanelTam;
        private bool AsistenteVisible { get { return asistenteVisible || AsistenteRegresando; } }

        // EL REGRESO ANIMADO, CONTADO SOLO POR EL FORMULARIO (revision de raiz del 12-10-2026: "despues de un rato ya no se
        // puede usar el asistente"). Mientras la estrella vuelve a su sitio (0,72 s) un boton no activa lo de debajo. Antes
        // ese "regresando" se le preguntaba a la tarjeta grafica, que lo sabe por su ultimo cuadro; si los cuadros se paraban
        // (video, ventana minimizada, dispositivo perdido) la respuesta se quedaba vieja. Ahora no se le pregunta nada: son
        // RegresoTopeMs desde que se guardo y punto. La tarjeta anima a su ritmo; el formulario no la espera nunca.
        internal const int RegresoTopeMs = 800;
        private bool asistenteRegresando; private int asistenteCierreTick;
        private bool AsistenteRegresando
        {
            get
            {
                if (!asistenteRegresando) return false;
                if (RegresoVigente(asistenteCierreTick, Environment.TickCount)) return true;
                asistenteRegresando = false;
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
            try { return PantallaRocola.Delante == this; }   // EN LINUX: GetForegroundWindow() == Handle: la de delante es la rocola
            catch { sinUser32 = true; return false; }   // sin Windows (pruebas en Linux): solo cuenta el foco
        }
        // ---------------- EL ROJO Y EL ABAJO, MANTENIDOS ----------------
        // ROJO + ABAJO necesita saber si el otro boton sigue apretado. Antes se miraba solo GetKeyState, que es el estado
        // "segun los mensajes que ya leyo este hilo". Ahora vale tambien el estado fisico del boton (GetAsyncKeyState), que
        // no depende de quien se quedo con el mensaje. Y el orden da igual: ABAJO con el rojo apretado, o ROJO con el
        // abajo apretado (al apretar los dos a la vez, cualquiera puede llegar primero; si llegaba primero el abajo, antes
        // no pasaba nada y la ayuda parecia muerta).
        // EN LINUX: el estado fisico de los botones lo da Linux en el momento (EVIOCGKEY de cada teclado: TeclasLinux.Fisica)
        private static short GetAsyncKeyState(int vKey) { return TeclasLinux.Fisica(vKey) ? unchecked((short)0x8000) : (short)0; }   // EN LINUX: user32 GetAsyncKeyState: el bit alto si la tecla esta abajo ahora (el estado fisico, no lo ya atendido)
        private static bool sinAsync;
        private static bool Fisica(Tecla k)   // EN LINUX: Keys -> Tecla, los mismos numeros
        {
            if (sinAsync || k == Tecla.None) return false;   // EN LINUX: Keys -> Tecla, los mismos numeros
            try { return (GetAsyncKeyState((int)(k & Tecla.KeyCode)) & 0x8000) != 0; }   // EN LINUX: Keys -> Tecla, los mismos numeros
            catch { sinAsync = true; return false; }   // sin Windows (pruebas en Linux)
        }
        private static bool SegunCola(Tecla k)   // EN LINUX: Keys -> Tecla, los mismos numeros
        {
            if (sinUser32 || k == Tecla.None) return false;   // EN LINUX: Keys -> Tecla, los mismos numeros
            try { return KeyHeld(k); } catch { sinUser32 = true; return false; }
        }
        private bool RojoMantenido()
        {
            bool escLibre = !IsConfiguredKey(Tecla.Escape);   // EN LINUX: Keys -> Tecla, los mismos numeros
            return SegunCola(keyBack) || Fisica(keyBack) || (escLibre && (SegunCola(Tecla.Escape) || Fisica(Tecla.Escape)));   // EN LINUX: Keys -> Tecla, los mismos numeros
        }
        private bool AbajoMantenido()
        {
            bool flechaLibre = !IsConfiguredKey(Tecla.Down);   // EN LINUX: Keys -> Tecla, los mismos numeros
            return SegunCola(keyDown) || Fisica(keyDown) || (flechaLibre && (SegunCola(Tecla.Down) || Fisica(Tecla.Down)));   // EN LINUX: Keys -> Tecla, los mismos numeros
        }
        // Pura: ¿esta tecla, con lo que hay mantenido, es ROJO + ABAJO? Una vez por pulsacion (sin autorrepeticion).
        internal static bool EsAtajoAyuda(bool esAbajo, bool rojoMantenido, bool esRojo, bool abajoMantenido, bool repeticion)
        { return !repeticion && ((esAbajo && rojoMantenido) || (esRojo && abajoMantenido)); }

        // La ayuda solo se queda con los botones mientras PREGUNTA (el menu) y durante su regreso animado.
        // Guiando, cada boton hace lo de siempre: es el cliente quien lo hace, la estrella solo senala.
        private bool AsistenteCapturaEntrada
        { get { return (asistenteVisible && asistenteMision == 0) || (!asistenteVisible && AsistenteVisible); } }


        // QUE IMPIDE LA AYUDA, Y SOLO ESO. null = se puede. Antes tambien la negaban el foco del teclado (si una tecla llego
        // aqui, la rocola la tiene), leer la biblioteca (la ayuda no toca la biblioteca) y verde = rojo (era del acorde
        // viejo): tres maneras de quedarse sin ayuda sin que nada lo dijera.
        private string MotivoSinAyuda()
        {
            if (!gpuEnabled || gpuPanel == null) return "sin tarjeta grafica";
            if (startupClosing || IsDisposed) return "cerrando";
            if (arranqueEnEspera) return "arrancando";
            if (configMode) return "configurando botones";
            if (maintMode) return "mantenimiento (F10)";
            if (!Enabled) return "hay una ventana de ajustes abierta";
            return null;
        }
        private bool AsistentePermitido() { return MotivoSinAyuda() == null; }

        private void AbreAsistente()
        {
            if (asistenteVisible) return;
            asistenteVisible = true; asistenteRegresando = false;   // si aun regresaba, la tarjeta da media vuelta desde donde iba
            asistentePagina = asistenteLeccion = asistenteSeleccion = 0;
            asistenteMision = 0; asistenteLogroTick = 0; asistenteAdiosTick = 0;
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
            if (asistenteAdiosTick == 0) AnimaAsistente(AsistenteGestoGpu.Despedir);   // si ya se despidio, no otra vez
            asistenteAdiosTick = 0;
            asistenteVisible = false; asistenteMision = 0; asistenteLogroTick = 0;
            asistenteRegresando = true; asistenteCierreTick = Environment.TickCount;
            lastInput = DateTime.Now; searchLastActivity = DateTime.Now;
            asistenteRestaurarVideo = true;
            if (video != null) video.Visible = videoOn && !searchOpen && !AsistenteVisible;
            if (!startupClosing && !IsDisposed) { SyncGpuFullScreen(); KeepVideoOnTop(); }
        }

        // ROJO + ABAJO y la ayuda no se puede abrir: SE DICE EN LA PANTALLA (y se apunta en rocola_error.txt, con tope). Nunca
        // mas un atajo que no hace nada sin explicar por que.
        private int ayudaNoPermitidaApuntes, ayudaNoPermitidaTick;
        private void AvisaSinAyuda(string motivo)
        {
            if (motivo == null) return;
            int t = Environment.TickCount;
            if (ayudaNoPermitidaTick == 0 || unchecked(t - ayudaNoPermitidaTick) > 1500)
            {
                ayudaNoPermitidaTick = t == 0 ? 1 : t;
                try { ShowToast("La ayuda no esta disponible ahora: " + motivo, Color.FromArgb(200, 130, 35)); } catch { }
            }
            if (ayudaNoPermitidaApuntes >= 20) return;
            ayudaNoPermitidaApuntes++;
            LogError("ROJO + ABAJO sin ayuda: " + motivo, null);
        }

        private bool AsistenteEntrada(Tecla keyData, ref PulsoTecla msg)   // EN LINUX: (Keys, Message) -> (Tecla, PulsoTecla)
        {
            Tecla kc = keyData & Tecla.KeyCode;   // EN LINUX: Keys -> Tecla, los mismos numeros
            // Mantenimiento/salida siempre accesibles.
            if ((keyData & (Tecla.Control | Tecla.Alt)) != 0 || kc == Tecla.F5 || kc == Tecla.F12   // EN LINUX: Keys -> Tecla, los mismos numeros
                || ((kc == Tecla.F8 || kc == Tecla.F10) && !IsConfiguredKey(kc)))   // EN LINUX: Keys -> Tecla, los mismos numeros
            { if (asistenteVisible) CierraAsistente(); return false; }
            bool repeticion = msg.Repetida;   // EN LINUX: (msg.LParam & 0x40000000) != 0 -> msg.Repetida, el bit 30 de WM_KEYDOWN
            bool esAbajo = IsDown(kc), esRojo = IsBack(kc);
            bool atajo = (esAbajo || esRojo) && EsAtajoAyuda(esAbajo, esAbajo && RojoMantenido(), esRojo, esRojo && AbajoMantenido(), repeticion);
            string motivo = MotivoSinAyuda();
            if (motivo != null)
            {
                if (asistenteVisible) CierraAsistente();
                if (atajo) { if (disparoAyuda.Pide()) AvisaSinAyuda(motivo); return true; }
                return false;
            }
            if (asistenteVisible) asistenteActividadTick = Environment.TickCount;
            if (atajo)
            {
                // Tambien mientras la estrella regresa: da media vuelta desde donde iba. Abrir tiene tope (TopeAperturas,
                // Form1.cs): cerrar no. Y una vez por pulsacion (UnDisparo.cs): con los dos botones apretados no vuelve a
                // abrir ni a cerrar, aunque el teclado no marque sus repeticiones; hay que soltar y apretar otra vez.
                if (!disparoAyuda.Pide()) return true;
                if (asistenteVisible) CierraAsistente();
                else if (topeAyuda.Pide(Environment.TickCount)) AbreAsistente();
                else AvisaTopeCombo();
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
            int ahora = Environment.TickCount;
            switch (PasoDespedida(ahora, asistenteActividadTick, asistenteAdiosTick))
            {
                case 1: asistenteAdiosTick = ahora == 0 ? 1 : ahora; AnimaAsistente(AsistenteGestoGpu.Despedir); break;   // 45 s quieta: se despide
                case 2: CierraAsistente(); return;                                                                      // y se va
                case 3: asistenteAdiosTick = 0; AnimaAsistente(AsistenteGestoGpu.Saludar); break;                       // alguien volvio: se queda
            }
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
            if (asistenteVisible && asistenteMision != 0 && asistenteAdiosTick == 0)
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
            bool adios = asistenteAdiosTick != 0;
            bool guia = asistenteMision != 0 && !adios;   // despidiendose: el globo de siempre, con el adios
            int w = ClientSize.Width, h = ClientSize.Height;
            Size tam = guia ? GpuPanel.AsistenteGuiaTam(w, h) : GpuPanel.AsistentePanelRect(w, h).Size;
            int pagina = adios ? PaginaAdios : guia ? 16 + asistenteMision : 0;
            int leccion = guia ? ClaveGuia() : 0;
            if (asistentePanel != null && asistentePanelTam == tam && asistentePanelPagina == pagina
                && asistentePanelSeleccion == asistenteSeleccion && asistentePanelLeccion == leccion) return;
            SKBitmap nuevo = adios ? PintaAdiosAsistente(tam.Width, tam.Height)   // EN LINUX: un SKBitmap
                : guia ? PintaGuiaActual(tam.Width, tam.Height)
                : PintaPanelAsistente(tam.Width, tam.Height, 0, 0, asistenteSeleccion);
            SKBitmap anterior = asistentePanel; asistentePanel = nuevo; asistentePanelVersion++;   // EN LINUX: un SKBitmap
            asistentePanelTam = tam; asistentePanelPagina = pagina; asistentePanelSeleccion = asistenteSeleccion; asistentePanelLeccion = leccion;
            asistentePagina = pagina; asistenteLeccion = leccion;
            if (anterior != null) anterior.Dispose();
        }

        internal static Rectangle AsistenteOpcionRect(int w, int h, int i)
        {
            int x = AsistenteGloboMargen(w), derecha = Math.Max(12, w / 20);
            return new Rectangle(x, (int)(h * (.50f + i * .135f)), Math.Max(1, w - x - derecha), Math.Max(22, (int)(h * .115f)));
        }

        // LOS CONTROLES DIBUJADOS EN LA OPCION ELEGIDA (peticion del 12-10-2026: "pon los controles en su pantalla,
        // graficos, para que el cliente lo entienda mas facil; en cada casilla que el cliente se mueva los controles se
        // moveran con cada seleccion"). Dentro de la opcion elegida, a la derecha: las dos flechas de subir y bajar (rojas,
        // como en la cruceta; la que no lleva a ningun lado, apagada) y el boton VERDE de elegir. Las otras opciones no
        // llevan nada: los controles van con la seleccion. Coordenadas del globo, como AsistenteOpcionRect.
        internal static void AsistenteControlesOpcion(Rectangle r, out Rectangle arriba, out Rectangle abajo, out Rectangle verde)
        {
            int d = Math.Max(12, (int)(r.Height * .62f)), k = Math.Max(9, (int)(r.Height * .40f));
            int cy = r.Top + r.Height / 2;
            verde = new Rectangle(r.Right - 10 - d, cy - d / 2, d, d);
            int kx = verde.Left - 8 - k;
            arriba = new Rectangle(kx, cy - k - 1, k, k);
            abajo = new Rectangle(kx, cy + 1, k, k);
        }
        // Lo que ocupan esos controles al final de la opcion (con su aire): el nombre de la opcion acaba antes.
        internal static int AsistenteAnchoControles(Rectangle r)
        {
            Rectangle a, b, v; AsistenteControlesOpcion(r, out a, out b, out v);
            return r.Right - a.Left + 8;
        }

        internal static int AsistenteGloboMargen(int w)
        { return Math.Min(Math.Max(0, w - 1), Math.Max(12, w / 20) + Math.Min(32, Math.Max(18, w / 18))); }

        // Contorno unico: la cola del globo apunta a la estrella y no deja una
        // costura entre un rectangulo y un triangulo. Solo se compone al cambiar texto.
        internal static SKPath AsistenteGloboCamino(int w, int h)   // EN LINUX: un SKPath, hecho como lo hace un GraphicsPath de GDI+ (cada arco o linea se une con una linea al punto anterior de la figura)
        {
            SKPath p = new SKPath { FillType = SKPathFillType.EvenOdd };   // EN LINUX: new GraphicsPath(): su relleno de fabrica (Alternate) es par-impar
            if (w < 64 || h < 64) { p.AddRect(new SKRect(0, 0, Math.Max(1, w), Math.Max(1, h))); return p; }   // EN LINUX: p.AddRectangle(RectangleF(0, 0, ancho, alto)): la caja de (0, 0) a (ancho, alto)
            float x = Math.Min(32, Math.Max(18, w / 18)), y = 2, derecha = w - 3, abajo = h - 3;
            float r = Math.Min(36, Math.Min((derecha - x) * .18f, h * .10f));
            float cola = h * .46f, baseCola = Math.Min(30, h * .055f);
            p.ArcTo(new SKRect(x, y, x + r * 2, y + r * 2), 180, 90, true);   // EN LINUX: p.AddArc(x, y, r * 2, r * 2, 180, 90): en la caja (x, y, x + ancho, y + alto); el primero de la figura empieza en su punto (forceMoveTo)
            p.ArcTo(new SKRect(derecha - r * 2, y, derecha - r * 2 + r * 2, y + r * 2), 270, 90, false);   // EN LINUX: p.AddArc: con la figura abierta, una linea desde el punto anterior hasta el arco (forceMoveTo falso), como GDI+
            p.ArcTo(new SKRect(derecha - r * 2, abajo - r * 2, derecha - r * 2 + r * 2, abajo - r * 2 + r * 2), 0, 90, false);   // EN LINUX: p.AddArc
            p.ArcTo(new SKRect(x, abajo - r * 2, x + r * 2, abajo - r * 2 + r * 2), 90, 90, false);   // EN LINUX: p.AddArc
            p.LineTo(x, abajo - r); p.LineTo(x, cola + baseCola);   // EN LINUX: p.AddLine(x1, y1, x2, y2): con la figura abierta, una linea hasta el primer punto y otra hasta el segundo, como GDI+
            p.LineTo(x, cola + baseCola); p.CubicTo(x - 2, cola + baseCola * .3f, 4, cola + 4, 2, cola);   // EN LINUX: p.AddBezier: una linea hasta su primer punto (aqui, el mismo) y la curva
            p.LineTo(2, cola); p.CubicTo(6, cola - 2, x - 3, cola - baseCola * .8f, x, cola - baseCola);   // EN LINUX: p.AddBezier
            p.LineTo(x, cola - baseCola); p.LineTo(x, y + r); p.Close(); return p;   // EN LINUX: p.AddLine y p.CloseFigure()
        }

        // El globo de la despedida: el mismo globo, con «¡Adios!» y como volver a llamarla.
        internal const int PaginaAdios = 99;
        internal static SKBitmap PintaAdiosAsistente(int w, int h)   // EN LINUX: un SKBitmap
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            SKBitmap bmp = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage(bmp): un lienzo nuevo (el estado del dibujo, abajo, en variables; el medio pixel y la copia, los de fabrica)
            using (Rocola.Nativo.Letra titulo = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(22, Math.Min(56, h * .14f)), true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel): la familia como la resuelve GDI+, en pixeles y en negrita (Nativo/Letras.cs)
            using (Rocola.Nativo.Letra cuerpo = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(13, Math.Min(24, h * .05f))))   // EN LINUX: new Font("Trebuchet MS", px, Regular, Pixel)
            using (SKPaint blanco = new SKPaint { Color = Color.FromArgb(246, 248, 255).Sk() })   // EN LINUX: SolidBrush: el pincel de un color en un SKPaint
            using (SKPaint tenue = new SKPaint { Color = Color.FromArgb(185, 206, 234).Sk() })   // EN LINUX: SolidBrush
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto centro = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                g.Clear(Color.Transparent.Sk()); bool suave = true;   // EN LINUX: Graphics.Clear; g.SmoothingMode = AntiAlias: el estado del dibujo en una variable
                AjusteLetra ajuste = AjusteLetra.Suave;   // EN LINUX: g.TextRenderingHint = AntiAlias
                using (SKPath borde = AsistenteGloboCamino(w, h))   // EN LINUX: un SKPath
                using (SKShader degradado = DegradadoV(new Rectangle(0, 0, w, h), Color.FromArgb(252, 24, 42, 74), Color.FromArgb(248, 10, 18, 37), false)) using (SKPaint vidrio = new SKPaint { Shader = degradado })   // EN LINUX: DegradadoV (PulidoGpu.cs) da el shader del pincel, con el medio pixel de este lienzo (el de fabrica), en un SKPaint; se suelta con el
                using (SKPaint halo = new SKPaint { Color = Color.FromArgb(35, 101, 206, 255).Sk(), StrokeWidth = 5f })   // EN LINUX: Pen(color, 5): la pluma en un SKPaint (Pixel le pone el minimo de un pixel y las esquinas de GDI+)
                using (SKPaint brillo = new SKPaint { Color = Color.FromArgb(220, 142, 215, 255).Sk(), StrokeWidth = 1.8f })   // EN LINUX: Pen(color, 1,8)
                { Pixel.Traza(g, borde, halo, suave); Pixel.Rellena(g, borde, vidrio, suave); Pixel.Traza(g, borde, brillo, suave); }   // EN LINUX: g.DrawPath(halo, borde); g.FillPath(vidrio, borde); g.DrawPath(brillo, borde) (Nativo/Pixel.cs)
                int x = AsistenteGloboMargen(w), tw = Math.Max(1, w - x - Math.Max(12, w / 20));
                centro.Horizontal = Alinea.Cerca; centro.Vertical = Alinea.Centro;   // EN LINUX: Alignment = Near; LineAlignment = Center
                TextoGdiPlus.Escribe(g, "¡Adiós!", titulo, ajuste, blanco, new RectangleF(x, h * .18f, tw, h * .30f), centro);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                TextoGdiPlus.Escribe(g, "Aquí estaré cuando me necesites: ROJO + ABAJO, o la casilla AYUDA.",   // EN LINUX: g.DrawString sin formato
                    cuerpo, ajuste, tenue, new RectangleF(x, h * .50f, tw, h * .34f), null);   // EN LINUX: (null: el formato de fabrica)
            }
            return bmp;
        }

        internal static SKBitmap PintaPanelAsistente(int w, int h, int pagina, int leccion, int seleccion)   // EN LINUX: un SKBitmap
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            SKBitmap bmp = Mapas.Nuevo(w, h);   // EN LINUX: new Bitmap(w, h, Format32bppPArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            using (SKCanvas g = new SKCanvas(bmp))   // EN LINUX: Graphics.FromImage(bmp): un lienzo nuevo (el estado del dibujo, abajo, en variables)
            using (Rocola.Nativo.Letra pequeno = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(10, Math.Min(18, h * .034f)), true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel): la familia como la resuelve GDI+ (Nativo/Letras.cs)
            using (Rocola.Nativo.Letra titulo = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(20, Math.Min(42, h * .085f)), true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel)
            using (Rocola.Nativo.Letra cuerpo = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(12, Math.Min(21, h * .043f))))   // EN LINUX: new Font("Trebuchet MS", px, Regular, Pixel)
            using (Rocola.Nativo.Letra opcion = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), Math.Max(14, Math.Min(27, h * .052f)), true))   // EN LINUX: new Font("Trebuchet MS", px, Bold, Pixel)
            using (SKPaint blanco = new SKPaint { Color = Color.FromArgb(246, 248, 255).Sk() })   // EN LINUX: SolidBrush: el pincel de un color en un SKPaint
            using (SKPaint tenue = new SKPaint { Color = Color.FromArgb(185, 206, 234).Sk() })   // EN LINUX: SolidBrush
            using (SKPaint azul = new SKPaint { Color = Color.FromArgb(150, 215, 255).Sk() })   // EN LINUX: SolidBrush
            {   // EN LINUX: (el StringFormat es un FormatoTexto, que no se suelta: va dentro)
                FormatoTexto formato = new FormatoTexto();   // EN LINUX: new StringFormat() (Nativo/TextoGdiPlus.cs)
                g.Clear(Color.Transparent.Sk()); bool suave = true;   // EN LINUX: Graphics.Clear; g.SmoothingMode = AntiAlias: el estado del dibujo en variables
                AjusteLetra ajuste = AjusteLetra.Suave; bool copia = false, medio = false;   // EN LINUX: g.TextRenderingHint = AntiAlias; la copia y el medio pixel, los de fabrica de un Graphics nuevo (XP no los toca)
                using (SKPath borde = AsistenteGloboCamino(w, h))   // EN LINUX: un SKPath
                using (SKShader degradado = DegradadoV(new Rectangle(0, 0, w, h), Color.FromArgb(252, 24, 42, 74), Color.FromArgb(248, 10, 18, 37), medio)) using (SKPaint vidrio = new SKPaint { Shader = degradado })   // EN LINUX: DegradadoV (PulidoGpu.cs) da el shader del pincel, con el medio pixel de este lienzo, en un SKPaint; se suelta con el
                using (SKPaint halo = new SKPaint { Color = Color.FromArgb(35, 101, 206, 255).Sk(), StrokeWidth = 5f })   // EN LINUX: Pen(color, 5): la pluma en un SKPaint
                using (SKPaint brillo = new SKPaint { Color = Color.FromArgb(220, 142, 215, 255).Sk(), StrokeWidth = 1.8f })   // EN LINUX: Pen(color, 1,8)
                { Pixel.Traza(g, borde, halo, suave, medio, copia); Pixel.Rellena(g, borde, vidrio, suave, medio, copia); Pixel.Traza(g, borde, brillo, suave, medio, copia); }   // EN LINUX: g.DrawPath(halo, borde); g.FillPath(vidrio, borde); g.DrawPath(brillo, borde) (Nativo/Pixel.cs)
                int x = AsistenteGloboMargen(w), tw = Math.Max(1, w - x - Math.Max(12, w / 20));
                TextoGdiPlus.Escribe(g, "¡HOLA! TE ACOMPAÑO PASO A PASO", pequeno, ajuste, azul, new RectangleF(x, h * .045f, tw, h * .06f), null, copia, medio);   // EN LINUX: g.DrawString sin formato (null: el de fabrica) (Nativo/TextoGdiPlus.cs)
                TextoGdiPlus.Escribe(g, "¿Qué quieres hacer?", titulo, ajuste, blanco, new RectangleF(x, h * .12f, tw, h * .17f), null, copia, medio);   // EN LINUX: g.DrawString
                TextoGdiPlus.Escribe(g, "Elige y yo te voy señalando qué botón tocar. Tú lo haces y la música sigue sonando.",   // EN LINUX: g.DrawString
                    cuerpo, ajuste, tenue, new RectangleF(x, h * .30f, tw, h * .19f), null, copia, medio);   // EN LINUX: (la letra, el modo de letra, el pincel, la caja, sin formato)
                formato.Vertical = Alinea.Centro; formato.Banderas = BanderasTexto.SinPartir;   // EN LINUX: LineAlignment = Center; FormatFlags = NoWrap (con =: deja solo esa)
                string[] nombres = { "Poner una canción", "Buscar una canción por su nombre", "Aprender a moverme" };
                Color rojo = Color.FromArgb(228, 72, 72), verdeC = Color.FromArgb(70, 200, 105), apagado = Color.FromArgb(104, 112, 134);
                // Todas las opciones con la misma letra, la que deje caber el nombre mas largo junto a los controles.
                Rectangle r0 = AsistenteOpcionRect(w, h, 0);
                float libreTxt = Math.Max(1, r0.Width - 24 - AsistenteAnchoControles(r0));
                Rocola.Nativo.Letra fOpc = opcion, propia = null;   // EN LINUX: Letra
                for (int paso = 0; paso < 8; paso++)
                {
                    float mas = 0; for (int i = 0; i < nombres.Length; i++) mas = Math.Max(mas, TextoGdiPlus.Mide(nombres[i], fOpc, ajuste, new SizeF(0, 0), formato).Width);   // EN LINUX: g.MeasureString(s, f, PointF.Empty, formato): sin caja (0, 0), con ese formato (Nativo/TextoGdiPlus.cs)
                    if (mas <= libreTxt || fOpc.Tamano <= opcion.Tamano * .70f) break;   // EN LINUX: Font.Size es el Tamano de la Letra (en pixeles)
                    if (propia != null) propia.Dispose();
                    propia = Rocola.Nativo.Letra.EnPixeles(opcion.Familia, Math.Max(opcion.Tamano * .70f, fOpc.Tamano * Math.Max(.70f, libreTxt / mas * .98f)), opcion.Negrita, opcion.Cursiva, opcion.Subrayada);   // EN LINUX: new Font(opcion.FontFamily, px, opcion.Style, Pixel): la misma familia y el mismo estilo, en pixeles
                    fOpc = propia;
                }
                try
                {
                    for (int i = 0; i < AsistenteOpciones; i++)
                    {
                        Rectangle r = AsistenteOpcionRect(w, h, i);
                        bool elegida = i == seleccion;
                        using (Rocola.Dibujo2D.GraphicsPath camino = RoundedPath(r, Math.Min(14, r.Height / 3)))   // EN LINUX: RoundedPath (Form1.Fondo.cs, aun sin traducir) devuelve un GraphicsPath: se pinta su SKPath (PuenteDibujo.Ruta: el mismo, sin copia) y se suelta como en XP
                        using (SKPaint relleno = new SKPaint { Color = (elegida ? Color.FromArgb(235, 42, 87, 130) : Color.FromArgb(150, 28, 42, 68)).Sk() })   // EN LINUX: SolidBrush
                        using (SKPaint contorno = new SKPaint { Color = (elegida ? Color.FromArgb(150, 235, 188) : Color.FromArgb(90, 113, 150)).Sk(), StrokeWidth = elegida ? 2f : 1f })   // EN LINUX: Pen(color, ancho)
                        { Pixel.Rellena(g, PuenteDibujo.Ruta(camino), relleno, suave, medio, copia); Pixel.Traza(g, PuenteDibujo.Ruta(camino), contorno, suave, medio, copia); }   // EN LINUX: g.FillPath(relleno, camino); g.DrawPath(contorno, camino) (Nativo/Pixel.cs)
                        TextoGdiPlus.Escribe(g, nombres[i], fOpc, ajuste, blanco, new RectangleF(r.Left + 12, r.Top, Math.Max(1, r.Width - 24 - (elegida ? AsistenteAnchoControles(r) : 0)), r.Height), formato, copia, medio);   // EN LINUX: g.DrawString (Nativo/TextoGdiPlus.cs)
                        if (!elegida) continue;
                        Rectangle ka, kb, kv; AsistenteControlesOpcion(r, out ka, out kb, out kv);
                        TeclaFlecha(g, suave, ajuste, copia, medio, ka.X, ka.Y + ka.Height / 2, ka.Width, i > 0 ? rojo : apagado, 0);   // EN LINUX: el lienzo y su estado
                        TeclaFlecha(g, suave, ajuste, copia, medio, kb.X, kb.Y + kb.Height / 2, kb.Width, i < AsistenteOpciones - 1 ? rojo : apagado, 1);   // EN LINUX: el lienzo y su estado
                        CtrlDot(g, suave, ajuste, copia, medio, kv.X, kv.Y + kv.Height / 2, kv.Width, verdeC);   // EN LINUX: CtrlDot (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado
                    }
                }
                finally { if (propia != null) propia.Dispose(); }
                // Flechas rojas ARRIBA/ABAJO: mover. VERDE: elegir. ROJO: salir.
                int ld = Math.Max(12, Math.Min(26, (int)(h * .046f))), lcy = (int)(h * .955f);
                float lx = x;
                Color azulC = Color.FromArgb(150, 215, 255);
                TeclaFlecha(g, suave, ajuste, copia, medio, lx, lcy, ld, rojo, 0); lx += ld + 3;   // EN LINUX: el lienzo y su estado
                TeclaFlecha(g, suave, ajuste, copia, medio, lx, lcy, ld, rojo, 1); lx += ld + 6;   // EN LINUX: el lienzo y su estado
                lx = CtrlTxt(g, suave, ajuste, copia, medio, pequeno, "mover", lx, lcy, azulC) + ld;   // EN LINUX: CtrlTxt (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado y la Letra
                lx = CtrlDot(g, suave, ajuste, copia, medio, lx, lcy, ld, verdeC) + 6;   // EN LINUX: CtrlDot (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado
                lx = CtrlTxt(g, suave, ajuste, copia, medio, pequeno, "elegir", lx, lcy, azulC) + ld;   // EN LINUX: CtrlTxt (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado y la Letra
                lx = CtrlDot(g, suave, ajuste, copia, medio, lx, lcy, ld, rojo) + 6;   // EN LINUX: CtrlDot (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado
                CtrlTxt(g, suave, ajuste, copia, medio, pequeno, "salir", lx, lcy, azulC);   // EN LINUX: CtrlTxt (Form1.Pared.cs, ya traducido), directo: el lienzo y su estado y la Letra
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

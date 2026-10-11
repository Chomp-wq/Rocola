#if ROCOLA_GPU
using System;
using System.Drawing;

namespace Rocola
{
    // El formulario conserva la propiedad del bitmap. Version cambia al reemplazar
    // el mensaje/seleccion, nunca por el salto ni por los gestos del personaje.
    internal struct AsistenteEstadoGpu
    {
        public bool Visible, Animada;
        public Bitmap Panel;
        public int Version;
        public AsistenteGestoGpu Gesto;
        public int Evento, Pagina, Leccion, Seleccion;
        // Guiando (AsistenteGuiaRocola.cs): globo pequeno apartado, y lo que hay que senalar en pantalla.
        public bool Guia, Logro;
        public RectangleF Objetivo;
    }

    internal struct AsistentePoseGpu
    {
        public PointF Centro;
        public float Radio, Giro, Miembros, Alfa;
        public double Yaw, Pitch;
    }

    internal sealed partial class GpuPanel
    {
        public Func<AsistenteEstadoGpu> GetAsistenteState;
        private AsistenteEstadoGpu asistenteEstado;
        private AsistentePoseGpu asistentePose, asistenteDesde;
        private bool asistenteAnteriorVisible, asistenteDibujando;
        private double asistenteInicio;
        private int asistenteOrigenW, asistenteOrigenH;
        private float asistenteFrente;
        private IntPtr asistenteTex;
        private Bitmap asistenteBmp; // referencia prestada; no se libera aqui
        private int asistenteVer = -1, asistenteW, asistenteH;
        private int asistenteTextoClave = -1;
        private double asistenteTextoInicio;
        private float asistenteU, asistenteV;
        private readonly D3D9.Vtx[] asistenteQuad = new D3D9.Vtx[6];
        private readonly D3D9.Vtx[] asistenteVelo = new D3D9.Vtx[6];
        // Dos lotes de miembros alrededor del cuerpo, < 72 KiB en total.
        // No hay cara ni geometria facial, ni reservas por cuadro.
        private readonly D3D9.Vtx[] asistenteMiembros = new D3D9.Vtx[1900];
        private readonly D3D9.Vtx[] asistenteBrazos = new D3D9.Vtx[700];
        private static readonly float[] asistenteCirculoX = AsistenteCirculo(true);
        private static readonly float[] asistenteCirculoY = AsistenteCirculo(false);
        internal const double AsistenteSaltoSegundos = .72;

        internal static Rectangle AsistentePanelRect(int w, int h)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            int margen = Math.Min(20, Math.Max(2, w / 40));
            int x = Math.Min(w - 1, Math.Max(margen, (int)(w * .42f)));
            int y = Math.Min(h - 1, Math.Max(8, (int)(h * .12f)));
            // La ayuda devuelve el estado a su barra normal. Reservar ESA franja,
            // no el estado ampliado anterior, para que dos opciones y el texto
            // sean legibles tambien a 640x480. El tope evita tarjetas gigantes.
            int reserva = Math.Min(132, Math.Max(48, (int)(h * .11f)));
            int alto = Math.Max(1, Math.Min(800, Math.Min((int)(h * .65f), h - y - reserva)));
            return new Rectangle(x, y, Math.Min(1120, Math.Max(1, w - x - margen)), alto);
        }

        // EL GLOBO PEQUENO DE LA GUIA. Mismo tamano siempre (el formulario lo hornea una vez por paso) y se
        // coloca en la esquina contraria a lo que hay que mirar, para no taparlo. La estrella va a su izquierda.
        internal static Size AsistenteGuiaTam(int w, int h)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            int gw = Math.Max(260, Math.Min(660, (int)(w * .40f))), gh = Math.Max(124, Math.Min(250, (int)(h * .22f)));
            return new Size(Math.Max(1, Math.Min(gw, w - 16)), Math.Max(1, Math.Min(gh, h - 16)));
        }
        internal static float AsistenteGuiaRadio(int alto) { return Math.Max(8f, alto * .34f); }
        // Las cuatro esquinas donde puede ir el globo (las mismas para elegir y para quedarse).
        private static void EsquinasGuia(int w, int h, Size s, float radio, out int xIzq, out int xDer, out int yArr, out int yAba)
        {
            int margen = Math.Min(16, Math.Max(4, w / 60));
            xIzq = Math.Min(Math.Max(0, w - s.Width), margen + (int)(radio * 2.9f)); xDer = Math.Max(0, w - margen - s.Width);
            int reserva = Math.Min(132, Math.Max(48, (int)(h * .11f)));
            yArr = Math.Min(Math.Max(0, h - s.Height), Math.Max(8, (int)(h * .13f))); yAba = Math.Max(0, h - reserva - s.Height - 10);
        }
        // Lo que tapa del objetivo el globo puesto en 'r' junto con la estrella (que va a su izquierda). 0 = nada.
        private static float TapaGuia(Rectangle r, float radio, RectangleF objetivo)
        {
            if (!(objetivo.Width > 0) || !(objetivo.Height > 0)) return 0f;
            RectangleF ocupa = new RectangleF(r.Left - radio * 2.9f, r.Top - radio * .2f, r.Width + radio * 2.9f, r.Height + radio * .4f);
            RectangleF cruce = RectangleF.Intersect(ocupa, RectangleF.Inflate(objetivo, 10, 10));
            return cruce.Width > 0 && cruce.Height > 0 ? cruce.Width * cruce.Height : 0f;
        }
        // CON MEMORIA: si el globo ya esta en una esquina que no tapa lo que hay que mirar, se queda. Asi no cruza
        // la pantalla cada vez que el disco elegido pasa del centro mientras el cliente se mueve; solo se muda
        // cuando taparia (o si la pantalla cambio de tamano).
        internal static Rectangle AsistenteGuiaRectSigue(int w, int h, RectangleF objetivo, Rectangle actual)
        {
            Rectangle nueva = AsistenteGuiaRect(w, h, objetivo);
            if (actual.Size != nueva.Size) return nueva;
            int xIzq, xDer, yArr, yAba;
            EsquinasGuia(Math.Max(1, w), Math.Max(1, h), nueva.Size, AsistenteGuiaRadio(nueva.Height), out xIzq, out xDer, out yArr, out yAba);
            bool esquina = (actual.X == xIzq || actual.X == xDer) && (actual.Y == yArr || actual.Y == yAba);
            if (!esquina || TapaGuia(actual, AsistenteGuiaRadio(actual.Height), objetivo) > 0f) return nueva;
            return actual;
        }
        internal static Rectangle AsistenteGuiaRect(int w, int h, RectangleF objetivo)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            Size s = AsistenteGuiaTam(w, h);
            float radio = AsistenteGuiaRadio(s.Height);
            int xIzq, xDer, yArr, yAba;
            EsquinasGuia(w, h, s, radio, out xIzq, out xDer, out yArr, out yAba);
            bool vacio = !(objetivo.Width > 0) || !(objetivo.Height > 0);
            float ox = vacio ? 0 : objetivo.X + objetivo.Width * .5f, oy = vacio ? 0 : objetivo.Y + objetivo.Height * .5f;
            // Primero la esquina opuesta al objetivo; luego las otras tres, por orden de cercania a esa.
            bool abajo = vacio || oy < h * .5f, derecha = vacio || ox < w * .5f;
            int[] xs = { derecha ? xDer : xIzq, derecha ? xIzq : xDer, derecha ? xDer : xIzq, derecha ? xIzq : xDer };
            int[] ys = { abajo ? yAba : yArr, abajo ? yAba : yArr, abajo ? yArr : yAba, abajo ? yArr : yAba };
            Rectangle mejor = new Rectangle(xs[0], ys[0], s.Width, s.Height); float menor = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Rectangle r = new Rectangle(xs[i], ys[i], s.Width, s.Height);
                if (vacio) return r;
                // Lo que tapa del objetivo, y la estrella (a la izquierda del globo) tampoco debe taparlo.
                float tapa = TapaGuia(r, radio, objetivo);
                if (tapa <= 0) return r;
                if (tapa < menor) { menor = tapa; mejor = r; }
            }
            return mejor;
        }

        internal static PointF AsistenteCentroFrente(int w, int h)
        {
            Rectangle r = AsistentePanelRect(w, h);
            float radio = AsistenteRadioFrente(w, h);
            // Mantenerla junto al globo tambien en 4K/ultrapanoramico: el tope
            // del tamano de la estrella no debe dejar medio metro de hueco.
            float x = r.Left - radio * 1.40f - Math.Min(24, Math.Max(8, w / 32));
            x = Math.Min(Math.Max(1, w), Math.Max(radio * 1.35f + 4, x));
            return new PointF(x, r.Y + r.Height * .46f);
        }

        internal static float AsistenteRadioFrente(int w, int h)
        {
            Rectangle r = AsistentePanelRect(w, h);
            return Math.Max(1, Math.Min(220, Math.Min(w * .115f, r.Height * .32f)));
        }

        internal static float AsistenteSuaviza(double t)
        {
            if (double.IsNaN(t)) return 0;
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            // Velocidad y aceleracion cero en ambos extremos: sin sacudida al llegar.
            return (float)(t * t * t * (t * (t * 6 - 15) + 10));
        }

        internal static PointF AsistenteTrayecto(PointF desde, PointF hasta, double t, float salto)
        {
            float p = AsistenteSuaviza(t);
            double fase = double.IsNaN(t) ? 0 : Math.Max(0, Math.Min(1, t));
            double arco = Math.Sin(Math.PI * fase);
            return new PointF(desde.X + (hasta.X - desde.X) * p,
                desde.Y + (hasta.Y - desde.Y) * p - Math.Max(0, salto) * (float)(arco * arco));
        }

        private bool AsistenteOcultaFondo
        { get { return asistenteEstado.Visible || asistenteDibujando; } }

        // Incluye la estancia en primer plano y el regreso: el formulario no
        // debe ocultar este panel ni devolver WMP al frente a mitad del salto.
        // El regreso solo lo termina un cuadro de la tarjeta (AvanzaAsistente). Si los cuadros se paran a mitad
        // (ventana minimizada, dispositivo perdido, panel oculto), no puede quedarse "regresando" para siempre: el
        // formulario se tragaria los botones, sin pistas del rojo ni forma de volver a abrir la ayuda. Pasado un
        // segundo y medio sin cuadros, el regreso deja de bloquear (la animacion sigue al volver los cuadros).
        internal bool AsistenteEnTransicion
        { get { return asistenteDibujando && !RegresoSinCuadros(asistenteEstado.Visible, asistenteUltimoAvance, AhoraMs() / 1000); } }
        internal const double RegresoSinCuadrosSeg = 1.5;
        internal static bool RegresoSinCuadros(bool visible, double ultimoAvance, double ahora)
        { return !visible && ultimoAvance >= 0 && !(ahora - ultimoAvance < RegresoSinCuadrosSeg); }

        private void PreparaAsistente()
        {
            asistenteEstado = GetAsistenteState != null ? GetAsistenteState() : new AsistenteEstadoGpu();
            if (!asistenteEstado.Visible && !asistenteDibujando)
            { SueltaPanelAsistente(); return; }
            if (!asistenteEstado.Visible || asistenteEstado.Panel == null) return;
            if (asistenteTex != IntPtr.Zero && asistenteVer == asistenteEstado.Version
                && ReferenceEquals(asistenteBmp, asistenteEstado.Panel)) return;
            asistenteTex = Upload(asistenteTex, ref asistenteW, ref asistenteH,
                asistenteEstado.Panel, out asistenteU, out asistenteV);
            if (asistenteTex != IntPtr.Zero)
            {
                asistenteVer = asistenteEstado.Version; asistenteBmp = asistenteEstado.Panel;
                int textoClave = asistenteEstado.Pagina * 65536 + asistenteEstado.Leccion;
                if (asistenteTextoClave != textoClave) {
                    asistenteTextoClave = textoClave;
                    asistenteTextoInicio = (cuadroMs >= 0 ? cuadroMs : AhoraMs()) / 1000;
                }
            }
        }

        private AsistentePoseGpu AsistentePoseFondo()
        {
            float enfasis = LunaProtagonista ? Math.Max(MoonEmphasis(SceneDim, true), idleBlend) : 0;
            float radio = DetailedMoonDiameter(Height, enfasis) * MoonSceneScale(idleBlend, LunaActiva) * .5f;
            PointF centro = LunaActiva ? StarSceneCenter()
                : DetailedMoonCenter(Width, Height, moonMotionTime, idleBlend, IdleRise, false);
            centro.X = Math.Max(radio, Math.Min(Width - radio, centro.X));
            centro.Y = Math.Max(radio, Math.Min(StatusTopY - radio, centro.Y));
            return new AsistentePoseGpu { Centro = centro, Radio = radio,
                Giro = LunaActiva ? StarRoll(moonMotionTime) : 0,
                Yaw = LunaActiva ? starYaw : 0, Pitch = StarPitch(), Alfa = ConLuna ? 1 : 0 };
        }

        private static double AsistenteGiroCorto(double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle)) return 0;
            angle %= Math.PI * 2;
            return angle > Math.PI ? angle - Math.PI * 2 : angle < -Math.PI ? angle + Math.PI * 2 : angle;
        }

        private void AvanzaAsistente()
        {
            double ahora = (cuadroMs >= 0 ? cuadroMs : AhoraMs()) / 1000;
            bool entrada = asistenteEstado.Visible && !asistenteAnteriorVisible;
            if (asistenteEstado.Visible != asistenteAnteriorVisible)
            {
                asistenteDesde = asistenteDibujando ? asistentePose : AsistentePoseFondo();
                asistenteDesde.Yaw = AsistenteGiroCorto(asistenteDesde.Yaw);
                asistenteOrigenW = Math.Max(1, Width); asistenteOrigenH = Math.Max(1, Height);
                asistenteInicio = ahora;
                asistenteAnteriorVisible = asistenteEstado.Visible;
                asistenteDibujando = true;
                ResetStarEchoes(); // la misma estrella viene al frente; no queda otra copia detras
            }
            if (!asistenteDibujando) return;
            AvanzaPersonalidad(ahora, entrada);
            double dtGuia = asistenteUltimoAvance < 0 ? 0 : Math.Max(0, Math.Min(.25, ahora - asistenteUltimoAvance));
            asistenteUltimoAvance = ahora;
            AvanzaGlobosGuia(dtGuia);
            AsistentePoseGpu hasta = asistenteEstado.Visible ? AsistentePoseFrente() : AsistentePoseFondo();
            float sx = Width / (float)Math.Max(1, asistenteOrigenW);
            float sy = Height / (float)Math.Max(1, asistenteOrigenH);
            AsistentePoseGpu desde = asistenteDesde;
            desde.Centro = new PointF(desde.Centro.X * sx, desde.Centro.Y * sy);
            desde.Radio *= Math.Min(sx, sy);
            // Al devolverla, elegir el giro equivalente mas cercano, no dar una
            // vuelta de golpe cuando el reloj musical ya paso de 2 pi.
            hasta.Yaw = desde.Yaw + AsistenteGiroCorto(hasta.Yaw - desde.Yaw);
            double t = asistenteEstado.Animada ? Math.Max(0, (ahora - asistenteInicio) / AsistenteSaltoSegundos) : 1;
            // Ya en primer plano, va SIGUIENDO su sitio (del menu al globo de la guia, y de esquina a esquina
            // cuando cambia lo que senala) en vez de saltar: amortiguado, sin sacudidas.
            if (asistenteEstado.Visible && t >= 1)
            {
                if (!asistenteSigueListo) { asistenteSigue = hasta; asistenteSigueListo = true; }
                else
                {
                    float k = asistenteEstado.Animada ? 1 - (float)Math.Exp(-dtGuia * 6) : 1;
                    asistenteSigue.Centro = new PointF(asistenteSigue.Centro.X + (hasta.Centro.X - asistenteSigue.Centro.X) * k,
                        asistenteSigue.Centro.Y + (hasta.Centro.Y - asistenteSigue.Centro.Y) * k);
                    asistenteSigue.Radio += (hasta.Radio - asistenteSigue.Radio) * k;
                }
                hasta.Centro = asistenteSigue.Centro; hasta.Radio = asistenteSigue.Radio;
            }
            else asistenteSigueListo = false;
            float p = AsistenteSuaviza(t);
            asistentePose.Centro = AsistenteTrayecto(desde.Centro, hasta.Centro, t, Height * .075f);
            // El arco nunca sale por el borde superior de la pantalla.
            asistentePose.Radio = desde.Radio + (hasta.Radio - desde.Radio) * p;
            asistentePose.Centro.Y = Math.Max(asistentePose.Radio + 4, asistentePose.Centro.Y);
            asistentePose.Giro = desde.Giro + (hasta.Giro - desde.Giro) * p;
            asistentePose.Yaw = desde.Yaw + (hasta.Yaw - desde.Yaw) * p;
            asistentePose.Pitch = desde.Pitch + (hasta.Pitch - desde.Pitch) * p;
            asistentePose.Miembros = desde.Miembros + (hasta.Miembros - desde.Miembros) * p;
            asistentePose.Alfa = desde.Alfa + (hasta.Alfa - desde.Alfa) * p;
            asistenteFrente = SafeMoonLevel(asistentePose.Miembros);
            if (asistenteEstado.Visible && t >= 1 && asistenteEstado.Animada)
            {
                asistentePose.Giro = asistenteExpresion.Inclinacion;
                asistentePose.Centro.X += asistentePose.Radio * asistenteExpresion.Desplazamiento;
                asistentePose.Centro.Y += asistentePose.Radio * asistenteExpresion.Rebote;
                asistentePose.Pitch += asistenteExpresion.Reverencia;
                asistentePose.Radio *= asistenteExpresion.Escala;
            }
            if (!asistenteEstado.Visible && t >= 1)
            {
                asistenteDibujando = false; asistenteFrente = 0;
                ResetStarEchoes(); // no reaparecen ecos viejos al terminar el regreso
            }
        }

        private void DibujaAsistente()
        {
            if (!asistenteDibujando || stars == null) return;
            float presencia = SafeMoonLevel(asistenteFrente);
            bool guia = asistenteEstado.Visible ? asistenteEstado.Guia : asistenteGuiaListo;
            if (guia) DibujaFocoGuia(presencia);   // guiando: se ve la Rocola, con foco en lo que toca usar
            else
            {
            uint velo = ((uint)(presencia * 138) << 24) | 0x00060B1Du;
            if ((velo >> 24) != 0)
            {
                SetTexQuadA(asistenteVelo, 0, 0, Width, Height, 1, 1, velo);
                stars.DrawColorQuad(asistenteVelo); // despues de toda la UI; nunca se atenúa el sonido
            }
            }
            Rectangle panel = guia ? Rectangle.Round(asistenteGuiaPanel) : AsistentePanelRect(Width, Height);
            float entradaPanel = AsistenteSuaviza((presencia - .13) / .72);
            float panelAlpha = entradaPanel;
            // El mensaje nuevo respira sin mover las opciones ni reescribir el
            // texto letra a letra. La seleccion no vuelve a disparar este fundido.
            if (asistenteEstado.Animada && asistenteEstado.Visible)
                panelAlpha *= .82f + .18f * AsistenteSuaviza(((cuadroMs >= 0 ? cuadroMs : AhoraMs()) / 1000 - asistenteTextoInicio) / .18);
            if (asistenteTex != IntPtr.Zero && panelAlpha > .001f)
            {
                float sube = asistenteEstado.Animada ? (1 - entradaPanel) * 16 : 0;
                SetTexQuadA(asistenteQuad, panel.Left, panel.Top + sube, panel.Right, panel.Bottom + sube,
                    asistenteU, asistenteV, ((uint)(panelAlpha * 255) << 24) | 0x00FFFFFFu);
                stars.DrawQuad(asistenteTex, asistenteQuad);
            }
            float alfa = SafeMoonLevel(asistentePose.Alfa);
            if (alfa <= .001f) return;
            float cx = asistentePose.Centro.X, cy = asistentePose.Centro.Y, radio = asistentePose.Radio;
            if (presencia > 0)
            {
                stars.GlowBatchBegin(2);
                uint tinte = StarColorAt(cx, cy) & 0x00FFFFFFu;
                stars.GlowBatchAdd(cx, cy, radio * 1.43f, ((uint)(46 * presencia * alfa) << 24) | tinte);
                stars.GlowBatchAdd(cx, cy + radio * 1.46f, radio * .6f, ((uint)(18 * presencia * alfa) << 24) | tinte);
                stars.GlowBatchEnd();
                int n = AsistenteMiembrosExpresivos(asistenteMiembros, cx, cy, radio, presencia, alfa, ExpresionGuia(cx, cy, presencia), asistentePose.Giro);
                if (n > 0) stars.DrawMesh(asistenteMiembros, n / 3);
            }
            uint color = StarColorAt(cx, cy);
            if (Estrella3D)
            {
                int filo, mejor; float brillo;
                int n = PoseEstrella(e3caras, 0, e3filo, 0, cx, cy, radio * Estrella3dEscala,
                    asistentePose.Yaw, asistentePose.Pitch, asistentePose.Giro, color, alfa * 255,
                    false, 1.1f, out filo, out mejor, out brillo);
                if (filo > 0) stars.DrawMesh(e3filo, filo / 3);
                if (n > 0) stars.DrawMesh(e3caras, n / 3);
            }
            else if (lunaTex != IntPtr.Zero)
            {
                if (FondoNatural) color = 0xFF000000u | (PaletteFlow.Mix(0xD6E4FFu, color, .26f) & 0xFFFFFFu);
                color = FadeArgb(color, alfa);
                SetTexQuadA(lunaQuad, cx - radio, cy - radio, cx + radio, cy + radio, luU, luV, color);
                RotateStarQuad(lunaQuad, cx, cy, asistentePose.Giro);
                stars.DrawQuad(lunaTex, lunaQuad);
            }
            int brazos = AsistenteBrazosExpresivos(asistenteBrazos, cx, cy, radio, presencia, alfa, ExpresionGuia(cx, cy, presencia), asistentePose.Giro);
            if (brazos > 0) stars.DrawMesh(asistenteBrazos, brazos / 3);
            if (guia && asistenteEstado.Visible) DibujaFlechaGuia(cx, cy, radio, presencia);
        }

        // Geometria negra con extremos redondos y filo suavizado. Se puede probar
        // sin dispositivo D3D; un unico lote para brazos, manos, piernas y zapatos.
        internal static int AsistenteMallaMiembros(D3D9.Vtx[] v, float cx, float cy,
            float radio, float cantidad, float gesto, float alfa)
        {
            if (!AsistenteGeometriaValida(v, 2600, cx, cy, radio)) return 0;
            cantidad = SafeMoonLevel(cantidad); alfa = SafeMoonLevel(alfa);
            if (cantidad <= 0 || alfa <= 0) return 0;
            gesto = Math.Max(-1, Math.Min(1, float.IsNaN(gesto) ? 0 : gesto));
            AsistenteExpresionGpu e = AsistenteNeutral(); e.ManoDerY = -.20f - .15f * gesto;
            int n = AsistenteMiembrosExpresivos(v, cx, cy, radio, cantidad, alfa, e, 0);
            AsistenteArmaBrazos(v, ref n, cx, cy, radio*cantidad,
                Math.Max(2, radio*.055f)*cantidad, ((uint)(alfa*255)<<24)|0x0006080Du, e);
            return n;
        }

        private static void AsistenteSegmento(D3D9.Vtx[] v, ref int n, float x0, float y0,
            float x1, float y1, float ancho, uint color)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float largo = (float)Math.Sqrt(dx * dx + dy * dy);
            if (largo <= .001f) return;
            float px = -dy / largo, py = dx / largo;
            float ax = px * ancho, ay = py * ancho;
            AsistenteCuadro(v, ref n, x0 - ax, y0 - ay, x1 - ax, y1 - ay,
                x1 + ax, y1 + ay, x0 + ax, y0 + ay, color, color, color, color);
            uint fuera = color & 0xFFFFFFu;
            AsistenteCuadro(v, ref n, x0 - ax - px, y0 - ay - py, x1 - ax - px, y1 - ay - py,
                x1 - ax, y1 - ay, x0 - ax, y0 - ay, fuera, fuera, color, color);
            AsistenteCuadro(v, ref n, x0 + ax, y0 + ay, x1 + ax, y1 + ay,
                x1 + ax + px, y1 + ay + py, x0 + ax + px, y0 + ay + py, color, color, fuera, fuera);
            AsistenteDisco(v, ref n, x0, y0, ancho, color);
            AsistenteDisco(v, ref n, x1, y1, ancho, color);
        }

        private static void AsistenteCuadro(D3D9.Vtx[] v, ref int n, float x0, float y0,
            float x1, float y1, float x2, float y2, float x3, float y3,
            uint c0, uint c1, uint c2, uint c3)
        {
            V3(v, n++, x0, y0, c0); V3(v, n++, x1, y1, c1); V3(v, n++, x2, y2, c2);
            V3(v, n++, x0, y0, c0); V3(v, n++, x2, y2, c2); V3(v, n++, x3, y3, c3);
        }

        private static void AsistenteDisco(D3D9.Vtx[] v, ref int n, float x, float y, float r, uint color)
        {
            const int pasos = 12;
            float x0 = r, y0 = 0;
            for (int i = 1; i <= pasos; i++)
            {
                float x1 = asistenteCirculoX[i] * r, y1 = asistenteCirculoY[i] * r;
                V3(v, n++, x, y, color); V3(v, n++, x + x0, y + y0, color); V3(v, n++, x + x1, y + y1, color);
                uint fuera = color & 0xFFFFFFu;
                float f = (r + 1) / Math.Max(.001f, r);
                AsistenteCuadro(v, ref n, x + x0, y + y0, x + x1, y + y1,
                    x + x1 * f, y + y1 * f, x + x0 * f, y + y0 * f, color, color, fuera, fuera);
                x0 = x1; y0 = y1;
            }
        }

        private static float[] AsistenteCirculo(bool horizontal)
        {
            float[] valores = new float[13];
            for (int i = 0; i < valores.Length; i++)
                valores[i] = (float)(horizontal ? Math.Cos(i * Math.PI / 6) : Math.Sin(i * Math.PI / 6));
            return valores;
        }

        private void SueltaAsistente()
        {
            SueltaPanelAsistente();
            asistenteAnteriorVisible = asistenteDibujando = false; asistenteFrente = 0;
            asistenteEstado = new AsistenteEstadoGpu();
            asistenteEventoVisto = Int32.MinValue;
            asistenteExpresion = asistenteExpresionDesde = AsistenteNeutral();
        }

        private void SueltaPanelAsistente()
        {
            if (asistenteTex != IntPtr.Zero) { D3D9.Release(asistenteTex); asistenteTex = IntPtr.Zero; }
            asistenteBmp = null; asistenteVer = -1; asistenteW = asistenteH = 0;
            asistenteTextoClave = -1;
        }
    }
}
#endif

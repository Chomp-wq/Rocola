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
        internal bool AsistenteEnTransicion
        { get { return asistenteDibujando; } }

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
                int textoClave = asistenteEstado.Pagina * 4 + asistenteEstado.Leccion;
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
            AsistentePoseGpu hasta = asistenteEstado.Visible
                ? new AsistentePoseGpu { Centro = AsistenteCentroFrente(Width, Height),
                    Radio = AsistenteRadioFrente(Width, Height), Pitch = .08, Miembros = 1, Alfa = 1 }
                : AsistentePoseFondo();
            float sx = Width / (float)Math.Max(1, asistenteOrigenW);
            float sy = Height / (float)Math.Max(1, asistenteOrigenH);
            AsistentePoseGpu desde = asistenteDesde;
            desde.Centro = new PointF(desde.Centro.X * sx, desde.Centro.Y * sy);
            desde.Radio *= Math.Min(sx, sy);
            // Al devolverla, elegir el giro equivalente mas cercano, no dar una
            // vuelta de golpe cuando el reloj musical ya paso de 2 pi.
            hasta.Yaw = desde.Yaw + AsistenteGiroCorto(hasta.Yaw - desde.Yaw);
            double t = asistenteEstado.Animada ? Math.Max(0, (ahora - asistenteInicio) / AsistenteSaltoSegundos) : 1;
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
            uint velo = ((uint)(presencia * 138) << 24) | 0x00060B1Du;
            if ((velo >> 24) != 0)
            {
                SetTexQuadA(asistenteVelo, 0, 0, Width, Height, 1, 1, velo);
                stars.DrawColorQuad(asistenteVelo); // despues de toda la UI; nunca se atenúa el sonido
            }
            Rectangle panel = AsistentePanelRect(Width, Height);
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
                int n = AsistenteMiembrosExpresivos(asistenteMiembros, cx, cy, radio, presencia, alfa, asistenteExpresion, asistentePose.Giro);
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
            int brazos = AsistenteBrazosExpresivos(asistenteBrazos, cx, cy, radio, presencia, alfa, asistenteExpresion, asistentePose.Giro);
            if (brazos > 0) stars.DrawMesh(asistenteBrazos, brazos / 3);
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

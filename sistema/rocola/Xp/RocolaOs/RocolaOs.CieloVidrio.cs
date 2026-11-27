// EL CIELO VIVO DE ROCOLA OS, VIDRIO (VI1-VI6; PLAN_CIELO_VIVO.md; FASE11_cielo-vidrio.md). Las MISMAS piezas de XP, en su sitio, con su tamano,
// sus colores y su letra: solo cambia el material.
//
//   VI1 la ficha rapida del disco (clave vidrio_ficha), VI2 la franja «AHORA SUENA» del reposo (vidrio_franja), VI4 la lista del disco, tenida por la
//   portada (vidrio_lista) y VI6 los avisos y el buscador (vidrio_buscador): DESENFOCAN lo de detras (RocolaOs.CieloVidrioGl.cs) y por eso van APAGADAS
//   de fabrica, hasta medirlas en el A4. VI3 las pastillas (BUSCAR, AYUDA, la barra de letras, En cola, creditos; vidrio_pastillas) y VI5 el filo de luz
//   que sigue al cielo (vidrio_luz): no desenfocan; encendidas dentro de la funcion «vidrio», que desde el 27-11-2026 va ENCENDIDA de fabrica (DeFabrica = true; VI1, VI2, VI4 y VI6 siguen apagadas).
//
// COMO SE HACE (sin tocar el dibujo de las piezas, que sigue siendo el de XP):
//   - Debajo de la pieza, en su capa (AntesDeLaFicha, AntesDelEstado, AntesDeLaInterfaz, antes del aviso), el pintor deja lo de detras desenfocado y
//     oscurecido un poco (la pieza de XP va encima).
//   - Para que se vea, el relleno de la pieza de XP baja de opaco a translucido: un gancho de una linea en su horneado (RocolaOsVidrio.Alfa: 0,82 de su
//     alfa de XP; con la idea apagada, o sin ROCOLA_OS, el mismo alfa de XP). La letra no se toca. Esos rellenos se hornean al armar la pieza (la
//     ficha de cada disco, el panel al cambiar la cancion, la lista al abrir un disco, el aviso al salir): encender o apagar la idea en vivo vale para
//     lo siguiente que se hornee.
//   - Al final del cuadro (gancho g360, antes del boleto) se pintan encima de todas las piezas que dejaron los pintores en este cuadro: un brillo
//     de luz arriba (blanco al 9 %, que se va en el 45 % del alto) y el FILO DE LUZ de VI5, de 1,2 px, con el color de la raya del horizonte (DN1: el
//     de la hora; sin DN1 o de noche, el plateado de XP), mas fuerte arriba. Sumado: no cubre la letra.
//   - Regla 3 (siempre se lee): el fondo desenfocado va al 0,78 de su brillo y el relleno de XP (azul oscuro) a 0,82 de su alfa; con la pared mas
//     blanca de detras (todo blanco: no pasa) el vidrio de la ficha da luminancia ~0,05: el blanco de la letra a 10:1 y su gris azulado a 4,8:1 (la
//     bateria lo calcula: `rocola-xp --prueba-vidrio` y mide las capturas).
//   - Nada se mueve solo (el desenfoque sigue a lo de detras, 15 veces por segundo como mucho): la regla 5 (calma al elegir) no tiene nada que
//     calmar aqui.
// Sin ROCOLA_OS o con las seis claves en 0, no se llama a nada de esto y el alfa es el de XP (cintas iguales).
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    internal static class RocolaOsVidrio
    {
        internal const int Ficha = 1, Franja = 2, Pastillas = 3, Lista = 4, Luz = 5, Buscador = 6;
        internal const float FactorAlfa = 0.82f;        // el relleno de XP de las piezas que desenfocan, por esto
        internal const float BrilloFondo = 0.78f;       // lo de detras desenfocado, por esto (oscuro: se lee)
        internal const double CadaMs = 66.0;            // un desenfoque nuevo cada 66 ms como mucho (15 por segundo)
        internal static volatile bool SinDesenfoque;    // la tarjeta no pudo (sin memoria): las piezas quedan como las de XP

        // ---- el gancho de los rellenos: el alfa de la pieza (0 a 255) de XP o el de vidrio
        internal static int Alfa(int pieza, int xp)
        {
            if (!RocolaOs.Activo || SinDesenfoque) return xp;
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.Vidrio || RocolaOsCielo.NucleoCaido) return xp;
            bool si = pieza == Ficha ? f.VI1 : pieza == Franja ? f.VI2 : pieza == Lista ? f.VI4 : pieza == Buscador ? f.VI6 : false;
            if (!si) return xp;
            int a = (int)(xp * FactorAlfa + 0.5f);
            return a < 0 ? 0 : (a > 255 ? 255 : a);
        }

        // el color de un relleno de XP con el alfa de vidrio (el aviso y el toast, que llegan como Color)
        internal static Color Con(int pieza, Color c) { return Color.FromArgb(Alfa(pieza, c.A), c); }

        // ---- las piezas de este cuadro (las deja cada pintor; las lee el filo de luz de encima de todo)
        internal struct Pieza { internal float X, Y, W, H, R, Alfa; internal int Tipo; }
        private static readonly Pieza[] piezas = new Pieza[24];
        private static int nPiezas;
        internal static void Limpia() { nPiezas = 0; }
        internal static int Cuantas { get { return nPiezas; } }
        internal static Pieza Dame(int i) { return piezas[i]; }
        internal static void Anota(int tipo, float x, float y, float w, float h, float r, float alfa)
        {
            if (nPiezas >= piezas.Length || alfa <= 0.004f || w < 6f || h < 6f) return;
            Pieza p; p.X = x; p.Y = y; p.W = w; p.H = h; p.R = r; p.Alfa = alfa > 1f ? 1f : alfa; p.Tipo = tipo;
            piezas[nPiezas++] = p;
        }

        // ---- la geometria: un rectangulo de esquinas redondas como contorno (puntos en orden, de la esquina de arriba a la izquierda)
        internal const int SegEsquina = 7;
        internal static int Contorno(float x, float y, float w, float h, float r, float[] px, float[] py)
        {
            r = Math.Min(r, Math.Min(w, h) * 0.5f);
            int seg = r < 1.2f ? 0 : SegEsquina, n = 0;
            for (int esq = 0; esq < 4; esq++)
            {
                float cx = (esq == 0 || esq == 3) ? x + r : x + w - r, cy = (esq == 0 || esq == 1) ? y + r : y + h - r;
                double a0 = Math.PI * (1.0 + esq * 0.5);   // arriba-izq: 180 a 270; arriba-der: 270 a 360; abajo-der: 0 a 90; abajo-izq: 90 a 180
                if (seg == 0) { px[n] = (esq == 0 || esq == 3) ? x : x + w; py[n] = (esq == 0 || esq == 1) ? y : y + h; n++; continue; }
                for (int k = 0; k <= seg; k++)
                {
                    double a = a0 + (Math.PI * 0.5) * k / seg;
                    px[n] = cx + (float)Math.Cos(a) * r; py[n] = cy + (float)Math.Sin(a) * r; n++;
                }
            }
            return n;
        }
        internal const int MaxPuntos = 4 * (SegEsquina + 1);

        // ---- el color del filo de luz: el de la raya del horizonte de la hora (DN1) mezclado con el plateado de XP por lo cerrada que esta la noche
        internal static uint ColorFilo()
        {
            float r = 205f, g = 215f, b = 235f;   // el plateado de XP
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f != null && f.DN1)
            {
                float k = RocolaOsCielo.NocheCuadro;   // 1 = la noche de XP
                r = RocolaOsCielo.RayaR + (r - RocolaOsCielo.RayaR) * k; g = RocolaOsCielo.RayaG + (g - RocolaOsCielo.RayaG) * k; b = RocolaOsCielo.RayaB + (b - RocolaOsCielo.RayaB) * k;
            }
            return 0xFF000000u | ((uint)Math.Max(0, Math.Min(255, (int)(r + 0.5f))) << 16) | ((uint)Math.Max(0, Math.Min(255, (int)(g + 0.5f))) << 8) | (uint)Math.Max(0, Math.Min(255, (int)(b + 0.5f)));
        }

        // ---- las cuentas de la regla 3, puras (la prueba las recorre): la luminancia relativa del vidrio sobre un fondo de cierto brillo (0 a 1 en
        // sRGB, gris), con el desenfoque a BrilloFondo y el relleno (r, g, b de 0 a 255) a Alfa(xp)
        internal static double LuminanciaVidrio(double fondoGris, int rellenoR, int rellenoG, int rellenoB, int alfaXp, bool vidrio)
        {
            double a = (vidrio ? Math.Min(255, alfaXp * FactorAlfa) : alfaXp) / 255.0;
            double bg = vidrio ? fondoGris * BrilloFondo : fondoGris;
            return Lum(a * rellenoR / 255.0 + (1 - a) * bg * 1.0, a * rellenoG / 255.0 + (1 - a) * bg, a * rellenoB / 255.0 + (1 - a) * bg);
        }
        internal static double Lum(double r, double g, double b) { return 0.2126 * Lineal(r) + 0.7152 * Lineal(g) + 0.0722 * Lineal(b); }
        internal static double Lineal(double c) { return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        // el contraste de un texto (luminancia lt) sobre un fondo (lf)
        internal static double Contraste(double lt, double lf) { double a = Math.Max(lt, lf), b = Math.Min(lt, lf); return (a + 0.05) / (b + 0.05); }
    }

    // ======================================================================== LOS PINTORES (cada uno en su capa, debajo de la pieza de XP)
    internal abstract class RocolaOsVidrioPintor : RocolaOsCieloPintor
    {
        protected RocolaOsVidrioPintor(string quien, int orden) { Quien = quien; Orden = RocolaOsCieloOrden.Vidrio + orden; }
#pragma warning disable CA2255
        [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.AntesDeLaFicha, new RocolaOsVidrioFichaPintor());
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.AntesDelEstado, new RocolaOsVidrioEstadoPintor());
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.AntesDeLaInterfaz, new RocolaOsVidrioInterfazPintor());
        }
    }
    // el primero del grupo en cada cuadro limpia la lista de piezas (el orden Vidrio + 0 corre primero en su capa; Prepara corre antes del cuadro)
    internal sealed class RocolaOsVidrioFichaPintor : RocolaOsVidrioPintor
    {
        internal RocolaOsVidrioFichaPintor() : base("VI1 la ficha de vidrio", 0) { }
        internal override void Prepara(GpuPanel p) { RocolaOsVidrio.Limpia(); }
        internal override void Pinta(GpuPanel p, int capa) { p.RocolaOsVidrioFicha(); }
    }
    internal sealed class RocolaOsVidrioEstadoPintor : RocolaOsVidrioPintor
    {
        internal RocolaOsVidrioEstadoPintor() : base("VI2 la franja de vidrio", 10) { }
        internal override void Pinta(GpuPanel p, int capa) { p.RocolaOsVidrioEstado(); }
    }
    internal sealed class RocolaOsVidrioInterfazPintor : RocolaOsVidrioPintor
    {
        internal RocolaOsVidrioInterfazPintor() : base("VI3 las pastillas, VI4 la lista y VI6 el buscador de vidrio", 20) { }
        internal override void Pinta(GpuPanel p, int capa) { p.RocolaOsVidrioInterfaz(); }
    }

    // ======================================================================== LO QUE SE PINTA (en GpuPanel: ve lo privado de XP)
    internal sealed partial class GpuPanel
    {
        private Tarjeta.Vtx[] RocolaOsVidrioV = new Tarjeta.Vtx[(RocolaOsVidrio.MaxPuntos + 2) * 3 * 2 + 64];
        private readonly float[] RocolaOsVidrioX = new float[RocolaOsVidrio.MaxPuntos], RocolaOsVidrioY = new float[RocolaOsVidrio.MaxPuntos];
        private readonly float[] RocolaOsVidrioX2 = new float[RocolaOsVidrio.MaxPuntos], RocolaOsVidrioY2 = new float[RocolaOsVidrio.MaxPuntos];
        private int RocolaOsVidrioRanuraSig;

        private static uint VidrioColor(int a, uint rgb) { return ((uint)Math.Max(0, Math.Min(255, a)) << 24) | (rgb & 0x00FFFFFFu); }
        private static uint VidrioGris(float k, float acento, uint acentoRgb, int a)
        {
            // el brillo del fondo desenfocado (gris), mezclado un poco con el color de la portada si lo hay
            float r = 255f * k, g = r, b = r;
            if (acento > 0f)
            {
                float ar = (acentoRgb >> 16) & 255, ag = (acentoRgb >> 8) & 255, ab = acentoRgb & 255;
                r = r * (1f - acento) + ar * k * acento; g = g * (1f - acento) + ag * k * acento; b = b * (1f - acento) + ab * k * acento;
            }
            return ((uint)Math.Max(0, Math.Min(255, a)) << 24) | ((uint)Math.Max(0, Math.Min(255, (int)(r + 0.5f))) << 16) | ((uint)Math.Max(0, Math.Min(255, (int)(g + 0.5f))) << 8) | (uint)Math.Max(0, Math.Min(255, (int)(b + 0.5f)));
        }

        // El fondo desenfocado de una pieza: un abanico de triangulos del contorno redondo (inset 1,5 px: debajo del borde de la pieza de XP), con la
        // textura de la ranura (U, V de 0 a 1 son la zona entera).
        private bool RocolaOsVidrioFondo(int ranura, float x, float y, float w, float h, float r, float alfa, float acento, uint acentoRgb)
        {
            GpuStarfield st = stars;
            if (st == null || !st.Ok) return false;
            int zx = (int)Math.Floor(x), zy = (int)Math.Floor(y), zw = (int)Math.Ceiling(x + w) - zx, zh = (int)Math.Ceiling(y + h) - zy;
            IntPtr tex;
            if (!st.RocolaOsVidrioDesenfoca(ranura, zx, zy, zw, zh, AhoraMs(), RocolaOsVidrio.CadaMs, out tex))
            { if (st.RocolaOsVidrioNoPuede) RocolaOsVidrio.SinDesenfoque = true; return false; }
            // la zona entera SIN recortar es la que mide la textura (U, V de 0 a 1); el contorno que se dibuja se recorta con las coordenadas (VidrioVtx
            // sujeta U, V) y lo de fuera de la pantalla no se ve
            float fx0 = zx, fy0 = zy, zW = zw, zH = zh;
            if (zW < 2f || zH < 2f) return false;
            float ins = 1.5f;
            int n = RocolaOsVidrio.Contorno(x + ins, y + ins, w - 2 * ins, h - 2 * ins, Math.Max(0f, r - ins), RocolaOsVidrioX, RocolaOsVidrioY);
            uint c = VidrioGris(RocolaOsVidrio.BrilloFondo, acento, acentoRgb, (int)(alfa * 255f + 0.5f));
            float cx = x + w * 0.5f, cy = y + h * 0.5f;
            Tarjeta.Vtx[] v = RocolaOsVidrioV; int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                VidrioVtx(ref v[t++], cx, cy, fx0, fy0, zW, zH, c);
                VidrioVtx(ref v[t++], RocolaOsVidrioX[i], RocolaOsVidrioY[i], fx0, fy0, zW, zH, c);
                VidrioVtx(ref v[t++], RocolaOsVidrioX[j], RocolaOsVidrioY[j], fx0, fy0, zW, zH, c);
            }
            st.DrawTexturedBatch(tex, v, t / 3);
            return true;
        }
        private static void VidrioVtx(ref Tarjeta.Vtx v, float x, float y, float fx0, float fy0, float zW, float zH, uint c)
        {
            v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.Color = c;
            v.U = Math.Max(0f, Math.Min(1f, (x - fx0 + 0.5f) / zW)); v.V = Math.Max(0f, Math.Min(1f, (y - fy0 + 0.5f) / zH));
        }

        // VI1 (capa AntesDeLaFicha): la ficha rapida del disco
        internal void RocolaOsVidrioFicha()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.VI1 || RocolaOsVidrio.SinDesenfoque) return;
            if (!fichaCuadro.Has || fichaTex == IntPtr.Zero || fichaCuadro.Bmp == null || fichaCuadro.Alpha <= 0.004f) return;
            float al = fichaCuadro.Alpha * Math.Max(0f, Math.Min(1f, WallOpacity));
            float w = fichaCuadro.Bmp.Width, h = fichaCuadro.Bmp.Height, u = h / 230f;   // (PintaFicha: la caja de 1 a w-2 y la esquina de 16 u)
            RocolaOsVidrioFondo(0, fichaCuadro.X + 1f, fichaCuadro.Y + 1f, w - 3f, h - 3f, 16f * u, al, 0f, 0u);
            RocolaOsVidrio.Anota(RocolaOsVidrio.Ficha, fichaCuadro.X + 1f, fichaCuadro.Y + 1f, w - 3f, h - 3f, 16f * u, al);
        }
        // VI2 (capa AntesDelEstado): la franja «AHORA SUENA» del reposo con musica
        internal void RocolaOsVidrioEstado()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.VI2 || RocolaOsVidrio.SinDesenfoque) return;
            if (!(idleBlend > 0.004f && idleTex != IntPtr.Zero) || EstadoOpacoListo) return;
            float py = StatusTopY, al = idleBlend;
            RocolaOsVidrioFondo(1, 0f, py, Width, idleH, 0f, al, 0f, 0u);
            RocolaOsVidrio.Anota(RocolaOsVidrio.Franja, 0f, py, Width, idleH, 0f, al);
        }
        // VI3, VI4 y VI6 (capa AntesDeLaInterfaz): las pastillas, la lista del disco y el buscador
        internal void RocolaOsVidrioInterfaz()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null) return;
            float alUi = ControlsOpacity * SceneAlpha() * PaletteAlpha();
            if (f.VI4 && !RocolaOsVidrio.SinDesenfoque && GlassPieces && GetPiezaRect != null && PiezasN > 0)
            {
                Rectangle r = GetPiezaRect(0);   // la pieza 0: el cristal de la lista (BakeSongGlassTint)
                if (r.Width >= 40 && r.Height >= 40)
                {
                    float al = SceneAlpha() * ControlsOpacity;
                    uint ac = GlassTint;
                    RocolaOsVidrioFondo(2, r.X + 1f, r.Y + 1f, r.Width - 3f, r.Height - 3f, Math.Min(28f, Math.Min(r.Width, r.Height) / 4f), al, 0.30f, ac);
                    RocolaOsVidrio.Anota(RocolaOsVidrio.Lista, r.X + 1f, r.Y + 1f, r.Width - 3f, r.Height - 3f, Math.Min(28f, Math.Min(r.Width, r.Height) / 4f), al);
                }
            }
            if (f.VI3 && idleBlend < 0.5f)
            {
                Form1 fo = RocolaOs.LaRocola;
                if (fo != null) fo.RocolaOsVidrioPastillas(alUi * (1f - idleBlend));
            }
        }

        // el filo de luz y el brillo de TODAS las piezas de este cuadro, encima de ellas (gancho g360)
        private readonly Tarjeta.Vtx[] RocolaOsVidrioM = new Tarjeta.Vtx[(RocolaOsVidrio.MaxPuntos * 6 + 12) * 2];
        private int RocolaOsVidrioDibujadas;
        internal void RocolaOsVidrioEncima()
        {
            if (!RocolaOs.Activo) return;
            RocolaOsVidrioDibujadas = 0;
            RocolaOsVidrioEncimaDesde();
        }
        private void RocolaOsVidrioEncimaDesde()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.Vidrio || RocolaOsCielo.NucleoCaido || RocolaOsVidrio.Cuantas <= RocolaOsVidrioDibujadas) return;
            GpuStarfield st = stars; if (st == null || !st.Ok) return;
            uint filo = RocolaOsVidrio.ColorFilo();
            int desde = RocolaOsVidrioDibujadas; RocolaOsVidrioDibujadas = RocolaOsVidrio.Cuantas;
            for (int k = desde; k < RocolaOsVidrio.Cuantas; k++)
            {
                RocolaOsVidrio.Pieza pz = RocolaOsVidrio.Dame(k);
                // el brillo de luz arriba: solo en las piezas con vidrio (las pastillas son chicas y de color: el filo les basta)
                if (pz.Tipo != RocolaOsVidrio.Pastillas && pz.W > 20f)
                {
                    int n = RocolaOsVidrio.Contorno(pz.X, pz.Y, pz.W, pz.H, pz.R, RocolaOsVidrioX, RocolaOsVidrioY);
                    float alto = pz.H * 0.45f; int t = 0; Tarjeta.Vtx[] v = RocolaOsVidrioM;
                    float cx = pz.X + pz.W * 0.5f, cy = pz.Y + pz.H * 0.5f;
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        VidrioMalla(ref v[t++], cx, cy, pz, alto, 0.09f); VidrioMalla(ref v[t++], RocolaOsVidrioX[i], RocolaOsVidrioY[i], pz, alto, 0.09f); VidrioMalla(ref v[t++], RocolaOsVidrioX[j], RocolaOsVidrioY[j], pz, alto, 0.09f);
                    }
                    st.DrawMesh(v, t / 3);
                }
                // el filo de luz (VI5): un anillo de 1,2 px por dentro del contorno, mas claro arriba
                if (f.VI5) VidrioFilo(st, pz, filo);
            }
        }
        // VI6 (gancho g367, antes del aviso de siempre): la pastilla de color del aviso (toast) de XP, con lo de detras desenfocado
        private SKBitmap vidrioToastDe; private int vidrioToastVer = -1; private Rectangle vidrioToastCaja;
        internal void RocolaOsVidrioToast(GpuToast tt)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (!RocolaOs.Activo || f == null || !f.VI6 || RocolaOsVidrio.SinDesenfoque || !RocolaOsCielo.Va) return;
            if (!(tt.Has && toastTex != IntPtr.Zero && tt.Alpha > 0.004f && tt.Bmp != null)) return;
            if (!ReferenceEquals(vidrioToastDe, tt.Bmp) || vidrioToastVer != tt.Ver) { vidrioToastCaja = VidrioCajaOpaca(tt.Bmp); vidrioToastDe = tt.Bmp; vidrioToastVer = tt.Ver; }
            Rectangle c = vidrioToastCaja; if (c.Width < 16) return;
            float x = tt.X + c.X + 1f, y = tt.Y + c.Y + 1f, w = c.Width - 2f, h = c.Height - 2f, al = tt.Alpha;
            RocolaOsVidrioFondo(3, x, y, w, h, 16f, al, 0f, 0u);
            RocolaOsVidrio.Anota(RocolaOsVidrio.Buscador, x, y, w, h, 16f, al);
        }
        // el rectangulo de lo que esta pintado de verdad (alfa alto) en un mapa de bits: la pastilla de un aviso, sin su margen transparente
        private static Rectangle VidrioCajaOpaca(SKBitmap bmp)
        {
            try
            {
                int w = bmp.Width, h = bmp.Height; ReadOnlySpan<byte> px = bmp.GetPixelSpan();
                int x0 = w, x1 = -1, y0 = h, y1 = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[(y * w + x) * 4 + 3] > 200) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
                return x1 < 0 ? Rectangle.Empty : new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            }
            catch { return Rectangle.Empty; }
        }
        // VI6 (gancho g368, en el aviso claro): lo de detras desenfocado antes, y despues el filo de luz del toast y del aviso
        internal void RocolaOsVidrioAviso()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (!RocolaOs.Activo || f == null || !f.VI6 || RocolaOsVidrio.SinDesenfoque || !RocolaOsCielo.Va) return;
            if (!avisoCuadro.Has || avisoTex == IntPtr.Zero || avisoCuadro.Bmp == null) return;
            double ms = MomentoDisco() - avisoCuadro.Desde;
            float alfa, dx; CurvaAviso(ms, out alfa, out dx);
            if (alfa <= 0.004f) return;
            float bw = avisoCuadro.Bmp.Width, bh = avisoCuadro.Bmp.Height, aire = Form1.AvisoAire;
            float x = avisoCuadro.X + dx + aire + 1f, y = avisoCuadro.Y + aire + 1f, w = bw - 2f * aire - 2.5f, h = bh - 2f * aire - 2.5f, r = Form1.RadioAviso((int)bh);
            RocolaOsVidrioFondo(4, x, y, w, h, r, alfa, 0f, 0u);
            RocolaOsVidrio.Anota(RocolaOsVidrio.Buscador, x, y, w, h, r, alfa);
        }
        internal void RocolaOsVidrioFiloAviso() { if (RocolaOs.Activo) RocolaOsVidrioEncimaDesde(); }
        private static void VidrioMalla(ref Tarjeta.Vtx v, float x, float y, RocolaOsVidrio.Pieza pz, float alto, float a0)
        {
            float q = 1f - (y - pz.Y) / alto; q = q < 0f ? 0f : (q > 1f ? 1f : q);
            v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.U = 0f; v.V = 0f;
            v.Color = ((uint)(a0 * q * q * pz.Alfa * 255f + 0.5f) << 24) | 0x00F2F6FFu;
        }
        private void VidrioFilo(GpuStarfield st, RocolaOsVidrio.Pieza pz, uint rgb)
        {
            const float grosor = 1.2f;
            int n = RocolaOsVidrio.Contorno(pz.X, pz.Y, pz.W, pz.H, pz.R, RocolaOsVidrioX, RocolaOsVidrioY);
            int m = RocolaOsVidrio.Contorno(pz.X + grosor, pz.Y + grosor, pz.W - 2 * grosor, pz.H - 2 * grosor, Math.Max(0f, pz.R - grosor), RocolaOsVidrioX2, RocolaOsVidrioY2);
            if (n != m || n < 4) return;
            float arriba = 0.50f, abajo = 0.16f; if (pz.Tipo == RocolaOsVidrio.Pastillas) { arriba = 0.42f; abajo = 0.22f; }
            Tarjeta.Vtx[] v = RocolaOsVidrioM; int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                uint c0 = FiloColor(rgb, pz, RocolaOsVidrioY[i], arriba, abajo), c1 = FiloColor(rgb, pz, RocolaOsVidrioY[j], arriba, abajo);
                uint d0 = FiloColor(rgb, pz, RocolaOsVidrioY2[i], arriba, abajo), d1 = FiloColor(rgb, pz, RocolaOsVidrioY2[j], arriba, abajo);
                FiloV(ref v[t++], RocolaOsVidrioX[i], RocolaOsVidrioY[i], c0); FiloV(ref v[t++], RocolaOsVidrioX[j], RocolaOsVidrioY[j], c1); FiloV(ref v[t++], RocolaOsVidrioX2[j], RocolaOsVidrioY2[j], d1);
                FiloV(ref v[t++], RocolaOsVidrioX[i], RocolaOsVidrioY[i], c0); FiloV(ref v[t++], RocolaOsVidrioX2[j], RocolaOsVidrioY2[j], d1); FiloV(ref v[t++], RocolaOsVidrioX2[i], RocolaOsVidrioY2[i], d0);
            }
            st.DrawMeshAdd(v, t / 3);
        }
        private static uint FiloColor(uint rgb, RocolaOsVidrio.Pieza pz, float y, float arriba, float abajo)
        {
            float q = (y - pz.Y) / Math.Max(1f, pz.H); q = q < 0f ? 0f : (q > 1f ? 1f : q);
            float a = (arriba + (abajo - arriba) * q) * pz.Alfa;
            return ((uint)(a * 255f + 0.5f) << 24) | (rgb & 0x00FFFFFFu);
        }
        private static void FiloV(ref Tarjeta.Vtx v, float x, float y, uint c) { v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.U = 0f; v.V = 0f; v.Color = c; }
    }

    public partial class Form1
    {
        // VI3: las pastillas de la interfaz (BUSCAR, AYUDA, la barra de letras, En cola y creditos), en su sitio: solo se anotan para el filo de luz
        // (la pastilla de XP es translucida y ya deja ver el cielo: el vidrio de aqui es su filo y nada mas; no desenfocan)
        internal void RocolaOsVidrioPastillas(float alfa)
        {
            if (alfa <= 0.01f) return;
            RocolaOsVidrioPast(buscaView != null && buscaView.Visible ? buscaView.Bounds : Rectangle.Empty, alfa, 0f);
            RocolaOsVidrioPast(ayudaView != null && ayudaView.Visible ? ayudaView.Bounds : Rectangle.Empty, alfa, 0f);
            RocolaOsVidrioPast(pastCola != null && pastCola.Visible ? pastCola.Bounds : Rectangle.Empty, alfa, 1f);
            RocolaOsVidrioPast(pastCred != null && pastCred.Visible ? pastCred.Bounds : Rectangle.Empty, alfa, 1f);
            RocolaOsVidrioPast(letterBar != null && letterBar.Visible ? letterBar.Bounds : Rectangle.Empty, alfa, 0f);
        }
        private static void RocolaOsVidrioPast(Rectangle r, float alfa, float redonda)
        {
            if (r.Width < 20 || r.Height < 10) return;
            RocolaOsVidrio.Anota(RocolaOsVidrio.Pastillas, r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f, redonda > 0.5f ? (r.Height - 1f) / 2f : 12f, alfa);
        }
    }
}

#if ROCOLA_GPU
using System;
using System.Drawing;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        public bool RespuestaInmediata = true, TransicionesTacto = true, CachePortadas = true;
        public bool ControlesReposo = true, OndasUnidas = true;
        internal const int StatusEdgeOffset = 1;
        internal static float IdleControlAlpha(float blend, bool enabled)
        {
            if (!enabled) return 1f;
            if (float.IsNaN(blend)) blend = 0;
            return 1f - Math.Max(0, Math.Min(1, blend));
        }
        public float ControlsOpacity
        { get { return IdleControlAlpha(idleBlend, ControlesReposo && sceneMode == 1); } }
        internal static uint FadeArgb(uint color, float opacity)
        { return (color & 0xFFFFFFu) | ((uint)((color >> 24) * Math.Max(0, Math.Min(1, opacity))) << 24); }
        internal static float StatusPanelTop(int screenH, int compactH, int expandedH, float blend)
        {
            compactH = Math.Max(0, compactH);
            if (float.IsNaN(blend)) blend = 0;
            return screenH - compactH - Math.Max(0, expandedH - compactH) * Math.Max(0, Math.Min(1, blend));
        }
        private float StatusTopY
        { get { return StatusPanelTop(Height, uiBotH, idleH, idleBlend); } }
        internal static float AuroraBaseAtStatus(int screenH, int compactH, int expandedH, float blend)
        {
            // AurBrillo eleva su nucleo AurLineaAlza desde BaseY. Compensarlo
            // con la misma coordenada y el mismo borde que usa el panel, sin
            // redondear durante la animacion: no aparece una segunda linea.
            return StatusPanelTop(screenH, compactH, expandedH, blend) + StatusEdgeOffset + AurLineaAlza;
        }
        public bool GlassPieces, ColoresCache = true, LunaActiva = true;
        public uint GlassTint = 0xFFFFFFFFu;
        public Func<bool> GetGlassMode;
        public Func<float> GetSongSlide, GetSongTravel;
        public Func<bool> GetSongVertical;
        private float songFrameSlide, songFrameTravel;
        private bool songFrameVertical;
        private void CaptureSongSlide()
        {
            // Pagina, seleccion y pagina saliente comparten el MISMO instante.
            songFrameSlide = GetSongSlide != null ? GetSongSlide() : 0f;
            songFrameTravel = GetSongTravel != null ? GetSongTravel() : 0f;
            songFrameVertical = GetSongVertical != null && GetSongVertical();
        }
        public Func<int> GetPaletteKey;
        public Func<long> GetPaletteContent;
        private int paletteKey;
        private long paletteContent;
        private double paletteStart = -1000;
        private sealed class PaletteTexture
        {
            public IntPtr Tex; public Rectangle Rect; public float U, V;
        }
        private readonly PaletteTexture[] paletteOld = new PaletteTexture[MaxPiezas + 1];
        private void ClearPaletteFade()
        {
            for (int i = 0; i < paletteOld.Length; i++)
                if (paletteOld[i] != null)
                {
                    if (paletteOld[i].Tex != IntPtr.Zero) D3D9.Release(paletteOld[i].Tex);
                    paletteOld[i] = null;
                }
            paletteStart = -1000;
        }
        private void PreparePalette()
        {
            // Fijar el modo UNA vez por cuadro. Al terminar el hojeo no pueden
            // aparecer las capas nuevas encima de la ultima UI compuesta.
            bool glass = GetGlassMode != null && GetGlassMode();
            if (glass != GlassPieces) { GlassPieces = glass; UiUrgent = true; }
            int key = GetPaletteKey != null && ColoresCache ? GetPaletteKey() : 0;
            long content = GetPaletteContent != null ? GetPaletteContent() : 0;
            if (key == 0 || content != paletteContent || AhoraMs() - paletteStart >= 420)
                ClearPaletteFade();
            if (key != 0 && key != paletteKey && paletteKey != 0 && content == paletteContent && uiTex != IntPtr.Zero)
            {
                ClearPaletteFade();
                long bytes = CoverBytes(uiBmpW, uiBmpH);
                for (int i = 0; i < MaxPiezas; i++) bytes += CoverBytes(piezaW[i], piezaH[i]);
                // Cota adicional: la transicion nunca retiene mas de 64 MiB de texturas.
                if (bytes <= 64L * 1024 * 1024)
                {
                    paletteOld[0] = new PaletteTexture { Tex = uiTex, Rect = new Rectangle(0, 0, uiBmpW, uiBmpH), U = uiU, V = uiV };
                    uiTex = IntPtr.Zero; uiBmpW = uiBmpH = -1; uiVer = -1;
                    for (int i = 0; i < MaxPiezas; i++)
                    {
                        if (piezaTex[i] == IntPtr.Zero) continue;
                        paletteOld[i + 1] = new PaletteTexture { Tex = piezaTex[i], Rect = GetPiezaRect(i), U = piezaU[i], V = piezaV[i] };
                        piezaTex[i] = IntPtr.Zero; piezaW[i] = piezaH[i] = -1; piezaVer[i] = -1;
                    }
                    paletteStart = AhoraMs();
                }
            }
            if (key != paletteKey) UiUrgent = true;
            paletteKey = key; paletteContent = content;
        }
        private float PaletteAlpha()
        { return paletteOld[0] == null ? 1 : TactileAlpha(AhoraMs() - paletteStart, 420, 0); }
        private void DrawPaletteOld()
        {
            for (int i = 0; i < paletteOld.Length; i++)
            {
                PaletteTexture old = paletteOld[i];
                if (old == null || old.Tex == IntPtr.Zero) continue;
                Rectangle r = old.Rect;
                SetTexQuadA(uiQuad, r.Left, r.Top, r.Right, r.Bottom, old.U, old.V, FadeArgb(0xFFFFFFFFu, ControlsOpacity));
                stars.DrawQuad(old.Tex, uiQuad);
            }
        }
        // Omite la fila elegida en la pagina base: asi ni el texto ni sus bordes
        // suavizados se dibujan dos veces bajo la capa de seleccion.
        private void DrawSongPage(Rectangle page, Rectangle selected, int index, uint tint)
        {
            int cutTop = Math.Max(page.Top, Math.Min(page.Bottom, selected.Top));
            int cutBottom = Math.Max(cutTop, Math.Min(page.Bottom, selected.Bottom));
            if (selected.IsEmpty) { cutTop = page.Bottom; cutBottom = page.Bottom; }
            DrawPieceBand(page, index, page.Top, cutTop, tint);
            DrawPieceBand(page, index, cutBottom, page.Bottom, tint);
        }
        private void DrawPieceBand(Rectangle page, int index, int top, int bottom, uint tint)
        {
            if (bottom <= top) return;
            Rectangle clip = GetPiezaRect != null ? GetPiezaRect(0) : page;
            float dx = songFrameSlide;
            if (index == 3) dx -= songFrameTravel;
            float dy = 0;
            if (songFrameVertical) { dy = dx; dx = 0; }
            float left = Math.Max(clip.Left, page.Left + dx), right = Math.Min(clip.Right, page.Right + dx);
            float y0 = Math.Max(clip.Top, top + dy), y1 = Math.Min(clip.Bottom, bottom + dy);
            if (right <= left || y1 <= y0) return;
            SetTexQuadA(piezaQuad, left, y0, right, y1, piezaU[index], piezaV[index], tint);
            float u0 = piezaU[index] * (left - page.Left - dx) / page.Width;
            float u1 = piezaU[index] * (right - page.Left - dx) / page.Width;
            float from = piezaV[index] * (y0 - page.Top - dy) / page.Height;
            float to = piezaV[index] * (y1 - page.Top - dy) / page.Height;
            for (int k = 0; k < piezaQuad.Length; k++)
            {
                piezaQuad[k].U = piezaQuad[k].U == 0 ? u0 : u1;
                piezaQuad[k].V = piezaQuad[k].V == 0 ? from : to;
            }
            stars.DrawQuad(piezaTex[index], piezaQuad);
        }
        private float lunaBeat, lunaMid, lunaHigh;
        private double lunaClock;
        internal static float SafeMoonLevel(float level)
        { return float.IsNaN(level) || float.IsInfinity(level) ? 0 : Math.Max(0, Math.Min(1, level)); }
        internal static PointF MoonCenter(int w, int h, double seconds, float rise)
        {
            return new PointF(w * (.79f + .045f * (float)Math.Sin(seconds * .13)),
                h * (.57f + .038f * (float)Math.Cos(seconds * .17)) - Math.Max(0, rise) * .50f);
        }
        private void UpdateMoonMusic(float pulse)
        {
            // La estrella se mueve para el momento en que se va a VER el cuadro, no para el de dibujarlo: asi cada
            // refresco de la pantalla la muestra avanzada lo mismo (RelojPantallaGpu.cs). Sin saberlo (sin compositor,
            // o un cuadro pedido fuera del bucle), con el reloj de siempre, y nunca hacia atras.
            double now = RelojPantalla.MomentoVista(AhoraMs(), vistoMs, periodoVistaMs);
            if (now < cuadroMs) now = cuadroMs;
            cuadroMs = now;
            float dt = (float)Math.Max(0, Math.Min(.1, (now - lunaClock) / 1000)); lunaClock = now;
            float blend = 1 - (float)Math.Exp(-dt * 9);
            // Fuente musical independiente de la perilla de aurora; apagar ondas no apaga la estrella.
            StarMusic music=GetStarMusic!=null ? GetStarMusic() : new StarMusic {
                Bass=SafeMoonLevel(pulse),Mid=auCuadro.Has ? SafeMoonLevel(auCuadro.B[3]) : 0,
                High=auCuadro.Has ? SafeMoonLevel(auCuadro.B[6]) : 0 };
            float mid=SafeMoonLevel(music.Mid),high=SafeMoonLevel(music.High);
            AdvanceStarMusic(dt,music.Bass,mid,high);
            lunaBeat += (SafeMoonLevel(music.Bass) - lunaBeat) * blend;
            lunaMid += (mid - lunaMid) * blend; lunaHigh += (high - lunaHigh) * blend;
        }
        private void DrawMoonOrbits(float cx, float cy, float radius)
        {
            double seconds = AhoraMs() / 1000;
            stars.GlowBatchBegin(19);
            uint rgb = (NebColor & 0xFFFFFFu) | 0x00406080u;
            stars.GlowBatchAdd(cx, cy, radius * (2.5f + lunaBeat), ((uint)((25 + lunaBeat * 50) * Dim) << 24) | rgb);
            for (int orbit = 0; orbit < 3; orbit++)
            {
                float band = orbit == 0 ? lunaBeat : orbit == 1 ? lunaMid : lunaHigh;
                double angle = seconds * (.35 + orbit * .16) + orbit * 2.094;
                uint color = orbit == 0 ? 0xFFD899u : orbit == 1 ? 0xAACFFFu : 0xECC2FFu;
                for (int tail = 5; tail >= 0; tail--)
                {
                    double a = angle - tail * .095;
                    float x = cx + (float)Math.Cos(a) * radius * (1.7f + orbit * .33f);
                    float y = cy + (float)Math.Sin(a) * radius * (.60f + orbit * .15f);
                    float size = radius * (tail == 0 ? .24f + band * .22f : .12f);
                    int alpha = (int)((tail == 0 ? 165 + band * 80 : 75 - tail * 11) * Dim);
                    stars.GlowBatchAdd(x, y, Math.Max(1, size), ((uint)alpha << 24) | color);
                }
            }
            stars.GlowBatchEnd();
        }
        private double sceneStart = -1000, focusStart = -1000;
        private Rectangle focusPrevious;
        private int sceneMode;
        public void TouchUi()
        {
            ClearPaletteFade(); // la entrada nunca espera a una transicion de color
            if (!RespuestaInmediata) return;
            UiUrgent = true;
            IdleExpandTarget = false;
            // Una peticion por cuadro; nunca dibujar dentro del evento de entrada.
            proxCuadroMs = Math.Min(proxCuadroMs, AhoraMs());
        }
        public void SetScene(int mode)
        {
            if (mode == sceneMode) return;
            sceneMode = mode; sceneStart = AhoraMs();
            focusPrevious = Rectangle.Empty; focusStart = -1000;
        }
        internal static float TactileAlpha(double elapsed, double duration, float initial)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed)) return 1;
            double t = Math.Max(0, Math.Min(1, elapsed / duration));
            return initial + (1 - initial) * (float)(1 - Math.Pow(1 - t, 3));
        }
        private float SceneAlpha()
        {
            return TransicionesTacto && (sceneMode == 1 || sceneMode == 3)
                ? TactileAlpha(AhoraMs() - sceneStart, 140, .75f) : 1;
        }
        private float FocusAlpha(int piece, Rectangle rect)
        {
            if (piece == 2 && rect != focusPrevious)
            { focusPrevious = rect; focusStart = AhoraMs(); }
            return TransicionesTacto && piece >= 2
                ? TactileAlpha(AhoraMs() - focusStart, 120, .65f) : 1;
        }

        // Solo texturas ya subidas, a resolucion original. Cada COM pertenece a
        // un unico slot: al recuperar se intercambian, no se comparten ni duplican.
        private sealed class CoverSlot
        {
            public IntPtr Tex; public Bitmap Bmp;
            public int W, H; public float U, V;
            public long Stamp;
        }
        private readonly CoverSlot[] recentCovers = new CoverSlot[8];
        private long coverStamp;
        internal static long CoverBytes(int w, int h)
        {
            long pw = 1, ph = 1;
            while (pw < w && pw < 0x40000000) pw <<= 1;
            while (ph < h && ph < 0x40000000) ph <<= 1;
            return w < 1 || h < 1 ? 0 : pw * ph * 4;
        }
        private void TrimRecentCovers(bool all)
        {
            long total = 0;
            for (int i = 0; i < recentCovers.Length; i++)
                if (recentCovers[i] != null) total += CoverBytes(recentCovers[i].W, recentCovers[i].H);
            while (all || total > 8L * 1024 * 1024)
            {
                int old = -1;
                for (int i = 0; i < recentCovers.Length; i++)
                    if (recentCovers[i] != null && (old < 0 || recentCovers[i].Stamp < recentCovers[old].Stamp)) old = i;
                if (old < 0) break;
                CoverSlot s = recentCovers[old];
                total -= CoverBytes(s.W, s.H);
                if (s.Tex != IntPtr.Zero) D3D9.Release(s.Tex);
                recentCovers[old] = null; // no somos duenos del Bitmap
            }
        }
        private void EnsureRecentCover(ref IntPtr tex, ref Bitmap last, ref int w, ref int h, ref float u, ref float v, Bitmap cur)
        {
            if (!CachePortadas) { TrimRecentCovers(true); EnsureCover(ref tex, ref last, ref w, ref h, ref u, ref v, cur); return; }
            if (ReferenceEquals(cur, last) && (cur == null || tex != IntPtr.Zero)) return;
            int slot = -1;
            bool hit = false;
            for (int i = 0; i < recentCovers.Length; i++)
                if (recentCovers[i] != null && cur != null && ReferenceEquals(recentCovers[i].Bmp, cur) && recentCovers[i].Tex != IntPtr.Zero)
                { slot = i; hit = true; break; }
            if (slot < 0)
                for (int i = 0; i < recentCovers.Length; i++)
                    if (recentCovers[i] == null) { slot = i; break; }
            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < recentCovers.Length; i++)
                    if (recentCovers[i].Stamp < recentCovers[slot].Stamp) slot = i;
            }
            CoverSlot s = recentCovers[slot] ?? new CoverSlot();
            IntPtr nextTex = s.Tex; Bitmap nextBmp = s.Bmp;
            int nextW = s.W, nextH = s.H; float nextU = s.U, nextV = s.V;
            s.Tex = tex; s.Bmp = last; s.W = w; s.H = h; s.U = u; s.V = v; s.Stamp = ++coverStamp;
            recentCovers[slot] = s;
            tex = nextTex; last = nextBmp; w = nextW; h = nextH; u = nextU; v = nextV;
            // Un hueco vacio no debe mantener un bitmap ni contarse como textura.
            if (s.Tex == IntPtr.Zero) recentCovers[slot] = null;
            if (!hit) EnsureCover(ref tex, ref last, ref w, ref h, ref u, ref v, cur);
            TrimRecentCovers(false);
        }
    }
}
#endif

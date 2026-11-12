#if ROCOLA_GPU
using System;
using System.Drawing; using SkiaSharp;   // EN LINUX: lo que se sube es un SKBitmap, y las portadas que llegan de D3D9Star.cs tambien (la cache de portadas)
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
            // La medida del mapa de bits con el que se subio (Rect es donde se dibuja, y puede no coincidir): la que necesita el
            // repuesto para saber cuanto mide de verdad la textura. Con otra, una subida posterior escribiria filas de mas.
            public int BmpW, BmpH;
        }
        private readonly PaletteTexture[] paletteOld = new PaletteTexture[MaxPiezas + 1];
        private void ClearPaletteFade()
        {
            for (int i = 0; i < paletteOld.Length; i++)
                if (paletteOld[i] != null)
                {
                    if (paletteOld[i].Tex != IntPtr.Zero) Repone(paletteOld[i].Tex, paletteOld[i].BmpW, paletteOld[i].BmpH);
                    paletteOld[i] = null;
                }
            paletteStart = -1000;
        }

        // EL REPUESTO DE TEXTURAS DE LA INTERFAZ (24-10-2026). El diagnostico del A4 ponia 23 de los 25 huecos de mas de 50 ms en la fase
        // de la interfaz, al entrar y salir de un disco: la textura de las barras cambia de medida (navegar: una franja; dentro: casi toda
        // la pantalla), y cada vez se destruia y se creaba otra de hasta 1024x1024 (4 MB), y lo mismo las piezas de cristal de la lista y
        // la textura que guarda el fundido de colores al cambiar de letra. Ahora la que ya no sirve se guarda (hasta cuatro) y la siguiente
        // que necesita la misma medida en potencias de dos toma esa y solo le sube la imagen encima. Lo que se ve es identico: la imagen se
        // sube entera, con su borde, como en una textura nueva. Automatico, sin interruptor (no cambia el aspecto).
        private const int RepuestoMax = 4;
        private readonly IntPtr[] repuestoTex = new IntPtr[RepuestoMax];
        private readonly int[] repuestoTw = new int[RepuestoMax], repuestoTh = new int[RepuestoMax];
        private int repuestoTurno;
        internal static long RepuestosGuardados, RepuestosUsados;   // (las pruebas)
        private static int Pot2(int n) { if (n < 1 || n > 0x40000000) return 0; int p = 1; while (p < n) p <<= 1; return p; }
        // En vez de soltarla: se guarda. Si ya hay cuatro, se suelta la mas vieja.
        private void Repone(IntPtr tex, int bw, int bh)
        {
            if (tex == IntPtr.Zero) return;
            int tw = Pot2(bw), th = Pot2(bh);
            if (tw == 0 || th == 0 || TexturasDivididasGpu.Busca(tex) != null) { Tarjeta.Suelta(tex); return; }   // (las divididas, no)
            int k = -1;
            for (int i = 0; i < RepuestoMax; i++) if (repuestoTex[i] == IntPtr.Zero) { k = i; break; }
            if (k < 0) { k = repuestoTurno; repuestoTurno = (repuestoTurno + 1) % RepuestoMax; Tarjeta.Suelta(repuestoTex[k]); }
            repuestoTex[k] = tex; repuestoTw[k] = tw; repuestoTh[k] = th; RepuestosGuardados++;
        }
        private IntPtr TomaRepuesto(int bw, int bh)
        {
            int tw = Pot2(bw), th = Pot2(bh);
            if (tw == 0 || th == 0) return IntPtr.Zero;
            for (int i = 0; i < RepuestoMax; i++)
                if (repuestoTex[i] != IntPtr.Zero && repuestoTw[i] == tw && repuestoTh[i] == th)
                { IntPtr t = repuestoTex[i]; repuestoTex[i] = IntPtr.Zero; return t; }
            return IntPtr.Zero;
        }
        private void SueltaRepuestos()
        {
            for (int i = 0; i < RepuestoMax; i++) if (repuestoTex[i] != IntPtr.Zero) { Tarjeta.Suelta(repuestoTex[i]); repuestoTex[i] = IntPtr.Zero; }
        }
        // Como Upload, pero una textura de otra medida se guarda en vez de destruirse, y se usa una guardada que valga.
        private IntPtr SubeReponiendo(IntPtr tex, ref int bw, ref int bh, SKBitmap bmp, out float u, out float v)   // EN LINUX: un SKBitmap (UpdateOverlay y Upload ya lo reciben); los que pasan un Bitmap de la capa entran por Puente.portadas.cs
        {
            if (bmp != null && (tex == IntPtr.Zero || bw != bmp.Width || bh != bmp.Height))
            {
                if (tex != IntPtr.Zero) { Repone(tex, bw, bh); tex = IntPtr.Zero; }
                IntPtr r = TomaRepuesto(bmp.Width, bmp.Height);
                if (r != IntPtr.Zero)
                {
                    if (libroOn) libro.Subidas++;
                    if (stars.UpdateOverlay(r, bmp, out u, out v)) { bw = bmp.Width; bh = bmp.Height; RepuestosUsados++; return r; }
                    Tarjeta.Suelta(r);
                }
            }
            return Upload(tex, ref bw, ref bh, bmp, out u, out v);
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
            if (key != 0 && key != paletteKey && paletteKey != 0 && content == paletteContent && (uiTex != IntPtr.Zero || uiVacia))
            {
                ClearPaletteFade();
                long bytes = CoverBytes(uiBmpW, uiBmpH);
                for (int i = 0; i < MaxPiezas; i++) bytes += CoverBytes(piezaW[i], piezaH[i]);
                // Cota adicional: la transicion nunca retiene mas de 64 MiB de texturas.
                if (bytes <= 64L * 1024 * 1024)
                {
                    paletteOld[0] = new PaletteTexture { Tex = uiTex, Rect = new Rectangle(0, 0, uiBmpW, uiBmpH), U = uiU, V = uiV, BmpW = uiBmpW, BmpH = uiBmpH };
                    uiTex = IntPtr.Zero; uiBmpW = uiBmpH = -1; uiVer = -1;
                    for (int i = 0; i < MaxPiezas; i++)
                    {
                        if (piezaTex[i] == IntPtr.Zero) continue;
                        paletteOld[i + 1] = new PaletteTexture { Tex = piezaTex[i], Rect = GetPiezaRect(i), U = piezaU[i], V = piezaV[i], BmpW = piezaW[i], BmpH = piezaH[i] };
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

        // Solo portadas inmutables ya subidas, a resolucion original. Cada slot
        // posee una referencia: puede compartir el COM, nunca modificar su contenido.
        private sealed class CoverSlot
        {
            public IntPtr Tex; public SKBitmap Bmp;   // EN LINUX: Bitmap: la portada, un SKBitmap: la da GpuCubeInfo (D3D9Star.cs) y se compara por identidad
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
                if (s.Tex != IntPtr.Zero) Tarjeta.Suelta(s.Tex);
                recentCovers[old] = null; // no somos duenos del Bitmap
            }
        }
        private void EnsureRecentCover(ref IntPtr tex, ref SKBitmap last, ref int w, ref int h, ref float u, ref float v, SKBitmap cur)   // EN LINUX: Bitmap: las portadas, SKBitmap (selImgA, relImg y CoverA, de D3D9Star.cs)
        {
            if (!CachePortadas) { TrimRecentCovers(true); EnsureImmutableCover(ref tex, ref last, ref w, ref h, ref u, ref v, cur); return; }
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
            IntPtr nextTex = s.Tex; SKBitmap nextBmp = s.Bmp;   // EN LINUX: Bitmap: la portada, un SKBitmap
            int nextW = s.W, nextH = s.H; float nextU = s.U, nextV = s.V;
            s.Tex = tex; s.Bmp = last; s.W = w; s.H = h; s.U = u; s.V = v; s.Stamp = ++coverStamp;
            recentCovers[slot] = s;
            tex = nextTex; last = nextBmp; w = nextW; h = nextH; u = nextU; v = nextV;
            // Un hueco vacio no debe mantener un bitmap ni contarse como textura.
            if (s.Tex == IntPtr.Zero) recentCovers[slot] = null;
            if (!hit) EnsureImmutableCover(ref tex, ref last, ref w, ref h, ref u, ref v, cur);
            TrimRecentCovers(false);
        }
    }
}
#endif

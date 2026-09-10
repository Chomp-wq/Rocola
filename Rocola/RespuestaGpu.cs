#if ROCOLA_GPU
using System;
using System.Drawing;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        public bool RespuestaInmediata = true, TransicionesTacto = true, CachePortadas = true;
        private double sceneStart = -1000, focusStart = -1000;
        private Rectangle focusPrevious;
        private int sceneMode;
        public void TouchUi()
        {
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

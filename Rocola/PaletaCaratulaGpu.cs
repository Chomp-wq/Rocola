using System;
using System.Drawing;

namespace Rocola
{
    public partial class Form1
    {
        private bool paletaCompleta = true;
#if ROCOLA_GPU
        // Solo al entrar una portada en la cache: 1024 muestras, 27 grupos, hasta 8 tonos.
        // Incluye blancos/grises/negros y colores minoritarios; no inventa saturacion.
        internal static Color[] ExtractCoverPalette(Bitmap bitmap)
        {
            if (bitmap == null) return new Color[0];
            int[] count = new int[27], red = new int[27], green = new int[27], blue = new int[27];
            int nx = Math.Min(32, bitmap.Width), ny = Math.Min(32, bitmap.Height), total = 0;
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    Color c = bitmap.GetPixel((2*x+1)*bitmap.Width/(2*nx), (2*y+1)*bitmap.Height/(2*ny));
                    if (c.A < 128) continue;
                    int max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B));
                    int bin = max < 36 || max-min < 24 ? 24 + Math.Min(2, max/86) : Math.Min(23, (int)(c.GetHue()/15));
                    count[bin]++; red[bin] += c.R; green[bin] += c.G; blue[bin] += c.B; total++;
                }
            Color[] candidates = new Color[27];
            bool[] selected = new bool[27];
            for (int i = 0; i < 27; i++)
                if (count[i] > 0) candidates[i] = Color.FromArgb(red[i]/count[i], green[i]/count[i], blue[i]/count[i]);
            Color[] result = new Color[8]; int used = 0;
            while (used < result.Length)
            {
                int best = -1; double score = -1;
                for (int i = 0; i < 27; i++)
                {
                    if (selected[i] || count[i] == 0 || count[i]*100 < total) continue;
                    double distance = 195075;
                    for (int j = 0; j < used; j++)
                    {
                        int dr = candidates[i].R-result[j].R, dg = candidates[i].G-result[j].G, db = candidates[i].B-result[j].B;
                        distance = Math.Min(distance, dr*dr+dg*dg+db*db);
                    }
                    if (used > 0 && distance < 900) continue;
                    double value = Math.Sqrt(count[i]) * (used == 0 ? 1 : Math.Sqrt(distance));
                    if (value > score) { score = value; best = i; }
                }
                if (best < 0) break;
                selected[best] = true; result[used++] = candidates[best];
            }
            Array.Resize(ref result, used);
            // Orden estable por tono para evitar saltos de banda al cambiar de disco.
            Array.Sort(result, delegate(Color a, Color b) { return a.GetHue().CompareTo(b.GetHue()); });
            return result;
        }

        private void SyncGpuCoverPalette()
        {
            Color[] palette = null; float weight = 0f;
            if (paletaCompleta && fondoPortadaOn && !attractOn && !bgArtistaFijo &&
                nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Disc != null)
            {
                ColoresDisco cached;
                string key = nowPlaying.Disc.CoverPath;
                if (!string.IsNullOrEmpty(key) && colorPortada.TryGetValue(key, out cached))
                {
                    palette = cached.Paleta;
                    weight = bgSinGenero && portadaManda > 0 ? 1f : (float)portadaManda;
                }
            }
            gpuPanel.SetCoverPalette(palette, weight);
        }
#endif
    }

#if ROCOLA_GPU
    internal sealed partial class GpuPanel
    {
        private Color[] coverPalette;
        private readonly float[] coverTarget = new float[24], coverCurrent = new float[24];
        private readonly uint[] coverBands = new uint[8];
        private float coverWeight, coverTargetWeight;
        private double coverClock;
        private bool coverClockReady, coverColorsReady;

        internal void SetCoverPalette(Color[] palette, float weight)
        {
            coverTargetWeight = palette != null && palette.Length > 0 && weight > 0 ? Math.Min(1f, weight) : 0f;
            if (palette == null || palette.Length == 0 || object.ReferenceEquals(palette, coverPalette)) return;
            coverPalette = palette;
            for (int i = 0; i < 8; i++)
            {
                Color c = palette[(int)Math.Round(i*(palette.Length-1)/7f)];
                coverTarget[i*3] = c.R;
                coverTarget[i*3+1] = c.G;
                coverTarget[i*3+2] = c.B;
            }
            if (!coverColorsReady) { Array.Copy(coverTarget, coverCurrent, 24); coverColorsReady = true; }
        }

        internal void AdvanceCoverPalette(double now)
        {
            double dt = coverClockReady ? Math.Max(0, Math.Min(.1, (now-coverClock)/1000)) : 0;
            coverClock = now; coverClockReady = true;
            float f = (float)(1-Math.Exp(-dt*5));
            coverWeight += (coverTargetWeight-coverWeight)*f;
            if (Math.Abs(coverTargetWeight-coverWeight) < .0001f) coverWeight = coverTargetWeight;
            for (int i = 0; i < 24; i++) coverCurrent[i] += (coverTarget[i]-coverCurrent[i])*f;
            for (int i = 0; i < 8; i++)
                coverBands[i] = ((uint)Math.Round(coverCurrent[i*3])<<16) | ((uint)Math.Round(coverCurrent[i*3+1])<<8) | (uint)Math.Round(coverCurrent[i*3+2]);
        }

        private uint CoverColor(float position)
        {
            float p = Math.Max(0, Math.Min(1, position))*7;
            int lo = (int)p, hi = Math.Min(7, lo+1);
            float f = p-lo; f = f*f*(3-2*f);
            return AurLerpRgb(coverBands[lo], coverBands[hi], f);
        }

        private uint MixCoverColor(uint original, uint cover)
        { return coverWeight <= 0 ? original : (original & 0xFF000000u) | AurLerpRgb(original, cover, coverWeight); }

        private void TintCoverNebula(D3D9.Vtx[] quad, uint argb)
        {
            if (coverWeight <= 0) return;
            // Colores de vertices, sin nuevas texturas, lecturas de disco ni llamadas de dibujo.
            for (int i = 0; i < quad.Length; i++)
            {
                float x = Math.Max(0, Math.Min(1, quad[i].X / Math.Max(1, Width)));
                float y = Math.Max(0, Math.Min(1, quad[i].Y / Math.Max(1, Height)));
                quad[i].Color = MixCoverColor(argb, CoverColor(x*.7f+y*.3f));
            }
        }
    }
#endif
}

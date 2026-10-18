using System;
using System.Drawing;

namespace Rocola
{
    public partial class Form1
    {
        private bool paletaCompleta = true;
        private bool coloresFluidos = true;
#if ROCOLA_GPU
        // Solo al entrar una portada en la cache: 1024 muestras, 27 grupos, hasta 8 tonos.
        // Incluye blancos/grises/negros y colores minoritarios; no inventa saturacion.
        internal static Color[] ExtractCoverPalette(Bitmap bitmap)
        {
            if (bitmap == null) return new Color[0];
            int[] count = new int[27], red = new int[27], green = new int[27], blue = new int[27];
            int nx = Math.Min(32, bitmap.Width), ny = Math.Min(32, bitmap.Height), total = 0;
            int[] pixels = MuestrasPaleta(bitmap, nx, ny);
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    Color c = pixels != null ? Color.FromArgb(pixels[y*nx+x])
                        : bitmap.GetPixel((2*x+1)*bitmap.Width/(2*nx), (2*y+1)*bitmap.Height/(2*ny));
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
        // Misma rejilla/mismos bytes; sin 1024 cruces GDI+. PArgb translucido conserva
        // GetPixel: su redondeo de des-premultiplicacion no se adivina ni cambia la paleta.
        private static int[] MuestrasPaleta(Bitmap b, int nx, int ny)
        {
            System.Drawing.Imaging.PixelFormat f = b.PixelFormat;
            bool prem = f == System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
            bool rgb = f == System.Drawing.Imaging.PixelFormat.Format32bppRgb || f == System.Drawing.Imaging.PixelFormat.Format24bppRgb;
            if (!prem && !rgb && f != System.Drawing.Imaging.PixelFormat.Format32bppArgb) return null;
            int bytes = f == System.Drawing.Imaging.PixelFormat.Format24bppRgb ? 3 : 4;
            System.Drawing.Imaging.BitmapData d = null;
            try
            {
                d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, f);
                int[] r = new int[nx*ny];
                for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
                {
                    int px = (2*x+1)*b.Width/(2*nx), py = (2*y+1)*b.Height/(2*ny);
                    IntPtr p = new IntPtr(d.Scan0.ToInt64() + (long)py*d.Stride + px*bytes);
                    int a = rgb ? 255 : System.Runtime.InteropServices.Marshal.ReadByte(p, 3);
                    if (prem && a != 255) return null;
                    r[y*nx+x] = (a << 24) | (System.Runtime.InteropServices.Marshal.ReadByte(p, 2) << 16)
                        | (System.Runtime.InteropServices.Marshal.ReadByte(p, 1) << 8) | System.Runtime.InteropServices.Marshal.ReadByte(p);
                }
                return r;
            }
            catch { return null; }
            finally { if (d != null) b.UnlockBits(d); }
        }

        private void SyncGpuCoverPalette()
        {
            gpuPanel.SetStarGenre(nowPlaying!=null&&!nowPlaying.IsVideo ? GeneroDeCancion(nowPlaying.Song,nowPlaying.Disc) : null);
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
            AdvanceColorFlow(dt);
        }


        public bool ColoresFluidos=true;
        private readonly uint[] flowColors=new uint[64],flowGenre=new uint[4];
        private double flowPhase;
        private float flowBlend;
        private void AdvanceColorFlow(double dt)
        {
            flowBlend+=((ColoresFluidos?1f:0f)-flowBlend)*(float)(1-Math.Exp(-dt*3));
            if(flowBlend<.0001f)flowBlend=0;
            // Reloj integrado: cambiar de cancion o de intensidad no salta a otra region.
            // Un 46% mas rapido; solo espectro/estrella, no el cielo nocturno.
            flowPhase=(flowPhase+dt*(.038+.038*starDrive))%1.0;
            flowGenre[0]=NebColor&0xFFFFFFu;flowGenre[1]=auCuadro.Has?auCuadro.RgbA:NebColor2;
            flowGenre[2]=NebColor2&0xFFFFFFu;flowGenre[3]=auCuadro.Has?auCuadro.Rgb2A:NebColor;
            for(int i=0;i<flowColors.Length;i++){
                double p=flowPhase+i/(double)flowColors.Length;
                // En los extremos no participa una de las paletas. No omitir la tabla completa:
                // los ecos de la estrella tambien la consultan con ColoresFluidos apagado.
                if (coverWeight == 0f) flowColors[i] = PaletteFlow.Ring(flowGenre,p);
                else if (coverWeight == 1f) flowColors[i] = PaletteFlow.Ring(coverBands,p);
                else {
                    uint genre=PaletteFlow.Ring(flowGenre,p);
                    flowColors[i]=AurLerpRgb(genre,PaletteFlow.Ring(coverBands,p),coverWeight);
                }
            }
        }
        private uint FlowColor(uint original,float x,float y)
        {
            return flowBlend<=0?original:(original&0xFF000000u)|
                AurLerpRgb(original,PaletteFlow.Sample(flowColors,x,y),flowBlend);
        }
        private uint StarColorAt(float x,float y)
        {
            uint rgb=FlowColor(StarColor(),x/Math.Max(1,Width),y/Math.Max(1,Height));
            // Conserva lectura de las facetas incluso cuando la portada contiene negro puro.
            return 0xFF000000u|((72+((rgb>>16)&255)*183/255)<<16)|
                ((72+((rgb>>8)&255)*183/255)<<8)|(72+(rgb&255)*183/255);
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

    }
    internal static class PaletteFlow
    {
        internal static uint Mix(uint a,uint b,float f)
        {
            f=GpuPanel.SafeMoonLevel(f);
            // Por entero y no por "sin signo": los mismos numeros (0..255, f entre 0 y 1) sin la conversion lenta.
            return ((uint)(int)((int)((a>>16)&255)*(1-f)+(int)((b>>16)&255)*f)<<16)|
                ((uint)(int)((int)((a>>8)&255)*(1-f)+(int)((b>>8)&255)*f)<<8)|(uint)(int)((int)(a&255)*(1-f)+(int)(b&255)*f);
        }
        internal static uint Ring(uint[] colors,double position)
        {
            if(colors==null||colors.Length==0)return 0;
            if(double.IsNaN(position)||double.IsInfinity(position))position=0;
            double p=(position-Math.Floor(position))*colors.Length;
            int lo=Math.Min(colors.Length-1,(int)p);float f=(float)(p-lo);f=f*f*(3-2*f);
            return Mix(colors[lo],colors[(lo+1)%colors.Length],f);
        }
        internal static uint Sample(uint[] colors,float x,float y)
        {
            // Tabla pequena ya desplazada este cuadro; ninguna trigonometria por estrella/vertice.
            float p=(x*.80f+y*.27f)*colors.Length;
            int whole=(int)Math.Floor(p),lo=whole%colors.Length;if(lo<0)lo+=colors.Length;
            return Mix(colors[lo],colors[(lo+1)%colors.Length],p-whole);
        }
    }

#endif
}

using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la paleta se saca de los puntos de un SKBitmap con lo nativo (Xp/Nativo), no con GDI+; quien aun trae un Bitmap de la capa llega por el puente (Plataforma/Puente.pared.cs)

namespace Rocola
{
    public partial class Form1
    {
        private bool paletaCompleta = true;
        private bool coloresFluidos = true;
#if ROCOLA_GPU
        // Solo al entrar una portada en la cache: 1024 muestras, 27 grupos, hasta 8 tonos.
        // Incluye blancos/grises/negros y colores minoritarios; no inventa saturacion.
        internal static Color[] ExtractCoverPalette(SKBitmap bitmap, FormatoMapa formato)   // EN LINUX: los puntos de la portada (un SKBitmap, siempre BGRA premultiplicado) y el formato que tenia el Bitmap en XP (bitmap.PixelFormat: de el depende como se leen; Nativo/FormatoMapa.cs); quien aun pasa un Bitmap llega por el puente (Plataforma/Puente.pared.cs)
        {
            if (bitmap == null) return new Color[0];
            int[] count = new int[27], red = new int[27], green = new int[27], blue = new int[27];
            int width = bitmap.Width, height = bitmap.Height;
            int nx = Math.Min(32, width), ny = Math.Min(32, height), total = 0;
            int[] pixels = MuestrasPaleta(bitmap, formato, nx, ny);   // EN LINUX: con el formato
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    Color c = pixels != null ? Color.FromArgb(pixels[y*nx+x])
                        : ColorSk.DeSk(bitmap.GetPixel((2*x+1)*width/(2*nx), (2*y+1)*height/(2*ny)));   // EN LINUX: bitmap.GetPixel: el color sin premultiplicar de ese punto (SKBitmap.GetPixel) como Color
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
        private static int[] MuestrasPaleta(SKBitmap b, FormatoMapa f, int nx, int ny)   // EN LINUX: los puntos y el formato que tenia el Bitmap en XP
        {
            // EN LINUX: PixelFormat f = b.PixelFormat: llega como parametro (un SKBitmap no lo lleva)
            bool prem = f == FormatoMapa.PArgb32;   // EN LINUX: PixelFormat.Format32bppPArgb
            bool rgb = f == FormatoMapa.Rgb32 || f == FormatoMapa.Rgb24;   // EN LINUX: Format32bppRgb o Format24bppRgb
            if (!prem && !rgb && f != FormatoMapa.Argb32) return null;   // EN LINUX: Format32bppArgb
            int bytes = f == FormatoMapa.Rgb24 ? 3 : 4;   // EN LINUX: Format24bppRgb
            IntPtr d = IntPtr.Zero; int paso = 0; bool propia = false;   // EN LINUX: BitmapData d = null: el puntero a los puntos (Scan0), su paso (Stride) y si es una copia que hay que soltar
            try
            {
                int width = b.Width, height = b.Height;
                if (prem) { d = b.GetPixels(); paso = b.RowBytes; } else { FormatoPuntos fp = f == FormatoMapa.Rgb24 ? FormatoPuntos.Rgb24 : f == FormatoMapa.Rgb32 ? FormatoPuntos.Rgb32 : FormatoPuntos.Argb32; paso = Pixeles.Paso(fp, width); d = System.Runtime.InteropServices.Marshal.AllocHGlobal(new IntPtr(Math.Max(1L, (long)paso * height))); propia = true; Pixeles.Lee(b, new Rectangle(0, 0, width, height), fp, d, paso); }   // EN LINUX: b.LockBits(todo, ReadOnly, f): en Format32bppPArgb, los puntos mismos con el paso del mapa; en los demas, una copia convertida a ese formato (Nativo/Pixeles.cs: sin premultiplicar, o sin alfa de 32 o de 24 bits) con su paso
                int[] r = new int[nx*ny];
                for (int y = 0; y < ny; y++)
                {
                    int py = (2*y+1)*height/(2*ny);
                    long row = d.ToInt64() + (long)py*paso;   // EN LINUX: d.Scan0 y d.Stride
                    for (int x = 0; x < nx; x++)
                    {
                        int px = (2*x+1)*width/(2*nx);
                        IntPtr p = new IntPtr(row + px*bytes);
                        if (bytes == 4)
                        {
                            // Un pixel BGRA completo; RGB fuerza alfa opaco como GetPixel.
                            int pixel = System.Runtime.InteropServices.Marshal.ReadInt32(p);
                            if (prem && (uint)pixel >> 24 != 255) return null;
                            r[y*nx+x] = rgb ? pixel | unchecked((int)0xFF000000u) : pixel;
                        }
                        else
                            // Tres bytes exactos: no leer fuera de la ultima fila RGB de 24 bits.
                            r[y*nx+x] = unchecked((int)0xFF000000u) | (System.Runtime.InteropServices.Marshal.ReadByte(p, 2) << 16)
                                | (System.Runtime.InteropServices.Marshal.ReadByte(p, 1) << 8) | System.Runtime.InteropServices.Marshal.ReadByte(p);
                    }
                }
                return r;
            }
            catch { return null; }
            finally { if (propia) System.Runtime.InteropServices.Marshal.FreeHGlobal(d); }   // EN LINUX: b.UnlockBits(d): era de solo leer; solo se suelta la copia
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

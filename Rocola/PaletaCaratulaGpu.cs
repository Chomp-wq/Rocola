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
        private readonly D3D9.Vtx[] flowNebula=new D3D9.Vtx[8*3*6];
        private double flowPhase;
        private float flowBlend;
        private void AdvanceColorFlow(double dt)
        {
            flowBlend+=((ColoresFluidos?1f:0f)-flowBlend)*(float)(1-Math.Exp(-dt*3));
            if(flowBlend<.0001f)flowBlend=0;
            // Reloj integrado: cambiar de cancion o de intensidad no salta a otra region.
            flowPhase=(flowPhase+dt*(.026+.026*starDrive))%1.0;
            if(flowBlend<=0){if(stars!=null)stars.FlowBlend=0;return;}
            flowGenre[0]=NebColor&0xFFFFFFu;flowGenre[1]=auCuadro.Has?auCuadro.RgbA:NebColor2;
            flowGenre[2]=NebColor2&0xFFFFFFu;flowGenre[3]=auCuadro.Has?auCuadro.Rgb2A:NebColor;
            for(int i=0;i<flowColors.Length;i++){
                double p=flowPhase+i/(double)flowColors.Length;
                uint genre=PaletteFlow.Ring(flowGenre,p);
                flowColors[i]=AurLerpRgb(genre,PaletteFlow.Ring(coverBands,p),coverWeight);
            }
            if(stars!=null){stars.FlowPalette=flowColors;stars.FlowBlend=flowBlend;}
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
        private uint FlowNebulaColor(float x,float y,float u,float v)
        {
            uint a=nieblaQuad[0].Color,b=nieblaQuad[1].Color,c=nieblaQuad[2].Color,d=nieblaQuad[5].Color;
            // Reproduce los dos triangulos originales al apagar el flujo, sin salto de paleta.
            float t=Math.Max(u,v);
            uint side=u>=v?PaletteFlow.Mix(b,c,t>0?v/t:0):PaletteFlow.Mix(d,c,t>0?u/t:0);
            uint original=(a&0xFF000000u)|PaletteFlow.Mix(a,side,t);
            return FlowColor(original,x,y);
        }
        private void DrawFlowNebula(IntPtr texture,float u,float v)
        {
            // Misma textura, una sola llamada. La malla deja pasar los ocho colores por el centro,
            // no solo una interpolacion entre las cuatro esquinas del rectangulo.
            float left=nieblaQuad[0].X+.5f,top=nieblaQuad[0].Y+.5f;
            float right=nieblaQuad[2].X+.5f,bottom=nieblaQuad[2].Y+.5f;
            int n=0;
            for(int row=0;row<3;row++)for(int col=0;col<8;col++){
                float x0=left+(right-left)*col/8f,x1=left+(right-left)*(col+1)/8f;
                float y0=top+(bottom-top)*row/3f,y1=top+(bottom-top)*(row+1)/3f;
                float u0=u*col/8f,u1=u*(col+1)/8f,v0=v*row/3f,v1=v*(row+1)/3f;
                uint c0=FlowNebulaColor(x0/Math.Max(1,Width),y0/Math.Max(1,Height),col/8f,row/3f);
                uint c1=FlowNebulaColor(x1/Math.Max(1,Width),y0/Math.Max(1,Height),(col+1)/8f,row/3f);
                uint c2=FlowNebulaColor(x1/Math.Max(1,Width),y1/Math.Max(1,Height),(col+1)/8f,(row+1)/3f);
                uint c3=FlowNebulaColor(x0/Math.Max(1,Width),y1/Math.Max(1,Height),col/8f,(row+1)/3f);
                PaletteFlow.Vertex(flowNebula,n++,x0,y0,u0,v0,c0);
                PaletteFlow.Vertex(flowNebula,n++,x1,y0,u1,v0,c1);
                PaletteFlow.Vertex(flowNebula,n++,x1,y1,u1,v1,c2);
                PaletteFlow.Vertex(flowNebula,n++,x0,y0,u0,v0,c0);
                PaletteFlow.Vertex(flowNebula,n++,x1,y1,u1,v1,c2);
                PaletteFlow.Vertex(flowNebula,n++,x0,y1,u0,v1,c3);
            }
            stars.DrawTexturedBatch(texture,flowNebula,n/3);
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
internal static class PaletteFlow
    {
        internal static uint Mix(uint a,uint b,float f)
        {
            f=GpuPanel.SafeMoonLevel(f);
            return ((uint)(((a>>16)&255)*(1-f)+((b>>16)&255)*f)<<16)|
                ((uint)(((a>>8)&255)*(1-f)+((b>>8)&255)*f)<<8)|(uint)((a&255)*(1-f)+(b&255)*f);
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
        internal static void Vertex(D3D9.Vtx[] v,int i,float x,float y,float u,float vv,uint color)
        { v[i].X=x-.5f;v[i].Y=y-.5f;v[i].Z=0;v[i].Rhw=1;v[i].U=u;v[i].V=vv;v[i].Color=color; }
    }
    internal sealed partial class GpuStarfield
    {
        internal uint[] FlowPalette;
        internal float FlowBlend;
        private readonly D3D9.Vtx[] flowSky=new D3D9.Vtx[8*2*6];
        private uint FlowStarColor(uint rgb,float x,float y)
        {
            if(FlowBlend<=0||FlowPalette==null)return rgb;
            return PaletteFlow.Mix(rgb,PaletteFlow.Sample(FlowPalette,x,y),FlowBlend*.72f);
        }
        private uint FlowSkyColor(uint original,float x,float y)
        {
            uint c=PaletteFlow.Sample(FlowPalette,x,y);
            uint max=Math.Max((original>>16)&255,Math.Max((original>>8)&255,original&255));
            // Respeta la oscuridad del cielo: una portada blanca no lo convierte en una pared blanca.
            c=(((c>>16)&255)*max/255<<16)|(((c>>8)&255)*max/255<<8)|(c&255)*max/255;
            return 0xFF000000u|PaletteFlow.Mix(original,c,FlowBlend*.88f);
        }
        private void DrawFlowSky(int w,int h,float midY)
        {
            int n=0;
            for(int row=0;row<2;row++)for(int col=0;col<8;col++){
                float x0=w*col/8f,x1=w*(col+1)/8f,y0=row==0?0:midY,y1=row==0?midY:h;
                uint a=row==0?skyTop:skyMid,b=row==0?skyMid:skyBot;
                uint c0=FlowSkyColor(a,col/8f,y0/Math.Max(1,h)),c1=FlowSkyColor(a,(col+1)/8f,y0/Math.Max(1,h));
                uint c2=FlowSkyColor(b,(col+1)/8f,y1/Math.Max(1,h)),c3=FlowSkyColor(b,col/8f,y1/Math.Max(1,h));
                PaletteFlow.Vertex(flowSky,n++,x0,y0,0,0,c0);PaletteFlow.Vertex(flowSky,n++,x1,y0,0,0,c1);
                PaletteFlow.Vertex(flowSky,n++,x1,y1,0,0,c2);PaletteFlow.Vertex(flowSky,n++,x0,y0,0,0,c0);
                PaletteFlow.Vertex(flowSky,n++,x1,y1,0,0,c2);PaletteFlow.Vertex(flowSky,n++,x0,y1,0,0,c3);
            }
            gpu.DrawMesh(flowSky,n/3);
        }
    }

#endif
}

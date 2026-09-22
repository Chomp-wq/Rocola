#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // Silueta estelar, no esfera ruidosa: cuatro puntas largas, cuatro cortas
        // y nucleo solido. Una textura neutra por estilo, tintada en GPU.
        internal static Bitmap BakeStar(bool detailed)
        {
            const int n = MoonTextureSize;
            int[] pixels = new int[n*n];
            for (int y=0;y<n;y++) for (int x=0;x<n;x++)
            {
                double nx=(x-(n-1)*.5)/(n*.49), ny=(y-(n-1)*.5)/(n*.49);
                double r=Math.Sqrt(nx*nx+ny*ny);
                if(r>=1)continue;
                double axis=Math.Pow(Math.Abs(nx)/.94,.43)+Math.Pow(Math.Abs(ny)/.94,.43);
                double solid=Math.Max(0,Math.Min(1,(1-axis)*65))*(.30+.70*Math.Exp(-r*r/.12));
                double core=Math.Max(0,Math.Min(1,(.115-r)*180));
                double diagonal=0;
                if(detailed)
                {
                    double u=(nx+ny)*.70710678, v=(nx-ny)*.70710678;
                    diagonal=.64*Math.Max(0,Math.Min(1,(1-Math.Pow(Math.Abs(u)/.47,.43)-Math.Pow(Math.Abs(v)/.47,.43))*55))*Math.Exp(-r*r/.18);
                }
                double halo=(detailed?.26:.13)*Math.Exp(-r*r/ .065)*Math.Pow(1-r,2);
                double alpha=Math.Max(Math.Max(solid,core),Math.Max(diagonal,halo));
                double light=Math.Min(1,.50+.50*Math.Exp(-r*r/.055));
                int a=(int)Math.Round(255*alpha), c=(int)Math.Round(255*light);
                pixels[y*n+x]=unchecked((int)((uint)a<<24))|(c<<16)|(c<<8)|c;
            }
            Bitmap b=new Bitmap(n,n,PixelFormat.Format32bppArgb);
            BitmapData data=b.LockBits(new Rectangle(0,0,n,n),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<n;y++)Marshal.Copy(pixels,y*n,new IntPtr(data.Scan0.ToInt64()+y*(long)data.Stride),n); }
            finally { b.UnlockBits(data); }
            return b;
        }
        public bool EstelaEstelar = true;
        private struct StarEcho
        {
            internal double Time;
            internal float X,Y,Radius,Angle;
            internal uint Color;
        }
        private readonly StarEcho[] starEchoes=new StarEcho[8];
        private readonly D3D9.Vtx[] starEchoQuad=new D3D9.Vtx[6];
        private readonly D3D9.Vtx[] starEchoBatch=new D3D9.Vtx[8*6];
        private int starEchoHead,starEchoCount,starEchoWidth,starEchoHeight;
        private double starEchoLast=-1,starEchoClock=-1;
        private float starEchoVisibility;
        private void ResetStarEchoes()
        { starEchoHead=starEchoCount=0;starEchoLast=starEchoClock=-1;starEchoVisibility=0; }

        internal static float StarEchoAlpha(double age,float visibility)
        {
            if(double.IsNaN(age)||age<0||age>=1.6)return 0;
            float life=(float)(1-age/1.6);
            return .50f*SafeMoonLevel(visibility)*life;
        }
        private void UpdateStarEchoes(double now,float x,float y,float radius,float angle,uint color)
        {
            // Cambios de pantalla, pausas y reinicios no dejan fantasmas en otro lugar.
            if(starEchoWidth!=Width||starEchoHeight!=Height||now<starEchoClock||
                (starEchoClock>=0&&now-starEchoClock>.3))
            { ResetStarEchoes();starEchoWidth=Width;starEchoHeight=Height; }
            float dt=starEchoClock<0?0:(float)Math.Max(0,Math.Min(.1,now-starEchoClock));
            starEchoClock=now;
            float target=EstelaEstelar&&LunaActiva?SafeMoonLevel(idleBlend):0;
            starEchoVisibility+=(target-starEchoVisibility)*(1-(float)Math.Exp(-dt*8));
            if(target<=0)
            {
                if(starEchoVisibility<.002f){starEchoCount=0;starEchoLast=-1;}
                return;
            }
            if(starEchoLast>=0&&now-starEchoLast<.18)return;
            // Si no hubo desplazamiento real, no apilar siluetas en el mismo punto.
            if(starEchoCount>0)
            {
                StarEcho last=starEchoes[(starEchoHead+starEchoes.Length-1)%starEchoes.Length];
                float dx=x-last.X,dy=y-last.Y;
                if(dx*dx+dy*dy<1)return;
            }
            starEchoes[starEchoHead]=new StarEcho {Time=now,X=x,Y=y,Radius=radius,Angle=angle,Color=StarEchoColor(color,starEchoHead,1)};
            starEchoHead=(starEchoHead+1)%starEchoes.Length;
            starEchoCount=Math.Min(starEchoes.Length,starEchoCount+1);starEchoLast=now;
        }
        internal static uint StarEchoColor(uint color,int index,float alpha)
        {
            // Ecos espectrales mezclados con la portada; no un arcoiris ajeno al cielo.
            uint accent=index%3==0?0x71E6FFu:index%3==1?0xAE8CFFu:0xFF91CCu;
            uint r=(((color>>16)&255)*2+((accent>>16)&255)*3)/5;
            uint g=(((color>>8)&255)*2+((accent>>8)&255)*3)/5;
            uint b=((color&255)*2+(accent&255)*3)/5;
            return ((uint)(255*SafeMoonLevel(alpha))<<24)|(r<<16)|(g<<8)|b;
        }
        private static void RotateStarQuad(D3D9.Vtx[] quad,float x,float y,float angle)
        {
            float co=(float)Math.Cos(angle),si=(float)Math.Sin(angle);
            for(int i=0;i<quad.Length;i++)
            {
                float dx=quad[i].X-x,dy=quad[i].Y-y;
                quad[i].X=x+dx*co-dy*si;quad[i].Y=y+dx*si+dy*co;
            }
        }
        private void DrawStarEchoes(double now)
        {
            int vertices=0;
            for(int i=0;i<starEchoCount;i++)
            {
                StarEcho echo=starEchoes[(starEchoHead-starEchoCount+i+starEchoes.Length)%starEchoes.Length];
                double age=now-echo.Time;
                float alpha=StarEchoAlpha(age,starEchoVisibility);
                if(age<.055||alpha<.003f)continue;
                float r=echo.Radius*(.80f+.15f*(float)(1-age/1.6));
                uint tint=((uint)(alpha*255)<<24)|(echo.Color&0xFFFFFFu);
                SetTexQuadA(starEchoQuad,echo.X-r,echo.Y-r,echo.X+r,echo.Y+r,luU,luV,tint);
                RotateStarQuad(starEchoQuad,echo.X,echo.Y,echo.Angle);
                Array.Copy(starEchoQuad,0,starEchoBatch,vertices,6);vertices+=6;
            }
            if(vertices>0)stars.DrawTexturedBatch(lunaTex,starEchoBatch,vertices/3);
        }
        private uint StarColor()
        {
            float position=(float)(.5+.5*Math.Sin(moonMotionTime*.035));
            uint rgb=MixCoverColor(NebColor,CoverColor(position));
            uint r=(rgb>>16)&255,g=(rgb>>8)&255,b=rgb&255;
            return 0xFF000000u|((64+r*191/255)<<16)|((64+g*191/255)<<8)|(64+b*191/255);
        }
        internal static float IdleWallAlpha(float legacyAlpha)
        { return float.IsNaN(legacyAlpha) ? 1f : Math.Max(0f,Math.Min(1f,(legacyAlpha-.1f)/.9f)); }
    }
}
#endif

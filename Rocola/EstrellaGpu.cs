#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // Textura neutra preparada una vez: el color, giro y pulso viven en vertices.
        internal static Bitmap BakeStar(bool detailed)
        {
            const int n = MoonTextureSize;
            int[] pixels = new int[n*n];
            for (int y=0;y<n;y++) for (int x=0;x<n;x++)
            {
                double nx=(x-(n-1)*.5)/(n*.49), ny=(y-(n-1)*.5)/(n*.49);
                double r=Math.Sqrt(nx*nx+ny*ny);
                if (r>=1) continue;
                double alpha, light;
                if (r<.82)
                {
                    double depth=Math.Sqrt(Math.Max(0,1-r*r/(.82*.82)));
                    double grain=detailed ? .12*(CloudNoise((float)(nx*19+31),(float)(ny*19+23))-.5)
                        +.10*Math.Sin(nx*30+CloudNoise((float)(nx*7+9),(float)(ny*7+12))*5)*Math.Cos(ny*26) : 0;
                    light=Math.Max(.52,Math.Min(1,.68+.29*depth+grain));
                    alpha=1; // superficie opaca, incluso si el cielo es brillante
                }
                else
                {
                    double angle=Math.Atan2(ny,nx);
                    double rays=.35+.65*Math.Pow(.5+.5*Math.Cos(angle*(detailed?12:6)+Math.Sin(angle*3)),5);
                    double fall=(1-r)/.18;
                    alpha=fall*fall*(.12+.48*rays);
                    light=.95;
                }
                // Antialias en el limite de la superficie, sin transparentar su interior.
                if(r>=.812&&r<.82) alpha=.60+.40*(.82-r)/.008;
                int a=(int)Math.Round(255*alpha), c=(int)Math.Round(255*light);
                pixels[y*n+x]=unchecked((int)((uint)a<<24))|(c<<16)|(c<<8)|c;
            }
            Bitmap b=new Bitmap(n,n,PixelFormat.Format32bppArgb);
            BitmapData data=b.LockBits(new Rectangle(0,0,n,n),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<n;y++)Marshal.Copy(pixels,y*n,new IntPtr(data.Scan0.ToInt64()+y*(long)data.Stride),n); }
            finally { b.UnlockBits(data); }
            return b;
        }
        private uint StarColor()
        {
            // En reposo recorre suavemente los tonos de la caratula. Con el movimiento
            // apagado mantiene su posicion cromatica, pero sigue los cambios de cancion.
            float position=(float)(.5+.5*Math.Sin(moonMotionTime*.035));
            uint rgb=MixCoverColor(NebColor,CoverColor(position));
            uint r=(rgb>>16)&255,g=(rgb>>8)&255,b=rgb&255;
            // Un astro luminoso no puede volverse negro por una portada oscura.
            return 0xFF000000u|((64+r*191/255)<<16)|((64+g*191/255)<<8)|(64+b*191/255);
        }
        internal static float IdleWallAlpha(float legacyAlpha)
        { return float.IsNaN(legacyAlpha) ? 1f : Math.Max(0f,Math.Min(1f,(legacyAlpha-.1f)/.9f)); }
    }
}
#endif

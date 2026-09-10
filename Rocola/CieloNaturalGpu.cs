#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        private static float NoiseHash(int x,int y)
        {
            uint v=unchecked((uint)(x*374761393+y*668265263));
            v=unchecked((v^(v>>13))*1274126177u);
            return ((v^(v>>16))&65535)/65535f;
        }
        private static float CloudNoise(float x,float y)
        {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);
            float fx=x-ix,fy=y-iy; fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
            float a=NoiseHash(ix,iy),b=NoiseHash(ix+1,iy),c=NoiseHash(ix,iy+1),d=NoiseHash(ix+1,iy+1);
            return (a+(b-a)*fx)*(1-fy)+(c+(d-c)*fx)*fy;
        }
        internal static Bitmap BakeNatural(int w,int h)
        {
            if(w<1||h<1)throw new ArgumentOutOfRangeException("w");
            Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
            int[] pixels=new int[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float u=x/(float)Math.Max(1,w-1),v=y/(float)Math.Max(1,h-1);
                float warp=CloudNoise(u*4+7,v*4+3),n=0,amp=.52f,scale=5;
                for(int k=0;k<5;k++){n+=amp*CloudNoise(u*scale+warp*2,v*scale+warp+11);amp*=.5f;scale*=2.03f;}
                float ridge=1-Math.Abs(CloudNoise(u*27+warp*4,v*22+warp*3)*2-1);
                float envelope=(float)Math.Exp(-Math.Pow((v-(.35+.15*Math.Sin(u*6)))*2.4,2));
                float value=Math.Max(0,n-.28f)*envelope*(.6f+.4f*ridge*ridge);
                // La fuerza multiplica el VALOR y no el tope: asi la bruma ancha baja y los picos se
                // quedan donde estan, que es donde vive la forma de la nube. Bajando tambien el tope
                // saldria lo mismo pero todo mas oscuro, que no es lo que se quiere.
                int alpha=(int)Math.Min(125,value*180*Form1.NebFuerza*Form1.nebNaturalFuerza);
                pixels[y*w+x]=unchecked((int)((uint)alpha<<24|0x00FFFFFFu));
            }
            BitmapData data=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w,new IntPtr(data.Scan0.ToInt64()+y*(long)data.Stride),w);}
            finally{b.UnlockBits(data);}
            return b;
        }
        internal static float PerfilDefinido(float[] bands,float x)
        {
            if(bands==null||bands.Length<2)return 0;
            if(float.IsNaN(x))x=0;
            float t=Math.Max(0,Math.Min(1,x))*(bands.Length-1); int center=(int)t;float sum=0;
            // Solo center y center+1 pueden aportar: t cae dentro de [center,center+1), asi que
            // center-1 y center+2 quedan siempre a distancia 1 o mas y la guarda de abajo (d>=1) los
            // descarta SIEMPRE. Eran dos vueltas de cada cuatro que no podian sumar nada, y esto corre
            // una vez por columna de la aurora en cada cuadro.
            for(int i=center;i<=Math.Min(bands.Length-1,center+1);i++)
            {
                float d=Math.Abs(t-i)/.90f;if(d>=1)continue;
                float value=bands[i];if(!(value>0))continue;value=Math.Min(1,value);
                float k=1-d*d*(3-2*d);sum=Math.Max(sum,value*k);
            }
            return Math.Min(1,sum);
        }
    }
}
#endif
